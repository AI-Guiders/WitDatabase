using OutWit.Database.Core.Exceptions;

namespace OutWit.Database.Core.Concurrency
{
    /// <summary>
    /// Strategy for selecting the victim transaction when a deadlock is detected.
    /// </summary>
    public enum DeadlockVictimStrategy
    {
        /// <summary>
        /// Choose the youngest transaction (highest transaction ID) as victim.
        /// This is often the best choice as it has done the least work.
        /// </summary>
        Youngest,

        /// <summary>
        /// Choose the oldest transaction (lowest transaction ID) as victim.
        /// </summary>
        Oldest,

        /// <summary>
        /// Choose the transaction that would cause the least work to redo.
        /// Based on number of locks held (approximation of work done).
        /// </summary>
        LeastWork,

        /// <summary>
        /// Choose the transaction waiting for the most resources.
        /// </summary>
        MostWaiting
    }

    /// <summary>
    /// Detects deadlocks using a wait-for graph.
    /// Can be used for on-demand detection or periodic background detection.
    /// </summary>
    public sealed class DeadlockDetector : IDisposable
    {
        #region Fields

        private readonly WaitForGraph m_waitForGraph;
        private readonly IRowLockManager? m_lockManager;
        private readonly DeadlockVictimStrategy m_victimStrategy;
        private readonly Timer? m_backgroundTimer;
        private readonly Lock m_lock = new();
        private readonly Action<DeadlockException>? m_onDeadlockDetected;
        private bool m_disposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a deadlock detector with on-demand detection only.
        /// </summary>
        /// <param name="victimStrategy">Strategy for selecting victim.</param>
        public DeadlockDetector(DeadlockVictimStrategy victimStrategy = DeadlockVictimStrategy.Youngest)
            : this(null, victimStrategy, null, null)
        {
        }

        /// <summary>
        /// Creates a deadlock detector with optional background detection.
        /// </summary>
        /// <param name="lockManager">Lock manager to query for lock counts.</param>
        /// <param name="victimStrategy">Strategy for selecting victim.</param>
        /// <param name="detectionInterval">Interval for background detection (null = no background).</param>
        /// <param name="onDeadlockDetected">Callback when deadlock detected in background.</param>
        public DeadlockDetector(
            IRowLockManager? lockManager,
            DeadlockVictimStrategy victimStrategy = DeadlockVictimStrategy.Youngest,
            TimeSpan? detectionInterval = null,
            Action<DeadlockException>? onDeadlockDetected = null)
        {
            m_waitForGraph = new WaitForGraph();
            m_lockManager = lockManager;
            m_victimStrategy = victimStrategy;
            m_onDeadlockDetected = onDeadlockDetected;

            if (detectionInterval.HasValue)
            {
                m_backgroundTimer = new Timer(
                    BackgroundDetection,
                    null,
                    detectionInterval.Value,
                    detectionInterval.Value);
            }
        }

        #endregion

        #region Wait Registration

        /// <summary>
        /// Registers that a transaction is waiting for another transaction.
        /// </summary>
        /// <param name="waiterTxId">Transaction that is waiting.</param>
        /// <param name="holderTxId">Transaction that holds the resource.</param>
        /// <exception cref="DeadlockException">Thrown if this wait would create a deadlock.</exception>
        public void RegisterWait(long waiterTxId, long holderTxId)
        {
            ThrowIfDisposed();

            lock (m_lock)
            {
                // Check if this would create a cycle
                if (m_waitForGraph.WouldCreateCycle(waiterTxId, holderTxId))
                {
                    // Deadlock detected - find cycle and select victim
                    m_waitForGraph.AddEdge(waiterTxId, holderTxId);
                    var cycle = m_waitForGraph.FindCycle();
                    m_waitForGraph.RemoveEdge(waiterTxId, holderTxId);

                    if (cycle != null && cycle.Count > 0)
                    {
                        var victim = SelectVictim(cycle);
                        throw new DeadlockException(victim, cycle);
                    }
                }

                m_waitForGraph.AddEdge(waiterTxId, holderTxId);
            }
        }

        /// <summary>
        /// Registers that a transaction is waiting for multiple holders.
        /// </summary>
        /// <param name="waiterTxId">Transaction that is waiting.</param>
        /// <param name="holderTxIds">Transactions holding the resources.</param>
        /// <exception cref="DeadlockException">Thrown if this wait would create a deadlock.</exception>
        public void RegisterWait(long waiterTxId, IEnumerable<long> holderTxIds)
        {
            ThrowIfDisposed();

            foreach (var holderId in holderTxIds)
            {
                RegisterWait(waiterTxId, holderId);
            }
        }

        /// <summary>
        /// Unregisters a wait (transaction got the resource or gave up).
        /// </summary>
        /// <param name="waiterTxId">Transaction that was waiting.</param>
        /// <param name="holderTxId">Transaction that held the resource.</param>
        public void UnregisterWait(long waiterTxId, long holderTxId)
        {
            ThrowIfDisposed();

            lock (m_lock)
            {
                m_waitForGraph.RemoveEdge(waiterTxId, holderTxId);
            }
        }

        /// <summary>
        /// Removes all wait information for a transaction (when it completes).
        /// </summary>
        /// <param name="txId">Transaction ID.</param>
        public void TransactionCompleted(long txId)
        {
            ThrowIfDisposed();

            lock (m_lock)
            {
                m_waitForGraph.RemoveTransaction(txId);
            }
        }

        #endregion

        #region Detection

        /// <summary>
        /// Checks if there's currently a deadlock.
        /// </summary>
        /// <returns>True if a deadlock exists.</returns>
        public bool HasDeadlock()
        {
            ThrowIfDisposed();

            lock (m_lock)
            {
                return m_waitForGraph.HasCycle();
            }
        }

        /// <summary>
        /// Detects deadlock and returns the cycle if found.
        /// </summary>
        /// <returns>Cycle of transaction IDs, or null if no deadlock.</returns>
        public IReadOnlyList<long>? DetectDeadlock()
        {
            ThrowIfDisposed();

            lock (m_lock)
            {
                return m_waitForGraph.FindCycle();
            }
        }

        /// <summary>
        /// Detects deadlock and returns the recommended victim.
        /// </summary>
        /// <returns>Victim transaction ID, or null if no deadlock.</returns>
        public long? DetectAndSelectVictim()
        {
            ThrowIfDisposed();

            lock (m_lock)
            {
                var cycle = m_waitForGraph.FindCycle();
                if (cycle == null || cycle.Count == 0)
                    return null;

                return SelectVictim(cycle);
            }
        }

        /// <summary>
        /// Detects all deadlocks currently in the system.
        /// </summary>
        /// <returns>List of cycles.</returns>
        public IReadOnlyList<IReadOnlyList<long>> DetectAllDeadlocks()
        {
            ThrowIfDisposed();

            lock (m_lock)
            {
                return m_waitForGraph.FindAllCycles();
            }
        }

        #endregion

        #region Victim Selection

        private long SelectVictim(IReadOnlyList<long> cycle)
        {
            if (cycle.Count == 0)
                throw new ArgumentException("Cycle cannot be empty", nameof(cycle));

            return m_victimStrategy switch
            {
                DeadlockVictimStrategy.Youngest => cycle.Max(),
                DeadlockVictimStrategy.Oldest => cycle.Min(),
                DeadlockVictimStrategy.LeastWork => SelectByLeastWork(cycle),
                DeadlockVictimStrategy.MostWaiting => SelectByMostWaiting(cycle),
                _ => cycle.Max() // Default to youngest
            };
        }

        private long SelectByLeastWork(IReadOnlyList<long> cycle)
        {
            if (m_lockManager == null)
                return cycle.Max(); // Fall back to youngest

            // Transaction with fewest locks has done least work
            return cycle
                .OrderBy(txId => m_lockManager.GetLockedKeys(txId).Count)
                .ThenByDescending(txId => txId) // Tie-breaker: youngest
                .First();
        }

        private long SelectByMostWaiting(IReadOnlyList<long> cycle)
        {
            // Transaction waiting for most resources
            return cycle
                .OrderByDescending(txId => m_waitForGraph.GetWaitingFor(txId).Count)
                .ThenByDescending(txId => txId) // Tie-breaker: youngest
                .First();
        }

        #endregion

        #region Background Detection

        private void BackgroundDetection(object? state)
        {
            if (m_disposed)
                return;

            try
            {
                lock (m_lock)
                {
                    var cycle = m_waitForGraph.FindCycle();
                    if (cycle != null && cycle.Count > 0)
                    {
                        var victim = SelectVictim(cycle);
                        var exception = new DeadlockException(victim, cycle);
                        m_onDeadlockDetected?.Invoke(exception);
                    }
                }
            }
            catch
            {
                // Ignore errors in background detection
            }
        }

        #endregion

        #region Query

        /// <summary>
        /// Gets the underlying wait-for graph.
        /// </summary>
        public WaitForGraph WaitForGraph => m_waitForGraph;

        /// <summary>
        /// Gets the number of waiting transactions.
        /// </summary>
        public int WaitingTransactionCount
        {
            get
            {
                lock (m_lock)
                {
                    return m_waitForGraph.EdgeCount > 0 
                        ? m_waitForGraph.NodeCount 
                        : 0;
                }
            }
        }

        #endregion

        #region Tools

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(m_disposed, this);
        }

        #endregion

        #region IDisposable

        /// <inheritdoc/>
        public void Dispose()
        {
            if (m_disposed) return;
            m_disposed = true;

            m_backgroundTimer?.Dispose();
            m_waitForGraph.Clear();
        }

        #endregion
    }
}

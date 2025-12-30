using System.Collections.Concurrent;

namespace OutWit.Database.Core.Tree;

/// <summary>
/// Manages latches for BTree pages.
/// Provides page-level locking for concurrent access.
/// </summary>
/// <remarks>
/// Features:
/// - Lazy latch creation (only when needed)
/// - Automatic cleanup of unused latches
/// - Statistics tracking
/// </remarks>
public sealed class PageLatchManager : IDisposable
{
    #region Constants

    private const int DEFAULT_INITIAL_CAPACITY = 256;
    private const int CLEANUP_THRESHOLD = 1000;

    #endregion

    #region Fields

    private readonly ConcurrentDictionary<uint, PageLatch> m_latches;
    private readonly Lock m_cleanupLock = new();
    private long m_acquireCount;
    private long m_releaseCount;
    private long m_contentionCount;
    private int m_cleanupCounter;
    private bool m_disposed;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a new page latch manager.
    /// </summary>
    /// <param name="initialCapacity">Initial capacity for latch dictionary.</param>
    public PageLatchManager(int initialCapacity = DEFAULT_INITIAL_CAPACITY)
    {
        m_latches = new ConcurrentDictionary<uint, PageLatch>(
            concurrencyLevel: Environment.ProcessorCount,
            capacity: initialCapacity);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Gets or creates a latch for the specified page.
    /// </summary>
    /// <param name="pageNumber">The page number.</param>
    /// <returns>The page latch.</returns>
    public PageLatch GetLatch(uint pageNumber)
    {
        ThrowIfDisposed();
        return m_latches.GetOrAdd(pageNumber, pn => new PageLatch(pn));
    }

    /// <summary>
    /// Acquires a shared (read) latch on a page.
    /// </summary>
    /// <param name="pageNumber">The page number.</param>
    /// <returns>A handle that releases the latch when disposed.</returns>
    public LatchHandle AcquireShared(uint pageNumber)
    {
        ThrowIfDisposed();
        
        var latch = GetLatch(pageNumber);
        
        // Track contention
        if (latch.IsWriteLockHeld || latch.WaitingWriteCount > 0)
        {
            Interlocked.Increment(ref m_contentionCount);
        }
        
        latch.AcquireShared();
        Interlocked.Increment(ref m_acquireCount);
        
        IncrementCleanupCounter();
        
        return new LatchHandle(this, pageNumber, isExclusive: false);
    }

    /// <summary>
    /// Tries to acquire a shared latch without blocking.
    /// </summary>
    /// <param name="pageNumber">The page number.</param>
    /// <param name="handle">The latch handle if acquired.</param>
    /// <returns>True if latch acquired.</returns>
    public bool TryAcquireShared(uint pageNumber, out LatchHandle handle)
    {
        ThrowIfDisposed();
        
        var latch = GetLatch(pageNumber);
        
        if (latch.TryAcquireShared())
        {
            Interlocked.Increment(ref m_acquireCount);
            handle = new LatchHandle(this, pageNumber, isExclusive: false);
            return true;
        }
        
        Interlocked.Increment(ref m_contentionCount);
        handle = default;
        return false;
    }

    /// <summary>
    /// Acquires an exclusive (write) latch on a page.
    /// </summary>
    /// <param name="pageNumber">The page number.</param>
    /// <returns>A handle that releases the latch when disposed.</returns>
    public LatchHandle AcquireExclusive(uint pageNumber)
    {
        ThrowIfDisposed();
        
        var latch = GetLatch(pageNumber);
        
        // Track contention
        if (latch.IsReadLockHeld || latch.IsWriteLockHeld || 
            latch.CurrentReadCount > 0 || latch.WaitingWriteCount > 0)
        {
            Interlocked.Increment(ref m_contentionCount);
        }
        
        latch.AcquireExclusive();
        Interlocked.Increment(ref m_acquireCount);
        
        IncrementCleanupCounter();
        
        return new LatchHandle(this, pageNumber, isExclusive: true);
    }

    /// <summary>
    /// Tries to acquire an exclusive latch without blocking.
    /// </summary>
    /// <param name="pageNumber">The page number.</param>
    /// <param name="handle">The latch handle if acquired.</param>
    /// <returns>True if latch acquired.</returns>
    public bool TryAcquireExclusive(uint pageNumber, out LatchHandle handle)
    {
        ThrowIfDisposed();
        
        var latch = GetLatch(pageNumber);
        
        if (latch.TryAcquireExclusive())
        {
            Interlocked.Increment(ref m_acquireCount);
            handle = new LatchHandle(this, pageNumber, isExclusive: true);
            return true;
        }
        
        Interlocked.Increment(ref m_contentionCount);
        handle = default;
        return false;
    }

    /// <summary>
    /// Releases a latch on a page.
    /// </summary>
    /// <param name="pageNumber">The page number.</param>
    /// <param name="isExclusive">True if exclusive latch, false if shared.</param>
    internal void Release(uint pageNumber, bool isExclusive)
    {
        if (m_disposed) return;
        
        if (m_latches.TryGetValue(pageNumber, out var latch))
        {
            if (isExclusive)
            {
                latch.ReleaseExclusive();
            }
            else
            {
                latch.ReleaseShared();
            }
            
            Interlocked.Increment(ref m_releaseCount);
        }
    }

    /// <summary>
    /// Removes unused latches to free memory.
    /// </summary>
    public void Cleanup()
    {
        ThrowIfDisposed();
        
        lock (m_cleanupLock)
        {
            var toRemove = new List<uint>();
            
            foreach (var (pageNumber, latch) in m_latches)
            {
                // Only remove latches that are not in use
                if (!latch.IsReadLockHeld && !latch.IsWriteLockHeld &&
                    latch.CurrentReadCount == 0 &&
                    latch.WaitingReadCount == 0 && latch.WaitingWriteCount == 0)
                {
                    toRemove.Add(pageNumber);
                }
            }
            
            foreach (var pageNumber in toRemove)
            {
                if (m_latches.TryRemove(pageNumber, out var removedLatch))
                {
                    removedLatch.Dispose();
                }
            }
        }
    }

    #endregion

    #region Statistics

    /// <summary>
    /// Gets the number of latches currently tracked.
    /// </summary>
    public int LatchCount => m_latches.Count;

    /// <summary>
    /// Gets the total number of latch acquisitions.
    /// </summary>
    public long AcquireCount => Volatile.Read(ref m_acquireCount);

    /// <summary>
    /// Gets the total number of latch releases.
    /// </summary>
    public long ReleaseCount => Volatile.Read(ref m_releaseCount);

    /// <summary>
    /// Gets the number of times contention was detected.
    /// </summary>
    public long ContentionCount => Volatile.Read(ref m_contentionCount);

    /// <summary>
    /// Gets the contention ratio (0.0 = no contention, 1.0 = always contended).
    /// </summary>
    public double ContentionRatio
    {
        get
        {
            var acquires = AcquireCount;
            return acquires > 0 ? (double)ContentionCount / acquires : 0;
        }
    }

    #endregion

    #region Private Methods

    private void IncrementCleanupCounter()
    {
        var count = Interlocked.Increment(ref m_cleanupCounter);
        if (count >= CLEANUP_THRESHOLD)
        {
            if (Interlocked.CompareExchange(ref m_cleanupCounter, 0, count) == count)
            {
                // Only one thread will do cleanup
                Task.Run(Cleanup);
            }
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(m_disposed, this);
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (m_disposed) return;
        m_disposed = true;

        foreach (var (_, latch) in m_latches)
        {
            latch.Dispose();
        }
        m_latches.Clear();
    }

    #endregion

    #region Nested Types

    /// <summary>
    /// Handle for a held latch. Releases the latch when disposed.
    /// </summary>
    public readonly struct LatchHandle : IDisposable
    {
        private readonly PageLatchManager? m_manager;
        private readonly uint m_pageNumber;
        private readonly bool m_isExclusive;

        internal LatchHandle(PageLatchManager manager, uint pageNumber, bool isExclusive)
        {
            m_manager = manager;
            m_pageNumber = pageNumber;
            m_isExclusive = isExclusive;
        }

        /// <summary>
        /// Gets the page number this handle is for.
        /// </summary>
        public uint PageNumber => m_pageNumber;

        /// <summary>
        /// Gets whether this is an exclusive latch.
        /// </summary>
        public bool IsExclusive => m_isExclusive;

        /// <summary>
        /// Gets whether this handle is valid (has a manager).
        /// </summary>
        public bool IsValid => m_manager != null;

        /// <summary>
        /// Releases the latch.
        /// </summary>
        public void Dispose()
        {
            m_manager?.Release(m_pageNumber, m_isExclusive);
        }
    }

    #endregion
}

using OutWit.Database.Core.Interfaces;

namespace OutWit.Database.Core.Query
{
    /// <summary>
    /// Default implementation of query execution context.
    /// Provides mutable state for tracking query execution results.
    /// </summary>
    public sealed class QueryContext : IQueryContext
    {
        #region Constants

        private const long NOT_APPLICABLE = -1;

        #endregion

        #region Fields

        private readonly CancellationTokenSource? m_timeoutCts;
        private readonly CancellationTokenSource m_linkedCts;
        private long m_affectedRows;
        private long m_lastInsertId;
        private bool m_isTimedOut;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new query context with no timeout.
        /// </summary>
        public QueryContext()
            : this(0, CancellationToken.None)
        {
        }

        /// <summary>
        /// Creates a new query context with the specified cancellation token.
        /// </summary>
        /// <param name="cancellationToken">External cancellation token.</param>
        public QueryContext(CancellationToken cancellationToken)
            : this(0, cancellationToken)
        {
        }

        /// <summary>
        /// Creates a new query context with the specified timeout.
        /// </summary>
        /// <param name="timeoutMilliseconds">Timeout in milliseconds. 0 means no timeout.</param>
        public QueryContext(int timeoutMilliseconds)
            : this(timeoutMilliseconds, CancellationToken.None)
        {
        }

        /// <summary>
        /// Creates a new query context with the specified timeout and cancellation token.
        /// </summary>
        /// <param name="timeoutMilliseconds">Timeout in milliseconds. 0 means no timeout.</param>
        /// <param name="cancellationToken">External cancellation token.</param>
        public QueryContext(int timeoutMilliseconds, CancellationToken cancellationToken)
        {
            TimeoutMilliseconds = timeoutMilliseconds;
            m_affectedRows = NOT_APPLICABLE;
            m_lastInsertId = NOT_APPLICABLE;

            if (timeoutMilliseconds > 0)
            {
                m_timeoutCts = new CancellationTokenSource(timeoutMilliseconds);
                m_linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    m_timeoutCts.Token, cancellationToken);
                
                // Register callback to track timeout
                m_timeoutCts.Token.Register(() => m_isTimedOut = true);
            }
            else
            {
                m_linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            }
        }

        #endregion

        #region SetAffectedRows

        /// <summary>
        /// Sets the number of affected rows.
        /// </summary>
        /// <param name="count">The number of affected rows.</param>
        public void SetAffectedRows(long count)
        {
            m_affectedRows = count;
        }

        /// <summary>
        /// Increments the affected rows counter.
        /// </summary>
        /// <param name="delta">The amount to increment by. Default is 1.</param>
        public void IncrementAffectedRows(long delta = 1)
        {
            if (m_affectedRows == NOT_APPLICABLE)
                m_affectedRows = 0;
            
            Interlocked.Add(ref m_affectedRows, delta);
        }

        #endregion

        #region SetLastInsertId

        /// <summary>
        /// Sets the last insert ID.
        /// </summary>
        /// <param name="id">The last generated ID.</param>
        public void SetLastInsertId(long id)
        {
            m_lastInsertId = id;
        }

        #endregion

        #region Cancel

        /// <summary>
        /// Cancels the query execution.
        /// </summary>
        public void Cancel()
        {
            m_linkedCts.Cancel();
        }

        #endregion

        #region ThrowIfCancellationRequested

        /// <summary>
        /// Throws an OperationCanceledException if cancellation has been requested.
        /// </summary>
        public void ThrowIfCancellationRequested()
        {
            CancellationToken.ThrowIfCancellationRequested();
        }

        #endregion

        #region Reset

        /// <summary>
        /// Resets the context state for reuse.
        /// </summary>
        public void Reset()
        {
            m_affectedRows = NOT_APPLICABLE;
            m_lastInsertId = NOT_APPLICABLE;
        }

        #endregion

        #region IQueryContext

        /// <inheritdoc/>
        public long AffectedRows => Interlocked.Read(ref m_affectedRows);

        /// <inheritdoc/>
        public long LastInsertId => Interlocked.Read(ref m_lastInsertId);

        /// <inheritdoc/>
        public int TimeoutMilliseconds { get; }

        /// <inheritdoc/>
        public CancellationToken CancellationToken => m_linkedCts.Token;

        /// <inheritdoc/>
        public bool IsCancelled => m_linkedCts.IsCancellationRequested;

        /// <inheritdoc/>
        public bool IsTimedOut => m_isTimedOut;

        #endregion
    }
}

namespace OutWit.Database.Core.Concurrency
{
    /// <summary>
    /// Provides thread-safe database locking with configurable timeout.
    /// Supports shared (read) and exclusive (write) locks.
    /// Includes both synchronous and asynchronous APIs.
    /// </summary>
    public sealed class DatabaseLock : IDisposable
    {
        #region Constants

        /// <summary>
        /// Gets the default lock timeout.
        /// </summary>
        internal static readonly TimeSpan DEFAULT_TIMEOUT = TimeSpan.FromSeconds(5);

        #endregion

        #region Fields

        private readonly SemaphoreSlim m_writeSemaphore = new(1, 1);
        private readonly SemaphoreSlim m_readCountLock = new(1, 1);
        private readonly ReaderWriterLockSlim m_syncLock;
        private readonly TimeSpan m_lockTimeout;
        private int m_readerCount;
        private bool m_disposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new database lock with the specified timeout.
        /// </summary>
        /// <param name="lockTimeout">Maximum time to wait for lock acquisition.</param>
        public DatabaseLock(TimeSpan? lockTimeout = null)
        {
            m_lockTimeout = lockTimeout ?? DEFAULT_TIMEOUT;
            m_syncLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        }

        #endregion

        #region ReadLock

        /// <summary>
        /// Acquires a shared (read) lock.
        /// Multiple readers can hold shared locks simultaneously.
        /// </summary>
        /// <returns>A disposable handle that releases the lock when disposed.</returns>
        /// <exception cref="TimeoutException">Thrown if the lock cannot be acquired within the timeout.</exception>
        public IDisposable AcquireReadLock()
        {
            ThrowIfDisposed();
        
            if (!m_syncLock.TryEnterReadLock(m_lockTimeout))
                throw new TimeoutException($"Could not acquire read lock within {m_lockTimeout.TotalSeconds:F1} seconds. Database is locked.");
        
            return new LockHandleSync(() => m_syncLock.ExitReadLock());
        }

        /// <summary>
        /// Acquires a shared (read) lock asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An async disposable handle that releases the lock when disposed.</returns>
        public async Task<IAsyncDisposable> AcquireReadLockAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(m_lockTimeout);

            try
            {
                await m_readCountLock.WaitAsync(cts.Token).ConfigureAwait(false);
                try
                {
                    m_readerCount++;
                    if (m_readerCount == 1)
                    {
                        // First reader blocks writers
                        await m_writeSemaphore.WaitAsync(cts.Token).ConfigureAwait(false);
                    }
                }
                finally
                {
                    m_readCountLock.Release();
                }

                return new LockHandleAsync(async () =>
                {
                    await m_readCountLock.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        m_readerCount--;
                        if (m_readerCount == 0)
                        {
                            m_writeSemaphore.Release();
                        }
                    }
                    finally
                    {
                        m_readCountLock.Release();
                    }
                });
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Could not acquire read lock within {m_lockTimeout.TotalSeconds:F1} seconds. Database is locked.");
            }
        }

        #endregion

        #region WriteLock

        /// <summary>
        /// Acquires an exclusive (write) lock.
        /// Only one writer can hold the lock, and no readers are allowed.
        /// </summary>
        /// <returns>A disposable handle that releases the lock when disposed.</returns>
        /// <exception cref="TimeoutException">Thrown if the lock cannot be acquired within the timeout.</exception>
        public IDisposable AcquireWriteLock()
        {
            ThrowIfDisposed();
        
            if (!m_syncLock.TryEnterWriteLock(m_lockTimeout))
                throw new TimeoutException($"Could not acquire write lock within {m_lockTimeout.TotalSeconds:F1} seconds. Database is locked.");
        
            return new LockHandleSync(() => m_syncLock.ExitWriteLock());
        }


        /// <summary>
        /// Acquires an exclusive (write) lock asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An async disposable handle that releases the lock when disposed.</returns>
        public async Task<IAsyncDisposable> AcquireWriteLockAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(m_lockTimeout);

            try
            {
                await m_writeSemaphore.WaitAsync(cts.Token).ConfigureAwait(false);
                return new LockHandleAsync(() =>
                {
                    m_writeSemaphore.Release();
                    return ValueTask.CompletedTask;
                });
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Could not acquire write lock within {m_lockTimeout.TotalSeconds:F1} seconds. Database is locked.");
            }
        }

        #endregion

        #region Functions

        /// <summary>
        /// Acquires an upgradeable read lock.
        /// Can be upgraded to write lock without releasing.
        /// </summary>
        public IDisposable AcquireUpgradeableReadLock()
        {
            ThrowIfDisposed();
        
            if (!m_syncLock.TryEnterUpgradeableReadLock(m_lockTimeout))
                throw new TimeoutException($"Could not acquire upgradeable lock within {m_lockTimeout.TotalSeconds:F1} seconds. Database is locked.");
        
            return new LockHandleSync(() => m_syncLock.ExitUpgradeableReadLock());
        }

        #endregion

        #region Tools

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
            m_syncLock.Dispose();
            m_writeSemaphore.Dispose();
            m_readCountLock.Dispose();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the current number of threads waiting to acquire a read lock.
        /// </summary>
        public int WaitingReadCount => m_syncLock.WaitingReadCount;

        /// <summary>
        /// Gets the current number of threads waiting to acquire a write lock.
        /// </summary>
        public int WaitingWriteCount => m_syncLock.WaitingWriteCount;

        /// <summary>
        /// Gets whether any thread holds a read lock.
        /// </summary>
        public bool IsReadLockHeld => m_syncLock.IsReadLockHeld;

        /// <summary>
        /// Gets whether any thread holds a write lock.
        /// </summary>
        public bool IsWriteLockHeld => m_syncLock.IsWriteLockHeld;

        #endregion

    }
}

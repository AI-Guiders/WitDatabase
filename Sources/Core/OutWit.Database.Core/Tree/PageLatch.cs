namespace OutWit.Database.Core.Tree;

/// <summary>
/// Represents a latch (lightweight lock) on a BTree page.
/// Supports shared (read) and exclusive (write) access modes.
/// </summary>
/// <remarks>
/// Latches are short-term locks held only during page access.
/// They differ from database locks which are held for transaction duration.
/// 
/// Latch coupling (crabbing) protocol:
/// 1. Acquire latch on parent
/// 2. Acquire latch on child  
/// 3. Release parent latch if child is safe (won't split/merge)
/// </remarks>
public sealed class PageLatch : IDisposable
{
    #region Fields

    private readonly ReaderWriterLockSlim m_lock;
    private readonly uint m_pageNumber;
    private bool m_disposed;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a new page latch.
    /// </summary>
    /// <param name="pageNumber">The page number this latch protects.</param>
    public PageLatch(uint pageNumber)
    {
        m_pageNumber = pageNumber;
        m_lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
    }

    #endregion

    #region Latch Operations

    /// <summary>
    /// Acquires a shared (read) latch. Multiple readers allowed.
    /// </summary>
    public void AcquireShared()
    {
        ThrowIfDisposed();
        m_lock.EnterReadLock();
    }

    /// <summary>
    /// Tries to acquire a shared latch without blocking.
    /// </summary>
    /// <returns>True if latch acquired.</returns>
    public bool TryAcquireShared()
    {
        ThrowIfDisposed();
        return m_lock.TryEnterReadLock(0);
    }

    /// <summary>
    /// Tries to acquire a shared latch with timeout.
    /// </summary>
    /// <param name="timeout">Maximum time to wait.</param>
    /// <returns>True if latch acquired.</returns>
    public bool TryAcquireShared(TimeSpan timeout)
    {
        ThrowIfDisposed();
        return m_lock.TryEnterReadLock(timeout);
    }

    /// <summary>
    /// Releases a shared (read) latch.
    /// </summary>
    public void ReleaseShared()
    {
        ThrowIfDisposed();
        m_lock.ExitReadLock();
    }

    /// <summary>
    /// Acquires an exclusive (write) latch. No other readers or writers allowed.
    /// </summary>
    public void AcquireExclusive()
    {
        ThrowIfDisposed();
        m_lock.EnterWriteLock();
    }

    /// <summary>
    /// Tries to acquire an exclusive latch without blocking.
    /// </summary>
    /// <returns>True if latch acquired.</returns>
    public bool TryAcquireExclusive()
    {
        ThrowIfDisposed();
        return m_lock.TryEnterWriteLock(0);
    }

    /// <summary>
    /// Tries to acquire an exclusive latch with timeout.
    /// </summary>
    /// <param name="timeout">Maximum time to wait.</param>
    /// <returns>True if latch acquired.</returns>
    public bool TryAcquireExclusive(TimeSpan timeout)
    {
        ThrowIfDisposed();
        return m_lock.TryEnterWriteLock(timeout);
    }

    /// <summary>
    /// Releases an exclusive (write) latch.
    /// </summary>
    public void ReleaseExclusive()
    {
        ThrowIfDisposed();
        m_lock.ExitWriteLock();
    }

    /// <summary>
    /// Upgrades a shared latch to exclusive.
    /// Must already hold a shared latch.
    /// </summary>
    public void UpgradeToExclusive()
    {
        ThrowIfDisposed();
        m_lock.EnterUpgradeableReadLock();
        m_lock.EnterWriteLock();
    }

    /// <summary>
    /// Downgrades an exclusive latch to shared.
    /// </summary>
    public void DowngradeToShared()
    {
        ThrowIfDisposed();
        // ReaderWriterLockSlim doesn't support direct downgrade
        // This is a limitation - caller should release exclusive and acquire shared
        throw new NotSupportedException("Direct downgrade not supported. Release exclusive and acquire shared.");
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the page number this latch protects.
    /// </summary>
    public uint PageNumber => m_pageNumber;

    /// <summary>
    /// Gets whether any thread holds a read latch.
    /// </summary>
    public bool IsReadLockHeld => m_lock.IsReadLockHeld;

    /// <summary>
    /// Gets whether any thread holds a write latch.
    /// </summary>
    public bool IsWriteLockHeld => m_lock.IsWriteLockHeld;

    /// <summary>
    /// Gets the current number of readers.
    /// </summary>
    public int CurrentReadCount => m_lock.CurrentReadCount;

    /// <summary>
    /// Gets the number of threads waiting for read access.
    /// </summary>
    public int WaitingReadCount => m_lock.WaitingReadCount;

    /// <summary>
    /// Gets the number of threads waiting for write access.
    /// </summary>
    public int WaitingWriteCount => m_lock.WaitingWriteCount;

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

        m_lock.Dispose();
    }

    #endregion
}

namespace OutWit.Database.Core.Concurrency;

/// <summary>
/// Provides file-level locking for multi-process synchronization.
/// Uses a dedicated lock file to coordinate access.
/// Features exponential backoff for retry and cleanup on close.
/// </summary>
public sealed class FileLock : IDisposable
{
    #region Constants

    private const int INITIAL_RETRY_DELAY_MS = 10;
    private const int MAX_RETRY_DELAY_MS = 500;

    #endregion

    #region Fields

    private readonly string m_lockFilePath;
    private readonly TimeSpan m_timeout;
    private FileStream? m_lockFile;
    private bool m_disposed;
    private bool m_hasExclusiveLock;
    private bool m_hasSharedLock;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a file lock for the specified database path.
    /// </summary>
    /// <param name="databasePath">Path to the database file.</param>
    /// <param name="timeout">Lock acquisition timeout.</param>
    public FileLock(string databasePath, TimeSpan? timeout = null)
    {
        m_lockFilePath = databasePath + ".lock";
        m_timeout = timeout ?? DatabaseLock.DEFAULT_TIMEOUT;
    }

    #endregion

    #region SharedLock

    /// <summary>
    /// Acquires a shared (read) lock on the database file.
    /// Multiple processes can hold shared locks.
    /// Uses exponential backoff for retries.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for the lock (overrides constructor timeout).</param>
    /// <exception cref="TimeoutException">Thrown if the lock cannot be acquired.</exception>
    public void AcquireSharedLock(TimeSpan? timeout = null)
    {
        ThrowIfDisposed();
        
        var effectiveTimeout = timeout ?? m_timeout;
        var deadline = DateTime.UtcNow + effectiveTimeout;
        var delay = INITIAL_RETRY_DELAY_MS;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                m_lockFile = new FileStream(
                    m_lockFilePath,
                    FileMode.OpenOrCreate,
                    FileAccess.Read,
                    FileShare.Read,
                    1,
                    FileOptions.None);
                
                m_hasSharedLock = true;
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(delay);
                delay = Math.Min(delay * 2, MAX_RETRY_DELAY_MS);
            }
        }

        throw new TimeoutException($"Could not acquire shared file lock within {effectiveTimeout.TotalSeconds:F1} seconds.");
    }


    /// <summary>
    /// Acquires a shared lock asynchronously with exponential backoff.
    /// </summary>
    public async Task AcquireSharedLockAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var effectiveTimeout = timeout ?? m_timeout;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(effectiveTimeout);

        var delay = INITIAL_RETRY_DELAY_MS;

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                m_lockFile = new FileStream(
                    m_lockFilePath,
                    FileMode.OpenOrCreate,
                    FileAccess.Read,
                    FileShare.Read,
                    1,
                    FileOptions.Asynchronous);

                m_hasSharedLock = true;
                return;
            }
            catch (IOException)
            {
                await Task.Delay(delay, cts.Token).ConfigureAwait(false);
                delay = Math.Min(delay * 2, MAX_RETRY_DELAY_MS);
            }
        }

        throw new TimeoutException($"Could not acquire shared file lock within {effectiveTimeout.TotalSeconds:F1} seconds.");
    }

    #endregion

    #region ExclusiveLock

    /// <summary>
    /// Acquires an exclusive (write) lock on the database file.
    /// No other processes can access while this lock is held.
    /// Uses exponential backoff for retries.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for the lock (overrides constructor timeout).</param>
    /// <exception cref="TimeoutException">Thrown if the lock cannot be acquired.</exception>
    public void AcquireExclusiveLock(TimeSpan? timeout = null)
    {
        ThrowIfDisposed();
        
        var effectiveTimeout = timeout ?? m_timeout;
        var deadline = DateTime.UtcNow + effectiveTimeout;
        var delay = INITIAL_RETRY_DELAY_MS;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                m_lockFile = new FileStream(
                    m_lockFilePath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    1,
                    FileOptions.None);
                
                m_hasExclusiveLock = true;
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(delay);
                delay = Math.Min(delay * 2, MAX_RETRY_DELAY_MS);
            }
        }

        throw new TimeoutException($"Could not acquire exclusive file lock within {effectiveTimeout.TotalSeconds:F1} seconds.");
    }

    /// <summary>
    /// Acquires an exclusive lock asynchronously with exponential backoff.
    /// </summary>
    public async Task AcquireExclusiveLockAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        var effectiveTimeout = timeout ?? m_timeout;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(effectiveTimeout);
        
        var delay = INITIAL_RETRY_DELAY_MS;

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                m_lockFile = new FileStream(
                    m_lockFilePath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    1,
                    FileOptions.Asynchronous);
                
                m_hasExclusiveLock = true;
                return;
            }
            catch (IOException)
            {
                await Task.Delay(delay, cts.Token).ConfigureAwait(false);
                delay = Math.Min(delay * 2, MAX_RETRY_DELAY_MS);
            }
        }

        throw new TimeoutException($"Could not acquire exclusive file lock within {effectiveTimeout.TotalSeconds:F1} seconds.");
    }

    #endregion

    #region ReleaseLock

    /// <summary>
    /// Releases the currently held lock.
    /// </summary>
    public void ReleaseLock()
    {
        m_lockFile?.Dispose();
        m_lockFile = null;
        m_hasExclusiveLock = false;
        m_hasSharedLock = false;
    }

    #endregion

    #region Tools

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(m_disposed, this);
    }

    private void TryDeleteLockFile()
    {
        try
        {
            if (File.Exists(m_lockFilePath))
            {
                File.Delete(m_lockFilePath);
            }
        }
        catch
        {
            // Ignore - file may be held by another process
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (m_disposed) return;
        m_disposed = true;

        m_lockFile?.Dispose();
        m_lockFile = null;

        // Clean up lock file on normal close
        TryDeleteLockFile();
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets whether an exclusive lock is currently held.
    /// </summary>
    public bool HasExclusiveLock => m_hasExclusiveLock;

    /// <summary>
    /// Gets whether a shared lock is currently held.
    /// </summary>
    public bool HasSharedLock => m_hasSharedLock;

    #endregion

}
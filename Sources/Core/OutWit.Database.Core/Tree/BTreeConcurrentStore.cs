using System.Runtime.CompilerServices;
using OutWit.Database.Core.Interfaces;
using OutWit.Database.Core.Managers;
using OutWit.Database.Core.Stores;

namespace OutWit.Database.Core.Tree;

/// <summary>
/// Thread-safe BTree store wrapper for concurrent access.
/// Provides safe concurrent access to StoreBTree for multi-threaded scenarios.
/// </summary>
/// <remarks>
/// This store is designed for scenarios where multiple threads need to access the same BTree.
/// It uses a simple but effective ReaderWriterLock strategy:
/// - Multiple concurrent readers allowed
/// - Single writer with exclusive access
/// 
/// For maximum single-threaded performance, use StoreBTree directly.
/// This wrapper adds ~1-5% overhead for thread safety.
/// </remarks>
public sealed class BTreeConcurrentStore : IKeyValueStore, IKeyValueStoreStatistics
{
    #region Constants

    /// <summary>
    /// Provider key for concurrent B-Tree store.
    /// </summary>
    public const string PROVIDER_KEY = "btree-concurrent";

    #endregion

    #region Fields

    private readonly StoreBTree m_store;
    private readonly ReaderWriterLockSlim m_lock;
    private readonly BTreeConcurrencyOptions m_options;
    private readonly bool m_ownsStore;

    private long m_readCount;
    private long m_writeCount;
    private bool m_disposed;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a new concurrent BTree store with file storage.
    /// </summary>
    /// <param name="filePath">Path to the database file.</param>
    /// <param name="options">Concurrency options.</param>
    /// <param name="pageSize">Page size in bytes.</param>
    /// <param name="cacheSize">Number of pages to cache.</param>
    public BTreeConcurrentStore(
        string filePath,
        BTreeConcurrencyOptions? options = null,
        int pageSize = 4096,
        int cacheSize = 1000)
    {
        m_options = options ?? BTreeConcurrencyOptions.Default;
        m_store = new StoreBTree(filePath, pageSize, cacheSize);
        m_ownsStore = true;
        m_lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
    }

    /// <summary>
    /// Creates a new concurrent BTree store wrapping an existing StoreBTree.
    /// </summary>
    /// <param name="store">The underlying store.</param>
    /// <param name="options">Concurrency options.</param>
    /// <param name="ownsStore">Whether to dispose the store on disposal.</param>
    public BTreeConcurrentStore(
        StoreBTree store,
        BTreeConcurrencyOptions? options = null,
        bool ownsStore = false)
    {
        ArgumentNullException.ThrowIfNull(store);
        
        m_store = store;
        m_options = options ?? BTreeConcurrencyOptions.Default;
        m_ownsStore = ownsStore;
        m_lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
    }

    #endregion

    #region Get

    /// <inheritdoc/>
    public byte[]? Get(ReadOnlySpan<byte> key)
    {
        ThrowIfDisposed();
        IncrementReadCount();

        m_lock.EnterReadLock();
        try
        {
            return m_store.Get(key);
        }
        finally
        {
            m_lock.ExitReadLock();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<byte[]?> GetAsync(byte[] key, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        IncrementReadCount();

        m_lock.EnterReadLock();
        try
        {
            return await m_store.GetAsync(key, cancellationToken);
        }
        finally
        {
            m_lock.ExitReadLock();
        }
    }

    #endregion

    #region Put

    /// <inheritdoc/>
    public void Put(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
    {
        ThrowIfDisposed();
        IncrementWriteCount();

        m_lock.EnterWriteLock();
        try
        {
            m_store.Put(key, value);
        }
        finally
        {
            m_lock.ExitWriteLock();
        }
    }

    /// <inheritdoc/>
    public async ValueTask PutAsync(byte[] key, byte[] value, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        IncrementWriteCount();

        m_lock.EnterWriteLock();
        try
        {
            await m_store.PutAsync(key, value, cancellationToken);
        }
        finally
        {
            m_lock.ExitWriteLock();
        }
    }

    #endregion

    #region Delete

    /// <inheritdoc/>
    public bool Delete(ReadOnlySpan<byte> key)
    {
        ThrowIfDisposed();
        IncrementWriteCount();

        m_lock.EnterWriteLock();
        try
        {
            return m_store.Delete(key);
        }
        finally
        {
            m_lock.ExitWriteLock();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(byte[] key, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        IncrementWriteCount();

        m_lock.EnterWriteLock();
        try
        {
            return await m_store.DeleteAsync(key, cancellationToken);
        }
        finally
        {
            m_lock.ExitWriteLock();
        }
    }

    #endregion

    #region Scan

    /// <inheritdoc/>
    public IEnumerable<(byte[] Key, byte[] Value)> Scan(byte[]? startKey, byte[]? endKey)
    {
        ThrowIfDisposed();

        // Materialize results while holding read lock
        m_lock.EnterReadLock();
        try
        {
            return m_store.Scan(startKey, endKey).ToList();
        }
        finally
        {
            m_lock.ExitReadLock();
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<(byte[] Key, byte[] Value)> ScanAsync(
        byte[]? startKey,
        byte[]? endKey,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        List<(byte[] Key, byte[] Value)> results;

        m_lock.EnterReadLock();
        try
        {
            results = m_store.Scan(startKey, endKey).ToList();
        }
        finally
        {
            m_lock.ExitReadLock();
        }

        foreach (var item in results)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }

        await ValueTask.CompletedTask;
    }

    #endregion

    #region Flush

    /// <inheritdoc/>
    public void Flush()
    {
        ThrowIfDisposed();

        m_lock.EnterWriteLock();
        try
        {
            m_store.Flush();
        }
        finally
        {
            m_lock.ExitWriteLock();
        }
    }

    /// <inheritdoc/>
    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        m_lock.EnterWriteLock();
        try
        {
            await m_store.FlushAsync(cancellationToken);
        }
        finally
        {
            m_lock.ExitWriteLock();
        }
    }

    #endregion

    #region IKeyValueStoreStatistics

    /// <inheritdoc/>
    public long Count()
    {
        ThrowIfDisposed();

        m_lock.EnterReadLock();
        try
        {
            return m_store.Count();
        }
        finally
        {
            m_lock.ExitReadLock();
        }
    }

    /// <inheritdoc/>
    public ValueTask<long> CountAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Count());
    }

    /// <inheritdoc/>
    public long ApproximateSizeInBytes
    {
        get
        {
            ThrowIfDisposed();
            return m_store.ApproximateSizeInBytes;
        }
    }

    /// <inheritdoc/>
    public long EstimatedKeyCount => Count();

    /// <inheritdoc/>
    public bool AreStatisticsExact => true;

    /// <inheritdoc/>
    public string ProviderKey => PROVIDER_KEY;

    #endregion

    #region Statistics

    /// <summary>
    /// Gets the total number of read operations.
    /// </summary>
    public long ReadCount => Volatile.Read(ref m_readCount);

    /// <summary>
    /// Gets the total number of write operations.
    /// </summary>
    public long WriteCount => Volatile.Read(ref m_writeCount);

    /// <summary>
    /// Gets the concurrency options.
    /// </summary>
    public BTreeConcurrencyOptions Options => m_options;

    #endregion

    #region Private Helpers

    private void IncrementReadCount()
    {
        if (m_options.TrackStatistics)
        {
            Interlocked.Increment(ref m_readCount);
        }
    }

    private void IncrementWriteCount()
    {
        if (m_options.TrackStatistics)
        {
            Interlocked.Increment(ref m_writeCount);
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

        m_lock.Dispose();

        if (m_ownsStore)
        {
            m_store.Dispose();
        }
    }

    #endregion
}

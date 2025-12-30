using System.Runtime.CompilerServices;
using OutWit.Database.Core.Interfaces;
using OutWit.Database.Core.Managers;
using OutWit.Database.Core.Stores;

namespace OutWit.Database.Core.Tree;

/// <summary>
/// Thread-safe BTree store with page-level latching for concurrent access.
/// Wraps StoreBTree and adds concurrent access capabilities.
/// </summary>
/// <remarks>
/// Features:
/// - Page-level latching for fine-grained concurrency
/// - Optimistic reads (no latch for simple reads)
/// - Configurable statistics tracking
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
    private readonly PageLatchManager m_latchManager;
    private readonly BTreeConcurrencyOptions m_options;
    private readonly Lock m_globalLock = new();
    private readonly bool m_ownsStore;

    private long m_readCount;
    private long m_writeCount;
    private long m_optimisticReadHits;
    private long m_optimisticReadMisses;
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
        m_latchManager = new PageLatchManager(m_options.LatchManagerCapacity);
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
        m_latchManager = new PageLatchManager(m_options.LatchManagerCapacity);
    }

    #endregion

    #region Get

    /// <inheritdoc/>
    public byte[]? Get(ReadOnlySpan<byte> key)
    {
        ThrowIfDisposed();
        IncrementReadCount();

        if (!m_options.EnableConcurrentAccess)
        {
            lock (m_globalLock)
            {
                return m_store.Get(key);
            }
        }

        if (m_options.UseOptimisticReads)
        {
            // Optimistic read - try without latch first
            try
            {
                var result = m_store.Get(key);
                IncrementOptimisticHits();
                return result;
            }
            catch
            {
                // If read fails, retry with latch
                IncrementOptimisticMisses();
            }
        }

        // Pessimistic read with root latch
        using var latch = AcquireSharedLatch(m_store.RootPageNumber);
        return m_store.Get(key);
    }

    /// <inheritdoc/>
    public async ValueTask<byte[]?> GetAsync(byte[] key, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        IncrementReadCount();

        if (!m_options.EnableConcurrentAccess)
        {
            byte[]? result;
            lock (m_globalLock)
            {
                result = m_store.Get(key);
            }
            return result;
        }

        if (m_options.UseOptimisticReads)
        {
            try
            {
                var result = await m_store.GetAsync(key, cancellationToken);
                IncrementOptimisticHits();
                return result;
            }
            catch
            {
                IncrementOptimisticMisses();
            }
        }

        using var latch = AcquireSharedLatch(m_store.RootPageNumber);
        return await m_store.GetAsync(key, cancellationToken);
    }

    #endregion

    #region Put

    /// <inheritdoc/>
    public void Put(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
    {
        ThrowIfDisposed();
        IncrementWriteCount();

        if (!m_options.EnableConcurrentAccess)
        {
            lock (m_globalLock)
            {
                m_store.Put(key, value);
                return;
            }
        }

        // Write requires exclusive latch on root
        using var latch = AcquireExclusiveLatch(m_store.RootPageNumber);
        m_store.Put(key, value);
    }

    /// <inheritdoc/>
    public async ValueTask PutAsync(byte[] key, byte[] value, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        IncrementWriteCount();

        if (!m_options.EnableConcurrentAccess)
        {
            lock (m_globalLock)
            {
                m_store.Put(key, value);
                return;
            }
        }

        using var latch = AcquireExclusiveLatch(m_store.RootPageNumber);
        await m_store.PutAsync(key, value, cancellationToken);
    }

    #endregion

    #region Delete

    /// <inheritdoc/>
    public bool Delete(ReadOnlySpan<byte> key)
    {
        ThrowIfDisposed();
        IncrementWriteCount();

        if (!m_options.EnableConcurrentAccess)
        {
            lock (m_globalLock)
            {
                return m_store.Delete(key);
            }
        }

        using var latch = AcquireExclusiveLatch(m_store.RootPageNumber);
        return m_store.Delete(key);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(byte[] key, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        IncrementWriteCount();

        if (!m_options.EnableConcurrentAccess)
        {
            lock (m_globalLock)
            {
                return m_store.Delete(key);
            }
        }

        using var latch = AcquireExclusiveLatch(m_store.RootPageNumber);
        return await m_store.DeleteAsync(key, cancellationToken);
    }

    #endregion

    #region Scan

    /// <inheritdoc/>
    public IEnumerable<(byte[] Key, byte[] Value)> Scan(byte[]? startKey, byte[]? endKey)
    {
        ThrowIfDisposed();

        if (!m_options.EnableConcurrentAccess)
        {
            lock (m_globalLock)
            {
                // Materialize results while holding lock
                return m_store.Scan(startKey, endKey).ToList();
            }
        }

        // Scan with shared latch
        using var latch = AcquireSharedLatch(m_store.RootPageNumber);
        return m_store.Scan(startKey, endKey).ToList();
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<(byte[] Key, byte[] Value)> ScanAsync(
        byte[]? startKey,
        byte[]? endKey,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        List<(byte[] Key, byte[] Value)> results;

        if (!m_options.EnableConcurrentAccess)
        {
            lock (m_globalLock)
            {
                results = m_store.Scan(startKey, endKey).ToList();
            }
        }
        else
        {
            using var latch = AcquireSharedLatch(m_store.RootPageNumber);
            results = m_store.Scan(startKey, endKey).ToList();
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

        if (!m_options.EnableConcurrentAccess)
        {
            lock (m_globalLock)
            {
                m_store.Flush();
                return;
            }
        }

        // Flush with exclusive latch on root
        using var latch = AcquireExclusiveLatch(m_store.RootPageNumber);
        m_store.Flush();
    }

    /// <inheritdoc/>
    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!m_options.EnableConcurrentAccess)
        {
            lock (m_globalLock)
            {
                m_store.Flush();
                return;
            }
        }

        using var latch = AcquireExclusiveLatch(m_store.RootPageNumber);
        await m_store.FlushAsync(cancellationToken);
    }

    #endregion

    #region IKeyValueStoreStatistics

    /// <inheritdoc/>
    public long Count()
    {
        ThrowIfDisposed();

        if (!m_options.EnableConcurrentAccess)
        {
            lock (m_globalLock)
            {
                return m_store.Count();
            }
        }

        using var latch = AcquireSharedLatch(m_store.RootPageNumber);
        return m_store.Count();
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
    /// Gets the number of successful optimistic reads.
    /// </summary>
    public long OptimisticReadHits => Volatile.Read(ref m_optimisticReadHits);

    /// <summary>
    /// Gets the number of failed optimistic reads (required retry with latch).
    /// </summary>
    public long OptimisticReadMisses => Volatile.Read(ref m_optimisticReadMisses);

    /// <summary>
    /// Gets the optimistic read hit ratio.
    /// </summary>
    public double OptimisticReadHitRatio
    {
        get
        {
            var hits = OptimisticReadHits;
            var misses = OptimisticReadMisses;
            var total = hits + misses;
            return total > 0 ? (double)hits / total : 1.0;
        }
    }

    /// <summary>
    /// Gets the latch manager for statistics.
    /// </summary>
    public PageLatchManager LatchManager => m_latchManager;

    /// <summary>
    /// Gets the concurrency options.
    /// </summary>
    public BTreeConcurrencyOptions Options => m_options;

    #endregion

    #region Private Helpers

    private PageLatchManager.LatchHandle AcquireSharedLatch(uint pageNumber)
    {
        return m_latchManager.AcquireShared(pageNumber, m_options.LatchTimeout);
    }

    private PageLatchManager.LatchHandle AcquireExclusiveLatch(uint pageNumber)
    {
        return m_latchManager.AcquireExclusive(pageNumber, m_options.LatchTimeout);
    }

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

    private void IncrementOptimisticHits()
    {
        if (m_options.TrackStatistics)
        {
            Interlocked.Increment(ref m_optimisticReadHits);
        }
    }

    private void IncrementOptimisticMisses()
    {
        if (m_options.TrackStatistics)
        {
            Interlocked.Increment(ref m_optimisticReadMisses);
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

        m_latchManager.Dispose();

        if (m_ownsStore)
        {
            m_store.Dispose();
        }
    }

    #endregion
}

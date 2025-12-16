using System.Runtime.CompilerServices;
using OutWit.Database.Core.Interfaces;

namespace OutWit.Database.Core.Cache;

/// <summary>
/// High-performance page cache using Clock (Second Chance) algorithm with sharding.
/// Provides better concurrency than simple LRU and resistance to scan patterns.
/// </summary>
/// <remarks>
/// Recommended for high-concurrency and scan-heavy workloads.
/// For simpler workloads with low concurrency, <see cref="LruPageCache"/> may be sufficient.
/// </remarks>
public sealed class ShardedClockCache : IPageCache
{
    #region Constants

    private const int DEFAULT_SHARD_COUNT = 16;
    private const int MIN_PAGES_PER_SHARD = 4;

    #endregion

    #region Nested Types

    private sealed class CacheShard : IDisposable
    {
        private readonly IStorage m_storage;
        private readonly int m_capacity;
        private readonly Lock m_lock = new();
        
        // Clock data structures
        private readonly CachedPage?[] m_pages;
        private readonly Dictionary<long, int> m_pageIndex;
        private int m_clockHand;
        private int m_count;
        private bool m_disposed;

        public CacheShard(IStorage storage, int capacity)
        {
            m_storage = storage;
            m_capacity = capacity;
            m_pages = new CachedPage?[capacity];
            m_pageIndex = new Dictionary<long, int>(capacity);
            m_clockHand = 0;
            m_count = 0;
        }

        public CachedPage GetPage(long pageNumber)
        {
            lock (m_lock)
            {
                ThrowIfDisposed();

                if (m_pageIndex.TryGetValue(pageNumber, out int index))
                {
                    var page = m_pages[index]!;
                    page.Referenced = true; // Second chance
                    page.ReferenceCount++;
                    return page;
                }

                return LoadPage(pageNumber);
            }
        }

        public CachedPage CreatePage(long pageNumber)
        {
            lock (m_lock)
            {
                ThrowIfDisposed();

                if (m_pageIndex.ContainsKey(pageNumber))
                    throw new InvalidOperationException($"Page {pageNumber} already exists in cache");

                int slot = FindSlotForNewPage();

                var page = new CachedPage(pageNumber, m_storage.PageSize);
                page.Data.Clear();
                page.MarkDirty();
                page.ReferenceCount = 1;
                page.Referenced = true;

                m_pages[slot] = page;
                m_pageIndex[pageNumber] = slot;
                m_count++;

                return page;
            }
        }

        public void MarkDirty(long pageNumber)
        {
            lock (m_lock)
            {
                if (m_pageIndex.TryGetValue(pageNumber, out int index))
                {
                    m_pages[index]?.MarkDirty();
                }
            }
        }

        public void ReleasePage(long pageNumber)
        {
            lock (m_lock)
            {
                if (m_pageIndex.TryGetValue(pageNumber, out int index))
                {
                    var page = m_pages[index];
                    if (page != null)
                    {
                        page.ReferenceCount = Math.Max(0, page.ReferenceCount - 1);
                    }
                }
            }
        }

        public void Evict(long pageNumber)
        {
            lock (m_lock)
            {
                ThrowIfDisposed();

                if (m_pageIndex.TryGetValue(pageNumber, out int index))
                {
                    var page = m_pages[index];
                    if (page != null)
                    {
                        if (page.ReferenceCount > 0)
                            throw new InvalidOperationException($"Cannot evict pinned page {pageNumber}");

                        EvictSlot(index);
                    }
                }
            }
        }

        public void FlushAll()
        {
            lock (m_lock)
            {
                ThrowIfDisposed();

                for (int i = 0; i < m_capacity; i++)
                {
                    var page = m_pages[i];
                    if (page != null && page.IsDirty)
                    {
                        m_storage.WritePage(page.PageNumber, page.ReadOnlyData);
                        page.ClearDirty();
                    }
                }
            }
        }

        public async ValueTask FlushAllAsync(CancellationToken cancellationToken)
        {
            List<CachedPage> dirtyPages;

            lock (m_lock)
            {
                ThrowIfDisposed();
                
                dirtyPages = new List<CachedPage>();
                for (int i = 0; i < m_capacity; i++)
                {
                    var page = m_pages[i];
                    if (page != null && page.IsDirty)
                    {
                        page.ReferenceCount++;
                        dirtyPages.Add(page);
                    }
                }
            }

            try
            {
                foreach (var page in dirtyPages)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!page.IsDisposed)
                    {
                        await m_storage.WritePageAsync(page.PageNumber, page.Memory, cancellationToken)
                            .ConfigureAwait(false);
                        page.ClearDirty();
                    }
                }
            }
            finally
            {
                lock (m_lock)
                {
                    foreach (var page in dirtyPages)
                    {
                        page.ReferenceCount = Math.Max(0, page.ReferenceCount - 1);
                    }
                }
            }
        }

        public void Clear()
        {
            lock (m_lock)
            {
                FlushAllInternal();

                for (int i = 0; i < m_capacity; i++)
                {
                    m_pages[i]?.Dispose();
                    m_pages[i] = null;
                }

                m_pageIndex.Clear();
                m_count = 0;
                m_clockHand = 0;
            }
        }

        public int Count
        {
            get
            {
                lock (m_lock)
                {
                    return m_count;
                }
            }
        }

        public int DirtyCount
        {
            get
            {
                lock (m_lock)
                {
                    int count = 0;
                    for (int i = 0; i < m_capacity; i++)
                    {
                        if (m_pages[i]?.IsDirty == true)
                            count++;
                    }
                    return count;
                }
            }
        }

        private CachedPage LoadPage(long pageNumber)
        {
            int slot = FindSlotForNewPage();

            var page = new CachedPage(pageNumber, m_storage.PageSize);
            m_storage.ReadPage(pageNumber, page.Data);
            page.ReferenceCount = 1;
            page.Referenced = true;

            m_pages[slot] = page;
            m_pageIndex[pageNumber] = slot;
            m_count++;

            return page;
        }

        private int FindSlotForNewPage()
        {
            // If we have empty slots, use one
            if (m_count < m_capacity)
            {
                for (int i = 0; i < m_capacity; i++)
                {
                    if (m_pages[i] == null)
                        return i;
                }
            }

            // Clock algorithm: find a page to evict
            int startHand = m_clockHand;
            int iterations = 0;
            int maxIterations = m_capacity * 2; // Prevent infinite loop

            while (iterations < maxIterations)
            {
                var page = m_pages[m_clockHand];

                if (page != null && page.ReferenceCount == 0)
                {
                    if (page.Referenced)
                    {
                        // Second chance: clear referenced bit
                        page.Referenced = false;
                    }
                    else
                    {
                        // Evict this page
                        int slot = m_clockHand;
                        EvictSlot(slot);
                        m_clockHand = (m_clockHand + 1) % m_capacity;
                        return slot;
                    }
                }

                m_clockHand = (m_clockHand + 1) % m_capacity;
                iterations++;
            }

            throw new InvalidOperationException("Cache is full and all pages are pinned");
        }

        private void EvictSlot(int slot)
        {
            var page = m_pages[slot];
            if (page != null)
            {
                if (page.IsDirty)
                {
                    m_storage.WritePage(page.PageNumber, page.ReadOnlyData);
                }

                m_pageIndex.Remove(page.PageNumber);
                page.Dispose();
                m_pages[slot] = null;
                m_count--;
            }
        }

        private void FlushAllInternal()
        {
            for (int i = 0; i < m_capacity; i++)
            {
                var page = m_pages[i];
                if (page != null && page.IsDirty)
                {
                    m_storage.WritePage(page.PageNumber, page.ReadOnlyData);
                    page.ClearDirty();
                }
            }
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(m_disposed, this);
        }

        public void Dispose()
        {
            if (!m_disposed)
            {
                lock (m_lock)
                {
                    if (!m_disposed)
                    {
                        Clear();
                        m_disposed = true;
                    }
                }
            }
        }
    }

    #endregion

    #region Fields

    private readonly CacheShard[] m_shards;
    private readonly int m_shardMask;
    private bool m_disposed;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a new sharded clock cache.
    /// </summary>
    /// <param name="storage">Underlying storage</param>
    /// <param name="maxPages">Maximum number of pages to cache</param>
    /// <param name="shardCount">Number of shards (default: 16, must be power of 2)</param>
    public ShardedClockCache(IStorage storage, int maxPages = DatabaseConstants.DEFAULT_CACHE_SIZE, int? shardCount = null)
    {
        ArgumentNullException.ThrowIfNull(storage);

        if (maxPages < 1)
            throw new ArgumentOutOfRangeException(nameof(maxPages), "Cache must hold at least 1 page");

        int actualShardCount = shardCount ?? Math.Min(DEFAULT_SHARD_COUNT, Math.Max(1, maxPages / MIN_PAGES_PER_SHARD));
        
        // Round up to power of 2 for fast modulo
        actualShardCount = RoundUpToPowerOf2(actualShardCount);
        
        m_shardMask = actualShardCount - 1;
        m_shards = new CacheShard[actualShardCount];

        int pagesPerShard = Math.Max(1, maxPages / actualShardCount);
        
        for (int i = 0; i < actualShardCount; i++)
        {
            m_shards[i] = new CacheShard(storage, pagesPerShard);
        }
    }

    #endregion

    #region Functions

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private CacheShard GetShard(long pageNumber)
    {
        // Use lower bits of page number for better distribution
        return m_shards[pageNumber & m_shardMask];
    }

    /// <summary>
    /// Gets a page from the cache, loading from storage if necessary.
    /// </summary>
    public CachedPage GetPage(long pageNumber)
    {
        ThrowIfDisposed();
        return GetShard(pageNumber).GetPage(pageNumber);
    }

    /// <summary>
    /// Creates a new page in the cache (for newly allocated pages).
    /// </summary>
    public CachedPage CreatePage(long pageNumber)
    {
        ThrowIfDisposed();
        return GetShard(pageNumber).CreatePage(pageNumber);
    }

    /// <summary>
    /// Marks a page as dirty (modified).
    /// </summary>
    public void MarkDirty(long pageNumber)
    {
        ThrowIfDisposed();
        GetShard(pageNumber).MarkDirty(pageNumber);
    }

    /// <summary>
    /// Releases a reference to a page.
    /// </summary>
    public void ReleasePage(long pageNumber)
    {
        GetShard(pageNumber).ReleasePage(pageNumber);
    }

    /// <summary>
    /// Evicts a specific page from the cache.
    /// </summary>
    public void Evict(long pageNumber)
    {
        ThrowIfDisposed();
        GetShard(pageNumber).Evict(pageNumber);
    }

    /// <summary>
    /// Flushes all dirty pages to storage.
    /// </summary>
    public void FlushAll()
    {
        ThrowIfDisposed();

        foreach (var shard in m_shards)
        {
            shard.FlushAll();
        }
    }

    /// <summary>
    /// Flushes all dirty pages to storage asynchronously.
    /// </summary>
    public async ValueTask FlushAllAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Flush all shards in parallel
        var tasks = new ValueTask[m_shards.Length];
        for (int i = 0; i < m_shards.Length; i++)
        {
            tasks[i] = m_shards[i].FlushAllAsync(cancellationToken);
        }

        foreach (var task in tasks)
        {
            await task.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Clears all pages from the cache.
    /// </summary>
    public void Clear()
    {
        ThrowIfDisposed();

        foreach (var shard in m_shards)
        {
            shard.Clear();
        }
    }

    private static int RoundUpToPowerOf2(int value)
    {
        value--;
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return value + 1;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(m_disposed, this);
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (!m_disposed)
        {
            m_disposed = true;

            foreach (var shard in m_shards)
            {
                shard.Dispose();
            }
        }
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the current number of cached pages.
    /// </summary>
    public int Count => m_shards.Sum(s => s.Count);

    /// <summary>
    /// Gets the number of dirty pages in the cache.
    /// </summary>
    public int DirtyCount => m_shards.Sum(s => s.DirtyCount);

    /// <summary>
    /// Gets the number of shards.
    /// </summary>
    public int ShardCount => m_shards.Length;

    #endregion
}

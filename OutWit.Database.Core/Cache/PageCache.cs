using OutWit.Database.Core.Interfaces;

namespace OutWit.Database.Core.Cache;

/// <summary>
/// LRU (Least Recently Used) page cache for buffering frequently accessed pages.
/// Reduces disk I/O by keeping hot pages in memory.
/// </summary>
public sealed class PageCache : IDisposable
{
    #region Fields

    private readonly IStorage m_storage;

    private readonly int m_maxPages;

    private readonly Dictionary<long, LinkedListNode<CachedPage>> m_cache;

    private readonly LinkedList<CachedPage> m_lruList;

    private readonly Lock m_lock = new();

    private bool m_disposed;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a new page cache with the specified maximum size.
    /// </summary>
    /// <param name="storage">Underlying storage</param>
    /// <param name="maxPages">Maximum number of pages to cache</param>
    public PageCache(IStorage storage, int maxPages = DatabaseConstants.DEFAULT_CACHE_SIZE)
    {
        ArgumentNullException.ThrowIfNull(storage);

        if (maxPages < 1)
            throw new ArgumentOutOfRangeException(nameof(maxPages), "Cache must hold at least 1 page");

        m_storage = storage;
        m_maxPages = maxPages;
        m_cache = new Dictionary<long, LinkedListNode<CachedPage>>(maxPages);
        m_lruList = new LinkedList<CachedPage>();
    }

    #endregion

    #region Functions

    /// <summary>
    /// Gets a page from the cache, loading from storage if necessary.
    /// The page is moved to the front of the LRU list.
    /// </summary>
    /// <param name="pageNumber">Page number to retrieve</param>
    /// <returns>The cached page</returns>
    public CachedPage GetPage(long pageNumber)
    {
        lock (m_lock)
        {
            ThrowIfDisposed();

            if (m_cache.TryGetValue(pageNumber, out var node))
            {
                // Move to front (most recently used)
                m_lruList.Remove(node);
                m_lruList.AddFirst(node);
                node.Value.ReferenceCount++;
                return node.Value;
            }

            // Need to load from storage
            return LoadPage(pageNumber);
        }
    }

    /// <summary>
    /// Creates a new page in the cache (for newly allocated pages).
    /// </summary>
    /// <param name="pageNumber">Page number for the new page</param>
    /// <returns>The new cached page</returns>
    public CachedPage CreatePage(long pageNumber)
    {
        lock (m_lock)
        {
            ThrowIfDisposed();

            if (m_cache.ContainsKey(pageNumber))
                throw new InvalidOperationException($"Page {pageNumber} already exists in cache");

            EnsureCapacity();

            var page = new CachedPage(pageNumber, m_storage.PageSize);
            page.Data.Clear(); // Initialize to zeros
            page.MarkDirty(); // New pages are dirty
            page.ReferenceCount = 1; // Caller holds a reference

            var node = m_lruList.AddFirst(page);
            m_cache[pageNumber] = node;

            return page;
        }
    }

    /// <summary>
    /// Marks a page as dirty (modified).
    /// </summary>
    public void MarkDirty(long pageNumber)
    {
        lock (m_lock)
        {
            ThrowIfDisposed();

            if (m_cache.TryGetValue(pageNumber, out var node))
            {
                node.Value.MarkDirty();
            }
        }
    }

    /// <summary>
    /// Releases a reference to a page. When reference count reaches zero,
    /// the page becomes eligible for eviction.
    /// </summary>
    public void ReleasePage(long pageNumber)
    {
        lock (m_lock)
        {
            if (m_cache.TryGetValue(pageNumber, out var node))
            {
                node.Value.ReferenceCount = Math.Max(0, node.Value.ReferenceCount - 1);
            }
        }
    }

    /// <summary>
    /// Flushes all dirty pages to storage.
    /// </summary>
    public void FlushAll()
    {
        lock (m_lock)
        {
            ThrowIfDisposed();
            FlushAllInternal();
        }
    }

    /// <summary>
    /// Flushes all dirty pages to storage asynchronously.
    /// </summary>
    public async ValueTask FlushAllAsync(CancellationToken cancellationToken = default)
    {
        // Collect pages to flush while holding lock, increment reference to prevent eviction
        List<CachedPage> dirtyPages;
        
        lock (m_lock)
        {
            ThrowIfDisposed();
            dirtyPages = m_lruList.Where(p => p.IsDirty).ToList();
            
            // Pin all dirty pages to prevent eviction during async operation
            foreach (var page in dirtyPages)
            {
                page.ReferenceCount++;
            }
        }

        try
        {
            foreach (var page in dirtyPages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // Check if page is still valid (not disposed)
                if (!page.IsDisposed)
                {
                    await m_storage.WritePageAsync(page.PageNumber, page.Memory, cancellationToken).ConfigureAwait(false);
                    page.ClearDirty();
                }
            }

            await m_storage.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // Release the references we acquired
            lock (m_lock)
            {
                foreach (var page in dirtyPages)
                {
                    if (m_cache.ContainsKey(page.PageNumber))
                    {
                        page.ReferenceCount = Math.Max(0, page.ReferenceCount - 1);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Flushes a specific dirty page to storage.
    /// </summary>
    public void FlushPage(long pageNumber)
    {
        lock (m_lock)
        {
            ThrowIfDisposed();

            if (m_cache.TryGetValue(pageNumber, out var node) && node.Value.IsDirty)
            {
                m_storage.WritePage(node.Value.PageNumber, node.Value.ReadOnlyData);
                node.Value.ClearDirty();
            }
        }
    }

    /// <summary>
    /// Flushes a specific dirty page to storage asynchronously.
    /// </summary>
    public async ValueTask FlushPageAsync(long pageNumber, CancellationToken cancellationToken = default)
    {
        CachedPage? pageToFlush = null;
        
        lock (m_lock)
        {
            ThrowIfDisposed();

            if (m_cache.TryGetValue(pageNumber, out var node) && node.Value.IsDirty)
            {
                pageToFlush = node.Value;
                // Pin the page to prevent eviction during async operation
                pageToFlush.ReferenceCount++;
            }
        }

        if (pageToFlush == null)
            return;

        try
        {
            if (!pageToFlush.IsDisposed)
            {
                await m_storage.WritePageAsync(pageToFlush.PageNumber, pageToFlush.Memory, cancellationToken).ConfigureAwait(false);
                pageToFlush.ClearDirty();
            }
        }
        finally
        {
            // Release the reference we acquired
            lock (m_lock)
            {
                if (m_cache.ContainsKey(pageNumber))
                {
                    pageToFlush.ReferenceCount = Math.Max(0, pageToFlush.ReferenceCount - 1);
                }
            }
        }
    }

    /// <summary>
    /// Evicts a specific page from the cache (flushing if dirty).
    /// </summary>
    public void Evict(long pageNumber)
    {
        lock (m_lock)
        {
            ThrowIfDisposed();

            if (m_cache.TryGetValue(pageNumber, out var node))
            {
                if (node.Value.ReferenceCount > 0)
                    throw new InvalidOperationException($"Cannot evict page {pageNumber}: page is pinned (ReferenceCount = {node.Value.ReferenceCount})");
                    
                EvictNode(node);
            }
        }
    }

    /// <summary>
    /// Clears all pages from the cache, flushing dirty pages first.
    /// </summary>
    public void Clear()
    {
        lock (m_lock)
        {
            ThrowIfDisposed();
            FlushAllInternal();

            foreach (var page in m_lruList)
            {
                page.Dispose();
            }

            m_cache.Clear();
            m_lruList.Clear();
        }
    }

    private void FlushAllInternal()
    {
        foreach (var page in m_lruList.Where(p => p.IsDirty))
        {
            m_storage.WritePage(page.PageNumber, page.ReadOnlyData);
            page.ClearDirty();
        }

        m_storage.Flush();
    }

    private CachedPage LoadPage(long pageNumber)
    {
        EnsureCapacity();

        var page = new CachedPage(pageNumber, m_storage.PageSize);
        m_storage.ReadPage(pageNumber, page.Data);
        page.ReferenceCount = 1; // Caller holds a reference

        var node = m_lruList.AddFirst(page);
        m_cache[pageNumber] = node;

        return page;
    }

    private void EnsureCapacity()
    {
        while (m_cache.Count >= m_maxPages)
        {
            // Find LRU page that is not pinned (reference count = 0)
            var nodeToEvict = m_lruList.Last;

            while (nodeToEvict != null && nodeToEvict.Value.ReferenceCount > 0)
            {
                nodeToEvict = nodeToEvict.Previous;
            }

            if (nodeToEvict == null)
                throw new InvalidOperationException("Cache is full and all pages are pinned");

            EvictNode(nodeToEvict);
        }
    }

    private void EvictNode(LinkedListNode<CachedPage> node)
    {
        var page = node.Value;

        if (page.IsDirty)
        {
            m_storage.WritePage(page.PageNumber, page.ReadOnlyData);
        }

        m_cache.Remove(page.PageNumber);
        m_lruList.Remove(node);
        page.Dispose();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(m_disposed, this);
    }

    #endregion

    #region IDisposable

    /// <inheritdoc/>
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

    #endregion

    #region Properties

    /// <summary>
    /// Gets the current number of cached pages.
    /// </summary>
    public int Count
    {
        get
        {
            lock (m_lock)
            {
                return m_cache.Count;
            }
        }
    }

    /// <summary>
    /// Gets the number of dirty pages in the cache.
    /// </summary>
    public int DirtyCount
    {
        get
        {
            lock (m_lock)
            {
                return m_lruList.Count(p => p.IsDirty);
            }
        }
    }

    #endregion
}

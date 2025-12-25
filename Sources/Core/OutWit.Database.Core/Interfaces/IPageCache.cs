namespace OutWit.Database.Core.Interfaces;

/// <summary>
/// Interface for page cache implementations.
/// Implement this interface to provide custom caching strategies.
/// </summary>
public interface IPageCache : IProvider, IDisposable
{
    #region Sync Operations

    /// <summary>
    /// Gets a page from the cache, loading from storage if necessary.
    /// </summary>
    /// <param name="pageNumber">The page number to retrieve.</param>
    /// <returns>The cached page.</returns>
    /// <remarks>
    /// For async-only storage (e.g., IndexedDB), use <see cref="GetPageAsync"/> instead.
    /// </remarks>
    Cache.CachedPage GetPage(long pageNumber);

    /// <summary>
    /// Creates a new page in the cache (for newly allocated pages).
    /// </summary>
    /// <param name="pageNumber">The page number to create.</param>
    /// <returns>The newly created cached page.</returns>
    /// <remarks>
    /// For async-only storage (e.g., IndexedDB), use <see cref="CreatePageAsync"/> instead.
    /// </remarks>
    Cache.CachedPage CreatePage(long pageNumber);

    /// <summary>
    /// Marks a page as dirty (modified).
    /// </summary>
    /// <param name="pageNumber">The page number to mark dirty.</param>
    void MarkDirty(long pageNumber);

    /// <summary>
    /// Releases a reference to a page.
    /// </summary>
    /// <param name="pageNumber">The page number to release.</param>
    void ReleasePage(long pageNumber);

    /// <summary>
    /// Evicts a specific page from the cache.
    /// </summary>
    /// <param name="pageNumber">The page number to evict.</param>
    void Evict(long pageNumber);

    /// <summary>
    /// Flushes all dirty pages to storage.
    /// </summary>
    void FlushAll();

    /// <summary>
    /// Clears all pages from the cache.
    /// </summary>
    void Clear();

    #endregion

    #region Async Operations

    /// <summary>
    /// Gets a page from the cache asynchronously, loading from storage if necessary.
    /// </summary>
    /// <param name="pageNumber">The page number to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cached page.</returns>
    /// <remarks>
    /// Use this method for async-only storage backends like IndexedDB.
    /// Default implementation calls sync <see cref="GetPage"/>.
    /// </remarks>
    ValueTask<Cache.CachedPage> GetPageAsync(long pageNumber, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(GetPage(pageNumber));
    }

    /// <summary>
    /// Creates a new page in the cache asynchronously (for newly allocated pages).
    /// </summary>
    /// <param name="pageNumber">The page number to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The newly created cached page.</returns>
    /// <remarks>
    /// Use this method for async-only storage backends like IndexedDB.
    /// Default implementation calls sync <see cref="CreatePage"/>.
    /// </remarks>
    ValueTask<Cache.CachedPage> CreatePageAsync(long pageNumber, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(CreatePage(pageNumber));
    }

    /// <summary>
    /// Flushes all dirty pages to storage asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask FlushAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Evicts a specific page from the cache asynchronously.
    /// </summary>
    /// <param name="pageNumber">The page number to evict.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// Default implementation calls sync <see cref="Evict"/>.
    /// </remarks>
    ValueTask EvictAsync(long pageNumber, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Evict(pageNumber);
        return ValueTask.CompletedTask;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the current number of cached pages.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets the number of dirty pages in the cache.
    /// </summary>
    int DirtyCount { get; }

    /// <summary>
    /// Gets the unique provider key identifying this cache implementation.
    /// Used for validation when opening existing databases.
    /// </summary>
    /// <example>
    /// "lru" for LRU cache, "clock" for sharded clock cache.
    /// </example>
    string ProviderKey { get; }

    #endregion
}

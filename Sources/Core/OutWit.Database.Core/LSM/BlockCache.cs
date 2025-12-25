using System.Collections.Concurrent;

namespace OutWit.Database.Core.LSM
{
    /// <summary>
    /// Thread-safe LRU cache for SSTable data blocks.
    /// Reduces disk I/O by caching recently accessed blocks in memory.
    /// </summary>
    public sealed class BlockCache : IDisposable
    {
        #region Nested Types

        private sealed class CacheEntry
        {
            public required byte[] Data { get; init; }
            public required int Size { get; init; }
            public long LastAccessTicks;
        }

        #endregion

        #region Fields

        private readonly ConcurrentDictionary<(string FilePath, int BlockIndex), CacheEntry> m_cache = new();
        private readonly long m_maxSizeBytes;
        private long m_currentSizeBytes;
        private long m_hits;
        private long m_misses;
        private bool m_disposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new block cache with specified maximum size.
        /// </summary>
        /// <param name="maxSizeBytes">Maximum cache size in bytes.</param>
        public BlockCache(long maxSizeBytes = 64 * 1024 * 1024) // Default 64 MB
        {
            if (maxSizeBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxSizeBytes), "Cache size must be positive");
            
            m_maxSizeBytes = maxSizeBytes;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Tries to get a cached block.
        /// </summary>
        /// <param name="filePath">SSTable file path.</param>
        /// <param name="blockIndex">Block index within the file.</param>
        /// <param name="data">Cached block data if found.</param>
        /// <returns>True if block was in cache.</returns>
        public bool TryGet(string filePath, int blockIndex, out byte[]? data)
        {
            ThrowIfDisposed();
            
            var key = (filePath, blockIndex);
            if (m_cache.TryGetValue(key, out var entry))
            {
                // Update access time for LRU
                entry.LastAccessTicks = Environment.TickCount64;
                Interlocked.Increment(ref m_hits);
                data = entry.Data;
                return true;
            }

            Interlocked.Increment(ref m_misses);
            data = null;
            return false;
        }

        /// <summary>
        /// Adds a block to the cache.
        /// </summary>
        /// <param name="filePath">SSTable file path.</param>
        /// <param name="blockIndex">Block index within the file.</param>
        /// <param name="data">Block data to cache.</param>
        public void Put(string filePath, int blockIndex, byte[] data)
        {
            ThrowIfDisposed();
            
            if (data.Length > m_maxSizeBytes / 4)
            {
                // Don't cache blocks larger than 25% of total cache size
                return;
            }

            var key = (filePath, blockIndex);
            var entry = new CacheEntry
            {
                Data = data,
                Size = data.Length,
                LastAccessTicks = Environment.TickCount64
            };

            // Try to add or update
            if (m_cache.TryGetValue(key, out var existing))
            {
                // Update existing entry
                if (m_cache.TryUpdate(key, entry, existing))
                {
                    Interlocked.Add(ref m_currentSizeBytes, entry.Size - existing.Size);
                }
            }
            else
            {
                // Add new entry
                if (m_cache.TryAdd(key, entry))
                {
                    Interlocked.Add(ref m_currentSizeBytes, entry.Size);
                }
            }

            // Evict if over capacity
            EvictIfNeeded();
        }

        /// <summary>
        /// Invalidates all cached blocks for a specific file.
        /// Call this when an SSTable is deleted or compacted.
        /// </summary>
        /// <param name="filePath">SSTable file path to invalidate.</param>
        public void Invalidate(string filePath)
        {
            ThrowIfDisposed();
            
            var keysToRemove = m_cache.Keys.Where(k => k.FilePath == filePath).ToList();
            foreach (var key in keysToRemove)
            {
                if (m_cache.TryRemove(key, out var entry))
                {
                    Interlocked.Add(ref m_currentSizeBytes, -entry.Size);
                }
            }
        }

        /// <summary>
        /// Clears all cached blocks.
        /// </summary>
        public void Clear()
        {
            ThrowIfDisposed();
            
            m_cache.Clear();
            Interlocked.Exchange(ref m_currentSizeBytes, 0);
        }

        #endregion

        #region Private Methods

        private void EvictIfNeeded()
        {
            // Fast path: no eviction needed
            if (Interlocked.Read(ref m_currentSizeBytes) <= m_maxSizeBytes)
                return;

            // Evict oldest entries until under capacity
            // Target 90% capacity to avoid frequent evictions
            var targetSize = (long)(m_maxSizeBytes * 0.9);

            var entries = m_cache
                .OrderBy(kvp => kvp.Value.LastAccessTicks)
                .ToList();

            foreach (var kvp in entries)
            {
                if (Interlocked.Read(ref m_currentSizeBytes) <= targetSize)
                    break;

                if (m_cache.TryRemove(kvp.Key, out var entry))
                {
                    Interlocked.Add(ref m_currentSizeBytes, -entry.Size);
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
            m_cache.Clear();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the maximum cache size in bytes.
        /// </summary>
        public long MaxSizeBytes => m_maxSizeBytes;

        /// <summary>
        /// Gets the current cache size in bytes.
        /// </summary>
        public long CurrentSizeBytes => Interlocked.Read(ref m_currentSizeBytes);

        /// <summary>
        /// Gets the number of cached blocks.
        /// </summary>
        public int Count => m_cache.Count;

        /// <summary>
        /// Gets the number of cache hits.
        /// </summary>
        public long Hits => Interlocked.Read(ref m_hits);

        /// <summary>
        /// Gets the number of cache misses.
        /// </summary>
        public long Misses => Interlocked.Read(ref m_misses);

        /// <summary>
        /// Gets the cache hit ratio (0.0 to 1.0).
        /// </summary>
        public double HitRatio
        {
            get
            {
                var total = Hits + Misses;
                return total == 0 ? 0.0 : (double)Hits / total;
            }
        }

        #endregion
    }
}

using OutWit.Database.Core.Interfaces;

namespace OutWit.Database.Core.Stores
{
    /// <summary>
    /// Extension methods for getting statistics from any IKeyValueStore.
    /// Falls back to scanning if native statistics are not available.
    /// </summary>
    public static class KeyValueStoreStatisticsExtensions
    {
        /// <summary>
        /// Gets the count of key-value pairs in the store.
        /// Uses native Count() if available, otherwise scans all keys.
        /// </summary>
        /// <param name="store">The key-value store.</param>
        /// <returns>Number of entries.</returns>
        public static long Count(this IKeyValueStore store)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            // Use native statistics if available
            if (store is IKeyValueStoreStatistics stats)
            {
                return stats.Count();
            }

            // Fallback: scan and count
            return store.Scan(null, null).LongCount();
        }

        /// <summary>
        /// Gets the count of key-value pairs asynchronously.
        /// </summary>
        /// <param name="store">The key-value store.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of entries.</returns>
        public static async ValueTask<long> CountAsync(
            this IKeyValueStore store, 
            CancellationToken cancellationToken = default)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            // Use native statistics if available
            if (store is IKeyValueStoreStatistics stats)
            {
                return await stats.CountAsync(cancellationToken).ConfigureAwait(false);
            }

            // Fallback: scan and count
            long count = 0;
            await foreach (var _ in store.ScanAsync(null, null, cancellationToken).ConfigureAwait(false))
            {
                count++;
            }
            return count;
        }

        /// <summary>
        /// Gets the approximate size of the store in bytes.
        /// Returns -1 if not available.
        /// </summary>
        /// <param name="store">The key-value store.</param>
        /// <returns>Size in bytes or -1.</returns>
        public static long GetApproximateSizeInBytes(this IKeyValueStore store)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            if (store is IKeyValueStoreStatistics stats)
            {
                return stats.ApproximateSizeInBytes;
            }

            return -1;
        }

        /// <summary>
        /// Gets statistics wrapper for any store.
        /// </summary>
        /// <param name="store">The key-value store.</param>
        /// <returns>Statistics wrapper.</returns>
        public static StoreStatistics GetStatistics(this IKeyValueStore store)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            return new StoreStatistics(store);
        }

        /// <summary>
        /// Checks if the store is empty.
        /// </summary>
        /// <param name="store">The key-value store.</param>
        /// <returns>True if empty.</returns>
        public static bool IsEmpty(this IKeyValueStore store)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            // Use native statistics if available
            if (store is IKeyValueStoreStatistics stats)
            {
                return stats.Count() == 0;
            }

            // Fallback: try to get first item
            return !store.Scan(null, null).Any();
        }

        /// <summary>
        /// Checks if the store has a key (alias for ContainsKey).
        /// </summary>
        /// <param name="store">The key-value store.</param>
        /// <param name="key">The key to check.</param>
        /// <returns>True if key exists.</returns>
        public static bool ContainsKey(this IKeyValueStore store, ReadOnlySpan<byte> key)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            return store.Get(key) != null;
        }
    }

    /// <summary>
    /// Wrapper class providing statistics for any IKeyValueStore.
    /// </summary>
    public sealed class StoreStatistics : IKeyValueStoreStatistics
    {
        #region Fields

        private readonly IKeyValueStore m_store;
        private readonly IKeyValueStoreStatistics? m_nativeStats;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a statistics wrapper for the given store.
        /// </summary>
        /// <param name="store">The store to get statistics for.</param>
        public StoreStatistics(IKeyValueStore store)
        {
            m_store = store ?? throw new ArgumentNullException(nameof(store));
            m_nativeStats = store as IKeyValueStoreStatistics;
        }

        #endregion

        #region Count

        /// <inheritdoc/>
        public long Count()
        {
            if (m_nativeStats != null)
            {
                return m_nativeStats.Count();
            }

            return m_store.Scan(null, null).LongCount();
        }

        /// <inheritdoc/>
        public async ValueTask<long> CountAsync(CancellationToken cancellationToken = default)
        {
            if (m_nativeStats != null)
            {
                return await m_nativeStats.CountAsync(cancellationToken).ConfigureAwait(false);
            }

            long count = 0;
            await foreach (var _ in m_store.ScanAsync(null, null, cancellationToken).ConfigureAwait(false))
            {
                count++;
            }
            return count;
        }

        #endregion

        #region Properties

        /// <inheritdoc/>
        public long ApproximateSizeInBytes => m_nativeStats?.ApproximateSizeInBytes ?? -1;

        /// <inheritdoc/>
        public long EstimatedKeyCount => m_nativeStats?.EstimatedKeyCount ?? Count();

        /// <inheritdoc/>
        public bool AreStatisticsExact => m_nativeStats?.AreStatisticsExact ?? true;

        /// <summary>
        /// Gets whether native statistics are available.
        /// </summary>
        public bool HasNativeStatistics => m_nativeStats != null;

        #endregion
    }
}

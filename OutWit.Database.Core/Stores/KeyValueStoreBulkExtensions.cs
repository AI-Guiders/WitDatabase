using OutWit.Database.Core.Interfaces;

namespace OutWit.Database.Core.Stores
{
    /// <summary>
    /// Extension methods for bulk operations on IKeyValueStore.
    /// These methods work with any store implementation, using native bulk operations
    /// if available or falling back to sequential operations.
    /// </summary>
    public static class KeyValueStoreBulkExtensions
    {
        #region BulkPut

        /// <summary>
        /// Inserts or updates multiple key-value pairs.
        /// Uses native bulk operations if available, otherwise falls back to sequential Put.
        /// </summary>
        /// <param name="store">The key-value store.</param>
        /// <param name="items">The key-value pairs to insert or update.</param>
        /// <returns>Number of items successfully processed.</returns>
        public static int BulkPut(this IKeyValueStore store, IEnumerable<(byte[] Key, byte[] Value)> items)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            if (items == null)
                throw new ArgumentNullException(nameof(items));

            // Use native bulk operations if available
            if (store is IBulkKeyValueStore bulkStore)
            {
                return bulkStore.BulkPut(items);
            }

            // Fallback to sequential operations
            int count = 0;
            foreach (var (key, value) in items)
            {
                store.Put(key, value);
                count++;
            }
            return count;
        }

        /// <summary>
        /// Inserts or updates multiple key-value pairs asynchronously.
        /// </summary>
        /// <param name="store">The key-value store.</param>
        /// <param name="items">The key-value pairs to insert or update.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of items successfully processed.</returns>
        public static async ValueTask<int> BulkPutAsync(
            this IKeyValueStore store,
            IEnumerable<(byte[] Key, byte[] Value)> items,
            CancellationToken cancellationToken = default)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            if (items == null)
                throw new ArgumentNullException(nameof(items));

            // Use native bulk operations if available
            if (store is IBulkKeyValueStore bulkStore)
            {
                return await bulkStore.BulkPutAsync(items, cancellationToken).ConfigureAwait(false);
            }

            // Fallback to sequential operations
            int count = 0;
            foreach (var (key, value) in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await store.PutAsync(key, value, cancellationToken).ConfigureAwait(false);
                count++;
            }
            return count;
        }

        #endregion

        #region BulkDelete

        /// <summary>
        /// Deletes multiple keys.
        /// Uses native bulk operations if available, otherwise falls back to sequential Delete.
        /// </summary>
        /// <param name="store">The key-value store.</param>
        /// <param name="keys">The keys to delete.</param>
        /// <returns>Number of keys successfully deleted.</returns>
        public static int BulkDelete(this IKeyValueStore store, IEnumerable<byte[]> keys)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            if (keys == null)
                throw new ArgumentNullException(nameof(keys));

            // Use native bulk operations if available
            if (store is IBulkKeyValueStore bulkStore)
            {
                return bulkStore.BulkDelete(keys);
            }

            // Fallback to sequential operations
            int deletedCount = 0;
            foreach (var key in keys)
            {
                if (store.Delete(key))
                {
                    deletedCount++;
                }
            }
            return deletedCount;
        }

        /// <summary>
        /// Deletes multiple keys asynchronously.
        /// </summary>
        /// <param name="store">The key-value store.</param>
        /// <param name="keys">The keys to delete.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of keys successfully deleted.</returns>
        public static async ValueTask<int> BulkDeleteAsync(
            this IKeyValueStore store,
            IEnumerable<byte[]> keys,
            CancellationToken cancellationToken = default)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            if (keys == null)
                throw new ArgumentNullException(nameof(keys));

            // Use native bulk operations if available
            if (store is IBulkKeyValueStore bulkStore)
            {
                return await bulkStore.BulkDeleteAsync(keys, cancellationToken).ConfigureAwait(false);
            }

            // Fallback to sequential operations
            int deletedCount = 0;
            foreach (var key in keys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await store.DeleteAsync(key, cancellationToken).ConfigureAwait(false))
                {
                    deletedCount++;
                }
            }
            return deletedCount;
        }

        #endregion

        #region Batch with Flush

        /// <summary>
        /// Performs bulk put operation and flushes the store.
        /// Useful for ensuring all data is persisted immediately.
        /// </summary>
        /// <param name="store">The key-value store.</param>
        /// <param name="items">The key-value pairs to insert or update.</param>
        /// <param name="flushAfter">If true, flush after bulk put.</param>
        /// <returns>Number of items successfully processed.</returns>
        public static int BulkPutAndFlush(
            this IKeyValueStore store,
            IEnumerable<(byte[] Key, byte[] Value)> items,
            bool flushAfter = true)
        {
            var count = store.BulkPut(items);
            if (flushAfter)
            {
                store.Flush();
            }
            return count;
        }

        /// <summary>
        /// Performs bulk delete operation and flushes the store.
        /// </summary>
        /// <param name="store">The key-value store.</param>
        /// <param name="keys">The keys to delete.</param>
        /// <param name="flushAfter">If true, flush after bulk delete.</param>
        /// <returns>Number of keys successfully deleted.</returns>
        public static int BulkDeleteAndFlush(
            this IKeyValueStore store,
            IEnumerable<byte[]> keys,
            bool flushAfter = true)
        {
            var count = store.BulkDelete(keys);
            if (flushAfter)
            {
                store.Flush();
            }
            return count;
        }

        #endregion
    }
}

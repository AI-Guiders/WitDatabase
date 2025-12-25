using System.Runtime.CompilerServices;
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

        #region Streaming Insert

        /// <summary>
        /// Streams key-value pairs into the store in batches.
        /// Flushes after each batch for durability and memory management.
        /// </summary>
        /// <param name="store">The key-value store.</param>
        /// <param name="items">The key-value pairs to stream.</param>
        /// <param name="batchSize">Number of items per batch before flush.</param>
        /// <param name="progress">Optional progress callback (called after each batch with total count).</param>
        /// <returns>Total number of items inserted.</returns>
        public static int StreamingPut(
            this IKeyValueStore store,
            IEnumerable<(byte[] Key, byte[] Value)> items,
            int batchSize = 1000,
            Action<int>? progress = null)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            if (items == null)
                throw new ArgumentNullException(nameof(items));

            if (batchSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be positive.");

            int totalCount = 0;
            int batchCount = 0;

            foreach (var (key, value) in items)
            {
                store.Put(key, value);
                totalCount++;
                batchCount++;

                if (batchCount >= batchSize)
                {
                    store.Flush();
                    progress?.Invoke(totalCount);
                    batchCount = 0;
                }
            }

            // Flush remaining items
            if (batchCount > 0)
            {
                store.Flush();
                progress?.Invoke(totalCount);
            }

            return totalCount;
        }

        /// <summary>
        /// Streams key-value pairs into the store asynchronously in batches.
        /// </summary>
        /// <param name="store">The key-value store.</param>
        /// <param name="items">The key-value pairs to stream.</param>
        /// <param name="batchSize">Number of items per batch before flush.</param>
        /// <param name="progress">Optional progress callback.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Total number of items inserted.</returns>
        public static async ValueTask<int> StreamingPutAsync(
            this IKeyValueStore store,
            IEnumerable<(byte[] Key, byte[] Value)> items,
            int batchSize = 1000,
            Action<int>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            if (items == null)
                throw new ArgumentNullException(nameof(items));

            if (batchSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be positive.");

            int totalCount = 0;
            int batchCount = 0;

            foreach (var (key, value) in items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await store.PutAsync(key, value, cancellationToken).ConfigureAwait(false);
                totalCount++;
                batchCount++;

                if (batchCount >= batchSize)
                {
                    await store.FlushAsync(cancellationToken).ConfigureAwait(false);
                    progress?.Invoke(totalCount);
                    batchCount = 0;
                }
            }

            // Flush remaining items
            if (batchCount > 0)
            {
                await store.FlushAsync(cancellationToken).ConfigureAwait(false);
                progress?.Invoke(totalCount);
            }

            return totalCount;
        }

        /// <summary>
        /// Streams key-value pairs from an async enumerable into the store in batches.
        /// </summary>
        /// <param name="store">The key-value store.</param>
        /// <param name="items">The async enumerable of key-value pairs.</param>
        /// <param name="batchSize">Number of items per batch before flush.</param>
        /// <param name="progress">Optional progress callback.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Total number of items inserted.</returns>
        public static async ValueTask<int> StreamingPutAsync(
            this IKeyValueStore store,
            IAsyncEnumerable<(byte[] Key, byte[] Value)> items,
            int batchSize = 1000,
            Action<int>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            if (items == null)
                throw new ArgumentNullException(nameof(items));

            if (batchSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be positive.");

            int totalCount = 0;
            int batchCount = 0;

            await foreach (var (key, value) in items.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                await store.PutAsync(key, value, cancellationToken).ConfigureAwait(false);
                totalCount++;
                batchCount++;

                if (batchCount >= batchSize)
                {
                    await store.FlushAsync(cancellationToken).ConfigureAwait(false);
                    progress?.Invoke(totalCount);
                    batchCount = 0;
                }
            }

            // Flush remaining items
            if (batchCount > 0)
            {
                await store.FlushAsync(cancellationToken).ConfigureAwait(false);
                progress?.Invoke(totalCount);
            }

            return totalCount;
        }

        #endregion

        #region Streaming with Transaction

        /// <summary>
        /// Streams key-value pairs into a transactional store in batched transactions.
        /// Each batch is committed as a separate transaction for durability and memory management.
        /// </summary>
        /// <param name="store">The transactional store.</param>
        /// <param name="items">The key-value pairs to stream.</param>
        /// <param name="batchSize">Number of items per transaction.</param>
        /// <param name="progress">Optional progress callback (called after each commit with total count).</param>
        /// <returns>Total number of items inserted.</returns>
        public static int StreamingPutWithTransaction(
            this ITransactionalStore store,
            IEnumerable<(byte[] Key, byte[] Value)> items,
            int batchSize = 1000,
            Action<int>? progress = null)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            if (items == null)
                throw new ArgumentNullException(nameof(items));

            if (batchSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be positive.");

            int totalCount = 0;
            int batchCount = 0;
            ITransaction? tx = null;

            try
            {
                foreach (var (key, value) in items)
                {
                    if (tx == null)
                    {
                        tx = store.BeginTransaction();
                    }

                    tx.Put(key, value);
                    totalCount++;
                    batchCount++;

                    if (batchCount >= batchSize)
                    {
                        tx.Commit();
                        tx.Dispose();
                        tx = null;
                        progress?.Invoke(totalCount);
                        batchCount = 0;
                    }
                }

                // Commit remaining items
                if (tx != null)
                {
                    tx.Commit();
                    progress?.Invoke(totalCount);
                }
            }
            finally
            {
                tx?.Dispose();
            }

            return totalCount;
        }

        #endregion
    }
}

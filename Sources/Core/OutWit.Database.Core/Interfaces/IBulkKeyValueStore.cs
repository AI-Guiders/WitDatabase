namespace OutWit.Database.Core.Interfaces
{
    /// <summary>
    /// Extended interface for key-value stores that support bulk operations.
    /// Provides optimized methods for batch inserts and deletes.
    /// </summary>
    public interface IBulkKeyValueStore : IKeyValueStore
    {
        /// <summary>
        /// Inserts or updates multiple key-value pairs in a single optimized operation.
        /// More efficient than individual Put calls for large batches.
        /// </summary>
        /// <param name="items">The key-value pairs to insert or update.</param>
        /// <returns>Number of items successfully processed.</returns>
        int BulkPut(IEnumerable<(byte[] Key, byte[] Value)> items);

        /// <summary>
        /// Inserts or updates multiple key-value pairs asynchronously.
        /// </summary>
        /// <param name="items">The key-value pairs to insert or update.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of items successfully processed.</returns>
        ValueTask<int> BulkPutAsync(IEnumerable<(byte[] Key, byte[] Value)> items, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes multiple keys in a single optimized operation.
        /// More efficient than individual Delete calls for large batches.
        /// </summary>
        /// <param name="keys">The keys to delete.</param>
        /// <returns>Number of keys successfully deleted.</returns>
        int BulkDelete(IEnumerable<byte[]> keys);

        /// <summary>
        /// Deletes multiple keys asynchronously.
        /// </summary>
        /// <param name="keys">The keys to delete.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of keys successfully deleted.</returns>
        ValueTask<int> BulkDeleteAsync(IEnumerable<byte[]> keys, CancellationToken cancellationToken = default);
    }
}

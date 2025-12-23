namespace OutWit.Database.Core.Interfaces
{
    /// <summary>
    /// Provides row versioning support for optimistic concurrency control.
    /// Each Put operation generates a new version number for the key.
    /// </summary>
    public interface IVersionedKeyValueStore : IKeyValueStore
    {
        /// <summary>
        /// Gets the value and its version for a key.
        /// </summary>
        /// <param name="key">The key to look up.</param>
        /// <returns>The value and version, or null if not found.</returns>
        (byte[] Value, long Version)? GetWithVersion(ReadOnlySpan<byte> key);

        /// <summary>
        /// Gets the value and its version asynchronously.
        /// </summary>
        /// <param name="key">The key to look up.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The value and version, or null if not found.</returns>
        ValueTask<(byte[] Value, long Version)?> GetWithVersionAsync(
            byte[] key, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Inserts or updates a key-value pair and returns the new version.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns>The new version number assigned to this key.</returns>
        long PutWithVersion(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value);

        /// <summary>
        /// Inserts or updates a key-value pair asynchronously and returns the new version.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The new version number assigned to this key.</returns>
        ValueTask<long> PutWithVersionAsync(
            byte[] key, 
            byte[] value, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Conditionally updates a key-value pair only if the current version matches.
        /// Used for optimistic concurrency control.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The new value.</param>
        /// <param name="expectedVersion">The expected current version.</param>
        /// <returns>
        /// Tuple of (Success, NewVersion). 
        /// If Success is true, NewVersion contains the new version.
        /// If Success is false, the update was rejected due to version mismatch.
        /// </returns>
        (bool Success, long NewVersion) ConditionalPut(
            ReadOnlySpan<byte> key, 
            ReadOnlySpan<byte> value, 
            long expectedVersion);

        /// <summary>
        /// Conditionally updates asynchronously only if the current version matches.
        /// </summary>
        (bool Success, long NewVersion) ConditionalPutAsync(
            byte[] key, 
            byte[] value, 
            long expectedVersion,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Conditionally deletes a key only if the current version matches.
        /// </summary>
        /// <param name="key">The key to delete.</param>
        /// <param name="expectedVersion">The expected current version.</param>
        /// <returns>True if deleted, false if version mismatch or not found.</returns>
        bool ConditionalDelete(ReadOnlySpan<byte> key, long expectedVersion);

        /// <summary>
        /// Conditionally deletes asynchronously only if the current version matches.
        /// </summary>
        ValueTask<bool> ConditionalDeleteAsync(
            byte[] key, 
            long expectedVersion,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current version of a key without retrieving the value.
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <returns>The current version, or null if key not found.</returns>
        long? GetVersion(ReadOnlySpan<byte> key);

        /// <summary>
        /// Gets the global version counter.
        /// This is incremented on each Put operation.
        /// </summary>
        long CurrentGlobalVersion { get; }
    }
}

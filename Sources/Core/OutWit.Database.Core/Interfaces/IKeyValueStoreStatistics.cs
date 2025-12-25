namespace OutWit.Database.Core.Interfaces
{
    /// <summary>
    /// Interface for key-value stores that can provide statistics.
    /// Used by query optimizer for cost estimation.
    /// </summary>
    public interface IKeyValueStoreStatistics
    {
        /// <summary>
        /// Gets the total number of key-value pairs in the store.
        /// May be approximate for some implementations.
        /// </summary>
        /// <returns>Number of entries in the store.</returns>
        long Count();

        /// <summary>
        /// Gets the total number of key-value pairs asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of entries in the store.</returns>
        ValueTask<long> CountAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the approximate size of the store in bytes.
        /// Includes all data, metadata, and overhead.
        /// </summary>
        /// <returns>Approximate size in bytes, or -1 if not available.</returns>
        long ApproximateSizeInBytes { get; }

        /// <summary>
        /// Gets the estimated number of distinct keys.
        /// Used for cardinality estimation in query planning.
        /// </summary>
        /// <returns>Estimated unique key count.</returns>
        long EstimatedKeyCount { get; }

        /// <summary>
        /// Gets whether the statistics are exact or approximate.
        /// </summary>
        bool AreStatisticsExact { get; }
    }

    /// <summary>
    /// Extended statistics for index-aware stores.
    /// </summary>
    public interface IIndexStatistics
    {
        /// <summary>
        /// Gets the depth of the index structure (for tree-based indexes).
        /// </summary>
        int TreeDepth { get; }

        /// <summary>
        /// Gets the number of pages/nodes in the index.
        /// </summary>
        long PageCount { get; }

        /// <summary>
        /// Gets the fill factor (0.0 to 1.0) indicating how full pages are on average.
        /// </summary>
        double FillFactor { get; }

        /// <summary>
        /// Gets the average key size in bytes.
        /// </summary>
        int AverageKeySize { get; }

        /// <summary>
        /// Gets the average value size in bytes.
        /// </summary>
        int AverageValueSize { get; }
    }
}

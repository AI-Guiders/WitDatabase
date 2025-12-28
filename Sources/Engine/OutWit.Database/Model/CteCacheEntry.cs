namespace OutWit.Database.Model
{
    /// <summary>
    /// Represents a cached CTE result.
    /// </summary>
    public sealed class CteCacheEntry
    {
        /// <summary>
        /// The cached rows from the CTE execution.
        /// </summary>
        public required IReadOnlyList<WitSqlRow> Rows { get; init; }

        /// <summary>
        /// The schema of the cached CTE result.
        /// </summary>
        public required IReadOnlyList<WitSqlColumnInfo> Schema { get; init; }
    }
}
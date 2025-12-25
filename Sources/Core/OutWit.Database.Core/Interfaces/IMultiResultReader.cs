namespace OutWit.Database.Core.Interfaces
{
    /// <summary>
    /// Provides a way to read multiple result sets from a batch execution.
    /// Used for queries that return multiple result sets, such as stored procedures
    /// or batch SQL statements.
    /// </summary>
    public interface IMultiResultReader : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Gets the current result set as an enumerable of key-value pairs.
        /// Returns null if there are no more result sets.
        /// </summary>
        IEnumerable<(byte[] Key, byte[] Value)>? CurrentResult { get; }

        /// <summary>
        /// Advances to the next result set.
        /// </summary>
        /// <returns>True if there is another result set; false otherwise.</returns>
        bool NextResult();

        /// <summary>
        /// Advances to the next result set asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if there is another result set; false otherwise.</returns>
        ValueTask<bool> NextResultAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the number of result sets available.
        /// Returns -1 if the count is unknown (streaming mode).
        /// </summary>
        int ResultSetCount { get; }

        /// <summary>
        /// Gets the zero-based index of the current result set.
        /// Returns -1 if positioned before the first result set.
        /// </summary>
        int CurrentResultIndex { get; }

        /// <summary>
        /// Gets a value indicating whether there are more result sets available.
        /// </summary>
        bool HasMoreResults { get; }

        /// <summary>
        /// Gets the number of rows affected by the current result set (for DML statements).
        /// Returns -1 for SELECT statements or if not applicable.
        /// </summary>
        int RecordsAffected { get; }

        /// <summary>
        /// Gets a value indicating whether the reader has been closed.
        /// </summary>
        bool IsClosed { get; }
    }
}

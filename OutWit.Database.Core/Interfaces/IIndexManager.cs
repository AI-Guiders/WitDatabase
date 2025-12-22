namespace OutWit.Database.Core.Interfaces
{
    /// <summary>
    /// Manages secondary indexes for a table.
    /// Coordinates index updates when data changes.
    /// </summary>
    public interface IIndexManager : IDisposable
    {
        #region Index Management

        /// <summary>
        /// Creates a new secondary index.
        /// </summary>
        /// <param name="name">The unique name of the index.</param>
        /// <param name="isUnique">Whether the index should enforce uniqueness.</param>
        /// <returns>The created index.</returns>
        /// <exception cref="ArgumentException">Thrown when an index with the same name already exists.</exception>
        ISecondaryIndex CreateIndex(string name, bool isUnique);

        /// <summary>
        /// Gets an existing index by name.
        /// </summary>
        /// <param name="name">The name of the index.</param>
        /// <returns>The index, or null if not found.</returns>
        ISecondaryIndex? GetIndex(string name);

        /// <summary>
        /// Drops (removes) an index.
        /// </summary>
        /// <param name="name">The name of the index to drop.</param>
        /// <returns>True if the index was dropped; false if it didn't exist.</returns>
        bool DropIndex(string name);

        /// <summary>
        /// Checks if an index with the specified name exists.
        /// </summary>
        /// <param name="name">The name of the index.</param>
        /// <returns>True if the index exists; otherwise false.</returns>
        bool HasIndex(string name);

        #endregion

        #region Index Updates

        /// <summary>
        /// Notifies the index manager that a row has been inserted.
        /// Updates all relevant indexes.
        /// </summary>
        /// <param name="primaryKey">The primary key of the inserted row.</param>
        /// <param name="indexKeys">Dictionary of index names to their corresponding key values.</param>
        void OnRowInserted(ReadOnlySpan<byte> primaryKey, IReadOnlyDictionary<string, byte[]> indexKeys);

        /// <summary>
        /// Notifies the index manager that a row has been deleted.
        /// Removes entries from all relevant indexes.
        /// </summary>
        /// <param name="primaryKey">The primary key of the deleted row.</param>
        /// <param name="indexKeys">Dictionary of index names to their corresponding key values.</param>
        void OnRowDeleted(ReadOnlySpan<byte> primaryKey, IReadOnlyDictionary<string, byte[]> indexKeys);

        /// <summary>
        /// Notifies the index manager that a row has been updated.
        /// Updates indexes where the indexed column values changed.
        /// </summary>
        /// <param name="primaryKey">The primary key of the updated row.</param>
        /// <param name="oldIndexKeys">Old index key values.</param>
        /// <param name="newIndexKeys">New index key values.</param>
        void OnRowUpdated(
            ReadOnlySpan<byte> primaryKey, 
            IReadOnlyDictionary<string, byte[]> oldIndexKeys,
            IReadOnlyDictionary<string, byte[]> newIndexKeys);

        #endregion

        #region Flush

        /// <summary>
        /// Flushes all indexes to durable storage.
        /// </summary>
        void Flush();

        /// <summary>
        /// Flushes all indexes to durable storage asynchronously.
        /// </summary>
        ValueTask FlushAsync(CancellationToken cancellationToken = default);

        #endregion

        #region Properties

        /// <summary>
        /// Gets the names of all indexes managed by this manager.
        /// </summary>
        IReadOnlyList<string> IndexNames { get; }

        /// <summary>
        /// Gets the number of indexes managed by this manager.
        /// </summary>
        int IndexCount { get; }

        #endregion
    }
}

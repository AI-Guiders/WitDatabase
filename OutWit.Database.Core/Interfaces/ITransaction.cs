namespace OutWit.Database.Core.Interfaces
{
    /// <summary>
    /// Represents a database transaction.
    /// Changes are buffered until Commit() is called.
    /// </summary>
    public interface ITransaction : IDisposable, IAsyncDisposable
    {
        #region Get

        /// <summary>
        /// Gets the value for a key, considering uncommitted changes in this transaction.
        /// </summary>
        byte[]? Get(ReadOnlySpan<byte> key);

        /// <summary>
        /// Gets the value for a key asynchronously.
        /// </summary>
        ValueTask<byte[]?> GetAsync(byte[] key, CancellationToken cancellationToken = default);

        #endregion

        #region Put

        /// <summary>
        /// Inserts or updates a key-value pair within this transaction.
        /// </summary>
        void Put(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value);

        /// <summary>
        /// Inserts or updates a key-value pair asynchronously.
        /// </summary>
        ValueTask PutAsync(byte[] key, byte[] value, CancellationToken cancellationToken = default);

        #endregion

        #region Delete

        /// <summary>
        /// Deletes a key within this transaction.
        /// </summary>
        bool Delete(ReadOnlySpan<byte> key);


        /// <summary>
        /// Deletes a key asynchronously.
        /// </summary>
        ValueTask<bool> DeleteAsync(byte[] key, CancellationToken cancellationToken = default);

        #endregion

        #region Commit

        /// <summary>
        /// Commits all changes made in this transaction.
        /// After commit, the transaction cannot be used.
        /// </summary>
        void Commit();

        /// <summary>
        /// Commits all changes asynchronously.
        /// </summary>
        ValueTask CommitAsync(CancellationToken cancellationToken = default);

        #endregion

        #region Rollback

        /// <summary>
        /// Rolls back all changes made in this transaction.
        /// After rollback, the transaction cannot be used.
        /// </summary>
        void Rollback();

        /// <summary>
        /// Rolls back all changes asynchronously.
        /// </summary>
        ValueTask RollbackAsync(CancellationToken cancellationToken = default);

        #endregion

        #region Properties

        /// <summary>
        /// Gets the current state of the transaction.
        /// </summary>
        TransactionState State { get; }

        /// <summary>
        /// Gets the transaction ID.
        /// </summary>
        long TransactionId { get; }

        #endregion
    }
}

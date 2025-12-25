namespace OutWit.Database.Core.Interfaces
{
    /// <summary>
    /// Represents a database transaction with savepoint support.
    /// Extends <see cref="ITransaction"/> with the ability to create, rollback to, and release savepoints.
    /// </summary>
    /// <remarks>
    /// Savepoints allow partial rollback within a transaction. They are used by EF Core for 
    /// nested transactions and SaveChanges with retry logic.
    /// </remarks>
    public interface ITransactionWithSavepoints : ITransaction
    {
        #region CreateSavepoint

        /// <summary>
        /// Creates a savepoint with the specified name.
        /// </summary>
        /// <param name="name">The name of the savepoint. Must be unique within the transaction.</param>
        /// <exception cref="ArgumentNullException">Thrown when name is null or empty.</exception>
        /// <exception cref="ArgumentException">Thrown when a savepoint with the same name already exists.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the transaction is not active.</exception>
        void CreateSavepoint(string name);

        /// <summary>
        /// Creates a savepoint with the specified name asynchronously.
        /// </summary>
        /// <param name="name">The name of the savepoint. Must be unique within the transaction.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="ArgumentNullException">Thrown when name is null or empty.</exception>
        /// <exception cref="ArgumentException">Thrown when a savepoint with the same name already exists.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the transaction is not active.</exception>
        ValueTask CreateSavepointAsync(string name, CancellationToken cancellationToken = default);

        #endregion

        #region RollbackToSavepoint

        /// <summary>
        /// Rolls back all changes made after the specified savepoint was created.
        /// The savepoint remains valid and can be used again.
        /// </summary>
        /// <param name="name">The name of the savepoint to rollback to.</param>
        /// <exception cref="ArgumentNullException">Thrown when name is null or empty.</exception>
        /// <exception cref="ArgumentException">Thrown when no savepoint with the given name exists.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the transaction is not active.</exception>
        void RollbackToSavepoint(string name);

        /// <summary>
        /// Rolls back all changes made after the specified savepoint was created asynchronously.
        /// The savepoint remains valid and can be used again.
        /// </summary>
        /// <param name="name">The name of the savepoint to rollback to.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="ArgumentNullException">Thrown when name is null or empty.</exception>
        /// <exception cref="ArgumentException">Thrown when no savepoint with the given name exists.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the transaction is not active.</exception>
        ValueTask RollbackToSavepointAsync(string name, CancellationToken cancellationToken = default);

        #endregion

        #region ReleaseSavepoint

        /// <summary>
        /// Releases (removes) the specified savepoint and all savepoints created after it.
        /// The changes made after the savepoint remain in effect.
        /// </summary>
        /// <param name="name">The name of the savepoint to release.</param>
        /// <exception cref="ArgumentNullException">Thrown when name is null or empty.</exception>
        /// <exception cref="ArgumentException">Thrown when no savepoint with the given name exists.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the transaction is not active.</exception>
        void ReleaseSavepoint(string name);

        /// <summary>
        /// Releases (removes) the specified savepoint and all savepoints created after it asynchronously.
        /// The changes made after the savepoint remain in effect.
        /// </summary>
        /// <param name="name">The name of the savepoint to release.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="ArgumentNullException">Thrown when name is null or empty.</exception>
        /// <exception cref="ArgumentException">Thrown when no savepoint with the given name exists.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the transaction is not active.</exception>
        ValueTask ReleaseSavepointAsync(string name, CancellationToken cancellationToken = default);

        #endregion

        #region Properties

        /// <summary>
        /// Gets the names of all active savepoints in order of creation.
        /// </summary>
        IReadOnlyList<string> Savepoints { get; }

        /// <summary>
        /// Gets whether the specified savepoint exists.
        /// </summary>
        /// <param name="name">The name of the savepoint.</param>
        /// <returns>True if the savepoint exists; otherwise false.</returns>
        bool HasSavepoint(string name);

        #endregion
    }
}

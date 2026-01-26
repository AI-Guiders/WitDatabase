namespace OutWit.Database.Core.Interfaces
{
    /// <summary>
    /// Store that supports transactions.
    /// </summary>
    public interface ITransactionalStore : IKeyValueStore
    {
        /// <summary>
        /// Begins a new transaction with default isolation level (ReadCommitted).
        /// </summary>
        /// <returns>A new transaction.</returns>
        ITransaction BeginTransaction();

        /// <summary>
        /// Begins a new transaction with specified isolation level.
        /// </summary>
        /// <param name="isolationLevel">The isolation level for the transaction.</param>
        /// <returns>A new transaction.</returns>
        ITransaction BeginTransaction(WitIsolationLevel isolationLevel);

        /// <summary>
        /// Begins a new transaction asynchronously with default isolation level.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A new transaction.</returns>
        ValueTask<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Begins a new transaction asynchronously with specified isolation level.
        /// </summary>
        /// <param name="isolationLevel">The isolation level for the transaction.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A new transaction.</returns>
        ValueTask<ITransaction> BeginTransactionAsync(WitIsolationLevel isolationLevel, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the number of active transactions.
        /// </summary>
        int ActiveTransactionCount { get; }
    }
}

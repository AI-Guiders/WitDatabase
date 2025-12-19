namespace OutWit.Database.Core.Interfaces
{
    /// <summary>
    /// Store that supports transactions.
    /// </summary>
    public interface ITransactionalStore : IKeyValueStore
    {
        /// <summary>
        /// Begins a new transaction.
        /// </summary>
        /// <returns>A new transaction.</returns>
        ITransaction BeginTransaction();

        /// <summary>
        /// Begins a new transaction asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A new transaction.</returns>
        ValueTask<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the number of active transactions.
        /// </summary>
        int ActiveTransactionCount { get; }
    }
}

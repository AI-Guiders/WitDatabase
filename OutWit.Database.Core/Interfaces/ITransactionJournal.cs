namespace OutWit.Database.Core.Interfaces
{
    /// <summary>
    /// Interface for transaction journaling mechanisms.
    /// Provides durability guarantees for transactions.
    /// </summary>
    public interface ITransactionJournal : IDisposable
    {
        /// <summary>
        /// Logs the beginning of a transaction.
        /// </summary>
        void BeginTransaction(long transactionId);

        /// <summary>
        /// Logs a Put operation within a transaction.
        /// </summary>
        void LogPut(long transactionId, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value, ReadOnlySpan<byte> oldValue);

        /// <summary>
        /// Logs a Delete operation within a transaction.
        /// </summary>
        void LogDelete(long transactionId, ReadOnlySpan<byte> key, ReadOnlySpan<byte> oldValue);

        /// <summary>
        /// Logs the commit of a transaction.
        /// </summary>
        void CommitTransaction(long transactionId);

        /// <summary>
        /// Logs the rollback of a transaction.
        /// </summary>
        void RollbackTransaction(long transactionId);

        /// <summary>
        /// Ensures all pending writes are flushed to disk.
        /// </summary>
        void Sync();

        /// <summary>
        /// Recovers committed or rolls back uncommitted transactions after a crash.
        /// </summary>
        /// <returns>Number of operations recovered/rolled back.</returns>
        int Recover(IKeyValueStore store);

        /// <summary>
        /// Creates a checkpoint, truncating the journal.
        /// </summary>
        void Checkpoint();
    }

    /// <summary>
    /// Transaction journaling mode.
    /// </summary>
    public enum TransactionMode
    {
        /// <summary>
        /// Write-Ahead Logging - changes written to log before applying.
        /// Faster writes, allows concurrent readers during writes.
        /// </summary>
        Wal,

        /// <summary>
        /// Rollback Journal - original values saved before modification.
        /// Simpler, original values restored on rollback.
        /// </summary>
        RollbackJournal
    }
}

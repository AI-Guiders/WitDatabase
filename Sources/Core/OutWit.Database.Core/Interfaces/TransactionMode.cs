namespace OutWit.Database.Core.Interfaces
{
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

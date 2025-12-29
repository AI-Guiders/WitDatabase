namespace OutWit.Database.Core.Interfaces
{
    /// <summary>
    /// Transaction isolation levels.
    /// Defines the degree to which transactions are isolated from each other.
    /// </summary>
    /// <remarks>
    /// Higher isolation levels provide more data consistency but may reduce concurrency.
    /// </remarks>
    public enum WitIsolationLevel
    {
        /// <summary>
        /// Allows dirty reads. Transaction can see uncommitted changes from other transactions.
        /// Provides highest concurrency but lowest consistency.
        /// Phenomena allowed: dirty reads, non-repeatable reads, phantom reads.
        /// </summary>
        ReadUncommitted = 0,

        /// <summary>
        /// Only committed data is visible. Prevents dirty reads but allows non-repeatable reads.
        /// Most common default isolation level in databases.
        /// Phenomena allowed: non-repeatable reads, phantom reads.
        /// </summary>
        ReadCommitted = 1,

        /// <summary>
        /// Read locks are held for the duration of the transaction.
        /// Prevents dirty and non-repeatable reads.
        /// Phenomena allowed: phantom reads.
        /// </summary>
        RepeatableRead = 2,

        /// <summary>
        /// Highest isolation level. Transactions are completely isolated.
        /// Prevents all phenomena but may reduce concurrency significantly.
        /// Phenomena allowed: none.
        /// </summary>
        Serializable = 3,

        /// <summary>
        /// Snapshot isolation using multi-version concurrency control (MVCC).
        /// Each transaction sees a consistent snapshot of the database as of its start time.
        /// Provides good balance between isolation and concurrency.
        /// </summary>
        Snapshot = 4
    }
}

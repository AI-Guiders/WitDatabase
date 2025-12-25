namespace OutWit.Database.Parser.Schema.Types
{
    /// <summary>
    /// Represents SQL transaction isolation levels.
    /// </summary>
    public enum IsolationLevelType
    {
        /// <summary>
        /// Read Uncommitted - lowest isolation level, allows dirty reads.
        /// </summary>
        ReadUncommitted,

        /// <summary>
        /// Read Committed - prevents dirty reads but allows non-repeatable reads.
        /// </summary>
        ReadCommitted,

        /// <summary>
        /// Repeatable Read - prevents dirty and non-repeatable reads but allows phantom reads.
        /// </summary>
        RepeatableRead,

        /// <summary>
        /// Serializable - highest isolation level, prevents all concurrency issues.
        /// </summary>
        Serializable,

        /// <summary>
        /// Snapshot - provides a consistent view of data as of the start of the transaction.
        /// </summary>
        Snapshot
    }
}

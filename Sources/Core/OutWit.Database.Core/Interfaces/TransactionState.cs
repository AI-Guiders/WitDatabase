namespace OutWit.Database.Core.Interfaces
{
    /// <summary>
    /// State of a transaction.
    /// </summary>
    public enum TransactionState
    {
        /// <summary>
        /// Transaction is active and can accept operations.
        /// </summary>
        Active,

        /// <summary>
        /// Transaction has been committed successfully.
        /// </summary>
        Committed,

        /// <summary>
        /// Transaction has been rolled back.
        /// </summary>
        RolledBack
    }
}

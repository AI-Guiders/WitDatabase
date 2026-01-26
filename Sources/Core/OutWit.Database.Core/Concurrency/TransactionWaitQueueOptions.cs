namespace OutWit.Database.Core.Concurrency
{
    /// <summary>
    /// Priority for transactions in the wait queue.
    /// </summary>
    public enum TransactionPriority
    {
        /// <summary>
        /// Low priority - yields to higher priority transactions.
        /// </summary>
        Low = 0,

        /// <summary>
        /// Normal priority - default for most transactions.
        /// </summary>
        Normal = 1,

        /// <summary>
        /// High priority - processed before normal transactions.
        /// </summary>
        High = 2,

        /// <summary>
        /// Critical priority - highest priority, for system operations.
        /// </summary>
        Critical = 3
    }

    /// <summary>
    /// Options for configuring the transaction wait queue.
    /// </summary>
    public sealed class TransactionWaitQueueOptions
    {
        /// <summary>
        /// Gets or sets whether writers have priority over readers.
        /// When true, waiting writers are processed before waiting readers.
        /// Default is true.
        /// </summary>
        public bool WriterPriority { get; set; } = true;

        /// <summary>
        /// Gets or sets the default timeout for waiting in the queue.
        /// Default is 30 seconds.
        /// </summary>
        public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets whether to use FIFO ordering within the same priority level.
        /// When false, uses LIFO (stack) ordering.
        /// Default is true.
        /// </summary>
        public bool UseFifoOrdering { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of waiting transactions.
        /// When exceeded, new wait requests are rejected.
        /// Default is 1000.
        /// </summary>
        public int MaxWaitingTransactions { get; set; } = 1000;
    }
}

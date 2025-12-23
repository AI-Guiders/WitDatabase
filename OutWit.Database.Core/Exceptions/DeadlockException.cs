namespace OutWit.Database.Core.Exceptions
{
    /// <summary>
    /// Exception thrown when a deadlock is detected.
    /// </summary>
    public class DeadlockException : Exception
    {
        #region Constructors

        /// <summary>
        /// Creates a new deadlock exception.
        /// </summary>
        public DeadlockException()
            : base("Deadlock detected. Transaction was chosen as victim and rolled back.")
        {
        }

        /// <summary>
        /// Creates a new deadlock exception with a message.
        /// </summary>
        /// <param name="message">The error message.</param>
        public DeadlockException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Creates a new deadlock exception with a message and inner exception.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public DeadlockException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Creates a new deadlock exception with victim information.
        /// </summary>
        /// <param name="victimTransactionId">The transaction chosen as victim.</param>
        /// <param name="cycleParticipants">All transactions involved in the deadlock.</param>
        public DeadlockException(long victimTransactionId, IReadOnlyList<long> cycleParticipants)
            : base($"Deadlock detected. Transaction {victimTransactionId} was chosen as victim. " +
                   $"Cycle involved transactions: [{string.Join(" -> ", cycleParticipants)}]")
        {
            VictimTransactionId = victimTransactionId;
            CycleParticipants = cycleParticipants;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the ID of the transaction chosen as the deadlock victim.
        /// </summary>
        public long? VictimTransactionId { get; }

        /// <summary>
        /// Gets the list of transaction IDs involved in the deadlock cycle.
        /// </summary>
        public IReadOnlyList<long>? CycleParticipants { get; }

        /// <summary>
        /// Gets whether the operation should be retried.
        /// Generally, the victim transaction should be retried.
        /// </summary>
        public bool ShouldRetry => true;

        #endregion
    }
}

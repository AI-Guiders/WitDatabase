namespace OutWit.Database.Core.Interfaces
{
    /// <summary>
    /// Provides context and metadata about query execution.
    /// This interface is essential for ADO.NET compatibility.
    /// </summary>
    public interface IQueryContext
    {
        /// <summary>
        /// Gets the number of rows affected by the last INSERT, UPDATE, or DELETE operation.
        /// Returns -1 if not applicable (e.g., for SELECT queries).
        /// </summary>
        long AffectedRows { get; }

        /// <summary>
        /// Gets the last auto-generated ID (e.g., from AUTOINCREMENT columns).
        /// Returns -1 if no ID was generated.
        /// </summary>
        long LastInsertId { get; }

        /// <summary>
        /// Gets the query timeout in milliseconds.
        /// A value of 0 means no timeout.
        /// </summary>
        int TimeoutMilliseconds { get; }

        /// <summary>
        /// Gets the cancellation token associated with this query context.
        /// </summary>
        CancellationToken CancellationToken { get; }

        /// <summary>
        /// Gets whether the query execution was cancelled.
        /// </summary>
        bool IsCancelled { get; }

        /// <summary>
        /// Gets whether the query execution has timed out.
        /// </summary>
        bool IsTimedOut { get; }
    }
}

namespace OutWit.Database.Core.Concurrency;

/// <summary>
/// Lock mode for database access.
/// </summary>
public enum LockMode
{
    /// <summary>
    /// No lock required.
    /// </summary>
    None,
    
    /// <summary>
    /// Shared (read) lock - multiple readers allowed.
    /// </summary>
    Shared,
    
    /// <summary>
    /// Exclusive (write) lock - single writer, no readers.
    /// </summary>
    Exclusive
}
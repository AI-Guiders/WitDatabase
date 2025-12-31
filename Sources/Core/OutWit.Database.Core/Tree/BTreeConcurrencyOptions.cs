namespace OutWit.Database.Core.Tree;

/// <summary>
/// Options for BTree concurrent access wrapper.
/// </summary>
public sealed class BTreeConcurrencyOptions
{
    /// <summary>
    /// Gets or sets whether to track operation statistics.
    /// Enabling adds slight overhead but helps diagnose contention issues.
    /// Default: false
    /// </summary>
    public bool TrackStatistics { get; set; } = false;

    /// <summary>
    /// Creates default options.
    /// </summary>
    public static BTreeConcurrencyOptions Default => new();

    /// <summary>
    /// Creates options for debugging concurrency issues.
    /// </summary>
    public static BTreeConcurrencyOptions Debug => new()
    {
        TrackStatistics = true
    };
}

namespace OutWit.Database.Core.Tree;

/// <summary>
/// Options for BTree concurrent access.
/// </summary>
public sealed class BTreeConcurrencyOptions
{
    /// <summary>
    /// Gets or sets whether concurrent access is enabled.
    /// When disabled, a single global lock is used (default behavior).
    /// Default: false
    /// </summary>
    public bool EnableConcurrentAccess { get; set; } = false;

    /// <summary>
    /// Gets or sets the initial capacity for the latch manager.
    /// Higher values reduce memory allocations but use more memory upfront.
    /// Default: 256
    /// </summary>
    public int LatchManagerCapacity { get; set; } = 256;

    /// <summary>
    /// Gets or sets whether to use optimistic reads.
    /// When enabled, reads don't acquire latches initially.
    /// If the page changes during read, it retries with a latch.
    /// Default: true
    /// </summary>
    public bool UseOptimisticReads { get; set; } = true;

    /// <summary>
    /// Gets or sets the timeout for latch acquisition.
    /// If a latch cannot be acquired within this time, a TimeoutException is thrown.
    /// Default: 30 seconds
    /// </summary>
    public TimeSpan LatchTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets whether to track operation statistics.
    /// Enabling adds slight overhead but helps diagnose contention issues.
    /// Default: false
    /// </summary>
    public bool TrackStatistics { get; set; } = false;

    /// <summary>
    /// Creates default options with concurrent access disabled.
    /// </summary>
    public static BTreeConcurrencyOptions Default => new();

    /// <summary>
    /// Creates options optimized for high concurrency.
    /// </summary>
    public static BTreeConcurrencyOptions HighConcurrency => new()
    {
        EnableConcurrentAccess = true,
        UseOptimisticReads = true,
        LatchManagerCapacity = 1024,
        TrackStatistics = false
    };

    /// <summary>
    /// Creates options for debugging concurrency issues.
    /// </summary>
    public static BTreeConcurrencyOptions Debug => new()
    {
        EnableConcurrentAccess = true,
        UseOptimisticReads = false,
        LatchManagerCapacity = 256,
        TrackStatistics = true,
        LatchTimeout = TimeSpan.FromSeconds(5)
    };
}

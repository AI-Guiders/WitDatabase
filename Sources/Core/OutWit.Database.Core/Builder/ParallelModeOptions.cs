namespace OutWit.Database.Core.Builder;

/// <summary>
/// Specifies the parallel access mode for the database.
/// </summary>
public enum ParallelMode
{
    /// <summary>
    /// No parallelism. Single global lock for all operations.
    /// Most compatible mode, suitable for single-threaded applications.
    /// </summary>
    None,

    /// <summary>
    /// Automatic mode selection based on store type and hardware.
    /// - For LSM-Tree: Uses thread-local buffers with background merge
    /// - For BTree: Uses page-level latching
    /// </summary>
    Auto,

    /// <summary>
    /// Thread-local write buffers with background merge.
    /// Best for high write throughput scenarios.
    /// Primarily for LSM-Tree, but can be used with BTree.
    /// </summary>
    Buffered,

    /// <summary>
    /// Page-level latching for fine-grained concurrency.
    /// Best for mixed read/write workloads.
    /// Primarily for BTree.
    /// </summary>
    Latched,

    /// <summary>
    /// Optimistic concurrency with minimal locking.
    /// Reads proceed without locks, writes use minimal synchronization.
    /// May require retry on conflict.
    /// </summary>
    Optimistic
}

/// <summary>
/// Configuration options for parallel access mode.
/// </summary>
public sealed class ParallelModeOptions
{
    /// <summary>
    /// Gets or sets the parallel access mode.
    /// Default: None (single-threaded behavior)
    /// </summary>
    public ParallelMode Mode { get; set; } = ParallelMode.None;

    /// <summary>
    /// Gets or sets the number of parallel writers (for Buffered mode).
    /// Default: Environment.ProcessorCount
    /// </summary>
    public int MaxWriters { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Gets or sets the buffer size threshold for auto-flush (in bytes).
    /// Default: 64KB
    /// </summary>
    public int BufferSizeThreshold { get; set; } = 64 * 1024;

    /// <summary>
    /// Gets or sets the latch timeout for acquiring page locks.
    /// Default: 30 seconds
    /// </summary>
    public TimeSpan LatchTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets whether to use optimistic reads (no latch for reads).
    /// Default: true
    /// </summary>
    public bool UseOptimisticReads { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to track parallel operation statistics.
    /// Default: false (adds overhead)
    /// </summary>
    public bool TrackStatistics { get; set; } = false;

    /// <summary>
    /// Creates default options (no parallelism).
    /// </summary>
    public static ParallelModeOptions Default => new();

    /// <summary>
    /// Creates options optimized for high write throughput.
    /// </summary>
    public static ParallelModeOptions HighWriteThroughput => new()
    {
        Mode = ParallelMode.Buffered,
        MaxWriters = Environment.ProcessorCount * 2,
        BufferSizeThreshold = 128 * 1024,
        TrackStatistics = false
    };

    /// <summary>
    /// Creates options optimized for mixed read/write workloads.
    /// </summary>
    public static ParallelModeOptions MixedWorkload => new()
    {
        Mode = ParallelMode.Latched,
        UseOptimisticReads = true,
        TrackStatistics = false
    };

    /// <summary>
    /// Creates options for debugging parallel access issues.
    /// </summary>
    public static ParallelModeOptions Debug => new()
    {
        Mode = ParallelMode.Latched,
        UseOptimisticReads = false,
        LatchTimeout = TimeSpan.FromSeconds(5),
        TrackStatistics = true
    };
}

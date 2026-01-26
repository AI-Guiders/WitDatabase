namespace OutWit.Database.Core.Builder;

/// <summary>
/// Specifies the parallel access mode for the database.
/// </summary>
public enum ParallelMode
{
    /// <summary>
    /// No parallelism. Single-threaded access, best performance for single-threaded apps.
    /// Use this mode unless you need concurrent access from multiple threads.
    /// </summary>
    None,

    /// <summary>
    /// Automatic mode - chooses the best strategy at runtime.
    /// - Detects multi-threaded access patterns
    /// - For single-thread: no overhead (equivalent to None)
    /// - For multi-thread: activates thread-safe mode automatically
    /// 
    /// Note: Auto mode provides thread SAFETY, not parallelism speedup.
    /// Embedded databases are I/O bound - parallel writes don't help.
    /// </summary>
    Auto,

    /// <summary>
    /// Thread-local write buffers with background merge.
    /// Provides thread-safe writes with batching for LSM-Tree.
    /// Useful when multiple threads produce write data.
    /// </summary>
    Buffered,

    /// <summary>
    /// Reader-writer lock wrapper for thread-safe access.
    /// Allows multiple concurrent readers, single writer.
    /// Best for mixed read/write workloads with thread safety.
    /// </summary>
    Latched,

    /// <summary>
    /// Alias for Latched mode - provides optimistic reads where possible.
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
    /// Default: None (single-threaded, best performance)
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
    /// Gets or sets the flush interval for buffered writes (in milliseconds).
    /// Default: 10ms
    /// </summary>
    public int FlushIntervalMs { get; set; } = 10;

    /// <summary>
    /// Gets or sets the latch timeout for acquiring locks.
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
    /// Creates default options (no parallelism, best single-thread performance).
    /// </summary>
    public static ParallelModeOptions Default => new();

    /// <summary>
    /// Creates options for thread-safe multi-threaded access.
    /// Does not guarantee performance improvement over single-threaded.
    /// </summary>
    public static ParallelModeOptions ThreadSafe => new()
    {
        Mode = ParallelMode.Auto,
        TrackStatistics = false
    };

    /// <summary>
    /// Creates options for high write throughput with buffering.
    /// Best when multiple threads produce write data that can be batched.
    /// </summary>
    public static ParallelModeOptions HighWriteThroughput => new()
    {
        Mode = ParallelMode.Buffered,
        MaxWriters = Environment.ProcessorCount,
        BufferSizeThreshold = 128 * 1024,
        FlushIntervalMs = 5,
        TrackStatistics = false
    };

    /// <summary>
    /// Creates options for debugging parallel access issues.
    /// </summary>
    public static ParallelModeOptions Debug => new()
    {
        Mode = ParallelMode.Latched,
        TrackStatistics = true,
        LatchTimeout = TimeSpan.FromSeconds(5)
    };
}

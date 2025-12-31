using System.Runtime.CompilerServices;
using OutWit.Database.Core.Interfaces;
using OutWit.Database.Core.Stores;

namespace OutWit.Database.Core.Builder;

/// <summary>
/// Options for LSM parallel store.
/// </summary>
public sealed class LsmParallelStoreOptions
{
    /// <summary>
    /// Gets or sets the maximum number of parallel writers.
    /// Default: Environment.ProcessorCount
    /// </summary>
    public int MaxWriters { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Gets or sets the buffer size threshold for auto-flush (in bytes).
    /// Default: 64KB
    /// </summary>
    public int BufferSizeThreshold { get; set; } = 64 * 1024;

    /// <summary>
    /// Gets or sets whether to track statistics.
    /// Default: false
    /// </summary>
    public bool TrackStatistics { get; set; } = false;

    /// <summary>
    /// Gets or sets the flush interval in milliseconds.
    /// Default: 10ms
    /// </summary>
    public int FlushIntervalMs { get; set; } = 10;

    /// <summary>
    /// Creates default options.
    /// </summary>
    public static LsmParallelStoreOptions Default => new();

    /// <summary>
    /// Creates options optimized for high throughput.
    /// </summary>
    public static LsmParallelStoreOptions HighThroughput => new()
    {
        MaxWriters = Environment.ProcessorCount * 2,
        BufferSizeThreshold = 128 * 1024,
        FlushIntervalMs = 5
    };
}

using OutWit.Database.Core.Interfaces;
using OutWit.Database.Core.Stores;
using OutWit.Database.Core.Tree;

namespace OutWit.Database.Core.Builder;

/// <summary>
/// Factory for creating key-value stores with optional thread-safe wrappers.
/// This is the main integration point for parallel storage mode.
/// </summary>
/// <remarks>
/// Important: Parallel mode provides thread SAFETY, not performance improvement.
/// Embedded databases are I/O bound - parallel writes to the same store don't improve throughput.
/// Use parallel mode only when you need concurrent access from multiple threads.
/// </remarks>
public static class KeyValueStoreFactory
{
    #region BTree Store Creation

    /// <summary>
    /// Creates a BTree store with the specified parallel mode options.
    /// </summary>
    /// <param name="filePath">Path to the database file.</param>
    /// <param name="options">Parallel mode options.</param>
    /// <param name="pageSize">Page size in bytes.</param>
    /// <param name="cacheSize">Number of pages to cache.</param>
    /// <returns>A key-value store configured for the specified parallel mode.</returns>
    public static IKeyValueStore CreateBTreeStore(
        string filePath,
        ParallelModeOptions? options = null,
        int pageSize = 4096,
        int cacheSize = 1000)
    {
        options ??= ParallelModeOptions.Default;

        var effectiveMode = ResolveParallelMode(options.Mode, StoreType.BTree);

        // For None mode, return raw store (best performance)
        if (effectiveMode == ParallelMode.None)
        {
            return new StoreBTree(filePath, pageSize, cacheSize);
        }

        // For any parallel mode, wrap with thread-safe wrapper
        return CreateConcurrentBTreeStore(filePath, options, pageSize, cacheSize);
    }

    /// <summary>
    /// Creates a concurrent BTree store with the specified options.
    /// </summary>
    private static BTreeConcurrentStore CreateConcurrentBTreeStore(
        string filePath,
        ParallelModeOptions options,
        int pageSize,
        int cacheSize)
    {
        var concurrencyOptions = new BTreeConcurrencyOptions
        {
            TrackStatistics = options.TrackStatistics
        };

        return new BTreeConcurrentStore(filePath, concurrencyOptions, pageSize, cacheSize);
    }

    #endregion

    #region LSM Store Creation

    /// <summary>
    /// Creates an LSM store with the specified parallel mode options.
    /// </summary>
    /// <param name="directory">Directory for LSM files.</param>
    /// <param name="options">Parallel mode options.</param>
    /// <returns>A key-value store configured for the specified parallel mode.</returns>
    public static IKeyValueStore CreateLsmStore(
        string directory,
        ParallelModeOptions? options = null)
    {
        options ??= ParallelModeOptions.Default;

        var effectiveMode = ResolveParallelMode(options.Mode, StoreType.Lsm);

        // For None mode, return raw store (best performance)
        if (effectiveMode == ParallelMode.None)
        {
            return new StoreLsm(directory);
        }

        // For Buffered mode, use LsmParallelStore with write buffering
        if (effectiveMode == ParallelMode.Buffered)
        {
            return CreateParallelLsmStore(directory, options);
        }

        // For other modes (Latched, Auto), also use LsmParallelStore
        // LSM already has internal locking, buffered mode is most beneficial
        return CreateParallelLsmStore(directory, options);
    }

    /// <summary>
    /// Creates a parallel LSM store with the specified options.
    /// </summary>
    private static LsmParallelStore CreateParallelLsmStore(
        string directory,
        ParallelModeOptions options)
    {
        var lsmOptions = new LsmParallelStoreOptions
        {
            MaxWriters = options.MaxWriters,
            BufferSizeThreshold = options.BufferSizeThreshold,
            FlushIntervalMs = options.FlushIntervalMs,
            TrackStatistics = options.TrackStatistics
        };

        return new LsmParallelStore(directory, lsmOptions);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Resolves the effective parallel mode based on store type.
    /// </summary>
    private static ParallelMode ResolveParallelMode(ParallelMode mode, StoreType storeType)
    {
        if (mode == ParallelMode.None)
            return ParallelMode.None;

        if (mode == ParallelMode.Auto)
        {
            // Auto mode: choose appropriate strategy for store type
            return storeType switch
            {
                StoreType.BTree => ParallelMode.Latched,    // RW lock wrapper
                StoreType.Lsm => ParallelMode.Buffered,     // Buffered writes
                _ => ParallelMode.None
            };
        }

        // Optimistic is alias for Latched
        if (mode == ParallelMode.Optimistic)
            return ParallelMode.Latched;

        return mode;
    }

    #endregion

    #region Nested Types

    private enum StoreType
    {
        BTree,
        Lsm
    }

    #endregion
}

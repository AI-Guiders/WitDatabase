using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Reports;
using OutWit.Database.Core.Builder;
using OutWit.Database.Core.Interfaces;
using OutWit.Database.Core.Stores;
using OutWit.Database.Core.Tree;
using System.Text;

namespace OutWit.Database.Comparison.Benchmarks;

/// <summary>
/// Benchmarks comparing single-threaded vs parallel mode for concurrent workloads.
/// This demonstrates the real benefit of parallel mode - handling multiple concurrent writers.
/// </summary>
public class ConcurrentAccessBenchmarkConfig : ManualConfig
{
    public ConcurrentAccessBenchmarkConfig()
    {
        SummaryStyle = SummaryStyle.Default
            .WithRatioStyle(RatioStyle.Trend)
            .WithTimeUnit(Perfolizer.Horology.TimeUnit.Millisecond);
        HideColumns(Column.Error, Column.StdDev, Column.RatioSD);
    }
}

public enum StoreMode
{
    BTreeSingleThreaded,   // Regular BTree (global lock)
    BTreeParallel,         // BTree with page-level latching
    LsmSingleThreaded,     // Regular LSM
    LsmParallel            // LSM with parallel writer
}

/// <summary>
/// Concurrent write benchmarks - multiple threads writing to the same store.
/// This is where parallel mode shines!
/// </summary>
[Config(typeof(ConcurrentAccessBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class ConcurrentWriteBenchmarks : IDisposable
{
    private IKeyValueStore? _store;
    private string _path = null!;

    [Params(10000, 50000)]
    public int TotalEntries { get; set; }

    [Params(1, 2, 4, 8)]
    public int ThreadCount { get; set; }

    [Params(StoreMode.BTreeSingleThreaded, StoreMode.BTreeParallel)]
    public StoreMode Mode { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _path = Path.Combine(Path.GetTempPath(), $"concurrent_bench_{Guid.NewGuid():N}.witdb");
    }

    [IterationSetup]
    public void IterationSetup()
    {
        // Clean up any previous file
        try { if (File.Exists(_path)) File.Delete(_path); } catch { }

        // Create store based on mode
        var builder = new WitDatabaseBuilder()
            .WithFilePath(_path)
            .WithBTree()
            .WithoutTransactions();

        if (Mode == StoreMode.BTreeParallel)
        {
            builder.WithParallelWrites(ParallelMode.Latched);
        }

        _store = builder.BuildStore();
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _store?.Dispose();
        _store = null;
        try { if (File.Exists(_path)) File.Delete(_path); } catch { }
    }

    [GlobalCleanup]
    public void GlobalCleanup() => IterationCleanup();

    [Benchmark(Description = "Concurrent PUT")]
    public int ConcurrentPut()
    {
        int entriesPerThread = TotalEntries / ThreadCount;
        int totalWritten = 0;

        if (ThreadCount == 1)
        {
            // Single-threaded - direct writes
            for (int i = 0; i < TotalEntries; i++)
            {
                var key = Encoding.UTF8.GetBytes($"key_{i:D8}");
                var value = Encoding.UTF8.GetBytes($"value_{i}_data_payload_for_testing");
                _store!.Put(key, value);
                totalWritten++;
            }
        }
        else
        {
            // Multi-threaded - concurrent writes
            var tasks = new Task<int>[ThreadCount];
            
            for (int t = 0; t < ThreadCount; t++)
            {
                int threadId = t;
                int startIdx = t * entriesPerThread;
                
                tasks[t] = Task.Run(() =>
                {
                    int written = 0;
                    for (int i = startIdx; i < startIdx + entriesPerThread; i++)
                    {
                        var key = Encoding.UTF8.GetBytes($"key_{i:D8}");
                        var value = Encoding.UTF8.GetBytes($"value_{i}_data_payload_for_testing_thread_{threadId}");
                        _store!.Put(key, value);
                        written++;
                    }
                    return written;
                });
            }
            
            Task.WaitAll(tasks);
            totalWritten = tasks.Sum(t => t.Result);
        }

        return totalWritten;
    }

    public void Dispose() => GlobalCleanup();
}

/// <summary>
/// Mixed read/write concurrent benchmarks.
/// </summary>
[Config(typeof(ConcurrentAccessBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class MixedConcurrentBenchmarks : IDisposable
{
    private IKeyValueStore? _store;
    private string _path = null!;
    private byte[][] _keys = null!;

    [Params(10000)]
    public int InitialEntries { get; set; }

    [Params(4, 8)]
    public int ThreadCount { get; set; }

    [Params(StoreMode.BTreeSingleThreaded, StoreMode.BTreeParallel)]
    public StoreMode Mode { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _path = Path.Combine(Path.GetTempPath(), $"mixed_bench_{Guid.NewGuid():N}.witdb");
        
        // Pre-generate keys
        _keys = new byte[InitialEntries][];
        for (int i = 0; i < InitialEntries; i++)
        {
            _keys[i] = Encoding.UTF8.GetBytes($"key_{i:D8}");
        }
    }

    [IterationSetup]
    public void IterationSetup()
    {
        try { if (File.Exists(_path)) File.Delete(_path); } catch { }

        var builder = new WitDatabaseBuilder()
            .WithFilePath(_path)
            .WithBTree()
            .WithoutTransactions();

        if (Mode == StoreMode.BTreeParallel)
        {
            builder.WithParallelWrites(ParallelMode.Latched);
        }

        _store = builder.BuildStore();

        // Pre-populate with data
        for (int i = 0; i < InitialEntries; i++)
        {
            var value = Encoding.UTF8.GetBytes($"initial_value_{i}");
            _store.Put(_keys[i], value);
        }
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _store?.Dispose();
        _store = null;
        try { if (File.Exists(_path)) File.Delete(_path); } catch { }
    }

    [GlobalCleanup]
    public void GlobalCleanup() => IterationCleanup();

    [Benchmark(Description = "Mixed Read/Write (50/50)")]
    public int MixedReadWrite()
    {
        int opsPerThread = InitialEntries / ThreadCount;
        int totalOps = 0;
        var rnd = new Random(42);

        var tasks = new Task<int>[ThreadCount];
        
        for (int t = 0; t < ThreadCount; t++)
        {
            int threadId = t;
            int startIdx = t * opsPerThread;
            
            tasks[t] = Task.Run(() =>
            {
                var localRnd = new Random(42 + threadId);
                int ops = 0;
                
                for (int i = 0; i < opsPerThread; i++)
                {
                    int keyIdx = localRnd.Next(InitialEntries);
                    
                    if (i % 2 == 0)
                    {
                        // Read operation
                        var value = _store!.Get(_keys[keyIdx]);
                        if (value != null) ops++;
                    }
                    else
                    {
                        // Write operation
                        var newValue = Encoding.UTF8.GetBytes($"updated_{keyIdx}_by_thread_{threadId}");
                        _store!.Put(_keys[keyIdx], newValue);
                        ops++;
                    }
                }
                return ops;
            });
        }
        
        Task.WaitAll(tasks);
        return tasks.Sum(t => t.Result);
    }

    [Benchmark(Description = "Heavy Write (90% write, 10% read)")]
    public int HeavyWrite()
    {
        int opsPerThread = InitialEntries / ThreadCount;
        int totalOps = 0;

        var tasks = new Task<int>[ThreadCount];
        
        for (int t = 0; t < ThreadCount; t++)
        {
            int threadId = t;
            int startIdx = t * opsPerThread;
            
            tasks[t] = Task.Run(() =>
            {
                var localRnd = new Random(42 + threadId);
                int ops = 0;
                
                for (int i = 0; i < opsPerThread; i++)
                {
                    int keyIdx = localRnd.Next(InitialEntries);
                    
                    if (i % 10 == 0)
                    {
                        // Read operation (10%)
                        var value = _store!.Get(_keys[keyIdx]);
                        if (value != null) ops++;
                    }
                    else
                    {
                        // Write operation (90%)
                        var newValue = Encoding.UTF8.GetBytes($"heavy_write_{keyIdx}_thread_{threadId}_iter_{i}");
                        _store!.Put(_keys[keyIdx], newValue);
                        ops++;
                    }
                }
                return ops;
            });
        }
        
        Task.WaitAll(tasks);
        return tasks.Sum(t => t.Result);
    }

    public void Dispose() => GlobalCleanup();
}

/// <summary>
/// LSM-specific concurrent benchmarks.
/// </summary>
[Config(typeof(ConcurrentAccessBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class LsmConcurrentWriteBenchmarks : IDisposable
{
    private IKeyValueStore? _store;
    private string _path = null!;

    [Params(10000, 50000)]
    public int TotalEntries { get; set; }

    [Params(1, 2, 4, 8)]
    public int ThreadCount { get; set; }

    [Params(StoreMode.LsmSingleThreaded, StoreMode.LsmParallel)]
    public StoreMode Mode { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _path = Path.Combine(Path.GetTempPath(), $"lsm_concurrent_{Guid.NewGuid():N}");
    }

    [IterationSetup]
    public void IterationSetup()
    {
        try { if (Directory.Exists(_path)) Directory.Delete(_path, true); } catch { }

        var builder = new WitDatabaseBuilder()
            .WithLsmTree(_path)
            .WithoutTransactions();

        if (Mode == StoreMode.LsmParallel)
        {
            builder.WithParallelWrites(ParallelMode.Buffered);
        }

        _store = builder.BuildStore();
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _store?.Dispose();
        _store = null;
        try { if (Directory.Exists(_path)) Directory.Delete(_path, true); } catch { }
    }

    [GlobalCleanup]
    public void GlobalCleanup() => IterationCleanup();

    [Benchmark(Description = "LSM Concurrent PUT")]
    public int LsmConcurrentPut()
    {
        int entriesPerThread = TotalEntries / ThreadCount;
        int totalWritten = 0;

        if (ThreadCount == 1)
        {
            for (int i = 0; i < TotalEntries; i++)
            {
                var key = Encoding.UTF8.GetBytes($"key_{i:D8}");
                var value = Encoding.UTF8.GetBytes($"value_{i}_data_payload");
                _store!.Put(key, value);
                totalWritten++;
            }
        }
        else
        {
            var tasks = new Task<int>[ThreadCount];
            
            for (int t = 0; t < ThreadCount; t++)
            {
                int threadId = t;
                int startIdx = t * entriesPerThread;
                
                tasks[t] = Task.Run(() =>
                {
                    int written = 0;
                    for (int i = startIdx; i < startIdx + entriesPerThread; i++)
                    {
                        var key = Encoding.UTF8.GetBytes($"key_{i:D8}");
                        var value = Encoding.UTF8.GetBytes($"value_{i}_thread_{threadId}");
                        _store!.Put(key, value);
                        written++;
                    }
                    return written;
                });
            }
            
            Task.WaitAll(tasks);
            totalWritten = tasks.Sum(t => t.Result);
        }

        // Flush to ensure all writes are persisted
        _store!.Flush();
        return totalWritten;
    }

    public void Dispose() => GlobalCleanup();
}

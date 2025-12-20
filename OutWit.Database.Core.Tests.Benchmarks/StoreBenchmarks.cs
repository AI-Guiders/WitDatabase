using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Reports;
using OutWit.Database.Core.Interfaces;
using OutWit.Database.Core.LSM;
using OutWit.Database.Core.Storage;
using OutWit.Database.Core.Stores;

namespace OutWit.Database.Core.Tests.Benchmarks;

#region Store Type Enum

public enum StoreType
{
    BTree,
    LSM
}

#endregion

#region LSM Tree vs BTree Insert Benchmarks

/// <summary>
/// Benchmarks comparing BTree vs LSM-Tree insert performance.
/// </summary>
[Config(typeof(CleanBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class StoreInsertBenchmarks : IDisposable
{
    private IKeyValueStore m_store = null!;
    private IStorage? m_btreeStorage;
    private byte[][] m_keys = null!;
    private byte[][] m_values = null!;
    private string? m_lsmDir;
    
    [Params(1000, 10000, 50000)]
    public int Count { get; set; }
    
    [Params(StoreType.BTree, StoreType.LSM)]
    public StoreType Store { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        var random = new Random(42);
        m_keys = new byte[Count][];
        m_values = new byte[Count][];
        
        for (int i = 0; i < Count; i++)
        {
            m_keys[i] = BitConverter.GetBytes(i);
            m_values[i] = new byte[100];
            random.NextBytes(m_values[i]);
        }
    }

    [IterationSetup]
    public void IterationSetup()
    {
        if (Store == StoreType.BTree)
        {
            m_btreeStorage = new StorageMemory(4096, Count / 3 + 1000);
            m_store = new StoreBTree(m_btreeStorage, ownsStorage: false);
        }
        else
        {
            m_lsmDir = Path.Combine(Path.GetTempPath(), $"lsm_bench_{Guid.NewGuid():N}");
            Directory.CreateDirectory(m_lsmDir);
            var options = new LsmOptions
            {
                EnableWal = false, // Disable WAL for fair comparison
                EnableBlockCache = true,
                BlockCacheSizeBytes = 10 * 1024 * 1024,
                MemTableSizeLimit = 4 * 1024 * 1024,
                Level0CompactionTrigger = 100, // Disable auto-compaction during benchmark
                BackgroundCompaction = false
            };
            m_store = new StoreLsm(m_lsmDir, options);
        }
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        m_store?.Dispose();
        m_btreeStorage?.Dispose();
        
        if (m_lsmDir != null && Directory.Exists(m_lsmDir))
        {
            try { Directory.Delete(m_lsmDir, recursive: true); } catch { }
            m_lsmDir = null;
        }
    }

    [Benchmark(Description = "Sequential Insert")]
    [BenchmarkCategory("Insert")]
    public void SequentialInsert()
    {
        for (int i = 0; i < Count; i++)
        {
            m_store.Put(m_keys[i], m_values[i]);
        }
    }

    public void Dispose()
    {
        IterationCleanup();
    }
}

#endregion

#region LSM Tree vs BTree Read Benchmarks

/// <summary>
/// Benchmarks comparing BTree vs LSM-Tree read performance.
/// </summary>
[Config(typeof(CleanBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class StoreReadBenchmarks : IDisposable
{
    private IKeyValueStore m_store = null!;
    private IStorage? m_btreeStorage;
    private byte[][] m_keys = null!;
    private int[] m_randomOrder = null!;
    private string? m_lsmDir;
    
    [Params(10000, 50000)]
    public int TreeSize { get; set; }
    
    [Params(StoreType.BTree, StoreType.LSM)]
    public StoreType Store { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        var random = new Random(42);
        
        if (Store == StoreType.BTree)
        {
            m_btreeStorage = new StorageMemory(4096, TreeSize / 3 + 1000);
            m_store = new StoreBTree(m_btreeStorage, ownsStorage: false);
        }
        else
        {
            m_lsmDir = Path.Combine(Path.GetTempPath(), $"lsm_bench_{Guid.NewGuid():N}");
            Directory.CreateDirectory(m_lsmDir);
            var options = new LsmOptions
            {
                EnableWal = false,
                EnableBlockCache = true,
                BlockCacheSizeBytes = 50 * 1024 * 1024, // Large cache for read benchmark
                MemTableSizeLimit = 16 * 1024 * 1024,
                Level0CompactionTrigger = 100,
                BackgroundCompaction = false
            };
            m_store = new StoreLsm(m_lsmDir, options);
        }
        
        m_keys = new byte[TreeSize][];
        
        // Insert data
        for (int i = 0; i < TreeSize; i++)
        {
            m_keys[i] = BitConverter.GetBytes(i);
            m_store.Put(m_keys[i], m_keys[i]);
        }
        m_store.Flush();
        
        // Random read order
        m_randomOrder = Enumerable.Range(0, TreeSize)
            .OrderBy(_ => random.Next())
            .Take(1000)
            .ToArray();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        m_store?.Dispose();
        m_btreeStorage?.Dispose();
        
        if (m_lsmDir != null && Directory.Exists(m_lsmDir))
        {
            try { Directory.Delete(m_lsmDir, recursive: true); } catch { }
        }
    }

    [Benchmark(Description = "Random Read (1000 ops)")]
    public int RandomRead()
    {
        int found = 0;
        for (int i = 0; i < 1000; i++)
        {
            if (m_store.Get(m_keys[m_randomOrder[i]]) != null) found++;
        }
        return found;
    }

    [Benchmark(Description = "Sequential Read (1000 ops)")]
    public int SequentialRead()
    {
        int found = 0;
        for (int i = 0; i < 1000; i++)
        {
            if (m_store.Get(m_keys[i]) != null) found++;
        }
        return found;
    }

    public void Dispose()
    {
        GlobalCleanup();
    }
}

#endregion

#region LSM Tree vs BTree Mixed Workload Benchmarks

/// <summary>
/// Realistic mixed workload benchmarks comparing BTree vs LSM-Tree.
/// </summary>
[Config(typeof(CleanBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class StoreMixedWorkloadBenchmarks : IDisposable
{
    private IKeyValueStore m_store = null!;
    private IStorage? m_btreeStorage;
    private (int Op, byte[] Key, byte[] Value)[] m_operations = null!;
    private string? m_lsmDir;
    
    [Params(10000)]
    public int OperationCount { get; set; }
    
    [Params(StoreType.BTree, StoreType.LSM)]
    public StoreType Store { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        var random = new Random(42);
        m_operations = new (int, byte[], byte[])[OperationCount];
        
        // 50% read, 35% write, 15% delete
        for (int i = 0; i < OperationCount; i++)
        {
            int op = random.Next(100);
            int keyInt = random.Next(5000);
            byte[] key = BitConverter.GetBytes(keyInt);
            byte[] value = new byte[100];
            random.NextBytes(value);
            
            int opType = op switch
            {
                < 50 => 0,  // Get
                < 85 => 1,  // Put
                _ => 2      // Delete
            };
            
            m_operations[i] = (opType, key, value);
        }
    }

    [IterationSetup]
    public void IterationSetup()
    {
        if (Store == StoreType.BTree)
        {
            m_btreeStorage = new StorageMemory(4096, 5000);
            m_store = new StoreBTree(m_btreeStorage, ownsStorage: false);
        }
        else
        {
            m_lsmDir = Path.Combine(Path.GetTempPath(), $"lsm_bench_{Guid.NewGuid():N}");
            Directory.CreateDirectory(m_lsmDir);
            var options = new LsmOptions
            {
                EnableWal = false,
                EnableBlockCache = true,
                BlockCacheSizeBytes = 10 * 1024 * 1024,
                MemTableSizeLimit = 4 * 1024 * 1024,
                Level0CompactionTrigger = 100,
                BackgroundCompaction = false
            };
            m_store = new StoreLsm(m_lsmDir, options);
        }
        
        // Pre-populate with 2500 entries
        for (int i = 0; i < 2500; i++)
        {
            m_store.Put(BitConverter.GetBytes(i), BitConverter.GetBytes(i));
        }
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        m_store?.Dispose();
        m_btreeStorage?.Dispose();
        
        if (m_lsmDir != null && Directory.Exists(m_lsmDir))
        {
            try { Directory.Delete(m_lsmDir, recursive: true); } catch { }
            m_lsmDir = null;
        }
    }

    [Benchmark(Description = "Mixed (50% read, 35% write, 15% delete)")]
    public int MixedOperations()
    {
        int result = 0;
        
        for (int i = 0; i < OperationCount; i++)
        {
            var (op, key, value) = m_operations[i];
            
            switch (op)
            {
                case 0:
                    if (m_store.Get(key) != null) result++;
                    break;
                case 1:
                    m_store.Put(key, value);
                    result++;
                    break;
                case 2:
                    m_store.Delete(key);
                    result++;
                    break;
            }
        }
        
        return result;
    }

    public void Dispose()
    {
        IterationCleanup();
    }
}

#endregion

#region LSM Tree vs BTree Scan Benchmarks

/// <summary>
/// Benchmarks comparing BTree vs LSM-Tree scan performance.
/// </summary>
[Config(typeof(CleanBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class StoreScanBenchmarks : IDisposable
{
    private IKeyValueStore m_store = null!;
    private IStorage? m_btreeStorage;
    private string? m_lsmDir;
    
    [Params(10000, 50000)]
    public int TreeSize { get; set; }
    
    [Params(StoreType.BTree, StoreType.LSM)]
    public StoreType Store { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        if (Store == StoreType.BTree)
        {
            m_btreeStorage = new StorageMemory(4096, TreeSize / 3 + 1000);
            m_store = new StoreBTree(m_btreeStorage, ownsStorage: false);
        }
        else
        {
            m_lsmDir = Path.Combine(Path.GetTempPath(), $"lsm_bench_{Guid.NewGuid():N}");
            Directory.CreateDirectory(m_lsmDir);
            var options = new LsmOptions
            {
                EnableWal = false,
                EnableBlockCache = true,
                BlockCacheSizeBytes = 50 * 1024 * 1024,
                MemTableSizeLimit = 16 * 1024 * 1024,
                Level0CompactionTrigger = 100,
                BackgroundCompaction = false
            };
            m_store = new StoreLsm(m_lsmDir, options);
        }
        
        // Insert data
        for (int i = 0; i < TreeSize; i++)
        {
            m_store.Put(BitConverter.GetBytes(i), BitConverter.GetBytes(i));
        }
        m_store.Flush();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        m_store?.Dispose();
        m_btreeStorage?.Dispose();
        
        if (m_lsmDir != null && Directory.Exists(m_lsmDir))
        {
            try { Directory.Delete(m_lsmDir, recursive: true); } catch { }
        }
    }

    [Benchmark(Description = "Scan Range (1000 entries)")]
    public int ScanRange()
    {
        int start = TreeSize / 4;
        int count = 0;
        foreach (var _ in m_store.Scan(BitConverter.GetBytes(start), BitConverter.GetBytes(start + 1000)))
        {
            count++;
        }
        return count;
    }

    [Benchmark(Description = "Scan All")]
    public int ScanAll()
    {
        int count = 0;
        foreach (var _ in m_store.Scan(null, null))
        {
            count++;
        }
        return count;
    }

    public void Dispose()
    {
        GlobalCleanup();
    }
}

#endregion

#region Write-Heavy Workload Benchmarks

/// <summary>
/// Write-heavy workload benchmarks (typical LSM-Tree advantage scenario).
/// </summary>
[Config(typeof(CleanBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class WriteHeavyBenchmarks : IDisposable
{
    private IKeyValueStore m_store = null!;
    private IStorage? m_btreeStorage;
    private byte[][] m_keys = null!;
    private byte[][] m_values = null!;
    private string? m_lsmDir;
    
    [Params(50000)]
    public int Count { get; set; }
    
    [Params(StoreType.BTree, StoreType.LSM)]
    public StoreType Store { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        var random = new Random(42);
        m_keys = new byte[Count][];
        m_values = new byte[Count][];
        
        // Random keys to cause more tree rebalancing in BTree
        for (int i = 0; i < Count; i++)
        {
            m_keys[i] = new byte[8];
            random.NextBytes(m_keys[i]);
            m_values[i] = new byte[200]; // Larger values
            random.NextBytes(m_values[i]);
        }
    }

    [IterationSetup]
    public void IterationSetup()
    {
        if (Store == StoreType.BTree)
        {
            m_btreeStorage = new StorageMemory(4096, Count + 5000);
            m_store = new StoreBTree(m_btreeStorage, ownsStorage: false);
        }
        else
        {
            m_lsmDir = Path.Combine(Path.GetTempPath(), $"lsm_bench_{Guid.NewGuid():N}");
            Directory.CreateDirectory(m_lsmDir);
            var options = new LsmOptions
            {
                EnableWal = false,
                EnableBlockCache = false, // Disable cache for write benchmark
                MemTableSizeLimit = 8 * 1024 * 1024,
                Level0CompactionTrigger = 100,
                BackgroundCompaction = false
            };
            m_store = new StoreLsm(m_lsmDir, options);
        }
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        m_store?.Dispose();
        m_btreeStorage?.Dispose();
        
        if (m_lsmDir != null && Directory.Exists(m_lsmDir))
        {
            try { Directory.Delete(m_lsmDir, recursive: true); } catch { }
            m_lsmDir = null;
        }
    }

    [Benchmark(Description = "Random Key Insert")]
    public void RandomKeyInsert()
    {
        for (int i = 0; i < Count; i++)
        {
            m_store.Put(m_keys[i], m_values[i]);
        }
    }

    [Benchmark(Description = "Insert + Update Same Keys")]
    public void InsertAndUpdate()
    {
        // Insert
        for (int i = 0; i < Count / 2; i++)
        {
            m_store.Put(m_keys[i], m_values[i]);
        }
        
        // Update same keys
        for (int i = 0; i < Count / 2; i++)
        {
            m_store.Put(m_keys[i], m_values[Count / 2 + i]);
        }
    }

    public void Dispose()
    {
        IterationCleanup();
    }
}

#endregion

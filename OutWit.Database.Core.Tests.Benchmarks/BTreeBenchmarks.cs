using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Reports;
using OutWit.Database.Core.Encryption;
using OutWit.Database.Core.Interfaces;
using OutWit.Database.Core.Managers;
using OutWit.Database.Core.Providers;
using OutWit.Database.Core.Storage;
using OutWit.Database.Core.Stores;
using OutWit.Database.Core.Tree;
using System.Security.Cryptography;

namespace OutWit.Database.Core.Tests.Benchmarks;

#region Custom Config

/// <summary>
/// Clean benchmark configuration showing only essential metrics.
/// </summary>
public class CleanBenchmarkConfig : ManualConfig
{
    public CleanBenchmarkConfig()
    {
        SummaryStyle = SummaryStyle.Default
            .WithRatioStyle(RatioStyle.Trend)
            .WithTimeUnit(Perfolizer.Horology.TimeUnit.Microsecond);
        
        // Hide unnecessary columns
        HideColumns(Column.Job, Column.Error, Column.StdDev, Column.RatioSD);
    }
}

#endregion

#region Storage Type Enum

public enum StorageType
{
    Memory,
    File
}

#endregion

#region BTree Insert Benchmarks

/// <summary>
/// Benchmarks for BTree Insert operations with different storage types.
/// </summary>
[Config(typeof(CleanBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class BTreeInsertBenchmarks
{
    private IStorage m_storage = null!;
    private PageManager m_pageManager = null!;
    private BTree m_tree = null!;
    private byte[][] m_keys = null!;
    private byte[][] m_values = null!;
    private string? m_tempFile;
    
    [Params(1000, 10000)]
    public int Count { get; set; }
    
    [Params(StorageType.Memory, StorageType.File)]
    public StorageType Storage { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        var random = new Random(42);
        m_keys = new byte[Count][];
        m_values = new byte[Count][];
        
        for (int i = 0; i < Count; i++)
        {
            m_keys[i] = new byte[16];
            m_values[i] = new byte[64];
            BitConverter.TryWriteBytes(m_keys[i], i);
            random.NextBytes(m_keys[i].AsSpan(4));
            random.NextBytes(m_values[i]);
        }
    }

    [IterationSetup]
    public void IterationSetup()
    {
        if (Storage == StorageType.File)
        {
            m_tempFile = Path.GetTempFileName();
            m_storage = new FileStorage(m_tempFile, 4096);
        }
        else
        {
            m_storage = new MemoryStorage(4096, Count / 5 + 1000);
        }
        
        m_pageManager = new PageManager(m_storage);
        m_tree = new BTree(m_pageManager);
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        m_tree?.Dispose();
        m_pageManager?.Dispose();
        m_storage?.Dispose();
        
        if (m_tempFile != null && File.Exists(m_tempFile))
        {
            try { File.Delete(m_tempFile); } catch { }
            m_tempFile = null;
        }
    }

    [Benchmark(Description = "Insert")]
    [BenchmarkCategory("Insert")]
    public void SequentialInsert()
    {
        for (int i = 0; i < Count; i++)
        {
            m_tree.Insert(m_keys[i], m_values[i]);
        }
    }
}

#endregion

#region BTree Search Benchmarks

/// <summary>
/// Benchmarks for BTree Search operations.
/// </summary>
[Config(typeof(CleanBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class BTreeSearchBenchmarks
{
    private IStorage m_storage = null!;
    private PageManager m_pageManager = null!;
    private BTree m_tree = null!;
    private byte[][] m_keys = null!;
    private int[] m_searchOrder = null!;
    private string? m_tempFile;
    
    [Params(10000, 50000)]
    public int TreeSize { get; set; }
    
    [Params(StorageType.Memory, StorageType.File)]
    public StorageType Storage { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        if (Storage == StorageType.File)
        {
            m_tempFile = Path.GetTempFileName();
            m_storage = new FileStorage(m_tempFile, 4096);
        }
        else
        {
            m_storage = new MemoryStorage(4096, TreeSize / 5 + 1000);
        }
        
        m_pageManager = new PageManager(m_storage);
        m_tree = new BTree(m_pageManager);
        
        var random = new Random(42);
        m_keys = new byte[TreeSize][];
        
        for (int i = 0; i < TreeSize; i++)
        {
            m_keys[i] = BitConverter.GetBytes(i);
            m_tree.Insert(m_keys[i], m_keys[i]);
        }
        
        m_searchOrder = Enumerable.Range(0, TreeSize)
            .OrderBy(_ => random.Next())
            .Take(1000)
            .ToArray();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        m_tree?.Dispose();
        m_pageManager?.Dispose();
        m_storage?.Dispose();
        
        if (m_tempFile != null && File.Exists(m_tempFile))
        {
            try { File.Delete(m_tempFile); } catch { }
        }
    }

    [Benchmark(Description = "Random Search (1000 ops)")]
    public int RandomSearch()
    {
        int found = 0;
        for (int i = 0; i < 1000; i++)
        {
            if (m_tree.Search(m_keys[m_searchOrder[i]]) != null) found++;
        }
        return found;
    }

    [Benchmark(Description = "ContainsKey (1000 ops)")]
    public int ContainsKey()
    {
        int found = 0;
        for (int i = 0; i < 1000; i++)
        {
            if (m_tree.ContainsKey(m_keys[m_searchOrder[i]])) found++;
        }
        return found;
    }
}

#endregion

#region BTreeStore Benchmarks

/// <summary>
/// Benchmarks for BTreeStore (high-level API) with different storage types.
/// </summary>
[Config(typeof(CleanBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class BTreeStoreBenchmarks
{
    private IStorage m_storage = null!;
    private BTreeStore m_store = null!;
    private byte[][] m_keys = null!;
    private byte[][] m_values = null!;
    private string? m_tempFile;
    
    [Params(1000, 10000)]
    public int Count { get; set; }
    
    [Params(StorageType.Memory, StorageType.File)]
    public StorageType Storage { get; set; }

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
        if (Storage == StorageType.File)
        {
            m_tempFile = Path.GetTempFileName();
            m_storage = new FileStorage(m_tempFile, 4096);
        }
        else
        {
            m_storage = new MemoryStorage(4096, Count / 3 + 1000);
        }
        
        m_store = new BTreeStore(m_storage, ownsStorage: false);
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        m_store?.Dispose();
        m_storage?.Dispose();
        
        if (m_tempFile != null && File.Exists(m_tempFile))
        {
            try { File.Delete(m_tempFile); } catch { }
            m_tempFile = null;
        }
    }

    [Benchmark(Description = "Put (insert)")]
    public void PutInsert()
    {
        for (int i = 0; i < Count; i++)
        {
            m_store.Put(m_keys[i], m_values[i]);
        }
    }

    [Benchmark(Description = "Put + Get")]
    public int PutThenGet()
    {
        // Insert half
        for (int i = 0; i < Count / 2; i++)
        {
            m_store.Put(m_keys[i], m_values[i]);
        }
        
        // Get all inserted
        int found = 0;
        for (int i = 0; i < Count / 2; i++)
        {
            if (m_store.Get(m_keys[i]) != null) found++;
        }
        return found;
    }
}

#endregion

#region Mixed Workload Benchmarks

/// <summary>
/// Realistic mixed workload benchmarks.
/// </summary>
[Config(typeof(CleanBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class MixedWorkloadBenchmarks
{
    private IStorage m_storage = null!;
    private BTreeStore m_store = null!;
    private (int Op, byte[] Key, byte[] Value)[] m_operations = null!;
    private string? m_tempFile;
    
    [Params(10000)]
    public int OperationCount { get; set; }
    
    [Params(StorageType.Memory, StorageType.File)]
    public StorageType Storage { get; set; }

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
            byte[] value = BitConverter.GetBytes(i);
            
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
        if (Storage == StorageType.File)
        {
            m_tempFile = Path.GetTempFileName();
            m_storage = new FileStorage(m_tempFile, 4096);
        }
        else
        {
            m_storage = new MemoryStorage(4096, 3000);
        }
        
        m_store = new BTreeStore(m_storage, ownsStorage: false);
        
        // Pre-populate
        for (int i = 0; i < 2500; i++)
        {
            m_store.Put(BitConverter.GetBytes(i), BitConverter.GetBytes(i));
        }
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        m_store?.Dispose();
        m_storage?.Dispose();
        
        if (m_tempFile != null && File.Exists(m_tempFile))
        {
            try { File.Delete(m_tempFile); } catch { }
            m_tempFile = null;
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
                    if (m_store.Delete(key)) result++;
                    break;
            }
        }
        
        return result;
    }
}

#endregion

#region Range Scan Benchmarks

/// <summary>
/// Benchmarks for range scan operations.
/// </summary>
[Config(typeof(CleanBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class RangeScanBenchmarks
{
    private IStorage m_storage = null!;
    private BTreeStore m_store = null!;
    private string? m_tempFile;
    
    [Params(10000, 50000)]
    public int TreeSize { get; set; }
    
    [Params(StorageType.Memory, StorageType.File)]
    public StorageType Storage { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        if (Storage == StorageType.File)
        {
            m_tempFile = Path.GetTempFileName();
            m_storage = new FileStorage(m_tempFile, 4096);
        }
        else
        {
            m_storage = new MemoryStorage(4096, TreeSize / 3 + 1000);
        }
        
        m_store = new BTreeStore(m_storage, ownsStorage: false);
        
        for (int i = 0; i < TreeSize; i++)
        {
            m_store.Put(BitConverter.GetBytes(i), BitConverter.GetBytes(i));
        }
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        m_store?.Dispose();
        m_storage?.Dispose();
        
        if (m_tempFile != null && File.Exists(m_tempFile))
        {
            try { File.Delete(m_tempFile); } catch { }
        }
    }

    [Benchmark(Description = "Scan 1000 entries")]
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
}

#endregion

#region Overflow Benchmarks

/// <summary>
/// Benchmarks comparing inline vs overflow values.
/// </summary>
[Config(typeof(CleanBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class OverflowBenchmarks
{
    private IStorage m_storage = null!;
    private BTreeStore m_store = null!;
    private byte[][] m_keys = null!;
    private byte[][] m_smallValues = null!;
    private byte[][] m_largeValues = null!;
    private string? m_tempFile;
    
    [Params(1000)]
    public int Count { get; set; }
    
    [Params(StorageType.Memory, StorageType.File)]
    public StorageType Storage { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        var random = new Random(42);
        m_keys = new byte[Count][];
        m_smallValues = new byte[Count][];
        m_largeValues = new byte[Count][];
        
        for (int i = 0; i < Count; i++)
        {
            m_keys[i] = BitConverter.GetBytes(i);
            m_smallValues[i] = new byte[100];
            m_largeValues[i] = new byte[2000]; // Overflow
            
            random.NextBytes(m_smallValues[i]);
            random.NextBytes(m_largeValues[i]);
        }
    }

    [IterationSetup]
    public void IterationSetup()
    {
        if (Storage == StorageType.File)
        {
            m_tempFile = Path.GetTempFileName();
            m_storage = new FileStorage(m_tempFile, 4096);
        }
        else
        {
            m_storage = new MemoryStorage(4096, Count * 3 + 1000);
        }
        
        m_store = new BTreeStore(m_storage, ownsStorage: false);
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        m_store?.Dispose();
        m_storage?.Dispose();
        
        if (m_tempFile != null && File.Exists(m_tempFile))
        {
            try { File.Delete(m_tempFile); } catch { }
            m_tempFile = null;
        }
    }

    [Benchmark(Description = "Insert Inline (100B values)")]
    public void InsertInline()
    {
        for (int i = 0; i < Count; i++)
        {
            m_store.Put(m_keys[i], m_smallValues[i]);
        }
    }

    [Benchmark(Description = "Insert Overflow (2KB values)")]
    public void InsertOverflow()
    {
        for (int i = 0; i < Count; i++)
        {
            m_store.Put(m_keys[i], m_largeValues[i]);
        }
    }
}

#endregion

#region Encryption Benchmarks

/// <summary>
/// Benchmarks for encryption overhead.
/// </summary>
[Config(typeof(CleanBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class EncryptionBenchmarks
{
    private byte[] m_key = null!;
    private byte[] m_salt = null!;
    private PageEncryptor m_pageEncryptor = null!;
    private BlockEncryptor m_blockEncryptor = null!;
    private byte[] m_plaintext = null!;
    private byte[] m_ciphertext = null!;
    private byte[] m_decrypted = null!;

    [Params(4096, 8192, 16384)]
    public int PageSize { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        m_key = RandomNumberGenerator.GetBytes(32);
        m_salt = RandomNumberGenerator.GetBytes(16);
        
        // Each encryptor gets its own provider (they dispose it)
        m_pageEncryptor = new PageEncryptor(new AesGcmCryptoProvider(m_key), m_salt);
        m_blockEncryptor = new BlockEncryptor(new AesGcmCryptoProvider(m_key), m_salt);

        m_plaintext = new byte[PageSize];
        Random.Shared.NextBytes(m_plaintext);
        m_ciphertext = new byte[PageSize + m_pageEncryptor.Overhead];
        m_decrypted = new byte[PageSize];
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        m_blockEncryptor?.Dispose();
        m_pageEncryptor?.Dispose();
    }

    [Benchmark(Description = "PageEncryptor.Encrypt")]
    public int PageEncrypt()
    {
        return m_pageEncryptor.Encrypt(m_plaintext, pageNumber: 1, m_ciphertext);
    }

    [Benchmark(Description = "PageEncryptor.Encrypt+Decrypt")]
    public int PageEncryptDecrypt()
    {
        int encLen = m_pageEncryptor.Encrypt(m_plaintext, pageNumber: 1, m_ciphertext);
        return m_pageEncryptor.Decrypt(m_ciphertext.AsSpan(0, encLen), pageNumber: 1, m_decrypted);
    }

    [Benchmark(Description = "BlockEncryptor.Encrypt")]
    public byte[] BlockEncrypt()
    {
        return m_blockEncryptor.Encrypt(m_plaintext, blockId: 1);
    }

    [Benchmark(Description = "BlockEncryptor.Encrypt+Decrypt")]
    public byte[]? BlockEncryptDecrypt()
    {
        var encrypted = m_blockEncryptor.Encrypt(m_plaintext, blockId: 1);
        return m_blockEncryptor.Decrypt(encrypted, blockId: 1);
    }
}

#endregion

#region Encrypted Storage Benchmarks

/// <summary>
/// Benchmarks comparing encrypted vs non-encrypted storage.
/// </summary>
[Config(typeof(CleanBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class EncryptedStorageBenchmarks
{
    private byte[] m_key = null!;
    private byte[] m_salt = null!;
    private IStorage m_storage = null!;
    private BTreeStore m_store = null!;
    private byte[][] m_keys = null!;
    private byte[][] m_values = null!;

    [Params(1000, 5000)]
    public int Count { get; set; }

    [Params(false, true)]
    public bool Encrypted { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        m_key = RandomNumberGenerator.GetBytes(32);
        m_salt = RandomNumberGenerator.GetBytes(16);

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
        if (Encrypted)
        {
            var innerStorage = new MemoryStorage(4096 + 28, Count / 3 + 1000);
            var provider = new AesGcmCryptoProvider(m_key);
            var encryptor = new PageEncryptor(provider, m_salt);
            m_storage = new EncryptedStorage(innerStorage, encryptor);
        }
        else
        {
            m_storage = new MemoryStorage(4096, Count / 3 + 1000);
        }
        
        m_store = new BTreeStore(m_storage, ownsStorage: false);
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        m_store?.Dispose();
        m_storage?.Dispose();
    }

    [Benchmark(Description = "Insert")]
    public void Insert()
    {
        for (int i = 0; i < Count; i++)
        {
            m_store.Put(m_keys[i], m_values[i]);
        }
    }

    [Benchmark(Description = "Insert + Get")]
    public int InsertAndGet()
    {
        for (int i = 0; i < Count; i++)
        {
            m_store.Put(m_keys[i], m_values[i]);
        }

        int found = 0;
        for (int i = 0; i < Count; i++)
        {
            if (m_store.Get(m_keys[i]) != null) found++;
        }
        return found;
    }
}

#endregion

#region Encrypted Mixed Workload Benchmarks

/// <summary>
/// Realistic mixed workload benchmarks with encryption.
/// </summary>
[Config(typeof(CleanBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class EncryptedMixedWorkloadBenchmarks
{
    private byte[] m_key = null!;
    private byte[] m_salt = null!;
    private IStorage m_storage = null!;
    private BTreeStore m_store = null!;
    private (int Op, byte[] Key, byte[] Value)[] m_operations = null!;

    [Params(10000)]
    public int OperationCount { get; set; }

    [Params(false, true)]
    public bool Encrypted { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        m_key = RandomNumberGenerator.GetBytes(32);
        m_salt = RandomNumberGenerator.GetBytes(16);

        var random = new Random(42);
        m_operations = new (int, byte[], byte[])[OperationCount];

        for (int i = 0; i < OperationCount; i++)
        {
            int op = random.Next(100);
            int keyInt = random.Next(5000);
            byte[] key = BitConverter.GetBytes(keyInt);
            byte[] value = BitConverter.GetBytes(i);

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
        if (Encrypted)
        {
            var innerStorage = new MemoryStorage(4096 + 28, 5000);
            var provider = new AesGcmCryptoProvider(m_key);
            var encryptor = new PageEncryptor(provider, m_salt);
            m_storage = new EncryptedStorage(innerStorage, encryptor);
        }
        else
        {
            m_storage = new MemoryStorage(4096, 5000);
        }

        m_store = new BTreeStore(m_storage, ownsStorage: false);

        // Pre-populate
        for (int i = 0; i < 2500; i++)
        {
            m_store.Put(BitConverter.GetBytes(i), BitConverter.GetBytes(i));
        }
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        m_store?.Dispose();
        m_storage?.Dispose();
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
                    if (m_store.Delete(key)) result++;
                    break;
            }
        }

        return result;
    }
}

#endregion

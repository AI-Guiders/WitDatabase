using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using OutWit.Database.Core.Concurrency;
using OutWit.Database.Core.Interfaces;
using OutWit.Database.Core.Storage;
using OutWit.Database.Core.Stores;
using OutWit.Database.Core.Transactions;
using OutWit.Database.Core.Wal;

namespace OutWit.Database.Core.Tests.Benchmarks;

#region Transactional Store Benchmarks

/// <summary>
/// Benchmarks for TransactionalStore operations.
/// Measures transaction overhead vs direct store access.
/// </summary>
[Config(typeof(CleanBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class TransactionalStoreBenchmarks : IDisposable
{
    private IKeyValueStore m_directStore = null!;
    private TransactionalStore m_txStore = null!;
    private TransactionalStore m_txStoreNoJournal = null!;
    private IStorage m_storage = null!;
    private string m_testDir = null!;
    private byte[][] m_keys = null!;
    private byte[][] m_values = null!;

    [Params(100, 1000)]
    public int OperationCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        m_testDir = Path.Combine(Path.GetTempPath(), $"tx_bench_{Guid.NewGuid():N}");
        Directory.CreateDirectory(m_testDir);

        // Generate test data
        var random = new Random(42);
        m_keys = new byte[OperationCount][];
        m_values = new byte[OperationCount][];

        for (int i = 0; i < OperationCount; i++)
        {
            m_keys[i] = BitConverter.GetBytes(i);
            m_values[i] = new byte[100];
            random.NextBytes(m_values[i]);
        }
    }

    [IterationSetup]
    public void IterationSetup()
    {
        // Direct store (no transactions)
        m_storage = new StorageMemory(4096, OperationCount + 1000);
        m_directStore = new StoreBTree(m_storage, ownsStorage: false);

        // Transactional store with journal
        var storage1 = new StorageMemory(4096, OperationCount + 1000);
        var btree1 = new StoreBTree(storage1, ownsStorage: true);
        var journalPath = Path.Combine(m_testDir, $"bench_{Guid.NewGuid():N}.wal");
        var journal = new WalTransactionJournal(journalPath);
        var lockManager = new LockManager();
        m_txStore = new TransactionalStore(btree1, journal, lockManager);

        // Transactional store without journal (lock only)
        var storage2 = new StorageMemory(4096, OperationCount + 1000);
        var btree2 = new StoreBTree(storage2, ownsStorage: true);
        var lockManager2 = new LockManager();
        m_txStoreNoJournal = new TransactionalStore(btree2, null, lockManager2);
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        m_directStore?.Dispose();
        m_txStore?.Dispose();
        m_txStoreNoJournal?.Dispose();
        m_storage?.Dispose();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        if (Directory.Exists(m_testDir))
        {
            try { Directory.Delete(m_testDir, recursive: true); } catch { }
        }
    }

    [Benchmark(Baseline = true, Description = "Direct Store (no tx)")]
    public void DirectStoreInsert()
    {
        for (int i = 0; i < OperationCount; i++)
        {
            m_directStore.Put(m_keys[i], m_values[i]);
        }
    }

    [Benchmark(Description = "Auto-commit per operation")]
    public void TxStoreAutoCommit()
    {
        for (int i = 0; i < OperationCount; i++)
        {
            m_txStore.Put(m_keys[i], m_values[i]);
        }
    }

    [Benchmark(Description = "Single transaction (with journal)")]
    public void TxStoreSingleTransaction()
    {
        using var tx = m_txStore.BeginTransaction();
        for (int i = 0; i < OperationCount; i++)
        {
            tx.Put(m_keys[i], m_values[i]);
        }
        tx.Commit();
    }

    [Benchmark(Description = "Single transaction (no journal)")]
    public void TxStoreNoJournalSingleTransaction()
    {
        using var tx = m_txStoreNoJournal.BeginTransaction();
        for (int i = 0; i < OperationCount; i++)
        {
            tx.Put(m_keys[i], m_values[i]);
        }
        tx.Commit();
    }

    [Benchmark(Description = "Many small transactions")]
    public void TxStoreManySmallTransactions()
    {
        for (int i = 0; i < OperationCount; i++)
        {
            using var tx = m_txStoreNoJournal.BeginTransaction();
            tx.Put(m_keys[i], m_values[i]);
            tx.Commit();
        }
    }

    public void Dispose()
    {
        IterationCleanup();
        GlobalCleanup();
    }
}

#endregion

#region Lock Manager Benchmarks

/// <summary>
/// Benchmarks for lock acquisition overhead.
/// </summary>
[Config(typeof(CleanBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class LockManagerBenchmarks : IDisposable
{
    private LockManager m_lockManager = null!;
    private DatabaseLock m_databaseLock = null!;

    [Params(1000, 10000)]
    public int LockCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        m_lockManager = new LockManager();
        m_databaseLock = new DatabaseLock();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        m_lockManager?.Dispose();
        m_databaseLock?.Dispose();
    }

    [Benchmark(Description = "DatabaseLock Read Acquire/Release")]
    public void DatabaseLockReadAcquireRelease()
    {
        for (int i = 0; i < LockCount; i++)
        {
            using var handle = m_databaseLock.AcquireReadLock();
        }
    }

    [Benchmark(Description = "DatabaseLock Write Acquire/Release")]
    public void DatabaseLockWriteAcquireRelease()
    {
        for (int i = 0; i < LockCount; i++)
        {
            using var handle = m_databaseLock.AcquireWriteLock();
        }
    }

    [Benchmark(Description = "LockManager Read (no file lock)")]
    public void LockManagerReadAcquireRelease()
    {
        for (int i = 0; i < LockCount; i++)
        {
            using var handle = m_lockManager.AcquireReadLock();
        }
    }

    [Benchmark(Description = "LockManager Write (no file lock)")]
    public void LockManagerWriteAcquireRelease()
    {
        for (int i = 0; i < LockCount; i++)
        {
            using var handle = m_lockManager.AcquireWriteLock();
        }
    }

    public void Dispose()
    {
        GlobalCleanup();
    }
}

#endregion

#region Concurrent Access Benchmarks

/// <summary>
/// Benchmarks for concurrent read/write scenarios.
/// </summary>
[Config(typeof(CleanBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class ConcurrentAccessBenchmarks : IDisposable
{
    private TransactionalStore m_store = null!;
    private byte[][] m_keys = null!;
    private byte[][] m_values = null!;
    private string m_testDir = null!;

    [Params(4, 8)]
    public int ReaderCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        m_testDir = Path.Combine(Path.GetTempPath(), $"conc_bench_{Guid.NewGuid():N}");
        Directory.CreateDirectory(m_testDir);

        var storage = new StorageMemory(4096, 10000);
        var btree = new StoreBTree(storage, ownsStorage: true);
        var lockManager = new LockManager();
        m_store = new TransactionalStore(btree, null, lockManager);

        // Pre-populate
        m_keys = new byte[1000][];
        m_values = new byte[1000][];
        var random = new Random(42);

        for (int i = 0; i < 1000; i++)
        {
            m_keys[i] = BitConverter.GetBytes(i);
            m_values[i] = new byte[50];
            random.NextBytes(m_values[i]);
            m_store.Put(m_keys[i], m_values[i]);
        }
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        m_store?.Dispose();
        if (Directory.Exists(m_testDir))
        {
            try { Directory.Delete(m_testDir, recursive: true); } catch { }
        }
    }

    [Benchmark(Description = "Concurrent Readers")]
    public async Task ConcurrentReaders()
    {
        var tasks = Enumerable.Range(0, ReaderCount).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                var result = m_store.Get(m_keys[i % 1000]);
            }
        })).ToArray();

        await Task.WhenAll(tasks);
    }

    [Benchmark(Description = "Sequential Readers (baseline)")]
    public void SequentialReaders()
    {
        for (int r = 0; r < ReaderCount; r++)
        {
            for (int i = 0; i < 100; i++)
            {
                var result = m_store.Get(m_keys[i % 1000]);
            }
        }
    }

    public void Dispose()
    {
        GlobalCleanup();
    }
}

#endregion

#region Transaction Commit Benchmarks

/// <summary>
/// Benchmarks for transaction commit overhead with different data sizes.
/// </summary>
[Config(typeof(CleanBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class TransactionCommitBenchmarks : IDisposable
{
    private TransactionalStore m_store = null!;
    private string m_testDir = null!;
    private byte[][] m_keys = null!;
    private byte[][] m_smallValues = null!;
    private byte[][] m_largeValues = null!;

    [Params(10, 100)]
    public int TransactionSize { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        m_testDir = Path.Combine(Path.GetTempPath(), $"commit_bench_{Guid.NewGuid():N}");
        Directory.CreateDirectory(m_testDir);

        var random = new Random(42);
        m_keys = new byte[1000][];
        m_smallValues = new byte[1000][];
        m_largeValues = new byte[1000][];

        for (int i = 0; i < 1000; i++)
        {
            m_keys[i] = BitConverter.GetBytes(i);
            m_smallValues[i] = new byte[50];
            m_largeValues[i] = new byte[10000];
            random.NextBytes(m_smallValues[i]);
            random.NextBytes(m_largeValues[i]);
        }
    }

    [IterationSetup]
    public void IterationSetup()
    {
        var storage = new StorageMemory(4096, 50000);
        var btree = new StoreBTree(storage, ownsStorage: true);
        var journalPath = Path.Combine(m_testDir, $"bench_{Guid.NewGuid():N}.wal");
        var journal = new WalTransactionJournal(journalPath);
        var lockManager = new LockManager();
        m_store = new TransactionalStore(btree, journal, lockManager);
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        m_store?.Dispose();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        if (Directory.Exists(m_testDir))
        {
            try { Directory.Delete(m_testDir, recursive: true); } catch { }
        }
    }

    [Benchmark(Description = "Commit small values")]
    public void CommitSmallValues()
    {
        using var tx = m_store.BeginTransaction();
        for (int i = 0; i < TransactionSize; i++)
        {
            tx.Put(m_keys[i], m_smallValues[i]);
        }
        tx.Commit();
    }

    [Benchmark(Description = "Commit large values")]
    public void CommitLargeValues()
    {
        using var tx = m_store.BeginTransaction();
        for (int i = 0; i < TransactionSize; i++)
        {
            tx.Put(m_keys[i], m_largeValues[i]);
        }
        tx.Commit();
    }

    [Benchmark(Description = "Commit then Checkpoint")]
    public void CommitAndCheckpoint()
    {
        using var tx = m_store.BeginTransaction();
        for (int i = 0; i < TransactionSize; i++)
        {
            tx.Put(m_keys[i], m_smallValues[i]);
        }
        tx.Commit();
        m_store.Checkpoint();
    }

    public void Dispose()
    {
        IterationCleanup();
        GlobalCleanup();
    }
}

#endregion

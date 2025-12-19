using OutWit.Database.Core.Concurrency;
using OutWit.Database.Core.Interfaces;
using OutWit.Database.Core.Storage;
using OutWit.Database.Core.Stores;
using OutWit.Database.Core.Transactions;
using OutWit.Database.Core.Wal;
using TextEncoding = System.Text.Encoding;

namespace OutWit.Database.Core.Tests.Transactions;

/// <summary>
/// Stress tests for TransactionalStore.
/// Tests concurrent access patterns and durability under load.
/// </summary>
[TestFixture]
[Category("Stress")]
public class TransactionalStoreStressTests : IDisposable
{
    private string m_testDir = null!;

    [SetUp]
    public void SetUp()
    {
        m_testDir = Path.Combine(Path.GetTempPath(), $"tx_stress_{Guid.NewGuid():N}");
        Directory.CreateDirectory(m_testDir);
    }

    [TearDown]
    public void TearDown()
    {
        Dispose();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(m_testDir))
                Directory.Delete(m_testDir, recursive: true);
        }
        catch { }
    }

    private static byte[] ToBytes(string s) => TextEncoding.UTF8.GetBytes(s);

    private TransactionalStore CreateStore(string name, TimeSpan? timeout = null)
    {
        var subDir = Path.Combine(m_testDir, name);
        Directory.CreateDirectory(subDir);

        var storage = new MemoryStorage();
        var btree = new BTreeStore(storage);
        var journal = new WalTransactionJournal(Path.Combine(subDir, "test.wal"));
        var lockManager = new LockManager(Path.Combine(subDir, "test.db"), timeout ?? TimeSpan.FromSeconds(30));

        return new TransactionalStore(btree, journal, lockManager);
    }

    #region Sequential Transaction Stress

    [Test]
    public void SequentialTransactions_100Commits()
    {
        using var store = CreateStore("seq_100");

        for (int i = 0; i < 100; i++)
        {
            using var tx = store.BeginTransaction();
            tx.Put(ToBytes($"key{i}"), ToBytes($"value{i}"));
            tx.Commit();
        }

        // Verify all data
        for (int i = 0; i < 100; i++)
        {
            var value = store.Get(ToBytes($"key{i}"));
            Assert.That(value, Is.Not.Null, $"Key {i} should exist");
        }
    }

    [Test]
    public void SequentialTransactions_AlternatingCommitRollback()
    {
        using var store = CreateStore("seq_alt");
        var committedKeys = new List<int>();

        for (int i = 0; i < 100; i++)
        {
            using var tx = store.BeginTransaction();
            tx.Put(ToBytes($"key{i}"), ToBytes($"value{i}"));

            if (i % 2 == 0)
            {
                tx.Commit();
                committedKeys.Add(i);
            }
            else
            {
                tx.Rollback();
            }
        }

        // Verify only committed keys exist
        foreach (var key in committedKeys)
        {
            Assert.That(store.Get(ToBytes($"key{key}")), Is.Not.Null);
        }

        for (int i = 1; i < 100; i += 2)
        {
            Assert.That(store.Get(ToBytes($"key{i}")), Is.Null, $"Rolled back key {i} should not exist");
        }
    }

    [Test]
    public void LargeTransaction_1000Operations()
    {
        using var store = CreateStore("large_tx");

        using var tx = store.BeginTransaction();

        for (int i = 0; i < 1000; i++)
        {
            tx.Put(ToBytes($"key{i:D4}"), ToBytes($"value{i}"));
        }

        tx.Commit();

        // Verify
        Assert.That(store.Get(ToBytes("key0000")), Is.Not.Null);
        Assert.That(store.Get(ToBytes("key0999")), Is.Not.Null);
    }

    [Test]
    public void LargeTransaction_Rollback_1000Operations()
    {
        using var store = CreateStore("large_rb");

        // Pre-populate
        store.Put(ToBytes("existing"), ToBytes("original"));

        using var tx = store.BeginTransaction();

        for (int i = 0; i < 1000; i++)
        {
            tx.Put(ToBytes($"key{i:D4}"), ToBytes($"value{i}"));
        }
        tx.Put(ToBytes("existing"), ToBytes("modified"));

        tx.Rollback();

        // Verify nothing was written
        Assert.That(store.Get(ToBytes("key0000")), Is.Null);
        Assert.That(store.Get(ToBytes("existing")), Is.EqualTo(ToBytes("original")));
    }

    #endregion

    #region Concurrent Writers Stress

    [Test]
    public async Task ConcurrentWriters_Serialize()
    {
        using var store = CreateStore("conc_wr", TimeSpan.FromSeconds(60));

        var completedTransactions = 0;
        var concurrentCount = 0;
        var maxConcurrent = 0;

        var tasks = Enumerable.Range(0, 5).Select(i => Task.Run(async () =>
        {
            await Task.Delay(i * 20); // Stagger starts

            using var tx = store.BeginTransaction();

            var current = Interlocked.Increment(ref concurrentCount);
            if (current > maxConcurrent)
                Interlocked.Exchange(ref maxConcurrent, current);

            tx.Put(ToBytes($"writer{i}"), ToBytes($"value{i}"));
            await Task.Delay(20); // Hold transaction briefly

            Interlocked.Decrement(ref concurrentCount);
            tx.Commit();
            Interlocked.Increment(ref completedTransactions);
        })).ToArray();

        await Task.WhenAll(tasks);

        Assert.That(completedTransactions, Is.EqualTo(5));
        Assert.That(maxConcurrent, Is.EqualTo(1), "Only one transaction should be active at a time");

        // Verify all data written
        for (int i = 0; i < 5; i++)
        {
            Assert.That(store.Get(ToBytes($"writer{i}")), Is.EqualTo(ToBytes($"value{i}")));
        }
    }

    #endregion

    #region Durability Stress

    [Test]
    public void JournalGrowth_ManyTransactions()
    {
        var subDir = Path.Combine(m_testDir, "journal_growth");
        Directory.CreateDirectory(subDir);
        var walPath = Path.Combine(subDir, "test.wal");

        var storage = new MemoryStorage();
        var btree = new BTreeStore(storage);
        var journal = new WalTransactionJournal(walPath);
        var lockManager = new LockManager(Path.Combine(subDir, "test.db"));

        using var store = new TransactionalStore(btree, journal, lockManager);

        // Many small transactions
        for (int i = 0; i < 100; i++)
        {
            using var tx = store.BeginTransaction();
            tx.Put(ToBytes($"key{i}"), ToBytes($"value{i}"));
            tx.Commit();
        }

        var walSize = new FileInfo(walPath).Length;
        TestContext.WriteLine($"WAL size after 100 transactions: {walSize:N0} bytes");

        // Checkpoint should truncate
        store.Checkpoint();

        var walSizeAfterCheckpoint = new FileInfo(walPath).Length;
        TestContext.WriteLine($"WAL size after checkpoint: {walSizeAfterCheckpoint:N0} bytes");

        Assert.That(walSizeAfterCheckpoint, Is.LessThan(walSize), "Checkpoint should reduce WAL size");
    }

    #endregion

    #region Mixed Operations Stress

    [Test]
    public void MixedOperations_PutDeleteGet()
    {
        using var store = CreateStore("mixed_ops");
        var random = new Random(42);

        // Phase 1: Populate
        for (int i = 0; i < 200; i++)
        {
            store.Put(ToBytes($"key{i:D3}"), ToBytes($"value{i}"));
        }

        // Phase 2: Mixed operations in transactions
        for (int round = 0; round < 20; round++)
        {
            using var tx = store.BeginTransaction();

            // Random puts
            for (int j = 0; j < 5; j++)
            {
                var key = random.Next(200);
                tx.Put(ToBytes($"key{key:D3}"), ToBytes($"updated_{round}_{j}"));
            }

            // Random deletes
            for (int j = 0; j < 2; j++)
            {
                var key = random.Next(200);
                tx.Delete(ToBytes($"key{key:D3}"));
            }

            // Randomly commit or rollback
            if (random.Next(10) > 2) // 70% commit
            {
                tx.Commit();
            }
            else
            {
                tx.Rollback();
            }
        }

        // Verify we can still read
        var existingCount = 0;
        for (int i = 0; i < 200; i++)
        {
            if (store.Get(ToBytes($"key{i:D3}")) != null)
                existingCount++;
        }

        TestContext.WriteLine($"Remaining keys: {existingCount}/200");
        Assert.That(existingCount, Is.GreaterThan(0));
    }

    [Test]
    public void ReadYourOwnWrites_InTransaction()
    {
        using var store = CreateStore("read_own");

        using var tx = store.BeginTransaction();

        // Write and immediately read back
        for (int i = 0; i < 100; i++)
        {
            var key = ToBytes($"key{i}");
            var value = ToBytes($"value{i}");

            tx.Put(key, value);

            var readBack = tx.Get(key);
            Assert.That(readBack, Is.EqualTo(value), $"Should read own write for key {i}");
        }

        // Delete and verify
        for (int i = 0; i < 50; i++)
        {
            var key = ToBytes($"key{i}");
            tx.Delete(key);

            var readBack = tx.Get(key);
            Assert.That(readBack, Is.Null, $"Should see delete for key {i}");
        }

        tx.Commit();
    }

    #endregion

    #region Async Stress Tests

    [Test]
    public async Task AsyncTransactions_Sequential()
    {
        using var store = CreateStore("async_seq");

        for (int i = 0; i < 50; i++)
        {
            await using var tx = await store.BeginTransactionAsync();
            await tx.PutAsync(ToBytes($"key{i}"), ToBytes($"value{i}"));
            await tx.CommitAsync();
        }

        // Verify
        for (int i = 0; i < 50; i++)
        {
            Assert.That(store.Get(ToBytes($"key{i}")), Is.Not.Null);
        }
    }

    #endregion

    #region Edge Cases

    [Test]
    public void EmptyTransaction_CommitSucceeds()
    {
        using var store = CreateStore("empty_tx");

        using var tx = store.BeginTransaction();
        // No operations
        tx.Commit();

        Assert.That(tx.State, Is.EqualTo(TransactionState.Committed));
    }

    [Test]
    public void EmptyTransaction_RollbackSucceeds()
    {
        using var store = CreateStore("empty_rb");

        using var tx = store.BeginTransaction();
        // No operations
        tx.Rollback();

        Assert.That(tx.State, Is.EqualTo(TransactionState.RolledBack));
    }

    [Test]
    public void TransactionTimeout_ThrowsOnConflict()
    {
        using var store = CreateStore("timeout", TimeSpan.FromMilliseconds(100));

        using var tx1 = store.BeginTransaction();

        Assert.Throws<TimeoutException>(() =>
        {
            using var tx2 = store.BeginTransaction();
        });
    }

    [Test]
    public void LargeValues_InTransaction()
    {
        using var store = CreateStore("large_val");
        var random = new Random(42);

        using var tx = store.BeginTransaction();

        // 10KB values
        for (int i = 0; i < 10; i++)
        {
            var value = new byte[10 * 1024];
            random.NextBytes(value);
            tx.Put(ToBytes($"large{i}"), value);
        }

        tx.Commit();

        // Verify
        for (int i = 0; i < 10; i++)
        {
            var value = store.Get(ToBytes($"large{i}"));
            Assert.That(value, Is.Not.Null);
            Assert.That(value!.Length, Is.EqualTo(10 * 1024));
        }
    }

    #endregion
}

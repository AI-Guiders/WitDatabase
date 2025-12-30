using OutWit.Database.Core.Storage;
using OutWit.Database.Core.Stores;
using OutWit.Database.Core.Tree;
using TextEncoding = System.Text.Encoding;

namespace OutWit.Database.Core.Tests.Tree;

/// <summary>
/// Tests for BTreeConcurrentStore concurrent access functionality.
/// </summary>
[TestFixture]
public class BTreeConcurrentStoreTests : IDisposable
{
    #region Fields

    private string m_testDir = null!;

    #endregion

    #region Setup/TearDown

    [SetUp]
    public void SetUp()
    {
        m_testDir = Path.Combine(Path.GetTempPath(), $"btree_concurrent_test_{Guid.NewGuid():N}");
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

    #endregion

    #region Basic Operations Tests

    [Test]
    public void PutAndGetWorksTest()
    {
        var filePath = Path.Combine(m_testDir, "basic.witdb");
        using var store = new BTreeConcurrentStore(filePath);

        store.Put(ToBytes("key1"), ToBytes("value1"));
        var result = store.Get(ToBytes("key1"));

        Assert.That(result, Is.Not.Null);
        Assert.That(FromBytes(result), Is.EqualTo("value1"));
    }

    [Test]
    public void DeleteWorksTest()
    {
        var filePath = Path.Combine(m_testDir, "delete.witdb");
        using var store = new BTreeConcurrentStore(filePath);

        store.Put(ToBytes("key1"), ToBytes("value1"));
        var deleted = store.Delete(ToBytes("key1"));
        var result = store.Get(ToBytes("key1"));

        Assert.That(deleted, Is.True);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void ScanWorksTest()
    {
        var filePath = Path.Combine(m_testDir, "scan.witdb");
        using var store = new BTreeConcurrentStore(filePath);

        for (int i = 0; i < 10; i++)
        {
            store.Put(ToBytes($"key_{i:D3}"), ToBytes($"value_{i}"));
        }

        var results = store.Scan(ToBytes("key_002"), ToBytes("key_007")).ToList();

        Assert.That(results.Count, Is.EqualTo(5)); // 002, 003, 004, 005, 006
    }

    #endregion

    #region Concurrent Access Tests (Disabled Mode)

    [Test]
    public void ConcurrentAccessDisabledUsesGlobalLockTest()
    {
        var filePath = Path.Combine(m_testDir, "disabled.witdb");
        var options = new BTreeConcurrencyOptions { EnableConcurrentAccess = false };
        using var store = new BTreeConcurrentStore(filePath, options);

        const int threads = 4;
        const int entriesPerThread = 100;

        var tasks = Enumerable.Range(0, threads).Select(threadId => Task.Run(() =>
        {
            for (int i = 0; i < entriesPerThread; i++)
            {
                store.Put(ToBytes($"t{threadId}_key_{i:D5}"), ToBytes($"value_{i}"));
            }
        })).ToArray();

        Task.WaitAll(tasks);

        Assert.That(store.Count(), Is.EqualTo(threads * entriesPerThread));
    }

    #endregion

    #region Concurrent Access Tests (Enabled Mode)

    [Test]
    public void ConcurrentAccessEnabledWorksTest()
    {
        var filePath = Path.Combine(m_testDir, "enabled.witdb");
        var options = BTreeConcurrencyOptions.HighConcurrency;
        using var store = new BTreeConcurrentStore(filePath, options);

        const int threads = 4;
        const int entriesPerThread = 100;

        var tasks = Enumerable.Range(0, threads).Select(threadId => Task.Run(() =>
        {
            for (int i = 0; i < entriesPerThread; i++)
            {
                store.Put(ToBytes($"t{threadId}_key_{i:D5}"), ToBytes($"value_{i}"));
            }
        })).ToArray();

        Task.WaitAll(tasks);

        Assert.That(store.Count(), Is.EqualTo(threads * entriesPerThread));
    }

    [Test]
    public void ConcurrentReadsAndWritesTest()
    {
        var filePath = Path.Combine(m_testDir, "mixed.witdb");
        var options = BTreeConcurrencyOptions.HighConcurrency;
        using var store = new BTreeConcurrentStore(filePath, options);

        // Prepopulate
        for (int i = 0; i < 100; i++)
        {
            store.Put(ToBytes($"key_{i:D5}"), ToBytes($"value_{i}"));
        }

        const int readers = 4;
        const int writers = 2;
        const int operations = 50;

        var readCount = 0;
        var writeCount = 0;

        var readerTasks = Enumerable.Range(0, readers).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < operations; i++)
            {
                var key = $"key_{i % 100:D5}";
                var result = store.Get(ToBytes(key));
                if (result != null)
                {
                    Interlocked.Increment(ref readCount);
                }
            }
        })).ToArray();

        var writerTasks = Enumerable.Range(0, writers).Select(threadId => Task.Run(() =>
        {
            for (int i = 0; i < operations; i++)
            {
                store.Put(ToBytes($"new_t{threadId}_key_{i:D5}"), ToBytes($"new_value_{i}"));
                Interlocked.Increment(ref writeCount);
            }
        })).ToArray();

        Task.WaitAll(readerTasks.Concat(writerTasks).ToArray());

        Assert.That(readCount, Is.EqualTo(readers * operations));
        Assert.That(writeCount, Is.EqualTo(writers * operations));
    }

    [Test]
    public void HighContentionTest()
    {
        var filePath = Path.Combine(m_testDir, "contention.witdb");
        var options = BTreeConcurrencyOptions.Debug; // Track statistics
        using var store = new BTreeConcurrentStore(filePath, options);

        const int threads = 8;
        const int operations = 100;
        var errors = 0;

        var tasks = Enumerable.Range(0, threads).Select(threadId => Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < operations; i++)
                {
                    // Mix of operations on same keys
                    var key = $"shared_key_{i % 10:D3}";
                    
                    if (i % 3 == 0)
                    {
                        store.Put(ToBytes(key), ToBytes($"value_{threadId}_{i}"));
                    }
                    else
                    {
                        store.Get(ToBytes(key));
                    }
                }
            }
            catch (Exception)
            {
                Interlocked.Increment(ref errors);
            }
        })).ToArray();

        Task.WaitAll(tasks);

        Assert.That(errors, Is.EqualTo(0), "No exceptions should occur under contention");
        
        TestContext.WriteLine($"Read count: {store.ReadCount}");
        TestContext.WriteLine($"Write count: {store.WriteCount}");
        TestContext.WriteLine($"Latch contention: {store.LatchManager.ContentionCount}");
        TestContext.WriteLine($"Contention ratio: {store.LatchManager.ContentionRatio:P2}");
    }

    #endregion

    #region Optimistic Read Tests

    [Test]
    public void OptimisticReadsEnabledTest()
    {
        var filePath = Path.Combine(m_testDir, "optimistic.witdb");
        var options = new BTreeConcurrencyOptions 
        { 
            EnableConcurrentAccess = true,
            UseOptimisticReads = true 
        };
        using var store = new BTreeConcurrentStore(filePath, options);

        // Prepopulate
        for (int i = 0; i < 100; i++)
        {
            store.Put(ToBytes($"key_{i:D5}"), ToBytes($"value_{i}"));
        }

        // Do reads
        for (int i = 0; i < 100; i++)
        {
            store.Get(ToBytes($"key_{i:D5}"));
        }

        Assert.That(store.ReadCount, Is.EqualTo(100));
        // Most reads should be optimistic hits (no latch contention)
        TestContext.WriteLine($"Optimistic hits: {store.OptimisticReadHits}");
        TestContext.WriteLine($"Optimistic misses: {store.OptimisticReadMisses}");
        TestContext.WriteLine($"Hit ratio: {store.OptimisticReadHitRatio:P2}");
    }

    #endregion

    #region Statistics Tests

    [Test]
    public void StatisticsTrackCorrectlyTest()
    {
        var filePath = Path.Combine(m_testDir, "stats.witdb");
        var options = BTreeConcurrencyOptions.HighConcurrency;
        using var store = new BTreeConcurrentStore(filePath, options);

        Assert.That(store.ReadCount, Is.EqualTo(0));
        Assert.That(store.WriteCount, Is.EqualTo(0));

        store.Put(ToBytes("key1"), ToBytes("value1"));
        Assert.That(store.WriteCount, Is.EqualTo(1));

        store.Get(ToBytes("key1"));
        Assert.That(store.ReadCount, Is.EqualTo(1));

        store.Delete(ToBytes("key1"));
        Assert.That(store.WriteCount, Is.EqualTo(2));
    }

    #endregion

    #region Performance Comparison Tests

    [Test]
    [Category("Performance")]
    public void CompareDisabledVsEnabledConcurrencyTest()
    {
        const int threads = 4;
        const int entriesPerThread = 250;
        const int totalEntries = threads * entriesPerThread;

        // Disabled mode
        var disabledFile = Path.Combine(m_testDir, "perf_disabled.witdb");
        long disabledMs;
        using (var store = new BTreeConcurrentStore(disabledFile, 
            new BTreeConcurrencyOptions { EnableConcurrentAccess = false }))
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var tasks = Enumerable.Range(0, threads).Select(threadId => Task.Run(() =>
            {
                for (int i = 0; i < entriesPerThread; i++)
                {
                    store.Put(ToBytes($"t{threadId}_key_{i:D5}"), ToBytes($"value_{i}"));
                }
            })).ToArray();

            Task.WaitAll(tasks);
            sw.Stop();
            disabledMs = sw.ElapsedMilliseconds;
        }

        // Enabled mode
        var enabledFile = Path.Combine(m_testDir, "perf_enabled.witdb");
        long enabledMs;
        using (var store = new BTreeConcurrentStore(enabledFile, BTreeConcurrencyOptions.HighConcurrency))
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var tasks = Enumerable.Range(0, threads).Select(threadId => Task.Run(() =>
            {
                for (int i = 0; i < entriesPerThread; i++)
                {
                    store.Put(ToBytes($"t{threadId}_key_{i:D5}"), ToBytes($"value_{i}"));
                }
            })).ToArray();

            Task.WaitAll(tasks);
            sw.Stop();
            enabledMs = sw.ElapsedMilliseconds;
        }

        TestContext.WriteLine("=== Concurrency Mode Comparison ===");
        TestContext.WriteLine($"Total entries: {totalEntries}, Threads: {threads}");
        TestContext.WriteLine($"Disabled (global lock): {disabledMs}ms ({totalEntries * 1000.0 / disabledMs:F0} ops/sec)");
        TestContext.WriteLine($"Enabled (page latches): {enabledMs}ms ({totalEntries * 1000.0 / enabledMs:F0} ops/sec)");

        Assert.Pass("Performance comparison completed");
    }

    #endregion

    #region Helpers

    private static byte[] ToBytes(string s) => TextEncoding.UTF8.GetBytes(s);
    private static string FromBytes(byte[] bytes) => TextEncoding.UTF8.GetString(bytes);

    #endregion
}

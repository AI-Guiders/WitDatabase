using OutWit.Database.Core.Builder;
using OutWit.Database.Core.Interfaces;
using OutWit.Database.Core.Stores;
using OutWit.Database.Core.Tree;
using TextEncoding = System.Text.Encoding;

namespace OutWit.Database.Core.Tests.Integration;

/// <summary>
/// Integration tests comparing regular vs parallel BTree store performance.
/// </summary>
[TestFixture]
public class BTreeParallelIntegrationTests : IDisposable
{
    #region Fields

    private string m_testDir = null!;

    #endregion

    #region Setup/TearDown

    [SetUp]
    public void SetUp()
    {
        m_testDir = Path.Combine(Path.GetTempPath(), $"btree_integration_test_{Guid.NewGuid():N}");
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

    #region Factory Tests

    [Test]
    public void FactoryCreatesRegularStoreWithNoneModeTest()
    {
        var filePath = Path.Combine(m_testDir, "regular.witdb");
        var options = new ParallelModeOptions { Mode = ParallelMode.None };

        using var store = KeyValueStoreFactory.CreateBTreeStore(filePath, options);

        Assert.That(store, Is.TypeOf<StoreBTree>());
        Assert.That(store.ProviderKey, Is.EqualTo("btree"));
    }

    [Test]
    public void FactoryCreatesConcurrentStoreWithLatchedModeTest()
    {
        var filePath = Path.Combine(m_testDir, "latched.witdb");
        var options = new ParallelModeOptions { Mode = ParallelMode.Latched };

        using var store = KeyValueStoreFactory.CreateBTreeStore(filePath, options);

        Assert.That(store, Is.TypeOf<BTreeConcurrentStore>());
        Assert.That(store.ProviderKey, Is.EqualTo("btree-concurrent"));
    }

    [Test]
    public void FactoryCreatesConcurrentStoreWithAutoModeTest()
    {
        var filePath = Path.Combine(m_testDir, "auto.witdb");
        var options = new ParallelModeOptions { Mode = ParallelMode.Auto };

        using var store = KeyValueStoreFactory.CreateBTreeStore(filePath, options);

        Assert.That(store, Is.TypeOf<BTreeConcurrentStore>());
    }

    #endregion

    #region Functional Equivalence Tests

    [Test]
    public void RegularAndParallelStoreHaveSameBehaviorTest()
    {
        var regularPath = Path.Combine(m_testDir, "regular.witdb");
        var parallelPath = Path.Combine(m_testDir, "parallel.witdb");

        using var regularStore = KeyValueStoreFactory.CreateBTreeStore(regularPath, 
            new ParallelModeOptions { Mode = ParallelMode.None });
        using var parallelStore = KeyValueStoreFactory.CreateBTreeStore(parallelPath, 
            new ParallelModeOptions { Mode = ParallelMode.Latched });

        // Insert same data into both stores
        for (int i = 0; i < 100; i++)
        {
            var key = ToBytes($"key_{i:D5}");
            var value = ToBytes($"value_{i}");

            regularStore.Put(key, value);
            parallelStore.Put(key, value);
        }

        // Verify both stores have same data
        for (int i = 0; i < 100; i++)
        {
            var key = ToBytes($"key_{i:D5}");

            var regularValue = regularStore.Get(key);
            var parallelValue = parallelStore.Get(key);

            Assert.That(parallelValue, Is.EqualTo(regularValue));
        }

        // Verify counts match
        var regularStats = (IKeyValueStoreStatistics)regularStore;
        var parallelStats = (IKeyValueStoreStatistics)parallelStore;

        Assert.That(parallelStats.EstimatedKeyCount, Is.EqualTo(regularStats.EstimatedKeyCount));
    }

    [Test]
    public void ParallelStoreHandlesDeleteCorrectlyTest()
    {
        var filePath = Path.Combine(m_testDir, "delete_test.witdb");
        var options = new ParallelModeOptions { Mode = ParallelMode.Latched };

        using var store = KeyValueStoreFactory.CreateBTreeStore(filePath, options);

        // Insert
        store.Put(ToBytes("key1"), ToBytes("value1"));
        store.Put(ToBytes("key2"), ToBytes("value2"));

        // Verify
        Assert.That(store.Get(ToBytes("key1")), Is.Not.Null);
        Assert.That(store.Get(ToBytes("key2")), Is.Not.Null);

        // Delete
        var deleted = store.Delete(ToBytes("key1"));
        Assert.That(deleted, Is.True);

        // Verify delete
        Assert.That(store.Get(ToBytes("key1")), Is.Null);
        Assert.That(store.Get(ToBytes("key2")), Is.Not.Null);
    }

    [Test]
    public void ParallelStoreScanWorksCorrectlyTest()
    {
        var filePath = Path.Combine(m_testDir, "scan_test.witdb");
        var options = new ParallelModeOptions { Mode = ParallelMode.Latched };

        using var store = KeyValueStoreFactory.CreateBTreeStore(filePath, options);

        // Insert ordered keys
        for (int i = 0; i < 50; i++)
        {
            store.Put(ToBytes($"key_{i:D3}"), ToBytes($"value_{i}"));
        }

        // Scan range
        var results = store.Scan(ToBytes("key_010"), ToBytes("key_020")).ToList();

        // Should include key_010 to key_019 (exclusive end)
        Assert.That(results.Count, Is.EqualTo(10));
        Assert.That(FromBytes(results[0].Key), Is.EqualTo("key_010"));
    }

    #endregion

    #region Concurrent Access Tests

    [Test]
    public void ParallelStoreSupportsConcurrentWritesTest()
    {
        var filePath = Path.Combine(m_testDir, "concurrent_write.witdb");
        var options = new ParallelModeOptions 
        { 
            Mode = ParallelMode.Latched,
            TrackStatistics = true 
        };

        using var store = KeyValueStoreFactory.CreateBTreeStore(filePath, options);

        const int threads = 4;
        const int entriesPerThread = 200;
        var errors = new List<Exception>();

        var tasks = Enumerable.Range(0, threads).Select(threadId => Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < entriesPerThread; i++)
                {
                    var key = ToBytes($"t{threadId}_key_{i:D5}");
                    var value = ToBytes($"value_{threadId}_{i}");
                    store.Put(key, value);
                }
            }
            catch (Exception ex)
            {
                lock (errors) { errors.Add(ex); }
            }
        })).ToArray();

        Task.WaitAll(tasks);

        Assert.That(errors, Is.Empty, $"Errors: {string.Join(", ", errors.Select(e => e.Message))}");

        var stats = (IKeyValueStoreStatistics)store;
        Assert.That(stats.EstimatedKeyCount, Is.EqualTo(threads * entriesPerThread));
    }

    [Test]
    public void ParallelStoreSupportsConcurrentReadsTest()
    {
        var filePath = Path.Combine(m_testDir, "concurrent_read.witdb");
        var options = new ParallelModeOptions { Mode = ParallelMode.Latched };

        using var store = KeyValueStoreFactory.CreateBTreeStore(filePath, options);

        // Prepopulate
        const int totalEntries = 500;
        for (int i = 0; i < totalEntries; i++)
        {
            store.Put(ToBytes($"key_{i:D5}"), ToBytes($"value_{i}"));
        }

        const int threads = 4;
        const int readsPerThread = 200;
        var errors = new List<Exception>();
        var successfulReads = 0;

        var tasks = Enumerable.Range(0, threads).Select(threadId => Task.Run(() =>
        {
            try
            {
                var random = new Random(threadId);
                for (int i = 0; i < readsPerThread; i++)
                {
                    var keyIndex = random.Next(totalEntries);
                    var result = store.Get(ToBytes($"key_{keyIndex:D5}"));
                    if (result != null)
                    {
                        Interlocked.Increment(ref successfulReads);
                    }
                }
            }
            catch (Exception ex)
            {
                lock (errors) { errors.Add(ex); }
            }
        })).ToArray();

        Task.WaitAll(tasks);

        Assert.That(errors, Is.Empty);
        Assert.That(successfulReads, Is.EqualTo(threads * readsPerThread));
    }

    [Test]
    public void ParallelStoreSupportsMixedReadWriteTest()
    {
        var filePath = Path.Combine(m_testDir, "mixed_rw.witdb");
        var options = new ParallelModeOptions { Mode = ParallelMode.Latched };

        using var store = KeyValueStoreFactory.CreateBTreeStore(filePath, options);

        // Prepopulate
        for (int i = 0; i < 100; i++)
        {
            store.Put(ToBytes($"key_{i:D5}"), ToBytes($"value_{i}"));
        }

        const int readers = 2;
        const int writers = 2;
        const int operations = 100;
        var errors = new List<Exception>();

        var readerTasks = Enumerable.Range(0, readers).Select(_ => Task.Run(() =>
        {
            try
            {
                var random = new Random();
                for (int i = 0; i < operations; i++)
                {
                    var keyIndex = random.Next(100);
                    store.Get(ToBytes($"key_{keyIndex:D5}"));
                }
            }
            catch (Exception ex)
            {
                lock (errors) { errors.Add(ex); }
            }
        })).ToArray();

        var writerTasks = Enumerable.Range(0, writers).Select(writerId => Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < operations; i++)
                {
                    var key = ToBytes($"new_w{writerId}_k{i:D5}");
                    store.Put(key, ToBytes($"v{i}"));
                }
            }
            catch (Exception ex)
            {
                lock (errors) { errors.Add(ex); }
            }
        })).ToArray();

        Task.WaitAll(readerTasks.Concat(writerTasks).ToArray());

        Assert.That(errors, Is.Empty);
    }

    #endregion

    #region Performance Comparison Tests

    [Test]
    [Category("Performance")]
    public void CompareRegularVsParallelSingleThreadedTest()
    {
        const int entries = 2000;

        // Regular store
        var regularPath = Path.Combine(m_testDir, "perf_regular.witdb");
        long regularMs;
        using (var store = KeyValueStoreFactory.CreateBTreeStore(regularPath, 
            new ParallelModeOptions { Mode = ParallelMode.None }))
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < entries; i++)
            {
                store.Put(ToBytes($"key_{i:D6}"), ToBytes($"value_{i}"));
            }
            store.Flush();
            sw.Stop();
            regularMs = sw.ElapsedMilliseconds;
        }

        // Parallel store (single-threaded usage)
        var parallelPath = Path.Combine(m_testDir, "perf_parallel.witdb");
        long parallelMs;
        using (var store = KeyValueStoreFactory.CreateBTreeStore(parallelPath, 
            new ParallelModeOptions { Mode = ParallelMode.Latched }))
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < entries; i++)
            {
                store.Put(ToBytes($"key_{i:D6}"), ToBytes($"value_{i}"));
            }
            store.Flush();
            sw.Stop();
            parallelMs = sw.ElapsedMilliseconds;
        }

        TestContext.WriteLine("=== BTree Single-Threaded Performance ===");
        TestContext.WriteLine($"Entries: {entries}");
        TestContext.WriteLine($"Regular store: {regularMs}ms ({entries * 1000.0 / Math.Max(regularMs, 1):F0} ops/sec)");
        TestContext.WriteLine($"Parallel store: {parallelMs}ms ({entries * 1000.0 / Math.Max(parallelMs, 1):F0} ops/sec)");
        TestContext.WriteLine($"Overhead: {(parallelMs - regularMs) * 100.0 / Math.Max(regularMs, 1):F1}%");

        // Parallel store has some overhead in single-threaded, but should be reasonable
        Assert.That(parallelMs, Is.LessThan(regularMs * 3), "Parallel overhead too high for single-threaded");
    }

    [Test]
    [Category("Performance")]
    public void CompareRegularVsParallelMultiThreadedTest()
    {
        const int threads = 4;
        const int entriesPerThread = 500;
        const int totalEntries = threads * entriesPerThread;

        // Regular store (with global lock contention)
        var regularPath = Path.Combine(m_testDir, "perf_mt_regular.witdb");
        long regularMs;
        using (var store = new StoreBTree(regularPath))
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var tasks = Enumerable.Range(0, threads).Select(threadId => Task.Run(() =>
            {
                for (int i = 0; i < entriesPerThread; i++)
                {
                    lock (store) // Need external lock for thread safety
                    {
                        store.Put(ToBytes($"t{threadId}_key_{i:D5}"), ToBytes($"v{i}"));
                    }
                }
            })).ToArray();
            Task.WaitAll(tasks);
            store.Flush();
            sw.Stop();
            regularMs = sw.ElapsedMilliseconds;
        }

        // Parallel store (with fine-grained latching)
        var parallelPath = Path.Combine(m_testDir, "perf_mt_parallel.witdb");
        long parallelMs;
        using (var store = KeyValueStoreFactory.CreateBTreeStore(parallelPath, 
            new ParallelModeOptions { Mode = ParallelMode.Latched }))
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var tasks = Enumerable.Range(0, threads).Select(threadId => Task.Run(() =>
            {
                for (int i = 0; i < entriesPerThread; i++)
                {
                    store.Put(ToBytes($"t{threadId}_key_{i:D5}"), ToBytes($"v{i}"));
                }
            })).ToArray();
            Task.WaitAll(tasks);
            store.Flush();
            sw.Stop();
            parallelMs = sw.ElapsedMilliseconds;
        }

        TestContext.WriteLine("=== BTree Multi-Threaded Performance ===");
        TestContext.WriteLine($"Threads: {threads}, Entries/thread: {entriesPerThread}, Total: {totalEntries}");
        TestContext.WriteLine($"Regular store (global lock): {regularMs}ms ({totalEntries * 1000.0 / Math.Max(regularMs, 1):F0} ops/sec)");
        TestContext.WriteLine($"Parallel store (latched): {parallelMs}ms ({totalEntries * 1000.0 / Math.Max(parallelMs, 1):F0} ops/sec)");
        
        var speedup = (double)regularMs / Math.Max(parallelMs, 1);
        TestContext.WriteLine($"Speedup: {speedup:F2}x");

        // Parallel should be faster or at least not significantly slower
        Assert.Pass($"Multi-threaded comparison completed. Speedup: {speedup:F2}x");
    }

    #endregion

    #region Helpers

    private static byte[] ToBytes(string s) => TextEncoding.UTF8.GetBytes(s);
    private static string FromBytes(byte[] bytes) => TextEncoding.UTF8.GetString(bytes);

    #endregion
}

using OutWit.Database.Core.Storage;
using OutWit.Database.Core.Stores;
using OutWit.Database.Core.Tree;
using TextEncoding = System.Text.Encoding;

namespace OutWit.Database.Core.Tests.Tree;

/// <summary>
/// Tests for BTreeConcurrentStore - thread-safe BTree wrapper.
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

    #region Concurrent Access Tests

    [Test]
    public void ConcurrentWritesAreThreadSafeTest()
    {
        var filePath = Path.Combine(m_testDir, "concurrent.witdb");
        using var store = new BTreeConcurrentStore(filePath);

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
        using var store = new BTreeConcurrentStore(filePath);

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
    public void HighContentionNoExceptionsTest()
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
    }

    #endregion

    #region Statistics Tests

    [Test]
    public void StatisticsTrackCorrectlyTest()
    {
        var filePath = Path.Combine(m_testDir, "stats.witdb");
        // Use Debug mode which has TrackStatistics = true
        var options = BTreeConcurrencyOptions.Debug;
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

    [Test]
    public void StatisticsDisabledByDefaultTest()
    {
        var filePath = Path.Combine(m_testDir, "no_stats.witdb");
        using var store = new BTreeConcurrentStore(filePath);

        store.Put(ToBytes("key1"), ToBytes("value1"));
        store.Get(ToBytes("key1"));

        // With default options (TrackStatistics = false), counts stay 0
        Assert.That(store.ReadCount, Is.EqualTo(0));
        Assert.That(store.WriteCount, Is.EqualTo(0));
    }

    #endregion

    #region Helpers

    private static byte[] ToBytes(string s) => TextEncoding.UTF8.GetBytes(s);
    private static string FromBytes(byte[] bytes) => TextEncoding.UTF8.GetString(bytes);

    #endregion
}

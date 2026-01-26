using OutWit.Database.Core.Stores;
using OutWit.Database.Core.Tree;
using TextEncoding = System.Text.Encoding;

namespace OutWit.Database.Core.Tests.Tree;

/// <summary>
/// Stress tests for BTree concurrent access - thread-safety verification.
/// </summary>
[TestFixture]
public class BTreeConcurrentStoreStressTests : IDisposable
{
    #region Fields

    private string m_testDir = null!;

    #endregion

    #region Setup/TearDown

    [SetUp]
    public void SetUp()
    {
        m_testDir = Path.Combine(Path.GetTempPath(), $"btree_stress_test_{Guid.NewGuid():N}");
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

    #region Concurrency Tests

    [Test]
    [Category("Stress")]
    public void HighContentionWriteTest()
    {
        var filePath = Path.Combine(m_testDir, "contention.witdb");
        var options = new BTreeConcurrencyOptions
        {
            TrackStatistics = true
        };

        using var store = new BTreeConcurrentStore(filePath, options);

        const int threads = 4;
        const int operations = 100;
        var errors = new List<Exception>();

        // All threads write to same key range
        var tasks = Enumerable.Range(0, threads).Select(threadId => Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < operations; i++)
                {
                    var key = ToBytes($"shared_key_{i % 10}");
                    store.Put(key, ToBytes($"value_{threadId}_{i}"));
                }
            }
            catch (Exception ex)
            {
                lock (errors) { errors.Add(ex); }
            }
        })).ToArray();

        Task.WaitAll(tasks);

        TestContext.WriteLine($"Read count: {store.ReadCount}");
        TestContext.WriteLine($"Write count: {store.WriteCount}");

        Assert.That(errors, Is.Empty, $"Errors: {string.Join(", ", errors.Select(e => e.Message))}");
    }

    [Test]
    [Category("Stress")]
    public void ParallelPutGetTest()
    {
        var filePath = Path.Combine(m_testDir, "parallel_put_get.witdb");
        var options = new BTreeConcurrencyOptions { TrackStatistics = true };

        using var store = new BTreeConcurrentStore(filePath, options);

        const int threads = 4;
        const int operations = 200;
        var errors = new List<Exception>();

        var tasks = Enumerable.Range(0, threads).Select(threadId => Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < operations; i++)
                {
                    var key = ToBytes($"key_{threadId}_{i % 50}");
                    store.Put(key, ToBytes($"v{i}"));
                    store.Get(key);
                }
            }
            catch (Exception ex)
            {
                lock (errors) { errors.Add(ex); }
            }
        })).ToArray();

        Task.WaitAll(tasks);

        Assert.That(errors, Is.Empty, $"Errors: {string.Join(", ", errors.Select(e => e.Message))}");
    }

    #endregion

    #region High Volume Tests

    [Test]
    [Category("Stress")]
    public void HighVolumeWritesTest()
    {
        var filePath = Path.Combine(m_testDir, "high_volume.witdb");
        var options = new BTreeConcurrencyOptions { TrackStatistics = true };

        using var store = new BTreeConcurrentStore(filePath, options);

        const int threads = 4;
        const int entriesPerThread = 500;
        var errors = new List<Exception>();

        var sw = System.Diagnostics.Stopwatch.StartNew();

        var tasks = Enumerable.Range(0, threads).Select(threadId => Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < entriesPerThread; i++)
                {
                    var key = ToBytes($"t{threadId}_key_{i:D5}");
                    var value = ToBytes($"value_{i}");
                    store.Put(key, value);
                }
            }
            catch (Exception ex)
            {
                lock (errors) { errors.Add(ex); }
            }
        })).ToArray();

        Task.WaitAll(tasks);
        sw.Stop();

        var totalEntries = threads * entriesPerThread;
        var opsPerSec = totalEntries * 1000.0 / Math.Max(sw.ElapsedMilliseconds, 1);

        TestContext.WriteLine($"Total entries: {totalEntries}");
        TestContext.WriteLine($"Time: {sw.ElapsedMilliseconds}ms");
        TestContext.WriteLine($"Throughput: {opsPerSec:F0} ops/sec");

        Assert.That(errors, Is.Empty);
        Assert.That(store.Count(), Is.EqualTo(totalEntries));
    }

    [Test]
    [Category("Stress")]
    public void HighVolumeReadsTest()
    {
        var filePath = Path.Combine(m_testDir, "high_volume_read.witdb");
        var options = new BTreeConcurrencyOptions { TrackStatistics = true };

        using var store = new BTreeConcurrentStore(filePath, options);

        // Prepopulate
        const int totalEntries = 1000;
        for (int i = 0; i < totalEntries; i++)
        {
            store.Put(ToBytes($"key_{i:D5}"), ToBytes($"value_{i}"));
        }

        const int threads = 4;
        const int readsPerThread = 500;
        var errors = new List<Exception>();
        var successfulReads = 0;

        var sw = System.Diagnostics.Stopwatch.StartNew();

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
        sw.Stop();

        var totalReads = threads * readsPerThread;
        var opsPerSec = totalReads * 1000.0 / Math.Max(sw.ElapsedMilliseconds, 1);

        TestContext.WriteLine($"Total reads: {totalReads}");
        TestContext.WriteLine($"Time: {sw.ElapsedMilliseconds}ms");
        TestContext.WriteLine($"Throughput: {opsPerSec:F0} ops/sec");

        Assert.That(errors, Is.Empty);
        Assert.That(successfulReads, Is.EqualTo(totalReads));
    }

    #endregion

    #region Helpers

    private static byte[] ToBytes(string s) => TextEncoding.UTF8.GetBytes(s);

    #endregion
}

using OutWit.Database.Core.LSM;
using OutWit.Database.Core.Stores;
using TextEncoding = System.Text.Encoding;

namespace OutWit.Database.Core.Tests.LSM;

/// <summary>
/// Stress tests for LSM parallel write components.
/// </summary>
[TestFixture]
public class LsmParallelStressTests : IDisposable
{
    #region Fields

    private string m_testDir = null!;

    #endregion

    #region Setup/TearDown

    [SetUp]
    public void SetUp()
    {
        m_testDir = Path.Combine(Path.GetTempPath(), $"lsm_stress_test_{Guid.NewGuid():N}");
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

    #region LsmParallelWriter Tests

    [Test]
    [Category("Stress")]
    public async Task ParallelWriterMultiThreadTest()
    {
        var dir = Path.Combine(m_testDir, "parallel_writer");
        using var store = new StoreLsm(dir);
        using var writer = new LsmParallelWriter(store, bufferSizeThreshold: 32 * 1024);

        const int threads = 4;
        const int entriesPerThread = 500;
        var errors = new List<Exception>();

        var tasks = Enumerable.Range(0, threads).Select(threadId => Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < entriesPerThread; i++)
                {
                    var key = ToBytes($"t{threadId}_key_{i:D5}");
                    var value = ToBytes($"value_{i}");
                    writer.Put(key, value);
                }
            }
            catch (Exception ex)
            {
                lock (errors) { errors.Add(ex); }
            }
        })).ToArray();

        Task.WaitAll(tasks);
        await writer.FlushAllAsync();

        var totalEntries = threads * entriesPerThread;

        TestContext.WriteLine($"Total entries: {totalEntries}");
        TestContext.WriteLine($"Entries merged: {writer.EntriesMerged}");
        TestContext.WriteLine($"Buffers submitted: {writer.BuffersSubmitted}");

        Assert.That(errors, Is.Empty);
        Assert.That(writer.EntriesMerged, Is.EqualTo(totalEntries));
    }

    [Test]
    [Category("Stress")]
    public async Task ParallelWriterMixedOperationsTest()
    {
        var dir = Path.Combine(m_testDir, "mixed_ops");
        using var store = new StoreLsm(dir);
        using var writer = new LsmParallelWriter(store);

        const int threads = 4;
        const int operations = 200;
        var errors = new List<Exception>();

        var tasks = Enumerable.Range(0, threads).Select(threadId => Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < operations; i++)
                {
                    var key = ToBytes($"t{threadId}_key_{i % 50}");
                    
                    if (i % 5 == 0)
                    {
                        writer.Delete(key);
                    }
                    else
                    {
                        writer.Put(key, ToBytes($"value_{i}"));
                    }
                }
            }
            catch (Exception ex)
            {
                lock (errors) { errors.Add(ex); }
            }
        })).ToArray();

        Task.WaitAll(tasks);
        await writer.FlushAllAsync();

        TestContext.WriteLine($"Buffers submitted: {writer.BuffersSubmitted}");
        TestContext.WriteLine($"Merge operations: {writer.MergeOperations}");

        Assert.That(errors, Is.Empty);
    }

    #endregion

    #region Direct StoreLsm Tests

    [Test]
    [Category("Stress")]
    public void DirectStoreLsmParallelWriteTest()
    {
        var dir = Path.Combine(m_testDir, "direct_parallel");
        using var store = new StoreLsm(dir);

        const int threads = 4;
        const int entriesPerThread = 250;
        var errors = new List<Exception>();

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

        TestContext.WriteLine($"Total entries written: {threads * entriesPerThread}");

        Assert.That(errors, Is.Empty);
    }

    [Test]
    [Category("Stress")]
    public void DirectStoreLsmConcurrentReadWriteTest()
    {
        var dir = Path.Combine(m_testDir, "concurrent_rw");
        using var store = new StoreLsm(dir);

        // Prepopulate
        for (int i = 0; i < 100; i++)
        {
            store.Put(ToBytes($"key_{i:D5}"), ToBytes($"value_{i}"));
        }

        const int readers = 2;
        const int writers = 2;
        const int operations = 100;
        var errors = new List<Exception>();
        var readCount = 0;
        var writeCount = 0;

        var readerTasks = Enumerable.Range(0, readers).Select(_ => Task.Run(() =>
        {
            try
            {
                var random = new Random();
                for (int i = 0; i < operations; i++)
                {
                    var keyIndex = random.Next(100);
                    var result = store.Get(ToBytes($"key_{keyIndex:D5}"));
                    if (result != null)
                    {
                        Interlocked.Increment(ref readCount);
                    }
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
                    Interlocked.Increment(ref writeCount);
                }
            }
            catch (Exception ex)
            {
                lock (errors) { errors.Add(ex); }
            }
        })).ToArray();

        Task.WaitAll(readerTasks.Concat(writerTasks).ToArray());

        TestContext.WriteLine($"Read count: {readCount}");
        TestContext.WriteLine($"Write count: {writeCount}");

        Assert.That(errors, Is.Empty);
        Assert.That(writeCount, Is.EqualTo(writers * operations));
    }

    #endregion

    #region Helpers

    private static byte[] ToBytes(string s) => TextEncoding.UTF8.GetBytes(s);

    #endregion
}

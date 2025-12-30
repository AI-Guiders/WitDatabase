using System.Diagnostics;
using OutWit.Database.Core.LSM;
using OutWit.Database.Core.Stores;
using TextEncoding = System.Text.Encoding;

namespace OutWit.Database.Core.Tests.LSM;

/// <summary>
/// Performance comparison tests for LSM parallel vs sequential writes.
/// </summary>
[TestFixture]
public class LsmParallelPerformanceTests : IDisposable
{
    #region Fields

    private string m_testDir = null!;

    #endregion

    #region Setup/TearDown

    [SetUp]
    public void SetUp()
    {
        m_testDir = Path.Combine(Path.GetTempPath(), $"lsm_perf_test_{Guid.NewGuid():N}");
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

    #region Sequential vs Parallel Comparison Tests

    [Test]
    [Category("Performance")]
    public void SequentialWritesSingleThreadTest()
    {
        var dir = Path.Combine(m_testDir, "sequential_single");
        var options = new LsmOptions 
        { 
            EnableWal = false, 
            MemTableSizeLimit = 1024 * 1024,
            Level0CompactionTrigger = 100
        };
        
        using var store = new StoreLsm(dir, options);

        const int entryCount = 10_000;
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < entryCount; i++)
        {
            store.Put(ToBytes($"key_{i:D8}"), ToBytes($"value_{i:D8}"));
        }

        sw.Stop();

        TestContext.WriteLine($"Sequential single-thread: {entryCount} entries in {sw.ElapsedMilliseconds}ms");
        TestContext.WriteLine($"Throughput: {entryCount * 1000.0 / sw.ElapsedMilliseconds:F0} ops/sec");

        // Verify data
        Assert.That(store.Count(), Is.EqualTo(entryCount));
    }

    [Test]
    [Category("Performance")]
    public async Task ParallelWriterSingleThreadTest()
    {
        var dir = Path.Combine(m_testDir, "parallel_single");
        var options = new LsmOptions 
        { 
            EnableWal = false, 
            MemTableSizeLimit = 1024 * 1024,
            Level0CompactionTrigger = 100
        };
        
        using var store = new StoreLsm(dir, options);
        await using var writer = new LsmParallelWriter(store, bufferSizeThreshold: 32 * 1024);

        const int entryCount = 10_000;
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < entryCount; i++)
        {
            writer.Put(ToBytes($"key_{i:D8}"), ToBytes($"value_{i:D8}"));
        }
        await writer.FlushAllAsync();

        sw.Stop();

        TestContext.WriteLine($"Parallel single-thread: {entryCount} entries in {sw.ElapsedMilliseconds}ms");
        TestContext.WriteLine($"Throughput: {entryCount * 1000.0 / sw.ElapsedMilliseconds:F0} ops/sec");
        TestContext.WriteLine($"Buffers submitted: {writer.BuffersSubmitted}");
        TestContext.WriteLine($"Avg entries/merge: {writer.AverageEntriesPerMerge:F1}");

        // Verify data
        Assert.That(store.Count(), Is.EqualTo(entryCount));
    }

    [Test]
    [Category("Performance")]
    public void SequentialWritesMultiThreadTest()
    {
        var dir = Path.Combine(m_testDir, "sequential_multi");
        var options = new LsmOptions 
        { 
            EnableWal = false, 
            MemTableSizeLimit = 1024 * 1024,
            Level0CompactionTrigger = 100
        };
        
        using var store = new StoreLsm(dir, options);

        const int threads = 4;
        const int entriesPerThread = 2_500;
        var sw = Stopwatch.StartNew();

        var tasks = Enumerable.Range(0, threads).Select(threadId => Task.Run(() =>
        {
            for (int i = 0; i < entriesPerThread; i++)
            {
                store.Put(
                    ToBytes($"t{threadId}_key_{i:D8}"), 
                    ToBytes($"value_{threadId}_{i:D8}"));
            }
        })).ToArray();

        Task.WaitAll(tasks);

        sw.Stop();

        var totalEntries = threads * entriesPerThread;
        TestContext.WriteLine($"Sequential multi-thread ({threads} threads): {totalEntries} entries in {sw.ElapsedMilliseconds}ms");
        TestContext.WriteLine($"Throughput: {totalEntries * 1000.0 / sw.ElapsedMilliseconds:F0} ops/sec");

        // Verify data count (may be less due to key collisions in sequential mode)
        Assert.That(store.Count(), Is.EqualTo(totalEntries));
    }

    [Test]
    [Category("Performance")]
    public async Task ParallelWriterMultiThreadTest()
    {
        var dir = Path.Combine(m_testDir, "parallel_multi");
        var options = new LsmOptions 
        { 
            EnableWal = false, 
            MemTableSizeLimit = 1024 * 1024,
            Level0CompactionTrigger = 100
        };
        
        using var store = new StoreLsm(dir, options);
        await using var writer = new LsmParallelWriter(store, bufferSizeThreshold: 16 * 1024);

        const int threads = 4;
        const int entriesPerThread = 2_500;
        var sw = Stopwatch.StartNew();

        var tasks = Enumerable.Range(0, threads).Select(threadId => Task.Run(async () =>
        {
            for (int i = 0; i < entriesPerThread; i++)
            {
                writer.Put(
                    ToBytes($"t{threadId}_key_{i:D8}"), 
                    ToBytes($"value_{threadId}_{i:D8}"));
            }
            await writer.FlushCurrentBufferAsync();
        })).ToArray();

        await Task.WhenAll(tasks);

        sw.Stop();

        var totalEntries = threads * entriesPerThread;
        TestContext.WriteLine($"Parallel multi-thread ({threads} threads): {totalEntries} entries in {sw.ElapsedMilliseconds}ms");
        TestContext.WriteLine($"Throughput: {totalEntries * 1000.0 / sw.ElapsedMilliseconds:F0} ops/sec");
        TestContext.WriteLine($"Buffers submitted: {writer.BuffersSubmitted}");
        TestContext.WriteLine($"Merge operations: {writer.MergeOperations}");
        TestContext.WriteLine($"Avg entries/merge: {writer.AverageEntriesPerMerge:F1}");

        // Verify data
        Assert.That(store.Count(), Is.EqualTo(totalEntries));
    }

    [Test]
    [Category("Performance")]
    public async Task CompareSequentialVsParallelTest()
    {
        const int threads = 4;
        const int entriesPerThread = 2_500;
        const int totalEntries = threads * entriesPerThread;

        // Sequential test
        var seqDir = Path.Combine(m_testDir, "compare_seq");
        var options = new LsmOptions 
        { 
            EnableWal = false, 
            MemTableSizeLimit = 1024 * 1024,
            Level0CompactionTrigger = 100
        };

        long sequentialMs;
        using (var store = new StoreLsm(seqDir, options))
        {
            var sw = Stopwatch.StartNew();

            var tasks = Enumerable.Range(0, threads).Select(threadId => Task.Run(() =>
            {
                for (int i = 0; i < entriesPerThread; i++)
                {
                    store.Put(
                        ToBytes($"t{threadId}_key_{i:D8}"), 
                        ToBytes($"value_{threadId}_{i:D8}"));
                }
            })).ToArray();

            Task.WaitAll(tasks);
            sw.Stop();
            sequentialMs = sw.ElapsedMilliseconds;
        }

        // Parallel test
        var parDir = Path.Combine(m_testDir, "compare_par");
        long parallelMs;
        using (var store = new StoreLsm(parDir, options))
        {
            await using var writer = new LsmParallelWriter(store, bufferSizeThreshold: 16 * 1024);

            var sw = Stopwatch.StartNew();

            var tasks = Enumerable.Range(0, threads).Select(threadId => Task.Run(async () =>
            {
                for (int i = 0; i < entriesPerThread; i++)
                {
                    writer.Put(
                        ToBytes($"t{threadId}_key_{i:D8}"), 
                        ToBytes($"value_{threadId}_{i:D8}"));
                }
                await writer.FlushCurrentBufferAsync();
            })).ToArray();

            await Task.WhenAll(tasks);
            sw.Stop();
            parallelMs = sw.ElapsedMilliseconds;
        }

        // Report results
        TestContext.WriteLine("=== Performance Comparison ===");
        TestContext.WriteLine($"Total entries: {totalEntries}");
        TestContext.WriteLine($"Threads: {threads}");
        TestContext.WriteLine($"Sequential: {sequentialMs}ms ({totalEntries * 1000.0 / sequentialMs:F0} ops/sec)");
        TestContext.WriteLine($"Parallel:   {parallelMs}ms ({totalEntries * 1000.0 / parallelMs:F0} ops/sec)");

        if (parallelMs < sequentialMs)
        {
            var improvement = (double)sequentialMs / parallelMs;
            TestContext.WriteLine($"Improvement: {improvement:F2}x faster");
        }
        else
        {
            var slowdown = (double)parallelMs / sequentialMs;
            TestContext.WriteLine($"Slowdown: {slowdown:F2}x slower (expected for low contention)");
        }

        // Both should complete without errors
        Assert.Pass("Performance comparison completed");
    }

    [Test]
    [Category("Performance")]
    public async Task HighContentionComparisonTest()
    {
        const int threads = 8;
        const int entriesPerThread = 1_000;
        const int totalEntries = threads * entriesPerThread;

        var options = new LsmOptions 
        { 
            EnableWal = false, 
            MemTableSizeLimit = 512 * 1024,
            Level0CompactionTrigger = 100
        };

        // Sequential with high contention
        var seqDir = Path.Combine(m_testDir, "contention_seq");
        long sequentialMs;
        using (var store = new StoreLsm(seqDir, options))
        {
            var sw = Stopwatch.StartNew();

            var tasks = Enumerable.Range(0, threads).Select(threadId => Task.Run(() =>
            {
                for (int i = 0; i < entriesPerThread; i++)
                {
                    store.Put(
                        ToBytes($"t{threadId}_key_{i:D8}"), 
                        ToBytes($"value_{threadId}_{i:D8}"));
                }
            })).ToArray();

            Task.WaitAll(tasks);
            sw.Stop();
            sequentialMs = sw.ElapsedMilliseconds;
        }

        // Parallel with high contention
        var parDir = Path.Combine(m_testDir, "contention_par");
        long parallelMs;
        using (var store = new StoreLsm(parDir, options))
        {
            await using var writer = new LsmParallelWriter(store, bufferSizeThreshold: 8 * 1024);

            var sw = Stopwatch.StartNew();

            var tasks = Enumerable.Range(0, threads).Select(threadId => Task.Run(async () =>
            {
                for (int i = 0; i < entriesPerThread; i++)
                {
                    writer.Put(
                        ToBytes($"t{threadId}_key_{i:D8}"), 
                        ToBytes($"value_{threadId}_{i:D8}"));
                }
                await writer.FlushCurrentBufferAsync();
            })).ToArray();

            await Task.WhenAll(tasks);
            sw.Stop();
            parallelMs = sw.ElapsedMilliseconds;
        }

        // Report results
        TestContext.WriteLine("=== High Contention Comparison ({threads} threads) ===");
        TestContext.WriteLine($"Total entries: {totalEntries}");
        TestContext.WriteLine($"Sequential: {sequentialMs}ms ({totalEntries * 1000.0 / sequentialMs:F0} ops/sec)");
        TestContext.WriteLine($"Parallel:   {parallelMs}ms ({totalEntries * 1000.0 / parallelMs:F0} ops/sec)");

        if (parallelMs < sequentialMs)
        {
            var improvement = (double)sequentialMs / parallelMs;
            TestContext.WriteLine($"Improvement: {improvement:F2}x faster");
        }

        Assert.Pass("High contention comparison completed");
    }

    #endregion

    #region Buffer Size Impact Tests

    [Test]
    [Category("Performance")]
    public async Task BufferSizeImpactTest()
    {
        const int threads = 4;
        const int entriesPerThread = 2_000;
        const int totalEntries = threads * entriesPerThread;
        
        var bufferSizes = new[] { 4 * 1024, 16 * 1024, 64 * 1024, 256 * 1024 };
        var results = new List<(int BufferSize, long Ms, long BuffersSubmitted)>();

        var options = new LsmOptions 
        { 
            EnableWal = false, 
            MemTableSizeLimit = 1024 * 1024,
            Level0CompactionTrigger = 100
        };

        foreach (var bufferSize in bufferSizes)
        {
            var dir = Path.Combine(m_testDir, $"buffer_{bufferSize}");
            
            using var store = new StoreLsm(dir, options);
            await using var writer = new LsmParallelWriter(store, bufferSizeThreshold: bufferSize);

            var sw = Stopwatch.StartNew();

            var tasks = Enumerable.Range(0, threads).Select(threadId => Task.Run(async () =>
            {
                for (int i = 0; i < entriesPerThread; i++)
                {
                    writer.Put(
                        ToBytes($"t{threadId}_key_{i:D8}"), 
                        ToBytes($"value_{threadId}_{i:D8}"));
                }
                await writer.FlushCurrentBufferAsync();
            })).ToArray();

            await Task.WhenAll(tasks);
            sw.Stop();

            results.Add((bufferSize, sw.ElapsedMilliseconds, writer.BuffersSubmitted));
        }

        // Report results
        TestContext.WriteLine("=== Buffer Size Impact ===");
        TestContext.WriteLine($"Total entries: {totalEntries}, Threads: {threads}");
        TestContext.WriteLine("");
        TestContext.WriteLine("Buffer Size | Time (ms) | Ops/sec | Buffers Submitted");
        TestContext.WriteLine("------------|-----------|---------|------------------");
        
        foreach (var (bufferSize, ms, buffers) in results)
        {
            var opsPerSec = totalEntries * 1000.0 / ms;
            TestContext.WriteLine($"{bufferSize / 1024,7} KB | {ms,9} | {opsPerSec,7:F0} | {buffers,17}");
        }

        Assert.Pass("Buffer size impact test completed");
    }

    #endregion

    #region Helpers

    private static byte[] ToBytes(string s) => TextEncoding.UTF8.GetBytes(s);

    #endregion
}

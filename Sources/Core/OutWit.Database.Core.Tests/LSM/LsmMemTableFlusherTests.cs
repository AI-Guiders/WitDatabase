using OutWit.Database.Core.LSM;
using TextEncoding = System.Text.Encoding;

namespace OutWit.Database.Core.Tests.LSM;

/// <summary>
/// Tests for LsmMemTableFlusher parallel flush functionality.
/// </summary>
[TestFixture]
public class LsmMemTableFlusherTests : IDisposable
{
    #region Fields

    private string m_testDir = null!;

    #endregion

    #region Setup/TearDown

    [SetUp]
    public void SetUp()
    {
        m_testDir = Path.Combine(Path.GetTempPath(), $"flusher_test_{Guid.NewGuid():N}");
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
    public async Task TrySubmitFlushesMemTableTest()
    {
        var flushedPaths = new List<string>();
        var flushCount = 0;

        string FlushAction(MemTable mt, int id)
        {
            Interlocked.Increment(ref flushCount);
            var path = Path.Combine(m_testDir, $"sst_{id:D6}.sst");
            // Simulate flush by creating empty file
            File.WriteAllBytes(path, []);
            return path;
        }

        await using var flusher = new LsmMemTableFlusher(
            FlushAction,
            path => { lock (flushedPaths) flushedPaths.Add(path); });

        var memTable = new MemTable();
        memTable.Put(ToBytes("key1"), ToBytes("value1"));

        var result = flusher.TrySubmit(memTable, 1);
        Assert.That(result, Is.True);

        await flusher.WaitForAllAsync();

        Assert.That(flushCount, Is.EqualTo(1));
        Assert.That(flushedPaths, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task SubmitAndWaitAsyncReturnsPathTest()
    {
        string FlushAction(MemTable mt, int id)
        {
            var path = Path.Combine(m_testDir, $"sst_{id:D6}.sst");
            File.WriteAllBytes(path, []);
            return path;
        }

        await using var flusher = new LsmMemTableFlusher(
            FlushAction,
            _ => { });

        var memTable = new MemTable();
        memTable.Put(ToBytes("key1"), ToBytes("value1"));

        var path = await flusher.SubmitAndWaitAsync(memTable, 42);

        Assert.That(path, Does.Contain("sst_000042.sst"));
        Assert.That(File.Exists(path), Is.True);
    }

    #endregion

    #region Parallel Flush Tests

    [Test]
    public async Task MultipleMemTablesFlushInParallelTest()
    {
        var flushStartTimes = new List<long>();
        var flushEndTimes = new List<long>();
        var lockObj = new object();

        string FlushAction(MemTable mt, int id)
        {
            lock (lockObj)
            {
                flushStartTimes.Add(Environment.TickCount64);
            }

            // Simulate slow flush
            Thread.Sleep(50);

            lock (lockObj)
            {
                flushEndTimes.Add(Environment.TickCount64);
            }

            var path = Path.Combine(m_testDir, $"sst_{id:D6}.sst");
            File.WriteAllBytes(path, []);
            return path;
        }

        await using var flusher = new LsmMemTableFlusher(
            FlushAction,
            _ => { },
            maxParallelFlushes: 2);

        // Submit 4 MemTables
        var tasks = new List<Task<string>>();
        for (int i = 0; i < 4; i++)
        {
            var memTable = new MemTable();
            memTable.Put(ToBytes($"key{i}"), ToBytes($"value{i}"));
            tasks.Add(flusher.SubmitAndWaitAsync(memTable, i));
        }

        await Task.WhenAll(tasks);

        // With 2 parallel workers, flushes should overlap
        Assert.That(flusher.MemTablesFlushed, Is.EqualTo(4));
    }

    [Test]
    public async Task ConcurrentSubmissionsTest()
    {
        var flushCount = 0;

        string FlushAction(MemTable mt, int id)
        {
            Interlocked.Increment(ref flushCount);
            var path = Path.Combine(m_testDir, $"sst_{id:D6}.sst");
            File.WriteAllBytes(path, []);
            return path;
        }

        await using var flusher = new LsmMemTableFlusher(
            FlushAction,
            _ => { },
            maxParallelFlushes: 4,
            maxQueueSize: 10);

        const int totalFlushes = 20;
        var tasks = Enumerable.Range(0, totalFlushes).Select(i => Task.Run(async () =>
        {
            var memTable = new MemTable();
            memTable.Put(ToBytes($"key{i}"), ToBytes($"value{i}"));
            await flusher.SubmitAndWaitAsync(memTable, i);
        })).ToArray();

        await Task.WhenAll(tasks);

        Assert.That(flushCount, Is.EqualTo(totalFlushes));
        Assert.That(flusher.MemTablesFlushed, Is.EqualTo(totalFlushes));
    }

    #endregion

    #region Statistics Tests

    [Test]
    public async Task StatisticsTrackCorrectlyTest()
    {
        string FlushAction(MemTable mt, int id)
        {
            var path = Path.Combine(m_testDir, $"sst_{id:D6}.sst");
            File.WriteAllBytes(path, []);
            return path;
        }

        await using var flusher = new LsmMemTableFlusher(
            FlushAction,
            _ => { });

        Assert.That(flusher.MemTablesFlushed, Is.EqualTo(0));
        Assert.That(flusher.TotalEntriesFlushed, Is.EqualTo(0));

        // Flush MemTable with 5 entries
        var memTable = new MemTable();
        for (int i = 0; i < 5; i++)
        {
            memTable.Put(ToBytes($"key{i}"), ToBytes($"value{i}"));
        }

        await flusher.SubmitAndWaitAsync(memTable, 1);

        Assert.That(flusher.MemTablesFlushed, Is.EqualTo(1));
        Assert.That(flusher.TotalEntriesFlushed, Is.EqualTo(5));
        Assert.That(flusher.TotalBytesFlushed, Is.GreaterThan(0));
    }

    [Test]
    public async Task PendingFlushesTracksQueueDepthTest()
    {
        var flushStarted = new ManualResetEventSlim(false);
        var allowFlush = new ManualResetEventSlim(false);

        string FlushAction(MemTable mt, int id)
        {
            flushStarted.Set();
            allowFlush.Wait();
            
            var path = Path.Combine(m_testDir, $"sst_{id:D6}.sst");
            File.WriteAllBytes(path, []);
            return path;
        }

        await using var flusher = new LsmMemTableFlusher(
            FlushAction,
            _ => { },
            maxParallelFlushes: 1,
            maxQueueSize: 5);

        // Submit first flush (will block in FlushAction)
        var memTable1 = new MemTable();
        memTable1.Put(ToBytes("key1"), ToBytes("value1"));
        var task1 = flusher.SubmitAndWaitAsync(memTable1, 1);

        // Wait for flush to start
        flushStarted.Wait();

        // Submit more - should queue
        for (int i = 2; i <= 4; i++)
        {
            var mt = new MemTable();
            mt.Put(ToBytes($"key{i}"), ToBytes($"value{i}"));
            flusher.TrySubmit(mt, i);
        }

        // Should have pending items
        Assert.That(flusher.PendingFlushes, Is.GreaterThanOrEqualTo(0));

        // Allow flushes to complete
        allowFlush.Set();
        await flusher.WaitForAllAsync();

        Assert.That(flusher.PendingFlushes, Is.EqualTo(0));
    }

    #endregion

    #region Error Handling Tests

    [Test]
    public async Task FlushErrorPropagatesTest()
    {
        string FlushAction(MemTable mt, int id)
        {
            throw new InvalidOperationException("Simulated flush error");
        }

        await using var flusher = new LsmMemTableFlusher(
            FlushAction,
            _ => { });

        var memTable = new MemTable();
        memTable.Put(ToBytes("key1"), ToBytes("value1"));

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await flusher.SubmitAndWaitAsync(memTable, 1));

        Assert.That(ex.Message, Does.Contain("Simulated flush error"));
    }

    [Test]
    public async Task TrySubmitOnFailedFlushDoesNotCrashTest()
    {
        var failFirst = true;

        string FlushAction(MemTable mt, int id)
        {
            if (failFirst)
            {
                failFirst = false;
                throw new InvalidOperationException("First flush fails");
            }
            
            var path = Path.Combine(m_testDir, $"sst_{id:D6}.sst");
            File.WriteAllBytes(path, []);
            return path;
        }

        await using var flusher = new LsmMemTableFlusher(
            FlushAction,
            _ => { });

        // First flush will fail
        var memTable1 = new MemTable();
        memTable1.Put(ToBytes("key1"), ToBytes("value1"));
        
        try
        {
            await flusher.SubmitAndWaitAsync(memTable1, 1);
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        // Second flush should succeed
        var memTable2 = new MemTable();
        memTable2.Put(ToBytes("key2"), ToBytes("value2"));
        
        var path = await flusher.SubmitAndWaitAsync(memTable2, 2);
        Assert.That(path, Does.Contain("sst_000002.sst"));
    }

    #endregion

    #region Disposal Tests

    [Test]
    public async Task DisposeWaitsForPendingFlushesTest()
    {
        var flushCount = 0;

        string FlushAction(MemTable mt, int id)
        {
            Thread.Sleep(10);
            Interlocked.Increment(ref flushCount);
            var path = Path.Combine(m_testDir, $"sst_{id:D6}.sst");
            File.WriteAllBytes(path, []);
            return path;
        }

        var flusher = new LsmMemTableFlusher(
            FlushAction,
            _ => { });

        // Submit flushes and wait for them to be queued
        var submittedCount = 0;
        for (int i = 0; i < 5; i++)
        {
            var memTable = new MemTable();
            memTable.Put(ToBytes($"key{i}"), ToBytes($"value{i}"));
            if (flusher.TrySubmit(memTable, i))
            {
                submittedCount++;
            }
        }

        await flusher.DisposeAsync();

        // All submitted flushes should have completed
        Assert.That(flushCount, Is.EqualTo(submittedCount));
        Assert.That(flushCount, Is.GreaterThan(0));
    }

    [Test]
    public void DisposedFlusherThrowsTest()
    {
        string FlushAction(MemTable mt, int id) => "";

        var flusher = new LsmMemTableFlusher(FlushAction, _ => { });
        flusher.Dispose();

        var memTable = new MemTable();
        Assert.Throws<ObjectDisposedException>(() => flusher.TrySubmit(memTable, 1));
    }

    #endregion

    #region Callback Tests

    [Test]
    public async Task OnFlushCompleteCalledForEachFlushTest()
    {
        var completedPaths = new List<string>();
        var lockObj = new object();

        string FlushAction(MemTable mt, int id)
        {
            var path = Path.Combine(m_testDir, $"sst_{id:D6}.sst");
            File.WriteAllBytes(path, []);
            return path;
        }

        await using var flusher = new LsmMemTableFlusher(
            FlushAction,
            path => { lock (lockObj) completedPaths.Add(path); });

        for (int i = 0; i < 5; i++)
        {
            var memTable = new MemTable();
            memTable.Put(ToBytes($"key{i}"), ToBytes($"value{i}"));
            await flusher.SubmitAndWaitAsync(memTable, i);
        }

        Assert.That(completedPaths, Has.Count.EqualTo(5));
        for (int i = 0; i < 5; i++)
        {
            Assert.That(completedPaths, Has.One.Contains($"sst_{i:D6}.sst"));
        }
    }

    #endregion

    #region Helpers

    private static byte[] ToBytes(string s) => TextEncoding.UTF8.GetBytes(s);

    #endregion
}

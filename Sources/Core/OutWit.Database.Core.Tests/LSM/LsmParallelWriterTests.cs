using OutWit.Database.Core.LSM;
using OutWit.Database.Core.Stores;
using TextEncoding = System.Text.Encoding;

namespace OutWit.Database.Core.Tests.LSM;

/// <summary>
/// Tests for LsmParallelWriter concurrent write functionality.
/// </summary>
[TestFixture]
public class LsmParallelWriterTests : IDisposable
{
    #region Fields

    private string m_testDir = null!;

    #endregion

    #region Setup/TearDown

    [SetUp]
    public void SetUp()
    {
        m_testDir = Path.Combine(Path.GetTempPath(), $"lsm_parallel_test_{Guid.NewGuid():N}");
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
    public async Task PutWritesDataToStoreTest()
    {
        var dir = Path.Combine(m_testDir, "basic_put");
        var options = new LsmOptions { EnableWal = false, MemTableSizeLimit = 1024 * 1024 };
        
        using var store = new StoreLsm(dir, options);
        await using var writer = new LsmParallelWriter(store);

        writer.Put(ToBytes("key1"), ToBytes("value1"));
        await writer.FlushAllAsync();

        var result = store.Get(ToBytes("key1"));
        Assert.That(result, Is.EqualTo(ToBytes("value1")));
    }

    [Test]
    public async Task DeleteRemovesDataFromStoreTest()
    {
        var dir = Path.Combine(m_testDir, "basic_delete");
        var options = new LsmOptions { EnableWal = false, MemTableSizeLimit = 1024 * 1024 };
        
        using var store = new StoreLsm(dir, options);
        
        // First put directly
        store.Put(ToBytes("key1"), ToBytes("value1"));
        
        await using var writer = new LsmParallelWriter(store);
        writer.Delete(ToBytes("key1"));
        await writer.FlushAllAsync();

        var result = store.Get(ToBytes("key1"));
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task PutAsyncWritesDataTest()
    {
        var dir = Path.Combine(m_testDir, "async_put");
        var options = new LsmOptions { EnableWal = false, MemTableSizeLimit = 1024 * 1024 };
        
        using var store = new StoreLsm(dir, options);
        await using var writer = new LsmParallelWriter(store);

        await writer.PutAsync(ToBytes("key1"), ToBytes("value1"));
        await writer.FlushAllAsync();

        var result = store.Get(ToBytes("key1"));
        Assert.That(result, Is.EqualTo(ToBytes("value1")));
    }

    #endregion

    #region Concurrent Write Tests

    [Test]
    public async Task MultipleThreadsWriteSafelyTest()
    {
        var dir = Path.Combine(m_testDir, "concurrent");
        var options = new LsmOptions 
        { 
            EnableWal = false, 
            MemTableSizeLimit = 1024 * 1024,
            Level0CompactionTrigger = 100
        };
        
        using var store = new StoreLsm(dir, options);
        await using var writer = new LsmParallelWriter(store, bufferSizeThreshold: 1024);

        const int threads = 4;
        const int entriesPerThread = 100;

        var tasks = Enumerable.Range(0, threads).Select(threadId => Task.Run(async () =>
        {
            for (int i = 0; i < entriesPerThread; i++)
            {
                writer.Put(
                    ToBytes($"t{threadId}_k{i:D5}"),
                    ToBytes($"value_{threadId}_{i}"));
            }
            // Flush this thread's buffer and wait for completion
            await writer.FlushCurrentBufferAsync();
        })).ToArray();

        await Task.WhenAll(tasks);

        // Verify all data was written
        for (int t = 0; t < threads; t++)
        {
            for (int i = 0; i < entriesPerThread; i++)
            {
                var result = store.Get(ToBytes($"t{t}_k{i:D5}"));
                Assert.That(result, Is.Not.Null, $"Missing key t{t}_k{i:D5}");
            }
        }
    }

    [Test]
    public async Task ConcurrentPutsAndDeletesTest()
    {
        var dir = Path.Combine(m_testDir, "concurrent_mixed");
        var options = new LsmOptions 
        { 
            EnableWal = false, 
            MemTableSizeLimit = 1024 * 1024,
            Level0CompactionTrigger = 100
        };
        
        using var store = new StoreLsm(dir, options);
        await using var writer = new LsmParallelWriter(store);

        const int threads = 4;
        const int operations = 50;

        // First, put some data
        for (int i = 0; i < 100; i++)
        {
            store.Put(ToBytes($"existing_{i:D5}"), ToBytes($"value_{i}"));
        }

        var tasks = Enumerable.Range(0, threads).Select(threadId => Task.Run(async () =>
        {
            for (int i = 0; i < operations; i++)
            {
                if (i % 2 == 0)
                {
                    writer.Put(ToBytes($"new_{threadId}_{i:D5}"), ToBytes($"new_value"));
                }
                else
                {
                    writer.Delete(ToBytes($"existing_{i}"));
                }
            }
            await writer.FlushCurrentBufferAsync();
        })).ToArray();

        await Task.WhenAll(tasks);

        // Verify new keys exist
        for (int t = 0; t < threads; t++)
        {
            for (int i = 0; i < operations; i += 2)
            {
                var result = store.Get(ToBytes($"new_{t}_{i:D5}"));
                Assert.That(result, Is.Not.Null, $"Missing new key new_{t}_{i:D5}");
            }
        }
    }

    [Test]
    public async Task HighContententionWritesTest()
    {
        var dir = Path.Combine(m_testDir, "high_contention");
        var options = new LsmOptions 
        { 
            EnableWal = false, 
            MemTableSizeLimit = 1024 * 1024,
            Level0CompactionTrigger = 100
        };
        
        using var store = new StoreLsm(dir, options);
        await using var writer = new LsmParallelWriter(store, bufferSizeThreshold: 512);

        const int threads = 8;
        const int entriesPerThread = 200;
        var successCount = 0;

        var tasks = Enumerable.Range(0, threads).Select(threadId => Task.Run(async () =>
        {
            for (int i = 0; i < entriesPerThread; i++)
            {
                writer.Put(
                    ToBytes($"key_{threadId}_{i:D5}"),
                    ToBytes($"value_{threadId}_{i}"));
                Interlocked.Increment(ref successCount);
            }
            await writer.FlushCurrentBufferAsync();
        })).ToArray();

        await Task.WhenAll(tasks);

        Assert.That(successCount, Is.EqualTo(threads * entriesPerThread));
        Assert.That(writer.EntriesMerged, Is.EqualTo(threads * entriesPerThread));
    }

    #endregion

    #region Auto-Flush Tests

    [Test]
    public async Task AutoFlushTriggersOnThresholdTest()
    {
        var dir = Path.Combine(m_testDir, "auto_flush");
        var options = new LsmOptions { EnableWal = false, MemTableSizeLimit = 1024 * 1024 };
        
        using var store = new StoreLsm(dir, options);
        await using var writer = new LsmParallelWriter(store, bufferSizeThreshold: 100);

        // Write enough data to trigger auto-flush
        for (int i = 0; i < 50; i++)
        {
            writer.Put(ToBytes($"key{i:D10}"), ToBytes($"value{i:D10}"));
        }

        // Wait for background merge
        await Task.Delay(100);

        Assert.That(writer.BuffersSubmitted, Is.GreaterThan(0));
    }

    #endregion

    #region Statistics Tests

    [Test]
    public async Task StatisticsTrackCorrectlyTest()
    {
        var dir = Path.Combine(m_testDir, "stats");
        var options = new LsmOptions { EnableWal = false, MemTableSizeLimit = 1024 * 1024 };
        
        using var store = new StoreLsm(dir, options);
        await using var writer = new LsmParallelWriter(store, bufferSizeThreshold: 1024);

        Assert.That(writer.BuffersSubmitted, Is.EqualTo(0));
        Assert.That(writer.EntriesMerged, Is.EqualTo(0));
        Assert.That(writer.MergeOperations, Is.EqualTo(0));

        for (int i = 0; i < 10; i++)
        {
            writer.Put(ToBytes($"key{i}"), ToBytes($"value{i}"));
        }
        writer.FlushCurrentBuffer();

        // Wait for merge
        await Task.Delay(100);

        Assert.That(writer.BuffersSubmitted, Is.GreaterThan(0));
        Assert.That(writer.EntriesMerged, Is.EqualTo(10));
        Assert.That(writer.MergeOperations, Is.GreaterThan(0));
    }

    [Test]
    public async Task AverageEntriesPerMergeCalculatesCorrectlyTest()
    {
        var dir = Path.Combine(m_testDir, "avg_entries");
        var options = new LsmOptions { EnableWal = false, MemTableSizeLimit = 1024 * 1024 };
        
        using var store = new StoreLsm(dir, options);
        await using var writer = new LsmParallelWriter(store, bufferSizeThreshold: 10000);

        // Write entries in batches
        for (int batch = 0; batch < 3; batch++)
        {
            for (int i = 0; i < 20; i++)
            {
                writer.Put(ToBytes($"b{batch}_k{i}"), ToBytes($"v{i}"));
            }
            writer.FlushCurrentBuffer();
        }

        await Task.Delay(100);

        Assert.That(writer.EntriesMerged, Is.EqualTo(60));
        Assert.That(writer.AverageEntriesPerMerge, Is.GreaterThan(0));
    }

    #endregion

    #region Disposal Tests

    [Test]
    public async Task DisposeFlushesRemainingDataTest()
    {
        var dir = Path.Combine(m_testDir, "dispose");
        var options = new LsmOptions { EnableWal = false, MemTableSizeLimit = 1024 * 1024 };
        
        using var store = new StoreLsm(dir, options);
        
        var writer = new LsmParallelWriter(store);
        writer.Put(ToBytes("key1"), ToBytes("value1"));
        writer.FlushCurrentBuffer();
        await writer.DisposeAsync();

        // Data should be in store
        var result = store.Get(ToBytes("key1"));
        Assert.That(result, Is.EqualTo(ToBytes("value1")));
    }

    [Test]
    public void DisposedWriterThrowsTest()
    {
        var dir = Path.Combine(m_testDir, "disposed");
        var options = new LsmOptions { EnableWal = false, MemTableSizeLimit = 1024 * 1024 };
        
        using var store = new StoreLsm(dir, options);
        var writer = new LsmParallelWriter(store);
        writer.Dispose();

        Assert.Throws<ObjectDisposedException>(() => writer.Put(ToBytes("key"), ToBytes("value")));
    }

    #endregion

    #region Integration Tests

    [Test]
    public async Task IntegrationWithWalEnabledTest()
    {
        var dir = Path.Combine(m_testDir, "wal_integration");
        var options = new LsmOptions { EnableWal = true, SyncWrites = false, MemTableSizeLimit = 1024 * 1024 };
        
        using var store = new StoreLsm(dir, options);
        await using var writer = new LsmParallelWriter(store);

        for (int i = 0; i < 50; i++)
        {
            writer.Put(ToBytes($"key{i:D5}"), ToBytes($"value{i}"));
        }
        await writer.FlushAllAsync();

        // Verify data
        for (int i = 0; i < 50; i++)
        {
            var result = store.Get(ToBytes($"key{i:D5}"));
            Assert.That(result, Is.Not.Null, $"Key {i} not found");
        }
    }

    [Test]
    public async Task IntegrationWithMemTableFlushTest()
    {
        var dir = Path.Combine(m_testDir, "memtable_flush");
        var options = new LsmOptions 
        { 
            EnableWal = false, 
            MemTableSizeLimit = 1000,
            Level0CompactionTrigger = 100
        };
        
        using var store = new StoreLsm(dir, options);
        await using var writer = new LsmParallelWriter(store, bufferSizeThreshold: 500);

        // Write enough to trigger multiple MemTable flushes
        for (int i = 0; i < 200; i++)
        {
            writer.Put(ToBytes($"key{i:D5}"), ToBytes($"value{i:D20}"));
        }
        await writer.FlushAllAsync();

        // Force store flush
        store.Flush();

        // Verify data
        for (int i = 0; i < 200; i++)
        {
            var result = store.Get(ToBytes($"key{i:D5}"));
            Assert.That(result, Is.Not.Null, $"Key {i} not found");
        }
    }

    #endregion

    #region Helpers

    private static byte[] ToBytes(string s) => TextEncoding.UTF8.GetBytes(s);

    #endregion
}

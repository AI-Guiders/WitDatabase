using OutWit.Database.Core.Cache;
using OutWit.Database.Core.Storage;

namespace OutWit.Database.Core.Tests.Cache;

[TestFixture]
public class PageCacheAsyncTests
{
    #region GetPageAsync Tests

    [Test]
    public async Task GetPageAsyncLoadsFromStorageTest()
    {
        using var storage = new StorageMemory(initialPageCount: 10);
        
        // Write test data to storage
        byte[] testData = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
        testData[0] = 0xAB;
        testData[100] = 0xCD;
        storage.WritePage(5, testData);
        
        using var cache = new PageCacheShardedClock(storage, 100);
        
        var page = await cache.GetPageAsync(5);
        
        Assert.That(page.ReadOnlyData[0], Is.EqualTo(0xAB));
        Assert.That(page.ReadOnlyData[100], Is.EqualTo(0xCD));
        
        cache.ReleasePage(5);
    }

    [Test]
    public async Task GetPageAsyncCachesPageTest()
    {
        using var storage = new StorageMemory(initialPageCount: 10);
        using var cache = new PageCacheShardedClock(storage, 100);
        
        var page1 = await cache.GetPageAsync(5);
        page1.Data[0] = 0xFF;
        cache.ReleasePage(5);
        
        var page2 = await cache.GetPageAsync(5);
        
        Assert.That(page2.Data[0], Is.EqualTo(0xFF));
        Assert.That(cache.Count, Is.EqualTo(1));
        
        cache.ReleasePage(5);
    }

    [Test]
    public async Task GetPageAsyncIncrementsReferenceCountTest()
    {
        using var storage = new StorageMemory(initialPageCount: 10);
        using var cache = new PageCacheShardedClock(storage, 100);
        
        var page1 = await cache.GetPageAsync(5);
        var page2 = await cache.GetPageAsync(5);
        var page3 = await cache.GetPageAsync(5);
        
        // All should be same page
        Assert.That(page1, Is.SameAs(page2));
        Assert.That(page2, Is.SameAs(page3));
        
        cache.ReleasePage(5);
        cache.ReleasePage(5);
        cache.ReleasePage(5);
    }

    [Test]
    public async Task GetPageAsyncWithCancellationTest()
    {
        using var storage = new StorageMemory(initialPageCount: 10);
        using var cache = new PageCacheShardedClock(storage, 100);
        
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        
        // CatchAsync catches base type and derived types
        var ex = Assert.CatchAsync<OperationCanceledException>(async () => 
            await cache.GetPageAsync(5, cts.Token));
        
        Assert.That(ex, Is.Not.Null);
    }

    #endregion

    #region CreatePageAsync Tests

    [Test]
    public async Task CreatePageAsyncInitializesNewPageTest()
    {
        using var storage = new StorageMemory(initialPageCount: 10);
        using var cache = new PageCacheShardedClock(storage, 100);
        
        var page = await cache.CreatePageAsync(5);
        
        Assert.That(page.IsDirty, Is.True);
        Assert.That(page.Data[0], Is.EqualTo(0));
        Assert.That(cache.Count, Is.EqualTo(1));
        
        cache.ReleasePage(5);
    }

    [Test]
    public async Task CreatePageAsyncDuplicateThrowsTest()
    {
        using var storage = new StorageMemory(initialPageCount: 10);
        using var cache = new PageCacheShardedClock(storage, 100);
        
        await cache.CreatePageAsync(5);
        cache.ReleasePage(5);
        
        Assert.ThrowsAsync<InvalidOperationException>(async () => 
            await cache.CreatePageAsync(5));
    }

    [Test]
    public async Task CreatePageAsyncWithCancellationTest()
    {
        using var storage = new StorageMemory(initialPageCount: 10);
        using var cache = new PageCacheShardedClock(storage, 100);
        
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        
        // CatchAsync catches base type and derived types
        var ex = Assert.CatchAsync<OperationCanceledException>(async () => 
            await cache.CreatePageAsync(5, cts.Token));
        
        Assert.That(ex, Is.Not.Null);
    }

    #endregion

    #region EvictAsync Tests

    [Test]
    public async Task EvictAsyncRemovesPageTest()
    {
        using var storage = new StorageMemory(initialPageCount: 10);
        using var cache = new PageCacheShardedClock(storage, 100);
        
        await cache.CreatePageAsync(5);
        cache.ReleasePage(5);
        
        Assert.That(cache.Count, Is.EqualTo(1));
        
        await cache.EvictAsync(5);
        
        Assert.That(cache.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task EvictAsyncPinnedPageThrowsTest()
    {
        using var storage = new StorageMemory(initialPageCount: 10);
        using var cache = new PageCacheShardedClock(storage, 100);
        
        await cache.CreatePageAsync(5); // Don't release
        
        Assert.ThrowsAsync<InvalidOperationException>(async () => 
            await cache.EvictAsync(5));
        
        cache.ReleasePage(5);
    }

    [Test]
    public async Task EvictAsyncFlushesDirtyPageTest()
    {
        using var storage = new StorageMemory(initialPageCount: 10);
        using var cache = new PageCacheShardedClock(storage, 100);
        
        var page = await cache.CreatePageAsync(5);
        page.Data[0] = 0xEF;
        cache.ReleasePage(5);
        
        await cache.EvictAsync(5);
        
        // Should have been flushed before eviction
        byte[] buffer = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
        storage.ReadPage(5, buffer);
        
        Assert.That(buffer[0], Is.EqualTo(0xEF));
    }

    #endregion

    #region FlushAllAsync Tests

    [Test]
    public async Task FlushAllAsyncWritesDirtyPagesTest()
    {
        using var storage = new StorageMemory(initialPageCount: 10);
        using var cache = new PageCacheShardedClock(storage, 100);
        
        var page = await cache.CreatePageAsync(5);
        page.Data[0] = 0xAB;
        cache.ReleasePage(5);
        
        await cache.FlushAllAsync();
        
        // Verify written to storage
        byte[] buffer = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
        storage.ReadPage(5, buffer);
        
        Assert.That(buffer[0], Is.EqualTo(0xAB));
    }

    [Test]
    public async Task FlushAllAsyncWithCancellationTest()
    {
        using var storage = new StorageMemory(initialPageCount: 10);
        using var cache = new PageCacheShardedClock(storage, 100);
        
        var page = await cache.CreatePageAsync(5);
        page.Data[0] = 0xAB;
        cache.ReleasePage(5);
        
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        
        // CatchAsync catches base type and derived types
        var ex = Assert.CatchAsync<OperationCanceledException>(async () => 
            await cache.FlushAllAsync(cts.Token));
        
        Assert.That(ex, Is.Not.Null);
    }

    #endregion

    #region Concurrent Async Tests

    [Test]
    public async Task ConcurrentAsyncAccessTest()
    {
        using var storage = new StorageMemory(initialPageCount: 100);
        using var cache = new PageCacheShardedClock(storage, 100, shardCount: 8);
        
        // Pre-create pages
        for (int i = 0; i < 50; i++)
        {
            await cache.CreatePageAsync(i);
            cache.ReleasePage(i);
        }
        
        // Concurrent async access
        var tasks = new Task[10];
        for (int t = 0; t < 10; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(async () =>
            {
                for (int i = 0; i < 50; i++)
                {
                    int pageNum = (threadId + i) % 50;
                    var page = await cache.GetPageAsync(pageNum);
                    page.Data[threadId] = (byte)threadId;
                    cache.MarkDirty(pageNum);
                    cache.ReleasePage(pageNum);
                    
                    // Small delay to increase chance of interleaving
                    if (i % 10 == 0)
                        await Task.Yield();
                }
            });
        }
        
        await Task.WhenAll(tasks);
        
        // Should complete without deadlocks or exceptions
        Assert.Pass();
    }

    [Test]
    public async Task MixedSyncAndAsyncAccessTest()
    {
        using var storage = new StorageMemory(initialPageCount: 100);
        using var cache = new PageCacheShardedClock(storage, 100, shardCount: 4);
        
        // Pre-create some pages synchronously
        for (int i = 0; i < 20; i++)
        {
            cache.CreatePage(i);
            cache.ReleasePage(i);
        }
        
        // Mix sync and async operations
        var tasks = new List<Task>();
        
        for (int i = 0; i < 5; i++)
        {
            int idx = i;
            
            // Async task
            tasks.Add(Task.Run(async () =>
            {
                for (int j = 0; j < 20; j++)
                {
                    var page = await cache.GetPageAsync(j);
                    cache.ReleasePage(j);
                }
            }));
            
            // Sync task
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 20; j++)
                {
                    var page = cache.GetPage(j);
                    cache.ReleasePage(j);
                }
            }));
        }
        
        await Task.WhenAll(tasks);
        
        Assert.Pass();
    }

    #endregion

    #region LRU Cache Async Tests

    [Test]
    public async Task LruCacheGetPageAsyncTest()
    {
        using var storage = new StorageMemory(initialPageCount: 10);
        using var cache = new PageCacheLru(storage, 100);
        
        // Write test data
        byte[] testData = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
        testData[0] = 0x42;
        storage.WritePage(3, testData);
        
        var page = await cache.GetPageAsync(3);
        
        Assert.That(page.ReadOnlyData[0], Is.EqualTo(0x42));
        
        cache.ReleasePage(3);
    }

    [Test]
    public async Task LruCacheCreatePageAsyncTest()
    {
        using var storage = new StorageMemory(initialPageCount: 10);
        using var cache = new PageCacheLru(storage, 100);
        
        var page = await cache.CreatePageAsync(7);
        
        Assert.That(page.IsDirty, Is.True);
        Assert.That(cache.Count, Is.EqualTo(1));
        
        cache.ReleasePage(7);
    }

    [Test]
    public async Task LruCacheEvictAsyncTest()
    {
        using var storage = new StorageMemory(initialPageCount: 10);
        using var cache = new PageCacheLru(storage, 100);
        
        var page = await cache.CreatePageAsync(5);
        page.Data[0] = 0xAA;
        cache.ReleasePage(5);
        
        await cache.EvictAsync(5);
        
        Assert.That(cache.Count, Is.EqualTo(0));
        
        // Verify data was flushed
        byte[] buffer = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
        storage.ReadPage(5, buffer);
        Assert.That(buffer[0], Is.EqualTo(0xAA));
    }

    [Test]
    public async Task LruCacheFlushAllAsyncTest()
    {
        using var storage = new StorageMemory(initialPageCount: 10);
        using var cache = new PageCacheLru(storage, 100);
        
        var page = await cache.CreatePageAsync(5);
        page.Data[0] = 0xBB;
        cache.ReleasePage(5);
        
        await cache.FlushAllAsync();
        
        byte[] buffer = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
        storage.ReadPage(5, buffer);
        Assert.That(buffer[0], Is.EqualTo(0xBB));
    }

    #endregion

    #region Eviction Under Pressure Async Tests

    [Test]
    public async Task AsyncEvictionUnderPressureTest()
    {
        using var storage = new StorageMemory(initialPageCount: 100);
        // Small cache to force evictions
        using var cache = new PageCacheShardedClock(storage, 8, shardCount: 1);
        
        // Create pages until we need to evict
        for (int i = 0; i < 8; i++)
        {
            var page = await cache.CreatePageAsync(i);
            page.Data[0] = (byte)i;
            cache.ReleasePage(i);
        }
        
        Assert.That(cache.Count, Is.EqualTo(8));
        
        // Adding more should trigger eviction
        for (int i = 8; i < 16; i++)
        {
            var page = await cache.CreatePageAsync(i);
            page.Data[0] = (byte)i;
            cache.ReleasePage(i);
        }
        
        // Should still have max 8 pages
        Assert.That(cache.Count, Is.LessThanOrEqualTo(8));
        
        // Flush and verify evicted pages were written
        await cache.FlushAllAsync();
        
        // Check some evicted pages were written to storage
        byte[] buffer = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
        storage.ReadPage(0, buffer);
        Assert.That(buffer[0], Is.EqualTo(0));
    }

    #endregion
}

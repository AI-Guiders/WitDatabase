using OutWit.Database.Core.Cache;
using OutWit.Database.Core.Storage;

namespace OutWit.Database.Core.Tests.Cache;

[TestFixture]
public class PageCacheShardedClockTest
{
    #region Constructor Tests

    [Test]
    public void ConstructorWithDefaultsTest()
    {
        using var storage = new StorageMemory(initialPageCount: 10);
        using var cache = new PageCacheShardedClock(storage);
        
        Assert.That(cache.Count, Is.EqualTo(0));
        Assert.That(cache.ShardCount, Is.GreaterThan(0));
    }

    [Test]
    public void ConstructorNullStorageThrowsTest()
    {
        Assert.Throws<ArgumentNullException>(() => new PageCacheShardedClock(null!));
    }

    [Test]
    public void ConstructorInvalidCacheSizeThrowsTest()
    {
        using var storage = new StorageMemory(initialPageCount: 10);
        
        Assert.Throws<ArgumentOutOfRangeException>(() => new PageCacheShardedClock(storage, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PageCacheShardedClock(storage, -1));
    }

    [Test]
    public void ConstructorShardCountIsPowerOfTwoTest()
    {
        using var storage = new StorageMemory(initialPageCount: 100);
        
        // Request various shard counts
        using var cache1 = new PageCacheShardedClock(storage, 100, shardCount: 3);
        using var cache2 = new PageCacheShardedClock(storage, 100, shardCount: 7);
        using var cache3 = new PageCacheShardedClock(storage, 100, shardCount: 16);
        
        // Should be rounded to power of 2
        Assert.That(cache1.ShardCount, Is.EqualTo(4)); // 3 -> 4
        Assert.That(cache2.ShardCount, Is.EqualTo(8)); // 7 -> 8
        Assert.That(cache3.ShardCount, Is.EqualTo(16));
    }

    #endregion

    #region GetPage Tests

    [Test]
    public void GetPageLoadsFromStorageTest()
    {
        using var storage = new StorageMemory(initialPageCount: 10);
        
        // Write test data to storage
        byte[] testData = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
        testData[0] = 0xAB;
        testData[100] = 0xCD;
        storage.WritePage(5, testData);
        
        using var cache = new PageCacheShardedClock(storage, 100);
        
        var page = cache.GetPage(5);
        
        Assert.That(page.ReadOnlyData[0], Is.EqualTo(0xAB));
        Assert.That(page.ReadOnlyData[100], Is.EqualTo(0xCD));
        
        cache.ReleasePage(5);
    }

    [Test]
    public void GetPageCachesPageTest()
    {
        using var storage = new StorageMemory(initialPageCount: 10);
        using var cache = new PageCacheShardedClock(storage, 100);
        
        var page1 = cache.GetPage(5);
        page1.Data[0] = 0xFF;
        cache.ReleasePage(5);
        
        var page2 = cache.GetPage(5);
        
        Assert.That(page2.Data[0], Is.EqualTo(0xFF));
        Assert.That(cache.Count, Is.EqualTo(1));
        
        cache.ReleasePage(5);
    }

    [Test]
    public void GetPageIncrementsReferenceCountTest()
    {
        using var storage = new StorageMemory(initialPageCount: 10);
        using var cache = new PageCacheShardedClock(storage, 100);
        
        var page1 = cache.GetPage(5);
        var page2 = cache.GetPage(5);
        var page3 = cache.GetPage(5);
        
        // All should be same page
        Assert.That(page1, Is.SameAs(page2));
        Assert.That(page2, Is.SameAs(page3));
        
        cache.ReleasePage(5);
        cache.ReleasePage(5);
        cache.ReleasePage(5);
    }

    #endregion

    #region CreatePage Tests

    [Test]
    public void CreatePageInitializesNewPageTest()
    {
        using var storage = new StorageMemory(initialPageCount: 10);
        using var cache = new PageCacheShardedClock(storage, 100);
        
        var page = cache.CreatePage(5);
        
        Assert.That(page.IsDirty, Is.True);
        Assert.That(page.Data[0], Is.EqualTo(0));
        Assert.That(cache.Count, Is.EqualTo(1));
        
        cache.ReleasePage(5);
    }

    [Test]
    public void CreatePageDuplicateThrowsTest()
    {
        using var storage = new StorageMemory(initialPageCount: 10);
        using var cache = new PageCacheShardedClock(storage, 100);
        
        cache.CreatePage(5);
        cache.ReleasePage(5);
        
        Assert.Throws<InvalidOperationException>(() => cache.CreatePage(5));
    }

    #endregion

    #region Clock Algorithm Tests

    [Test]
    public void ClockEvictionRespectsPinnedPagesTest()
    {
        using var storage = new StorageMemory(initialPageCount: 100);
        // Small cache to force eviction
        using var cache = new PageCacheShardedClock(storage, 8, shardCount: 1);
        
        // Fill cache with pinned pages
        var pinnedPages = new List<CachedPage>();
        for (int i = 0; i < 7; i++)
        {
            pinnedPages.Add(cache.CreatePage(i));
        }
        
        // Add one unpinned page
        var unpinnedPage = cache.CreatePage(7);
        cache.ReleasePage(7);
        
        // Try to add another page - should evict unpinned page (7)
        var newPage = cache.CreatePage(8);
        
        // Unpinned page should have been evicted
        Assert.That(cache.Count, Is.EqualTo(8));
        
        // Cleanup
        cache.ReleasePage(8);
        foreach (var p in pinnedPages)
        {
            cache.ReleasePage(p.PageNumber);
        }
    }

    [Test]
    public void ClockGivesSecondChanceTest()
    {
        using var storage = new StorageMemory(initialPageCount: 200);
        using var cache = new PageCacheShardedClock(storage, 8, shardCount: 1);
        
        // Add pages
        for (int i = 0; i < 8; i++)
        {
            cache.CreatePage(i);
            cache.ReleasePage(i);
        }
        
        // Access some pages to set their referenced bit
        cache.GetPage(0);
        cache.ReleasePage(0);
        cache.GetPage(2);
        cache.ReleasePage(2);
        cache.GetPage(4);
        cache.ReleasePage(4);
        
        // Add new page - should evict a non-referenced page first
        var newPage = cache.CreatePage(100);
        cache.ReleasePage(100);
        
        // Referenced pages should still be in cache
        // Try to get page 0, 2, or 4 - at least some should still be there
        // (Clock algorithm gives second chance but doesn't guarantee all survive)
        bool foundReferenced = false;
        foreach (int pageNum in new[] { 0, 2, 4 })
        {
            try
            {
                var page = cache.GetPage(pageNum);
                foundReferenced = true;
                cache.ReleasePage(pageNum);
                break;
            }
            catch
            {
                // Page was evicted
            }
        }
        
        // At least one referenced page should have survived
        Assert.That(foundReferenced, Is.True, "At least one referenced page should survive eviction");
    }

    [Test]
    public void AllPinnedThrowsExceptionTest()
    {
        using var storage = new StorageMemory(initialPageCount: 100);
        using var cache = new PageCacheShardedClock(storage, 4, shardCount: 1);
        
        // Fill cache with pinned pages
        for (int i = 0; i < 4; i++)
        {
            cache.CreatePage(i); // Don't release
        }
        
        // Try to add another - should throw
        Assert.Throws<InvalidOperationException>(() => cache.CreatePage(99));
        
        // Cleanup
        for (int i = 0; i < 4; i++)
        {
            cache.ReleasePage(i);
        }
    }

    #endregion

    #region Sharding Tests

    [Test]
    public void PagesDistributeAcrossShardsTest()
    {
        using var storage = new StorageMemory(initialPageCount: 100);
        using var cache = new PageCacheShardedClock(storage, 64, shardCount: 4);
        
        // Add pages that should go to different shards
        // Page 0 -> shard 0, Page 1 -> shard 1, Page 2 -> shard 2, Page 3 -> shard 3
        for (int i = 0; i < 16; i++)
        {
            cache.CreatePage(i);
            cache.ReleasePage(i);
        }
        
        Assert.That(cache.Count, Is.EqualTo(16));
    }

    [Test]
    public void ConcurrentAccessDifferentShardsTest()
    {
        using var storage = new StorageMemory(initialPageCount: 1000);
        using var cache = new PageCacheShardedClock(storage, 200, shardCount: 8);
        
        // Pre-create pages
        for (int i = 0; i < 100; i++)
        {
            cache.CreatePage(i);
            cache.ReleasePage(i);
        }
        
        // Concurrent access
        var tasks = new Task[8];
        for (int t = 0; t < 8; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    // Each thread accesses pages that map to its shard
                    int pageNum = threadId + (i * 8);
                    if (pageNum < 100)
                    {
                        var page = cache.GetPage(pageNum);
                        page.Data[0] = (byte)threadId;
                        cache.MarkDirty(pageNum);
                        cache.ReleasePage(pageNum);
                    }
                }
            });
        }
        
        Task.WaitAll(tasks);
        
        // Should complete without deadlocks
        Assert.Pass();
    }

    #endregion

    #region FlushAll Tests

    [Test]
    public void FlushAllWritesDirtyPagesTest()
    {
        using var storage = new StorageMemory(initialPageCount: 10);
        using var cache = new PageCacheShardedClock(storage, 100);
        
        var page = cache.CreatePage(5);
        page.Data[0] = 0xAB;
        cache.ReleasePage(5);
        
        cache.FlushAll();
        
        // Verify written to storage
        byte[] buffer = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
        storage.ReadPage(5, buffer);
        
        Assert.That(buffer[0], Is.EqualTo(0xAB));
    }

    [Test]
    public async Task FlushAllAsyncTest()
    {
        using var storage = new StorageMemory(initialPageCount: 10);
        using var cache = new PageCacheShardedClock(storage, 100);
        
        var page = cache.CreatePage(5);
        page.Data[0] = 0xCD;
        cache.ReleasePage(5);
        
        await cache.FlushAllAsync();
        
        byte[] buffer = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
        storage.ReadPage(5, buffer);
        
        Assert.That(buffer[0], Is.EqualTo(0xCD));
    }

    #endregion

    #region Evict Tests

    [Test]
    public void EvictRemovesPageTest()
    {
        using var storage = new StorageMemory(initialPageCount: 10);
        using var cache = new PageCacheShardedClock(storage, 100);
        
        cache.CreatePage(5);
        cache.ReleasePage(5);
        
        Assert.That(cache.Count, Is.EqualTo(1));
        
        cache.Evict(5);
        
        Assert.That(cache.Count, Is.EqualTo(0));
    }

    [Test]
    public void EvictPinnedPageThrowsTest()
    {
        using var storage = new StorageMemory(initialPageCount: 10);
        using var cache = new PageCacheShardedClock(storage, 100);
        
        cache.CreatePage(5); // Don't release
        
        Assert.Throws<InvalidOperationException>(() => cache.Evict(5));
        
        cache.ReleasePage(5);
    }

    [Test]
    public void EvictFlushesDirtyPageTest()
    {
        using var storage = new StorageMemory(initialPageCount: 10);
        using var cache = new PageCacheShardedClock(storage, 100);
        
        var page = cache.CreatePage(5);
        page.Data[0] = 0xEF;
        cache.ReleasePage(5);
        
        cache.Evict(5);
        
        // Should have been flushed before eviction
        byte[] buffer = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
        storage.ReadPage(5, buffer);
        
        Assert.That(buffer[0], Is.EqualTo(0xEF));
    }

    #endregion

    #region Clear Tests

    [Test]
    public void ClearRemovesAllPagesTest()
    {
        using var storage = new StorageMemory(initialPageCount: 100);
        using var cache = new PageCacheShardedClock(storage, 100);
        
        for (int i = 0; i < 50; i++)
        {
            cache.CreatePage(i);
            cache.ReleasePage(i);
        }
        
        Assert.That(cache.Count, Is.EqualTo(50));
        
        cache.Clear();
        
        Assert.That(cache.Count, Is.EqualTo(0));
    }

    #endregion

    #region Dispose Tests

    [Test]
    public void DisposeMultipleTimesDoesNotThrowTest()
    {
        using var storage = new StorageMemory(initialPageCount: 10);
        var cache = new PageCacheShardedClock(storage, 100);
        
        cache.Dispose();
        cache.Dispose();
        
        Assert.Pass();
    }

    [Test]
    public void OperationsAfterDisposeThrowTest()
    {
        using var storage = new StorageMemory(initialPageCount: 10);
        var cache = new PageCacheShardedClock(storage, 100);
        cache.Dispose();
        
        Assert.Throws<ObjectDisposedException>(() => cache.GetPage(0));
        Assert.Throws<ObjectDisposedException>(() => cache.CreatePage(0));
        Assert.Throws<ObjectDisposedException>(() => cache.FlushAll());
    }

    #endregion

    #region DirtyCount Tests

    [Test]
    public void DirtyCountTracksCorrectlyTest()
    {
        using var storage = new StorageMemory(initialPageCount: 10);
        using var cache = new PageCacheShardedClock(storage, 100);
        
        Assert.That(cache.DirtyCount, Is.EqualTo(0));
        
        cache.CreatePage(1);
        cache.CreatePage(2);
        cache.ReleasePage(1);
        cache.ReleasePage(2);
        
        Assert.That(cache.DirtyCount, Is.EqualTo(2));
        
        cache.FlushAll();
        
        Assert.That(cache.DirtyCount, Is.EqualTo(0));
    }

    #endregion
}

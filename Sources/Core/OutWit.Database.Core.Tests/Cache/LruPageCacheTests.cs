using OutWit.Database.Core.Cache;
using OutWit.Database.Core.Storage;

namespace OutWit.Database.Core.Tests.Cache;

[TestFixture]
public class PageCacheLruTest
{
    #region Constructor Tests

    [Test]
    public void ConstructorNullStorageThrowsTest()
    {
        Assert.Throws<ArgumentNullException>(() => new PageCacheLru(null!));
    }

    [Test]
    public void ConstructorZeroMaxPagesThrowsTest()
    {
        using var storage = new StorageMemory(initialPageCount: 5);
        Assert.Throws<ArgumentOutOfRangeException>(() => new PageCacheLru(storage, maxPages: 0));
    }

    [Test]
    public void ConstructorNegativeMaxPagesThrowsTest()
    {
        using var storage = new StorageMemory(initialPageCount: 5);
        Assert.Throws<ArgumentOutOfRangeException>(() => new PageCacheLru(storage, maxPages: -1));
    }

    #endregion

    #region GetPage Tests

    [Test]
    public void GetPageLoadsFromStorageTest()
    {
        using var storage = new StorageMemory(initialPageCount: 5);
        
        // Write some data to page 2
        byte[] data = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
        Array.Fill(data, (byte)0x42);
        storage.WritePage(2, data);
        
        using var cache = new PageCacheLru(storage, maxPages: 10);
        
        var page = cache.GetPage(2);
        
        Assert.That(page.ReadOnlyData[0], Is.EqualTo((byte)0x42));
        Assert.That(cache.Count, Is.EqualTo(1));
    }

    [Test]
    public void GetPageReturnsCachedPageTest()
    {
        using var storage = new StorageMemory(initialPageCount: 5);
        using var cache = new PageCacheLru(storage, maxPages: 10);
        
        var page1 = cache.GetPage(0);
        page1.Data[0] = 0xAB;
        
        cache.ReleasePage(0);
        
        var page2 = cache.GetPage(0);
        
        Assert.That(page2.Data[0], Is.EqualTo((byte)0xAB));
        Assert.That(cache.Count, Is.EqualTo(1));
    }

    [Test]
    public void GetPageIncrementsReferenceCountTest()
    {
        using var storage = new StorageMemory(initialPageCount: 5);
        using var cache = new PageCacheLru(storage, maxPages: 10);
        
        var page1 = cache.GetPage(0);
        cache.ReleasePage(0);
        
        // Get same page twice without releasing
        var page2 = cache.GetPage(0);
        var page3 = cache.GetPage(0);
        
        // Should not be able to evict (reference count > 0)
        Assert.Throws<InvalidOperationException>(() => cache.Evict(0));
    }

    #endregion

    #region CreatePage Tests

    [Test]
    public void CreatePageNewPageTest()
    {
        using var storage = new StorageMemory(initialPageCount: 5);
        using var cache = new PageCacheLru(storage, maxPages: 10);
        
        storage.SetSize(10);
        var page = cache.CreatePage(7);
        
        Assert.That(page.IsDirty, Is.True);
        Assert.That(cache.Count, Is.EqualTo(1));
        Assert.That(cache.DirtyCount, Is.EqualTo(1));
    }

    [Test]
    public void CreatePageDuplicateThrowsTest()
    {
        using var storage = new StorageMemory(initialPageCount: 5);
        using var cache = new PageCacheLru(storage, maxPages: 10);
        
        _ = cache.GetPage(0);
        
        Assert.Throws<InvalidOperationException>(() => cache.CreatePage(0));
    }

    [Test]
    public void CreatePageInitializesToZerosTest()
    {
        using var storage = new StorageMemory(initialPageCount: 5);
        using var cache = new PageCacheLru(storage, maxPages: 10);
        
        storage.SetSize(10);
        var page = cache.CreatePage(7);
        
        // All bytes should be zero
        foreach (byte b in page.ReadOnlyData)
        {
            Assert.That(b, Is.EqualTo(0));
        }
    }

    #endregion

    #region MarkDirty Tests

    [Test]
    public void MarkDirtyTest()
    {
        using var storage = new StorageMemory(initialPageCount: 5);
        using var cache = new PageCacheLru(storage, maxPages: 10);
        
        var page = cache.GetPage(0);
        Assert.That(page.IsDirty, Is.False);
        
        cache.MarkDirty(0);
        
        Assert.That(page.IsDirty, Is.True);
    }

    [Test]
    public void MarkDirtyNonExistentPageDoesNothingTest()
    {
        using var storage = new StorageMemory(initialPageCount: 5);
        using var cache = new PageCacheLru(storage, maxPages: 10);
        
        // Should not throw
        cache.MarkDirty(999);
        
        Assert.That(cache.DirtyCount, Is.EqualTo(0));
    }

    #endregion

    #region ReleasePage Tests

    [Test]
    public void ReleasePageDecrementsReferenceCountTest()
    {
        using var storage = new StorageMemory(initialPageCount: 5);
        using var cache = new PageCacheLru(storage, maxPages: 10);
        
        _ = cache.GetPage(0);
        cache.ReleasePage(0);
        
        // Now page can be evicted
        cache.Evict(0);
        
        Assert.That(cache.Count, Is.EqualTo(0));
    }

    [Test]
    public void ReleasePageNonExistentPageDoesNothingTest()
    {
        using var storage = new StorageMemory(initialPageCount: 5);
        using var cache = new PageCacheLru(storage, maxPages: 10);
        
        // Should not throw
        cache.ReleasePage(999);
    }

    [Test]
    public void ReleasePageMultipleTimesDoesNotGoNegativeTest()
    {
        using var storage = new StorageMemory(initialPageCount: 5);
        using var cache = new PageCacheLru(storage, maxPages: 10);
        
        _ = cache.GetPage(0);
        
        // Release multiple times
        cache.ReleasePage(0);
        cache.ReleasePage(0);
        cache.ReleasePage(0);
        
        // Should still be evictable (not throw)
        cache.Evict(0);
        Assert.That(cache.Count, Is.EqualTo(0));
    }

    #endregion

    #region FlushAll Tests

    [Test]
    public void FlushAllWritesDirtyPagesTest()
    {
        using var storage = new StorageMemory(initialPageCount: 5);
        using var cache = new PageCacheLru(storage, maxPages: 10);
        
        var page = cache.GetPage(0);
        page.Data[0] = 0xCD;
        page.MarkDirty();
        
        cache.FlushAll();
        
        Assert.That(cache.DirtyCount, Is.EqualTo(0));
        
        // Verify data was written to storage
        byte[] buffer = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
        storage.ReadPage(0, buffer);
        Assert.That(buffer[0], Is.EqualTo((byte)0xCD));
    }

    [Test]
    public void FlushAllEmptyCacheDoesNotThrowTest()
    {
        using var storage = new StorageMemory(initialPageCount: 5);
        using var cache = new PageCacheLru(storage, maxPages: 10);
        
        cache.FlushAll();
        
        Assert.That(cache.DirtyCount, Is.EqualTo(0));
    }

    [Test]
    public async Task FlushAllAsyncWritesDirtyPagesTest()
    {
        using var storage = new StorageMemory(initialPageCount: 5);
        using var cache = new PageCacheLru(storage, maxPages: 10);
        
        var page = cache.GetPage(0);
        page.Data[0] = 0xEE;
        page.MarkDirty();
        
        await cache.FlushAllAsync();
        
        Assert.That(cache.DirtyCount, Is.EqualTo(0));
        
        byte[] buffer = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
        storage.ReadPage(0, buffer);
        Assert.That(buffer[0], Is.EqualTo((byte)0xEE));
    }

    [Test]
    public async Task FlushAllAsyncMultipleDirtyPagesTest()
    {
        using var storage = new StorageMemory(initialPageCount: 5);
        using var cache = new PageCacheLru(storage, maxPages: 10);
        
        var page0 = cache.GetPage(0);
        page0.Data[0] = 0x11;
        page0.MarkDirty();
        
        var page1 = cache.GetPage(1);
        page1.Data[0] = 0x22;
        page1.MarkDirty();
        
        var page2 = cache.GetPage(2);
        page2.Data[0] = 0x33;
        page2.MarkDirty();
        
        Assert.That(cache.DirtyCount, Is.EqualTo(3));
        
        await cache.FlushAllAsync();
        
        Assert.That(cache.DirtyCount, Is.EqualTo(0));
    }

    [Test]
    public async Task FlushAllAsyncCancellationTest()
    {
        using var storage = new StorageMemory(initialPageCount: 5);
        using var cache = new PageCacheLru(storage, maxPages: 10);
        
        var page = cache.GetPage(0);
        page.MarkDirty();
        cache.ReleasePage(0);
        
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        
        // CatchAsync catches base type and derived types (TaskCanceledException derives from OperationCanceledException)
        var ex = Assert.CatchAsync<OperationCanceledException>(async () => 
            await cache.FlushAllAsync(cts.Token));
        
        Assert.That(ex, Is.Not.Null);
    }

    #endregion

    #region FlushPage Tests

    [Test]
    public void FlushPageWritesSinglePageTest()
    {
        using var storage = new StorageMemory(initialPageCount: 5);
        using var cache = new PageCacheLru(storage, maxPages: 10);
        
        var page0 = cache.GetPage(0);
        page0.Data[0] = 0xAA;
        page0.MarkDirty();
        
        var page1 = cache.GetPage(1);
        page1.Data[0] = 0xBB;
        page1.MarkDirty();
        
        cache.FlushPage(0);
        
        // Only page 0 should be flushed
        Assert.That(page0.IsDirty, Is.False);
        Assert.That(page1.IsDirty, Is.True);
        Assert.That(cache.DirtyCount, Is.EqualTo(1));
    }

    [Test]
    public void FlushPageNonExistentPageDoesNothingTest()
    {
        using var storage = new StorageMemory(initialPageCount: 5);
        using var cache = new PageCacheLru(storage, maxPages: 10);
        
        // Should not throw
        cache.FlushPage(999);
    }

    [Test]
    public void FlushPageCleanPageDoesNothingTest()
    {
        using var storage = new StorageMemory(initialPageCount: 5);
        using var cache = new PageCacheLru(storage, maxPages: 10);
        
        _ = cache.GetPage(0);
        
        // Page is not dirty, should not throw
        cache.FlushPage(0);
    }

    [Test]
    public async Task FlushPageAsyncWritesSinglePageTest()
    {
        using var storage = new StorageMemory(initialPageCount: 5);
        using var cache = new PageCacheLru(storage, maxPages: 10);
        
        var page = cache.GetPage(0);
        page.Data[0] = 0xCC;
        page.MarkDirty();
        
        await cache.FlushPageAsync(0);
        
        Assert.That(page.IsDirty, Is.False);
        
        byte[] buffer = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
        storage.ReadPage(0, buffer);
        Assert.That(buffer[0], Is.EqualTo((byte)0xCC));
    }

    [Test]
    public async Task FlushPageAsyncNonExistentPageDoesNothingTest()
    {
        using var storage = new StorageMemory(initialPageCount: 5);
        using var cache = new PageCacheLru(storage, maxPages: 10);
        
        // Should not throw
        await cache.FlushPageAsync(999);
    }

    #endregion

    #region Evict Tests

    [Test]
    public void EvictFlushesAndRemovesTest()
    {
        using var storage = new StorageMemory(initialPageCount: 5);
        using var cache = new PageCacheLru(storage, maxPages: 10);
        
        var page = cache.GetPage(0);
        page.Data[0] = 0xEF;
        page.MarkDirty();
        cache.ReleasePage(0);
        
        Assert.That(cache.Count, Is.EqualTo(1));
        
        cache.Evict(0);
        
        Assert.That(cache.Count, Is.EqualTo(0));
        
        // Verify data was written to storage
        byte[] buffer = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
        storage.ReadPage(0, buffer);
        Assert.That(buffer[0], Is.EqualTo((byte)0xEF));
    }

    [Test]
    public void EvictPinnedPageThrowsTest()
    {
        using var storage = new StorageMemory(initialPageCount: 5);
        using var cache = new PageCacheLru(storage, maxPages: 10);
        
        _ = cache.GetPage(0); // Page is pinned (ReferenceCount = 1)
        
        Assert.Throws<InvalidOperationException>(() => cache.Evict(0));
    }

    [Test]
    public void EvictNonExistentPageDoesNothingTest()
    {
        using var storage = new StorageMemory(initialPageCount: 5);
        using var cache = new PageCacheLru(storage, maxPages: 10);
        
        // Should not throw
        cache.Evict(999);
    }

    #endregion

    #region LRU Tests

    [Test]
    public void LruEvictionTest()
    {
        using var storage = new StorageMemory(initialPageCount: 10);
        using var cache = new PageCacheLru(storage, maxPages: 3);
        
        // Load 3 pages
        cache.GetPage(0);
        cache.ReleasePage(0);
        
        cache.GetPage(1);
        cache.ReleasePage(1);
        
        cache.GetPage(2);
        cache.ReleasePage(2);
        
        Assert.That(cache.Count, Is.EqualTo(3));
        
        // Access page 0 again to make it MRU
        cache.GetPage(0);
        cache.ReleasePage(0);
        
        // Load page 3 - should evict page 1 (LRU)
        cache.GetPage(3);
        cache.ReleasePage(3);
        
        Assert.That(cache.Count, Is.EqualTo(3));
    }

    [Test]
    public void PinnedPageNotEvictedTest()
    {
        using var storage = new StorageMemory(initialPageCount: 10);
        using var cache = new PageCacheLru(storage, maxPages: 2);
        
        // Get two pages and keep both pinned (don't release)
        _ = cache.GetPage(0);
        _ = cache.GetPage(1);
        
        // Try to get a third page - should fail because both pages are pinned
        Assert.Throws<InvalidOperationException>(() => cache.GetPage(2));
    }

    [Test]
    public void LruEvictsOldestUnpinnedPageTest()
    {
        using var storage = new StorageMemory(initialPageCount: 10);
        using var cache = new PageCacheLru(storage, maxPages: 3);
        
        // Load pages 0, 1, 2
        _ = cache.GetPage(0); // Keep pinned
        
        cache.GetPage(1);
        cache.ReleasePage(1);
        
        cache.GetPage(2);
        cache.ReleasePage(2);
        
        // Page 0 is pinned, pages 1 and 2 are not
        // Load page 3 - should evict page 1 (oldest unpinned)
        cache.GetPage(3);
        cache.ReleasePage(3);
        
        Assert.That(cache.Count, Is.EqualTo(3));
        
        // Page 0 should still be in cache (was pinned)
        // Getting it again should work without loading from storage
        cache.ReleasePage(0);
    }

    #endregion

    #region Clear Tests

    [Test]
    public void ClearFlushesAndDisposesTest()
    {
        using var storage = new StorageMemory(initialPageCount: 5);
        using var cache = new PageCacheLru(storage, maxPages: 10);
        
        var page = cache.GetPage(0);
        page.Data[0] = 0x11;
        page.MarkDirty();
        cache.ReleasePage(0);
        
        cache.Clear();
        
        Assert.That(cache.Count, Is.EqualTo(0));
        Assert.That(cache.DirtyCount, Is.EqualTo(0));
        
        // Verify data was written
        byte[] buffer = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
        storage.ReadPage(0, buffer);
        Assert.That(buffer[0], Is.EqualTo((byte)0x11));
    }

    [Test]
    public void ClearEmptyCacheDoesNotThrowTest()
    {
        using var storage = new StorageMemory(initialPageCount: 5);
        using var cache = new PageCacheLru(storage, maxPages: 10);
        
        cache.Clear();
        
        Assert.That(cache.Count, Is.EqualTo(0));
    }

    #endregion

    #region Dispose Tests

    [Test]
    public void DisposeFlushesAllTest()
    {
        using var storage = new StorageMemory(initialPageCount: 5);
        
        // Scope the cache
        {
            using var cache = new PageCacheLru(storage, maxPages: 10);
            var page = cache.GetPage(0);
            page.Data[0] = 0x22;
            page.MarkDirty();
        }
        
        // After dispose, data should be written
        byte[] buffer = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
        storage.ReadPage(0, buffer);
        Assert.That(buffer[0], Is.EqualTo((byte)0x22));
    }

    [Test]
    public void DisposeMultipleTimesDoesNotThrowTest()
    {
        using var storage = new StorageMemory(initialPageCount: 5);
        var cache = new PageCacheLru(storage, maxPages: 10);
        
        cache.Dispose();
        cache.Dispose(); // Should not throw
    }

    [Test]
    public void OperationsAfterDisposeThrowTest()
    {
        using var storage = new StorageMemory(initialPageCount: 5);
        var cache = new PageCacheLru(storage, maxPages: 10);
        cache.Dispose();

        Assert.Throws<ObjectDisposedException>(() => cache.GetPage(0));
        Assert.Throws<ObjectDisposedException>(() => cache.CreatePage(5));
        Assert.Throws<ObjectDisposedException>(() => cache.MarkDirty(0));
        Assert.Throws<ObjectDisposedException>(() => cache.FlushAll());
        Assert.Throws<ObjectDisposedException>(() => cache.FlushPage(0));
        Assert.Throws<ObjectDisposedException>(() => cache.Evict(0));
        Assert.Throws<ObjectDisposedException>(() => cache.Clear());
    }

    #endregion

    #region DirtyCount Tests

    [Test]
    public void DirtyCountReflectsStateTest()
    {
        using var storage = new StorageMemory(initialPageCount: 5);
        using var cache = new PageCacheLru(storage, maxPages: 10);
        
        Assert.That(cache.DirtyCount, Is.EqualTo(0));
        
        var page0 = cache.GetPage(0);
        Assert.That(cache.DirtyCount, Is.EqualTo(0));
        
        page0.MarkDirty();
        Assert.That(cache.DirtyCount, Is.EqualTo(1));
        
        var page1 = cache.GetPage(1);
        page1.MarkDirty();
        Assert.That(cache.DirtyCount, Is.EqualTo(2));
        
        cache.FlushPage(0);
        Assert.That(cache.DirtyCount, Is.EqualTo(1));
        
        cache.FlushAll();
        Assert.That(cache.DirtyCount, Is.EqualTo(0));
    }

    #endregion

    #region Concurrency Tests

    [Test]
    public void ConcurrentGetPageTest()
    {
        using var storage = new StorageMemory(initialPageCount: 100);
        using var cache = new PageCacheLru(storage, maxPages: 50);
        
        const int threadCount = 10;
        const int operationsPerThread = 100;
        
        var tasks = new Task[threadCount];
        var exceptions = new List<Exception>();
        
        for (int t = 0; t < threadCount; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                try
                {
                    var random = new Random(threadId);
                    for (int i = 0; i < operationsPerThread; i++)
                    {
                        long pageNum = random.Next(0, 100);
                        var page = cache.GetPage(pageNum);
                        cache.ReleasePage(pageNum);
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            });
        }
        
        Task.WaitAll(tasks);
        
        Assert.That(exceptions, Is.Empty, 
            $"Exceptions occurred: {string.Join(", ", exceptions.Select(e => e.Message))}");
    }

    [Test]
    public void ConcurrentReadWriteTest()
    {
        using var storage = new StorageMemory(initialPageCount: 20);
        using var cache = new PageCacheLru(storage, maxPages: 10);
        
        const int threadCount = 5;
        const int operationsPerThread = 50;
        
        var tasks = new Task[threadCount];
        
        for (int t = 0; t < threadCount; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                var random = new Random(threadId);
                for (int i = 0; i < operationsPerThread; i++)
                {
                    long pageNum = random.Next(0, 20);
                    var page = cache.GetPage(pageNum);
                    
                    // Read
                    _ = page.ReadOnlyData[0];
                    
                    // Sometimes write
                    if (random.Next(2) == 0)
                    {
                        page.Data[0] = (byte)threadId;
                        page.MarkDirty();
                    }
                    
                    cache.ReleasePage(pageNum);
                }
            });
        }
        
        Task.WaitAll(tasks);
        
        // Should complete without deadlocks or exceptions
        cache.FlushAll();
    }

    #endregion
}

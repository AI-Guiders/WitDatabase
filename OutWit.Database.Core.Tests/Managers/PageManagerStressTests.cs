using OutWit.Database.Core.Cache;
using OutWit.Database.Core.Interfaces;
using OutWit.Database.Core.Managers;
using OutWit.Database.Core.Pages;
using OutWit.Database.Core.Storage;

namespace OutWit.Database.Core.Tests.Managers;

[TestFixture]
public class PageManagerStressTest : PageManagerTestBase
{
    #region Test Case Sources

    private static readonly object[] StorageCacheCombinations =
    [
        new object[] { StorageType.Memory, CacheType.Lru },
        new object[] { StorageType.Memory, CacheType.ShardedClock },
        new object[] { StorageType.File, CacheType.Lru },
        new object[] { StorageType.File, CacheType.ShardedClock }
    ];

    #endregion

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void AllocateManyPagesTest(StorageType storageType, CacheType cacheType)
    {
        const int pageCount = 500;
        
        using var pageManager = CreatePageManager(storageType, cacheType);

        var allocatedPages = new List<uint>();

        // Allocate many pages
        for (int i = 0; i < pageCount; i++)
        {
            var (pageNumber, page) = pageManager.AllocatePage(PageType.Leaf);
            page.Data[0] = (byte)(i % 256);
            page.Data[1] = (byte)(i / 256);
            pageManager.MarkDirty(pageNumber);
            pageManager.ReleasePage(pageNumber);
            allocatedPages.Add(pageNumber);
        }

        Assert.That(pageManager.TotalPageCount, Is.EqualTo((uint)(pageCount + 1))); // +1 for header

        // Verify all pages
        foreach (var pageNumber in allocatedPages)
        {
            var page = pageManager.GetPage(pageNumber);
            int expectedValue = (int)(pageNumber - 1); // First allocated is page 1
            Assert.That(page.Data[0], Is.EqualTo((byte)(expectedValue % 256)));
            Assert.That(page.Data[1], Is.EqualTo((byte)(expectedValue / 256)));
            pageManager.ReleasePage(pageNumber);
        }
    }

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void FreeAndReusePatternTest(StorageType storageType, CacheType cacheType)
    {
        const int cycles = 100;
        const int pagesPerCycle = 10;
        
        using var pageManager = CreatePageManager(storageType, cacheType);

        for (int cycle = 0; cycle < cycles; cycle++)
        {
            var allocated = new List<uint>();

            // Allocate batch
            for (int i = 0; i < pagesPerCycle; i++)
            {
                var (pageNumber, _) = pageManager.AllocatePage(PageType.Leaf);
                pageManager.ReleasePage(pageNumber);
                allocated.Add(pageNumber);
            }

            // Free every other page
            for (int i = 0; i < allocated.Count; i += 2)
            {
                pageManager.FreePage(allocated[i]);
            }
        }

        // Total pages should be less than cycles * pagesPerCycle due to reuse
        Assert.That(pageManager.TotalPageCount, Is.LessThan((uint)(cycles * pagesPerCycle)));
        Assert.That(pageManager.FreePageCount, Is.GreaterThan(0u));
    }

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void RandomWriteToManyPagesTest(StorageType storageType, CacheType cacheType)
    {
        const int pageCount = 200;
        const int writeOperations = 1000;
        
        using var pageManager = CreatePageManager(storageType, cacheType, cacheSize: 50);

        // Allocate pages
        var pages = new List<uint>();
        for (int i = 0; i < pageCount; i++)
        {
            var (pageNumber, _) = pageManager.AllocatePage(PageType.Leaf);
            pageManager.ReleasePage(pageNumber);
            pages.Add(pageNumber);
        }

        // Track latest value for each page
        var pageValues = new Dictionary<uint, int>();
        var random = new Random(42); // Fixed seed for reproducibility

        // Random writes
        for (int op = 0; op < writeOperations; op++)
        {
            uint targetPage = pages[random.Next(pages.Count)];
            
            var page = pageManager.GetPage(targetPage);
            int value = op;
            page.Data[0] = (byte)(value & 0xFF);
            page.Data[1] = (byte)((value >> 8) & 0xFF);
            page.Data[2] = (byte)((value >> 16) & 0xFF);
            page.Data[3] = (byte)((value >> 24) & 0xFF);
            pageManager.MarkDirty(targetPage);
            pageManager.ReleasePage(targetPage);
            
            pageValues[targetPage] = value;
        }

        // Verify all pages have correct latest value
        foreach (var (pageNumber, expectedValue) in pageValues)
        {
            var page = pageManager.GetPage(pageNumber);
            int actualValue = page.Data[0] | 
                (page.Data[1] << 8) | 
                (page.Data[2] << 16) | 
                (page.Data[3] << 24);
            Assert.That(actualValue, Is.EqualTo(expectedValue), 
                $"Page {pageNumber} value mismatch");
            pageManager.ReleasePage(pageNumber);
        }
    }

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void CacheEvictionUnderPressureTest(StorageType storageType, CacheType cacheType)
    {
        const int pageCount = 200;
        const int cacheSize = 20;
        
        using var pageManager = CreatePageManager(storageType, cacheType, cacheSize);

        // Allocate more pages than cache can hold
        var pages = new List<uint>();
        for (int i = 0; i < pageCount; i++)
        {
            var (pageNumber, page) = pageManager.AllocatePage(PageType.Leaf);
            
            // Write unique data
            page.Data[0] = (byte)(i % 256);
            page.Data[1] = (byte)(i / 256);
            pageManager.MarkDirty(pageNumber);
            pageManager.ReleasePage(pageNumber);
            
            pages.Add(pageNumber);
        }

        // Access pages in various patterns - should cause cache evictions
        // Pattern 1: Sequential
        for (int i = 0; i < pageCount; i++)
        {
            var page = pageManager.GetPage(pages[i]);
            pageManager.ReleasePage(pages[i]);
        }

        // Pattern 2: Reverse
        for (int i = pageCount - 1; i >= 0; i--)
        {
            var page = pageManager.GetPage(pages[i]);
            pageManager.ReleasePage(pages[i]);
        }

        // Pattern 3: Random
        var random = new Random(42);
        for (int i = 0; i < 500; i++)
        {
            int idx = random.Next(pageCount);
            var page = pageManager.GetPage(pages[idx]);
            pageManager.ReleasePage(pages[idx]);
        }

        // Verify all data is still correct
        for (int i = 0; i < pageCount; i++)
        {
            var page = pageManager.GetPage(pages[i]);
            Assert.That(page.Data[0], Is.EqualTo((byte)(i % 256)), $"Page {i} byte 0 mismatch");
            Assert.That(page.Data[1], Is.EqualTo((byte)(i / 256)), $"Page {i} byte 1 mismatch");
            pageManager.ReleasePage(pages[i]);
        }
    }

    [Test]
    [TestCase(StorageType.Memory, CacheType.Lru)]
    [TestCase(StorageType.Memory, CacheType.ShardedClock)]
    public void FlushAndReopenTest_Memory(StorageType storageType, CacheType cacheType)
    {
        const int pageCount = 100;
        
        var storage = CreateStorage(storageType);

        using (var cache1 = CreateCache(storage, cacheType))
        using (var pm1 = new PageManager(storage, cache1))
        {
            for (int i = 0; i < pageCount; i++)
            {
                var (pageNumber, page) = pm1.AllocatePage(PageType.Leaf);
                page.Data[100] = (byte)i;
                pm1.MarkDirty(pageNumber);
                pm1.ReleasePage(pageNumber);
            }
            pm1.Flush();
        }

        using var cache2 = CreateCache(storage, cacheType);
        using var pm2 = new PageManager(storage, cache2);
        
        Assert.That(pm2.TotalPageCount, Is.EqualTo((uint)(pageCount + 1)));

        for (uint i = 1; i <= pageCount; i++)
        {
            var page = pm2.GetPage(i);
            Assert.That(page.Data[100], Is.EqualTo((byte)(i - 1)));
            pm2.ReleasePage(i);
        }
    }

    [Test]
    [TestCase(CacheType.Lru)]
    [TestCase(CacheType.ShardedClock)]
    public void FlushAndReopenTest_File(CacheType cacheType)
    {
        const int pageCount = 100;
        var dbPath = Path.Combine(TestDir!, $"flush_reopen_{Guid.NewGuid():N}.db");

        using (var storage1 = new FileStorage(dbPath))
        using (var cache1 = CreateCache(storage1, cacheType))
        using (var pm1 = new PageManager(storage1, cache1))
        {
            for (int i = 0; i < pageCount; i++)
            {
                var (pageNumber, page) = pm1.AllocatePage(PageType.Leaf);
                page.Data[100] = (byte)i;
                pm1.MarkDirty(pageNumber);
                pm1.ReleasePage(pageNumber);
            }
        }

        using var storage2 = new FileStorage(dbPath);
        using var cache2 = CreateCache(storage2, cacheType);
        using var pm2 = new PageManager(storage2, cache2);
        
        Assert.That(pm2.TotalPageCount, Is.EqualTo((uint)(pageCount + 1)));

        for (uint i = 1; i <= pageCount; i++)
        {
            var page = pm2.GetPage(i);
            Assert.That(page.Data[100], Is.EqualTo((byte)(i - 1)));
            pm2.ReleasePage(i);
        }
    }

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void InterleavedAllocFreeTest(StorageType storageType, CacheType cacheType)
    {
        using var pageManager = CreatePageManager(storageType, cacheType, cacheSize: 30);

        var activePages = new HashSet<uint>();
        const int operations = 500;
        var random = new Random(42);

        for (int op = 0; op < operations; op++)
        {
            bool shouldAllocate = activePages.Count == 0 || 
                (activePages.Count < 100 && random.Next(3) != 0);

            if (shouldAllocate)
            {
                var (pageNumber, page) = pageManager.AllocatePage(PageType.Leaf);
                page.Data[0] = (byte)(pageNumber % 256);
                pageManager.MarkDirty(pageNumber);
                pageManager.ReleasePage(pageNumber);
                activePages.Add(pageNumber);
            }
            else
            {
                var pageToFree = activePages.ElementAt(random.Next(activePages.Count));
                activePages.Remove(pageToFree);
                pageManager.FreePage(pageToFree);
            }
        }

        // Verify remaining pages
        foreach (uint pageNumber in activePages)
        {
            var page = pageManager.GetPage(pageNumber);
            Assert.That(page.Data[0], Is.EqualTo((byte)(pageNumber % 256)));
            pageManager.ReleasePage(pageNumber);
        }

        TestContext.WriteLine($"Final: {activePages.Count} active, {pageManager.FreePageCount} free, {pageManager.TotalPageCount} total");
    }

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void ConcurrentReadWriteTest(StorageType storageType, CacheType cacheType)
    {
        const int pageCount = 50;
        const int threadCount = 4;
        const int operationsPerThread = 100;
        
        // Use larger cache to avoid "all pages pinned" with sharded cache
        // ShardedClock divides cache among shards, so we need more headroom
        using var pageManager = CreatePageManager(storageType, cacheType, cacheSize: 100);

        // Pre-allocate pages
        var pages = new List<uint>();
        for (int i = 0; i < pageCount; i++)
        {
            var (pageNumber, _) = pageManager.AllocatePage(PageType.Leaf);
            pageManager.ReleasePage(pageNumber);
            pages.Add(pageNumber);
        }

        var exceptions = new List<Exception>();
        var tasks = new Task[threadCount];

        for (int t = 0; t < threadCount; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                try
                {
                    var random = new Random(threadId * 1000);
                    for (int i = 0; i < operationsPerThread; i++)
                    {
                        uint targetPage = pages[random.Next(pages.Count)];
                        
                        var page = pageManager.GetPage(targetPage);
                        
                        // Read
                        _ = page.ReadOnlyData[0];
                        
                        // Sometimes write
                        if (random.Next(2) == 0)
                        {
                            page.Data[0] = (byte)threadId;
                            pageManager.MarkDirty(targetPage);
                        }
                        
                        pageManager.ReleasePage(targetPage);
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

        pageManager.Flush();
    }
}

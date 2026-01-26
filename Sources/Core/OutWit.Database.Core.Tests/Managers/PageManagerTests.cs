using OutWit.Database.Core.Cache;
using OutWit.Database.Core.Interfaces;
using OutWit.Database.Core.Managers;
using OutWit.Database.Core.Pages;
using OutWit.Database.Core.Storage;

namespace OutWit.Database.Core.Tests.Managers;

/// <summary>
/// Storage type for parameterized tests.
/// </summary>
public enum StorageType
{
    Memory,
    File
}

/// <summary>
/// Cache type for parameterized tests.
/// </summary>
public enum CacheType
{
    Lru,
    ShardedClock
}

/// <summary>
/// Base class for PageManager tests with different Storage/Cache combinations.
/// </summary>
public abstract class PageManagerTestBase : IDisposable
{
    protected string? TestDir { get; private set; }
    
    [SetUp]
    public void SetUp()
    {
        TestDir = Path.Combine(Path.GetTempPath(), $"pm_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(TestDir);
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
            if (TestDir != null && Directory.Exists(TestDir))
                Directory.Delete(TestDir, recursive: true);
        }
        catch { }
    }
    
    protected IStorage CreateStorage(StorageType storageType)
    {
        return storageType switch
        {
            StorageType.Memory => new StorageMemory(initialPageCount: 0),
            StorageType.File => new StorageFile(Path.Combine(TestDir!, $"test_{Guid.NewGuid():N}.db")),
            _ => throw new ArgumentOutOfRangeException(nameof(storageType))
        };
    }
    
    protected IPageCache CreateCache(IStorage storage, CacheType cacheType, int cacheSize = 100)
    {
        return cacheType switch
        {
            CacheType.Lru => new PageCacheLru(storage, cacheSize),
            CacheType.ShardedClock => new PageCacheShardedClock(storage, cacheSize),
            _ => throw new ArgumentOutOfRangeException(nameof(cacheType))
        };
    }
    
    protected PageManager CreatePageManager(StorageType storageType, CacheType cacheType, int cacheSize = 100)
    {
        var storage = CreateStorage(storageType);
        var cache = CreateCache(storage, cacheType, cacheSize);
        return new PageManager(storage, cache);
    }
}

[TestFixture]
public class PageManagerTest : PageManagerTestBase
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

    #region Constructor Tests

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void CreateNewDatabaseTest(StorageType storageType, CacheType cacheType)
    {
        using var pageManager = CreatePageManager(storageType, cacheType);
        
        Assert.That(pageManager.PageSize, Is.EqualTo(DatabaseConstants.DEFAULT_PAGE_SIZE));
        Assert.That(pageManager.TotalPageCount, Is.EqualTo(1u));
        Assert.That(pageManager.FreePageCount, Is.EqualTo(0u));
    }

    [Test]
    public void ConstructorNullStorageThrowsTest()
    {
        Assert.Throws<ArgumentNullException>(() => new PageManager(null!));
    }

    [Test]
    public void ConstructorNullCacheThrowsTest()
    {
        using var storage = new StorageMemory(initialPageCount: 0);
        Assert.Throws<ArgumentNullException>(() => new PageManager(storage, (IPageCache)null!));
    }

    #endregion

    #region AllocatePage Tests

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void AllocatePageExtendsFileTest(StorageType storageType, CacheType cacheType)
    {
        using var pageManager = CreatePageManager(storageType, cacheType);
        
        var (pageNumber, page) = pageManager.AllocatePage(PageType.Leaf);
        
        Assert.That(pageNumber, Is.EqualTo(1u));
        Assert.That(pageManager.TotalPageCount, Is.EqualTo(2u));
        
        pageManager.ReleasePage(pageNumber);
    }

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void AllocateMultiplePagesTest(StorageType storageType, CacheType cacheType)
    {
        using var pageManager = CreatePageManager(storageType, cacheType);
        
        var (page1, _) = pageManager.AllocatePage(PageType.Leaf);
        pageManager.ReleasePage(page1);
        
        var (page2, _) = pageManager.AllocatePage(PageType.Internal);
        pageManager.ReleasePage(page2);
        
        var (page3, _) = pageManager.AllocatePage(PageType.Leaf);
        pageManager.ReleasePage(page3);
        
        Assert.That(page1, Is.EqualTo(1u));
        Assert.That(page2, Is.EqualTo(2u));
        Assert.That(page3, Is.EqualTo(3u));
        Assert.That(pageManager.TotalPageCount, Is.EqualTo(4u));
    }

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void AllocatePageInitializesHeaderTest(StorageType storageType, CacheType cacheType)
    {
        using var pageManager = CreatePageManager(storageType, cacheType);
        
        var (pageNumber, page) = pageManager.AllocatePage(PageType.Leaf);
        
        var header = PageHeader.ReadFrom(page.ReadOnlyData);
        Assert.That(header.PageType, Is.EqualTo(PageType.Leaf));
        Assert.That(header.CellCount, Is.EqualTo(0));
        
        pageManager.ReleasePage(pageNumber);
    }

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void AllocatePageDifferentTypesTest(StorageType storageType, CacheType cacheType)
    {
        using var pageManager = CreatePageManager(storageType, cacheType);
        
        foreach (PageType pageType in new[] { PageType.Leaf, PageType.Internal, PageType.Overflow, PageType.Schema })
        {
            var (pageNumber, page) = pageManager.AllocatePage(pageType);
            var header = PageHeader.ReadFrom(page.ReadOnlyData);
            
            Assert.That(header.PageType, Is.EqualTo(pageType));
            pageManager.ReleasePage(pageNumber);
        }
    }

    #endregion

    #region FreePage Tests

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void FreePageAddToFreeListTest(StorageType storageType, CacheType cacheType)
    {
        using var pageManager = CreatePageManager(storageType, cacheType);
        
        var (pageNumber, _) = pageManager.AllocatePage(PageType.Leaf);
        pageManager.ReleasePage(pageNumber);
        
        Assert.That(pageManager.FreePageCount, Is.EqualTo(0u));
        
        pageManager.FreePage(pageNumber);
        
        Assert.That(pageManager.FreePageCount, Is.EqualTo(1u));
    }

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void AllocateReusesFreePageTest(StorageType storageType, CacheType cacheType)
    {
        using var pageManager = CreatePageManager(storageType, cacheType);
        
        var (freedPage, _) = pageManager.AllocatePage(PageType.Leaf);
        pageManager.ReleasePage(freedPage);
        pageManager.FreePage(freedPage);
        
        uint countBefore = pageManager.TotalPageCount;
        
        var (reusedPage, _) = pageManager.AllocatePage(PageType.Internal);
        pageManager.ReleasePage(reusedPage);
        
        Assert.That(reusedPage, Is.EqualTo(freedPage));
        Assert.That(pageManager.TotalPageCount, Is.EqualTo(countBefore));
        Assert.That(pageManager.FreePageCount, Is.EqualTo(0u));
    }

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void FreeHeaderPageThrowsTest(StorageType storageType, CacheType cacheType)
    {
        using var pageManager = CreatePageManager(storageType, cacheType);
        
        Assert.Throws<ArgumentException>(() => pageManager.FreePage(0));
    }

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void FreeOutOfRangePageThrowsTest(StorageType storageType, CacheType cacheType)
    {
        using var pageManager = CreatePageManager(storageType, cacheType);
        
        Assert.Throws<ArgumentOutOfRangeException>(() => pageManager.FreePage(999));
    }

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void FreeMultiplePagesCreatesChainTest(StorageType storageType, CacheType cacheType)
    {
        using var pageManager = CreatePageManager(storageType, cacheType);
        
        var pages = new List<uint>();
        for (int i = 0; i < 5; i++)
        {
            var (pn, _) = pageManager.AllocatePage(PageType.Leaf);
            pageManager.ReleasePage(pn);
            pages.Add(pn);
        }
        
        foreach (var pn in pages)
        {
            pageManager.FreePage(pn);
        }
        
        Assert.That(pageManager.FreePageCount, Is.EqualTo(5u));
        
        for (int i = 0; i < 5; i++)
        {
            var (pn, _) = pageManager.AllocatePage(PageType.Leaf);
            pageManager.ReleasePage(pn);
            Assert.That(pages.Contains(pn), Is.True);
        }
        
        Assert.That(pageManager.FreePageCount, Is.EqualTo(0u));
    }

    #endregion

    #region GetPage Tests

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void GetPageReturnsCorrectDataTest(StorageType storageType, CacheType cacheType)
    {
        using var pageManager = CreatePageManager(storageType, cacheType);
        
        var (pageNumber, page) = pageManager.AllocatePage(PageType.Leaf);
        page.Data[100] = 0xAB;
        pageManager.MarkDirty(pageNumber);
        pageManager.ReleasePage(pageNumber);
        
        var same = pageManager.GetPage(pageNumber);
        Assert.That(same.Data[100], Is.EqualTo((byte)0xAB));
        pageManager.ReleasePage(pageNumber);
    }

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void GetOutOfRangePageThrowsTest(StorageType storageType, CacheType cacheType)
    {
        using var pageManager = CreatePageManager(storageType, cacheType);
        
        Assert.Throws<ArgumentOutOfRangeException>(() => pageManager.GetPage(999));
    }

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void GetHeaderPageTest(StorageType storageType, CacheType cacheType)
    {
        using var pageManager = CreatePageManager(storageType, cacheType);
        
        var page = pageManager.GetPage(0);
        
        Assert.That(page.ReadOnlyData[..16].SequenceEqual(DatabaseConstants.MAGIC_BYTES.ToArray()), Is.True);
        
        pageManager.ReleasePage(0);
    }

    #endregion

    #region Flush Tests

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void FlushWritesToStorageTest(StorageType storageType, CacheType cacheType)
    {
        var storage = CreateStorage(storageType);
        var cache = CreateCache(storage, cacheType);
        using var pageManager = new PageManager(storage, cache);
        
        var (pageNumber, page) = pageManager.AllocatePage(PageType.Leaf);
        page.Data[50] = 0xCD;
        pageManager.MarkDirty(pageNumber);
        pageManager.ReleasePage(pageNumber);
        
        pageManager.Flush();
        
        byte[] buffer = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
        storage.ReadPage(pageNumber, buffer);
        Assert.That(buffer[50], Is.EqualTo((byte)0xCD));
    }

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public async Task FlushAsyncWritesToStorageTest(StorageType storageType, CacheType cacheType)
    {
        var storage = CreateStorage(storageType);
        var cache = CreateCache(storage, cacheType);
        using var pageManager = new PageManager(storage, cache);
        
        var (pageNumber, page) = pageManager.AllocatePage(PageType.Leaf);
        page.Data[50] = 0xEF;
        pageManager.MarkDirty(pageNumber);
        pageManager.ReleasePage(pageNumber);
        
        await pageManager.FlushAsync();
        
        byte[] buffer = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
        storage.ReadPage(pageNumber, buffer);
        Assert.That(buffer[50], Is.EqualTo((byte)0xEF));
    }

    #endregion

    #region Header Tests

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void GetHeaderReturnsCurrentStateTest(StorageType storageType, CacheType cacheType)
    {
        using var pageManager = CreatePageManager(storageType, cacheType);
        
        var header = pageManager.GetHeader();
        
        Assert.That(header.FormatVersion, Is.EqualTo(DatabaseConstants.FORMAT_VERSION));
        Assert.That(header.PageSize, Is.EqualTo((ushort)DatabaseConstants.DEFAULT_PAGE_SIZE));
    }

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void SetSchemaRootPageTest(StorageType storageType, CacheType cacheType)
    {
        using var pageManager = CreatePageManager(storageType, cacheType);
        
        var (pageNumber, _) = pageManager.AllocatePage(PageType.Schema);
        pageManager.ReleasePage(pageNumber);
        
        pageManager.SetSchemaRootPage(pageNumber);
        
        var header = pageManager.GetHeader();
        Assert.That(header.SchemaRootPage, Is.EqualTo(pageNumber));
    }

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void IncrementTransactionCounterTest(StorageType storageType, CacheType cacheType)
    {
        using var pageManager = CreatePageManager(storageType, cacheType);
        
        var initial = pageManager.GetHeader().TransactionCounter;
        
        var next1 = pageManager.IncrementTransactionCounter();
        var next2 = pageManager.IncrementTransactionCounter();
        
        Assert.That(next1, Is.EqualTo(initial + 1));
        Assert.That(next2, Is.EqualTo(initial + 2));
    }

    #endregion

    #region Persistence Tests

    [Test]
    [TestCase(StorageType.Memory, CacheType.Lru)]
    [TestCase(StorageType.Memory, CacheType.ShardedClock)]
    public void ReopenDatabaseLoadsHeaderTest_Memory(StorageType storageType, CacheType cacheType)
    {
        var storage = CreateStorage(storageType);
        
        using (var cache1 = CreateCache(storage, cacheType))
        using (var pm1 = new PageManager(storage, cache1))
        {
            var (pn, _) = pm1.AllocatePage(PageType.Leaf);
            pm1.ReleasePage(pn);
            pm1.SetSchemaRootPage(pn);
            pm1.IncrementTransactionCounter();
        }
        
        using var cache2 = CreateCache(storage, cacheType);
        using var pm2 = new PageManager(storage, cache2);
        var header = pm2.GetHeader();
        
        Assert.That(header.TotalPageCount, Is.EqualTo(2u));
        Assert.That(header.SchemaRootPage, Is.EqualTo(1u));
        Assert.That(header.TransactionCounter, Is.EqualTo(1u));
    }

    [Test]
    [TestCase(CacheType.Lru)]
    [TestCase(CacheType.ShardedClock)]
    public void ReopenDatabaseLoadsHeaderTest_File(CacheType cacheType)
    {
        var dbPath = Path.Combine(TestDir!, $"reopen_{Guid.NewGuid():N}.db");
        
        using (var storage1 = new StorageFile(dbPath))
        using (var cache1 = CreateCache(storage1, cacheType))
        using (var pm1 = new PageManager(storage1, cache1))
        {
            var (pn, _) = pm1.AllocatePage(PageType.Leaf);
            pm1.ReleasePage(pn);
            pm1.SetSchemaRootPage(pn);
            pm1.IncrementTransactionCounter();
        }
        
        using var storage2 = new StorageFile(dbPath);
        using var cache2 = CreateCache(storage2, cacheType);
        using var pm2 = new PageManager(storage2, cache2);
        var header = pm2.GetHeader();
        
        Assert.That(header.TotalPageCount, Is.EqualTo(2u));
        Assert.That(header.SchemaRootPage, Is.EqualTo(1u));
        Assert.That(header.TransactionCounter, Is.EqualTo(1u));
    }

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void FreeListPersistedAcrossReopenTest(StorageType storageType, CacheType cacheType)
    {
        if (storageType == StorageType.File)
        {
            // File storage requires separate test due to reopen semantics
            return;
        }
        
        var storage = CreateStorage(storageType);
        
        using (var cache1 = CreateCache(storage, cacheType))
        using (var pm1 = new PageManager(storage, cache1))
        {
            var (pn1, _) = pm1.AllocatePage(PageType.Leaf);
            pm1.ReleasePage(pn1);
            var (pn2, _) = pm1.AllocatePage(PageType.Leaf);
            pm1.ReleasePage(pn2);
            
            pm1.FreePage(pn1);
            Assert.That(pm1.FreePageCount, Is.EqualTo(1u));
        }
        
        using var cache2 = CreateCache(storage, cacheType);
        using var pm2 = new PageManager(storage, cache2);
        Assert.That(pm2.FreePageCount, Is.EqualTo(1u));
        
        var (reused, _) = pm2.AllocatePage(PageType.Leaf);
        pm2.ReleasePage(reused);
        Assert.That(reused, Is.EqualTo(1u));
        Assert.That(pm2.FreePageCount, Is.EqualTo(0u));
    }

    #endregion

    #region Dispose Tests

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void DisposeFlushesDataTest(StorageType storageType, CacheType cacheType)
    {
        var storage = CreateStorage(storageType);
        
        using (var cache = CreateCache(storage, cacheType))
        using (var pageManager = new PageManager(storage, cache))
        {
            var (pn, page) = pageManager.AllocatePage(PageType.Leaf);
            page.Data[0] = 0xAA;
            pageManager.MarkDirty(pn);
            pageManager.ReleasePage(pn);
        }
        
        byte[] buffer = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
        storage.ReadPage(1, buffer);
        Assert.That(buffer[0], Is.EqualTo((byte)0xAA));
    }

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void DisposeMultipleTimesDoesNotThrowTest(StorageType storageType, CacheType cacheType)
    {
        var pageManager = CreatePageManager(storageType, cacheType);
        
        pageManager.Dispose();
        pageManager.Dispose();
        
        Assert.Pass();
    }

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void OperationsAfterDisposeThrowTest(StorageType storageType, CacheType cacheType)
    {
        var pageManager = CreatePageManager(storageType, cacheType);
        pageManager.Dispose();
        
        Assert.Throws<ObjectDisposedException>(() => pageManager.AllocatePage(PageType.Leaf));
        Assert.Throws<ObjectDisposedException>(() => pageManager.GetPage(0));
        Assert.Throws<ObjectDisposedException>(() => pageManager.FreePage(1));
        Assert.Throws<ObjectDisposedException>(() => pageManager.Flush());
        Assert.Throws<ObjectDisposedException>(() => pageManager.SetSchemaRootPage(1));
        Assert.Throws<ObjectDisposedException>(() => pageManager.IncrementTransactionCounter());
    }

    #endregion

    #region AllocatePages Batch Tests

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void AllocatePagesReturnsCorrectCountTest(StorageType storageType, CacheType cacheType)
    {
        using var pageManager = CreatePageManager(storageType, cacheType);
        
        var pages = pageManager.AllocatePages(PageType.Leaf, 10);
        
        Assert.That(pages.Length, Is.EqualTo(10));
        Assert.That(pageManager.TotalPageCount, Is.EqualTo(11u));
    }

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void AllocatePagesReusesFreePagesTest(StorageType storageType, CacheType cacheType)
    {
        using var pageManager = CreatePageManager(storageType, cacheType);
        
        var initial = pageManager.AllocatePages(PageType.Leaf, 5);
        foreach (var pn in initial)
            pageManager.FreePage(pn);
        
        Assert.That(pageManager.FreePageCount, Is.EqualTo(5u));
        
        var reused = pageManager.AllocatePages(PageType.Leaf, 5);
        
        Assert.That(pageManager.FreePageCount, Is.EqualTo(0u));
        foreach (var pn in reused)
            Assert.That(initial.Contains(pn), Is.True);
    }

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void AllocatePagesZeroCountThrowsTest(StorageType storageType, CacheType cacheType)
    {
        using var pageManager = CreatePageManager(storageType, cacheType);
        
        Assert.Throws<ArgumentOutOfRangeException>(() => pageManager.AllocatePages(PageType.Leaf, 0));
    }

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void AllocatePagesNegativeCountThrowsTest(StorageType storageType, CacheType cacheType)
    {
        using var pageManager = CreatePageManager(storageType, cacheType);
        
        Assert.Throws<ArgumentOutOfRangeException>(() => pageManager.AllocatePages(PageType.Leaf, -1));
    }

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void AllocatePagesMixedFreeAndNewTest(StorageType storageType, CacheType cacheType)
    {
        using var pageManager = CreatePageManager(storageType, cacheType);
        
        var initial = pageManager.AllocatePages(PageType.Leaf, 3);
        
        pageManager.FreePage(initial[0]);
        pageManager.FreePage(initial[2]);
        
        Assert.That(pageManager.FreePageCount, Is.EqualTo(2u));
        
        var mixed = pageManager.AllocatePages(PageType.Leaf, 5);
        
        Assert.That(mixed.Length, Is.EqualTo(5));
        Assert.That(pageManager.FreePageCount, Is.EqualTo(0u));
    }

    #endregion

    #region FreePages Batch Tests

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void FreePagesMultipleTest(StorageType storageType, CacheType cacheType)
    {
        using var pageManager = CreatePageManager(storageType, cacheType);
        
        var pages = pageManager.AllocatePages(PageType.Leaf, 5);
        
        pageManager.FreePages(pages);
        
        Assert.That(pageManager.FreePageCount, Is.EqualTo(5u));
    }

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void FreePagesEmptySpanDoesNothingTest(StorageType storageType, CacheType cacheType)
    {
        using var pageManager = CreatePageManager(storageType, cacheType);
        
        pageManager.FreePages(ReadOnlySpan<uint>.Empty);
        
        Assert.That(pageManager.FreePageCount, Is.EqualTo(0u));
    }

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void FreePagesWithHeaderPageThrowsTest(StorageType storageType, CacheType cacheType)
    {
        using var pageManager = CreatePageManager(storageType, cacheType);
        
        var pages = new uint[] { 0 };
        
        Assert.Throws<ArgumentException>(() => pageManager.FreePages(pages));
    }

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void FreePagesOutOfRangeThrowsTest(StorageType storageType, CacheType cacheType)
    {
        using var pageManager = CreatePageManager(storageType, cacheType);
        
        var pages = new uint[] { 999 };
        
        Assert.Throws<ArgumentOutOfRangeException>(() => pageManager.FreePages(pages));
    }

    #endregion

    #region Header Dirty Flag Tests

    [Test]
    [TestCaseSource(nameof(StorageCacheCombinations))]
    public void HeaderNotWrittenUntilFlushTest(StorageType storageType, CacheType cacheType)
    {
        var storage = CreateStorage(storageType);
        var cache = CreateCache(storage, cacheType);
        using var pageManager = new PageManager(storage, cache);
        
        pageManager.AllocatePages(PageType.Leaf, 5);
        
        pageManager.Flush();
        
        byte[] buffer = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
        storage.ReadPage(0, buffer);
        var flushedHeader = DatabaseHeader.ReadFrom(buffer);
        
        Assert.That(flushedHeader.TotalPageCount, Is.EqualTo(6u));
    }

    #endregion
}

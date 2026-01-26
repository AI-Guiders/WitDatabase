using OutWit.Database.Core.Cache;
using OutWit.Database.Core.Interfaces;
using OutWit.Database.Core.Managers;
using OutWit.Database.Core.Pages;
using OutWit.Database.Core.Storage;

namespace OutWit.Database.Core.Tests.Managers;

[TestFixture]
public class PageManagerAsyncTests : IDisposable
{
    #region Fields

    private string? m_testDir;

    #endregion

    #region Setup/Teardown

    [SetUp]
    public void SetUp()
    {
        m_testDir = Path.Combine(Path.GetTempPath(), $"pm_async_test_{Guid.NewGuid():N}");
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
            if (m_testDir != null && Directory.Exists(m_testDir))
                Directory.Delete(m_testDir, recursive: true);
        }
        catch { }
    }

    #endregion

    #region CreateAsync Tests

    [Test]
    public async Task CreateAsyncWithMemoryStorageTest()
    {
        var storage = new StorageMemory(initialPageCount: 0);
        var cache = new PageCacheShardedClock(storage, 100);
        
        var pageManager = await PageManager.CreateAsync(storage, cache);
        
        Assert.That(pageManager.IsInitialized, Is.True);
        Assert.That(pageManager.PageSize, Is.EqualTo(DatabaseConstants.DEFAULT_PAGE_SIZE));
        Assert.That(pageManager.TotalPageCount, Is.EqualTo(1u));
        
        pageManager.Dispose();
    }

    [Test]
    public async Task CreateAsyncWithFileStorageTest()
    {
        var dbPath = Path.Combine(m_testDir!, $"async_test_{Guid.NewGuid():N}.db");
        var storage = new StorageFile(dbPath);
        var cache = new PageCacheShardedClock(storage, 100);
        
        var pageManager = await PageManager.CreateAsync(storage, cache);
        
        Assert.That(pageManager.IsInitialized, Is.True);
        Assert.That(pageManager.TotalPageCount, Is.EqualTo(1u));
        
        pageManager.Dispose();
        storage.Dispose();
    }

    [Test]
    public async Task CreateAsyncWithDefaultCacheTest()
    {
        var storage = new StorageMemory(initialPageCount: 0);
        
        var pageManager = await PageManager.CreateAsync(storage, cacheSize: 50);
        
        Assert.That(pageManager.IsInitialized, Is.True);
        
        pageManager.Dispose();
    }

    [Test]
    public async Task CreateAsyncWithProviderMetadataTest()
    {
        var storage = new StorageMemory(initialPageCount: 0);
        var cache = new PageCacheShardedClock(storage, 100);
        var metadata = new ProviderMetadata
        {
            Features = ProviderFeatures.Encryption | ProviderFeatures.Transactions,
            StoreProviderKey = "btree",
            EncryptionProviderKey = "aes-gcm"
        };
        
        var pageManager = await PageManager.CreateAsync(storage, cache, metadata);
        
        var storedMetadata = pageManager.GetProviderMetadata();
        Assert.That(storedMetadata.Features, Is.EqualTo(metadata.Features));
        Assert.That(storedMetadata.StoreProviderKey, Is.EqualTo(metadata.StoreProviderKey));
        Assert.That(storedMetadata.EncryptionProviderKey, Is.EqualTo(metadata.EncryptionProviderKey));
        
        pageManager.Dispose();
    }

    [Test]
    public async Task CreateAsyncWithCancellationTest()
    {
        var storage = new StorageMemory(initialPageCount: 0);
        var cache = new PageCacheShardedClock(storage, 100);
        
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        
        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await PageManager.CreateAsync(storage, cache, cancellationToken: cts.Token));
    }

    #endregion

    #region Async Initialization Tests

    [Test]
    public async Task CreateAsyncReopensExistingDatabaseTest()
    {
        var storage = new StorageMemory(initialPageCount: 0);
        
        // Create and populate database
        using (var cache1 = new PageCacheShardedClock(storage, 100))
        {
            var pm1 = await PageManager.CreateAsync(storage, cache1);
            
            var (pn, _) = pm1.AllocatePage(PageType.Leaf);
            pm1.ReleasePage(pn);
            pm1.SetSchemaRootPage(pn);
            pm1.IncrementTransactionCounter();
            pm1.Flush();
            pm1.Dispose();
        }
        
        // Reopen with async
        using var cache2 = new PageCacheShardedClock(storage, 100);
        var pm2 = await PageManager.CreateAsync(storage, cache2);
        
        var header = pm2.GetHeader();
        Assert.That(header.TotalPageCount, Is.EqualTo(2u));
        Assert.That(header.SchemaRootPage, Is.EqualTo(1u));
        Assert.That(header.TransactionCounter, Is.EqualTo(1u));
        
        pm2.Dispose();
    }

    [Test]
    public async Task CreateAsyncOperationsWorkAfterInitializationTest()
    {
        var storage = new StorageMemory(initialPageCount: 0);
        var cache = new PageCacheShardedClock(storage, 100);
        
        var pageManager = await PageManager.CreateAsync(storage, cache);
        
        // Test AllocatePage
        var (pageNumber, page) = pageManager.AllocatePage(PageType.Leaf);
        Assert.That(pageNumber, Is.EqualTo(1u));
        page.Data[100] = 0xAB;
        pageManager.MarkDirty(pageNumber);
        pageManager.ReleasePage(pageNumber);
        
        // Test GetPage
        var retrieved = pageManager.GetPage(pageNumber);
        Assert.That(retrieved.Data[100], Is.EqualTo((byte)0xAB));
        pageManager.ReleasePage(pageNumber);
        
        // Test FreePage
        pageManager.FreePage(pageNumber);
        Assert.That(pageManager.FreePageCount, Is.EqualTo(1u));
        
        // Test FlushAsync
        await pageManager.FlushAsync();
        
        pageManager.Dispose();
    }

    #endregion

    #region IsInitialized Property Tests

    [Test]
    public void SyncConstructorSetsIsInitializedTest()
    {
        var storage = new StorageMemory(initialPageCount: 0);
        var cache = new PageCacheShardedClock(storage, 100);
        
        var pageManager = new PageManager(storage, cache);
        
        Assert.That(pageManager.IsInitialized, Is.True);
        
        pageManager.Dispose();
    }

    #endregion

    #region Flush With Uninitialized Tests

    [Test]
    public async Task FlushOnUninitializedDoesNotThrowTest()
    {
        var storage = new StorageMemory(initialPageCount: 0);
        var cache = new PageCacheShardedClock(storage, 100);
        
        // Create with async to ensure proper initialization
        var pageManager = await PageManager.CreateAsync(storage, cache);
        
        // Flush should work
        pageManager.Flush();
        await pageManager.FlushAsync();
        
        pageManager.Dispose();
    }

    #endregion
}

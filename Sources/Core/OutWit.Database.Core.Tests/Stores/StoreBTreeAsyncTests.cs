using OutWit.Database.Core.Cache;
using OutWit.Database.Core.Interfaces;
using OutWit.Database.Core.Managers;
using OutWit.Database.Core.Storage;
using OutWit.Database.Core.Stores;

namespace OutWit.Database.Core.Tests.Stores;

[TestFixture]
public class StoreBTreeAsyncTests : IDisposable
{
    #region Fields

    private string? m_testDir;

    #endregion

    #region Setup/Teardown

    [SetUp]
    public void SetUp()
    {
        m_testDir = Path.Combine(Path.GetTempPath(), $"btree_async_test_{Guid.NewGuid():N}");
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
        var storage = new StorageMemory();
        
        await using var store = await StoreBTree.CreateAsync(storage);
        
        Assert.That(store.ProviderKey, Is.EqualTo(StoreBTree.PROVIDER_KEY));
    }

    [Test]
    public async Task CreateAsyncWithFilePathTest()
    {
        var dbPath = Path.Combine(m_testDir!, $"async_btree_{Guid.NewGuid():N}.db");
        
        await using var store = await StoreBTree.CreateAsync(dbPath);
        
        Assert.That(store.ProviderKey, Is.EqualTo(StoreBTree.PROVIDER_KEY));
        Assert.That(File.Exists(dbPath), Is.True);
    }

    [Test]
    public async Task CreateAsyncWithCacheSizeTest()
    {
        var storage = new StorageMemory();
        
        await using var store = await StoreBTree.CreateAsync(storage, cacheSize: 500);
        
        Assert.That(store.ProviderKey, Is.EqualTo(StoreBTree.PROVIDER_KEY));
    }

    [Test]
    public async Task CreateAsyncWithProviderMetadataTest()
    {
        var storage = new StorageMemory();
        var metadata = new ProviderMetadata
        {
            Features = ProviderFeatures.Transactions,
            StoreProviderKey = "btree"
        };
        
        await using var store = await StoreBTree.CreateAsync(storage, cacheSize: 100, ownsStorage: true, metadata);
        
        Assert.That(store.ProviderKey, Is.EqualTo(StoreBTree.PROVIDER_KEY));
    }

    [Test]
    public async Task CreateAsyncWithCancellationTest()
    {
        var storage = new StorageMemory();
        
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        
        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await StoreBTree.CreateAsync(storage, cancellationToken: cts.Token));
    }

    #endregion

    #region CRUD Operations After Async Create Tests

    [Test]
    public async Task PutAndGetAfterAsyncCreateTest()
    {
        var storage = new StorageMemory();
        await using var store = await StoreBTree.CreateAsync(storage);
        
        var key = System.Text.Encoding.UTF8.GetBytes("test-key");
        var value = System.Text.Encoding.UTF8.GetBytes("test-value");
        
        store.Put(key, value);
        
        var retrieved = store.Get(key);
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(System.Text.Encoding.UTF8.GetString(retrieved!), Is.EqualTo("test-value"));
    }

    [Test]
    public async Task PutAsyncAndGetAsyncAfterAsyncCreateTest()
    {
        var storage = new StorageMemory();
        await using var store = await StoreBTree.CreateAsync(storage);
        
        var key = System.Text.Encoding.UTF8.GetBytes("async-key");
        var value = System.Text.Encoding.UTF8.GetBytes("async-value");
        
        await store.PutAsync(key, value);
        
        var retrieved = await store.GetAsync(key);
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(System.Text.Encoding.UTF8.GetString(retrieved!), Is.EqualTo("async-value"));
    }

    [Test]
    public async Task DeleteAfterAsyncCreateTest()
    {
        var storage = new StorageMemory();
        await using var store = await StoreBTree.CreateAsync(storage);
        
        var key = System.Text.Encoding.UTF8.GetBytes("delete-key");
        var value = System.Text.Encoding.UTF8.GetBytes("delete-value");
        
        store.Put(key, value);
        Assert.That(store.Get(key), Is.Not.Null);
        
        var deleted = store.Delete(key);
        Assert.That(deleted, Is.True);
        Assert.That(store.Get(key), Is.Null);
    }

    [Test]
    public async Task ScanAfterAsyncCreateTest()
    {
        var storage = new StorageMemory();
        await using var store = await StoreBTree.CreateAsync(storage);
        
        for (int i = 0; i < 10; i++)
        {
            store.Put(System.Text.Encoding.UTF8.GetBytes($"key:{i:D3}"), System.Text.Encoding.UTF8.GetBytes($"value:{i}"));
        }
        
        var results = store.Scan(null, null).ToList();
        Assert.That(results.Count, Is.EqualTo(10));
    }

    [Test]
    public async Task ScanAsyncAfterAsyncCreateTest()
    {
        var storage = new StorageMemory();
        await using var store = await StoreBTree.CreateAsync(storage);
        
        for (int i = 0; i < 10; i++)
        {
            await store.PutAsync(System.Text.Encoding.UTF8.GetBytes($"key:{i:D3}"), System.Text.Encoding.UTF8.GetBytes($"value:{i}"));
        }
        
        var results = new List<(byte[] Key, byte[] Value)>();
        await foreach (var item in store.ScanAsync(null, null))
        {
            results.Add(item);
        }
        
        Assert.That(results.Count, Is.EqualTo(10));
    }

    #endregion

    #region Flush Tests

    [Test]
    public async Task FlushAfterAsyncCreateTest()
    {
        var storage = new StorageMemory();
        await using var store = await StoreBTree.CreateAsync(storage);
        
        store.Put(System.Text.Encoding.UTF8.GetBytes("key"), System.Text.Encoding.UTF8.GetBytes("value"));
        
        store.Flush();
        
        Assert.Pass();
    }

    [Test]
    public async Task FlushAsyncAfterAsyncCreateTest()
    {
        var storage = new StorageMemory();
        await using var store = await StoreBTree.CreateAsync(storage);
        
        await store.PutAsync(System.Text.Encoding.UTF8.GetBytes("key"), System.Text.Encoding.UTF8.GetBytes("value"));
        
        await store.FlushAsync();
        
        Assert.Pass();
    }

    #endregion

    #region Count and Statistics Tests

    [Test]
    public async Task CountAfterAsyncCreateTest()
    {
        var storage = new StorageMemory();
        await using var store = await StoreBTree.CreateAsync(storage);
        
        Assert.That(store.Count(), Is.EqualTo(0));
        
        for (int i = 0; i < 5; i++)
        {
            store.Put(System.Text.Encoding.UTF8.GetBytes($"key:{i}"), System.Text.Encoding.UTF8.GetBytes($"value:{i}"));
        }
        
        Assert.That(store.Count(), Is.EqualTo(5));
    }

    [Test]
    public async Task ContainsKeyAfterAsyncCreateTest()
    {
        var storage = new StorageMemory();
        await using var store = await StoreBTree.CreateAsync(storage);
        
        var key = System.Text.Encoding.UTF8.GetBytes("exists-key");
        
        Assert.That(store.ContainsKey(key), Is.False);
        
        store.Put(key, System.Text.Encoding.UTF8.GetBytes("value"));
        
        Assert.That(store.ContainsKey(key), Is.True);
    }

    #endregion

    #region Persistence Tests

    [Test]
    public async Task DataPersistsAcrossReopenWithAsyncCreateTest()
    {
        var dbPath = Path.Combine(m_testDir!, $"persist_async_{Guid.NewGuid():N}.db");
        
        // Create and populate
        {
            await using var store = await StoreBTree.CreateAsync(dbPath);
            
            store.Put(System.Text.Encoding.UTF8.GetBytes("persist-key"), System.Text.Encoding.UTF8.GetBytes("persist-value"));
            await store.FlushAsync();
        }
        
        // Reopen and verify
        {
            await using var store = await StoreBTree.CreateAsync(dbPath);
            
            var value = store.Get(System.Text.Encoding.UTF8.GetBytes("persist-key"));
            Assert.That(value, Is.Not.Null);
            Assert.That(System.Text.Encoding.UTF8.GetString(value!), Is.EqualTo("persist-value"));
        }
    }

    #endregion

    #region CreateFromPageManager Tests

    [Test]
    public async Task CreateFromPageManagerTest()
    {
        var storage = new StorageMemory();
        var cache = new PageCacheShardedClock(storage, 100);
        var pageManager = await PageManager.CreateAsync(storage, cache);
        
        var store = StoreBTree.CreateFromPageManager(pageManager);
        
        store.Put(System.Text.Encoding.UTF8.GetBytes("key"), System.Text.Encoding.UTF8.GetBytes("value"));
        
        var value = store.Get(System.Text.Encoding.UTF8.GetBytes("key"));
        Assert.That(value, Is.Not.Null);
        
        store.Dispose();
        pageManager.Dispose();
    }

    #endregion
}

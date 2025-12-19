using OutWit.Database.Core.Managers;
using OutWit.Database.Core.Storage;
using OutWit.Database.Core.Stores;
using TextEncoding = System.Text.Encoding;

namespace OutWit.Database.Core.Tests.Integration;

/// <summary>
/// Integration tests verifying the full stack: Storage -> PageManager -> BTree -> BTreeStore
/// </summary>
[TestFixture]
public class StorageStackIntegrationTest
{
    private string? m_testDir;

    [SetUp]
    public void SetUp()
    {
        m_testDir = Path.Combine(Path.GetTempPath(), $"IntegrationTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(m_testDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (m_testDir != null && Directory.Exists(m_testDir))
        {
            try { Directory.Delete(m_testDir, recursive: true); }
            catch { /* Ignore cleanup errors */ }
        }
    }

    #region Memory Storage Integration

    [Test]
    public void MemoryStorage_FullLifecycle()
    {
        using var storage = new MemoryStorage(4096, 1000);
        using var pageManager = new PageManager(storage);
        using var store = new BTreeStore(pageManager);

        // Write phase
        for (int i = 0; i < 1000; i++)
        {
            store.Put(BitConverter.GetBytes(i), TextEncoding.UTF8.GetBytes($"value_{i}"));
        }

        store.Flush();

        // Read phase
        for (int i = 0; i < 1000; i++)
        {
            var result = store.Get(BitConverter.GetBytes(i));
            Assert.That(result, Is.Not.Null);
            Assert.That(TextEncoding.UTF8.GetString(result!), Is.EqualTo($"value_{i}"));
        }

        // Scan phase
        var all = store.Scan(null, null).ToList();
        Assert.That(all.Count, Is.EqualTo(1000));
    }

    [Test]
    public void MemoryStorage_MultipleTreesSharePageManager()
    {
        using var storage = new MemoryStorage(4096, 2000);
        using var pageManager = new PageManager(storage);

        // Create two separate trees
        using var tree1 = new BTreeStore(pageManager);
        
        // Store tree1's root page
        tree1.Put("tree1_key"u8.ToArray(), "tree1_value"u8.ToArray());
        uint root1 = tree1.RootPageNumber;

        // Create another tree (new root)
        using var tree2 = new BTreeStore(pageManager);
        tree2.Put("tree2_key"u8.ToArray(), "tree2_value"u8.ToArray());

        // Both trees should work independently
        Assert.That(tree1.Get("tree1_key"u8), Is.Not.Null);
        Assert.That(tree2.Get("tree2_key"u8), Is.Not.Null);

        // Cross-check: tree1 shouldn't see tree2's keys
        Assert.That(tree1.Get("tree2_key"u8), Is.Null);
    }

    #endregion

    #region File Storage Integration

    [Test]
    public void FileStorage_CreateAndReopen()
    {
        var dbPath = Path.Combine(m_testDir!, "test.db");

        // Create and populate
        using (var store = new BTreeStore(dbPath))
        {
            for (int i = 0; i < 500; i++)
            {
                store.Put(BitConverter.GetBytes(i), BitConverter.GetBytes(i * 10));
            }
        }

        // Reopen and verify
        using (var store = new BTreeStore(dbPath))
        {
            Assert.That(store.Count(), Is.EqualTo(500));

            for (int i = 0; i < 500; i++)
            {
                var result = store.Get(BitConverter.GetBytes(i));
                Assert.That(BitConverter.ToInt32(result!), Is.EqualTo(i * 10));
            }
        }
    }

    [Test]
    public void FileStorage_ModifyAndReopen()
    {
        var dbPath = Path.Combine(m_testDir!, "modify.db");

        // Phase 1: Create
        using (var store = new BTreeStore(dbPath))
        {
            for (int i = 0; i < 1000; i++)
            {
                store.Put(BitConverter.GetBytes(i), BitConverter.GetBytes(i));
            }
        }

        // Phase 2: Modify
        using (var store = new BTreeStore(dbPath))
        {
            // Delete half
            for (int i = 0; i < 500; i++)
            {
                store.Delete(BitConverter.GetBytes(i));
            }

            // Update remaining
            for (int i = 500; i < 1000; i++)
            {
                store.Put(BitConverter.GetBytes(i), BitConverter.GetBytes(i * 100));
            }

            // Add new
            for (int i = 1000; i < 1500; i++)
            {
                store.Put(BitConverter.GetBytes(i), BitConverter.GetBytes(i));
            }
        }

        // Phase 3: Verify
        using (var store = new BTreeStore(dbPath))
        {
            Assert.That(store.Count(), Is.EqualTo(1000)); // 500-999 + 1000-1499

            // Check deleted
            for (int i = 0; i < 500; i++)
            {
                Assert.That(store.Get(BitConverter.GetBytes(i)), Is.Null);
            }

            // Check updated
            for (int i = 500; i < 1000; i++)
            {
                var result = store.Get(BitConverter.GetBytes(i));
                Assert.That(BitConverter.ToInt32(result!), Is.EqualTo(i * 100));
            }

            // Check new
            for (int i = 1000; i < 1500; i++)
            {
                var result = store.Get(BitConverter.GetBytes(i));
                Assert.That(BitConverter.ToInt32(result!), Is.EqualTo(i));
            }
        }
    }

    [Test]
    public void FileStorage_LargeDataset()
    {
        var dbPath = Path.Combine(m_testDir!, "large.db");
        const int count = 50000;

        // Create large dataset
        using (var store = new BTreeStore(dbPath, pageSize: 4096, cacheSize: 500))
        {
            for (int i = 0; i < count; i++)
            {
                store.Put(
                    TextEncoding.UTF8.GetBytes($"key_{i:D8}"),
                    TextEncoding.UTF8.GetBytes($"value_{i:D8}_with_some_extra_data"));
            }
        }

        // Verify
        using (var store = new BTreeStore(dbPath, pageSize: 4096, cacheSize: 500))
        {
            Assert.That(store.Count(), Is.EqualTo(count));

            // Sample verification
            var random = new Random(42);
            for (int i = 0; i < 100; i++)
            {
                int idx = random.Next(count);
                var result = store.Get(TextEncoding.UTF8.GetBytes($"key_{idx:D8}"));
                Assert.That(result, Is.Not.Null);
                Assert.That(TextEncoding.UTF8.GetString(result!),
                    Is.EqualTo($"value_{idx:D8}_with_some_extra_data"));
            }
        }
    }

    #endregion

    #region Cache Behavior

    [Test]
    public void SmallCache_StillWorks()
    {
        using var storage = new MemoryStorage(4096, 10000);
        using var pageManager = new PageManager(storage, cacheSize: 10); // Very small cache
        using var store = new BTreeStore(pageManager);

        // Insert more data than fits in cache
        const int count = 5000;
        for (int i = 0; i < count; i++)
        {
            store.Put(BitConverter.GetBytes(i), BitConverter.GetBytes(i));
        }

        // All should still be accessible
        for (int i = 0; i < count; i++)
        {
            var result = store.Get(BitConverter.GetBytes(i));
            Assert.That(result, Is.Not.Null, $"Key {i} not found with small cache");
        }
    }

    [Test]
    public void CacheFlush_PersistsAllData()
    {
        using var storage = new MemoryStorage(4096, 1000);
        using var pageManager = new PageManager(storage, cacheSize: 100);

        uint rootPage;

        using (var store = new BTreeStore(pageManager))
        {
            for (int i = 0; i < 1000; i++)
            {
                store.Put(BitConverter.GetBytes(i), BitConverter.GetBytes(i));
            }
            
            store.Flush(); // Explicit flush
            rootPage = store.RootPageNumber;
        }

        // Reopen with same page manager
        using (var store = new BTreeStore(pageManager, rootPage))
        {
            Assert.That(store.Count(), Is.EqualTo(1000));
        }
    }

    #endregion

    #region Overflow Pages Integration

    [Test]
    public void OverflowPages_PersistCorrectly()
    {
        var dbPath = Path.Combine(m_testDir!, "overflow.db");
        var largeValues = new Dictionary<int, byte[]>();
        var random = new Random(42);

        // Create with overflow values
        using (var store = new BTreeStore(dbPath))
        {
            for (int i = 0; i < 100; i++)
            {
                byte[] value = new byte[store.MaxInlineValueSize + 500 + i * 10];
                random.NextBytes(value);
                store.Put(BitConverter.GetBytes(i), value);
                largeValues[i] = value;
            }
        }

        // Reopen and verify
        using (var store = new BTreeStore(dbPath))
        {
            Assert.That(store.Count(), Is.EqualTo(100));

            foreach (var (key, expectedValue) in largeValues)
            {
                var result = store.Get(BitConverter.GetBytes(key));
                Assert.That(result, Is.Not.Null);
                Assert.That(result!.SequenceEqual(expectedValue), Is.True,
                    $"Overflow value mismatch for key {key}");
            }
        }
    }

    [Test]
    public void OverflowPages_DeletedCorrectly()
    {
        using var storage = new MemoryStorage(4096, 2000);
        using var pageManager = new PageManager(storage);
        using var store = new BTreeStore(pageManager);

        var random = new Random(42);

        // Insert overflow values
        for (int i = 0; i < 50; i++)
        {
            byte[] value = new byte[store.MaxInlineValueSize + 1000];
            random.NextBytes(value);
            store.Put(BitConverter.GetBytes(i), value);
        }

        // Delete all
        for (int i = 0; i < 50; i++)
        {
            store.Delete(BitConverter.GetBytes(i));
        }

        Assert.That(store.Count(), Is.EqualTo(0));

        // Insert new values - should reuse freed pages
        for (int i = 100; i < 150; i++)
        {
            byte[] value = new byte[store.MaxInlineValueSize + 500];
            random.NextBytes(value);
            store.Put(BitConverter.GetBytes(i), value);
        }

        Assert.That(store.Count(), Is.EqualTo(50));
    }

    #endregion

    #region Error Recovery Scenarios

    [Test]
    public void DisposedStore_ThrowsOnAccess()
    {
        using var storage = new MemoryStorage();
        var store = new BTreeStore(storage, ownsStorage: false);
        store.Put("key"u8.ToArray(), "value"u8.ToArray());
        store.Dispose();

        Assert.Throws<ObjectDisposedException>(() => store.Get("key"u8));
        Assert.Throws<ObjectDisposedException>(() => store.Put("key"u8.ToArray(), "value"u8.ToArray()));
        Assert.Throws<ObjectDisposedException>(() => store.Delete("key"u8));
    }

    #endregion

    #region Async Integration

    [Test]
    public async Task AsyncOperations_FullWorkflow()
    {
        using var storage = new MemoryStorage(4096, 1000);
        using var store = new BTreeStore(storage);

        // Async put
        for (int i = 0; i < 100; i++)
        {
            await store.PutAsync(BitConverter.GetBytes(i), BitConverter.GetBytes(i));
        }

        // Async get
        for (int i = 0; i < 100; i++)
        {
            var result = await store.GetAsync(BitConverter.GetBytes(i));
            Assert.That(result, Is.Not.Null);
            Assert.That(BitConverter.ToInt32(result!), Is.EqualTo(i));
        }

        // Async scan
        var all = new List<(byte[] Key, byte[] Value)>();
        await foreach (var item in store.ScanAsync(null, null))
        {
            all.Add(item);
        }
        Assert.That(all.Count, Is.EqualTo(100));

        // Async delete
        for (int i = 0; i < 50; i++)
        {
            var deleted = await store.DeleteAsync(BitConverter.GetBytes(i));
            Assert.That(deleted, Is.True);
        }

        // Async flush
        await store.FlushAsync();

        Assert.That(store.Count(), Is.EqualTo(50));
    }

    #endregion
}

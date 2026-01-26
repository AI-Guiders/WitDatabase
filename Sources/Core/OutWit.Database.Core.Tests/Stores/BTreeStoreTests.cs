using OutWit.Database.Core.Managers;
using OutWit.Database.Core.Storage;
using OutWit.Database.Core.Stores;
using OutWit.Database.Core.Interfaces;
using TextEncoding = System.Text.Encoding;

namespace OutWit.Database.Core.Tests.Stores;

[TestFixture]
public class StoreBTreeTest
{
    private string? m_testDir;

    [SetUp]
    public void SetUp()
    {
        m_testDir = Path.Combine(Path.GetTempPath(), $"BTreeStoreTest_{Guid.NewGuid():N}");
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

    #region Basic Operations

    [Test]
    public void PutAndGetTest()
    {
        using var storage = new StorageMemory();
        using var store = new StoreBTree(storage);
        
        byte[] key = "key"u8.ToArray();
        byte[] value = "value"u8.ToArray();
        
        store.Put(key, value);
        
        var result = store.Get(key);
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.EqualTo(value));
    }

    [Test]
    public void GetNonExistentKeyReturnsNullTest()
    {
        using var storage = new StorageMemory();
        using var store = new StoreBTree(storage);
        
        var result = store.Get("missing"u8);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void PutUpdatesExistingKeyTest()
    {
        using var storage = new StorageMemory();
        using var store = new StoreBTree(storage);
        
        byte[] key = "key"u8.ToArray();
        store.Put(key, "value1"u8.ToArray());
        store.Put(key, "value2"u8.ToArray());
        
        var result = store.Get(key);
        Assert.That(TextEncoding.UTF8.GetString(result!), Is.EqualTo("value2"));
        Assert.That(store.Count(), Is.EqualTo(1));
    }

    [Test]
    public void DeleteTest()
    {
        using var storage = new StorageMemory();
        using var store = new StoreBTree(storage);
        
        byte[] key = "key"u8.ToArray();
        store.Put(key, "value"u8.ToArray());
        
        Assert.That(store.Delete(key), Is.True);
        Assert.That(store.Get(key), Is.Null);
        Assert.That(store.Count(), Is.EqualTo(0));
    }

    [Test]
    public void DeleteNonExistentReturnsFalseTest()
    {
        using var storage = new StorageMemory();
        using var store = new StoreBTree(storage);
        
        Assert.That(store.Delete("missing"u8), Is.False);
    }

    [Test]
    public void ContainsKeyTest()
    {
        using var storage = new StorageMemory();
        using var store = new StoreBTree(storage);
        
        byte[] key = "key"u8.ToArray();
        store.Put(key, "value"u8.ToArray());
        
        Assert.That(store.ContainsKey(key), Is.True);
        Assert.That(store.ContainsKey("missing"u8), Is.False);
    }

    #endregion

    #region Scan Operations

    [Test]
    public void ScanAllTest()
    {
        using var storage = new StorageMemory();
        using var store = new StoreBTree(storage);
        
        store.Put("c"u8.ToArray(), "3"u8.ToArray());
        store.Put("a"u8.ToArray(), "1"u8.ToArray());
        store.Put("b"u8.ToArray(), "2"u8.ToArray());
        
        var all = store.Scan(null, null).ToList();
        
        Assert.That(all.Count, Is.EqualTo(3));
        Assert.That(all[0].Key[0], Is.EqualTo((byte)'a'));
        Assert.That(all[1].Key[0], Is.EqualTo((byte)'b'));
        Assert.That(all[2].Key[0], Is.EqualTo((byte)'c'));
    }

    [Test]
    public void ScanRangeTest()
    {
        using var storage = new StorageMemory();
        using var store = new StoreBTree(storage);
        
        for (int i = 0; i < 26; i++)
        {
            char c = (char)('a' + i);
            store.Put([(byte)c], [(byte)c]);
        }
        
        // Use ScanInclusive for inclusive end range (d, e, f, g)
        var range = store.ScanInclusive([(byte)'d'], [(byte)'g']).ToList();
        
        Assert.That(range.Count, Is.EqualTo(4)); // d, e, f, g
        Assert.That(range[0].Key[0], Is.EqualTo((byte)'d'));
        Assert.That(range[3].Key[0], Is.EqualTo((byte)'g'));
    }

    [Test]
    public void ScanRangeExclusiveEndTest()
    {
        using var storage = new StorageMemory();
        using var store = new StoreBTree(storage);
        
        for (int i = 0; i < 26; i++)
        {
            char c = (char)('a' + i);
            store.Put([(byte)c], [(byte)c]);
        }
        
        // Scan with exclusive end: d to h (exclusive) = d, e, f, g
        var range = store.Scan([(byte)'d'], [(byte)'h']).ToList();
        
        Assert.That(range.Count, Is.EqualTo(4)); // d, e, f, g (h excluded)
        Assert.That(range[0].Key[0], Is.EqualTo((byte)'d'));
        Assert.That(range[3].Key[0], Is.EqualTo((byte)'g'));
    }

    #endregion

    #region Async Operations

    [Test]
    public async Task GetAsyncTest()
    {
        using var storage = new StorageMemory();
        using var store = new StoreBTree(storage);
        
        byte[] key = "key"u8.ToArray();
        byte[] value = "value"u8.ToArray();
        
        await store.PutAsync(key, value);
        
        var result = await store.GetAsync(key);
        Assert.That(result, Is.EqualTo(value));
    }

    [Test]
    public async Task DeleteAsyncTest()
    {
        using var storage = new StorageMemory();
        using var store = new StoreBTree(storage);
        
        byte[] key = "key"u8.ToArray();
        await store.PutAsync(key, "value"u8.ToArray());
        
        var deleted = await store.DeleteAsync(key);
        Assert.That(deleted, Is.True);
        
        var result = await store.GetAsync(key);
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task ScanAsyncTest()
    {
        using var storage = new StorageMemory();
        using var store = new StoreBTree(storage);
        
        for (int i = 0; i < 10; i++)
        {
            await store.PutAsync(
                TextEncoding.UTF8.GetBytes($"key{i:D2}"),
                TextEncoding.UTF8.GetBytes($"value{i}"));
        }
        
        var results = new List<(byte[] Key, byte[] Value)>();
        await foreach (var item in store.ScanAsync(null, null))
        {
            results.Add(item);
        }
        
        Assert.That(results.Count, Is.EqualTo(10));
    }

    #endregion

    #region Persistence Tests

    [Test]
    public void FileStoragePersistenceTest()
    {
        var dbPath = Path.Combine(m_testDir!, "test.db");
        
        // Create and populate
        using (var store = new StoreBTree(dbPath))
        {
            for (int i = 0; i < 100; i++)
            {
                store.Put(
                    TextEncoding.UTF8.GetBytes($"key{i:D3}"),
                    TextEncoding.UTF8.GetBytes($"value{i}"));
            }
            store.Flush();
        }
        
        // Reopen and verify
        using (var store = new StoreBTree(dbPath))
        {
            Assert.That(store.Count(), Is.EqualTo(100));
            
            for (int i = 0; i < 100; i++)
            {
                var result = store.Get(TextEncoding.UTF8.GetBytes($"key{i:D3}"));
                Assert.That(result, Is.Not.Null);
                Assert.That(TextEncoding.UTF8.GetString(result!), Is.EqualTo($"value{i}"));
            }
        }
    }

    [Test]
    public void FlushPersistsDataTest()
    {
        var dbPath = Path.Combine(m_testDir!, "flush.db");
        
        using (var store = new StoreBTree(dbPath))
        {
            store.Put("key"u8.ToArray(), "value"u8.ToArray());
            // No explicit flush - disposed should flush
        }
        
        using (var store = new StoreBTree(dbPath))
        {
            var result = store.Get("key"u8);
            Assert.That(TextEncoding.UTF8.GetString(result!), Is.EqualTo("value"));
        }
    }

    #endregion

    #region Large Values (Overflow)

    [Test]
    public void LargeValueTest()
    {
        using var storage = new StorageMemory();
        using var store = new StoreBTree(storage);
        
        byte[] key = "large"u8.ToArray();
        byte[] value = new byte[store.MaxInlineValueSize + 1000];
        Random.Shared.NextBytes(value);
        
        store.Put(key, value);
        
        var result = store.Get(key);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.SequenceEqual(value), Is.True);
    }

    [Test]
    public void UpdateFromSmallToLargeValueTest()
    {
        using var storage = new StorageMemory();
        using var store = new StoreBTree(storage);
        
        byte[] key = "key"u8.ToArray();
        byte[] smallValue = "small"u8.ToArray();
        byte[] largeValue = new byte[store.MaxInlineValueSize + 500];
        Random.Shared.NextBytes(largeValue);
        
        store.Put(key, smallValue);
        store.Put(key, largeValue);
        
        var result = store.Get(key);
        Assert.That(result!.SequenceEqual(largeValue), Is.True);
    }

    #endregion

    #region IKeyValueStore Interface Compliance

    [Test]
    public void ImplementsIKeyValueStoreTest()
    {
        using var storage = new StorageMemory();
        using var store = new StoreBTree(storage);
        
        // Should compile - proves it implements the interface
        IKeyValueStore kvStore = store;
        
        kvStore.Put("key"u8.ToArray(), "value"u8.ToArray());
        var result = kvStore.Get("key"u8);
        Assert.That(result, Is.Not.Null);
    }

    #endregion

    #region Constructor Tests

    [Test]
    public void ConstructorWithPageManagerTest()
    {
        using var storage = new StorageMemory();
        using var pageManager = new PageManager(storage);
        using var store = new StoreBTree(pageManager);
        
        store.Put("key"u8.ToArray(), "value"u8.ToArray());
        Assert.That(store.Get("key"u8), Is.Not.Null);
    }

    [Test]
    public void ConstructorWithExistingRootPageTest()
    {
        using var storage = new StorageMemory();
        using var pageManager = new PageManager(storage);
        
        uint rootPage;
        using (var store1 = new StoreBTree(pageManager))
        {
            store1.Put("key"u8.ToArray(), "value"u8.ToArray());
            rootPage = store1.RootPageNumber;
        }
        
        using var store2 = new StoreBTree(pageManager, rootPage);
        Assert.That(store2.Get("key"u8), Is.Not.Null);
    }

    #endregion

    #region Stress Tests

    [Test]
    public void ManyOperationsTest()
    {
        using var storage = new StorageMemory(4096, 2000);
        using var store = new StoreBTree(storage);
        
        const int count = 1000;
        
        // Insert
        for (int i = 0; i < count; i++)
        {
            store.Put(BitConverter.GetBytes(i), BitConverter.GetBytes(i * 10));
        }
        
        Assert.That(store.Count(), Is.EqualTo(count));
        
        // Update half
        for (int i = 0; i < count / 2; i++)
        {
            store.Put(BitConverter.GetBytes(i), BitConverter.GetBytes(i * 100));
        }
        
        Assert.That(store.Count(), Is.EqualTo(count));
        
        // Delete quarter
        for (int i = 0; i < count / 4; i++)
        {
            store.Delete(BitConverter.GetBytes(i));
        }
        
        Assert.That(store.Count(), Is.EqualTo(count - count / 4));
        
        // Verify remaining
        for (int i = count / 4; i < count; i++)
        {
            var result = store.Get(BitConverter.GetBytes(i));
            Assert.That(result, Is.Not.Null);
        }
    }

    #endregion
}

using OutWit.Database.Core.Managers;
using OutWit.Database.Core.Storage;
using OutWit.Database.Core.Tree;
using TextEncoding = System.Text.Encoding;

namespace OutWit.Database.Core.Tests.Tree;

[TestFixture]
public class BTreeTest
{
    private MemoryStorage m_storage = null!;
    private PageManager m_pageManager = null!;

    [SetUp]
    public void SetUp()
    {
        m_storage = new MemoryStorage(4096, 1000);
        m_pageManager = new PageManager(m_storage);
    }

    [TearDown]
    public void TearDown()
    {
        m_pageManager?.Dispose();
        m_storage?.Dispose();
    }

    #region Basic Operations

    [Test]
    public void InsertAndSearchSingleKeyTest()
    {
        using var tree = new BTree(m_pageManager);
        
        byte[] key = TextEncoding.UTF8.GetBytes("hello");
        byte[] value = TextEncoding.UTF8.GetBytes("world");
        
        bool inserted = tree.Insert(key, value);
        Assert.That(inserted, Is.True);
        
        byte[]? result = tree.Search(key);
        Assert.That(result, Is.Not.Null);
        Assert.That(TextEncoding.UTF8.GetString(result!), Is.EqualTo("world"));
    }

    [Test]
    public void SearchNonExistentKeyReturnsNullTest()
    {
        using var tree = new BTree(m_pageManager);
        
        byte[] key = TextEncoding.UTF8.GetBytes("missing");
        byte[]? result = tree.Search(key);
        
        Assert.That(result, Is.Null);
    }

    [Test]
    public void InsertDuplicateKeyReturnsFalseTest()
    {
        using var tree = new BTree(m_pageManager);
        
        byte[] key = TextEncoding.UTF8.GetBytes("key");
        byte[] value1 = TextEncoding.UTF8.GetBytes("value1");
        byte[] value2 = TextEncoding.UTF8.GetBytes("value2");
        
        Assert.That(tree.Insert(key, value1), Is.True);
        Assert.That(tree.Insert(key, value2), Is.False);
        
        // Original value should be unchanged
        var result = tree.Search(key);
        Assert.That(TextEncoding.UTF8.GetString(result!), Is.EqualTo("value1"));
    }

    [Test]
    public void ContainsKeyTest()
    {
        using var tree = new BTree(m_pageManager);
        
        byte[] key = TextEncoding.UTF8.GetBytes("exists");
        tree.Insert(key, "value"u8.ToArray());
        
        Assert.That(tree.ContainsKey(key), Is.True);
        Assert.That(tree.ContainsKey("missing"u8.ToArray()), Is.False);
    }

    [Test]
    public void DeleteKeyTest()
    {
        using var tree = new BTree(m_pageManager);
        
        byte[] key = TextEncoding.UTF8.GetBytes("delete-me");
        tree.Insert(key, "value"u8.ToArray());
        
        Assert.That(tree.ContainsKey(key), Is.True);
        
        bool deleted = tree.Delete(key);
        Assert.That(deleted, Is.True);
        Assert.That(tree.ContainsKey(key), Is.False);
    }

    [Test]
    public void DeleteNonExistentKeyReturnsFalseTest()
    {
        using var tree = new BTree(m_pageManager);
        
        bool deleted = tree.Delete("missing"u8.ToArray());
        Assert.That(deleted, Is.False);
    }

    [Test]
    public void EmptyKeyThrowsTest()
    {
        using var tree = new BTree(m_pageManager);
        
        Assert.Throws<ArgumentException>(() => tree.Insert([], "value"u8.ToArray()));
        Assert.Throws<ArgumentException>(() => tree.Search([]));
        Assert.Throws<ArgumentException>(() => tree.ContainsKey([]));
        Assert.Throws<ArgumentException>(() => tree.Delete([]));
    }

    [Test]
    public void KeyTooLargeThrowsTest()
    {
        using var tree = new BTree(m_pageManager);
        
        byte[] largeKey = new byte[BTree.MAX_KEY_SIZE + 1];
        Assert.Throws<ArgumentException>(() => tree.Insert(largeKey, "value"u8.ToArray()));
    }

    #endregion

    #region Upsert Tests

    [Test]
    public void UpsertInsertsNewKeyTest()
    {
        using var tree = new BTree(m_pageManager);
        
        byte[] key = "key"u8.ToArray();
        byte[] value = "value"u8.ToArray();
        
        bool inserted = tree.Upsert(key, value);
        Assert.That(inserted, Is.True);
        Assert.That(tree.Count(), Is.EqualTo(1));
        
        var result = tree.Search(key);
        Assert.That(result, Is.EqualTo(value));
    }

    [Test]
    public void UpsertUpdatesExistingKeyTest()
    {
        using var tree = new BTree(m_pageManager);
        
        byte[] key = "key"u8.ToArray();
        tree.Insert(key, "value1"u8.ToArray());
        
        bool inserted = tree.Upsert(key, "value2"u8.ToArray());
        Assert.That(inserted, Is.False); // Was update, not insert
        Assert.That(tree.Count(), Is.EqualTo(1)); // Count unchanged
        
        var result = tree.Search(key);
        Assert.That(TextEncoding.UTF8.GetString(result!), Is.EqualTo("value2"));
    }

    #endregion

    #region Multiple Keys

    [Test]
    public void InsertMultipleKeysTest()
    {
        using var tree = new BTree(m_pageManager);
        
        for (int i = 0; i < 100; i++)
        {
            byte[] key = TextEncoding.UTF8.GetBytes($"key{i:D3}");
            byte[] value = TextEncoding.UTF8.GetBytes($"value{i}");
            Assert.That(tree.Insert(key, value), Is.True);
        }
        
        Assert.That(tree.Count(), Is.EqualTo(100));
        
        // Verify all keys
        for (int i = 0; i < 100; i++)
        {
            byte[] key = TextEncoding.UTF8.GetBytes($"key{i:D3}");
            var result = tree.Search(key);
            Assert.That(result, Is.Not.Null);
            Assert.That(TextEncoding.UTF8.GetString(result!), Is.EqualTo($"value{i}"));
        }
    }

    [Test]
    public void InsertKeysInReverseOrderTest()
    {
        using var tree = new BTree(m_pageManager);
        
        for (int i = 99; i >= 0; i--)
        {
            byte[] key = TextEncoding.UTF8.GetBytes($"key{i:D3}");
            byte[] value = TextEncoding.UTF8.GetBytes($"value{i}");
            tree.Insert(key, value);
        }
        
        Assert.That(tree.Count(), Is.EqualTo(100));
        
        // Keys should still be searchable
        for (int i = 0; i < 100; i++)
        {
            Assert.That(tree.ContainsKey(TextEncoding.UTF8.GetBytes($"key{i:D3}")), Is.True);
        }
    }

    [Test]
    public void InsertRandomKeysTest()
    {
        using var tree = new BTree(m_pageManager);
        
        var random = new Random(42);
        var keys = Enumerable.Range(0, 500)
            .Select(i => TextEncoding.UTF8.GetBytes($"key{random.Next(10000):D5}"))
            .Distinct(new ByteArrayEqualityComparer())
            .ToList();
        
        foreach (var key in keys)
        {
            tree.Insert(key, key);
        }
        
        foreach (var key in keys)
        {
            Assert.That(tree.ContainsKey(key), Is.True, $"Key {TextEncoding.UTF8.GetString(key)} not found");
        }
    }

    #endregion

    #region Range Scan

    [Test]
    public void GetAllReturnsAllKeysInOrderTest()
    {
        using var tree = new BTree(m_pageManager);
        
        // Insert in random order
        var keys = new[] { "cherry", "apple", "date", "banana" };
        foreach (var k in keys)
        {
            tree.Insert(TextEncoding.UTF8.GetBytes(k), TextEncoding.UTF8.GetBytes(k.ToUpper()));
        }
        
        var all = tree.GetAll().ToList();
        Assert.That(all.Count, Is.EqualTo(4));
        
        // Should be in sorted order
        var sortedKeys = keys.OrderBy(k => k).ToArray();
        for (int i = 0; i < 4; i++)
        {
            Assert.That(TextEncoding.UTF8.GetString(all[i].Key), Is.EqualTo(sortedKeys[i]));
        }
    }

    [Test]
    public void GetRangeReturnsCorrectSubsetTest()
    {
        using var tree = new BTree(m_pageManager);
        
        for (int i = 0; i < 26; i++)
        {
            char c = (char)('a' + i);
            tree.Insert([(byte)c], [(byte)c]);
        }
        
        // Range from 'f' to 'j' (inclusive) - use GetRangeInclusive
        var range = tree.GetRangeInclusive([(byte)'f'], [(byte)'j']).ToList();
        
        Assert.That(range.Count, Is.EqualTo(5)); // f, g, h, i, j
        Assert.That(range[0].Key[0], Is.EqualTo((byte)'f'));
        Assert.That(range[4].Key[0], Is.EqualTo((byte)'j'));
    }

    [Test]
    public void GetRangeExclusiveEndTest()
    {
        using var tree = new BTree(m_pageManager);
        
        for (int i = 0; i < 26; i++)
        {
            char c = (char)('a' + i);
            tree.Insert([(byte)c], [(byte)c]);
        }
        
        // Range from 'f' to 'k' (exclusive end) - should get f, g, h, i, j
        var range = tree.GetRange([(byte)'f'], [(byte)'k']).ToList();
        
        Assert.That(range.Count, Is.EqualTo(5)); // f, g, h, i, j (k excluded)
        Assert.That(range[0].Key[0], Is.EqualTo((byte)'f'));
        Assert.That(range[4].Key[0], Is.EqualTo((byte)'j'));
    }

    #endregion

    #region Split Tests (Force Page Splits)

    [Test]
    public void InsertManyKeysForcesPageSplitTest()
    {
        using var tree = new BTree(m_pageManager);
        
        // Insert enough keys to force multiple page splits
        for (int i = 0; i < 500; i++)
        {
            byte[] key = new byte[20];
            byte[] value = new byte[30];
            BitConverter.TryWriteBytes(key, i);
            BitConverter.TryWriteBytes(value, i * 2);
            
            tree.Insert(key, value);
        }
        
        Assert.That(tree.Count(), Is.EqualTo(500));
        
        // Verify all keys are still findable
        for (int i = 0; i < 500; i++)
        {
            byte[] key = new byte[20];
            BitConverter.TryWriteBytes(key, i);
            Assert.That(tree.ContainsKey(key), Is.True, $"Key {i} not found after splits");
        }
    }

    [Test]
    public void InsertManyKeysForcesInternalNodeSplitTest()
    {
        using var tree = new BTree(m_pageManager);
        
        // Insert enough keys to force internal node splits
        // Need many keys with small values to create deep tree
        for (int i = 0; i < 2000; i++)
        {
            byte[] key = BitConverter.GetBytes(i);
            byte[] value = BitConverter.GetBytes(i);
            tree.Insert(key, value);
        }
        
        Assert.That(tree.Count(), Is.EqualTo(2000));
        
        // Verify all keys
        for (int i = 0; i < 2000; i++)
        {
            byte[] key = BitConverter.GetBytes(i);
            var result = tree.Search(key);
            Assert.That(result, Is.Not.Null, $"Key {i} not found");
            Assert.That(BitConverter.ToInt32(result!), Is.EqualTo(i));
        }
    }

    #endregion

    #region Overflow Tests

    [Test]
    public void LargeValueUsesOverflowTest()
    {
        using var tree = new BTree(m_pageManager);
        
        byte[] key = "large-value-key"u8.ToArray();
        byte[] largeValue = new byte[tree.MaxInlineValueSize + 100];
        Random.Shared.NextBytes(largeValue);
        
        Assert.That(tree.Insert(key, largeValue), Is.True);
        
        var result = tree.Search(key);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Length, Is.EqualTo(largeValue.Length));
        Assert.That(result.SequenceEqual(largeValue), Is.True);
    }

    [Test]
    public void MultipleLargeValuesTest()
    {
        using var tree = new BTree(m_pageManager);
        
        var entries = new Dictionary<string, byte[]>();
        
        for (int i = 0; i < 20; i++)
        {
            string keyStr = $"key{i:D3}";
            byte[] key = TextEncoding.UTF8.GetBytes(keyStr);
            byte[] value = new byte[tree.MaxInlineValueSize + i * 100];
            Random.Shared.NextBytes(value);
            
            tree.Insert(key, value);
            entries[keyStr] = value;
        }
        
        Assert.That(tree.Count(), Is.EqualTo(20));
        
        foreach (var (keyStr, expectedValue) in entries)
        {
            var result = tree.Search(TextEncoding.UTF8.GetBytes(keyStr));
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.SequenceEqual(expectedValue), Is.True, $"Value mismatch for {keyStr}");
        }
    }

    [Test]
    public void DeleteOverflowValueTest()
    {
        using var tree = new BTree(m_pageManager);
        
        byte[] key = "overflow-delete"u8.ToArray();
        byte[] largeValue = new byte[tree.MaxInlineValueSize + 500];
        Random.Shared.NextBytes(largeValue);
        
        tree.Insert(key, largeValue);
        Assert.That(tree.ContainsKey(key), Is.True);
        
        bool deleted = tree.Delete(key);
        Assert.That(deleted, Is.True);
        Assert.That(tree.ContainsKey(key), Is.False);
        Assert.That(tree.Count(), Is.EqualTo(0));
    }

    [Test]
    public void UpsertOverflowValueTest()
    {
        using var tree = new BTree(m_pageManager);
        
        byte[] key = "upsert-overflow"u8.ToArray();
        byte[] smallValue = "small"u8.ToArray();
        byte[] largeValue = new byte[tree.MaxInlineValueSize + 200];
        Random.Shared.NextBytes(largeValue);
        
        // Insert small value
        tree.Insert(key, smallValue);
        
        // Upsert to large value
        tree.Upsert(key, largeValue);
        
        var result = tree.Search(key);
        Assert.That(result!.SequenceEqual(largeValue), Is.True);
        
        // Upsert back to small value
        tree.Upsert(key, smallValue);
        
        result = tree.Search(key);
        Assert.That(TextEncoding.UTF8.GetString(result!), Is.EqualTo("small"));
    }

    [Test]
    public void RangeScanWithOverflowValuesTest()
    {
        using var tree = new BTree(m_pageManager);
        
        // Mix of small and large values
        for (int i = 0; i < 10; i++)
        {
            byte[] key = TextEncoding.UTF8.GetBytes($"key{i:D2}");
            byte[] value = i % 2 == 0 
                ? new byte[tree.MaxInlineValueSize + 100] 
                : TextEncoding.UTF8.GetBytes($"small{i}");
            
            if (i % 2 == 0)
                Random.Shared.NextBytes(value);
            
            tree.Insert(key, value);
        }
        
        var all = tree.GetAll().ToList();
        Assert.That(all.Count, Is.EqualTo(10));
        
        // Verify order
        for (int i = 0; i < 10; i++)
        {
            Assert.That(TextEncoding.UTF8.GetString(all[i].Key), Is.EqualTo($"key{i:D2}"));
        }
    }

    #endregion

    #region Integer Keys

    [Test]
    public void IntegerKeysTest()
    {
        using var tree = new BTree(m_pageManager);
        
        for (int i = 0; i < 100; i++)
        {
            byte[] key = BitConverter.GetBytes(i);
            byte[] value = BitConverter.GetBytes(i * 10);
            tree.Insert(key, value);
        }
        
        for (int i = 0; i < 100; i++)
        {
            byte[] key = BitConverter.GetBytes(i);
            var result = tree.Search(key);
            Assert.That(result, Is.Not.Null);
            Assert.That(BitConverter.ToInt32(result!), Is.EqualTo(i * 10));
        }
    }

    #endregion

    #region Persistence

    [Test]
    public void ReopenTreeAfterFlushTest()
    {
        uint rootPage;
        
        // Create and populate tree
        using (var tree = new BTree(m_pageManager))
        {
            for (int i = 0; i < 50; i++)
            {
                byte[] key = TextEncoding.UTF8.GetBytes($"key{i:D3}");
                byte[] value = TextEncoding.UTF8.GetBytes($"value{i}");
                tree.Insert(key, value);
            }
            
            rootPage = tree.RootPageNumber;
        }
        
        // Reopen with same root page
        using (var tree = new BTree(m_pageManager, rootPage))
        {
            Assert.That(tree.Count(), Is.EqualTo(50));
            
            // Verify keys
            for (int i = 0; i < 50; i++)
            {
                byte[] key = TextEncoding.UTF8.GetBytes($"key{i:D3}");
                Assert.That(tree.ContainsKey(key), Is.True);
            }
        }
    }

    [Test]
    public void SchemaRootPageUpdatedTest()
    {
        using var tree = new BTree(m_pageManager);
        
        // Insert enough to cause root split
        for (int i = 0; i < 500; i++)
        {
            tree.Insert(BitConverter.GetBytes(i), BitConverter.GetBytes(i));
        }
        
        // Schema root page should match tree root
        var header = m_pageManager.GetHeader();
        Assert.That(header.SchemaRootPage, Is.EqualTo(tree.RootPageNumber));
    }

    #endregion

    #region Count Tests

    [Test]
    public void CountIsAccurateAfterOperationsTest()
    {
        using var tree = new BTree(m_pageManager);
        
        Assert.That(tree.Count(), Is.EqualTo(0));
        
        // Insert
        for (int i = 0; i < 100; i++)
        {
            tree.Insert(BitConverter.GetBytes(i), BitConverter.GetBytes(i));
        }
        Assert.That(tree.Count(), Is.EqualTo(100));
        
        // Delete some
        for (int i = 0; i < 30; i++)
        {
            tree.Delete(BitConverter.GetBytes(i));
        }
        Assert.That(tree.Count(), Is.EqualTo(70));
        
        // Upsert (update existing)
        tree.Upsert(BitConverter.GetBytes(50), "updated"u8.ToArray());
        Assert.That(tree.Count(), Is.EqualTo(70)); // No change
        
        // Upsert (insert new)
        tree.Upsert(BitConverter.GetBytes(1000), "new"u8.ToArray());
        Assert.That(tree.Count(), Is.EqualTo(71));
    }

    #endregion

    #region Stress Tests

    [Test]
    public void InsertDeleteStressTest()
    {
        using var tree = new BTree(m_pageManager);
        
        var random = new Random(42);
        var existingKeys = new HashSet<int>();
        
        for (int round = 0; round < 10; round++)
        {
            // Insert 100 random keys
            for (int i = 0; i < 100; i++)
            {
                int keyInt = random.Next(10000);
                if (existingKeys.Add(keyInt))
                {
                    tree.Insert(BitConverter.GetBytes(keyInt), BitConverter.GetBytes(keyInt));
                }
            }
            
            // Delete ~30% of keys
            var toDelete = existingKeys.Where(_ => random.Next(100) < 30).ToList();
            foreach (var keyInt in toDelete)
            {
                tree.Delete(BitConverter.GetBytes(keyInt));
                existingKeys.Remove(keyInt);
            }
            
            Assert.That(tree.Count(), Is.EqualTo(existingKeys.Count));
        }
        
        // Verify remaining keys
        foreach (var keyInt in existingKeys)
        {
            Assert.That(tree.ContainsKey(BitConverter.GetBytes(keyInt)), Is.True);
        }
    }

    [Test]
    public void LargeDatasetTest()
    {
        using var tree = new BTree(m_pageManager);
        
        const int count = 5000;
        
        for (int i = 0; i < count; i++)
        {
            byte[] key = TextEncoding.UTF8.GetBytes($"key{i:D6}");
            byte[] value = TextEncoding.UTF8.GetBytes($"value{i}");
            tree.Insert(key, value);
        }
        
        Assert.That(tree.Count(), Is.EqualTo(count));
        
        // Verify random sample
        var random = new Random(42);
        for (int i = 0; i < 100; i++)
        {
            int idx = random.Next(count);
            byte[] key = TextEncoding.UTF8.GetBytes($"key{idx:D6}");
            var result = tree.Search(key);
            Assert.That(result, Is.Not.Null);
            Assert.That(TextEncoding.UTF8.GetString(result!), Is.EqualTo($"value{idx}"));
        }
    }

    #endregion

    #region Additional Tests for New Features

    [Test]
    public void DeleteAllEntriesTest()
    {
        using var tree = new BTree(m_pageManager);
        
        for (int i = 0; i < 100; i++)
        {
            tree.Insert(BitConverter.GetBytes(i), BitConverter.GetBytes(i));
        }
        
        Assert.That(tree.Count(), Is.EqualTo(100));
        
        // Delete all
        for (int i = 0; i < 100; i++)
        {
            Assert.That(tree.Delete(BitConverter.GetBytes(i)), Is.True);
        }
        
        Assert.That(tree.Count(), Is.EqualTo(0));
        Assert.That(tree.GetAll().ToList(), Is.Empty);
    }

    [Test]
    public void VeryDeepTreeTest()
    {
        using var tree = new BTree(m_pageManager);
        
        const int count = 10000;
        
        // Insert many entries with small keys to force many splits
        for (int i = 0; i < count; i++)
        {
            byte[] key = BitConverter.GetBytes(i);
            byte[] value = BitConverter.GetBytes(i * 2);
            tree.Insert(key, value);
        }
        
        Assert.That(tree.Count(), Is.EqualTo(count));
        
        // Verify random samples
        var random = new Random(42);
        for (int i = 0; i < 100; i++)
        {
            int idx = random.Next(count);
            var result = tree.Search(BitConverter.GetBytes(idx));
            Assert.That(result, Is.Not.Null, $"Key {idx} not found");
            Assert.That(BitConverter.ToInt32(result!), Is.EqualTo(idx * 2));
        }
    }

    [Test]
    public void ReopenTreeCountIsPersistedTest()
    {
        uint rootPage;
        
        using (var tree = new BTree(m_pageManager))
        {
            for (int i = 0; i < 100; i++)
            {
                tree.Insert(BitConverter.GetBytes(i), BitConverter.GetBytes(i));
            }
            
            // Delete some to make sure count is updated
            for (int i = 0; i < 30; i++)
            {
                tree.Delete(BitConverter.GetBytes(i));
            }
            
            Assert.That(tree.Count(), Is.EqualTo(70));
            rootPage = tree.RootPageNumber;
        }
        
        // Reopen and verify count is persisted
        using (var tree = new BTree(m_pageManager, rootPage))
        {
            Assert.That(tree.Count(), Is.EqualTo(70));
        }
    }

    [Test]
    public void ConcurrentReadsDuringIterationTest()
    {
        using var tree = new BTree(m_pageManager);
        
        for (int i = 0; i < 100; i++)
        {
            tree.Insert(BitConverter.GetBytes(i), BitConverter.GetBytes(i));
        }
        
        // Iterate and do reads at the same time
        foreach (var (key, value) in tree.GetAll())
        {
            // This should not cause issues - reads during iteration
            Assert.That(tree.ContainsKey(key), Is.True);
            var result = tree.Search(key);
            Assert.That(result, Is.EqualTo(value));
        }
    }

    [Test]
    public void GetRangeWithNullStartTest()
    {
        using var tree = new BTree(m_pageManager);
        
        for (int i = 0; i < 10; i++)
        {
            tree.Insert(BitConverter.GetBytes(i), BitConverter.GetBytes(i));
        }
        
        // Range from beginning to key 5 (exclusive)
        var range = tree.GetRange(null, BitConverter.GetBytes(5)).ToList();
        
        Assert.That(range.Count, Is.EqualTo(5)); // 0, 1, 2, 3, 4
        Assert.That(BitConverter.ToInt32(range[0].Key), Is.EqualTo(0));
        Assert.That(BitConverter.ToInt32(range[4].Key), Is.EqualTo(4));
    }

    [Test]
    public void GetRangeWithNullEndTest()
    {
        using var tree = new BTree(m_pageManager);
        
        for (int i = 0; i < 10; i++)
        {
            tree.Insert(BitConverter.GetBytes(i), BitConverter.GetBytes(i));
        }
        
        // Range from key 5 to end
        var range = tree.GetRange(BitConverter.GetBytes(5), null).ToList();
        
        Assert.That(range.Count, Is.EqualTo(5)); // 5, 6, 7, 8, 9
        Assert.That(BitConverter.ToInt32(range[0].Key), Is.EqualTo(5));
        Assert.That(BitConverter.ToInt32(range[4].Key), Is.EqualTo(9));
    }

    [Test]
    public void EmptyTreeOperationsTest()
    {
        using var tree = new BTree(m_pageManager);
        
        Assert.That(tree.Count(), Is.EqualTo(0));
        Assert.That(tree.GetAll().ToList(), Is.Empty);
        Assert.That(tree.ContainsKey("any"u8), Is.False);
        Assert.That(tree.Search("any"u8), Is.Null);
        Assert.That(tree.Delete("any"u8), Is.False);
    }

    [Test]
    public void SequentialAndRandomAccessTest()
    {
        using var tree = new BTree(m_pageManager);
        
        const int count = 1000;
        
        // Insert sequentially
        for (int i = 0; i < count; i++)
        {
            tree.Insert(BitConverter.GetBytes(i), BitConverter.GetBytes(i));
        }
        
        // Access randomly
        var random = new Random(42);
        var indices = Enumerable.Range(0, count).OrderBy(_ => random.Next()).ToList();
        
        foreach (var idx in indices)
        {
            var result = tree.Search(BitConverter.GetBytes(idx));
            Assert.That(result, Is.Not.Null);
            Assert.That(BitConverter.ToInt32(result!), Is.EqualTo(idx));
        }
    }

    [Test]
    public async Task AsyncDisposeTest()
    {
        await using var tree = new BTree(m_pageManager);
        
        tree.Insert("key"u8.ToArray(), "value"u8.ToArray());
        Assert.That(tree.ContainsKey("key"u8), Is.True);
        
        // Should not throw on async dispose
    }

    #endregion

    private class ByteArrayEqualityComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[]? x, byte[]? y)
        {
            if (x == null && y == null) return true;
            if (x == null || y == null) return false;
            return x.SequenceEqual(y);
        }
        public int GetHashCode(byte[] obj) => obj.Length > 0 ? obj[0] : 0;
    }
}

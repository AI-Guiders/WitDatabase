using OutWit.Database.Core.Managers;
using OutWit.Database.Core.Storage;
using OutWit.Database.Core.Tree;
using TextEncoding = System.Text.Encoding;

namespace OutWit.Database.Core.Tests.Tree;

/// <summary>
/// Additional stress and edge case tests for BTree.
/// These tests focus on scenarios that might break the tree invariants.
/// </summary>
[TestFixture]
[Category("Stress")]
public class BTreeStressTest
{
    #region Concurrent-like Access Patterns

    [Test]
    public void InterleavedInsertDeleteMaintainsConsistencyTest()
    {
        using var storage = new StorageMemory(4096, 5000);
        using var pageManager = new PageManager(storage);
        using var tree = new BTree(pageManager);

        var random = new Random(123);
        var keys = new HashSet<int>();

        // Interleaved pattern: insert 3, delete 1
        for (int batch = 0; batch < 1000; batch++)
        {
            // Insert 3
            for (int i = 0; i < 3; i++)
            {
                int key = random.Next(100000);
                if (keys.Add(key))
                {
                    tree.Insert(BitConverter.GetBytes(key), BitConverter.GetBytes(key));
                }
            }

            // Delete 1
            if (keys.Count > 0)
            {
                var keyToDelete = keys.First();
                tree.Delete(BitConverter.GetBytes(keyToDelete));
                keys.Remove(keyToDelete);
            }
        }

        Assert.That(tree.Count(), Is.EqualTo(keys.Count));

        foreach (var key in keys)
        {
            Assert.That(tree.ContainsKey(BitConverter.GetBytes(key)), Is.True);
        }
    }

    #endregion

    #region Key Distribution Patterns

    [Test]
    public void AllSamePrefixHandlesCorrectlyTest()
    {
        using var storage = new StorageMemory(4096, 2000);
        using var pageManager = new PageManager(storage);
        using var tree = new BTree(pageManager);

        // All keys have same 8-byte prefix, differ only in last 4 bytes
        byte[] prefix = "PREFIX__"u8.ToArray();
        
        for (int i = 0; i < 5000; i++)
        {
            byte[] key = new byte[12];
            prefix.CopyTo(key, 0);
            BitConverter.TryWriteBytes(key.AsSpan(8), i);
            
            tree.Insert(key, BitConverter.GetBytes(i));
        }

        Assert.That(tree.Count(), Is.EqualTo(5000));

        // Verify all
        for (int i = 0; i < 5000; i++)
        {
            byte[] key = new byte[12];
            prefix.CopyTo(key, 0);
            BitConverter.TryWriteBytes(key.AsSpan(8), i);
            
            var result = tree.Search(key);
            Assert.That(result, Is.Not.Null);
            Assert.That(BitConverter.ToInt32(result!), Is.EqualTo(i));
        }
    }

    [Test]
    public void AlternatingPatternInsertOddDeleteEvenTest()
    {
        using var storage = new StorageMemory(4096, 2000);
        using var pageManager = new PageManager(storage);
        using var tree = new BTree(pageManager);

        const int count = 10000;

        // Insert all
        for (int i = 0; i < count; i++)
        {
            tree.Insert(BitConverter.GetBytes(i), BitConverter.GetBytes(i));
        }

        // Delete even
        for (int i = 0; i < count; i += 2)
        {
            tree.Delete(BitConverter.GetBytes(i));
        }

        Assert.That(tree.Count(), Is.EqualTo(count / 2));

        // Verify odd remain
        for (int i = 1; i < count; i += 2)
        {
            Assert.That(tree.ContainsKey(BitConverter.GetBytes(i)), Is.True);
        }

        // Verify even deleted
        for (int i = 0; i < count; i += 2)
        {
            Assert.That(tree.ContainsKey(BitConverter.GetBytes(i)), Is.False);
        }
    }

    [Test]
    public void ZigZagInsertFromBothEndsTest()
    {
        using var storage = new StorageMemory(4096, 2000);
        using var pageManager = new PageManager(storage);
        using var tree = new BTree(pageManager);

        const int count = 5000;

        // Insert alternating from start and end using string keys for proper ordering
        for (int i = 0; i < count / 2; i++)
        {
            tree.Insert(TextEncoding.UTF8.GetBytes($"{i:D8}"), BitConverter.GetBytes(i));
            tree.Insert(TextEncoding.UTF8.GetBytes($"{count - 1 - i:D8}"), BitConverter.GetBytes(count - 1 - i));
        }

        Assert.That(tree.Count(), Is.EqualTo(count));

        // Verify all searchable
        for (int i = 0; i < count; i++)
        {
            Assert.That(tree.ContainsKey(TextEncoding.UTF8.GetBytes($"{i:D8}")), Is.True);
        }

        // Verify scan order
        var all = tree.GetAll().ToList();
        for (int i = 0; i < count; i++)
        {
            string keyStr = TextEncoding.UTF8.GetString(all[i].Key);
            Assert.That(keyStr, Is.EqualTo($"{i:D8}"));
        }
    }

    #endregion

    #region Value Size Extremes

    [Test]
    public void MixedInlineAndOverflowRapidlyTest()
    {
        using var storage = new StorageMemory(4096, 5000);
        using var pageManager = new PageManager(storage);
        using var tree = new BTree(pageManager);

        var random = new Random(42);
        var expected = new Dictionary<int, byte[]>();

        for (int i = 0; i < 2000; i++)
        {
            // Alternate between tiny, medium, and overflow values
            int sizeType = i % 3;
            int valueSize = sizeType switch
            {
                0 => 10,                                    // tiny inline
                1 => tree.MaxInlineValueSize - 10,          // max inline
                _ => tree.MaxInlineValueSize + 500          // overflow
            };

            byte[] value = new byte[valueSize];
            random.NextBytes(value);
            
            tree.Insert(BitConverter.GetBytes(i), value);
            expected[i] = value;
        }

        // Verify all
        foreach (var (key, value) in expected)
        {
            var result = tree.Search(BitConverter.GetBytes(key));
            Assert.That(result, Is.Not.Null, $"Key {key} not found");
            Assert.That(result!.SequenceEqual(value), Is.True, $"Value mismatch for key {key}");
        }
    }

    [Test]
    public void UpdateBetweenInlineAndOverflowTest()
    {
        using var storage = new StorageMemory(4096, 2000);
        using var pageManager = new PageManager(storage);
        using var tree = new BTree(pageManager);

        var random = new Random(42);
        const int keyCount = 500;

        // Initial insert with small values
        for (int i = 0; i < keyCount; i++)
        {
            tree.Insert(BitConverter.GetBytes(i), BitConverter.GetBytes(i));
        }

        // Update to overflow values
        for (int i = 0; i < keyCount; i++)
        {
            byte[] largeValue = new byte[tree.MaxInlineValueSize + 100];
            random.NextBytes(largeValue);
            tree.Upsert(BitConverter.GetBytes(i), largeValue);
        }

        // Update back to small values
        for (int i = 0; i < keyCount; i++)
        {
            tree.Upsert(BitConverter.GetBytes(i), BitConverter.GetBytes(i * 2));
        }

        Assert.That(tree.Count(), Is.EqualTo(keyCount));

        // Verify final values
        for (int i = 0; i < keyCount; i++)
        {
            var result = tree.Search(BitConverter.GetBytes(i));
            Assert.That(BitConverter.ToInt32(result!), Is.EqualTo(i * 2));
        }
    }

    #endregion

    #region Split Edge Cases

    [Test]
    public void ForceManyInternalSplitsTest()
    {
        using var storage = new StorageMemory(4096, 10000);
        using var pageManager = new PageManager(storage);
        using var tree = new BTree(pageManager);

        // Use small keys and values to maximize entries per page
        // This forces more internal node splits
        const int count = 100000;

        for (int i = 0; i < count; i++)
        {
            byte[] key = BitConverter.GetBytes(i);
            tree.Insert(key, key);
        }

        Assert.That(tree.Count(), Is.EqualTo(count));

        // Sample verification
        for (int i = 0; i < count; i += 1000)
        {
            Assert.That(tree.ContainsKey(BitConverter.GetBytes(i)), Is.True);
        }
    }

    [Test]
    public void ForceMultipleLevelTreeTest()
    {
        using var storage = new StorageMemory(4096, 15000);
        using var pageManager = new PageManager(storage);
        using var tree = new BTree(pageManager);

        // Insert enough to create 4+ level tree
        const int count = 200000;

        for (int i = 0; i < count; i++)
        {
            tree.Insert(BitConverter.GetBytes(i), BitConverter.GetBytes(i));
        }

        Assert.That(tree.Count(), Is.EqualTo(count));

        // Full range scan should work
        int scanCount = 0;
        foreach (var _ in tree.GetAll())
        {
            scanCount++;
        }
        Assert.That(scanCount, Is.EqualTo(count));
    }

    #endregion

    #region Compaction Under Stress

    [Test]
    public void RepeatedUpsertSameKeysCompactionWorksTest()
    {
        using var storage = new StorageMemory(4096, 1000);
        using var pageManager = new PageManager(storage);
        using var tree = new BTree(pageManager);

        var random = new Random(42);
        const int keyCount = 100;
        const int updateRounds = 50;

        // Initial insert
        for (int i = 0; i < keyCount; i++)
        {
            tree.Insert(BitConverter.GetBytes(i), BitConverter.GetBytes(i));
        }

        // Many rounds of updates with varying value sizes
        for (int round = 0; round < updateRounds; round++)
        {
            for (int i = 0; i < keyCount; i++)
            {
                int valueSize = random.Next(10, 200);
                byte[] newValue = new byte[valueSize];
                random.NextBytes(newValue);
                tree.Upsert(BitConverter.GetBytes(i), newValue);
            }
        }

        Assert.That(tree.Count(), Is.EqualTo(keyCount));

        // All keys should still be accessible
        for (int i = 0; i < keyCount; i++)
        {
            Assert.That(tree.ContainsKey(BitConverter.GetBytes(i)), Is.True);
        }
    }

    [Test]
    public void DeleteHalfInsertHalfRepeatedCyclesTest()
    {
        using var storage = new StorageMemory(4096, 2000);
        using var pageManager = new PageManager(storage);
        using var tree = new BTree(pageManager);

        var keys = new HashSet<int>();
        var nextKey = 0;

        for (int cycle = 0; cycle < 20; cycle++)
        {
            // Insert 500 new keys
            for (int i = 0; i < 500; i++)
            {
                tree.Insert(BitConverter.GetBytes(nextKey), BitConverter.GetBytes(nextKey));
                keys.Add(nextKey);
                nextKey++;
            }

            // Delete 250 random existing keys
            var toDelete = keys.OrderBy(_ => Random.Shared.Next()).Take(250).ToList();
            foreach (var k in toDelete)
            {
                tree.Delete(BitConverter.GetBytes(k));
                keys.Remove(k);
            }
        }

        Assert.That(tree.Count(), Is.EqualTo(keys.Count));

        // Verify remaining
        foreach (var k in keys)
        {
            Assert.That(tree.ContainsKey(BitConverter.GetBytes(k)), Is.True);
        }
    }

    #endregion

    #region Range Scan Edge Cases

    [Test]
    public void RangeScanSpanningMultipleLeavesTest()
    {
        using var storage = new StorageMemory(4096, 2000);
        using var pageManager = new PageManager(storage);
        using var tree = new BTree(pageManager);

        const int count = 10000;

        for (int i = 0; i < count; i++)
        {
            tree.Insert(TextEncoding.UTF8.GetBytes($"{i:D8}"), BitConverter.GetBytes(i));
        }

        // Scan that definitely spans multiple leaves
        var range = tree.GetRange(
            TextEncoding.UTF8.GetBytes($"{1000:D8}"),
            TextEncoding.UTF8.GetBytes($"{5000:D8}")
        ).ToList();

        Assert.That(range.Count, Is.EqualTo(4000)); // 1000 to 4999

        for (int i = 0; i < range.Count; i++)
        {
            int expectedKey = 1000 + i;
            string actualKey = TextEncoding.UTF8.GetString(range[i].Key);
            Assert.That(actualKey, Is.EqualTo($"{expectedKey:D8}"));
        }
    }

    [Test]
    public void RangeScanWithOverflowValuesTest()
    {
        using var storage = new StorageMemory(4096, 3000);
        using var pageManager = new PageManager(storage);
        using var tree = new BTree(pageManager);

        var random = new Random(42);
        var expected = new Dictionary<int, byte[]>();

        // Mix of inline and overflow values - use string keys for proper ordering
        for (int i = 0; i < 500; i++)
        {
            bool isOverflow = i % 5 == 0;
            int valueSize = isOverflow ? tree.MaxInlineValueSize + 200 : 50;
            
            byte[] value = new byte[valueSize];
            random.NextBytes(value);
            
            tree.Insert(TextEncoding.UTF8.GetBytes($"{i:D8}"), value);
            expected[i] = value;
        }

        // Range scan using string keys
        var range = tree.GetRange(
            TextEncoding.UTF8.GetBytes($"{100:D8}"),
            TextEncoding.UTF8.GetBytes($"{300:D8}")
        ).ToList();
        
        Assert.That(range.Count, Is.EqualTo(200)); // 100 to 299

        for (int i = 0; i < range.Count; i++)
        {
            int key = int.Parse(TextEncoding.UTF8.GetString(range[i].Key));
            Assert.That(range[i].Value.SequenceEqual(expected[key]), Is.True);
        }
    }

    #endregion

    #region Persistence Stress

    [Test]
    public void ReopenAfterHeavyOperationsTest()
    {
        using var storage = new StorageMemory(4096, 5000);
        using var pageManager = new PageManager(storage);

        uint rootPage;
        var expectedKeys = new HashSet<int>();
        var random = new Random(42);

        // Phase 1: Heavy operations
        using (var tree = new BTree(pageManager))
        {
            for (int i = 0; i < 10000; i++)
            {
                tree.Insert(BitConverter.GetBytes(i), BitConverter.GetBytes(i));
                expectedKeys.Add(i);
            }

            // Delete random half
            var toDelete = expectedKeys.OrderBy(_ => random.Next()).Take(5000).ToList();
            foreach (var k in toDelete)
            {
                tree.Delete(BitConverter.GetBytes(k));
                expectedKeys.Remove(k);
            }

            // Insert more
            for (int i = 10000; i < 15000; i++)
            {
                tree.Insert(BitConverter.GetBytes(i), BitConverter.GetBytes(i));
                expectedKeys.Add(i);
            }

            rootPage = tree.RootPageNumber;
        }

        // Phase 2: Reopen and verify
        using (var tree = new BTree(pageManager, rootPage))
        {
            Assert.That(tree.Count(), Is.EqualTo(expectedKeys.Count));

            foreach (var k in expectedKeys.Take(100)) // Sample check
            {
                Assert.That(tree.ContainsKey(BitConverter.GetBytes(k)), Is.True);
            }
        }
    }

    #endregion
}

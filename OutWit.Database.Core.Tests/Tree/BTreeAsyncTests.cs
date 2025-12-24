using OutWit.Database.Core.Cache;
using OutWit.Database.Core.Managers;
using OutWit.Database.Core.Storage;
using OutWit.Database.Core.Tree;

namespace OutWit.Database.Core.Tests.Tree;

[TestFixture]
public class BTreeAsyncTests
{
    #region Helper Methods

    private static async Task<(BTree Tree, PageManager PageManager, StorageMemory Storage)> CreateTreeAsync(int initialPages = 100)
    {
        var storage = new StorageMemory(initialPageCount: initialPages);
        var cache = new PageCacheShardedClock(storage, maxPages: 50);
        var pageManager = await PageManager.CreateAsync(storage, cache);
        var tree = await BTree.CreateAsync(pageManager);
        return (tree, pageManager, storage);
    }

    private static void DisposeAll(BTree tree, PageManager pageManager, StorageMemory storage)
    {
        tree.Dispose();
        pageManager.Dispose();
        storage.Dispose();
    }

    #endregion

    #region SearchAsync Tests

    [Test]
    public async Task SearchAsyncFindsExistingKeyTest()
    {
        var (tree, pageManager, storage) = await CreateTreeAsync();
        
        try
        {
            byte[] key = "test-key"u8.ToArray();
            byte[] value = "test-value"u8.ToArray();
            
            tree.Insert(key, value);
            
            var result = await tree.SearchAsync(key);
            
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.EqualTo(value));
        }
        finally
        {
            DisposeAll(tree, pageManager, storage);
        }
    }

    [Test]
    public async Task SearchAsyncReturnsNullForMissingKeyTest()
    {
        var (tree, pageManager, storage) = await CreateTreeAsync();
        
        try
        {
            byte[] key = "nonexistent"u8.ToArray();
            
            var result = await tree.SearchAsync(key);
            
            Assert.That(result, Is.Null);
        }
        finally
        {
            DisposeAll(tree, pageManager, storage);
        }
    }

    [Test]
    public async Task SearchAsyncWithCancellationTest()
    {
        var (tree, pageManager, storage) = await CreateTreeAsync();
        
        try
        {
            byte[] key = "test"u8.ToArray();
            tree.Insert(key, "value"u8.ToArray());
            
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            
            var ex = Assert.CatchAsync<OperationCanceledException>(async () =>
                await tree.SearchAsync(key, cts.Token));
            
            Assert.That(ex, Is.Not.Null);
        }
        finally
        {
            DisposeAll(tree, pageManager, storage);
        }
    }

    #endregion

    #region ContainsKeyAsync Tests

    [Test]
    public async Task ContainsKeyAsyncReturnsTrueForExistingKeyTest()
    {
        var (tree, pageManager, storage) = await CreateTreeAsync();
        
        try
        {
            byte[] key = "exists"u8.ToArray();
            tree.Insert(key, "value"u8.ToArray());
            
            var result = await tree.ContainsKeyAsync(key);
            
            Assert.That(result, Is.True);
        }
        finally
        {
            DisposeAll(tree, pageManager, storage);
        }
    }

    [Test]
    public async Task ContainsKeyAsyncReturnsFalseForMissingKeyTest()
    {
        var (tree, pageManager, storage) = await CreateTreeAsync();
        
        try
        {
            byte[] key = "missing"u8.ToArray();
            
            var result = await tree.ContainsKeyAsync(key);
            
            Assert.That(result, Is.False);
        }
        finally
        {
            DisposeAll(tree, pageManager, storage);
        }
    }

    #endregion

    #region InsertAsync Tests

    [Test]
    public async Task InsertAsyncInsertsNewKeyTest()
    {
        var (tree, pageManager, storage) = await CreateTreeAsync();
        
        try
        {
            byte[] key = "new-key"u8.ToArray();
            byte[] value = "new-value"u8.ToArray();
            
            var result = await tree.InsertAsync(key, value);
            
            Assert.That(result, Is.True);
            Assert.That(tree.Count, Is.EqualTo(1));
            
            var retrieved = tree.Search(key);
            Assert.That(retrieved, Is.EqualTo(value));
        }
        finally
        {
            DisposeAll(tree, pageManager, storage);
        }
    }

    [Test]
    public async Task InsertAsyncReturnsFalseForDuplicateKeyTest()
    {
        var (tree, pageManager, storage) = await CreateTreeAsync();
        
        try
        {
            byte[] key = "duplicate"u8.ToArray();
            byte[] value1 = "value1"u8.ToArray();
            byte[] value2 = "value2"u8.ToArray();
            
            await tree.InsertAsync(key, value1);
            var result = await tree.InsertAsync(key, value2);
            
            Assert.That(result, Is.False);
            Assert.That(tree.Count, Is.EqualTo(1));
            
            // Value should not change
            var retrieved = tree.Search(key);
            Assert.That(retrieved, Is.EqualTo(value1));
        }
        finally
        {
            DisposeAll(tree, pageManager, storage);
        }
    }

    [Test]
    public async Task InsertAsyncMultipleKeysTest()
    {
        var (tree, pageManager, storage) = await CreateTreeAsync();
        
        try
        {
            for (int i = 0; i < 50; i++)
            {
                byte[] key = BitConverter.GetBytes(i);
                byte[] value = BitConverter.GetBytes(i * 10);
                await tree.InsertAsync(key, value);
            }
            
            Assert.That(tree.Count, Is.EqualTo(50));
            
            // Verify all values
            for (int i = 0; i < 50; i++)
            {
                byte[] key = BitConverter.GetBytes(i);
                var result = await tree.SearchAsync(key);
                Assert.That(result, Is.EqualTo(BitConverter.GetBytes(i * 10)));
            }
        }
        finally
        {
            DisposeAll(tree, pageManager, storage);
        }
    }

    #endregion

    #region UpsertAsync Tests

    [Test]
    public async Task UpsertAsyncInsertsNewKeyTest()
    {
        var (tree, pageManager, storage) = await CreateTreeAsync();
        
        try
        {
            byte[] key = "upsert-key"u8.ToArray();
            byte[] value = "upsert-value"u8.ToArray();
            
            var result = await tree.UpsertAsync(key, value);
            
            Assert.That(result, Is.True); // New key inserted
            Assert.That(tree.Count, Is.EqualTo(1));
        }
        finally
        {
            DisposeAll(tree, pageManager, storage);
        }
    }

    [Test]
    public async Task UpsertAsyncUpdatesExistingKeyTest()
    {
        var (tree, pageManager, storage) = await CreateTreeAsync();
        
        try
        {
            byte[] key = "upsert-key"u8.ToArray();
            byte[] value1 = "value1"u8.ToArray();
            byte[] value2 = "value2-updated"u8.ToArray();
            
            await tree.UpsertAsync(key, value1);
            var result = await tree.UpsertAsync(key, value2);
            
            Assert.That(result, Is.False); // Existing key updated
            Assert.That(tree.Count, Is.EqualTo(1));
            
            var retrieved = tree.Search(key);
            Assert.That(retrieved, Is.EqualTo(value2));
        }
        finally
        {
            DisposeAll(tree, pageManager, storage);
        }
    }

    #endregion

    #region DeleteAsync Tests

    [Test]
    public async Task DeleteAsyncRemovesExistingKeyTest()
    {
        var (tree, pageManager, storage) = await CreateTreeAsync();
        
        try
        {
            byte[] key = "to-delete"u8.ToArray();
            byte[] value = "value"u8.ToArray();
            
            tree.Insert(key, value);
            Assert.That(tree.Count, Is.EqualTo(1));
            
            var result = await tree.DeleteAsync(key);
            
            Assert.That(result, Is.True);
            Assert.That(tree.Count, Is.EqualTo(0));
            Assert.That(await tree.ContainsKeyAsync(key), Is.False);
        }
        finally
        {
            DisposeAll(tree, pageManager, storage);
        }
    }

    [Test]
    public async Task DeleteAsyncReturnsFalseForMissingKeyTest()
    {
        var (tree, pageManager, storage) = await CreateTreeAsync();
        
        try
        {
            byte[] key = "nonexistent"u8.ToArray();
            
            var result = await tree.DeleteAsync(key);
            
            Assert.That(result, Is.False);
        }
        finally
        {
            DisposeAll(tree, pageManager, storage);
        }
    }

    #endregion

    #region GetRangeAsync Tests

    [Test]
    public async Task GetAllAsyncReturnsAllEntriesTest()
    {
        var (tree, pageManager, storage) = await CreateTreeAsync();
        
        try
        {
            // Insert some entries
            for (int i = 0; i < 10; i++)
            {
                byte[] key = BitConverter.GetBytes(i);
                byte[] value = BitConverter.GetBytes(i * 100);
                tree.Insert(key, value);
            }
            
            var results = new List<(byte[] Key, byte[] Value)>();
            await foreach (var entry in tree.GetAllAsync())
            {
                results.Add(entry);
            }
            
            Assert.That(results.Count, Is.EqualTo(10));
        }
        finally
        {
            DisposeAll(tree, pageManager, storage);
        }
    }

    [Test]
    public async Task GetRangeAsyncReturnsRangeTest()
    {
        var (tree, pageManager, storage) = await CreateTreeAsync();
        
        try
        {
            // Insert entries 0-9
            for (int i = 0; i < 10; i++)
            {
                byte[] key = new byte[] { (byte)i };
                byte[] value = new byte[] { (byte)(i * 10) };
                tree.Insert(key, value);
            }
            
            // Get range [3, 7)
            byte[] minKey = new byte[] { 3 };
            byte[] maxKey = new byte[] { 7 };
            
            var results = new List<(byte[] Key, byte[] Value)>();
            await foreach (var entry in tree.GetRangeAsync(minKey, maxKey))
            {
                results.Add(entry);
            }
            
            Assert.That(results.Count, Is.EqualTo(4)); // Keys 3, 4, 5, 6
        }
        finally
        {
            DisposeAll(tree, pageManager, storage);
        }
    }

    [Test]
    public async Task GetRangeAsyncWithCancellationTest()
    {
        var (tree, pageManager, storage) = await CreateTreeAsync();
        
        try
        {
            for (int i = 0; i < 10; i++)
            {
                tree.Insert(new byte[] { (byte)i }, new byte[] { (byte)i });
            }
            
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            
            var ex = Assert.CatchAsync<OperationCanceledException>(async () =>
            {
                await foreach (var _ in tree.GetAllAsync(cts.Token))
                {
                    // Should throw before yielding
                }
            });
            
            Assert.That(ex, Is.Not.Null);
        }
        finally
        {
            DisposeAll(tree, pageManager, storage);
        }
    }

    #endregion

    #region Split Operations Async Tests

    [Test]
    public async Task InsertAsyncTriggersLeafSplitTest()
    {
        var (tree, pageManager, storage) = await CreateTreeAsync(200);
        
        try
        {
            // Insert enough entries to trigger splits
            for (int i = 0; i < 100; i++)
            {
                byte[] key = BitConverter.GetBytes(i);
                byte[] value = new byte[100]; // Larger value to fill pages faster
                Array.Fill(value, (byte)(i % 256));
                
                await tree.InsertAsync(key, value);
            }
            
            Assert.That(tree.Count, Is.EqualTo(100));
            
            // Verify all entries are retrievable
            for (int i = 0; i < 100; i++)
            {
                byte[] key = BitConverter.GetBytes(i);
                var result = await tree.SearchAsync(key);
                
                Assert.That(result, Is.Not.Null);
                Assert.That(result![0], Is.EqualTo((byte)(i % 256)));
            }
        }
        finally
        {
            DisposeAll(tree, pageManager, storage);
        }
    }

    [Test]
    public async Task UpsertAsyncTriggersLeafSplitTest()
    {
        var (tree, pageManager, storage) = await CreateTreeAsync(200);
        
        try
        {
            // Insert enough entries to trigger splits
            for (int i = 0; i < 100; i++)
            {
                byte[] key = BitConverter.GetBytes(i);
                byte[] value = new byte[100];
                Array.Fill(value, (byte)(i % 256));
                
                await tree.UpsertAsync(key, value);
            }
            
            Assert.That(tree.Count, Is.EqualTo(100));
            
            // Update all values
            for (int i = 0; i < 100; i++)
            {
                byte[] key = BitConverter.GetBytes(i);
                byte[] value = new byte[100];
                Array.Fill(value, (byte)((i + 1) % 256));
                
                var wasInsert = await tree.UpsertAsync(key, value);
                Assert.That(wasInsert, Is.False); // Should be update
            }
            
            // Verify updated values
            for (int i = 0; i < 100; i++)
            {
                byte[] key = BitConverter.GetBytes(i);
                var result = await tree.SearchAsync(key);
                
                Assert.That(result, Is.Not.Null);
                Assert.That(result![0], Is.EqualTo((byte)((i + 1) % 256)));
            }
        }
        finally
        {
            DisposeAll(tree, pageManager, storage);
        }
    }

    #endregion

    #region Concurrent Async Tests

    [Test]
    public async Task ConcurrentAsyncReadsTest()
    {
        var (tree, pageManager, storage) = await CreateTreeAsync(200);
        
        try
        {
            // Insert entries
            for (int i = 0; i < 50; i++)
            {
                tree.Insert(BitConverter.GetBytes(i), BitConverter.GetBytes(i * 10));
            }
            
            // Concurrent reads
            var tasks = new Task[10];
            for (int t = 0; t < 10; t++)
            {
                tasks[t] = Task.Run(async () =>
                {
                    for (int i = 0; i < 50; i++)
                    {
                        var result = await tree.SearchAsync(BitConverter.GetBytes(i));
                        Assert.That(result, Is.Not.Null);
                    }
                });
            }
            
            await Task.WhenAll(tasks);
        }
        finally
        {
            DisposeAll(tree, pageManager, storage);
        }
    }

    #endregion

    #region Mixed Sync/Async Tests

    [Test]
    public async Task MixedSyncAndAsyncOperationsTest()
    {
        var (tree, pageManager, storage) = await CreateTreeAsync(200);
        
        try
        {
            // Sync inserts
            for (int i = 0; i < 25; i++)
            {
                tree.Insert(BitConverter.GetBytes(i), BitConverter.GetBytes(i));
            }
            
            // Async inserts
            for (int i = 25; i < 50; i++)
            {
                await tree.InsertAsync(BitConverter.GetBytes(i), BitConverter.GetBytes(i));
            }
            
            Assert.That(tree.Count, Is.EqualTo(50));
            
            // Mixed reads
            for (int i = 0; i < 50; i++)
            {
                if (i % 2 == 0)
                {
                    var syncResult = tree.Search(BitConverter.GetBytes(i));
                    Assert.That(syncResult, Is.Not.Null);
                }
                else
                {
                    var asyncResult = await tree.SearchAsync(BitConverter.GetBytes(i));
                    Assert.That(asyncResult, Is.Not.Null);
                }
            }
        }
        finally
        {
            DisposeAll(tree, pageManager, storage);
        }
    }

    #endregion
}

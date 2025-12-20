using OutWit.Database.Core.Stores;

namespace OutWit.Database.Core.Tests.Stores;

[TestFixture]
public class StoreInMemoryTest
{
    #region Basic Operations Tests

    [Test]
    public void PutAndGetTest()
    {
        using var store = new StoreInMemory();
        
        byte[] key = [1, 2, 3];
        byte[] value = [4, 5, 6];
        
        store.Put(key, value);
        byte[]? result = store.Get(key);
        
        Assert.That(result, Is.EqualTo(value));
    }

    [Test]
    public void GetNonExistentKeyReturnsNullTest()
    {
        using var store = new StoreInMemory();
        
        byte[]? result = store.Get([1, 2, 3]);
        
        Assert.That(result, Is.Null);
    }

    [Test]
    public void PutOverwritesExistingValueTest()
    {
        using var store = new StoreInMemory();
        
        byte[] key = [1, 2, 3];
        byte[] value1 = [4, 5, 6];
        byte[] value2 = [7, 8, 9];
        
        store.Put(key, value1);
        store.Put(key, value2);
        
        byte[]? result = store.Get(key);
        Assert.That(result, Is.EqualTo(value2));
    }

    [Test]
    public void DeleteExistingKeyTest()
    {
        using var store = new StoreInMemory();
        
        byte[] key = [1, 2, 3];
        byte[] value = [4, 5, 6];
        
        store.Put(key, value);
        bool deleted = store.Delete(key);
        
        Assert.That(deleted, Is.True);
        Assert.That(store.Get(key), Is.Null);
    }

    [Test]
    public void DeleteNonExistentKeyReturnsFalseTest()
    {
        using var store = new StoreInMemory();
        
        bool deleted = store.Delete([1, 2, 3]);
        
        Assert.That(deleted, Is.False);
    }

    #endregion

    #region Scan Tests

    [Test]
    public void ScanAllKeysTest()
    {
        using var store = new StoreInMemory();
        
        store.Put([1], [1]);
        store.Put([2], [2]);
        store.Put([3], [3]);
        
        var results = store.Scan(null, null).ToList();
        
        Assert.That(results, Has.Count.EqualTo(3));
        Assert.That(results[0].Key, Is.EqualTo(new byte[] { 1 }));
        Assert.That(results[1].Key, Is.EqualTo(new byte[] { 2 }));
        Assert.That(results[2].Key, Is.EqualTo(new byte[] { 3 }));
    }

    [Test]
    public void ScanWithStartKeyTest()
    {
        using var store = new StoreInMemory();
        
        store.Put([1], [1]);
        store.Put([2], [2]);
        store.Put([3], [3]);
        
        var results = store.Scan([2], null).ToList();
        
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results[0].Key, Is.EqualTo(new byte[] { 2 }));
        Assert.That(results[1].Key, Is.EqualTo(new byte[] { 3 }));
    }

    [Test]
    public void ScanWithEndKeyTest()
    {
        using var store = new StoreInMemory();
        
        store.Put([1], [1]);
        store.Put([2], [2]);
        store.Put([3], [3]);
        
        var results = store.Scan(null, [3]).ToList();
        
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results[0].Key, Is.EqualTo(new byte[] { 1 }));
        Assert.That(results[1].Key, Is.EqualTo(new byte[] { 2 }));
    }

    [Test]
    public void ScanWithRangeTest()
    {
        using var store = new StoreInMemory();
        
        store.Put([1], [1]);
        store.Put([2], [2]);
        store.Put([3], [3]);
        store.Put([4], [4]);
        
        var results = store.Scan([2], [4]).ToList();
        
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results[0].Key, Is.EqualTo(new byte[] { 2 }));
        Assert.That(results[1].Key, Is.EqualTo(new byte[] { 3 }));
    }

    [Test]
    public void ScanEmptyStoreTest()
    {
        using var store = new StoreInMemory();
        
        var results = store.Scan(null, null).ToList();
        
        Assert.That(results, Is.Empty);
    }

    #endregion

    #region Async Tests

    [Test]
    public async Task GetAsyncTest()
    {
        using var store = new StoreInMemory();
        
        byte[] key = [1, 2, 3];
        byte[] value = [4, 5, 6];
        
        store.Put(key, value);
        byte[]? result = await store.GetAsync(key);
        
        Assert.That(result, Is.EqualTo(value));
    }

    [Test]
    public async Task PutAsyncTest()
    {
        using var store = new StoreInMemory();
        
        byte[] key = [1, 2, 3];
        byte[] value = [4, 5, 6];
        
        await store.PutAsync(key, value);
        byte[]? result = store.Get(key);
        
        Assert.That(result, Is.EqualTo(value));
    }

    [Test]
    public async Task DeleteAsyncTest()
    {
        using var store = new StoreInMemory();
        
        byte[] key = [1, 2, 3];
        byte[] value = [4, 5, 6];
        
        store.Put(key, value);
        bool deleted = await store.DeleteAsync(key);
        
        Assert.That(deleted, Is.True);
        Assert.That(store.Get(key), Is.Null);
    }

    [Test]
    public async Task ScanAsyncTest()
    {
        using var store = new StoreInMemory();
        
        store.Put([1], [1]);
        store.Put([2], [2]);
        store.Put([3], [3]);
        
        var results = new List<(byte[] Key, byte[] Value)>();
        await foreach (var item in store.ScanAsync(null, null))
        {
            results.Add(item);
        }
        
        Assert.That(results, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task FlushAsyncDoesNotThrowTest()
    {
        using var store = new StoreInMemory();
        
        await store.FlushAsync();
        
        // Should complete without exception
        Assert.Pass();
    }

    #endregion

    #region Cancellation Tests

    [Test]
    public void GetAsyncCancellationTest()
    {
        using var store = new StoreInMemory();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        
        Assert.ThrowsAsync<OperationCanceledException>(async () => 
            await store.GetAsync([1], cts.Token));
    }

    [Test]
    public void PutAsyncCancellationTest()
    {
        using var store = new StoreInMemory();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        
        Assert.ThrowsAsync<OperationCanceledException>(async () => 
            await store.PutAsync([1], [1], cts.Token));
    }

    [Test]
    public void DeleteAsyncCancellationTest()
    {
        using var store = new StoreInMemory();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        
        Assert.ThrowsAsync<OperationCanceledException>(async () => 
            await store.DeleteAsync([1], cts.Token));
    }

    #endregion

    #region Dispose Tests

    [Test]
    public void DisposeMultipleTimesDoesNotThrowTest()
    {
        var store = new StoreInMemory();
        
        store.Dispose();
        store.Dispose();
        
        // Should not throw
        Assert.Pass();
    }

    [Test]
    public void OperationsAfterDisposeThrowTest()
    {
        var store = new StoreInMemory();
        store.Dispose();
        
        Assert.Throws<ObjectDisposedException>(() => store.Get([1]));
        Assert.Throws<ObjectDisposedException>(() => store.Put([1], [1]));
        Assert.Throws<ObjectDisposedException>(() => store.Delete([1]));
        Assert.Throws<ObjectDisposedException>(() => store.Scan(null, null).ToList());
    }

    #endregion

    #region Count Tests

    [Test]
    public void CountEmptyStoreTest()
    {
        using var store = new StoreInMemory();
        
        Assert.That(store.Count, Is.EqualTo(0));
    }

    [Test]
    public void CountAfterPutTest()
    {
        using var store = new StoreInMemory();
        
        store.Put([1], [1]);
        store.Put([2], [2]);
        store.Put([3], [3]);
        
        Assert.That(store.Count, Is.EqualTo(3));
    }

    [Test]
    public void CountAfterDeleteTest()
    {
        using var store = new StoreInMemory();
        
        store.Put([1], [1]);
        store.Put([2], [2]);
        store.Delete([1]);
        
        Assert.That(store.Count, Is.EqualTo(1));
    }

    [Test]
    public void CountAfterOverwriteTest()
    {
        using var store = new StoreInMemory();
        
        store.Put([1], [1]);
        store.Put([1], [2]); // Overwrite, not new entry
        
        Assert.That(store.Count, Is.EqualTo(1));
    }

    #endregion

    #region Concurrency Tests

    [Test]
    public void ConcurrentPutAndGetTest()
    {
        using var store = new StoreInMemory();
        
        const int threadCount = 10;
        const int operationsPerThread = 100;
        
        var tasks = new Task[threadCount];
        
        for (int t = 0; t < threadCount; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < operationsPerThread; i++)
                {
                    byte[] key = [(byte)threadId, (byte)i];
                    byte[] value = [(byte)threadId, (byte)i, (byte)(i + 1)];
                    
                    store.Put(key, value);
                    byte[]? result = store.Get(key);
                    
                    // Value might be overwritten by same thread, but should not be null
                    Assert.That(result, Is.Not.Null);
                }
            });
        }
        
        Task.WaitAll(tasks);
        
        // Should complete without exceptions
        Assert.That(store.Count, Is.GreaterThan(0));
    }

    [Test]
    public void ConcurrentScanTest()
    {
        using var store = new StoreInMemory();
        
        // Populate store
        for (int i = 0; i < 100; i++)
        {
            store.Put([(byte)i], [(byte)i]);
        }
        
        const int threadCount = 5;
        var tasks = new Task[threadCount];
        
        for (int t = 0; t < threadCount; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < 20; i++)
                {
                    var results = store.Scan(null, null).ToList();
                    Assert.That(results.Count, Is.EqualTo(100));
                }
            });
        }
        
        Task.WaitAll(tasks);
    }

    #endregion
}

using OutWit.Database.Core.Interfaces;
using TextEncoding = System.Text.Encoding;

namespace OutWit.Database.Core.Tests.Stores;

/// <summary>
/// Abstract base class for testing IKeyValueStore implementations.
/// Each store implementation should create a derived test class and provide its store factory.
/// </summary>
/// <remarks>
/// This ensures consistent behavior across different storage engines (BTree, LSM, etc.)
/// </remarks>
public abstract class KeyValueStoreTestBase
{
    protected IKeyValueStore Store { get; private set; } = null!;

    /// <summary>
    /// Creates a new instance of the store being tested.
    /// Called before each test.
    /// </summary>
    protected abstract IKeyValueStore CreateStore();

    /// <summary>
    /// Override to perform additional cleanup.
    /// </summary>
    protected virtual void CleanupStore() { }

    [SetUp]
    public void BaseSetUp()
    {
        Store = CreateStore();
    }

    [TearDown]
    public void BaseTearDown()
    {
        Store?.Dispose();
        CleanupStore();
    }

    #region Basic CRUD Operations

    [Test]
    public void Put_And_Get_SingleKey()
    {
        byte[] key = "test-key"u8.ToArray();
        byte[] value = "test-value"u8.ToArray();

        Store.Put(key, value);

        var result = Store.Get(key);
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.EqualTo(value));
    }

    [Test]
    public void Get_NonExistentKey_ReturnsNull()
    {
        var result = Store.Get("non-existent"u8);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void Put_UpdatesExistingKey()
    {
        byte[] key = "key"u8.ToArray();
        
        Store.Put(key, "value1"u8.ToArray());
        Store.Put(key, "value2"u8.ToArray());

        var result = Store.Get(key);
        Assert.That(TextEncoding.UTF8.GetString(result!), Is.EqualTo("value2"));
    }

    [Test]
    public void Delete_ExistingKey_ReturnsTrue()
    {
        byte[] key = "to-delete"u8.ToArray();
        Store.Put(key, "value"u8.ToArray());

        var deleted = Store.Delete(key);

        Assert.That(deleted, Is.True);
        Assert.That(Store.Get(key), Is.Null);
    }

    [Test]
    public void Delete_NonExistentKey_ReturnsFalse()
    {
        var deleted = Store.Delete("non-existent"u8);
        Assert.That(deleted, Is.False);
    }

    [Test]
    public void Put_EmptyValue_Succeeds()
    {
        byte[] key = "empty-value"u8.ToArray();
        byte[] value = [];

        Store.Put(key, value);

        var result = Store.Get(key);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Length, Is.EqualTo(0));
    }

    #endregion

    #region Scan Operations

    [Test]
    public void Scan_AllEntries_ReturnsInOrder()
    {
        Store.Put("c"u8.ToArray(), "3"u8.ToArray());
        Store.Put("a"u8.ToArray(), "1"u8.ToArray());
        Store.Put("b"u8.ToArray(), "2"u8.ToArray());

        var results = Store.Scan(null, null).ToList();

        Assert.That(results.Count, Is.EqualTo(3));
        Assert.That(results[0].Key[0], Is.EqualTo((byte)'a'));
        Assert.That(results[1].Key[0], Is.EqualTo((byte)'b'));
        Assert.That(results[2].Key[0], Is.EqualTo((byte)'c'));
    }

    [Test]
    public void Scan_EmptyStore_ReturnsEmpty()
    {
        var results = Store.Scan(null, null).ToList();
        Assert.That(results, Is.Empty);
    }

    [Test]
    public void Scan_WithStartKey_StartsFromKey()
    {
        for (int i = 0; i < 10; i++)
        {
            Store.Put(TextEncoding.UTF8.GetBytes($"key{i:D2}"), BitConverter.GetBytes(i));
        }

        var results = Store.Scan(TextEncoding.UTF8.GetBytes("key05"), null).ToList();

        Assert.That(results.Count, Is.EqualTo(5)); // key05 through key09
        Assert.That(TextEncoding.UTF8.GetString(results[0].Key), Is.EqualTo("key05"));
    }

    [Test]
    public void Scan_WithEndKey_ExcludesEndKey()
    {
        for (int i = 0; i < 10; i++)
        {
            Store.Put(TextEncoding.UTF8.GetBytes($"key{i:D2}"), BitConverter.GetBytes(i));
        }

        var results = Store.Scan(TextEncoding.UTF8.GetBytes("key02"), TextEncoding.UTF8.GetBytes("key05")).ToList();

        Assert.That(results.Count, Is.EqualTo(3)); // key02, key03, key04 (key05 excluded)
        Assert.That(TextEncoding.UTF8.GetString(results[0].Key), Is.EqualTo("key02"));
        Assert.That(TextEncoding.UTF8.GetString(results[2].Key), Is.EqualTo("key04"));
    }

    [Test]
    public void Scan_RangeOutsideData_ReturnsEmpty()
    {
        Store.Put("bbb"u8.ToArray(), "value"u8.ToArray());

        var results = Store.Scan("ccc"u8.ToArray(), "ddd"u8.ToArray()).ToList();

        Assert.That(results, Is.Empty);
    }

    #endregion

    #region Async Operations

    [Test]
    public async Task GetAsync_ExistingKey_ReturnsValue()
    {
        byte[] key = "async-key"u8.ToArray();
        byte[] value = "async-value"u8.ToArray();

        await Store.PutAsync(key, value);

        var result = await Store.GetAsync(key);
        Assert.That(result, Is.EqualTo(value));
    }

    [Test]
    public async Task DeleteAsync_ExistingKey_Deletes()
    {
        byte[] key = "async-delete"u8.ToArray();
        await Store.PutAsync(key, "value"u8.ToArray());

        var deleted = await Store.DeleteAsync(key);

        Assert.That(deleted, Is.True);
        Assert.That(await Store.GetAsync(key), Is.Null);
    }

    [Test]
    public async Task ScanAsync_ReturnsAllEntries()
    {
        for (int i = 0; i < 5; i++)
        {
            await Store.PutAsync(BitConverter.GetBytes(i), BitConverter.GetBytes(i));
        }

        var results = new List<(byte[] Key, byte[] Value)>();
        await foreach (var item in Store.ScanAsync(null, null))
        {
            results.Add(item);
        }

        Assert.That(results.Count, Is.EqualTo(5));
    }

    [Test]
    public async Task FlushAsync_CompletesSuccessfully()
    {
        Store.Put("key"u8.ToArray(), "value"u8.ToArray());
        
        // Should not throw
        await Store.FlushAsync();
    }

    #endregion

    #region Multiple Operations

    [Test]
    public void MultipleKeys_AllRetrievable()
    {
        const int count = 100;

        for (int i = 0; i < count; i++)
        {
            Store.Put(BitConverter.GetBytes(i), BitConverter.GetBytes(i * 10));
        }

        for (int i = 0; i < count; i++)
        {
            var result = Store.Get(BitConverter.GetBytes(i));
            Assert.That(result, Is.Not.Null, $"Key {i} not found");
            Assert.That(BitConverter.ToInt32(result!), Is.EqualTo(i * 10));
        }
    }

    [Test]
    public void InsertDeleteCycle_MaintainsConsistency()
    {
        // Insert
        for (int i = 0; i < 50; i++)
        {
            Store.Put(BitConverter.GetBytes(i), BitConverter.GetBytes(i));
        }

        // Delete even numbers
        for (int i = 0; i < 50; i += 2)
        {
            Store.Delete(BitConverter.GetBytes(i));
        }

        // Verify odd numbers remain
        for (int i = 1; i < 50; i += 2)
        {
            Assert.That(Store.Get(BitConverter.GetBytes(i)), Is.Not.Null, $"Key {i} should exist");
        }

        // Verify even numbers deleted
        for (int i = 0; i < 50; i += 2)
        {
            Assert.That(Store.Get(BitConverter.GetBytes(i)), Is.Null, $"Key {i} should be deleted");
        }
    }

    [Test]
    public void UpdateMultipleTimes_LastValueWins()
    {
        byte[] key = "update-test"u8.ToArray();

        for (int i = 0; i < 10; i++)
        {
            Store.Put(key, BitConverter.GetBytes(i));
        }

        var result = Store.Get(key);
        Assert.That(BitConverter.ToInt32(result!), Is.EqualTo(9));
    }

    #endregion

    #region Edge Cases

    [Test]
    public void BinaryKeys_WorkCorrectly()
    {
        // Keys with null bytes and special characters
        byte[] key1 = [0x00, 0x01, 0x02];
        byte[] key2 = [0xFF, 0xFE, 0xFD];
        byte[] key3 = [0x00, 0x00, 0x00];

        Store.Put(key1, "value1"u8.ToArray());
        Store.Put(key2, "value2"u8.ToArray());
        Store.Put(key3, "value3"u8.ToArray());

        Assert.That(TextEncoding.UTF8.GetString(Store.Get(key1)!), Is.EqualTo("value1"));
        Assert.That(TextEncoding.UTF8.GetString(Store.Get(key2)!), Is.EqualTo("value2"));
        Assert.That(TextEncoding.UTF8.GetString(Store.Get(key3)!), Is.EqualTo("value3"));
    }

    [Test]
    public void LargeKey_UpToMaxSize_Succeeds()
    {
        // Most implementations support at least 1KB keys
        byte[] key = new byte[1000];
        Random.Shared.NextBytes(key);
        byte[] value = "large-key-value"u8.ToArray();

        Store.Put(key, value);

        var result = Store.Get(key);
        Assert.That(result, Is.EqualTo(value));
    }

    [Test]
    public void SequentialKeys_ScanInOrder()
    {
        // Insert in random order
        var indices = Enumerable.Range(0, 100).OrderBy(_ => Random.Shared.Next()).ToList();
        
        foreach (var i in indices)
        {
            Store.Put(TextEncoding.UTF8.GetBytes($"{i:D3}"), BitConverter.GetBytes(i));
        }

        var results = Store.Scan(null, null).ToList();

        for (int i = 0; i < 100; i++)
        {
            Assert.That(TextEncoding.UTF8.GetString(results[i].Key), Is.EqualTo($"{i:D3}"));
        }
    }

    #endregion

    #region Stress Tests

    [Test]
    [Category("Stress")]
    public void RandomOperations_MaintainsConsistency()
    {
        var random = new Random(42);
        var expectedState = new Dictionary<int, byte[]>();

        const int operationCount = 10000;
        const int keySpace = 1000;

        for (int op = 0; op < operationCount; op++)
        {
            int keyInt = random.Next(keySpace);
            byte[] key = BitConverter.GetBytes(keyInt);
            int action = random.Next(100);

            if (action < 50) // 50% put
            {
                byte[] value = new byte[random.Next(10, 100)];
                random.NextBytes(value);
                Store.Put(key, value);
                expectedState[keyInt] = value;
            }
            else if (action < 80) // 30% get
            {
                var result = Store.Get(key);
                if (expectedState.TryGetValue(keyInt, out var expected))
                {
                    Assert.That(result, Is.Not.Null, $"Key {keyInt} should exist at op {op}");
                    Assert.That(result!.SequenceEqual(expected), Is.True, $"Value mismatch for key {keyInt} at op {op}");
                }
                else
                {
                    Assert.That(result, Is.Null, $"Key {keyInt} should not exist at op {op}");
                }
            }
            else // 20% delete
            {
                Store.Delete(key);
                expectedState.Remove(keyInt);
            }
        }

        // Final verification
        foreach (var (keyInt, expectedValue) in expectedState)
        {
            var result = Store.Get(BitConverter.GetBytes(keyInt));
            Assert.That(result, Is.Not.Null, $"Final check: key {keyInt} not found");
            Assert.That(result!.SequenceEqual(expectedValue), Is.True, $"Final check: value mismatch for key {keyInt}");
        }
    }

    [Test]
    [Category("Stress")]
    public void SequentialInsert_ThenScan_PerformanceTest()
    {
        const int count = 50000;

        // Insert
        for (int i = 0; i < count; i++)
        {
            Store.Put(TextEncoding.UTF8.GetBytes($"{i:D8}"), BitConverter.GetBytes(i));
        }

        // Full scan
        var scanResults = Store.Scan(null, null).ToList();
        Assert.That(scanResults.Count, Is.EqualTo(count));

        // Verify order
        for (int i = 0; i < count; i++)
        {
            Assert.That(TextEncoding.UTF8.GetString(scanResults[i].Key), Is.EqualTo($"{i:D8}"));
        }
    }

    #endregion
}

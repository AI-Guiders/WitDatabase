using OutWit.Database.Core.Interfaces;
using OutWit.Database.Core.Storage;
using OutWit.Database.Core.Stores;
using OutWit.Database.Core.Transactions;
using TextEncoding = System.Text.Encoding;

namespace OutWit.Database.Core.Tests.Transactions;

/// <summary>
/// Async tests for TransactionalStore to verify WASM-compatible async operations.
/// </summary>
[TestFixture]
public class TransactionalStoreAsyncTests : IDisposable
{
    private IKeyValueStore m_underlyingStore = null!;
    private TransactionalStore m_store = null!;

    [SetUp]
    public void SetUp()
    {
        var storage = new StorageMemory();
        m_underlyingStore = new StoreBTree(storage);
        m_store = new TransactionalStore(m_underlyingStore, journal: null, lockManager: null);
    }

    [TearDown]
    public void TearDown()
    {
        Dispose();
    }

    public void Dispose()
    {
        m_store?.Dispose();
    }

    private static byte[] ToBytes(string s) => TextEncoding.UTF8.GetBytes(s);

    #region Basic Async Operations Tests

    [Test]
    public async Task GetAsyncReturnsValueTest()
    {
        await m_store.PutAsync(ToBytes("key1"), ToBytes("value1"));

        var result = await m_store.GetAsync(ToBytes("key1"));

        Assert.That(result, Is.EqualTo(ToBytes("value1")));
    }

    [Test]
    public async Task GetAsyncReturnsNullForNonExistentKeyTest()
    {
        var result = await m_store.GetAsync(ToBytes("nonexistent"));

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task PutAsyncPersistsDataTest()
    {
        await m_store.PutAsync(ToBytes("key1"), ToBytes("value1"));
        await m_store.FlushAsync();

        var result = await m_store.GetAsync(ToBytes("key1"));
        Assert.That(result, Is.EqualTo(ToBytes("value1")));
    }

    [Test]
    public async Task DeleteAsyncRemovesKeyTest()
    {
        await m_store.PutAsync(ToBytes("key1"), ToBytes("value1"));

        var deleted = await m_store.DeleteAsync(ToBytes("key1"));

        Assert.That(deleted, Is.True);
        Assert.That(await m_store.GetAsync(ToBytes("key1")), Is.Null);
    }

    [Test]
    public async Task DeleteAsyncReturnsFalseForNonExistentKeyTest()
    {
        var deleted = await m_store.DeleteAsync(ToBytes("nonexistent"));

        Assert.That(deleted, Is.False);
    }

    #endregion

    #region Transaction Async Operations Tests

    [Test]
    public async Task TransactionGetAsyncSeesOwnChangesTest()
    {
        await using var tx = await m_store.BeginTransactionAsync();

        await tx.PutAsync(ToBytes("key"), ToBytes("value"));
        var result = await tx.GetAsync(ToBytes("key"));

        Assert.That(result, Is.EqualTo(ToBytes("value")));
    }

    [Test]
    public async Task TransactionCommitAsyncPersistsChangesTest()
    {
        await using var tx = await m_store.BeginTransactionAsync();
        await tx.PutAsync(ToBytes("key1"), ToBytes("value1"));
        await tx.PutAsync(ToBytes("key2"), ToBytes("value2"));
        await tx.CommitAsync();

        Assert.That(tx.State, Is.EqualTo(TransactionState.Committed));
        Assert.That(await m_store.GetAsync(ToBytes("key1")), Is.EqualTo(ToBytes("value1")));
        Assert.That(await m_store.GetAsync(ToBytes("key2")), Is.EqualTo(ToBytes("value2")));
    }

    [Test]
    public async Task TransactionRollbackAsyncDiscardsChangesTest()
    {
        await m_store.PutAsync(ToBytes("existing"), ToBytes("original"));

        await using var tx = await m_store.BeginTransactionAsync();
        await tx.PutAsync(ToBytes("new_key"), ToBytes("new_value"));
        await tx.PutAsync(ToBytes("existing"), ToBytes("modified"));
        await tx.RollbackAsync();

        Assert.That(tx.State, Is.EqualTo(TransactionState.RolledBack));
        Assert.That(await m_store.GetAsync(ToBytes("new_key")), Is.Null);
        Assert.That(await m_store.GetAsync(ToBytes("existing")), Is.EqualTo(ToBytes("original")));
    }

    [Test]
    public async Task TransactionDeleteAsyncIsRolledBackTest()
    {
        await m_store.PutAsync(ToBytes("key"), ToBytes("value"));

        await using var tx = await m_store.BeginTransactionAsync();
        await tx.DeleteAsync(ToBytes("key"));
        Assert.That(await tx.GetAsync(ToBytes("key")), Is.Null);
        await tx.RollbackAsync();

        Assert.That(await m_store.GetAsync(ToBytes("key")), Is.EqualTo(ToBytes("value")));
    }

    [Test]
    public async Task TransactionMultipleOperationsAsyncAtomicTest()
    {
        await using var tx = await m_store.BeginTransactionAsync();

        for (int i = 0; i < 50; i++)
        {
            await tx.PutAsync(ToBytes($"key{i}"), ToBytes($"value{i}"));
        }

        await tx.CommitAsync();

        for (int i = 0; i < 50; i++)
        {
            var value = await m_store.GetAsync(ToBytes($"key{i}"));
            Assert.That(value, Is.Not.Null, $"Key {i} should exist");
        }
    }

    #endregion

    #region Scan Async Tests

    [Test]
    public async Task ScanAsyncReturnsAllKeysTest()
    {
        for (int i = 0; i < 10; i++)
        {
            await m_store.PutAsync(ToBytes($"key{i:D2}"), ToBytes($"value{i}"));
        }

        var results = new List<(byte[] Key, byte[] Value)>();
        await foreach (var item in m_store.ScanAsync(null, null))
        {
            results.Add(item);
        }

        Assert.That(results.Count, Is.EqualTo(10));
    }

    [Test]
    public async Task ScanAsyncWithRangeReturnsFilteredKeysTest()
    {
        for (int i = 0; i < 10; i++)
        {
            await m_store.PutAsync(ToBytes($"key{i:D2}"), ToBytes($"value{i}"));
        }

        var results = new List<(byte[] Key, byte[] Value)>();
        await foreach (var item in m_store.ScanAsync(ToBytes("key03"), ToBytes("key07")))
        {
            results.Add(item);
        }

        Assert.That(results.Count, Is.GreaterThanOrEqualTo(4));
    }

    [Test]
    public async Task ScanAsyncSupportsCancellationTest()
    {
        for (int i = 0; i < 100; i++)
        {
            await m_store.PutAsync(ToBytes($"key{i:D3}"), ToBytes($"value{i}"));
        }

        using var cts = new CancellationTokenSource();
        var count = 0;

        await foreach (var item in m_store.ScanAsync(null, null, cts.Token))
        {
            count++;
            if (count >= 10)
            {
                cts.Cancel();
                break;
            }
        }

        Assert.That(count, Is.EqualTo(10));
    }

    #endregion

    #region Savepoint Async Tests

    [Test]
    public async Task CreateSavepointAsyncCreatesNamedSavepointTest()
    {
        await using var tx = await m_store.BeginTransactionAsync();

        await tx.PutAsync(ToBytes("key1"), ToBytes("value1"));
        await ((ITransactionWithSavepoints)tx).CreateSavepointAsync("sp1");

        Assert.That(((ITransactionWithSavepoints)tx).HasSavepoint("sp1"), Is.True);
    }

    [Test]
    public async Task RollbackToSavepointAsyncRestoresStateTest()
    {
        await using var tx = await m_store.BeginTransactionAsync();
        var txSp = (ITransactionWithSavepoints)tx;

        await tx.PutAsync(ToBytes("key1"), ToBytes("value1"));
        await txSp.CreateSavepointAsync("sp1");
        await tx.PutAsync(ToBytes("key2"), ToBytes("value2"));

        await txSp.RollbackToSavepointAsync("sp1");

        Assert.That(await tx.GetAsync(ToBytes("key1")), Is.EqualTo(ToBytes("value1")));
        Assert.That(await tx.GetAsync(ToBytes("key2")), Is.Null);
    }

    [Test]
    public async Task ReleaseSavepointAsyncRemovesSavepointTest()
    {
        await using var tx = await m_store.BeginTransactionAsync();
        var txSp = (ITransactionWithSavepoints)tx;

        await txSp.CreateSavepointAsync("sp1");
        await txSp.ReleaseSavepointAsync("sp1");

        Assert.That(txSp.HasSavepoint("sp1"), Is.False);
    }

    #endregion

    #region Dispose Async Tests

    [Test]
    public async Task DisposeAsyncRollsBackActiveTransactionTest()
    {
        ITransaction tx = await m_store.BeginTransactionAsync();
        await tx.PutAsync(ToBytes("key"), ToBytes("value"));
        await tx.DisposeAsync();

        Assert.That(tx.State, Is.EqualTo(TransactionState.RolledBack));
        Assert.That(await m_store.GetAsync(ToBytes("key")), Is.Null);
    }

    [Test]
    public async Task FlushAsyncPersistsAllDataTest()
    {
        await m_store.PutAsync(ToBytes("key1"), ToBytes("value1"));
        await m_store.PutAsync(ToBytes("key2"), ToBytes("value2"));
        await m_store.FlushAsync();

        Assert.That(await m_store.GetAsync(ToBytes("key1")), Is.EqualTo(ToBytes("value1")));
        Assert.That(await m_store.GetAsync(ToBytes("key2")), Is.EqualTo(ToBytes("value2")));
    }

    #endregion
}

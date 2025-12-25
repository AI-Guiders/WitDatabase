using OutWit.Database.Core.Concurrency;
using OutWit.Database.Core.Interfaces;
using OutWit.Database.Core.Stores;
using OutWit.Database.Core.Storage;
using OutWit.Database.Core.Transactions;
using OutWit.Database.Core.Wal;
using TextEncoding = System.Text.Encoding;

namespace OutWit.Database.Core.Tests.Transactions;

/// <summary>
/// Unit tests for TransactionalStore component.
/// Tests transaction management and ACID properties.
/// </summary>
[TestFixture]
public class TransactionalStoreTests : IDisposable
{
    private string m_testDir = null!;
    private IKeyValueStore m_underlyingStore = null!;
    private TransactionalStore m_store = null!;

    [SetUp]
    public void SetUp()
    {
        m_testDir = Path.Combine(Path.GetTempPath(), $"tx_store_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(m_testDir);

        var dbPath = Path.Combine(m_testDir, "test.db");
        var storage = new StorageMemory();
        m_underlyingStore = new StoreBTree(storage);
        
        var journal = new WalTransactionJournal(Path.Combine(m_testDir, "test.wal"));
        var lockManager = new LockManager(dbPath);
        
        m_store = new TransactionalStore(m_underlyingStore, journal, lockManager);
    }

    [TearDown]
    public void TearDown()
    {
        Dispose();
    }

    public void Dispose()
    {
        m_store?.Dispose();
        try
        {
            if (Directory.Exists(m_testDir))
                Directory.Delete(m_testDir, recursive: true);
        }
        catch { }
    }

    private static byte[] ToBytes(string s) => TextEncoding.UTF8.GetBytes(s);

    #region Basic Operations Tests

    [Test]
    public void PutWithoutTransactionSucceedsTest()
    {
        m_store.Put(ToBytes("key1"), ToBytes("value1"));
        
        var result = m_store.Get(ToBytes("key1"));
        Assert.That(result, Is.EqualTo(ToBytes("value1")));
    }

    [Test]
    public void GetNonExistentKeyReturnsNullTest()
    {
        var result = m_store.Get(ToBytes("nonexistent"));
        
        Assert.That(result, Is.Null);
    }

    [Test]
    public void DeleteExistingKeySucceedsTest()
    {
        m_store.Put(ToBytes("key1"), ToBytes("value1"));
        
        var deleted = m_store.Delete(ToBytes("key1"));
        
        Assert.That(deleted, Is.True);
        Assert.That(m_store.Get(ToBytes("key1")), Is.Null);
    }

    [Test]
    public void FlushPersistsDataTest()
    {
        m_store.Put(ToBytes("key1"), ToBytes("value1"));
        m_store.Flush();
        
        Assert.That(m_store.Get(ToBytes("key1")), Is.EqualTo(ToBytes("value1")));
    }

    #endregion

    #region Transaction Lifecycle Tests

    [Test]
    public void BeginTransactionReturnsActiveTransactionTest()
    {
        using var tx = m_store.BeginTransaction();
        
        Assert.That(tx, Is.Not.Null);
        Assert.That(tx.State, Is.EqualTo(TransactionState.Active));
    }

    [Test]
    public async Task BeginTransactionAsyncReturnsActiveTransactionTest()
    {
        await using var tx = await m_store.BeginTransactionAsync();
        
        Assert.That(tx, Is.Not.Null);
        Assert.That(tx.State, Is.EqualTo(TransactionState.Active));
    }

    [Test]
    public void CommitSetsStateToCommittedTest()
    {
        using var tx = m_store.BeginTransaction();
        tx.Put(ToBytes("key"), ToBytes("value"));
        tx.Commit();
        
        Assert.That(tx.State, Is.EqualTo(TransactionState.Committed));
    }

    [Test]
    public void RollbackSetsStateToRolledBackTest()
    {
        using var tx = m_store.BeginTransaction();
        tx.Put(ToBytes("key"), ToBytes("value"));
        tx.Rollback();
        
        Assert.That(tx.State, Is.EqualTo(TransactionState.RolledBack));
    }

    [Test]
    public void DisposeRollsBackActiveTransactionTest()
    {
        ITransaction tx = m_store.BeginTransaction();
        tx.Put(ToBytes("key"), ToBytes("value"));
        tx.Dispose();
        
        Assert.That(tx.State, Is.EqualTo(TransactionState.RolledBack));
        Assert.That(m_store.Get(ToBytes("key")), Is.Null);
    }

    #endregion

    #region Transaction Isolation Tests

    [Test]
    public void TransactionSeesOwnChangesTest()
    {
        using var tx = m_store.BeginTransaction();
        
        tx.Put(ToBytes("key"), ToBytes("value"));
        
        var result = tx.Get(ToBytes("key"));
        Assert.That(result, Is.EqualTo(ToBytes("value")));
    }

    [Test]
    public void TransactionCommitPersistsChangesTest()
    {
        using var tx = m_store.BeginTransaction();
        tx.Put(ToBytes("key1"), ToBytes("value1"));
        tx.Put(ToBytes("key2"), ToBytes("value2"));
        tx.Commit();
        
        Assert.That(m_store.Get(ToBytes("key1")), Is.EqualTo(ToBytes("value1")));
        Assert.That(m_store.Get(ToBytes("key2")), Is.EqualTo(ToBytes("value2")));
    }

    [Test]
    public void TransactionRollbackDiscardsChangesTest()
    {
        m_store.Put(ToBytes("existing"), ToBytes("original"));
        
        using var tx = m_store.BeginTransaction();
        tx.Put(ToBytes("new_key"), ToBytes("new_value"));
        tx.Put(ToBytes("existing"), ToBytes("modified"));
        tx.Rollback();
        
        Assert.That(m_store.Get(ToBytes("new_key")), Is.Null, "New key should not exist");
        Assert.That(m_store.Get(ToBytes("existing")), Is.EqualTo(ToBytes("original")), "Existing key should have original value");
    }

    [Test]
    public void TransactionDeleteIsRolledBackTest()
    {
        m_store.Put(ToBytes("key"), ToBytes("value"));
        
        using var tx = m_store.BeginTransaction();
        tx.Delete(ToBytes("key"));
        Assert.That(tx.Get(ToBytes("key")), Is.Null, "Key should be deleted in transaction");
        tx.Rollback();
        
        Assert.That(m_store.Get(ToBytes("key")), Is.EqualTo(ToBytes("value")), "Key should be restored after rollback");
    }

    #endregion

    #region Multiple Operations Tests

    [Test]
    public void TransactionMultipleOperationsAtomicTest()
    {
        using var tx = m_store.BeginTransaction();
        
        for (int i = 0; i < 100; i++)
        {
            tx.Put(ToBytes($"key{i}"), ToBytes($"value{i}"));
        }
        
        tx.Commit();
        
        for (int i = 0; i < 100; i++)
        {
            Assert.That(m_store.Get(ToBytes($"key{i}")), Is.Not.Null, $"Key {i} should exist");
        }
    }

    [Test]
    public void TransactionOverwritesSameKeyTest()
    {
        using var tx = m_store.BeginTransaction();
        
        tx.Put(ToBytes("key"), ToBytes("value1"));
        tx.Put(ToBytes("key"), ToBytes("value2"));
        tx.Put(ToBytes("key"), ToBytes("value3"));
        
        var result = tx.Get(ToBytes("key"));
        Assert.That(result, Is.EqualTo(ToBytes("value3")));
        
        tx.Commit();
        
        Assert.That(m_store.Get(ToBytes("key")), Is.EqualTo(ToBytes("value3")));
    }

    [Test]
    public void TransactionDeleteThenPutTest()
    {
        m_store.Put(ToBytes("key"), ToBytes("original"));
        
        using var tx = m_store.BeginTransaction();
        tx.Delete(ToBytes("key"));
        tx.Put(ToBytes("key"), ToBytes("new_value"));
        tx.Commit();
        
        Assert.That(m_store.Get(ToBytes("key")), Is.EqualTo(ToBytes("new_value")));
    }

    [Test]
    public void TransactionPutThenDeleteTest()
    {
        using var tx = m_store.BeginTransaction();
        tx.Put(ToBytes("key"), ToBytes("value"));
        tx.Delete(ToBytes("key"));
        tx.Commit();
        
        Assert.That(m_store.Get(ToBytes("key")), Is.Null);
    }

    #endregion

    #region Error Handling Tests

    [Test]
    public void CommitAfterCommitThrowsInvalidOperationTest()
    {
        using var tx = m_store.BeginTransaction();
        tx.Put(ToBytes("key"), ToBytes("value"));
        tx.Commit();
        
        Assert.Throws<InvalidOperationException>(() => tx.Commit());
    }

    [Test]
    public void RollbackAfterCommitThrowsInvalidOperationTest()
    {
        using var tx = m_store.BeginTransaction();
        tx.Put(ToBytes("key"), ToBytes("value"));
        tx.Commit();
        
        Assert.Throws<InvalidOperationException>(() => tx.Rollback());
    }

    [Test]
    public void PutAfterCommitThrowsInvalidOperationTest()
    {
        using var tx = m_store.BeginTransaction();
        tx.Commit();
        
        Assert.Throws<InvalidOperationException>(() => tx.Put(ToBytes("key"), ToBytes("value")));
    }

    [Test]
    public void GetAfterCommitThrowsInvalidOperationTest()
    {
        using var tx = m_store.BeginTransaction();
        tx.Commit();
        
        Assert.Throws<InvalidOperationException>(() => tx.Get(ToBytes("key")));
    }

    [Test]
    public void DeleteAfterRollbackThrowsInvalidOperationTest()
    {
        using var tx = m_store.BeginTransaction();
        tx.Rollback();
        
        Assert.Throws<InvalidOperationException>(() => tx.Delete(ToBytes("key")));
    }

    #endregion

    #region Async Tests

    [Test]
    public async Task CommitAsyncPersistsChangesTest()
    {
        await using var tx = await m_store.BeginTransactionAsync();
        await tx.PutAsync(ToBytes("key"), ToBytes("value"));
        await tx.CommitAsync();
        
        Assert.That(tx.State, Is.EqualTo(TransactionState.Committed));
        Assert.That(m_store.Get(ToBytes("key")), Is.EqualTo(ToBytes("value")));
    }

    [Test]
    public async Task RollbackAsyncDiscardsChangesTest()
    {
        await using var tx = await m_store.BeginTransactionAsync();
        await tx.PutAsync(ToBytes("key"), ToBytes("value"));
        await tx.RollbackAsync();
        
        Assert.That(tx.State, Is.EqualTo(TransactionState.RolledBack));
        Assert.That(m_store.Get(ToBytes("key")), Is.Null);
    }

    [Test]
    public async Task DisposeAsyncRollsBackActiveTransactionTest()
    {
        ITransaction tx = await m_store.BeginTransactionAsync();
        await tx.PutAsync(ToBytes("key"), ToBytes("value"));
        await tx.DisposeAsync();
        
        Assert.That(tx.State, Is.EqualTo(TransactionState.RolledBack));
    }

    #endregion

    #region Concurrent Transaction Tests

    [Test]
    public async Task ConcurrentTransactionStartBlocksTest()
    {
        var shortTimeoutStore = CreateStoreWithTimeout(TimeSpan.FromMilliseconds(200));
        
        using var tx1 = shortTimeoutStore.BeginTransaction();
        
        // Try to start second transaction from different thread - should timeout
        var task = Task.Run(() =>
        {
            Assert.Throws<TimeoutException>(() =>
            {
                using var tx2 = shortTimeoutStore.BeginTransaction();
            });
        });
        
        await task;
        
        shortTimeoutStore.Dispose();
    }

    [Test]
    public void TransactionCompletedAllowsNextTest()
    {
        using var tx1 = m_store.BeginTransaction();
        tx1.Put(ToBytes("key1"), ToBytes("value1"));
        tx1.Commit();
        
        using var tx2 = m_store.BeginTransaction();
        tx2.Put(ToBytes("key2"), ToBytes("value2"));
        tx2.Commit();
        
        Assert.That(m_store.Get(ToBytes("key1")), Is.EqualTo(ToBytes("value1")));
        Assert.That(m_store.Get(ToBytes("key2")), Is.EqualTo(ToBytes("value2")));
    }

    #endregion

    #region Scan Tests

    [Test]
    public void ScanReturnsAllKeysTest()
    {
        for (int i = 0; i < 10; i++)
        {
            m_store.Put(ToBytes($"key{i:D2}"), ToBytes($"value{i}"));
        }
        
        var results = m_store.Scan(null, null).ToList();
        
        Assert.That(results.Count, Is.EqualTo(10));
    }

    [Test]
    public void ScanWithRangeReturnsFilteredKeysTest()
    {
        for (int i = 0; i < 10; i++)
        {
            m_store.Put(ToBytes($"key{i:D2}"), ToBytes($"value{i}"));
        }
        
        var results = m_store.Scan(ToBytes("key03"), ToBytes("key07")).ToList();
        
        Assert.That(results.Count, Is.GreaterThanOrEqualTo(4));
    }

    #endregion

    #region Isolation Level Tests

    [Test]
    public void BeginTransactionWithDefaultIsolationLevelTest()
    {
        using var tx = m_store.BeginTransaction();
        
        Assert.That(tx.IsolationLevel, Is.EqualTo(IsolationLevel.ReadCommitted));
    }

    [Test]
    public void BeginTransactionWithSpecificIsolationLevelTest()
    {
        using var tx = m_store.BeginTransaction(IsolationLevel.Serializable);
        
        Assert.That(tx.IsolationLevel, Is.EqualTo(IsolationLevel.Serializable));
    }

    [Test]
    public void BeginTransactionWithReadUncommittedTest()
    {
        using var tx = m_store.BeginTransaction(IsolationLevel.ReadUncommitted);
        
        Assert.That(tx.IsolationLevel, Is.EqualTo(IsolationLevel.ReadUncommitted));
        Assert.That(tx.State, Is.EqualTo(TransactionState.Active));
    }

    [Test]
    public void BeginTransactionWithRepeatableReadTest()
    {
        using var tx = m_store.BeginTransaction(IsolationLevel.RepeatableRead);
        
        Assert.That(tx.IsolationLevel, Is.EqualTo(IsolationLevel.RepeatableRead));
    }

    [Test]
    public void BeginTransactionWithSnapshotTest()
    {
        using var tx = m_store.BeginTransaction(IsolationLevel.Snapshot);
        
        Assert.That(tx.IsolationLevel, Is.EqualTo(IsolationLevel.Snapshot));
    }

    [Test]
    public void BeginTransactionWithInvalidIsolationLevelThrowsTest()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            m_store.BeginTransaction((IsolationLevel)999));
    }

    [Test]
    public async Task BeginTransactionAsyncWithDefaultIsolationLevelTest()
    {
        await using var tx = await m_store.BeginTransactionAsync();
        
        Assert.That(tx.IsolationLevel, Is.EqualTo(IsolationLevel.ReadCommitted));
    }

    [Test]
    public async Task BeginTransactionAsyncWithSpecificIsolationLevelTest()
    {
        await using var tx = await m_store.BeginTransactionAsync(IsolationLevel.Serializable);
        
        Assert.That(tx.IsolationLevel, Is.EqualTo(IsolationLevel.Serializable));
    }

    [Test]
    public async Task BeginTransactionAsyncWithInvalidIsolationLevelThrowsTest()
    {
        Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => 
            await m_store.BeginTransactionAsync((IsolationLevel)999));
    }

    [Test]
    public void TransactionWithIsolationLevelCommitsSuccessfullyTest()
    {
        using var tx = m_store.BeginTransaction(IsolationLevel.Serializable);
        tx.Put(ToBytes("key"), ToBytes("value"));
        tx.Commit();
        
        Assert.That(tx.State, Is.EqualTo(TransactionState.Committed));
        Assert.That(m_store.Get(ToBytes("key")), Is.EqualTo(ToBytes("value")));
    }

    [Test]
    public void TransactionWithIsolationLevelRollsBackSuccessfullyTest()
    {
        using var tx = m_store.BeginTransaction(IsolationLevel.Snapshot);
        tx.Put(ToBytes("key"), ToBytes("value"));
        tx.Rollback();
        
        Assert.That(tx.State, Is.EqualTo(TransactionState.RolledBack));
        Assert.That(m_store.Get(ToBytes("key")), Is.Null);
    }

    #endregion

    #region Helper Methods

    private TransactionalStore CreateStoreWithTimeout(TimeSpan timeout)
    {
        var subDir = Path.Combine(m_testDir, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(subDir);
        
        var storage = new StorageMemory();
        var store = new StoreBTree(storage);
        var journal = new WalTransactionJournal(Path.Combine(subDir, "test.wal"));
        var lockManager = new LockManager(Path.Combine(subDir, "test.db"), timeout);
        
        return new TransactionalStore(store, journal, lockManager);
    }

    #endregion
}

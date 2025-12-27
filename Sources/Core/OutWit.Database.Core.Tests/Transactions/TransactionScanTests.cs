using OutWit.Database.Core.Concurrency;
using OutWit.Database.Core.Interfaces;
using OutWit.Database.Core.Stores;
using OutWit.Database.Core.Storage;
using OutWit.Database.Core.Transactions;
using OutWit.Database.Core.Wal;
using TextEncoding = System.Text.Encoding;

namespace OutWit.Database.Core.Tests.Transactions;

/// <summary>
/// Tests for ITransaction.Scan functionality.
/// Validates that Scan correctly merges uncommitted changes with store data.
/// </summary>
[TestFixture]
public class TransactionScanTests : IDisposable
{
    #region Fields

    private string m_testDir = null!;
    private IKeyValueStore m_underlyingStore = null!;
    private TransactionalStore m_store = null!;

    #endregion

    #region Setup/Teardown

    [SetUp]
    public void SetUp()
    {
        m_testDir = Path.Combine(Path.GetTempPath(), $"tx_scan_test_{Guid.NewGuid():N}");
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

    #endregion

    #region Helpers

    private static byte[] ToBytes(string s) => TextEncoding.UTF8.GetBytes(s);
    private static string ToString(byte[] bytes) => TextEncoding.UTF8.GetString(bytes);

    #endregion

    #region Basic Scan Tests

    [Test]
    public void ScanWithNoChangesReturnsStoreDataTest()
    {
        // Arrange - add data outside transaction
        m_store.Put(ToBytes("key1"), ToBytes("value1"));
        m_store.Put(ToBytes("key2"), ToBytes("value2"));
        m_store.Put(ToBytes("key3"), ToBytes("value3"));

        // Act
        using var tx = m_store.BeginTransaction();
        var results = tx.Scan(null, null).ToList();

        // Assert
        Assert.That(results, Has.Count.EqualTo(3));
        Assert.That(results.Any(r => ToString(r.Key) == "key1" && ToString(r.Value) == "value1"), Is.True);
        Assert.That(results.Any(r => ToString(r.Key) == "key2" && ToString(r.Value) == "value2"), Is.True);
        Assert.That(results.Any(r => ToString(r.Key) == "key3" && ToString(r.Value) == "value3"), Is.True);
    }

    [Test]
    public void ScanWithEmptyStoreAndNoChangesReturnsEmptyTest()
    {
        using var tx = m_store.BeginTransaction();
        var results = tx.Scan(null, null).ToList();

        Assert.That(results, Is.Empty);
    }

    [Test]
    public void ScanAfterCommitThrowsTest()
    {
        using var tx = m_store.BeginTransaction();
        tx.Commit();

        Assert.Throws<InvalidOperationException>(() => tx.Scan(null, null).ToList());
    }

    [Test]
    public void ScanAfterRollbackThrowsTest()
    {
        using var tx = m_store.BeginTransaction();
        tx.Rollback();

        Assert.Throws<InvalidOperationException>(() => tx.Scan(null, null).ToList());
    }

    #endregion

    #region Scan Sees Uncommitted Inserts Tests

    [Test]
    public void ScanSeesUncommittedInsertTest()
    {
        using var tx = m_store.BeginTransaction();
        
        tx.Put(ToBytes("key1"), ToBytes("value1"));
        
        var results = tx.Scan(null, null).ToList();

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(ToString(results[0].Key), Is.EqualTo("key1"));
        Assert.That(ToString(results[0].Value), Is.EqualTo("value1"));
    }

    [Test]
    public void ScanSeesMultipleUncommittedInsertsTest()
    {
        using var tx = m_store.BeginTransaction();
        
        tx.Put(ToBytes("key1"), ToBytes("value1"));
        tx.Put(ToBytes("key2"), ToBytes("value2"));
        tx.Put(ToBytes("key3"), ToBytes("value3"));
        
        var results = tx.Scan(null, null).ToList();

        Assert.That(results, Has.Count.EqualTo(3));
    }

    [Test]
    public void ScanSeesUncommittedInsertsMergedWithStoreDataTest()
    {
        // Arrange - existing data
        m_store.Put(ToBytes("key1"), ToBytes("existing1"));
        m_store.Put(ToBytes("key3"), ToBytes("existing3"));

        // Act
        using var tx = m_store.BeginTransaction();
        tx.Put(ToBytes("key2"), ToBytes("new2"));
        tx.Put(ToBytes("key4"), ToBytes("new4"));
        
        var results = tx.Scan(null, null).ToList();

        // Assert - should see all 4 keys
        Assert.That(results, Has.Count.EqualTo(4));
        Assert.That(results.Any(r => ToString(r.Key) == "key1" && ToString(r.Value) == "existing1"), Is.True);
        Assert.That(results.Any(r => ToString(r.Key) == "key2" && ToString(r.Value) == "new2"), Is.True);
        Assert.That(results.Any(r => ToString(r.Key) == "key3" && ToString(r.Value) == "existing3"), Is.True);
        Assert.That(results.Any(r => ToString(r.Key) == "key4" && ToString(r.Value) == "new4"), Is.True);
    }

    #endregion

    #region Scan Sees Uncommitted Updates Tests

    [Test]
    public void ScanSeesUncommittedUpdateTest()
    {
        // Arrange
        m_store.Put(ToBytes("key1"), ToBytes("original"));

        // Act
        using var tx = m_store.BeginTransaction();
        tx.Put(ToBytes("key1"), ToBytes("updated"));
        
        var results = tx.Scan(null, null).ToList();

        // Assert - should see updated value
        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(ToString(results[0].Value), Is.EqualTo("updated"));
    }

    [Test]
    public void ScanSeesMultipleUpdatesInSequenceTest()
    {
        // Arrange
        m_store.Put(ToBytes("key1"), ToBytes("original"));

        // Act
        using var tx = m_store.BeginTransaction();
        tx.Put(ToBytes("key1"), ToBytes("update1"));
        tx.Put(ToBytes("key1"), ToBytes("update2"));
        tx.Put(ToBytes("key1"), ToBytes("update3"));
        
        var results = tx.Scan(null, null).ToList();

        // Assert - should see final update
        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(ToString(results[0].Value), Is.EqualTo("update3"));
    }

    #endregion

    #region Scan Sees Uncommitted Deletes Tests

    [Test]
    public void ScanExcludesUncommittedDeleteTest()
    {
        // Arrange
        m_store.Put(ToBytes("key1"), ToBytes("value1"));
        m_store.Put(ToBytes("key2"), ToBytes("value2"));

        // Act
        using var tx = m_store.BeginTransaction();
        tx.Delete(ToBytes("key1"));
        
        var results = tx.Scan(null, null).ToList();

        // Assert - should only see key2
        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(ToString(results[0].Key), Is.EqualTo("key2"));
    }

    [Test]
    public void ScanExcludesAllUncommittedDeletesTest()
    {
        // Arrange
        m_store.Put(ToBytes("key1"), ToBytes("value1"));
        m_store.Put(ToBytes("key2"), ToBytes("value2"));
        m_store.Put(ToBytes("key3"), ToBytes("value3"));

        // Act
        using var tx = m_store.BeginTransaction();
        tx.Delete(ToBytes("key1"));
        tx.Delete(ToBytes("key2"));
        tx.Delete(ToBytes("key3"));
        
        var results = tx.Scan(null, null).ToList();

        // Assert - should be empty
        Assert.That(results, Is.Empty);
    }

    [Test]
    public void ScanAfterDeleteThenInsertShowsNewValueTest()
    {
        // Arrange
        m_store.Put(ToBytes("key1"), ToBytes("original"));

        // Act
        using var tx = m_store.BeginTransaction();
        tx.Delete(ToBytes("key1"));
        tx.Put(ToBytes("key1"), ToBytes("reinserted"));
        
        var results = tx.Scan(null, null).ToList();

        // Assert - should see reinserted value
        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(ToString(results[0].Value), Is.EqualTo("reinserted"));
    }

    [Test]
    public void ScanAfterInsertThenDeleteShowsNothingTest()
    {
        // Act
        using var tx = m_store.BeginTransaction();
        tx.Put(ToBytes("key1"), ToBytes("inserted"));
        tx.Delete(ToBytes("key1"));
        
        var results = tx.Scan(null, null).ToList();

        // Assert - should be empty
        Assert.That(results, Is.Empty);
    }

    #endregion

    #region Scan Range Tests

    [Test]
    public void ScanWithStartKeyFiltersCorrectlyTest()
    {
        // Arrange
        m_store.Put(ToBytes("a_key"), ToBytes("value_a"));
        m_store.Put(ToBytes("b_key"), ToBytes("value_b"));
        m_store.Put(ToBytes("c_key"), ToBytes("value_c"));

        // Act
        using var tx = m_store.BeginTransaction();
        var results = tx.Scan(ToBytes("b_key"), null).ToList();

        // Assert - should see b_key and c_key
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results.All(r => ToString(r.Key).CompareTo("b_key") >= 0), Is.True);
    }

    [Test]
    public void ScanWithEndKeyFiltersCorrectlyTest()
    {
        // Arrange
        m_store.Put(ToBytes("a_key"), ToBytes("value_a"));
        m_store.Put(ToBytes("b_key"), ToBytes("value_b"));
        m_store.Put(ToBytes("c_key"), ToBytes("value_c"));

        // Act
        using var tx = m_store.BeginTransaction();
        var results = tx.Scan(null, ToBytes("c_key")).ToList();

        // Assert - should see a_key and b_key (end key is exclusive)
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results.All(r => ToString(r.Key).CompareTo("c_key") < 0), Is.True);
    }

    [Test]
    public void ScanWithRangeFiltersCorrectlyTest()
    {
        // Arrange
        m_store.Put(ToBytes("a_key"), ToBytes("value_a"));
        m_store.Put(ToBytes("b_key"), ToBytes("value_b"));
        m_store.Put(ToBytes("c_key"), ToBytes("value_c"));
        m_store.Put(ToBytes("d_key"), ToBytes("value_d"));

        // Act
        using var tx = m_store.BeginTransaction();
        var results = tx.Scan(ToBytes("b_key"), ToBytes("d_key")).ToList();

        // Assert - should see b_key and c_key
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results.Any(r => ToString(r.Key) == "b_key"), Is.True);
        Assert.That(results.Any(r => ToString(r.Key) == "c_key"), Is.True);
    }

    [Test]
    public void ScanRangeIncludesUncommittedInsertsInRangeTest()
    {
        // Arrange
        m_store.Put(ToBytes("a_key"), ToBytes("value_a"));
        m_store.Put(ToBytes("d_key"), ToBytes("value_d"));

        // Act
        using var tx = m_store.BeginTransaction();
        tx.Put(ToBytes("b_key"), ToBytes("new_b")); // In range
        tx.Put(ToBytes("e_key"), ToBytes("new_e")); // Out of range
        
        var results = tx.Scan(ToBytes("a_key"), ToBytes("d_key")).ToList();

        // Assert - should see a_key, b_key, c_key but NOT d_key (end exclusive) or e_key (out of range)
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results.Any(r => ToString(r.Key) == "a_key"), Is.True);
        Assert.That(results.Any(r => ToString(r.Key) == "b_key"), Is.True);
        Assert.That(results.Any(r => ToString(r.Key) == "e_key"), Is.False);
    }

    [Test]
    public void ScanRangeExcludesUncommittedDeletesTest()
    {
        // Arrange
        m_store.Put(ToBytes("a_key"), ToBytes("value_a"));
        m_store.Put(ToBytes("b_key"), ToBytes("value_b"));
        m_store.Put(ToBytes("c_key"), ToBytes("value_c"));

        // Act
        using var tx = m_store.BeginTransaction();
        tx.Delete(ToBytes("b_key"));
        
        var results = tx.Scan(ToBytes("a_key"), ToBytes("d_key")).ToList();

        // Assert - should see a_key and c_key, NOT b_key
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results.Any(r => ToString(r.Key) == "a_key"), Is.True);
        Assert.That(results.Any(r => ToString(r.Key) == "c_key"), Is.True);
        Assert.That(results.Any(r => ToString(r.Key) == "b_key"), Is.False);
    }

    #endregion

    #region Scan Ordering Tests

    [Test]
    public void ScanReturnsResultsInSortedOrderTest()
    {
        // Arrange - insert in random order
        m_store.Put(ToBytes("key_c"), ToBytes("value_c"));
        m_store.Put(ToBytes("key_a"), ToBytes("value_a"));
        m_store.Put(ToBytes("key_b"), ToBytes("value_b"));

        // Act
        using var tx = m_store.BeginTransaction();
        var results = tx.Scan(null, null).ToList();

        // Assert - should be sorted
        Assert.That(results, Has.Count.EqualTo(3));
        var keys = results.Select(r => ToString(r.Key)).ToList();
        Assert.That(keys, Is.Ordered);
    }

    [Test]
    public void ScanWithUncommittedInsertsMaintainsSortOrderTest()
    {
        // Arrange
        m_store.Put(ToBytes("key_a"), ToBytes("value_a"));
        m_store.Put(ToBytes("key_c"), ToBytes("value_c"));

        // Act
        using var tx = m_store.BeginTransaction();
        tx.Put(ToBytes("key_b"), ToBytes("new_b")); // Insert between a and c
        
        var results = tx.Scan(null, null).ToList();

        // Assert - should be sorted: a, b, c
        Assert.That(results, Has.Count.EqualTo(3));
        var keys = results.Select(r => ToString(r.Key)).ToList();
        Assert.That(keys, Is.EqualTo(new[] { "key_a", "key_b", "key_c" }));
    }

    #endregion

    #region ScanAsync Tests

    [Test]
    public async Task ScanAsyncReturnsCorrectResultsTest()
    {
        // Arrange
        m_store.Put(ToBytes("key1"), ToBytes("value1"));
        m_store.Put(ToBytes("key2"), ToBytes("value2"));

        // Act
        await using var tx = await m_store.BeginTransactionAsync();
        tx.Put(ToBytes("key3"), ToBytes("value3"));
        
        var results = new List<(byte[] Key, byte[] Value)>();
        await foreach (var item in tx.ScanAsync(null, null))
        {
            results.Add(item);
        }

        // Assert
        Assert.That(results, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task ScanAsyncSupportsCancellationTest()
    {
        // Arrange
        for (int i = 0; i < 100; i++)
        {
            m_store.Put(ToBytes($"key{i:D3}"), ToBytes($"value{i}"));
        }

        await using var tx = await m_store.BeginTransactionAsync();
        
        using var cts = new CancellationTokenSource();
        var results = new List<(byte[] Key, byte[] Value)>();
        
        // Act - cancel after a few items
        await foreach (var item in tx.ScanAsync(null, null, cts.Token))
        {
            results.Add(item);
            if (results.Count >= 10)
            {
                cts.Cancel();
                break;
            }
        }

        // Assert - should have stopped after cancellation
        Assert.That(results.Count, Is.LessThanOrEqualTo(11));
    }

    [Test]
    public async Task ScanAsyncAfterCommitThrowsTest()
    {
        await using var tx = await m_store.BeginTransactionAsync();
        await tx.CommitAsync();

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in tx.ScanAsync(null, null))
            {
                // Should not reach here
            }
        });
    }

    #endregion

    #region Complex Scenario Tests

    [Test]
    public void ScanWithMixedOperationsTest()
    {
        // Arrange - existing data
        m_store.Put(ToBytes("key1"), ToBytes("original1"));
        m_store.Put(ToBytes("key2"), ToBytes("original2"));
        m_store.Put(ToBytes("key3"), ToBytes("original3"));
        m_store.Put(ToBytes("key4"), ToBytes("original4"));

        // Act
        using var tx = m_store.BeginTransaction();
        tx.Delete(ToBytes("key1"));                    // Delete existing
        tx.Put(ToBytes("key2"), ToBytes("updated2"));  // Update existing
        tx.Put(ToBytes("key5"), ToBytes("new5"));      // Insert new
        // key3 and key4 unchanged
        
        var results = tx.Scan(null, null).ToList();

        // Assert
        Assert.That(results, Has.Count.EqualTo(4)); // key2, key3, key4, key5
        Assert.That(results.Any(r => ToString(r.Key) == "key1"), Is.False, "key1 should be deleted");
        Assert.That(results.Any(r => ToString(r.Key) == "key2" && ToString(r.Value) == "updated2"), Is.True, "key2 should be updated");
        Assert.That(results.Any(r => ToString(r.Key) == "key3" && ToString(r.Value) == "original3"), Is.True, "key3 should be unchanged");
        Assert.That(results.Any(r => ToString(r.Key) == "key4" && ToString(r.Value) == "original4"), Is.True, "key4 should be unchanged");
        Assert.That(results.Any(r => ToString(r.Key) == "key5" && ToString(r.Value) == "new5"), Is.True, "key5 should be new");
    }

    [Test]
    public void ScanAfterSavepointRollbackTest()
    {
        // Arrange
        m_store.Put(ToBytes("key1"), ToBytes("original1"));

        // Act
        using var tx = m_store.BeginTransaction();
        tx.Put(ToBytes("key2"), ToBytes("value2"));
        
        // Verify we see 2 keys
        var resultsBeforeSavepoint = tx.Scan(null, null).ToList();
        Assert.That(resultsBeforeSavepoint, Has.Count.EqualTo(2));
        
        // Create savepoint
        ((ITransactionWithSavepoints)tx).CreateSavepoint("sp1");
        
        tx.Put(ToBytes("key3"), ToBytes("value3"));
        tx.Delete(ToBytes("key1"));
        
        // Verify we see key2 and key3, but not key1
        var resultsAfterChanges = tx.Scan(null, null).ToList();
        Assert.That(resultsAfterChanges, Has.Count.EqualTo(2));
        
        // Rollback to savepoint
        ((ITransactionWithSavepoints)tx).RollbackToSavepoint("sp1");
        
        // Verify we're back to key1 and key2
        var resultsAfterRollback = tx.Scan(null, null).ToList();
        Assert.That(resultsAfterRollback, Has.Count.EqualTo(2));
        Assert.That(resultsAfterRollback.Any(r => ToString(r.Key) == "key1"), Is.True);
        Assert.That(resultsAfterRollback.Any(r => ToString(r.Key) == "key2"), Is.True);
    }

    #endregion
}

using NUnit.Framework;
using OutWit.Database.Core.Interfaces;
using OutWit.Database.Core.Stores;
using OutWit.Database.Core.Transactions;

namespace OutWit.Database.Core.Tests.Transactions
{
    [TestFixture]
    public class SavepointTests
    {
        #region Fields

        private TransactionalStore m_store = null!;

        #endregion

        #region Setup

        [SetUp]
        public void Setup()
        {
            var innerStore = new StoreInMemory();
            m_store = new TransactionalStore(innerStore, ownsStore: true);
        }

        [TearDown]
        public void TearDown()
        {
            m_store.Dispose();
        }

        #endregion

        #region CreateSavepoint Tests

        [Test]
        public void CreateSavepointSucceedsTest()
        {
            // Arrange
            using var tx = m_store.BeginTransaction();
            var txWithSavepoints = (ITransactionWithSavepoints)tx;

            // Act
            txWithSavepoints.CreateSavepoint("sp1");

            // Assert
            Assert.That(txWithSavepoints.Savepoints, Has.Count.EqualTo(1));
            Assert.That(txWithSavepoints.Savepoints[0], Is.EqualTo("sp1"));
            Assert.That(txWithSavepoints.HasSavepoint("sp1"), Is.True);
        }

        [Test]
        public void CreateMultipleSavepointsSucceedsTest()
        {
            // Arrange
            using var tx = m_store.BeginTransaction();
            var txWithSavepoints = (ITransactionWithSavepoints)tx;

            // Act
            txWithSavepoints.CreateSavepoint("sp1");
            txWithSavepoints.CreateSavepoint("sp2");
            txWithSavepoints.CreateSavepoint("sp3");

            // Assert
            Assert.That(txWithSavepoints.Savepoints, Has.Count.EqualTo(3));
            Assert.That(txWithSavepoints.Savepoints, Is.EqualTo(new[] { "sp1", "sp2", "sp3" }));
        }

        [Test]
        public void CreateSavepointWithDuplicateNameThrowsTest()
        {
            // Arrange
            using var tx = m_store.BeginTransaction();
            var txWithSavepoints = (ITransactionWithSavepoints)tx;
            txWithSavepoints.CreateSavepoint("sp1");

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => txWithSavepoints.CreateSavepoint("sp1"));
            Assert.That(ex!.Message, Does.Contain("sp1"));
        }

        [Test]
        public void CreateSavepointWithNullNameThrowsTest()
        {
            // Arrange
            using var tx = m_store.BeginTransaction();
            var txWithSavepoints = (ITransactionWithSavepoints)tx;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => txWithSavepoints.CreateSavepoint(null!));
        }

        [Test]
        public void CreateSavepointWithEmptyNameThrowsTest()
        {
            // Arrange
            using var tx = m_store.BeginTransaction();
            var txWithSavepoints = (ITransactionWithSavepoints)tx;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => txWithSavepoints.CreateSavepoint(string.Empty));
        }

        [Test]
        public void CreateSavepointOnCommittedTransactionThrowsTest()
        {
            // Arrange
            using var tx = m_store.BeginTransaction();
            var txWithSavepoints = (ITransactionWithSavepoints)tx;
            tx.Commit();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => txWithSavepoints.CreateSavepoint("sp1"));
        }

        #endregion

        #region RollbackToSavepoint Tests

        [Test]
        public void RollbackToSavepointRestoresStateTest()
        {
            // Arrange
            using var tx = m_store.BeginTransaction();
            var txWithSavepoints = (ITransactionWithSavepoints)tx;

            var key1 = System.Text.Encoding.UTF8.GetBytes("key1");
            var value1 = System.Text.Encoding.UTF8.GetBytes("value1");
            var value2 = System.Text.Encoding.UTF8.GetBytes("value2");

            tx.Put(key1, value1);
            txWithSavepoints.CreateSavepoint("sp1");
            tx.Put(key1, value2);

            // Act
            txWithSavepoints.RollbackToSavepoint("sp1");

            // Assert
            var result = tx.Get(key1);
            Assert.That(result, Is.EqualTo(value1));
        }

        [Test]
        public void RollbackToSavepointRemovesLaterSavepointsTest()
        {
            // Arrange
            using var tx = m_store.BeginTransaction();
            var txWithSavepoints = (ITransactionWithSavepoints)tx;

            txWithSavepoints.CreateSavepoint("sp1");
            txWithSavepoints.CreateSavepoint("sp2");
            txWithSavepoints.CreateSavepoint("sp3");

            // Act
            txWithSavepoints.RollbackToSavepoint("sp1");

            // Assert
            Assert.That(txWithSavepoints.Savepoints, Has.Count.EqualTo(1));
            Assert.That(txWithSavepoints.Savepoints[0], Is.EqualTo("sp1"));
            Assert.That(txWithSavepoints.HasSavepoint("sp2"), Is.False);
            Assert.That(txWithSavepoints.HasSavepoint("sp3"), Is.False);
        }

        [Test]
        public void RollbackToSavepointUndoesDeleteTest()
        {
            // Arrange
            var key1 = System.Text.Encoding.UTF8.GetBytes("key1");
            var value1 = System.Text.Encoding.UTF8.GetBytes("value1");
            m_store.Put(key1, value1);

            using var tx = m_store.BeginTransaction();
            var txWithSavepoints = (ITransactionWithSavepoints)tx;

            txWithSavepoints.CreateSavepoint("sp1");
            tx.Delete(key1);

            // Act
            txWithSavepoints.RollbackToSavepoint("sp1");

            // Assert
            var result = tx.Get(key1);
            Assert.That(result, Is.EqualTo(value1));
        }

        [Test]
        public void RollbackToNonExistentSavepointThrowsTest()
        {
            // Arrange
            using var tx = m_store.BeginTransaction();
            var txWithSavepoints = (ITransactionWithSavepoints)tx;

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => txWithSavepoints.RollbackToSavepoint("nonexistent"));
            Assert.That(ex!.Message, Does.Contain("nonexistent"));
        }

        [Test]
        public void RollbackToSavepointCanBeUsedMultipleTimesTest()
        {
            // Arrange
            using var tx = m_store.BeginTransaction();
            var txWithSavepoints = (ITransactionWithSavepoints)tx;

            var key1 = System.Text.Encoding.UTF8.GetBytes("key1");
            var value1 = System.Text.Encoding.UTF8.GetBytes("value1");
            var value2 = System.Text.Encoding.UTF8.GetBytes("value2");
            var value3 = System.Text.Encoding.UTF8.GetBytes("value3");

            tx.Put(key1, value1);
            txWithSavepoints.CreateSavepoint("sp1");

            // First modification and rollback
            tx.Put(key1, value2);
            txWithSavepoints.RollbackToSavepoint("sp1");
            Assert.That(tx.Get(key1), Is.EqualTo(value1));

            // Second modification and rollback
            tx.Put(key1, value3);
            txWithSavepoints.RollbackToSavepoint("sp1");
            Assert.That(tx.Get(key1), Is.EqualTo(value1));
        }

        #endregion

        #region ReleaseSavepoint Tests

        [Test]
        public void ReleaseSavepointRemovesSavepointTest()
        {
            // Arrange
            using var tx = m_store.BeginTransaction();
            var txWithSavepoints = (ITransactionWithSavepoints)tx;
            txWithSavepoints.CreateSavepoint("sp1");

            // Act
            txWithSavepoints.ReleaseSavepoint("sp1");

            // Assert
            Assert.That(txWithSavepoints.Savepoints, Is.Empty);
            Assert.That(txWithSavepoints.HasSavepoint("sp1"), Is.False);
        }

        [Test]
        public void ReleaseSavepointRemovesLaterSavepointsTest()
        {
            // Arrange
            using var tx = m_store.BeginTransaction();
            var txWithSavepoints = (ITransactionWithSavepoints)tx;

            txWithSavepoints.CreateSavepoint("sp1");
            txWithSavepoints.CreateSavepoint("sp2");
            txWithSavepoints.CreateSavepoint("sp3");

            // Act
            txWithSavepoints.ReleaseSavepoint("sp2");

            // Assert
            Assert.That(txWithSavepoints.Savepoints, Has.Count.EqualTo(1));
            Assert.That(txWithSavepoints.Savepoints[0], Is.EqualTo("sp1"));
        }

        [Test]
        public void ReleaseSavepointKeepsChangesTest()
        {
            // Arrange
            using var tx = m_store.BeginTransaction();
            var txWithSavepoints = (ITransactionWithSavepoints)tx;

            var key1 = System.Text.Encoding.UTF8.GetBytes("key1");
            var value1 = System.Text.Encoding.UTF8.GetBytes("value1");
            var value2 = System.Text.Encoding.UTF8.GetBytes("value2");

            tx.Put(key1, value1);
            txWithSavepoints.CreateSavepoint("sp1");
            tx.Put(key1, value2);

            // Act
            txWithSavepoints.ReleaseSavepoint("sp1");

            // Assert - value should still be value2
            var result = tx.Get(key1);
            Assert.That(result, Is.EqualTo(value2));
        }

        [Test]
        public void ReleaseNonExistentSavepointThrowsTest()
        {
            // Arrange
            using var tx = m_store.BeginTransaction();
            var txWithSavepoints = (ITransactionWithSavepoints)tx;

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => txWithSavepoints.ReleaseSavepoint("nonexistent"));
            Assert.That(ex!.Message, Does.Contain("nonexistent"));
        }

        #endregion

        #region HasSavepoint Tests

        [Test]
        public void HasSavepointReturnsTrueForExistingSavepointTest()
        {
            // Arrange
            using var tx = m_store.BeginTransaction();
            var txWithSavepoints = (ITransactionWithSavepoints)tx;
            txWithSavepoints.CreateSavepoint("sp1");

            // Act & Assert
            Assert.That(txWithSavepoints.HasSavepoint("sp1"), Is.True);
        }

        [Test]
        public void HasSavepointReturnsFalseForNonExistingSavepointTest()
        {
            // Arrange
            using var tx = m_store.BeginTransaction();
            var txWithSavepoints = (ITransactionWithSavepoints)tx;

            // Act & Assert
            Assert.That(txWithSavepoints.HasSavepoint("sp1"), Is.False);
        }

        [Test]
        public void HasSavepointReturnsFalseForNullNameTest()
        {
            // Arrange
            using var tx = m_store.BeginTransaction();
            var txWithSavepoints = (ITransactionWithSavepoints)tx;

            // Act & Assert
            Assert.That(txWithSavepoints.HasSavepoint(null!), Is.False);
        }

        [Test]
        public void HasSavepointReturnsFalseForEmptyNameTest()
        {
            // Arrange
            using var tx = m_store.BeginTransaction();
            var txWithSavepoints = (ITransactionWithSavepoints)tx;

            // Act & Assert
            Assert.That(txWithSavepoints.HasSavepoint(string.Empty), Is.False);
        }

        #endregion

        #region Commit/Rollback Clear Savepoints Tests

        [Test]
        public void CommitClearsSavepointsTest()
        {
            // Arrange
            using var tx = m_store.BeginTransaction();
            var txWithSavepoints = (ITransactionWithSavepoints)tx;

            txWithSavepoints.CreateSavepoint("sp1");
            txWithSavepoints.CreateSavepoint("sp2");

            // Act
            tx.Commit();

            // Assert
            Assert.That(txWithSavepoints.Savepoints, Is.Empty);
        }

        [Test]
        public void RollbackClearsSavepointsTest()
        {
            // Arrange
            using var tx = m_store.BeginTransaction();
            var txWithSavepoints = (ITransactionWithSavepoints)tx;

            txWithSavepoints.CreateSavepoint("sp1");
            txWithSavepoints.CreateSavepoint("sp2");

            // Act
            tx.Rollback();

            // Assert
            Assert.That(txWithSavepoints.Savepoints, Is.Empty);
        }

        #endregion

        #region Async Tests

        [Test]
        public async Task CreateSavepointAsyncSucceedsTest()
        {
            // Arrange
            await using var tx = await m_store.BeginTransactionAsync();
            var txWithSavepoints = (ITransactionWithSavepoints)tx;

            // Act
            await txWithSavepoints.CreateSavepointAsync("sp1");

            // Assert
            Assert.That(txWithSavepoints.HasSavepoint("sp1"), Is.True);
        }

        [Test]
        public async Task RollbackToSavepointAsyncRestoresStateTest()
        {
            // Arrange
            await using var tx = await m_store.BeginTransactionAsync();
            var txWithSavepoints = (ITransactionWithSavepoints)tx;

            var key1 = System.Text.Encoding.UTF8.GetBytes("key1");
            var value1 = System.Text.Encoding.UTF8.GetBytes("value1");
            var value2 = System.Text.Encoding.UTF8.GetBytes("value2");

            await tx.PutAsync(key1, value1);
            await txWithSavepoints.CreateSavepointAsync("sp1");
            await tx.PutAsync(key1, value2);

            // Act
            await txWithSavepoints.RollbackToSavepointAsync("sp1");

            // Assert
            var result = await tx.GetAsync(key1);
            Assert.That(result, Is.EqualTo(value1));
        }

        [Test]
        public async Task ReleaseSavepointAsyncSucceedsTest()
        {
            // Arrange
            await using var tx = await m_store.BeginTransactionAsync();
            var txWithSavepoints = (ITransactionWithSavepoints)tx;
            await txWithSavepoints.CreateSavepointAsync("sp1");

            // Act
            await txWithSavepoints.ReleaseSavepointAsync("sp1");

            // Assert
            Assert.That(txWithSavepoints.HasSavepoint("sp1"), Is.False);
        }

        [Test]
        public async Task AsyncCancellationWorksForSavepointsTest()
        {
            // Arrange
            await using var tx = await m_store.BeginTransactionAsync();
            var txWithSavepoints = (ITransactionWithSavepoints)tx;
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            Assert.ThrowsAsync<OperationCanceledException>(
                async () => await txWithSavepoints.CreateSavepointAsync("sp1", cts.Token));
        }

        #endregion

        #region Complex Scenario Tests

        [Test]
        public void NestedSavepointsWorkCorrectlyTest()
        {
            // Arrange
            using var tx = m_store.BeginTransaction();
            var txWithSavepoints = (ITransactionWithSavepoints)tx;

            var key = System.Text.Encoding.UTF8.GetBytes("key");
            var value1 = System.Text.Encoding.UTF8.GetBytes("v1");
            var value2 = System.Text.Encoding.UTF8.GetBytes("v2");
            var value3 = System.Text.Encoding.UTF8.GetBytes("v3");

            // Create nested savepoints with different values
            tx.Put(key, value1);
            txWithSavepoints.CreateSavepoint("sp1");

            tx.Put(key, value2);
            txWithSavepoints.CreateSavepoint("sp2");

            tx.Put(key, value3);
            txWithSavepoints.CreateSavepoint("sp3");

            // Act & Assert
            // Rollback to sp2 - should get value2
            txWithSavepoints.RollbackToSavepoint("sp2");
            Assert.That(tx.Get(key), Is.EqualTo(value2));
            Assert.That(txWithSavepoints.Savepoints, Has.Count.EqualTo(2)); // sp1, sp2

            // Rollback to sp1 - should get value1
            txWithSavepoints.RollbackToSavepoint("sp1");
            Assert.That(tx.Get(key), Is.EqualTo(value1));
            Assert.That(txWithSavepoints.Savepoints, Has.Count.EqualTo(1)); // sp1
        }

        [Test]
        public void SavepointWithMultipleKeysTest()
        {
            // Arrange
            using var tx = m_store.BeginTransaction();
            var txWithSavepoints = (ITransactionWithSavepoints)tx;

            var key1 = System.Text.Encoding.UTF8.GetBytes("key1");
            var key2 = System.Text.Encoding.UTF8.GetBytes("key2");
            var key3 = System.Text.Encoding.UTF8.GetBytes("key3");
            var value = System.Text.Encoding.UTF8.GetBytes("value");
            var newValue = System.Text.Encoding.UTF8.GetBytes("newValue");

            tx.Put(key1, value);
            tx.Put(key2, value);
            txWithSavepoints.CreateSavepoint("sp1");

            // Modify multiple keys
            tx.Put(key1, newValue);
            tx.Delete(key2);
            tx.Put(key3, newValue);

            // Act
            txWithSavepoints.RollbackToSavepoint("sp1");

            // Assert
            Assert.That(tx.Get(key1), Is.EqualTo(value));
            Assert.That(tx.Get(key2), Is.EqualTo(value));
            Assert.That(tx.Get(key3), Is.Null);
        }

        #endregion
    }
}

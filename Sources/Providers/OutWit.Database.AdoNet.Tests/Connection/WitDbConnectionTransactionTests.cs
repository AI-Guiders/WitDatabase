using NUnit.Framework;
using System.Data;

namespace OutWit.Database.AdoNet.Tests.Connection;

/// <summary>
/// Tests for WitDbConnection transaction support.
/// </summary>
[TestFixture]
public class WitDbConnectionTransactionTests
{
    #region BeginTransaction Tests

    [Test]
    public void BeginTransactionReturnsTransactionTest()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        connection.Open();

        using var transaction = connection.BeginTransaction();

        Assert.That(transaction, Is.Not.Null);
        Assert.That(transaction, Is.InstanceOf<WitDbTransaction>());
    }

    [Test]
    public void BeginTransactionHasCorrectConnectionTest()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        connection.Open();

        using var transaction = connection.BeginTransaction();

        Assert.That(transaction.Connection, Is.SameAs(connection));
    }

    [Test]
    public void BeginTransactionWithIsolationLevelSetsCorrectLevelTest()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        connection.Open();

        using var transaction = connection.BeginTransaction(IsolationLevel.Snapshot);

        Assert.That(transaction.IsolationLevel, Is.EqualTo(IsolationLevel.Snapshot));
    }

    [TestCase(IsolationLevel.ReadUncommitted)]
    [TestCase(IsolationLevel.ReadCommitted)]
    [TestCase(IsolationLevel.RepeatableRead)]
    [TestCase(IsolationLevel.Serializable)]
    [TestCase(IsolationLevel.Snapshot)]
    public void BeginTransactionAllIsolationLevelsWorkTest(IsolationLevel level)
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        connection.Open();

        using var transaction = connection.BeginTransaction(level);

        Assert.That(transaction.IsolationLevel, Is.EqualTo(level));
    }

    [Test]
    public void BeginTransactionWhenConnectionClosedThrowsTest()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");

        Assert.Throws<InvalidOperationException>(() => connection.BeginTransaction());
    }

    #endregion

    #region Commit Tests

    [Test]
    public void CommitPersistsChangesTest()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE Test (Id INT)";
        cmd.ExecuteNonQuery();

        using var transaction = (WitDbTransaction)connection.BeginTransaction();
        cmd.Transaction = transaction;
        cmd.CommandText = "INSERT INTO Test VALUES (1)";
        cmd.ExecuteNonQuery();
        
        transaction.Commit();

        // Verify data is committed
        cmd.Transaction = null;
        cmd.CommandText = "SELECT COUNT(*) FROM Test";
        var count = cmd.ExecuteScalar();
        Assert.That(count, Is.EqualTo(1L));
    }

    [Test]
    public void CommitAllowsNewTransactionTest()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        connection.Open();

        using var transaction1 = (WitDbTransaction)connection.BeginTransaction();
        transaction1.Commit();

        using var transaction2 = connection.BeginTransaction();
        Assert.That(transaction2, Is.Not.Null);
    }

    #endregion

    #region Rollback Tests

    [Test]
    public void RollbackRevertsChangesTest()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE Test (Id INT)";
        cmd.ExecuteNonQuery();

        using var transaction = (WitDbTransaction)connection.BeginTransaction();
        cmd.Transaction = transaction;
        cmd.CommandText = "INSERT INTO Test VALUES (1)";
        cmd.ExecuteNonQuery();
        
        transaction.Rollback();

        // Verify data was not committed
        cmd.Transaction = null;
        cmd.CommandText = "SELECT COUNT(*) FROM Test";
        var count = cmd.ExecuteScalar();
        Assert.That(count, Is.EqualTo(0L));
    }

    [Test]
    public void RollbackAllowsNewTransactionTest()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        connection.Open();

        using var transaction1 = (WitDbTransaction)connection.BeginTransaction();
        transaction1.Rollback();

        using var transaction2 = connection.BeginTransaction();
        Assert.That(transaction2, Is.Not.Null);
    }

    #endregion

    #region MVCC Tests

    [Test]
    public void MvccEnabledSnapshotIsolationWorksTest()
    {
        using var connection = new WitDbConnection("Data Source=:memory:;MVCC=true;Isolation Level=Snapshot");
        connection.Open();

        using var transaction = connection.BeginTransaction(IsolationLevel.Snapshot);
        Assert.That(transaction.IsolationLevel, Is.EqualTo(IsolationLevel.Snapshot));
    }

    [Test]
    public void MvccDisabledTransactionsStillWorkTest()
    {
        using var connection = new WitDbConnection("Data Source=:memory:;MVCC=false");
        connection.Open();

        using var transaction = connection.BeginTransaction();
        Assert.That(transaction, Is.Not.Null);
    }

    #endregion

    #region Transactions Disabled Tests

    [Test]
    public void TransactionsDisabledConnectionStillOpensTest()
    {
        using var connection = new WitDbConnection("Data Source=:memory:;Transactions=false");
        connection.Open();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
    }

    #endregion

    #region Nested Transactions

    [Test]
    public void NestedTransactionThrowsOrSupportedTest()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        connection.Open();

        using var transaction1 = connection.BeginTransaction();

        // Depending on implementation, nested transactions may throw or be supported via savepoints
        try
        {
            using var transaction2 = connection.BeginTransaction();
            // If supported, both should be valid
            Assert.That(transaction2, Is.Not.Null);
        }
        catch (InvalidOperationException)
        {
            // Expected if nested transactions are not supported
            Assert.Pass("Nested transactions are not supported");
        }
    }

    #endregion
}

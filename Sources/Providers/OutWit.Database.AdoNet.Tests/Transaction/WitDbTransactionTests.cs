using NUnit.Framework;

namespace OutWit.Database.AdoNet.Tests.Transaction;

/// <summary>
/// Tests for WitDbTransaction.
/// </summary>
[TestFixture]
public class WitDbTransactionTests
{
    #region Fields

    private WitDbConnection m_connection = null!;

    #endregion

    #region Setup/TearDown

    [SetUp]
    public void Setup()
    {
        m_connection = new WitDbConnection("Data Source=:memory:");
        m_connection.Open();

        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE Test (Id INT PRIMARY KEY, Name VARCHAR(100))";
        cmd.ExecuteNonQuery();
    }

    [TearDown]
    public void TearDown()
    {
        m_connection?.Dispose();
    }

    #endregion

    #region Properties Tests

    [Test]
    public void ConnectionPropertyReturnsCorrectConnectionTest()
    {
        using var transaction = (WitDbTransaction)m_connection.BeginTransaction();

        Assert.That(transaction.Connection, Is.SameAs(m_connection));
    }

    [Test]
    public void IsolationLevelPropertyReturnsCorrectLevelTest()
    {
        using var transaction = (WitDbTransaction)m_connection.BeginTransaction(System.Data.IsolationLevel.Serializable);

        Assert.That(transaction.IsolationLevel, Is.EqualTo(System.Data.IsolationLevel.Serializable));
    }

    [Test]
    public void UnspecifiedIsolationLevelDefaultsToReadCommittedTest()
    {
        using var transaction = (WitDbTransaction)m_connection.BeginTransaction(System.Data.IsolationLevel.Unspecified);

        Assert.That(transaction.IsolationLevel, Is.EqualTo(System.Data.IsolationLevel.ReadCommitted));
    }

    #endregion

    #region Commit Tests

    [Test]
    public void CommitPersistsDataTest()
    {
        using var transaction = (WitDbTransaction)m_connection.BeginTransaction();

        using var cmd = m_connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "INSERT INTO Test VALUES (1, 'Committed')";
        cmd.ExecuteNonQuery();

        transaction.Commit();

        cmd.Transaction = null;
        cmd.CommandText = "SELECT Name FROM Test WHERE Id = 1";
        var result = cmd.ExecuteScalar();

        Assert.That(result, Is.EqualTo("Committed"));
    }

    [Test]
    public void CommitTwiceThrowsTest()
    {
        using var transaction = (WitDbTransaction)m_connection.BeginTransaction();
        transaction.Commit();

        Assert.Throws<InvalidOperationException>(() => transaction.Commit());
    }

    [Test]
    public async Task CommitAsyncWorksTest()
    {
        using var transaction = (WitDbTransaction)m_connection.BeginTransaction();

        using var cmd = m_connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "INSERT INTO Test VALUES (1, 'AsyncCommit')";
        cmd.ExecuteNonQuery();

        await transaction.CommitAsync();

        cmd.Transaction = null;
        cmd.CommandText = "SELECT COUNT(*) FROM Test";
        var count = cmd.ExecuteScalar();

        Assert.That(count, Is.EqualTo(1L));
    }

    #endregion

    #region Rollback Tests

    [Test]
    public void RollbackRevertsDataTest()
    {
        using var transaction = (WitDbTransaction)m_connection.BeginTransaction();

        using var cmd = m_connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "INSERT INTO Test VALUES (1, 'ToBeRolledBack')";
        cmd.ExecuteNonQuery();

        transaction.Rollback();

        cmd.Transaction = null;
        cmd.CommandText = "SELECT COUNT(*) FROM Test";
        var count = cmd.ExecuteScalar();

        Assert.That(count, Is.EqualTo(0L));
    }

    [Test]
    public void RollbackTwiceThrowsTest()
    {
        using var transaction = (WitDbTransaction)m_connection.BeginTransaction();
        transaction.Rollback();

        Assert.Throws<InvalidOperationException>(() => transaction.Rollback());
    }

    [Test]
    public async Task RollbackAsyncWorksTest()
    {
        using var transaction = (WitDbTransaction)m_connection.BeginTransaction();

        using var cmd = m_connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "INSERT INTO Test VALUES (1, 'AsyncRollback')";
        cmd.ExecuteNonQuery();

        await transaction.RollbackAsync();

        cmd.Transaction = null;
        cmd.CommandText = "SELECT COUNT(*) FROM Test";
        var count = cmd.ExecuteScalar();

        Assert.That(count, Is.EqualTo(0L));
    }

    [Test]
    public void RollbackAfterCommitThrowsTest()
    {
        using var transaction = (WitDbTransaction)m_connection.BeginTransaction();
        transaction.Commit();

        Assert.Throws<InvalidOperationException>(() => transaction.Rollback());
    }

    [Test]
    public void CommitAfterRollbackThrowsTest()
    {
        using var transaction = (WitDbTransaction)m_connection.BeginTransaction();
        transaction.Rollback();

        Assert.Throws<InvalidOperationException>(() => transaction.Commit());
    }

    #endregion

    #region Savepoint Tests

    [Test]
    public void SaveCreatesNamedSavepointTest()
    {
        using var transaction = (WitDbTransaction)m_connection.BeginTransaction();

        Assert.DoesNotThrow(() => transaction.Save("sp1"));
    }

    [Test]
    public void SaveWithEmptyNameThrowsTest()
    {
        using var transaction = (WitDbTransaction)m_connection.BeginTransaction();

        Assert.Throws<ArgumentException>(() => transaction.Save(""));
    }

    [Test]
    public void SaveWithNullNameThrowsTest()
    {
        using var transaction = (WitDbTransaction)m_connection.BeginTransaction();

        Assert.Throws<ArgumentException>(() => transaction.Save(null!));
    }

    [Test]
    public void RollbackToSavepointRevertsToPointTest()
    {
        using var transaction = (WitDbTransaction)m_connection.BeginTransaction();

        using var cmd = m_connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "INSERT INTO Test VALUES (1, 'Before')";
        cmd.ExecuteNonQuery();

        transaction.Save("sp1");

        cmd.CommandText = "INSERT INTO Test VALUES (2, 'After')";
        cmd.ExecuteNonQuery();

        transaction.Rollback("sp1");

        transaction.Commit();

        cmd.Transaction = null;
        cmd.CommandText = "SELECT COUNT(*) FROM Test";
        var count = cmd.ExecuteScalar();

        Assert.That(count, Is.EqualTo(1L));
    }

    [Test]
    public void ReleaseSavepointWorksTest()
    {
        using var transaction = (WitDbTransaction)m_connection.BeginTransaction();

        transaction.Save("sp1");

        Assert.DoesNotThrow(() => transaction.Release("sp1"));
    }

    [Test]
    public async Task SaveAsyncWorksTest()
    {
        using var transaction = (WitDbTransaction)m_connection.BeginTransaction();

        await transaction.SaveAsync("sp1");

        Assert.Pass();
    }

    [Test]
    public async Task RollbackToSavepointAsyncWorksTest()
    {
        using var transaction = (WitDbTransaction)m_connection.BeginTransaction();
        transaction.Save("sp1");

        await transaction.RollbackAsync("sp1");

        Assert.Pass();
    }

    [Test]
    public async Task ReleaseAsyncWorksTest()
    {
        using var transaction = (WitDbTransaction)m_connection.BeginTransaction();
        transaction.Save("sp1");

        await transaction.ReleaseAsync("sp1");

        Assert.Pass();
    }

    #endregion

    #region Dispose Tests

    [Test]
    public void DisposeRollsBackUncommittedTransactionTest()
    {
        using (var transaction = (WitDbTransaction)m_connection.BeginTransaction())
        {
            using var cmd = m_connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "INSERT INTO Test VALUES (1, 'Uncommitted')";
            cmd.ExecuteNonQuery();
            // No commit - dispose should rollback
        }

        using var checkCmd = m_connection.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(*) FROM Test";
        var count = checkCmd.ExecuteScalar();

        Assert.That(count, Is.EqualTo(0L));
    }

    [Test]
    public void DisposeAfterCommitDoesNotRollbackTest()
    {
        using (var transaction = (WitDbTransaction)m_connection.BeginTransaction())
        {
            using var cmd = m_connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "INSERT INTO Test VALUES (1, 'Committed')";
            cmd.ExecuteNonQuery();
            transaction.Commit();
        }

        using var checkCmd = m_connection.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(*) FROM Test";
        var count = checkCmd.ExecuteScalar();

        Assert.That(count, Is.EqualTo(1L));
    }

    [Test]
    public async Task DisposeAsyncRollsBackUncommittedTransactionTest()
    {
        await using (var transaction = (WitDbTransaction)m_connection.BeginTransaction())
        {
            using var cmd = m_connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "INSERT INTO Test VALUES (1, 'Uncommitted')";
            cmd.ExecuteNonQuery();
        }

        using var checkCmd = m_connection.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(*) FROM Test";
        var count = checkCmd.ExecuteScalar();

        Assert.That(count, Is.EqualTo(0L));
    }

    [Test]
    public void ConnectionPropertyIsNullAfterDisposeTest()
    {
        var transaction = (WitDbTransaction)m_connection.BeginTransaction();
        transaction.Dispose();

        Assert.That(transaction.Connection, Is.Null);
    }

    #endregion

    #region Connection State Tests

    [Test]
    public void OperationsOnClosedConnectionThrowTest()
    {
        var transaction = (WitDbTransaction)m_connection.BeginTransaction();
        m_connection.Close();

        Assert.Throws<InvalidOperationException>(() => transaction.Commit());
    }

    #endregion
}

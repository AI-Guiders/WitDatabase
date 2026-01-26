using NUnit.Framework;
using System.Data;

namespace OutWit.Database.AdoNet.Tests.Connection;

/// <summary>
/// Tests for WitDbConnection constructor and basic lifecycle (Open/Close/Dispose).
/// </summary>
[TestFixture]
public class WitDbConnectionLifecycleTests
{
    #region Fields

    private string? m_testDbPath;

    #endregion

    #region Setup/TearDown

    [SetUp]
    public void Setup()
    {
        m_testDbPath = Path.Combine(Path.GetTempPath(), $"WitDbConnection_{Guid.NewGuid():N}.witdb");
    }

    [TearDown]
    public void TearDown()
    {
        if (m_testDbPath != null && File.Exists(m_testDbPath))
        {
            try { File.Delete(m_testDbPath); } catch { }
        }
    }

    #endregion

    #region Constructor Tests

    [Test]
    public void DefaultConstructorCreatesClosedConnectionTest()
    {
        using var connection = new WitDbConnection();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
        Assert.That(connection.ConnectionString, Is.Empty);
    }

    [Test]
    public void ConstructorWithConnectionStringSetsPropertyTest()
    {
        var connectionString = "Data Source=:memory:";
        using var connection = new WitDbConnection(connectionString);

        Assert.That(connection.ConnectionString, Is.EqualTo(connectionString));
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
    }

    [Test]
    public void ConstructorWithEmptyConnectionStringCreatesClosedConnectionTest()
    {
        using var connection = new WitDbConnection("");

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
    }

    #endregion

    #region Open Tests

    [Test]
    public void OpenWithMemoryDatabaseSucceedsTest()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        
        connection.Open();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
    }

    [Test]
    public void OpenWithFileDatabaseCreatesFileTest()
    {
        using var connection = new WitDbConnection($"Data Source={m_testDbPath}");
        
        Assert.That(File.Exists(m_testDbPath), Is.False);
        
        connection.Open();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
        Assert.That(File.Exists(m_testDbPath), Is.True);
    }

    [Test]
    public void OpenWithoutConnectionStringThrowsTest()
    {
        using var connection = new WitDbConnection();

        Assert.Throws<InvalidOperationException>(() => connection.Open());
    }

    [Test]
    public void OpenCalledTwiceDoesNotThrowTest()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        connection.Open();

        Assert.DoesNotThrow(() => connection.Open());
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
    }

    [Test]
    public async Task OpenAsyncSucceedsTest()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        
        await connection.OpenAsync();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
    }

    [Test]
    public async Task OpenAsyncWithCancellationTokenSucceedsTest()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        using var cts = new CancellationTokenSource();
        
        await connection.OpenAsync(cts.Token);

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
    }

    #endregion

    #region Close Tests

    [Test]
    public void CloseChangesStateToClosedTest()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        connection.Open();
        
        connection.Close();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
    }

    [Test]
    public void CloseCalledTwiceDoesNotThrowTest()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        connection.Open();
        connection.Close();

        Assert.DoesNotThrow(() => connection.Close());
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
    }

    [Test]
    public void CloseWhenNotOpenDoesNotThrowTest()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");

        Assert.DoesNotThrow(() => connection.Close());
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
    }

    [Test]
    public async Task CloseAsyncSucceedsTest()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        await connection.OpenAsync();
        
        await connection.CloseAsync();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
    }

    #endregion

    #region Dispose Tests

    [Test]
    public void DisposeClosesConnectionTest()
    {
        var connection = new WitDbConnection("Data Source=:memory:");
        connection.Open();
        
        connection.Dispose();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
    }

    [Test]
    public void DisposeCalledTwiceDoesNotThrowTest()
    {
        var connection = new WitDbConnection("Data Source=:memory:");
        connection.Open();
        
        connection.Dispose();
        Assert.DoesNotThrow(() => connection.Dispose());
    }

    [Test]
    public void DisposeRollsBackActiveTransactionTest()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE Test (Id INT)";
        cmd.ExecuteNonQuery();

        var transaction = (WitDbTransaction)connection.BeginTransaction();
        cmd.Transaction = transaction;
        cmd.CommandText = "INSERT INTO Test VALUES (1)";
        cmd.ExecuteNonQuery();

        // Dispose connection without committing
        connection.Dispose();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
    }

    [Test]
    public async Task DisposeAsyncClosesConnectionTest()
    {
        var connection = new WitDbConnection("Data Source=:memory:");
        await connection.OpenAsync();
        
        await connection.DisposeAsync();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
    }

    #endregion

    #region Connection State Transitions

    [Test]
    public void OpenCloseOpenWorksCorrectlyTest()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        
        connection.Open();
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
        
        connection.Close();
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
        
        connection.Open();
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
    }

    [Test]
    public void ConnectionStringCanBeChangedWhenClosedTest()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        
        connection.ConnectionString = "Data Source=other.db";

        Assert.That(connection.ConnectionString, Does.Contain("other.db"));
    }

    [Test]
    public void ConnectionStringCannotBeChangedWhenOpenTest()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        connection.Open();

        Assert.Throws<InvalidOperationException>(() => 
            connection.ConnectionString = "Data Source=other.db");
    }

    #endregion
}

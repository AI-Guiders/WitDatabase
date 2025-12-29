using NUnit.Framework;
using System.Data;

namespace OutWit.Database.AdoNet.Tests.Command;

/// <summary>
/// Tests for WitDbCommand.
/// </summary>
[TestFixture]
public class WitDbCommandTests
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
        cmd.CommandText = "CREATE TABLE Test (Id INT PRIMARY KEY, Name VARCHAR(100), Value DECIMAL(10,2))";
        cmd.ExecuteNonQuery();
    }

    [TearDown]
    public void TearDown()
    {
        m_connection?.Dispose();
    }

    #endregion

    #region Constructor Tests

    [Test]
    public void DefaultConstructorCreatesCommandTest()
    {
        using var cmd = new WitDbCommand();

        Assert.That(cmd.CommandText, Is.Empty);
        Assert.That(cmd.Connection, Is.Null);
        Assert.That(cmd.CommandType, Is.EqualTo(CommandType.Text));
        Assert.That(cmd.CommandTimeout, Is.EqualTo(30));
    }

    [Test]
    public void ConstructorWithCommandTextSetsPropertyTest()
    {
        using var cmd = new WitDbCommand("SELECT 1");

        Assert.That(cmd.CommandText, Is.EqualTo("SELECT 1"));
    }

    [Test]
    public void ConstructorWithConnectionSetsPropertyTest()
    {
        using var cmd = new WitDbCommand("SELECT 1", m_connection);

        Assert.That(cmd.CommandText, Is.EqualTo("SELECT 1"));
        Assert.That(cmd.Connection, Is.SameAs(m_connection));
    }

    [Test]
    public void ConstructorWithTransactionSetsPropertyTest()
    {
        using var transaction = (WitDbTransaction)m_connection.BeginTransaction();
        using var cmd = new WitDbCommand("SELECT 1", m_connection, transaction);

        Assert.That(cmd.Transaction, Is.SameAs(transaction));
    }

    #endregion

    #region ExecuteNonQuery Tests

    [Test]
    public void ExecuteNonQueryInsertReturnsRowsAffectedTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "INSERT INTO Test (Id, Name) VALUES (1, 'Test')";

        var result = cmd.ExecuteNonQuery();

        Assert.That(result, Is.EqualTo(1));
    }

    [Test]
    public void ExecuteNonQueryUpdateReturnsRowsAffectedTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "INSERT INTO Test (Id, Name) VALUES (1, 'Test')";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "UPDATE Test SET Name = 'Updated' WHERE Id = 1";
        var result = cmd.ExecuteNonQuery();

        Assert.That(result, Is.EqualTo(1));
    }

    [Test]
    public void ExecuteNonQueryDeleteReturnsRowsAffectedTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "INSERT INTO Test (Id, Name) VALUES (1, 'Test')";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "DELETE FROM Test WHERE Id = 1";
        var result = cmd.ExecuteNonQuery();

        Assert.That(result, Is.EqualTo(1));
    }

    [Test]
    public void ExecuteNonQueryWithoutConnectionThrowsTest()
    {
        using var cmd = new WitDbCommand("SELECT 1");

        Assert.Throws<InvalidOperationException>(() => cmd.ExecuteNonQuery());
    }

    [Test]
    public void ExecuteNonQueryWithClosedConnectionThrowsTest()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT 1";

        Assert.Throws<InvalidOperationException>(() => cmd.ExecuteNonQuery());
    }

    [Test]
    public void ExecuteNonQueryWithEmptyCommandTextThrowsTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "";

        Assert.Throws<InvalidOperationException>(() => cmd.ExecuteNonQuery());
    }

    [Test]
    public async Task ExecuteNonQueryAsyncReturnsRowsAffectedTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "INSERT INTO Test (Id, Name) VALUES (1, 'Test')";

        var result = await cmd.ExecuteNonQueryAsync();

        Assert.That(result, Is.EqualTo(1));
    }

    #endregion

    #region ExecuteScalar Tests

    [Test]
    public void ExecuteScalarReturnsFirstColumnFirstRowTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "INSERT INTO Test (Id, Name) VALUES (1, 'Test')";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT Name FROM Test WHERE Id = 1";
        var result = cmd.ExecuteScalar();

        Assert.That(result, Is.EqualTo("Test"));
    }

    [Test]
    public void ExecuteScalarReturnsNullForEmptyResultTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT Name FROM Test WHERE Id = 999";

        var result = cmd.ExecuteScalar();

        Assert.That(result, Is.Null);
    }

    [Test]
    public void ExecuteScalarReturnsIntegerValueTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "INSERT INTO Test (Id, Name) VALUES (42, 'Test')";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT Id FROM Test WHERE Name = 'Test'";
        var result = cmd.ExecuteScalar();

        Assert.That(result, Is.EqualTo(42L));
    }

    [Test]
    public void ExecuteScalarReturnsDecimalValueTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "INSERT INTO Test (Id, Name, Value) VALUES (1, 'Test', 123.45)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT Value FROM Test WHERE Id = 1";
        var result = cmd.ExecuteScalar();

        Assert.That(result, Is.EqualTo(123.45m));
    }

    [Test]
    public async Task ExecuteScalarAsyncReturnsValueTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT 42";

        var result = await cmd.ExecuteScalarAsync();

        Assert.That(result, Is.EqualTo(42L));
    }

    #endregion

    #region ExecuteReader Tests

    [Test]
    public void ExecuteReaderReturnsDataReaderTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT 1 AS Value";

        using var reader = cmd.ExecuteReader();

        Assert.That(reader, Is.Not.Null);
        Assert.That(reader, Is.InstanceOf<WitDbDataReader>());
    }

    [Test]
    public void ExecuteReaderCanReadDataTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "INSERT INTO Test (Id, Name) VALUES (1, 'Test')";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT Id, Name FROM Test";
        using var reader = cmd.ExecuteReader();

        Assert.That(reader.Read(), Is.True);
        Assert.That(reader.GetInt64(0), Is.EqualTo(1));
        Assert.That(reader.GetString(1), Is.EqualTo("Test"));
    }

    [Test]
    public async Task ExecuteReaderAsyncReturnsDataReaderTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT 1 AS Value";

        using var reader = await cmd.ExecuteReaderAsync();

        Assert.That(reader, Is.Not.Null);
    }

    [Test]
    public void ExecuteReaderWithCloseConnectionBehaviorClosesConnectionTest()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT 1";
        
        using var reader = cmd.ExecuteReader(CommandBehavior.CloseConnection);
        reader.Close();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
    }

    #endregion

    #region Parameters Tests

    [Test]
    public void CreateParameterReturnsWitDbParameterTest()
    {
        using var cmd = m_connection.CreateCommand();

        var param = cmd.CreateParameter();

        Assert.That(param, Is.InstanceOf<WitDbParameter>());
    }

    [Test]
    public void ParametersCollectionIsNotNullTest()
    {
        using var cmd = m_connection.CreateCommand();

        Assert.That(cmd.Parameters, Is.Not.Null);
        Assert.That(cmd.Parameters, Is.InstanceOf<WitDbParameterCollection>());
    }

    [Test]
    public void ParameterizedQueryWorksCorrectlyTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "INSERT INTO Test (Id, Name) VALUES (@id, @name)";
        cmd.Parameters.AddWithValue("@id", 1);
        cmd.Parameters.AddWithValue("@name", "Parametrized");

        var result = cmd.ExecuteNonQuery();

        Assert.That(result, Is.EqualTo(1));

        cmd.Parameters.Clear();
        cmd.CommandText = "SELECT Name FROM Test WHERE Id = 1";
        var name = cmd.ExecuteScalar();
        Assert.That(name, Is.EqualTo("Parametrized"));
    }

    [Test]
    public void ParameterWithoutPrefixWorksTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "INSERT INTO Test (Id, Name) VALUES (@id, @name)";
        cmd.Parameters.AddWithValue("id", 2);
        cmd.Parameters.AddWithValue("name", "NoPrefixParam");

        var result = cmd.ExecuteNonQuery();

        Assert.That(result, Is.EqualTo(1));
    }

    #endregion

    #region CommandTimeout Tests

    [Test]
    public void CommandTimeoutDefaultIs30SecondsTest()
    {
        using var cmd = m_connection.CreateCommand();

        Assert.That(cmd.CommandTimeout, Is.EqualTo(30));
    }

    [Test]
    public void CommandTimeoutCanBeSetTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandTimeout = 60;

        Assert.That(cmd.CommandTimeout, Is.EqualTo(60));
    }

    [Test]
    public void CommandTimeoutNegativeThrowsTest()
    {
        using var cmd = m_connection.CreateCommand();

        Assert.Throws<ArgumentOutOfRangeException>(() => cmd.CommandTimeout = -1);
    }

    #endregion

    #region CommandType Tests

    [Test]
    public void CommandTypeDefaultIsTextTest()
    {
        using var cmd = m_connection.CreateCommand();

        Assert.That(cmd.CommandType, Is.EqualTo(CommandType.Text));
    }

    [Test]
    public void CommandTypeStoredProcedureNotSupportedTest()
    {
        using var cmd = m_connection.CreateCommand();

        Assert.Throws<NotSupportedException>(() => cmd.CommandType = CommandType.StoredProcedure);
    }

    #endregion

    #region Prepare Tests

    [Test]
    public void PrepareDoesNotThrowTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Test WHERE Id = @id";

        Assert.DoesNotThrow(() => cmd.Prepare());
    }

    [Test]
    public void PrepareWithEmptyCommandTextThrowsTest()
    {
        using var cmd = m_connection.CreateCommand();

        Assert.Throws<InvalidOperationException>(() => cmd.Prepare());
    }

    [Test]
    public async Task PrepareAsyncDoesNotThrowTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Test";

        await cmd.PrepareAsync();

        Assert.Pass();
    }

    #endregion

    #region Cancel Tests

    [Test]
    public void CancelDoesNotThrowTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT 1";

        Assert.DoesNotThrow(() => cmd.Cancel());
    }

    #endregion

    #region Dispose Tests

    [Test]
    public void DisposeDoesNotThrowTest()
    {
        var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT 1";
        cmd.Parameters.AddWithValue("@p", 1);

        Assert.DoesNotThrow(() => cmd.Dispose());
    }

    [Test]
    public void DisposeClearsParametersTest()
    {
        var cmd = m_connection.CreateCommand();
        cmd.Parameters.AddWithValue("@p", 1);
        
        cmd.Dispose();

        Assert.That(cmd.Parameters.Count, Is.EqualTo(0));
    }

    #endregion
}

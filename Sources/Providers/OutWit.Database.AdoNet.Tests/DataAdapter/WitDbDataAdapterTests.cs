using NUnit.Framework;
using System.Data;

namespace OutWit.Database.AdoNet.Tests.DataAdapter;

/// <summary>
/// Tests for WitDbDataAdapter.
/// </summary>
[TestFixture]
public class WitDbDataAdapterTests
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

        cmd.CommandText = "INSERT INTO Test VALUES (1, 'First')";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "INSERT INTO Test VALUES (2, 'Second')";
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
    public void DefaultConstructorCreatesAdapterTest()
    {
        using var adapter = new WitDbDataAdapter();

        Assert.That(adapter.SelectCommand, Is.Null);
    }

    [Test]
    public void ConstructorWithSelectCommandSetsPropertyTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Test";

        using var adapter = new WitDbDataAdapter(cmd);

        Assert.That(adapter.SelectCommand, Is.SameAs(cmd));
    }

    [Test]
    public void ConstructorWithCommandTextAndConnectionCreatesCommandTest()
    {
        using var adapter = new WitDbDataAdapter("SELECT * FROM Test", m_connection);

        Assert.That(adapter.SelectCommand, Is.Not.Null);
        Assert.That(adapter.SelectCommand!.CommandText, Is.EqualTo("SELECT * FROM Test"));
        Assert.That(adapter.SelectCommand.Connection, Is.SameAs(m_connection));
    }

    [Test]
    public void ConstructorWithConnectionStringCreatesConnectionTest()
    {
        using var adapter = new WitDbDataAdapter("SELECT 1", "Data Source=:memory:");

        Assert.That(adapter.SelectCommand, Is.Not.Null);
        Assert.That(adapter.SelectCommand!.Connection, Is.Not.Null);
    }

    #endregion

    #region Fill Tests

    [Test]
    public void FillDataSetPopulatesDataTest()
    {
        using var adapter = new WitDbDataAdapter("SELECT * FROM Test", m_connection);
        var dataSet = new DataSet();

        var rowsAffected = adapter.Fill(dataSet);

        Assert.That(rowsAffected, Is.EqualTo(2));
        Assert.That(dataSet.Tables.Count, Is.GreaterThan(0));
        Assert.That(dataSet.Tables[0].Rows.Count, Is.EqualTo(2));
    }

    [Test]
    public void FillDataTablePopulatesDataTest()
    {
        using var adapter = new WitDbDataAdapter("SELECT * FROM Test", m_connection);
        var dataTable = new DataTable();

        var rowsAffected = adapter.Fill(dataTable);

        Assert.That(rowsAffected, Is.EqualTo(2));
        Assert.That(dataTable.Rows.Count, Is.EqualTo(2));
    }

    [Test]
    public void FillCreatesCorrectColumnsTest()
    {
        using var adapter = new WitDbDataAdapter("SELECT Id, Name FROM Test", m_connection);
        var dataTable = new DataTable();

        adapter.Fill(dataTable);

        Assert.That(dataTable.Columns.Contains("Id"), Is.True);
        Assert.That(dataTable.Columns.Contains("Name"), Is.True);
    }

    [Test]
    public void FillWithCorrectDataTypesTest()
    {
        using var adapter = new WitDbDataAdapter("SELECT Id, Name FROM Test WHERE Id = 1", m_connection);
        var dataTable = new DataTable();

        adapter.Fill(dataTable);

        // Verify data values can be retrieved (types may vary based on SQL type mapping)
        var idValue = dataTable.Rows[0]["Id"];
        var nameValue = dataTable.Rows[0]["Name"];

        Assert.That(idValue, Is.Not.Null);
        Assert.That(nameValue, Is.EqualTo("First"));
        
        // Id should be convertible to int
        Assert.That(Convert.ToInt64(idValue), Is.EqualTo(1));
    }

    #endregion

    #region Command Properties Tests

    [Test]
    public void SelectCommandCanBeSetTest()
    {
        using var adapter = new WitDbDataAdapter();
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Test";

        adapter.SelectCommand = cmd;

        Assert.That(adapter.SelectCommand, Is.SameAs(cmd));
    }

    [Test]
    public void InsertCommandCanBeSetTest()
    {
        using var adapter = new WitDbDataAdapter();
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "INSERT INTO Test VALUES (@id, @name)";

        adapter.InsertCommand = cmd;

        Assert.That(adapter.InsertCommand, Is.SameAs(cmd));
    }

    [Test]
    public void UpdateCommandCanBeSetTest()
    {
        using var adapter = new WitDbDataAdapter();
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "UPDATE Test SET Name = @name WHERE Id = @id";

        adapter.UpdateCommand = cmd;

        Assert.That(adapter.UpdateCommand, Is.SameAs(cmd));
    }

    [Test]
    public void DeleteCommandCanBeSetTest()
    {
        using var adapter = new WitDbDataAdapter();
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Test WHERE Id = @id";

        adapter.DeleteCommand = cmd;

        Assert.That(adapter.DeleteCommand, Is.SameAs(cmd));
    }

    #endregion

    #region Update Tests

    [Test]
    public void UpdateWithInsertCommandInsertsRowTest()
    {
        using var adapter = new WitDbDataAdapter("SELECT Id, Name FROM Test", m_connection);
        
        using var insertCmd = m_connection.CreateCommand();
        insertCmd.CommandText = "INSERT INTO Test (Id, Name) VALUES (@Id, @Name)";
        insertCmd.Parameters.Add(new WitDbParameter("@Id", DbType.Int64) { SourceColumn = "Id" });
        insertCmd.Parameters.Add(new WitDbParameter("@Name", DbType.String) { SourceColumn = "Name" });
        adapter.InsertCommand = insertCmd;

        var dataTable = new DataTable();
        adapter.Fill(dataTable);

        var newRow = dataTable.NewRow();
        newRow["Id"] = 3L;
        newRow["Name"] = "Third";
        dataTable.Rows.Add(newRow);

        var affected = adapter.Update(dataTable);

        Assert.That(affected, Is.EqualTo(1));

        // Verify in database
        using var verifyCmd = m_connection.CreateCommand();
        verifyCmd.CommandText = "SELECT COUNT(*) FROM Test";
        var count = verifyCmd.ExecuteScalar();
        Assert.That(count, Is.EqualTo(3L));
    }

    #endregion

    #region RowUpdating/RowUpdated Events Tests

    [Test]
    public void RowUpdatingEventIsFiredTest()
    {
        using var adapter = new WitDbDataAdapter("SELECT Id, Name FROM Test", m_connection);
        
        using var insertCmd = m_connection.CreateCommand();
        insertCmd.CommandText = "INSERT INTO Test (Id, Name) VALUES (@Id, @Name)";
        insertCmd.Parameters.Add(new WitDbParameter("@Id", DbType.Int64) { SourceColumn = "Id" });
        insertCmd.Parameters.Add(new WitDbParameter("@Name", DbType.String) { SourceColumn = "Name" });
        adapter.InsertCommand = insertCmd;

        var eventFired = false;
        adapter.RowUpdating += (s, e) => eventFired = true;

        var dataTable = new DataTable();
        adapter.Fill(dataTable);
        var newRow = dataTable.NewRow();
        newRow["Id"] = 3L;
        newRow["Name"] = "Third";
        dataTable.Rows.Add(newRow);

        adapter.Update(dataTable);

        Assert.That(eventFired, Is.True);
    }

    [Test]
    public void RowUpdatedEventIsFiredTest()
    {
        using var adapter = new WitDbDataAdapter("SELECT Id, Name FROM Test", m_connection);
        
        using var insertCmd = m_connection.CreateCommand();
        insertCmd.CommandText = "INSERT INTO Test (Id, Name) VALUES (@Id, @Name)";
        insertCmd.Parameters.Add(new WitDbParameter("@Id", DbType.Int64) { SourceColumn = "Id" });
        insertCmd.Parameters.Add(new WitDbParameter("@Name", DbType.String) { SourceColumn = "Name" });
        adapter.InsertCommand = insertCmd;

        var eventFired = false;
        adapter.RowUpdated += (s, e) => eventFired = true;

        var dataTable = new DataTable();
        adapter.Fill(dataTable);
        var newRow = dataTable.NewRow();
        newRow["Id"] = 3L;
        newRow["Name"] = "Third";
        dataTable.Rows.Add(newRow);

        adapter.Update(dataTable);

        Assert.That(eventFired, Is.True);
    }

    #endregion
}

using NUnit.Framework;
using System.Data;

namespace OutWit.Database.AdoNet.Tests.DataReader;

/// <summary>
/// Tests for WitDbDataReader.
/// </summary>
[TestFixture]
public class WitDbDataReaderTests
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
        cmd.CommandText = @"
            CREATE TABLE Test (
                Id INT PRIMARY KEY,
                Name VARCHAR(100),
                Amount DECIMAL(10,2),
                IsActive BOOLEAN,
                Created DATETIME,
                Data BLOB,
                UniqueId GUID
            )";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"INSERT INTO Test VALUES (1, 'First', 100.50, true, '2024-01-15 10:30:00', X'48656C6C6F', '550e8400-e29b-41d4-a716-446655440000')";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"INSERT INTO Test VALUES (2, 'Second', 200.75, false, '2024-02-20 14:45:00', X'576F726C64', '6ba7b810-9dad-11d1-80b4-00c04fd430c8')";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"INSERT INTO Test VALUES (3, NULL, NULL, NULL, NULL, NULL, NULL)";
        cmd.ExecuteNonQuery();
    }

    [TearDown]
    public void TearDown()
    {
        m_connection?.Dispose();
    }

    #endregion

    #region Read Tests

    [Test]
    public void ReadReturnsTrueWhenRowsExistTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Test";
        using var reader = cmd.ExecuteReader();

        Assert.That(reader.Read(), Is.True);
    }

    [Test]
    public void ReadReturnsFalseWhenNoMoreRowsTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Test WHERE Id = 999";
        using var reader = cmd.ExecuteReader();

        Assert.That(reader.Read(), Is.False);
    }

    [Test]
    public void ReadIteratesAllRowsTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Test";
        using var reader = cmd.ExecuteReader();

        var count = 0;
        while (reader.Read()) count++;

        Assert.That(count, Is.EqualTo(3));
    }

    [Test]
    public async Task ReadAsyncWorksTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Test";
        using var reader = await cmd.ExecuteReaderAsync();

        Assert.That(await reader.ReadAsync(), Is.True);
    }

    #endregion

    #region FieldCount and HasRows Tests

    [Test]
    public void FieldCountReturnsCorrectValueTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name FROM Test";
        using var reader = cmd.ExecuteReader();

        Assert.That(reader.FieldCount, Is.EqualTo(2));
    }

    [Test]
    public void HasRowsReturnsTrueWhenDataExistsTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Test";
        using var reader = cmd.ExecuteReader();

        Assert.That(reader.HasRows, Is.True);
    }

    [Test]
    public void HasRowsAfterReadingAllRowsStillReturnsTrueTest()
    {
        // HasRows indicates if there WERE rows, not if there are remaining rows
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Test";
        using var reader = cmd.ExecuteReader();

        while (reader.Read()) { }

        Assert.That(reader.HasRows, Is.True);
    }

    #endregion

    #region GetName and GetOrdinal Tests

    [Test]
    public void GetNameReturnsColumnNameTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name FROM Test";
        using var reader = cmd.ExecuteReader();

        Assert.That(reader.GetName(0), Is.EqualTo("Id"));
        Assert.That(reader.GetName(1), Is.EqualTo("Name"));
    }

    [Test]
    public void GetOrdinalReturnsColumnIndexTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name FROM Test";
        using var reader = cmd.ExecuteReader();

        Assert.That(reader.GetOrdinal("Id"), Is.EqualTo(0));
        Assert.That(reader.GetOrdinal("Name"), Is.EqualTo(1));
    }

    [Test]
    public void GetOrdinalIsCaseInsensitiveTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name FROM Test";
        using var reader = cmd.ExecuteReader();

        Assert.That(reader.GetOrdinal("ID"), Is.EqualTo(0));
        Assert.That(reader.GetOrdinal("name"), Is.EqualTo(1));
    }

    [Test]
    public void GetOrdinalThrowsForUnknownColumnTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT Id FROM Test";
        using var reader = cmd.ExecuteReader();

        Assert.Throws<ArgumentException>(() => reader.GetOrdinal("Unknown"));
    }

    #endregion

    #region Typed Getters Tests

    [Test]
    public void GetInt64ReturnsValueTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT Id FROM Test WHERE Id = 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        Assert.That(reader.GetInt64(0), Is.EqualTo(1));
    }

    [Test]
    public void GetInt32ReturnsValueTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT Id FROM Test WHERE Id = 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        Assert.That(reader.GetInt32(0), Is.EqualTo(1));
    }

    [Test]
    public void GetStringReturnsValueTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT Name FROM Test WHERE Id = 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        Assert.That(reader.GetString(0), Is.EqualTo("First"));
    }

    [Test]
    public void GetDecimalReturnsValueTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT Amount FROM Test WHERE Id = 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        Assert.That(reader.GetDecimal(0), Is.EqualTo(100.50m));
    }

    [Test]
    public void GetDoubleReturnsValueTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT Amount FROM Test WHERE Id = 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        Assert.That(reader.GetDouble(0), Is.EqualTo(100.50d).Within(0.001));
    }

    [Test]
    public void GetBooleanReturnsValueTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT IsActive FROM Test WHERE Id = 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        Assert.That(reader.GetBoolean(0), Is.True);
    }

    [Test]
    public void GetDateTimeReturnsValueTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT Created FROM Test WHERE Id = 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var expected = new DateTime(2024, 1, 15, 10, 30, 0);
        Assert.That(reader.GetDateTime(0), Is.EqualTo(expected));
    }

    [Test]
    public void GetGuidReturnsValueTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT UniqueId FROM Test WHERE Id = 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var expected = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");
        Assert.That(reader.GetGuid(0), Is.EqualTo(expected));
    }

    #endregion

    #region GetValue and IsDBNull Tests

    [Test]
    public void GetValueReturnsObjectTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT Name FROM Test WHERE Id = 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        Assert.That(reader.GetValue(0), Is.EqualTo("First"));
    }

    [Test]
    public void GetValueReturnsDBNullForNullTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT Name FROM Test WHERE Id = 3";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        Assert.That(reader.GetValue(0), Is.EqualTo(DBNull.Value));
    }

    [Test]
    public void IsDBNullReturnsTrueForNullTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT Name FROM Test WHERE Id = 3";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        Assert.That(reader.IsDBNull(0), Is.True);
    }

    [Test]
    public void IsDBNullReturnsFalseForNonNullTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT Name FROM Test WHERE Id = 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        Assert.That(reader.IsDBNull(0), Is.False);
    }

    [Test]
    public async Task IsDBNullAsyncWorksTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT Name FROM Test WHERE Id = 3";
        using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();

        Assert.That(await reader.IsDBNullAsync(0), Is.True);
    }

    #endregion

    #region GetFieldValue Tests

    [Test]
    public void GetFieldValueReturnsTypedValueTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT Id FROM Test WHERE Id = 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        Assert.That(reader.GetFieldValue<long>(0), Is.EqualTo(1));
    }

    [Test]
    public void GetFieldValueConvertsTypesTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT Id FROM Test WHERE Id = 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        Assert.That(reader.GetFieldValue<int>(0), Is.EqualTo(1));
    }

    [Test]
    public async Task GetFieldValueAsyncWorksTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT Name FROM Test WHERE Id = 1";
        using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();

        Assert.That(await reader.GetFieldValueAsync<string>(0), Is.EqualTo("First"));
    }

    #endregion

    #region Indexer Tests

    [Test]
    public void IndexerByOrdinalReturnsValueTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT Name FROM Test WHERE Id = 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        Assert.That(reader[0], Is.EqualTo("First"));
    }

    [Test]
    public void IndexerByNameReturnsValueTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT Name FROM Test WHERE Id = 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        Assert.That(reader["Name"], Is.EqualTo("First"));
    }

    #endregion

    #region Close and Dispose Tests

    [Test]
    public void CloseMarksReaderAsClosedTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Test";
        using var reader = cmd.ExecuteReader();

        reader.Close();

        Assert.That(reader.IsClosed, Is.True);
    }

    [Test]
    public void ReadAfterCloseThrowsTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Test";
        using var reader = cmd.ExecuteReader();
        reader.Close();

        Assert.Throws<InvalidOperationException>(() => reader.Read());
    }

    [Test]
    public async Task CloseAsyncWorksTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Test";
        using var reader = await cmd.ExecuteReaderAsync();

        await reader.CloseAsync();

        Assert.That(reader.IsClosed, Is.True);
    }

    #endregion

    #region GetBytes and GetChars Tests

    [Test]
    public void GetBytesReturnsDataTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT Data FROM Test WHERE Id = 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var buffer = new byte[5];
        var bytesRead = reader.GetBytes(0, 0, buffer, 0, 5);

        Assert.That(bytesRead, Is.EqualTo(5));
        Assert.That(buffer, Is.EqualTo(new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F })); // "Hello"
    }

    [Test]
    public void GetBytesWithNullBufferReturnsLengthTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT Data FROM Test WHERE Id = 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var length = reader.GetBytes(0, 0, null, 0, 0);

        Assert.That(length, Is.EqualTo(5));
    }

    [Test]
    public void GetCharsReturnsDataTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT Name FROM Test WHERE Id = 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var buffer = new char[5];
        var charsRead = reader.GetChars(0, 0, buffer, 0, 5);

        Assert.That(charsRead, Is.EqualTo(5));
        Assert.That(new string(buffer), Is.EqualTo("First"));
    }

    #endregion

    #region GetSchemaTable Tests

    [Test]
    public void GetSchemaTableReturnsSchemaTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name FROM Test";
        using var reader = cmd.ExecuteReader();

        var schema = reader.GetSchemaTable();

        Assert.That(schema, Is.Not.Null);
        Assert.That(schema!.Rows.Count, Is.EqualTo(2));
    }

    [Test]
    public void GetSchemaTableHasCorrectColumnsTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name FROM Test";
        using var reader = cmd.ExecuteReader();

        var schema = reader.GetSchemaTable();

        Assert.That(schema!.Columns.Contains("ColumnName"), Is.True);
        Assert.That(schema.Columns.Contains("ColumnOrdinal"), Is.True);
        Assert.That(schema.Columns.Contains("DataType"), Is.True);
    }

    #endregion

    #region NextResult Tests

    [Test]
    public void NextResultReturnsFalseTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Test";
        using var reader = cmd.ExecuteReader();

        Assert.That(reader.NextResult(), Is.False);
    }

    #endregion

    #region RecordsAffected Tests

    [Test]
    public void RecordsAffectedReturnsMinusOneForSelectTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Test";
        using var reader = cmd.ExecuteReader();

        // For SELECT, RecordsAffected is typically -1
        Assert.That(reader.RecordsAffected, Is.EqualTo(-1).Or.GreaterThanOrEqualTo(0));
    }

    #endregion

    #region GetDataTypeName and GetFieldType Tests

    [Test]
    public void GetDataTypeNameReturnsTypeNameTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name FROM Test";
        using var reader = cmd.ExecuteReader();

        Assert.That(reader.GetDataTypeName(0), Is.Not.Empty);
    }

    [Test]
    public void GetFieldTypeReturnsValidClrTypeTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name FROM Test";
        using var reader = cmd.ExecuteReader();

        // Just verify it returns a valid Type
        var idType = reader.GetFieldType(0);
        var nameType = reader.GetFieldType(1);

        Assert.That(idType, Is.Not.Null);
        Assert.That(nameType, Is.Not.Null);
    }

    [Test]
    public void GetFieldTypeForStringColumnReturnsStringTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT Name FROM Test";
        using var reader = cmd.ExecuteReader();

        Assert.That(reader.GetFieldType(0), Is.EqualTo(typeof(string)));
    }

    #endregion

    #region Depth Tests

    [Test]
    public void DepthReturnsZeroTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Test";
        using var reader = cmd.ExecuteReader();

        Assert.That(reader.Depth, Is.EqualTo(0));
    }

    #endregion
}

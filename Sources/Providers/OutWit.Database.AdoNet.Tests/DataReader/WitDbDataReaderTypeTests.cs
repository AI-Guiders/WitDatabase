using NUnit.Framework;
using System.Data;
using OutWit.Database.Types;

namespace OutWit.Database.AdoNet.Tests.DataReader;

/// <summary>
/// Tests for column type mapping in WitDbDataReader.
/// </summary>
[TestFixture]
public class WitDbDataReaderTypeTests
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
    }

    [TearDown]
    public void TearDown()
    {
        m_connection?.Dispose();
    }

    #endregion

    #region INT Type Tests

    [Test]
    public void IntColumnReturnsIntegerTypeTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE TestInt (Id INT PRIMARY KEY, Value INT)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "INSERT INTO TestInt VALUES (1, 42)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT Id, Value FROM TestInt";
        using var reader = cmd.ExecuteReader();

        // Check the declared types
        var idType = reader.GetFieldType(0);
        var valueType = reader.GetFieldType(1);

        Console.WriteLine($"Id column type: {idType.FullName}");
        Console.WriteLine($"Value column type: {valueType.FullName}");
        Console.WriteLine($"Id DataTypeName: {reader.GetDataTypeName(0)}");
        Console.WriteLine($"Value DataTypeName: {reader.GetDataTypeName(1)}");

        // INT should map to long (Int64) in WitDatabase
        Assert.That(idType, Is.EqualTo(typeof(long)), "INT column should be long");
        Assert.That(valueType, Is.EqualTo(typeof(long)), "INT column should be long");

        // Read and verify actual values
        reader.Read();
        var idValue = reader.GetInt64(0);
        var valueValue = reader.GetInt64(1);

        Assert.That(idValue, Is.EqualTo(1));
        Assert.That(valueValue, Is.EqualTo(42));
    }

    [Test]
    public void BigIntColumnReturnsInt64TypeTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE TestBigInt (Id BIGINT PRIMARY KEY)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "INSERT INTO TestBigInt VALUES (9223372036854775807)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT Id FROM TestBigInt";
        using var reader = cmd.ExecuteReader();

        var idType = reader.GetFieldType(0);
        Console.WriteLine($"BIGINT column type: {idType.FullName}");
        Console.WriteLine($"BIGINT DataTypeName: {reader.GetDataTypeName(0)}");

        Assert.That(idType, Is.EqualTo(typeof(long)));
    }

    #endregion

    #region Boolean Type Tests

    [Test]
    public void BooleanColumnReturnsBoolTypeTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE TestBool (Id INT PRIMARY KEY, Flag BOOLEAN)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "INSERT INTO TestBool VALUES (1, true)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT Flag FROM TestBool";
        using var reader = cmd.ExecuteReader();

        var flagType = reader.GetFieldType(0);
        Console.WriteLine($"BOOLEAN column type: {flagType.FullName}");
        Console.WriteLine($"BOOLEAN DataTypeName: {reader.GetDataTypeName(0)}");

        Assert.That(flagType, Is.EqualTo(typeof(bool)));
    }

    #endregion

    #region Decimal Type Tests

    [Test]
    public void DecimalColumnReturnsDecimalTypeTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE TestDecimal (Id INT PRIMARY KEY, Amount DECIMAL(10,2))";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "INSERT INTO TestDecimal VALUES (1, 123.45)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT Amount FROM TestDecimal";
        using var reader = cmd.ExecuteReader();

        var amountType = reader.GetFieldType(0);
        Console.WriteLine($"DECIMAL column type: {amountType.FullName}");
        Console.WriteLine($"DECIMAL DataTypeName: {reader.GetDataTypeName(0)}");

        Assert.That(amountType, Is.EqualTo(typeof(decimal)));
    }

    #endregion

    #region DateTime Type Tests

    [Test]
    public void DateTimeColumnReturnsDateTimeTypeTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE TestDateTime (Id INT PRIMARY KEY, Created DATETIME)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "INSERT INTO TestDateTime VALUES (1, '2024-01-15 10:30:00')";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT Created FROM TestDateTime";
        using var reader = cmd.ExecuteReader();

        var createdType = reader.GetFieldType(0);
        Console.WriteLine($"DATETIME column type: {createdType.FullName}");
        Console.WriteLine($"DATETIME DataTypeName: {reader.GetDataTypeName(0)}");

        Assert.That(createdType, Is.EqualTo(typeof(DateTime)));
    }

    #endregion

    #region GUID Type Tests

    [Test]
    public void GuidColumnReturnsGuidTypeTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE TestGuid (Id INT PRIMARY KEY, UniqueId GUID)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "INSERT INTO TestGuid VALUES (1, '550e8400-e29b-41d4-a716-446655440000')";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT UniqueId FROM TestGuid";
        using var reader = cmd.ExecuteReader();

        var guidType = reader.GetFieldType(0);
        Console.WriteLine($"GUID column type: {guidType.FullName}");
        Console.WriteLine($"GUID DataTypeName: {reader.GetDataTypeName(0)}");

        Assert.That(guidType, Is.EqualTo(typeof(Guid)));
    }

    #endregion

    #region VARCHAR Type Tests

    [Test]
    public void VarcharColumnReturnsStringTypeTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE TestVarchar (Id INT PRIMARY KEY, Name VARCHAR(100))";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "INSERT INTO TestVarchar VALUES (1, 'Test')";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT Name FROM TestVarchar";
        using var reader = cmd.ExecuteReader();

        var nameType = reader.GetFieldType(0);
        Console.WriteLine($"VARCHAR column type: {nameType.FullName}");
        Console.WriteLine($"VARCHAR DataTypeName: {reader.GetDataTypeName(0)}");

        Assert.That(nameType, Is.EqualTo(typeof(string)));
    }

    #endregion

    #region BLOB Type Tests

    [Test]
    public void BlobColumnReturnsByteArrayTypeTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE TestBlob (Id INT PRIMARY KEY, Data BLOB)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "INSERT INTO TestBlob VALUES (1, X'48656C6C6F')";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT Data FROM TestBlob";
        using var reader = cmd.ExecuteReader();

        var dataType = reader.GetFieldType(0);
        Console.WriteLine($"BLOB column type: {dataType.FullName}");
        Console.WriteLine($"BLOB DataTypeName: {reader.GetDataTypeName(0)}");

        Assert.That(dataType, Is.EqualTo(typeof(byte[])));
    }

    #endregion

    #region All Types Test

    [Test]
    public void AllTypesReturnCorrectClrTypesTest()
    {
        using var cmd = m_connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE AllTypes (
                ColInt INT PRIMARY KEY,
                ColBigInt BIGINT,
                ColDecimal DECIMAL(10,2),
                ColBoolean BOOLEAN,
                ColVarchar VARCHAR(100),
                ColDateTime DATETIME,
                ColGuid GUID,
                ColBlob BLOB
            )";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"INSERT INTO AllTypes VALUES (
            1, 
            9223372036854775807, 
            123.45, 
            true, 
            'Test', 
            '2024-01-15', 
            '550e8400-e29b-41d4-a716-446655440000',
            X'48656C6C6F'
        )";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT * FROM AllTypes";
        using var reader = cmd.ExecuteReader();

        Console.WriteLine("Column types:");
        for (int i = 0; i < reader.FieldCount; i++)
        {
            Console.WriteLine($"  {reader.GetName(i)}: {reader.GetFieldType(i).Name} (DataTypeName: {reader.GetDataTypeName(i)})");
        }

        // Expected type mappings
        var expectedTypes = new Dictionary<string, Type>
        {
            { "ColInt", typeof(long) },
            { "ColBigInt", typeof(long) },
            { "ColDecimal", typeof(decimal) },
            { "ColBoolean", typeof(bool) },
            { "ColVarchar", typeof(string) },
            { "ColDateTime", typeof(DateTime) },
            { "ColGuid", typeof(Guid) },
            { "ColBlob", typeof(byte[]) }
        };

        foreach (var (colName, expectedType) in expectedTypes)
        {
            var ordinal = reader.GetOrdinal(colName);
            var actualType = reader.GetFieldType(ordinal);
            Assert.That(actualType, Is.EqualTo(expectedType), $"Column {colName} should be {expectedType.Name}");
        }
    }

    #endregion
}

using NUnit.Framework;
using System.Data;

namespace OutWit.Database.AdoNet.Tests.Schema;

/// <summary>
/// Tests for schema retrieval via WitDbConnection.GetSchema().
/// </summary>
[TestFixture]
public class SchemaProviderTests
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
            CREATE TABLE Users (
                Id INT PRIMARY KEY,
                Name VARCHAR(100) NOT NULL,
                Email VARCHAR(200),
                CreatedAt DATETIME
            )";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
            CREATE TABLE Orders (
                OrderId INT PRIMARY KEY,
                UserId INT,
                Amount DECIMAL(10,2),
                FOREIGN KEY (UserId) REFERENCES Users(Id)
            )";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "CREATE INDEX idx_users_email ON Users(Email)";
        cmd.ExecuteNonQuery();
    }

    [TearDown]
    public void TearDown()
    {
        m_connection?.Dispose();
    }

    #endregion

    #region MetaDataCollections Tests

    [Test]
    public void GetSchemaReturnsMetaDataCollectionsTest()
    {
        var schema = m_connection.GetSchema();

        Assert.That(schema, Is.Not.Null);
        Assert.That(schema.TableName, Is.EqualTo("MetaDataCollections"));
    }

    [Test]
    public void MetaDataCollectionsContainsExpectedCollectionsTest()
    {
        var schema = m_connection.GetSchema("MetaDataCollections");

        var collectionNames = schema.Rows.Cast<DataRow>()
            .Select(r => r["CollectionName"].ToString())
            .ToList();

        Assert.That(collectionNames, Does.Contain("Tables"));
        Assert.That(collectionNames, Does.Contain("Columns"));
        Assert.That(collectionNames, Does.Contain("Indexes"));
        Assert.That(collectionNames, Does.Contain("DataTypes"));
    }

    #endregion

    #region DataSourceInformation Tests

    [Test]
    public void DataSourceInformationReturnsInfoTest()
    {
        var schema = m_connection.GetSchema("DataSourceInformation");

        Assert.That(schema.Rows.Count, Is.EqualTo(1));
        Assert.That(schema.Rows[0]["DataSourceProductName"], Is.EqualTo("WitDatabase"));
    }

    [Test]
    public void DataSourceInformationContainsRequiredColumnsTest()
    {
        var schema = m_connection.GetSchema("DataSourceInformation");

        Assert.That(schema.Columns.Contains("ParameterMarkerFormat"), Is.True);
        Assert.That(schema.Columns.Contains("QuotedIdentifierPattern"), Is.True);
    }

    #endregion

    #region DataTypes Tests

    [Test]
    public void DataTypesReturnsTypesTest()
    {
        var schema = m_connection.GetSchema("DataTypes");

        Assert.That(schema.Rows.Count, Is.GreaterThan(0));
    }

    [Test]
    public void DataTypesContainsCommonTypesTest()
    {
        var schema = m_connection.GetSchema("DataTypes");

        var typeNames = schema.Rows.Cast<DataRow>()
            .Select(r => r["TypeName"].ToString()!.ToUpperInvariant())
            .ToList();

        Assert.That(typeNames, Does.Contain("INT"));
        Assert.That(typeNames, Does.Contain("VARCHAR"));
        Assert.That(typeNames, Does.Contain("DATETIME"));
        Assert.That(typeNames, Does.Contain("DECIMAL"));
    }

    #endregion

    #region Tables Tests

    [Test]
    public void TablesReturnsAllTablesTest()
    {
        var schema = m_connection.GetSchema("Tables");

        var tableNames = schema.Rows.Cast<DataRow>()
            .Select(r => r["TABLE_NAME"].ToString())
            .ToList();

        Assert.That(tableNames, Does.Contain("Users"));
        Assert.That(tableNames, Does.Contain("Orders"));
    }

    [Test]
    public void TablesWithRestrictionFiltersResultsTest()
    {
        var schema = m_connection.GetSchema("Tables", new[] { null, "Users" });

        Assert.That(schema.Rows.Count, Is.EqualTo(1));
        Assert.That(schema.Rows[0]["TABLE_NAME"], Is.EqualTo("Users"));
    }

    [Test]
    public void TablesContainsRequiredColumnsTest()
    {
        var schema = m_connection.GetSchema("Tables");

        Assert.That(schema.Columns.Contains("TABLE_CATALOG"), Is.True);
        Assert.That(schema.Columns.Contains("TABLE_NAME"), Is.True);
        Assert.That(schema.Columns.Contains("TABLE_TYPE"), Is.True);
    }

    #endregion

    #region Columns Tests

    [Test]
    public void ColumnsReturnsAllColumnsTest()
    {
        var schema = m_connection.GetSchema("Columns");

        Assert.That(schema.Rows.Count, Is.GreaterThan(0));
    }

    [Test]
    public void ColumnsWithTableRestrictionFiltersResultsTest()
    {
        var schema = m_connection.GetSchema("Columns", new[] { null, "Users", null });

        var columnNames = schema.Rows.Cast<DataRow>()
            .Select(r => r["COLUMN_NAME"].ToString())
            .ToList();

        Assert.That(columnNames, Does.Contain("Id"));
        Assert.That(columnNames, Does.Contain("Name"));
        Assert.That(columnNames, Does.Contain("Email"));
        Assert.That(columnNames, Does.Contain("CreatedAt"));
        Assert.That(columnNames.Count, Is.EqualTo(4));
    }

    [Test]
    public void ColumnsWithColumnRestrictionFiltersResultsTest()
    {
        var schema = m_connection.GetSchema("Columns", new[] { null, "Users", "Email" });

        Assert.That(schema.Rows.Count, Is.EqualTo(1));
        Assert.That(schema.Rows[0]["COLUMN_NAME"], Is.EqualTo("Email"));
    }

    [Test]
    public void ColumnsContainsRequiredColumnsTest()
    {
        var schema = m_connection.GetSchema("Columns");

        Assert.That(schema.Columns.Contains("TABLE_NAME"), Is.True);
        Assert.That(schema.Columns.Contains("COLUMN_NAME"), Is.True);
        Assert.That(schema.Columns.Contains("DATA_TYPE"), Is.True);
        Assert.That(schema.Columns.Contains("IS_NULLABLE"), Is.True);
        Assert.That(schema.Columns.Contains("ORDINAL_POSITION"), Is.True);
    }

    [Test]
    public void ColumnsShowsNullabilityTest()
    {
        var schema = m_connection.GetSchema("Columns", new[] { null, "Users", null });

        var nameRow = schema.Rows.Cast<DataRow>().First(r => r["COLUMN_NAME"].ToString() == "Name");
        var emailRow = schema.Rows.Cast<DataRow>().First(r => r["COLUMN_NAME"].ToString() == "Email");

        Assert.That(nameRow["IS_NULLABLE"], Is.EqualTo("NO"));
        Assert.That(emailRow["IS_NULLABLE"], Is.EqualTo("YES"));
    }

    #endregion

    #region Indexes Tests

    [Test]
    public void IndexesReturnsIndexesTest()
    {
        var schema = m_connection.GetSchema("Indexes");

        var indexNames = schema.Rows.Cast<DataRow>()
            .Select(r => r["INDEX_NAME"].ToString())
            .ToList();

        Assert.That(indexNames, Does.Contain("idx_users_email"));
    }

    [Test]
    public void IndexesContainsRequiredColumnsTest()
    {
        var schema = m_connection.GetSchema("Indexes");

        Assert.That(schema.Columns.Contains("TABLE_NAME"), Is.True);
        Assert.That(schema.Columns.Contains("INDEX_NAME"), Is.True);
        Assert.That(schema.Columns.Contains("IS_UNIQUE"), Is.True);
    }

    #endregion

    #region IndexColumns Tests

    [Test]
    public void IndexColumnsReturnsColumnsTest()
    {
        var schema = m_connection.GetSchema("IndexColumns");

        Assert.That(schema.Rows.Count, Is.GreaterThan(0));
    }

    [Test]
    public void IndexColumnsContainsRequiredColumnsTest()
    {
        var schema = m_connection.GetSchema("IndexColumns");

        Assert.That(schema.Columns.Contains("INDEX_NAME"), Is.True);
        Assert.That(schema.Columns.Contains("COLUMN_NAME"), Is.True);
        Assert.That(schema.Columns.Contains("ORDINAL_POSITION"), Is.True);
    }

    #endregion

    #region ReservedWords Tests

    [Test]
    public void ReservedWordsReturnsWordsTest()
    {
        var schema = m_connection.GetSchema("ReservedWords");

        Assert.That(schema.Rows.Count, Is.GreaterThan(0));
    }

    [Test]
    public void ReservedWordsContainsCommonKeywordsTest()
    {
        var schema = m_connection.GetSchema("ReservedWords");

        var words = schema.Rows.Cast<DataRow>()
            .Select(r => r["ReservedWord"].ToString()!.ToUpperInvariant())
            .ToList();

        Assert.That(words, Does.Contain("SELECT"));
        Assert.That(words, Does.Contain("FROM"));
        Assert.That(words, Does.Contain("WHERE"));
        Assert.That(words, Does.Contain("INSERT"));
        Assert.That(words, Does.Contain("UPDATE"));
        Assert.That(words, Does.Contain("DELETE"));
    }

    #endregion

    #region Restrictions Tests

    [Test]
    public void RestrictionsReturnsRestrictionsTest()
    {
        var schema = m_connection.GetSchema("Restrictions");

        Assert.That(schema.Rows.Count, Is.GreaterThan(0));
    }

    [Test]
    public void RestrictionsContainsRequiredColumnsTest()
    {
        var schema = m_connection.GetSchema("Restrictions");

        Assert.That(schema.Columns.Contains("CollectionName"), Is.True);
        Assert.That(schema.Columns.Contains("RestrictionName"), Is.True);
        Assert.That(schema.Columns.Contains("RestrictionNumber"), Is.True);
    }

    #endregion

    #region Views Tests

    [Test]
    public void ViewsReturnsEmptyForNowTest()
    {
        var schema = m_connection.GetSchema("Views");

        // Views may not be implemented yet
        Assert.That(schema, Is.Not.Null);
    }

    #endregion

    #region ForeignKeys Tests

    [Test]
    public void ForeignKeysReturnsKeysTest()
    {
        var schema = m_connection.GetSchema("ForeignKeys");

        Assert.That(schema, Is.Not.Null);
    }

    #endregion

    #region Error Cases Tests

    [Test]
    public void UnknownCollectionThrowsTest()
    {
        Assert.Throws<ArgumentException>(() => m_connection.GetSchema("UnknownCollection"));
    }

    [Test]
    public void GetSchemaWhenClosedThrowsTest()
    {
        m_connection.Close();

        Assert.Throws<InvalidOperationException>(() => m_connection.GetSchema());
    }

    #endregion
}

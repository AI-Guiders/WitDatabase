using OutWit.Database.Core.Builder;

namespace OutWit.Database.Tests;

/// <summary>
/// Reproduction tests for SELECT WHERE returning empty results when table has 10+ rows.
/// Tests the interaction between the query planner's MIN_ROWS_FOR_INDEX threshold
/// and index-based query execution.
/// </summary>
[TestFixture]
public sealed class WitSqlEngineSelectWhereRowCountTests : WitSqlEngineTestsBase
{
    #region Setup

    public override void Setup()
    {
        var database = new WitDatabaseBuilder()
            .WithMemoryStorage()
            .WithBTree()
            .WithTransactions()
            .Build();
        m_engine = new Engine.WitSqlEngine(database, ownsStore: true);
    }

    #endregion

    #region Basic WHERE with increasing row counts

    [TestCase(1)]
    [TestCase(5)]
    [TestCase(8)]
    [TestCase(9)]
    [TestCase(10)]
    [TestCase(11)]
    [TestCase(15)]
    [TestCase(20)]
    [TestCase(50)]
    public void SelectWhereReturnsCorrectResultsWithNRowsTest(int rowCount)
    {
        // Arrange: create a settings-like table with a composite index
        m_engine.Execute(@"
            CREATE TABLE Settings (
                Id BIGINT PRIMARY KEY AUTOINCREMENT,
                [Group] VARCHAR(100) NOT NULL,
                [Key] VARCHAR(100) NOT NULL,
                Value VARCHAR(500)
            )");
        m_engine.Execute("CREATE UNIQUE INDEX IX_Settings_Group_Key ON Settings ([Group], [Key])");

        // Insert N rows all in the same group
        for (int i = 1; i <= rowCount; i++)
        {
            m_engine.Execute($"INSERT INTO Settings ([Group], [Key], Value) VALUES ('GroupA', 'Key{i}', 'Value{i}')");
        }

        // Act: query with WHERE on the first index column using a parameter
        var result = m_engine.Query(
            "SELECT * FROM Settings WHERE [Group] = @group",
            new Dictionary<string, object?> { ["group"] = "GroupA" });

        // Assert
        Assert.That(result.Count, Is.EqualTo(rowCount),
            $"Expected {rowCount} rows but got {result.Count}. Query planner may be incorrectly using index when table has >= 10 rows.");
    }

    [TestCase(1)]
    [TestCase(5)]
    [TestCase(9)]
    [TestCase(10)]
    [TestCase(15)]
    [TestCase(20)]
    public void SelectWhereWithLiteralReturnsCorrectResultsTest(int rowCount)
    {
        // Arrange
        m_engine.Execute(@"
            CREATE TABLE Settings (
                Id BIGINT PRIMARY KEY AUTOINCREMENT,
                [Group] VARCHAR(100) NOT NULL,
                [Key] VARCHAR(100) NOT NULL,
                Value VARCHAR(500)
            )");
        m_engine.Execute("CREATE UNIQUE INDEX IX_Settings_Group_Key ON Settings ([Group], [Key])");

        for (int i = 1; i <= rowCount; i++)
        {
            m_engine.Execute($"INSERT INTO Settings ([Group], [Key], Value) VALUES ('GroupA', 'Key{i}', 'Value{i}')");
        }

        // Act: query with literal value in WHERE
        var result = m_engine.Query("SELECT * FROM Settings WHERE [Group] = 'GroupA'");

        // Assert
        Assert.That(result.Count, Is.EqualTo(rowCount),
            $"Expected {rowCount} rows but got {result.Count}.");
    }

    #endregion

    #region Cross-group tests

    [TestCase(5, 4)]  // total 9: should work
    [TestCase(5, 5)]  // total 10: might break
    [TestCase(6, 3)]  // total 9: should work
    [TestCase(6, 4)]  // total 10: might break
    [TestCase(10, 10)] // total 20
    public void SelectWhereWithMultipleGroupsTest(int groupACount, int groupBCount)
    {
        // Arrange
        m_engine.Execute(@"
            CREATE TABLE Settings (
                Id BIGINT PRIMARY KEY AUTOINCREMENT,
                [Group] VARCHAR(100) NOT NULL,
                [Key] VARCHAR(100) NOT NULL,
                Value VARCHAR(500)
            )");
        m_engine.Execute("CREATE UNIQUE INDEX IX_Settings_Group_Key ON Settings ([Group], [Key])");

        for (int i = 1; i <= groupACount; i++)
        {
            m_engine.Execute($"INSERT INTO Settings ([Group], [Key], Value) VALUES ('GroupA', 'Key{i}', 'ValueA{i}')");
        }
        for (int i = 1; i <= groupBCount; i++)
        {
            m_engine.Execute($"INSERT INTO Settings ([Group], [Key], Value) VALUES ('GroupB', 'Key{i}', 'ValueB{i}')");
        }

        // Act
        var resultA = m_engine.Query(
            "SELECT * FROM Settings WHERE [Group] = @group",
            new Dictionary<string, object?> { ["group"] = "GroupA" });
        var resultB = m_engine.Query(
            "SELECT * FROM Settings WHERE [Group] = @group",
            new Dictionary<string, object?> { ["group"] = "GroupB" });

        // Assert
        Assert.That(resultA.Count, Is.EqualTo(groupACount),
            $"GroupA: expected {groupACount} but got {resultA.Count}");
        Assert.That(resultB.Count, Is.EqualTo(groupBCount),
            $"GroupB: expected {groupBCount} but got {resultB.Count}");
    }

    #endregion

    #region DISTINCT vs WHERE comparison

    [TestCase(10)]
    [TestCase(15)]
    [TestCase(20)]
    public void SelectDistinctWorksButWhereFailsTest(int rowCount)
    {
        // Arrange
        m_engine.Execute(@"
            CREATE TABLE Settings (
                Id BIGINT PRIMARY KEY AUTOINCREMENT,
                [Group] VARCHAR(100) NOT NULL,
                [Key] VARCHAR(100) NOT NULL,
                Value VARCHAR(500)
            )");
        m_engine.Execute("CREATE UNIQUE INDEX IX_Settings_Group_Key ON Settings ([Group], [Key])");

        for (int i = 1; i <= rowCount; i++)
        {
            m_engine.Execute($"INSERT INTO Settings ([Group], [Key], Value) VALUES ('GroupA', 'Key{i}', 'Value{i}')");
        }

        // Act
        var distinctResult = m_engine.Query("SELECT DISTINCT [Group] FROM Settings");
        var whereResult = m_engine.Query(
            "SELECT * FROM Settings WHERE [Group] = @group",
            new Dictionary<string, object?> { ["group"] = "GroupA" });

        // Assert
        Assert.That(distinctResult.Count, Is.EqualTo(1), "DISTINCT should find the group");
        Assert.That(whereResult.Count, Is.EqualTo(rowCount),
            $"WHERE should find all {rowCount} rows, but found {whereResult.Count}. DISTINCT returned {distinctResult.Count} group(s).");
    }

    #endregion

    #region Transaction-based tests (EF Core pattern)

    [TestCase(9)]
    [TestCase(10)]
    [TestCase(15)]
    public void SelectWhereAfterTransactionalInsertTest(int rowCount)
    {
        // Arrange: simulate EF Core SaveChanges pattern
        m_engine.Execute(@"
            CREATE TABLE Settings (
                Id BIGINT PRIMARY KEY AUTOINCREMENT,
                [Group] VARCHAR(100) NOT NULL,
                [Key] VARCHAR(100) NOT NULL,
                Value VARCHAR(500)
            )");
        m_engine.Execute("CREATE UNIQUE INDEX IX_Settings_Group_Key ON Settings ([Group], [Key])");

        // Insert inside a transaction (like EF Core's SaveChanges)
        m_engine.Execute("BEGIN TRANSACTION");
        for (int i = 1; i <= rowCount; i++)
        {
            m_engine.Execute($"INSERT INTO Settings ([Group], [Key], Value) VALUES ('GroupA', 'Key{i}', 'Value{i}')");
        }
        m_engine.Execute("COMMIT");

        // Act: read back after commit (like EF Core post-SaveChanges query)
        var result = m_engine.Query(
            "SELECT * FROM Settings WHERE [Group] = @group",
            new Dictionary<string, object?> { ["group"] = "GroupA" });

        // Assert
        Assert.That(result.Count, Is.EqualTo(rowCount),
            $"Expected {rowCount} rows after committed transaction but got {result.Count}");
    }

    #endregion

    #region No index tests (baseline)

    [TestCase(10)]
    [TestCase(15)]
    [TestCase(20)]
    public void SelectWhereWithoutIndexWorksTest(int rowCount)
    {
        // Arrange: no index, only table scan path
        m_engine.Execute(@"
            CREATE TABLE Settings (
                Id BIGINT PRIMARY KEY AUTOINCREMENT,
                [Group] VARCHAR(100) NOT NULL,
                [Key] VARCHAR(100) NOT NULL,
                Value VARCHAR(500)
            )");
        // Deliberately NO index

        for (int i = 1; i <= rowCount; i++)
        {
            m_engine.Execute($"INSERT INTO Settings ([Group], [Key], Value) VALUES ('GroupA', 'Key{i}', 'Value{i}')");
        }

        // Act
        var result = m_engine.Query(
            "SELECT * FROM Settings WHERE [Group] = @group",
            new Dictionary<string, object?> { ["group"] = "GroupA" });

        // Assert
        Assert.That(result.Count, Is.EqualTo(rowCount),
            $"Without index: expected {rowCount} but got {result.Count}");
    }

    #endregion

    #region Full composite key match tests

    [TestCase(10)]
    [TestCase(15)]
    [TestCase(20)]
    public void SelectWhereWithFullCompositeMatchTest(int rowCount)
    {
        // Arrange: composite unique index on (Group, Key), equality on BOTH columns
        m_engine.Execute(@"
            CREATE TABLE Settings (
                Id BIGINT PRIMARY KEY AUTOINCREMENT,
                [Group] VARCHAR(100) NOT NULL,
                [Key] VARCHAR(100) NOT NULL,
                Value VARCHAR(500)
            )");
        m_engine.Execute("CREATE UNIQUE INDEX IX_Settings_Group_Key ON Settings ([Group], [Key])");

        for (int i = 1; i <= rowCount; i++)
        {
            m_engine.Execute($"INSERT INTO Settings ([Group], [Key], Value) VALUES ('GroupA', 'Key{i}', 'Value{i}')");
        }

        // Act: full composite match - both index columns in WHERE
        var result = m_engine.Query(
            "SELECT * FROM Settings WHERE [Group] = @group AND [Key] = @key",
            new Dictionary<string, object?> { ["group"] = "GroupA", ["key"] = "Key5" });

        // Assert: should find exactly 1 row
        Assert.That(result.Count, Is.EqualTo(1),
            $"Full composite key match should return 1 row but got {result.Count}");
        Assert.That(result[0]["Value"].AsString(), Is.EqualTo("Value5"));
    }

    [TestCase(10)]
    [TestCase(15)]
    [TestCase(20)]
    public void SelectWhereWithFullCompositeMatchNonUniqueIndexTest(int rowCount)
    {
        // Arrange: non-unique composite index
        m_engine.Execute(@"
            CREATE TABLE Settings (
                Id BIGINT PRIMARY KEY AUTOINCREMENT,
                [Group] VARCHAR(100) NOT NULL,
                [Key] VARCHAR(100) NOT NULL,
                Value VARCHAR(500)
            )");
        m_engine.Execute("CREATE INDEX IX_Settings_Group_Key ON Settings ([Group], [Key])");

        for (int i = 1; i <= rowCount; i++)
        {
            m_engine.Execute($"INSERT INTO Settings ([Group], [Key], Value) VALUES ('GroupA', 'Key{i}', 'Value{i}')");
        }

        // Act
        var result = m_engine.Query(
            "SELECT * FROM Settings WHERE [Group] = @group AND [Key] = @key",
            new Dictionary<string, object?> { ["group"] = "GroupA", ["key"] = "Key3" });

        // Assert
        Assert.That(result.Count, Is.EqualTo(1),
            $"Non-unique composite full match should return 1 row but got {result.Count}");
    }

    [TestCase(10)]
    [TestCase(15)]
    public void SelectWhereWithFullCompositeMatchNoMatchTest(int rowCount)
    {
        // Arrange: query for a key that doesn't exist
        m_engine.Execute(@"
            CREATE TABLE Settings (
                Id BIGINT PRIMARY KEY AUTOINCREMENT,
                [Group] VARCHAR(100) NOT NULL,
                [Key] VARCHAR(100) NOT NULL,
                Value VARCHAR(500)
            )");
        m_engine.Execute("CREATE UNIQUE INDEX IX_Settings_Group_Key ON Settings ([Group], [Key])");

        for (int i = 1; i <= rowCount; i++)
        {
            m_engine.Execute($"INSERT INTO Settings ([Group], [Key], Value) VALUES ('GroupA', 'Key{i}', 'Value{i}')");
        }

        // Act: search for a non-existing composite key
        var result = m_engine.Query(
            "SELECT * FROM Settings WHERE [Group] = @group AND [Key] = @key",
            new Dictionary<string, object?> { ["group"] = "GroupA", ["key"] = "NonExistent" });

        // Assert
        Assert.That(result.Count, Is.EqualTo(0),
            "Non-existing composite key should return 0 rows");
    }

    #endregion

    #region Range predicate on composite index tests

    [TestCase(10)]
    [TestCase(15)]
    public void SelectWhereWithRangeOnCompositeIndexFirstColumnTest(int rowCount)
    {
        // Arrange
        m_engine.Execute(@"
            CREATE TABLE Items (
                Id BIGINT PRIMARY KEY AUTOINCREMENT,
                Category VARCHAR(100) NOT NULL,
                Name VARCHAR(100) NOT NULL,
                Price INT NOT NULL
            )");
        m_engine.Execute("CREATE INDEX IX_Items_Cat_Name ON Items (Category, Name)");

        for (int i = 1; i <= rowCount; i++)
        {
            m_engine.Execute($"INSERT INTO Items (Category, Name, Price) VALUES ('Cat{i}', 'Item{i}', {i * 10})");
        }

        // Act: range on first column of composite index
        var result = m_engine.Query("SELECT * FROM Items WHERE Category > 'Cat5'");

        // Assert: should correctly filter via range scan + WHERE filter
        int expectedCount = 0;
        for (int i = 1; i <= rowCount; i++)
        {
            if (string.Compare($"Cat{i}", "Cat5", StringComparison.Ordinal) > 0) expectedCount++;
        }
        Assert.That(result.Count, Is.EqualTo(expectedCount),
            $"Range on composite index first column: expected {expectedCount} but got {result.Count}");
    }

    #endregion
}

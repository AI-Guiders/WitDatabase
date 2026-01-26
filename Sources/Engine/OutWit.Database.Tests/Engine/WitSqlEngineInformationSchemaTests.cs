using OutWit.Database.Core.Builder;

namespace OutWit.Database.Tests;

/// <summary>
/// Tests for INFORMATION_SCHEMA views.
/// </summary>
[TestFixture]
public sealed class WitSqlEngineInformationSchemaTests : WitSqlEngineTestsBase
{
    #region INFORMATION_SCHEMA.TABLES Tests

    [Test]
    public void InformationSchemaTablesReturnsAllTablesTest()
    {
        m_engine.Execute("CREATE TABLE Users (Id BIGINT PRIMARY KEY, Name VARCHAR(100))");
        m_engine.Execute("CREATE TABLE Orders (Id BIGINT PRIMARY KEY, UserId BIGINT)");
        
        var rows = m_engine.Query("SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'");
        
        Assert.That(rows, Has.Count.EqualTo(2));
        Assert.That(rows.Select(r => r["TABLE_NAME"].AsString()), Does.Contain("Users"));
        Assert.That(rows.Select(r => r["TABLE_NAME"].AsString()), Does.Contain("Orders"));
    }

    [Test]
    public void InformationSchemaTablesIncludesViewsTest()
    {
        m_engine.Execute("CREATE TABLE Users (Id BIGINT PRIMARY KEY, Name VARCHAR(100))");
        m_engine.Execute("CREATE VIEW ActiveUsers AS SELECT * FROM Users");
        
        var rows = m_engine.Query("SELECT * FROM INFORMATION_SCHEMA.TABLES ORDER BY TABLE_NAME");
        
        Assert.That(rows, Has.Count.EqualTo(2));
        Assert.That(rows.Any(r => r["TABLE_TYPE"].AsString() == "VIEW"), Is.True);
    }

    [Test]
    public void InformationSchemaTablesFilterByTableTypeTest()
    {
        m_engine.Execute("CREATE TABLE Users (Id BIGINT PRIMARY KEY, Name VARCHAR(100))");
        m_engine.Execute("CREATE VIEW ActiveUsers AS SELECT * FROM Users");
        
        var tables = m_engine.Query("SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'");
        var views = m_engine.Query("SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'VIEW'");
        
        Assert.That(tables, Has.Count.EqualTo(1));
        Assert.That(views, Has.Count.EqualTo(1));
    }

    #endregion

    #region INFORMATION_SCHEMA.COLUMNS Tests

    [Test]
    public void InformationSchemaColumnsReturnsAllColumnsTest()
    {
        m_engine.Execute(@"
            CREATE TABLE Users (
                Id BIGINT PRIMARY KEY,
                Name VARCHAR(100) NOT NULL,
                Email VARCHAR(255)
            )");
        
        var rows = m_engine.Query(@"
            SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
            WHERE TABLE_NAME = 'Users' 
            ORDER BY ORDINAL_POSITION");
        
        Assert.That(rows, Has.Count.EqualTo(3));
        Assert.That(rows[0]["COLUMN_NAME"].AsString(), Is.EqualTo("Id"));
        Assert.That(rows[1]["COLUMN_NAME"].AsString(), Is.EqualTo("Name"));
        Assert.That(rows[2]["COLUMN_NAME"].AsString(), Is.EqualTo("Email"));
    }

    [Test]
    public void InformationSchemaColumnsShowsNullabilityTest()
    {
        m_engine.Execute(@"
            CREATE TABLE Users (
                Id BIGINT PRIMARY KEY,
                Name VARCHAR(100) NOT NULL,
                Email VARCHAR(255)
            )");
        
        var rows = m_engine.Query(@"
            SELECT COLUMN_NAME, IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS 
            WHERE TABLE_NAME = 'Users' 
            ORDER BY ORDINAL_POSITION");
        
        Assert.That(rows[0]["IS_NULLABLE"].AsString(), Is.EqualTo("NO")); // Id (PK, not nullable)
        Assert.That(rows[1]["IS_NULLABLE"].AsString(), Is.EqualTo("NO")); // Name (NOT NULL)
        Assert.That(rows[2]["IS_NULLABLE"].AsString(), Is.EqualTo("YES")); // Email (nullable)
    }

    [Test]
    public void InformationSchemaColumnsShowsDataTypeTest()
    {
        m_engine.Execute(@"
            CREATE TABLE Products (
                Id BIGINT PRIMARY KEY,
                Name VARCHAR(100),
                Price DECIMAL(10,2),
                Quantity INTEGER
            )");
        
        var rows = m_engine.Query(@"
            SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS 
            WHERE TABLE_NAME = 'Products' 
            ORDER BY ORDINAL_POSITION");
        
        Assert.That(rows[0]["DATA_TYPE"].AsString(), Is.EqualTo("BIGINT"));
        Assert.That(rows[1]["DATA_TYPE"].AsString(), Is.EqualTo("VARCHAR"));
        Assert.That(rows[2]["DATA_TYPE"].AsString(), Is.EqualTo("DECIMAL"));
        Assert.That(rows[3]["DATA_TYPE"].AsString(), Is.EqualTo("INTEGER"));
    }

    [Test]
    public void InformationSchemaColumnsShowsComputedColumnTest()
    {
        m_engine.Execute(@"
            CREATE TABLE Products (
                Id BIGINT PRIMARY KEY,
                Price DECIMAL(10,2),
                Quantity INTEGER,
                Total AS (Price * Quantity) STORED
            )");
        
        var rows = m_engine.Query(@"
            SELECT COLUMN_NAME, IS_GENERATED, GENERATION_EXPRESSION FROM INFORMATION_SCHEMA.COLUMNS 
            WHERE TABLE_NAME = 'Products' AND COLUMN_NAME = 'Total'");
        
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0]["IS_GENERATED"].AsString(), Is.EqualTo("STORED"));
        // Expression may include parentheses from serialization
        Assert.That(rows[0]["GENERATION_EXPRESSION"].AsString(), Does.Contain("Price"));
        Assert.That(rows[0]["GENERATION_EXPRESSION"].AsString(), Does.Contain("Quantity"));
    }

    [Test]
    public void InformationSchemaColumnsShowsAutoIncrementTest()
    {
        m_engine.Execute(@"
            CREATE TABLE Users (
                Id BIGINT PRIMARY KEY AUTOINCREMENT,
                Name VARCHAR(100) NOT NULL
            )");
        
        var rows = m_engine.Query(@"
            SELECT COLUMN_NAME, IS_AUTOINCREMENT FROM INFORMATION_SCHEMA.COLUMNS 
            WHERE TABLE_NAME = 'Users' 
            ORDER BY ORDINAL_POSITION");
        
        Assert.That(rows, Has.Count.EqualTo(2));
        Assert.That(rows[0]["COLUMN_NAME"].AsString(), Is.EqualTo("Id"));
        Assert.That(rows[0]["IS_AUTOINCREMENT"].AsString(), Is.EqualTo("YES"));
        Assert.That(rows[1]["COLUMN_NAME"].AsString(), Is.EqualTo("Name"));
        Assert.That(rows[1]["IS_AUTOINCREMENT"].AsString(), Is.EqualTo("NO"));
    }

    [Test]
    public void InformationSchemaColumnsShowsUniqueConstraintTest()
    {
        m_engine.Execute(@"
            CREATE TABLE Users (
                Id BIGINT PRIMARY KEY,
                Email VARCHAR(255) UNIQUE,
                Name VARCHAR(100)
            )");
        
        var rows = m_engine.Query(@"
            SELECT COLUMN_NAME, IS_UNIQUE FROM INFORMATION_SCHEMA.COLUMNS 
            WHERE TABLE_NAME = 'Users' 
            ORDER BY ORDINAL_POSITION");
        
        Assert.That(rows, Has.Count.EqualTo(3));
        Assert.That(rows[0]["COLUMN_NAME"].AsString(), Is.EqualTo("Id"));
        Assert.That(rows[0]["IS_UNIQUE"].AsString(), Is.EqualTo("NO")); // PK is not marked as UNIQUE separately
        Assert.That(rows[1]["COLUMN_NAME"].AsString(), Is.EqualTo("Email"));
        Assert.That(rows[1]["IS_UNIQUE"].AsString(), Is.EqualTo("YES"));
        Assert.That(rows[2]["COLUMN_NAME"].AsString(), Is.EqualTo("Name"));
        Assert.That(rows[2]["IS_UNIQUE"].AsString(), Is.EqualTo("NO"));
    }

    [Test]
    public void InformationSchemaColumnsShowsCheckExpressionTest()
    {
        m_engine.Execute(@"
            CREATE TABLE Products (
                Id BIGINT PRIMARY KEY,
                Price DECIMAL(10,2) CHECK (Price > 0),
                Quantity INTEGER
            )");
        
        var rows = m_engine.Query(@"
            SELECT COLUMN_NAME, CHECK_EXPRESSION FROM INFORMATION_SCHEMA.COLUMNS 
            WHERE TABLE_NAME = 'Products' AND CHECK_EXPRESSION IS NOT NULL");
        
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0]["COLUMN_NAME"].AsString(), Is.EqualTo("Price"));
        Assert.That(rows[0]["CHECK_EXPRESSION"].AsString(), Does.Contain("Price"));
        Assert.That(rows[0]["CHECK_EXPRESSION"].AsString(), Does.Contain("0"));
    }

    // Note: COLLATE as a column constraint in CREATE TABLE is not currently supported.
    // COLLATE is supported in expressions (e.g., WHERE Name COLLATE NOCASE = 'value')
    // and ORDER BY clauses. The COLLATION_NAME column will be NULL for columns
    // until column-level COLLATE constraint parsing is implemented.

    // Note: CHARACTER_MAXIMUM_LENGTH and NUMERIC_PRECISION/SCALE from INFORMATION_SCHEMA.COLUMNS
    // may return NULL if the parser/engine doesn't preserve these type details.
    // These features require parser support for storing type parameters in DefinitionColumn.

    #endregion

    #region INFORMATION_SCHEMA.KEY_COLUMN_USAGE Tests

    [Test]
    public void InformationSchemaKeyColumnUsageShowsPrimaryKeyTest()
    {
        m_engine.Execute(@"
            CREATE TABLE Users (
                Id BIGINT PRIMARY KEY,
                Name VARCHAR(100)
            )");
        
        var rows = m_engine.Query(@"
            SELECT * FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE 
            WHERE TABLE_NAME = 'Users'");
        
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0]["COLUMN_NAME"].AsString(), Is.EqualTo("Id"));
        Assert.That(rows[0]["CONSTRAINT_NAME"].AsString(), Does.Contain("PK_"));
    }

    [Test]
    public void InformationSchemaKeyColumnUsageShowsForeignKeyTest()
    {
        m_engine.Execute("CREATE TABLE Users (Id BIGINT PRIMARY KEY, Name VARCHAR(100))");
        m_engine.Execute(@"
            CREATE TABLE Orders (
                Id BIGINT PRIMARY KEY,
                UserId BIGINT REFERENCES Users(Id)
            )");
        
        var rows = m_engine.Query(@"
            SELECT * FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE 
            WHERE TABLE_NAME = 'Orders' AND REFERENCED_TABLE_NAME IS NOT NULL");
        
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0]["COLUMN_NAME"].AsString(), Is.EqualTo("UserId"));
        Assert.That(rows[0]["REFERENCED_TABLE_NAME"].AsString(), Is.EqualTo("Users"));
    }

    #endregion

    #region INFORMATION_SCHEMA.TABLE_CONSTRAINTS Tests

    [Test]
    public void InformationSchemaTableConstraintsShowsPrimaryKeyTest()
    {
        m_engine.Execute(@"
            CREATE TABLE Users (
                Id BIGINT PRIMARY KEY,
                Name VARCHAR(100)
            )");
        
        var rows = m_engine.Query(@"
            SELECT * FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS 
            WHERE TABLE_NAME = 'Users' AND CONSTRAINT_TYPE = 'PRIMARY KEY'");
        
        Assert.That(rows, Has.Count.EqualTo(1));
    }

    [Test]
    public void InformationSchemaTableConstraintsShowsUniqueConstraintTest()
    {
        m_engine.Execute(@"
            CREATE TABLE Users (
                Id BIGINT PRIMARY KEY,
                Email VARCHAR(255) UNIQUE
            )");
        
        var rows = m_engine.Query(@"
            SELECT * FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS 
            WHERE TABLE_NAME = 'Users' AND CONSTRAINT_TYPE = 'UNIQUE'");
        
        Assert.That(rows, Has.Count.EqualTo(1));
    }

    [Test]
    public void InformationSchemaTableConstraintsShowsCheckConstraintTest()
    {
        m_engine.Execute(@"
            CREATE TABLE Products (
                Id BIGINT PRIMARY KEY,
                Price DECIMAL(10,2) CHECK (Price > 0)
            )");
        
        var rows = m_engine.Query(@"
            SELECT * FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS 
            WHERE TABLE_NAME = 'Products' AND CONSTRAINT_TYPE = 'CHECK'");
        
        Assert.That(rows, Has.Count.EqualTo(1));
    }

    #endregion

    #region INFORMATION_SCHEMA.INDEXES Tests

    [Test]
    public void InformationSchemaIndexesShowsAllIndexesTest()
    {
        m_engine.Execute("CREATE TABLE Users (Id BIGINT PRIMARY KEY, Name VARCHAR(100), Email VARCHAR(255))");
        m_engine.Execute("CREATE INDEX IX_Users_Name ON Users (Name)");
        m_engine.Execute("CREATE UNIQUE INDEX IX_Users_Email ON Users (Email)");
        
        var rows = m_engine.Query(@"
            SELECT * FROM INFORMATION_SCHEMA.INDEXES 
            WHERE TABLE_NAME = 'Users'
            ORDER BY INDEX_NAME");
        
        Assert.That(rows, Has.Count.EqualTo(2));
        Assert.That(rows.Select(r => r["INDEX_NAME"].AsString()), Does.Contain("IX_Users_Name"));
        Assert.That(rows.Select(r => r["INDEX_NAME"].AsString()), Does.Contain("IX_Users_Email"));
    }

    [Test]
    public void InformationSchemaIndexesShowsUniquePropertyTest()
    {
        m_engine.Execute("CREATE TABLE Users (Id BIGINT PRIMARY KEY, Name VARCHAR(100))");
        m_engine.Execute("CREATE INDEX IX_Users_Name ON Users (Name)");
        m_engine.Execute("CREATE UNIQUE INDEX IX_Users_Name_Unique ON Users (Name)");
        
        var nonUnique = m_engine.Query(@"
            SELECT * FROM INFORMATION_SCHEMA.INDEXES 
            WHERE INDEX_NAME = 'IX_Users_Name'");
        
        var unique = m_engine.Query(@"
            SELECT * FROM INFORMATION_SCHEMA.INDEXES 
            WHERE INDEX_NAME = 'IX_Users_Name_Unique'");
        
        Assert.That(nonUnique[0]["IS_UNIQUE"].AsString(), Is.EqualTo("NO"));
        Assert.That(unique[0]["IS_UNIQUE"].AsString(), Is.EqualTo("YES"));
    }

    #endregion

    #region INFORMATION_SCHEMA.VIEWS Tests

    [Test]
    public void InformationSchemaViewsShowsAllViewsTest()
    {
        m_engine.Execute("CREATE TABLE Users (Id BIGINT PRIMARY KEY, Name VARCHAR(100), IsActive BOOLEAN)");
        m_engine.Execute("CREATE VIEW ActiveUsers AS SELECT * FROM Users WHERE IsActive = TRUE");
        m_engine.Execute("CREATE VIEW InactiveUsers AS SELECT * FROM Users WHERE IsActive = FALSE");
        
        var rows = m_engine.Query("SELECT * FROM INFORMATION_SCHEMA.VIEWS ORDER BY TABLE_NAME");
        
        Assert.That(rows, Has.Count.EqualTo(2));
        Assert.That(rows.Select(r => r["TABLE_NAME"].AsString()), Does.Contain("ActiveUsers"));
        Assert.That(rows.Select(r => r["TABLE_NAME"].AsString()), Does.Contain("InactiveUsers"));
    }

    [Test]
    public void InformationSchemaViewsShowsDefinitionTest()
    {
        m_engine.Execute("CREATE TABLE Users (Id BIGINT PRIMARY KEY, Name VARCHAR(100))");
        m_engine.Execute("CREATE VIEW AllUsers AS SELECT * FROM Users");
        
        var rows = m_engine.Query("SELECT * FROM INFORMATION_SCHEMA.VIEWS WHERE TABLE_NAME = 'AllUsers'");
        
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0]["VIEW_DEFINITION"].AsString(), Does.Contain("SELECT"));
    }

    #endregion

    #region INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS Tests

    [Test]
    public void InformationSchemaReferentialConstraintsShowsForeignKeysTest()
    {
        m_engine.Execute("CREATE TABLE Users (Id BIGINT PRIMARY KEY, Name VARCHAR(100))");
        m_engine.Execute(@"
            CREATE TABLE Orders (
                Id BIGINT PRIMARY KEY,
                UserId BIGINT REFERENCES Users(Id) ON DELETE CASCADE
            )");
        
        var rows = m_engine.Query("SELECT * FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS");
        
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0]["DELETE_RULE"].AsString(), Is.EqualTo("CASCADE"));
    }

    #endregion

    #region WHERE Clause Tests

    [Test]
    public void InformationSchemaSupportsWhereClauseTest()
    {
        m_engine.Execute("CREATE TABLE Users (Id BIGINT PRIMARY KEY, Name VARCHAR(100))");
        m_engine.Execute("CREATE TABLE Orders (Id BIGINT PRIMARY KEY, Total DECIMAL)");
        
        var rows = m_engine.Query(@"
            SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_NAME LIKE 'U%'");
        
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0]["TABLE_NAME"].AsString(), Is.EqualTo("Users"));
    }

    #endregion

    #region JOIN Tests

    [Test]
    public void InformationSchemaSupportsJoinWithTableAndColumnsTest()
    {
        m_engine.Execute("CREATE TABLE Users (Id BIGINT PRIMARY KEY, Name VARCHAR(100))");
        
        var rows = m_engine.Query(@"
            SELECT t.TABLE_NAME, c.COLUMN_NAME, c.DATA_TYPE
            FROM INFORMATION_SCHEMA.TABLES t
            INNER JOIN INFORMATION_SCHEMA.COLUMNS c ON t.TABLE_NAME = c.TABLE_NAME
            WHERE t.TABLE_NAME = 'Users'
            ORDER BY c.ORDINAL_POSITION");
        
        Assert.That(rows, Has.Count.EqualTo(2));
        Assert.That(rows[0]["TABLE_NAME"].AsString(), Is.EqualTo("Users"));
        Assert.That(rows[0]["COLUMN_NAME"].AsString(), Is.EqualTo("Id"));
    }

    #endregion

    #region Aggregate Tests

    [Test]
    public void InformationSchemaSupportsAggregatesTest()
    {
        m_engine.Execute("CREATE TABLE Users (Id BIGINT PRIMARY KEY, Name VARCHAR(100), Email VARCHAR(255))");
        m_engine.Execute("CREATE TABLE Orders (Id BIGINT PRIMARY KEY, Total DECIMAL)");
        
        var result = m_engine.ExecuteScalar(@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
            WHERE TABLE_NAME = 'Users'");
        
        Assert.That(result.AsInt64(), Is.EqualTo(3));
    }

    #endregion

    #region Case Insensitivity Tests

    [Test]
    public void InformationSchemaIsCaseInsensitiveTest()
    {
        m_engine.Execute("CREATE TABLE Users (Id BIGINT PRIMARY KEY)");
        
        var lower = m_engine.Query("SELECT * FROM information_schema.tables");
        var upper = m_engine.Query("SELECT * FROM INFORMATION_SCHEMA.TABLES");
        var mixed = m_engine.Query("SELECT * FROM Information_Schema.Tables");
        
        Assert.That(lower, Has.Count.EqualTo(1));
        Assert.That(upper, Has.Count.EqualTo(1));
        Assert.That(mixed, Has.Count.EqualTo(1));
    }

    #endregion

    #region INFORMATION_SCHEMA.TRIGGERS Tests

    [Test]
    public void InformationSchemaTriggersShowsAllTriggersTest()
    {
        m_engine.Execute("CREATE TABLE Users (Id BIGINT PRIMARY KEY, Name VARCHAR(100))");
        m_engine.Execute(@"
            CREATE TRIGGER trg_users_insert 
            AFTER INSERT ON Users 
            FOR EACH ROW 
            BEGIN 
                SELECT 1; 
            END");
        m_engine.Execute(@"
            CREATE TRIGGER trg_users_update 
            BEFORE UPDATE ON Users 
            FOR EACH ROW 
            BEGIN 
                SELECT 2; 
            END");

        var rows = m_engine.Query("SELECT * FROM INFORMATION_SCHEMA.TRIGGERS ORDER BY TRIGGER_NAME");

        Assert.That(rows, Has.Count.EqualTo(2));
        Assert.That(rows.Select(r => r["TRIGGER_NAME"].AsString()), Does.Contain("trg_users_insert"));
        Assert.That(rows.Select(r => r["TRIGGER_NAME"].AsString()), Does.Contain("trg_users_update"));
    }

    [Test]
    public void InformationSchemaTriggersShowsTimingAndEventTest()
    {
        m_engine.Execute("CREATE TABLE Users (Id BIGINT PRIMARY KEY, Name VARCHAR(100))");
        m_engine.Execute(@"
            CREATE TRIGGER trg_test 
            BEFORE DELETE ON Users 
            FOR EACH ROW 
            BEGIN 
                SELECT 1; 
            END");

        var rows = m_engine.Query(@"
            SELECT TRIGGER_NAME, ACTION_TIMING, EVENT_MANIPULATION, EVENT_OBJECT_TABLE 
            FROM INFORMATION_SCHEMA.TRIGGERS 
            WHERE TRIGGER_NAME = 'trg_test'");

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0]["ACTION_TIMING"].AsString(), Is.EqualTo("BEFORE"));
        Assert.That(rows[0]["EVENT_MANIPULATION"].AsString(), Is.EqualTo("DELETE"));
        Assert.That(rows[0]["EVENT_OBJECT_TABLE"].AsString(), Is.EqualTo("Users"));
    }

    [Test]
    public void InformationSchemaTriggersShowsBodyTest()
    {
        m_engine.Execute("CREATE TABLE Users (Id BIGINT PRIMARY KEY, Name VARCHAR(100))");
        m_engine.Execute(@"
            CREATE TRIGGER trg_test 
            AFTER INSERT ON Users 
            FOR EACH ROW 
            BEGIN 
                SELECT NEW.Name; 
            END");

        var rows = m_engine.Query(@"
            SELECT ACTION_STATEMENT 
            FROM INFORMATION_SCHEMA.TRIGGERS 
            WHERE TRIGGER_NAME = 'trg_test'");

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0]["ACTION_STATEMENT"].AsString(), Does.Contain("SELECT"));
    }

    [Test]
    public void InformationSchemaTriggersEmptyWhenNoTriggersTest()
    {
        m_engine.Execute("CREATE TABLE Users (Id BIGINT PRIMARY KEY)");

        var rows = m_engine.Query("SELECT * FROM INFORMATION_SCHEMA.TRIGGERS");

        Assert.That(rows, Is.Empty);
    }

    #endregion

    #region INFORMATION_SCHEMA.SEQUENCES Tests

    [Test]
    public void InformationSchemaSequencesShowsAllSequencesTest()
    {
        m_engine.Execute("CREATE SEQUENCE seq_orders START WITH 1000");
        m_engine.Execute("CREATE SEQUENCE seq_users START WITH 1");

        var rows = m_engine.Query("SELECT * FROM INFORMATION_SCHEMA.SEQUENCES ORDER BY SEQUENCE_NAME");

        Assert.That(rows, Has.Count.EqualTo(2));
        Assert.That(rows.Select(r => r["SEQUENCE_NAME"].AsString()), Does.Contain("seq_orders"));
        Assert.That(rows.Select(r => r["SEQUENCE_NAME"].AsString()), Does.Contain("seq_users"));
    }

    [Test]
    public void InformationSchemaSequencesShowsStartValueTest()
    {
        m_engine.Execute("CREATE SEQUENCE seq_test START WITH 5000");

        var rows = m_engine.Query(@"
            SELECT SEQUENCE_NAME, START_VALUE 
            FROM INFORMATION_SCHEMA.SEQUENCES 
            WHERE SEQUENCE_NAME = 'seq_test'");

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0]["START_VALUE"].AsInt64(), Is.EqualTo(5000));
    }

    [Test]
    public void InformationSchemaSequencesShowsCurrentValueTest()
    {
        m_engine.Execute("CREATE SEQUENCE seq_test START WITH 100");

        // Get next value a few times to increment the sequence
        m_engine.ExecuteScalar("SELECT NEXTVAL('seq_test')");
        m_engine.ExecuteScalar("SELECT NEXTVAL('seq_test')");
        m_engine.ExecuteScalar("SELECT NEXTVAL('seq_test')");

        var rows = m_engine.Query(@"
            SELECT CURRENT_VALUE 
            FROM INFORMATION_SCHEMA.SEQUENCES 
            WHERE SEQUENCE_NAME = 'seq_test'");

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0]["CURRENT_VALUE"].AsInt64(), Is.EqualTo(102)); // 100, 101, 102
    }

    [Test]
    public void InformationSchemaSequencesEmptyWhenNoSequencesTest()
    {
        m_engine.Execute("CREATE TABLE Users (Id BIGINT PRIMARY KEY)");

        var rows = m_engine.Query("SELECT * FROM INFORMATION_SCHEMA.SEQUENCES");

        Assert.That(rows, Is.Empty);
    }

    #endregion
}

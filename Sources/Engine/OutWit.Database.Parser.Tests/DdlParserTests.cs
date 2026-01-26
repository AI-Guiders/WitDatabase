using OutWit.Database.Parser.Schema.AlterActions;
using OutWit.Database.Parser.Schema.ColumnConstraints;
using OutWit.Database.Parser.Schema.TableConstraints;
using OutWit.Database.Parser.Schema.Types;
using OutWit.Database.Parser.Statements;

namespace OutWit.Database.Parser.Tests;

/// <summary>
/// Tests for DDL statement parsing: CREATE/DROP/ALTER TABLE, INDEX, VIEW, TRIGGER, SEQUENCE.
/// </summary>
[TestFixture]
public class DdlParserTests
{
    #region CREATE TABLE

    [Test]
    public void ParseCreateTableBasicTest()
    {
        var stmt = WitSql.ParseStatement("CREATE TABLE Users (Id INT, Name TEXT)");
        Assert.That(stmt, Is.InstanceOf<WitSqlStatementCreateTable>());
        var create = (WitSqlStatementCreateTable)stmt;
        Assert.That(create.TableName, Is.EqualTo("Users"));
        Assert.That(create.Columns, Has.Count.EqualTo(2));
    }

    [Test]
    public void ParseCreateTableIfNotExistsTest()
    {
        var stmt = WitSql.ParseStatement("CREATE TABLE IF NOT EXISTS Logs (Id INT)");
        var create = (WitSqlStatementCreateTable)stmt;
        Assert.That(create.IfNotExists, Is.True);
    }

    [Test]
    public void ParseCreateTableWithAllConstraintsTest()
    {
        var stmt = WitSql.ParseStatement(@"
            CREATE TABLE Products (
                Id BIGINT PRIMARY KEY AUTOINCREMENT,
                Name VARCHAR(100) NOT NULL UNIQUE,
                Price DECIMAL DEFAULT 0,
                CategoryId INT REFERENCES Categories(Id) ON DELETE CASCADE,
                IsActive BOOLEAN DEFAULT TRUE,
                CHECK (Price >= 0)
            )");
        var create = (WitSqlStatementCreateTable)stmt;
        Assert.That(create.Columns, Has.Count.EqualTo(5));
        
        var idConstraints = create.Columns[0].Constraints;
        Assert.That(idConstraints?.Any(c => c is ColumnConstraintPrimaryKey), Is.True);
        
        var nameConstraints = create.Columns[1].Constraints;
        Assert.That(nameConstraints?.Any(c => c is ColumnConstraintNotNull), Is.True);
        Assert.That(nameConstraints?.Any(c => c is ColumnConstraintUnique), Is.True);
        
        var priceConstraints = create.Columns[2].Constraints;
        Assert.That(priceConstraints?.Any(c => c is ColumnConstraintDefault), Is.True);
        
        var catConstraints = create.Columns[3].Constraints;
        Assert.That(catConstraints?.Any(c => c is ColumnConstraintReferences), Is.True);
    }

    [Test]
    public void ParseCreateTableWithMultipleCheckConstraintsTest()
    {
        var stmt = WitSql.ParseStatement(@"
            CREATE TABLE Products (
                Id INT PRIMARY KEY,
                Price DECIMAL CHECK (Price >= 0),
                Quantity INT CHECK (Quantity >= 0),
                CHECK (Price * Quantity <= 1000000)
            )");
        var create = (WitSqlStatementCreateTable)stmt;
        
        // Column-level CHECK constraints
        var priceConstraints = create.Columns[1].Constraints;
        Assert.That(priceConstraints?.Any(c => c is ColumnConstraintCheck), Is.True);
        
        var qtyConstraints = create.Columns[2].Constraints;
        Assert.That(qtyConstraints?.Any(c => c is ColumnConstraintCheck), Is.True);
        
        // Table-level CHECK constraint
        Assert.That(create.Constraints?.Any(c => c is TableConstraintCheck), Is.True);
    }

    [Test]
    public void ParseCreateTableWithTableConstraintsTest()
    {
        var stmt = WitSql.ParseStatement(@"
            CREATE TABLE OrderItems (
                OrderId INT,
                ProductId INT,
                Quantity INT,
                PRIMARY KEY (OrderId, ProductId),
                FOREIGN KEY (OrderId) REFERENCES Orders(Id),
                UNIQUE (OrderId, ProductId)
            )");
        var create = (WitSqlStatementCreateTable)stmt;
        Assert.That(create.Constraints, Is.Not.Null);
        Assert.That(create.Constraints!.Any(c => c is TableConstraintPrimaryKey), Is.True);
        Assert.That(create.Constraints!.Any(c => c is TableConstraintForeignKey), Is.True);
        Assert.That(create.Constraints!.Any(c => c is TableConstraintUnique), Is.True);
    }

    [Test]
    public void ParseCreateTableWithMultiColumnForeignKeyTest()
    {
        var stmt = WitSql.ParseStatement(@"
            CREATE TABLE OrderDetails (
                OrderId INT,
                LineNumber INT,
                ProductId INT,
                FOREIGN KEY (OrderId, LineNumber) REFERENCES OrderLines(OrderId, LineNo) ON DELETE CASCADE
            )");
        var create = (WitSqlStatementCreateTable)stmt;
        var fk = create.Constraints!.OfType<TableConstraintForeignKey>().First();
        Assert.That(fk.Columns, Has.Count.EqualTo(2));
        Assert.That(fk.ForeignColumns, Has.Count.EqualTo(2));
    }

    #endregion

    #region DROP/ALTER TABLE

    [Test]
    public void ParseDropTableTest()
    {
        var stmt = WitSql.ParseStatement("DROP TABLE Users");
        var drop = (WitSqlStatementDropTable)stmt;
        Assert.That(drop.TableName, Is.EqualTo("Users"));
        Assert.That(drop.IfExists, Is.False);
    }

    [Test]
    public void ParseDropTableIfExistsTest()
    {
        var stmt = WitSql.ParseStatement("DROP TABLE IF EXISTS TempData");
        var drop = (WitSqlStatementDropTable)stmt;
        Assert.That(drop.IfExists, Is.True);
    }

    [Test]
    public void ParseAlterTableAddColumnTest()
    {
        var stmt = WitSql.ParseStatement("ALTER TABLE Users ADD COLUMN Age INT");
        var alter = (WitSqlStatementAlterTable)stmt;
        Assert.That(alter.Action, Is.InstanceOf<AlterActionAddColumn>());
    }

    [Test]
    public void ParseAlterTableDropColumnTest()
    {
        var stmt = WitSql.ParseStatement("ALTER TABLE Users DROP COLUMN Age");
        var alter = (WitSqlStatementAlterTable)stmt;
        Assert.That(alter.Action, Is.InstanceOf<AlterActionDropColumn>());
    }

    [Test]
    public void ParseAlterTableRenameTest()
    {
        var stmt = WitSql.ParseStatement("ALTER TABLE Users RENAME TO Accounts");
        var alter = (WitSqlStatementAlterTable)stmt;
        Assert.That(alter.Action, Is.InstanceOf<AlterActionRenameTable>());
    }

    [Test]
    public void ParseAlterTableRenameColumnTest()
    {
        var stmt = WitSql.ParseStatement("ALTER TABLE Users RENAME COLUMN Username TO Login");
        var alter = (WitSqlStatementAlterTable)stmt;
        Assert.That(alter.Action, Is.InstanceOf<AlterActionRenameColumn>());
    }

    [Test]
    public void ParseAlterTableAlterColumnTypeTest()
    {
        var stmt = WitSql.ParseStatement("ALTER TABLE Users ALTER COLUMN Age TYPE BIGINT");
        var alter = (WitSqlStatementAlterTable)stmt;
        Assert.That(alter.Action, Is.InstanceOf<AlterActionAlterColumn>());
        var alterCol = (AlterActionAlterColumn)alter.Action;
        Assert.That(alterCol.ColumnName, Is.EqualTo("Age"));
        Assert.That(alterCol.NewType, Is.Not.Null);
        Assert.That(alterCol.NewType!.TypeName, Is.EqualTo("BIGINT"));
    }

    [Test]
    public void ParseAlterTableAlterColumnSetDefaultTest()
    {
        var stmt = WitSql.ParseStatement("ALTER TABLE Users ALTER COLUMN Status SET DEFAULT 'active'");
        var alter = (WitSqlStatementAlterTable)stmt;
        var alterCol = (AlterActionAlterColumn)alter.Action;
        Assert.That(alterCol.NewDefault, Is.Not.Null);
    }

    [Test]
    public void ParseAlterTableAlterColumnDropDefaultTest()
    {
        var stmt = WitSql.ParseStatement("ALTER TABLE Users ALTER COLUMN Status DROP DEFAULT");
        var alter = (WitSqlStatementAlterTable)stmt;
        var alterCol = (AlterActionAlterColumn)alter.Action;
        Assert.That(alterCol.DropDefault, Is.True);
    }

    [Test]
    public void ParseAlterTableAlterColumnSetNotNullTest()
    {
        var stmt = WitSql.ParseStatement("ALTER TABLE Users ALTER COLUMN Email SET NOT NULL");
        var alter = (WitSqlStatementAlterTable)stmt;
        var alterCol = (AlterActionAlterColumn)alter.Action;
        Assert.That(alterCol.SetNotNull, Is.True);
    }

    [Test]
    public void ParseAlterTableAlterColumnDropNotNullTest()
    {
        var stmt = WitSql.ParseStatement("ALTER TABLE Users ALTER COLUMN Nickname DROP NOT NULL");
        var alter = (WitSqlStatementAlterTable)stmt;
        var alterCol = (AlterActionAlterColumn)alter.Action;
        Assert.That(alterCol.SetNotNull, Is.False);
    }

    #endregion

    #region Named Constraints

    [Test]
    public void ParseNamedPrimaryKeyConstraintTest()
    {
        var stmt = WitSql.ParseStatement(@"
            CREATE TABLE Users (
                Id INT,
                CONSTRAINT PK_Users PRIMARY KEY (Id)
            )");
        var create = (WitSqlStatementCreateTable)stmt;
        var pk = create.Constraints!.OfType<TableConstraintPrimaryKey>().First();
        Assert.That(pk.Name, Is.EqualTo("PK_Users"));
        Assert.That(pk.Columns, Has.Count.EqualTo(1));
    }

    [Test]
    public void ParseNamedUniqueConstraintTest()
    {
        var stmt = WitSql.ParseStatement(@"
            CREATE TABLE Users (
                Id INT,
                Email VARCHAR(100),
                CONSTRAINT UQ_Users_Email UNIQUE (Email)
            )");
        var create = (WitSqlStatementCreateTable)stmt;
        var uniq = create.Constraints!.OfType<TableConstraintUnique>().First();
        Assert.That(uniq.Name, Is.EqualTo("UQ_Users_Email"));
    }

    [Test]
    public void ParseNamedForeignKeyConstraintTest()
    {
        var stmt = WitSql.ParseStatement(@"
            CREATE TABLE Orders (
                Id INT,
                UserId INT,
                CONSTRAINT FK_Orders_Users FOREIGN KEY (UserId) REFERENCES Users(Id)
            )");
        var create = (WitSqlStatementCreateTable)stmt;
        var fk = create.Constraints!.OfType<TableConstraintForeignKey>().First();
        Assert.That(fk.Name, Is.EqualTo("FK_Orders_Users"));
        Assert.That(fk.ForeignTable, Is.EqualTo("Users"));
    }

    [Test]
    public void ParseNamedCheckConstraintTest()
    {
        var stmt = WitSql.ParseStatement(@"
            CREATE TABLE Products (
                Id INT,
                Price DECIMAL,
                CONSTRAINT CK_Products_Price CHECK (Price >= 0)
            )");
        var create = (WitSqlStatementCreateTable)stmt;
        var check = create.Constraints!.OfType<TableConstraintCheck>().First();
        Assert.That(check.Name, Is.EqualTo("CK_Products_Price"));
    }

    [Test]
    public void ParseUnnamedConstraintStillWorksTest()
    {
        var stmt = WitSql.ParseStatement(@"
            CREATE TABLE Users (
                Id INT,
                PRIMARY KEY (Id)
            )");
        var create = (WitSqlStatementCreateTable)stmt;
        var pk = create.Constraints!.OfType<TableConstraintPrimaryKey>().First();
        Assert.That(pk.Name, Is.Null);
    }

    [Test]
    public void ParseAlterTableAddConstraintTest()
    {
        var stmt = WitSql.ParseStatement(
            "ALTER TABLE Orders ADD CONSTRAINT FK_Orders_Users FOREIGN KEY (UserId) REFERENCES Users(Id)");
        var alter = (WitSqlStatementAlterTable)stmt;
        Assert.That(alter.Action, Is.InstanceOf<AlterActionAddConstraint>());
        var addCons = (AlterActionAddConstraint)alter.Action;
        Assert.That(addCons.Constraint, Is.InstanceOf<TableConstraintForeignKey>());
        var fk = (TableConstraintForeignKey)addCons.Constraint!;
        Assert.That(fk.Name, Is.EqualTo("FK_Orders_Users"));
    }

    [Test]
    public void ParseAlterTableAddConstraintWithoutNameTest()
    {
        var stmt = WitSql.ParseStatement(
            "ALTER TABLE Orders ADD UNIQUE (OrderNumber)");
        var alter = (WitSqlStatementAlterTable)stmt;
        Assert.That(alter.Action, Is.InstanceOf<AlterActionAddConstraint>());
        var addCons = (AlterActionAddConstraint)alter.Action;
        Assert.That(addCons.Constraint, Is.InstanceOf<TableConstraintUnique>());
    }

    [Test]
    public void ParseAlterTableDropConstraintTest()
    {
        var stmt = WitSql.ParseStatement("ALTER TABLE Orders DROP CONSTRAINT FK_Orders_Users");
        var alter = (WitSqlStatementAlterTable)stmt;
        Assert.That(alter.Action, Is.InstanceOf<AlterActionDropConstraint>());
        var dropCons = (AlterActionDropConstraint)alter.Action;
        Assert.That(dropCons.ConstraintName, Is.EqualTo("FK_Orders_Users"));
    }

    #endregion

    #region INDEX

    [Test]
    public void ParseCreateIndexTest()
    {
        var stmt = WitSql.ParseStatement("CREATE INDEX IX_Users_Email ON Users (Email)");
        var create = (WitSqlStatementCreateIndex)stmt;
        Assert.That(create.IndexName, Is.EqualTo("IX_Users_Email"));
        Assert.That(create.TableName, Is.EqualTo("Users"));
        Assert.That(create.IsUnique, Is.False);
    }

    [Test]
    public void ParseCreateUniqueIndexTest()
    {
        var stmt = WitSql.ParseStatement("CREATE UNIQUE INDEX IX_Users_Username ON Users (Username)");
        var create = (WitSqlStatementCreateIndex)stmt;
        Assert.That(create.IsUnique, Is.True);
    }

    [Test]
    public void ParseCreateIndexIfNotExistsTest()
    {
        var stmt = WitSql.ParseStatement("CREATE INDEX IF NOT EXISTS IX_Users_Email ON Users (Email)");
        var create = (WitSqlStatementCreateIndex)stmt;
        Assert.That(create.IfNotExists, Is.True);
    }

    [Test]
    public void ParseCreateIndexMultiColumnTest()
    {
        var stmt = WitSql.ParseStatement("CREATE INDEX IX_Orders ON Orders (UserId, OrderDate DESC)");
        var create = (WitSqlStatementCreateIndex)stmt;
        Assert.That(create.Elements, Has.Count.EqualTo(2));
        Assert.That(create.Elements[1].Descending, Is.True);
    }

    [Test]
    public void ParseDropIndexTest()
    {
        var stmt = WitSql.ParseStatement("DROP INDEX IF EXISTS IX_Users_Email");
        var drop = (WitSqlStatementDropIndex)stmt;
        Assert.That(drop.IndexName, Is.EqualTo("IX_Users_Email"));
        Assert.That(drop.IfExists, Is.True);
    }

    #endregion

    #region Advanced Index Features

    [Test]
    public void ParsePartialIndexTest()
    {
        var stmt = WitSql.ParseStatement(
            "CREATE INDEX IX_ActiveUsers ON Users (Email) WHERE IsActive = TRUE");
        var create = (WitSqlStatementCreateIndex)stmt;
        Assert.That(create.WhereClause, Is.Not.Null);
    }

    [Test]
    public void ParseExpressionIndexTest()
    {
        var stmt = WitSql.ParseStatement(
            "CREATE INDEX IX_Users_LowerEmail ON Users ((LOWER(Email)))");
        var create = (WitSqlStatementCreateIndex)stmt;
        Assert.That(create.Elements, Has.Count.EqualTo(1));
        Assert.That(create.Elements[0].IsExpression, Is.True);
        Assert.That(create.Elements[0].Expression, Is.Not.Null);
    }

    [Test]
    public void ParseCoveringIndexTest()
    {
        var stmt = WitSql.ParseStatement(
            "CREATE INDEX IX_Orders ON Orders (UserId) INCLUDE (OrderDate, Total)");
        var create = (WitSqlStatementCreateIndex)stmt;
        Assert.That(create.IncludeColumns, Has.Count.EqualTo(2));
        Assert.That(create.IncludeColumns![0], Is.EqualTo("OrderDate"));
        Assert.That(create.IncludeColumns![1], Is.EqualTo("Total"));
    }

    [Test]
    public void ParseFullAdvancedIndexTest()
    {
        var stmt = WitSql.ParseStatement(
            "CREATE UNIQUE INDEX IX_ActiveEmails ON Users ((LOWER(Email))) INCLUDE (Name) WHERE IsActive = TRUE");
        var create = (WitSqlStatementCreateIndex)stmt;
        Assert.That(create.IsUnique, Is.True);
        Assert.That(create.Elements[0].IsExpression, Is.True);
        Assert.That(create.IncludeColumns, Has.Count.EqualTo(1));
        Assert.That(create.WhereClause, Is.Not.Null);
    }

    [Test]
    public void ParseMixedColumnAndExpressionIndexTest()
    {
        var stmt = WitSql.ParseStatement(
            "CREATE INDEX IX_Mixed ON Users (LastName, (LOWER(FirstName)) DESC)");
        var create = (WitSqlStatementCreateIndex)stmt;
        Assert.That(create.Elements, Has.Count.EqualTo(2));
        Assert.That(create.Elements[0].ColumnName, Is.EqualTo("LastName"));
        Assert.That(create.Elements[0].IsExpression, Is.False);
        Assert.That(create.Elements[1].IsExpression, Is.True);
        Assert.That(create.Elements[1].Descending, Is.True);
    }

    #endregion

    #region VIEW

    [Test]
    public void ParseCreateViewTest()
    {
        var stmt = WitSql.ParseStatement("CREATE VIEW ActiveUsers AS SELECT * FROM Users WHERE IsActive = TRUE");
        Assert.That(stmt, Is.InstanceOf<WitSqlStatementCreateView>());
        var create = (WitSqlStatementCreateView)stmt;
        Assert.That(create.ViewName, Is.EqualTo("ActiveUsers"));
        Assert.That(create.IfNotExists, Is.False);
    }

    [Test]
    public void ParseCreateViewIfNotExistsTest()
    {
        var stmt = WitSql.ParseStatement("CREATE VIEW IF NOT EXISTS V AS SELECT 1");
        var create = (WitSqlStatementCreateView)stmt;
        Assert.That(create.IfNotExists, Is.True);
    }

    [Test]
    public void ParseCreateViewWithColumnListTest()
    {
        var stmt = WitSql.ParseStatement(
            "CREATE VIEW UserSummary (UserId, UserName, OrderCount) AS SELECT Id, Name, COUNT(*) FROM Users");
        var create = (WitSqlStatementCreateView)stmt;
        Assert.That(create.ColumnNames, Has.Count.EqualTo(3));
        Assert.That(create.ColumnNames![0], Is.EqualTo("UserId"));
    }

    [Test]
    public void ParseDropViewTest()
    {
        var stmt = WitSql.ParseStatement("DROP VIEW IF EXISTS ActiveUsers");
        Assert.That(stmt, Is.InstanceOf<WitSqlStatementDropView>());
        var drop = (WitSqlStatementDropView)stmt;
        Assert.That(drop.ViewName, Is.EqualTo("ActiveUsers"));
        Assert.That(drop.IfExists, Is.True);
    }

    #endregion

    #region TRIGGER

    [Test]
    public void ParseCreateTriggerTest()
    {
        var stmt = WitSql.ParseStatement(@"
            CREATE TRIGGER UpdateTimestamp
            BEFORE UPDATE ON Users
            FOR EACH ROW
            BEGIN
                UPDATE Users SET UpdatedAt = NOW() WHERE Id = 1;
            END");
        Assert.That(stmt, Is.InstanceOf<WitSqlStatementCreateTrigger>());
        var create = (WitSqlStatementCreateTrigger)stmt;
        Assert.That(create.TriggerName, Is.EqualTo("UpdateTimestamp"));
        Assert.That(create.Time, Is.EqualTo(TriggerTimingType.Before));
        Assert.That(create.Event, Is.EqualTo(TriggerEventType.Update));
        Assert.That(create.TableName, Is.EqualTo("Users"));
        Assert.That(create.ForEachRow, Is.True);
    }

    [Test]
    public void ParseCreateTriggerIfNotExistsTest()
    {
        var stmt = WitSql.ParseStatement(@"
            CREATE TRIGGER IF NOT EXISTS AuditLog
            AFTER INSERT ON Orders
            BEGIN
                INSERT INTO AuditLog (ActionType) VALUES ('INSERT');
            END");
        var create = (WitSqlStatementCreateTrigger)stmt;
        Assert.That(create.IfNotExists, Is.True);
        Assert.That(create.Time, Is.EqualTo(TriggerTimingType.After));
        Assert.That(create.Event, Is.EqualTo(TriggerEventType.Insert));
    }

    [Test]
    public void ParseCreateTriggerWithWhenConditionTest()
    {
        var stmt = WitSql.ParseStatement(@"
            CREATE TRIGGER PreventNegativeBalance
            BEFORE UPDATE ON Accounts
            FOR EACH ROW
            WHEN (1 = 1)
            BEGIN
                SELECT 1;
            END");
        var create = (WitSqlStatementCreateTrigger)stmt;
        Assert.That(create.WhenCondition, Is.Not.Null);
    }

    [Test]
    public void ParseCreateTriggerInsteadOfTest()
    {
        var stmt = WitSql.ParseStatement(@"
            CREATE TRIGGER InsertIntoView
            INSTEAD OF INSERT ON ActiveUsersView
            BEGIN
                INSERT INTO Users (Name) VALUES ('test');
            END");
        var create = (WitSqlStatementCreateTrigger)stmt;
        Assert.That(create.Time, Is.EqualTo(TriggerTimingType.InsteadOf));
    }

    [Test]
    public void ParseCreateTriggerDeleteTest()
    {
        var stmt = WitSql.ParseStatement(@"
            CREATE TRIGGER AuditDelete
            AFTER DELETE ON Users
            BEGIN
                INSERT INTO DeleteLog (DeletedAt) VALUES (NOW());
            END");
        var create = (WitSqlStatementCreateTrigger)stmt;
        Assert.That(create.Event, Is.EqualTo(TriggerEventType.Delete));
    }

    [Test]
    public void ParseCreateTriggerUpdateOfColumnsTest()
    {
        var stmt = WitSql.ParseStatement(@"
            CREATE TRIGGER TrackPriceChange
            AFTER UPDATE OF Price, Quantity ON Products
            BEGIN
                INSERT INTO PriceHistory (ProductId) VALUES (1);
            END");
        var create = (WitSqlStatementCreateTrigger)stmt;
        Assert.That(create.Event, Is.EqualTo(TriggerEventType.Update));
        Assert.That(create.UpdateColumns, Has.Count.EqualTo(2));
    }

    [Test]
    public void ParseDropTriggerTest()
    {
        var stmt = WitSql.ParseStatement("DROP TRIGGER IF EXISTS UpdateTimestamp");
        Assert.That(stmt, Is.InstanceOf<WitSqlStatementDropTrigger>());
        var drop = (WitSqlStatementDropTrigger)stmt;
        Assert.That(drop.TriggerName, Is.EqualTo("UpdateTimestamp"));
        Assert.That(drop.IfExists, Is.True);
    }

    #endregion

    #region TRUNCATE TABLE

    [Test]
    public void ParseTruncateTableTest()
    {
        var stmt = WitSql.ParseStatement("TRUNCATE TABLE Users");
        Assert.That(stmt, Is.InstanceOf<WitSqlStatementTruncate>());
        var truncate = (WitSqlStatementTruncate)stmt;
        Assert.That(truncate.TableName, Is.EqualTo("Users"));
    }

    [Test]
    public void ParseTruncateTableCaseInsensitiveTest()
    {
        var stmt = WitSql.ParseStatement("truncate table Orders");
        Assert.That(stmt, Is.InstanceOf<WitSqlStatementTruncate>());
        var truncate = (WitSqlStatementTruncate)stmt;
        Assert.That(truncate.TableName, Is.EqualTo("Orders"));
    }

    #endregion

    #region Computed Columns

    [Test]
    public void ParseComputedColumnVirtualTest()
    {
        var stmt = WitSql.ParseStatement(@"
            CREATE TABLE Orders (
                Quantity INT,
                Price DECIMAL,
                Total AS (Quantity * Price)
            )");
        var create = (WitSqlStatementCreateTable)stmt;
        Assert.That(create.Columns, Has.Count.EqualTo(3));
        
        var totalCol = create.Columns[2];
        Assert.That(totalCol.Name, Is.EqualTo("Total"));
        Assert.That(totalCol.IsComputed, Is.True);
        Assert.That(totalCol.ComputedExpression, Is.Not.Null);
        Assert.That(totalCol.ComputedType, Is.EqualTo(ComputedColumnType.Virtual));
        Assert.That(totalCol.DataType, Is.Null);
    }

    [Test]
    public void ParseComputedColumnStoredTest()
    {
        var stmt = WitSql.ParseStatement(@"
            CREATE TABLE Users (
                FirstName VARCHAR(50),
                LastName VARCHAR(50),
                FullName AS (FirstName || ' ' || LastName) STORED
            )");
        var create = (WitSqlStatementCreateTable)stmt;
        
        var fullNameCol = create.Columns[2];
        Assert.That(fullNameCol.Name, Is.EqualTo("FullName"));
        Assert.That(fullNameCol.IsComputed, Is.True);
        Assert.That(fullNameCol.ComputedType, Is.EqualTo(ComputedColumnType.Stored));
    }

    [Test]
    public void ParseComputedColumnExplicitVirtualTest()
    {
        var stmt = WitSql.ParseStatement(@"
            CREATE TABLE Products (
                Price DECIMAL,
                Tax AS (Price * 0.1) VIRTUAL
            )");
        var create = (WitSqlStatementCreateTable)stmt;
        
        var taxCol = create.Columns[1];
        Assert.That(taxCol.ComputedType, Is.EqualTo(ComputedColumnType.Virtual));
    }

    [Test]
    public void ParseComputedColumnWithFunctionTest()
    {
        var stmt = WitSql.ParseStatement(@"
            CREATE TABLE Users (
                Email VARCHAR(100),
                EmailLower AS (LOWER(Email)) STORED
            )");
        var create = (WitSqlStatementCreateTable)stmt;
        
        var emailLowerCol = create.Columns[1];
        Assert.That(emailLowerCol.IsComputed, Is.True);
        Assert.That(emailLowerCol.ComputedType, Is.EqualTo(ComputedColumnType.Stored));
    }

    [Test]
    public void ParseMixedColumnsTest()
    {
        var stmt = WitSql.ParseStatement(@"
            CREATE TABLE Items (
                Id INT PRIMARY KEY,
                Name VARCHAR(100) NOT NULL,
                Qty INT DEFAULT 0,
                Price DECIMAL,
                Total AS (Qty * Price) STORED
            )");
        var create = (WitSqlStatementCreateTable)stmt;
        Assert.That(create.Columns, Has.Count.EqualTo(5));
        
        // Regular columns
        Assert.That(create.Columns[0].IsComputed, Is.False);
        Assert.That(create.Columns[1].IsComputed, Is.False);
        Assert.That(create.Columns[2].IsComputed, Is.False);
        Assert.That(create.Columns[3].IsComputed, Is.False);
        
        // Computed column
        Assert.That(create.Columns[4].IsComputed, Is.True);
    }

    #endregion

    #region SIGNAL SQLSTATE

    [Test]
    public void ParseSignalStatementTest()
    {
        var stmt = WitSql.ParseStatement("SIGNAL SQLSTATE '45000'");
        Assert.That(stmt, Is.InstanceOf<WitSqlStatementSignal>());
        var signal = (WitSqlStatementSignal)stmt;
        Assert.That(signal.SqlState, Is.EqualTo("45000"));
        Assert.That(signal.MessageText, Is.Null);
    }

    [Test]
    public void ParseSignalStatementWithMessageTest()
    {
        var stmt = WitSql.ParseStatement("SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Balance cannot be negative'");
        Assert.That(stmt, Is.InstanceOf<WitSqlStatementSignal>());
        var signal = (WitSqlStatementSignal)stmt;
        Assert.That(signal.SqlState, Is.EqualTo("45000"));
        Assert.That(signal.MessageText, Is.Not.Null);
    }

    [Test]
    public void ParseSignalStatementWithExpressionMessageTest()
    {
        var stmt = WitSql.ParseStatement("SIGNAL SQLSTATE '22003' SET MESSAGE_TEXT = 'Invalid value: ' || TOSTRING(@value)");
        Assert.That(stmt, Is.InstanceOf<WitSqlStatementSignal>());
        var signal = (WitSqlStatementSignal)stmt;
        Assert.That(signal.SqlState, Is.EqualTo("22003"));
        Assert.That(signal.MessageText, Is.Not.Null);
    }

    [Test]
    public void ParseSignalStatementCaseInsensitiveTest()
    {
        var stmt = WitSql.ParseStatement("signal sqlstate '45000' set message_text = 'Error'");
        Assert.That(stmt, Is.InstanceOf<WitSqlStatementSignal>());
        var signal = (WitSqlStatementSignal)stmt;
        Assert.That(signal.SqlState, Is.EqualTo("45000"));
    }

    [Test]
    public void ParseTriggerWithSignalStatementTest()
    {
        var stmt = WitSql.ParseStatement(@"
            CREATE TRIGGER PreventNegativeBalance
            BEFORE UPDATE ON Accounts
            FOR EACH ROW
            BEGIN
                SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Balance cannot be negative';
            END");
        Assert.That(stmt, Is.InstanceOf<WitSqlStatementCreateTrigger>());
        var create = (WitSqlStatementCreateTrigger)stmt;
        Assert.That(create.Body, Has.Count.EqualTo(1));
        Assert.That(create.Body[0], Is.InstanceOf<WitSqlStatementSignal>());
        var signal = (WitSqlStatementSignal)create.Body[0];
        Assert.That(signal.SqlState, Is.EqualTo("45000"));
    }

    #endregion

    #region Data Types - Other

    [Test]
    public void ParseGuidTypeTest()
    {
        var stmt = WitSql.ParseStatement("CREATE TABLE T (Id GUID PRIMARY KEY)");
        Assert.That(stmt, Is.InstanceOf<WitSqlStatementCreateTable>());
    }

    [Test]
    public void ParseUniqueIdentifierTypeTest()
    {
        var stmt = WitSql.ParseStatement("CREATE TABLE T (Id UNIQUEIDENTIFIER PRIMARY KEY)");
        Assert.That(stmt, Is.InstanceOf<WitSqlStatementCreateTable>());
    }

    [Test]
    public void ParseBooleanTypeTest()
    {
        var stmt = WitSql.ParseStatement("CREATE TABLE T (Flag BOOLEAN DEFAULT TRUE)");
        Assert.That(stmt, Is.InstanceOf<WitSqlStatementCreateTable>());
    }

    [Test]
    public void ParseBinaryTypesTest()
    {
        var types = new[] { "BLOB", "BINARY(16)", "VARBINARY(1024)" };
        foreach (var type in types)
        {
            var stmt = WitSql.ParseStatement($"CREATE TABLE T (Data {type})");
            Assert.That(stmt, Is.InstanceOf<WitSqlStatementCreateTable>());
        }
    }

    [Test]
    public void ParseRowVersionTypeTest()
    {
        var stmt = WitSql.ParseStatement("CREATE TABLE T (RowVer ROWVERSION NOT NULL)");
        Assert.That(stmt, Is.InstanceOf<WitSqlStatementCreateTable>());
        var create = (WitSqlStatementCreateTable)stmt;
        Assert.That(create.Columns[0].DataType.TypeName, Is.EqualTo("ROWVERSION"));
    }

    [Test]
    public void ParseJsonTypeTest()
    {
        var stmt = WitSql.ParseStatement("CREATE TABLE T (Data JSON)");
        Assert.That(stmt, Is.InstanceOf<WitSqlStatementCreateTable>());
        var create = (WitSqlStatementCreateTable)stmt;
        Assert.That(create.Columns[0].DataType.TypeName, Is.EqualTo("JSON"));
    }

    [Test]
    public void ParseJsonbTypeTest()
    {
        var stmt = WitSql.ParseStatement("CREATE TABLE T (Data JSONB)");
        Assert.That(stmt, Is.InstanceOf<WitSqlStatementCreateTable>());
        var create = (WitSqlStatementCreateTable)stmt;
        Assert.That(create.Columns[0].DataType.TypeName, Is.EqualTo("JSONB"));
    }

    #endregion
}

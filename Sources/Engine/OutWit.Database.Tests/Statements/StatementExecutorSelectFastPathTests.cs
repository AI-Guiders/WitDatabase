using NSubstitute;
using OutWit.Database.Definitions;
using OutWit.Database.Parser;
using OutWit.Database.Sql;
using OutWit.Database.Statements;
using OutWit.Database.Types;
using OutWit.Database.Values;

namespace OutWit.Database.Tests.Statements;

/// <summary>
/// Tests for SELECT statement fast path optimization.
/// </summary>
[TestFixture]
public sealed class StatementExecutorSelectFastPathTests : StatementExecutorTestsBase
{
    #region Single Row Fast Path Tests

    [Test]
    public void SelectByPkUsesGetRowByIdTest()
    {
        // Arrange
        var table = CreateUsersTable();
        m_database.GetTable("Users").Returns(table);
        
        var userRow = CreateUserRow(1, "Alice", "alice@test.com");
        m_database.GetRowById("Users", 1).Returns(userRow);

        var executor = new StatementExecutor(m_context);
        var stmt = WitSql.ParseStatement("SELECT * FROM Users WHERE Id = 1");

        // Act
        var result = executor.Execute(stmt);

        // Assert
        Assert.That(result.HasRows, Is.True);
        
        var rows = result.ReadAll();
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0]["Name"].AsString(), Is.EqualTo("Alice"));
        
        // Verify GetRowById was called (fast path used)
        m_database.Received(1).GetRowById("Users", 1);
        
        // Verify no table scan was created
        m_database.DidNotReceive().CreateTableScan("Users");
    }

    [Test]
    public void SelectByPkWithParameterUsesGetRowByIdTest()
    {
        // Arrange
        var table = CreateUsersTable();
        m_database.GetTable("Users").Returns(table);
        
        var userRow = CreateUserRow(42, "Bob", "bob@test.com");
        m_database.GetRowById("Users", 42).Returns(userRow);

        var executor = new StatementExecutor(m_context);

        // Set parameter
        m_context.Parameters["@id"] = WitSqlValue.FromInt(42);
        var stmt = WitSql.ParseStatement("SELECT * FROM Users WHERE Id = @id");

        // Act
        var result = executor.Execute(stmt);

        // Assert
        Assert.That(result.HasRows, Is.True);
        
        var rows = result.ReadAll();
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0]["Name"].AsString(), Is.EqualTo("Bob"));
        
        m_database.Received(1).GetRowById("Users", 42);
    }

    [Test]
    public void SelectByPkReturnsEmptyWhenRowNotFoundTest()
    {
        // Arrange
        var table = CreateUsersTable();
        m_database.GetTable("Users").Returns(table);
        m_database.GetRowById("Users", 999).Returns((WitSqlRow?)null);

        var executor = new StatementExecutor(m_context);
        var stmt = WitSql.ParseStatement("SELECT * FROM Users WHERE Id = 999");

        // Act
        var result = executor.Execute(stmt);

        // Assert
        Assert.That(result.HasRows, Is.True);
        
        var rows = result.ReadAll();
        Assert.That(rows, Has.Count.EqualTo(0));
        
        m_database.Received(1).GetRowById("Users", 999);
    }

    [Test]
    public void SelectByPkWithNullParameterReturnsEmptyTest()
    {
        // Arrange
        var table = CreateUsersTable();
        m_database.GetTable("Users").Returns(table);

        var executor = new StatementExecutor(m_context);
        m_context.Parameters["@id"] = WitSqlValue.Null;
        var stmt = WitSql.ParseStatement("SELECT * FROM Users WHERE Id = @id");

        // Act
        var result = executor.Execute(stmt);

        // Assert
        var rows = result.ReadAll();
        Assert.That(rows, Has.Count.EqualTo(0));
        
        // GetRowById should not be called for NULL PK
        m_database.DidNotReceive().GetRowById(Arg.Any<string>(), Arg.Any<long>());
    }

    [Test]
    public void SelectSpecificColumnsByPkTest()
    {
        // Arrange
        var table = CreateUsersTable();
        m_database.GetTable("Users").Returns(table);
        
        var userRow = CreateUserRow(1, "Alice", "alice@test.com");
        m_database.GetRowById("Users", 1).Returns(userRow);

        var executor = new StatementExecutor(m_context);
        var stmt = WitSql.ParseStatement("SELECT Name, Email FROM Users WHERE Id = 1");

        // Act
        var result = executor.Execute(stmt);

        // Assert
        var rows = result.ReadAll();
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0].ColumnNames, Has.Count.EqualTo(2));
        Assert.That(rows[0].ColumnNames, Does.Contain("Name"));
        Assert.That(rows[0].ColumnNames, Does.Contain("Email"));
        Assert.That(rows[0]["Name"].AsString(), Is.EqualTo("Alice"));
    }

    #endregion

    #region Batch Fast Path Tests (IN clause)

    [Test]
    public void SelectByPkInClauseUsesGetRowByIdForEachTest()
    {
        // Arrange
        var table = CreateUsersTable();
        m_database.GetTable("Users").Returns(table);
        
        var row1 = CreateUserRow(1, "Alice", "alice@test.com");
        var row2 = CreateUserRow(2, "Bob", "bob@test.com");
        var row3 = CreateUserRow(3, "Carol", "carol@test.com");
        
        m_database.GetRowById("Users", 1).Returns(row1);
        m_database.GetRowById("Users", 2).Returns(row2);
        m_database.GetRowById("Users", 3).Returns(row3);

        var executor = new StatementExecutor(m_context);
        var stmt = WitSql.ParseStatement("SELECT * FROM Users WHERE Id IN (1, 2, 3)");

        // Act
        var result = executor.Execute(stmt);

        // Assert
        var rows = result.ReadAll();
        Assert.That(rows, Has.Count.EqualTo(3));
        
        // Verify GetRowById was called for each ID
        m_database.Received(1).GetRowById("Users", 1);
        m_database.Received(1).GetRowById("Users", 2);
        m_database.Received(1).GetRowById("Users", 3);
    }

    [Test]
    public void SelectByPkInClauseWithMissingRowsTest()
    {
        // Arrange
        var table = CreateUsersTable();
        m_database.GetTable("Users").Returns(table);
        
        var row1 = CreateUserRow(1, "Alice", "alice@test.com");
        m_database.GetRowById("Users", 1).Returns(row1);
        m_database.GetRowById("Users", 2).Returns((WitSqlRow?)null);
        m_database.GetRowById("Users", 3).Returns((WitSqlRow?)null);

        var executor = new StatementExecutor(m_context);
        var stmt = WitSql.ParseStatement("SELECT * FROM Users WHERE Id IN (1, 2, 3)");

        // Act
        var result = executor.Execute(stmt);

        // Assert
        var rows = result.ReadAll();
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0]["Name"].AsString(), Is.EqualTo("Alice"));
    }

    [Test]
    public void SelectByPkInClauseWithOrderByTest()
    {
        // Arrange
        var table = CreateUsersTable();
        m_database.GetTable("Users").Returns(table);
        
        var row1 = CreateUserRow(1, "Alice", "alice@test.com");
        var row2 = CreateUserRow(2, "Bob", "bob@test.com");
        var row3 = CreateUserRow(3, "Carol", "carol@test.com");
        
        m_database.GetRowById("Users", 1).Returns(row1);
        m_database.GetRowById("Users", 2).Returns(row2);
        m_database.GetRowById("Users", 3).Returns(row3);

        var executor = new StatementExecutor(m_context);
        var stmt = WitSql.ParseStatement("SELECT * FROM Users WHERE Id IN (1, 2, 3) ORDER BY Name DESC");

        // Act
        var result = executor.Execute(stmt);

        // Assert
        var rows = result.ReadAll();
        Assert.That(rows, Has.Count.EqualTo(3));
        Assert.That(rows[0]["Name"].AsString(), Is.EqualTo("Carol"));
        Assert.That(rows[1]["Name"].AsString(), Is.EqualTo("Bob"));
        Assert.That(rows[2]["Name"].AsString(), Is.EqualTo("Alice"));
    }

    [Test]
    public void SelectByPkInClauseWithLimitTest()
    {
        // Arrange
        var table = CreateUsersTable();
        m_database.GetTable("Users").Returns(table);
        
        var row1 = CreateUserRow(1, "Alice", "alice@test.com");
        var row2 = CreateUserRow(2, "Bob", "bob@test.com");
        var row3 = CreateUserRow(3, "Carol", "carol@test.com");
        
        m_database.GetRowById("Users", 1).Returns(row1);
        m_database.GetRowById("Users", 2).Returns(row2);
        m_database.GetRowById("Users", 3).Returns(row3);

        var executor = new StatementExecutor(m_context);
        var stmt = WitSql.ParseStatement("SELECT * FROM Users WHERE Id IN (1, 2, 3) LIMIT 2");

        // Act
        var result = executor.Execute(stmt);

        // Assert
        var rows = result.ReadAll();
        Assert.That(rows, Has.Count.EqualTo(2));
    }

    [Test]
    public void SelectByPkInClauseWithOffsetTest()
    {
        // Arrange
        var table = CreateUsersTable();
        m_database.GetTable("Users").Returns(table);
        
        var row1 = CreateUserRow(1, "Alice", "alice@test.com");
        var row2 = CreateUserRow(2, "Bob", "bob@test.com");
        var row3 = CreateUserRow(3, "Carol", "carol@test.com");
        
        m_database.GetRowById("Users", 1).Returns(row1);
        m_database.GetRowById("Users", 2).Returns(row2);
        m_database.GetRowById("Users", 3).Returns(row3);

        var executor = new StatementExecutor(m_context);
        var stmt = WitSql.ParseStatement("SELECT * FROM Users WHERE Id IN (1, 2, 3) LIMIT 2 OFFSET 1");

        // Act
        var result = executor.Execute(stmt);

        // Assert
        var rows = result.ReadAll();
        Assert.That(rows, Has.Count.EqualTo(2));
    }

    #endregion

    #region Fast Path Not Applicable Tests

    [Test]
    public void SelectWithJoinDoesNotUseFastPathTest()
    {
        // Arrange
        var usersTable = CreateUsersTable();
        var ordersTable = CreateOrdersTableWithFK();
        
        m_database.GetTable("Users").Returns(usersTable);
        m_database.GetTable("Orders").Returns(ordersTable);
        
        // Setup table scan for fallback
        var userRows = new[] { CreateUserRow(1, "Alice", "alice@test.com") };
        m_database.CreateTableScan("Users").Returns(CreateMockIterator(userRows));
        m_database.CreateTableScan("Orders").Returns(CreateEmptyIterator());

        var executor = new StatementExecutor(m_context);
        var stmt = WitSql.ParseStatement("SELECT * FROM Users u JOIN Orders o ON u.Id = o.UserId WHERE u.Id = 1");

        // Act
        var result = executor.Execute(stmt);

        // Assert - should use table scan, not fast path
        m_database.Received().CreateTableScan("Users");
    }

    [Test]
    public void SelectWithGroupByDoesNotUseFastPathTest()
    {
        // Arrange
        var table = CreateUsersTable();
        m_database.GetTable("Users").Returns(table);
        
        var userRows = new[] 
        { 
            CreateUserRow(1, "Alice", "alice@test.com"),
            CreateUserRow(2, "Bob", "bob@test.com")
        };
        m_database.CreateTableScan("Users").Returns(CreateMockIterator(userRows));

        var executor = new StatementExecutor(m_context);
        var stmt = WitSql.ParseStatement("SELECT Name, COUNT(*) FROM Users WHERE Id = 1 GROUP BY Name");

        // Act
        var result = executor.Execute(stmt);

        // Assert - should use table scan, not fast path
        m_database.Received().CreateTableScan("Users");
    }

    [Test]
    public void SelectWithAggregateDoesNotUseFastPathTest()
    {
        // Arrange
        var table = CreateUsersTable();
        m_database.GetTable("Users").Returns(table);
        
        var userRows = new[] { CreateUserRow(1, "Alice", "alice@test.com") };
        m_database.CreateTableScan("Users").Returns(CreateMockIterator(userRows));

        var executor = new StatementExecutor(m_context);
        var stmt = WitSql.ParseStatement("SELECT COUNT(*) FROM Users WHERE Id = 1");

        // Act
        var result = executor.Execute(stmt);

        // Assert - should use table scan, not fast path (aggregate function)
        m_database.Received().CreateTableScan("Users");
    }

    [Test]
    public void SelectWithDistinctDoesNotUseFastPathTest()
    {
        // Arrange
        var table = CreateUsersTable();
        m_database.GetTable("Users").Returns(table);
        
        var userRows = new[] { CreateUserRow(1, "Alice", "alice@test.com") };
        m_database.CreateTableScan("Users").Returns(CreateMockIterator(userRows));

        var executor = new StatementExecutor(m_context);
        var stmt = WitSql.ParseStatement("SELECT DISTINCT Name FROM Users WHERE Id = 1");

        // Act
        var result = executor.Execute(stmt);

        // Assert - should use table scan, not fast path
        m_database.Received().CreateTableScan("Users");
    }

    [Test]
    public void SelectWithoutWhereDoesNotUseFastPathTest()
    {
        // Arrange
        var table = CreateUsersTable();
        m_database.GetTable("Users").Returns(table);
        
        var userRows = new[] 
        { 
            CreateUserRow(1, "Alice", "alice@test.com"),
            CreateUserRow(2, "Bob", "bob@test.com")
        };
        m_database.CreateTableScan("Users").Returns(CreateMockIterator(userRows));

        var executor = new StatementExecutor(m_context);
        var stmt = WitSql.ParseStatement("SELECT * FROM Users");

        // Act
        var result = executor.Execute(stmt);

        // Assert - should use table scan, not fast path
        m_database.Received().CreateTableScan("Users");
    }

    [Test]
    public void SelectWithNonPkWhereDoesNotUseFastPathTest()
    {
        // Arrange
        var table = CreateUsersTable();
        m_database.GetTable("Users").Returns(table);
        
        var userRows = new[] { CreateUserRow(1, "Alice", "alice@test.com") };
        m_database.CreateTableScan("Users").Returns(CreateMockIterator(userRows));

        var executor = new StatementExecutor(m_context);
        var stmt = WitSql.ParseStatement("SELECT * FROM Users WHERE Name = 'Alice'");

        // Act
        var result = executor.Execute(stmt);

        // Assert - should use table scan, not fast path (WHERE on non-PK column)
        m_database.Received().CreateTableScan("Users");
    }

    #endregion

    #region Non-Autoincrement PK Tests

    [Test]
    public void SelectWithNonAutoincrementPkDoesNotUseFastPathTest()
    {
        // Arrange - create table with PK that is NOT autoincrement
        var table = new DefinitionTable
        {
            Name = "Products",
            Columns =
            [
                new DefinitionColumn { Name = "Sku", Type = WitDataType.StringVariable, IsPrimaryKey = true, IsAutoIncrement = false, Ordinal = 0 },
                new DefinitionColumn { Name = "Name", Type = WitDataType.StringVariable, Ordinal = 1 }
            ],
            PrimaryKey = ["Sku"]
        };
        
        m_database.GetTable("Products").Returns(table);
        
        var productRow = CreateRow(
            ("_rowid", WitSqlValue.FromInt(1)),
            ("Sku", WitSqlValue.FromText("ABC123")),
            ("Name", WitSqlValue.FromText("Widget"))
        );
        m_database.CreateTableScan("Products").Returns(CreateMockIterator([productRow]));

        var executor = new StatementExecutor(m_context);
        var stmt = WitSql.ParseStatement("SELECT * FROM Products WHERE Sku = 'ABC123'");

        // Act
        var result = executor.Execute(stmt);

        // Assert - should NOT use fast path because PK is not autoincrement
        m_database.Received().CreateTableScan("Products");
        m_database.DidNotReceive().GetRowById(Arg.Any<string>(), Arg.Any<long>());
    }

    #endregion
}

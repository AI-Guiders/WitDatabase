using NSubstitute;
using OutWit.Database.Core.Concurrency;
using OutWit.Database.Core.Interfaces;
using OutWit.Database.Definitions;
using OutWit.Database.Parser;
using OutWit.Database.Parser.Schema.Types;
using OutWit.Database.Parser.Statements;
using OutWit.Database.Statements;
using OutWit.Database.Types;
using OutWit.Database.Values;

namespace OutWit.Database.Tests.Statements;

/// <summary>
/// Tests for SELECT with FOR UPDATE/FOR SHARE locking hints.
/// </summary>
[TestFixture]
public sealed class StatementExecutorLockingTests : StatementExecutorTestsBase
{
    #region Fields

    private StatementExecutor m_executor = null!;

    #endregion

    #region Setup

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        m_executor = new StatementExecutor(m_context);
    }

    #endregion

    #region FOR UPDATE Without Transaction Tests

    [Test]
    public void SelectForUpdateWithoutTransactionThrowsTest()
    {
        // Arrange
        var table = CreateUsersTable();
        m_database.GetTable("Users").Returns(table);
        m_database.CurrentTransaction.Returns((ITransaction?)null);

        var stmt = WitSql.ParseStatement("SELECT * FROM Users WHERE Id = 1 FOR UPDATE") as WitSqlStatementSelect;

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => m_executor.Execute(stmt!));
        Assert.That(ex.Message, Does.Contain("requires an active transaction"));
    }

    [Test]
    public void SelectForShareWithoutTransactionThrowsTest()
    {
        // Arrange
        var table = CreateUsersTable();
        m_database.GetTable("Users").Returns(table);
        m_database.CurrentTransaction.Returns((ITransaction?)null);

        var stmt = WitSql.ParseStatement("SELECT * FROM Users WHERE Id = 1 FOR SHARE") as WitSqlStatementSelect;

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => m_executor.Execute(stmt!));
        Assert.That(ex.Message, Does.Contain("requires an active transaction"));
    }

    #endregion

    #region FOR UPDATE With Non-MVCC Transaction Tests

    [Test]
    public void SelectForUpdateWithNonMvccTransactionThrowsTest()
    {
        // Arrange
        var table = CreateUsersTable();
        m_database.GetTable("Users").Returns(table);
        
        // Create a regular (non-MVCC) transaction mock
        var regularTransaction = Substitute.For<ITransaction>();
        m_database.CurrentTransaction.Returns(regularTransaction);
        m_database.CreateTableScan("Users").Returns(CreateMockIterator(
            CreateUserRow(1, "Alice", "alice@test.com")
        ));

        var stmt = WitSql.ParseStatement("SELECT * FROM Users WHERE Id = 1 FOR UPDATE") as WitSqlStatementSelect;

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => m_executor.Execute(stmt!));
        Assert.That(ex.Message, Does.Contain("MVCC transaction support"));
    }

    #endregion

    #region FOR UPDATE Clause Parsing Tests

    [Test]
    public void SelectWithForUpdateClauseIsParsedTest()
    {
        var stmt = WitSql.ParseStatement("SELECT * FROM Users FOR UPDATE") as WitSqlStatementSelect;
        
        Assert.That(stmt, Is.Not.Null);
        Assert.That(stmt!.ForClause, Is.Not.Null);
        Assert.That(stmt.ForClause!.LockingType, Is.EqualTo(LockingType.ForUpdate));
        Assert.That(stmt.ForClause.IsNoWait, Is.False);
        Assert.That(stmt.ForClause.IsSkipLocked, Is.False);
    }

    [Test]
    public void SelectWithForShareClauseIsParsedTest()
    {
        var stmt = WitSql.ParseStatement("SELECT * FROM Users FOR SHARE") as WitSqlStatementSelect;
        
        Assert.That(stmt, Is.Not.Null);
        Assert.That(stmt!.ForClause, Is.Not.Null);
        Assert.That(stmt.ForClause!.LockingType, Is.EqualTo(LockingType.ForShare));
    }

    [Test]
    public void SelectWithForUpdateNoWaitIsParsedTest()
    {
        var stmt = WitSql.ParseStatement("SELECT * FROM Users FOR UPDATE NOWAIT") as WitSqlStatementSelect;
        
        Assert.That(stmt, Is.Not.Null);
        Assert.That(stmt!.ForClause, Is.Not.Null);
        Assert.That(stmt.ForClause!.LockingType, Is.EqualTo(LockingType.ForUpdate));
        Assert.That(stmt.ForClause.IsNoWait, Is.True);
    }

    [Test]
    public void SelectWithForUpdateSkipLockedIsParsedTest()
    {
        var stmt = WitSql.ParseStatement("SELECT * FROM Users FOR UPDATE SKIP LOCKED") as WitSqlStatementSelect;
        
        Assert.That(stmt, Is.Not.Null);
        Assert.That(stmt!.ForClause, Is.Not.Null);
        Assert.That(stmt.ForClause!.LockingType, Is.EqualTo(LockingType.ForUpdate));
        Assert.That(stmt.ForClause.IsSkipLocked, Is.True);
    }

    [Test]
    public void SelectWithForShareNoWaitIsParsedTest()
    {
        var stmt = WitSql.ParseStatement("SELECT * FROM Users FOR SHARE NOWAIT") as WitSqlStatementSelect;
        
        Assert.That(stmt, Is.Not.Null);
        Assert.That(stmt!.ForClause, Is.Not.Null);
        Assert.That(stmt.ForClause!.LockingType, Is.EqualTo(LockingType.ForShare));
        Assert.That(stmt.ForClause.IsNoWait, Is.True);
    }

    #endregion

    #region FOR UPDATE Without FROM Tests

    [Test]
    public void SelectForUpdateWithoutFromThrowsTest()
    {
        // Arrange - SELECT 1 FOR UPDATE - no table to lock
        var mvccTransaction = Substitute.For<IMvccTransaction>();
        m_database.CurrentTransaction.Returns(mvccTransaction);

        var stmt = WitSql.ParseStatement("SELECT 1 FOR UPDATE") as WitSqlStatementSelect;

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => m_executor.Execute(stmt!));
        Assert.That(ex.Message, Does.Contain("requires a table"));
    }

    #endregion

    #region Normal SELECT Without FOR Clause Tests

    [Test]
    public void SelectWithoutForClauseDoesNotRequireTransactionTest()
    {
        // Arrange
        var table = CreateUsersTable();
        m_database.GetTable("Users").Returns(table);
        m_database.CurrentTransaction.Returns((ITransaction?)null);
        m_database.CreateTableScan("Users").Returns(CreateMockIterator(
            CreateUserRow(1, "Alice", "alice@test.com")
        ));

        var stmt = WitSql.ParseStatement("SELECT * FROM Users") as WitSqlStatementSelect;

        // Act
        var result = m_executor.Execute(stmt!);
        var rows = result.ReadAll();

        // Assert - query should succeed without transaction
        Assert.That(rows, Has.Count.EqualTo(1));
    }

    #endregion

    #region FOR Clause Combinations

    [Test]
    public void SelectForUpdateWithWhereClauseIsParsedTest()
    {
        var stmt = WitSql.ParseStatement("SELECT * FROM Users WHERE Age > 18 FOR UPDATE") as WitSqlStatementSelect;
        
        Assert.That(stmt, Is.Not.Null);
        Assert.That(stmt!.WhereClause, Is.Not.Null);
        Assert.That(stmt.ForClause, Is.Not.Null);
        Assert.That(stmt.ForClause!.LockingType, Is.EqualTo(LockingType.ForUpdate));
    }

    [Test]
    public void SelectForShareWithJoinIsParsedTest()
    {
        var stmt = WitSql.ParseStatement(
            "SELECT u.*, o.* FROM Users u JOIN Orders o ON u.Id = o.UserId FOR SHARE") as WitSqlStatementSelect;
        
        Assert.That(stmt, Is.Not.Null);
        Assert.That(stmt!.ForClause, Is.Not.Null);
        Assert.That(stmt.ForClause!.LockingType, Is.EqualTo(LockingType.ForShare));
    }

    [Test]
    public void SelectForUpdateWithGroupByIsParsedTest()
    {
        var stmt = WitSql.ParseStatement(
            "SELECT Category, COUNT(*) FROM Products GROUP BY Category FOR UPDATE") as WitSqlStatementSelect;
        
        Assert.That(stmt, Is.Not.Null);
        Assert.That(stmt!.GroupByClause, Is.Not.Null);
        Assert.That(stmt.ForClause, Is.Not.Null);
        Assert.That(stmt.ForClause!.LockingType, Is.EqualTo(LockingType.ForUpdate));
    }

    #endregion

    #region LockingType Mapping Tests

    [Test]
    public void LockingTypeNoneDoesNotRequireTransactionTest()
    {
        // SELECT without FOR clause has LockingType.None
        var stmt = WitSql.ParseStatement("SELECT * FROM Users") as WitSqlStatementSelect;
        
        Assert.That(stmt, Is.Not.Null);
        Assert.That(stmt!.ForClause, Is.Null);
    }

    [TestCase("SELECT * FROM Users FOR UPDATE", LockingType.ForUpdate)]
    [TestCase("SELECT * FROM Users FOR SHARE", LockingType.ForShare)]
    public void LockingTypeMappingTest(string sql, LockingType expectedType)
    {
        var stmt = WitSql.ParseStatement(sql) as WitSqlStatementSelect;
        
        Assert.That(stmt, Is.Not.Null);
        Assert.That(stmt!.ForClause, Is.Not.Null);
        Assert.That(stmt.ForClause!.LockingType, Is.EqualTo(expectedType));
    }

    #endregion

    #region Subquery FOR UPDATE Tests

    [Test]
    public void SelectForUpdateWithSubqueryInFromReturnsNullTableNameTest()
    {
        // FOR UPDATE with subquery - no table to lock
        var mvccTransaction = Substitute.For<IMvccTransaction>();
        m_database.CurrentTransaction.Returns(mvccTransaction);

        // Subquery as table source - parser should handle this
        var stmt = WitSql.ParseStatement(
            "SELECT * FROM (SELECT Id, Name FROM Users) AS sub FOR UPDATE") as WitSqlStatementSelect;

        // Act & Assert - should throw because subquery doesn't have table name
        var ex = Assert.Throws<InvalidOperationException>(() => m_executor.Execute(stmt!));
        Assert.That(ex.Message, Does.Contain("requires a table"));
    }

    #endregion
}

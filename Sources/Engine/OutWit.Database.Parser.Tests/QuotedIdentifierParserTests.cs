using OutWit.Database.Parser.Expressions;
using OutWit.Database.Parser.Schema.TableSources;
using OutWit.Database.Parser.Statements;

namespace OutWit.Database.Parser.Tests;

/// <summary>
/// Tests for quoted identifier parsing (double quotes, square brackets, backticks).
/// </summary>
[TestFixture]
public class QuotedIdentifierParserTests
{
    #region Double Quote Tests

    [Test]
    public void ParseSelectWithDoubleQuotedTableTest()
    {
        var statements = WitSql.Parse("SELECT * FROM \"Users\"");

        Assert.That(statements, Has.Count.EqualTo(1));

        var select = statements[0] as WitSqlStatementSelect;
        Assert.That(select, Is.Not.Null);

        var from = select!.FromClause![0] as TableSourceSimple;
        Assert.That(from!.TableName, Is.EqualTo("Users")); // Without quotes
    }

    [Test]
    public void ParseSelectWithDoubleQuotedColumnTest()
    {
        var statements = WitSql.Parse("SELECT \"FirstName\", \"LastName\" FROM Users");

        var select = statements[0] as WitSqlStatementSelect;
        
        var col1 = (select!.SelectList[0].Expression as WitSqlExpressionColumnRef)!;
        var col2 = (select.SelectList[1].Expression as WitSqlExpressionColumnRef)!;
        
        Assert.That(col1.ColumnName, Is.EqualTo("FirstName")); // Without quotes
        Assert.That(col2.ColumnName, Is.EqualTo("LastName"));
    }

    [Test]
    public void ParseSelectWithEscapedDoubleQuoteTest()
    {
        var statements = WitSql.Parse("SELECT * FROM \"Table\"\"Name\"");

        var select = statements[0] as WitSqlStatementSelect;
        var from = select!.FromClause![0] as TableSourceSimple;
        Assert.That(from!.TableName, Is.EqualTo("Table\"Name")); // Escaped quote becomes single
    }

    #endregion

    #region Square Bracket Tests

    [Test]
    public void ParseSelectWithSquareBracketTableTest()
    {
        var statements = WitSql.Parse("SELECT * FROM [Users]");

        var select = statements[0] as WitSqlStatementSelect;
        var from = select!.FromClause![0] as TableSourceSimple;
        Assert.That(from!.TableName, Is.EqualTo("Users")); // Without brackets
    }

    [Test]
    public void ParseSelectWithSquareBracketColumnTest()
    {
        var statements = WitSql.Parse("SELECT [Id], [Name] FROM Users");

        var select = statements[0] as WitSqlStatementSelect;
        
        var col1 = (select!.SelectList[0].Expression as WitSqlExpressionColumnRef)!;
        var col2 = (select.SelectList[1].Expression as WitSqlExpressionColumnRef)!;
        
        Assert.That(col1.ColumnName, Is.EqualTo("Id"));
        Assert.That(col2.ColumnName, Is.EqualTo("Name"));
    }

    #endregion

    #region Backtick Tests

    [Test]
    public void ParseSelectWithBacktickTableTest()
    {
        var statements = WitSql.Parse("SELECT * FROM `Users`");

        var select = statements[0] as WitSqlStatementSelect;
        var from = select!.FromClause![0] as TableSourceSimple;
        Assert.That(from!.TableName, Is.EqualTo("Users")); // Without backticks
    }

    [Test]
    public void ParseSelectWithBacktickColumnTest()
    {
        var statements = WitSql.Parse("SELECT `Id`, `Name` FROM Users");

        var select = statements[0] as WitSqlStatementSelect;
        
        var col1 = (select!.SelectList[0].Expression as WitSqlExpressionColumnRef)!;
        var col2 = (select.SelectList[1].Expression as WitSqlExpressionColumnRef)!;
        
        Assert.That(col1.ColumnName, Is.EqualTo("Id"));
        Assert.That(col2.ColumnName, Is.EqualTo("Name"));
    }

    #endregion

    #region Mixed Quoting Tests

    [Test]
    public void ParseSelectWithMixedQuotingStylesTest()
    {
        var statements = WitSql.Parse("SELECT [Id], \"Name\" FROM `Users`");

        var select = statements[0] as WitSqlStatementSelect;
        
        var col1 = (select!.SelectList[0].Expression as WitSqlExpressionColumnRef)!;
        var col2 = (select.SelectList[1].Expression as WitSqlExpressionColumnRef)!;
        var from = select.FromClause![0] as TableSourceSimple;
        
        Assert.That(col1.ColumnName, Is.EqualTo("Id"));
        Assert.That(col2.ColumnName, Is.EqualTo("Name"));
        Assert.That(from!.TableName, Is.EqualTo("Users"));
    }

    [Test]
    public void ParseSelectWithQuotedAliasTest()
    {
        var statements = WitSql.Parse("SELECT u.Name FROM Users AS \"u\"");

        var select = statements[0] as WitSqlStatementSelect;
        var from = select!.FromClause![0] as TableSourceSimple;
        Assert.That(from!.Alias, Is.EqualTo("u")); // Without quotes
    }

    #endregion

    #region DML Statement Tests

    [Test]
    public void ParseInsertWithQuotedNamesTest()
    {
        var statements = WitSql.Parse("INSERT INTO \"Users\" (\"Id\", \"Name\") VALUES (1, 'John')");

        var insert = statements[0] as WitSqlStatementInsert;
        Assert.That(insert!.TableName, Is.EqualTo("Users"));
        Assert.That(insert.ColumnNames![0], Is.EqualTo("Id"));
        Assert.That(insert.ColumnNames[1], Is.EqualTo("Name"));
    }

    [Test]
    public void ParseUpdateWithQuotedNamesTest()
    {
        var statements = WitSql.Parse("UPDATE \"Users\" SET \"Name\" = 'Jane' WHERE \"Id\" = 1");

        var update = statements[0] as WitSqlStatementUpdate;
        Assert.That(update!.TableName, Is.EqualTo("Users"));
        Assert.That(update.SetClauses[0].ColumnName, Is.EqualTo("Name"));
    }

    [Test]
    public void ParseDeleteWithQuotedNamesTest()
    {
        var statements = WitSql.Parse("DELETE FROM \"Users\" WHERE \"Id\" = 1");

        var delete = statements[0] as WitSqlStatementDelete;
        Assert.That(delete!.TableName, Is.EqualTo("Users"));
    }

    #endregion

    #region DDL Statement Tests

    [Test]
    public void ParseCreateTableWithQuotedNamesTest()
    {
        var statements = WitSql.Parse("CREATE TABLE \"Users\" (\"Id\" INT PRIMARY KEY, \"Name\" VARCHAR(100))");

        var create = statements[0] as WitSqlStatementCreateTable;
        Assert.That(create!.TableName, Is.EqualTo("Users"));
        Assert.That(create.Columns[0].Name, Is.EqualTo("Id"));
        Assert.That(create.Columns[1].Name, Is.EqualTo("Name"));
    }

    [Test]
    public void ParseCreateIndexWithQuotedNamesTest()
    {
        var statements = WitSql.Parse("CREATE INDEX \"IX_Users_Name\" ON \"Users\" (\"Name\")");

        var create = statements[0] as WitSqlStatementCreateIndex;
        Assert.That(create!.IndexName, Is.EqualTo("IX_Users_Name"));
        Assert.That(create.TableName, Is.EqualTo("Users"));
    }

    [Test]
    public void ParseDropTableWithQuotedNameTest()
    {
        var statements = WitSql.Parse("DROP TABLE IF EXISTS \"Users\"");

        var drop = statements[0] as WitSqlStatementDropTable;
        Assert.That(drop!.TableName, Is.EqualTo("Users"));
    }

    #endregion

    #region Special Cases Tests

    [Test]
    public void ParseTableNameWithSpacesTest()
    {
        var statements = WitSql.Parse("SELECT * FROM \"User Table\"");

        var select = statements[0] as WitSqlStatementSelect;
        var from = select!.FromClause![0] as TableSourceSimple;
        Assert.That(from!.TableName, Is.EqualTo("User Table"));
    }

    [Test]
    public void ParseTableNameWithReservedWordTest()
    {
        var statements = WitSql.Parse("SELECT * FROM \"SELECT\"");

        var select = statements[0] as WitSqlStatementSelect;
        var from = select!.FromClause![0] as TableSourceSimple;
        Assert.That(from!.TableName, Is.EqualTo("SELECT"));
    }

    [Test]
    public void ParseColumnNameStartingWithDigitTest()
    {
        var statements = WitSql.Parse("SELECT \"1Column\" FROM Users");

        var select = statements[0] as WitSqlStatementSelect;
        var col = (select!.SelectList[0].Expression as WitSqlExpressionColumnRef)!;
        Assert.That(col.ColumnName, Is.EqualTo("1Column"));
    }

    #endregion
}

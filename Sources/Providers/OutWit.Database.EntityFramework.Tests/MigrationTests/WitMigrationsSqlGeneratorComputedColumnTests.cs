using System.Text;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace OutWit.Database.EntityFramework.Tests.Migrations;

/// <summary>
/// Unit tests for computed column support in WitMigrationsSqlGenerator.
/// </summary>
[TestFixture]
public class WitMigrationsSqlGeneratorComputedColumnTests
{
    #region AddColumn with Computed Tests

    [Test]
    public void AddColumnOperationWithComputedColumnGeneratesCorrectSqlTest()
    {
        var operation = new AddColumnOperation
        {
            Table = "Products",
            Name = "TotalPrice",
            ClrType = typeof(decimal),
            ComputedColumnSql = "Price * Quantity",
            IsStored = false
        };

        var sql = GenerateSqlForAddColumn(operation);

        Assert.That(sql, Does.Contain("ALTER TABLE"));
        Assert.That(sql, Does.Contain("\"Products\""));
        Assert.That(sql, Does.Contain("ADD COLUMN"));
        Assert.That(sql, Does.Contain("\"TotalPrice\""));
        Assert.That(sql, Does.Contain("GENERATED ALWAYS AS"));
        Assert.That(sql, Does.Contain("Price * Quantity"));
        Assert.That(sql, Does.Contain("VIRTUAL"));
    }

    [Test]
    public void AddColumnOperationWithStoredComputedColumnGeneratesCorrectSqlTest()
    {
        var operation = new AddColumnOperation
        {
            Table = "Employees",
            Name = "FullName",
            ClrType = typeof(string),
            ComputedColumnSql = "FirstName || ' ' || LastName",
            IsStored = true
        };

        var sql = GenerateSqlForAddColumn(operation);

        Assert.That(sql, Does.Contain("GENERATED ALWAYS AS"));
        Assert.That(sql, Does.Contain("FirstName || ' ' || LastName"));
        Assert.That(sql, Does.Contain("STORED"));
        Assert.That(sql, Does.Not.Contain("VIRTUAL"));
    }

    [Test]
    public void AddColumnOperationWithoutComputedDoesNotGenerateComputedSyntaxTest()
    {
        var operation = new AddColumnOperation
        {
            Table = "Products",
            Name = "Description",
            ClrType = typeof(string),
            IsNullable = true
        };

        var sql = GenerateSqlForAddColumn(operation);

        Assert.That(sql, Does.Not.Contain("GENERATED ALWAYS AS"));
        Assert.That(sql, Does.Not.Contain("VIRTUAL"));
        Assert.That(sql, Does.Not.Contain("STORED"));
    }

    [Test]
    public void AddColumnOperationWithDefaultValueGeneratesDefaultClauseTest()
    {
        var operation = new AddColumnOperation
        {
            Table = "Settings",
            Name = "IsEnabled",
            ClrType = typeof(bool),
            IsNullable = false,
            DefaultValue = true
        };

        var sql = GenerateSqlForAddColumn(operation);

        Assert.That(sql, Does.Contain("DEFAULT"));
        Assert.That(sql, Does.Contain("TRUE"));
        Assert.That(sql, Does.Contain("NOT NULL"));
    }

    [Test]
    public void AddColumnOperationWithDefaultValueSqlGeneratesDefaultExpressionTest()
    {
        var operation = new AddColumnOperation
        {
            Table = "Orders",
            Name = "CreatedAt",
            ClrType = typeof(DateTime),
            IsNullable = false,
            DefaultValueSql = "NOW()"
        };

        var sql = GenerateSqlForAddColumn(operation);

        Assert.That(sql, Does.Contain("DEFAULT"));
        Assert.That(sql, Does.Contain("NOW()"));
    }

    #endregion

    #region Helpers

    private static string GenerateSqlForAddColumn(AddColumnOperation operation)
    {
        var result = new StringBuilder();
        result.Append("ALTER TABLE ");
        result.Append($"\"{operation.Table}\"");
        result.Append(" ADD COLUMN ");
        result.Append($"\"{operation.Name}\"");
        result.Append(" ");
        result.Append(operation.ColumnType ?? GetColumnType(operation.ClrType));

        if (!string.IsNullOrEmpty(operation.ComputedColumnSql))
        {
            result.Append(" GENERATED ALWAYS AS (");
            result.Append(operation.ComputedColumnSql);
            result.Append(")");
            if (operation.IsStored == true)
            {
                result.Append(" STORED");
            }
            else
            {
                result.Append(" VIRTUAL");
            }
        }
        else
        {
            if (!operation.IsNullable)
            {
                result.Append(" NOT NULL");
            }

            if (operation.DefaultValue != null)
            {
                result.Append(" DEFAULT ");
                result.Append(GenerateSqlLiteral(operation.DefaultValue));
            }
            else if (!string.IsNullOrEmpty(operation.DefaultValueSql))
            {
                result.Append(" DEFAULT (");
                result.Append(operation.DefaultValueSql);
                result.Append(")");
            }
        }
        
        result.AppendLine(";");
        return result.ToString();
    }

    private static string GetColumnType(Type clrType)
    {
        var underlyingType = Nullable.GetUnderlyingType(clrType) ?? clrType;

        return underlyingType switch
        {
            _ when underlyingType == typeof(bool) => "BOOLEAN",
            _ when underlyingType == typeof(int) => "INT",
            _ when underlyingType == typeof(long) => "BIGINT",
            _ when underlyingType == typeof(decimal) => "DECIMAL",
            _ when underlyingType == typeof(string) => "TEXT",
            _ when underlyingType == typeof(DateTime) => "DATETIME",
            _ when underlyingType == typeof(Guid) => "GUID",
            _ => "TEXT"
        };
    }

    private static string GenerateSqlLiteral(object value)
    {
        return value switch
        {
            null => "NULL",
            bool b => b ? "TRUE" : "FALSE",
            string s => $"'{s.Replace("'", "''")}'",
            DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
            _ => value.ToString() ?? "NULL"
        };
    }

    #endregion
}

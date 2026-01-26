using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using OutWit.Database.EntityFramework.Extensions;
using OutWit.Database.EntityFramework.Migrations;

namespace OutWit.Database.EntityFramework.Tests.Migrations;

/// <summary>
/// Unit tests for <see cref="WitMigrationsSqlGenerator"/>.
/// </summary>
[TestFixture]
public class WitMigrationsSqlGeneratorTests
{
    #region Fields

    private WitMigrationsSqlGenerator m_generator = null!;
    private TestMigrationContext m_context = null!;

    #endregion

    #region Setup/TearDown

    [SetUp]
    public void Setup()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestMigrationContext>();
        optionsBuilder.UseWitDbInMemory();

        m_context = new TestMigrationContext(optionsBuilder.Options);
        var dependencies = m_context.GetService<MigrationsSqlGeneratorDependencies>();
        
        m_generator = new WitMigrationsSqlGenerator(dependencies);
    }

    [TearDown]
    public void TearDown()
    {
        m_context?.Dispose();
    }

    #endregion

    #region Table Operations Tests

    [Test]
    public void GenerateCreateTableOperationProducesValidSqlTest()
    {
        var operation = new CreateTableOperation
        {
            Name = "TestTable",
            Columns =
            {
                new AddColumnOperation
                {
                    Name = "Id",
                    Table = "TestTable",
                    ClrType = typeof(int),
                    IsNullable = false
                },
                new AddColumnOperation
                {
                    Name = "Name",
                    Table = "TestTable",
                    ClrType = typeof(string),
                    IsNullable = true
                }
            }
        };

        var commands = m_generator.Generate(new[] { operation }, m_context.Model);
        var sql = string.Join("", commands.Select(c => c.CommandText));

        Assert.That(sql, Does.Contain("CREATE TABLE"));
        Assert.That(sql, Does.Contain("\"TestTable\""));
        Assert.That(sql, Does.Contain("\"Id\""));
        Assert.That(sql, Does.Contain("\"Name\""));
    }

    [Test]
    public void GenerateDropTableOperationProducesDropIfExistsTest()
    {
        var operation = new DropTableOperation { Name = "TestTable" };

        var commands = m_generator.Generate(new[] { operation }, m_context.Model);
        var sql = string.Join("", commands.Select(c => c.CommandText));

        Assert.That(sql, Does.Contain("DROP TABLE IF EXISTS"));
        Assert.That(sql, Does.Contain("\"TestTable\""));
    }

    [Test]
    public void GenerateRenameTableOperationProducesAlterTableRenameTest()
    {
        var operation = new RenameTableOperation
        {
            Name = "OldTable",
            NewName = "NewTable"
        };

        var commands = m_generator.Generate(new[] { operation }, m_context.Model);
        var sql = string.Join("", commands.Select(c => c.CommandText));

        Assert.That(sql, Does.Contain("ALTER TABLE"));
        Assert.That(sql, Does.Contain("\"OldTable\""));
        Assert.That(sql, Does.Contain("RENAME TO"));
        Assert.That(sql, Does.Contain("\"NewTable\""));
    }

    #endregion

    #region Column Operations Tests

    [Test]
    public void GenerateAddColumnOperationProducesValidSqlTest()
    {
        var operation = new AddColumnOperation
        {
            Table = "TestTable",
            Name = "NewColumn",
            ClrType = typeof(string),
            IsNullable = true
        };

        var commands = m_generator.Generate(new[] { operation }, m_context.Model);
        var sql = string.Join("", commands.Select(c => c.CommandText));

        Assert.That(sql, Does.Contain("ALTER TABLE"));
        Assert.That(sql, Does.Contain("\"TestTable\""));
        Assert.That(sql, Does.Contain("ADD COLUMN"));
        Assert.That(sql, Does.Contain("\"NewColumn\""));
    }

    [Test]
    public void GenerateAddColumnOperationWithNotNullProducesNotNullConstraintTest()
    {
        var operation = new AddColumnOperation
        {
            Table = "TestTable",
            Name = "RequiredColumn",
            ClrType = typeof(int),
            IsNullable = false
        };

        var commands = m_generator.Generate(new[] { operation }, m_context.Model);
        var sql = string.Join("", commands.Select(c => c.CommandText));

        Assert.That(sql, Does.Contain("NOT NULL"));
    }

    [Test]
    public void GenerateAddColumnOperationWithDefaultValueProducesDefaultClauseTest()
    {
        var operation = new AddColumnOperation
        {
            Table = "TestTable",
            Name = "WithDefault",
            ClrType = typeof(int),
            DefaultValue = 42
        };

        var commands = m_generator.Generate(new[] { operation }, m_context.Model);
        var sql = string.Join("", commands.Select(c => c.CommandText));

        Assert.That(sql, Does.Contain("DEFAULT"));
        Assert.That(sql, Does.Contain("42"));
    }

    [Test]
    public void GenerateDropColumnOperationProducesValidSqlTest()
    {
        var operation = new DropColumnOperation
        {
            Table = "TestTable",
            Name = "ColumnToRemove"
        };

        var commands = m_generator.Generate(new[] { operation }, m_context.Model);
        var sql = string.Join("", commands.Select(c => c.CommandText));

        Assert.That(sql, Does.Contain("ALTER TABLE"));
        Assert.That(sql, Does.Contain("\"TestTable\""));
        Assert.That(sql, Does.Contain("DROP COLUMN"));
        Assert.That(sql, Does.Contain("\"ColumnToRemove\""));
    }

    [Test]
    public void GenerateRenameColumnOperationProducesValidSqlTest()
    {
        var operation = new RenameColumnOperation
        {
            Table = "TestTable",
            Name = "OldColumn",
            NewName = "NewColumn"
        };

        var commands = m_generator.Generate(new[] { operation }, m_context.Model);
        var sql = string.Join("", commands.Select(c => c.CommandText));

        Assert.That(sql, Does.Contain("ALTER TABLE"));
        Assert.That(sql, Does.Contain("RENAME COLUMN"));
        Assert.That(sql, Does.Contain("\"OldColumn\""));
        Assert.That(sql, Does.Contain("\"NewColumn\""));
    }

    #endregion

    #region Index Operations Tests

    [Test]
    public void GenerateCreateIndexOperationProducesValidSqlTest()
    {
        var operation = new CreateIndexOperation
        {
            Name = "IX_TestTable_Column",
            Table = "TestTable",
            Columns = new[] { "Column1", "Column2" }
        };

        var commands = m_generator.Generate(new[] { operation }, m_context.Model);
        var sql = string.Join("", commands.Select(c => c.CommandText));

        Assert.That(sql, Does.Contain("CREATE"));
        Assert.That(sql, Does.Contain("INDEX"));
        Assert.That(sql, Does.Contain("IF NOT EXISTS"));
        Assert.That(sql, Does.Contain("\"IX_TestTable_Column\""));
        Assert.That(sql, Does.Contain("ON"));
        Assert.That(sql, Does.Contain("\"TestTable\""));
    }

    [Test]
    public void GenerateCreateUniqueIndexOperationProducesUniqueKeywordTest()
    {
        var operation = new CreateIndexOperation
        {
            Name = "IX_Unique",
            Table = "TestTable",
            Columns = new[] { "UniqueColumn" },
            IsUnique = true
        };

        var commands = m_generator.Generate(new[] { operation }, m_context.Model);
        var sql = string.Join("", commands.Select(c => c.CommandText));

        Assert.That(sql, Does.Contain("CREATE UNIQUE INDEX"));
    }

    [Test]
    public void GenerateDropIndexOperationProducesDropIfExistsTest()
    {
        var operation = new DropIndexOperation
        {
            Name = "IX_ToRemove",
            Table = "TestTable"
        };

        var commands = m_generator.Generate(new[] { operation }, m_context.Model);
        var sql = string.Join("", commands.Select(c => c.CommandText));

        Assert.That(sql, Does.Contain("DROP INDEX IF EXISTS"));
        Assert.That(sql, Does.Contain("\"IX_ToRemove\""));
    }

    #endregion

    #region Sequence Operations Tests

    [Test]
    public void GenerateCreateSequenceOperationProducesValidSqlTest()
    {
        var operation = new CreateSequenceOperation
        {
            Name = "MySequence",
            StartValue = 100
        };

        var commands = m_generator.Generate(new[] { operation }, m_context.Model);
        var sql = string.Join("", commands.Select(c => c.CommandText));

        Assert.That(sql, Does.Contain("CREATE SEQUENCE"));
        Assert.That(sql, Does.Contain("\"MySequence\""));
        Assert.That(sql, Does.Contain("START WITH 100"));
    }

    [Test]
    public void GenerateDropSequenceOperationProducesValidSqlTest()
    {
        var operation = new DropSequenceOperation { Name = "MySequence" };

        var commands = m_generator.Generate(new[] { operation }, m_context.Model);
        var sql = string.Join("", commands.Select(c => c.CommandText));

        Assert.That(sql, Does.Contain("DROP SEQUENCE"));
        Assert.That(sql, Does.Contain("\"MySequence\""));
    }

    #endregion

    #region SQL Operation Tests

    [Test]
    public void GenerateSqlOperationPassesThroughRawSqlTest()
    {
        var operation = new SqlOperation
        {
            Sql = "SELECT * FROM SomeTable WHERE Id = 1"
        };

        var commands = m_generator.Generate(new[] { operation }, m_context.Model);
        var sql = string.Join("", commands.Select(c => c.CommandText));

        Assert.That(sql, Does.Contain("SELECT * FROM SomeTable WHERE Id = 1"));
    }

    #endregion

    #region Type Mapping Tests

    [Test]
    [TestCase(typeof(bool), "BOOLEAN")]
    [TestCase(typeof(int), "INT")]
    [TestCase(typeof(long), "BIGINT")]
    [TestCase(typeof(double), "DOUBLE")]
    [TestCase(typeof(decimal), "DECIMAL")]
    [TestCase(typeof(string), "TEXT")]
    [TestCase(typeof(DateTime), "DATETIME")]
    [TestCase(typeof(Guid), "GUID")]
    [TestCase(typeof(byte[]), "BLOB")]
    public void GenerateAddColumnOperationMapsClrTypeCorrectlyTest(Type clrType, string expectedSqlType)
    {
        var operation = new AddColumnOperation
        {
            Table = "TestTable",
            Name = "TypedColumn",
            ClrType = clrType,
            IsNullable = true
        };

        var commands = m_generator.Generate(new[] { operation }, m_context.Model);
        var sql = string.Join("", commands.Select(c => c.CommandText));

        Assert.That(sql, Does.Contain(expectedSqlType));
    }

    #endregion

    #region Test Models

    private class TestMigrationContext : DbContext
    {
        public TestMigrationContext(DbContextOptions<TestMigrationContext> options)
            : base(options)
        {
        }
    }

    #endregion

    #region Foreign Key Operations Tests

    [Test]
    public void GenerateAddForeignKeyOperationProducesValidSqlTest()
    {
        var operation = new AddForeignKeyOperation
        {
            Name = "FK_Orders_Users",
            Table = "Orders",
            Columns = new[] { "UserId" },
            PrincipalTable = "Users",
            PrincipalColumns = new[] { "Id" }
        };

        var commands = m_generator.Generate(new[] { operation }, m_context.Model);
        var sql = string.Join("", commands.Select(c => c.CommandText));

        Assert.That(sql, Does.Contain("ALTER TABLE"));
        Assert.That(sql, Does.Contain("\"Orders\""));
        Assert.That(sql, Does.Contain("ADD CONSTRAINT"));
        Assert.That(sql, Does.Contain("\"FK_Orders_Users\""));
        Assert.That(sql, Does.Contain("FOREIGN KEY"));
        Assert.That(sql, Does.Contain("\"UserId\""));
        Assert.That(sql, Does.Contain("REFERENCES"));
        Assert.That(sql, Does.Contain("\"Users\""));
        Assert.That(sql, Does.Contain("\"Id\""));
    }

    [Test]
    public void GenerateAddForeignKeyOperationWithCascadeDeleteProducesOnDeleteCascadeTest()
    {
        var operation = new AddForeignKeyOperation
        {
            Name = "FK_Orders_Users",
            Table = "Orders",
            Columns = new[] { "UserId" },
            PrincipalTable = "Users",
            PrincipalColumns = new[] { "Id" },
            OnDelete = ReferentialAction.Cascade
        };

        var commands = m_generator.Generate(new[] { operation }, m_context.Model);
        var sql = string.Join("", commands.Select(c => c.CommandText));

        Assert.That(sql, Does.Contain("ON DELETE CASCADE"));
    }

    [Test]
    public void GenerateAddForeignKeyOperationWithSetNullProducesOnDeleteSetNullTest()
    {
        var operation = new AddForeignKeyOperation
        {
            Name = "FK_Orders_Users",
            Table = "Orders",
            Columns = new[] { "UserId" },
            PrincipalTable = "Users",
            PrincipalColumns = new[] { "Id" },
            OnDelete = ReferentialAction.SetNull
        };

        var commands = m_generator.Generate(new[] { operation }, m_context.Model);
        var sql = string.Join("", commands.Select(c => c.CommandText));

        Assert.That(sql, Does.Contain("ON DELETE SET NULL"));
    }

    [Test]
    public void GenerateAddForeignKeyOperationWithRestrictProducesOnDeleteRestrictTest()
    {
        var operation = new AddForeignKeyOperation
        {
            Name = "FK_Orders_Users",
            Table = "Orders",
            Columns = new[] { "UserId" },
            PrincipalTable = "Users",
            PrincipalColumns = new[] { "Id" },
            OnDelete = ReferentialAction.Restrict
        };

        var commands = m_generator.Generate(new[] { operation }, m_context.Model);
        var sql = string.Join("", commands.Select(c => c.CommandText));

        Assert.That(sql, Does.Contain("ON DELETE RESTRICT"));
    }

    [Test]
    public void GenerateAddForeignKeyOperationWithOnUpdateCascadeProducesOnUpdateCascadeTest()
    {
        var operation = new AddForeignKeyOperation
        {
            Name = "FK_Orders_Users",
            Table = "Orders",
            Columns = new[] { "UserId" },
            PrincipalTable = "Users",
            PrincipalColumns = new[] { "Id" },
            OnUpdate = ReferentialAction.Cascade
        };

        var commands = m_generator.Generate(new[] { operation }, m_context.Model);
        var sql = string.Join("", commands.Select(c => c.CommandText));

        Assert.That(sql, Does.Contain("ON UPDATE CASCADE"));
    }

    [Test]
    public void GenerateAddForeignKeyOperationCompositeKeyProducesMultipleColumnsTest()
    {
        var operation = new AddForeignKeyOperation
        {
            Name = "FK_OrderItems_OrderLines",
            Table = "OrderItems",
            Columns = new[] { "OrderId", "LineNumber" },
            PrincipalTable = "OrderLines",
            PrincipalColumns = new[] { "OrderId", "LineNo" }
        };

        var commands = m_generator.Generate(new[] { operation }, m_context.Model);
        var sql = string.Join("", commands.Select(c => c.CommandText));

        Assert.That(sql, Does.Contain("\"OrderId\""));
        Assert.That(sql, Does.Contain("\"LineNumber\""));
        Assert.That(sql, Does.Contain("\"LineNo\""));
    }

    [Test]
    public void GenerateDropForeignKeyOperationProducesValidSqlTest()
    {
        var operation = new DropForeignKeyOperation
        {
            Name = "FK_Orders_Users",
            Table = "Orders"
        };

        var commands = m_generator.Generate(new[] { operation }, m_context.Model);
        var sql = string.Join("", commands.Select(c => c.CommandText));

        Assert.That(sql, Does.Contain("ALTER TABLE"));
        Assert.That(sql, Does.Contain("\"Orders\""));
        Assert.That(sql, Does.Contain("DROP CONSTRAINT"));
        Assert.That(sql, Does.Contain("\"FK_Orders_Users\""));
    }

    #endregion

    #region Check Constraint Operations Tests

    [Test]
    public void GenerateAddCheckConstraintOperationProducesValidSqlTest()
    {
        var operation = new AddCheckConstraintOperation
        {
            Name = "CK_Products_Price",
            Table = "Products",
            Sql = "Price >= 0"
        };

        var commands = m_generator.Generate(new[] { operation }, m_context.Model);
        var sql = string.Join("", commands.Select(c => c.CommandText));

        Assert.That(sql, Does.Contain("ALTER TABLE"));
        Assert.That(sql, Does.Contain("\"Products\""));
        Assert.That(sql, Does.Contain("ADD CONSTRAINT"));
        Assert.That(sql, Does.Contain("\"CK_Products_Price\""));
        Assert.That(sql, Does.Contain("CHECK"));
        Assert.That(sql, Does.Contain("Price >= 0"));
    }

    [Test]
    public void GenerateAddCheckConstraintOperationWithComplexExpressionProducesValidSqlTest()
    {
        var operation = new AddCheckConstraintOperation
        {
            Name = "CK_Orders_Amount",
            Table = "Orders",
            Sql = "Quantity > 0 AND Price > 0 AND Total = Quantity * Price"
        };

        var commands = m_generator.Generate(new[] { operation }, m_context.Model);
        var sql = string.Join("", commands.Select(c => c.CommandText));

        Assert.That(sql, Does.Contain("CHECK"));
        Assert.That(sql, Does.Contain("Quantity > 0"));
        Assert.That(sql, Does.Contain("Total = Quantity * Price"));
    }

    [Test]
    public void GenerateDropCheckConstraintOperationProducesValidSqlTest()
    {
        var operation = new DropCheckConstraintOperation
        {
            Name = "CK_Products_Price",
            Table = "Products"
        };

        var commands = m_generator.Generate(new[] { operation }, m_context.Model);
        var sql = string.Join("", commands.Select(c => c.CommandText));

        Assert.That(sql, Does.Contain("ALTER TABLE"));
        Assert.That(sql, Does.Contain("\"Products\""));
        Assert.That(sql, Does.Contain("DROP CONSTRAINT"));
        Assert.That(sql, Does.Contain("\"CK_Products_Price\""));
    }

    #endregion
}

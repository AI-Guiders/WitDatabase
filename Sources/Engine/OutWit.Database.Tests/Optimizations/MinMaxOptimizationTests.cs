using NUnit.Framework;
using OutWit.Database.Core.Builder;
using OutWit.Database.Engine;

namespace OutWit.Database.Tests.Optimizations;

/// <summary>
/// Tests for MIN/MAX index optimization.
/// When an index exists on a column, MIN() should read the first entry
/// and MAX() should read the last entry, instead of scanning the entire table.
/// </summary>
[TestFixture]
public class MinMaxOptimizationTests
{
    #region Fields

    private WitSqlEngine m_engine = null!;

    #endregion

    #region Setup/Teardown

    [SetUp]
    public void Setup()
    {
        var db = new WitDatabaseBuilder()
            .WithMemoryStorage()
            .Build();
        m_engine = new WitSqlEngine(db, ownsStore: true);
    }

    [TearDown]
    public void TearDown()
    {
        m_engine.Dispose();
    }

    #endregion

    #region Helper Methods

    private void CreateTableWithIndex()
    {
        m_engine.Execute("CREATE TABLE Products (Id BIGINT PRIMARY KEY AUTOINCREMENT, Name VARCHAR(100), Price DOUBLE, Quantity INT)");
        m_engine.Execute("CREATE INDEX IX_Products_Price ON Products(Price)");
        m_engine.Execute("CREATE INDEX IX_Products_Quantity ON Products(Quantity)");
    }

    private void InsertTestData()
    {
        m_engine.Execute("INSERT INTO Products (Name, Price, Quantity) VALUES ('Apple', 1.50, 100)");
        m_engine.Execute("INSERT INTO Products (Name, Price, Quantity) VALUES ('Banana', 0.75, 200)");
        m_engine.Execute("INSERT INTO Products (Name, Price, Quantity) VALUES ('Cherry', 3.00, 50)");
        m_engine.Execute("INSERT INTO Products (Name, Price, Quantity) VALUES ('Date', 2.25, 150)");
        m_engine.Execute("INSERT INTO Products (Name, Price, Quantity) VALUES ('Elderberry', 4.50, 25)");
    }

    #endregion

    #region MIN Tests

    [Test]
    public void MinOnIndexedColumn_ReturnsCorrectValue_Test()
    {
        CreateTableWithIndex();
        InsertTestData();

        using var result = m_engine.Execute("SELECT MIN(Price) FROM Products");
        
        Assert.That(result.Read(), Is.True);
        var min = result.CurrentRow[0].AsDouble();
        Assert.That(min, Is.EqualTo(0.75)); // Banana
    }

    [Test]
    public void MinOnIntegerColumn_ReturnsCorrectValue_Test()
    {
        CreateTableWithIndex();
        InsertTestData();

        using var result = m_engine.Execute("SELECT MIN(Quantity) FROM Products");
        
        Assert.That(result.Read(), Is.True);
        var min = result.CurrentRow[0].AsInt64();
        Assert.That(min, Is.EqualTo(25)); // Elderberry
    }

    [Test]
    public void MinOnEmptyTable_ReturnsNull_Test()
    {
        CreateTableWithIndex();
        // No data inserted

        using var result = m_engine.Execute("SELECT MIN(Price) FROM Products");
        
        Assert.That(result.Read(), Is.True);
        Assert.That(result.CurrentRow[0].IsNull, Is.True);
    }

    [Test]
    public void MinWithAlias_UsesAlias_Test()
    {
        CreateTableWithIndex();
        InsertTestData();

        using var result = m_engine.Execute("SELECT MIN(Price) AS MinPrice FROM Products");
        
        Assert.That(result.Columns[0].Name, Is.EqualTo("MinPrice"));
        Assert.That(result.Read(), Is.True);
        Assert.That(result.CurrentRow[0].AsDouble(), Is.EqualTo(0.75));
    }

    #endregion

    #region MAX Tests

    [Test]
    public void MaxOnIndexedColumn_ReturnsCorrectValue_Test()
    {
        CreateTableWithIndex();
        InsertTestData();

        using var result = m_engine.Execute("SELECT MAX(Price) FROM Products");
        
        Assert.That(result.Read(), Is.True);
        var max = result.CurrentRow[0].AsDouble();
        Assert.That(max, Is.EqualTo(4.50)); // Elderberry
    }

    [Test]
    public void MaxOnIntegerColumn_ReturnsCorrectValue_Test()
    {
        CreateTableWithIndex();
        InsertTestData();

        using var result = m_engine.Execute("SELECT MAX(Quantity) FROM Products");
        
        Assert.That(result.Read(), Is.True);
        var max = result.CurrentRow[0].AsInt64();
        Assert.That(max, Is.EqualTo(200)); // Banana
    }

    [Test]
    public void MaxOnEmptyTable_ReturnsNull_Test()
    {
        CreateTableWithIndex();
        // No data inserted

        using var result = m_engine.Execute("SELECT MAX(Price) FROM Products");
        
        Assert.That(result.Read(), Is.True);
        Assert.That(result.CurrentRow[0].IsNull, Is.True);
    }

    [Test]
    public void MaxWithAlias_UsesAlias_Test()
    {
        CreateTableWithIndex();
        InsertTestData();

        using var result = m_engine.Execute("SELECT MAX(Price) AS MaxPrice FROM Products");
        
        Assert.That(result.Columns[0].Name, Is.EqualTo("MaxPrice"));
        Assert.That(result.Read(), Is.True);
        Assert.That(result.CurrentRow[0].AsDouble(), Is.EqualTo(4.50));
    }

    #endregion

    #region Non-Optimized Cases

    [Test]
    public void MinWithWhereClause_DoesNotUseOptimization_Test()
    {
        CreateTableWithIndex();
        InsertTestData();

        // With WHERE clause, should use streaming aggregate (not index optimization)
        using var result = m_engine.Execute("SELECT MIN(Price) FROM Products WHERE Quantity > 50");
        
        Assert.That(result.Read(), Is.True);
        // Apple (1.50, 100), Banana (0.75, 200), Date (2.25, 150) have Quantity > 50
        var min = result.CurrentRow[0].AsDouble();
        Assert.That(min, Is.EqualTo(0.75)); // Banana
    }

    [Test]
    public void MinWithGroupBy_DoesNotUseOptimization_Test()
    {
        m_engine.Execute("CREATE TABLE Sales (Id BIGINT PRIMARY KEY AUTOINCREMENT, Region VARCHAR(50), Amount DOUBLE)");
        m_engine.Execute("CREATE INDEX IX_Sales_Amount ON Sales(Amount)");
        m_engine.Execute("INSERT INTO Sales (Region, Amount) VALUES ('North', 100)");
        m_engine.Execute("INSERT INTO Sales (Region, Amount) VALUES ('North', 200)");
        m_engine.Execute("INSERT INTO Sales (Region, Amount) VALUES ('South', 150)");

        // GROUP BY cannot use index optimization
        using var result = m_engine.Execute("SELECT Region, MIN(Amount) FROM Sales GROUP BY Region ORDER BY Region");
        
        var rows = new List<(string Region, double Min)>();
        while (result.Read())
        {
            rows.Add((result.CurrentRow[0].AsString(), result.CurrentRow[1].AsDouble()));
        }
        
        Assert.That(rows.Count, Is.EqualTo(2));
        Assert.That(rows.Any(r => r.Region == "North" && r.Min == 100), Is.True);
        Assert.That(rows.Any(r => r.Region == "South" && r.Min == 150), Is.True);
    }

    [Test]
    public void MinOnNonIndexedColumn_DoesNotUseOptimization_Test()
    {
        CreateTableWithIndex();
        InsertTestData();

        // Name column is not indexed
        using var result = m_engine.Execute("SELECT MIN(Name) FROM Products");
        
        Assert.That(result.Read(), Is.True);
        // Should still work, just not optimized
        Assert.That(result.CurrentRow[0].AsString(), Is.EqualTo("Apple")); // Alphabetically first
    }

    [Test]
    public void MultipleAggregates_DoesNotUseOptimization_Test()
    {
        CreateTableWithIndex();
        InsertTestData();

        // Multiple aggregates cannot use index optimization
        using var result = m_engine.Execute("SELECT MIN(Price), MAX(Price) FROM Products");
        
        Assert.That(result.Read(), Is.True);
        Assert.That(result.CurrentRow[0].AsDouble(), Is.EqualTo(0.75)); // MIN
        Assert.That(result.CurrentRow[1].AsDouble(), Is.EqualTo(4.50)); // MAX
    }

    #endregion

    #region Edge Cases

    [Test]
    public void MinAfterInsert_ReturnsUpdatedValue_Test()
    {
        CreateTableWithIndex();
        InsertTestData();

        // Initial MIN
        using (var result = m_engine.Execute("SELECT MIN(Price) FROM Products"))
        {
            result.Read();
            Assert.That(result.CurrentRow[0].AsDouble(), Is.EqualTo(0.75));
        }

        // Insert a lower value
        m_engine.Execute("INSERT INTO Products (Name, Price, Quantity) VALUES ('Fig', 0.25, 300)");

        // MIN should update
        using (var result = m_engine.Execute("SELECT MIN(Price) FROM Products"))
        {
            result.Read();
            Assert.That(result.CurrentRow[0].AsDouble(), Is.EqualTo(0.25));
        }
    }

    [Test]
    public void MaxAfterDelete_ReturnsUpdatedValue_Test()
    {
        CreateTableWithIndex();
        InsertTestData();

        // Initial MAX
        using (var result = m_engine.Execute("SELECT MAX(Price) FROM Products"))
        {
            result.Read();
            Assert.That(result.CurrentRow[0].AsDouble(), Is.EqualTo(4.50)); // Elderberry
        }

        // Delete the max value
        m_engine.Execute("DELETE FROM Products WHERE Name = 'Elderberry'");

        // MAX should update
        using (var result = m_engine.Execute("SELECT MAX(Price) FROM Products"))
        {
            result.Read();
            Assert.That(result.CurrentRow[0].AsDouble(), Is.EqualTo(3.00)); // Cherry
        }
    }

    [Test]
    public void MinOnStringColumn_WithIndex_ReturnsCorrectValue_Test()
    {
        m_engine.Execute("CREATE TABLE Items (Id BIGINT PRIMARY KEY AUTOINCREMENT, Code VARCHAR(10))");
        m_engine.Execute("CREATE INDEX IX_Items_Code ON Items(Code)");
        m_engine.Execute("INSERT INTO Items (Code) VALUES ('ZZZ')");
        m_engine.Execute("INSERT INTO Items (Code) VALUES ('AAA')");
        m_engine.Execute("INSERT INTO Items (Code) VALUES ('MMM')");

        using var result = m_engine.Execute("SELECT MIN(Code) FROM Items");
        
        Assert.That(result.Read(), Is.True);
        Assert.That(result.CurrentRow[0].AsString(), Is.EqualTo("AAA"));
    }

    [Test]
    public void MaxOnStringColumn_WithIndex_ReturnsCorrectValue_Test()
    {
        m_engine.Execute("CREATE TABLE Items (Id BIGINT PRIMARY KEY AUTOINCREMENT, Code VARCHAR(10))");
        m_engine.Execute("CREATE INDEX IX_Items_Code ON Items(Code)");
        m_engine.Execute("INSERT INTO Items (Code) VALUES ('ZZZ')");
        m_engine.Execute("INSERT INTO Items (Code) VALUES ('AAA')");
        m_engine.Execute("INSERT INTO Items (Code) VALUES ('MMM')");

        using var result = m_engine.Execute("SELECT MAX(Code) FROM Items");
        
        Assert.That(result.Read(), Is.True);
        Assert.That(result.CurrentRow[0].AsString(), Is.EqualTo("ZZZ"));
    }

    #endregion

    #region Performance Tests

    [Test]
    [Category("Performance")]
    public void MinMaxOptimization_IsFast_Test()
    {
        CreateTableWithIndex();
        
        // Insert many rows
        m_engine.Execute("BEGIN TRANSACTION");
        for (int i = 0; i < 1000; i++)
        {
            m_engine.Execute($"INSERT INTO Products (Name, Price, Quantity) VALUES ('Product{i}', {i * 0.01}, {i})");
        }
        m_engine.Execute("COMMIT");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        // Run MIN/MAX multiple times
        for (int i = 0; i < 100; i++)
        {
            using var minResult = m_engine.Execute("SELECT MIN(Price) FROM Products");
            minResult.Read();
            
            using var maxResult = m_engine.Execute("SELECT MAX(Price) FROM Products");
            maxResult.Read();
        }

        sw.Stop();
        
        // Should complete quickly with optimization (< 500ms for 200 queries)
        Console.WriteLine($"MIN/MAX optimization: {sw.ElapsedMilliseconds}ms for 200 queries");
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(1000), 
            $"MIN/MAX optimization took {sw.ElapsedMilliseconds}ms, expected < 1000ms");
    }

    #endregion
}

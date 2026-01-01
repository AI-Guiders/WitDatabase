using NUnit.Framework;
using OutWit.Database.Core.Builder;
using OutWit.Database.Engine;

namespace OutWit.Database.Tests.Optimizations;

/// <summary>
/// Tests for COUNT(*) metadata optimization.
/// Verifies that SELECT COUNT(*) FROM table (without WHERE) uses O(1) metadata lookup.
/// </summary>
[TestFixture]
public class CountStarOptimizationTests
{
    #region Fields

    private WitSqlEngine m_engine = null!;

    #endregion

    #region Setup/TearDown

    [SetUp]
    public void Setup()
    {
        var database = new WitDatabaseBuilder()
            .WithMemoryStorage()
            .Build();
        m_engine = new WitSqlEngine(database, ownsStore: true);
    }

    [TearDown]
    public void TearDown()
    {
        m_engine?.Dispose();
    }

    #endregion

    #region Row Count Tracking Tests

    [Test]
    public void RowCountStartsAtZeroForNewTableTest()
    {
        m_engine.Execute("CREATE TABLE T (Id INT)");
        
        var count = m_engine.GetTableRowCount("T");
        
        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public void RowCountIncrementsOnInsertTest()
    {
        m_engine.Execute("CREATE TABLE T (Id INT)");
        m_engine.Execute("INSERT INTO T (Id) VALUES (1)");
        
        var count = m_engine.GetTableRowCount("T");
        
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void RowCountIncrementsOnMultipleInsertsTest()
    {
        m_engine.Execute("CREATE TABLE T (Id INT)");
        
        for (int i = 0; i < 100; i++)
        {
            m_engine.Execute($"INSERT INTO T (Id) VALUES ({i})");
        }
        
        var count = m_engine.GetTableRowCount("T");
        
        Assert.That(count, Is.EqualTo(100));
    }

    [Test]
    public void RowCountDecrementsOnDeleteTest()
    {
        m_engine.Execute("CREATE TABLE T (Id INT)");
        m_engine.Execute("INSERT INTO T (Id) VALUES (1)");
        m_engine.Execute("INSERT INTO T (Id) VALUES (2)");
        m_engine.Execute("INSERT INTO T (Id) VALUES (3)");
        
        m_engine.Execute("DELETE FROM T WHERE Id = 2");
        
        var count = m_engine.GetTableRowCount("T");
        
        Assert.That(count, Is.EqualTo(2));
    }

    [Test]
    public void RowCountResetsOnTruncateTest()
    {
        m_engine.Execute("CREATE TABLE T (Id INT)");
        
        for (int i = 0; i < 50; i++)
        {
            m_engine.Execute($"INSERT INTO T (Id) VALUES ({i})");
        }
        
        m_engine.Execute("TRUNCATE TABLE T");
        
        var count = m_engine.GetTableRowCount("T");
        
        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public void RowCountUnchangedOnUpdateTest()
    {
        m_engine.Execute("CREATE TABLE T (Id INT, Value INT)");
        m_engine.Execute("INSERT INTO T (Id, Value) VALUES (1, 100)");
        m_engine.Execute("INSERT INTO T (Id, Value) VALUES (2, 200)");
        
        m_engine.Execute("UPDATE T SET Value = 999 WHERE Id = 1");
        
        var count = m_engine.GetTableRowCount("T");
        
        Assert.That(count, Is.EqualTo(2));
    }

    [Test]
    public void RowCountReturnsMinusOneForNonExistentTableTest()
    {
        var count = m_engine.GetTableRowCount("NonExistent");
        
        Assert.That(count, Is.EqualTo(-1));
    }

    #endregion

    #region COUNT(*) Optimization Tests

    [Test]
    public void CountStarUsesMetadataForSimpleQueryTest()
    {
        m_engine.Execute("CREATE TABLE T (Id INT)");
        
        for (int i = 0; i < 100; i++)
        {
            m_engine.Execute($"INSERT INTO T (Id) VALUES ({i})");
        }
        
        var result = m_engine.ExecuteScalar("SELECT COUNT(*) FROM T");
        
        Assert.That(result.AsInt64(), Is.EqualTo(100));
    }

    [Test]
    public void CountStarWithWhereDoesNotUseOptimizationTest()
    {
        m_engine.Execute("CREATE TABLE T (Id INT)");
        
        for (int i = 0; i < 100; i++)
        {
            m_engine.Execute($"INSERT INTO T (Id) VALUES ({i})");
        }
        
        // WHERE clause should prevent optimization
        var result = m_engine.ExecuteScalar("SELECT COUNT(*) FROM T WHERE Id > 50");
        
        // Should still get correct result (just via scan)
        Assert.That(result.AsInt64(), Is.EqualTo(49)); // 51-99 = 49 rows
    }

    [Test]
    public void CountStarWithGroupByDoesNotUseOptimizationTest()
    {
        m_engine.Execute("CREATE TABLE T (Category VARCHAR(10), Value INT)");
        m_engine.Execute("INSERT INTO T (Category, Value) VALUES ('A', 1)");
        m_engine.Execute("INSERT INTO T (Category, Value) VALUES ('A', 2)");
        m_engine.Execute("INSERT INTO T (Category, Value) VALUES ('B', 3)");
        
        // GROUP BY should prevent optimization
        var rows = m_engine.Query("SELECT Category, COUNT(*) FROM T GROUP BY Category ORDER BY Category");
        
        Assert.That(rows.Count, Is.EqualTo(2));
        Assert.That(rows[0]["Category"].AsString(), Is.EqualTo("A"));
        Assert.That(rows[0][1].AsInt64(), Is.EqualTo(2));
        Assert.That(rows[1]["Category"].AsString(), Is.EqualTo("B"));
        Assert.That(rows[1][1].AsInt64(), Is.EqualTo(1));
    }

    [Test]
    public void CountStarWithAliasTest()
    {
        m_engine.Execute("CREATE TABLE T (Id INT)");
        m_engine.Execute("INSERT INTO T (Id) VALUES (1)");
        m_engine.Execute("INSERT INTO T (Id) VALUES (2)");
        
        var rows = m_engine.Query("SELECT COUNT(*) AS Total FROM T");
        
        Assert.That(rows.Count, Is.EqualTo(1));
        Assert.That(rows[0]["Total"].AsInt64(), Is.EqualTo(2));
    }

    [Test]
    public void CountStarOnEmptyTableTest()
    {
        m_engine.Execute("CREATE TABLE T (Id INT)");
        
        var result = m_engine.ExecuteScalar("SELECT COUNT(*) FROM T");
        
        Assert.That(result.AsInt64(), Is.EqualTo(0));
    }

    [Test]
    public void MultipleAggregatesDoNotUseCountStarOptimizationTest()
    {
        m_engine.Execute("CREATE TABLE T (Id INT, Value INT)");
        
        for (int i = 1; i <= 10; i++)
        {
            m_engine.Execute($"INSERT INTO T (Id, Value) VALUES ({i}, {i * 10})");
        }
        
        // Multiple aggregates should use streaming (not count metadata)
        var rows = m_engine.Query("SELECT COUNT(*), SUM(Value) FROM T");
        
        Assert.That(rows[0][0].AsInt64(), Is.EqualTo(10));
        Assert.That(rows[0][1].AsInt64(), Is.EqualTo(550)); // 10+20+...+100
    }

    [Test]
    public void CountStarWithJoinDoesNotUseOptimizationTest()
    {
        m_engine.Execute("CREATE TABLE A (Id INT)");
        m_engine.Execute("CREATE TABLE B (AId INT)");
        m_engine.Execute("INSERT INTO A (Id) VALUES (1)");
        m_engine.Execute("INSERT INTO A (Id) VALUES (2)");
        m_engine.Execute("INSERT INTO B (AId) VALUES (1)");
        m_engine.Execute("INSERT INTO B (AId) VALUES (1)");
        
        // JOIN should prevent optimization
        var result = m_engine.ExecuteScalar("SELECT COUNT(*) FROM A INNER JOIN B ON A.Id = B.AId");
        
        Assert.That(result.AsInt64(), Is.EqualTo(2));
    }

    [Test]
    public void CountStarWithSubqueryDoesNotUseOptimizationTest()
    {
        m_engine.Execute("CREATE TABLE T (Id INT)");
        m_engine.Execute("INSERT INTO T (Id) VALUES (1)");
        m_engine.Execute("INSERT INTO T (Id) VALUES (2)");
        
        // Subquery in FROM should prevent optimization
        var result = m_engine.ExecuteScalar("SELECT COUNT(*) FROM (SELECT * FROM T) AS Sub");
        
        Assert.That(result.AsInt64(), Is.EqualTo(2));
    }

    #endregion

    #region Transaction Tests

    [Test]
    public void RowCountCorrectAfterMultipleOperationsTest()
    {
        m_engine.Execute("CREATE TABLE T (Id INT)");
        m_engine.Execute("INSERT INTO T (Id) VALUES (1)");
        m_engine.Execute("INSERT INTO T (Id) VALUES (2)");
        m_engine.Execute("INSERT INTO T (Id) VALUES (3)");
        m_engine.Execute("DELETE FROM T WHERE Id = 2");
        
        // Count should be 2
        var count = m_engine.ExecuteScalar("SELECT COUNT(*) FROM T");
        Assert.That(count.AsInt64(), Is.EqualTo(2));
        
        // Verify with full scan
        var scanCount = m_engine.ExecuteScalar("SELECT COUNT(*) FROM T WHERE Id >= 0");
        Assert.That(scanCount.AsInt64(), Is.EqualTo(2));
    }

    [Test]
    public void RowCountMatchesActualDataTest()
    {
        m_engine.Execute("CREATE TABLE T (Id INT)");
        
        // Insert 50 rows
        for (int i = 0; i < 50; i++)
        {
            m_engine.Execute($"INSERT INTO T (Id) VALUES ({i})");
        }
        
        // Delete 10 rows
        m_engine.Execute("DELETE FROM T WHERE Id < 10");
        
        // Optimized count
        var optimizedCount = m_engine.ExecuteScalar("SELECT COUNT(*) FROM T");
        
        // Full scan count (verification)
        var fullScanCount = m_engine.ExecuteScalar("SELECT COUNT(*) FROM T WHERE Id >= 0");
        
        Assert.That(optimizedCount.AsInt64(), Is.EqualTo(40));
        Assert.That(fullScanCount.AsInt64(), Is.EqualTo(40));
    }

    #endregion

    #region Performance Tests

    [Test]
    [Category("Performance")]
    public void CountStarOptimizationIsFastTest()
    {
        m_engine.Execute("CREATE TABLE BigTable (Id BIGINT PRIMARY KEY AUTOINCREMENT, Data VARCHAR(100))");
        
        const int rowCount = 10000;
        m_engine.Execute("BEGIN TRANSACTION");
        for (int i = 0; i < rowCount; i++)
        {
            m_engine.Execute($"INSERT INTO BigTable (Data) VALUES ('Row {i}')");
        }
        m_engine.Execute("COMMIT");

        // Optimized COUNT(*) should be nearly instant
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var count = m_engine.ExecuteScalar("SELECT COUNT(*) FROM BigTable");
        sw.Stop();

        Console.WriteLine($"COUNT(*) on {rowCount} rows:");
        Console.WriteLine($"  Result: {count.AsInt64()}");
        Console.WriteLine($"  Time: {sw.ElapsedMilliseconds} ms");

        Assert.That(count.AsInt64(), Is.EqualTo(rowCount));
        
        // Optimized COUNT(*) should be < 1ms
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(10), 
            "Optimized COUNT(*) should be nearly instant");
    }

    [Test]
    [Category("Performance")]
    public void CountStarOptimizationVsFullScanTest()
    {
        m_engine.Execute("CREATE TABLE TestTable (Id BIGINT PRIMARY KEY AUTOINCREMENT, Value INT)");
        
        const int rowCount = 5000;
        m_engine.Execute("BEGIN TRANSACTION");
        for (int i = 0; i < rowCount; i++)
        {
            m_engine.Execute($"INSERT INTO TestTable (Value) VALUES ({i})");
        }
        m_engine.Execute("COMMIT");

        // Optimized path: COUNT(*) without WHERE
        var sw1 = System.Diagnostics.Stopwatch.StartNew();
        var count1 = m_engine.ExecuteScalar("SELECT COUNT(*) FROM TestTable");
        sw1.Stop();

        // Non-optimized path: COUNT(*) with WHERE (forces scan)
        var sw2 = System.Diagnostics.Stopwatch.StartNew();
        var count2 = m_engine.ExecuteScalar("SELECT COUNT(*) FROM TestTable WHERE Value >= 0");
        sw2.Stop();

        Console.WriteLine($"Optimized COUNT(*): {sw1.ElapsedMilliseconds}ms");
        Console.WriteLine($"Full scan COUNT(*): {sw2.ElapsedMilliseconds}ms");
        Console.WriteLine($"Speedup: {(double)sw2.ElapsedMilliseconds / Math.Max(1, sw1.ElapsedMilliseconds):F1}x");

        Assert.That(count1.AsInt64(), Is.EqualTo(rowCount));
        Assert.That(count2.AsInt64(), Is.EqualTo(rowCount));
        
        // Optimized should be significantly faster
        Assert.That(sw1.ElapsedMilliseconds, Is.LessThan(sw2.ElapsedMilliseconds));
    }

    #endregion
}

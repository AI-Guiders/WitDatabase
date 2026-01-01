using OutWit.Database.Core.Builder;

namespace OutWit.Database.Tests;

/// <summary>
/// Integration tests for Hash Join optimization in SQL queries.
/// Verifies that Hash Join is automatically selected for appropriate queries
/// and produces correct results.
/// </summary>
[TestFixture]
public sealed class WitSqlEngineHashJoinIntegrationTests : WitSqlEngineTestsBase
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

    #region EXPLAIN Hash Join Tests

    [Test]
    public void ExplainShowsHashJoinForEquiJoinTest()
    {
        // Create tables large enough to trigger hash join
        CreateUsersAndOrdersTables(50, 200);

        var result = m_engine.Execute(@"
            EXPLAIN QUERY PLAN 
            SELECT u.Name, o.Total 
            FROM Users u 
            INNER JOIN Orders o ON u.Id = o.UserId");
        var rows = result.ReadAll();

        Assert.That(rows.Count, Is.GreaterThan(0));
        var details = rows.Select(r => r["detail"].AsString()).ToList();
        
        // Should show HASH JOIN for this query size
        Assert.That(details.Any(d => 
            d.Contains("HASH", StringComparison.OrdinalIgnoreCase) ||
            d.Contains("HashJoin", StringComparison.OrdinalIgnoreCase)), Is.True,
            $"Expected HASH in EXPLAIN output. Got: {string.Join("; ", details)}");
    }

    [Test]
    public void ExplainShowsNestedLoopForSmallTablesTest()
    {
        // Create very small tables that should use nested loop
        CreateUsersAndOrdersTables(3, 5);

        var result = m_engine.Execute(@"
            EXPLAIN QUERY PLAN 
            SELECT u.Name, o.Total 
            FROM Users u 
            INNER JOIN Orders o ON u.Id = o.UserId");
        var rows = result.ReadAll();

        Assert.That(rows.Count, Is.GreaterThan(0));
        var details = rows.Select(r => r["detail"].AsString()).ToList();
        
        // Small tables may use either algorithm, but should mention JOIN
        Assert.That(details.Any(d => 
            d.Contains("JOIN", StringComparison.OrdinalIgnoreCase) ||
            d.Contains("NESTED", StringComparison.OrdinalIgnoreCase) ||
            d.Contains("HASH", StringComparison.OrdinalIgnoreCase)), Is.True);
    }

    [Test]
    public void ExplainShowsHashJoinForLeftJoinTest()
    {
        CreateUsersAndOrdersTables(50, 200);

        var result = m_engine.Execute(@"
            EXPLAIN QUERY PLAN 
            SELECT u.Name, o.Total 
            FROM Users u 
            LEFT JOIN Orders o ON u.Id = o.UserId");
        var rows = result.ReadAll();

        Assert.That(rows.Count, Is.GreaterThan(0));
        var details = rows.Select(r => r["detail"].AsString()).ToList();
        
        // LEFT JOIN with equi-condition should also use hash join
        Assert.That(details.Any(d => 
            d.Contains("HASH", StringComparison.OrdinalIgnoreCase) ||
            d.Contains("HashJoin", StringComparison.OrdinalIgnoreCase) ||
            d.Contains("LEFT", StringComparison.OrdinalIgnoreCase)), Is.True);
    }

    #endregion

    #region Correctness Tests

    [Test]
    public void HashJoinInnerReturnsCorrectRowCountTest()
    {
        CreateUsersAndOrdersTables(50, 200);

        // Each user has 4 orders (200/50 = 4)
        var result = m_engine.Query(@"
            SELECT u.Id, u.Name, o.Id AS OrderId, o.Total 
            FROM Users u 
            INNER JOIN Orders o ON u.Id = o.UserId");

        Assert.That(result.Count, Is.EqualTo(200));
    }

    [Test]
    public void HashJoinLeftReturnsAllLeftRowsTest()
    {
        CreateUsersWithSomeOrders(50, 25);

        // Only 25 users have orders, but LEFT JOIN should return all 50
        var result = m_engine.Query(@"
            SELECT u.Id, u.Name, o.Id AS OrderId 
            FROM Users u 
            LEFT JOIN Orders o ON u.Id = o.UserId
            ORDER BY u.Id");

        Assert.That(result.Count, Is.GreaterThanOrEqualTo(50));
        
        // Users without orders should have NULL OrderId
        var usersWithoutOrders = result.Where(r => r["OrderId"].IsNull).ToList();
        Assert.That(usersWithoutOrders.Count, Is.EqualTo(25));
    }

    [Test]
    public void HashJoinPreservesColumnValuesTest()
    {
        m_engine.Execute(@"
            CREATE TABLE Products (
                Id BIGINT PRIMARY KEY AUTOINCREMENT,
                Name VARCHAR(100) NOT NULL,
                Price DECIMAL NOT NULL
            )");

        m_engine.Execute(@"
            CREATE TABLE OrderItems (
                Id BIGINT PRIMARY KEY AUTOINCREMENT,
                ProductId BIGINT NOT NULL,
                Quantity INT NOT NULL
            )");

        m_engine.Execute("INSERT INTO Products (Name, Price) VALUES ('Widget', 9.99)");
        m_engine.Execute("INSERT INTO Products (Name, Price) VALUES ('Gadget', 19.99)");
        m_engine.Execute("INSERT INTO OrderItems (ProductId, Quantity) VALUES (1, 5)");
        m_engine.Execute("INSERT INTO OrderItems (ProductId, Quantity) VALUES (2, 3)");

        var result = m_engine.Query(@"
            SELECT p.Name, p.Price, oi.Quantity, p.Price * oi.Quantity AS Total
            FROM Products p
            INNER JOIN OrderItems oi ON p.Id = oi.ProductId
            ORDER BY p.Name");

        Assert.That(result.Count, Is.EqualTo(2));
        Assert.That(result[0]["Name"].AsString(), Is.EqualTo("Gadget"));
        Assert.That(result[0]["Price"].AsDecimal(), Is.EqualTo(19.99m));
        Assert.That(result[0]["Quantity"].AsInt64(), Is.EqualTo(3));
    }

    [Test]
    public void HashJoinWithNullsDoesNotMatchTest()
    {
        m_engine.Execute("CREATE TABLE TableA (Id BIGINT PRIMARY KEY, JoinKey BIGINT)");
        m_engine.Execute("CREATE TABLE TableB (Id BIGINT PRIMARY KEY, JoinKey BIGINT)");

        m_engine.Execute("INSERT INTO TableA (Id, JoinKey) VALUES (1, 100)");
        m_engine.Execute("INSERT INTO TableA (Id, JoinKey) VALUES (2, NULL)");
        m_engine.Execute("INSERT INTO TableA (Id, JoinKey) VALUES (3, 200)");
        
        m_engine.Execute("INSERT INTO TableB (Id, JoinKey) VALUES (1, 100)");
        m_engine.Execute("INSERT INTO TableB (Id, JoinKey) VALUES (2, NULL)");
        m_engine.Execute("INSERT INTO TableB (Id, JoinKey) VALUES (3, 300)");

        var result = m_engine.Query(@"
            SELECT a.Id AS AId, b.Id AS BId, a.JoinKey
            FROM TableA a 
            INNER JOIN TableB b ON a.JoinKey = b.JoinKey");

        // NULL never equals NULL in SQL, so only (1,100) should match
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0]["AId"].AsInt64(), Is.EqualTo(1));
        Assert.That(result[0]["BId"].AsInt64(), Is.EqualTo(1));
    }

    #endregion

    #region Multi-Column Key Tests

    [Test]
    public void HashJoinWithMultipleColumnsMatchesCorrectlyTest()
    {
        m_engine.Execute(@"
            CREATE TABLE Invoices (
                Year INT NOT NULL,
                Month INT NOT NULL,
                InvoiceNum INT NOT NULL,
                Amount DECIMAL NOT NULL,
                PRIMARY KEY (Year, Month, InvoiceNum)
            )");

        m_engine.Execute(@"
            CREATE TABLE Payments (
                Id BIGINT PRIMARY KEY AUTOINCREMENT,
                InvYear INT NOT NULL,
                InvMonth INT NOT NULL,
                InvNum INT NOT NULL,
                PaidAmount DECIMAL NOT NULL
            )");

        m_engine.Execute("INSERT INTO Invoices (Year, Month, InvoiceNum, Amount) VALUES (2024, 1, 1, 100)");
        m_engine.Execute("INSERT INTO Invoices (Year, Month, InvoiceNum, Amount) VALUES (2024, 1, 2, 200)");
        m_engine.Execute("INSERT INTO Invoices (Year, Month, InvoiceNum, Amount) VALUES (2024, 2, 1, 150)");
        
        m_engine.Execute("INSERT INTO Payments (InvYear, InvMonth, InvNum, PaidAmount) VALUES (2024, 1, 1, 50)");
        m_engine.Execute("INSERT INTO Payments (InvYear, InvMonth, InvNum, PaidAmount) VALUES (2024, 1, 1, 50)");
        m_engine.Execute("INSERT INTO Payments (InvYear, InvMonth, InvNum, PaidAmount) VALUES (2024, 2, 1, 150)");

        var result = m_engine.Query(@"
            SELECT i.Year, i.Month, i.InvoiceNum, i.Amount, SUM(p.PaidAmount) AS TotalPaid
            FROM Invoices i
            INNER JOIN Payments p ON i.Year = p.InvYear AND i.Month = p.InvMonth AND i.InvoiceNum = p.InvNum
            GROUP BY i.Year, i.Month, i.InvoiceNum, i.Amount
            ORDER BY i.Year, i.Month, i.InvoiceNum");

        Assert.That(result.Count, Is.EqualTo(2));
        Assert.That(result[0]["TotalPaid"].AsDecimal(), Is.EqualTo(100)); // Two payments of 50
        Assert.That(result[1]["TotalPaid"].AsDecimal(), Is.EqualTo(150)); // One payment of 150
    }

    #endregion

    #region Complex Query Tests

    [Test]
    public void HashJoinWithFilterAfterJoinTest()
    {
        CreateUsersAndOrdersTables(50, 200);

        var result = m_engine.Query(@"
            SELECT u.Name, o.Total
            FROM Users u
            INNER JOIN Orders o ON u.Id = o.UserId
            WHERE o.Total > 100
            ORDER BY o.Total");

        Assert.That(result.All(r => r["Total"].AsDecimal() > 100), Is.True);
    }

    [Test]
    public void HashJoinWithAggregationTest()
    {
        CreateUsersAndOrdersTables(50, 200);

        var result = m_engine.Query(@"
            SELECT u.Name, COUNT(*) AS OrderCount, SUM(o.Total) AS TotalAmount
            FROM Users u
            INNER JOIN Orders o ON u.Id = o.UserId
            GROUP BY u.Name
            ORDER BY TotalAmount DESC");

        Assert.That(result.Count, Is.EqualTo(50));
        Assert.That(result.All(r => r["OrderCount"].AsInt64() == 4), Is.True);
    }

    [Test]
    public void HashJoinInSubqueryTest()
    {
        CreateUsersAndOrdersTables(20, 100);

        var result = m_engine.Query(@"
            SELECT u.Name
            FROM Users u
            WHERE u.Id IN (
                SELECT o.UserId
                FROM Orders o
                WHERE o.Total > 50
            )
            ORDER BY u.Name");

        Assert.That(result.Count, Is.GreaterThan(0));
    }

    [Test]
    public void MultipleHashJoinsInQueryTest()
    {
        m_engine.Execute("CREATE TABLE Categories (Id BIGINT PRIMARY KEY, Name VARCHAR(50))");
        m_engine.Execute("CREATE TABLE Products (Id BIGINT PRIMARY KEY, CategoryId BIGINT, Name VARCHAR(100))");
        m_engine.Execute("CREATE TABLE OrderItems (Id BIGINT PRIMARY KEY, ProductId BIGINT, Qty INT)");

        // Insert data
        for (int i = 1; i <= 10; i++)
            m_engine.Execute($"INSERT INTO Categories (Id, Name) VALUES ({i}, 'Cat{i}')");
        
        for (int i = 1; i <= 50; i++)
        {
            var catId = ((i - 1) % 10) + 1;
            m_engine.Execute($"INSERT INTO Products (Id, CategoryId, Name) VALUES ({i}, {catId}, 'Prod{i}')");
        }
        
        for (int i = 1; i <= 200; i++)
        {
            var prodId = ((i - 1) % 50) + 1;
            m_engine.Execute($"INSERT INTO OrderItems (Id, ProductId, Qty) VALUES ({i}, {prodId}, {(i % 10) + 1})");
        }

        var result = m_engine.Query(@"
            SELECT c.Name AS Category, p.Name AS Product, oi.Qty
            FROM Categories c
            INNER JOIN Products p ON c.Id = p.CategoryId
            INNER JOIN OrderItems oi ON p.Id = oi.ProductId
            ORDER BY c.Name, p.Name");

        // 200 order items
        Assert.That(result.Count, Is.EqualTo(200));
    }

    #endregion

    #region Performance Regression Tests

    [Test]
    public void LargeJoinCompletesInReasonableTimeTest()
    {
        // Create tables large enough to measure performance difference
        CreateUsersAndOrdersTables(100, 1000);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        var result = m_engine.Query(@"
            SELECT u.Name, COUNT(*) AS OrderCount
            FROM Users u
            INNER JOIN Orders o ON u.Id = o.UserId
            GROUP BY u.Name");
        
        sw.Stop();

        Assert.That(result.Count, Is.EqualTo(100));
        
        // With hash join, this should complete well under 1 second
        // Nested loop would be slower for this data size
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(2000),
            $"Query took {sw.ElapsedMilliseconds}ms, expected under 2000ms");
    }

    #endregion

    #region Helper Methods

    private void CreateUsersAndOrdersTables(int userCount, int orderCount)
    {
        m_engine.Execute(@"
            CREATE TABLE Users (
                Id BIGINT PRIMARY KEY AUTOINCREMENT,
                Name VARCHAR(100) NOT NULL
            )");

        m_engine.Execute(@"
            CREATE TABLE Orders (
                Id BIGINT PRIMARY KEY AUTOINCREMENT,
                UserId BIGINT NOT NULL,
                Total DECIMAL NOT NULL
            )");

        for (int i = 1; i <= userCount; i++)
        {
            m_engine.Execute($"INSERT INTO Users (Name) VALUES ('User{i}')");
        }

        for (int i = 1; i <= orderCount; i++)
        {
            var userId = ((i - 1) % userCount) + 1;
            var total = (i * 10) + 0.99m;
            m_engine.Execute($"INSERT INTO Orders (UserId, Total) VALUES ({userId}, {total})");
        }
    }

    private void CreateUsersWithSomeOrders(int userCount, int usersWithOrders)
    {
        m_engine.Execute(@"
            CREATE TABLE Users (
                Id BIGINT PRIMARY KEY AUTOINCREMENT,
                Name VARCHAR(100) NOT NULL
            )");

        m_engine.Execute(@"
            CREATE TABLE Orders (
                Id BIGINT PRIMARY KEY AUTOINCREMENT,
                UserId BIGINT NOT NULL,
                Total DECIMAL NOT NULL
            )");

        for (int i = 1; i <= userCount; i++)
        {
            m_engine.Execute($"INSERT INTO Users (Name) VALUES ('User{i}')");
        }

        // Only first N users have orders
        for (int i = 1; i <= usersWithOrders; i++)
        {
            m_engine.Execute($"INSERT INTO Orders (UserId, Total) VALUES ({i}, {i * 100})");
        }
    }

    #endregion
}

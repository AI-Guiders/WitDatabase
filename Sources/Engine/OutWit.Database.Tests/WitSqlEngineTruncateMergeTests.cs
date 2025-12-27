using NUnit.Framework;

namespace OutWit.Database.Tests;

/// <summary>
/// Tests for TRUNCATE TABLE and MERGE statements.
/// </summary>
[TestFixture]
public class WitSqlEngineTruncateMergeTests : WitSqlEngineTestsBase
{
    [SetUp]
    public override void Setup()
    {
        base.Setup();
        CreateTestTables();
        InsertTestData();
    }

    private void CreateTestTables()
    {
        // Target table for MERGE tests
        m_engine.Execute(@"
            CREATE TABLE Products (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Sku TEXT UNIQUE NOT NULL,
                Name TEXT NOT NULL,
                Price DECIMAL(10,2) DEFAULT 0,
                Stock INTEGER DEFAULT 0
            )");

        // Source table for MERGE tests
        m_engine.Execute(@"
            CREATE TABLE ProductUpdates (
                Sku TEXT NOT NULL,
                Name TEXT NOT NULL,
                Price DECIMAL(10,2),
                Stock INTEGER
            )");

        // Table with index for TRUNCATE tests
        m_engine.Execute(@"
            CREATE TABLE Orders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProductId INTEGER,
                Quantity INTEGER,
                Status TEXT DEFAULT 'pending'
            )");

        m_engine.Execute("CREATE INDEX IX_Orders_ProductId ON Orders (ProductId)");
        m_engine.Execute("CREATE INDEX IX_Orders_Status ON Orders (Status)");
    }

    private void InsertTestData()
    {
        // Products
        m_engine.Execute("INSERT INTO Products (Sku, Name, Price, Stock) VALUES ('SKU001', 'Widget', 29.99, 100)");
        m_engine.Execute("INSERT INTO Products (Sku, Name, Price, Stock) VALUES ('SKU002', 'Gadget', 49.99, 50)");
        m_engine.Execute("INSERT INTO Products (Sku, Name, Price, Stock) VALUES ('SKU003', 'Gizmo', 19.99, 200)");

        // Orders
        m_engine.Execute("INSERT INTO Orders (ProductId, Quantity, Status) VALUES (1, 5, 'shipped')");
        m_engine.Execute("INSERT INTO Orders (ProductId, Quantity, Status) VALUES (2, 3, 'pending')");
        m_engine.Execute("INSERT INTO Orders (ProductId, Quantity, Status) VALUES (1, 10, 'pending')");
        m_engine.Execute("INSERT INTO Orders (ProductId, Quantity, Status) VALUES (3, 7, 'delivered')");
    }

    #region TRUNCATE Tests

    [Test]
    public void TruncateRemovesAllRowsTest()
    {
        // Verify initial data
        var beforeResult = m_engine.Execute("SELECT COUNT(*) FROM Products");
        beforeResult.Read();
        Assert.That(beforeResult.CurrentRow[0].AsInt64(), Is.EqualTo(3));

        // TRUNCATE
        var truncateResult = m_engine.Execute("TRUNCATE TABLE Products");
        Assert.That(truncateResult.RowsAffected, Is.EqualTo(0)); // TRUNCATE returns 0

        // Verify all rows removed
        var afterResult = m_engine.Execute("SELECT COUNT(*) FROM Products");
        afterResult.Read();
        Assert.That(afterResult.CurrentRow[0].AsInt64(), Is.EqualTo(0));
    }

    [Test]
    public void TruncateResetsAutoIncrementTest()
    {
        // Insert some rows
        m_engine.Execute("INSERT INTO Products (Sku, Name, Price, Stock) VALUES ('SKU004', 'NewProduct', 99.99, 10)");
        
        // Verify Id > 3
        var maxIdBefore = m_engine.Execute("SELECT MAX(Id) FROM Products");
        maxIdBefore.Read();
        Assert.That(maxIdBefore.CurrentRow[0].AsInt64(), Is.GreaterThanOrEqualTo(4));

        // TRUNCATE
        m_engine.Execute("TRUNCATE TABLE Products");

        // Insert new row - should get Id = 1
        m_engine.Execute("INSERT INTO Products (Sku, Name, Price, Stock) VALUES ('SKU001', 'Widget', 29.99, 100)");
        
        var newIdResult = m_engine.Execute("SELECT Id FROM Products WHERE Sku = 'SKU001'");
        newIdResult.Read();
        Assert.That(newIdResult.CurrentRow[0].AsInt64(), Is.EqualTo(1));
    }

    [Test]
    public void TruncateClearsSecondaryIndexesTest()
    {
        // TRUNCATE the Orders table (has indexes)
        m_engine.Execute("TRUNCATE TABLE Orders");

        // Verify all rows removed
        var afterResult = m_engine.Execute("SELECT COUNT(*) FROM Orders");
        afterResult.Read();
        Assert.That(afterResult.CurrentRow[0].AsInt64(), Is.EqualTo(0));

        // Insert new rows - indexes should work correctly
        m_engine.Execute("INSERT INTO Orders (ProductId, Quantity, Status) VALUES (1, 2, 'new')");
        
        // Query using indexed column should work
        var queryResult = m_engine.Execute("SELECT * FROM Orders WHERE Status = 'new'");
        var rows = queryResult.ReadAll();
        Assert.That(rows.Count, Is.EqualTo(1));
    }

    [Test]
    public void TruncateNonExistentTableThrowsTest()
    {
        Assert.Throws<InvalidOperationException>(() =>
            m_engine.Execute("TRUNCATE TABLE NonExistentTable"));
    }

    [Test]
    public void TruncateEmptyTableSucceedsTest()
    {
        // First truncate to make sure it's empty
        m_engine.Execute("TRUNCATE TABLE Products");

        // Truncate again - should not throw
        Assert.DoesNotThrow(() =>
            m_engine.Execute("TRUNCATE TABLE Products"));
    }

    #endregion

    #region MERGE Tests - WHEN MATCHED UPDATE

    [Test]
    public void MergeWhenMatchedUpdateTest()
    {
        // Add source data
        m_engine.Execute("INSERT INTO ProductUpdates (Sku, Name, Price, Stock) VALUES ('SKU001', 'Widget Pro', 39.99, 150)");

        // MERGE - update matching row
        var mergeResult = m_engine.Execute(@"
            MERGE INTO Products AS t
            USING ProductUpdates AS s
            ON t.Sku = s.Sku
            WHEN MATCHED THEN
                UPDATE SET Name = s.Name, Price = s.Price, Stock = s.Stock");

        Assert.That(mergeResult.RowsAffected, Is.EqualTo(1));

        // Verify update
        var result = m_engine.Execute("SELECT Name, Price, Stock FROM Products WHERE Sku = 'SKU001'");
        result.Read();
        Assert.That(result.CurrentRow[0].AsString(), Is.EqualTo("Widget Pro"));
        Assert.That(result.CurrentRow[1].AsDecimal(), Is.EqualTo(39.99m));
        Assert.That(result.CurrentRow[2].AsInt64(), Is.EqualTo(150));
    }

    [Test]
    public void MergeWhenMatchedUpdateWithConditionTest()
    {
        // Add source data - one match with low stock, one with high stock
        m_engine.Execute("INSERT INTO ProductUpdates (Sku, Name, Price, Stock) VALUES ('SKU001', 'Widget New', 35.00, 10)");  // Stock < current
        m_engine.Execute("INSERT INTO ProductUpdates (Sku, Name, Price, Stock) VALUES ('SKU002', 'Gadget New', 55.00, 200)"); // Stock > current

        // MERGE - only update if source stock > target stock
        var mergeResult = m_engine.Execute(@"
            MERGE INTO Products AS t
            USING ProductUpdates AS s
            ON t.Sku = s.Sku
            WHEN MATCHED AND s.Stock > t.Stock THEN
                UPDATE SET Name = s.Name, Price = s.Price, Stock = s.Stock");

        Assert.That(mergeResult.RowsAffected, Is.EqualTo(1)); // Only SKU002 updated

        // SKU001 should be unchanged
        var result1 = m_engine.Execute("SELECT Name, Stock FROM Products WHERE Sku = 'SKU001'");
        result1.Read();
        Assert.That(result1.CurrentRow[0].AsString(), Is.EqualTo("Widget"));

        // SKU002 should be updated
        var result2 = m_engine.Execute("SELECT Name, Stock FROM Products WHERE Sku = 'SKU002'");
        result2.Read();
        Assert.That(result2.CurrentRow[0].AsString(), Is.EqualTo("Gadget New"));
        Assert.That(result2.CurrentRow[1].AsInt64(), Is.EqualTo(200));
    }

    #endregion

    #region MERGE Tests - WHEN NOT MATCHED INSERT

    [Test]
    public void MergeWhenNotMatchedInsertTest()
    {
        // Add source data - one existing, one new
        m_engine.Execute("INSERT INTO ProductUpdates (Sku, Name, Price, Stock) VALUES ('SKU001', 'Widget', 29.99, 100)");  // Exists
        m_engine.Execute("INSERT INTO ProductUpdates (Sku, Name, Price, Stock) VALUES ('SKU004', 'NewItem', 79.99, 25)");  // New

        // MERGE - insert non-matching rows
        var mergeResult = m_engine.Execute(@"
            MERGE INTO Products AS t
            USING ProductUpdates AS s
            ON t.Sku = s.Sku
            WHEN NOT MATCHED THEN
                INSERT (Sku, Name, Price, Stock) VALUES (s.Sku, s.Name, s.Price, s.Stock)");

        Assert.That(mergeResult.RowsAffected, Is.EqualTo(1)); // Only SKU004 inserted

        // Verify new row inserted
        var result = m_engine.Execute("SELECT * FROM Products WHERE Sku = 'SKU004'");
        result.Read();
        Assert.That(result.CurrentRow["Name"].AsString(), Is.EqualTo("NewItem"));
        Assert.That(result.CurrentRow["Price"].AsDecimal(), Is.EqualTo(79.99m));
        Assert.That(result.CurrentRow["Stock"].AsInt64(), Is.EqualTo(25));
    }

    #endregion

    #region MERGE Tests - WHEN MATCHED DELETE

    [Test]
    public void MergeWhenMatchedDeleteTest()
    {
        // Add source data indicating items to delete
        m_engine.Execute("INSERT INTO ProductUpdates (Sku, Name, Price, Stock) VALUES ('SKU003', 'Gizmo', 0, 0)");

        // MERGE - delete matching rows
        var mergeResult = m_engine.Execute(@"
            MERGE INTO Products AS t
            USING ProductUpdates AS s
            ON t.Sku = s.Sku
            WHEN MATCHED THEN DELETE");

        Assert.That(mergeResult.RowsAffected, Is.EqualTo(1));

        // Verify row deleted
        var result = m_engine.Execute("SELECT COUNT(*) FROM Products WHERE Sku = 'SKU003'");
        result.Read();
        Assert.That(result.CurrentRow[0].AsInt64(), Is.EqualTo(0));

        // Other rows should remain
        var totalResult = m_engine.Execute("SELECT COUNT(*) FROM Products");
        totalResult.Read();
        Assert.That(totalResult.CurrentRow[0].AsInt64(), Is.EqualTo(2));
    }

    #endregion

    #region MERGE Tests - Multiple WHEN Clauses

    [Test]
    public void MergeMultipleWhenClausesTest()
    {
        // Add source data - mix of existing and new
        m_engine.Execute("INSERT INTO ProductUpdates (Sku, Name, Price, Stock) VALUES ('SKU001', 'Widget Updated', 34.99, 120)"); // Update
        m_engine.Execute("INSERT INTO ProductUpdates (Sku, Name, Price, Stock) VALUES ('SKU005', 'Brand New', 99.99, 10)");       // Insert

        // MERGE with both WHEN MATCHED and WHEN NOT MATCHED
        var mergeResult = m_engine.Execute(@"
            MERGE INTO Products AS t
            USING ProductUpdates AS s
            ON t.Sku = s.Sku
            WHEN MATCHED THEN
                UPDATE SET Name = s.Name, Price = s.Price, Stock = s.Stock
            WHEN NOT MATCHED THEN
                INSERT (Sku, Name, Price, Stock) VALUES (s.Sku, s.Name, s.Price, s.Stock)");

        Assert.That(mergeResult.RowsAffected, Is.EqualTo(2)); // 1 update + 1 insert

        // Verify update
        var updateResult = m_engine.Execute("SELECT Name, Price FROM Products WHERE Sku = 'SKU001'");
        updateResult.Read();
        Assert.That(updateResult.CurrentRow[0].AsString(), Is.EqualTo("Widget Updated"));
        Assert.That(updateResult.CurrentRow[1].AsDecimal(), Is.EqualTo(34.99m));

        // Verify insert
        var insertResult = m_engine.Execute("SELECT Name, Price FROM Products WHERE Sku = 'SKU005'");
        var insertRows = insertResult.ReadAll();
        Assert.That(insertRows.Count, Is.EqualTo(1));
        Assert.That(insertRows[0][0].AsString(), Is.EqualTo("Brand New"));
    }

    [Test]
    public void MergeWithSubquerySourceTest()
    {
        // Create a staging table
        m_engine.Execute(@"
            CREATE TABLE StagingProducts (
                Sku TEXT NOT NULL,
                Name TEXT NOT NULL,
                Price DECIMAL(10,2),
                Stock INTEGER,
                IsActive BOOLEAN DEFAULT TRUE
            )");

        m_engine.Execute("INSERT INTO StagingProducts (Sku, Name, Price, Stock, IsActive) VALUES ('SKU001', 'Widget V2', 31.99, 110, TRUE)");
        m_engine.Execute("INSERT INTO StagingProducts (Sku, Name, Price, Stock, IsActive) VALUES ('SKU006', 'Inactive', 0, 0, FALSE)");
        m_engine.Execute("INSERT INTO StagingProducts (Sku, Name, Price, Stock, IsActive) VALUES ('SKU007', 'Active New', 44.99, 30, TRUE)");

        // MERGE using subquery - only active products
        var mergeResult = m_engine.Execute(@"
            MERGE INTO Products AS t
            USING (SELECT Sku, Name, Price, Stock FROM StagingProducts WHERE IsActive = TRUE) AS s
            ON t.Sku = s.Sku
            WHEN MATCHED THEN
                UPDATE SET Name = s.Name, Price = s.Price, Stock = s.Stock
            WHEN NOT MATCHED THEN
                INSERT (Sku, Name, Price, Stock) VALUES (s.Sku, s.Name, s.Price, s.Stock)");

        Assert.That(mergeResult.RowsAffected, Is.EqualTo(2)); // SKU001 update, SKU007 insert

        // Verify SKU001 updated
        var result1 = m_engine.Execute("SELECT Name FROM Products WHERE Sku = 'SKU001'");
        result1.Read();
        Assert.That(result1.CurrentRow[0].AsString(), Is.EqualTo("Widget V2"));

        // Verify SKU007 inserted
        var result2 = m_engine.Execute("SELECT COUNT(*) FROM Products WHERE Sku = 'SKU007'");
        result2.Read();
        Assert.That(result2.CurrentRow[0].AsInt64(), Is.EqualTo(1));

        // Verify SKU006 NOT inserted (IsActive = FALSE)
        var result3 = m_engine.Execute("SELECT COUNT(*) FROM Products WHERE Sku = 'SKU006'");
        result3.Read();
        Assert.That(result3.CurrentRow[0].AsInt64(), Is.EqualTo(0));
    }

    #endregion

    #region MERGE Tests - Error Cases

    [Test]
    public void MergeTargetTableNotFoundThrowsTest()
    {
        m_engine.Execute("INSERT INTO ProductUpdates (Sku, Name, Price, Stock) VALUES ('SKU001', 'Widget', 29.99, 100)");

        Assert.Throws<InvalidOperationException>(() =>
            m_engine.Execute(@"
                MERGE INTO NonExistentTable AS t
                USING ProductUpdates AS s
                ON t.Sku = s.Sku
                WHEN MATCHED THEN
                    UPDATE SET Name = s.Name"));
    }

    [Test]
    public void MergeNoMatchingWhenClauseTest()
    {
        // Add source data that exists
        m_engine.Execute("INSERT INTO ProductUpdates (Sku, Name, Price, Stock) VALUES ('SKU001', 'Widget', 29.99, 100)");

        // MERGE with only WHEN NOT MATCHED - existing row won't be affected
        var mergeResult = m_engine.Execute(@"
            MERGE INTO Products AS t
            USING ProductUpdates AS s
            ON t.Sku = s.Sku
            WHEN NOT MATCHED THEN
                INSERT (Sku, Name, Price, Stock) VALUES (s.Sku, s.Name, s.Price, s.Stock)");

        Assert.That(mergeResult.RowsAffected, Is.EqualTo(0)); // No changes - row exists
    }

    #endregion
}

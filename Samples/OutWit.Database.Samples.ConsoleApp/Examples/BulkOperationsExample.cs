using OutWit.Database.Core.Builder;
using OutWit.Database.Engine;
using System.Diagnostics;

namespace OutWit.Database.Samples.ConsoleApp.Examples;

/// <summary>
/// Demonstrates bulk operations and performance optimizations.
/// </summary>
public static class BulkOperationsExample
{
    #region Run

    public static async Task RunAsync()
    {
        Console.WriteLine("=== Bulk Operations Example ===");
        Console.WriteLine();

        var database = new WitDatabaseBuilder()
            .WithMemoryStorage()
            .WithBTree()
            .WithTransactions()
            .Build();

        using var engine = new WitSqlEngine(database, ownsStore: true);

        // Create products table
        Console.WriteLine("1. Creating 'Products' table with indexes...");
        engine.Execute("""
            CREATE TABLE Products (
                Id BIGINT PRIMARY KEY AUTOINCREMENT,
                SKU VARCHAR(50) NOT NULL,
                Name VARCHAR(200) NOT NULL,
                Category VARCHAR(50) NOT NULL,
                Price DECIMAL(10, 2) NOT NULL,
                Stock INT NOT NULL DEFAULT 0,
                CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
            )
            """);

        // Create indexes for better query performance
        engine.Execute("CREATE UNIQUE INDEX IX_Products_SKU ON Products(SKU)");
        engine.Execute("CREATE INDEX IX_Products_Category ON Products(Category)");
        Console.WriteLine("   [OK] Table and indexes created");
        Console.WriteLine();

        // === Bulk Insert with Transaction ===
        Console.WriteLine("2. Bulk inserting 5000 products (in transaction)...");
        var stopwatch = Stopwatch.StartNew();
        
        engine.Execute("BEGIN TRANSACTION");
        for (int i = 0; i < 5000; i++)
        {
            var category = (i % 5) switch
            {
                0 => "Electronics",
                1 => "Clothing",
                2 => "Books",
                3 => "Home & Garden",
                _ => "Sports"
            };
            
            engine.Execute(
                "INSERT INTO Products (SKU, Name, Category, Price, Stock) VALUES (@sku, @name, @category, @price, @stock)",
                new Dictionary<string, object?>
                {
                    { "@sku", $"SKU-{i:D6}" },
                    { "@name", $"Product {i} - {category}" },
                    { "@category", category },
                    { "@price", 10.00m + (i % 100) * 5.50m },
                    { "@stock", 100 + (i % 50) }
                });
        }
        engine.Execute("COMMIT");
        
        stopwatch.Stop();
        Console.WriteLine($"   [OK] Inserted 5000 rows in {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"   [OK] Rate: {5000.0 / stopwatch.Elapsed.TotalSeconds:F0} rows/sec");
        Console.WriteLine();

        // === Aggregation Queries ===
        Console.WriteLine("3. Running aggregation queries...");
        Console.WriteLine();
        
        using (var result = engine.Execute("""
            SELECT 
                Category,
                COUNT(*) AS ProductCount,
                AVG(Price) AS AvgPrice,
                SUM(Stock) AS TotalStock
            FROM Products
            GROUP BY Category
            ORDER BY ProductCount DESC
            """))
        {
            Console.WriteLine("   Category       | Count  | Avg Price  | Total Stock");
            Console.WriteLine("   ---------------|--------|------------|------------");
            while (result.Read())
            {
                Console.WriteLine($"   {result.CurrentRow["Category"],-15}| {result.CurrentRow["ProductCount"],6} | ${result.CurrentRow["AvgPrice"],9:N2} | {result.CurrentRow["TotalStock"],10:N0}");
            }
        }
        Console.WriteLine();

        // === Bulk Update ===
        Console.WriteLine("4. Bulk update: 10% price increase for Electronics...");
        stopwatch.Restart();
        
        var affected = engine.ExecuteNonQuery("""
            UPDATE Products 
            SET Price = Price * 1.10 
            WHERE Category = 'Electronics'
            """);
        
        stopwatch.Stop();
        Console.WriteLine($"   [OK] Updated {affected} rows in {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine();

        // === Range Query with Index ===
        Console.WriteLine("5. Range query: Products with Price between $100 and $200...");
        stopwatch.Restart();
        
        using (var result = engine.Execute("""
            SELECT COUNT(*) AS Count, MIN(Price) AS MinPrice, MAX(Price) AS MaxPrice
            FROM Products
            WHERE Price BETWEEN 100 AND 200
            """))
        {
            stopwatch.Stop();
            if (result.Read())
            {
                Console.WriteLine($"   Found: {result.CurrentRow["Count"]} products");
                Console.WriteLine($"   Price range: ${result.CurrentRow["MinPrice"]:N2} - ${result.CurrentRow["MaxPrice"]:N2}");
                Console.WriteLine($"   Query time: {stopwatch.ElapsedMilliseconds}ms");
            }
        }
        Console.WriteLine();

        // === Bulk Delete ===
        Console.WriteLine("6. Bulk delete: Remove products with Stock < 105...");
        stopwatch.Restart();
        
        affected = engine.ExecuteNonQuery("DELETE FROM Products WHERE Stock < 105");
        
        stopwatch.Stop();
        Console.WriteLine($"   [OK] Deleted {affected} rows in {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine();

        // === Final Statistics ===
        Console.WriteLine("7. Final statistics...");
        using (var result = engine.Execute("""
            SELECT 
                COUNT(*) AS TotalProducts,
                SUM(Price * Stock) AS TotalInventoryValue,
                AVG(Stock) AS AvgStock
            FROM Products
            """))
        {
            if (result.Read())
            {
                Console.WriteLine($"   Total products remaining: {result.CurrentRow["TotalProducts"]}");
                Console.WriteLine($"   Total inventory value: ${result.CurrentRow["TotalInventoryValue"]:N2}");
                Console.WriteLine($"   Average stock level: {result.CurrentRow["AvgStock"]:N1}");
            }
        }
        Console.WriteLine();

        // === Performance Tips ===
        Console.WriteLine("Performance Tips:");
        Console.WriteLine("  * Use transactions for bulk operations");
        Console.WriteLine("  * Create indexes on frequently queried columns");
        Console.WriteLine("  * Use parameterized queries to leverage plan caching");
        Console.WriteLine("  * For write-heavy workloads, consider LSM-Tree storage");
        Console.WriteLine();

        Console.WriteLine("=== Example Complete ===");
        
        await Task.CompletedTask;
    }

    #endregion
}

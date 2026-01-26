using OutWit.Database.Core.Builder;
using OutWit.Database.Engine;

namespace OutWit.Database.Samples.ConsoleApp.Examples;

/// <summary>
/// Demonstrates transaction support with commit, rollback, and savepoints.
/// </summary>
public static class TransactionExample
{
    #region Run

    public static async Task RunAsync()
    {
        Console.WriteLine("=== Transaction & Savepoint Example ===");
        Console.WriteLine();
        
        var database = new WitDatabaseBuilder()
            .WithMemoryStorage()
            .WithBTree()
            .WithTransactions()
            .Build();
        
        using var engine = new WitSqlEngine(database, ownsStore: true);

        // Create accounts table
        Console.WriteLine("1. Creating 'Accounts' table...");
        engine.Execute("""
            CREATE TABLE Accounts (
                Id BIGINT PRIMARY KEY AUTOINCREMENT,
                Name VARCHAR(100) NOT NULL,
                Balance DECIMAL(15, 2) NOT NULL DEFAULT 0,
                UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
            )
            """);
        Console.WriteLine("   [OK] Table created");
        Console.WriteLine();

        // Insert initial accounts
        Console.WriteLine("2. Inserting initial accounts...");
        engine.Execute("""
            INSERT INTO Accounts (Name, Balance) VALUES
                ('Alice', 1000.00),
                ('Bob', 500.00),
                ('Carol', 250.00)
            """);
        PrintAccounts(engine, "Initial balances");
        Console.WriteLine();

        // === Committed Transaction ===
        Console.WriteLine("3. Transaction with COMMIT (transfer $200 from Alice to Bob)...");
        engine.Execute("BEGIN TRANSACTION");
        Console.WriteLine("   BEGIN TRANSACTION");
        
        engine.Execute("UPDATE Accounts SET Balance = Balance - 200 WHERE Name = 'Alice'");
        Console.WriteLine("   UPDATE Alice: -$200");
        
        engine.Execute("UPDATE Accounts SET Balance = Balance + 200 WHERE Name = 'Bob'");
        Console.WriteLine("   UPDATE Bob: +$200");
        
        engine.Execute("COMMIT");
        Console.WriteLine("   COMMIT");
        
        PrintAccounts(engine, "After committed transfer");
        Console.WriteLine();

        // === Rolled Back Transaction ===
        Console.WriteLine("4. Transaction with ROLLBACK (failed transfer)...");
        engine.Execute("BEGIN TRANSACTION");
        Console.WriteLine("   BEGIN TRANSACTION");
        
        engine.Execute("UPDATE Accounts SET Balance = Balance - 300 WHERE Name = 'Bob'");
        Console.WriteLine("   UPDATE Bob: -$300");
        
        engine.Execute("UPDATE Accounts SET Balance = Balance + 300 WHERE Name = 'Carol'");
        Console.WriteLine("   UPDATE Carol: +$300");
        
        Console.WriteLine("   Simulating an error... ROLLBACK");
        engine.Execute("ROLLBACK");
        
        PrintAccounts(engine, "After rollback (balances unchanged)");
        Console.WriteLine();

        // === Transaction with Savepoints ===
        Console.WriteLine("5. Transaction with SAVEPOINTs...");
        engine.Execute("BEGIN TRANSACTION");
        Console.WriteLine("   BEGIN TRANSACTION");
        
        // First operation
        engine.Execute("UPDATE Accounts SET Balance = Balance + 100 WHERE Name = 'Alice'");
        Console.WriteLine("   UPDATE Alice: +$100 (bonus)");
        
        // Create savepoint
        engine.Execute("SAVEPOINT sp_before_carol");
        Console.WriteLine("   SAVEPOINT sp_before_carol");
        
        // Second operation (will be rolled back)
        engine.Execute("UPDATE Accounts SET Balance = Balance + 500 WHERE Name = 'Carol'");
        Console.WriteLine("   UPDATE Carol: +$500 (mistaken bonus)");
        
        PrintAccounts(engine, "Before rollback to savepoint");
        
        // Rollback to savepoint
        engine.Execute("ROLLBACK TO SAVEPOINT sp_before_carol");
        Console.WriteLine("   ROLLBACK TO SAVEPOINT sp_before_carol");
        
        // Different operation after rollback to savepoint
        engine.Execute("UPDATE Accounts SET Balance = Balance + 50 WHERE Name = 'Carol'");
        Console.WriteLine("   UPDATE Carol: +$50 (correct bonus)");
        
        engine.Execute("COMMIT");
        Console.WriteLine("   COMMIT");
        
        PrintAccounts(engine, "Final balances");
        Console.WriteLine();

        // === Verify Transaction Isolation ===
        Console.WriteLine("6. Verifying total money conservation...");
        using (var result = engine.Execute("SELECT SUM(Balance) AS Total FROM Accounts"))
        {
            result.Read();
            var total = result.CurrentRow["Total"];
            Console.WriteLine($"   Total money in system: ${total:N2}");
            Console.WriteLine($"   Original total: $1,750.00");
            Console.WriteLine($"   Bonuses added: $150.00 (Alice +$100, Carol +$50)");
            Console.WriteLine($"   Expected total: $1,900.00");
        }
        Console.WriteLine();

        Console.WriteLine("=== Example Complete ===");
        
        await Task.CompletedTask;
    }

    #endregion

    #region Helpers

    private static void PrintAccounts(WitSqlEngine engine, string title)
    {
        Console.WriteLine();
        Console.WriteLine($"   {title}:");
        using (var result = engine.Execute("SELECT Name, Balance FROM Accounts ORDER BY Name"))
        {
            while (result.Read())
            {
                Console.WriteLine($"      {result.CurrentRow["Name"],-10}: ${result.CurrentRow["Balance"],10:N2}");
            }
        }
    }

    #endregion
}

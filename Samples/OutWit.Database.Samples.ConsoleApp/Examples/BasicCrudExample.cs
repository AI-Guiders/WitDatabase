using OutWit.Database.Core.Builder;
using OutWit.Database.Engine;

namespace OutWit.Database.Samples.ConsoleApp.Examples;

/// <summary>
/// Demonstrates basic CRUD operations with WitDatabase.
/// </summary>
public static class BasicCrudExample
{
    #region Run

    public static async Task RunAsync()
    {
        Console.WriteLine("=== Basic CRUD Example ===");
        Console.WriteLine();
        
        // Create in-memory database for demo
        var database = new WitDatabaseBuilder()
            .WithMemoryStorage()
            .WithBTree()
            .Build();
        
        using var engine = new WitSqlEngine(database, ownsStore: true);
        
        // Create table with various data types
        Console.WriteLine("1. Creating table 'Users'...");
        engine.Execute("""
            CREATE TABLE IF NOT EXISTS Users (
                Id BIGINT PRIMARY KEY AUTOINCREMENT,
                Name VARCHAR(100) NOT NULL,
                Email VARCHAR(255) UNIQUE,
                Age INT CHECK (Age >= 0 AND Age <= 150),
                Salary DECIMAL(10, 2),
                IsActive BOOLEAN DEFAULT TRUE,
                CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
            )
            """);
        Console.WriteLine("   [OK] Table created");
        Console.WriteLine();

        // Insert single row
        Console.WriteLine("2. Inserting single user...");
        engine.Execute("""
            INSERT INTO Users (Name, Email, Age, Salary)
            VALUES ('John Doe', 'john@example.com', 30, 75000.50)
            """);
        Console.WriteLine("   [OK] User inserted");
        Console.WriteLine();

        // Insert with RETURNING
        Console.WriteLine("3. Inserting user with RETURNING clause...");
        using (var result = engine.Execute("""
            INSERT INTO Users (Name, Email, Age, Salary)
            VALUES ('Jane Smith', 'jane@example.com', 28, 82000.00)
            RETURNING Id, Name, CreatedAt
            """))
        {
            if (result.Read())
            {
                Console.WriteLine($"   [OK] Inserted: Id={result.CurrentRow["Id"]}, " +
                                  $"Name={result.CurrentRow["Name"]}, " +
                                  $"CreatedAt={result.CurrentRow["CreatedAt"]}");
            }
        }
        Console.WriteLine();

        // Bulk insert
        Console.WriteLine("4. Bulk inserting users...");
        engine.Execute("""
            INSERT INTO Users (Name, Email, Age, Salary) VALUES
                ('Alice Johnson', 'alice@example.com', 25, 65000.00),
                ('Bob Wilson', 'bob@example.com', 35, 95000.00),
                ('Carol Davis', 'carol@example.com', 42, 110000.00)
            """);
        Console.WriteLine("   [OK] 3 users inserted");
        Console.WriteLine();

        // Select all users
        Console.WriteLine("5. Querying all users...");
        Console.WriteLine();
        using (var result = engine.Execute("SELECT Id, Name, Email, Age, Salary, IsActive FROM Users ORDER BY Name"))
        {
            PrintResultTable(result, ["Id", "Name", "Email", "Age", "Salary", "IsActive"]);
        }
        Console.WriteLine();

        // Select with WHERE
        Console.WriteLine("6. Querying users with Age > 30...");
        Console.WriteLine();
        using (var result = engine.Execute("SELECT Name, Age, Salary FROM Users WHERE Age > 30 ORDER BY Age"))
        {
            PrintResultTable(result, ["Name", "Age", "Salary"]);
        }
        Console.WriteLine();

        // Update with parameters
        Console.WriteLine("7. Updating user salary with parameters...");
        var parameters = new Dictionary<string, object?>
        {
            { "@name", "John Doe" },
            { "@newSalary", 80000.00m }
        };
        var affected = engine.ExecuteNonQuery(
            "UPDATE Users SET Salary = @newSalary WHERE Name = @name",
            parameters);
        Console.WriteLine($"   [OK] Updated {affected} row(s)");
        Console.WriteLine();

        // Aggregation query
        Console.WriteLine("8. Running aggregation query...");
        using (var result = engine.Execute("""
            SELECT 
                COUNT(*) AS TotalUsers,
                AVG(Age) AS AvgAge,
                MIN(Salary) AS MinSalary,
                MAX(Salary) AS MaxSalary,
                SUM(Salary) AS TotalSalaries
            FROM Users
            """))
        {
            if (result.Read())
            {
                Console.WriteLine($"   Total Users: {result.CurrentRow["TotalUsers"]}");
                Console.WriteLine($"   Average Age: {result.CurrentRow["AvgAge"]:F1}");
                Console.WriteLine($"   Min Salary: ${result.CurrentRow["MinSalary"]:N2}");
                Console.WriteLine($"   Max Salary: ${result.CurrentRow["MaxSalary"]:N2}");
                Console.WriteLine($"   Total Salaries: ${result.CurrentRow["TotalSalaries"]:N2}");
            }
        }
        Console.WriteLine();

        // Delete with RETURNING
        Console.WriteLine("9. Deleting user with RETURNING...");
        using (var result = engine.Execute("""
            DELETE FROM Users 
            WHERE Name = 'Alice Johnson'
            RETURNING Id, Name
            """))
        {
            if (result.Read())
            {
                Console.WriteLine($"   [OK] Deleted: Id={result.CurrentRow["Id"]}, Name={result.CurrentRow["Name"]}");
            }
        }
        Console.WriteLine();

        // Final count
        var finalCount = engine.ExecuteScalar("SELECT COUNT(*) FROM Users").AsInt64();
        Console.WriteLine($"10. Final user count: {finalCount}");
        Console.WriteLine();

        Console.WriteLine("=== Example Complete ===");
        
        await Task.CompletedTask;
    }

    #endregion

    #region Helpers

    private static void PrintResultTable(Sql.WitSqlResult result, string[] columns)
    {
        // Print header
        Console.Write("   ");
        foreach (var col in columns)
        {
            Console.Write($"{col,-15}");
        }
        Console.WriteLine();
        Console.Write("   ");
        Console.WriteLine(new string('-', columns.Length * 15));

        // Print rows
        while (result.Read())
        {
            Console.Write("   ");
            foreach (var col in columns)
            {
                var value = result.CurrentRow[col];
                var display = value.IsNull ? "NULL" : value.ToString() ?? "";
                if (display.Length > 14)
                    display = display[..11] + "...";
                Console.Write($"{display,-15}");
            }
            Console.WriteLine();
        }
    }

    #endregion
}

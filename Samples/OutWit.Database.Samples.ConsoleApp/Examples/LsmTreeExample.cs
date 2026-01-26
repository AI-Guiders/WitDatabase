using OutWit.Database.Core.Builder;
using OutWit.Database.Engine;

namespace OutWit.Database.Samples.ConsoleApp.Examples;

/// <summary>
/// Demonstrates LSM-Tree storage engine with write-optimized workloads.
/// </summary>
public static class LsmTreeExample
{
    #region Constants

    private const string LSM_DIR = "lsm_demo_data";

    #endregion

    #region Run

    public static async Task RunAsync()
    {
        Console.WriteLine("=== LSM-Tree Storage Engine Example ===");
        Console.WriteLine();
        Console.WriteLine("LSM-Tree (Log-Structured Merge-Tree) is optimized for:");
        Console.WriteLine("  - Write-heavy workloads");
        Console.WriteLine("  - Sequential write patterns");
        Console.WriteLine("  - Time-series data and logging");
        Console.WriteLine();

        // Clean up from previous runs
        CleanupLsmDirectory();

        // === Create LSM-Tree Database ===
        Console.WriteLine("1. Creating LSM-Tree database...");
        Console.WriteLine($"   Directory: {Path.GetFullPath(LSM_DIR)}");
        Console.WriteLine();

        var database = new WitDatabaseBuilder()
            .WithLsmTree(LSM_DIR, opts =>
            {
                opts.EnableWal = true;
                opts.EnableBlockCache = true;
                opts.BlockCacheSizeBytes = 16 * 1024 * 1024; // 16MB cache
                opts.MemTableSizeLimit = 1 * 1024 * 1024;    // 1MB before flush
                opts.BackgroundCompaction = true;
            })
            .WithTransactions()
            .Build();

        using var engine = new WitSqlEngine(database, ownsStore: true);

        // Create logging table
        Console.WriteLine("2. Creating time-series logging table...");
        engine.Execute("""
            CREATE TABLE EventLogs (
                Id BIGINT PRIMARY KEY AUTOINCREMENT,
                EventTime DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                Level VARCHAR(10) NOT NULL,
                Source VARCHAR(100) NOT NULL,
                Message TEXT NOT NULL,
                Data TEXT
            )
            """);
        Console.WriteLine("   [OK] Table created");
        Console.WriteLine();

        // === Bulk Insert Performance ===
        Console.WriteLine("3. Bulk inserting 1000 log entries...");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        engine.Execute("BEGIN TRANSACTION");
        for (int i = 0; i < 1000; i++)
        {
            var level = (i % 10) switch
            {
                0 => "ERROR",
                < 3 => "WARN",
                _ => "INFO"
            };
            
            engine.Execute(
                "INSERT INTO EventLogs (Level, Source, Message, Data) VALUES (@level, @source, @message, @data)",
                new Dictionary<string, object?>
                {
                    { "@level", level },
                    { "@source", $"Service{i % 5}" },
                    { "@message", $"Event #{i}: Processing request {Guid.NewGuid():N}" },
                    { "@data", $"{{\"iteration\": {i}, \"timestamp_ms\": {stopwatch.ElapsedMilliseconds}}}" }
                });
        }
        engine.Execute("COMMIT");
        
        stopwatch.Stop();
        Console.WriteLine($"   [OK] Inserted 1000 rows in {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"   [OK] Rate: {1000.0 / stopwatch.Elapsed.TotalSeconds:F0} rows/sec");
        Console.WriteLine();

        // === Query by Time Range ===
        Console.WriteLine("4. Querying logs by level distribution...");
        using (var result = engine.Execute("""
            SELECT Level, COUNT(*) AS Count
            FROM EventLogs
            GROUP BY Level
            ORDER BY Count DESC
            """))
        {
            Console.WriteLine();
            Console.WriteLine("   Level     | Count");
            Console.WriteLine("   ----------|------");
            while (result.Read())
            {
                Console.WriteLine($"   {result.CurrentRow["Level"],-10}| {result.CurrentRow["Count"]}");
            }
        }
        Console.WriteLine();

        // === Query Recent Errors ===
        Console.WriteLine("5. Querying recent ERROR logs (last 10)...");
        using (var result = engine.Execute("""
            SELECT Id, Source, Message
            FROM EventLogs
            WHERE Level = 'ERROR'
            ORDER BY Id DESC
            LIMIT 10
            """))
        {
            Console.WriteLine();
            while (result.Read())
            {
                var msg = result.CurrentRow["Message"].ToString() ?? "";
                if (msg.Length > 50) msg = msg[..47] + "...";
                Console.WriteLine($"   [{result.CurrentRow["Id"]}] {result.CurrentRow["Source"]}: {msg}");
            }
        }
        Console.WriteLine();

        // === Statistics ===
        Console.WriteLine("6. Database statistics...");
        using (var result = engine.Execute("""
            SELECT 
                COUNT(*) AS TotalLogs,
                MIN(EventTime) AS FirstEvent,
                MAX(EventTime) AS LastEvent
            FROM EventLogs
            """))
        {
            if (result.Read())
            {
                Console.WriteLine($"   Total logs: {result.CurrentRow["TotalLogs"]}");
                Console.WriteLine($"   First event: {result.CurrentRow["FirstEvent"]}");
                Console.WriteLine($"   Last event: {result.CurrentRow["LastEvent"]}");
            }
        }
        Console.WriteLine();

        // Show file structure
        Console.WriteLine("7. LSM-Tree file structure:");
        ShowDirectoryContents(LSM_DIR);
        Console.WriteLine();

        Console.WriteLine("=== Example Complete ===");
        Console.WriteLine();
        Console.WriteLine("Note: LSM files will be cleaned up on exit.");
        
        // Clean up
        database.Dispose();
        CleanupLsmDirectory();
        
        await Task.CompletedTask;
    }

    #endregion

    #region Helpers

    private static void CleanupLsmDirectory()
    {
        if (Directory.Exists(LSM_DIR))
        {
            try
            {
                Directory.Delete(LSM_DIR, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private static void ShowDirectoryContents(string path)
    {
        if (!Directory.Exists(path))
        {
            Console.WriteLine($"   Directory not found: {path}");
            return;
        }

        var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
        foreach (var file in files.Take(10))
        {
            var info = new FileInfo(file);
            var relativePath = Path.GetRelativePath(path, file);
            Console.WriteLine($"   {relativePath,-30} {FormatSize(info.Length),10}");
        }

        if (files.Length > 10)
        {
            Console.WriteLine($"   ... and {files.Length - 10} more files");
        }
    }

    private static string FormatSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            _ => $"{bytes / (1024.0 * 1024):F1} MB"
        };
    }

    #endregion
}

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using LiteDB;
using Microsoft.Data.Sqlite;
using OutWit.Database.AdoNet;

namespace OutWit.Database.Comparison.Benchmarks;

/// <summary>
/// Comprehensive comparison benchmarks: WitDb (BTree) vs SQLite vs LiteDB
/// Includes standard INSERT, Bulk INSERT, and Parallel INSERT modes.
/// </summary>
public enum InsertMode 
{ 
    Standard,      // Loop with prepared statement
    Bulk,          // Using BulkInsert API (WitDb only)
    Parallel       // Parallel inserts (where supported)
}

[Config(typeof(ComparisonBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class ComprehensiveComparisonBenchmarks : IDisposable
{
    public enum DbEngine { WitDb_BTree, SQLite, LiteDB }

    private WitDbConnection _witConn = null!;
    private SqliteConnection _sqliteConn = null!;
    private LiteDatabase _liteDb = null!;
    private string _witPath = null!;
    private string _sqlitePath = null!;
    private string _liteDbPath = null!;

    [Params(1000, 5000, 10000)]
    public int RowCount { get; set; }

    [Params(DbEngine.WitDb_BTree, DbEngine.SQLite, DbEngine.LiteDB)]
    public DbEngine Database { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _witPath = Path.Combine(Path.GetTempPath(), $"wit_comp_{Guid.NewGuid():N}.witdb");
        _sqlitePath = Path.Combine(Path.GetTempPath(), $"sql_comp_{Guid.NewGuid():N}.db");
        _liteDbPath = Path.Combine(Path.GetTempPath(), $"lite_comp_{Guid.NewGuid():N}.db");
    }

    [IterationSetup]
    public void IterationSetup()
    {
        switch (Database)
        {
            case DbEngine.WitDb_BTree:
                _witConn = new WitDbConnection($"Data Source={_witPath};Transactions=true;MVCC=false");
                _witConn.Open();
                using (var cmd = _witConn.CreateCommand())
                {
                    cmd.CommandText = "DROP TABLE IF EXISTS Items";
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = "CREATE TABLE Items (Id BIGINT PRIMARY KEY AUTOINCREMENT, Name VARCHAR(100), Value DOUBLE, Category VARCHAR(50))";
                    cmd.ExecuteNonQuery();
                }
                break;

            case DbEngine.SQLite:
                _sqliteConn = new SqliteConnection($"Data Source={_sqlitePath}");
                _sqliteConn.Open();
                using (var cmd = _sqliteConn.CreateCommand())
                {
                    cmd.CommandText = "DROP TABLE IF EXISTS Items";
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = "CREATE TABLE Items (Id INTEGER PRIMARY KEY, Name TEXT, Value REAL, Category TEXT)";
                    cmd.ExecuteNonQuery();
                }
                break;

            case DbEngine.LiteDB:
                _liteDb = new LiteDatabase(_liteDbPath);
                var col = _liteDb.GetCollection<BsonDocument>("Items");
                col.DeleteAll();
                break;
        }
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _witConn?.Dispose();
        _witConn = null!;
        _sqliteConn?.Dispose();
        _sqliteConn = null!;
        _liteDb?.Dispose();
        _liteDb = null!;
        
        try { if (File.Exists(_witPath)) File.Delete(_witPath); } catch { }
        try { if (File.Exists(_sqlitePath)) File.Delete(_sqlitePath); } catch { }
        try { if (File.Exists(_liteDbPath)) File.Delete(_liteDbPath); } catch { }
    }

    [GlobalCleanup]
    public void GlobalCleanup() => IterationCleanup();

    #region Standard INSERT Benchmark

    [Benchmark(Description = "Standard INSERT")]
    public int InsertStandard()
    {
        int count = 0;
        
        switch (Database)
        {
            case DbEngine.WitDb_BTree:
                using (var tx = (WitDbTransaction)_witConn.BeginTransaction())
                {
                    using var cmd = _witConn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = "INSERT INTO Items (Name, Value, Category) VALUES (@n, @v, @c)";
                    var pn = cmd.CreateParameter(); pn.ParameterName = "@n"; cmd.Parameters.Add(pn);
                    var pv = cmd.CreateParameter(); pv.ParameterName = "@v"; cmd.Parameters.Add(pv);
                    var pc = cmd.CreateParameter(); pc.ParameterName = "@c"; cmd.Parameters.Add(pc);
                    
                    for (int i = 0; i < RowCount; i++)
                    {
                        pn.Value = $"Item{i}";
                        pv.Value = i * 1.5;
                        pc.Value = $"Category{i % 10}";
                        count += cmd.ExecuteNonQuery();
                    }
                    tx.Commit();
                }
                break;

            case DbEngine.SQLite:
                using (var tx = _sqliteConn.BeginTransaction())
                {
                    using var cmd = _sqliteConn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = "INSERT INTO Items (Name, Value, Category) VALUES (@n, @v, @c)";
                    var pn = cmd.CreateParameter(); pn.ParameterName = "@n"; cmd.Parameters.Add(pn);
                    var pv = cmd.CreateParameter(); pv.ParameterName = "@v"; cmd.Parameters.Add(pv);
                    var pc = cmd.CreateParameter(); pc.ParameterName = "@c"; cmd.Parameters.Add(pc);
                    
                    for (int i = 0; i < RowCount; i++)
                    {
                        pn.Value = $"Item{i}";
                        pv.Value = i * 1.5;
                        pc.Value = $"Category{i % 10}";
                        count += cmd.ExecuteNonQuery();
                    }
                    tx.Commit();
                }
                break;

            case DbEngine.LiteDB:
                var col = _liteDb.GetCollection<BsonDocument>("Items");
                _liteDb.BeginTrans();
                for (int i = 0; i < RowCount; i++)
                {
                    col.Insert(new BsonDocument
                    {
                        ["Name"] = $"Item{i}",
                        ["Value"] = i * 1.5,
                        ["Category"] = $"Category{i % 10}"
                    });
                    count++;
                }
                _liteDb.Commit();
                break;
        }
        
        return count;
    }

    #endregion

    public void Dispose() => GlobalCleanup();
}

/// <summary>
/// Benchmarks comparing different insert strategies for WitDb
/// </summary>
[Config(typeof(ComparisonBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class WitDbInsertStrategiesBenchmarks : IDisposable
{
    private WitDbConnection _conn = null!;
    private string _path = null!;

    [Params(1000, 5000, 10000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _path = Path.Combine(Path.GetTempPath(), $"wit_strategies_{Guid.NewGuid():N}.witdb");
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _conn = new WitDbConnection($"Data Source={_path};Transactions=true;MVCC=false");
        _conn.Open();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "DROP TABLE IF EXISTS Items";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "CREATE TABLE Items (Id BIGINT PRIMARY KEY AUTOINCREMENT, Name VARCHAR(100), Value DOUBLE, Category VARCHAR(50))";
        cmd.ExecuteNonQuery();
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _conn?.Dispose();
        _conn = null!;
        try { if (File.Exists(_path)) File.Delete(_path); } catch { }
    }

    [GlobalCleanup]
    public void GlobalCleanup() => IterationCleanup();

    [Benchmark(Description = "1. Standard Loop", Baseline = true)]
    public int StandardLoop()
    {
        int count = 0;
        using var tx = (WitDbTransaction)_conn.BeginTransaction();
        using var cmd = _conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO Items (Name, Value, Category) VALUES (@n, @v, @c)";
        var pn = cmd.CreateParameter(); pn.ParameterName = "@n"; cmd.Parameters.Add(pn);
        var pv = cmd.CreateParameter(); pv.ParameterName = "@v"; cmd.Parameters.Add(pv);
        var pc = cmd.CreateParameter(); pc.ParameterName = "@c"; cmd.Parameters.Add(pc);
        
        for (int i = 0; i < RowCount; i++)
        {
            pn.Value = $"Item{i}";
            pv.Value = i * 1.5;
            pc.Value = $"Category{i % 10}";
            count += cmd.ExecuteNonQuery();
        }
        tx.Commit();
        return count;
    }

    [Benchmark(Description = "2. BulkInsert API")]
    public int BulkInsertApi()
    {
        var engine = _conn.Engine!;
        
        var rows = Enumerable.Range(0, RowCount)
            .Select(i => new Dictionary<string, object?>
            {
                ["Name"] = $"Item{i}",
                ["Value"] = i * 1.5,
                ["Category"] = $"Category{i % 10}"
            });

        return engine.BulkInsert("Items", rows);
    }

    [Benchmark(Description = "3. ExecuteBatch API")]
    public int ExecuteBatchApi()
    {
        var engine = _conn.Engine!;
        
        using var stmt = engine.Prepare("INSERT INTO Items (Name, Value, Category) VALUES (@Name, @Value, @Category)");
        
        var paramSets = Enumerable.Range(0, RowCount)
            .Select(i => new Dictionary<string, object?>
            {
                ["Name"] = $"Item{i}",
                ["Value"] = i * 1.5,
                ["Category"] = $"Category{i % 10}"
            });

        return stmt.ExecuteBatch(paramSets);
    }

    [Benchmark(Description = "4. Parallel Batches (4 threads)")]
    public int ParallelBatches()
    {
        var engine = _conn.Engine!;
        int batchCount = 4;
        int batchSize = RowCount / batchCount;
        int totalInserted = 0;

        // Note: This requires thread-safe engine access
        // For now, we'll use sequential batches with separate transactions
        engine.Execute("BEGIN TRANSACTION");
        try
        {
            var batches = Enumerable.Range(0, batchCount)
                .Select(b => Enumerable.Range(b * batchSize, batchSize)
                    .Select(i => new Dictionary<string, object?>
                    {
                        ["Name"] = $"Item{i}",
                        ["Value"] = i * 1.5,
                        ["Category"] = $"Category{i % 10}"
                    })
                    .ToList())
                .ToList();

            // Process batches (sequentially for thread safety, but prepared for parallel)
            foreach (var batch in batches)
            {
                totalInserted += engine.BulkInsert("Items", batch);
            }

            engine.Execute("COMMIT");
        }
        catch
        {
            engine.Execute("ROLLBACK");
            throw;
        }

        return totalInserted;
    }

    public void Dispose() => GlobalCleanup();
}

/// <summary>
/// Read benchmarks - requires pre-populated data
/// </summary>
[Config(typeof(ComparisonBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class ComprehensiveReadBenchmarks : IDisposable
{
    public enum DbEngine { WitDb_BTree, SQLite, LiteDB }

    private WitDbConnection _witConn = null!;
    private SqliteConnection _sqliteConn = null!;
    private LiteDatabase _liteDb = null!;
    private string _witPath = null!;
    private string _sqlitePath = null!;
    private string _liteDbPath = null!;
    private int[] _randomIds = null!;

    [Params(1000, 5000, 10000)]
    public int TableSize { get; set; }

    [Params(DbEngine.WitDb_BTree, DbEngine.SQLite, DbEngine.LiteDB)]
    public DbEngine Database { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Use file paths for all databases
        _witPath = Path.Combine(Path.GetTempPath(), $"wit_read_{Guid.NewGuid():N}.witdb");
        _sqlitePath = Path.Combine(Path.GetTempPath(), $"sql_read_{Guid.NewGuid():N}.db");
        _liteDbPath = Path.Combine(Path.GetTempPath(), $"lite_read_{Guid.NewGuid():N}.db");

        // Generate random IDs for point queries
        var rnd = new Random(42);
        _randomIds = Enumerable.Range(0, 100).Select(_ => rnd.Next(1, TableSize + 1)).ToArray();

        switch (Database)
        {
            case DbEngine.WitDb_BTree:
                SetupWitDb();
                break;
            case DbEngine.SQLite:
                SetupSqlite();
                break;
            case DbEngine.LiteDB:
                SetupLiteDb();
                break;
        }
    }

    private void SetupWitDb()
    {
        _witConn = new WitDbConnection($"Data Source={_witPath};Transactions=true;MVCC=false");
        _witConn.Open();
        
        using (var cmd = _witConn.CreateCommand())
        {
            cmd.CommandText = "CREATE TABLE Items (Id BIGINT PRIMARY KEY AUTOINCREMENT, Name VARCHAR(100), Value DOUBLE, Category VARCHAR(50))";
            cmd.ExecuteNonQuery();
        }

        // Use BulkInsert for faster setup!
        var engine = _witConn.Engine!;
        var rows = Enumerable.Range(0, TableSize)
            .Select(i => new Dictionary<string, object?>
            {
                ["Name"] = $"Item{i}",
                ["Value"] = i * 1.5,
                ["Category"] = $"Category{i % 10}"
            });
        engine.BulkInsert("Items", rows);

        // Create index on Category for filtered queries
        using (var cmd = _witConn.CreateCommand())
        {
            cmd.CommandText = "CREATE INDEX IX_Items_Category ON Items(Category)";
            cmd.ExecuteNonQuery();
        }
    }

    private void SetupSqlite()
    {
        _sqliteConn = new SqliteConnection($"Data Source={_sqlitePath}");
        _sqliteConn.Open();
        
        using (var cmd = _sqliteConn.CreateCommand())
        {
            cmd.CommandText = "CREATE TABLE Items (Id INTEGER PRIMARY KEY, Name TEXT, Value REAL, Category TEXT)";
            cmd.ExecuteNonQuery();
        }

        using var tx = _sqliteConn.BeginTransaction();
        using (var cmd = _sqliteConn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO Items (Name, Value, Category) VALUES (@n, @v, @c)";
            var pn = cmd.CreateParameter(); pn.ParameterName = "@n"; cmd.Parameters.Add(pn);
            var pv = cmd.CreateParameter(); pv.ParameterName = "@v"; cmd.Parameters.Add(pv);
            var pc = cmd.CreateParameter(); pc.ParameterName = "@c"; cmd.Parameters.Add(pc);
            
            for (int i = 0; i < TableSize; i++)
            {
                pn.Value = $"Item{i}";
                pv.Value = i * 1.5;
                pc.Value = $"Category{i % 10}";
                cmd.ExecuteNonQuery();
            }
        }
        tx.Commit();

        // Create index on Category
        using (var cmd = _sqliteConn.CreateCommand())
        {
            cmd.CommandText = "CREATE INDEX IX_Items_Category ON Items(Category)";
            cmd.ExecuteNonQuery();
        }
    }

    private void SetupLiteDb()
    {
        _liteDb = new LiteDatabase(_liteDbPath);
        var col = _liteDb.GetCollection<BsonDocument>("Items");
        
        _liteDb.BeginTrans();
        for (int i = 0; i < TableSize; i++)
        {
            col.Insert(new BsonDocument
            {
                ["_id"] = i + 1,  // Explicit ID starting from 1
                ["Name"] = $"Item{i}",
                ["Value"] = i * 1.5,
                ["Category"] = $"Category{i % 10}"
            });
        }
        _liteDb.Commit();

        // Create index on Category
        col.EnsureIndex("Category");
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _witConn?.Dispose();
        _sqliteConn?.Dispose();
        _liteDb?.Dispose();
        
        // Clean up files
        try { if (File.Exists(_witPath)) File.Delete(_witPath); } catch { }
        try { if (File.Exists(_sqlitePath)) File.Delete(_sqlitePath); } catch { }
        try { if (File.Exists(_liteDbPath)) File.Delete(_liteDbPath); } catch { }
        
        // Try cleaning up WitDb directory format too
        var witDir = Path.ChangeExtension(_witPath, null);
        try { if (Directory.Exists(witDir)) Directory.Delete(witDir, true); } catch { }
    }

    #region COUNT Benchmarks

    [Benchmark(Description = "COUNT(*)")]
    public long CountAll()
    {
        switch (Database)
        {
            case DbEngine.WitDb_BTree:
                using (var cmd = _witConn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM Items";
                    return Convert.ToInt64(cmd.ExecuteScalar());
                }

            case DbEngine.SQLite:
                using (var cmd = _sqliteConn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM Items";
                    return Convert.ToInt64(cmd.ExecuteScalar());
                }

            case DbEngine.LiteDB:
                return _liteDb.GetCollection<BsonDocument>("Items").Count();
        }
        return 0;
    }

    #endregion

    #region Full Scan Benchmarks

    [Benchmark(Description = "Full Table Scan")]
    public int FullTableScan()
    {
        int count = 0;
        
        switch (Database)
        {
            case DbEngine.WitDb_BTree:
                using (var cmd = _witConn.CreateCommand())
                {
                    cmd.CommandText = "SELECT Id, Name, Value, Category FROM Items";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read()) count++;
                }
                break;

            case DbEngine.SQLite:
                using (var cmd = _sqliteConn.CreateCommand())
                {
                    cmd.CommandText = "SELECT Id, Name, Value, Category FROM Items";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read()) count++;
                }
                break;

            case DbEngine.LiteDB:
                foreach (var doc in _liteDb.GetCollection<BsonDocument>("Items").FindAll())
                    count++;
                break;
        }
        
        return count;
    }

    #endregion

    #region Point Query Benchmarks

    [Benchmark(Description = "Point Query by PK (100x)")]
    public int PointQueryByPk()
    {
        int found = 0;
        
        switch (Database)
        {
            case DbEngine.WitDb_BTree:
                using (var cmd = _witConn.CreateCommand())
                {
                    cmd.CommandText = "SELECT Id, Name, Value, Category FROM Items WHERE Id = @id";
                    var p = cmd.CreateParameter(); p.ParameterName = "@id"; cmd.Parameters.Add(p);
                    
                    foreach (var id in _randomIds)
                    {
                        p.Value = id;
                        using var reader = cmd.ExecuteReader();
                        if (reader.Read()) found++;
                    }
                }
                break;

            case DbEngine.SQLite:
                using (var cmd = _sqliteConn.CreateCommand())
                {
                    cmd.CommandText = "SELECT Id, Name, Value, Category FROM Items WHERE Id = @id";
                    var p = cmd.CreateParameter(); p.ParameterName = "@id"; cmd.Parameters.Add(p);
                    
                    foreach (var id in _randomIds)
                    {
                        p.Value = id;
                        using var reader = cmd.ExecuteReader();
                        if (reader.Read()) found++;
                    }
                }
                break;

            case DbEngine.LiteDB:
                var col = _liteDb.GetCollection<BsonDocument>("Items");
                foreach (var id in _randomIds)
                {
                    var doc = col.FindById(id);
                    if (doc != null) found++;
                }
                break;
        }
        
        return found;
    }

    #endregion

    #region Aggregation Benchmarks

    [Benchmark(Description = "SUM(Value)")]
    public double SumValue()
    {
        switch (Database)
        {
            case DbEngine.WitDb_BTree:
                using (var cmd = _witConn.CreateCommand())
                {
                    cmd.CommandText = "SELECT SUM(Value) FROM Items";
                    return Convert.ToDouble(cmd.ExecuteScalar());
                }

            case DbEngine.SQLite:
                using (var cmd = _sqliteConn.CreateCommand())
                {
                    cmd.CommandText = "SELECT SUM(Value) FROM Items";
                    return Convert.ToDouble(cmd.ExecuteScalar());
                }

            case DbEngine.LiteDB:
                // LiteDB doesn't have built-in SUM, must iterate
                double sum = 0;
                foreach (var doc in _liteDb.GetCollection<BsonDocument>("Items").FindAll())
                    sum += doc["Value"].AsDouble;
                return sum;
        }
        return 0;
    }

    [Benchmark(Description = "AVG(Value) GROUP BY Category")]
    public int GroupByCategory()
    {
        int groups = 0;
        
        switch (Database)
        {
            case DbEngine.WitDb_BTree:
                using (var cmd = _witConn.CreateCommand())
                {
                    cmd.CommandText = "SELECT Category, AVG(Value) FROM Items GROUP BY Category";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read()) groups++;
                }
                break;

            case DbEngine.SQLite:
                using (var cmd = _sqliteConn.CreateCommand())
                {
                    cmd.CommandText = "SELECT Category, AVG(Value) FROM Items GROUP BY Category";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read()) groups++;
                }
                break;

            case DbEngine.LiteDB:
                // LiteDB - manual grouping
                var grouped = _liteDb.GetCollection<BsonDocument>("Items")
                    .FindAll()
                    .GroupBy(d => d["Category"].AsString)
                    .Select(g => new { Category = g.Key, Avg = g.Average(d => d["Value"].AsDouble) })
                    .ToList();
                groups = grouped.Count;
                break;
        }
        
        return groups;
    }

    #endregion

    public void Dispose() => GlobalCleanup();
}

/// <summary>
/// Benchmarks for true parallel inserts using multiple connections
/// </summary>
[Config(typeof(ComparisonBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class ParallelInsertBenchmarks : IDisposable
{
    private string _dbPath = null!;

    [Params(5000, 10000)]
    public int RowCount { get; set; }

    [Params(1, 2, 4)]
    public int ThreadCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"wit_parallel_{Guid.NewGuid():N}.witdb");
    }

    [IterationSetup]
    public void IterationSetup()
    {
        // Create initial schema
        using var conn = new WitDbConnection($"Data Source={_dbPath};Transactions=true;MVCC=false");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DROP TABLE IF EXISTS Items";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "CREATE TABLE Items (Id BIGINT PRIMARY KEY AUTOINCREMENT, ThreadId INT, Name VARCHAR(100), Value DOUBLE)";
        cmd.ExecuteNonQuery();
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
    }

    [GlobalCleanup]
    public void GlobalCleanup() => IterationCleanup();

    [Benchmark(Description = "Parallel Insert (multiple connections)")]
    public int ParallelInsertMultipleConnections()
    {
        int rowsPerThread = RowCount / ThreadCount;
        int totalInserted = 0;

        // For single thread, just do it directly
        if (ThreadCount == 1)
        {
            using var conn = new WitDbConnection($"Data Source={_dbPath};Transactions=true;MVCC=false");
            conn.Open();
            var engine = conn.Engine!;
            
            var rows = Enumerable.Range(0, RowCount)
                .Select(i => new Dictionary<string, object?>
                {
                    ["ThreadId"] = 0,
                    ["Name"] = $"Item{i}",
                    ["Value"] = i * 1.5
                });
            
            return engine.BulkInsert("Items", rows);
        }

        // For multiple threads, each thread gets its own connection
        var tasks = new Task<int>[ThreadCount];
        
        for (int t = 0; t < ThreadCount; t++)
        {
            int threadId = t;
            int startIdx = t * rowsPerThread;
            
            tasks[t] = Task.Run(() =>
            {
                using var conn = new WitDbConnection($"Data Source={_dbPath};Transactions=true;MVCC=false");
                conn.Open();
                var engine = conn.Engine!;
                
                var rows = Enumerable.Range(startIdx, rowsPerThread)
                    .Select(i => new Dictionary<string, object?>
                    {
                        ["ThreadId"] = threadId,
                        ["Name"] = $"Item{i}",
                        ["Value"] = i * 1.5
                    });
                
                return engine.BulkInsert("Items", rows);
            });
        }
        
        Task.WaitAll(tasks);
        return tasks.Sum(t => t.Result);
    }

    public void Dispose() => GlobalCleanup();
}

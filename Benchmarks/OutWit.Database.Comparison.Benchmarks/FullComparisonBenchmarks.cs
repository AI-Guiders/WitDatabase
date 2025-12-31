using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Reports;
using LiteDB;
using Microsoft.Data.Sqlite;
using OutWit.Database.AdoNet;

namespace OutWit.Database.Comparison.Benchmarks;

/// <summary>
/// Full comparison benchmark: WitDb BTree, WitDb LSM (with Parallel), SQLite, LiteDB
/// Operations: INSERT (standard + bulk), SELECT, UPDATE, DELETE, COUNT, SUM, GROUP BY
/// </summary>
public class FullComparisonConfig : ManualConfig
{
    public FullComparisonConfig()
    {
        SummaryStyle = SummaryStyle.Default
            .WithRatioStyle(RatioStyle.Trend)
            .WithTimeUnit(Perfolizer.Horology.TimeUnit.Millisecond);
        HideColumns(Column.Error, Column.StdDev, Column.RatioSD);
    }
}

public enum FullDbEngine
{
    WitDb_BTree,
    WitDb_BTree_Parallel,
    WitDb_LSM,
    WitDb_LSM_Parallel,
    SQLite,
    LiteDB
}

#region INSERT Benchmarks - Standard Loop

[Config(typeof(FullComparisonConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class FullInsertStandardBenchmarks : IDisposable
{
    private WitDbConnection? _witBTreeConn;
    private WitDbConnection? _witBTreeParallelConn;
    private WitDbConnection? _witLsmConn;
    private WitDbConnection? _witLsmParallelConn;
    private SqliteConnection? _sqliteConn;
    private LiteDatabase? _liteDb;
    
    private string _witBTreePath = null!;
    private string _witBTreeParallelPath = null!;
    private string _witLsmPath = null!;
    private string _witLsmParallelPath = null!;
    private string _sqlitePath = null!;
    private string _liteDbPath = null!;

    [Params(100, 1000, 5000, 10000)]
    public int RowCount { get; set; }

    [Params(FullDbEngine.WitDb_BTree, FullDbEngine.WitDb_BTree_Parallel, FullDbEngine.SQLite, FullDbEngine.LiteDB)]
    public FullDbEngine Database { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        var id = Guid.NewGuid().ToString("N");
        _witBTreePath = Path.Combine(Path.GetTempPath(), $"wit_btree_{id}.witdb");
        _witBTreeParallelPath = Path.Combine(Path.GetTempPath(), $"wit_btree_par_{id}.witdb");
        _witLsmPath = Path.Combine(Path.GetTempPath(), $"wit_lsm_{id}");
        _witLsmParallelPath = Path.Combine(Path.GetTempPath(), $"wit_lsm_par_{id}");
        _sqlitePath = Path.Combine(Path.GetTempPath(), $"sql_{id}.db");
        _liteDbPath = Path.Combine(Path.GetTempPath(), $"lite_{id}.db");
    }

    [IterationSetup]
    public void IterationSetup()
    {
        switch (Database)
        {
            case FullDbEngine.WitDb_BTree:
                _witBTreeConn = new WitDbConnection($"Data Source={_witBTreePath};Store=btree;Transactions=true;MVCC=false");
                _witBTreeConn.Open();
                SetupWitTable(_witBTreeConn);
                break;
                
            case FullDbEngine.WitDb_BTree_Parallel:
                _witBTreeParallelConn = new WitDbConnection($"Data Source={_witBTreeParallelPath};Store=btree;Transactions=true;MVCC=false;Parallel Mode=Auto");
                _witBTreeParallelConn.Open();
                SetupWitTable(_witBTreeParallelConn);
                break;
                
            case FullDbEngine.WitDb_LSM:
                _witLsmConn = new WitDbConnection($"Data Source={_witLsmPath};Store=lsm;Transactions=true;MVCC=false;SyncWrites=false");
                _witLsmConn.Open();
                SetupWitTable(_witLsmConn);
                break;
                
            case FullDbEngine.WitDb_LSM_Parallel:
                _witLsmParallelConn = new WitDbConnection($"Data Source={_witLsmParallelPath};Store=lsm;Transactions=true;MVCC=false;SyncWrites=false;Parallel Mode=Auto");
                _witLsmParallelConn.Open();
                SetupWitTable(_witLsmParallelConn);
                break;
                
            case FullDbEngine.SQLite:
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
                
            case FullDbEngine.LiteDB:
                _liteDb = new LiteDatabase(_liteDbPath);
                _liteDb.GetCollection<BsonDocument>("Items").DeleteAll();
                break;
        }
    }

    private void SetupWitTable(WitDbConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DROP TABLE IF EXISTS Items";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "CREATE TABLE Items (Id BIGINT PRIMARY KEY AUTOINCREMENT, Name VARCHAR(100), Value DOUBLE, Category VARCHAR(50))";
        cmd.ExecuteNonQuery();
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _witBTreeConn?.Dispose(); _witBTreeConn = null;
        _witBTreeParallelConn?.Dispose(); _witBTreeParallelConn = null;
        _witLsmConn?.Dispose(); _witLsmConn = null;
        _witLsmParallelConn?.Dispose(); _witLsmParallelConn = null;
        _sqliteConn?.Dispose(); _sqliteConn = null;
        _liteDb?.Dispose(); _liteDb = null;
        
        CleanupFiles();
    }

    [GlobalCleanup]
    public void GlobalCleanup() => IterationCleanup();

    private void CleanupFiles()
    {
        try { File.Delete(_witBTreePath); } catch { }
        try { File.Delete(_witBTreeParallelPath); } catch { }
        try { Directory.Delete(_witLsmPath, true); } catch { }
        try { Directory.Delete(_witLsmParallelPath, true); } catch { }
        try { File.Delete(_sqlitePath); } catch { }
        try { File.Delete(_liteDbPath); } catch { }
        
        // Delete WitDb index directories
        try { if (Directory.Exists(_witBTreePath + "_indexes")) Directory.Delete(_witBTreePath + "_indexes", true); } catch { }
        try { if (Directory.Exists(_witBTreeParallelPath + "_indexes")) Directory.Delete(_witBTreeParallelPath + "_indexes", true); } catch { }
    }

    private WitDbConnection GetWitConnection() => Database switch
    {
        FullDbEngine.WitDb_BTree => _witBTreeConn!,
        FullDbEngine.WitDb_BTree_Parallel => _witBTreeParallelConn!,
        FullDbEngine.WitDb_LSM => _witLsmConn!,
        FullDbEngine.WitDb_LSM_Parallel => _witLsmParallelConn!,
        _ => throw new InvalidOperationException()
    };

    [Benchmark(Description = "INSERT Standard Loop")]
    public int InsertStandardLoop()
    {
        int count = 0;

        if (Database is FullDbEngine.WitDb_BTree or FullDbEngine.WitDb_BTree_Parallel 
            or FullDbEngine.WitDb_LSM or FullDbEngine.WitDb_LSM_Parallel)
        {
            var conn = GetWitConnection();
            using var tx = (WitDbTransaction)conn.BeginTransaction();
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO Items (Name, Value, Category) VALUES (@n, @v, @c)";
            var pn = cmd.CreateParameter(); pn.ParameterName = "@n"; cmd.Parameters.Add(pn);
            var pv = cmd.CreateParameter(); pv.ParameterName = "@v"; cmd.Parameters.Add(pv);
            var pc = cmd.CreateParameter(); pc.ParameterName = "@c"; cmd.Parameters.Add(pc);
            
            for (int i = 0; i < RowCount; i++)
            {
                pn.Value = $"Item{i}";
                pv.Value = i * 1.5;
                pc.Value = $"Cat{i % 10}";
                count += cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }
        else if (Database == FullDbEngine.SQLite)
        {
            using var tx = _sqliteConn!.BeginTransaction();
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
                pc.Value = $"Cat{i % 10}";
                count += cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }
        else // LiteDB
        {
            var col = _liteDb!.GetCollection<BsonDocument>("Items");
            _liteDb.BeginTrans();
            for (int i = 0; i < RowCount; i++)
            {
                col.Insert(new BsonDocument
                {
                    ["Name"] = $"Item{i}",
                    ["Value"] = i * 1.5,
                    ["Category"] = $"Cat{i % 10}"
                });
                count++;
            }
            _liteDb.Commit();
        }

        return count;
    }

    public void Dispose() => GlobalCleanup();
}

#endregion

#region INSERT Benchmarks - Bulk API

[Config(typeof(FullComparisonConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class FullInsertBulkBenchmarks : IDisposable
{
    private WitDbConnection? _witBTreeConn;
    private WitDbConnection? _witBTreeParallelConn;
    private SqliteConnection? _sqliteConn;
    private LiteDatabase? _liteDb;
    
    private string _witBTreePath = null!;
    private string _witBTreeParallelPath = null!;
    private string _sqlitePath = null!;
    private string _liteDbPath = null!;

    [Params(1000, 5000, 10000)]
    public int RowCount { get; set; }

    [Params(FullDbEngine.WitDb_BTree, FullDbEngine.WitDb_BTree_Parallel, FullDbEngine.SQLite, FullDbEngine.LiteDB)]
    public FullDbEngine Database { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        var id = Guid.NewGuid().ToString("N");
        _witBTreePath = Path.Combine(Path.GetTempPath(), $"wit_btree_bulk_{id}.witdb");
        _witBTreeParallelPath = Path.Combine(Path.GetTempPath(), $"wit_btree_par_bulk_{id}.witdb");
        _sqlitePath = Path.Combine(Path.GetTempPath(), $"sql_bulk_{id}.db");
        _liteDbPath = Path.Combine(Path.GetTempPath(), $"lite_bulk_{id}.db");
    }

    [IterationSetup]
    public void IterationSetup()
    {
        switch (Database)
        {
            case FullDbEngine.WitDb_BTree:
                _witBTreeConn = new WitDbConnection($"Data Source={_witBTreePath};Store=btree;Transactions=true;MVCC=false");
                _witBTreeConn.Open();
                SetupWitTable(_witBTreeConn);
                break;
                
            case FullDbEngine.WitDb_BTree_Parallel:
                _witBTreeParallelConn = new WitDbConnection($"Data Source={_witBTreeParallelPath};Store=btree;Transactions=true;MVCC=false;Parallel Mode=Auto");
                _witBTreeParallelConn.Open();
                SetupWitTable(_witBTreeParallelConn);
                break;
                
            case FullDbEngine.SQLite:
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
                
            case FullDbEngine.LiteDB:
                _liteDb = new LiteDatabase(_liteDbPath);
                _liteDb.GetCollection<BsonDocument>("Items").DeleteAll();
                break;
                
            default:
                // Skip LSM for bulk tests - focus on BTree vs others
                break;
        }
    }

    private void SetupWitTable(WitDbConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DROP TABLE IF EXISTS Items";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "CREATE TABLE Items (Id BIGINT PRIMARY KEY AUTOINCREMENT, Name VARCHAR(100), Value DOUBLE, Category VARCHAR(50))";
        cmd.ExecuteNonQuery();
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _witBTreeConn?.Dispose(); _witBTreeConn = null;
        _witBTreeParallelConn?.Dispose(); _witBTreeParallelConn = null;
        _sqliteConn?.Dispose(); _sqliteConn = null;
        _liteDb?.Dispose(); _liteDb = null;
        
        try { File.Delete(_witBTreePath); } catch { }
        try { File.Delete(_witBTreeParallelPath); } catch { }
        try { File.Delete(_sqlitePath); } catch { }
        try { File.Delete(_liteDbPath); } catch { }
    }

    [GlobalCleanup]
    public void GlobalCleanup() => IterationCleanup();

    private IEnumerable<Dictionary<string, object?>> GenerateRows()
    {
        return Enumerable.Range(0, RowCount)
            .Select(i => new Dictionary<string, object?>
            {
                ["Name"] = $"Item{i}",
                ["Value"] = i * 1.5,
                ["Category"] = $"Cat{i % 10}"
            });
    }

    [Benchmark(Description = "INSERT Bulk API")]
    public int InsertBulk()
    {
        switch (Database)
        {
            case FullDbEngine.WitDb_BTree:
                return _witBTreeConn!.Engine!.BulkInsert("Items", GenerateRows());
                
            case FullDbEngine.WitDb_BTree_Parallel:
                return _witBTreeParallelConn!.Engine!.BulkInsert("Items", GenerateRows());
                
            case FullDbEngine.SQLite:
                // SQLite doesn't have bulk API, use prepared statement loop
                using (var tx = _sqliteConn!.BeginTransaction())
                {
                    using var cmd = _sqliteConn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = "INSERT INTO Items (Name, Value, Category) VALUES (@n, @v, @c)";
                    var pn = cmd.CreateParameter(); pn.ParameterName = "@n"; cmd.Parameters.Add(pn);
                    var pv = cmd.CreateParameter(); pv.ParameterName = "@v"; cmd.Parameters.Add(pv);
                    var pc = cmd.CreateParameter(); pc.ParameterName = "@c"; cmd.Parameters.Add(pc);
                    
                    int count = 0;
                    foreach (var row in GenerateRows())
                    {
                        pn.Value = row["Name"];
                        pv.Value = row["Value"];
                        pc.Value = row["Category"];
                        count += cmd.ExecuteNonQuery();
                    }
                    tx.Commit();
                    return count;
                }
                
            case FullDbEngine.LiteDB:
                // LiteDB InsertBulk
                var docs = GenerateRows().Select(r => new BsonDocument
                {
                    ["Name"] = (string)r["Name"]!,
                    ["Value"] = (double)r["Value"]!,
                    ["Category"] = (string)r["Category"]!
                });
                return _liteDb!.GetCollection<BsonDocument>("Items").InsertBulk(docs);
                
            default:
                return 0;
        }
    }

    public void Dispose() => GlobalCleanup();
}

#endregion

#region READ Benchmarks (SELECT, COUNT, SUM, GROUP BY)

[Config(typeof(FullComparisonConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class FullReadBenchmarks : IDisposable
{
    private WitDbConnection? _witBTreeConn;
    private WitDbConnection? _witBTreeParallelConn;
    private SqliteConnection? _sqliteConn;
    private LiteDatabase? _liteDb;
    
    private string _benchDir = null!;
    private string _witBTreePath = null!;
    private string _witBTreeParallelPath = null!;
    private string _sqlitePath = null!;
    private string _liteDbPath = null!;
    
    private int[] _randomIds = null!;

    [Params(1000, 5000, 10000)]
    public int TableSize { get; set; }

    [Params(FullDbEngine.WitDb_BTree, FullDbEngine.WitDb_BTree_Parallel, FullDbEngine.SQLite, FullDbEngine.LiteDB)]
    public FullDbEngine Database { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Create unique temp directory for this benchmark run
        _benchDir = Path.Combine(Path.GetTempPath(), $"wit_bench_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_benchDir);
        
        _witBTreePath = Path.Combine(_benchDir, "btree.witdb");
        _witBTreeParallelPath = Path.Combine(_benchDir, "btree_par.witdb");
        _sqlitePath = Path.Combine(_benchDir, "sqlite.db");
        _liteDbPath = Path.Combine(_benchDir, "litedb.db");
        
        var rnd = new Random(42);
        _randomIds = Enumerable.Range(0, 100).Select(_ => rnd.Next(1, TableSize + 1)).ToArray();

        switch (Database)
        {
            case FullDbEngine.WitDb_BTree:
                _witBTreeConn = new WitDbConnection($"Data Source={_witBTreePath};Store=btree;Transactions=true;MVCC=false");
                _witBTreeConn.Open();
                SetupWitDbWithData(_witBTreeConn);
                break;
                
            case FullDbEngine.WitDb_BTree_Parallel:
                _witBTreeParallelConn = new WitDbConnection($"Data Source={_witBTreeParallelPath};Store=btree;Transactions=true;MVCC=false;Parallel Mode=Auto");
                _witBTreeParallelConn.Open();
                SetupWitDbWithData(_witBTreeParallelConn);
                break;
                
            case FullDbEngine.SQLite:
                SetupSqliteWithData();
                break;
                
            case FullDbEngine.LiteDB:
                SetupLiteDbWithData();
                break;
        }
    }

    private void SetupWitDbWithData(WitDbConnection conn)
    {
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "CREATE TABLE Items (Id BIGINT PRIMARY KEY AUTOINCREMENT, Name VARCHAR(100), Value DOUBLE, Category VARCHAR(50))";
            cmd.ExecuteNonQuery();
        }

        var rows = Enumerable.Range(0, TableSize)
            .Select(i => new Dictionary<string, object?>
            {
                ["Name"] = $"Item{i}",
                ["Value"] = i * 1.5,
                ["Category"] = $"Cat{i % 10}"
            });
        conn.Engine!.BulkInsert("Items", rows);

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "CREATE INDEX IX_Category ON Items(Category)";
            cmd.ExecuteNonQuery();
        }
    }

    private void SetupSqliteWithData()
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
                pc.Value = $"Cat{i % 10}";
                cmd.ExecuteNonQuery();
            }
        }
        tx.Commit();

        using (var cmd = _sqliteConn.CreateCommand())
        {
            cmd.CommandText = "CREATE INDEX IX_Category ON Items(Category)";
            cmd.ExecuteNonQuery();
        }
    }

    private void SetupLiteDbWithData()
    {
        _liteDb = new LiteDatabase(_liteDbPath);
        var col = _liteDb.GetCollection<BsonDocument>("Items");
        
        var docs = Enumerable.Range(0, TableSize)
            .Select(i => new BsonDocument
            {
                ["_id"] = i + 1,
                ["Name"] = $"Item{i}",
                ["Value"] = i * 1.5,
                ["Category"] = $"Cat{i % 10}"
            });
        col.InsertBulk(docs);
        col.EnsureIndex("Category");
    }

    private void CleanupFiles()
    {
        // Delete entire benchmark directory
        if (!string.IsNullOrEmpty(_benchDir) && Directory.Exists(_benchDir))
        {
            try { Directory.Delete(_benchDir, true); } catch { }
        }
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _witBTreeConn?.Dispose(); _witBTreeConn = null;
        _witBTreeParallelConn?.Dispose(); _witBTreeParallelConn = null;
        _sqliteConn?.Dispose(); _sqliteConn = null;
        _liteDb?.Dispose(); _liteDb = null;
        
        CleanupFiles();
    }

    private WitDbConnection GetWitConnection() => Database switch
    {
        FullDbEngine.WitDb_BTree => _witBTreeConn!,
        FullDbEngine.WitDb_BTree_Parallel => _witBTreeParallelConn!,
        _ => throw new InvalidOperationException()
    };

    #region COUNT

    [Benchmark(Description = "COUNT(*)")]
    public long CountAll()
    {
        switch (Database)
        {
            case FullDbEngine.WitDb_BTree:
            case FullDbEngine.WitDb_BTree_Parallel:
                using (var cmd = GetWitConnection().CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM Items";
                    return Convert.ToInt64(cmd.ExecuteScalar());
                }

            case FullDbEngine.SQLite:
                using (var cmd = _sqliteConn!.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM Items";
                    return Convert.ToInt64(cmd.ExecuteScalar());
                }

            case FullDbEngine.LiteDB:
                return _liteDb!.GetCollection<BsonDocument>("Items").Count();
        }
        return 0;
    }

    #endregion

    #region Point Query

    [Benchmark(Description = "SELECT by PK (100x)")]
    public int SelectByPk()
    {
        int found = 0;

        switch (Database)
        {
            case FullDbEngine.WitDb_BTree:
            case FullDbEngine.WitDb_BTree_Parallel:
                using (var cmd = GetWitConnection().CreateCommand())
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

            case FullDbEngine.SQLite:
                using (var cmd = _sqliteConn!.CreateCommand())
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

            case FullDbEngine.LiteDB:
                var col = _liteDb!.GetCollection<BsonDocument>("Items");
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

    #region Full Scan

    [Benchmark(Description = "SELECT * (Full Scan)")]
    public int SelectFullScan()
    {
        int count = 0;

        switch (Database)
        {
            case FullDbEngine.WitDb_BTree:
            case FullDbEngine.WitDb_BTree_Parallel:
                using (var cmd = GetWitConnection().CreateCommand())
                {
                    cmd.CommandText = "SELECT Id, Name, Value, Category FROM Items";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read()) count++;
                }
                break;

            case FullDbEngine.SQLite:
                using (var cmd = _sqliteConn!.CreateCommand())
                {
                    cmd.CommandText = "SELECT Id, Name, Value, Category FROM Items";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read()) count++;
                }
                break;

            case FullDbEngine.LiteDB:
                foreach (var _ in _liteDb!.GetCollection<BsonDocument>("Items").FindAll())
                    count++;
                break;
        }

        return count;
    }

    #endregion

    #region SUM

    [Benchmark(Description = "SUM(Value)")]
    public double SumValue()
    {
        switch (Database)
        {
            case FullDbEngine.WitDb_BTree:
            case FullDbEngine.WitDb_BTree_Parallel:
                using (var cmd = GetWitConnection().CreateCommand())
                {
                    cmd.CommandText = "SELECT SUM(Value) FROM Items";
                    return Convert.ToDouble(cmd.ExecuteScalar());
                }

            case FullDbEngine.SQLite:
                using (var cmd = _sqliteConn!.CreateCommand())
                {
                    cmd.CommandText = "SELECT SUM(Value) FROM Items";
                    return Convert.ToDouble(cmd.ExecuteScalar());
                }

            case FullDbEngine.LiteDB:
                double sum = 0;
                foreach (var doc in _liteDb!.GetCollection<BsonDocument>("Items").FindAll())
                    sum += doc["Value"].AsDouble;
                return sum;
        }
        return 0;
    }

    #endregion

    #region GROUP BY

    [Benchmark(Description = "GROUP BY Category")]
    public int GroupByCategory()
    {
        int groups = 0;

        switch (Database)
        {
            case FullDbEngine.WitDb_BTree:
            case FullDbEngine.WitDb_BTree_Parallel:
                using (var cmd = GetWitConnection().CreateCommand())
                {
                    cmd.CommandText = "SELECT Category, COUNT(*), AVG(Value) FROM Items GROUP BY Category";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read()) groups++;
                }
                break;

            case FullDbEngine.SQLite:
                using (var cmd = _sqliteConn!.CreateCommand())
                {
                    cmd.CommandText = "SELECT Category, COUNT(*), AVG(Value) FROM Items GROUP BY Category";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read()) groups++;
                }
                break;

            case FullDbEngine.LiteDB:
                var grouped = _liteDb!.GetCollection<BsonDocument>("Items")
                    .FindAll()
                    .GroupBy(d => d["Category"].AsString)
                    .Select(g => new { Category = g.Key, Count = g.Count(), Avg = g.Average(d => d["Value"].AsDouble) })
                    .ToList();
                groups = grouped.Count;
                break;
        }

        return groups;
    }

    #endregion

    #region WHERE with Index

    [Benchmark(Description = "SELECT WHERE Category (indexed)")]
    public int SelectByCategory()
    {
        int count = 0;
        string category = "Cat5"; // Should match ~10% of rows

        switch (Database)
        {
            case FullDbEngine.WitDb_BTree:
            case FullDbEngine.WitDb_BTree_Parallel:
                using (var cmd = GetWitConnection().CreateCommand())
                {
                    cmd.CommandText = "SELECT Id, Name, Value FROM Items WHERE Category = @c";
                    var p = cmd.CreateParameter(); p.ParameterName = "@c"; p.Value = category; cmd.Parameters.Add(p);
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read()) count++;
                }
                break;

            case FullDbEngine.SQLite:
                using (var cmd = _sqliteConn!.CreateCommand())
                {
                    cmd.CommandText = "SELECT Id, Name, Value FROM Items WHERE Category = @c";
                    var p = cmd.CreateParameter(); p.ParameterName = "@c"; p.Value = category; cmd.Parameters.Add(p);
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read()) count++;
                }
                break;

            case FullDbEngine.LiteDB:
                var docs = _liteDb!.GetCollection<BsonDocument>("Items").Find(d => d["Category"] == category);
                foreach (var _ in docs) count++;
                break;
        }

        return count;
    }

    #endregion

    public void Dispose() => GlobalCleanup();
}

#endregion

#region UPDATE Benchmarks

[Config(typeof(FullComparisonConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class FullUpdateBenchmarks : IDisposable
{
    private WitDbConnection? _witBTreeConn;
    private WitDbConnection? _witBTreeParallelConn;
    private SqliteConnection? _sqliteConn;
    private LiteDatabase? _liteDb;
    
    private string _witBTreePath = null!;
    private string _witBTreeParallelPath = null!;
    private string _sqlitePath = null!;
    private string _liteDbPath = null!;

    [Params(100, 1000, 5000)]
    public int RowCount { get; set; }

    [Params(FullDbEngine.WitDb_BTree, FullDbEngine.WitDb_BTree_Parallel, FullDbEngine.SQLite, FullDbEngine.LiteDB)]
    public FullDbEngine Database { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        var id = Guid.NewGuid().ToString("N");
        _witBTreePath = Path.Combine(Path.GetTempPath(), $"wit_btree_upd_{id}.witdb");
        _witBTreeParallelPath = Path.Combine(Path.GetTempPath(), $"wit_btree_par_upd_{id}.witdb");
        _sqlitePath = Path.Combine(Path.GetTempPath(), $"sql_upd_{id}.db");
        _liteDbPath = Path.Combine(Path.GetTempPath(), $"lite_upd_{id}.db");
    }

    [IterationSetup]
    public void IterationSetup()
    {
        switch (Database)
        {
            case FullDbEngine.WitDb_BTree:
                _witBTreeConn = new WitDbConnection($"Data Source={_witBTreePath};Store=btree;Transactions=true;MVCC=false");
                _witBTreeConn.Open();
                SetupWitDbWithData(_witBTreeConn);
                break;
                
            case FullDbEngine.WitDb_BTree_Parallel:
                _witBTreeParallelConn = new WitDbConnection($"Data Source={_witBTreeParallelPath};Store=btree;Transactions=true;MVCC=false;Parallel Mode=Auto");
                _witBTreeParallelConn.Open();
                SetupWitDbWithData(_witBTreeParallelConn);
                break;
                
            case FullDbEngine.SQLite:
                SetupSqliteWithData();
                break;
                
            case FullDbEngine.LiteDB:
                SetupLiteDbWithData();
                break;
        }
    }

    private void SetupWitDbWithData(WitDbConnection conn)
    {
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DROP TABLE IF EXISTS Items";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE TABLE Items (Id BIGINT PRIMARY KEY AUTOINCREMENT, Name VARCHAR(100), Value DOUBLE, Category VARCHAR(50))";
            cmd.ExecuteNonQuery();
        }

        var rows = Enumerable.Range(0, RowCount)
            .Select(i => new Dictionary<string, object?>
            {
                ["Name"] = $"Item{i}",
                ["Value"] = i * 1.5,
                ["Category"] = $"Cat{i % 10}"
            });
        conn.Engine!.BulkInsert("Items", rows);
    }

    private void SetupSqliteWithData()
    {
        _sqliteConn = new SqliteConnection($"Data Source={_sqlitePath}");
        _sqliteConn.Open();
        
        using (var cmd = _sqliteConn.CreateCommand())
        {
            cmd.CommandText = "DROP TABLE IF EXISTS Items";
            cmd.ExecuteNonQuery();
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
            
            for (int i = 0; i < RowCount; i++)
            {
                pn.Value = $"Item{i}";
                pv.Value = i * 1.5;
                pc.Value = $"Cat{i % 10}";
                cmd.ExecuteNonQuery();
            }
        }
        tx.Commit();
    }

    private void SetupLiteDbWithData()
    {
        _liteDb = new LiteDatabase(_liteDbPath);
        var col = _liteDb.GetCollection<BsonDocument>("Items");
        col.DeleteAll();
        
        var docs = Enumerable.Range(0, RowCount)
            .Select(i => new BsonDocument
            {
                ["_id"] = i + 1,
                ["Name"] = $"Item{i}",
                ["Value"] = i * 1.5,
                ["Category"] = $"Cat{i % 10}"
            });
        col.InsertBulk(docs);
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _witBTreeConn?.Dispose(); _witBTreeConn = null;
        _witBTreeParallelConn?.Dispose(); _witBTreeParallelConn = null;
        _sqliteConn?.Dispose(); _sqliteConn = null;
        _liteDb?.Dispose(); _liteDb = null;
        
        try { File.Delete(_witBTreePath); } catch { }
        try { File.Delete(_witBTreeParallelPath); } catch { }
        try { File.Delete(_sqlitePath); } catch { }
        try { File.Delete(_liteDbPath); } catch { }
    }

    [GlobalCleanup]
    public void GlobalCleanup() => IterationCleanup();

    private WitDbConnection GetWitConnection() => Database switch
    {
        FullDbEngine.WitDb_BTree => _witBTreeConn!,
        FullDbEngine.WitDb_BTree_Parallel => _witBTreeParallelConn!,
        _ => throw new InvalidOperationException()
    };

    [Benchmark(Description = "UPDATE by PK (all rows)")]
    public int UpdateByPk()
    {
        int updated = 0;

        switch (Database)
        {
            case FullDbEngine.WitDb_BTree:
            case FullDbEngine.WitDb_BTree_Parallel:
                using (var tx = (WitDbTransaction)GetWitConnection().BeginTransaction())
                {
                    using var cmd = GetWitConnection().CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = "UPDATE Items SET Value = @v WHERE Id = @id";
                    var pv = cmd.CreateParameter(); pv.ParameterName = "@v"; cmd.Parameters.Add(pv);
                    var pid = cmd.CreateParameter(); pid.ParameterName = "@id"; cmd.Parameters.Add(pid);
                    
                    for (int i = 1; i <= RowCount; i++)
                    {
                        pv.Value = i * 2.5;
                        pid.Value = i;
                        updated += cmd.ExecuteNonQuery();
                    }
                    tx.Commit();
                }
                break;

            case FullDbEngine.SQLite:
                using (var tx = _sqliteConn!.BeginTransaction())
                {
                    using var cmd = _sqliteConn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = "UPDATE Items SET Value = @v WHERE Id = @id";
                    var pv = cmd.CreateParameter(); pv.ParameterName = "@v"; cmd.Parameters.Add(pv);
                    var pid = cmd.CreateParameter(); pid.ParameterName = "@id"; cmd.Parameters.Add(pid);
                    
                    for (int i = 1; i <= RowCount; i++)
                    {
                        pv.Value = i * 2.5;
                        pid.Value = i;
                        updated += cmd.ExecuteNonQuery();
                    }
                    tx.Commit();
                }
                break;

            case FullDbEngine.LiteDB:
                var col = _liteDb!.GetCollection<BsonDocument>("Items");
                _liteDb.BeginTrans();
                for (int i = 1; i <= RowCount; i++)
                {
                    var doc = col.FindById(i);
                    if (doc != null)
                    {
                        doc["Value"] = i * 2.5;
                        if (col.Update(doc)) updated++;
                    }
                }
                _liteDb.Commit();
                break;
        }

        return updated;
    }

    [Benchmark(Description = "UPDATE SET (batch)")]
    public int UpdateBatch()
    {
        switch (Database)
        {
            case FullDbEngine.WitDb_BTree:
            case FullDbEngine.WitDb_BTree_Parallel:
                using (var cmd = GetWitConnection().CreateCommand())
                {
                    cmd.CommandText = "UPDATE Items SET Value = Value * 2 WHERE Category = 'Cat5'";
                    return cmd.ExecuteNonQuery();
                }

            case FullDbEngine.SQLite:
                using (var cmd = _sqliteConn!.CreateCommand())
                {
                    cmd.CommandText = "UPDATE Items SET Value = Value * 2 WHERE Category = 'Cat5'";
                    return cmd.ExecuteNonQuery();
                }

            case FullDbEngine.LiteDB:
                var col = _liteDb!.GetCollection<BsonDocument>("Items");
                int updated = 0;
                _liteDb.BeginTrans();
                var docs = col.Find(d => d["Category"] == "Cat5").ToList();
                foreach (var doc in docs)
                {
                    doc["Value"] = doc["Value"].AsDouble * 2;
                    if (col.Update(doc)) updated++;
                }
                _liteDb.Commit();
                return updated;
        }
        return 0;
    }

    public void Dispose() => GlobalCleanup();
}

#endregion

#region DELETE Benchmarks

[Config(typeof(FullComparisonConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class FullDeleteBenchmarks : IDisposable
{
    private WitDbConnection? _witBTreeConn;
    private WitDbConnection? _witBTreeParallelConn;
    private SqliteConnection? _sqliteConn;
    private LiteDatabase? _liteDb;
    
    private string _witBTreePath = null!;
    private string _witBTreeParallelPath = null!;
    private string _sqlitePath = null!;
    private string _liteDbPath = null!;

    [Params(100, 1000, 5000)]
    public int RowCount { get; set; }

    [Params(FullDbEngine.WitDb_BTree, FullDbEngine.WitDb_BTree_Parallel, FullDbEngine.SQLite, FullDbEngine.LiteDB)]
    public FullDbEngine Database { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        var id = Guid.NewGuid().ToString("N");
        _witBTreePath = Path.Combine(Path.GetTempPath(), $"wit_btree_del_{id}.witdb");
        _witBTreeParallelPath = Path.Combine(Path.GetTempPath(), $"wit_btree_par_del_{id}.witdb");
        _sqlitePath = Path.Combine(Path.GetTempPath(), $"sql_del_{id}.db");
        _liteDbPath = Path.Combine(Path.GetTempPath(), $"lite_del_{id}.db");
    }

    [IterationSetup]
    public void IterationSetup()
    {
        switch (Database)
        {
            case FullDbEngine.WitDb_BTree:
                _witBTreeConn = new WitDbConnection($"Data Source={_witBTreePath};Store=btree;Transactions=true;MVCC=false");
                _witBTreeConn.Open();
                SetupWitDbWithData(_witBTreeConn);
                break;
                
            case FullDbEngine.WitDb_BTree_Parallel:
                _witBTreeParallelConn = new WitDbConnection($"Data Source={_witBTreeParallelPath};Store=btree;Transactions=true;MVCC=false;Parallel Mode=Auto");
                _witBTreeParallelConn.Open();
                SetupWitDbWithData(_witBTreeParallelConn);
                break;
                
            case FullDbEngine.SQLite:
                SetupSqliteWithData();
                break;
                
            case FullDbEngine.LiteDB:
                SetupLiteDbWithData();
                break;
        }
    }

    private void SetupWitDbWithData(WitDbConnection conn)
    {
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DROP TABLE IF EXISTS Items";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE TABLE Items (Id BIGINT PRIMARY KEY AUTOINCREMENT, Name VARCHAR(100), Value DOUBLE, Category VARCHAR(50))";
            cmd.ExecuteNonQuery();
        }

        var rows = Enumerable.Range(0, RowCount)
            .Select(i => new Dictionary<string, object?>
            {
                ["Name"] = $"Item{i}",
                ["Value"] = i * 1.5,
                ["Category"] = $"Cat{i % 10}"
            });
        conn.Engine!.BulkInsert("Items", rows);
    }

    private void SetupSqliteWithData()
    {
        _sqliteConn = new SqliteConnection($"Data Source={_sqlitePath}");
        _sqliteConn.Open();
        
        using (var cmd = _sqliteConn.CreateCommand())
        {
            cmd.CommandText = "DROP TABLE IF EXISTS Items";
            cmd.ExecuteNonQuery();
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
            
            for (int i = 0; i < RowCount; i++)
            {
                pn.Value = $"Item{i}";
                pv.Value = i * 1.5;
                pc.Value = $"Cat{i % 10}";
                cmd.ExecuteNonQuery();
            }
        }
        tx.Commit();
    }

    private void SetupLiteDbWithData()
    {
        _liteDb = new LiteDatabase(_liteDbPath);
        var col = _liteDb.GetCollection<BsonDocument>("Items");
        col.DeleteAll();
        
        var docs = Enumerable.Range(0, RowCount)
            .Select(i => new BsonDocument
            {
                ["_id"] = i + 1,
                ["Name"] = $"Item{i}",
                ["Value"] = i * 1.5,
                ["Category"] = $"Cat{i % 10}"
            });
        col.InsertBulk(docs);
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _witBTreeConn?.Dispose(); _witBTreeConn = null;
        _witBTreeParallelConn?.Dispose(); _witBTreeParallelConn = null;
        _sqliteConn?.Dispose(); _sqliteConn = null;
        _liteDb?.Dispose(); _liteDb = null;
        
        try { File.Delete(_witBTreePath); } catch { }
        try { File.Delete(_witBTreeParallelPath); } catch { }
        try { File.Delete(_sqlitePath); } catch { }
        try { File.Delete(_liteDbPath); } catch { }
        
        // Delete WitDb index directories
        try { if (Directory.Exists(_witBTreePath + "_indexes")) Directory.Delete(_witBTreePath + "_indexes", true); } catch { }
        try { if (Directory.Exists(_witBTreeParallelPath + "_indexes")) Directory.Delete(_witBTreeParallelPath + "_indexes", true); } catch { }
    }

    [GlobalCleanup]
    public void GlobalCleanup() => IterationCleanup();

    private WitDbConnection GetWitConnection() => Database switch
    {
        FullDbEngine.WitDb_BTree => _witBTreeConn!,
        FullDbEngine.WitDb_BTree_Parallel => _witBTreeParallelConn!,
        _ => throw new InvalidOperationException()
    };

    [Benchmark(Description = "DELETE by PK (50%)")]
    public int DeleteByPk()
    {
        int deleted = 0;
        int halfCount = RowCount / 2;

        switch (Database)
        {
            case FullDbEngine.WitDb_BTree:
            case FullDbEngine.WitDb_BTree_Parallel:
                using (var tx = (WitDbTransaction)GetWitConnection().BeginTransaction())
                {
                    using var cmd = GetWitConnection().CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = "DELETE FROM Items WHERE Id = @id";
                    var pid = cmd.CreateParameter(); pid.ParameterName = "@id"; cmd.Parameters.Add(pid);
                    
                    for (int i = 1; i <= halfCount; i++)
                    {
                        pid.Value = i;
                        deleted += cmd.ExecuteNonQuery();
                    }
                    tx.Commit();
                }
                break;

            case FullDbEngine.SQLite:
                using (var tx = _sqliteConn!.BeginTransaction())
                {
                    using var cmd = _sqliteConn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = "DELETE FROM Items WHERE Id = @id";
                    var pid = cmd.CreateParameter(); pid.ParameterName = "@id"; cmd.Parameters.Add(pid);
                    
                    for (int i = 1; i <= halfCount; i++)
                    {
                        pid.Value = i;
                        deleted += cmd.ExecuteNonQuery();
                    }
                    tx.Commit();
                }
                break;

            case FullDbEngine.LiteDB:
                var col = _liteDb!.GetCollection<BsonDocument>("Items");
                _liteDb.BeginTrans();
                for (int i = 1; i <= halfCount; i++)
                {
                    if (col.Delete(i)) deleted++;
                }
                _liteDb.Commit();
                break;
        }

        return deleted;
    }

    [Benchmark(Description = "DELETE WHERE (batch)")]
    public int DeleteBatch()
    {
        switch (Database)
        {
            case FullDbEngine.WitDb_BTree:
            case FullDbEngine.WitDb_BTree_Parallel:
                using (var cmd = GetWitConnection().CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM Items WHERE Category = 'Cat5'";
                    return cmd.ExecuteNonQuery();
                }

            case FullDbEngine.SQLite:
                using (var cmd = _sqliteConn!.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM Items WHERE Category = 'Cat5'";
                    return cmd.ExecuteNonQuery();
                }

            case FullDbEngine.LiteDB:
                return _liteDb!.GetCollection<BsonDocument>("Items").DeleteMany(d => d["Category"] == "Cat5");
        }
        return 0;
    }

    public void Dispose() => GlobalCleanup();
}

#endregion

#region DML Fast Path Benchmarks

/// <summary>
/// Benchmarks for UPDATE/DELETE Fast Path optimization.
/// Demonstrates performance improvement from direct PK lookup vs iterator-based execution.
/// </summary>
[Config(typeof(FullComparisonConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class DmlFastPathBenchmarks : IDisposable
{
    private WitDbConnection? _witConn;
    private SqliteConnection? _sqliteConn;
    private LiteDatabase? _liteDb;
    
    private string _witPath = null!;
    private string _sqlitePath = null!;
    private string _liteDbPath = null!;

    [Params(100, 1000)]
    public int RowCount { get; set; }

    [Params(FullDbEngine.WitDb_BTree, FullDbEngine.SQLite, FullDbEngine.LiteDB)]
    public FullDbEngine Database { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        var id = Guid.NewGuid().ToString("N");
        _witPath = Path.Combine(Path.GetTempPath(), $"wit_dml_{id}.witdb");
        _sqlitePath = Path.Combine(Path.GetTempPath(), $"sql_dml_{id}.db");
        _liteDbPath = Path.Combine(Path.GetTempPath(), $"lite_dml_{id}.db");
    }

    [IterationSetup]
    public void IterationSetup()
    {
        switch (Database)
        {
            case FullDbEngine.WitDb_BTree:
                _witConn = new WitDbConnection($"Data Source={_witPath};Store=btree;Transactions=true;MVCC=false");
                _witConn.Open();
                SetupWitDbWithData(_witConn);
                break;
                
            case FullDbEngine.SQLite:
                SetupSqliteWithData();
                break;
                
            case FullDbEngine.LiteDB:
                SetupLiteDbWithData();
                break;
        }
    }

    private void SetupWitDbWithData(WitDbConnection conn)
    {
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DROP TABLE IF EXISTS Items";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE TABLE Items (Id BIGINT PRIMARY KEY AUTOINCREMENT, Name VARCHAR(100), Value DOUBLE, Category VARCHAR(50))";
            cmd.ExecuteNonQuery();
        }

        var rows = Enumerable.Range(0, RowCount)
            .Select(i => new Dictionary<string, object?>
            {
                ["Name"] = $"Item{i}",
                ["Value"] = i * 1.5,
                ["Category"] = $"Cat{i % 10}"
            });
        conn.Engine!.BulkInsert("Items", rows);
    }

    private void SetupSqliteWithData()
    {
        _sqliteConn = new SqliteConnection($"Data Source={_sqlitePath}");
        _sqliteConn.Open();
        
        using (var cmd = _sqliteConn.CreateCommand())
        {
            cmd.CommandText = "DROP TABLE IF EXISTS Items";
            cmd.ExecuteNonQuery();
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
            
            for (int i = 0; i < RowCount; i++)
            {
                pn.Value = $"Item{i}";
                pv.Value = i * 1.5;
                pc.Value = $"Cat{i % 10}";
                cmd.ExecuteNonQuery();
            }
        }
        tx.Commit();
    }

    private void SetupLiteDbWithData()
    {
        _liteDb = new LiteDatabase(_liteDbPath);
        var col = _liteDb.GetCollection<BsonDocument>("Items");
        col.DeleteAll();
        
        var docs = Enumerable.Range(0, RowCount)
            .Select(i => new BsonDocument
            {
                ["_id"] = i + 1,
                ["Name"] = $"Item{i}",
                ["Value"] = i * 1.5,
                ["Category"] = $"Cat{i % 10}"
            });
        col.InsertBulk(docs);
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _witConn?.Dispose(); _witConn = null;
        _sqliteConn?.Dispose(); _sqliteConn = null;
        _liteDb?.Dispose(); _liteDb = null;
        
        try { File.Delete(_witPath); } catch { }
        try { File.Delete(_sqlitePath); } catch { }
        try { File.Delete(_liteDbPath); } catch { }
    }

    [GlobalCleanup]
    public void GlobalCleanup() => IterationCleanup();

    [Benchmark(Description = "UPDATE by PK (prepared)")]
    public int UpdateByPkPrepared()
    {
        int updated = 0;
        int halfCount = RowCount / 2;

        switch (Database)
        {
            case FullDbEngine.WitDb_BTree:
                using (var tx = (WitDbTransaction)_witConn!.BeginTransaction())
                {
                    using var cmd = _witConn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = "UPDATE Items SET Value = @v WHERE Id = @id";
                    var pv = cmd.CreateParameter(); pv.ParameterName = "@v"; cmd.Parameters.Add(pv);
                    var pid = cmd.CreateParameter(); pid.ParameterName = "@id"; cmd.Parameters.Add(pid);
                    
                    for (int i = 1; i <= halfCount; i++)
                    {
                        pv.Value = i * 100.0;
                        pid.Value = i;
                        updated += cmd.ExecuteNonQuery();
                    }
                    tx.Commit();
                }
                break;

            case FullDbEngine.SQLite:
                using (var tx = _sqliteConn!.BeginTransaction())
                {
                    using var cmd = _sqliteConn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = "UPDATE Items SET Value = @v WHERE Id = @id";
                    var pv = cmd.CreateParameter(); pv.ParameterName = "@v"; cmd.Parameters.Add(pv);
                    var pid = cmd.CreateParameter(); pid.ParameterName = "@id"; cmd.Parameters.Add(pid);
                    
                    for (int i = 1; i <= halfCount; i++)
                    {
                        pv.Value = i * 100.0;
                        pid.Value = i;
                        updated += cmd.ExecuteNonQuery();
                    }
                    tx.Commit();
                }
                break;

            case FullDbEngine.LiteDB:
                var col = _liteDb!.GetCollection<BsonDocument>("Items");
                _liteDb.BeginTrans();
                for (int i = 1; i <= halfCount; i++)
                {
                    var doc = col.FindById(i);
                    if (doc != null)
                    {
                        doc["Value"] = i * 100.0;
                        if (col.Update(doc)) updated++;
                    }
                }
                _liteDb.Commit();
                break;
        }

        return updated;
    }

    [Benchmark(Description = "DELETE by PK (prepared)")]
    public int DeleteByPkPrepared()
    {
        int deleted = 0;
        int tenPercent = RowCount / 10;

        switch (Database)
        {
            case FullDbEngine.WitDb_BTree:
                using (var tx = (WitDbTransaction)_witConn!.BeginTransaction())
                {
                    using var cmd = _witConn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = "DELETE FROM Items WHERE Id = @id";
                    var pid = cmd.CreateParameter(); pid.ParameterName = "@id"; cmd.Parameters.Add(pid);
                    
                    for (int i = 1; i <= tenPercent; i++)
                    {
                        pid.Value = i;
                        deleted += cmd.ExecuteNonQuery();
                    }
                    tx.Commit();
                }
                break;

            case FullDbEngine.SQLite:
                using (var tx = _sqliteConn!.BeginTransaction())
                {
                    using var cmd = _sqliteConn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = "DELETE FROM Items WHERE Id = @id";
                    var pid = cmd.CreateParameter(); pid.ParameterName = "@id"; cmd.Parameters.Add(pid);
                    
                    for (int i = 1; i <= tenPercent; i++)
                    {
                        pid.Value = i;
                        deleted += cmd.ExecuteNonQuery();
                    }
                    tx.Commit();
                }
                break;

            case FullDbEngine.LiteDB:
                var col = _liteDb!.GetCollection<BsonDocument>("Items");
                _liteDb.BeginTrans();
                for (int i = 1; i <= tenPercent; i++)
                {
                    if (col.Delete(i)) deleted++;
                }
                _liteDb.Commit();
                break;
        }

        return deleted;
    }

    [Benchmark(Description = "UPDATE batch IN clause")]
    public int UpdateBatchIn()
    {
        // Update 10 rows with IN clause
        switch (Database)
        {
            case FullDbEngine.WitDb_BTree:
                using (var cmd = _witConn!.CreateCommand())
                {
                    cmd.CommandText = "UPDATE Items SET Value = 999 WHERE Id IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10)";
                    return cmd.ExecuteNonQuery();
                }

            case FullDbEngine.SQLite:
                using (var cmd = _sqliteConn!.CreateCommand())
                {
                    cmd.CommandText = "UPDATE Items SET Value = 999 WHERE Id IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10)";
                    return cmd.ExecuteNonQuery();
                }

            case FullDbEngine.LiteDB:
                // LiteDB doesn't have SQL IN clause, emulate with loop
                var col = _liteDb!.GetCollection<BsonDocument>("Items");
                int updated = 0;
                _liteDb.BeginTrans();
                for (int i = 1; i <= 10; i++)
                {
                    var doc = col.FindById(i);
                    if (doc != null)
                    {
                        doc["Value"] = 999.0;
                        if (col.Update(doc)) updated++;
                    }
                }
                _liteDb.Commit();
                return updated;
        }
        return 0;
    }

    [Benchmark(Description = "DELETE batch IN clause")]
    public int DeleteBatchIn()
    {
        // Delete 10 rows with IN clause
        switch (Database)
        {
            case FullDbEngine.WitDb_BTree:
                using (var cmd = _witConn!.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM Items WHERE Id IN (11, 12, 13, 14, 15, 16, 17, 18, 19, 20)";
                    return cmd.ExecuteNonQuery();
                }

            case FullDbEngine.SQLite:
                using (var cmd = _sqliteConn!.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM Items WHERE Id IN (11, 12, 13, 14, 15, 16, 17, 18, 19, 20)";
                    return cmd.ExecuteNonQuery();
                }

            case FullDbEngine.LiteDB:
                var col = _liteDb!.GetCollection<BsonDocument>("Items");
                int deleted = 0;
                _liteDb.BeginTrans();
                for (int i = 11; i <= 20; i++)
                {
                    if (col.Delete(i)) deleted++;
                }
                _liteDb.Commit();
                return deleted;
        }
        return 0;
    }

    public void Dispose() => GlobalCleanup();
}

#endregion

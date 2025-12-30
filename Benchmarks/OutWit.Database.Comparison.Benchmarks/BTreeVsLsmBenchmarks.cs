using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using OutWit.Database.AdoNet;

namespace OutWit.Database.Comparison.Benchmarks;

/// <summary>
/// Benchmarks comparing BTree vs LSM storage engines in WitDatabase.
/// </summary>
public enum WitStoreType { BTree, LSM }

[Config(typeof(ComparisonBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class BTreeVsLsmBenchmarks : IDisposable
{
    private WitDbConnection _conn = null!;
    private string _path = null!;

    [Params(100, 1000, 10000)]
    public int RowCount { get; set; }

    [Params(WitStoreType.BTree, WitStoreType.LSM)]
    public WitStoreType StoreType { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _path = Path.Combine(Path.GetTempPath(), $"wit_store_{Guid.NewGuid():N}");
    }

    [IterationSetup]
    public void IterationSetup()
    {
        var storeKey = StoreType == WitStoreType.BTree ? "btree" : "lsm";
        _conn = new WitDbConnection($"Data Source={_path};Store={storeKey};Transactions=true;MVCC=false;SyncWrites=false");
        _conn.Open();

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "DROP TABLE IF EXISTS T";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "CREATE TABLE T (Id BIGINT PRIMARY KEY AUTOINCREMENT, N VARCHAR(100), V DOUBLE)";
        cmd.ExecuteNonQuery();
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _conn?.Dispose();
        try { Directory.Delete(_path, true); } catch { }
    }

    [GlobalCleanup]
    public void GlobalCleanup() => IterationCleanup();

    [Benchmark(Description = "INSERT (auto PK)")]
    public void InsertAutoPk()
    {
        using var tx = (WitDbTransaction)_conn.BeginTransaction();
        using var cmd = _conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO T (N, V) VALUES (@n, @v)";
        var pn = cmd.CreateParameter();
        pn.ParameterName = "@n";
        cmd.Parameters.Add(pn);
        var pv = cmd.CreateParameter();
        pv.ParameterName = "@v";
        cmd.Parameters.Add(pv);

        for (int i = 0; i < RowCount; i++)
        {
            pn.Value = $"Item{i}";
            pv.Value = i * 1.5;
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    public void Dispose() => IterationCleanup();
}

[Config(typeof(ComparisonBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class BTreeVsLsmReadBenchmarks : IDisposable
{
    private WitDbConnection _conn = null!;
    private string _path = null!;
    private int[] _ids = null!;

    [Params(1000, 10000)]
    public int TableSize { get; set; }

    [Params(WitStoreType.BTree, WitStoreType.LSM)]
    public WitStoreType StoreType { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _path = Path.Combine(Path.GetTempPath(), $"wit_store_{Guid.NewGuid():N}");
        
        var storeKey = StoreType == WitStoreType.BTree ? "btree" : "lsm";
        _conn = new WitDbConnection($"Data Source={_path};Store={storeKey};Transactions=true;MVCC=false;SyncWrites=false");
        _conn.Open();

        using (var cmd = _conn.CreateCommand())
        {
            cmd.CommandText = "CREATE TABLE T (Id BIGINT PRIMARY KEY AUTOINCREMENT, N VARCHAR(100), V DOUBLE)";
            cmd.ExecuteNonQuery();
        }

        using var tx = (WitDbTransaction)_conn.BeginTransaction();
        using (var cmd = _conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO T (N, V) VALUES (@n, @v)";
            var pn = cmd.CreateParameter();
            pn.ParameterName = "@n";
            cmd.Parameters.Add(pn);
            var pv = cmd.CreateParameter();
            pv.ParameterName = "@v";
            cmd.Parameters.Add(pv);

            for (int i = 0; i < TableSize; i++)
            {
                pn.Value = $"Item{i}";
                pv.Value = i * 1.5;
                cmd.ExecuteNonQuery();
            }
        }
        tx.Commit();

        // Create index for point queries
        using (var cmd = _conn.CreateCommand())
        {
            cmd.CommandText = "CREATE INDEX IX_T_Id ON T(Id)";
            cmd.ExecuteNonQuery();
        }

        // Generate random IDs for point queries
        var rnd = new Random(42);
        _ids = Enumerable.Range(0, 1000).Select(_ => rnd.Next(1, TableSize + 1)).ToArray();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _conn?.Dispose();
        try { Directory.Delete(_path, true); } catch { }
    }

    [Benchmark(Description = "Point Query 1000x")]
    public int PointQuery()
    {
        int cnt = 0;
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT Id, N, V FROM T WHERE Id = @i";
        var pi = cmd.CreateParameter();
        pi.ParameterName = "@i";
        cmd.Parameters.Add(pi);

        foreach (var id in _ids)
        {
            pi.Value = id;
            using var r = cmd.ExecuteReader();
            if (r.Read()) cnt++;
        }
        return cnt;
    }

    [Benchmark(Description = "Full Scan")]
    public int FullScan()
    {
        int cnt = 0;
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM T";
        using var r = cmd.ExecuteReader();
        while (r.Read()) cnt++;
        return cnt;
    }

    [Benchmark(Description = "COUNT(*)")]
    public long CountAll()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM T";
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    public void Dispose() => GlobalCleanup();
}

/// <summary>
/// Benchmarks for Prepared Statements and Batch operations.
/// </summary>
[Config(typeof(ComparisonBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class PreparedStatementBenchmarks : IDisposable
{
    private OutWit.Database.Engine.WitSqlEngine _engine = null!;
    private string _path = null!;

    [Params(1000, 5000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _path = Path.Combine(Path.GetTempPath(), $"wit_prepared_{Guid.NewGuid():N}");
    }

    [IterationSetup]
    public void IterationSetup()
    {
        var db = OutWit.Database.Core.Builder.WitDatabase.CreateInMemory();
        _engine = new OutWit.Database.Engine.WitSqlEngine(db, ownsStore: true);
        
        _engine.Execute("CREATE TABLE T (Id BIGINT PRIMARY KEY AUTOINCREMENT, Name VARCHAR(100), Value INT)");
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _engine?.Dispose();
    }

    [GlobalCleanup]
    public void GlobalCleanup() => IterationCleanup();

    [Benchmark(Description = "Individual Execute (no prepare)", Baseline = true)]
    public int IndividualExecute()
    {
        int total = 0;
        for (int i = 0; i < RowCount; i++)
        {
            using var result = _engine.Execute(
                "INSERT INTO T (Name, Value) VALUES (@name, @value)",
                new Dictionary<string, object?> { ["name"] = $"Item{i}", ["value"] = i });
            total += result.RowsAffected;
        }
        return total;
    }

    [Benchmark(Description = "Prepared Execute (reuse)")]
    public int PreparedExecute()
    {
        int total = 0;
        using var stmt = _engine.Prepare("INSERT INTO T (Name, Value) VALUES (@name, @value)");
        
        for (int i = 0; i < RowCount; i++)
        {
            stmt.ClearParameters();
            stmt.SetParameter("name", $"Item{i}");
            stmt.SetParameter("value", i);
            using var result = stmt.Execute();
            total += result.RowsAffected;
        }
        return total;
    }

    [Benchmark(Description = "ExecuteBatch (bulk)")]
    public int ExecuteBatch()
    {
        using var stmt = _engine.Prepare("INSERT INTO T (Name, Value) VALUES (@name, @value)");
        
        var paramSets = Enumerable.Range(0, RowCount)
            .Select(i => new Dictionary<string, object?>
            {
                ["name"] = $"Item{i}",
                ["value"] = i
            });
        
        return stmt.ExecuteBatch(paramSets);
    }

    [Benchmark(Description = "BulkInsert API")]
    public int BulkInsertApi()
    {
        var rows = Enumerable.Range(0, RowCount)
            .Select(i => new Dictionary<string, object?>
            {
                ["Name"] = $"Item{i}",
                ["Value"] = i
            });
        
        return _engine.BulkInsert("T", rows);
    }

    public void Dispose() => GlobalCleanup();
}

/// <summary>
/// Benchmarks comparing WitDB (BTree) vs LiteDB
/// LiteDB also uses B+Tree internally, so this is a fair comparison.
/// </summary>
[Config(typeof(ComparisonBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class WitDbVsLiteDbBenchmarks : IDisposable
{
    private WitDbConnection _witConn = null!;
    private LiteDB.LiteDatabase _liteDb = null!;
    private string _witPath = null!;
    private string _liteDbPath = null!;

    public enum DbEngine { WitDb_BTree, LiteDB }

    [Params(100, 1000, 5000)]
    public int RowCount { get; set; }

    [Params(DbEngine.WitDb_BTree, DbEngine.LiteDB)]
    public DbEngine Database { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _witPath = Path.Combine(Path.GetTempPath(), $"wit_btree_{Guid.NewGuid():N}");
        _liteDbPath = Path.Combine(Path.GetTempPath(), $"lite_{Guid.NewGuid():N}.db");
    }

    [IterationSetup]
    public void IterationSetup()
    {
        if (Database == DbEngine.WitDb_BTree)
        {
            // WitDb with BTree storage (same as LiteDB uses internally)
            _witConn = new WitDbConnection($"Data Source={_witPath};Store=btree;Transactions=true;MVCC=false;SyncWrites=false");
            _witConn.Open();
            using var cmd = _witConn.CreateCommand();
            cmd.CommandText = "DROP TABLE IF EXISTS T";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE TABLE T (Id BIGINT PRIMARY KEY AUTOINCREMENT, Name VARCHAR(100), Value DOUBLE)";
            cmd.ExecuteNonQuery();
        }
        else
        {
            _liteDb = new LiteDB.LiteDatabase(_liteDbPath);
            var col = _liteDb.GetCollection<LiteDB.BsonDocument>("T");
            col.DeleteAll();
        }
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _witConn?.Dispose();
        _liteDb?.Dispose();
        try { Directory.Delete(_witPath, true); } catch { }
        try { File.Delete(_liteDbPath); } catch { }
    }

    [GlobalCleanup]
    public void GlobalCleanup() => IterationCleanup();

    [Benchmark(Description = "INSERT in transaction")]
    public void InsertInTransaction()
    {
        if (Database == DbEngine.WitDb_BTree)
        {
            using var tx = (WitDbTransaction)_witConn.BeginTransaction();
            using var cmd = _witConn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO T (Name, Value) VALUES (@n, @v)";
            var pn = cmd.CreateParameter(); pn.ParameterName = "@n"; cmd.Parameters.Add(pn);
            var pv = cmd.CreateParameter(); pv.ParameterName = "@v"; cmd.Parameters.Add(pv);
            
            for (int i = 0; i < RowCount; i++)
            {
                pn.Value = $"Item{i}";
                pv.Value = i * 1.5;
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }
        else // LiteDB
        {
            var col = _liteDb.GetCollection<LiteDB.BsonDocument>("T");
            _liteDb.BeginTrans();
            for (int i = 0; i < RowCount; i++)
            {
                col.Insert(new LiteDB.BsonDocument
                {
                    ["Name"] = $"Item{i}",
                    ["Value"] = i * 1.5
                });
            }
            _liteDb.Commit();
        }
    }

    public void Dispose() => GlobalCleanup();
}

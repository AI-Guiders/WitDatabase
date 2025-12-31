using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Reports;
using LiteDB;
using Microsoft.Data.Sqlite;
using OutWit.Database.AdoNet;

namespace OutWit.Database.Comparison.Benchmarks;

public class ComparisonBenchmarkConfig : ManualConfig
{
    public ComparisonBenchmarkConfig()
    {
        SummaryStyle = SummaryStyle.Default
            .WithRatioStyle(RatioStyle.Trend)
            .WithTimeUnit(Perfolizer.Horology.TimeUnit.Millisecond);
        HideColumns(Column.Error, Column.StdDev, Column.RatioSD);
    }
}

public enum DatabaseType 
{ 
    WitDbLsm,           // WitDb with LSM-Tree (original)
    WitDbBTree,         // WitDb with BTree
    WitDbBTreeParallel, // WitDb with BTree + Parallel Mode
    SQLite, 
    LiteDB 
}

#region Insert Benchmarks

[Config(typeof(ComparisonBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class InsertBenchmarks : IDisposable
{
    private WitDbConnection? _witLsmConn;
    private WitDbConnection? _witBTreeConn;
    private WitDbConnection? _witBTreeParallelConn;
    private SqliteConnection? _sqliteConn;
    private LiteDatabase? _liteDb;
    private string _witLsmPath = null!;
    private string _witBTreePath = null!;
    private string _witBTreeParallelPath = null!;
    private string _sqlitePath = null!;
    private string _liteDbPath = null!;

    [Params(100, 1000, 10000)]
    public int RowCount { get; set; }

    [Params(DatabaseType.WitDbLsm, DatabaseType.WitDbBTree, DatabaseType.WitDbBTreeParallel, DatabaseType.SQLite, DatabaseType.LiteDB)]
    public DatabaseType Database { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _witLsmPath = Path.Combine(Path.GetTempPath(), $"wit_lsm_{Guid.NewGuid():N}");
        _witBTreePath = Path.Combine(Path.GetTempPath(), $"wit_btree_{Guid.NewGuid():N}.witdb");
        _witBTreeParallelPath = Path.Combine(Path.GetTempPath(), $"wit_btree_par_{Guid.NewGuid():N}.witdb");
        _sqlitePath = Path.Combine(Path.GetTempPath(), $"sql_{Guid.NewGuid():N}.db");
        _liteDbPath = Path.Combine(Path.GetTempPath(), $"lite_{Guid.NewGuid():N}.db");
    }

    [IterationSetup]
    public void IterationSetup()
    {
        // WitDb LSM-Tree
        _witLsmConn = new WitDbConnection($"Data Source={_witLsmPath};Store=lsm;Transactions=true;MVCC=false;SyncWrites=false");
        _witLsmConn.Open();
        using (var c = _witLsmConn.CreateCommand())
        {
            c.CommandText = "DROP TABLE IF EXISTS T"; c.ExecuteNonQuery();
            c.CommandText = "CREATE TABLE T (Id BIGINT PRIMARY KEY AUTOINCREMENT, N VARCHAR(100), V DOUBLE)"; 
            c.ExecuteNonQuery();
        }

        // WitDb BTree
        _witBTreeConn = new WitDbConnection($"Data Source={_witBTreePath};Store=btree;Transactions=true;MVCC=false");
        _witBTreeConn.Open();
        using (var c = _witBTreeConn.CreateCommand())
        {
            c.CommandText = "DROP TABLE IF EXISTS T"; c.ExecuteNonQuery();
            c.CommandText = "CREATE TABLE T (Id BIGINT PRIMARY KEY AUTOINCREMENT, N VARCHAR(100), V DOUBLE)"; 
            c.ExecuteNonQuery();
        }

        // WitDb BTree + Parallel
        _witBTreeParallelConn = new WitDbConnection($"Data Source={_witBTreeParallelPath};Store=btree;Transactions=true;MVCC=false;Parallel Mode=Latched");
        _witBTreeParallelConn.Open();
        using (var c = _witBTreeParallelConn.CreateCommand())
        {
            c.CommandText = "DROP TABLE IF EXISTS T"; c.ExecuteNonQuery();
            c.CommandText = "CREATE TABLE T (Id BIGINT PRIMARY KEY AUTOINCREMENT, N VARCHAR(100), V DOUBLE)"; 
            c.ExecuteNonQuery();
        }

        // SQLite
        _sqliteConn = new SqliteConnection($"Data Source={_sqlitePath}");
        _sqliteConn.Open();
        using (var c = _sqliteConn.CreateCommand())
        {
            c.CommandText = "DROP TABLE IF EXISTS T"; c.ExecuteNonQuery();
            c.CommandText = "CREATE TABLE T (Id INTEGER PRIMARY KEY, N TEXT, V REAL)"; c.ExecuteNonQuery();
        }

        // LiteDB
        _liteDb = new LiteDatabase(_liteDbPath);
        var col = _liteDb.GetCollection<BsonDocument>("T");
        col.DeleteAll();
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _witLsmConn?.Dispose(); _witLsmConn = null;
        _witBTreeConn?.Dispose(); _witBTreeConn = null;
        _witBTreeParallelConn?.Dispose(); _witBTreeParallelConn = null;
        _sqliteConn?.Dispose(); _sqliteConn = null;
        _liteDb?.Dispose(); _liteDb = null;
        try { Directory.Delete(_witLsmPath, true); } catch { }
        try { File.Delete(_witBTreePath); } catch { }
        try { File.Delete(_witBTreeParallelPath); } catch { }
        try { File.Delete(_sqlitePath); } catch { }
        try { File.Delete(_liteDbPath); } catch { }
    }

    [GlobalCleanup]
    public void GlobalCleanup() => IterationCleanup();

    private WitDbConnection GetWitConnection() => Database switch
    {
        DatabaseType.WitDbLsm => _witLsmConn!,
        DatabaseType.WitDbBTree => _witBTreeConn!,
        DatabaseType.WitDbBTreeParallel => _witBTreeParallelConn!,
        _ => throw new InvalidOperationException()
    };

    [Benchmark(Description = "INSERT in tx (auto PK)", Baseline = true)]
    public void InsertInTxAutoPk()
    {
        if (Database == DatabaseType.WitDbLsm || Database == DatabaseType.WitDbBTree || Database == DatabaseType.WitDbBTreeParallel)
        {
            var conn = GetWitConnection();
            var tx = (WitDbTransaction)conn.BeginTransaction();
            using var c = conn.CreateCommand();
            c.Transaction = tx;
            c.CommandText = "INSERT INTO T (N, V) VALUES (@n, @v)";
            var pn = c.CreateParameter(); pn.ParameterName = "@n"; c.Parameters.Add(pn);
            var pv = c.CreateParameter(); pv.ParameterName = "@v"; c.Parameters.Add(pv);
            for (int i = 0; i < RowCount; i++)
            {
                pn.Value = $"I{i}";
                pv.Value = i * 1.5;
                c.ExecuteNonQuery();
            }
            tx.Commit();
            tx.Dispose();
        }
        else if (Database == DatabaseType.SQLite)
        {
            var tx = _sqliteConn!.BeginTransaction();
            using var c = _sqliteConn.CreateCommand();
            c.Transaction = tx;
            c.CommandText = "INSERT INTO T (N, V) VALUES (@n, @v)";
            var pn = c.CreateParameter(); pn.ParameterName = "@n"; c.Parameters.Add(pn);
            var pv = c.CreateParameter(); pv.ParameterName = "@v"; c.Parameters.Add(pv);
            for (int i = 0; i < RowCount; i++)
            {
                pn.Value = $"I{i}";
                pv.Value = i * 1.5;
                c.ExecuteNonQuery();
            }
            tx.Commit();
            tx.Dispose();
        }
        else // LiteDB
        {
            var col = _liteDb!.GetCollection<BsonDocument>("T");
            _liteDb.BeginTrans();
            for (int i = 0; i < RowCount; i++)
            {
                var doc = new BsonDocument
                {
                    ["N"] = $"I{i}",
                    ["V"] = i * 1.5
                };
                col.Insert(doc);
            }
            _liteDb.Commit();
        }
    }

    public void Dispose() => IterationCleanup();
}

#endregion

#region Select Benchmarks

[Config(typeof(ComparisonBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class SelectBenchmarks : IDisposable
{
    private WitDbConnection? _witLsmConn;
    private WitDbConnection? _witBTreeConn;
    private WitDbConnection? _witBTreeParallelConn;
    private SqliteConnection? _sqliteConn;
    private LiteDatabase? _liteDb;
    private string _witLsmPath = null!;
    private string _witBTreePath = null!;
    private string _witBTreeParallelPath = null!;
    private string _sqlitePath = null!;
    private string _liteDbPath = null!;
    private int[] _ids = null!;

    [Params(1000, 10000)]
    public int TableSize { get; set; }

    [Params(DatabaseType.WitDbLsm, DatabaseType.WitDbBTree, DatabaseType.WitDbBTreeParallel, DatabaseType.SQLite, DatabaseType.LiteDB)]
    public DatabaseType Database { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _witLsmPath = Path.Combine(Path.GetTempPath(), $"wit_lsm_{Guid.NewGuid():N}");
        _witBTreePath = Path.Combine(Path.GetTempPath(), $"wit_btree_{Guid.NewGuid():N}.witdb");
        _witBTreeParallelPath = Path.Combine(Path.GetTempPath(), $"wit_btree_par_{Guid.NewGuid():N}.witdb");
        _sqlitePath = Path.Combine(Path.GetTempPath(), $"sql_{Guid.NewGuid():N}.db");
        _liteDbPath = Path.Combine(Path.GetTempPath(), $"lite_{Guid.NewGuid():N}.db");

        // Setup all databases - only for the current Database parameter
        if (Database == DatabaseType.WitDbLsm)
            SetupWitDb(_witLsmPath, "lsm", "Transactions=true;MVCC=false;SyncWrites=false", out _witLsmConn);
        else if (Database == DatabaseType.WitDbBTree)
            SetupWitDb(_witBTreePath, "btree", "Transactions=true;MVCC=false", out _witBTreeConn);
        else if (Database == DatabaseType.WitDbBTreeParallel)
            SetupWitDb(_witBTreeParallelPath, "btree", "Transactions=true;MVCC=false;Parallel Mode=Latched", out _witBTreeParallelConn);
        else if (Database == DatabaseType.SQLite)
        {
            // SQLite
            _sqliteConn = new SqliteConnection($"Data Source={_sqlitePath}");
            _sqliteConn.Open();
            using (var c = _sqliteConn.CreateCommand())
            {
                c.CommandText = "CREATE TABLE T (Id INTEGER PRIMARY KEY, N TEXT, V REAL)";
                c.ExecuteNonQuery();
            }
            var txS = _sqliteConn.BeginTransaction();
            using (var c = _sqliteConn.CreateCommand())
            {
                c.Transaction = txS;
                c.CommandText = "INSERT INTO T (N, V) VALUES (@n, @v)";
                var pn = c.CreateParameter(); pn.ParameterName = "@n"; c.Parameters.Add(pn);
                var pv = c.CreateParameter(); pv.ParameterName = "@v"; c.Parameters.Add(pv);
                for (int i = 0; i < TableSize; i++)
                {
                    pn.Value = $"I{i}";
                    pv.Value = i * 1.5;
                    c.ExecuteNonQuery();
                }
            }
            txS.Commit();
            txS.Dispose();
        }
        else // LiteDB
        {
            _liteDb = new LiteDatabase(_liteDbPath);
            var col = _liteDb.GetCollection<BsonDocument>("T");
            for (int i = 0; i < TableSize; i++)
            {
                col.Insert(new BsonDocument
                {
                    ["Id"] = i,
                    ["N"] = $"I{i}",
                    ["V"] = i * 1.5
                });
            }
            col.EnsureIndex("Id");
        }

        var rnd = new Random(42);
        _ids = Enumerable.Range(0, 1000).Select(_ => rnd.Next(1, TableSize + 1)).ToArray();
    }

    private void SetupWitDb(string path, string store, string options, out WitDbConnection conn)
    {
        conn = new WitDbConnection($"Data Source={path};Store={store};{options}");
        conn.Open();
        using (var c = conn.CreateCommand())
        {
            c.CommandText = "CREATE TABLE T (Id BIGINT PRIMARY KEY AUTOINCREMENT, N VARCHAR(100), V DOUBLE)";
            c.ExecuteNonQuery();
        }
        var tx = (WitDbTransaction)conn.BeginTransaction();
        using (var c = conn.CreateCommand())
        {
            c.Transaction = tx;
            c.CommandText = "INSERT INTO T (N, V) VALUES (@n, @v)";
            var pn = c.CreateParameter(); pn.ParameterName = "@n"; c.Parameters.Add(pn);
            var pv = c.CreateParameter(); pv.ParameterName = "@v"; c.Parameters.Add(pv);
            for (int i = 0; i < TableSize; i++)
            {
                pn.Value = $"I{i}";
                pv.Value = i * 1.5;
                c.ExecuteNonQuery();
            }
        }
        tx.Commit();
        tx.Dispose();
        // Note: No need to CREATE INDEX on PK column - it's already indexed as primary key
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _witLsmConn?.Dispose();
        _witBTreeConn?.Dispose();
        _witBTreeParallelConn?.Dispose();
        _sqliteConn?.Dispose();
        _liteDb?.Dispose();
        try { Directory.Delete(_witLsmPath, true); } catch { }
        try { File.Delete(_witBTreePath); } catch { }
        try { File.Delete(_witBTreeParallelPath); } catch { }
        try { File.Delete(_sqlitePath); } catch { }
        try { File.Delete(_liteDbPath); } catch { }
    }

    private WitDbConnection GetWitConnection() => Database switch
    {
        DatabaseType.WitDbLsm => _witLsmConn!,
        DatabaseType.WitDbBTree => _witBTreeConn!,
        DatabaseType.WitDbBTreeParallel => _witBTreeParallelConn!,
        _ => throw new InvalidOperationException()
    };

    [Benchmark(Description = "Point Query 1000x")]
    public int PointQuery()
    {
        int cnt = 0;
        if (Database == DatabaseType.WitDbLsm || Database == DatabaseType.WitDbBTree || Database == DatabaseType.WitDbBTreeParallel)
        {
            var conn = GetWitConnection();
            using var c = conn.CreateCommand();
            c.CommandText = "SELECT Id, N, V FROM T WHERE Id = @i";
            var pi = c.CreateParameter(); pi.ParameterName = "@i"; c.Parameters.Add(pi);
            foreach (var id in _ids)
            {
                pi.Value = id;
                using var r = c.ExecuteReader();
                if (r.Read()) cnt++;
            }
        }
        else if (Database == DatabaseType.SQLite)
        {
            using var c = _sqliteConn!.CreateCommand();
            c.CommandText = "SELECT Id, N, V FROM T WHERE Id = @i";
            var pi = c.CreateParameter(); pi.ParameterName = "@i"; c.Parameters.Add(pi);
            foreach (var id in _ids)
            {
                pi.Value = id;
                using var r = c.ExecuteReader();
                if (r.Read()) cnt++;
            }
        }
        else // LiteDB
        {
            var col = _liteDb!.GetCollection<BsonDocument>("T");
            foreach (var id in _ids)
            {
                var doc = col.FindOne(x => x["Id"] == id);
                if (doc != null) cnt++;
            }
        }
        return cnt;
    }

    [Benchmark(Description = "Full Scan")]
    public int FullScan()
    {
        int cnt = 0;
        if (Database == DatabaseType.WitDbLsm || Database == DatabaseType.WitDbBTree || Database == DatabaseType.WitDbBTreeParallel)
        {
            var conn = GetWitConnection();
            using var c = conn.CreateCommand();
            c.CommandText = "SELECT * FROM T";
            using var r = c.ExecuteReader();
            while (r.Read()) cnt++;
        }
        else if (Database == DatabaseType.SQLite)
        {
            using var c = _sqliteConn!.CreateCommand();
            c.CommandText = "SELECT * FROM T";
            using var r = c.ExecuteReader();
            while (r.Read()) cnt++;
        }
        else // LiteDB
        {
            var col = _liteDb!.GetCollection<BsonDocument>("T");
            foreach (var doc in col.FindAll())
            {
                cnt++;
            }
        }
        return cnt;
    }

    [Benchmark(Description = "Aggregation COUNT")]
    public long AggregationCount()
    {
        if (Database == DatabaseType.WitDbLsm || Database == DatabaseType.WitDbBTree || Database == DatabaseType.WitDbBTreeParallel)
        {
            var conn = GetWitConnection();
            using var c = conn.CreateCommand();
            c.CommandText = "SELECT COUNT(*) FROM T";
            return Convert.ToInt64(c.ExecuteScalar());
        }
        else if (Database == DatabaseType.SQLite)
        {
            using var c = _sqliteConn!.CreateCommand();
            c.CommandText = "SELECT COUNT(*) FROM T";
            return Convert.ToInt64(c.ExecuteScalar());
        }
        else // LiteDB
        {
            var col = _liteDb!.GetCollection<BsonDocument>("T");
            return col.Count();
        }
    }

    public void Dispose() => GlobalCleanup();
}

#endregion

#region Update/Delete Benchmarks

[Config(typeof(ComparisonBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class UpdateDeleteBenchmarks : IDisposable
{
    private WitDbConnection? _witLsmConn;
    private WitDbConnection? _witBTreeConn;
    private WitDbConnection? _witBTreeParallelConn;
    private SqliteConnection? _sqliteConn;
    private LiteDatabase? _liteDb;
    private string _witLsmPath = null!;
    private string _witBTreePath = null!;
    private string _witBTreeParallelPath = null!;
    private string _sqlitePath = null!;
    private string _liteDbPath = null!;

    [Params(100, 1000)]
    public int RowCount { get; set; }

    [Params(DatabaseType.WitDbLsm, DatabaseType.WitDbBTree, DatabaseType.WitDbBTreeParallel, DatabaseType.SQLite, DatabaseType.LiteDB)]
    public DatabaseType Database { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _witLsmPath = Path.Combine(Path.GetTempPath(), $"wit_lsm_{Guid.NewGuid():N}");
        _witBTreePath = Path.Combine(Path.GetTempPath(), $"wit_btree_{Guid.NewGuid():N}.witdb");
        _witBTreeParallelPath = Path.Combine(Path.GetTempPath(), $"wit_btree_par_{Guid.NewGuid():N}.witdb");
        _sqlitePath = Path.Combine(Path.GetTempPath(), $"sql_{Guid.NewGuid():N}.db");
        _liteDbPath = Path.Combine(Path.GetTempPath(), $"lite_{Guid.NewGuid():N}.db");
    }

    [IterationSetup]
    public void IterationSetup()
    {
        // WitDb LSM
        _witLsmConn = new WitDbConnection($"Data Source={_witLsmPath};Store=lsm;Transactions=true;MVCC=false;SyncWrites=false");
        _witLsmConn.Open();
        SetupWitTable(_witLsmConn);

        // WitDb BTree
        _witBTreeConn = new WitDbConnection($"Data Source={_witBTreePath};Store=btree;Transactions=true;MVCC=false");
        _witBTreeConn.Open();
        SetupWitTable(_witBTreeConn);

        // WitDb BTree + Parallel
        _witBTreeParallelConn = new WitDbConnection($"Data Source={_witBTreeParallelPath};Store=btree;Transactions=true;MVCC=false;Parallel Mode=Latched");
        _witBTreeParallelConn.Open();
        SetupWitTable(_witBTreeParallelConn);

        // SQLite
        _sqliteConn = new SqliteConnection($"Data Source={_sqlitePath}");
        _sqliteConn.Open();
        using (var c = _sqliteConn.CreateCommand())
        {
            c.CommandText = "DROP TABLE IF EXISTS T"; c.ExecuteNonQuery();
            c.CommandText = "CREATE TABLE T (Id INTEGER PRIMARY KEY, N TEXT, V REAL)"; c.ExecuteNonQuery();
        }
        var txS = _sqliteConn.BeginTransaction();
        using (var c = _sqliteConn.CreateCommand())
        {
            c.Transaction = txS;
            c.CommandText = "INSERT INTO T (N, V) VALUES (@n, @v)";
            var pn = c.CreateParameter(); pn.ParameterName = "@n"; c.Parameters.Add(pn);
            var pv = c.CreateParameter(); pv.ParameterName = "@v"; c.Parameters.Add(pv);
            for (int i = 0; i < RowCount; i++)
            {
                pn.Value = $"N{i}"; pv.Value = i * 1.5;
                c.ExecuteNonQuery();
            }
        }
        txS.Commit();
        txS.Dispose();

        // LiteDB
        _liteDb = new LiteDatabase(_liteDbPath);
        var col = _liteDb.GetCollection<BsonDocument>("T");
        col.DeleteAll();
        for (int i = 0; i < RowCount; i++)
        {
            col.Insert(new BsonDocument
            {
                ["Id"] = i,
                ["N"] = $"N{i}",
                ["V"] = i * 1.5
            });
        }
        col.EnsureIndex("Id");
    }

    private void SetupWitTable(WitDbConnection conn)
    {
        using (var c = conn.CreateCommand())
        {
            c.CommandText = "DROP TABLE IF EXISTS T"; c.ExecuteNonQuery();
            c.CommandText = "CREATE TABLE T (Id BIGINT PRIMARY KEY AUTOINCREMENT, N VARCHAR(100), V DOUBLE)"; 
            c.ExecuteNonQuery();
        }
        var tx = (WitDbTransaction)conn.BeginTransaction();
        using (var c = conn.CreateCommand())
        {
            c.Transaction = tx;
            c.CommandText = "INSERT INTO T (N, V) VALUES (@n, @v)";
            var pn = c.CreateParameter(); pn.ParameterName = "@n"; c.Parameters.Add(pn);
            var pv = c.CreateParameter(); pv.ParameterName = "@v"; c.Parameters.Add(pv);
            for (int i = 0; i < RowCount; i++)
            {
                pn.Value = $"N{i}"; pv.Value = i * 1.5;
                c.ExecuteNonQuery();
            }
        }
        tx.Commit();
        tx.Dispose();
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _witLsmConn?.Dispose(); _witLsmConn = null;
        _witBTreeConn?.Dispose(); _witBTreeConn = null;
        _witBTreeParallelConn?.Dispose(); _witBTreeParallelConn = null;
        _sqliteConn?.Dispose(); _sqliteConn = null;
        _liteDb?.Dispose(); _liteDb = null;
        try { Directory.Delete(_witLsmPath, true); } catch { }
        try { File.Delete(_witBTreePath); } catch { }
        try { File.Delete(_witBTreeParallelPath); } catch { }
        try { File.Delete(_sqlitePath); } catch { }
        try { File.Delete(_liteDbPath); } catch { }
    }

    [GlobalCleanup]
    public void GlobalCleanup() => IterationCleanup();

    private WitDbConnection GetWitConnection() => Database switch
    {
        DatabaseType.WitDbLsm => _witLsmConn!,
        DatabaseType.WitDbBTree => _witBTreeConn!,
        DatabaseType.WitDbBTreeParallel => _witBTreeParallelConn!,
        _ => throw new InvalidOperationException()
    };

    [Benchmark(Description = "UPDATE by PK in tx")]
    public void UpdateByPkInTx()
    {
        if (Database == DatabaseType.WitDbLsm || Database == DatabaseType.WitDbBTree || Database == DatabaseType.WitDbBTreeParallel)
        {
            var conn = GetWitConnection();
            var tx = (WitDbTransaction)conn.BeginTransaction();
            using var c = conn.CreateCommand();
            c.Transaction = tx;
            c.CommandText = "UPDATE T SET V = @v WHERE Id = @i";
            var pi = c.CreateParameter(); pi.ParameterName = "@i"; c.Parameters.Add(pi);
            var pv = c.CreateParameter(); pv.ParameterName = "@v"; c.Parameters.Add(pv);
            for (int i = 1; i <= RowCount; i++)
            {
                pi.Value = i;
                pv.Value = i * 2.0;
                c.ExecuteNonQuery();
            }
            tx.Commit();
            tx.Dispose();
        }
        else if (Database == DatabaseType.SQLite)
        {
            var tx = _sqliteConn!.BeginTransaction();
            using var c = _sqliteConn.CreateCommand();
            c.Transaction = tx;
            c.CommandText = "UPDATE T SET V = @v WHERE Id = @i";
            var pi = c.CreateParameter(); pi.ParameterName = "@i"; c.Parameters.Add(pi);
            var pv = c.CreateParameter(); pv.ParameterName = "@v"; c.Parameters.Add(pv);
            for (int i = 1; i <= RowCount; i++)
            {
                pi.Value = i;
                pv.Value = i * 2.0;
                c.ExecuteNonQuery();
            }
            tx.Commit();
            tx.Dispose();
        }
        else // LiteDB
        {
            var col = _liteDb!.GetCollection<BsonDocument>("T");
            _liteDb.BeginTrans();
            for (int i = 0; i < RowCount; i++)
            {
                var doc = col.FindOne(x => x["Id"] == i);
                if (doc != null)
                {
                    doc["V"] = i * 2.0;
                    col.Update(doc);
                }
            }
            _liteDb.Commit();
        }
    }

    [Benchmark(Description = "DELETE by PK in tx")]
    public void DeleteByPkInTx()
    {
        if (Database == DatabaseType.WitDbLsm || Database == DatabaseType.WitDbBTree || Database == DatabaseType.WitDbBTreeParallel)
        {
            var conn = GetWitConnection();
            var tx = (WitDbTransaction)conn.BeginTransaction();
            using var c = conn.CreateCommand();
            c.Transaction = tx;
            c.CommandText = "DELETE FROM T WHERE Id = @i";
            var pi = c.CreateParameter(); pi.ParameterName = "@i"; c.Parameters.Add(pi);
            for (int i = 1; i <= RowCount / 2; i++)
            {
                pi.Value = i;
                c.ExecuteNonQuery();
            }
            tx.Commit();
            tx.Dispose();
        }
        else if (Database == DatabaseType.SQLite)
        {
            var tx = _sqliteConn!.BeginTransaction();
            using var c = _sqliteConn.CreateCommand();
            c.Transaction = tx;
            c.CommandText = "DELETE FROM T WHERE Id = @i";
            var pi = c.CreateParameter(); pi.ParameterName = "@i"; c.Parameters.Add(pi);
            for (int i = 1; i <= RowCount / 2; i++)
            {
                pi.Value = i;
                c.ExecuteNonQuery();
            }
            tx.Commit();
            tx.Dispose();
        }
        else // LiteDB
        {
            var col = _liteDb!.GetCollection<BsonDocument>("T");
            _liteDb.BeginTrans();
            for (int i = 0; i < RowCount / 2; i++)
            {
                col.DeleteMany(x => x["Id"] == i);
            }
            _liteDb.Commit();
        }
    }

    public void Dispose() => GlobalCleanup();
}

#endregion

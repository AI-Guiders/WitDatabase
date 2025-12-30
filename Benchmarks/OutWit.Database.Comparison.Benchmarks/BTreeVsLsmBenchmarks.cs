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

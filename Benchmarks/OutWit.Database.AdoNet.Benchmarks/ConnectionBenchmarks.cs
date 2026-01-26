using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using LiteDB;
using Microsoft.Data.Sqlite;
using OutWit.Database.AdoNet;

namespace OutWit.Database.AdoNet.Benchmarks;

/// <summary>
/// Benchmarks for connection open/close performance.
/// Tests connection lifecycle overhead.
/// </summary>
[Config(typeof(AdoNetBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class ConnectionBenchmarks : IDisposable
{
    #region Fields

    private string m_witPath = null!;
    private string m_sqlitePath = null!;
    private string m_liteDbPath = null!;

    #endregion

    #region Parameters

    [Params(DbProviderType.WitDb, DbProviderType.SQLite, DbProviderType.LiteDB)]
    public DbProviderType Provider { get; set; }

    #endregion

    #region Setup/Cleanup

    [GlobalSetup]
    public void GlobalSetup()
    {
        m_witPath = BenchmarkPathHelper.GenerateUniquePath("wit_conn", ".witdb");
        m_sqlitePath = BenchmarkPathHelper.GenerateUniquePath("sql_conn", ".db");
        m_liteDbPath = BenchmarkPathHelper.GenerateUniquePath("lite_conn", ".db");

        CleanupPaths();

        // Create databases with schema
        using (var conn = new WitDbConnection($"Data Source={m_witPath}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE Test (Id INT PRIMARY KEY, Value TEXT)";
            cmd.ExecuteNonQuery();
        }

        using (var conn = new SqliteConnection($"Data Source={m_sqlitePath}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE Test (Id INTEGER PRIMARY KEY, Value TEXT)";
            cmd.ExecuteNonQuery();
        }

        // LiteDB creates collection on first insert
        using (var db = new LiteDatabase(m_liteDbPath))
        {
            var col = db.GetCollection<LiteDbTestDoc>("test");
            col.EnsureIndex(x => x.Id);
        }
    }

    private void CleanupPaths()
    {
        BenchmarkPathHelper.SafeCleanup(m_witPath);
        BenchmarkPathHelper.SafeCleanup(m_witPath + "_indexes");
        BenchmarkPathHelper.SafeCleanup(m_sqlitePath);
        BenchmarkPathHelper.SafeCleanup(m_liteDbPath);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        CleanupPaths();
    }

    #endregion

    #region Benchmarks - Open/Close

    [Benchmark(Description = "Open + Close (single)")]
    public void OpenCloseSingle()
    {
        if (Provider == DbProviderType.WitDb)
        {
            using var conn = new WitDbConnection($"Data Source={m_witPath}");
            conn.Open();
            conn.Close();
        }
        else if (Provider == DbProviderType.SQLite)
        {
            using var conn = new SqliteConnection($"Data Source={m_sqlitePath}");
            conn.Open();
            conn.Close();
        }
        else
        {
            using var db = new LiteDatabase(m_liteDbPath);
        }
    }

    [Benchmark(Description = "Open + Close (100x)")]
    public void OpenClose100x()
    {
        if (Provider == DbProviderType.WitDb)
        {
            for (int i = 0; i < 100; i++)
            {
                using var conn = new WitDbConnection($"Data Source={m_witPath}");
                conn.Open();
                conn.Close();
            }
        }
        else if (Provider == DbProviderType.SQLite)
        {
            for (int i = 0; i < 100; i++)
            {
                using var conn = new SqliteConnection($"Data Source={m_sqlitePath}");
                conn.Open();
                conn.Close();
            }
        }
        else
        {
            for (int i = 0; i < 100; i++)
            {
                using var db = new LiteDatabase(m_liteDbPath);
            }
        }
    }

    [Benchmark(Description = "Open + Simple Query + Close")]
    public object? OpenQueryClose()
    {
        if (Provider == DbProviderType.WitDb)
        {
            using var conn = new WitDbConnection($"Data Source={m_witPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            var result = cmd.ExecuteScalar();
            conn.Close();
            return result;
        }
        else if (Provider == DbProviderType.SQLite)
        {
            using var conn = new SqliteConnection($"Data Source={m_sqlitePath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            var result = cmd.ExecuteScalar();
            conn.Close();
            return result;
        }
        else
        {
            using var db = new LiteDatabase(m_liteDbPath);
            var col = db.GetCollection<LiteDbTestDoc>("test");
            return col.Count();
        }
    }

    #endregion

    #region Benchmarks - Keep Open

    [Benchmark(Description = "Single connection, 100 queries")]
    public int SingleConnection100Queries()
    {
        int sum = 0;
        if (Provider == DbProviderType.WitDb)
        {
            using var conn = new WitDbConnection($"Data Source={m_witPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            for (int i = 0; i < 100; i++)
            {
                sum += Convert.ToInt32(cmd.ExecuteScalar());
            }
        }
        else if (Provider == DbProviderType.SQLite)
        {
            using var conn = new SqliteConnection($"Data Source={m_sqlitePath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            for (int i = 0; i < 100; i++)
            {
                sum += Convert.ToInt32(cmd.ExecuteScalar());
            }
        }
        else
        {
            using var db = new LiteDatabase(m_liteDbPath);
            var col = db.GetCollection<LiteDbTestDoc>("test");
            for (int i = 0; i < 100; i++)
            {
                sum += col.Count();
            }
        }
        return sum;
    }

    #endregion

    #region IDisposable

    public void Dispose() => GlobalCleanup();

    #endregion
}

/// <summary>
/// Simple document for LiteDB tests.
/// </summary>
public class LiteDbTestDoc
{
    public int Id { get; set; }
    public string? Value { get; set; }
}

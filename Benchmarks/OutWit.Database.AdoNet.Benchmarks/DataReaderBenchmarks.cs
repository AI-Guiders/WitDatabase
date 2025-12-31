using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using LiteDB;
using Microsoft.Data.Sqlite;
using OutWit.Database.AdoNet;
using System.Data.Common;

namespace OutWit.Database.AdoNet.Benchmarks;

/// <summary>
/// Benchmarks for DataReader performance.
/// Tests row iteration, typed getters, and field access patterns.
/// </summary>
[Config(typeof(AdoNetBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class DataReaderBenchmarks : IDisposable
{
    #region Fields

    private WitDbConnection m_witConn = null!;
    private SqliteConnection m_sqliteConn = null!;
    private LiteDatabase m_liteDb = null!;
    private ILiteCollection<DataDoc> m_liteCollection = null!;
    private string m_witPath = null!;
    private string m_sqlitePath = null!;
    private string m_liteDbPath = null!;

    #endregion

    #region Parameters

    [Params(100, 1000, 5000)]
    public int RowCount { get; set; }

    [Params(DbProviderType.WitDb, DbProviderType.SQLite, DbProviderType.LiteDB)]
    public DbProviderType Provider { get; set; }

    #endregion

    #region Setup/Cleanup

    [GlobalSetup]
    public void GlobalSetup()
    {
        m_witPath = BenchmarkPathHelper.GenerateUniquePath("wit_dr", ".witdb");
        m_sqlitePath = BenchmarkPathHelper.GenerateUniquePath("sql_dr", ".db");
        m_liteDbPath = BenchmarkPathHelper.GenerateUniquePath("lite_dr", ".db");

        CleanupPaths();

        // Setup WitDb
        m_witConn = new WitDbConnection($"Data Source={m_witPath}");
        m_witConn.Open();
        using (var cmd = m_witConn.CreateCommand())
        {
            cmd.CommandText = @"CREATE TABLE Data (
                Id BIGINT PRIMARY KEY AUTOINCREMENT,
                IntVal INT,
                DoubleVal DOUBLE,
                StringVal VARCHAR(200),
                DateVal DATE,
                BoolVal BOOLEAN
            )";
            cmd.ExecuteNonQuery();
        }
        InsertTestData(m_witConn, RowCount);

        // Setup SQLite
        m_sqliteConn = new SqliteConnection($"Data Source={m_sqlitePath}");
        m_sqliteConn.Open();
        using (var cmd = m_sqliteConn.CreateCommand())
        {
            cmd.CommandText = @"CREATE TABLE Data (
                Id INTEGER PRIMARY KEY,
                IntVal INTEGER,
                DoubleVal REAL,
                StringVal TEXT,
                DateVal TEXT,
                BoolVal INTEGER
            )";
            cmd.ExecuteNonQuery();
        }
        InsertTestDataSqlite(m_sqliteConn, RowCount);

        // Setup LiteDB
        m_liteDb = new LiteDatabase(m_liteDbPath);
        m_liteCollection = m_liteDb.GetCollection<DataDoc>("data");
        InsertLiteDbTestData(RowCount);
    }

    private void CleanupPaths()
    {
        BenchmarkPathHelper.SafeCleanup(m_witPath);
        BenchmarkPathHelper.SafeCleanup(m_witPath + "_indexes");
        BenchmarkPathHelper.SafeCleanup(m_sqlitePath);
        BenchmarkPathHelper.SafeCleanup(m_liteDbPath);
    }

    private void InsertTestData(WitDbConnection conn, int count)
    {
        var tx = (WitDbTransaction)conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO Data (IntVal, DoubleVal, StringVal, DateVal, BoolVal) VALUES (@i, @d, @s, @dt, @b)";

        var pi = cmd.CreateParameter(); pi.ParameterName = "@i"; cmd.Parameters.Add(pi);
        var pd = cmd.CreateParameter(); pd.ParameterName = "@d"; cmd.Parameters.Add(pd);
        var ps = cmd.CreateParameter(); ps.ParameterName = "@s"; cmd.Parameters.Add(ps);
        var pdt = cmd.CreateParameter(); pdt.ParameterName = "@dt"; cmd.Parameters.Add(pdt);
        var pb = cmd.CreateParameter(); pb.ParameterName = "@b"; cmd.Parameters.Add(pb);

        var baseDate = new DateOnly(2024, 1, 1);
        for (int i = 0; i < count; i++)
        {
            pi.Value = i * 10;
            pd.Value = i * 1.5;
            ps.Value = $"String value number {i} with some additional text for padding";
            pdt.Value = baseDate.AddDays(i % 365);
            pb.Value = i % 2 == 0;
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    private void InsertTestDataSqlite(SqliteConnection conn, int count)
    {
        var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO Data (IntVal, DoubleVal, StringVal, DateVal, BoolVal) VALUES (@i, @d, @s, @dt, @b)";

        var pi = cmd.CreateParameter(); pi.ParameterName = "@i"; cmd.Parameters.Add(pi);
        var pd = cmd.CreateParameter(); pd.ParameterName = "@d"; cmd.Parameters.Add(pd);
        var ps = cmd.CreateParameter(); ps.ParameterName = "@s"; cmd.Parameters.Add(ps);
        var pdt = cmd.CreateParameter(); pdt.ParameterName = "@dt"; cmd.Parameters.Add(pdt);
        var pb = cmd.CreateParameter(); pb.ParameterName = "@b"; cmd.Parameters.Add(pb);

        var baseDate = new DateOnly(2024, 1, 1);
        for (int i = 0; i < count; i++)
        {
            pi.Value = i * 10;
            pd.Value = i * 1.5;
            ps.Value = $"String value number {i} with some additional text for padding";
            pdt.Value = baseDate.AddDays(i % 365).ToString("yyyy-MM-dd");
            pb.Value = i % 2 == 0 ? 1 : 0;
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    private void InsertLiteDbTestData(int count)
    {
        var docs = new List<DataDoc>(count);
        var baseDate = new DateTime(2024, 1, 1);
        for (int i = 0; i < count; i++)
        {
            docs.Add(new DataDoc
            {
                Id = i + 1,
                IntVal = i * 10,
                DoubleVal = i * 1.5,
                StringVal = $"String value number {i} with some additional text for padding",
                DateVal = baseDate.AddDays(i % 365),
                BoolVal = i % 2 == 0
            });
        }
        m_liteCollection.InsertBulk(docs);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        m_witConn?.Dispose();
        m_sqliteConn?.Dispose();
        m_liteDb?.Dispose();
        CleanupPaths();
    }

    #endregion

    #region Benchmarks - Read All Rows

    [Benchmark(Description = "Read all rows (just iterate)")]
    public int ReadAllRowsIterate()
    {
        int cnt = 0;
        if (Provider == DbProviderType.WitDb)
        {
            using var cmd = m_witConn.CreateCommand();
            cmd.CommandText = "SELECT * FROM Data";
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) cnt++;
        }
        else if (Provider == DbProviderType.SQLite)
        {
            using var cmd = m_sqliteConn.CreateCommand();
            cmd.CommandText = "SELECT * FROM Data";
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) cnt++;
        }
        else
        {
            foreach (var doc in m_liteCollection.FindAll())
                cnt++;
        }
        return cnt;
    }

    [Benchmark(Description = "Read all rows (GetValue)")]
    public int ReadAllRowsGetValue()
    {
        int cnt = 0;
        if (Provider == DbProviderType.WitDb)
        {
            using var cmd = m_witConn.CreateCommand();
            cmd.CommandText = "SELECT Id, IntVal, DoubleVal, StringVal FROM Data";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                _ = reader.GetValue(0);
                _ = reader.GetValue(1);
                _ = reader.GetValue(2);
                _ = reader.GetValue(3);
                cnt++;
            }
        }
        else if (Provider == DbProviderType.SQLite)
        {
            using var cmd = m_sqliteConn.CreateCommand();
            cmd.CommandText = "SELECT Id, IntVal, DoubleVal, StringVal FROM Data";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                _ = reader.GetValue(0);
                _ = reader.GetValue(1);
                _ = reader.GetValue(2);
                _ = reader.GetValue(3);
                cnt++;
            }
        }
        else
        {
            foreach (var doc in m_liteCollection.FindAll())
            {
                _ = doc.Id;
                _ = doc.IntVal;
                _ = doc.DoubleVal;
                _ = doc.StringVal;
                cnt++;
            }
        }
        return cnt;
    }

    #endregion

    #region Benchmarks - Typed Getters

    [Benchmark(Description = "Read with typed getters")]
    public int ReadTypedGetters()
    {
        int cnt = 0;
        if (Provider == DbProviderType.WitDb)
        {
            using var cmd = m_witConn.CreateCommand();
            cmd.CommandText = "SELECT Id, IntVal, DoubleVal, StringVal FROM Data";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                _ = reader.GetInt64(0);
                _ = reader.GetInt32(1);
                _ = reader.GetDouble(2);
                _ = reader.GetString(3);
                cnt++;
            }
        }
        else if (Provider == DbProviderType.SQLite)
        {
            using var cmd = m_sqliteConn.CreateCommand();
            cmd.CommandText = "SELECT Id, IntVal, DoubleVal, StringVal FROM Data";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                _ = reader.GetInt64(0);
                _ = reader.GetInt32(1);
                _ = reader.GetDouble(2);
                _ = reader.GetString(3);
                cnt++;
            }
        }
        else
        {
            // LiteDB always returns strongly typed - same as GetValue
            foreach (var doc in m_liteCollection.FindAll())
            {
                _ = doc.Id;
                _ = doc.IntVal;
                _ = doc.DoubleVal;
                _ = doc.StringVal;
                cnt++;
            }
        }
        return cnt;
    }

    [Benchmark(Description = "Read by column name")]
    public int ReadByColumnName()
    {
        int cnt = 0;
        if (Provider == DbProviderType.WitDb)
        {
            using var cmd = m_witConn.CreateCommand();
            cmd.CommandText = "SELECT Id, IntVal, DoubleVal, StringVal FROM Data";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                _ = reader["Id"];
                _ = reader["IntVal"];
                _ = reader["DoubleVal"];
                _ = reader["StringVal"];
                cnt++;
            }
        }
        else if (Provider == DbProviderType.SQLite)
        {
            using var cmd = m_sqliteConn.CreateCommand();
            cmd.CommandText = "SELECT Id, IntVal, DoubleVal, StringVal FROM Data";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                _ = reader["Id"];
                _ = reader["IntVal"];
                _ = reader["DoubleVal"];
                _ = reader["StringVal"];
                cnt++;
            }
        }
        else
        {
            // LiteDB - property access is equivalent
            foreach (var doc in m_liteCollection.FindAll())
            {
                _ = doc.Id;
                _ = doc.IntVal;
                _ = doc.DoubleVal;
                _ = doc.StringVal;
                cnt++;
            }
        }
        return cnt;
    }

    #endregion

    #region Benchmarks - Filtered Query

    [Benchmark(Description = "Read filtered (IntVal > N)")]
    public int ReadFiltered()
    {
        int cnt = 0;
        int threshold = RowCount * 5; // ~50% of data

        if (Provider == DbProviderType.WitDb)
        {
            using var cmd = m_witConn.CreateCommand();
            cmd.CommandText = $"SELECT * FROM Data WHERE IntVal > {threshold}";
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) cnt++;
        }
        else if (Provider == DbProviderType.SQLite)
        {
            using var cmd = m_sqliteConn.CreateCommand();
            cmd.CommandText = $"SELECT * FROM Data WHERE IntVal > {threshold}";
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) cnt++;
        }
        else
        {
            foreach (var doc in m_liteCollection.Find(x => x.IntVal > threshold))
                cnt++;
        }
        return cnt;
    }

    #endregion

    #region IDisposable

    public void Dispose() => GlobalCleanup();

    #endregion
}

/// <summary>
/// Data document for LiteDB benchmarks.
/// </summary>
public class DataDoc
{
    public int Id { get; set; }
    public int IntVal { get; set; }
    public double DoubleVal { get; set; }
    public string StringVal { get; set; } = string.Empty;
    public DateTime DateVal { get; set; }
    public bool BoolVal { get; set; }
}

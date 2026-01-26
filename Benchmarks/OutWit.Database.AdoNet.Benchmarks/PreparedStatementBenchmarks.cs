using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using LiteDB;
using Microsoft.Data.Sqlite;
using OutWit.Database.AdoNet;
using System.Data.Common;

namespace OutWit.Database.AdoNet.Benchmarks;

/// <summary>
/// Benchmarks for prepared statement performance.
/// Tests prepared vs non-prepared statements.
/// </summary>
[Config(typeof(AdoNetBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class PreparedStatementBenchmarks : IDisposable
{
    #region Fields

    private WitDbConnection m_witConn = null!;
    private SqliteConnection m_sqliteConn = null!;
    private LiteDatabase m_liteDb = null!;
    private ILiteCollection<ItemDoc> m_liteCollection = null!;
    private string m_witPath = null!;
    private string m_sqlitePath = null!;
    private string m_liteDbPath = null!;

    #endregion

    #region Parameters

    [Params(100, 500)]
    public int Iterations { get; set; }

    [Params(DbProviderType.WitDb, DbProviderType.SQLite, DbProviderType.LiteDB)]
    public DbProviderType Provider { get; set; }

    #endregion

    #region Setup/Cleanup

    [GlobalSetup]
    public void GlobalSetup()
    {
        m_witPath = BenchmarkPathHelper.GenerateUniquePath("wit_prep", ".witdb");
        m_sqlitePath = BenchmarkPathHelper.GenerateUniquePath("sql_prep", ".db");
        m_liteDbPath = BenchmarkPathHelper.GenerateUniquePath("lite_prep", ".db");

        CleanupPaths();

        // Setup WitDb
        m_witConn = new WitDbConnection($"Data Source={m_witPath}");
        m_witConn.Open();
        using (var cmd = m_witConn.CreateCommand())
        {
            cmd.CommandText = "CREATE TABLE Items (Id BIGINT PRIMARY KEY AUTOINCREMENT, Value INT)";
            cmd.ExecuteNonQuery();
        }
        InsertWitDbTestData(1000);

        // Setup SQLite
        m_sqliteConn = new SqliteConnection($"Data Source={m_sqlitePath}");
        m_sqliteConn.Open();
        using (var cmd = m_sqliteConn.CreateCommand())
        {
            cmd.CommandText = "CREATE TABLE Items (Id INTEGER PRIMARY KEY, Value INTEGER)";
            cmd.ExecuteNonQuery();
        }
        InsertSqliteTestData(1000);

        // Setup LiteDB
        m_liteDb = new LiteDatabase(m_liteDbPath);
        m_liteCollection = m_liteDb.GetCollection<ItemDoc>("items");
        InsertLiteDbTestData(1000);
    }

    private void CleanupPaths()
    {
        BenchmarkPathHelper.SafeCleanup(m_witPath);
        BenchmarkPathHelper.SafeCleanup(m_witPath + "_indexes");
        BenchmarkPathHelper.SafeCleanup(m_sqlitePath);
        BenchmarkPathHelper.SafeCleanup(m_liteDbPath);
    }

    private void InsertWitDbTestData(int count)
    {
        using var tx = (WitDbTransaction)m_witConn.BeginTransaction();
        using var cmd = m_witConn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO Items (Value) VALUES (@v)";
        var p = cmd.CreateParameter(); p.ParameterName = "@v"; cmd.Parameters.Add(p);
        for (int i = 0; i < count; i++)
        {
            p.Value = i;
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    private void InsertSqliteTestData(int count)
    {
        using var tx = m_sqliteConn.BeginTransaction();
        using var cmd = m_sqliteConn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO Items (Value) VALUES (@v)";
        var p = cmd.CreateParameter(); p.ParameterName = "@v"; cmd.Parameters.Add(p);
        for (int i = 0; i < count; i++)
        {
            p.Value = i;
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    private void InsertLiteDbTestData(int count)
    {
        var docs = new List<ItemDoc>(count);
        for (int i = 0; i < count; i++)
        {
            docs.Add(new ItemDoc { Id = i + 1, Value = i });
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

    #region Benchmarks - Reused Command

    [Benchmark(Description = "Reuse command (params change)")]
    public int ReuseCommandParamsChange()
    {
        int total = 0;
        if (Provider == DbProviderType.WitDb)
        {
            using var cmd = m_witConn.CreateCommand();
            cmd.CommandText = "SELECT Value FROM Items WHERE Id = @id";
            var p = cmd.CreateParameter(); p.ParameterName = "@id"; cmd.Parameters.Add(p);

            for (int i = 1; i <= Iterations; i++)
            {
                p.Value = i;
                var result = cmd.ExecuteScalar();
                if (result != null) total += Convert.ToInt32(result);
            }
        }
        else if (Provider == DbProviderType.SQLite)
        {
            using var cmd = m_sqliteConn.CreateCommand();
            cmd.CommandText = "SELECT Value FROM Items WHERE Id = @id";
            var p = cmd.CreateParameter(); p.ParameterName = "@id"; cmd.Parameters.Add(p);

            for (int i = 1; i <= Iterations; i++)
            {
                p.Value = i;
                var result = cmd.ExecuteScalar();
                if (result != null) total += Convert.ToInt32(result);
            }
        }
        else
        {
            for (int i = 1; i <= Iterations; i++)
            {
                var doc = m_liteCollection.FindById(i);
                if (doc != null) total += doc.Value;
            }
        }
        return total;
    }

    [Benchmark(Description = "New command each time")]
    public int NewCommandEachTime()
    {
        int total = 0;
        if (Provider == DbProviderType.WitDb)
        {
            for (int i = 1; i <= Iterations; i++)
            {
                using var cmd = m_witConn.CreateCommand();
                cmd.CommandText = "SELECT Value FROM Items WHERE Id = @id";
                var p = cmd.CreateParameter(); p.ParameterName = "@id"; p.Value = i; cmd.Parameters.Add(p);
                var result = cmd.ExecuteScalar();
                if (result != null) total += Convert.ToInt32(result);
            }
        }
        else if (Provider == DbProviderType.SQLite)
        {
            for (int i = 1; i <= Iterations; i++)
            {
                using var cmd = m_sqliteConn.CreateCommand();
                cmd.CommandText = "SELECT Value FROM Items WHERE Id = @id";
                var p = cmd.CreateParameter(); p.ParameterName = "@id"; p.Value = i; cmd.Parameters.Add(p);
                var result = cmd.ExecuteScalar();
                if (result != null) total += Convert.ToInt32(result);
            }
        }
        else
        {
            // LiteDB doesn't have command concept - same as reuse
            for (int i = 1; i <= Iterations; i++)
            {
                var doc = m_liteCollection.FindById(i);
                if (doc != null) total += doc.Value;
            }
        }
        return total;
    }

    #endregion

    #region Benchmarks - Prepared Statement

    [Benchmark(Description = "Prepare + Execute")]
    public int PrepareAndExecute()
    {
        int total = 0;
        if (Provider == DbProviderType.WitDb)
        {
            using var cmd = m_witConn.CreateCommand();
            cmd.CommandText = "SELECT Value FROM Items WHERE Id = @id";
            var p = cmd.CreateParameter(); p.ParameterName = "@id"; cmd.Parameters.Add(p);
            cmd.Prepare();

            for (int i = 1; i <= Iterations; i++)
            {
                p.Value = i;
                var result = cmd.ExecuteScalar();
                if (result != null) total += Convert.ToInt32(result);
            }
        }
        else if (Provider == DbProviderType.SQLite)
        {
            using var cmd = m_sqliteConn.CreateCommand();
            cmd.CommandText = "SELECT Value FROM Items WHERE Id = @id";
            var p = cmd.CreateParameter(); p.ParameterName = "@id"; cmd.Parameters.Add(p);
            cmd.Prepare();

            for (int i = 1; i <= Iterations; i++)
            {
                p.Value = i;
                var result = cmd.ExecuteScalar();
                if (result != null) total += Convert.ToInt32(result);
            }
        }
        else
        {
            // LiteDB doesn't have prepare - same as FindById
            for (int i = 1; i <= Iterations; i++)
            {
                var doc = m_liteCollection.FindById(i);
                if (doc != null) total += doc.Value;
            }
        }
        return total;
    }

    #endregion

    #region Benchmarks - Batch Operations

    [Benchmark(Description = "Batch INSERT in transaction")]
    public int BatchInsertTransaction()
    {
        if (Provider == DbProviderType.WitDb)
        {
            using var tx = (WitDbTransaction)m_witConn.BeginTransaction();
            using var cmd = m_witConn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO Items (Value) VALUES (@v)";
            var p = cmd.CreateParameter(); p.ParameterName = "@v"; cmd.Parameters.Add(p);

            for (int i = 0; i < Iterations; i++)
            {
                p.Value = i + 10000;
                cmd.ExecuteNonQuery();
            }
            tx.Rollback(); // Don't actually commit to keep db size stable
        }
        else if (Provider == DbProviderType.SQLite)
        {
            using var tx = m_sqliteConn.BeginTransaction();
            using var cmd = m_sqliteConn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO Items (Value) VALUES (@v)";
            var p = cmd.CreateParameter(); p.ParameterName = "@v"; cmd.Parameters.Add(p);

            for (int i = 0; i < Iterations; i++)
            {
                p.Value = i + 10000;
                cmd.ExecuteNonQuery();
            }
            tx.Rollback();
        }
        else
        {
            // LiteDB - use transaction
            m_liteDb.BeginTrans();
            try
            {
                var docs = new List<ItemDoc>(Iterations);
                for (int i = 0; i < Iterations; i++)
                {
                    docs.Add(new ItemDoc { Value = i + 10000 });
                }
                m_liteCollection.InsertBulk(docs);
                m_liteDb.Rollback();
            }
            catch
            {
                m_liteDb.Rollback();
                throw;
            }
        }
        return Iterations;
    }

    #endregion

    #region IDisposable

    public void Dispose() => GlobalCleanup();

    #endregion
}

/// <summary>
/// Item document for LiteDB benchmarks.
/// </summary>
public class ItemDoc
{
    public int Id { get; set; }
    public int Value { get; set; }
}

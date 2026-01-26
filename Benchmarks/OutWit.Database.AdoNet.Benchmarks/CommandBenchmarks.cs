using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using LiteDB;
using Microsoft.Data.Sqlite;
using OutWit.Database.AdoNet;
using System.Data;
using System.Data.Common;

namespace OutWit.Database.AdoNet.Benchmarks;

/// <summary>
/// Benchmarks for command execution performance.
/// Tests ExecuteNonQuery, ExecuteScalar, ExecuteReader.
/// </summary>
[Config(typeof(AdoNetBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class CommandBenchmarks : IDisposable
{
    #region Fields

    private WitDbConnection m_witConn = null!;
    private SqliteConnection m_sqliteConn = null!;
    private LiteDatabase m_liteDb = null!;
    private ILiteCollection<UserDoc> m_liteCollection = null!;
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
        m_witPath = BenchmarkPathHelper.GenerateUniquePath("wit_cmd", ".witdb");
        m_sqlitePath = BenchmarkPathHelper.GenerateUniquePath("sql_cmd", ".db");
        m_liteDbPath = BenchmarkPathHelper.GenerateUniquePath("lite_cmd", ".db");

        CleanupPaths();

        // Setup WitDb
        m_witConn = new WitDbConnection($"Data Source={m_witPath}");
        m_witConn.Open();
        using (var cmd = m_witConn.CreateCommand())
        {
            cmd.CommandText = @"CREATE TABLE Users (
                Id BIGINT PRIMARY KEY AUTOINCREMENT,
                Name VARCHAR(100),
                Email VARCHAR(255),
                Age INT
            )";
            cmd.ExecuteNonQuery();
        }
        InsertTestData(m_witConn, 1000);

        // Setup SQLite
        m_sqliteConn = new SqliteConnection($"Data Source={m_sqlitePath}");
        m_sqliteConn.Open();
        using (var cmd = m_sqliteConn.CreateCommand())
        {
            cmd.CommandText = @"CREATE TABLE Users (
                Id INTEGER PRIMARY KEY,
                Name TEXT,
                Email TEXT,
                Age INTEGER
            )";
            cmd.ExecuteNonQuery();
        }
        InsertTestData(m_sqliteConn, 1000);

        // Setup LiteDB
        m_liteDb = new LiteDatabase(m_liteDbPath);
        m_liteCollection = m_liteDb.GetCollection<UserDoc>("users");
        m_liteCollection.EnsureIndex(x => x.Name);
        m_liteCollection.EnsureIndex(x => x.Email);
        InsertLiteDbTestData(1000);
    }

    private void CleanupPaths()
    {
        BenchmarkPathHelper.SafeCleanup(m_witPath);
        BenchmarkPathHelper.SafeCleanup(m_witPath + "_indexes");
        BenchmarkPathHelper.SafeCleanup(m_sqlitePath);
        BenchmarkPathHelper.SafeCleanup(m_liteDbPath);
    }

    private void InsertTestData(DbConnection conn, int count)
    {
        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO Users (Name, Email, Age) VALUES (@n, @e, @a)";

        var pn = cmd.CreateParameter(); pn.ParameterName = "@n"; cmd.Parameters.Add(pn);
        var pe = cmd.CreateParameter(); pe.ParameterName = "@e"; cmd.Parameters.Add(pe);
        var pa = cmd.CreateParameter(); pa.ParameterName = "@a"; cmd.Parameters.Add(pa);

        for (int i = 0; i < count; i++)
        {
            pn.Value = $"User_{i}";
            pe.Value = $"user{i}@test.com";
            pa.Value = 20 + (i % 50);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    private void InsertLiteDbTestData(int count)
    {
        var docs = new List<UserDoc>(count);
        for (int i = 0; i < count; i++)
        {
            docs.Add(new UserDoc
            {
                Id = i + 1,
                Name = $"User_{i}",
                Email = $"user{i}@test.com",
                Age = 20 + (i % 50)
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

    #region Benchmarks - ExecuteNonQuery / Update

    [Benchmark(Description = "Update single record")]
    public int UpdateSingleRecord()
    {
        if (Provider == DbProviderType.WitDb)
        {
            using var cmd = m_witConn.CreateCommand();
            cmd.CommandText = "UPDATE Users SET Age = Age + 1 WHERE Id = 1";
            return cmd.ExecuteNonQuery();
        }
        else if (Provider == DbProviderType.SQLite)
        {
            using var cmd = m_sqliteConn.CreateCommand();
            cmd.CommandText = "UPDATE Users SET Age = Age + 1 WHERE Id = 1";
            return cmd.ExecuteNonQuery();
        }
        else
        {
            var doc = m_liteCollection.FindById(1);
            if (doc != null)
            {
                doc.Age++;
                return m_liteCollection.Update(doc) ? 1 : 0;
            }
            return 0;
        }
    }

    [Benchmark(Description = "Update with parameters")]
    public int UpdateWithParams()
    {
        if (Provider == DbProviderType.WitDb)
        {
            using var cmd = m_witConn.CreateCommand();
            cmd.CommandText = "UPDATE Users SET Name = @n WHERE Id = @id";
            var pn = cmd.CreateParameter(); pn.ParameterName = "@n"; pn.Value = "Updated"; cmd.Parameters.Add(pn);
            var pid = cmd.CreateParameter(); pid.ParameterName = "@id"; pid.Value = 1; cmd.Parameters.Add(pid);
            return cmd.ExecuteNonQuery();
        }
        else if (Provider == DbProviderType.SQLite)
        {
            using var cmd = m_sqliteConn.CreateCommand();
            cmd.CommandText = "UPDATE Users SET Name = @n WHERE Id = @id";
            var pn = cmd.CreateParameter(); pn.ParameterName = "@n"; pn.Value = "Updated"; cmd.Parameters.Add(pn);
            var pid = cmd.CreateParameter(); pid.ParameterName = "@id"; pid.Value = 1; cmd.Parameters.Add(pid);
            return cmd.ExecuteNonQuery();
        }
        else
        {
            var doc = m_liteCollection.FindById(1);
            if (doc != null)
            {
                doc.Name = "Updated";
                return m_liteCollection.Update(doc) ? 1 : 0;
            }
            return 0;
        }
    }

    #endregion

    #region Benchmarks - ExecuteScalar / Count

    [Benchmark(Description = "Count all records")]
    public long CountAllRecords()
    {
        if (Provider == DbProviderType.WitDb)
        {
            using var cmd = m_witConn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Users";
            return Convert.ToInt64(cmd.ExecuteScalar());
        }
        else if (Provider == DbProviderType.SQLite)
        {
            using var cmd = m_sqliteConn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Users";
            return Convert.ToInt64(cmd.ExecuteScalar());
        }
        else
        {
            return m_liteCollection.Count();
        }
    }

    [Benchmark(Description = "Scalar query (simple)")]
    public int ScalarQuerySimple()
    {
        if (Provider == DbProviderType.WitDb)
        {
            using var cmd = m_witConn.CreateCommand();
            cmd.CommandText = "SELECT 42";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
        else if (Provider == DbProviderType.SQLite)
        {
            using var cmd = m_sqliteConn.CreateCommand();
            cmd.CommandText = "SELECT 42";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
        else
        {
            // LiteDB doesn't have scalar queries, return constant
            return 42;
        }
    }

    [Benchmark(Description = "Find by Id")]
    public object? FindById()
    {
        if (Provider == DbProviderType.WitDb)
        {
            using var cmd = m_witConn.CreateCommand();
            cmd.CommandText = "SELECT Name FROM Users WHERE Id = @id";
            var p = cmd.CreateParameter(); p.ParameterName = "@id"; p.Value = 500; cmd.Parameters.Add(p);
            return cmd.ExecuteScalar();
        }
        else if (Provider == DbProviderType.SQLite)
        {
            using var cmd = m_sqliteConn.CreateCommand();
            cmd.CommandText = "SELECT Name FROM Users WHERE Id = @id";
            var p = cmd.CreateParameter(); p.ParameterName = "@id"; p.Value = 500; cmd.Parameters.Add(p);
            return cmd.ExecuteScalar();
        }
        else
        {
            var doc = m_liteCollection.FindById(500);
            return doc?.Name;
        }
    }

    #endregion

    #region Benchmarks - ExecuteReader / FindAll

    [Benchmark(Description = "Read all records")]
    public int ReadAllRecords()
    {
        int cnt = 0;
        if (Provider == DbProviderType.WitDb)
        {
            using var cmd = m_witConn.CreateCommand();
            cmd.CommandText = "SELECT * FROM Users";
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) cnt++;
        }
        else if (Provider == DbProviderType.SQLite)
        {
            using var cmd = m_sqliteConn.CreateCommand();
            cmd.CommandText = "SELECT * FROM Users";
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

    [Benchmark(Description = "Read 100 records")]
    public int Read100Records()
    {
        int cnt = 0;
        if (Provider == DbProviderType.WitDb)
        {
            using var cmd = m_witConn.CreateCommand();
            cmd.CommandText = "SELECT * FROM Users LIMIT 100";
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) cnt++;
        }
        else if (Provider == DbProviderType.SQLite)
        {
            using var cmd = m_sqliteConn.CreateCommand();
            cmd.CommandText = "SELECT * FROM Users LIMIT 100";
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) cnt++;
        }
        else
        {
            foreach (var doc in m_liteCollection.FindAll().Take(100))
                cnt++;
        }
        return cnt;
    }

    [Benchmark(Description = "Read with field access")]
    public int ReadWithFieldAccess()
    {
        int cnt = 0;
        if (Provider == DbProviderType.WitDb)
        {
            using var cmd = m_witConn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, Email, Age FROM Users LIMIT 100";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetValue(0);
                var name = reader.GetValue(1);
                var email = reader.GetValue(2);
                var age = reader.GetValue(3);
                cnt++;
            }
        }
        else if (Provider == DbProviderType.SQLite)
        {
            using var cmd = m_sqliteConn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, Email, Age FROM Users LIMIT 100";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetValue(0);
                var name = reader.GetValue(1);
                var email = reader.GetValue(2);
                var age = reader.GetValue(3);
                cnt++;
            }
        }
        else
        {
            foreach (var doc in m_liteCollection.FindAll().Take(100))
            {
                var id = doc.Id;
                var name = doc.Name;
                var email = doc.Email;
                var age = doc.Age;
                cnt++;
            }
        }
        return cnt;
    }

    #endregion

    #region IDisposable

    public void Dispose() => GlobalCleanup();

    #endregion
}

/// <summary>
/// User document for LiteDB benchmarks.
/// </summary>
public class UserDoc
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
}

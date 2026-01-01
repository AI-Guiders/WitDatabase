using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using LiteDB;
using Microsoft.EntityFrameworkCore;

namespace OutWit.Database.EntityFramework.Benchmarks;

/// <summary>
/// Benchmarks for EF Core CRUD operations.
/// Tests Add, Update, Remove, SaveChanges patterns.
/// LiteDB included for managed .NET memory comparison.
/// </summary>
[Config(typeof(EfCoreBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class CrudBenchmarks : IDisposable
{
    #region Fields

    private string m_dbPath = null!;
    private string? m_liteDbPath;

    #endregion

    #region Parameters

    [Params(10, 50, 100)]
    public int BatchSize { get; set; }

    [Params(EfProviderType.WitDb, EfProviderType.SQLite, EfProviderType.LiteDB)]
    public EfProviderType Provider { get; set; }

    #endregion

    #region Setup/Cleanup

    [GlobalSetup]
    public void GlobalSetup()
    {
        var providerSuffix = Provider.ToString().ToLowerInvariant();
        m_dbPath = BenchmarkPathHelper.GenerateUniquePath($"efcrud_{providerSuffix}", 
            Provider == EfProviderType.WitDb ? ".witdb" : ".db");
        m_liteDbPath = Provider == EfProviderType.LiteDB ? m_dbPath : null;
    }

    [IterationSetup]
    public void IterationSetup()
    {
        CleanupPaths();

        if (Provider == EfProviderType.LiteDB)
        {
            // Recreate LiteDB
            using var db = new LiteDatabase(m_liteDbPath!);
            var col = db.GetCollection<LiteUser>("users");
            var users = new List<LiteUser>();
            for (int i = 0; i < 100; i++)
            {
                users.Add(new LiteUser
                {
                    Id = i + 1,
                    Name = $"User {i}",
                    Email = $"user{i}@test.com",
                    Age = 20 + (i % 50),
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                });
            }
            col.InsertBulk(users);
        }
        else
        {
            // Recreate databases for each iteration
            using var ctx = CreateContext();
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            // Seed with some initial data
            for (int i = 0; i < 100; i++)
            {
                ctx.Set<User>().Add(new User
                {
                    Name = $"User {i}",
                    Email = $"user{i}@test.com",
                    Age = 20 + (i % 50),
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                });
            }
            ctx.SaveChanges();
        }
    }

    private void CleanupPaths()
    {
        BenchmarkPathHelper.SafeCleanup(m_dbPath);
        if (Provider == EfProviderType.WitDb)
            BenchmarkPathHelper.SafeCleanup(m_dbPath + "_indexes");
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        CleanupPaths();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        CleanupPaths();
    }

    private DbContext CreateContext()
    {
        if (Provider == EfProviderType.WitDb)
            return WitDbBenchmarkContext.Create($"Data Source={m_dbPath}");
        return SqliteBenchmarkContext.Create($"Data Source={m_dbPath}");
    }

    #endregion

    #region Benchmarks - Add

    [Benchmark(Description = "Add single + SaveChanges")]
    public int AddSingle()
    {
        if (Provider == EfProviderType.LiteDB)
        {
            using var db = new LiteDatabase(m_liteDbPath!);
            var col = db.GetCollection<LiteUser>("users");
            col.Insert(new LiteUser
            {
                Name = "New User",
                Email = "new@test.com",
                Age = 25,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
            return 1;
        }

        using var ctx = CreateContext();
        ctx.Set<User>().Add(new User
        {
            Name = "New User",
            Email = "new@test.com",
            Age = 25,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        return ctx.SaveChanges();
    }

    [Benchmark(Description = "AddRange + SaveChanges")]
    public int AddRange()
    {
        if (Provider == EfProviderType.LiteDB)
        {
            using var db = new LiteDatabase(m_liteDbPath!);
            var col = db.GetCollection<LiteUser>("users");
            var users = Enumerable.Range(0, BatchSize).Select(i => new LiteUser
            {
                Name = $"Batch User {i}",
                Email = $"batch{i}@test.com",
                Age = 20 + (i % 50),
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            }).ToList();
            return col.InsertBulk(users);
        }

        using var ctx = CreateContext();
        var efUsers = Enumerable.Range(0, BatchSize).Select(i => new User
        {
            Name = $"Batch User {i}",
            Email = $"batch{i}@test.com",
            Age = 20 + (i % 50),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        }).ToList();

        ctx.Set<User>().AddRange(efUsers);
        return ctx.SaveChanges();
    }

    [Benchmark(Description = "Add in loop + single SaveChanges")]
    public int AddLoopSingleSave()
    {
        if (Provider == EfProviderType.LiteDB)
        {
            using var db = new LiteDatabase(m_liteDbPath!);
            var col = db.GetCollection<LiteUser>("users");
            db.BeginTrans();
            for (int i = 0; i < BatchSize; i++)
            {
                col.Insert(new LiteUser
                {
                    Name = $"Loop User {i}",
                    Email = $"loop{i}@test.com",
                    Age = 20 + (i % 50),
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                });
            }
            db.Commit();
            return BatchSize;
        }

        using var ctx = CreateContext();
        for (int i = 0; i < BatchSize; i++)
        {
            ctx.Set<User>().Add(new User
            {
                Name = $"Loop User {i}",
                Email = $"loop{i}@test.com",
                Age = 20 + (i % 50),
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
        }
        return ctx.SaveChanges();
    }

    #endregion

    #region Benchmarks - Update

    [Benchmark(Description = "Update single + SaveChanges")]
    public int UpdateSingle()
    {
        if (Provider == EfProviderType.LiteDB)
        {
            using var db = new LiteDatabase(m_liteDbPath!);
            var col = db.GetCollection<LiteUser>("users");
            var user = col.FindById(1);
            if (user != null)
            {
                user.Name = "Updated Name";
                user.Age = 99;
                col.Update(user);
            }
            return 1;
        }

        using var ctx = CreateContext();
        var efUser = ctx.Set<User>().First();
        efUser.Name = "Updated Name";
        efUser.Age = 99;
        return ctx.SaveChanges();
    }

    [Benchmark(Description = "Update multiple + SaveChanges")]
    public int UpdateMultiple()
    {
        if (Provider == EfProviderType.LiteDB)
        {
            using var db = new LiteDatabase(m_liteDbPath!);
            var col = db.GetCollection<LiteUser>("users");
            var users = col.FindAll().Take(BatchSize).ToList();
            foreach (var user in users)
            {
                user.Age++;
                col.Update(user);
            }
            return users.Count;
        }

        using var ctx = CreateContext();
        var efUsers = ctx.Set<User>().Take(BatchSize).ToList();
        foreach (var user in efUsers)
        {
            user.Age++;
        }
        return ctx.SaveChanges();
    }

    #endregion

    #region Benchmarks - Remove

    [Benchmark(Description = "Remove single + SaveChanges")]
    public int RemoveSingle()
    {
        if (Provider == EfProviderType.LiteDB)
        {
            using var db = new LiteDatabase(m_liteDbPath!);
            var col = db.GetCollection<LiteUser>("users");
            col.Delete(1);
            return 1;
        }

        using var ctx = CreateContext();
        var user = ctx.Set<User>().First();
        ctx.Set<User>().Remove(user);
        return ctx.SaveChanges();
    }

    [Benchmark(Description = "RemoveRange + SaveChanges")]
    public int RemoveRange()
    {
        if (Provider == EfProviderType.LiteDB)
        {
            using var db = new LiteDatabase(m_liteDbPath!);
            var col = db.GetCollection<LiteUser>("users");
            var ids = col.FindAll().Take(BatchSize).Select(u => u.Id).ToList();
            foreach (var id in ids)
                col.Delete(id);
            return ids.Count;
        }

        using var ctx = CreateContext();
        var users = ctx.Set<User>().Take(BatchSize).ToList();
        ctx.Set<User>().RemoveRange(users);
        return ctx.SaveChanges();
    }

    #endregion

    #region IDisposable

    public void Dispose() => GlobalCleanup();

    #endregion
}

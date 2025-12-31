using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using LiteDB;
using Microsoft.EntityFrameworkCore;

namespace OutWit.Database.EntityFramework.Benchmarks;

/// <summary>
/// Benchmarks for EF Core change tracking performance.
/// Tests tracking vs no-tracking, DetectChanges overhead.
/// LiteDB included for managed .NET memory comparison.
/// </summary>
[Config(typeof(EfCoreBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class TrackingBenchmarks : IDisposable
{
    #region Fields

    private WitDbBenchmarkContext m_witContext = null!;
    private SqliteBenchmarkContext m_sqliteContext = null!;
    private LiteDatabase m_liteDb = null!;
    private ILiteCollection<LiteUser> m_liteCollection = null!;
    private string m_witPath = null!;
    private string m_sqlitePath = null!;
    private string m_liteDbPath = null!;

    #endregion

    #region Parameters

    [Params(100, 500, 1000)]
    public int EntityCount { get; set; }

    [Params(EfProviderType.WitDb, EfProviderType.SQLite, EfProviderType.LiteDB)]
    public EfProviderType Provider { get; set; }

    #endregion

    #region Setup/Cleanup

    [GlobalSetup]
    public void GlobalSetup()
    {
        m_witPath = BenchmarkPathHelper.GenerateUniquePath("wit_eftrack", ".witdb");
        m_sqlitePath = BenchmarkPathHelper.GenerateUniquePath("sql_eftrack", ".db");
        m_liteDbPath = BenchmarkPathHelper.GenerateUniquePath("lite_eftrack", ".db");

        CleanupPaths();

        // Setup WitDb
        m_witContext = new WitDbBenchmarkContext($"Data Source={m_witPath}");
        m_witContext.Database.EnsureCreated();
        SeedData(m_witContext, EntityCount);

        // Setup SQLite
        m_sqliteContext = new SqliteBenchmarkContext($"Data Source={m_sqlitePath}");
        m_sqliteContext.Database.EnsureCreated();
        SeedData(m_sqliteContext, EntityCount);

        // Setup LiteDB
        m_liteDb = new LiteDatabase(m_liteDbPath);
        m_liteCollection = m_liteDb.GetCollection<LiteUser>("users");
        SeedLiteDbData(EntityCount);
    }

    private void CleanupPaths()
    {
        BenchmarkPathHelper.SafeCleanup(m_witPath);
        BenchmarkPathHelper.SafeCleanup(m_witPath + "_indexes");
        BenchmarkPathHelper.SafeCleanup(m_sqlitePath);
        BenchmarkPathHelper.SafeCleanup(m_liteDbPath);
    }

    private void SeedData(DbContext context, int count)
    {
        for (int i = 0; i < count; i++)
        {
            context.Set<User>().Add(new User
            {
                Name = $"User {i}",
                Email = $"user{i}@test.com",
                Age = 20 + (i % 50),
                CreatedAt = DateTime.UtcNow,
                IsActive = i % 2 == 0
            });
        }
        context.SaveChanges();
        context.ChangeTracker.Clear();
    }

    private void SeedLiteDbData(int count)
    {
        var users = new List<LiteUser>();
        for (int i = 0; i < count; i++)
        {
            users.Add(new LiteUser
            {
                Id = i + 1,
                Name = $"User {i}",
                Email = $"user{i}@test.com",
                Age = 20 + (i % 50),
                CreatedAt = DateTime.UtcNow,
                IsActive = i % 2 == 0
            });
        }
        m_liteCollection.InsertBulk(users);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        m_witContext?.Dispose();
        m_sqliteContext?.Dispose();
        m_liteDb?.Dispose();
        CleanupPaths();
    }

    private DbContext GetContext()
    {
        if (Provider == EfProviderType.WitDb)
            return m_witContext;
        return m_sqliteContext;
    }

    #endregion

    #region Benchmarks - Tracking vs NoTracking

    [Benchmark(Description = "With Tracking (default)")]
    public int WithTracking()
    {
        if (Provider == EfProviderType.LiteDB)
        {
            // LiteDB doesn't have tracking - just return all
            return m_liteCollection.FindAll().ToList().Count;
        }

        var ctx = GetContext();
        ctx.ChangeTracker.Clear();
        return ctx.Set<User>().ToList().Count;
    }

    [Benchmark(Description = "AsNoTracking")]
    public int WithNoTracking()
    {
        if (Provider == EfProviderType.LiteDB)
        {
            return m_liteCollection.FindAll().ToList().Count;
        }

        var ctx = GetContext();
        return ctx.Set<User>().AsNoTracking().ToList().Count;
    }

    [Benchmark(Description = "AsNoTrackingWithIdentityResolution")]
    public int WithNoTrackingIdentityResolution()
    {
        if (Provider == EfProviderType.LiteDB)
        {
            return m_liteCollection.FindAll().ToList().Count;
        }

        var ctx = GetContext();
        return ctx.Set<User>().AsNoTrackingWithIdentityResolution().ToList().Count;
    }

    #endregion

    #region Benchmarks - AutoDetectChanges

    [Benchmark(Description = "Load + Modify + SaveChanges")]
    public int LoadModifySave()
    {
        if (Provider == EfProviderType.LiteDB)
        {
            var users = m_liteCollection.FindAll().Take(50).ToList();
            foreach (var user in users)
            {
                user.Age++;
                m_liteCollection.Update(user);
            }
            return users.Count;
        }

        var ctx = GetContext();
        ctx.ChangeTracker.Clear();
        var efUsers = ctx.Set<User>().Take(50).ToList();
        foreach (var user in efUsers)
        {
            user.Age++;
        }
        return ctx.SaveChanges();
    }

    [Benchmark(Description = "Load + Modify (AutoDetectChanges=false)")]
    public int LoadModifySaveNoAutoDetect()
    {
        if (Provider == EfProviderType.LiteDB)
        {
            // LiteDB doesn't have auto-detect - same as normal
            var users = m_liteCollection.FindAll().Take(50).ToList();
            foreach (var user in users)
            {
                user.Age++;
                m_liteCollection.Update(user);
            }
            return users.Count;
        }

        var ctx = GetContext();
        ctx.ChangeTracker.Clear();
        ctx.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            var users = ctx.Set<User>().Take(50).ToList();
            foreach (var user in users)
            {
                user.Age++;
                ctx.Entry(user).State = EntityState.Modified;
            }
            return ctx.SaveChanges();
        }
        finally
        {
            ctx.ChangeTracker.AutoDetectChangesEnabled = true;
        }
    }

    #endregion

    #region Benchmarks - ChangeTracker State

    [Benchmark(Description = "ChangeTracker.HasChanges (no changes)")]
    public bool HasChangesNoChanges()
    {
        if (Provider == EfProviderType.LiteDB)
        {
            // LiteDB doesn't have change tracker
            _ = m_liteCollection.FindAll().Take(100).ToList();
            return false;
        }

        var ctx = GetContext();
        ctx.ChangeTracker.Clear();
        _ = ctx.Set<User>().Take(100).ToList();
        return ctx.ChangeTracker.HasChanges();
    }

    [Benchmark(Description = "ChangeTracker.HasChanges (with changes)")]
    public bool HasChangesWithChanges()
    {
        if (Provider == EfProviderType.LiteDB)
        {
            // LiteDB doesn't have change tracker
            var users = m_liteCollection.FindAll().Take(100).ToList();
            users[0].Name = "Modified";
            return true; // Always "has changes" conceptually
        }

        var ctx = GetContext();
        ctx.ChangeTracker.Clear();
        var efUsers = ctx.Set<User>().Take(100).ToList();
        efUsers[0].Name = "Modified";
        return ctx.ChangeTracker.HasChanges();
    }

    [Benchmark(Description = "ChangeTracker.Entries count")]
    public int EntriesCount()
    {
        if (Provider == EfProviderType.LiteDB)
        {
            // LiteDB doesn't have entries concept - return list count
            return m_liteCollection.FindAll().Take(100).ToList().Count;
        }

        var ctx = GetContext();
        ctx.ChangeTracker.Clear();
        _ = ctx.Set<User>().Take(100).ToList();
        return ctx.ChangeTracker.Entries().Count();
    }

    #endregion

    #region IDisposable

    public void Dispose() => GlobalCleanup();

    #endregion
}

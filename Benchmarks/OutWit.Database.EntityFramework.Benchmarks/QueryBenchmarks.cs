using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using LiteDB;
using Microsoft.EntityFrameworkCore;

namespace OutWit.Database.EntityFramework.Benchmarks;

/// <summary>
/// Benchmarks for EF Core query performance.
/// Tests ToList, Where, Include, AsNoTracking patterns.
/// LiteDB included for managed .NET memory comparison.
/// </summary>
[Config(typeof(EfCoreBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class QueryBenchmarks : IDisposable
{
    #region Fields

    private LiteDatabase? m_liteDb;
    private ILiteCollection<LiteUser>? m_userCollection;
    private ILiteCollection<LiteOrder>? m_orderCollection;
    private ILiteCollection<LiteProduct>? m_productCollection;
    private string m_dbPath = null!;

    #endregion

    #region Parameters

    [Params(100, 500)]
    public int UserCount { get; set; }

    [Params(EfProviderType.WitDb, EfProviderType.SQLite, EfProviderType.LiteDB)]
    public EfProviderType Provider { get; set; }

    #endregion

    #region Setup/Cleanup

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Generate unique path for this parameter combination
        var providerSuffix = Provider.ToString().ToLowerInvariant();
        m_dbPath = BenchmarkPathHelper.GenerateUniquePath($"efquery_{providerSuffix}", 
            Provider == EfProviderType.WitDb ? ".witdb" : ".db");

        CleanupPaths();

        if (Provider == EfProviderType.LiteDB)
        {
            m_liteDb = new LiteDatabase(m_dbPath);
            m_userCollection = m_liteDb.GetCollection<LiteUser>("users");
            m_orderCollection = m_liteDb.GetCollection<LiteOrder>("orders");
            m_productCollection = m_liteDb.GetCollection<LiteProduct>("products");
            SeedLiteDbData(UserCount);
        }
        else
        {
            // For EF providers, use EnsureCreated - it will work correctly
            // because each parameter combination runs in a fresh process
            using var ctx = CreateContext();
            ctx.Database.EnsureCreated();
            SeedDataViaEf(ctx, UserCount);
        }
    }

    private void CleanupPaths()
    {
        BenchmarkPathHelper.SafeCleanup(m_dbPath);
        if (Provider == EfProviderType.WitDb)
            BenchmarkPathHelper.SafeCleanup(m_dbPath + "_indexes");
    }

    private void SeedDataViaEf(DbContext context, int userCount)
    {
        var rnd = new Random(42);
        var baseDate = new DateTime(2024, 1, 1);

        // Products
        var products = new List<Product>();
        for (int i = 0; i < 50; i++)
        {
            products.Add(new Product
            {
                Name = $"Product {i}",
                Price = Math.Round((decimal)(rnd.NextDouble() * 100), 2),
                Stock = rnd.Next(0, 1000),
                Category = new[] { "Electronics", "Clothing", "Food", "Books", "Sports" }[i % 5]
            });
        }
        context.Set<Product>().AddRange(products);
        context.SaveChanges();

        // Users with Orders
        for (int i = 0; i < userCount; i++)
        {
            var user = new User
            {
                Name = $"User {i}",
                Email = $"user{i}@test.com",
                Age = 20 + (i % 50),
                CreatedAt = baseDate.AddDays(i),
                IsActive = i % 3 != 0
            };

            // Each user has 0-3 orders
            int orderCount = i % 4;
            for (int j = 0; j < orderCount; j++)
            {
                var order = new Order
                {
                    Amount = Math.Round((decimal)(rnd.NextDouble() * 500), 2),
                    OrderDate = baseDate.AddDays(i + j),
                    Status = new[] { "pending", "shipped", "delivered" }[j % 3]
                };

                // Each order has 1-3 items
                int itemCount = (j % 3) + 1;
                for (int k = 0; k < itemCount; k++)
                {
                    order.Items.Add(new OrderItem
                    {
                        ProductId = products[rnd.Next(products.Count)].Id,
                        Quantity = rnd.Next(1, 5),
                        UnitPrice = Math.Round((decimal)(rnd.NextDouble() * 50), 2)
                    });
                }

                user.Orders.Add(order);
            }

            context.Set<User>().Add(user);
        }
        context.SaveChanges();

        // Detach all entities to start fresh
        context.ChangeTracker.Clear();
    }

    private void SeedLiteDbData(int userCount)
    {
        var rnd = new Random(42);
        var baseDate = new DateTime(2024, 1, 1);

        // Products
        var products = new List<LiteProduct>();
        for (int i = 0; i < 50; i++)
        {
            products.Add(new LiteProduct
            {
                Id = i + 1,
                Name = $"Product {i}",
                Price = Math.Round((decimal)(rnd.NextDouble() * 100), 2),
                Stock = rnd.Next(0, 1000),
                Category = new[] { "Electronics", "Clothing", "Food", "Books", "Sports" }[i % 5]
            });
        }
        m_productCollection!.InsertBulk(products);
        m_productCollection.EnsureIndex(x => x.Category);

        // Users
        var users = new List<LiteUser>();
        var orders = new List<LiteOrder>();
        int orderId = 1;

        for (int i = 0; i < userCount; i++)
        {
            var user = new LiteUser
            {
                Id = i + 1,
                Name = $"User {i}",
                Email = $"user{i}@test.com",
                Age = 20 + (i % 50),
                CreatedAt = baseDate.AddDays(i),
                IsActive = i % 3 != 0,
                OrderIds = new List<int>()
            };

            // Each user has 0-3 orders
            int orderCount = i % 4;
            for (int j = 0; j < orderCount; j++)
            {
                var order = new LiteOrder
                {
                    Id = orderId++,
                    UserId = user.Id,
                    Amount = Math.Round((decimal)(rnd.NextDouble() * 500), 2),
                    OrderDate = baseDate.AddDays(i + j),
                    Status = new[] { "pending", "shipped", "delivered" }[j % 3]
                };
                user.OrderIds.Add(order.Id);
                orders.Add(order);
            }

            users.Add(user);
        }

        m_userCollection!.InsertBulk(users);
        m_userCollection.EnsureIndex(x => x.Email);
        m_userCollection.EnsureIndex(x => x.Age);
        m_orderCollection!.InsertBulk(orders);
        m_orderCollection.EnsureIndex(x => x.UserId);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        m_liteDb?.Dispose();
        m_liteDb = null;
        CleanupPaths();
    }

    private DbContext CreateContext()
    {
        if (Provider == EfProviderType.WitDb)
            return WitDbBenchmarkContext.Create($"Data Source={m_dbPath}");
        return SqliteBenchmarkContext.Create($"Data Source={m_dbPath}");
    }

    #endregion

    #region Benchmarks - Simple Query

    [Benchmark(Description = "ToList() all users")]
    public int ToListAll()
    {
        if (Provider == EfProviderType.LiteDB)
        {
            return m_userCollection!.FindAll().ToList().Count;
        }

        using var ctx = CreateContext();
        return ctx.Set<User>().ToList().Count;
    }

    [Benchmark(Description = "AsNoTracking().ToList()")]
    public int AsNoTrackingToList()
    {
        if (Provider == EfProviderType.LiteDB)
        {
            return m_userCollection!.FindAll().ToList().Count;
        }

        using var ctx = CreateContext();
        return ctx.Set<User>().AsNoTracking().ToList().Count;
    }

    #endregion

    #region Benchmarks - Filtered Query

    [Benchmark(Description = "Where(Age > 30).ToList()")]
    public int WhereToList()
    {
        if (Provider == EfProviderType.LiteDB)
        {
            return m_userCollection!.Find(x => x.Age > 30).ToList().Count;
        }

        using var ctx = CreateContext();
        return ctx.Set<User>().Where(u => u.Age > 30).ToList().Count;
    }

    [Benchmark(Description = "Where(IsActive).AsNoTracking()")]
    public int WhereAsNoTracking()
    {
        if (Provider == EfProviderType.LiteDB)
        {
            return m_userCollection!.Find(x => x.IsActive).ToList().Count;
        }

        using var ctx = CreateContext();
        return ctx.Set<User>().AsNoTracking().Where(u => u.IsActive).ToList().Count;
    }

    [Benchmark(Description = "FirstOrDefault by Id")]
    public object? FirstOrDefaultById()
    {
        if (Provider == EfProviderType.LiteDB)
        {
            return m_userCollection!.FindById(50);
        }

        using var ctx = CreateContext();
        return ctx.Set<User>().FirstOrDefault(u => u.Id == 50);
    }

    [Benchmark(Description = "Find by PK")]
    public object? FindByPk()
    {
        if (Provider == EfProviderType.LiteDB)
        {
            return m_userCollection!.FindById(50);
        }

        using var ctx = CreateContext();
        return ctx.Set<User>().Find(50L);
    }

    #endregion

    #region Benchmarks - Include Navigation

    [Benchmark(Description = "Include(Orders)")]
    public int IncludeOrders()
    {
        if (Provider == EfProviderType.LiteDB)
        {
            // LiteDB: manual join
            var users = m_userCollection!.FindAll().ToList();
            foreach (var user in users)
            {
                var orders = m_orderCollection!.Find(o => o.UserId == user.Id).ToList();
            }
            return users.Count;
        }

        using var ctx = CreateContext();
        return ctx.Set<User>()
            .Include(u => u.Orders)
            .ToList().Count;
    }

    [Benchmark(Description = "Include with AsNoTracking")]
    public int IncludeAsNoTracking()
    {
        if (Provider == EfProviderType.LiteDB)
        {
            // LiteDB: same as Include
            var users = m_userCollection!.FindAll().ToList();
            foreach (var user in users)
            {
                var orders = m_orderCollection!.Find(o => o.UserId == user.Id).ToList();
            }
            return users.Count;
        }

        using var ctx = CreateContext();
        return ctx.Set<User>()
            .AsNoTracking()
            .Include(u => u.Orders)
            .ToList().Count;
    }

    #endregion

    #region Benchmarks - Projection

    [Benchmark(Description = "Select projection (anonymous)")]
    public int SelectProjection()
    {
        if (Provider == EfProviderType.LiteDB)
        {
            return m_userCollection!.FindAll()
                .Select(u => new { u.Id, u.Name, u.Email })
                .ToList().Count;
        }

        using var ctx = CreateContext();
        return ctx.Set<User>()
            .Select(u => new { u.Id, u.Name, u.Email })
            .ToList().Count;
    }

    [Benchmark(Description = "Select with aggregation")]
    public int SelectWithAggregation()
    {
        if (Provider == EfProviderType.LiteDB)
        {
            var result = m_userCollection!.FindAll()
                .Select(u => new
                {
                    u.Id,
                    u.Name,
                    OrderCount = m_orderCollection!.Count(o => o.UserId == u.Id),
                    TotalAmount = m_orderCollection.Find(o => o.UserId == u.Id).Sum(o => o.Amount)
                })
                .ToList();
            return result.Count;
        }

        using var ctx = CreateContext();
        return ctx.Set<User>()
            .Select(u => new
            {
                u.Id,
                u.Name,
                OrderCount = u.Orders.Count,
                TotalAmount = u.Orders.Sum(o => o.Amount)
            })
            .ToList().Count;
    }

    #endregion

    #region Benchmarks - OrderBy

    [Benchmark(Description = "OrderBy(Name).ToList()")]
    public int OrderByToList()
    {
        if (Provider == EfProviderType.LiteDB)
        {
            return m_userCollection!.FindAll().OrderBy(u => u.Name).ToList().Count;
        }

        using var ctx = CreateContext();
        return ctx.Set<User>().OrderBy(u => u.Name).ToList().Count;
    }

    [Benchmark(Description = "OrderByDescending.Take(10)")]
    public int OrderByTake()
    {
        if (Provider == EfProviderType.LiteDB)
        {
            return m_userCollection!.FindAll().OrderByDescending(u => u.CreatedAt).Take(10).ToList().Count;
        }

        using var ctx = CreateContext();
        return ctx.Set<User>()
            .OrderByDescending(u => u.CreatedAt)
            .Take(10)
            .ToList().Count;
    }

    #endregion

    #region IDisposable

    public void Dispose() => GlobalCleanup();

    #endregion
}

#region LiteDB Document Classes

public class LiteUser
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    public List<int> OrderIds { get; set; } = new();
}

public class LiteOrder
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public decimal Amount { get; set; }
    public DateTime OrderDate { get; set; }
    public string Status { get; set; } = "pending";
}

public class LiteProduct
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string Category { get; set; } = string.Empty;
}

#endregion

using Microsoft.EntityFrameworkCore;
using OutWit.Database.EntityFramework.Extensions;

namespace OutWit.Database.EntityFramework.Benchmarks;

/// <summary>
/// DbContext for WitDb benchmarks.
/// </summary>
public class WitDbBenchmarkContext : DbContext
{
    private readonly string m_connectionString;

    public WitDbBenchmarkContext(string connectionString)
    {
        m_connectionString = connectionString;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Product> Products => Set<Product>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseWitDb(m_connectionString);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureEntities(modelBuilder);
    }

    public static void ConfigureEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.HasIndex(e => e.Email);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.HasOne(e => e.User)
                  .WithMany(u => u.Orders)
                  .HasForeignKey(e => e.UserId);
            entity.HasIndex(e => e.UserId);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Order)
                  .WithMany(o => o.Items)
                  .HasForeignKey(e => e.OrderId);
            entity.HasOne(e => e.Product)
                  .WithMany()
                  .HasForeignKey(e => e.ProductId);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.HasIndex(e => e.Category);
        });
    }
}

/// <summary>
/// DbContext for SQLite benchmarks.
/// </summary>
public class SqliteBenchmarkContext : DbContext
{
    private readonly string m_connectionString;

    public SqliteBenchmarkContext(string connectionString)
    {
        m_connectionString = connectionString;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Product> Products => Set<Product>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite(m_connectionString);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        WitDbBenchmarkContext.ConfigureEntities(modelBuilder);
    }
}

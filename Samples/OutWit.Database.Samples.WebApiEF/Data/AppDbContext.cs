using Microsoft.EntityFrameworkCore;
using OutWit.Database.Samples.WebApiEF.Models;

namespace OutWit.Database.Samples.WebApiEF.Data;

/// <summary>
/// Application database context for WitDatabase with Entity Framework Core.
/// </summary>
public class AppDbContext : DbContext
{
    #region Constructors

    public AppDbContext(DbContextOptions<AppDbContext> options) 
        : base(options)
    {
    }

    #endregion

    #region Model Configuration

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
        });

        // Product configuration
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Price).HasColumnType("DECIMAL(10, 2)");
            entity.HasIndex(e => e.Name);
        });

        // Order configuration
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TotalAmount).HasColumnType("DECIMAL(15, 2)");
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.OrderDate);
        });

        // OrderItem configuration
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UnitPrice).HasColumnType("DECIMAL(10, 2)");
        });
    }

    #endregion

    #region Properties

    public DbSet<User> Users => Set<User>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    #endregion
}

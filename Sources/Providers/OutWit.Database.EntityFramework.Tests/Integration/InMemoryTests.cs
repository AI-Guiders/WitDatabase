using Microsoft.EntityFrameworkCore;
using OutWit.Database.EntityFramework.Extensions;

namespace OutWit.Database.EntityFramework.Tests.Integration;

/// <summary>
/// Tests for in-memory database configuration and change tracking.
/// Note: These tests verify configuration and tracking, not full database execution.
/// </summary>
[TestFixture]
public class InMemoryTests
{
    #region Configuration Tests

    [Test]
    public void InMemoryContextCanBeCreatedTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<InMemoryDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new InMemoryDbContext(optionsBuilder.Options);

        Assert.That(context, Is.Not.Null);
        Assert.That(context.Database, Is.Not.Null);
    }

    [Test]
    public void InMemoryContextHasCorrectProviderTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<InMemoryDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new InMemoryDbContext(optionsBuilder.Options);

        Assert.That(context.Database.ProviderName, Is.EqualTo(WitDatabaseProvider.PROVIDER_NAME));
    }

    [Test]
    public void InMemoryContextCanConnectTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<InMemoryDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new InMemoryDbContext(optionsBuilder.Options);

        Assert.That(context.Database.CanConnect(), Is.True);
    }

    #endregion

    #region Change Tracking Tests

    [Test]
    public void InMemoryAddEntityTracksCorrectlyTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<InMemoryDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new InMemoryDbContext(optionsBuilder.Options);
        
        var entity = new SimpleEntity { Name = "Test", Value = 42 };
        context.Entities.Add(entity);

        var entry = context.Entry(entity);
        Assert.That(entry.State, Is.EqualTo(EntityState.Added));
    }

    [Test]
    public void InMemoryUpdateEntityTracksCorrectlyTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<InMemoryDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new InMemoryDbContext(optionsBuilder.Options);
        
        var entity = new SimpleEntity { Id = 1, Name = "Original", Value = 100 };
        context.Entities.Attach(entity);
        entity.Name = "Updated";

        var entry = context.Entry(entity);
        Assert.That(entry.State, Is.EqualTo(EntityState.Modified));
    }

    [Test]
    public void InMemoryDeleteEntityTracksCorrectlyTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<InMemoryDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new InMemoryDbContext(optionsBuilder.Options);
        
        var entity = new SimpleEntity { Id = 1, Name = "ToDelete", Value = 0 };
        context.Entities.Attach(entity);
        context.Entities.Remove(entity);

        var entry = context.Entry(entity);
        Assert.That(entry.State, Is.EqualTo(EntityState.Deleted));
    }

    [Test]
    public void InMemoryAddRangeTracksAllEntitiesTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<InMemoryDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new InMemoryDbContext(optionsBuilder.Options);
        
        context.Entities.AddRange(
            new SimpleEntity { Name = "A", Value = 10 },
            new SimpleEntity { Name = "B", Value = 20 },
            new SimpleEntity { Name = "C", Value = 30 }
        );

        var entries = context.ChangeTracker.Entries<SimpleEntity>().ToList();
        Assert.That(entries.Count, Is.EqualTo(3));
        Assert.That(entries.All(e => e.State == EntityState.Added), Is.True);
    }

    #endregion

    #region Query Expression Tests

    [Test]
    public void InMemoryWhereQueryCreatesCorrectExpressionTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<InMemoryDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new InMemoryDbContext(optionsBuilder.Options);

        var query = context.Entities.Where(e => e.Value > 15);

        Assert.That(query.Expression.ToString(), Does.Contain("Value"));
    }

    [Test]
    public void InMemoryOrderByQueryCreatesCorrectExpressionTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<InMemoryDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new InMemoryDbContext(optionsBuilder.Options);

        var query = context.Entities.OrderBy(e => e.Name);

        Assert.That(query.Expression.ToString(), Does.Contain("OrderBy"));
    }

    [Test]
    public void InMemorySelectQueryCreatesCorrectExpressionTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<InMemoryDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new InMemoryDbContext(optionsBuilder.Options);

        var query = context.Entities.Select(e => new { e.Name, e.Value });

        Assert.That(query.Expression.ToString(), Does.Contain("Select"));
    }

    #endregion

    #region Model Tests

    [Test]
    public void InMemoryModelHasCorrectEntityTypeTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<InMemoryDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new InMemoryDbContext(optionsBuilder.Options);
        var model = context.Model;

        var entityType = model.FindEntityType(typeof(SimpleEntity));
        Assert.That(entityType, Is.Not.Null);
    }

    [Test]
    public void InMemoryModelHasCorrectPrimaryKeyTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<InMemoryDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new InMemoryDbContext(optionsBuilder.Options);
        var entityType = context.Model.FindEntityType(typeof(SimpleEntity));
        var primaryKey = entityType?.FindPrimaryKey();

        Assert.That(primaryKey, Is.Not.Null);
        Assert.That(primaryKey!.Properties[0].Name, Is.EqualTo("Id"));
    }

    #endregion

    #region Connection Tests

    [Test]
    public void InMemoryOpenConnectionSucceedsTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<InMemoryDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new InMemoryDbContext(optionsBuilder.Options);

        Assert.DoesNotThrow(() => context.Database.OpenConnection());
        Assert.DoesNotThrow(() => context.Database.CloseConnection());
    }

    [Test]
    public async Task InMemoryOpenConnectionAsyncSucceedsTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<InMemoryDbContext>();
        optionsBuilder.UseWitDbInMemory();

        await using var context = new InMemoryDbContext(optionsBuilder.Options);

        Assert.DoesNotThrowAsync(async () => await context.Database.OpenConnectionAsync());
        Assert.DoesNotThrowAsync(async () => await context.Database.CloseConnectionAsync());
    }

    #endregion

    #region Test Models

    public class SimpleEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    public class InMemoryDbContext : DbContext
    {
        public InMemoryDbContext(DbContextOptions<InMemoryDbContext> options) : base(options) { }

        public DbSet<SimpleEntity> Entities => Set<SimpleEntity>();
    }

    #endregion
}

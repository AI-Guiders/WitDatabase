using Microsoft.EntityFrameworkCore;
using OutWit.Database.EntityFramework.Extensions;

namespace OutWit.Database.EntityFramework.Tests.Integration;

/// <summary>
/// End-to-end integration tests for database configuration.
/// Note: Full CRUD with SaveChanges requires additional Update pipeline configuration.
/// These tests verify the EF Core provider configuration is correct.
/// </summary>
[TestFixture]
public class EndToEndTests
{
    #region Fields

    private string m_testDbPath = null!;

    #endregion

    #region Setup/TearDown

    [SetUp]
    public void Setup()
    {
        m_testDbPath = Path.Combine(Path.GetTempPath(), $"WitDbE2E_{Guid.NewGuid():N}.witdb");
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(m_testDbPath))
        {
            try { File.Delete(m_testDbPath); } catch { }
        }
    }

    #endregion

    #region Configuration Tests

    [Test]
    public void FileBasedContextCanBeCreatedTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseWitDb($"Data Source={m_testDbPath}");

        using var context = new TestDbContext(optionsBuilder.Options);

        Assert.That(context, Is.Not.Null);
        Assert.That(context.Database, Is.Not.Null);
    }

    [Test]
    public void InMemoryContextCanBeCreatedTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new TestDbContext(optionsBuilder.Options);

        Assert.That(context, Is.Not.Null);
        Assert.That(context.Database, Is.Not.Null);
    }

    [Test]
    public void ProviderNameIsCorrectTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new TestDbContext(optionsBuilder.Options);

        Assert.That(context.Database.ProviderName, Is.EqualTo(WitDatabaseProvider.PROVIDER_NAME));
    }

    [Test]
    public void ModelCanBeAccessedTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new TestDbContext(optionsBuilder.Options);

        Assert.DoesNotThrow(() => _ = context.Model);
        Assert.That(context.Model, Is.Not.Null);
    }

    [Test]
    public void ModelContainsExpectedEntityTypesTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new TestDbContext(optionsBuilder.Options);
        var model = context.Model;

        Assert.That(model.GetEntityTypes().Any(e => e.ClrType == typeof(Product)), Is.True);
        Assert.That(model.GetEntityTypes().Any(e => e.ClrType == typeof(AllTypesEntity)), Is.True);
        Assert.That(model.GetEntityTypes().Any(e => e.ClrType == typeof(NullableTypesEntity)), Is.True);
    }

    [Test]
    public void EntityTypeHasCorrectTableNameTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new TestDbContext(optionsBuilder.Options);
        var entityType = context.Model.FindEntityType(typeof(Product));

        Assert.That(entityType, Is.Not.Null);
        Assert.That(entityType!.GetTableName(), Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void EntityTypeHasCorrectPrimaryKeyTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new TestDbContext(optionsBuilder.Options);
        var entityType = context.Model.FindEntityType(typeof(Product));
        var primaryKey = entityType?.FindPrimaryKey();

        Assert.That(primaryKey, Is.Not.Null);
        Assert.That(primaryKey!.Properties.Count, Is.EqualTo(1));
        Assert.That(primaryKey.Properties[0].Name, Is.EqualTo("Id"));
    }

    #endregion

    #region DbSet Tests

    [Test]
    public void DbSetIsAccessibleTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new TestDbContext(optionsBuilder.Options);

        Assert.That(context.Products, Is.Not.Null);
        Assert.That(context.AllTypes, Is.Not.Null);
        Assert.That(context.NullableTypes, Is.Not.Null);
    }

    [Test]
    public void DbSetLocalIsAccessibleTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new TestDbContext(optionsBuilder.Options);

        Assert.That(context.Products.Local, Is.Not.Null);
        Assert.That(context.Products.Local.Count, Is.EqualTo(0));
    }

    #endregion

    #region ChangeTracker Tests

    [Test]
    public void AddEntitySetsStateToAddedTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new TestDbContext(optionsBuilder.Options);

        var product = new Product
        {
            Name = "Test Product",
            Price = 99.99m,
            IsActive = true
        };
        context.Products.Add(product);

        var entry = context.Entry(product);
        Assert.That(entry.State, Is.EqualTo(EntityState.Added));
    }

    [Test]
    public void AddRangeAddsMultipleEntitiesTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new TestDbContext(optionsBuilder.Options);

        context.Products.AddRange(
            new Product { Name = "Product 1", Price = 10m, IsActive = true },
            new Product { Name = "Product 2", Price = 20m, IsActive = true },
            new Product { Name = "Product 3", Price = 30m, IsActive = false }
        );

        var entries = context.ChangeTracker.Entries<Product>().ToList();
        Assert.That(entries.Count, Is.EqualTo(3));
        Assert.That(entries.All(e => e.State == EntityState.Added), Is.True);
    }

    [Test]
    public void ModifyAttachedEntitySetsStateToModifiedTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new TestDbContext(optionsBuilder.Options);

        var product = new Product { Id = 1, Name = "Original", Price = 100m, IsActive = true };
        context.Products.Attach(product);
        
        Assert.That(context.Entry(product).State, Is.EqualTo(EntityState.Unchanged));
        
        product.Name = "Updated";
        
        Assert.That(context.Entry(product).State, Is.EqualTo(EntityState.Modified));
    }

    [Test]
    public void RemoveEntitySetsStateToDeletedTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new TestDbContext(optionsBuilder.Options);

        var product = new Product { Id = 1, Name = "ToDelete", Price = 50m, IsActive = true };
        context.Products.Attach(product);
        context.Products.Remove(product);

        Assert.That(context.Entry(product).State, Is.EqualTo(EntityState.Deleted));
    }

    [Test]
    public void ChangeTrackerHasNoChangesInitiallyTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new TestDbContext(optionsBuilder.Options);

        Assert.That(context.ChangeTracker.HasChanges(), Is.False);
    }

    [Test]
    public void ChangeTrackerHasChangesAfterAddTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new TestDbContext(optionsBuilder.Options);

        context.Products.Add(new Product { Name = "Test", Price = 10m, IsActive = true });

        Assert.That(context.ChangeTracker.HasChanges(), Is.True);
    }

    [Test]
    public void ChangeTrackerClearRemovesAllEntriesTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new TestDbContext(optionsBuilder.Options);

        context.Products.Add(new Product { Name = "Test", Price = 10m, IsActive = true });
        Assert.That(context.ChangeTracker.HasChanges(), Is.True);

        context.ChangeTracker.Clear();
        Assert.That(context.ChangeTracker.HasChanges(), Is.False);
    }

    #endregion

    #region Query Expression Tests

    [Test]
    public void WhereQueryCreatesCorrectExpressionTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new TestDbContext(optionsBuilder.Options);

        var query = context.Products.Where(p => p.IsActive);

        Assert.That(query.Expression.ToString(), Does.Contain("IsActive"));
    }

    [Test]
    public void OrderByQueryCreatesCorrectExpressionTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new TestDbContext(optionsBuilder.Options);

        var query = context.Products.OrderBy(p => p.Name);

        Assert.That(query.Expression.ToString(), Does.Contain("OrderBy"));
    }

    [Test]
    public void SkipTakeQueryCreatesCorrectExpressionTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new TestDbContext(optionsBuilder.Options);

        var query = context.Products.Skip(10).Take(20);

        Assert.That(query.Expression.ToString(), Does.Contain("Skip"));
        Assert.That(query.Expression.ToString(), Does.Contain("Take"));
    }

    [Test]
    public void SelectQueryCreatesCorrectExpressionTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new TestDbContext(optionsBuilder.Options);

        var query = context.Products.Select(p => new { p.Name, p.Price });

        Assert.That(query.Expression.ToString(), Does.Contain("Select"));
    }

    #endregion

    #region Database Connection Tests

    [Test]
    public void CanConnectReturnsTrueForInMemoryTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new TestDbContext(optionsBuilder.Options);

        Assert.That(context.Database.CanConnect(), Is.True);
    }

    [Test]
    public void OpenConnectionSucceedsTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new TestDbContext(optionsBuilder.Options);

        Assert.DoesNotThrow(() => context.Database.OpenConnection());
        Assert.DoesNotThrow(() => context.Database.CloseConnection());
    }

    [Test]
    public async Task OpenConnectionAsyncSucceedsTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        await using var context = new TestDbContext(optionsBuilder.Options);

        Assert.DoesNotThrowAsync(async () => await context.Database.OpenConnectionAsync());
        Assert.DoesNotThrowAsync(async () => await context.Database.CloseConnectionAsync());
    }

    #endregion

    #region Type Mapping Tests

    [Test]
    public void AllTypesEntityHasCorrectPropertyMappingsTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new TestDbContext(optionsBuilder.Options);
        var entityType = context.Model.FindEntityType(typeof(AllTypesEntity));

        Assert.That(entityType, Is.Not.Null);

        // Verify key properties exist
        var properties = entityType!.GetProperties().Select(p => p.Name).ToList();
        Assert.That(properties, Does.Contain("Id"));
        Assert.That(properties, Does.Contain("BoolValue"));
        Assert.That(properties, Does.Contain("IntValue"));
        Assert.That(properties, Does.Contain("LongValue"));
        Assert.That(properties, Does.Contain("DecimalValue"));
        Assert.That(properties, Does.Contain("StringValue"));
        Assert.That(properties, Does.Contain("DateTimeValue"));
        Assert.That(properties, Does.Contain("GuidValue"));
    }

    [Test]
    public void NullableTypesEntityHasCorrectPropertyMappingsTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new TestDbContext(optionsBuilder.Options);
        var entityType = context.Model.FindEntityType(typeof(NullableTypesEntity));

        Assert.That(entityType, Is.Not.Null);

        var nullableIntProp = entityType!.FindProperty("NullableInt");
        var nullableDecimalProp = entityType.FindProperty("NullableDecimal");

        Assert.That(nullableIntProp, Is.Not.Null);
        Assert.That(nullableIntProp!.IsNullable, Is.True);
        Assert.That(nullableDecimalProp, Is.Not.Null);
        Assert.That(nullableDecimalProp!.IsNullable, Is.True);
    }

    #endregion

    #region Test Models

    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public bool IsActive { get; set; }
        public string? Category { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AllTypesEntity
    {
        public int Id { get; set; }
        public bool BoolValue { get; set; }
        public byte ByteValue { get; set; }
        public short ShortValue { get; set; }
        public int IntValue { get; set; }
        public long LongValue { get; set; }
        public float FloatValue { get; set; }
        public double DoubleValue { get; set; }
        public decimal DecimalValue { get; set; }
        public string StringValue { get; set; } = string.Empty;
        public DateTime DateTimeValue { get; set; }
        public DateOnly DateOnlyValue { get; set; }
        public TimeOnly TimeOnlyValue { get; set; }
        public Guid GuidValue { get; set; }
        public byte[] ByteArrayValue { get; set; } = Array.Empty<byte>();
    }

    public class NullableTypesEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int? NullableInt { get; set; }
        public decimal? NullableDecimal { get; set; }
        public DateTime? NullableDateTime { get; set; }
        public Guid? NullableGuid { get; set; }
    }

    public class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

        public DbSet<Product> Products => Set<Product>();
        public DbSet<AllTypesEntity> AllTypes => Set<AllTypesEntity>();
        public DbSet<NullableTypesEntity> NullableTypes => Set<NullableTypesEntity>();
    }

    #endregion
}

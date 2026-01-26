using Microsoft.EntityFrameworkCore;
using OutWit.Database.EntityFramework.Extensions;

namespace OutWit.Database.EntityFramework.Tests.Integration;

/// <summary>
/// Integration tests for basic DbContext operations with WitDatabase.
/// </summary>
[TestFixture]
public class BasicDbContextTests
{
    #region Fields

    private string? m_testDbPath;

    #endregion

    #region Setup/TearDown

    [SetUp]
    public void Setup()
    {
        m_testDbPath = Path.Combine(Path.GetTempPath(), $"WitDbEf_{Guid.NewGuid():N}.witdb");
    }

    [TearDown]
    public void TearDown()
    {
        if (m_testDbPath != null && File.Exists(m_testDbPath))
        {
            try { File.Delete(m_testDbPath); } catch { }
        }
    }

    #endregion

    #region DbContext Creation Tests

    [Test]
    public void CreateDbContextWithConnectionStringSucceedsTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseWitDb($"Data Source={m_testDbPath}");

        using var context = new TestDbContext(optionsBuilder.Options);

        Assert.That(context, Is.Not.Null);
        Assert.That(context.Database, Is.Not.Null);
    }

    [Test]
    public void CreateDbContextInMemorySucceedsTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new TestDbContext(optionsBuilder.Options);

        Assert.That(context, Is.Not.Null);
        Assert.That(context.Database, Is.Not.Null);
    }

    [Test]
    public void DatabaseProviderNameIsCorrectTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new TestDbContext(optionsBuilder.Options);

        Assert.That(context.Database.ProviderName, Is.EqualTo(WitDatabaseProvider.PROVIDER_NAME));
    }

    #endregion

    #region Model Tests

    [Test]
    public void DbContextModelContainsEntityTypesTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new TestDbContext(optionsBuilder.Options);
        var model = context.Model;

        Assert.That(model.GetEntityTypes().Any(e => e.ClrType == typeof(TestEntity)), Is.True);
    }

    [Test]
    public void DbContextModelHasTableNameTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new TestDbContext(optionsBuilder.Options);
        var entityType = context.Model.FindEntityType(typeof(TestEntity));

        Assert.That(entityType, Is.Not.Null);
        Assert.That(entityType!.GetTableName(), Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void DbContextModelHasCorrectPrimaryKeyTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new TestDbContext(optionsBuilder.Options);
        var entityType = context.Model.FindEntityType(typeof(TestEntity));
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

        Assert.That(context.TestEntities, Is.Not.Null);
    }

    [Test]
    public void DbSetLocalIsAccessibleTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new TestDbContext(optionsBuilder.Options);

        Assert.That(context.TestEntities.Local, Is.Not.Null);
        Assert.That(context.TestEntities.Local.Count, Is.EqualTo(0));
    }

    #endregion

    #region ChangeTracker Tests

    [Test]
    public void ChangeTrackerTracksAddedEntitiesTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new TestDbContext(optionsBuilder.Options);
        
        var entity = new TestEntity { Name = "Test", CreatedAt = DateTime.UtcNow, IsActive = true };
        context.TestEntities.Add(entity);

        var entry = context.Entry(entity);
        Assert.That(entry.State, Is.EqualTo(EntityState.Added));
    }

    [Test]
    public void ChangeTrackerHasChangesAfterAddTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new TestDbContext(optionsBuilder.Options);
        
        Assert.That(context.ChangeTracker.HasChanges(), Is.False);
        
        context.TestEntities.Add(new TestEntity { Name = "Test" });

        Assert.That(context.ChangeTracker.HasChanges(), Is.True);
    }

    #endregion

    #region Test Models

    public class TestEntity
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
    }

    public class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

        public DbSet<TestEntity> TestEntities => Set<TestEntity>();
    }

    #endregion
}

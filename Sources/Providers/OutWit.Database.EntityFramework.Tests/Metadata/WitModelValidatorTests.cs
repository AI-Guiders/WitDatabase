using Microsoft.EntityFrameworkCore;
using OutWit.Database.EntityFramework.Extensions;

namespace OutWit.Database.EntityFramework.Tests.Metadata;

/// <summary>
/// Unit tests for WitModelValidator.
/// </summary>
[TestFixture]
public class WitModelValidatorTests
{
    #region Schema Validation Tests

    [Test]
    public void ModelWithSchemaThrowsExceptionTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<SchemaTestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new SchemaTestDbContext(optionsBuilder.Options);

        Assert.Throws<InvalidOperationException>(() => _ = context.Model);
    }

    [Test]
    public void ModelWithoutSchemaSucceedsTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<NoSchemaTestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new NoSchemaTestDbContext(optionsBuilder.Options);

        Assert.DoesNotThrow(() => _ = context.Model);
    }

    #endregion

    #region Key Type Validation Tests

    [Test]
    public void ModelWithIntKeySucceedsTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<IntKeyTestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new IntKeyTestDbContext(optionsBuilder.Options);

        Assert.DoesNotThrow(() => _ = context.Model);
    }

    [Test]
    public void ModelWithGuidKeySucceedsTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<GuidKeyTestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new GuidKeyTestDbContext(optionsBuilder.Options);

        Assert.DoesNotThrow(() => _ = context.Model);
    }

    [Test]
    public void ModelWithStringKeySucceedsTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<StringKeyTestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new StringKeyTestDbContext(optionsBuilder.Options);

        Assert.DoesNotThrow(() => _ = context.Model);
    }

    [Test]
    public void ModelWithCompositeKeySucceedsTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<CompositeKeyTestDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new CompositeKeyTestDbContext(optionsBuilder.Options);

        Assert.DoesNotThrow(() => _ = context.Model);
    }

    #endregion

    #region Test Models

    public class EntityWithSchema
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    public class SchemaTestDbContext : DbContext
    {
        public SchemaTestDbContext(DbContextOptions<SchemaTestDbContext> options) : base(options) { }

        public DbSet<EntityWithSchema> Entities => Set<EntityWithSchema>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EntityWithSchema>().ToTable("Entities", "dbo");
        }
    }

    public class EntityWithoutSchema
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    public class NoSchemaTestDbContext : DbContext
    {
        public NoSchemaTestDbContext(DbContextOptions<NoSchemaTestDbContext> options) : base(options) { }

        public DbSet<EntityWithoutSchema> Entities => Set<EntityWithoutSchema>();
    }

    public class EntityWithIntKey
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    public class IntKeyTestDbContext : DbContext
    {
        public IntKeyTestDbContext(DbContextOptions<IntKeyTestDbContext> options) : base(options) { }

        public DbSet<EntityWithIntKey> Entities => Set<EntityWithIntKey>();
    }

    public class EntityWithGuidKey
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
    }

    public class GuidKeyTestDbContext : DbContext
    {
        public GuidKeyTestDbContext(DbContextOptions<GuidKeyTestDbContext> options) : base(options) { }

        public DbSet<EntityWithGuidKey> Entities => Set<EntityWithGuidKey>();
    }

    public class EntityWithStringKey
    {
        public string Id { get; set; } = string.Empty;
        public string? Name { get; set; }
    }

    public class StringKeyTestDbContext : DbContext
    {
        public StringKeyTestDbContext(DbContextOptions<StringKeyTestDbContext> options) : base(options) { }

        public DbSet<EntityWithStringKey> Entities => Set<EntityWithStringKey>();
    }

    public class EntityWithCompositeKey
    {
        public int Key1 { get; set; }
        public string Key2 { get; set; } = string.Empty;
        public string? Name { get; set; }
    }

    public class CompositeKeyTestDbContext : DbContext
    {
        public CompositeKeyTestDbContext(DbContextOptions<CompositeKeyTestDbContext> options) : base(options) { }

        public DbSet<EntityWithCompositeKey> Entities => Set<EntityWithCompositeKey>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EntityWithCompositeKey>().HasKey(e => new { e.Key1, e.Key2 });
        }
    }

    #endregion
}

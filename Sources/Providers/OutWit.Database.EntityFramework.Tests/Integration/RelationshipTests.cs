using Microsoft.EntityFrameworkCore;
using OutWit.Database.EntityFramework.Extensions;

namespace OutWit.Database.EntityFramework.Tests.Integration;

/// <summary>
/// Tests for relationship configuration and navigation properties.
/// Note: These tests verify the model configuration, not full database execution.
/// </summary>
[TestFixture]
public class RelationshipTests
{
    #region One-to-Many Configuration Tests

    [Test]
    public void OneToManyRelationshipIsConfiguredCorrectlyTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<RelationshipDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new RelationshipDbContext(optionsBuilder.Options);
        var productType = context.Model.FindEntityType(typeof(ProductWithCategory));

        Assert.That(productType, Is.Not.Null);
        
        var foreignKey = productType!.GetForeignKeys().FirstOrDefault();
        Assert.That(foreignKey, Is.Not.Null);
        Assert.That(foreignKey!.PrincipalEntityType.ClrType, Is.EqualTo(typeof(Category)));
    }

    [Test]
    public void ParentEntityHasNavigationCollectionTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<RelationshipDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new RelationshipDbContext(optionsBuilder.Options);
        var categoryType = context.Model.FindEntityType(typeof(Category));

        Assert.That(categoryType, Is.Not.Null);
        
        var navigation = categoryType!.FindNavigation("Products");
        Assert.That(navigation, Is.Not.Null);
        Assert.That(navigation!.IsCollection, Is.True);
    }

    [Test]
    public void ChildEntityHasNavigationToParentTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<RelationshipDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new RelationshipDbContext(optionsBuilder.Options);
        var productType = context.Model.FindEntityType(typeof(ProductWithCategory));

        Assert.That(productType, Is.Not.Null);
        
        var navigation = productType!.FindNavigation("Category");
        Assert.That(navigation, Is.Not.Null);
        Assert.That(navigation!.IsCollection, Is.False);
    }

    [Test]
    public void ForeignKeyPropertyIsConfiguredTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<RelationshipDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new RelationshipDbContext(optionsBuilder.Options);
        var productType = context.Model.FindEntityType(typeof(ProductWithCategory));

        Assert.That(productType, Is.Not.Null);
        
        var fkProperty = productType!.FindProperty("CategoryId");
        Assert.That(fkProperty, Is.Not.Null);
        Assert.That(fkProperty!.IsForeignKey(), Is.True);
    }

    #endregion

    #region Self-Referencing Tests

    [Test]
    public void SelfReferencingRelationshipIsConfiguredCorrectlyTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<RelationshipDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new RelationshipDbContext(optionsBuilder.Options);
        var itemType = context.Model.FindEntityType(typeof(HierarchicalItem));

        Assert.That(itemType, Is.Not.Null);
        
        var parentNav = itemType!.FindNavigation("Parent");
        var childrenNav = itemType.FindNavigation("Children");
        
        Assert.That(parentNav, Is.Not.Null);
        Assert.That(childrenNav, Is.Not.Null);
        Assert.That(childrenNav!.IsCollection, Is.True);
    }

    [Test]
    public void NullableForeignKeyIsConfiguredTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<RelationshipDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new RelationshipDbContext(optionsBuilder.Options);
        var itemType = context.Model.FindEntityType(typeof(HierarchicalItem));

        Assert.That(itemType, Is.Not.Null);
        
        var parentIdProp = itemType!.FindProperty("ParentId");
        Assert.That(parentIdProp, Is.Not.Null);
        Assert.That(parentIdProp!.IsNullable, Is.True);
    }

    #endregion

    #region Change Tracker with Relationships Tests

    [Test]
    public void AddingParentWithChildrenTracksAllTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<RelationshipDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new RelationshipDbContext(optionsBuilder.Options);
        
        var category = new Category
        {
            Name = "Electronics",
            Products = new List<ProductWithCategory>
            {
                new() { Name = "Phone", Price = 999.99m },
                new() { Name = "Laptop", Price = 1499.99m }
            }
        };

        context.Categories.Add(category);

        var categoryEntry = context.Entry(category);
        Assert.That(categoryEntry.State, Is.EqualTo(EntityState.Added));

        var productEntries = context.ChangeTracker.Entries<ProductWithCategory>().ToList();
        Assert.That(productEntries.Count, Is.EqualTo(2));
        Assert.That(productEntries.All(e => e.State == EntityState.Added), Is.True);
    }

    [Test]
    public void AddingChildWithParentReferenceTracksParentTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<RelationshipDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new RelationshipDbContext(optionsBuilder.Options);
        
        var category = new Category { Name = "Clothing" };
        var product = new ProductWithCategory
        {
            Name = "Shirt",
            Price = 29.99m,
            Category = category
        };

        context.ProductsWithCategory.Add(product);

        var categoryEntry = context.Entry(category);
        Assert.That(categoryEntry.State, Is.EqualTo(EntityState.Added));
    }

    #endregion

    #region Include Expression Tests

    [Test]
    public void IncludeCreatesCorrectExpressionTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<RelationshipDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new RelationshipDbContext(optionsBuilder.Options);

        var query = context.Categories.Include(c => c.Products);

        Assert.That(query.Expression.ToString(), Does.Contain("Include"));
    }

    [Test]
    public void ThenIncludeCreatesCorrectExpressionTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<RelationshipDbContext>();
        optionsBuilder.UseWitDbInMemory();

        using var context = new RelationshipDbContext(optionsBuilder.Options);

        var query = context.ProductsWithCategory
            .Include(p => p.Category);

        Assert.That(query.Expression.ToString(), Does.Contain("Include"));
    }

    #endregion

    #region Test Models

    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<ProductWithCategory> Products { get; set; } = new();
    }

    public class ProductWithCategory
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int CategoryId { get; set; }
        public Category? Category { get; set; }
    }

    public class HierarchicalItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int? ParentId { get; set; }
        public HierarchicalItem? Parent { get; set; }
        public List<HierarchicalItem> Children { get; set; } = new();
    }

    public class RelationshipDbContext : DbContext
    {
        public RelationshipDbContext(DbContextOptions<RelationshipDbContext> options) : base(options) { }

        public DbSet<Category> Categories => Set<Category>();
        public DbSet<ProductWithCategory> ProductsWithCategory => Set<ProductWithCategory>();
        public DbSet<HierarchicalItem> HierarchicalItems => Set<HierarchicalItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProductWithCategory>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId);

            modelBuilder.Entity<HierarchicalItem>()
                .HasOne(h => h.Parent)
                .WithMany(h => h.Children)
                .HasForeignKey(h => h.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    #endregion
}

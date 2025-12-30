using Microsoft.EntityFrameworkCore;
using OutWit.Database.EntityFramework.Extensions;

namespace OutWit.Database.EntityFramework.Tests.Bulk;

/// <summary>
/// Tests for WitDbBulkExtensions.
/// </summary>
[TestFixture]
public sealed class WitDbBulkExtensionsTests
{
    #region Fields

    private string m_testDbPath = null!;

    #endregion

    #region Setup/TearDown

    [SetUp]
    public void Setup()
    {
        m_testDbPath = Path.Combine(Path.GetTempPath(), $"WitDbBulk_{Guid.NewGuid():N}.witdb");
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

    #region BulkInsert Tests

    [Test]
    public void BulkInsertInsertsEntitiesTest()
    {
        using var context = CreateContext();
        CreateTable(context);
        
        var users = Enumerable.Range(1, 100)
            .Select(i => new User { Name = $"User{i}", Email = $"user{i}@test.com" })
            .ToList();
        
        int inserted = context.BulkInsert(users);
        
        Assert.That(inserted, Is.EqualTo(100));
        
        var count = context.Users.Count();
        Assert.That(count, Is.EqualTo(100));
    }

    [Test]
    public async Task BulkInsertAsyncInsertsEntitiesTest()
    {
        using var context = CreateContext();
        CreateTable(context);
        
        var users = Enumerable.Range(1, 50)
            .Select(i => new User { Name = $"User{i}", Email = $"user{i}@test.com" })
            .ToList();
        
        int inserted = await context.BulkInsertAsync(users);
        
        Assert.That(inserted, Is.EqualTo(50));
    }

    [Test]
    public void BulkInsertPerformanceTest()
    {
        using var context = CreateContext();
        CreateTable(context);
        
        const int rowCount = 1000;
        var users = Enumerable.Range(1, rowCount)
            .Select(i => new User { Name = $"User{i}", Email = $"user{i}@test.com" })
            .ToList();
        
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int inserted = context.BulkInsert(users);
        sw.Stop();
        
        Assert.That(inserted, Is.EqualTo(rowCount));
        
        TestContext.WriteLine($"BulkInsert {rowCount} rows: {sw.ElapsedMilliseconds}ms");
        
        // Should be reasonably fast
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(5000), 
            $"BulkInsert of {rowCount} rows took {sw.ElapsedMilliseconds}ms");
    }

    #endregion

    #region BulkUpdate Tests

    [Test]
    public void BulkUpdateUpdatesEntitiesTest()
    {
        using var context = CreateContext();
        CreateTable(context);
        
        // Insert some users
        var users = Enumerable.Range(1, 10)
            .Select(i => new User { Name = $"User{i}", Email = $"user{i}@test.com" })
            .ToList();
        
        context.BulkInsert(users);
        
        // Get users with IDs assigned (need to query back)
        var existingUsers = context.Users.ToList();
        
        // Update names
        foreach (var user in existingUsers)
        {
            user.Name = $"Updated_{user.Name}";
        }
        
        int updated = context.BulkUpdate(existingUsers);
        
        Assert.That(updated, Is.EqualTo(10));
        
        // Verify updates
        var updatedUser = context.Users.First();
        Assert.That(updatedUser.Name, Does.StartWith("Updated_"));
    }

    #endregion

    #region BulkDelete Tests

    [Test]
    public void BulkDeleteDeletesEntitiesTest()
    {
        using var context = CreateContext();
        CreateTable(context);
        
        // Insert some users
        var users = Enumerable.Range(1, 10)
            .Select(i => new User { Name = $"User{i}", Email = $"user{i}@test.com" })
            .ToList();
        
        context.BulkInsert(users);
        
        // Get users to delete
        var usersToDelete = context.Users.Take(5).ToList();
        
        int deleted = context.BulkDelete(usersToDelete);
        
        Assert.That(deleted, Is.EqualTo(5));
        
        var remaining = context.Users.Count();
        Assert.That(remaining, Is.EqualTo(5));
    }

    #endregion

    #region BulkInsertOrUpdate Tests

    [Test]
    public void BulkInsertOrUpdateInsertsNewEntitiesTest()
    {
        using var context = CreateContext();
        CreateTable(context);
        
        // Insert using raw SQL to get proper IDs
        var connection = context.Database.GetDbConnection();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO ""Users"" (""Id"", ""Name"", ""Email"") VALUES (1, 'User1', 'user1@test.com');
                INSERT INTO ""Users"" (""Id"", ""Name"", ""Email"") VALUES (2, 'User2', 'user2@test.com');
            ";
            cmd.ExecuteNonQuery();
        }
        
        // Prepare entities with explicit IDs - some existing, some new
        var users = new []
        {
            new User { Id = 1, Name = "Updated_User1", Email = "updated1@test.com" }, // update
            new User { Id = 2, Name = "Updated_User2", Email = "updated2@test.com" }, // update
            new User { Id = 100, Name = "NewUser100", Email = "new100@test.com" },    // insert
        };
        
        int affected = context.BulkInsertOrUpdate(users);
        
        Assert.That(affected, Is.EqualTo(3));
        
        var count = context.Users.Count();
        Assert.That(count, Is.EqualTo(3));
        
        // Verify updates
        var user1 = context.Users.First(u => u.Id == 1);
        Assert.That(user1.Name, Is.EqualTo("Updated_User1"));
    }

    [Test]
    public void BulkInsertOrUpdateUpdatesExistingEntitiesTest()
    {
        using var context = CreateContext();
        CreateTable(context);
        
        // Insert initial users using raw SQL
        var connection = context.Database.GetDbConnection();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO ""Users"" (""Id"", ""Name"", ""Email"") VALUES (1, 'User1', 'user1@test.com');
                INSERT INTO ""Users"" (""Id"", ""Name"", ""Email"") VALUES (2, 'User2', 'user2@test.com');
                INSERT INTO ""Users"" (""Id"", ""Name"", ""Email"") VALUES (3, 'User3', 'user3@test.com');
            ";
            cmd.ExecuteNonQuery();
        }
        
        // Get existing users with proper IDs
        var existingUsers = context.Users.ToList();
        Assert.That(existingUsers.Count, Is.EqualTo(3));
        
        // Modify existing users
        foreach (var user in existingUsers)
        {
            user.Name = $"Updated_{user.Name}";
        }
        
        // Update existing users via BulkInsertOrUpdate
        int affected = context.BulkInsertOrUpdate(existingUsers);
        
        Assert.That(affected, Is.EqualTo(3));
        
        // Verify - count should still be 3 (no new inserts)
        var total = context.Users.Count();
        Assert.That(total, Is.EqualTo(3));
        
        // Verify all names are updated
        var allUpdated = context.Users.All(u => u.Name.StartsWith("Updated_"));
        Assert.That(allUpdated, Is.True);
    }

    #endregion

    #region Edge Cases

    [Test]
    public void BulkInsertWithEmptyCollectionReturnsZeroTest()
    {
        using var context = CreateContext();
        CreateTable(context);
        
        int inserted = context.BulkInsert(Array.Empty<User>());
        
        Assert.That(inserted, Is.EqualTo(0));
    }

    [Test]
    public void BulkInsertThrowsWhenConnectionClosedTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<BulkTestContext>();
        optionsBuilder.UseWitDb($"Data Source={m_testDbPath}");

        using var context = new BulkTestContext(optionsBuilder.Options);
        // Don't open the connection
        
        var users = new[] { new User { Name = "Test" } };
        
        Assert.Throws<InvalidOperationException>(() => context.BulkInsert(users));
    }

    #endregion

    #region Performance Comparison Tests

    [Test]
    public void BulkInsertVsSaveChangesPerformanceTest()
    {
        const int rowCount = 500;
        
        // Test BulkInsert
        using (var context = CreateContext())
        {
            CreateTable(context);
            
            var users = Enumerable.Range(1, rowCount)
                .Select(i => new User { Name = $"User{i}", Email = $"user{i}@test.com" })
                .ToList();
            
            var sw = System.Diagnostics.Stopwatch.StartNew();
            context.BulkInsert(users);
            var bulkTime = sw.ElapsedMilliseconds;
            
            TestContext.WriteLine($"BulkInsert {rowCount} rows: {bulkTime}ms");
        }
        
        // Reset DB for SaveChanges test
        if (File.Exists(m_testDbPath))
            File.Delete(m_testDbPath);
        
        // Test SaveChanges
        using (var context = CreateContext())
        {
            CreateTable(context);
            
            var users = Enumerable.Range(1, rowCount)
                .Select(i => new User { Name = $"User{i}", Email = $"user{i}@test.com" })
                .ToList();
            
            context.Users.AddRange(users);
            
            var sw = System.Diagnostics.Stopwatch.StartNew();
            context.SaveChanges();
            var saveChangesTime = sw.ElapsedMilliseconds;
            
            TestContext.WriteLine($"SaveChanges {rowCount} rows: {saveChangesTime}ms");
        }
    }

    #endregion

    #region Helpers

    private BulkTestContext CreateContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<BulkTestContext>();
        optionsBuilder.UseWitDb($"Data Source={m_testDbPath}");

        var context = new BulkTestContext(optionsBuilder.Options);
        context.Database.OpenConnection();
        return context;
    }

    private void CreateTable(BulkTestContext context)
    {
        var connection = context.Database.GetDbConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS ""Users"" (
                ""Id"" BIGINT PRIMARY KEY AUTOINCREMENT,
                ""Name"" VARCHAR(100) NOT NULL,
                ""Email"" VARCHAR(255)
            )";
        cmd.ExecuteNonQuery();
    }

    #endregion

    #region Test Models

    public class User
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
    }

    public class BulkTestContext : DbContext
    {
        public BulkTestContext(DbContextOptions<BulkTestContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Email).HasMaxLength(255);
            });
        }
    }

    #endregion
}

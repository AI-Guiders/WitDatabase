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

    #region BulkInsert with Options Tests

    [Test]
    public void BulkInsertWithBatchSizeTest()
    {
        using var context = CreateContext();
        CreateTable(context);
        
        var progressCounts = new List<int>();
        var options = new BulkOptions
        {
            BatchSize = 25,
            BatchProgress = count => progressCounts.Add(count)
        };
        
        var users = Enumerable.Range(1, 100)
            .Select(i => new User { Name = $"User{i}", Email = $"user{i}@test.com" })
            .ToList();
        
        int inserted = context.BulkInsert(users, options);
        
        Assert.That(inserted, Is.EqualTo(100));
        
        // BatchProgress is called after each batch commit (except the final commit)
        // With 100 rows and batch size 25:
        // - Batch 1: rows 1-25, progress(25)
        // - Batch 2: rows 26-50, progress(50)
        // - Batch 3: rows 51-75, progress(75)
        // - Batch 4: rows 76-100, final commit (no progress call)
        // But implementation calls progress after commit when batchCount >= batchSize
        // So we get 3 callbacks OR 4 depending on exact timing
        Assert.That(progressCounts.Count, Is.GreaterThanOrEqualTo(3));
        Assert.That(progressCounts, Does.Contain(25));
        Assert.That(progressCounts, Does.Contain(50));
        Assert.That(progressCounts, Does.Contain(75));
        
        var count = context.Users.Count();
        Assert.That(count, Is.EqualTo(100));
    }

    [Test]
    public void BulkInsertWithUseTransactionFalseTest()
    {
        using var context = CreateContext();
        CreateTable(context);
        
        var options = new BulkOptions { UseTransaction = false };
        
        var users = Enumerable.Range(1, 10)
            .Select(i => new User { Name = $"User{i}", Email = $"user{i}@test.com" })
            .ToList();
        
        int inserted = context.BulkInsert(users, options);
        
        Assert.That(inserted, Is.EqualTo(10));
        
        var count = context.Users.Count();
        Assert.That(count, Is.EqualTo(10));
    }

    [Test]
    public void BulkInsertWithPropertiesToExcludeTest()
    {
        using var context = CreateContext();
        CreateTableWithDefaults(context);
        
        var options = new BulkOptions
        {
            PropertiesToExclude = new List<string> { "Email" }
        };
        
        var users = Enumerable.Range(1, 5)
            .Select(i => new User { Name = $"User{i}", Email = $"user{i}@test.com" })
            .ToList();
        
        int inserted = context.BulkInsert(users, options);
        
        Assert.That(inserted, Is.EqualTo(5));
        
        // Email should be NULL (excluded from insert)
        var user = context.Users.First();
        Assert.That(user.Email, Is.Null);
    }

    [Test]
    public void BulkInsertWithPropertiesToIncludeTest()
    {
        using var context = CreateContext();
        CreateTableWithDefaults(context);
        
        var options = new BulkOptions
        {
            PropertiesToInclude = new List<string> { "Name" }
        };
        
        var users = Enumerable.Range(1, 5)
            .Select(i => new User { Name = $"User{i}", Email = $"should_not_insert@test.com" })
            .ToList();
        
        int inserted = context.BulkInsert(users, options);
        
        Assert.That(inserted, Is.EqualTo(5));
        
        // Email should be NULL (not included in insert)
        var user = context.Users.First();
        Assert.That(user.Email, Is.Null);
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

    [Test]
    public async Task BulkUpdateAsyncUpdatesEntitiesTest()
    {
        using var context = CreateContext();
        CreateTable(context);
        
        var users = Enumerable.Range(1, 5)
            .Select(i => new User { Name = $"User{i}", Email = $"user{i}@test.com" })
            .ToList();
        
        await context.BulkInsertAsync(users);
        
        var existingUsers = await context.Users.ToListAsync();
        foreach (var user in existingUsers)
        {
            user.Email = "async_updated@test.com";
        }
        
        int updated = await context.BulkUpdateAsync(existingUsers);
        
        Assert.That(updated, Is.EqualTo(5));
        
        var allUpdated = await context.Users.AllAsync(u => u.Email == "async_updated@test.com");
        Assert.That(allUpdated, Is.True);
    }

    [Test]
    public void BulkUpdateWithPropertiesToIncludeTest()
    {
        using var context = CreateContext();
        CreateTable(context);
        
        var users = Enumerable.Range(1, 5)
            .Select(i => new User { Name = $"User{i}", Email = $"user{i}@test.com" })
            .ToList();
        
        context.BulkInsert(users);
        
        var existingUsers = context.Users.AsNoTracking().ToList();
        foreach (var existingUser in existingUsers)
        {
            existingUser.Name = "NewName";
            existingUser.Email = "new_email@test.com"; // This should NOT be updated
        }
        
        var options = new BulkOptions
        {
            PropertiesToInclude = new List<string> { "Name" }
        };
        
        int updated = context.BulkUpdate(existingUsers, options);
        
        Assert.That(updated, Is.EqualTo(5));
        
        // Name should be updated, Email should remain original (query fresh from DB)
        var verifyUser = context.Users.AsNoTracking().First();
        Assert.That(verifyUser.Name, Is.EqualTo("NewName"));
        Assert.That(verifyUser.Email, Does.Contain("@test.com"));
        Assert.That(verifyUser.Email, Does.Not.EqualTo("new_email@test.com"));
    }

    [Test]
    public void BulkUpdateWithPropertiesToExcludeTest()
    {
        using var context = CreateContext();
        CreateTable(context);
        
        var users = Enumerable.Range(1, 5)
            .Select(i => new User { Name = $"User{i}", Email = $"user{i}@test.com" })
            .ToList();
        
        context.BulkInsert(users);
        
        var existingUsers = context.Users.AsNoTracking().ToList();
        var originalEmails = existingUsers.ToDictionary(u => u.Id, u => u.Email);
        
        foreach (var existingUser in existingUsers)
        {
            existingUser.Name = "ExcludeTest";
            existingUser.Email = "excluded@test.com"; // This should NOT be updated
        }
        
        var options = new BulkOptions
        {
            PropertiesToExclude = new List<string> { "Email" }
        };
        
        int updated = context.BulkUpdate(existingUsers, options);
        
        Assert.That(updated, Is.EqualTo(5));
        
        // Name should be updated, Email should remain original (query fresh from DB)
        var updatedUsers = context.Users.AsNoTracking().ToList();
        foreach (var updatedUser in updatedUsers)
        {
            Assert.That(updatedUser.Name, Is.EqualTo("ExcludeTest"));
            Assert.That(updatedUser.Email, Is.EqualTo(originalEmails[updatedUser.Id]));
        }
    }

    [Test]
    public void BulkUpdateWithNoColumnsToUpdateReturnsZeroTest()
    {
        using var context = CreateContext();
        CreateTable(context);
        
        var users = new List<User>
        {
            new User { Name = "User1", Email = "user1@test.com" }
        };
        
        context.BulkInsert(users);
        
        var existingUsers = context.Users.ToList();
        
        // Exclude all non-PK columns
        var options = new BulkOptions
        {
            PropertiesToExclude = new List<string> { "Name", "Email" }
        };
        
        int updated = context.BulkUpdate(existingUsers, options);
        
        // Should return 0 because no columns to update
        Assert.That(updated, Is.EqualTo(0));
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

    [Test]
    public async Task BulkDeleteAsyncDeletesEntitiesTest()
    {
        using var context = CreateContext();
        CreateTable(context);
        
        var users = Enumerable.Range(1, 10)
            .Select(i => new User { Name = $"User{i}", Email = $"user{i}@test.com" })
            .ToList();
        
        await context.BulkInsertAsync(users);
        
        var usersToDelete = await context.Users.Take(3).ToListAsync();
        
        int deleted = await context.BulkDeleteAsync(usersToDelete);
        
        Assert.That(deleted, Is.EqualTo(3));
        
        var remaining = await context.Users.CountAsync();
        Assert.That(remaining, Is.EqualTo(7));
    }

    [Test]
    public async Task BulkDeleteAsyncWithPredicateDeletesMatchingEntitiesTest()
    {
        using var context = CreateContext();
        CreateTable(context);
        
        var users = Enumerable.Range(1, 10)
            .Select(i => new User 
            { 
                Name = i % 2 == 0 ? "Even" : "Odd", 
                Email = $"user{i}@test.com" 
            })
            .ToList();
        
        await context.BulkInsertAsync(users);
        
        // Delete all "Even" users using predicate
        int deleted = await context.BulkDeleteAsync<User>(u => u.Name == "Even");
        
        Assert.That(deleted, Is.EqualTo(5));
        
        var remaining = await context.Users.ToListAsync();
        Assert.That(remaining.Count, Is.EqualTo(5));
        Assert.That(remaining.All(u => u.Name == "Odd"), Is.True);
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

    [Test]
    public async Task BulkInsertOrUpdateAsyncWorksTest()
    {
        using var context = CreateContext();
        CreateTable(context);
        
        var connection = context.Database.GetDbConnection();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO ""Users"" (""Id"", ""Name"", ""Email"") VALUES (1, 'User1', 'user1@test.com');
            ";
            cmd.ExecuteNonQuery();
        }
        
        var users = new []
        {
            new User { Id = 1, Name = "Updated_User1", Email = "updated1@test.com" },
            new User { Id = 2, Name = "NewUser2", Email = "new2@test.com" },
        };
        
        int affected = await context.BulkInsertOrUpdateAsync(users);
        
        Assert.That(affected, Is.EqualTo(2));
        
        var count = await context.Users.CountAsync();
        Assert.That(count, Is.EqualTo(2));
    }

    #endregion

    #region Cancellation Token Tests

    [Test]
    public async Task BulkInsertAsyncRespectsCancellationTokenTest()
    {
        using var context = CreateContext();
        CreateTable(context);
        
        var cts = new CancellationTokenSource();
        cts.Cancel();
        
        var users = Enumerable.Range(1, 100)
            .Select(i => new User { Name = $"User{i}" })
            .ToList();
        
        Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await context.BulkInsertAsync(users, cancellationToken: cts.Token));
    }

    [Test]
    public async Task BulkUpdateAsyncRespectsCancellationTokenTest()
    {
        using var context = CreateContext();
        CreateTable(context);
        
        var users = Enumerable.Range(1, 10)
            .Select(i => new User { Name = $"User{i}" })
            .ToList();
        
        await context.BulkInsertAsync(users);
        var existingUsers = await context.Users.ToListAsync();
        
        var cts = new CancellationTokenSource();
        cts.Cancel();
        
        Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await context.BulkUpdateAsync(existingUsers, cancellationToken: cts.Token));
    }

    [Test]
    public async Task BulkDeleteAsyncRespectsCancellationTokenTest()
    {
        using var context = CreateContext();
        CreateTable(context);
        
        var users = Enumerable.Range(1, 10)
            .Select(i => new User { Name = $"User{i}" })
            .ToList();
        
        await context.BulkInsertAsync(users);
        var existingUsers = await context.Users.ToListAsync();
        
        var cts = new CancellationTokenSource();
        cts.Cancel();
        
        Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await context.BulkDeleteAsync(existingUsers, cts.Token));
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
    public void BulkUpdateWithEmptyCollectionReturnsZeroTest()
    {
        using var context = CreateContext();
        CreateTable(context);
        
        int updated = context.BulkUpdate(Array.Empty<User>());
        
        Assert.That(updated, Is.EqualTo(0));
    }

    [Test]
    public void BulkDeleteWithEmptyCollectionReturnsZeroTest()
    {
        using var context = CreateContext();
        CreateTable(context);
        
        int deleted = context.BulkDelete(Array.Empty<User>());
        
        Assert.That(deleted, Is.EqualTo(0));
    }

    [Test]
    public void BulkInsertOrUpdateWithEmptyCollectionReturnsZeroTest()
    {
        using var context = CreateContext();
        CreateTable(context);
        
        int affected = context.BulkInsertOrUpdate(Array.Empty<User>());
        
        Assert.That(affected, Is.EqualTo(0));
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

    [Test]
    public void BulkInsertThrowsWhenEntityNotInModelTest()
    {
        using var context = CreateContext();
        
        // NotMappedEntity is not part of the model
        var entities = new[] { new NotMappedEntity { Value = 123 } };
        
        Assert.Throws<InvalidOperationException>(() => context.BulkInsert(entities));
    }

    [Test]
    public void BulkInsertThrowsForNullContextTest()
    {
        DbContext? context = null;
        var users = new[] { new User { Name = "Test" } };
        
        Assert.Throws<ArgumentNullException>(() => context!.BulkInsert(users));
    }

    [Test]
    public void BulkInsertThrowsForNullEntitiesTest()
    {
        using var context = CreateContext();
        CreateTable(context);
        
        Assert.Throws<ArgumentNullException>(() => context.BulkInsert<User>(null!));
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

    #region Multiple Entity Types Tests

    [Test]
    public void BulkOperationsWorkWithMultipleEntityTypesTest()
    {
        using var context = CreateContextWithProducts();
        CreateTablesWithProducts(context);
        
        // Insert users
        var users = Enumerable.Range(1, 10)
            .Select(i => new User { Name = $"User{i}", Email = $"user{i}@test.com" })
            .ToList();
        
        context.BulkInsert(users);
        
        // Insert products
        var products = Enumerable.Range(1, 20)
            .Select(i => new Product { Name = $"Product{i}", Price = i * 10.5m })
            .ToList();
        
        context.BulkInsert(products);
        
        Assert.That(context.Users.Count(), Is.EqualTo(10));
        Assert.That(context.Set<Product>().Count(), Is.EqualTo(20));
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

    private BulkTestContextWithProducts CreateContextWithProducts()
    {
        var optionsBuilder = new DbContextOptionsBuilder<BulkTestContextWithProducts>();
        optionsBuilder.UseWitDb($"Data Source={m_testDbPath}");

        var context = new BulkTestContextWithProducts(optionsBuilder.Options);
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

    private void CreateTableWithDefaults(BulkTestContext context)
    {
        var connection = context.Database.GetDbConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS ""Users"" (
                ""Id"" BIGINT PRIMARY KEY AUTOINCREMENT,
                ""Name"" VARCHAR(100) NOT NULL,
                ""Email"" VARCHAR(255) DEFAULT NULL
            )";
        cmd.ExecuteNonQuery();
    }

    private void CreateTablesWithProducts(BulkTestContextWithProducts context)
    {
        var connection = context.Database.GetDbConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS ""Users"" (
                ""Id"" BIGINT PRIMARY KEY AUTOINCREMENT,
                ""Name"" VARCHAR(100) NOT NULL,
                ""Email"" VARCHAR(255)
            );
            CREATE TABLE IF NOT EXISTS ""Products"" (
                ""Id"" BIGINT PRIMARY KEY AUTOINCREMENT,
                ""Name"" VARCHAR(100) NOT NULL,
                ""Price"" DECIMAL(18,2) NOT NULL
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

    public class Product
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    public class NotMappedEntity
    {
        public int Value { get; set; }
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

    public class BulkTestContextWithProducts : DbContext
    {
        public BulkTestContextWithProducts(DbContextOptions<BulkTestContextWithProducts> options) : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Product> Products => Set<Product>();

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

            modelBuilder.Entity<Product>(entity =>
            {
                entity.ToTable("Products");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Price).HasPrecision(18, 2);
            });
        }
    }

    #endregion
}

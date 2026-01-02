# WitDatabase - Benchmarks and Samples Plan

**Date:** 2025-02-06  
**Version:** 1.4  
**Last Updated:** 2025-02-11

---

## Part 1: Benchmarks

### 1.1 Existing Benchmarks (OutWit.Database.Core.Tests.Benchmarks)

| Category | Benchmark | Status |
|-----------|----------|--------|
| BTree | Insert (Memory/File) | Done |
| BTree | Search | Done |
| BTree | Range Scan | Done |
| BTree | Overflow values | Done |
| LSM vs BTree | Insert | Done |
| LSM vs BTree | Read | Done |
| LSM vs BTree | Mixed Workload | Done |
| Transactions | TransactionalStore | Done |
| Transactions | Lock Manager | Done |
| Transactions | Concurrent Access | Done |
| Encryption | AES-GCM | Done |
| Encryption | Encrypted Storage | Done |

### 1.2 Production Benchmarks

#### 1.2.1 OutWit.Database.Benchmarks (SQL Engine) - DONE

Main benchmark project for SQL layer performance testing.

```
Benchmarks/
  OutWit.Database.Benchmarks/
    QueryBenchmarks.cs       [x]
    InsertBenchmarks.cs      [x]
    UpdateBenchmarks.cs      [x]
    JoinBenchmarks.cs        [x]
    AggregateBenchmarks.cs   [x]
    IndexBenchmarks.cs       [x]
    TransactionBenchmarks.cs [x]
    BenchmarkConfig.cs       [x]
    Program.cs               [x]
    README.md                [x]
```

**Engine Modes Tested:**
- BTree (single-threaded)
- LSM (single-threaded)  
- BTreeParallelAuto (with parallel writes)
- LsmParallelAuto (with parallel writes)

**Performance Results (InsertBenchmarks):**

| Scenario | Best WitDb Mode | vs SQLite |
|----------|-----------------|-----------|
| 100 rows in transaction | BTree | **3.8x faster** |
| 1000 rows in transaction | BTree | 2.1x slower |
| 5000 rows in transaction | BTree | 5.4x slower |
| INSERT RETURNING (500 ops) | BTree | ~same for 5000 rows |
| Without transaction (100 rows) | LsmParallelAuto | **4-11x faster** |

| Benchmark | Description | Status |
|----------|----------|--------|
| **QueryBenchmarks** | | |
| SimpleSelect | `SELECT * FROM Users` | Done |
| SelectWithWhere | `SELECT * FROM Users WHERE Age > 18` | Done |
| SelectWithOrderBy | `SELECT * FROM Users ORDER BY Name` | Done |
| SelectWithLimit | `SELECT * FROM Users LIMIT 100` | Done |
| SelectProjection | `SELECT Id, Name FROM Users` | Done |
| **InsertBenchmarks** | | |
| SingleInsert | Single row INSERT | Done |
| BulkInsert | Multi-row INSERT (100/1000 rows) | Done |
| InsertReturning | INSERT...RETURNING | Done |
| **UpdateBenchmarks** | | |
| UpdateByPK | UPDATE by primary key | Done |
| UpdateByIndex | UPDATE with indexed WHERE | Done |
| BulkUpdate | UPDATE all rows | Done |
| UpdateReturning | UPDATE...RETURNING | Done |
| **JoinBenchmarks** | | |
| InnerJoin | 2-table INNER JOIN | Done |
| LeftJoin | 2-table LEFT JOIN | Done |
| MultipleJoins | 3+ table JOINs | Done |
| JoinWithGroupBy | JOIN + GROUP BY | Done |
| **AggregateBenchmarks** | | |
| CountAll | COUNT(*) | Done |
| GroupBySimple | GROUP BY single column | Done |
| GroupByMultiple | GROUP BY multiple columns | Done |
| AggregateWithHaving | GROUP BY...HAVING | Done |
| **IndexBenchmarks** | | |
| IndexSeek | Equality lookup on index | Done |
| IndexRangeScan | Range query on index | Done |
| CompositeIndex | Query using composite index | Done |
| **TransactionBenchmarks** | | |
| SingleTransaction | One transaction with N ops | Done |
| MixedWorkload | INSERT + UPDATE + SELECT in tx | Done |
| Savepoint | Transaction with savepoint | Done |

#### 1.2.2 OutWit.Database.AdoNet.Benchmarks - DONE

ADO.NET provider performance benchmarks.

```
Benchmarks/
  OutWit.Database.AdoNet.Benchmarks/
    ConnectionBenchmarks.cs       [x]
    CommandBenchmarks.cs          [x]
    DataReaderBenchmarks.cs       [x]
    PreparedStatementBenchmarks.cs [x]
    BenchmarkConfig.cs            [x]
    Program.cs                    [x]
    README.md                     [x]
```

| Benchmark | Description | Status |
|----------|----------|--------|
| **ConnectionBenchmarks** | | |
| OpenClose | Connection open/close cycle | Done |
| OpenQueryClose | Open + query + close | Done |
| SingleConnection100Queries | Reuse connection | Done |
| **CommandBenchmarks** | | |
| ExecuteNonQuery | INSERT/UPDATE/DELETE | Done |
| ExecuteScalar | Scalar result | Done |
| ExecuteReader | Full result set | Done |
| **DataReaderBenchmarks** | | |
| ReadAllRows | Iterate all rows | Done |
| GetTypedValues | GetInt32, GetString, etc. | Done |
| ReadByColumnName | Access by column name | Done |
| **PreparedStatementBenchmarks** | | |
| ReuseCommand | Prepared vs non-prepared | Done |
| BatchInsert | Batch in transaction | Done |

#### 1.2.3 Comparison Benchmarks (vs SQLite) - Existing

Already implemented in `OutWit.Database.Comparison.Benchmarks`:

| Benchmark | Description | Status |
|----------|----------|--------|
| InsertPerformance | 10K/100K inserts | Done |
| SelectPerformance | Point/Range queries | Done |
| TransactionOverhead | Transaction throughput | Done |
| ConcurrentReads | Parallel readers | Done |
| BTreeVsLsm | Storage engine comparison | Done |

---

## Part 2: Samples

### 2.1 Existing Samples

| Sample | Description | Status |
|--------|----------|--------|
| OutWit.Database.Samples.BlazorWasm | Blazor WASM with IndexedDB | Exists |

### 2.2 Planned Samples

#### 2.2.1 OutWit.Database.Samples.ConsoleApp - DONE

**Simple console application demonstrating WitDatabase basics.**

```
Samples/
  OutWit.Database.Samples.ConsoleApp/
    Program.cs                    [x]
    Examples/
      BasicCrudExample.cs         [x]
      TransactionExample.cs       [x]
      EncryptionExample.cs        [x]
      LsmTreeExample.cs           [x]
      BulkOperationsExample.cs    [x]
    README.md                     [x]
    OutWit.Database.Samples.ConsoleApp.csproj [x]
```

**Features:**
- [x] Create/open database
- [x] CRUD operations
- [x] Transactions with savepoints
- [x] AES-GCM Encryption
- [x] Storage engine selection (BTree vs LSM)
- [x] Bulk operations
- [x] Interactive menu

#### 2.2.2 OutWit.Database.Samples.WebApi - DONE

**ASP.NET Core Web API using ADO.NET provider.**

```
Samples/
  OutWit.Database.Samples.WebApi/
    Program.cs                    [x]
    Controllers/
      UsersController.cs          [x]
      ProductsController.cs       [x]
    Services/
      DatabaseInitializer.cs      [x]
      UserService.cs              [x]
      ProductService.cs           [x]
    Models/
      User.cs                     [x]
      Product.cs                  [x]
    appsettings.json              [x]
    README.md                     [x]
    OutWit.Database.Samples.WebApi.csproj [x]
```

**Features:**
- [x] REST API for CRUD operations
- [x] Direct WitDbConnection usage
- [x] Parameterized queries with WitDbParameter
- [x] Transaction management
- [x] Bulk operations with transactions
- [x] Swagger documentation
- [x] Search, pagination, filtering
- [x] Statistics endpoints

#### 2.2.3 OutWit.Database.Samples.WebApiEF - DONE

**ASP.NET Core Web API using Entity Framework Core.**

```
Samples/
  OutWit.Database.Samples.WebApiEF/
    Program.cs                    [x]
    Data/
      AppDbContext.cs             [x]
    Controllers/
      UsersController.cs          [x]
      OrdersController.cs         [x]
      ProductsController.cs       [x]
    Services/
      UserService.cs              [x]
      OrderService.cs             [x]
    appsettings.json              [x]
    README.md                     [x]
    OutWit.Database.Samples.WebApiEF.csproj [x]
```

**Features:**
- [x] Entity Framework Core integration
- [x] DbContext with relationships (one-to-many)
- [x] LINQ queries with Include
- [x] Pagination support
- [x] CRUD endpoints for Users, Products, Orders
- [x] Statistics endpoints
- [x] Swagger/OpenAPI documentation
- [x] Automatic database seeding

#### 2.2.4 OutWit.Database.Samples.BlazorServer

**Blazor Server application using EF Core.**

```
Samples/
  OutWit.Database.Samples.BlazorServer/
    Program.cs
    Components/
      App.razor
      Pages/
        Index.razor
        Users.razor
        Products.razor
        Reports.razor
    Data/
      AppDbContext.cs
      DbContextFactory.cs
    Models/
      ...
    Services/
      ...
    OutWit.Database.Samples.BlazorServer.csproj
```

**Features:**
- Blazor Server with EF Core
- CRUD with real-time UI updates
- Pagination
- Sorting and filtering
- Transactions in UI
- Dashboard with aggregates

#### 2.2.5 OutWit.Database.Samples.BlazorWasm (update existing)

**Improve existing Blazor WASM sample.**

Add:
- SQL queries demo (parser works in WASM)
- Encryption demo with password
- Performance comparison demo
- Export/Import demo

#### 2.2.6 OutWit.Database.Samples.WorkerService

**Background service for batch processing.**

```
Samples/
  OutWit.Database.Samples.WorkerService/
    Program.cs
    Workers/
      DataImportWorker.cs
      ReportGeneratorWorker.cs
    Services/
      BatchProcessor.cs
    OutWit.Database.Samples.WorkerService.csproj
```

**Features:**
- Background data processing
- Bulk imports from CSV/JSON
- Scheduled report generation
- Transaction batching

#### 2.2.7 OutWit.Database.Samples.Maui (optional)

**.NET MAUI application for mobile/desktop.**

```
Samples/
  OutWit.Database.Samples.Maui/
    MauiProgram.cs
    Views/
      MainPage.xaml
      ...
    ViewModels/
      ...
    OutWit.Database.Samples.Maui.csproj
```

**Features:**
- Cross-platform (iOS, Android, Windows, macOS)
- Local database storage
- Offline-first architecture
- Sync demo (optional)

---

## Part 3: Implementation Priorities

### Phase 1: Core Samples (Week 1-2) - COMPLETE

1. **OutWit.Database.Samples.ConsoleApp** - DONE
   - Basic demonstration of all features
   - Easy to run and understand

2. **OutWit.Database.Samples.WebApiEF** - DONE
   - Main use case scenario
   - Demonstrates EF Core integration
   - Real-world performance testing

### Phase 2: Extended Samples (Week 3-4) - IN PROGRESS

3. **OutWit.Database.Samples.WebApi** - DONE
   - ADO.NET demonstration
   - Direct SQL control
   - Transaction management

4. **OutWit.Database.Samples.BlazorWasm** (update) - P1 (TODO)
   - SQL queries in browser
   - Encryption demo

### Phase 3: Advanced Samples (Week 5-6)

5. **OutWit.Database.Samples.BlazorServer** - P2 (TODO)
   - Server-side Blazor
   - Real-time features

6. **OutWit.Database.Samples.WorkerService** - P2 (TODO)
   - Background processing
   - Batch operations

---

## Part 4: Directory Structure

```
WitDatabase/
  Benchmarks/
    OutWit.Database.Core.Tests.Benchmarks/     # Exists - Core layer
    OutWit.Database.Benchmarks/                # Done - SQL Engine
    OutWit.Database.AdoNet.Benchmarks/         # Done - ADO.NET
    OutWit.Database.Comparison.Benchmarks/     # Exists - vs SQLite

  Samples/
    OutWit.Database.Samples.BlazorWasm/        # Exists (update pending)
    OutWit.Database.Samples.ConsoleApp/        # DONE
    OutWit.Database.Samples.WebApi/            # DONE
    OutWit.Database.Samples.WebApiEF/          # DONE
    OutWit.Database.Samples.BlazorServer/      # TODO
    OutWit.Database.Samples.WorkerService/     # TODO
    OutWit.Database.Samples.Maui/              # TODO (optional)

  Sources/
    ... (unchanged)
```

---

## Part 5: Technical Requirements for Samples

### General Requirements

1. **Target Frameworks:** `net9.0`, `net10.0`
2. **Nullable:** `enable`
3. **Implicit Usings:** `enable`
4. **All samples must:**
   - Compile without warnings
   - Contain README.md with instructions
   - Have comments on key sections
   - Demonstrate best practices

### Dependencies

| Sample | Dependencies |
|--------|-------------|
| ConsoleApp | OutWit.Database |
| WebApi | OutWit.Database.AdoNet |
| WebApiEF | OutWit.Database.EntityFramework |
| BlazorWasm | OutWit.Database.Core.IndexedDb |
| BlazorServer | OutWit.Database.EntityFramework |
| WorkerService | OutWit.Database, OutWit.Database.AdoNet |
| Maui | OutWit.Database |

---

## Part 6: Sample Code Examples

### ConsoleApp - BasicCrudExample.cs

```csharp
using OutWit.Database.Core.Builder;

public static class BasicCrudExample
{
    public static async Task RunAsync()
    {
        Console.WriteLine("=== Basic CRUD Example ===\n");
        
        // Create database
        await using var db = WitDatabase.CreateOrOpen("demo.witdb");
        
        // Create table
        await db.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS Users (
                Id INT PRIMARY KEY AUTOINCREMENT,
                Name VARCHAR(100) NOT NULL,
                Email VARCHAR(255) UNIQUE,
                CreatedAt DATETIME DEFAULT NOW()
            )
            """);
        
        // Insert
        var insertedId = await db.ExecuteScalarAsync<long>("""
            INSERT INTO Users (Name, Email) 
            VALUES (@name, @email)
            RETURNING Id
            """, 
            new { name = "John Doe", email = "john@example.com" });
        
        Console.WriteLine($"Inserted user with Id: {insertedId}");
        
        // Select
        var users = await db.QueryAsync<User>("SELECT * FROM Users");
        foreach (var user in users)
        {
            Console.WriteLine($"  {user.Id}: {user.Name} ({user.Email})");
        }
        
        // Update
        var affected = await db.ExecuteAsync(
            "UPDATE Users SET Name = @name WHERE Id = @id",
            new { name = "Jane Doe", id = insertedId });
        Console.WriteLine($"Updated {affected} row(s)");
        
        // Delete
        affected = await db.ExecuteAsync(
            "DELETE FROM Users WHERE Id = @id",
            new { id = insertedId });
        Console.WriteLine($"Deleted {affected} row(s)");
    }
}

public record User(long Id, string Name, string Email, DateTime CreatedAt);
```

### WebApiEF - AppDbContext.cs

```csharp
using Microsoft.EntityFrameworkCore;
using OutWit.Database.EntityFramework.Extensions;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    
    public DbSet<User> Users => Set<User>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Product> Products => Set<Product>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Version).IsWitRowVersion();
        });
        
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasOne(o => o.User)
                  .WithMany(u => u.Orders)
                  .HasForeignKey(o => o.UserId);
        });
    }
}

// In Program.cs:
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseWitDb(builder.Configuration.GetConnectionString("Default")));
```

---

## Part 7: Benchmark Summary

| Project | Layer | Status | Purpose |
|---------|-------|--------|---------|
| OutWit.Database.Core.Tests.Benchmarks | Core | Done | Low-level storage performance |
| OutWit.Database.Benchmarks | SQL Engine | Done | SQL query performance |
| OutWit.Database.AdoNet.Benchmarks | ADO.NET | Done | Provider performance |
| OutWit.Database.Comparison.Benchmarks | All | Done | vs SQLite comparison |

> **Note:** Entity Framework Core benchmarks were removed due to EF Core's internal model caching making benchmark isolation unreliable. Real-world EF Core performance will be tested through sample applications (WebApiEF, BlazorServer).

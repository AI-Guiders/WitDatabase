# WitDatabase - Benchmarks and Samples Plan

**Date:** 2025-02-06  
**Version:** 1.1  
**Last Updated:** 2025-02-07

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

### 1.2 Planned Benchmarks

#### 1.2.1 OutWit.Database.Benchmarks (SQL Engine) - ? DONE

New project for SQL layer benchmarks.

```
Benchmarks/
  OutWit.Database.Benchmarks/
    QueryBenchmarks.cs       ?
    InsertBenchmarks.cs      ?
    UpdateBenchmarks.cs      ?
    JoinBenchmarks.cs        ?
    AggregateBenchmarks.cs   ?
    IndexBenchmarks.cs       ?
    TransactionBenchmarks.cs ?
    BenchmarkConfig.cs       ?
    Program.cs               ?
    README.md                ?
```

**Engine Modes Tested:**
- BTree (single-threaded)
- LSM (single-threaded)  
- BTreeParallelAuto (with parallel writes)
- LsmParallelAuto (with parallel writes)

**Initial Results (InsertBenchmarks):**

| Scenario | Best WitDb Mode | vs SQLite |
|----------|-----------------|-----------|
| 100 rows in transaction | BTree | **3.8x faster** |
| 1000 rows in transaction | BTree | 2.1x slower |
| 5000 rows in transaction | BTree | 5.4x slower |
| INSERT RETURNING (500 ops) | BTree | ~same for 5000 rows |
| Without transaction (100 rows) | LsmParallelAuto | **4-11x faster** |

| Benchmark | Description | Priority | Status |
|----------|----------|-----------|--------|
| **QueryBenchmarks** | | | |
| SimpleSelect | `SELECT * FROM Users` | P0 | ? Done |
| SelectWithWhere | `SELECT * FROM Users WHERE Age > 18` | P0 | ? Done |
| SelectWithOrderBy | `SELECT * FROM Users ORDER BY Name` | P0 | ? Done |
| SelectWithLimit | `SELECT * FROM Users LIMIT 100` | P0 | ? Done |
| SelectProjection | `SELECT Id, Name FROM Users` | P1 | ? Done |
| **InsertBenchmarks** | | | |
| SingleInsert | Single row INSERT | P0 | ? Done |
| BulkInsert | Multi-row INSERT (100/1000 rows) | P0 | ? Done |
| InsertReturning | INSERT...RETURNING | P1 | ? Done |
| **UpdateBenchmarks** | | | |
| UpdateByPK | UPDATE by primary key | P0 | ? Done |
| UpdateByIndex | UPDATE with indexed WHERE | P0 | ? Done |
| BulkUpdate | UPDATE all rows | P1 | ? Done |
| UpdateReturning | UPDATE...RETURNING | P1 | ? Done |
| **JoinBenchmarks** | | | |
| InnerJoin | 2-table INNER JOIN | P0 | ? Done |
| LeftJoin | 2-table LEFT JOIN | P0 | ? Done |
| MultipleJoins | 3+ table JOINs | P1 | ? Done |
| JoinWithGroupBy | JOIN + GROUP BY | P1 | ? Done |
| **AggregateBenchmarks** | | | |
| CountAll | COUNT(*) | P0 | ? Done |
| GroupBySimple | GROUP BY single column | P0 | ? Done |
| GroupByMultiple | GROUP BY multiple columns | P1 | ? Done |
| AggregateWithHaving | GROUP BY...HAVING | P1 | ? Done |
| **IndexBenchmarks** | | | |
| IndexSeek | Equality lookup on index | P0 | ? Done |
| IndexRangeScan | Range query on index | P0 | ? Done |
| CompositeIndex | Query using composite index | P1 | ? Done |
| **TransactionBenchmarks** | | | |
| SingleTransaction | One transaction with N ops | P0 | ? Done |
| MixedWorkload | INSERT + UPDATE + SELECT in tx | P1 | ? Done |
| Savepoint | Transaction with savepoint | P1 | ? Done |

#### 1.2.2 OutWit.Database.AdoNet.Benchmarks - ? DONE

```
Benchmarks/
  OutWit.Database.AdoNet.Benchmarks/
    ConnectionBenchmarks.cs       ?
    CommandBenchmarks.cs          ?
    DataReaderBenchmarks.cs       ?
    PreparedStatementBenchmarks.cs ?
    BenchmarkConfig.cs            ?
    Program.cs                    ?
    README.md                     ?
```

| Benchmark | Description | Priority | Status |
|----------|----------|-----------|--------|
| **ConnectionBenchmarks** | | | |
| OpenClose | Connection open/close cycle | P0 | ? Done |
| OpenQueryClose | Open + query + close | P0 | ? Done |
| SingleConnection100Queries | Reuse connection | P1 | ? Done |
| **CommandBenchmarks** | | | |
| ExecuteNonQuery | INSERT/UPDATE/DELETE | P0 | ? Done |
| ExecuteScalar | Scalar result | P0 | ? Done |
| ExecuteReader | Full result set | P0 | ? Done |
| **DataReaderBenchmarks** | | | |
| ReadAllRows | Iterate all rows | P0 | ? Done |
| GetTypedValues | GetInt32, GetString, etc. | P1 | ? Done |
| ReadByColumnName | Access by column name | P1 | ? Done |
| **PreparedStatementBenchmarks** | | | |
| ReuseCommand | Prepared vs non-prepared | P1 | ? Done |
| BatchInsert | Batch in transaction | P1 | ? Done |

#### 1.2.3 OutWit.Database.EntityFramework.Benchmarks - ? DONE

```
Benchmarks/
  OutWit.Database.EntityFramework.Benchmarks/
    QueryBenchmarks.cs     ?
    CrudBenchmarks.cs      ?
    TrackingBenchmarks.cs  ?
    Entities.cs            ?
    BenchmarkContexts.cs   ?
    BenchmarkConfig.cs     ?
    Program.cs             ?
    README.md              ?
```

| Benchmark | Description | Priority | Status |
|----------|----------|-----------|--------|
| **QueryBenchmarks** | | | |
| SimpleQuery | `context.Users.ToList()` | P0 | ? Done |
| FilteredQuery | `.Where(u => u.Age > 18)` | P0 | ? Done |
| IncludeNavigation | `.Include(u => u.Orders)` | P0 | ? Done |
| NoTracking | `AsNoTracking()` | P1 | ? Done |
| SelectProjection | `.Select(u => new {...})` | P1 | ? Done |
| **CrudBenchmarks** | | | |
| AddSingle | `Add()` + `SaveChanges()` | P0 | ? Done |
| AddRange | `AddRange()` + `SaveChanges()` | P0 | ? Done |
| UpdateSingle | Update + `SaveChanges()` | P0 | ? Done |
| RemoveSingle | `Remove()` + `SaveChanges()` | P0 | ? Done |
| **TrackingBenchmarks** | | | |
| TrackingVsNoTracking | Compare performance | P1 | ? Done |
| ChangeDetection | DetectChanges overhead | P1 | ? Done |

#### 1.2.4 Comparison Benchmarks (vs SQLite) - Existing

Already implemented in `OutWit.Database.Comparison.Benchmarks`:

| Benchmark | Description | Status |
|----------|----------|--------|
| InsertPerformance | 10K/100K inserts | ? Done |
| SelectPerformance | Point/Range queries | ? Done |
| TransactionOverhead | Transaction throughput | ? Done |
| ConcurrentReads | Parallel readers | ? Done |
| BTreeVsLsm | Storage engine comparison | ? Done |

---

## Part 2: Samples

### 2.1 Existing Samples

| Sample | Description | Status |
|--------|----------|--------|
| OutWit.Database.Samples.BlazorWasm | Blazor WASM with IndexedDB | Exists |

### 2.2 Planned Samples

#### 2.2.1 OutWit.Database.Samples.ConsoleApp

**Simple console application demonstrating WitDatabase basics.**

```
Samples/
  OutWit.Database.Samples.ConsoleApp/
    Program.cs
    Examples/
      BasicCrudExample.cs
      TransactionExample.cs
      EncryptionExample.cs
      LsmTreeExample.cs
      BulkOperationsExample.cs
    OutWit.Database.Samples.ConsoleApp.csproj
```

**Features:**
- Create/open database
- CRUD operations
- Transactions
- Encryption
- Storage engine selection (BTree vs LSM)
- Bulk operations
- Range scans

#### 2.2.2 OutWit.Database.Samples.WebApi

**ASP.NET Core Web API using ADO.NET provider.**

```
Samples/
  OutWit.Database.Samples.WebApi/
    Program.cs
    Controllers/
      UsersController.cs
      ProductsController.cs
    Services/
      UserService.cs
      ProductService.cs
    Models/
      User.cs
      Product.cs
    appsettings.json
    OutWit.Database.Samples.WebApi.csproj
```

**Features:**
- REST API for CRUD operations
- Direct WitDbConnection usage
- Connection pooling
- Swagger documentation
- Health checks
- Schema migration example

#### 2.2.3 OutWit.Database.Samples.WebApiEF

**ASP.NET Core Web API using Entity Framework Core.**

```
Samples/
  OutWit.Database.Samples.WebApiEF/
    Program.cs
    Data/
      AppDbContext.cs
      Migrations/
    Controllers/
      UsersController.cs
      OrdersController.cs
      ProductsController.cs
    Models/
      User.cs
      Order.cs
      OrderItem.cs
      Product.cs
    Services/
      OrderService.cs
    appsettings.json
    OutWit.Database.Samples.WebApiEF.csproj
```

**Features:**
- Entity Framework Core integration
- Code-First migrations
- Relationships (one-to-many, many-to-many)
- LINQ queries
- Optimistic concurrency (RowVersion)
- Computed columns
- JSON columns

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

### Phase 1: Core Samples (Week 1-2)

1. **OutWit.Database.Samples.ConsoleApp** - P0
   - Basic demonstration of all features
   - Easy to run and understand

2. **OutWit.Database.Samples.WebApiEF** - P0
   - Main use case scenario
   - Demonstrates EF Core integration

### Phase 2: Extended Samples (Week 3-4)

3. **OutWit.Database.Samples.WebApi** - P1
   - ADO.NET demonstration
   - Connection pooling

4. **OutWit.Database.Samples.BlazorWasm** (update) - P1
   - SQL queries in browser
   - Encryption demo

### Phase 3: Advanced Samples (Week 5-6)

5. **OutWit.Database.Samples.BlazorServer** - P2
   - Server-side Blazor
   - Real-time features

6. **OutWit.Database.Samples.WorkerService** - P2
   - Background processing
   - Batch operations

### Phase 4: Benchmarks (Parallel)

7. **OutWit.Database.Benchmarks** - P0
8. **OutWit.Database.AdoNet.Benchmarks** - P1
9. **OutWit.Database.EntityFramework.Benchmarks** - P1
10. **OutWit.Database.Comparison.Benchmarks** - P0

---

## Part 4: Directory Structure

```
WitDatabase/
  Benchmarks/
    OutWit.Database.Core.Tests.Benchmarks/     # Exists
    OutWit.Database.Benchmarks/                # NEW - SQL Engine
    OutWit.Database.AdoNet.Benchmarks/         # NEW - ADO.NET
    OutWit.Database.EntityFramework.Benchmarks/# NEW - EF Core
    OutWit.Database.Comparison.Benchmarks/     # NEW - vs SQLite

  Samples/
    OutWit.Database.Samples.BlazorWasm/        # Exists (update)
    OutWit.Database.Samples.ConsoleApp/        # NEW
    OutWit.Database.Samples.WebApi/            # NEW
    OutWit.Database.Samples.WebApiEF/          # NEW
    OutWit.Database.Samples.BlazorServer/      # NEW
    OutWit.Database.Samples.WorkerService/     # NEW
    OutWit.Database.Samples.Maui/              # NEW (optional)

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

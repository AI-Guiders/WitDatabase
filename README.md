# WitDatabase

A high-performance embedded key-value database for .NET with support for multiple storage engines, ACID transactions, and encryption.

[![.NET](https://img.shields.io/badge/.NET-9.0%20%7C%2010.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)

## Features

- **Two Storage Engines**
  - **B-Tree** - Optimized for read-heavy workloads with excellent random access
  - **LSM-Tree** - Optimized for write-heavy workloads with sequential write performance

- **Encryption**
  - AES-256-GCM with hardware acceleration
  - ChaCha20-Poly1305 via BouncyCastle (Blazor WASM compatible)
  - Password-based key derivation (PBKDF2)

- **ACID Transactions**
  - Atomicity, Consistency, Isolation, Durability
  - Write-Ahead Logging (WAL)
  - Crash recovery

- **Concurrency**
  - Reader-writer locking
  - File locking for multi-process safety
  - Async/await support

- **SQL Support**
  - Full SQL parser (WitSQL dialect)
  - ADO.NET provider
  - Entity Framework Core provider
  - Window functions, CTEs, subqueries
  - 60+ built-in functions

- **Fluent API**
  - Easy configuration with builder pattern
  - Extensible via extension methods
  - Simple static factory methods

- **Provider System**
  - Pluggable storage, encryption, cache, and journal providers
  - Auto-detection of settings when reopening databases
  - Easy registration of custom providers

## Packages

| Package | Description |
|---------|-------------|
| [OutWit.Database.Core](Sources/Core/OutWit.Database.Core/) | Core storage engine (B+Tree, LSM-Tree, MVCC) |
| [OutWit.Database.Core.BouncyCastle](Sources/Core/OutWit.Database.Core.BouncyCastle/) | ChaCha20-Poly1305 encryption provider |
| [OutWit.Database.Core.IndexedDb](Sources/Core/OutWit.Database.Core.IndexedDb/) | IndexedDB storage for Blazor WebAssembly |
| [OutWit.Database.Parser](Sources/Engine/OutWit.Database.Parser/) | SQL parser (ANTLR4-based) |
| [OutWit.Database](Sources/Engine/OutWit.Database/) | SQL execution engine |
| [OutWit.Database.AdoNet](Sources/Providers/OutWit.Database.AdoNet/) | ADO.NET provider |
| [OutWit.Database.EntityFramework](Sources/Providers/OutWit.Database.EntityFramework/) | Entity Framework Core provider |

## Installation

```bash
# Core storage engine
dotnet add package OutWit.Database.Core

# SQL engine with ADO.NET
dotnet add package OutWit.Database.AdoNet

# Entity Framework Core
dotnet add package OutWit.Database.EntityFramework

# Optional: BouncyCastle encryption (for Blazor WASM)
dotnet add package OutWit.Database.Core.BouncyCastle

# Optional: IndexedDB storage (for Blazor WASM)
dotnet add package OutWit.Database.Core.IndexedDb
```

## Quick Start

### Key-Value Storage (Core API)

```csharp
using OutWit.Database.Core.Builder;

// Create a new database
using var db = WitDatabase.Create("mydata.db");

// Or with encryption
using var db = WitDatabase.Create("secure.db", "my-password");

// Store and retrieve data
db.Put("user:1"u8, """{"name": "John", "age": 30}"""u8);
var value = db.Get("user:1"u8);
db.Delete("user:1"u8);
```

### SQL with ADO.NET

```csharp
using OutWit.Database.AdoNet;

using var connection = new WitDbConnection("Data Source=mydb.witdb");
connection.Open();

using var cmd = connection.CreateCommand();
cmd.CommandText = "CREATE TABLE Users (Id INT PRIMARY KEY, Name VARCHAR(100))";
cmd.ExecuteNonQuery();

cmd.CommandText = "INSERT INTO Users (Id, Name) VALUES (@id, @name)";
cmd.Parameters.AddWithValue("@id", 1);
cmd.Parameters.AddWithValue("@name", "John Doe");
cmd.ExecuteNonQuery();
```

### Entity Framework Core

```csharp
using Microsoft.EntityFrameworkCore;
using OutWit.Database.EntityFramework.Extensions;

public class AppDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    
    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseWitDb("Data Source=myapp.witdb");
}

// Usage
using var context = new AppDbContext();
context.Database.EnsureCreated();
context.Users.Add(new User { Name = "John" });
context.SaveChanges();
```

### Blazor WebAssembly

```csharp
using OutWit.Database.Core.Builder;
using OutWit.Database.Core.IndexedDb;
using OutWit.Database.Core.BouncyCastle;

// In Blazor component
var db = new WitDatabaseBuilder()
    .WithIndexedDbStorage("MyDatabase", JSRuntime)
    .WithBouncyCastleEncryption("password")  // Works in browser
    .WithBTree()
    .Build();

await ((StorageIndexedDb)db.Store).InitializeAsync();
```

## Configuration

### Storage Engines

| Method | Description |
|--------|-------------|
| `WithFilePath(path)` | Use file-based storage |
| `WithMemoryStorage()` | Use in-memory storage |
| `WithBTree()` | Use B-Tree engine (default) |
| `WithLsmTree()` | Use LSM-Tree engine |

### Encryption

| Method | Description |
|--------|-------------|
| `WithEncryption(password)` | AES-GCM with password |
| `WithBouncyCastleEncryption(password)` | ChaCha20-Poly1305 |

### Transactions

| Method | Description |
|--------|-------------|
| `WithTransactions()` | Enable ACID transactions |
| `WithMvcc()` | Enable MVCC |
| `WithFileLocking()` | Enable file locking |

## Architecture

```
+---------------------------------------------------------------+
|                      WitDatabaseBuilder                       |
|            (Fluent API for database configuration)            |
+---------------------------------------------------------------+
|                    TransactionalStore                         |
|                 (ACID transactions, locking)                  |
+---------------------------------------------------------------+
|      +----------------+    +----------------+                 |
|      |   StoreBTree   |    |    StoreLsm    |                 |
|      |  (B+Tree engine)|    | (LSM-Tree engine)|              |
|      +----------------+    +----------------+                 |
+---------------------------------------------------------------+
|                     ProviderRegistry                          |
|          (Pluggable providers for all components)             |
+---------------------------------------------------------------+
```

## Performance

WitDatabase is optimized for common database operations:

| Operation | vs SQLite | vs LiteDB |
|-----------|-----------|-----------|
| INSERT | 1.5-3x faster | 1.5-2x faster |
| UPDATE by PK | 1.1-10x faster | 2-4x faster |
| DELETE by PK | 20x faster | 1.7x faster |
| SELECT by PK | 22x faster | 10x faster |
| Transactions | 4-20x faster | 1.2-2x faster |

## Requirements

- .NET 9.0 or .NET 10.0
- Windows, Linux, or macOS

## Project Structure

```
WitDatabase/
+-- Sources/
|   +-- Core/
|   |   +-- OutWit.Database.Core/           # Storage engine
|   |   +-- OutWit.Database.Core.BouncyCastle/  # ChaCha20 encryption
|   |   +-- OutWit.Database.Core.IndexedDb/     # Blazor WASM storage
|   +-- Engine/
|   |   +-- OutWit.Database.Parser/         # SQL parser
|   |   +-- OutWit.Database/                # SQL engine
|   +-- Providers/
|       +-- OutWit.Database.AdoNet/         # ADO.NET provider
|       +-- OutWit.Database.EntityFramework/ # EF Core provider
+-- Tools/
|   +-- OutWit.Database.Studio/             # Database management tool
+-- Samples/
+-- Benchmarks/
```

## Documentation

- [ROADMAP.md](ROADMAP.md) - Future plans and version 2.0 features
- [Sources/Core/OutWit.Database.Core/EXTENSIBILITY.md](Sources/Core/OutWit.Database.Core/EXTENSIBILITY.md) - Extension guide

## Running Tests

```bash
# All tests
dotnet test

# Specific project
dotnet test Sources/Core/OutWit.Database.Core.Tests
```

## License

Licensed under the Apache License, Version 2.0. See `LICENSE`.

## Attribution (optional)

If you use WitDatabase in a product, a mention is appreciated (but not required), for example:
"Powered by WitDatabase https://witdatabase.io/".

## Trademark / Project name

"WitDatabase" and the WitDatabase logo are used to identify the official project by Dmitry Ratner.

You may:
- refer to the project name in a factual way (e.g., "built with WitDatabase");
- use the name to indicate compatibility (e.g., "WitDatabase-compatible").

You may not:
- use "WitDatabase" as the name of a fork or a derived product in a way that implies it is the official project;
- use the WitDatabase logo to promote forks or derived products without permission.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## Changelog

See [ROADMAP.md](ROADMAP.md) for version history and planned features.

# OutWit.Database.EntityFramework.Benchmarks

Entity Framework Core benchmarks comparing WitDb provider performance against SQLite and LiteDB.

## Overview

This benchmark project measures EF Core layer performance:

- **Queries** - ToList, Where, Include, AsNoTracking, projections
- **CRUD operations** - Add, Update, Remove, SaveChanges
- **Change tracking** - Tracking vs NoTracking, DetectChanges overhead

## Providers Compared

| Provider | Type | Notes |
|----------|------|-------|
| **WitDb** | Pure managed .NET + EF Core | Target database |
| **SQLite** | Native C + EF Core | Baseline for speed |
| **LiteDB** | Pure managed .NET (NoSQL) | Baseline for managed memory (no EF) |

## Benchmark Categories

| Benchmark Class | Description | Operations Tested |
|-----------------|-------------|-------------------|
| `QueryBenchmarks` | Query performance | ToList, Where, Include, Select, OrderBy |
| `CrudBenchmarks` | CRUD operations | Add, AddRange, Update, Remove, SaveChanges |
| `TrackingBenchmarks` | Change tracking | Tracking vs NoTracking, AutoDetectChanges |

## Entity Model

```
User (1) ??????? (*) Order (1) ??????? (*) OrderItem (*) ??????? (1) Product
```

- **User**: Id, Name, Email, Age, CreatedAt, IsActive
- **Order**: Id, UserId, Amount, OrderDate, Status
- **OrderItem**: Id, OrderId, ProductId, Quantity, UnitPrice
- **Product**: Id, Name, Price, Stock, Category

## Running Benchmarks

### All Benchmarks
```bash
cd Benchmarks/OutWit.Database.EntityFramework.Benchmarks
dotnet run -c Release
```

### Specific Benchmark Class
```bash
dotnet run -c Release -- --filter "*QueryBenchmarks*"
dotnet run -c Release -- --filter "*CrudBenchmarks*"
dotnet run -c Release -- --filter "*TrackingBenchmarks*"
```

### Specific Provider
```bash
dotnet run -c Release -- --filter "*WitDb*"
dotnet run -c Release -- --filter "*LiteDB*"
```

### Quick Run
```bash
dotnet run -c Release -- --job short
```

## Expected Results

Results are saved to:
```
BenchmarkDotNet.Artifacts/results/
```

## Key Metrics

- **Time per operation** - Lower is better
- **Memory allocation** - Compare WitDb vs LiteDB (both managed .NET)
- **GC collections** - Fewer is better

## Why LiteDB?

SQLite is a native C library, so memory allocation comparison isn't fair.
LiteDB is also a pure managed .NET embedded database (NoSQL), making it
a better baseline for comparing managed memory behavior.

Note: LiteDB doesn't use EF Core, so the benchmarks use its native API.

## Dependencies

- BenchmarkDotNet 0.15.8
- Microsoft.EntityFrameworkCore.Sqlite 9.0.6
- LiteDB 5.0.21
- OutWit.Database.EntityFramework

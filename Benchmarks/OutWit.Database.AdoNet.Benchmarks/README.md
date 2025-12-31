# OutWit.Database.AdoNet.Benchmarks

ADO.NET provider benchmarks comparing WitDbConnection/Command/DataReader performance against SQLite and LiteDB.

## Overview

This benchmark project measures the ADO.NET layer performance:

- **Connection lifecycle** - Open/Close overhead
- **Command execution** - ExecuteNonQuery, ExecuteScalar, ExecuteReader
- **DataReader** - Row iteration, typed getters, field access patterns
- **Prepared statements** - Command reuse vs new command

## Providers Compared

| Provider | Type | Notes |
|----------|------|-------|
| **WitDb** | Pure managed .NET | Target database |
| **SQLite** | Native C + .NET bindings | Baseline for speed |
| **LiteDB** | Pure managed .NET (NoSQL) | Baseline for managed memory |

## Benchmark Categories

| Benchmark Class | Description | Operations Tested |
|-----------------|-------------|-------------------|
| `ConnectionBenchmarks` | Connection lifecycle | Open/Close, single/multiple, with query |
| `CommandBenchmarks` | Command execution | Update, Count, FindById, ReadAll |
| `DataReaderBenchmarks` | DataReader performance | Row iteration, typed getters, filtered query |
| `PreparedStatementBenchmarks` | Statement reuse | Prepared vs non-prepared, batch operations |

## Running Benchmarks

### All Benchmarks
```bash
cd Benchmarks/OutWit.Database.AdoNet.Benchmarks
dotnet run -c Release
```

### Specific Benchmark Class
```bash
dotnet run -c Release -- --filter "*ConnectionBenchmarks*"
dotnet run -c Release -- --filter "*CommandBenchmarks*"
dotnet run -c Release -- --filter "*DataReaderBenchmarks*"
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

## Dependencies

- BenchmarkDotNet 0.15.8
- Microsoft.Data.Sqlite 9.0.6
- LiteDB 5.0.21
- OutWit.Database.AdoNet

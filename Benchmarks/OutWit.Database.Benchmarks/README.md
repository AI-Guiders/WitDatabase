# OutWit.Database.Benchmarks

SQL Engine benchmarks for WitDatabase, comparing performance against SQLite and LiteDB across different storage engines and parallel modes.

## Overview

This benchmark project measures the performance of the WitSQL execution engine across:

- **Storage Engines**: BTree (read-optimized) vs LSM-Tree (write-optimized)
- **Parallel Modes**: Single-threaded vs Auto parallel mode
- **Comparison**: WitDb vs SQLite (native) vs LiteDB (managed .NET)

## Providers Compared

| Provider | Type | Notes |
|----------|------|-------|
| **WitDb** | Pure managed .NET | Target database (multiple engine modes) |
| **SQLite** | Native C + .NET bindings | Baseline for speed |
| **LiteDB** | Pure managed .NET (NoSQL) | Baseline for managed memory |

## Benchmark Categories

| Benchmark Class | Description | Operations Tested |
|----------------|-------------|-------------------|
| `QueryBenchmarks` | SELECT query performance | Full scan, WHERE, ORDER BY, LIMIT, Point queries |
| `InsertBenchmarks` | INSERT statement performance | Single/Bulk INSERT, RETURNING |
| `UpdateBenchmarks` | UPDATE statement performance | By PK, By Index, Bulk UPDATE, RETURNING |
| `JoinBenchmarks` | JOIN query performance | 2/3/4 table JOINs, LEFT JOIN, GROUP BY |
| `AggregateBenchmarks` | Aggregation performance | COUNT, SUM, AVG, MIN/MAX, GROUP BY, HAVING |
| `IndexBenchmarks` | Index usage performance | Seek, Range scan, Composite index |
| `TransactionBenchmarks` | Transaction performance | Single TX with N ops, Mixed workload, Savepoints |

## Engine Modes

| Mode | Storage | Parallel | Best For |
|------|---------|----------|----------|
| `BTree` | B+Tree | No | Read-heavy, random access |
| `Lsm` | LSM-Tree | No | Write-heavy, sequential writes |
| `BTreeParallelAuto` | B+Tree | Auto | Mixed workload with reads |
| `LsmParallelAuto` | LSM-Tree | Auto | High write throughput |

## Running Benchmarks

### All Benchmarks
```bash
cd Benchmarks/OutWit.Database.Benchmarks
dotnet run -c Release
```

### Specific Benchmark Class
```bash
dotnet run -c Release -- --filter "*QueryBenchmarks*"
dotnet run -c Release -- --filter "*InsertBenchmarks*"
dotnet run -c Release -- --filter "*JoinBenchmarks*"
```

### Specific Engine Mode
```bash
dotnet run -c Release -- --filter "*BTree*"
```

### Specific Provider
```bash
dotnet run -c Release -- --filter "*LiteDB*"
```

### Quick Run (fewer iterations)
```bash
dotnet run -c Release -- --job short
```

### Export Results
```bash
dotnet run -c Release -- --exporters json csv markdown
```

## Expected Results

Results are saved to:
```
BenchmarkDotNet.Artifacts/results/
```

## Why LiteDB?

SQLite is a native C library, so memory allocation comparison isn't fair.
LiteDB is also a pure managed .NET embedded database (NoSQL), making it
a better baseline for comparing managed memory behavior with WitDb.

## Sample Output

```
| Method                           | TableSize | EngineMode        | Mean      | Ratio |
|--------------------------------- |---------- |------------------ |----------:|------:|
| SELECT * (full scan) - SQLite    | 1000      | BTree             | 1.234 ms  | 1.00  |
| SELECT * (full scan) - WitDb     | 1000      | BTree             | 1.456 ms  | 1.18  |
| FindAll (full scan) - LiteDB     | 1000      | BTree             | 1.678 ms  | 1.36  |
| SELECT * (full scan) - WitDb     | 1000      | BTreeParallelAuto | 1.234 ms  | 1.00  |
```

## Dependencies

- BenchmarkDotNet 0.15.8
- Microsoft.Data.Sqlite 9.0.6
- LiteDB 5.0.21
- OutWit.Database.AdoNet

## Related

- [OutWit.Database.Comparison.Benchmarks](../OutWit.Database.Comparison.Benchmarks) - Core storage benchmarks
- [OutWit.Database.Core.Tests.Benchmarks](../OutWit.Database.Core.Tests.Benchmarks) - B-Tree/LSM benchmarks
- [OutWit.Database.AdoNet.Benchmarks](../OutWit.Database.AdoNet.Benchmarks) - ADO.NET provider benchmarks
- [OutWit.Database.EntityFramework.Benchmarks](../OutWit.Database.EntityFramework.Benchmarks) - EF Core benchmarks

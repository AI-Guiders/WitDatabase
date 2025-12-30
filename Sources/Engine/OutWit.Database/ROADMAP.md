# OutWit.Database (Engine) - v2 Roadmap

**Version:** 3.0  
**Last Updated:** 2025-06-11

---

## v1 Status: 100% Complete

All SQL execution features are implemented.

See [STATUS.md](STATUS.md) for details.

**Test Coverage:** 1395+ tests passing

---

## v2 Planned Features

### Performance Optimization (P0 - Critical)

| Feature | Priority | Status | Description |
|---------|----------|--------|-------------|
| Prepared Statement Cache Integration | P0 | ? Done | Integrate QueryPlanCache with WitSqlEngineStatement |
| Bulk INSERT API | P0 | ? Done | Insert multiple rows in single operation |
| ExecuteBatch API | P0 | ? Done | Execute same statement with multiple parameter sets |
| Statement Reuse | P0 | ? Done | Skip re-parsing for identical SQL |

#### Performance APIs Implemented

**`WitSqlEngineStatement.ExecuteBatch()`** - Execute prepared statement with multiple parameter sets:
```csharp
using var stmt = engine.Prepare("INSERT INTO Users (Name, Email) VALUES (@name, @email)");
var users = new[]
{
    new Dictionary<string, object?> { ["name"] = "Alice", ["email"] = "alice@test.com" },
    new Dictionary<string, object?> { ["name"] = "Bob", ["email"] = "bob@test.com" }
};
int rowsAffected = stmt.ExecuteBatch(users);
```

**`WitSqlEngine.BulkInsert()`** - High-level bulk insert API:
```csharp
// From objects
var users = new[] { new { Name = "Alice", Email = "alice@test.com" } };
engine.BulkInsert("Users", users);

// From dictionaries
var rows = new[] { new Dictionary<string, object?> { ["Name"] = "Alice" } };
engine.BulkInsert("Users", rows);

// From arrays
engine.BulkInsert("Users", new[] { "Name", "Email" }, new[] { new object?[] { "Alice", "alice@test.com" } });
```

**`WitSqlEngine.BulkUpdate()`** and **`BulkDelete()`** - Bulk modification APIs.

#### Performance Targets

| Metric | Current | Target | Notes |
|--------|---------|--------|-------|
| INSERT 10K rows (BTree) | 115 ms | 50-70 ms | 2x improvement with batch/prepared |
| vs LiteDB | 1.5x slower | 1.5x faster | Beat pure .NET competitor |
| vs SQLite | 5x slower | 3x slower | Acceptable for embedded .NET |

### User-Defined Functions

| Feature | Priority | Description |
|---------|----------|-------------|
| `CREATE FUNCTION` execution | P2 | Execute UDF definitions |
| `RETURNS TABLE` support | P2 | Table-valued functions |
| `DETERMINISTIC` handling | P2 | Optimization hints |
| `DROP FUNCTION` execution | P2 | Remove UDFs |

### Stored Procedures

| Feature | Priority | Description |
|---------|----------|-------------|
| `CREATE PROCEDURE` execution | P2 | Execute procedure definitions |
| `DROP PROCEDURE` execution | P2 | Remove procedures |
| `CALL` / `EXECUTE` execution | P2 | Invoke procedures |

### Extended Query Analysis

| Feature | Priority | Description |
|---------|----------|-------------|
| `EXPLAIN ANALYZE` | P2 | Actual execution statistics |
| `EXPLAIN (FORMAT JSON/TEXT)` | P2 | Alternative output formats |

### Database Administration

| Feature | Priority | Description |
|---------|----------|-------------|
| `CREATE DATABASE` | P2 | Create new database files |
| `DROP DATABASE` | P2 | Delete database files |
| `ATTACH DATABASE` | P2 | Attach external databases |
| `DETACH DATABASE` | P2 | Detach attached databases |
| `VACUUM` execution | P2 | Reclaim unused space |
| `ANALYZE` execution | P2 | Update statistics |
| `PRAGMA` support | P2 | Database configuration |

### Advanced Optimization

| Feature | Priority | Description |
|---------|----------|-------------|
| Statistics histograms | P2 | Better cardinality estimation |
| Adaptive query execution | P2 | Runtime plan adjustment |
| Predicate pushdown | P2 | Push filters to storage |

---

## See Also

- [README.md](README.md) - Documentation
- [STATUS.md](STATUS.md) - Implementation status
- [../../WitSQL.md](../../WitSQL.md) - Language specification

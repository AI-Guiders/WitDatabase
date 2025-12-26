# OutWit.Database (Engine) - TODO List v1

**Last Updated:** 2025-01-27  
**Based on:** Code audit + Roadmap.Engine.md

---

## Goals

**Project Goal:** Create a database with full ADO.NET and EF Core provider support.

**Implementation Order:**
1. **Phase 1:** SQL Parser (COMPLETED - 100%)
2. **Phase 2:** SQL Engine (current phase)
3. **Phase 3:** ADO.NET Provider (after Engine completion)
4. **Phase 4:** EF Core Provider (after ADO.NET)

---

## Legend

| Symbol | Status |
|--------|--------|
| [ ] | Not started |
| [~] | In progress / Partial |
| [x] | Complete |

**Priority:**
- **P0** = Critical - blocks other tasks
- **P1** = Required for v1 - needed for ADO.NET/EF Core
- **P2** = Nice-to-have - can be deferred

---

## Summary

| Category | P0 | P1 | P2 | Status |
|----------|----|----|----|----|
| Transaction Support | 4 | 2 | 0 | BLOCKING |
| Index Implementation | 3 | 3 | 0 | BLOCKING |
| ALTER TABLE | 3 | 0 | 1 | MISSING |
| CTE Execution | 2 | 1 | 0 | Required |
| Window Functions | 0 | 6 | 3 | Required |
| DML Enhancements | 0 | 8 | 0 | Required |
| JSON Functions | 0 | 3 | 3 | Required |
| Query Optimization | 0 | 2 | 2 | Optional |
| INFORMATION_SCHEMA | 0 | 6 | 0 | Required |
| Misc/Cleanup | 0 | 2 | 3 | Polish |
| **ADO.NET Provider** | 0 | 9 | 0 | After Engine |
| **EF Core Provider** | 0 | 10+ | 0 | After ADO.NET |

---

# PHASE 2: SQL Engine (Current)

## 1. Transaction Support (P0 - BLOCKING)

**Current State:** Transaction methods exist but have lock recursion issues

### Found in Tests (Ignored):
```csharp
// WitSqlEngineTransactionTests.cs
[Ignore("Transaction support not fully implemented - lock recursion issue")]
public void CommitPersistsChangesTest()
public void RollbackDiscardsChangesTest()
public void DisposeWithoutCommitAutoRollbacksTest()
public void ChangesVisibleWithinTransactionTest()
```

### Tasks:
- [ ] **P0** Fix lock recursion issue in transactions
- [ ] **P0** Transaction isolation for queries (use MVCC from Core)
- [ ] **P0** `BEGIN TRANSACTION` / `COMMIT` / `ROLLBACK` SQL execution
- [ ] **P0** Isolation level support (READ COMMITTED, SERIALIZABLE, etc.)
- [ ] **P1** `SAVEPOINT` / `RELEASE SAVEPOINT` / `ROLLBACK TO SAVEPOINT`
- [ ] **P1** `FOR UPDATE` / `FOR SHARE` locking hints

---

## 2. Index Implementation (P0 - BLOCKING)

**Current State:** Index metadata stored, but NOT USED for queries

### Found in Code:
```csharp
// WitSqlEngine.Query.cs:117
// TODO: Implement proper index seek

// WitSqlEngine.Query.cs:135  
// TODO: Implement proper index range scan
```

### Tasks:
- [ ] **P0** Implement `CreateIndexSeek()` - equality lookup using B+Tree index
- [ ] **P0** Implement `CreateIndexRangeScan()` - range queries using index
- [ ] **P0** Index auto-update on INSERT/UPDATE/DELETE
- [ ] **P1** Partial index evaluation (WHERE clause on index)
- [ ] **P1** Expression index evaluation (functional indexes)
- [ ] **P1** Covering index support (INCLUDE columns)

---

## 3. ALTER TABLE (P0 - MISSING)

**Current State:** Partial implementation - several ALTER actions not executed

### Missing in `StatementExecutor.Ddl.cs`:
```csharp
// ExecuteAlterTable doesn't handle:
case AlterActionAddConstraint addConstraint:  // NOT IMPLEMENTED
case AlterActionDropConstraint dropConstraint: // NOT IMPLEMENTED
```

### Found in Tests (Ignored):
```csharp
// WitSqlEngineDdlTests.cs:99
[Ignore("ALTER TABLE ADD COLUMN with DEFAULT does not yet populate existing rows")]
public void AlterTableAddColumnWithDefaultPopulatesExistingRowsTest()
```

### Tasks:
- [ ] **P0** `ALTER TABLE ADD CONSTRAINT` - needed for EF Core migrations
- [ ] **P0** `ALTER TABLE DROP CONSTRAINT` - needed for EF Core migrations  
- [ ] **P0** `ALTER TABLE ADD COLUMN` - populate existing rows with DEFAULT value
- [ ] **P2** Computed columns support in ALTER TABLE

---

## 4. CTE (WITH clause) Execution (P0/P1)

**Current State:** Parser supports CTE, Engine doesn't execute

### Tasks:
- [ ] **P0** Simple CTE execution (non-recursive) - needed for EF Core
- [ ] **P0** Multiple CTEs in single query
- [ ] **P1** `WITH RECURSIVE` - recursive CTE execution

---

## 5. Window Functions (P1 - Required for EF Core)

**Current State:** Parser supports, Engine doesn't execute

### Tasks:
- [ ] **P1** OVER clause handling infrastructure
- [ ] **P1** PARTITION BY grouping
- [ ] **P1** ORDER BY in window
- [ ] **P1** ROW_NUMBER() - critical for EF Core pagination
- [ ] **P1** RANK() / DENSE_RANK()
- [ ] **P1** LAG() / LEAD()
- [ ] **P2** Frame clause (ROWS/RANGE)
- [ ] **P2** NTILE()
- [ ] **P2** FIRST_VALUE / LAST_VALUE

---

## 6. DML Enhancements (P1)

### Missing Features:
- [ ] **P1** `INSERT ... RETURNING` - critical for EF Core identity
- [ ] **P1** `UPDATE ... RETURNING`
- [ ] **P1** `DELETE ... RETURNING`
- [ ] **P1** `INSERT OR REPLACE`
- [ ] **P1** `INSERT ... ON CONFLICT DO UPDATE` (UPSERT)
- [ ] **P1** `INSERT ... ON CONFLICT DO NOTHING`
- [ ] **P1** `TRUNCATE TABLE`
- [ ] **P1** `MERGE` statement

---

## 7. JSON Functions (P1)

**Current State:** Partial implementation

### Implemented:
- [x] `JSON_EXTRACT(json, path)`
- [x] `JSON_TYPE(json)`
- [x] `JSON_ARRAY_LENGTH(json)`

### Missing (needed for EF Core JSON mapping):
- [ ] **P1** `JSON_VALUE(json, path)` - extract scalar as SQL type
- [ ] **P1** `JSON_QUERY(json, path)` - extract object/array
- [ ] **P1** `JSON_SET(json, path, value)` - modify JSON
- [ ] **P2** `JSON_INSERT` / `JSON_REPLACE` / `JSON_REMOVE`
- [ ] **P2** `JSON_ARRAY()` / `JSON_OBJECT()` - constructors
- [ ] **P2** `JSON_VALID(str)` - validation

---

## 8. INFORMATION_SCHEMA (P1 - Required for EF Core)

**Current State:** Not implemented

EF Core scaffolding requires these views for reverse engineering:

### Tasks:
- [ ] **P1** `INFORMATION_SCHEMA.TABLES`
- [ ] **P1** `INFORMATION_SCHEMA.COLUMNS`
- [ ] **P1** `INFORMATION_SCHEMA.KEY_COLUMN_USAGE`
- [ ] **P1** `INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS`
- [ ] **P1** `INFORMATION_SCHEMA.INDEXES`
- [ ] **P1** `INFORMATION_SCHEMA.VIEWS`

---

## 9. Query Optimization (P1/P2)

**Current State:** No optimization, always full table scan

### Tasks:
- [ ] **P1** Index selection in WHERE clause (cost-based)
- [ ] **P1** Basic predicate pushdown
- [ ] **P2** Join ordering optimization
- [ ] **P2** Query plan caching

---

## 10. Miscellaneous / Cleanup

### Code Cleanup:
- [ ] **P1** Enable ignored transaction tests after fix
- [ ] **P1** Enable ALTER TABLE DEFAULT test after fix
- [ ] **P2** ROWVERSION auto-increment support
- [ ] **P2** Cascading deletes (FK ON DELETE CASCADE)
- [ ] **P2** Query timeout cancellation

---

# PHASE 3: ADO.NET Provider (After Engine)

> Start only after completing all P0/P1 Engine tasks

## ADO.NET Classes to Implement

| Class | Purpose | Priority |
|-------|---------|----------|
| `WitDbConnection` | Connection management | P1 |
| `WitDbCommand` | Command execution | P1 |
| `WitDbDataReader` | Forward-only result reader | P1 |
| `WitDbParameter` | Named/positional parameters | P1 |
| `WitDbParameterCollection` | Parameter collection | P1 |
| `WitDbTransaction` | Transaction wrapper | P1 |
| `WitDbConnectionStringBuilder` | Connection string parsing | P1 |
| `WitDbProviderFactory` | Factory for DI | P1 |
| `WitDbException` | Provider-specific exception | P1 |

### ADO.NET Features:
- [ ] Async methods (`ExecuteReaderAsync`, etc.)
- [ ] Connection pooling (optional for embedded)
- [ ] Multiple result sets
- [ ] Batched commands

---

# PHASE 4: EF Core Provider (After ADO.NET)

> Start only after completing ADO.NET Provider

## EF Core Classes to Implement

| Class | Purpose |
|-------|---------|
| `WitDbContextOptionsExtensions` | DbContext configuration |
| `WitDbDatabaseProvider` | Database provider registration |
| `WitDbTypeMappingSource` | CLR to DB type mapping |
| `WitDbSqlGenerationHelper` | SQL generation helpers |
| `WitDbQuerySqlGenerator` | Query translation |
| `WitDbModificationCommandBatch` | Batch INSERT/UPDATE/DELETE |
| `WitDbMigrationsSqlGenerator` | Migration SQL generation |
| `WitDbDatabaseCreator` | Database creation/deletion |
| `WitDbRelationalConnection` | Relational connection |
| `WitDbModelValidator` | Model validation |

### EF Core Features:
- [ ] LINQ to SQL translation
- [ ] Change tracking integration
- [ ] Migrations support
- [ ] Scaffolding (reverse engineering)
- [ ] Compiled queries

---

## Implementation Timeline

### Phase 2: Engine Completion (Current)

| Week | Tasks |
|------|-------|
| **Week 1-2** | Transaction fix, Index seek/range, ALTER TABLE fix |
| **Week 3-4** | CTE execution, RETURNING clause |
| **Week 5-6** | Window functions (ROW_NUMBER, RANK) |
| **Week 7-8** | INFORMATION_SCHEMA, JSON functions |

### Phase 3: ADO.NET (After Engine)

| Week | Tasks |
|------|-------|
| **Week 9-10** | WitDbConnection, WitDbCommand |
| **Week 11-12** | WitDbDataReader, Parameters |
| **Week 13-14** | Transaction, Factory, Tests |

### Phase 4: EF Core (After ADO.NET)

| Week | Tasks |
|------|-------|
| **Week 15-16** | Basic provider registration |
| **Week 17-18** | Query translation |
| **Week 19-20** | Migrations, Scaffolding |

---

## Test Status

| Test File | Passing | Ignored | Notes |
|-----------|---------|---------|-------|
| ExpressionEvaluator* | 194 | 0 | OK |
| StatementExecutor* | 145 | 1 | ALTER TABLE DEFAULT |
| Iterators/* | 119 | 0 | OK |
| QueryPlanner* | 50 | 0 | OK |
| WitSqlValue* | 130 | 0 | OK |
| WitSqlEngine* | 115 | 5 | 4 Transactions + 1 ALTER |
| **Total** | **916** | **6** | 99.3% passing |

---

## Files to Create (Engine)

| File | Purpose |
|------|---------|
| `Iterators/IteratorWindow.cs` | Window function iterator |
| `Iterators/IteratorCte.cs` | CTE materialization iterator |
| `Iterators/IteratorIndexSeek.cs` | Index equality lookup |
| `Iterators/IteratorIndexRangeScan.cs` | Index range scan |
| `Schema/InformationSchema.cs` | INFORMATION_SCHEMA views |

---

## Files to Modify (Engine)

| File | Changes Needed |
|------|----------------|
| `StatementExecutor.Ddl.cs` | Add `AlterActionAddConstraint`, `AlterActionDropConstraint` |
| `StatementExecutor.Dml.cs` | Add RETURNING clause execution |
| `WitSqlEngine.Transactions.cs` | Fix lock recursion |
| `WitSqlEngine.Query.cs` | Implement index seek/range |

---

## Dependencies

```
+-------------------------------------------------------------+
|                     SQL ENGINE (Phase 2)                     |
+-------------------------------------------------------------+
|  Transaction Fix --> Index Usage --> Query Optimization      |
|        |                                                     |
|        +--> ALTER TABLE (ADD/DROP CONSTRAINT)                |
|        |                                                     |
|        +--> CTE Execution                                    |
|        |                                                     |
|        +--> Window Functions (ROW_NUMBER for pagination)     |
|        |                                                     |
|        +--> RETURNING clause (for identity values)           |
|        |                                                     |
|        +--> INFORMATION_SCHEMA (for scaffolding)             |
+-------------------------------------------------------------+
                            |
                            v
+-------------------------------------------------------------+
|                   ADO.NET PROVIDER (Phase 3)                 |
+-------------------------------------------------------------+
|  WitDbConnection --> WitDbCommand --> WitDbDataReader        |
|        |                                                     |
|        +--> WitDbTransaction --> WitDbProviderFactory        |
+-------------------------------------------------------------+
                            |
                            v
+-------------------------------------------------------------+
|                   EF CORE PROVIDER (Phase 4)                 |
+-------------------------------------------------------------+
|  TypeMapping --> QueryTranslation --> Migrations             |
+-------------------------------------------------------------+
```

---

## Next Steps (Immediate)

1. **Transaction Fix** - fix lock recursion issue
2. **ALTER TABLE** - add `AddConstraint` / `DropConstraint` to `ExecuteAlterTable`
3. **Index Seek** - implement `CreateIndexSeek()` for B+Tree
4. **Index Range Scan** - implement range queries
5. **Enable Tests** - enable ignored tests

---

**Last Updated:** 2025-01-27


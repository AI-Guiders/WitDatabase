# TODO: UPDATE/DELETE Performance Investigation

**Date:** 2025-06-11  
**Status:** ? COMPLETE - Fast Path Implemented for UPDATE and DELETE

---

## Problem Statement

UPDATE/DELETE operations were significantly slower than SQLite:
- SQLite: ~8.5ms for 1000 UPDATE by PK  
- WitDb BTree: ~480ms for 1000 UPDATE by PK (~56x slower)

---

## Solution Implemented ?

Added **Fast Path** for simple PK-based UPDATE and DELETE operations:

### Single-Row Fast Path

#### UPDATE Fast Path (`StatementExecutor.Update.cs`)

```csharp
private bool TryExecuteUpdateFastPath(WitSqlStatementUpdate update, DefinitionTable table, out WitSqlResult result)
```

#### DELETE Fast Path (`StatementExecutor.Delete.cs`)

```csharp
private bool TryExecuteDeleteFastPath(WitSqlStatementDelete delete, DefinitionTable table, out WitSqlResult result)
```

**Fast path conditions:**
1. No FROM/USING clause (simple table DML)
2. Simple PK equality WHERE clause (`Id = @value`)
3. No BEFORE/INSTEAD OF triggers
4. **PK column must be AUTOINCREMENT** (ensures PK value equals _rowid)

**Fast path behavior:**
- Extracts PK value from WHERE clause
- Calls `GetRowById` directly (bypasses iterator)
- Applies changes/delete directly
- Still fires AFTER triggers correctly

### Batch Fast Path (NEW)

#### UPDATE Batch Fast Path (`StatementExecutor.Update.cs`)

```csharp
private bool TryExecuteUpdateBatchFastPath(WitSqlStatementUpdate update, DefinitionTable table, out WitSqlResult result)
```

#### DELETE Batch Fast Path (`StatementExecutor.Delete.cs`)

```csharp
private bool TryExecuteDeleteBatchFastPath(WitSqlStatementDelete delete, DefinitionTable table, out WitSqlResult result)
```

**Batch fast path conditions:**
1. No FROM/USING clause (simple table DML)
2. PK IN (...) WHERE clause with value list (no subqueries)
3. No BEFORE/INSTEAD OF triggers
4. **PK column must be AUTOINCREMENT** (ensures PK value equals _rowid)

**Batch fast path behavior:**
- Extracts all PK values from IN (...) list
- Processes each row using direct `GetRowById`
- Fires AFTER triggers for each row
- Supports RETURNING clause
- Supports CASCADE delete

---

## Constraint Validation Optimization ? (NEW)

### Problem
UPDATE Fast Path was still slow because `ValidateConstraints()` was doing:
- Full table scan to check UNIQUE constraints
- Full table scan for each FK reference

### Solution: `ValidateConstraintsFastPath()`

**Key optimizations:**
1. **Track modified columns** - only validate constraints on columns that actually changed
2. **Skip PK uniqueness check** - if PK is not being modified, no need to check
3. **Skip FK validation** - if FK columns are not modified, no need to validate
4. **Use indexes when available** - for UNIQUE constraint checking

```csharp
private void ValidateConstraintsFastPath(
    DefinitionTable table, 
    WitSqlRow row, 
    string tableName, 
    long excludeRowId,
    HashSet<string> modifiedColumns)
```

**Constraint checks:**
- **CHECK constraints**: Only check if modified column has CHECK expression
- **UNIQUE constraints**: Only check if modified column is part of UNIQUE constraint  
- **PK constraints**: Only check if PK column itself is being modified
- **FK constraints**: Only check if FK column is being modified

---

## Performance Results

### UPDATE (Before vs After Optimization)
| Operation | Before | After | Improvement |
|-----------|--------|-------|-------------|
| Prepared SQL UPDATE by PK | 0.21ms | **~0.01ms** | ~20x faster |
| Batch UPDATE (IN clause) | N/A | **<0.5ms** for 10 rows | New feature |

### DELETE (With Fast Path)
| Operation | Time per row |
|-----------|-------------|
| Prepared SQL DELETE by PK | **<0.3ms** |
| Batch DELETE (IN clause) | **<0.3ms** for 10 rows |

### Benchmark Comparison (DmlFastPathBenchmarks)
| Operation | WitDb | SQLite | LiteDB |
|-----------|-------|--------|--------|
| DELETE batch IN (100 rows) | 0.26ms | 6.45ms | 0.43ms |
| DELETE batch IN (1000 rows) | 0.32ms | 6.50ms | 0.58ms |
| DELETE by PK (100 rows) | 0.33ms | 6.43ms | 0.46ms |

**WitDb DELETE is now faster than both SQLite and LiteDB!**

---

## Test Results

| Test Suite | Tests | Status |
|------------|-------|--------|
| DmlPerformanceTests | 15 | ? Pass |
| StatementExecutorUpdateTests | 13 | ? Pass |
| StatementExecutorDeleteTests | 11 | ? Pass |
| StatementExecutorConstraintTests | 9 | ? Pass |
| WitSqlEngineCascadeTests | 12 | ? Pass |
| **Total Engine Tests** | **1609** | ? Pass |

---

## Key Implementation Details

### Modified Column Tracking
```csharp
var modifiedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

foreach (var setClause in update.SetClauses)
{
    var newValue = evaluator.Evaluate(setClause.Value, existingRow.Value);
    // Only mark as modified if value actually changed
    if (!newValues[i].Equals(newValue))
    {
        newValues[i] = newValue;
        modifiedColumns.Add(setClause.ColumnName);
    }
}
```

### Smart Constraint Validation
- If `modifiedColumns` is empty (no actual changes), skip all validation
- CHECK: Only columns with CHECK expressions that were modified
- UNIQUE: Only constraints where any column was modified
- PK: Only if PK column was modified (important for `UPDATE ... SET Id = ?`)
- FK: Only if FK column was modified

---

## Files Modified

1. `StatementExecutor.Update.cs` - Fast path + optimized validation
2. `StatementExecutor.Delete.cs` - Fast path (unchanged from before)
3. `StatementExecutor.Validation.cs` - Utility methods
4. `DmlOptimizer.cs` - Iterator optimization for fallback path


# TODO: UPDATE Performance Optimization

## Summary

Based on benchmark analysis, UPDATE operations in WitDb show critical performance issues:
- **Bulk UPDATE 1000 rows**: 37x slower than SQLite, allocates 754MB RAM
- **UPDATE by indexed column 1000 rows**: 68x slower than SQLite, allocates 779MB RAM

## Priority P0 - Critical (Must Fix)

### 1. ? DONE - Streaming UPDATE with Batch Constraint Validation
**File**: `Sources/Engine/OutWit.Database/Statements/StatementExecutor.Update.cs`
**Changes**:
- Added `TryExecuteUpdateStreaming` method for streaming UPDATE
- Rows are updated immediately during iteration (no accumulation)
- UNIQUE constraints validated in batch (collect values, single scan)
- FK constraints validated in batch (single scan per foreign table)
- CHECK/NOT NULL validated per-row (fast, no I/O)

### 2. ? TODO - Further optimize UNIQUE constraint validation with indexes
**File**: `Sources/Engine/OutWit.Database/Statements/StatementExecutor.Update.cs`
**Current**: Falls back to table scan if no index
**Target**: Always try index seek first, batch the seeks

### 3. ? TODO - FK validation with index seek
**File**: `Sources/Engine/OutWit.Database/Statements/StatementExecutor.Validation.cs`
**Current**: Full table scan of foreign table
**Target**: Use PK index on foreign table for O(log N) lookup

## Priority P1 - High (Should Fix)

### 4. ? DONE - Skip Index Update for Unchanged Columns
**File**: `Sources/Engine/OutWit.Database/Engine/WitSqlEngine.Dml.IndexUpdates.cs`
**Changes**:
- Added `IndexUsesAnyColumn` method to check if index uses any modified columns
- Added overloaded `UpdateIndexesOnUpdate` that accepts `modifiedColumns` set
- Skips index processing entirely if no indexed columns changed
- Added `UpdateRow` overload in `IDatabase` interface for optimization hints

### 5. ? DONE - Expression Parsing Cache
**File**: `Sources/Engine/OutWit.Database/Statements/StatementExecutor.cs`
**Changes**:
- Added `m_expressionCache` dictionary (max 256 entries)
- Added `GetOrParseExpression(string)` method for cached parsing
- Updated `ValidateCheckConstraints` in `StatementExecutor.Validation.cs`
- Updated `ValidateCheckConstraintsFastPath` in `StatementExecutor.Update.cs`
- Updated computed column handling in all UPDATE paths
- Updated default value evaluation in cascading actions

### 6. ? DONE - Streaming UPDATE Optimization
**File**: `Sources/Engine/OutWit.Database/Statements/StatementExecutor.Update.cs`
**Changes**:
- Added `TryExecuteUpdateStreaming` method
- First pass: collect row IDs to update (minimal memory)
- Second pass: update rows by ID without accumulating
- Skips UNIQUE constraint tables scans for non-UNIQUE SET columns
- 78x faster for 5000 row batch UPDATE, 350x less memory

## Tests Added

### P0 Tests (StatementExecutorUpdateTests.cs)
- `BulkUpdateWithoutUniqueConstraintUsesOptimizedPathTest` - Verifies streaming UPDATE
- `BulkUpdateDetectsDuplicateInBatchTest` - Verifies batch UNIQUE validation catches duplicates
- `BulkUpdateNonUniqueColumnSucceedsTest` - Verifies non-UNIQUE columns can have same value
- `BulkUpdateWithWhereClauseTest` - Verifies WHERE clause filtering
- `BulkUpdateStreamingDoesNotAccumulateRowsTest` - Verifies no memory accumulation

### P1.4 Tests (WitSqlEngineIndexAutoUpdateTests.cs)
- `UpdateNonIndexedColumnSkipsIndexUpdateTest` - Verifies index skip optimization
- `UpdateWithMultipleIndexesOnlyUpdatesAffectedTest` - Verifies selective index update
- `BulkUpdateNonIndexedColumnDoesNotTouchIndexTest` - Verifies bulk optimization
- `UpdateCompositeIndexOnlyWhenAnyColumnChangedTest` - Verifies composite index handling
- `UpdateColumnInExpressionIndexTriggersUpdateTest` - Verifies expression index detection
- `UpdateColumnInPartialIndexWhereClauseTest` - Verifies partial index WHERE clause detection

## Benchmark Results After Optimization

### UPDATE SET (batch without PK) - Comparison with SQLite:
| RowCount | WitDb Before | WitDb After | SQLite | Improvement |
|----------|--------------|-------------|--------|-------------|
| 100 | 1.6ms, 1MB | 0.4ms, 150KB | 6.7ms | **17x faster than SQLite** |
| 1000 | 34ms, 82MB | 3.1ms, 1.2MB | 6.8ms | **2x faster than SQLite** |
| 5000 | 1,150ms, 2GB | 14.6ms, 5.8MB | 7.2ms | 2x slower (acceptable) |

### UPDATE by PK (prepared loop):
| RowCount | WitDb | SQLite | Comparison |
|----------|-------|--------|------------|
| 100 | 2.4ms | 6.8ms | **2.8x faster** |
| 1000 | 30ms | 9ms | 3x slower |
| 5000 | 87ms | 17ms | 5x slower |

### Memory Improvement:
| Operation | Before | After | Reduction |
|-----------|--------|-------|-----------|
| Bulk UPDATE 1000 rows | 82MB | 1.2MB | **68x** |
| Bulk UPDATE 5000 rows | 2GB | 5.8MB | **350x** |

## Architecture Notes

The UPDATE execution now has multiple paths:

1. **TryExecuteUpdateFastPath** - Single row by PK (fastest)
2. **TryExecuteUpdateBatchFastPath** - Multiple rows by PK IN (...)
3. **TryExecuteUpdateStreaming** - Streaming without accumulation (NEW)
4. **ExecuteUpdateStandard** - Full trigger/constraint support (fallback)

The streaming path is used when:
- No FROM clause
- No BEFORE/INSTEAD OF triggers
- No RETURNING clause
- No UNIQUE constraints on SET columns

The index update optimization:
- `UpdateRow(tableName, rowId, newRow, modifiedColumns)` - New overload
- `IndexUsesAnyColumn(indexDef, modifiedColumns)` - Checks if index affected
- Skips entire index processing when no indexed columns modified

Expression caching:
- `GetOrParseExpression(sql)` - Returns cached or newly parsed expression
- Used for CHECK constraints, computed columns, default values
- Cache limit: 256 entries (LRU not implemented - simple bounded cache)

## Progress Tracking

- [x] P0.1 - Streaming UPDATE implementation
- [x] P0.2 - Batch UNIQUE constraint validation
- [x] P0.3 - Batch FK constraint validation  
- [x] P1.4 - Skip index update for unchanged columns
- [x] P1.5 - Expression caching
- [x] P1.6 - Streaming UPDATE for bulk operations
- [x] Tests for streaming UPDATE
- [x] Tests for batch validation
- [x] Tests for index update optimization

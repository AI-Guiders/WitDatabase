# TODO: Simple Aggregates Optimization

## Current Status: ? Phase 1 & 2 Complete

WitDb simple aggregates (without GROUP BY) were 20-170x slower than SQLite.
**After Phase 1 optimization: COUNT(*) without WHERE is now O(1)!**
**After Phase 2 optimization: MIN/MAX on indexed columns is now O(1)!**

### Benchmark Results (10000 rows)

| Operation | WitDb (Before) | WitDb (After) | SQLite | LiteDB | Improvement |
|-----------|----------------|---------------|--------|--------|-------------|
| COUNT(*) | 10.0ms | **<0.1ms** | 0.06ms | 3.1ms | **100x+ faster** |
| MIN/MAX (indexed) | 11.0ms | **<0.1ms** | 0.66ms | 16.4ms | **100x+ faster** |
| SUM(Amount) | 10.7ms | 10.7ms | 0.45ms | 16.1ms | No change |
| AVG(Amount) | 10.1ms | 10.1ms | 0.41ms | 15.6ms | No change |
| MIN/MAX (no index) | 11.0ms | 11.0ms | 0.66ms | 16.4ms | No change |

## Phase 1: COUNT(*) Metadata ? COMPLETE

**Target**: 1000x improvement for COUNT(*) without WHERE ? Achieved

### Implementation

1. **Store row count in table metadata** ?
   - Added `m_tableRowCounts` dictionary in `SchemaCatalog`
   - Row count persisted with `$schema:_rowcount:{tableName}` key
   - Loaded on startup, saved on changes

2. **Update row count on DML operations** ?
   - `IncrementRowCount()` called on INSERT
   - `DecrementRowCount()` called on DELETE  
   - `ResetRowCount()` called on TRUNCATE
   - CREATE TABLE initializes count to 0

3. **Query planner shortcut** ?
   - `TryOptimizeSimpleCountStar()` in QueryPlanner
   - Returns `IteratorConstant` with cached count
   - Requirements: COUNT(*), single table, no WHERE/GROUP BY/HAVING/CTEs

4. **Created `IteratorConstant`** ?
   - Returns single constant row
   - O(1) memory and time

### Files Modified

- `Sources/Engine/OutWit.Database/Schema/SchemaCatalog.cs` - Row Count Management region
- `Sources/Engine/OutWit.Database/Schema/SchemaCatalog.Tables.cs` - Init on CREATE, delete on DROP
- `Sources/Engine/OutWit.Database/Schema/SchemaCatalog.Persistence.cs` - Load on startup
- `Sources/Engine/OutWit.Database/Engine/WitSqlEngine.Dml.Operations.cs` - Update on INSERT/DELETE/TRUNCATE
- `Sources/Engine/OutWit.Database/Engine/WitSqlEngine.Query.cs` - GetTableRowCount method
- `Sources/Engine/OutWit.Database/Interfaces/IDatabase.cs` - GetTableRowCount interface
- `Sources/Engine/OutWit.Database/Query/QueryPlanner.cs` - TryOptimizeSimpleCountStar
- `Sources/Engine/OutWit.Database/Iterators/IteratorConstant.cs` - New iterator
- `Sources/Engine/OutWit.Database.Tests/Statements/StatementExecutorTestsBase.cs` - Mock setup

### Test Coverage (19 tests)

`Sources/Engine/OutWit.Database.Tests/Optimizations/CountStarOptimizationTests.cs`:

**Row Count Tracking (7 tests)**:
- ? `RowCountStartsAtZeroForNewTableTest`
- ? `RowCountIncrementsOnInsertTest`
- ? `RowCountIncrementsOnMultipleInsertsTest`
- ? `RowCountDecrementsOnDeleteTest`
- ? `RowCountResetsOnTruncateTest`
- ? `RowCountUnchangedOnUpdateTest`
- ? `RowCountReturnsMinusOneForNonExistentTableTest`

**COUNT(*) Optimization (8 tests)**:
- ? `CountStarUsesMetadataForSimpleQueryTest`
- ? `CountStarWithWhereDoesNotUseOptimizationTest`
- ? `CountStarWithGroupByDoesNotUseOptimizationTest`
- ? `CountStarWithAliasTest`
- ? `CountStarOnEmptyTableTest`
- ? `MultipleAggregatesDoNotUseCountStarOptimizationTest`
- ? `CountStarWithJoinDoesNotUseOptimizationTest`
- ? `CountStarWithSubqueryDoesNotUseOptimizationTest`

**Consistency (2 tests)**:
- ? `RowCountCorrectAfterMultipleOperationsTest`
- ? `RowCountMatchesActualDataTest`

**Performance (2 tests)**:
- ? `CountStarOptimizationIsFastTest`
- ? `CountStarOptimizationVsFullScanTest`

## Phase 2: MIN/MAX Index Optimization ? COMPLETE

**Target**: 200x improvement for MIN/MAX on indexed column ? Achieved

### Implementation

1. **Added GetFirstEntry/GetLastEntry to ISecondaryIndex** ?
   - `GetFirstEntry()` returns (IndexKey, PrimaryKey) of leftmost entry (for MIN)
   - `GetLastEntry()` returns (IndexKey, PrimaryKey) of rightmost entry (for MAX)
   - Implemented in `SecondaryIndexKeyValueStore` and `SecondaryIndexIndexedDb`

2. **Query planner shortcut** ?
   - `TryOptimizeSimpleMinMax()` in QueryPlanner
   - Detects `SELECT MIN(col)` or `SELECT MAX(col)` on single table
   - Checks if index exists on the column
   - Returns `IteratorConstant` with value from index edge
   - Returns `IteratorConstantNull` for empty tables

3. **Index key deserialization** ?
   - `DeserializeIndexKey()` converts byte[] back to WitSqlValue
   - Handles all types: Integer, Double, String, Date, DateTime, Decimal, etc.

### Files Modified

- `Sources/Core/OutWit.Database.Core/Interfaces/ISecondaryIndex.cs` - Added GetFirstEntry/GetLastEntry
- `Sources/Core/OutWit.Database.Core/Indexes/SecondaryIndexKeyValueStore.cs` - Implementation
- `Sources/Core/OutWit.Database.Core.IndexedDb/Indexes/SecondaryIndexIndexedDb.cs` - Implementation
- `Sources/Engine/OutWit.Database/Query/QueryPlanner.cs` - TryOptimizeSimpleMinMax, DeserializeIndexKey
- `Sources/Engine/OutWit.Database/Engine/WitSqlEngine.Ddl.Indexes.cs` - GetPhysicalIndex
- `Sources/Engine/OutWit.Database/Iterators/IteratorConstant.cs` - Added IteratorConstantNull
- `Sources/Engine/OutWit.Database/Values/WitSqlValue.cs` - Added GetSqlType method

### Test Coverage (17 tests)

`Sources/Engine/OutWit.Database.Tests/Optimizations/MinMaxOptimizationTests.cs`:

**MIN Tests (4 tests)**:
- ? `MinOnIndexedColumn_ReturnsCorrectValue_Test`
- ? `MinOnIntegerColumn_ReturnsCorrectValue_Test`
- ? `MinOnEmptyTable_ReturnsNull_Test`
- ? `MinWithAlias_UsesAlias_Test`

**MAX Tests (4 tests)**:
- ? `MaxOnIndexedColumn_ReturnsCorrectValue_Test`
- ? `MaxOnIntegerColumn_ReturnsCorrectValue_Test`
- ? `MaxOnEmptyTable_ReturnsNull_Test`
- ? `MaxWithAlias_UsesAlias_Test`

**Non-Optimized Cases (4 tests)**:
- ? `MinWithWhereClause_DoesNotUseOptimization_Test`
- ? `MinWithGroupBy_DoesNotUseOptimization_Test`
- ? `MinOnNonIndexedColumn_DoesNotUseOptimization_Test`
- ? `MultipleAggregates_DoesNotUseOptimization_Test`

**Edge Cases (4 tests)**:
- ? `MinAfterInsert_ReturnsUpdatedValue_Test`
- ? `MaxAfterDelete_ReturnsUpdatedValue_Test`
- ? `MinOnStringColumn_WithIndex_ReturnsCorrectValue_Test`
- ? `MaxOnStringColumn_WithIndex_ReturnsCorrectValue_Test`

**Performance (1 test)**:
- ? `MinMaxOptimization_IsFast_Test`

## Phase 3-4: Deferred (Low Priority)

These optimizations are deferred because:
1. **COUNT(*) and MIN/MAX already match SQLite** - Main performance gap closed
2. **WitDb beats LiteDB** in all aggregate operations
3. **OLTP focus** - Main use case doesn't require analytics optimization
4. **Significant Core changes required** - Would need batch processing

### Phase 3: Batch Accumulation (Deferred)

Would require:
- Column-oriented batch reader
- `IteratorBatchAggregate` for processing batches
- Skip full row deserialization

**Estimated improvement**: 3-5x for SUM/AVG

### Phase 4: SIMD Aggregation (Deferred)

Would require:
- `Vector<T>` based accumulators
- Type-specific paths for numeric columns

**Estimated improvement**: 2-4x additional on numeric columns

## Summary

### Success Metrics After Phase 1 & 2

| Query Type | Performance | Status |
|------------|-------------|--------|
| `SELECT COUNT(*) FROM table` | **<0.1ms** (was 10ms) | ? **100x+ improvement** |
| `SELECT MIN(col) FROM table` (indexed) | **<0.1ms** (was 11ms) | ? **100x+ improvement** |
| `SELECT MAX(col) FROM table` (indexed) | **<0.1ms** (was 11ms) | ? **100x+ improvement** |
| `SELECT COUNT(*) FROM table WHERE ...` | ~10ms | Uses streaming |
| `SELECT MIN/MAX FROM table` (no index) | ~11ms | Uses streaming |
| `SELECT SUM/AVG/MIN/MAX FROM table` | ~10ms | Uses streaming |

### Comparison with Other Databases

| Operation | WitDb | SQLite | LiteDB | Winner |
|-----------|-------|--------|--------|--------|
| COUNT(*) no WHERE | <0.1ms | 0.06ms | 3.1ms | **WitDb ? SQLite** |
| MIN/MAX (indexed) | <0.1ms | 0.66ms | 16.4ms | **WitDb faster!** |
| SUM/AVG | 10.7ms | 0.45ms | 16.1ms | SQLite (WitDb beats LiteDB) |
| MIN/MAX (no index) | 11.0ms | 0.66ms | 16.4ms | SQLite (WitDb beats LiteDB) |
| GROUP BY | ~15ms | ~1ms | ~20ms | SQLite (WitDb beats LiteDB) |

**WitDb is competitive with embedded databases for OLTP workloads.**

## References

- Row count storage: `SchemaCatalog.cs` (Row Count Management region)
- Constant iterator: `IteratorConstant.cs`
- Query planner optimization: `QueryPlanner.cs` (TryOptimizeSimpleCountStar, TryOptimizeSimpleMinMax)
- Index interface: `ISecondaryIndex.cs` (GetFirstEntry, GetLastEntry)
- Current streaming aggregate: `IteratorStreamingAggregate.cs`
- Tests: `CountStarOptimizationTests.cs`, `MinMaxOptimizationTests.cs`

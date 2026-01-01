# TODO: JOIN Operations Optimization

## Current Status: ? Phase 1 Complete

WitDb JOIN operations were 10-50x slower than SQLite and 5-15x slower than LiteDB.
**After Phase 1 optimization: WitDb is now competitive with LiteDB for JOINs!**

### Benchmark Results (100 rows per table)

| Operation | WitDb (Before) | WitDb (After) | SQLite | LiteDB | Improvement |
|-----------|----------------|---------------|--------|--------|-------------|
| INNER JOIN 2 tables | 0.77ms | ~0.13ms | 0.07ms | 0.15ms | **5.9x faster** |
| LEFT JOIN | 0.72ms | ~0.13ms | 0.07ms | 0.15ms | **5.5x faster** |
| INNER JOIN 3 tables | 2.9ms | ~0.5ms | 0.08ms | 0.22ms | **~6x faster** |
| INNER JOIN 4 tables | 3.6ms | ~0.7ms | 0.09ms | 0.26ms | **~5x faster** |

### Root Cause Analysis

Original implementation used **Nested Loop Join** exclusively:
- For each row in outer table, scans entire inner table
- O(N × M) complexity for two tables
- O(N × M × K) for three tables, etc.
- No index utilization for join conditions

**Solution**: Implemented Hash Join algorithm with O(N + M) complexity.

## Phase 1: Hash Join for Equality Conditions ? COMPLETE

**Target**: 5-10x improvement for equi-joins ? Achieved

### Implementation

1. **Created `IteratorHashJoin` class** ?
   - Build hash table on smaller relation (build phase)
   - Probe hash table with larger relation (probe phase)  
   - O(N + M) complexity instead of O(N × M)
   - Supports INNER and LEFT joins
   - Proper NULL handling (NULLs don't match per SQL standard)
   - Multi-column join key support

2. **Created `OptimizerJoinCondition`** ?
   - Analyzes ON conditions for equi-join keys
   - Extracts residual conditions for non-equality parts
   - Determines when to use hash join vs nested loop
   - Chooses optimal build side based on table sizes

3. **Integrated with QueryPlanner** ?
   - Automatic detection of equi-join conditions
   - Falls back to nested loop for non-equi joins or small tables
   - EXPLAIN shows "HASH JOIN" when hash join is used

**Files created/modified**:
- `Sources/Engine/OutWit.Database/Iterators/IteratorHashJoin.cs` (new)
- `Sources/Engine/OutWit.Database/Optimizers/OptimizerJoinCondition.cs` (new)
- `Sources/Engine/OutWit.Database/Query/QueryPlanner.Sources.cs` (modified)
- `Sources/Engine/OutWit.Database/Statements/StatementExecutor.Explain.cs` (modified)
- `Sources/Engine/OutWit.Database.Tests/Iterators/IteratorHashJoinTests.cs` (new - 20 tests)
- `Sources/Engine/OutWit.Database.Tests/Engine/WitSqlEngineHashJoinIntegrationTests.cs` (new - 13 tests)

### Test Coverage

**Unit Tests (20 tests)**:
- ? INNER JOIN basic functionality
- ? LEFT JOIN with all left rows preserved  
- ? NULL handling (NULLs don't match)
- ? Multi-column join keys
- ? Duplicate keys (Cartesian product)
- ? Empty tables handling
- ? Large dataset (1000 rows)
- ? Build side selection
- ? Reset functionality
- ? Schema preservation

**Integration Tests (13 tests)**:
- ? EXPLAIN shows HASH JOIN for large tables
- ? EXPLAIN shows NESTED LOOP for small tables
- ? Correct row counts for INNER/LEFT joins
- ? NULL values don't match
- ? Multi-column key matching
- ? JOIN with WHERE clause
- ? JOIN with GROUP BY/aggregation
- ? Subquery with JOIN
- ? Multiple JOINs in single query
- ? Performance regression test

### Comparison After Optimization

| Table Size | WitDb | SQLite | LiteDB | vs SQLite | vs LiteDB |
|------------|-------|--------|--------|-----------|-----------|
| 100 rows | 0.13ms | 0.07ms | 0.17ms | 1.9x slower | **1.3x faster** ? |
| 500 rows | 0.71ms | 0.11ms | 0.73ms | 6.5x slower | **~same** ? |
| 1000 rows | ~1.4ms | ~0.15ms | ~1.5ms | ~9x slower | **~same** ? |

**WitDb now matches or beats LiteDB for JOIN operations!**

## Phase 2: Index Nested Loop Join ?? LOW PRIORITY

**Status**: Deferred - Hash Join already provides excellent performance for most cases.

**Analysis**:
- Hash Join has O(N + M) complexity
- Index Nested Loop has O(N × log M) complexity - **worse for large tables**
- Only beneficial when:
  - Memory is extremely constrained (can't build hash table)
  - Inner table is very large and indexed
  - Outer table is very small (< 10 rows)

**Conclusion**: The scenarios where Index Nested Loop beats Hash Join are rare in practice.
Hash Join is the better default choice for equi-joins.

### Implementation Plan (if needed in future)

When inner table has index on join column, use index seek instead of hash table:

```csharp
foreach (row in outerTable)
{
    var joinValue = row[joinColumn];
    // Use index seek instead of full scan
    var matches = innerTable.IndexSeek(joinColumn, joinValue);
    foreach (var match in matches)
        yield CombineRows(row, match);
}
```

## Phase 3: Merge Join for Sorted Input ?? LOW PRIORITY

**Status**: Deferred - Complexity outweighs benefits.

**Analysis**:
- Merge Join requires sorted input on both sides
- Hash Join has same O(N + M) complexity without sorting requirement
- Only beneficial when:
  - Data is already sorted (rare)
  - ORDER BY can be pushed down (complex to implement)

**Conclusion**: The implementation complexity is high for minimal benefit over Hash Join.

## Progress Tracking

- [x] Phase 1: Hash Join ? **COMPLETE**
  - [x] Create IteratorHashJoin
  - [x] Add join key hashing with multi-column support
  - [x] Integrate with QueryPlanner
  - [x] Update EXPLAIN to show hash join
  - [x] Unit tests (20 tests passing)
  - [x] Integration tests (13 tests passing)
- [ ] Phase 2: Index Nested Loop Join - **DEFERRED** (low ROI)
- [ ] Phase 3: Merge Join - **DEFERRED** (low ROI)

## Recommendations

Instead of implementing Phase 2-3, focus on other high-impact optimizations:

1. **Simple Aggregates** (TODO_SIMPLE_AGGREGATES_OPTIMIZATION.md)
   - COUNT(*) metadata: **1000x potential improvement**
   - MIN/MAX via index: **100x potential improvement**

2. **Index Seek** (TODO_INDEX_SEEK_OPTIMIZATION.md)
   - Cursor pooling: **3-5x improvement**
   - B-tree seek optimization: **2-3x improvement**

These optimizations have much higher ROI than Phase 2-3 of JOIN optimization.

## References

- Nested loop join: `IteratorJoin.cs`
- Hash join: `IteratorHashJoin.cs`
- Join condition analyzer: `OptimizerJoinCondition.cs`
- Query planning: `QueryPlanner.Sources.cs`
- Unit tests: `IteratorHashJoinTests.cs`
- Integration tests: `WitSqlEngineHashJoinIntegrationTests.cs`

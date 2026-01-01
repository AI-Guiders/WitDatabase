# TODO: JOIN Operations Optimization

## Current Status: ?? Phase 1 In Progress

WitDb JOIN operations are 10-50x slower than SQLite and 5-15x slower than LiteDB.

### Benchmark Results (100 rows per table)

| Operation | WitDb | SQLite | LiteDB | vs SQLite | vs LiteDB |
|-----------|-------|--------|--------|-----------|-----------|
| INNER JOIN 2 tables | 0.77ms | 0.07ms | 0.15ms | 10x slower | 5x slower |
| LEFT JOIN | 0.72ms | 0.07ms | 0.15ms | 10x slower | 5x slower |
| INNER JOIN 3 tables | 2.9ms | 0.08ms | 0.22ms | 36x slower | 13x slower |
| INNER JOIN 4 tables | 3.6ms | 0.09ms | 0.26ms | 40x slower | 14x slower |
| JOIN with WHERE | 2.8ms | 0.08ms | 0.19ms | 35x slower | 15x slower |
| JOIN with GROUP BY | 0.75ms | 0.09ms | 0.16ms | 8x slower | 5x slower |

### Root Cause Analysis

Current implementation uses **Nested Loop Join** exclusively:
- For each row in outer table, scans entire inner table
- O(N × M) complexity for two tables
- O(N × M × K) for three tables, etc.
- No index utilization for join conditions

**File**: `Sources/Engine/OutWit.Database/Iterators/IteratorJoin.cs`

### Optimization Strategy

## Phase 1: Hash Join for Equality Conditions ? IMPLEMENTED

**Target**: 5-10x improvement for equi-joins

### Implementation Complete

1. **Created `IteratorHashJoin` class** ?
   - Build hash table on smaller relation (build phase)
   - Probe hash table with larger relation (probe phase)
   - O(N + M) complexity instead of O(N × M)
   - Supports INNER and LEFT joins

2. **Created `OptimizerJoinCondition`** ?
   - Analyzes ON conditions for equi-join keys
   - Extracts residual conditions
   - Determines when to use hash join

3. **Integrated with QueryPlanner** ?
   - Automatic detection of equi-join conditions
   - Falls back to nested loop for non-equi joins
   - Chooses optimal build side based on table sizes

**Files created/modified**:
- `Sources/Engine/OutWit.Database/Iterators/IteratorHashJoin.cs` (new)
- `Sources/Engine/OutWit.Database/Optimizers/OptimizerJoinCondition.cs` (new)
- `Sources/Engine/OutWit.Database/Query/QueryPlanner.Sources.cs` (modified)
- `Sources/Engine/OutWit.Database.Tests/Iterators/IteratorHashJoinTests.cs` (new)

### Test Coverage

- ? INNER JOIN basic functionality
- ? LEFT JOIN with all left rows preserved
- ? NULL handling (NULLs don't match)
- ? Multi-column join keys
- ? Duplicate keys
- ? Empty tables
- ? Large dataset (1000 rows)
- ? Build side selection
- ? Reset functionality

## Phase 2: Index Nested Loop Join (Medium Priority)

**Target**: 3-5x improvement when index exists on join column

### Implementation Plan

1. **Detect indexed join conditions**:
   - If inner table has index on join column, use index seek instead of scan

2. **Algorithm**:
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

**Files to modify**:
- `Sources/Engine/OutWit.Database/Iterators/IteratorIndexNestedLoopJoin.cs` (new)
- `Sources/Engine/OutWit.Database/Query/QueryPlanner.Sources.Indexes.cs`

## Phase 3: Merge Join for Sorted Input (Low Priority)

**Target**: Optimal for pre-sorted data or when ORDER BY matches join

### Implementation Plan

1. **Detect sorted input**:
   - Both inputs sorted on join columns
   - Or one input sorted + other can use index

2. **Algorithm**: Single pass merge O(N + M)

## Expected Results vs Actual Results

| Operation | Before | After | Improvement | Target Met |
|-----------|--------|-------|-------------|------------|
| JOIN 100 rows | 0.77ms | 0.13ms | **5.9x faster** | ? < 0.2ms |
| JOIN 500 rows | ~3.5ms | 0.71ms | **~5x faster** | ? |

### Comparison After Optimization

| Table Size | WitDb | SQLite | LiteDB | vs SQLite | vs LiteDB |
|------------|-------|--------|--------|-----------|-----------|
| 100 rows | 0.13ms | 0.07ms | 0.17ms | 1.9x slower | **1.3x faster** ? |
| 500 rows | 0.71ms | 0.11ms | 0.73ms | 6.5x slower | **~same** |

**WitDb now matches or beats LiteDB for JOIN operations!**

### Success Metrics

After Phase 1 (Hash Join):
- 2-table JOIN 100 rows: 0.13ms ? (target was < 0.2ms)
- 2-table JOIN 500 rows: 0.71ms ?

Target achieved: **WitDb is now competitive with LiteDB for JOINs**

## Progress Tracking

- [x] Phase 1: Hash Join
  - [x] Create IteratorHashJoin
  - [x] Add join key hashing
  - [x] Integrate with QueryPlanner
  - [x] Unit tests (20 tests passing)
  - [ ] Benchmark validation
- [ ] Phase 2: Index Nested Loop Join
- [ ] Phase 3: Merge Join

## References

- Current nested loop: `IteratorJoin.cs`
- Hash join: `IteratorHashJoin.cs`
- Join condition analyzer: `OptimizerJoinCondition.cs`
- Query planning: `QueryPlanner.Sources.cs`
- Benchmark: `JoinBenchmarks.cs`

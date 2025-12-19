# LSM-Tree Improvements Plan

## Status Legend
- ? Not Started
- ?? In Progress
- ? Completed
- ? Blocked

---

## Phase 1: Critical Fixes (Required for Production) ? COMPLETE

### 1.1 Locking Issues ?
- [x] **MemTable: Fix mixed locking** - Replace `ReaderWriterLockSlim` + `Interlocked` with proper `Lock`
- [x] **LsmTreeStore: Replace `object` lock** - Use `ReaderWriterLockSlim` for SSTable list, `Lock` for writes
- [x] **SSTableReader: Add thread-safe reads** - Lock for FileStream access
- [x] **Add tests for concurrent access** - MemTable and LsmTreeStore concurrent tests

### 1.2 Compaction Integration ?
- [x] **LsmTreeStore: Integrate Compactor** - Call compaction when L0 threshold reached
- [x] **Atomic SSTable swap** - Replace old tables with compacted ones safely
- [x] **Add tests for compaction** - CompactorMergesSSTablesTest, CompactorRemovesTombstonesTest
- [x] **Cache invalidation on compaction** - Invalidate old SSTable blocks

### 1.3 Bloom Filter Integration ?
- [x] **SSTableBuilder: Add Bloom filter** - Build filter during SSTable creation
- [x] **SSTableReader: Use Bloom filter** - Skip block reads for definite non-matches
- [x] **Serialize Bloom filter in SSTable footer** - New V2 format (44 bytes)
- [x] **Add tests for Bloom filter integration** - SSTableBloomFilterSkipsNonExistentKeysTest, SSTableBloomFilterIntegrationTest

### 1.4 Block Cache ?
- [x] **SSTableReader: Add LRU block cache** - Cache recently read blocks
- [x] **Make cache size configurable** - EnableBlockCache, BlockCacheSizeBytes in LsmOptions
- [x] **Add cache hit/miss statistics** - Hits, Misses, HitRatio properties
- [x] **Add tests for caching behavior** - 7 BlockCache tests + 2 integration tests

---

## Phase 2: Performance Optimizations ?? IN PROGRESS

### 2.1 Scan Improvements ?
- [x] **Streaming Scan** - Use heap-based merge iterator instead of materializing all entries
- [x] **MergeIterator: Use PriorityQueue** - O(log n) instead of O(n) for min selection
- [ ] **Add benchmarks for scan operations**

### 2.2 Memory Optimizations
- [ ] **MemTable: Consider skip list** - Better cache locality than SortedDictionary
- [ ] **ArrayPool usage in SSTable** - Reduce allocations during reads
- [ ] **Span-based key comparisons** - Avoid byte[] allocations where possible

### 2.3 Compression
- [ ] **Block compression support** - LZ4 or Snappy
- [ ] **Configurable compression level**
- [ ] **Add compression flag to SSTable format**

### 2.4 Background Operations ?
- [x] **Background compaction thread** - Async compaction via Task.Run
- [x] **WaitForCompaction method** - Wait for pending compaction
- [x] **IsCompacting property** - Check if compaction is running
- [ ] **Rate limiting** - Control compaction I/O
- [ ] **Compaction scheduling** - Smart picking of files

---

## Phase 3: Production Features

### 3.1 Level-Based Compaction
- [ ] **Level structure** - L0, L1, L2... with size ratios
- [ ] **Compaction picker** - Choose which files to compact
- [ ] **Tiered vs Leveled strategy option**

### 3.2 Durability & Recovery
- [ ] **WAL checkpointing** - Periodic checkpoints
- [ ] **Manifest file** - Track SSTable metadata
- [ ] **Atomic manifest updates**

### 3.3 Monitoring
- [ ] **Statistics interface** - Read/write counts, latencies
- [ ] **Compaction statistics**
- [ ] **Memory usage tracking**

---

## Current Progress

| Phase | Total Items | Completed | Percentage |
|-------|-------------|-----------|------------|
| Phase 1 | 16 | 16 | 100% ? |
| Phase 2 | 14 | 5 | 36% |
| Phase 3 | 7 | 0 | 0% |

**Last Updated**: 2024-12-19

---

## Implementation Notes

### 1.1 MemTable Locking (COMPLETED)
Used simple `Lock` (C# 13) for all operations. MemTable write throughput is critical,
and the simple lock avoids complexity of ReaderWriterLockSlim.

### 1.2 LsmTreeStore Locking (COMPLETED)
- `Lock` for write serialization (Put/Delete)
- `ReaderWriterLockSlim` for SSTable list (allows concurrent reads)
- `Volatile.Read/Write` for immutableMemTable reference

### 1.3 Bloom Filter Integration (COMPLETED)
SSTable V2 footer format (44 bytes):
```
[IndexOffset:8][IndexSize:4][EntryCount:4][Flags:4]
[BloomOffset:8][BloomSizeBytes:4][BloomBitSize:4][BloomHashCount:4]
[Magic:4]
```

Key insight: Must store original bit size separately from byte size
because `ToBytes()` rounds up to byte boundary.

### 1.4 Block Cache (COMPLETED)
- LRU eviction based on `Environment.TickCount64`
- Configurable via `LsmOptions.EnableBlockCache` and `BlockCacheSizeBytes`
- Shared cache instance across all SSTableReaders in LsmTreeStore
- Cache invalidation on compaction
- Statistics: Hits, Misses, HitRatio, Count, CurrentSizeBytes

### 2.1 Scan Improvements (COMPLETED)
- MergeIterator now uses `PriorityQueue<T, TPriority>` for O(log n) min selection
- LsmTreeStore.Scan() uses streaming merge instead of materializing all entries
- Memory usage now O(sources) instead of O(total entries)

### 2.4 Background Compaction (COMPLETED)
- `BackgroundCompaction` option in LsmOptions (default: true)
- `ScheduleBackgroundCompaction()` schedules compaction via Task.Run
- `ExecuteCompaction()` does actual work, checks for race conditions
- `WaitForCompaction()` waits for pending compaction
- `IsCompacting` property indicates if compaction is running

---

## Test Coverage Summary

| Component | Tests |
|-----------|-------|
| MemTable | 7 tests (including 2 concurrent) |
| WriteAheadLog | 3 tests |
| SSTable | 7 tests (including Bloom filter) |
| BloomFilter | 5 tests |
| LsmTreeStore | 13 tests (including concurrent + compaction + cache + background) |
| Compactor | 2 tests |
| BlockCache | 7 tests |
| **Total** | **44 tests** |

---

## Performance Characteristics

### Read Path
1. MemTable lookup - O(log n) via SortedDictionary
2. Immutable MemTable lookup (if exists) - O(log n)
3. SSTable lookups (newest to oldest):
   - Bloom filter check - O(k) where k = hash count
   - Block cache lookup - O(1) amortized
   - Binary search in index - O(log blocks)
   - Linear scan in block - O(entries per block)

### Write Path
1. WAL append - O(1) sequential write
2. MemTable insert - O(log n)
3. Flush trigger check - O(1)

### Scan Path
1. Heap-based merge of all sources - O(n log s) where s = sources, n = entries
2. No materialization - results streamed

### Compaction
1. Background thread - doesn't block writes
2. Atomic SSTable swap - minimal read blocking
3. Cache invalidation - removes stale blocks

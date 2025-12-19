# LSM-Tree Improvements Plan

## Status Legend
- ? Not Started
- ?? In Progress
- ? Completed
- ? Blocked

---

## Phase 1: Critical Fixes (Required for Production)

### 1.1 Locking Issues
- [x] **MemTable: Fix mixed locking** - Replace `ReaderWriterLockSlim` + `Interlocked` with proper `Lock`
- [x] **LsmTreeStore: Replace `object` lock** - Use `ReaderWriterLockSlim` for SSTable list, `Lock` for writes
- [x] **SSTableReader: Add thread-safe reads** - Lock for FileStream access
- [x] **Add tests for concurrent access** - MemTable and LsmTreeStore concurrent tests

### 1.2 Compaction Integration
- [x] **LsmTreeStore: Integrate Compactor** - Call compaction when L0 threshold reached
- [x] **Atomic SSTable swap** - Replace old tables with compacted ones safely
- [x] **Add tests for compaction** - CompactorMergesSSTablesTest, CompactorRemovesTombstonesTest
- [ ] **Background compaction thread** - Don't block writes during compaction (TODO: Phase 2)

### 1.3 Bloom Filter Integration
- [x] **SSTableBuilder: Add Bloom filter** - Build filter during SSTable creation
- [x] **SSTableReader: Use Bloom filter** - Skip block reads for definite non-matches
- [x] **Serialize Bloom filter in SSTable footer** - New V2 format (44 bytes)
- [x] **Add tests for Bloom filter integration** - SSTableBloomFilterSkipsNonExistentKeysTest, SSTableBloomFilterIntegrationTest

### 1.4 Block Cache
- [ ] **SSTableReader: Add LRU block cache** - Cache recently read blocks
- [ ] **Make cache size configurable** - Add to LsmOptions
- [ ] **Add cache hit/miss statistics**
- [ ] **Add tests for caching behavior**

---

## Phase 2: Performance Optimizations

### 2.1 Scan Improvements
- [ ] **Streaming Scan** - Use merge iterator instead of materializing all entries
- [ ] **MergeIterator: Use PriorityQueue** - O(log n) instead of O(n) for min selection
- [ ] **Add benchmarks for scan operations**

### 2.2 Memory Optimizations
- [ ] **MemTable: Consider skip list** - Better cache locality than SortedDictionary
- [ ] **ArrayPool usage in SSTable** - Reduce allocations during reads
- [ ] **Span-based key comparisons** - Avoid byte[] allocations where possible

### 2.3 Compression
- [ ] **Block compression support** - LZ4 or Snappy
- [ ] **Configurable compression level**
- [ ] **Add compression flag to SSTable format**

### 2.4 Background Operations
- [ ] **Background compaction thread** - Async compaction
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
| Phase 1 | 12 | 11 | 92% |
| Phase 2 | 10 | 0 | 0% |
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

### 1.4 Block Cache (TODO)
LRU cache with configurable size. Key = (filePath, blockIndex).
Should significantly improve point lookup performance.

---

## Test Coverage Summary

| Component | Tests |
|-----------|-------|
| MemTable | 7 tests (including 2 concurrent) |
| WriteAheadLog | 3 tests |
| SSTable | 7 tests (including Bloom filter) |
| BloomFilter | 5 tests |
| LsmTreeStore | 9 tests (including concurrent + compaction) |
| Compactor | 2 tests |
| **Total** | **33 tests** |

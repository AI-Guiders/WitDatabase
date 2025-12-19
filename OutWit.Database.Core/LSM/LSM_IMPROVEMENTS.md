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

## Phase 2: Performance Optimizations ? MOSTLY COMPLETE

### 2.1 Scan Improvements ?
- [x] **Streaming Scan** - Use heap-based merge iterator instead of materializing all entries
- [x] **MergeIterator: Use PriorityQueue** - O(log n) instead of O(n) for min selection
- [x] **Add benchmarks for scan operations** - StoreScanBenchmarks

### 2.2 Memory Optimizations ?
- [ ] **MemTable: Consider skip list** - Deferred: SortedDictionary performs well enough
- [x] **ArrayPool usage in SSTable** - Reduce allocations during encrypted reads
- [x] **ArrayPool usage in WAL** - Reduce allocations during writes and reads
- [x] **Span-based operations** - Use stackalloc and Span<T> where possible

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

## Phase 3: Production Features ? MOSTLY COMPLETE

### 3.1 Level-Based Compaction
- [ ] **Level structure** - L0, L1, L2... with size ratios
- [ ] **Compaction picker** - Choose which files to compact
- [ ] **Tiered vs Leveled strategy option**

### 3.2 Durability & Recovery
- [ ] **WAL checkpointing** - Periodic checkpoints
- [ ] **Manifest file** - Track SSTable metadata
- [ ] **Atomic manifest updates**

### 3.3 Monitoring ?
- [x] **Statistics interface** - LsmStatistics with Gets, Puts, Deletes, Scans, Flushes, Compactions
- [x] **Bytes written/read tracking** - BytesWritten, BytesRead counters
- [x] **Bloom filter efficiency** - BloomFilterHits, BloomFilterMisses, BloomFilterEfficiency
- [x] **Snapshot support** - GetSnapshot() for point-in-time statistics

### 3.4 Integration & Testing ?
- [x] **IKeyValueStore interface** - Full implementation
- [x] **Interface conformance tests** - LsmTreeStoreInterfaceTests via KeyValueStoreTestBase
- [x] **Integration tests** - LsmTreeIntegrationTests with persistence, compaction, cache
- [x] **Production readiness audit** - LSM_AUDIT.md

### 3.5 Benchmarks ?
- [x] **LSM vs BTree Insert benchmarks** - StoreInsertBenchmarks
- [x] **LSM vs BTree Read benchmarks** - StoreReadBenchmarks
- [x] **LSM vs BTree Scan benchmarks** - StoreScanBenchmarks
- [x] **Mixed workload benchmarks** - StoreMixedWorkloadBenchmarks
- [x] **Write-heavy benchmarks** - WriteHeavyBenchmarks

---

## Current Progress

| Phase | Total Items | Completed | Percentage |
|-------|-------------|-----------|------------|
| Phase 1 | 16 | 16 | 100% ? |
| Phase 2 | 14 | 9 | 64% |
| Phase 3 | 17 | 12 | 71% |

**Last Updated**: 2024-12-19

---

## Test Organization

Tests are now organized into separate files:

| File | Tests | Description |
|------|-------|-------------|
| `MemTableTests.cs` | 12 | MemTable unit tests including concurrency |
| `WriteAheadLogTests.cs` | 7 | WAL tests including replay and truncate |
| `SSTableTests.cs` | 11 | SSTable build/read/scan/bloom filter tests |
| `BloomFilterTests.cs` | 9 | Bloom filter serialization and FPR tests |
| `BlockCacheTests.cs` | 11 | LRU cache tests including eviction |
| `CompactorTests.cs` | 8 | Compaction merge and tombstone removal |
| `LsmTreeStoreTests.cs` | 25 | Core LsmTreeStore tests |
| `LsmTreeIntegrationTests.cs` | 39+ | Integration tests via KeyValueStoreTestBase |
| **Total** | **~110** | |

---

## Benchmark Commands

Run LSM vs BTree comparison benchmarks:

```bash
# Quick comparison
dotnet run -c Release --project OutWit.Database.Core.Tests.Benchmarks -- --filter "Store*"

# All benchmarks
dotnet run -c Release --project OutWit.Database.Core.Tests.Benchmarks

# Specific benchmark
dotnet run -c Release --project OutWit.Database.Core.Tests.Benchmarks -- --filter "*WriteHeavy*"
```

Expected results:
- **Write-heavy**: LSM should be 2-5x faster (sequential writes vs random I/O)
- **Read-heavy**: BTree should be slightly faster (single tree lookup vs multiple sources)
- **Mixed workload**: Similar performance (workload dependent)
- **Scans**: BTree slightly faster (single B+Tree vs merge iterator)

---

## Production Recommendations

See `LSM_AUDIT.md` for detailed production readiness analysis.

**Key metrics to monitor:**
```csharp
tree.Statistics.Gets           // Read throughput
tree.Statistics.Puts           // Write throughput
tree.Statistics.BloomFilterEfficiency // Bloom filter effectiveness
tree.BlockCache?.HitRatio      // Cache effectiveness
tree.SSTableCount              // Number of SSTables (compaction health)
```

**Recommended settings:**
```csharp
var options = new LsmOptions
{
    EnableWal = true,              // Durability
    SyncWrites = false,            // Better throughput (async durability)
    MemTableSizeLimit = 8_000_000, // 8MB
    EnableBlockCache = true,
    BlockCacheSizeBytes = 128_000_000, // 128MB
    BackgroundCompaction = true,
    Level0CompactionTrigger = 4
};

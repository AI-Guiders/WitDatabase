# Database Core Roadmap

## ??????? ??????: Phase 3 ? ???????? ??

---

## Phase 1: Critical Fixes (Blocking Issues) ?

### 1.1 Transaction Subsystem Fixes

- [x] **Fix public field `_ownsStore`** ? `private m_ownsStore` ? `TransactionalStore.cs`
- [x] **Delete duplicate `TransactionLog.cs`** - ???????? `WalJournal` ??? encryption
- [x] **Fix blocking async dispose in `Transaction.Commit()`**
  - ????????: `m_asyncLockHandle?.DisposeAsync().AsTask().GetAwaiter().GetResult()` ????? ??????? deadlock
  - ???????: ???????? `ReleaseLocks()` ? `Task.Run().Wait(timeout)` ??? ??????????? ????????????
  - ????: `OutWit.Database.Core/Transactions/Transaction.cs`

### 1.2 Concurrency Subsystem Fixes

- [x] **Unify DatabaseLock sync/async mechanism**
  - ????????: ?????????????? ??? ?????? ????????? - `ReaderWriterLockSlim` ??? sync ? `SemaphoreSlim` ??? async
  - ???????: ????????????? ?? ???? `SemaphoreSlim` (???????????? ??? ??????)
  - ????: `OutWit.Database.Core/Concurrency/DatabaseLock.cs`

---

## Phase 2: WAL Unification ?

### 2.1 Shared Infrastructure

- [x] **Extract common CRC32 calculation to shared utility**
- [x] **Create unified IWriteAheadLog interface**
- [x] **Create WriteAheadLogBase base class**
- [x] **Create unified WriteAheadLog implementation**
- [x] **Create WalTransactionJournal adapter**
- [x] **Refactor LSM WAL to use base class**
- [x] **Delete old WalJournal**

---

## Phase 3: Tests for Concurrency & Transactions ??

### 3.1 Concurrency Tests

- [x] **Create `DatabaseLockTests.cs`** - 25 tests
  - Read/write lock acquisition
  - Multiple readers support
  - Timeout handling
  - Sync/async interoperability
  - Concurrent stress tests
  
- [x] **Create `LockManagerTests.cs`** - 20 tests
  - Basic lock operations
  - Multiple readers
  - Writer blocking
  - Lock release
  - File lock integration
  
- [x] **Create `FileLockTests.cs`** - 15 tests
  - Shared/exclusive locks
  - Lock blocking behavior
  - Lock release and cleanup
  - Timeout handling

### 3.2 Transaction Tests

- [x] **Create `TransactionalStoreTests.cs`** - 25 tests
  - Basic CRUD operations
  - Transaction lifecycle (begin/commit/rollback)
  - Transaction isolation
  - Multiple operations atomicity
  - Error handling
  - Async operations
  - Concurrent transactions

- [x] **Create `WriteAheadLogTests.cs`** (unified WAL) - 20 tests
  - Basic append operations
  - Replay functionality
  - Transaction markers
  - CRC32 integrity
  - Large data handling

### 3.3 Stress Tests

- [x] **Create `TransactionalStoreStressTests.cs`** - 15 tests
  - Sequential transactions
  - Large transactions  
  - Mixed operations
  - Async transactions
  - Edge cases
  
- [ ] **Create `ConcurrentAccessStressTests.cs`** ??
  - Concurrent readers during transaction - BLOCKED (deadlock issue)
  - Multiple process tests - BLOCKED (FileLock reliability)

### 3.4 Benchmarks ?

- [x] **Create `TransactionBenchmarks.cs`**
  - TransactionalStoreBenchmarks
  - LockManagerBenchmarks
  - ConcurrentAccessBenchmarks
  - TransactionCommitBenchmarks

---

## Phase 4: Concurrency Improvements ??

### 4.1 Critical Fixes

- [ ] **Fix reader/writer deadlock** (High Priority)
  - Problem: Concurrent read during transaction can deadlock
  - Solution: Review lock acquisition order

- [ ] **Add writer priority** (High Priority)
  - Problem: Readers can starve writers under heavy load
  - Solution: Implement fair queuing in DatabaseLock

### 4.2 New Features

- [ ] **Read-only transactions** (Medium Priority)
  - Don't acquire write lock for read-only ops
  - Allow concurrent read transactions

- [ ] **Transaction statistics** (Low Priority)
  - Operations count
  - Duration
  - Rollback rate

---

## Phase 5: API Improvements

### 5.1 Options Pattern

- [ ] **Create `TransactionalStoreOptions.cs`**
  - Timeout settings
  - Journal options
  - Lock options

### 5.2 Fluent Builder API

- [ ] **Create `TransactionalStoreBuilder.cs`**

### 5.3 Convenience Extensions

- [ ] **Create `KeyValueStoreExtensions.cs`**

---

## Phase 6: Documentation ? (Partial)

- [x] **Create ARCHITECTURE.md** - Full architecture documentation
- [x] **Create TRANSACTIONS_STATUS.md** - Detailed status report
- [ ] **Update README.md** with transaction examples
- [ ] **Add XML documentation** to public APIs
- [ ] **Create sample project** demonstrating typical usage

---

## Phase 7: Performance Optimizations (Future)

- [ ] **Batch operations support**
- [ ] **MVCC for concurrent reads** (Major effort)
- [ ] **Optimistic concurrency**

---

## Progress Tracking

| Phase | Items | Completed | Progress |
|-------|-------|-----------|----------|
| Phase 1 | 4 | 4 | ? 100% |
| Phase 2 | 7 | 7 | ? 100% |
| Phase 3 | 8 | 7 | ?? 88% |
| Phase 4 | 4 | 0 | ?? 0% |
| Phase 5 | 3 | 0 | 0% |
| Phase 6 | 5 | 2 | ?? 40% |
| Phase 7 | 3 | 0 | 0% |
| **Total** | **34** | **20** | **59%** |

---

## Test Statistics

```
Total Tests:     ~1040 (estimate)
Passing:         ~1035
Skipped:         1 (flaky FileLock test)
Stress Tests:    ~30 new

Test Files Added:
- Concurrency/DatabaseLockTests.cs       ~25 tests
- Concurrency/LockManagerTests.cs        ~20 tests  
- Concurrency/FileLockTests.cs           ~15 tests
- Transactions/TransactionalStoreTests.cs ~25 tests
- Transactions/TransactionalStoreStressTests.cs ~15 tests (NEW)
- Wal/WriteAheadLogTests.cs              ~20 tests

Benchmark Files Added:
- TransactionBenchmarks.cs               4 benchmark classes (NEW)
```

---

## Known Blockers

### Critical

| Issue | Impact | Status |
|-------|--------|--------|
| Concurrent read during tx deadlocks | Can't test concurrent access | ?? Open |

### High

| Issue | Impact | Status |
|-------|--------|--------|
| FileLock unreliable on network FS | Multi-process not safe | ?? Known limitation |
| Writer starvation possible | Perf degradation under load | ?? Open |

---

## Documentation

| Document | Description | Status |
|----------|-------------|--------|
| `ARCHITECTURE.md` | Full system architecture | ? Created |
| `TRANSACTIONS_STATUS.md` | Transaction subsystem status | ? Created |
| `ROADMAP.md` | This file | ? Updated |
| `LSM_AUDIT.md` | LSM-Tree audit | ? Exists |
| `CONCURRENCY_TRANSACTIONS_AUDIT.md` | Initial audit | ? Exists |

---

## Changelog

### 2024-12-20
- ? Created `TransactionalStoreStressTests.cs` (15 tests)
- ? Created `TransactionBenchmarks.cs` (4 benchmark classes)
- ? Created `TRANSACTIONS_STATUS.md`
- ?? Identified concurrent access deadlock issue
- ?? Updated test count estimate: 1026 ? ~1040

### 2024-12-19
- ? Phase 1 completed
- ? Phase 2 completed
- ?? Phase 3 started
- ? Created concurrency tests
- ? Created transaction tests
- ? Created `ARCHITECTURE.md`
- ?? Total tests: 920 ? 1026

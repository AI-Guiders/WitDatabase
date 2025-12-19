# Database Core Roadmap

## ??????? ??????: Phase 2 ???????? ?

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
  - ?????? `OutWit.Database.Core/Utils/Crc32.cs`
  - ???????????? ? LSM WAL ? ????? ??????????????? WAL

- [x] **Create unified IWriteAheadLog interface**
  - ?????? `OutWit.Database.Core/Interfaces/IWriteAheadLog.cs`
  - ???????? `IWalReplayVisitor`, `SimpleWalReplayVisitor`, `TransactionalWalReplayVisitor`

- [x] **Create unified WriteAheadLog implementation**
  - ?????? `OutWit.Database.Core/Wal/WriteAheadLog.cs`
  - ???????????? ??????????, CRC32, ??????????, ArrayPool

- [x] **Create WalTransactionJournal adapter**
  - ?????? `OutWit.Database.Core/Wal/WalTransactionJournal.cs`
  - ??????? IWriteAheadLog ? ITransactionJournal

- [x] **Update LSM WAL to implement IWriteAheadLog**
  - ???????? `OutWit.Database.Core/LSM/WriteAheadLog.cs`
  - ?????????? ????? Crc32
  - ????????? IWriteAheadLog ??? ?????????????

- [x] **Deprecate old WalJournal** 
  - `OutWit.Database.Core/Transactions/WalJournal.cs` ??????? ??? `[Obsolete]`
  - ????????????? ???????????? `WalTransactionJournal`

---

## Phase 3: Tests for Concurrency & Transactions

### 3.1 Concurrency Tests

- [ ] **Create `LockManagerTests.cs`**
  ```
  - ConcurrentReadersAllowedTest
  - WriterBlocksReadersTest  
  - TimeoutOnDeadlockTest
  - AsyncLockAcquisitionTest
  - LockReleaseOnDisposeTest
  ```

- [ ] **Create `DatabaseLockTests.cs`**
  ```
  - ReadLockAllowsMultipleReadersTest
  - WriteLockIsExclusiveTest
  - TimeoutThrowsExceptionTest
  - SyncAndAsyncCanMixTest
  ```

- [ ] **Create `FileLockTests.cs`**
  ```
  - SharedLockAllowsMultipleReadersTest
  - ExclusiveLockBlocksOthersTest
  - ExponentialBackoffWorksTest
  - LockFileCleanupOnDisposeTest
  ```

### 3.2 Transaction Tests

- [ ] **Create `TransactionalStoreTests.cs`**
- [ ] **Create `TransactionTests.cs`**
- [ ] **Create `WriteAheadLogTests.cs`** (for new unified WAL in Wal/)
- [ ] **Create `RollbackJournalTests.cs`**

### 3.3 Integration Tests

- [ ] **Create `TransactionalStoreIntegrationTests.cs`**

---

## Phase 4: API Improvements

### 4.1 Options Pattern

- [ ] **Create `TransactionalStoreOptions.cs`**

### 4.2 Fluent Builder API

- [ ] **Create `TransactionalStoreBuilder.cs`**

### 4.3 Convenience Extensions

- [ ] **Create `KeyValueStoreExtensions.cs`**

---

## Phase 5: Documentation

- [ ] **Update README.md** with transaction examples
- [ ] **Create ARCHITECTURE.md** describing the overall design
- [ ] **Add XML documentation** to public APIs
- [ ] **Create sample project** demonstrating typical usage

---

## Phase 6: Performance Optimizations (Future)

- [ ] **Batch operations support**
- [ ] **Read-only transactions**
- [ ] **Optimistic concurrency**

---

## Progress Tracking

| Phase | Items | Completed | Progress |
|-------|-------|-----------|----------|
| Phase 1 | 4 | 4 | ? 100% |
| Phase 2 | 6 | 6 | ? 100% |
| Phase 3 | 8 | 0 | 0% |
| Phase 4 | 3 | 0 | 0% |
| Phase 5 | 4 | 0 | 0% |
| Phase 6 | 3 | 0 | 0% |
| **Total** | **28** | **10** | **36%** |

---

## New WAL Architecture

### Structure
```
OutWit.Database.Core/
??? Utils/
?   ??? Crc32.cs                 # Shared CRC32 utility
??? Interfaces/
?   ??? IWriteAheadLog.cs        # Unified WAL interface + visitors
??? Wal/
?   ??? WriteAheadLog.cs         # Unified WAL with transactions
?   ??? WalTransactionJournal.cs # ITransactionJournal adapter
??? LSM/
?   ??? WriteAheadLog.cs         # LSM-specific WAL (implements IWriteAheadLog)
??? Transactions/
    ??? WalJournal.cs            # [DEPRECATED] Use WalTransactionJournal
    ??? RollbackJournal.cs       # Rollback journal (keeps old values)
```

### Usage Examples

```csharp
// For LSM (non-transactional):
var lsmWal = new OutWit.Database.Core.LSM.WriteAheadLog("data.wal");
lsmWal.AppendPut(key, value);
lsmWal.Replay(new SimpleWalReplayVisitor(onPut, onDelete));

// For BTree with transactions (new unified WAL):
var wal = new OutWit.Database.Core.Wal.WriteAheadLog("tx.wal");
var journal = new WalTransactionJournal(wal);
var store = new TransactionalStore(btree, journal, lockManager);

// Alternative: direct creation
var journal = new WalTransactionJournal("tx.wal", encryptor: null);
var store = new TransactionalStore(btree, journal, lockManager);
```

---

## Notes

### ????????????? ????????

1. **Interface-first**: ??? ????????? ?????????? ????? ??????????
2. **Composition over inheritance**: Builder/Options ?????? ????????
3. **Explicit configuration**: ??? ?????, ??? ????????????? ????
4. **Testability**: ??? ??????????? ?????????????

### WAL ?????????

| Feature | LSM WAL | Unified WAL | Old WalJournal |
|---------|---------|-------------|----------------|
| Interface | IWriteAheadLog | IWriteAheadLog | ITransactionJournal |
| CRC32 | ? | ? | ? |
| ArrayPool | ? | ? | ? |
| Encryption | ? | ? | ? |
| Transactions | ? | ? | ? |
| Auto-checkpoint | ? | via adapter | ? |
| Status | Active | Active | **DEPRECATED** |

---

## Changelog

### 2024-12-19
- ? Phase 1 completed
- ? Phase 2 completed
- ? Created `Utils/Crc32.cs`
- ? Created `Interfaces/IWriteAheadLog.cs` with visitors
- ? Created `Wal/WriteAheadLog.cs` (unified)
- ? Created `Wal/WalTransactionJournal.cs`
- ? Updated LSM WAL to implement `IWriteAheadLog`
- ? Deprecated old `WalJournal`

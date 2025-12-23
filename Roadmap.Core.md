# OutWit.Database.Core - Roadmap

**Version:** 1.4  
**Based on:** OutWit.Database.Core.TODO.md  
**Last Updated:** 2025-01-17

---

## Legend

| Symbol | Meaning |
|--------|---------|
| [x] | Implemented |
| [ ] | Not implemented |

**Priority Legend:**
- **P0** = Critical (required for ADO.NET/EF Core)
- **P1** = Important (production ready)
- **P2** = Optional (nice-to-have)

---

## Progress Summary

| Category | Implemented | Missing | Progress |
|----------|-------------|---------|----------|
| Key-Value Store | 3 | 0 | 100% |
| Storage Engines | 3 | 0 | 100% |
| Storage Backends | 3 | 0 | 100% |
| Encryption | 3 | 0 | 100% |
| Crash Recovery | 3 | 0 | 100% |
| Basic Concurrency | 4 | 0 | 100% |
| Isolation Levels + MVCC | 4 | 0 | 100% |
| Row-level Locks | 0 | 4 | 0% |
| Savepoints | 4 | 0 | 100% |
| Multiple Result Sets | 3 | 0 | 100% |
| Cursor Support | 0 | 4 | 0% - v2 |
| Query Context | 5 | 0 | 100% |
| Secondary Indexes | 8 | 0 | 100% |
| Storage Detection | 2 | 0 | 100% |
| Bulk Operations | 3 | 0 | 100% |
| Statistics | 2 | 2 | 50% |
| VACUUM/Compaction | 0 | 3 | 0% - v2 |
| Concurrent Transactions | 3 | 0 | 100% |
| ROWVERSION | 3 | 0 | 100% |
| **TOTAL** | **56** | **13** | **81%** |

---

## Recent Changes (v1.4)

### MVCC Integration Complete [x]

| Feature | Status | Description |
|---------|--------|-------------|
| `WitDatabaseBuilder.WithMvcc()` | [x] | Enable MVCC via builder |
| `WitDatabaseBuilder.WithMvcc(IsolationLevel)` | [x] | Enable with custom default isolation |
| `WitDatabase.SupportsMvcc` | [x] | Check if MVCC is enabled |
| `WitDatabase.BeginReadOnlyTransaction()` | [x] | Start lightweight read-only transaction |
| Configurable default isolation level | [x] | Set via builder options |
| Integration tests | [x] | Full test coverage for MVCC scenarios |

```csharp
// Enable MVCC via builder
var db = new WitDatabaseBuilder()
    .WithMemoryStorage()
    .WithBTree()
    .WithMvcc()  // Enables snapshot isolation
    .Build();

Assert.That(db.SupportsMvcc, Is.True);

// Multiple concurrent read transactions
var readTx1 = db.BeginReadOnlyTransaction();
var readTx2 = db.BeginReadOnlyTransaction();

// Both see consistent snapshots
var v1 = readTx1.Get(key);
var v2 = readTx2.Get(key);

// Read during write (snapshot isolation)
var writeTx = db.BeginTransaction();
writeTx.Put(key, newValue);
// readTx1 still sees old value (snapshot isolation)
var oldValue = readTx1.Get(key);

writeTx.Commit();
// readTx1 still sees old value (its snapshot)
// New transactions see new value

readTx1.Dispose();
readTx2.Dispose();

// With custom isolation level
var db2 = new WitDatabaseBuilder()
    .WithFilePath("data.db")
    .WithMvcc(IsolationLevel.Serializable)
    .Build();
```

---

## Recent Changes (v1.3)

### MVCC (Multi-Version Concurrency Control) [x]

| Feature | Status | Description |
|---------|--------|-------------|
| `ITransactionTimestampManager` | [x] | Manages transaction timestamps |
| `TransactionTimestampManager` | [x] | Thread-safe implementation |
| `MvccRecord` | [x] | Versioned record with visibility rules |
| `IMvccStore` | [x] | MVCC key-value store interface |
| `MvccKeyValueStore` | [x] | Multi-version storage wrapper |
| `IMvccTransaction` | [x] | MVCC transaction interface |
| `MvccTransaction` | [x] | Transaction with snapshot isolation |
| `MvccTransactionalStore` | [x] | Transactional store with MVCC |
| Snapshot isolation | [x] | Consistent point-in-time reads |
| Write-write conflict detection | [x] | First-committer-wins semantics |
| Read-only transactions | [x] | Lightweight concurrent reads |
| Version garbage collection | [x] | Cleanup of old versions |
| CommitTimestamp tracking | [x] | Accurate visibility for concurrent commits |

---

## Recent Changes (v1.2)

### Transaction Isolation Levels [x]

| Feature | Status | Description |
|---------|--------|-------------|
| `IsolationLevel` enum | [x] | ReadUncommitted, ReadCommitted, RepeatableRead, Serializable, Snapshot |
| `ITransaction.IsolationLevel` | [x] | Property to get transaction's isolation level |
| `BeginTransaction(IsolationLevel)` | [x] | Create transaction with specific isolation level |

### Multiple Result Sets [x]

| Feature | Status | Description |
|---------|--------|-------------|
| `IMultiResultReader` interface | [x] | Read multiple result sets from batch execution |
| `MultiResultReader` class | [x] | Implementation with ResultSet wrapper |
| `NextResult()` method | [x] | Advance to next result set |
| `IBatchExecutor` interface | [x] | Batch operation execution |
| `BatchExecutor` class | [x] | Execute multiple operations with results |

### Bulk Operations [x]

| Feature | Status | Description |
|---------|--------|-------------|
| `BulkPut` extension | [x] | Insert/update multiple key-value pairs |
| `BulkDelete` extension | [x] | Delete multiple keys |
| `IBulkKeyValueStore` interface | [x] | For stores with native bulk support |
| `StreamingPut` extension | [x] | Batch streaming with progress callback |
| `StreamingPutWithTransaction` | [x] | Transactional streaming with auto-commit |

### Store Statistics [x]

| Feature | Status | Description |
|---------|--------|-------------|
| `IKeyValueStoreStatistics` interface | [x] | Statistics for query optimizer |
| `Count()` extension | [x] | Get total key-value pairs |
| `IsEmpty()` extension | [x] | Check if store is empty |
| `GetStatistics()` extension | [x] | Get statistics wrapper |

### ROWVERSION / Optimistic Concurrency [x]

| Feature | Status | Description |
|---------|--------|-------------|
| `IVersionedKeyValueStore` interface | [x] | Versioned key-value operations |
| `VersionedKeyValueStore` class | [x] | Wrapper adding versions to any store |
| `ConditionalPut` / `ConditionalDelete` | [x] | Optimistic concurrency control |

---

## 1. Existing Components [x]

### 1.1 Key-Value Store Interfaces

| Feature | Status | Description |
|---------|--------|-------------|
| `IKeyValueStore` | [x] | Get, Put, Delete, Scan, Flush |
| `ITransactionalStore` | [x] | BeginTransaction, ACID support |
| `ITransaction` | [x] | Get, Put, Delete, Commit, Rollback |

### 1.2 Storage Engines

| Feature | Status | Description |
|---------|--------|-------------|
| `StoreBTree` | [x] | B+Tree storage, read-optimized |
| `StoreLsm` | [x] | LSM-Tree storage, write-optimized |
| `StoreInMemory` | [x] | In-memory storage for testing |

### 1.3 Concurrency

| Feature | Status | Description |
|---------|--------|-------------|
| Reader-writer locking | [x] | Multiple concurrent readers |
| Writer priority | [x] | Prevents writer starvation |
| File locking | [x] | Multi-process safety |
| MVCC | [x] | Multi-version concurrency control |

### 1.4 Savepoints

| Feature | Status | Description |
|---------|--------|-------------|
| `CreateSavepoint(name)` | [x] | Create named savepoint |
| `RollbackToSavepoint(name)` | [x] | Rollback to savepoint |
| `ReleaseSavepoint(name)` | [x] | Release savepoint |
| Nested savepoints | [x] | Stack-based savepoint management |

### 1.5 Secondary Indexes

| Feature | Status | Description |
|---------|--------|-------------|
| `ISecondaryIndex` interface | [x] | Secondary index operations |
| `SecondaryIndexKeyValueStore` | [x] | Uses any IKeyValueStore |
| Unique index support | [x] | Enforces uniqueness |
| Non-unique index support | [x] | Multiple PKs per index key |
| Index auto-update | [x] | Via IndexManager |
| Index metadata persistence | [x] | Auto-restored on reopen |

---

## 2. Missing Components [ ]

### 2.1 Row-level Locks

| Feature | Status | Priority | TODO Ref |
|---------|--------|----------|----------|
| `RowLockManager` class | [ ] | P0 | SS2.1 |
| `FOR UPDATE` / `FOR SHARE` support | [ ] | P0 | SS2.2 |
| `NOWAIT` / `SKIP LOCKED` modes | [ ] | P1 | SS2.3 |
| Deadlock detection | [ ] | P0 | SS2.4 |

### 2.2 Cursor Support (Deferred to v2)

| Feature | Status | Priority | TODO Ref |
|---------|--------|----------|----------|
| `ICursor` interface | [ ] | P2 - v2 | SS5.1 |
| Forward-only mode | [ ] | P2 - v2 | SS5.2 |
| Scrollable mode | [ ] | P2 - v2 | SS5.2 |
| Fetch size (batching) | [ ] | P2 - v2 | SS5.3 |

### 2.3 Statistics and Metadata

| Feature | Status | Priority | TODO Ref |
|---------|--------|----------|----------|
| Table row count (approximate/exact) | [x] | P1 | SS9.1 |
| Index statistics for query optimizer | [x] | P1 | SS9.2 |
| `ANALYZE` command support | [ ] | P2 - v2 | SS9.3 |
| Column cardinality estimation | [ ] | P2 - v2 | SS9.4 |

### 2.4 VACUUM / Compaction API (Deferred to v2)

| Feature | Status | Priority | TODO Ref |
|---------|--------|----------|----------|
| Explicit `Vacuum()` method for BTree | [ ] | P2 - v2 | SS10.1 |
| Incremental vacuum support | [ ] | P2 - v2 | SS10.2 |
| Compaction progress/status API | [ ] | P2 - v2 | SS10.3 |

---

## 3. Implementation Priorities

### 3.1 Phase 1: MVP for ADO.NET [x]

All Phase 1 features are complete.

### 3.2 Phase 2: EF Core Compatibility [x]

| Feature | Priority | Status |
|---------|----------|--------|
| `IsolationLevel` enum | P0 | [x] Done |
| MVCC implementation | P0 | [x] Done |
| WitDatabase MVCC integration | P0 | [x] Done |
| `RowLockManager` | P0 | [ ] TODO |
| Deadlock detection | P0 | [ ] TODO |
| Multiple concurrent transactions | P0 | [x] Done (MVCC) |
| ROWVERSION support | P1 | [x] Done |

### 3.3 Phase 3: Production Ready

| Feature | Priority | Status |
|---------|----------|--------|
| `IMultiResultReader` | P1 | [x] Done |
| Bulk operations | P1 | [x] Done |
| Statistics | P1 | [x] Done |
| `NOWAIT` / `SKIP LOCKED` | P1 | [ ] TODO |

### 3.4 Phase 4: Nice to Have (Deferred to v2)

| Feature | Priority | Status |
|---------|----------|--------|
| `ICursor` interface | P2 - v2 | [ ] v2 |
| VACUUM API | P2 - v2 | [ ] v2 |
| `ANALYZE` support | P2 - v2 | [ ] v2 |
| Cardinality estimation | P2 - v2 | [ ] v2 |

---

## 4. API Quick Reference

### 4.1 Opening Databases

```csharp
// Create new (BTree by default)
var db = WitDatabase.Create("data.db");
var db = WitDatabase.Create("data.db", password);

// Open existing (auto-detects BTree vs LSM)
var db = WitDatabase.Open("data.db");           // BTree file
var db = WitDatabase.Open("/path/to/lsm_dir"); // LSM directory
var db = WitDatabase.Open(path, password);      // Encrypted

// Create or open (auto-detects for existing)
var db = WitDatabase.CreateOrOpen(path);

// In-memory
var db = WitDatabase.CreateInMemory();

// With MVCC support
var db = new WitDatabaseBuilder()
    .WithFilePath("data.db")
    .WithBTree()
    .WithMvcc()  // Enable MVCC
    .Build();
```

### 4.2 MVCC Transactions

```csharp
// Check MVCC support
if (db.SupportsMvcc)
{
    // Multiple concurrent read transactions
    var readTx1 = db.BeginReadOnlyTransaction();
    var readTx2 = db.BeginReadOnlyTransaction();
    
    // Both see consistent snapshots
    var value1 = readTx1.Get(key);
    var value2 = readTx2.Get(key);
    
    readTx1.Dispose();
    readTx2.Dispose();
}

// Snapshot isolation during writes
var readTx = db.BeginReadOnlyTransaction();
var writeTx = db.BeginTransaction();
writeTx.Put(key, newValue);
writeTx.Commit();
// readTx still sees old value
readTx.Dispose();
```

### 4.3 Indexes

```csharp
var index = db.CreateIndex("idx_email", isUnique: true);
index.Add(emailBytes, userIdBytes);

var userIds = index.Find(emailBytes);
db.DropIndex("idx_email");
```

### 4.4 Transactions with Savepoints

```csharp
using var tx = db.BeginTransaction();
tx.Put(key, value);

if (tx is ITransactionWithSavepoints txSp)
{
    txSp.CreateSavepoint("sp1");
    tx.Put(key2, value2);
    txSp.RollbackToSavepoint("sp1"); // Undo key2
}

tx.Commit(); // Only key is committed
```

---

**Last Updated:** 2025-01-17

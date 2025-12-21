# WitDatabase Roadmap - Part 6: Core Engine Components

**Version:** 1.0  
**Based on:** WitSql.md specification v1.2 and OutWit.Database.Core.TODO.md

---

## Legend

| Symbol | Meaning |
|--------|---------|
| ? | Implemented |
| ?? | Not implemented |
| ?? | Partial |
| N/A | Not applicable for this component |

---

## 1. Core Storage Components (PROJECT_STATUS.md)

### 1.1 Key-Value Store Interfaces

| Feature | Core | Parser | Engine | Notes |
|---------|------|--------|--------|-------|
| `IKeyValueStore` interface | ? | N/A | ?? | Get, Put, Delete, Scan, Flush |
| `ITransactionalStore` interface | ? | N/A | ?? | BeginTransaction, ACID |
| `ITransaction` interface | ? | N/A | ?? | Get, Put, Delete, Commit, Rollback |

### 1.2 Storage Engines

| Feature | Core | Parser | Engine | Notes |
|---------|------|--------|--------|-------|
| `StoreBTree` - B+Tree storage | ? | N/A | ?? | Read-optimized |
| `StoreLsm` - LSM-Tree storage | ? | N/A | ?? | Write-optimized |
| `StoreInMemory` - In-memory storage | ? | N/A | ?? | For testing |

### 1.3 Storage Backends

| Feature | Core | Parser | Engine | Notes |
|---------|------|--------|--------|-------|
| `StorageFile` - File-based | ? | N/A | ?? | Main storage |
| `StorageMemory` - Memory-based | ? | N/A | ?? | For testing |
| `StorageEncrypted` - Encrypted | ? | N/A | ?? | AES-GCM, ChaCha20 |

### 1.4 Encryption

| Feature | Core | Parser | Engine | Notes |
|---------|------|--------|--------|-------|
| AES-256-GCM encryption | ? | N/A | ?? | Hardware accelerated |
| ChaCha20-Poly1305 (BouncyCastle) | ? | N/A | ?? | Blazor WASM compatible |
| Password-based key derivation (PBKDF2) | ? | N/A | ?? | User/password support |

### 1.5 Crash Recovery

| Feature | Core | Parser | Engine | Notes |
|---------|------|--------|--------|-------|
| Write-Ahead Log (WAL) | ? | N/A | ?? | For durability |
| Rollback Journal | ? | N/A | ?? | Alternative to WAL |
| Crash recovery | ? | N/A | ?? | On database open |

### 1.6 Concurrency

| Feature | Core | Parser | Engine | Notes |
|---------|------|--------|--------|-------|
| Reader-writer locking | ? | N/A | ?? | Multiple readers |
| Writer priority | ? | N/A | ?? | Prevent writer starvation |
| File locking | ? | N/A | ?? | Multi-process safety |
| Reentrancy detection | ? | N/A | ?? | Throws LockRecursionException |

---

## 2. Missing Core Components (OutWit.Database.Core.TODO.md)

### 2.1 Transaction Isolation Levels

| Feature | Core | Parser | Engine | Priority | TODO Ref |
|---------|------|--------|--------|----------|----------|
| `IsolationLevel` enum | ?? | ?? | ?? | ?? Critical | §1.1 |
| MVCC (Multi-Version Concurrency Control) | ?? | N/A | ?? | ?? Critical | §1.2 |
| Extend `ITransaction` for isolation level | ?? | N/A | ?? | ?? Critical | §1.3 |
| Record versioning (timestamp/row version) | ?? | N/A | ?? | ?? Critical | §1.4 |

### 2.2 Row-level Locks

| Feature | Core | Parser | Engine | Priority | TODO Ref |
|---------|------|--------|--------|----------|----------|
| `RowLockManager` class | ?? | N/A | ?? | ?? Critical | §2.1 |
| `FOR UPDATE` / `FOR SHARE` support | ?? | ?? | ?? | ?? Critical | §2.2 |
| `NOWAIT` / `SKIP LOCKED` modes | ?? | ?? | ?? | ?? Important | §2.3 |
| Deadlock detection | ?? | N/A | ?? | ?? Critical | §2.4 |

### 2.3 Savepoints

| Feature | Core | Parser | Engine | Priority | TODO Ref |
|---------|------|--------|--------|----------|----------|
| `CreateSavepoint(name)` method | ?? | ? | ?? | ?? Important | §3.1 |
| `RollbackToSavepoint(name)` method | ?? | ? | ?? | ?? Important | §3.2 |
| `ReleaseSavepoint(name)` method | ?? | ? | ?? | ?? Important | §3.3 |
| Nested savepoints (stack) | ?? | N/A | ?? | ?? Important | §3.4 |

### 2.4 Multiple Result Sets

| Feature | Core | Parser | Engine | Priority | TODO Ref |
|---------|------|--------|--------|----------|----------|
| `IMultiResultReader` interface | ?? | N/A | ?? | ?? Important | §4.1 |
| `NextResult()` method | ?? | N/A | ?? | ?? Important | §4.2 |
| Batch execution support | ?? | N/A | ?? | ?? Important | §4.3 |

### 2.5 Cursor Support (Optional)

| Feature | Core | Parser | Engine | Priority | TODO Ref |
|---------|------|--------|--------|----------|----------|
| `ICursor` interface | ?? | N/A | ?? | ?? Optional | §5.1 |
| Forward-only mode | ?? | N/A | ?? | ?? Optional | §5.2 |
| Scrollable mode | ?? | N/A | ?? | ?? Optional | §5.2 |
| Fetch size (batching) | ?? | N/A | ?? | ?? Optional | §5.3 |

### 2.6 Query Execution Context

| Feature | Core | Parser | Engine | Priority | TODO Ref |
|---------|------|--------|--------|----------|----------|
| `IQueryContext` interface | ?? | N/A | ?? | ?? Critical | §6.1 |
| `AffectedRows` property | ?? | N/A | ?? | ?? Critical | §6.2 |
| `LastInsertId` property | ?? | N/A | ?? | ?? Critical | §6.3 |
| Query timeout support | ?? | N/A | ?? | ?? Critical | §6.4 |
| `CancellationToken` propagation | ?? | N/A | ?? | ?? Critical | §6.5 |

### 2.7 Secondary Indexes

| Feature | Core | Parser | Engine | Priority | TODO Ref |
|---------|------|--------|--------|----------|----------|
| `ISecondaryIndex` interface | ?? | N/A | ?? | ?? Critical | §7.1 |
| B+Tree based secondary indexes | ?? | N/A | ?? | ?? Critical | §7.2 |
| Unique index support | ?? | N/A | ?? | ?? Critical | §7.3 |
| Composite index support | ?? | N/A | ?? | ?? Critical | §7.4 |
| Index maintenance (auto-update) | ?? | N/A | ?? | ?? Critical | §7.5 |

### 2.8 Bulk Operations

| Feature | Core | Parser | Engine | Priority | TODO Ref |
|---------|------|--------|--------|----------|----------|
| `BulkPut(IEnumerable<(key, value)>)` | ?? | N/A | ?? | ?? Important | §8.1 |
| `BulkDelete(IEnumerable<key>)` | ?? | N/A | ?? | ?? Important | §8.2 |
| Streaming insert support | ?? | N/A | ?? | ?? Important | §8.3 |

### 2.9 Statistics and Metadata

| Feature | Core | Parser | Engine | Priority | TODO Ref |
|---------|------|--------|--------|----------|----------|
| Table row count (approximate/exact) | ?? | N/A | ?? | ?? Important | §9.1 |
| Index statistics for query optimizer | ?? | N/A | ?? | ?? Important | §9.2 |
| `ANALYZE` command support | ?? | ?? | ?? | ?? Optional | §9.3 |
| Column cardinality estimation | ?? | N/A | ?? | ?? Optional | §9.4 |

### 2.10 VACUUM / Compaction API

| Feature | Core | Parser | Engine | Priority | TODO Ref |
|---------|------|--------|--------|----------|----------|
| Explicit `Vacuum()` method for BTree | ?? | N/A | ?? | ?? Optional | §10.1 |
| Incremental vacuum support | ?? | N/A | ?? | ?? Optional | §10.2 |
| Compaction progress/status API | ?? | N/A | ?? | ?? Optional | §10.3 |

### 2.11 Concurrent Transactions

| Feature | Core | Parser | Engine | Priority | TODO Ref |
|---------|------|--------|--------|----------|----------|
| Multiple concurrent read transactions | ?? | N/A | ?? | ?? Critical | §11.1 |
| Read transactions during write (MVCC) | ?? | N/A | ?? | ?? Critical | §11.2 |
| Transaction wait queue with priorities | ?? | N/A | ?? | ?? Critical | §11.3 |

### 2.12 ROWVERSION / Concurrency Tokens

| Feature | Core | Parser | Engine | Priority | TODO Ref |
|---------|------|--------|--------|----------|----------|
| Auto-incrementing row version support | ?? | ? | ?? | ?? Important | §12.1 |
| Optimistic concurrency check at kernel | ?? | N/A | ?? | ?? Important | §12.2 |
| Conditional Put/Delete (version check) | ?? | N/A | ?? | ?? Important | §12.3 |

---

## 3. Summary - Part 6

### Existing Core Components (?)

| Category | Count | Status |
|----------|-------|--------|
| Key-Value Store Interfaces | 3 | ? Complete |
| Storage Engines | 3 | ? Complete |
| Storage Backends | 3 | ? Complete |
| Encryption | 3 | ? Complete |
| Crash Recovery | 3 | ? Complete |
| Concurrency | 4 | ? Complete |

**Total Existing:** 19 components

### Missing Core Components (??)

| Category | Count | Priority | Status |
|----------|-------|----------|--------|
| Transaction Isolation Levels | 4 | ?? Critical | ?? Not started |
| Row-level Locks | 4 | ?? Critical | ?? Not started |
| Savepoints | 4 | ?? Important | ?? Core missing (Parser ?) |
| Multiple Result Sets | 3 | ?? Important | ?? Not started |
| Cursor Support | 4 | ?? Optional | ?? Not started |
| Query Execution Context | 5 | ?? Critical | ?? Not started |
| Secondary Indexes | 5 | ?? Critical | ?? Not started |
| Bulk Operations | 3 | ?? Important | ?? Not started |
| Statistics and Metadata | 4 | ?? Important | ?? Not started |
| VACUUM / Compaction API | 3 | ?? Optional | ?? Not started |
| Concurrent Transactions | 3 | ?? Critical | ?? Not started |
| ROWVERSION Support | 3 | ?? Important | ?? Not started |

**Total Missing:** 45 components

### Priority Breakdown

| Priority | Count | Description |
|----------|-------|-------------|
| ?? Critical | 21 | Required for ADO.NET/EF Core |
| ?? Important | 17 | Production ready features |
| ?? Optional | 7 | Nice-to-have features |

---

*Continue to [Roadmap.part7.md](Roadmap.part7.md) for Summary and Implementation Priorities*

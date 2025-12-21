# OutWit.Database.Core - TODO for SQL Engine

Analysis of the `OutWit.Database.Core` kernel for compliance with WitSql specifications to build a full-featured SQL engine with ADO.NET and EF Core support.

## Core Components Status

| Component | Status | Description |
|-----------|--------|-------------|
| IKeyValueStore | ? Done | Get, Put, Delete, Scan, Flush |
| ITransactionalStore | ? Done | BeginTransaction, ACID |
| ITransaction | ? Done | Get, Put, Delete, Commit, Rollback |
| LockManager | ? Done | Read/Write locks |
| WAL/RollbackJournal | ? Done | Crash recovery |
| StoreBTree | ? Done | B+Tree storage |
| StoreLsm | ? Done | LSM-Tree storage |
| Encryption | ? Done | AES-GCM, ChaCha20 |

---

## Missing Components

### Category 1: Transaction Isolation Levels

- [ ] **1.1** `IsolationLevel` enum (ReadUncommitted, ReadCommitted, RepeatableRead, Serializable, Snapshot)
- [ ] **1.2** MVCC (Multi-Version Concurrency Control) for Snapshot isolation
- [ ] **1.3** Extend `ITransaction` to specify isolation level
- [ ] **1.4** Record versioning (transaction timestamp / row version)

### Category 2: Row-level Locks

- [ ] **2.1** `RowLockManager` - row-level locks (not just database-level)
- [ ] **2.2** `FOR UPDATE` / `FOR SHARE` - shared/exclusive row locks
- [ ] **2.3** `NOWAIT` / `SKIP LOCKED` - non-blocking lock modes
- [ ] **2.4** Deadlock detection

### Category 3: Savepoints

- [ ] **3.1** Extend `ITransaction` for `CreateSavepoint(name)`
- [ ] **3.2** `RollbackToSavepoint(name)` - partial rollback
- [ ] **3.3** `ReleaseSavepoint(name)` - release savepoint
- [ ] **3.4** Nested savepoints (savepoint stack)

### Category 4: Multiple Result Sets

- [ ] **4.1** `IMultiResultReader` - reading multiple result sets
- [ ] **4.2** `NextResult()` - move to next result set
- [ ] **4.3** Batch execution support

### Category 5: Cursor Support (Optional)

- [ ] **5.1** `ICursor` interface for scrollable cursors
- [ ] **5.2** Forward-only and scrollable modes
- [ ] **5.3** Fetch size (batching)

### Category 6: Query Execution Context

- [ ] **6.1** `IQueryContext` - query execution context
- [ ] **6.2** `AffectedRows` - number of affected rows
- [ ] **6.3** `LastInsertId` - last auto-increment ID
- [ ] **6.4** Query timeout support
- [ ] **6.5** Query cancellation (`CancellationToken` propagation)

### Category 7: Secondary Indexes

- [ ] **7.1** `ISecondaryIndex` interface
- [ ] **7.2** B+Tree based secondary indexes
- [ ] **7.3** Unique index support
- [ ] **7.4** Composite index support
- [ ] **7.5** Index maintenance (auto-update on Put/Delete)

### Category 8: Bulk Operations

- [ ] **8.1** `BulkPut(IEnumerable<(key, value)>)` - batch insert
- [ ] **8.2** `BulkDelete(IEnumerable<key>)` - batch delete
- [ ] **8.3** Streaming insert support

### Category 9: Statistics and Metadata

- [ ] **9.1** Table row count (approximate/exact)
- [ ] **9.2** Index statistics for query optimizer
- [ ] **9.3** `ANALYZE` command support
- [ ] **9.4** Column cardinality estimation

### Category 10: VACUUM / Compaction API

- [ ] **10.1** Explicit `Vacuum()` method for BTree
- [ ] **10.2** Incremental vacuum support
- [ ] **10.3** Compaction progress/status API

### Category 11: Concurrent Transactions

- [ ] **11.1** Multiple concurrent read transactions
- [ ] **11.2** Read transactions during write transaction (MVCC)
- [ ] **11.3** Transaction wait queue with priorities

### Category 12: ROWVERSION / Concurrency Tokens

- [ ] **12.1** Auto-incrementing row version column support
- [ ] **12.2** Optimistic concurrency check at kernel level
- [ ] **12.3** Conditional Put/Delete (version check)

---

## Implementation Priorities

### MVP (Minimum for ADO.NET)

| # | Component | Priority |
|---|-----------|----------|
| 6.1-6.5 | Query execution context | ?? Critical |
| 7.1-7.5 | Secondary indexes | ?? Critical |
| 3.1-3.4 | Savepoints | ?? Important |
| 9.1-9.2 | Basic statistics | ?? Important |

### Production Ready (Full EF Core Support)

| # | Component | Priority |
|---|-----------|----------|
| 1.1-1.4 | Isolation levels + MVCC | ?? Critical |
| 2.1-2.4 | Row-level locks | ?? Critical |
| 4.1-4.3 | Multiple result sets | ?? Important |
| 8.1-8.3 | Bulk operations | ?? Important |
| 11.1-11.3 | Concurrent transactions | ?? Critical |
| 12.1-12.3 | ROWVERSION support | ?? Important |

### Nice to Have

| # | Component | Priority |
|---|-----------|----------|
| 5.1-5.3 | Cursor support | ?? Optional |
| 9.3-9.4 | Advanced statistics | ?? Optional |
| 10.1-10.3 | VACUUM API | ?? Optional |

---

## Notes

1. **Secondary indexes** - critical for any SQL engine. Without them, efficient filtering and JOIN operations are impossible.

2. **MVCC** - required for Snapshot isolation and concurrent reads. Without it, EF Core cannot work in multi-user scenarios.

3. **Savepoints** - used by EF Core for nested transactions and SaveChanges with retry.

4. **Query execution context** - ADO.NET requires information about affected rows count and last insert id.

5. **Current state** is sufficient for MVP SQL engine with basic operations, but for a production-ready system, implementation of items from categories 1-4, 7, 11 is required.

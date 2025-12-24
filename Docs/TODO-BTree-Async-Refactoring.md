# TODO: Full Async BTree Refactoring for WASM Support

## Problem Statement

Current BTree implementation uses synchronous I/O operations internally, which causes `PlatformNotSupportedException: Cannot wait on monitors on this runtime` in Blazor WebAssembly when using IndexedDB storage.

### Root Cause
- `StorageIndexedDb` sync methods use `.GetAwaiter().GetResult()` which deadlocks in WASM single-threaded environment
- BTree operations (Insert, Delete, Split, Merge) call `PageManager.AllocatePage()` synchronously
- Page cache (`PageCacheShardedClock`) loads pages synchronously via `IStorage.ReadPage()`

### Current State ? COMPLETE FOR CORE OPERATIONS
- ? `BTree.CreateAsync()` - async tree creation
- ? `PageManager.AllocatePageAsync()` - async page allocation
- ? `PageManager.CreateAsync()` - async initialization
- ? `StoreBTree.CreateAsync()` - async store creation
- ? `IStorage.SetSizeAsync()` - default interface method
- ? BTree operations (Search, Insert, Upsert, Delete, Range) now have async versions
- ? Page cache async methods (GetPageAsync, CreatePageAsync, EvictAsync)
- ? Node splits/merges now have async versions
- ? StoreBTree async methods use true async BTree operations
- ? TransactionalStore async operations use true async store operations

---

## Phase 1: Async Page Cache (Priority: HIGH) ? COMPLETED

### 1.1 Add async methods to `IPageCache` ?
### 1.2 Implement in `PageCacheShardedClock` ?
### 1.3 Implement in `PageCacheLru` ?
### 1.4 Tests ? - 19 tests passing

### Files modified:
- `OutWit.Database.Core\Interfaces\IPageCache.cs` ?
- `OutWit.Database.Core\Cache\PageCacheShardedClock.cs` ?
- `OutWit.Database.Core\Cache\PageCacheLru.cs` ?
- `OutWit.Database.Core.Tests\Cache\PageCacheAsyncTests.cs` ?

---

## Phase 2: Async PageManager Operations (Priority: HIGH) ? COMPLETED

### Files modified:
- `OutWit.Database.Core\Managers\PageManager.cs` ?

---

## Phase 3: Async BTree Operations (Priority: HIGH) ? COMPLETED

### 3.1 Core async methods ?
- `SearchAsync`, `ContainsKeyAsync`
- `InsertAsync`, `UpsertAsync`
- `DeleteAsync`
- `GetAllAsync`, `GetRangeAsync`, `GetRangeInclusiveAsync`

### 3.2 Internal async helpers ?
- Split operations (SplitLeafAsync, SplitInternalAsync, PropagateSplitUpAsync)
- Tree navigation (FindLeafInfoAsync, FindLeftmostLeafAsync)
- Update operations (UpdateValueAsync)

### 3.3 Tests ? - 16 tests passing

### Files modified:
- `OutWit.Database.Core\Tree\BTree.Search.cs` ?
- `OutWit.Database.Core\Tree\BTree.Insert.cs` ?
- `OutWit.Database.Core\Tree\BTree.Delete.cs` ?
- `OutWit.Database.Core\Tree\BTree.RangeScan.cs` ?
- `OutWit.Database.Core\Tree\BTree.Update.cs` ?
- `OutWit.Database.Core.Tests\Tree\BTreeAsyncTests.cs` ?

---

## Phase 4: Async StoreBTree (Priority: HIGH) ? COMPLETED

### 4.1 Updated async implementations ?
- `GetAsync` ? uses `m_tree.SearchAsync()`
- `PutAsync` ? uses `m_tree.UpsertAsync()`
- `DeleteAsync` ? uses `m_tree.DeleteAsync()`
- `ScanAsync` ? uses `m_tree.GetRangeAsync()`
- `ScanInclusiveAsync` ? uses `m_tree.GetRangeInclusiveAsync()` (NEW)
- `ContainsKeyAsync` ? uses `m_tree.ContainsKeyAsync()` (NEW)

### Files modified:
- `OutWit.Database.Core\Stores\StoreBTree.cs` ?

---

## Phase 5: Async Overflow Page Manager (Priority: MEDIUM) ? COMPLETED

### 5.1 Add async methods ?
- `StoreOverflowAsync`, `ReadOverflowAsync`, `FreeOverflowAsync`, `GetOverflowInfoAsync`

### Files modified:
- `OutWit.Database.Core\Managers\PageManagerOverflow.cs` ?

---

## Phase 6: Async Transactions (Priority: MEDIUM) ? COMPLETED

### 6.1 TransactionalStore async operations ?
- `GetAsync` ? uses `m_store.GetAsync()`
- `PutAsync` ? uses `m_store.GetAsync()` + `m_store.PutAsync()`
- `DeleteAsync` ? uses `m_store.GetAsync()` + `m_store.DeleteAsync()`
- `ScanAsync` ? uses `m_store.ScanAsync()` (IAsyncEnumerable)

### 6.2 Transaction async operations ?
- `GetAsync` ? uses `m_store.GetFromStoreAsync()`
- `CommitAsync` ? uses `m_store.PutToStoreAsync()` / `m_store.DeleteFromStoreAsync()`
- Internal store methods for async access

### 6.3 Tests ? - 18 tests passing

### Files modified:
- `OutWit.Database.Core\Transactions\TransactionalStore.cs` ?
- `OutWit.Database.Core\Transactions\Transaction.cs` ?
- `OutWit.Database.Core.Tests\Transactions\TransactionalStoreAsyncTests.cs` ? (NEW)

### Note on MvccTransactionalStore:
MvccTransaction uses in-memory MVCC versioning structures, so sync operations are acceptable.
For full WASM support, the primary path is TransactionalStore (not MVCC).

---

## Phase 7: Testing (Priority: HIGH) ? COMPLETED

### 7.1 Unit tests for async operations
- [x] `PageCacheAsyncTests.cs` - 19 tests ?
- [x] `BTreeAsyncTests.cs` - 16 tests ?
- [x] `StoreBTreeAsyncTests.cs` - existing tests pass with updated async ?
- [x] `TransactionalStoreAsyncTests.cs` - 18 tests ? (NEW)

### 7.2 Integration tests (pending)
- [ ] Test in actual Blazor WASM environment
- [ ] Test persistence across page reloads

### Files created:
- `OutWit.Database.Core.Tests\Cache\PageCacheAsyncTests.cs` ?
- `OutWit.Database.Core.Tests\Tree\BTreeAsyncTests.cs` ?
- `OutWit.Database.Core.Tests\Transactions\TransactionalStoreAsyncTests.cs` ?

---

## Phase 8: API Design Decisions ? PENDING

### 8.1 Dual API approach ? IMPLEMENTED
Both sync and async APIs available for backward compatibility.

### 8.2 Storage capability detection (TODO)
### 8.3 Builder configuration (TODO)

---

## Progress Summary

| Phase | Status | Tests |
|-------|--------|-------|
| Phase 1 | ? Complete | 19/19 |
| Phase 2 | ? Complete | - |
| Phase 3 | ? Complete | 16/16 |
| Phase 4 | ? Complete | 37/37 |
| Phase 5 | ? Complete | - |
| Phase 6 | ? Complete | 18/18 |
| Phase 7 | ? Complete | 53 tests |
| Phase 8 | ? Pending | - |

**Total Tests:** 1912 passing (all platforms)
**BTree-specific Tests:** 257 passing
**New Async Tests:** 53 (19 + 16 + 18)

---

## Estimated Effort

| Phase | Complexity | Estimated Time | Actual |
|-------|------------|----------------|--------|
| Phase 1 | Medium | 4-6 hours | ~3 hours ? |
| Phase 2 | Low | 2-3 hours | ~1 hour ? |
| Phase 3 | High | 8-12 hours | ~4 hours ? |
| Phase 4 | Low | 1-2 hours | ~30 min ? |
| Phase 5 | Medium | 3-4 hours | ~1 hour ? |
| Phase 6 | Medium | 4-6 hours | ~1 hour ? |
| Phase 7 | Medium | 6-8 hours | ~3 hours ? |
| Phase 8 | Low | 2-3 hours | - |
| **Total** | | **30-44 hours** | ~13.5 hours done |

---

## Notes

- Keep sync methods for backward compatibility with file/memory storage ?
- Use `ConfigureAwait(false)` everywhere in library code ?
- `IAsyncEnumerable` for range scans ?
- BTreeNode is `ref struct` - must recreate after await boundaries ?
- Test on actual WASM to verify no blocking calls remain (pending)

---

## API Summary for WASM Usage

```csharp
// Create database asynchronously (required for WASM)
var db = await new WitDatabaseBuilder()
    .WithIndexedDb(jsRuntime, "mydb")
    .BuildAsync();

// Create store asynchronously (required for WASM)
await using var store = await StoreBTree.CreateAsync(storage);

// All operations have async variants
await store.PutAsync(key, value, ct);
var value = await store.GetAsync(key, ct);
var exists = await store.ContainsKeyAsync(key, ct);
var deleted = await store.DeleteAsync(key, ct);

// Async iteration
await foreach (var (k, v) in store.ScanAsync(start, end, ct))
{
    // process entries
}

await store.FlushAsync(ct);

// Transactions with async operations
await using var tx = await db.BeginTransactionAsync();
await tx.PutAsync(key, value);
var val = await tx.GetAsync(key);
await tx.CommitAsync();
```

---

## Remaining Work

1. **Phase 8: API Design** - Storage capability detection, builder helpers
2. **Integration Testing** - Test in actual Blazor WASM environment
3. **Documentation** - Update README with WASM usage examples

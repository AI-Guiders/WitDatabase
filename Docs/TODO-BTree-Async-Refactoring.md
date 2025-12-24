# TODO: Full Async BTree Refactoring for WASM Support

## Problem Statement

Current BTree implementation uses synchronous I/O operations internally, which causes `PlatformNotSupportedException: Cannot wait on monitors on this runtime` in Blazor WebAssembly when using IndexedDB storage.

### Root Cause
- `StorageIndexedDb` sync methods use `.GetAwaiter().GetResult()` which deadlocks in WASM single-threaded environment
- BTree operations (Insert, Delete, Split, Merge) call `PageManager.AllocatePage()` synchronously
- Page cache (`PageCacheShardedClock`) loads pages synchronously via `IStorage.ReadPage()`

### Current State ? COMPLETE
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
- ? Storage capability detection (`IAsyncOnlyStorage`, `RequiresAsyncBuild()`)
- ? Builder validation for async-only storage

---

## Phase 1: Async Page Cache (Priority: HIGH) ? COMPLETED

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

### Files modified:
- `OutWit.Database.Core\Tree\BTree.Search.cs` ?
- `OutWit.Database.Core\Tree\BTree.Insert.cs` ?
- `OutWit.Database.Core\Tree\BTree.Delete.cs` ?
- `OutWit.Database.Core\Tree\BTree.RangeScan.cs` ?
- `OutWit.Database.Core\Tree\BTree.Update.cs` ?
- `OutWit.Database.Core.Tests\Tree\BTreeAsyncTests.cs` ?

---

## Phase 4: Async StoreBTree (Priority: HIGH) ? COMPLETED

### Files modified:
- `OutWit.Database.Core\Stores\StoreBTree.cs` ?

---

## Phase 5: Async Overflow Page Manager (Priority: MEDIUM) ? COMPLETED

### Files modified:
- `OutWit.Database.Core\Managers\PageManagerOverflow.cs` ?

---

## Phase 6: Async Transactions (Priority: MEDIUM) ? COMPLETED

### Files modified:
- `OutWit.Database.Core\Transactions\TransactionalStore.cs` ?
- `OutWit.Database.Core\Transactions\Transaction.cs` ?
- `OutWit.Database.Core.Tests\Transactions\TransactionalStoreAsyncTests.cs` ? (NEW)

---

## Phase 7: Testing (Priority: HIGH) ? COMPLETED

### Files created:
- `OutWit.Database.Core.Tests\Cache\PageCacheAsyncTests.cs` ? - 19 tests
- `OutWit.Database.Core.Tests\Tree\BTreeAsyncTests.cs` ? - 16 tests
- `OutWit.Database.Core.Tests\Transactions\TransactionalStoreAsyncTests.cs` ? - 18 tests
- `OutWit.Database.Core.Tests\Builder\WitDatabaseBuilderCapabilityTests.cs` ? - 13 tests

---

## Phase 8: API Design Decisions ? COMPLETED

### 8.1 Dual API approach ? IMPLEMENTED
Both sync and async APIs available for backward compatibility.

### 8.2 Storage capability detection ? IMPLEMENTED
- `IAsyncOnlyStorage` interface for storage that requires async operations
- `RequiresAsyncBuild()` extension method on builder
- `SupportsAsyncInitialization()` extension method
- `GetStorageProviderKey()` extension method

### 8.3 Builder validation ? IMPLEMENTED
- `Build()` throws `InvalidOperationException` for async-only storage
- Clear error message directing to use `BuildAsync()`
- Works automatically with IndexedDB and any `IAsyncOnlyStorage`

### Files modified:
- `OutWit.Database.Core\Interfaces\IAsyncOnlyStorage.cs` ? (NEW)
- `OutWit.Database.Core\Builder\WitDatabaseBuilder.cs` ?
- `OutWit.Database.Core\Builder\WitDatabaseBuilderExtensions.cs` ?
- `OutWit.Database.Core.IndexedDb\StorageIndexedDb.cs` ?

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
| Phase 7 | ? Complete | 66 tests |
| Phase 8 | ? Complete | 13/13 |

**Total Tests:** 1925 passing (all platforms)
**BTree-specific Tests:** 257 passing
**New Async Tests:** 66 (19 + 16 + 18 + 13)

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
| Phase 8 | Low | 2-3 hours | ~1.5 hours ? |
| **Total** | | **30-44 hours** | ~15 hours ? |

---

## Notes

- Keep sync methods for backward compatibility with file/memory storage ?
- Use `ConfigureAwait(false)` everywhere in library code ?
- `IAsyncEnumerable` for range scans ?
- BTreeNode is `ref struct` - must recreate after await boundaries ?
- Automatic detection of async-only storage ?

---

## API Summary for WASM Usage

```csharp
// Check if async build is required
var builder = new WitDatabaseBuilder()
    .WithIndexedDbStorage("MyDatabase", JSRuntime);

if (builder.RequiresAsyncBuild())
{
    // Must use async
    var db = await builder.BuildAsync();
}

// Typical Blazor WASM usage
@inject IJSRuntime JSRuntime

var db = await new WitDatabaseBuilder()
    .WithIndexedDbStorage("MyDatabase", JSRuntime)
    .WithBTree()
    .WithTransactions()
    .BuildAsync();  // Required for WASM

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

## ? REFACTORING COMPLETE

All phases completed. The BTree and related components now fully support async operations
for Blazor WebAssembly with IndexedDB storage.

### Key Features:
1. **Full async API** - All operations have async variants
2. **Automatic detection** - Builder detects async-only storage and validates
3. **Backward compatible** - Sync API still works for file/memory storage
4. **66 new tests** - Comprehensive async test coverage
5. **Clear error messages** - Guides users to use BuildAsync() when required

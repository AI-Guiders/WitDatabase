# TODO: Full Async BTree Refactoring for WASM Support

## Problem Statement

Current BTree implementation uses synchronous I/O operations internally, which causes `PlatformNotSupportedException: Cannot wait on monitors on this runtime` in Blazor WebAssembly when using IndexedDB storage.

### Root Cause
- `StorageIndexedDb` sync methods use `.GetAwaiter().GetResult()` which deadlocks in WASM single-threaded environment
- BTree operations (Insert, Delete, Split, Merge) call `PageManager.AllocatePage()` synchronously
- Page cache (`PageCacheShardedClock`) loads pages synchronously via `IStorage.ReadPage()`

### Current State (Partial Fix)
- ? `BTree.CreateAsync()` - async tree creation
- ? `PageManager.AllocatePageAsync()` - async page allocation
- ? `PageManager.CreateAsync()` - async initialization
- ? `StoreBTree.CreateAsync()` - async store creation
- ? `IStorage.SetSizeAsync()` - default interface method
- ? BTree operations (Upsert, Delete) still sync
- ? Page cache async methods (GetPageAsync, CreatePageAsync, EvictAsync)
- ? Node splits/merges still sync

---

## Phase 1: Async Page Cache (Priority: HIGH) ? COMPLETED

### 1.1 Add async methods to `IPageCache` ?
```csharp
// OutWit.Database.Core\Interfaces\IPageCache.cs
ValueTask<CachedPage> GetPageAsync(long pageNumber, CancellationToken ct = default);
ValueTask<CachedPage> CreatePageAsync(long pageNumber, CancellationToken ct = default);
ValueTask EvictAsync(long pageNumber, CancellationToken ct = default);
```

### 1.2 Implement in `PageCacheShardedClock` ?
- [x] Add `GetPageAsync()` - async page loading from storage
- [x] Add `CreatePageAsync()` - async page creation
- [x] Add `EvictAsync()` - async page eviction
- [x] Handle concurrent access properly (SemaphoreSlim for async locks)

### 1.3 Implement in `PageCacheLru` ?
- [x] Same async methods as ShardedClock

### 1.4 Tests ?
- [x] `PageCacheAsyncTests.cs` - 19 tests passing

### Files modified:
- `OutWit.Database.Core\Interfaces\IPageCache.cs` ?
- `OutWit.Database.Core\Cache\PageCacheShardedClock.cs` ?
- `OutWit.Database.Core\Cache\PageCacheLru.cs` ?
- `OutWit.Database.Core.Tests\Cache\PageCacheAsyncTests.cs` ? (created)

---

## Phase 2: Async PageManager Operations (Priority: HIGH) ? COMPLETED

### 2.1 Add async methods to `PageManager` ?
```csharp
// Already done:
ValueTask<(uint, CachedPage)> AllocatePageAsync(PageType, CancellationToken); ?

// Added:
ValueTask<CachedPage> GetPageAsync(uint pageNumber, CancellationToken ct = default); ?
ValueTask FreePageAsync(uint pageNumber, CancellationToken ct = default); ?
```

### Files modified:
- `OutWit.Database.Core\Managers\PageManager.cs` ?

---

## Phase 3: Async BTree Operations (Priority: HIGH) ?? IN PROGRESS

### 3.1 Core async methods
```csharp
// OutWit.Database.Core\Tree\BTree.cs
ValueTask<byte[]?> SearchAsync(ReadOnlyMemory<byte> key, CancellationToken ct = default);
ValueTask UpsertAsync(ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value, CancellationToken ct = default);
ValueTask<bool> DeleteAsync(ReadOnlyMemory<byte> key, CancellationToken ct = default);
IAsyncEnumerable<(byte[] Key, byte[] Value)> GetRangeAsync(byte[]? start, byte[]? end, CancellationToken ct = default);
```

### 3.2 Internal async helpers
```csharp
// BTree.Insert.cs
private ValueTask<uint> CreateLeafNodeAsync(CancellationToken ct);
private ValueTask<uint> CreateInternalNodeAsync(CancellationToken ct);
private ValueTask SplitChildAsync(uint parentPage, int childIndex, CancellationToken ct);
private ValueTask InsertNonFullAsync(uint pageNumber, byte[] key, byte[] value, CancellationToken ct);

// BTree.Delete.cs
private ValueTask MergeNodesAsync(uint leftPage, uint rightPage, CancellationToken ct);
private ValueTask RebalanceAfterDeleteAsync(uint pageNumber, CancellationToken ct);
```

### 3.3 File organization
- `BTree.cs` - Core + CreateAsync (done)
- `BTree.Search.cs` - Add SearchAsync
- `BTree.Insert.cs` - Add UpsertAsync, SplitChildAsync, etc.
- `BTree.Delete.cs` - Add DeleteAsync, MergeNodesAsync, etc.
- `BTree.RangeScan.cs` - Add GetRangeAsync

### Files to modify:
- `OutWit.Database.Core\Tree\BTree.cs`
- `OutWit.Database.Core\Tree\BTree.Search.cs`
- `OutWit.Database.Core\Tree\BTree.Insert.cs`
- `OutWit.Database.Core\Tree\BTree.Delete.cs`
- `OutWit.Database.Core\Tree\BTree.RangeScan.cs`

---

## Phase 4: Async StoreBTree (Priority: HIGH)

### 4.1 Update async implementations
```csharp
// Currently wraps sync methods:
public ValueTask PutAsync(byte[] key, byte[] value, CancellationToken ct)
{
    Put(key, value);  // ? This calls sync BTree.Upsert
    return ValueTask.CompletedTask;
}

// Should be:
public async ValueTask PutAsync(byte[] key, byte[] value, CancellationToken ct)
{
    await m_tree.UpsertAsync(key, value, ct).ConfigureAwait(false);
}
```

### Files to modify:
- `OutWit.Database.Core\Stores\StoreBTree.cs`

---

## Phase 5: Async Overflow Page Manager (Priority: MEDIUM)

### 5.1 Add async methods
```csharp
// OutWit.Database.Core\Managers\PageManagerOverflow.cs
ValueTask<uint> WriteOverflowAsync(ReadOnlyMemory<byte> data, CancellationToken ct);
ValueTask<byte[]> ReadOverflowAsync(uint startPage, int totalLength, CancellationToken ct);
ValueTask FreeOverflowChainAsync(uint startPage, CancellationToken ct);
```

### Files to modify:
- `OutWit.Database.Core\Managers\PageManagerOverflow.cs`

---

## Phase 6: Async Transactions (Priority: MEDIUM)

### 6.1 TransactionalStore async operations
- [ ] Ensure `ITransaction.Put/Delete` work with async BTree
- [ ] Add async commit/rollback if needed

### Files to modify:
- `OutWit.Database.Core\Transactions\TransactionalStore.cs`
- `OutWit.Database.Core\Transactions\Transaction.cs`

---

## Phase 7: Testing (Priority: HIGH)

### 7.1 Unit tests for async operations
- [x] `PageCacheAsyncTests.cs` - Test all async cache operations ? (19 tests)
- [ ] `BTreeAsyncTests.cs` - Test all async BTree operations
- [ ] `StoreBTreeAsyncTests.cs` - Test async store operations
- [ ] `IndexedDbIntegrationTests.cs` - Test with mock IndexedDB

### 7.2 Integration tests
- [ ] Test in actual Blazor WASM environment
- [ ] Test persistence across page reloads
- [ ] Test concurrent operations

### Files to create:
- `OutWit.Database.Core.Tests\Cache\PageCacheAsyncTests.cs` ?
- `OutWit.Database.Core.Tests\Tree\BTreeAsyncTests.cs`
- `OutWit.Database.Core.IndexedDb.Tests\IndexedDbIntegrationTests.cs`

---

## Phase 8: API Design Decisions

### 8.1 Dual API approach
Keep both sync and async APIs for backward compatibility:
```csharp
// Sync (for file/memory storage)
byte[]? Get(ReadOnlySpan<byte> key);
void Put(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value);

// Async (for IndexedDB and other async storages)
ValueTask<byte[]?> GetAsync(byte[] key, CancellationToken ct);
ValueTask PutAsync(byte[] key, byte[] value, CancellationToken ct);
```

### 8.2 Storage capability detection
```csharp
public interface IStorage
{
    // Existing...
    
    /// <summary>
    /// Gets whether this storage requires async operations.
    /// When true, sync methods may throw PlatformNotSupportedException.
    /// </summary>
    bool RequiresAsyncOperations => false;
}
```

### 8.3 Builder configuration
```csharp
var db = await new WitDatabaseBuilder()
    .WithIndexedDbStorage("MyDb", jsRuntime)
    .WithBTree()
    .WithAsyncMode()  // Forces async-only mode, throws on sync calls
    .BuildAsync();
```

---

## Implementation Order

1. **Phase 1** - Async Page Cache (foundation for everything) ? DONE
2. **Phase 2** - Async PageManager (depends on Phase 1) ? DONE
3. **Phase 3** - Async BTree (depends on Phase 2) ?? IN PROGRESS
4. **Phase 4** - Async StoreBTree (depends on Phase 3)
5. **Phase 7** - Testing (parallel with phases 1-4)
6. **Phase 5** - Async Overflow (can be done later)
7. **Phase 6** - Async Transactions (can be done later)
8. **Phase 8** - API polish (final step)

---

## Progress Summary

| Phase | Status | Tests |
|-------|--------|-------|
| Phase 1 | ? Complete | 19/19 |
| Phase 2 | ? Complete | - |
| Phase 3 | ?? In Progress | - |
| Phase 4 | ? Pending | - |
| Phase 5 | ? Pending | - |
| Phase 6 | ? Pending | - |
| Phase 7 | ?? In Progress | 19 tests |
| Phase 8 | ? Pending | - |

---

## Estimated Effort

| Phase | Complexity | Estimated Time | Actual |
|-------|------------|----------------|--------|
| Phase 1 | Medium | 4-6 hours | ~3 hours ? |
| Phase 2 | Low | 2-3 hours | ~1 hour ? |
| Phase 3 | High | 8-12 hours | - |
| Phase 4 | Low | 1-2 hours | - |
| Phase 5 | Medium | 3-4 hours | - |
| Phase 6 | Medium | 4-6 hours | - |
| Phase 7 | Medium | 6-8 hours | ~1 hour (partial) |
| Phase 8 | Low | 2-3 hours | - |
| **Total** | | **30-44 hours** | ~5 hours done |

---

## Alternative Approaches Considered

### Option A: Web Workers (Rejected)
- Run BTree in Web Worker with sync operations
- Cons: Complex IPC, data serialization overhead, not all browsers support SharedArrayBuffer

### Option B: Pre-allocation (Rejected)
- Pre-allocate pages at startup
- Cons: Wastes memory, doesn't scale, doesn't solve split problem

### Option C: Hybrid sync/async (Current partial solution)
- Async initialization, sync operations
- Cons: Fails when tree needs to grow (splits)

### Option D: Full async (Recommended) ?
- All operations async
- Pros: Works everywhere, clean API, future-proof
- Cons: More work, slight API change

---

## Notes

- Keep sync methods for backward compatibility with file/memory storage
- Use `ConfigureAwait(false)` everywhere in library code
- Consider `IAsyncEnumerable` for range scans
- Test on actual WASM to verify no blocking calls remain
- Document which methods are safe to call in WASM

---

## References

- [Blazor WASM Threading Limitations](https://docs.microsoft.com/en-us/aspnet/core/blazor/webassembly-performance-best-practices)
- [IndexedDB API](https://developer.mozilla.org/en-US/docs/Web/API/IndexedDB_API)
- [ValueTask Best Practices](https://devblogs.microsoft.com/dotnet/understanding-the-whys-whats-and-whens-of-valuetask/)

# Test Project Structure Audit

## Executive Summary

The test project has **good overall coverage** with consistent naming and organization.

**Current Status**: ~1050 tests passing ?

---

## Current Structure

```
OutWit.Database.Core.Tests/
??? Cache/                    # Cache implementations tests
?   ??? CachedPageTests.cs         (302 lines)
?   ??? LruPageCacheTests.cs       (643 lines)
?   ??? ShardedClockCacheTests.cs  (438 lines)
??? Comparers/                # Comparer tests
?   ??? ByteArrayComparerTests.cs  (218 lines)
??? Concurrency/              # Lock and concurrency tests
?   ??? DatabaseLockTests.cs
?   ??? FileLockTests.cs
?   ??? LockManagerTests.cs
??? Encoding/                 # Encoding tests
?   ??? VarIntTests.cs             (273 lines)
??? Encryption/               # Encryption tests ? Well organized
?   ??? AesGcmCryptoProviderTests.cs
?   ??? BlockEncryptorTests.cs
?   ??? EncryptedFileStorageTests.cs
?   ??? EncryptedStorageTests.cs
?   ??? EncryptionStressTests.cs
?   ??? PageEncryptorAuthenticationTests.cs
?   ??? PageEncryptorTests.cs
??? Integration/              # Integration tests ? Good
?   ??? EncryptedStorageIntegrationTests.cs
?   ??? StorageStackIntegrationTests.cs
??? LSM/                      # LSM-Tree tests ? Well organized
?   ??? BloomFilterTests.cs
?   ??? BlockCacheTests.cs
?   ??? CompactorTests.cs
?   ??? LsmTreeIntegrationTests.cs
?   ??? LsmTreeStoreTests.cs
?   ??? LsmTreeStressTests.cs
?   ??? MemTableTests.cs
?   ??? SSTableTests.cs
?   ??? WriteAheadLogTests.cs
??? Managers/                 # Page/Overflow manager tests
?   ??? OverflowPageTests.cs       (471 lines)
?   ??? PageManagerStressTests.cs  (336 lines)
?   ??? PageManagerTests.cs        (639 lines)
??? Pages/                    # Page structure tests
?   ??? PageHeaderTests.cs
?   ??? PageTests.cs               (633 lines)
??? Storage/                  # Storage layer tests
?   ??? FileStorageStressTests.cs
?   ??? FileStorageTests.cs
?   ??? MemoryStorageTests.cs
??? Stores/                   # Key-value store tests ? Cleaned
?   ??? BTreeStoreFileTests.cs     # BTree + FileStorage conformance
?   ??? BTreeStoreMemoryTests.cs   # BTree + MemoryStorage conformance
?   ??? BTreeStoreTests.cs         # BTree-specific tests
?   ??? InMemoryStoreTests.cs      # InMemoryStore tests
?   ??? KeyValueStoreParameterizedTests.cs  # All stores × all scenarios
?   ??? KeyValueStoreTestBase.cs   # Base class for conformance
?   ??? StorageFactories.cs        # Factory infrastructure
??? Transactions/             # Transaction tests
?   ??? TransactionalStoreStressTests.cs
?   ??? TransactionalStoreTests.cs
??? Tree/                     # B-Tree tests
?   ??? BTreeStressTests.cs        (417 lines)
?   ??? BTreeTests.cs              (1111 lines)
??? Wal/                      # Write-ahead log tests
?   ??? WriteAheadLogTests.cs
??? DatabaseHeaderTests.cs    # Root level test
```

---

## Naming Conventions

### File Naming
- ? All test files end with `Tests.cs` (not `Test.cs`)
- ? Pattern: `{ComponentName}Tests.cs`

### Method Naming
- ? All test methods end with `Test`
- ? No underscores in method names
- ? Pattern: `{MethodName}{Scenario}Test` (e.g., `GetNonExistentKeyReturnsNullTest`)

---

## Test Coverage by Component

| Component | Unit Tests | Integration | Stress | Parameterized | Total |
|-----------|------------|-------------|--------|---------------|-------|
| **BTree** | ~50 | 10 | 15 | 68 | ~143 |
| **LSM-Tree** | ~60 | 35 | 41 | 28 | ~164 |
| **Transactions** | ~30 | - | 20 | - | ~50 |
| **Concurrency** | ~40 | - | 15 | - | ~55 |
| **Cache** | ~45 | - | 10 | - | ~55 |
| **Encryption** | ~30 | 15 | 10 | 28 | ~83 |
| **Storage** | ~25 | 15 | 8 | - | ~48 |
| **Other** | ~50 | - | - | - | ~50 |
| **Total** | | | | | **~1050** |

---

## Storage Factory System

The `StorageFactories.cs` provides a unified way to test all `IKeyValueStore` implementations:

```csharp
// Available factory sources:
StorageFactorySource.AllStorages          // All 6 combinations
StorageFactorySource.AllBTreeStorages     // BTree: Memory, File, Encrypted×2
StorageFactorySource.AllLsmStorages       // LSM: Plain, Encrypted
StorageFactorySource.PlainBTreeStorages   // BTree: Memory, File
StorageFactorySource.EncryptedBTreeStorages // BTree: Encrypted×2
```

### Factory Types
| Factory | Store Type | Storage |
|---------|------------|---------|
| `MemoryStorageFactory` | BTreeStore | MemoryStorage |
| `FileStorageFactory` | BTreeStore | FileStorage |
| `EncryptedMemoryStorageFactory` | BTreeStore | EncryptedStorage(Memory) |
| `EncryptedFileStorageFactory` | BTreeStore | EncryptedStorage(File) |
| `LsmStorageFactory` | LsmTreeStore | Directory-based |
| `EncryptedLsmStorageFactory` | LsmTreeStore | Directory + BlockEncryptor |

---

## How to Extend Tests

### Adding tests for a new component

1. **Unit tests**: Create `{Folder}/{Component}Tests.cs`
2. **If implements IKeyValueStore**: 
   - Inherit from `KeyValueStoreTestBase` for conformance
   - Add factory to `StorageFactories.cs` for parameterized tests
3. **Add stress tests**: Create `{Folder}/{Component}StressTests.cs` with `[Category("Stress")]`

### Example: Adding new IKeyValueStore implementation

```csharp
// 1. Create conformance test
[TestFixture]
public class MyNewStoreConformanceTests : KeyValueStoreTestBase
{
    protected override IKeyValueStore CreateStore()
    {
        return new MyNewStore();
    }
    
    // Override if behavior differs from standard
    public override void DeleteNonExistentKeyReturnsFalseTest()
    {
        // Custom assertion for this implementation
    }
}

// 2. Add factory to StorageFactories.cs
public class MyNewStoreFactory : IStorageFactory
{
    public string Name => "MyNewStore";
    public IStorage CreateStorage() => throw new NotSupportedException();
    public IKeyValueStore CreateStore() => new MyNewStore();
    public void Dispose() { }
}

// 3. Add to StorageFactorySource
public static IEnumerable<TestCaseData> AllStorages
{
    get
    {
        // ...existing...
        yield return new TestCaseData(new MyNewStoreFactory()).SetName("{m}(MyNewStore)");
    }
}
```

### Handling Implementation Differences

When implementations behave differently (like LSM's Delete always returning true):

```csharp
// Test only for specific implementations
[Test]
[TestCaseSource(typeof(StorageFactorySource), nameof(StorageFactorySource.AllBTreeStorages))]
public void DeleteNonExistentReturnsFalseTest(IStorageFactory factory) { ... }

[Test]
[TestCaseSource(typeof(StorageFactorySource), nameof(StorageFactorySource.AllLsmStorages))]
public void DeleteNonExistentReturnsTrueForLsmTest(IStorageFactory factory) { ... }
```

---

## Test Categories

```csharp
[Category("Unit")]        // Fast, isolated unit tests
[Category("Integration")] // Multiple components working together
[Category("Stress")]      // Long-running, performance tests
[Category("Slow")]        // Tests taking > 1 second
```

### Running by category:
```bash
# Run only stress tests
dotnet test --filter "Category=Stress"

# Run everything except stress tests
dotnet test --filter "Category!=Stress"

# Run LSM tests only
dotnet test --filter "FullyQualifiedName~LSM"

# Run Transactions tests only
dotnet test --filter "FullyQualifiedName~Transactions"
```

---

## Test Statistics

```
Total Tests:     ~1050
Passing:         ~1050 (100%)
Duration:        ~90 seconds (full run)

By Framework:
- net9.0:        ~1050 tests
- net10.0:       ~1050 tests
```

---

## Audit History

### 2024-12-21
- ? Renamed all test files: `*Test.cs` ? `*Tests.cs` (21 files)
- ? Renamed all test methods to remove underscores and add `Test` suffix
- ? Updated documentation to reflect new structure
- ? Added Transactions and Concurrency sections

### Previous
- ? Deleted empty files: `LSM/LsmTreeTest.cs`, `Stores/KeyValueStoreParameterizedTest.cs`
- ? Added LSM to parameterized tests (`LsmStorageFactory`, `EncryptedLsmStorageFactory`)

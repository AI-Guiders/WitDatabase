using OutWit.Database.Core.Interfaces;
using OutWit.Database.Core.Storage;
using OutWit.Database.Core.Stores;

namespace OutWit.Database.Core.Tests.Stores;

/// <summary>
/// Tests BTreeStore with MemoryStorage using the common IKeyValueStore test suite.
/// </summary>
[TestFixture]
public class BTreeStoreMemoryTest : KeyValueStoreTestBase
{
#pragma warning disable NUnit1032 // Disposed in CleanupStore via base class
    private StorageMemory? m_storage;
#pragma warning restore NUnit1032

    protected override IKeyValueStore CreateStore()
    {
        m_storage = new StorageMemory(4096, 2000);
        return new StoreBTree(m_storage, ownsStorage: false);
    }

    protected override void CleanupStore()
    {
        m_storage?.Dispose();
        m_storage = null;
    }
}

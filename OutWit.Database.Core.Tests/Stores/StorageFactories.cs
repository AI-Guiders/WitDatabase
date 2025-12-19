using OutWit.Database.Core.Interfaces;
using OutWit.Database.Core.Managers;
using OutWit.Database.Core.Storage;
using OutWit.Database.Core.Stores;

namespace OutWit.Database.Core.Tests.Stores;

/// <summary>
/// Factory interface for creating storage instances in tests.
/// </summary>
public interface IStorageFactory : IDisposable
{
    /// <summary>
    /// Gets a descriptive name for the storage type.
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Creates a new storage instance.
    /// </summary>
    IStorage CreateStorage();
    
    /// <summary>
    /// Creates a store using the storage.
    /// </summary>
    IKeyValueStore CreateStore();
}

/// <summary>
/// Factory for creating MemoryStorage-based stores.
/// </summary>
public class MemoryStorageFactory : IStorageFactory
{
    private readonly int m_pageSize;
    private readonly int m_maxPages;
    private readonly int m_cacheSize;
    private readonly List<IDisposable> m_disposables = new();

    public MemoryStorageFactory(int pageSize = 4096, int maxPages = 5000, int cacheSize = 1000)
    {
        m_pageSize = pageSize;
        m_maxPages = maxPages;
        m_cacheSize = cacheSize;
    }

    public string Name => "MemoryStorage";

    public IStorage CreateStorage()
    {
        var storage = new MemoryStorage(m_pageSize, m_maxPages);
        m_disposables.Add(storage);
        return storage;
    }

    public IKeyValueStore CreateStore()
    {
        var storage = CreateStorage();
        var store = new BTreeStore(storage, m_cacheSize, ownsStorage: false);
        m_disposables.Add(store);
        return store;
    }

    public void Dispose()
    {
        foreach (var d in m_disposables.AsEnumerable().Reverse())
        {
            d.Dispose();
        }
        m_disposables.Clear();
    }

    public override string ToString() => Name;
}

/// <summary>
/// Factory for creating FileStorage-based stores.
/// </summary>
public class FileStorageFactory : IStorageFactory
{
    private readonly int m_pageSize;
    private readonly int m_cacheSize;
    private readonly string m_testDir;
    private readonly List<IDisposable> m_disposables = new();
    private readonly List<string> m_filesToDelete = new();

    public FileStorageFactory(int pageSize = 4096, int cacheSize = 1000)
    {
        m_pageSize = pageSize;
        m_cacheSize = cacheSize;
        m_testDir = Path.Combine(Path.GetTempPath(), $"StorageTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(m_testDir);
    }

    public string Name => "FileStorage";

    public IStorage CreateStorage()
    {
        var filePath = Path.Combine(m_testDir, $"test_{Guid.NewGuid():N}.db");
        m_filesToDelete.Add(filePath);
        var storage = new FileStorage(filePath, m_pageSize);
        m_disposables.Add(storage);
        return storage;
    }

    public IKeyValueStore CreateStore()
    {
        var filePath = Path.Combine(m_testDir, $"test_{Guid.NewGuid():N}.db");
        m_filesToDelete.Add(filePath);
        var store = new BTreeStore(filePath, m_pageSize, m_cacheSize);
        m_disposables.Add(store);
        return store;
    }

    public void Dispose()
    {
        foreach (var d in m_disposables.AsEnumerable().Reverse())
        {
            d.Dispose();
        }
        m_disposables.Clear();

        // Cleanup files
        foreach (var file in m_filesToDelete)
        {
            try { if (File.Exists(file)) File.Delete(file); }
            catch { /* Ignore */ }
        }
        m_filesToDelete.Clear();

        try { if (Directory.Exists(m_testDir)) Directory.Delete(m_testDir, true); }
        catch { /* Ignore */ }
    }

    public override string ToString() => Name;
}

/// <summary>
/// Provides storage factory instances for parameterized tests.
/// </summary>
public static class StorageFactorySource
{
    public static IEnumerable<IStorageFactory> AllStorageFactories
    {
        get
        {
            yield return new MemoryStorageFactory();
            yield return new FileStorageFactory();
        }
    }

    public static IEnumerable<TestCaseData> AllStorages
    {
        get
        {
            yield return new TestCaseData(new MemoryStorageFactory()).SetName("{m}(MemoryStorage)");
            yield return new TestCaseData(new FileStorageFactory()).SetName("{m}(FileStorage)");
        }
    }
}

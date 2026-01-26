using Microsoft.JSInterop;
using Moq;
using OutWit.Database.Core.IndexedDb.Indexes;

namespace OutWit.Database.Core.IndexedDb.Tests.Mocks;

/// <summary>
/// Mock IJSRuntime for testing IndexedDB operations without a browser.
/// Simulates IndexedDB behavior in memory.
/// </summary>
public sealed class MockJSRuntime : IJSRuntime
{
    #region Fields

    private readonly Dictionary<string, MockIndexedDatabase> m_databases = new();
    private readonly Lock m_lock = new();

    #endregion

    #region IJSRuntime

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (m_lock)
        {
            var result = ProcessCall(identifier, args);
            
            if (result is TValue typedResult)
            {
                return ValueTask.FromResult(typedResult);
            }
            
            // Handle null returns for nullable types
            if (result == null && default(TValue) == null)
            {
                return ValueTask.FromResult(default(TValue)!);
            }
            
            // Handle void returns (for InvokeVoidAsync)
            if (typeof(TValue) == typeof(object) && result == null)
            {
                return ValueTask.FromResult(default(TValue)!);
            }

            throw new InvalidOperationException(
                $"Expected return type {typeof(TValue).Name} but got {result?.GetType().Name ?? "null"}");
        }
    }

    #endregion

    #region Call Processing

    private object? ProcessCall(string identifier, object?[]? args)
    {
        return identifier switch
        {
            // Storage operations
            "witDb.open" => Open(args),
            "witDb.close" => Close(args),
            "witDb.readPage" => ReadPage(args),
            "witDb.writePage" => WritePage(args),
            "witDb.writePages" => WritePages(args),
            "witDb.getPageCount" => GetPageCount(args),
            "witDb.setPageCount" => SetPageCount(args),
            "witDb.getPageSize" => GetPageSize(args),
            "witDb.setPageSize" => SetPageSize(args),
            "witDb.deleteDatabase" => DeleteDatabase(args),
            "witDb.databaseExists" => DatabaseExists(args),
            "witDb.truncatePages" => TruncatePages(args),
            
            // Index operations
            "witDbIndex.open" => IndexOpen(args),
            "witDbIndex.close" => IndexClose(args),
            "witDbIndex.get" => IndexGet(args),
            "witDbIndex.put" => IndexPut(args),
            "witDbIndex.delete" => IndexDelete(args),
            "witDbIndex.deleteRange" => IndexDeleteRange(args),
            "witDbIndex.scan" => IndexScan(args),
            "witDbIndex.hasAny" => IndexHasAny(args),
            "witDbIndex.count" => IndexCount(args),
            "witDbIndex.clear" => IndexClear(args),
            
            _ => throw new NotImplementedException($"Unknown JS call: {identifier}")
        };
    }

    private object? Open(object?[]? args)
    {
        var dbName = GetArg<string>(args, 0);
        GetOrCreateDatabase(dbName);
        return null;
    }

    private object? Close(object?[]? args)
    {
        // Just ignore close - data stays in memory
        return null;
    }

    private byte[]? ReadPage(object?[]? args)
    {
        var dbName = GetArg<string>(args, 0);
        var pageNumber = GetArg<long>(args, 1);
        
        var db = GetOrCreateDatabase(dbName);
        return db.ReadPage(pageNumber);
    }

    private object? WritePage(object?[]? args)
    {
        var dbName = GetArg<string>(args, 0);
        var pageNumber = GetArg<long>(args, 1);
        var data = GetArg<byte[]>(args, 2);
        
        var db = GetOrCreateDatabase(dbName);
        db.WritePage(pageNumber, data);
        return null;
    }

    private object? WritePages(object?[]? args)
    {
        var dbName = GetArg<string>(args, 0);
        var pages = args?[1];
        
        // Pages come as array of anonymous objects
        // In real scenario this would be JSON, but Moq passes objects
        if (pages is System.Collections.IEnumerable enumerable)
        {
            var db = GetOrCreateDatabase(dbName);
            foreach (var page in enumerable)
            {
                var pageType = page.GetType();
                var pageNumber = (long)pageType.GetProperty("pageNumber")!.GetValue(page)!;
                var data = (byte[])pageType.GetProperty("data")!.GetValue(page)!;
                db.WritePage(pageNumber, data);
            }
        }
        
        return null;
    }

    private long GetPageCount(object?[]? args)
    {
        var dbName = GetArg<string>(args, 0);
        var db = GetOrCreateDatabase(dbName);
        return db.PageCount;
    }

    private object? SetPageCount(object?[]? args)
    {
        var dbName = GetArg<string>(args, 0);
        var count = GetArg<long>(args, 1);
        
        var db = GetOrCreateDatabase(dbName);
        db.PageCount = count;
        return null;
    }

    private int GetPageSize(object?[]? args)
    {
        var dbName = GetArg<string>(args, 0);
        var db = GetOrCreateDatabase(dbName);
        return db.PageSize;
    }

    private object? SetPageSize(object?[]? args)
    {
        var dbName = GetArg<string>(args, 0);
        var pageSize = GetArg<int>(args, 1);
        
        var db = GetOrCreateDatabase(dbName);
        db.PageSize = pageSize;
        return null;
    }

    private object? DeleteDatabase(object?[]? args)
    {
        var dbName = GetArg<string>(args, 0);
        m_databases.Remove(dbName);
        return null;
    }

    private bool DatabaseExists(object?[]? args)
    {
        var dbName = GetArg<string>(args, 0);
        return m_databases.ContainsKey(dbName);
    }

    private object? TruncatePages(object?[]? args)
    {
        var dbName = GetArg<string>(args, 0);
        var newPageCount = GetArg<long>(args, 1);
        
        var db = GetOrCreateDatabase(dbName);
        db.Truncate(newPageCount);
        return null;
    }

    #endregion

    #region Index Operations

    private object? IndexOpen(object?[]? args)
    {
        var dbName = GetArg<string>(args, 0);
        var storeName = GetArg<string>(args, 1);
        GetOrCreateDatabase(dbName).GetOrCreateIndexStore(storeName);
        return null;
    }

    private object? IndexClose(object?[]? args)
    {
        // Just ignore close - data stays in memory
        return null;
    }

    private byte[]? IndexGet(object?[]? args)
    {
        var dbName = GetArg<string>(args, 0);
        var storeName = GetArg<string>(args, 1);
        var key = GetArg<byte[]>(args, 2);
        
        var store = GetOrCreateDatabase(dbName).GetOrCreateIndexStore(storeName);
        return store.Get(key);
    }

    private object? IndexPut(object?[]? args)
    {
        var dbName = GetArg<string>(args, 0);
        var storeName = GetArg<string>(args, 1);
        var key = GetArg<byte[]>(args, 2);
        var value = GetArg<byte[]>(args, 3);
        
        var store = GetOrCreateDatabase(dbName).GetOrCreateIndexStore(storeName);
        store.Put(key, value);
        return null;
    }

    private bool IndexDelete(object?[]? args)
    {
        var dbName = GetArg<string>(args, 0);
        var storeName = GetArg<string>(args, 1);
        var key = GetArg<byte[]>(args, 2);
        
        var store = GetOrCreateDatabase(dbName).GetOrCreateIndexStore(storeName);
        return store.Delete(key);
    }

    private int IndexDeleteRange(object?[]? args)
    {
        var dbName = GetArg<string>(args, 0);
        var storeName = GetArg<string>(args, 1);
        var startKey = GetArg<byte[]>(args, 2);
        var endKey = GetArg<byte[]>(args, 3);
        
        var store = GetOrCreateDatabase(dbName).GetOrCreateIndexStore(storeName);
        return store.DeleteRange(startKey, endKey);
    }

    private IndexedDbIndexEntry[]? IndexScan(object?[]? args)
    {
        var dbName = GetArg<string>(args, 0);
        var storeName = GetArg<string>(args, 1);
        var startKey = GetArgOrNull<byte[]>(args, 2);
        var endKey = GetArgOrNull<byte[]>(args, 3);
        
        var store = GetOrCreateDatabase(dbName).GetOrCreateIndexStore(storeName);
        return store.Scan(startKey, endKey);
    }

    private bool IndexHasAny(object?[]? args)
    {
        var dbName = GetArg<string>(args, 0);
        var storeName = GetArg<string>(args, 1);
        var startKey = GetArg<byte[]>(args, 2);
        var endKey = GetArg<byte[]>(args, 3);
        
        var store = GetOrCreateDatabase(dbName).GetOrCreateIndexStore(storeName);
        return store.HasAny(startKey, endKey);
    }

    private long IndexCount(object?[]? args)
    {
        var dbName = GetArg<string>(args, 0);
        var storeName = GetArg<string>(args, 1);
        
        var store = GetOrCreateDatabase(dbName).GetOrCreateIndexStore(storeName);
        return store.Count;
    }

    private object? IndexClear(object?[]? args)
    {
        var dbName = GetArg<string>(args, 0);
        var storeName = GetArg<string>(args, 1);
        
        var store = GetOrCreateDatabase(dbName).GetOrCreateIndexStore(storeName);
        store.Clear();
        return null;
    }

    #endregion

    #region Tools

    private MockIndexedDatabase GetOrCreateDatabase(string name)
    {
        if (!m_databases.TryGetValue(name, out var db))
        {
            db = new MockIndexedDatabase(name);
            m_databases[name] = db;
        }
        return db;
    }

    private static T GetArg<T>(object?[]? args, int index)
    {
        if (args == null || args.Length <= index)
            throw new ArgumentException($"Missing argument at index {index}");
        
        var value = args[index];
        
        if (value is T typedValue)
            return typedValue;
        
        // Handle numeric conversions
        if (typeof(T) == typeof(long) && value is int intValue)
            return (T)(object)(long)intValue;
        
        if (typeof(T) == typeof(int) && value is long longValue)
            return (T)(object)(int)longValue;
        
        throw new ArgumentException(
            $"Argument at index {index} is {value?.GetType().Name ?? "null"}, expected {typeof(T).Name}");
    }

    private static T? GetArgOrNull<T>(object?[]? args, int index) where T : class
    {
        if (args == null || args.Length <= index)
            return null;
        
        var value = args[index];
        
        if (value is T typedValue)
            return typedValue;
        
        return null;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the mock databases for inspection in tests.
    /// </summary>
    public IReadOnlyDictionary<string, MockIndexedDatabase> Databases => m_databases;

    #endregion
}

/// <summary>
/// Represents a mock IndexedDB database.
/// </summary>
public sealed class MockIndexedDatabase
{
    #region Fields

    private readonly Dictionary<long, byte[]> m_pages = new();
    private readonly Dictionary<string, MockIndexStore> m_indexStores = new();

    #endregion

    #region Constructors

    public MockIndexedDatabase(string name)
    {
        Name = name;
    }

    #endregion

    #region Page Operations

    public byte[]? ReadPage(long pageNumber)
    {
        return m_pages.TryGetValue(pageNumber, out var data) ? data : null;
    }

    public void WritePage(long pageNumber, byte[] data)
    {
        m_pages[pageNumber] = (byte[])data.Clone();
    }

    public void Truncate(long newPageCount)
    {
        var keysToRemove = m_pages.Keys.Where(k => k >= newPageCount).ToList();
        foreach (var key in keysToRemove)
        {
            m_pages.Remove(key);
        }
        PageCount = newPageCount;
    }

    #endregion

    #region Index Store Operations

    public MockIndexStore GetOrCreateIndexStore(string storeName)
    {
        if (!m_indexStores.TryGetValue(storeName, out var store))
        {
            store = new MockIndexStore(storeName);
            m_indexStores[storeName] = store;
        }
        return store;
    }

    #endregion

    #region Properties

    public string Name { get; }
    public long PageCount { get; set; }
    public int PageSize { get; set; }

    /// <summary>
    /// Gets all stored pages for inspection.
    /// </summary>
    public IReadOnlyDictionary<long, byte[]> Pages => m_pages;

    /// <summary>
    /// Gets all index stores for inspection.
    /// </summary>
    public IReadOnlyDictionary<string, MockIndexStore> IndexStores => m_indexStores;

    #endregion
}

/// <summary>
/// Represents a mock IndexedDB object store for index data.
/// </summary>
public sealed class MockIndexStore
{
    #region Fields

    private readonly SortedDictionary<byte[], byte[]> m_data;

    #endregion

    #region Constructors

    public MockIndexStore(string name)
    {
        Name = name;
        m_data = new SortedDictionary<byte[], byte[]>(new ByteArrayComparer());
    }

    #endregion

    #region Operations

    public byte[]? Get(byte[] key)
    {
        return m_data.TryGetValue(key, out var value) ? value : null;
    }

    public void Put(byte[] key, byte[] value)
    {
        m_data[key] = (byte[])value.Clone();
    }

    public bool Delete(byte[] key)
    {
        return m_data.Remove(key);
    }

    public int DeleteRange(byte[] startKey, byte[] endKey)
    {
        var keysToDelete = m_data.Keys
            .Where(k => CompareBytes(k, startKey) >= 0 && CompareBytes(k, endKey) < 0)
            .ToList();
        
        foreach (var key in keysToDelete)
        {
            m_data.Remove(key);
        }
        
        return keysToDelete.Count;
    }

    public IndexedDbIndexEntry[] Scan(byte[]? startKey, byte[]? endKey)
    {
        return m_data
            .Where(kv => 
                (startKey == null || CompareBytes(kv.Key, startKey) >= 0) &&
                (endKey == null || CompareBytes(kv.Key, endKey) < 0))
            .Select(kv => new IndexedDbIndexEntry { Key = kv.Key, Value = kv.Value })
            .ToArray();
    }

    public bool HasAny(byte[] startKey, byte[] endKey)
    {
        return m_data.Keys.Any(k => 
            CompareBytes(k, startKey) >= 0 && CompareBytes(k, endKey) < 0);
    }

    public void Clear()
    {
        m_data.Clear();
    }

    private static int CompareBytes(byte[] a, byte[] b)
    {
        if (a == null && b == null) return 0;
        if (a == null) return -1;
        if (b == null) return 1;
        
        var minLen = Math.Min(a.Length, b.Length);
        for (var i = 0; i < minLen; i++)
        {
            if (a[i] != b[i])
                return a[i] - b[i];
        }
        return a.Length - b.Length;
    }

    #endregion

    #region Properties

    public string Name { get; }
    public long Count => m_data.Count;

    /// <summary>
    /// Gets all stored data for inspection.
    /// </summary>
    public IReadOnlyDictionary<byte[], byte[]> Data => m_data;

    #endregion

    #region Helper Types

    private sealed class ByteArrayComparer : IComparer<byte[]>
    {
        public int Compare(byte[]? x, byte[]? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;
            
            var minLen = Math.Min(x.Length, y.Length);
            for (var i = 0; i < minLen; i++)
            {
                if (x[i] != y[i])
                    return x[i] - y[i];
            }
            return x.Length - y.Length;
        }
    }

    #endregion
}

/// <summary>
/// Entry returned by index scan.
/// </summary>
public sealed class MockIndexEntry
{
    public byte[] Key { get; set; } = [];
    public byte[] Value { get; set; } = [];
}

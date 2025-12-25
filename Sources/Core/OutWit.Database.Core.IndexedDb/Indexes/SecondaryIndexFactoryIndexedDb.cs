using Microsoft.JSInterop;
using OutWit.Database.Core.Interfaces;

namespace OutWit.Database.Core.IndexedDb.Indexes;

/// <summary>
/// Factory for creating secondary indexes using IndexedDB storage.
/// Each index gets its own object store within the same IndexedDB database.
/// </summary>
/// <remarks>
/// This factory is designed for Blazor WebAssembly applications where
/// IndexedDB is the only persistent storage option. Each index uses a
/// separate object store, allowing efficient key-value operations.
/// </remarks>
public sealed class SecondaryIndexFactoryIndexedDb : ISecondaryIndexFactory
{
    #region Constants

    /// <summary>
    /// Provider key for IndexedDB index factory.
    /// </summary>
    public const string PROVIDER_KEY = "indexeddb";

    /// <summary>
    /// Prefix for index store names.
    /// </summary>
    private const string INDEX_STORE_PREFIX = "idx_";

    #endregion

    #region Fields

    private readonly IJSRuntime m_jsRuntime;
    private readonly string m_databaseName;
    private readonly string m_providerKey;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a new IndexedDB index factory.
    /// </summary>
    /// <param name="jsRuntime">Blazor JS runtime.</param>
    /// <param name="databaseName">Name of the IndexedDB database to use for indexes.</param>
    /// <param name="providerKey">Optional provider key override. Defaults to "indexeddb".</param>
    public SecondaryIndexFactoryIndexedDb(
        IJSRuntime jsRuntime, 
        string databaseName,
        string? providerKey = null)
    {
        m_jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty", nameof(databaseName));
        
        m_databaseName = databaseName;
        m_providerKey = providerKey ?? PROVIDER_KEY;
    }

    #endregion

    #region ISecondaryIndexFactory

    /// <inheritdoc/>
    public ISecondaryIndex CreateIndex(string name, bool isUnique)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentNullException(nameof(name));

        // Create store name with prefix to avoid conflicts with main data stores
        var storeName = INDEX_STORE_PREFIX + SanitizeStoreName(name);
        
        var interop = new IndexedDbIndexInterop(m_jsRuntime, m_databaseName, storeName);
        
        // Initialize the store (sync via async bridge - acceptable in WASM)
        interop.OpenAsync().AsTask().GetAwaiter().GetResult();
        
        return new SecondaryIndexIndexedDb(name, interop, isUnique, ownsInterop: true);
    }

    /// <inheritdoc/>
    public string ProviderKey => m_providerKey;

    #endregion

    #region Tools

    /// <summary>
    /// Sanitizes an index name for use as an IndexedDB object store name.
    /// </summary>
    private static string SanitizeStoreName(string name)
    {
        // IndexedDB store names can contain any string, but we normalize
        // to avoid potential issues with special characters
        return name
            .Replace('/', '_')
            .Replace('\\', '_')
            .Replace(':', '_');
    }

    #endregion
}

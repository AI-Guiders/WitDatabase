using Microsoft.JSInterop;

namespace OutWit.Database.Core.IndexedDb.Indexes;

/// <summary>
/// Provides .NET wrapper for IndexedDB index operations.
/// Supports key-value operations for secondary indexes.
/// </summary>
public sealed class IndexedDbIndexInterop : IAsyncDisposable
{
    #region Constants

    private const string JS_NAMESPACE = "witDbIndex";

    #endregion

    #region Fields

    private readonly IJSRuntime m_jsRuntime;
    private readonly string m_databaseName;
    private readonly string m_storeName;
    private bool m_disposed;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a new IndexedDB index interop wrapper.
    /// </summary>
    /// <param name="jsRuntime">Blazor JS runtime.</param>
    /// <param name="databaseName">Name of the IndexedDB database.</param>
    /// <param name="storeName">Name of the object store for this index.</param>
    public IndexedDbIndexInterop(IJSRuntime jsRuntime, string databaseName, string storeName)
    {
        m_jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty", nameof(databaseName));
        
        if (string.IsNullOrWhiteSpace(storeName))
            throw new ArgumentException("Store name cannot be empty", nameof(storeName));
        
        m_databaseName = databaseName;
        m_storeName = storeName;
    }

    #endregion

    #region Open / Close

    /// <summary>
    /// Opens or creates the index store.
    /// </summary>
    public async ValueTask OpenAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await m_jsRuntime.InvokeVoidAsync(
            $"{JS_NAMESPACE}.open", 
            cancellationToken, 
            m_databaseName, 
            m_storeName);
    }

    /// <summary>
    /// Closes the database connection.
    /// </summary>
    public async ValueTask CloseAsync(CancellationToken cancellationToken = default)
    {
        if (m_disposed) return;
        
        try
        {
            await m_jsRuntime.InvokeVoidAsync(
                $"{JS_NAMESPACE}.close", 
                cancellationToken, 
                m_databaseName);
        }
        catch (JSDisconnectedException)
        {
            // Blazor circuit is disconnected, ignore
        }
    }

    #endregion

    #region Get

    /// <summary>
    /// Gets a value by key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The value, or null if not found.</returns>
    public async ValueTask<byte[]?> GetAsync(byte[] key, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        return await m_jsRuntime.InvokeAsync<byte[]?>(
            $"{JS_NAMESPACE}.get", 
            cancellationToken, 
            m_databaseName, 
            m_storeName,
            key);
    }

    #endregion

    #region Put

    /// <summary>
    /// Puts a key-value pair.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask PutAsync(byte[] key, byte[] value, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        await m_jsRuntime.InvokeVoidAsync(
            $"{JS_NAMESPACE}.put", 
            cancellationToken, 
            m_databaseName, 
            m_storeName,
            key,
            value);
    }

    #endregion

    #region Delete

    /// <summary>
    /// Deletes a key.
    /// </summary>
    /// <param name="key">The key to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the key was deleted; false if it didn't exist.</returns>
    public async ValueTask<bool> DeleteAsync(byte[] key, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        return await m_jsRuntime.InvokeAsync<bool>(
            $"{JS_NAMESPACE}.delete", 
            cancellationToken, 
            m_databaseName, 
            m_storeName,
            key);
    }

    /// <summary>
    /// Deletes all keys in a range.
    /// </summary>
    /// <param name="startKey">Start key (inclusive).</param>
    /// <param name="endKey">End key (exclusive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of deleted entries.</returns>
    public async ValueTask<int> DeleteRangeAsync(byte[] startKey, byte[] endKey, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        return await m_jsRuntime.InvokeAsync<int>(
            $"{JS_NAMESPACE}.deleteRange", 
            cancellationToken, 
            m_databaseName, 
            m_storeName,
            startKey,
            endKey);
    }

    #endregion

    #region Scan

    /// <summary>
    /// Scans key-value pairs in a range.
    /// </summary>
    /// <param name="startKey">Start key (inclusive), or null for beginning.</param>
    /// <param name="endKey">End key (exclusive), or null for end.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async enumerable of key-value pairs.</returns>
    public async IAsyncEnumerable<(byte[] Key, byte[] Value)> ScanAsync(
        byte[]? startKey, 
        byte[]? endKey,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Get all matching entries in one call to minimize JS interop overhead
        var entries = await m_jsRuntime.InvokeAsync<IndexedDbIndexEntry[]?>(
            $"{JS_NAMESPACE}.scan",
            cancellationToken,
            m_databaseName,
            m_storeName,
            startKey,
            endKey);

        if (entries == null)
            yield break;

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return (entry.Key, entry.Value);
        }
    }

    /// <summary>
    /// Checks if any keys exist in a range.
    /// </summary>
    public async ValueTask<bool> HasAnyAsync(byte[] startKey, byte[] endKey, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        return await m_jsRuntime.InvokeAsync<bool>(
            $"{JS_NAMESPACE}.hasAny", 
            cancellationToken, 
            m_databaseName, 
            m_storeName,
            startKey,
            endKey);
    }

    #endregion

    #region Count / Clear

    /// <summary>
    /// Gets the number of entries in the index.
    /// </summary>
    public async ValueTask<long> CountAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        return await m_jsRuntime.InvokeAsync<long>(
            $"{JS_NAMESPACE}.count", 
            cancellationToken, 
            m_databaseName, 
            m_storeName);
    }

    /// <summary>
    /// Clears all entries from the index.
    /// </summary>
    public async ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        await m_jsRuntime.InvokeVoidAsync(
            $"{JS_NAMESPACE}.clear", 
            cancellationToken, 
            m_databaseName, 
            m_storeName);
    }

    #endregion

    #region Tools

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(m_disposed, this);
    }

    #endregion

    #region IAsyncDisposable

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (m_disposed) return;
        m_disposed = true;

        await CloseAsync();
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the database name.
    /// </summary>
    public string DatabaseName => m_databaseName;

    /// <summary>
    /// Gets the store name.
    /// </summary>
    public string StoreName => m_storeName;

    #endregion
}

/// <summary>
/// Entry from IndexedDB scan operation.
/// Public to allow mock implementations to create instances.
/// </summary>
public sealed class IndexedDbIndexEntry
{
    /// <summary>
    /// Gets or sets the key.
    /// </summary>
    public byte[] Key { get; set; } = [];

    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public byte[] Value { get; set; } = [];
}

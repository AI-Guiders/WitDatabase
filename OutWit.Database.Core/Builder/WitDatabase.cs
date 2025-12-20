using OutWit.Database.Core.Interfaces;

namespace OutWit.Database.Core.Builder;

/// <summary>
/// High-level database wrapper that provides a unified API 
/// regardless of the underlying storage engine.
/// </summary>
public sealed class WitDatabase : IDisposable
{
    #region Fields

    private readonly IKeyValueStore m_store;
    private readonly ITransactionalStore? m_transactionalStore;
    private readonly bool m_disposeStore;
    private bool m_disposed;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a new WitDatabase wrapping a key-value store.
    /// </summary>
    internal WitDatabase(IKeyValueStore store, bool disposeStore = true)
    {
        m_store = store ?? throw new ArgumentNullException(nameof(store));
        m_transactionalStore = store as ITransactionalStore;
        m_disposeStore = disposeStore;
    }

    /// <summary>
    /// Creates a new WitDatabase wrapping a transactional store.
    /// </summary>
    internal WitDatabase(ITransactionalStore store, bool disposeStore = true)
    {
        m_store = store ?? throw new ArgumentNullException(nameof(store));
        m_transactionalStore = store;
        m_disposeStore = disposeStore;
    }

    #endregion

    #region Get

    /// <summary>
    /// Gets the value for the specified key.
    /// </summary>
    public byte[]? Get(ReadOnlySpan<byte> key)
    {
        ThrowIfDisposed();
        return m_store.Get(key);
    }

    /// <summary>
    /// Gets the value for the specified key.
    /// </summary>
    public byte[]? Get(byte[] key) => Get(key.AsSpan());

    /// <summary>
    /// Gets the value for the specified string key (UTF-8 encoded).
    /// </summary>
    public byte[]? Get(string key) => Get(System.Text.Encoding.UTF8.GetBytes(key));

    /// <summary>
    /// Gets the value for the specified key asynchronously.
    /// </summary>
    public ValueTask<byte[]?> GetAsync(byte[] key, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return m_store.GetAsync(key, cancellationToken);
    }

    #endregion

    #region Put

    /// <summary>
    /// Inserts or updates a key-value pair.
    /// </summary>
    public void Put(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
    {
        ThrowIfDisposed();
        m_store.Put(key, value);
    }

    /// <summary>
    /// Inserts or updates a key-value pair.
    /// </summary>
    public void Put(byte[] key, byte[] value) => Put(key.AsSpan(), value.AsSpan());

    /// <summary>
    /// Inserts or updates a key-value pair with string key (UTF-8 encoded).
    /// </summary>
    public void Put(string key, byte[] value) => Put(System.Text.Encoding.UTF8.GetBytes(key), value);

    /// <summary>
    /// Inserts or updates a key-value pair asynchronously.
    /// </summary>
    public ValueTask PutAsync(byte[] key, byte[] value, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return m_store.PutAsync(key, value, cancellationToken);
    }

    #endregion

    #region Delete

    /// <summary>
    /// Deletes a key from the store.
    /// </summary>
    public bool Delete(ReadOnlySpan<byte> key)
    {
        ThrowIfDisposed();
        return m_store.Delete(key);
    }

    /// <summary>
    /// Deletes a key from the store.
    /// </summary>
    public bool Delete(byte[] key) => Delete(key.AsSpan());

    /// <summary>
    /// Deletes a key from the store (UTF-8 encoded).
    /// </summary>
    public bool Delete(string key) => Delete(System.Text.Encoding.UTF8.GetBytes(key));

    /// <summary>
    /// Deletes a key asynchronously.
    /// </summary>
    public ValueTask<bool> DeleteAsync(byte[] key, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return m_store.DeleteAsync(key, cancellationToken);
    }

    #endregion

    #region Scan

    /// <summary>
    /// Scans key-value pairs in the specified range.
    /// </summary>
    public IEnumerable<(byte[] Key, byte[] Value)> Scan(byte[]? startKey = null, byte[]? endKey = null)
    {
        ThrowIfDisposed();
        return m_store.Scan(startKey, endKey);
    }

    /// <summary>
    /// Scans key-value pairs asynchronously.
    /// </summary>
    public IAsyncEnumerable<(byte[] Key, byte[] Value)> ScanAsync(
        byte[]? startKey = null, 
        byte[]? endKey = null, 
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return m_store.ScanAsync(startKey, endKey, cancellationToken);
    }

    #endregion

    #region Transactions

    /// <summary>
    /// Begins a new transaction.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if transactions are not enabled.</exception>
    public ITransaction BeginTransaction()
    {
        ThrowIfDisposed();
        if (m_transactionalStore == null)
            throw new InvalidOperationException("Transactions are not enabled. Use WithTransactions() when building the database.");
        
        return m_transactionalStore.BeginTransaction();
    }

    /// <summary>
    /// Begins a new transaction asynchronously.
    /// </summary>
    public ValueTask<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (m_transactionalStore == null)
            throw new InvalidOperationException("Transactions are not enabled. Use WithTransactions() when building the database.");
        
        return m_transactionalStore.BeginTransactionAsync(cancellationToken);
    }

    #endregion

    #region Flush

    /// <summary>
    /// Flushes any pending writes to durable storage.
    /// </summary>
    public void Flush()
    {
        ThrowIfDisposed();
        m_store.Flush();
    }

    /// <summary>
    /// Flushes any pending writes asynchronously.
    /// </summary>
    public ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return m_store.FlushAsync(cancellationToken);
    }

    #endregion

    #region Tools

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(m_disposed, this);
    }

    #endregion

    #region IDisposable

    /// <inheritdoc/>
    public void Dispose()
    {
        if (m_disposed) return;
        m_disposed = true;

        if (m_disposeStore)
        {
            m_store.Dispose();
        }
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets whether transaction support is enabled.
    /// </summary>
    public bool SupportsTransactions => m_transactionalStore != null;

    /// <summary>
    /// Gets the number of active transactions (if transactions are supported).
    /// </summary>
    public int ActiveTransactionCount => m_transactionalStore?.ActiveTransactionCount ?? 0;

    /// <summary>
    /// Gets the underlying key-value store.
    /// </summary>
    public IKeyValueStore Store => m_store;

    #endregion
}

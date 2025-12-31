namespace OutWit.Database.Core.LSM;

/// <summary>
/// Thread-local write buffer for LSM parallel writes.
/// Accumulates writes before flushing to main MemTable.
/// This reduces contention on the main MemTable lock.
/// </summary>
/// <remarks>
/// Each writer thread can have its own buffer. When the buffer reaches
/// a threshold or on explicit flush, entries are merged into the main MemTable.
/// </remarks>
public sealed class LsmWriteBuffer : IDisposable
{
    #region Constants

    private const int DEFAULT_CAPACITY = 1000;
    private const int DEFAULT_SIZE_THRESHOLD = 64 * 1024; // 64KB

    #endregion

    #region Fields

    private readonly List<(byte[] Key, byte[]? Value, bool IsDelete)> m_entries;
    private readonly int m_sizeThreshold;
    private long m_approximateSize;
    private bool m_disposed;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a new write buffer.
    /// </summary>
    /// <param name="capacity">Initial capacity for entries.</param>
    /// <param name="sizeThreshold">Size threshold in bytes before suggesting flush.</param>
    public LsmWriteBuffer(int capacity = DEFAULT_CAPACITY, int sizeThreshold = DEFAULT_SIZE_THRESHOLD)
    {
        m_entries = new List<(byte[] Key, byte[]? Value, bool IsDelete)>(capacity);
        m_sizeThreshold = sizeThreshold;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Adds a Put operation to the buffer.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    public void Put(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
    {
        ThrowIfDisposed();
        
        var keyArray = key.ToArray();
        var valueArray = value.ToArray();
        
        m_entries.Add((keyArray, valueArray, false));
        m_approximateSize += keyArray.Length + valueArray.Length;
    }

    /// <summary>
    /// Adds a Delete operation to the buffer.
    /// </summary>
    /// <param name="key">The key to delete.</param>
    public void Delete(ReadOnlySpan<byte> key)
    {
        ThrowIfDisposed();
        
        var keyArray = key.ToArray();
        
        m_entries.Add((keyArray, null, true));
        m_approximateSize += keyArray.Length;
    }

    /// <summary>
    /// Drains all entries from the buffer.
    /// After this call, the buffer is empty.
    /// </summary>
    /// <returns>All buffered entries.</returns>
    public IReadOnlyList<(byte[] Key, byte[]? Value, bool IsDelete)> Drain()
    {
        ThrowIfDisposed();
        
        var result = m_entries.ToList();
        m_entries.Clear();
        m_approximateSize = 0;
        return result;
    }

    /// <summary>
    /// Clears all entries without returning them.
    /// </summary>
    public void Clear()
    {
        ThrowIfDisposed();
        
        m_entries.Clear();
        m_approximateSize = 0;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the number of entries in the buffer.
    /// </summary>
    public int Count => m_entries.Count;

    /// <summary>
    /// Gets the approximate size of buffered data in bytes.
    /// </summary>
    public long ApproximateSize => m_approximateSize;

    /// <summary>
    /// Gets whether the buffer should be flushed (exceeds size threshold).
    /// </summary>
    public bool ShouldFlush => m_approximateSize >= m_sizeThreshold;

    /// <summary>
    /// Gets whether the buffer is empty.
    /// </summary>
    public bool IsEmpty => m_entries.Count == 0;

    /// <summary>
    /// Gets whether the buffer has been disposed.
    /// </summary>
    public bool IsDisposed => m_disposed;

    #endregion

    #region Tools

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(m_disposed, this);
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (m_disposed) return;
        m_disposed = true;
        
        m_entries.Clear();
    }

    #endregion
}

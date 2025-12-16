using System.Buffers;

namespace OutWit.Database.Core.Cache;

/// <summary>
/// Represents a cached page with its data and metadata.
/// </summary>
public sealed class CachedPage : IDisposable
{
    #region Fields

    private readonly byte[] m_rentedBuffer;

    private readonly int m_pageSize;

    private volatile bool m_disposed;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a new cached page with data from the array pool.
    /// </summary>
    /// <param name="pageNumber">The page number in the database file.</param>
    /// <param name="pageSize">The size of the page in bytes.</param>
    public CachedPage(long pageNumber, int pageSize)
    {
        PageNumber = pageNumber;
        m_pageSize = pageSize;
        m_rentedBuffer = ArrayPool<byte>.Shared.Rent(pageSize);
        IsDirty = false;
        ReferenceCount = 0; // Caller should increment when using
        Referenced = false;
    }

    #endregion

    #region Functions

    /// <summary>
    /// Marks this page as modified.
    /// </summary>
    public void MarkDirty()
    {
        ThrowIfDisposed();
        IsDirty = true;
    }

    /// <summary>
    /// Clears the dirty flag (after flushing to storage).
    /// </summary>
    internal void ClearDirty()
    {
        IsDirty = false;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(m_disposed, this);
    }

    #endregion

    #region IDisposable

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!m_disposed)
        {
            m_disposed = true;
            ArrayPool<byte>.Shared.Return(m_rentedBuffer);
        }
    }

    #endregion

    #region Properties

    /// <summary>
    /// Page number in the database file
    /// </summary>
    public long PageNumber { get; }

    /// <summary>
    /// Whether this page has been modified since being loaded
    /// </summary>
    public bool IsDirty { get; private set; }

    /// <summary>
    /// Whether this page has been disposed
    /// </summary>
    public bool IsDisposed => m_disposed;

    /// <summary>
    /// The page data as a span
    /// </summary>
    public Span<byte> Data
    {
        get
        {
            ThrowIfDisposed();
            return m_rentedBuffer.AsSpan(0, m_pageSize);
        }
    }

    /// <summary>
    /// The page data as a readonly span
    /// </summary>
    public ReadOnlySpan<byte> ReadOnlyData
    {
        get
        {
            ThrowIfDisposed();
            return m_rentedBuffer.AsSpan(0, m_pageSize);
        }
    }

    /// <summary>
    /// The page data as memory
    /// </summary>
    public Memory<byte> Memory
    {
        get
        {
            ThrowIfDisposed();
            return m_rentedBuffer.AsMemory(0, m_pageSize);
        }
    }

    /// <summary>
    /// Reference count for tracking active users
    /// </summary>
    internal int ReferenceCount { get; set; }

    /// <summary>
    /// Referenced bit for Clock algorithm (second chance)
    /// </summary>
    internal bool Referenced { get; set; }

    #endregion
}

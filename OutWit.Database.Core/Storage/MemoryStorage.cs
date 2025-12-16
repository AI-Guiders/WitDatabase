using OutWit.Database.Core.Interfaces;

namespace OutWit.Database.Core.Storage
{
    /// <summary>
    /// In-memory storage implementation for temporary databases and testing.
    /// Data is lost when the storage is disposed.
    /// </summary>
    public sealed class MemoryStorage : IStorage
    {
        #region Fields

        private readonly int m_pageSize;

        private byte[] m_data;

        private bool m_disposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new in-memory storage with the specified page size.
        /// </summary>
        /// <param name="pageSize">Size of each page in bytes</param>
        /// <param name="initialPageCount">Initial number of pages to allocate</param>
        public MemoryStorage(int pageSize = DatabaseConstants.DEFAULT_PAGE_SIZE, int initialPageCount = 1)
        {
            if (pageSize < DatabaseConstants.MIN_PAGE_SIZE || pageSize > DatabaseConstants.MAX_PAGE_SIZE)
                throw new ArgumentOutOfRangeException(nameof(pageSize));

            m_pageSize = pageSize;
            m_data = new byte[initialPageCount * pageSize];
        }

        #endregion

        #region Read

        /// <inheritdoc/>
        public void ReadPage(long pageNumber, Span<byte> buffer)
        {
            ThrowIfDisposed();
            ValidatePageNumber(pageNumber);
            ValidateBuffer(buffer);

            var offset = pageNumber * m_pageSize;
            m_data.AsSpan((int)offset, m_pageSize).CopyTo(buffer);
        }

        /// <inheritdoc/>
        public ValueTask ReadPageAsync(long pageNumber, Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadPage(pageNumber, buffer.Span);
            return ValueTask.CompletedTask;
        }

        #endregion

        #region Write

        /// <inheritdoc/>
        public void WritePage(long pageNumber, ReadOnlySpan<byte> buffer)
        {
            ThrowIfDisposed();
            ValidatePageNumber(pageNumber);
            ValidateBuffer(buffer);

            var offset = pageNumber * m_pageSize;
            buffer[..m_pageSize].CopyTo(m_data.AsSpan((int)offset, m_pageSize));
        }

        /// <inheritdoc/>
        public ValueTask WritePageAsync(long pageNumber, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WritePage(pageNumber, buffer.Span);
            return ValueTask.CompletedTask;
        }

        #endregion

        #region Flush

        /// <inheritdoc/>
        public void Flush()
        {
            ThrowIfDisposed();
            // No-op for memory storage
        }

        /// <inheritdoc/>
        public ValueTask FlushAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Flush();
            return ValueTask.CompletedTask;
        }

        #endregion

        #region SetSize

        /// <inheritdoc/>
        public void SetSize(long pageCount)
        {
            ThrowIfDisposed();

            if (pageCount < 0)
                throw new ArgumentOutOfRangeException(nameof(pageCount));

            var newSize = pageCount * m_pageSize;
            if (newSize != m_data.Length)
            {
                Array.Resize(ref m_data, (int)newSize);
            }
        }

        #endregion

        #region Tools

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(m_disposed, this);
        }

        private void ValidatePageNumber(long pageNumber)
        {
            if (pageNumber < 0 || pageNumber >= PageCount)
                throw new ArgumentOutOfRangeException(nameof(pageNumber), 
                    $"Page number must be between 0 and {PageCount - 1}");
        }

        private void ValidateBuffer(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length < m_pageSize)
                throw new ArgumentException($"Buffer must be at least {m_pageSize} bytes", nameof(buffer));
        }

        #endregion

        #region IDisposable

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!m_disposed)
            {
                m_data = [];
                m_disposed = true;
            }
        }

        #endregion

        #region Properties

        /// <inheritdoc/>
        public int PageSize => m_pageSize;

        /// <inheritdoc/>
        public long PageCount => m_data.Length / m_pageSize;

        /// <inheritdoc/>
        public bool IsReadOnly => false;

        /// <summary>
        /// Gets a read-only view of the underlying data for testing purposes.
        /// </summary>
        public ReadOnlyMemory<byte> Data => m_data;

        #endregion
    }
}

using System.Security.Cryptography;
using OutWit.Database.Core.Interfaces;

namespace OutWit.Database.Core.Storage
{
    /// <summary>
    /// Storage wrapper that encrypts/decrypts pages transparently.
    /// </summary>
    public sealed class EncryptedStorage : IStorage
    {
        #region Fields

        private readonly IStorage m_innerStorage;

        private readonly IPageEncryptor m_encryptor;

        private readonly int m_overhead;

        private bool m_disposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates an encrypted storage wrapper.
        /// </summary>
        /// <param name="innerStorage">The underlying storage.</param>
        /// <param name="encryptor">The page encryptor.</param>
        public EncryptedStorage(IStorage innerStorage, IPageEncryptor encryptor)
        {
            ArgumentNullException.ThrowIfNull(innerStorage);
            ArgumentNullException.ThrowIfNull(encryptor);

            m_innerStorage = innerStorage;
            m_encryptor = encryptor;
            m_overhead = encryptor.Overhead;
        }

        #endregion

        #region Read

        /// <inheritdoc/>
        public void ReadPage(long pageNumber, Span<byte> buffer)
        {
            ThrowIfDisposed();
        
            // Read encrypted page from storage
            Span<byte> encrypted = stackalloc byte[m_innerStorage.PageSize];
            m_innerStorage.ReadPage(pageNumber, encrypted);
        
            // Check if page is uninitialized (all zeros) - return zeros
            bool isUninitialized = true;
            for (int i = 0; i < encrypted.Length && isUninitialized; i++)
            {
                if (encrypted[i] != 0) isUninitialized = false;
            }
        
            if (isUninitialized)
            {
                buffer[..PageSize].Clear();
                return;
            }
        
            // Decrypt
            int decryptedLen = m_encryptor.Decrypt(encrypted, pageNumber, buffer);
            if (decryptedLen < 0)
            {
                throw new CryptographicException(
                    $"Failed to decrypt page {pageNumber} - authentication failed");
            }
        }

        /// <inheritdoc/>
        public async ValueTask ReadPageAsync(long pageNumber, Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
        
            byte[] encrypted = new byte[m_innerStorage.PageSize];
            await m_innerStorage.ReadPageAsync(pageNumber, encrypted, cancellationToken);
        
            // Check if page is uninitialized (all zeros) - return zeros
            bool isUninitialized = true;
            for (int i = 0; i < encrypted.Length && isUninitialized; i++)
            {
                if (encrypted[i] != 0) isUninitialized = false;
            }
        
            if (isUninitialized)
            {
                buffer.Span[..PageSize].Clear();
                return;
            }
        
            int decryptedLen = m_encryptor.Decrypt(encrypted, pageNumber, buffer.Span);
            if (decryptedLen < 0)
            {
                throw new CryptographicException(
                    $"Failed to decrypt page {pageNumber} - authentication failed");
            }
        }

        #endregion

        #region Write

        /// <inheritdoc/>
        public void WritePage(long pageNumber, ReadOnlySpan<byte> buffer)
        {
            ThrowIfDisposed();
        
            // Encrypt
            Span<byte> encrypted = stackalloc byte[m_innerStorage.PageSize];
            m_encryptor.Encrypt(buffer, pageNumber, encrypted);
        
            // Write encrypted page
            m_innerStorage.WritePage(pageNumber, encrypted);
        }

        /// <inheritdoc/>
        public async ValueTask WritePageAsync(long pageNumber, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
        
            byte[] encrypted = new byte[m_innerStorage.PageSize];
            m_encryptor.Encrypt(buffer.Span, pageNumber, encrypted);
        
            await m_innerStorage.WritePageAsync(pageNumber, encrypted, cancellationToken);
        }

        #endregion

        #region Flush

        /// <inheritdoc/>
        public void Flush()
        {
            ThrowIfDisposed();
            m_innerStorage.Flush();
        }

        /// <inheritdoc/>
        public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            await m_innerStorage.FlushAsync(cancellationToken);
        }

        #endregion

        #region SetSize

        /// <inheritdoc/>
        public void SetSize(long pageCount)
        {
            ThrowIfDisposed();
            m_innerStorage.SetSize(pageCount);
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
            if (!m_disposed)
            {
                m_encryptor.Dispose();
                m_innerStorage.Dispose();
                m_disposed = true;
            }
        }

        #endregion

        #region Properties

        /// <inheritdoc/>
        public int PageSize => m_innerStorage.PageSize - m_overhead;

        /// <inheritdoc/>
        public long PageCount => m_innerStorage.PageCount;

        /// <inheritdoc/>
        public bool IsReadOnly => m_innerStorage.IsReadOnly;

        #endregion
    }
}

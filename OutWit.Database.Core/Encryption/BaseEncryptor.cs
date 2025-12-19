using OutWit.Database.Core.Interfaces;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace OutWit.Database.Core.Encryption
{
    /// <summary>
    /// Base encryptor class that handles common encryption logic.
    /// Uses ICryptoProvider for the actual cryptographic operations.
    /// Format: [nonce][ciphertext][tag]
    /// 
    /// SECURITY NOTE: For AES-GCM, nonce must NEVER repeat with the same key.
    /// This implementation uses deterministic nonce = f(salt, id, counter) where:
    /// - salt: random per-database (stored in header)
    /// - id: page/block number
    /// - counter: monotonic counter for same-page rewrites
    /// 
    /// For page-level encryption, the counter is embedded in the nonce and verified on decrypt.
    /// This prevents nonce reuse when a page is overwritten.
    /// </summary>
    public class BaseEncryptor : IDataEncryptor
    {
        #region Fields

        private readonly ICryptoProvider m_crypto;

        private readonly byte[] m_salt;

        private long m_writeCounter;

        private bool m_disposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates encryptor with specified crypto provider and salt.
        /// </summary>
        /// <param name="crypto">Crypto provider for raw AEAD operations.</param>
        /// <param name="salt">Salt for nonce derivation (at least 8 bytes, 16 recommended).</param>
        public BaseEncryptor(ICryptoProvider crypto, byte[] salt)
        {
            if (salt.Length < 8)
                throw new ArgumentException("Salt must be at least 8 bytes", nameof(salt));
        
            m_crypto = crypto ?? throw new ArgumentNullException(nameof(crypto));
            m_salt = (byte[])salt.Clone();
            m_writeCounter = 0;
        }

        #endregion

        #region IDataEncryptor

        /// <inheritdoc/>
        public virtual byte[] Encrypt(ReadOnlySpan<byte> plaintext, long id)
        {
            ThrowIfDisposed();

            Span<byte> nonce = stackalloc byte[m_crypto.NonceSize];
            GenerateNonce(id, nonce);
        
            var result = new byte[m_crypto.NonceSize + plaintext.Length + m_crypto.TagSize];
            nonce.CopyTo(result.AsSpan(0, m_crypto.NonceSize));

            var ciphertextSpan = result.AsSpan(m_crypto.NonceSize, plaintext.Length);
            var tagSpan = result.AsSpan(m_crypto.NonceSize + plaintext.Length, m_crypto.TagSize);

            m_crypto.Encrypt(nonce, plaintext, ciphertextSpan, tagSpan);

            return result;
        }

        /// <inheritdoc/>
        public virtual byte[]? Decrypt(ReadOnlySpan<byte> ciphertext, long id)
        {
            ThrowIfDisposed();

            if (ciphertext.Length < Overhead)
                return null;

            var storedNonce = ciphertext[..m_crypto.NonceSize];
        
            // Verify nonce prefix matches expected (salt XOR id)
            // The counter portion can vary, so we only check the deterministic part
            Span<byte> expectedNoncePrefix = stackalloc byte[4];
            GenerateNoncePrefix(id, expectedNoncePrefix);
            
            if (!CryptographicOperations.FixedTimeEquals(storedNonce[..4], expectedNoncePrefix))
                return null;

            var encryptedData = ciphertext[m_crypto.NonceSize..^m_crypto.TagSize];
            var tag = ciphertext[^m_crypto.TagSize..];

            var plaintext = new byte[encryptedData.Length];

            if (m_crypto.Decrypt(storedNonce, encryptedData, tag, plaintext))
                return plaintext;

            return null;
        }

        #endregion

        #region Tools

        /// <summary>
        /// Generates unique nonce from salt, id, and counter.
        /// Nonce structure (12 bytes):
        /// - Bytes 0-3: salt[0-3] XOR id_low_bytes (deterministic prefix for verification)
        /// - Bytes 4-7: salt[4-7] XOR id_high_bytes  
        /// - Bytes 8-11: monotonic counter (ensures uniqueness on rewrite)
        /// </summary>
        protected void GenerateNonce(long id, Span<byte> nonce)
        {
            // Increment counter for each encryption to ensure unique nonce
            long counter = Interlocked.Increment(ref m_writeCounter);
            
            Span<byte> idBytes = stackalloc byte[8];
            BinaryPrimitives.WriteInt64LittleEndian(idBytes, id);
            
            // First 4 bytes: salt XOR id (low)
            for (int i = 0; i < 4 && i < m_salt.Length; i++)
            {
                nonce[i] = (byte)(m_salt[i] ^ idBytes[i]);
            }
            
            // Next 4 bytes: salt XOR id (high)
            for (int i = 4; i < 8 && i < m_salt.Length; i++)
            {
                nonce[i] = (byte)(m_salt[i] ^ idBytes[i]);
            }
            
            // Last 4 bytes: counter (ensures uniqueness even for same page)
            BinaryPrimitives.WriteInt32LittleEndian(nonce[8..], (int)counter);
        }

        /// <summary>
        /// Generates the deterministic prefix of nonce for verification.
        /// </summary>
        private void GenerateNoncePrefix(long id, Span<byte> prefix)
        {
            Span<byte> idBytes = stackalloc byte[8];
            BinaryPrimitives.WriteInt64LittleEndian(idBytes, id);
            
            for (int i = 0; i < 4 && i < m_salt.Length; i++)
            {
                prefix[i] = (byte)(m_salt[i] ^ idBytes[i]);
            }
        }

        protected void ThrowIfDisposed()
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
            CryptographicOperations.ZeroMemory(m_salt);
            m_crypto.Dispose();
        }

        #endregion

        #region Properties

        /// <inheritdoc/>
        public int Overhead => m_crypto.Overhead;

        protected int NonceSize => m_crypto.NonceSize;

        protected int TagSize => m_crypto.TagSize;

        protected ICryptoProvider Crypto => m_crypto;

        #endregion
    }
}

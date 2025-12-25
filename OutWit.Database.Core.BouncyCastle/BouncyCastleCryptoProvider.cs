using System.Buffers;
using OutWit.Database.Core.Interfaces;
using System.Security.Cryptography;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using ChaCha20Poly1305 = Org.BouncyCastle.Crypto.Modes.ChaCha20Poly1305;

namespace OutWit.Database.Core.BouncyCastle
{
    /// <summary>
    /// ChaCha20-Poly1305 crypto provider using BouncyCastle.
    /// Good alternative when AES-NI is not available.
    /// Uses ArrayPool for temporary buffers to reduce GC pressure.
    /// </summary>
    public sealed class BouncyCastleCryptoProvider : ICryptoProvider
    {
        #region Constants

        /// <summary>
        /// Provider key for ChaCha20-Poly1305 crypto.
        /// </summary>
        public const string PROVIDER_KEY = "chacha20-poly1305";

        #endregion

        #region Fields

        private readonly byte[] m_key;

        private bool m_disposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates ChaCha20-Poly1305 provider with specified key.
        /// </summary>
        /// <param name="key">256-bit (32 bytes) encryption key.</param>
        public BouncyCastleCryptoProvider(byte[] key)
        {
            if (key.Length != 32)
                throw new ArgumentException("Key must be 256 bits (32 bytes)", nameof(key));
            m_key = (byte[])key.Clone();
        }

        #endregion

        #region Functions

        /// <summary>
        /// Creates ChaCha20-Poly1305 provider from password.
        /// </summary>
        public static BouncyCastleCryptoProvider FromPassword(string password, byte[] salt, int iterations = 100_000)
        {
            var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);
            return new BouncyCastleCryptoProvider(key);
        }

        /// <inheritdoc/>
        public void Encrypt(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> plaintext, Span<byte> ciphertext, Span<byte> tag)
        {
            ThrowIfDisposed();

            var cipher = new ChaCha20Poly1305();
            var keyParam = new KeyParameter(m_key);

            // Rent arrays from pool instead of allocating
            var nonceArray = ArrayPool<byte>.Shared.Rent(nonce.Length);
            var plaintextArray = ArrayPool<byte>.Shared.Rent(plaintext.Length);
            var outputArray = ArrayPool<byte>.Shared.Rent(plaintext.Length + TagSize);

            try
            {
                // Copy nonce (BouncyCastle requires array)
                nonce.CopyTo(nonceArray);

                var parameters = new AeadParameters(keyParam, TagSize * 8, nonceArray[..nonce.Length]);
                cipher.Init(true, parameters);

                // Copy plaintext (BouncyCastle requires array)
                plaintext.CopyTo(plaintextArray);

                var len = cipher.ProcessBytes(plaintextArray, 0, plaintext.Length, outputArray, 0);
                len += cipher.DoFinal(outputArray, len);

                // Copy results to output spans
                outputArray.AsSpan(0, plaintext.Length).CopyTo(ciphertext);
                outputArray.AsSpan(plaintext.Length, TagSize).CopyTo(tag);
            }
            finally
            {
                // Clear sensitive data and return to pool
                CryptographicOperations.ZeroMemory(nonceArray.AsSpan(0, nonce.Length));
                CryptographicOperations.ZeroMemory(plaintextArray.AsSpan(0, plaintext.Length));
                CryptographicOperations.ZeroMemory(outputArray.AsSpan(0, plaintext.Length + TagSize));
                
                ArrayPool<byte>.Shared.Return(nonceArray);
                ArrayPool<byte>.Shared.Return(plaintextArray);
                ArrayPool<byte>.Shared.Return(outputArray);
            }
        }

        /// <inheritdoc/>
        public bool Decrypt(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> tag, Span<byte> plaintext)
        {
            ThrowIfDisposed();

            // Rent arrays from pool instead of allocating
            var nonceArray = ArrayPool<byte>.Shared.Rent(nonce.Length);
            var inputArray = ArrayPool<byte>.Shared.Rent(ciphertext.Length + TagSize);
            var outputArray = ArrayPool<byte>.Shared.Rent(ciphertext.Length);

            try
            {
                var cipher = new ChaCha20Poly1305();
                var keyParam = new KeyParameter(m_key);

                // Copy nonce (BouncyCastle requires array)
                nonce.CopyTo(nonceArray);

                var parameters = new AeadParameters(keyParam, TagSize * 8, nonceArray[..nonce.Length]);
                cipher.Init(false, parameters);

                // Combine ciphertext + tag (BouncyCastle requires single array)
                ciphertext.CopyTo(inputArray);
                tag.CopyTo(inputArray.AsSpan(ciphertext.Length));

                var len = cipher.ProcessBytes(inputArray, 0, ciphertext.Length + TagSize, outputArray, 0);
                len += cipher.DoFinal(outputArray, len);

                outputArray.AsSpan(0, len).CopyTo(plaintext);
                return true;
            }
            catch (InvalidCipherTextException)
            {
                // Clear partial output on failure for security
                plaintext[..ciphertext.Length].Clear();
                return false;
            }
            finally
            {
                // Clear sensitive data and return to pool
                CryptographicOperations.ZeroMemory(nonceArray.AsSpan(0, nonce.Length));
                CryptographicOperations.ZeroMemory(inputArray.AsSpan(0, ciphertext.Length + TagSize));
                CryptographicOperations.ZeroMemory(outputArray.AsSpan(0, ciphertext.Length));
                
                ArrayPool<byte>.Shared.Return(nonceArray);
                ArrayPool<byte>.Shared.Return(inputArray);
                ArrayPool<byte>.Shared.Return(outputArray);
            }
        }

        /// <inheritdoc/>
        public ICryptoProvider Clone()
        {
            ThrowIfDisposed();
            return new BouncyCastleCryptoProvider(m_key);
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
            CryptographicOperations.ZeroMemory(m_key);
        }

        #endregion

        #region Properties

        /// <inheritdoc/>
        public int NonceSize => 12;

        /// <inheritdoc/>
        public int TagSize => 16;

        /// <inheritdoc/>
        public string ProviderKey => PROVIDER_KEY;

        #endregion
    }
}

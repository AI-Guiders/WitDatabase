namespace OutWit.Database.Core.Interfaces
{
    /// <summary>
    /// High-level interface for data encryption/decryption.
    /// Used for both page-level (BTree) and block-level (LSM) encryption.
    /// </summary>
    public interface IDataEncryptor : IDisposable
    {

        /// <summary>
        /// Encrypts data.
        /// </summary>
        /// <param name="plaintext">Data to encrypt.</param>
        /// <param name="id">Unique identifier for nonce derivation (page number or block ID).</param>
        /// <returns>Encrypted data with authentication tag.</returns>
        byte[] Encrypt(ReadOnlySpan<byte> plaintext, long id);

        /// <summary>
        /// Decrypts data.
        /// </summary>
        /// <param name="ciphertext">Encrypted data with authentication tag.</param>
        /// <param name="id">Identifier used during encryption.</param>
        /// <returns>Decrypted data, or null if authentication failed.</returns>
        byte[]? Decrypt(ReadOnlySpan<byte> ciphertext, long id);

        /// <summary>
        /// Gets the overhead added by encryption (nonce + auth tag).
        /// </summary>
        int Overhead { get; }
    }
}
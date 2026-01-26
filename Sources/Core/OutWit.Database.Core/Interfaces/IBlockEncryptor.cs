namespace OutWit.Database.Core.Interfaces
{
    /// <summary>
    /// Interface for encrypting/decrypting variable-length data blocks.
    /// Used for LSM-Tree WAL and SSTable encryption.
    /// </summary>
    public interface IBlockEncryptor : IDisposable
    {
        /// <summary>
        /// Encrypts a data block.
        /// </summary>
        /// <param name="plaintext">Data to encrypt.</param>
        /// <param name="blockId">Unique block identifier (used as nonce/IV component).</param>
        /// <returns>Encrypted data with authentication tag.</returns>
        byte[] Encrypt(ReadOnlySpan<byte> plaintext, long blockId);

        /// <summary>
        /// Decrypts a data block.
        /// </summary>
        /// <param name="ciphertext">Encrypted data with authentication tag.</param>
        /// <param name="blockId">Block identifier used during encryption.</param>
        /// <returns>Decrypted data, or null if authentication failed.</returns>
        byte[]? Decrypt(ReadOnlySpan<byte> ciphertext, long blockId);

        /// <summary>
        /// Encryption overhead in bytes (nonce + tag).
        /// </summary>
        int Overhead { get; }
    }
}

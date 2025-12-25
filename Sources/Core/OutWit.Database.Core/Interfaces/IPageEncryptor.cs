namespace OutWit.Database.Core.Interfaces
{
    /// <summary>
    /// Interface for page-level encryption.
    /// </summary>
    public interface IPageEncryptor : IDisposable
    {
        /// <summary>
        /// Encrypts a page.
        /// </summary>
        /// <param name="plaintext">The plaintext data to encrypt.</param>
        /// <param name="pageNumber">The page number (used as part of nonce).</param>
        /// <param name="ciphertext">Output buffer for ciphertext (must be at least plaintext.Length + Overhead).</param>
        /// <returns>Number of bytes written to ciphertext.</returns>
        int Encrypt(ReadOnlySpan<byte> plaintext, long pageNumber, Span<byte> ciphertext);

        /// <summary>
        /// Decrypts a page.
        /// </summary>
        /// <param name="ciphertext">The ciphertext to decrypt.</param>
        /// <param name="pageNumber">The page number (used as part of nonce).</param>
        /// <param name="plaintext">Output buffer for plaintext.</param>
        /// <returns>Number of bytes written to plaintext, or -1 if authentication failed.</returns>
        int Decrypt(ReadOnlySpan<byte> ciphertext, long pageNumber, Span<byte> plaintext);

        /// <summary>
        /// Gets the overhead added by encryption (IV + auth tag).
        /// </summary>
        int Overhead { get; }

    }
}

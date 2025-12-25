using OutWit.Database.Core.Builder;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;

namespace OutWit.Database.Core.BouncyCastle;

/// <summary>
/// Extension methods for configuring WitDatabaseBuilder with BouncyCastle encryption.
/// </summary>
public static class WitDatabaseBuilderBouncyCastleExtensions
{
    #region Constants

    private const int DEFAULT_PBKDF2_ITERATIONS = 100_000;
    private const int KEY_SIZE_BYTES = 32;
    private const int SALT_SIZE_BYTES = 16;

    #endregion

    #region Encryption

    /// <summary>
    /// Enable ChaCha20-Poly1305 encryption using BouncyCastle with password-based key derivation.
    /// Good alternative when AES-NI hardware acceleration is not available.
    /// </summary>
    /// <param name="builder">The database builder.</param>
    /// <param name="password">Password to derive encryption key from.</param>
    public static WitDatabaseBuilder WithBouncyCastleEncryption(this WitDatabaseBuilder builder, string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be empty", nameof(password));

        var salt = DerivePasswordSalt(password);
        var key = DeriveKey(password, salt);
        
        builder.Options.CryptoProvider = new BouncyCastleCryptoProvider(key);
        builder.Options.EncryptionSalt = salt;
        return builder;
    }

    /// <summary>
    /// Enable ChaCha20-Poly1305 encryption using BouncyCastle with user and password-based key derivation.
    /// </summary>
    /// <param name="builder">The database builder.</param>
    /// <param name="user">Username (used as salt basis).</param>
    /// <param name="password">Password to derive encryption key from.</param>
    public static WitDatabaseBuilder WithBouncyCastleEncryption(this WitDatabaseBuilder builder, string user, string password)
    {
        if (string.IsNullOrEmpty(user))
            throw new ArgumentException("User cannot be empty", nameof(user));
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be empty", nameof(password));

        var salt = DeriveUserSalt(user);
        var key = DeriveKey(password, salt);
        
        builder.Options.CryptoProvider = new BouncyCastleCryptoProvider(key);
        builder.Options.EncryptionSalt = salt;
        return builder;
    }

    /// <summary>
    /// Enable ChaCha20-Poly1305 encryption using BouncyCastle with the specified 256-bit key.
    /// Good alternative when AES-NI hardware acceleration is not available.
    /// </summary>
    /// <param name="builder">The database builder.</param>
    /// <param name="key">256-bit (32 bytes) encryption key.</param>
    public static WitDatabaseBuilder WithBouncyCastleEncryption(this WitDatabaseBuilder builder, byte[] key)
    {
        if (key.Length != KEY_SIZE_BYTES)
            throw new ArgumentException("ChaCha20-Poly1305 requires a 32-byte key", nameof(key));
        
        builder.Options.CryptoProvider = new BouncyCastleCryptoProvider(key);
        return builder;
    }

    /// <summary>
    /// Enable ChaCha20-Poly1305 encryption using BouncyCastle with the specified 256-bit key and salt.
    /// </summary>
    /// <param name="builder">The database builder.</param>
    /// <param name="key">256-bit (32 bytes) encryption key.</param>
    /// <param name="salt">Salt for key derivation (at least 8 bytes).</param>
    public static WitDatabaseBuilder WithBouncyCastleEncryption(this WitDatabaseBuilder builder, byte[] key, byte[] salt)
    {
        if (key.Length != KEY_SIZE_BYTES)
            throw new ArgumentException("ChaCha20-Poly1305 requires a 32-byte key", nameof(key));
        if (salt.Length < 8)
            throw new ArgumentException("Salt must be at least 8 bytes", nameof(salt));
        
        builder.Options.CryptoProvider = new BouncyCastleCryptoProvider(key);
        builder.Options.EncryptionSalt = salt;
        return builder;
    }

    #endregion

    #region Key Derivation (BouncyCastle)

    private static byte[] DerivePasswordSalt(string password)
    {
        // Use SHA-256 from BouncyCastle to derive salt from password
        var digest = new Sha256Digest();
        var input = System.Text.Encoding.UTF8.GetBytes(password + "_WitDB_BC_Salt");
        digest.BlockUpdate(input, 0, input.Length);
        
        var hash = new byte[digest.GetDigestSize()];
        digest.DoFinal(hash, 0);
        
        // Return first 16 bytes as salt
        var salt = new byte[SALT_SIZE_BYTES];
        Array.Copy(hash, salt, SALT_SIZE_BYTES);
        return salt;
    }

    private static byte[] DeriveUserSalt(string user)
    {
        var digest = new Sha256Digest();
        var input = System.Text.Encoding.UTF8.GetBytes(user + "_WitDB_BC_UserSalt");
        digest.BlockUpdate(input, 0, input.Length);
        
        var hash = new byte[digest.GetDigestSize()];
        digest.DoFinal(hash, 0);
        
        var salt = new byte[SALT_SIZE_BYTES];
        Array.Copy(hash, salt, SALT_SIZE_BYTES);
        return salt;
    }

    private static byte[] DeriveKey(string password, byte[] salt)
    {
        // Use PBKDF2 from BouncyCastle
        var generator = new Pkcs5S2ParametersGenerator(new Sha256Digest());
        generator.Init(
            Org.BouncyCastle.Crypto.PbeParametersGenerator.Pkcs5PasswordToUtf8Bytes(password.ToCharArray()),
            salt,
            DEFAULT_PBKDF2_ITERATIONS);
        
        var keyParam = (KeyParameter)generator.GenerateDerivedMacParameters(KEY_SIZE_BYTES * 8);
        return keyParam.GetKey();
    }

    #endregion
}

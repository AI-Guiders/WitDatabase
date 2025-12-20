using System.Security.Cryptography;
using OutWit.Database.Core.Encryption;
using OutWit.Database.Core.Providers;

namespace OutWit.Database.Core.Tests.Encryption;

/// <summary>
/// Tests for AesGcmCryptoProvider - key derivation, validation, and basic operations.
/// </summary>
[TestFixture]
public class CryptoProviderAesGcmTests
{
    private byte[] m_key = null!;
    private byte[] m_salt = null!;

    [SetUp]
    public void SetUp()
    {
        m_key = RandomNumberGenerator.GetBytes(32);
        m_salt = RandomNumberGenerator.GetBytes(16);
    }

    #region Password-Based Key Derivation

    [Test]
    public void FromPasswordCreatesWorkingProviderTest()
    {
        string password = "MySecurePassword123!";

        using var provider = CryptoProviderAesGcm.FromPassword(password, m_salt, iterations: 10000);
        using var encryptor = new PageEncryptor(provider, m_salt);
        
        byte[] plaintext = new byte[4096];
        Random.Shared.NextBytes(plaintext);
        
        byte[] ciphertext = new byte[plaintext.Length + encryptor.Overhead];
        byte[] decrypted = new byte[plaintext.Length];

        int encryptedLen = encryptor.Encrypt(plaintext, pageNumber: 0, ciphertext);
        int decryptedLen = encryptor.Decrypt(ciphertext.AsSpan(0, encryptedLen), pageNumber: 0, decrypted);

        Assert.That(decryptedLen, Is.EqualTo(plaintext.Length));
        Assert.That(decrypted, Is.EqualTo(plaintext));
    }

    [Test]
    public void FromPasswordSamePasswordAndSaltProducesSameKeyTest()
    {
        string password = "TestPassword";

        using var provider1 = CryptoProviderAesGcm.FromPassword(password, m_salt, iterations: 10000);
        using var encryptor1 = new PageEncryptor(provider1, m_salt);

        using var provider2 = CryptoProviderAesGcm.FromPassword(password, m_salt, iterations: 10000);
        using var encryptor2 = new PageEncryptor(provider2, m_salt);
        
        byte[] plaintext = new byte[100];
        Random.Shared.NextBytes(plaintext);
        
        byte[] ciphertext = new byte[plaintext.Length + encryptor1.Overhead];
        byte[] decrypted = new byte[plaintext.Length];

        int encryptedLen = encryptor1.Encrypt(plaintext, pageNumber: 5, ciphertext);
        int decryptedLen = encryptor2.Decrypt(ciphertext.AsSpan(0, encryptedLen), pageNumber: 5, decrypted);

        Assert.That(decryptedLen, Is.EqualTo(plaintext.Length));
        Assert.That(decrypted, Is.EqualTo(plaintext));
    }

    [Test]
    public void FromPasswordDifferentPasswordsProduceDifferentKeysTest()
    {
        using var provider1 = CryptoProviderAesGcm.FromPassword("password1", m_salt, iterations: 10000);
        using var encryptor1 = new PageEncryptor(provider1, m_salt);

        using var provider2 = CryptoProviderAesGcm.FromPassword("password2", m_salt, iterations: 10000);
        using var encryptor2 = new PageEncryptor(provider2, m_salt);
        
        byte[] plaintext = new byte[100];
        Random.Shared.NextBytes(plaintext);
        
        byte[] ciphertext = new byte[plaintext.Length + encryptor1.Overhead];
        byte[] decrypted = new byte[plaintext.Length];

        int encryptedLen = encryptor1.Encrypt(plaintext, pageNumber: 1, ciphertext);
        int decryptedLen = encryptor2.Decrypt(ciphertext.AsSpan(0, encryptedLen), pageNumber: 1, decrypted);

        Assert.That(decryptedLen, Is.EqualTo(-1));
    }

    [Test]
    public void FromPasswordDifferentSaltsProduceDifferentKeysTest()
    {
        byte[] salt1 = RandomNumberGenerator.GetBytes(16);
        byte[] salt2 = RandomNumberGenerator.GetBytes(16);

        using var provider1 = CryptoProviderAesGcm.FromPassword("password", salt1, iterations: 10000);
        using var encryptor1 = new PageEncryptor(provider1, salt1);

        using var provider2 = CryptoProviderAesGcm.FromPassword("password", salt2, iterations: 10000);
        using var encryptor2 = new PageEncryptor(provider2, salt2);
        
        byte[] plaintext = new byte[100];
        Random.Shared.NextBytes(plaintext);
        
        byte[] ciphertext = new byte[plaintext.Length + encryptor1.Overhead];
        byte[] decrypted = new byte[plaintext.Length];

        int encryptedLen = encryptor1.Encrypt(plaintext, pageNumber: 1, ciphertext);
        int decryptedLen = encryptor2.Decrypt(ciphertext.AsSpan(0, encryptedLen), pageNumber: 1, decrypted);

        Assert.That(decryptedLen, Is.EqualTo(-1));
    }

    #endregion

    #region Validation

    [Test]
    public void ConstructorInvalidKeySizeThrowsTest()
    {
        Assert.Throws<ArgumentException>(() => new CryptoProviderAesGcm(new byte[16]));
        Assert.Throws<ArgumentException>(() => new CryptoProviderAesGcm(new byte[24]));
        Assert.Throws<ArgumentException>(() => new CryptoProviderAesGcm(new byte[64]));
        Assert.Throws<ArgumentException>(() => new CryptoProviderAesGcm([]));
    }

    [Test]
    public void FromPasswordEmptyPasswordThrowsTest()
    {
        Assert.Throws<ArgumentException>(() => 
            CryptoProviderAesGcm.FromPassword("", m_salt, iterations: 10000));
        Assert.Throws<ArgumentException>(() => 
            CryptoProviderAesGcm.FromPassword(null!, m_salt, iterations: 10000));
    }

    [Test]
    public void FromPasswordSaltTooShortThrowsTest()
    {
        Assert.Throws<ArgumentException>(() => 
            CryptoProviderAesGcm.FromPassword("password", new byte[4], iterations: 10000));
    }

    [Test]
    public void FromPasswordIterationsTooLowThrowsTest()
    {
        Assert.Throws<ArgumentException>(() => 
            CryptoProviderAesGcm.FromPassword("password", m_salt, iterations: 1000));
    }

    [Test]
    public void AfterDisposeThrowsObjectDisposedExceptionTest()
    {
        var provider = new CryptoProviderAesGcm(m_key);
        provider.Dispose();

        byte[] nonce = new byte[12];
        byte[] plaintext = new byte[100];
        byte[] ciphertext = new byte[100];
        byte[] tag = new byte[16];

        Assert.Throws<ObjectDisposedException>(() => 
            provider.Encrypt(nonce, plaintext, ciphertext, tag));
        Assert.Throws<ObjectDisposedException>(() => 
            provider.Decrypt(nonce, ciphertext, tag, plaintext));
    }

    #endregion

    #region Properties

    [Test]
    public void NonceSizeIs12BytesTest()
    {
        using var provider = new CryptoProviderAesGcm(m_key);
        Assert.That(provider.NonceSize, Is.EqualTo(12));
    }

    [Test]
    public void TagSizeIs16BytesTest()
    {
        using var provider = new CryptoProviderAesGcm(m_key);
        Assert.That(provider.TagSize, Is.EqualTo(16));
    }

    [Test]
    public void OverheadIs28BytesTest()
    {
        using var provider = new CryptoProviderAesGcm(m_key);
        Assert.That(provider.NonceSize + provider.TagSize, Is.EqualTo(28));
    }

    #endregion
}

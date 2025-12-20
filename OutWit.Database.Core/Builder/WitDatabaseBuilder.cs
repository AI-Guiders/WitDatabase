using OutWit.Database.Core.Cache;
using OutWit.Database.Core.Concurrency;
using OutWit.Database.Core.Encryption;
using OutWit.Database.Core.Interfaces;
using OutWit.Database.Core.LSM;
using OutWit.Database.Core.Managers;
using OutWit.Database.Core.Providers;
using OutWit.Database.Core.Storage;
using OutWit.Database.Core.Stores;
using OutWit.Database.Core.Transactions;

namespace OutWit.Database.Core.Builder;

/// <summary>
/// Fluent builder for creating WitDatabase instances.
/// </summary>
public sealed class WitDatabaseBuilder
{
    #region Constants

    private static readonly byte[] DEFAULT_SALT = "WitDBSalt123"u8.ToArray();

    #endregion

    #region Fields

    private readonly WitDatabaseBuilderOptions m_options = new();
    private byte[] m_encryptionSalt = DEFAULT_SALT;

    #endregion

    #region Storage Configuration

    /// <summary>
    /// Use file-based storage with the specified path.
    /// </summary>
    public WitDatabaseBuilder WithFilePath(string path)
    {
        m_options.FilePath = path;
        m_options.UseMemoryStorage = false;
        return this;
    }

    /// <summary>
    /// Use in-memory storage (data is not persisted).
    /// </summary>
    public WitDatabaseBuilder WithMemoryStorage()
    {
        m_options.UseMemoryStorage = true;
        m_options.FilePath = null;
        return this;
    }

    /// <summary>
    /// Use a custom storage implementation.
    /// </summary>
    public WitDatabaseBuilder WithStorage(IStorage storage)
    {
        m_options.Storage = storage;
        return this;
    }

    #endregion

    #region Engine Selection

    /// <summary>
    /// Use B-Tree storage engine (default).
    /// Best for read-heavy workloads with good random access performance.
    /// </summary>
    public WitDatabaseBuilder WithBTree()
    {
        m_options.UseBTree = true;
        m_options.UseLsmTree = false;
        return this;
    }

    /// <summary>
    /// Use LSM-Tree storage engine.
    /// Best for write-heavy workloads with excellent sequential write performance.
    /// </summary>
    public WitDatabaseBuilder WithLsmTree()
    {
        m_options.UseLsmTree = true;
        m_options.UseBTree = false;
        return this;
    }

    /// <summary>
    /// Use LSM-Tree storage engine with custom options.
    /// </summary>
    public WitDatabaseBuilder WithLsmTree(Action<LsmOptions> configure)
    {
        m_options.UseLsmTree = true;
        m_options.UseBTree = false;
        m_options.LsmOptions = new LsmOptions();
        configure(m_options.LsmOptions);
        return this;
    }

    /// <summary>
    /// Use LSM-Tree storage engine with the specified directory.
    /// </summary>
    public WitDatabaseBuilder WithLsmTree(string directory)
    {
        m_options.UseLsmTree = true;
        m_options.UseBTree = false;
        m_options.LsmDirectory = directory;
        return this;
    }

    /// <summary>
    /// Use a custom key-value store implementation.
    /// </summary>
    public WitDatabaseBuilder WithStore(IKeyValueStore store)
    {
        m_options.KeyValueStore = store;
        return this;
    }

    #endregion

    #region Encryption

    /// <summary>
    /// Enable AES-GCM encryption with the specified 256-bit key.
    /// </summary>
    public WitDatabaseBuilder WithAesEncryption(byte[] key)
    {
        if (key.Length != 32)
            throw new ArgumentException("AES-256 requires a 32-byte key", nameof(key));
        
        m_options.CryptoProvider = new AesGcmCryptoProvider(key);
        return this;
    }

    /// <summary>
    /// Enable AES-GCM encryption with the specified 256-bit key and salt.
    /// </summary>
    public WitDatabaseBuilder WithAesEncryption(byte[] key, byte[] salt)
    {
        if (key.Length != 32)
            throw new ArgumentException("AES-256 requires a 32-byte key", nameof(key));
        if (salt.Length < 8)
            throw new ArgumentException("Salt must be at least 8 bytes", nameof(salt));
        
        m_options.CryptoProvider = new AesGcmCryptoProvider(key);
        m_encryptionSalt = salt;
        return this;
    }

    /// <summary>
    /// Use a custom crypto provider for encryption.
    /// </summary>
    public WitDatabaseBuilder WithEncryption(ICryptoProvider provider)
    {
        m_options.CryptoProvider = provider;
        return this;
    }

    /// <summary>
    /// Use a custom crypto provider for encryption with the specified salt.
    /// </summary>
    public WitDatabaseBuilder WithEncryption(ICryptoProvider provider, byte[] salt)
    {
        if (salt.Length < 8)
            throw new ArgumentException("Salt must be at least 8 bytes", nameof(salt));
        
        m_options.CryptoProvider = provider;
        m_encryptionSalt = salt;
        return this;
    }

    #endregion

    #region Transactions

    /// <summary>
    /// Enable transaction support (default).
    /// </summary>
    public WitDatabaseBuilder WithTransactions()
    {
        m_options.EnableTransactions = true;
        return this;
    }

    /// <summary>
    /// Enable transaction support with a custom journal.
    /// </summary>
    public WitDatabaseBuilder WithTransactions(ITransactionJournal journal)
    {
        m_options.EnableTransactions = true;
        m_options.TransactionJournal = journal;
        return this;
    }

    /// <summary>
    /// Disable transaction support (faster but no atomicity guarantees).
    /// </summary>
    public WitDatabaseBuilder WithoutTransactions()
    {
        m_options.EnableTransactions = false;
        return this;
    }

    #endregion

    #region Locking

    /// <summary>
    /// Enable file locking for concurrent access (default).
    /// </summary>
    public WitDatabaseBuilder WithFileLocking()
    {
        m_options.EnableFileLocking = true;
        return this;
    }

    /// <summary>
    /// Disable file locking (use only for single-process access).
    /// </summary>
    public WitDatabaseBuilder WithoutFileLocking()
    {
        m_options.EnableFileLocking = false;
        return this;
    }

    /// <summary>
    /// Set the lock timeout for concurrent operations.
    /// </summary>
    public WitDatabaseBuilder WithLockTimeout(TimeSpan timeout)
    {
        m_options.LockTimeout = timeout;
        return this;
    }

    #endregion

    #region Page/Cache Settings

    /// <summary>
    /// Set the page size (default: 4096 bytes).
    /// </summary>
    public WitDatabaseBuilder WithPageSize(int pageSize)
    {
        if (pageSize < DatabaseConstants.MIN_PAGE_SIZE || pageSize > DatabaseConstants.MAX_PAGE_SIZE)
            throw new ArgumentOutOfRangeException(nameof(pageSize), 
                $"Page size must be between {DatabaseConstants.MIN_PAGE_SIZE} and {DatabaseConstants.MAX_PAGE_SIZE}");
        
        m_options.PageSize = pageSize;
        return this;
    }

    /// <summary>
    /// Set the number of pages to cache in memory.
    /// </summary>
    public WitDatabaseBuilder WithCacheSize(int pages)
    {
        if (pages < 1)
            throw new ArgumentOutOfRangeException(nameof(pages), "Cache size must be at least 1");
        
        m_options.CacheSize = pages;
        return this;
    }

    #endregion

    #region Build

    /// <summary>
    /// Builds the database with the configured options.
    /// </summary>
    public WitDatabase Build()
    {
        ValidateConfiguration();
        
        var store = BuildStoreInternal();
        
        if (m_options.EnableTransactions)
        {
            var transactionalStore = BuildTransactionalStoreInternal(store);
            return new WitDatabase(transactionalStore, disposeStore: true);
        }
        
        return new WitDatabase(store, disposeStore: true);
    }

    /// <summary>
    /// Builds just the key-value store without transaction wrapper.
    /// </summary>
    public IKeyValueStore BuildStore()
    {
        ValidateConfiguration();
        return BuildStoreInternal();
    }

    /// <summary>
    /// Builds a transactional store.
    /// </summary>
    public ITransactionalStore BuildTransactionalStore()
    {
        ValidateConfiguration();
        var store = BuildStoreInternal();
        return BuildTransactionalStoreInternal(store);
    }

    private void ValidateConfiguration()
    {
        // Validate incompatible combinations FIRST (before checking if storage is configured)
        if (m_options.UseLsmTree && m_options.Storage != null)
        {
            throw new InvalidOperationException(
                "LSM-Tree uses directory-based storage and cannot use WithStorage(). " +
                "Use WithFilePath(directory) instead, or use BTree with WithStorage().");
        }

        // Validate custom store doesn't conflict with other settings
        if (m_options.KeyValueStore != null)
        {
            if (m_options.CryptoProvider != null)
            {
                throw new InvalidOperationException(
                    "Cannot use WithAesEncryption() or WithEncryption() with WithStore(). " +
                    "Configure encryption in your custom store implementation.");
            }
            if (m_options.Storage != null)
            {
                throw new InvalidOperationException(
                    "Cannot use WithStorage() with WithStore(). Choose one or the other.");
            }
        }

        // Validate storage is configured
        if (m_options.Storage == null && 
            m_options.KeyValueStore == null && 
            !m_options.UseMemoryStorage && 
            string.IsNullOrEmpty(m_options.FilePath) &&
            string.IsNullOrEmpty(m_options.LsmDirectory))
        {
            if (m_options.UseLsmTree)
            {
                throw new InvalidOperationException(
                    "LSM-Tree requires a directory path. Use WithFilePath(path) or WithLsmTree(directory).");
            }
            throw new InvalidOperationException(
                "Storage not configured. Use WithFilePath(path), WithMemoryStorage(), or WithStorage(storage).");
        }

        // Validate LSM-Tree has directory
        if (m_options.UseLsmTree && 
            m_options.KeyValueStore == null &&
            string.IsNullOrEmpty(m_options.LsmDirectory) && 
            string.IsNullOrEmpty(m_options.FilePath))
        {
            throw new InvalidOperationException(
                "LSM-Tree requires a directory path. Use WithFilePath(path) or WithLsmTree(directory).");
        }

        // Validate page size is power of 2
        if (!IsPowerOfTwo(m_options.PageSize))
        {
            throw new InvalidOperationException(
                $"Page size must be a power of 2. Got {m_options.PageSize}.");
        }
    }

    private static bool IsPowerOfTwo(int value)
    {
        return value > 0 && (value & (value - 1)) == 0;
    }

    private IKeyValueStore BuildStoreInternal()
    {
        // Use custom store if provided
        if (m_options.KeyValueStore != null)
            return m_options.KeyValueStore;

        // Build LSM-Tree store
        if (m_options.UseLsmTree)
        {
            var directory = m_options.LsmDirectory ?? m_options.FilePath!;
            
            var lsmOptions = m_options.LsmOptions ?? new LsmOptions();
            
            // Add encryption to LSM options if configured
            if (m_options.CryptoProvider != null)
            {
                lsmOptions.Encryptor = new BlockEncryptor(m_options.CryptoProvider, m_encryptionSalt);
            }
            
            return new LsmTreeStore(directory, lsmOptions);
        }

        // Build BTree store - use constructor that owns storage
        var storage = BuildStorage();
        return new BTreeStore(storage, m_options.CacheSize, ownsStorage: true);
    }

    private IStorage BuildStorage()
    {
        // Use custom storage if provided
        if (m_options.Storage != null)
            return m_options.Storage;

        IStorage baseStorage;
        
        // Calculate actual storage page size (including encryption overhead if needed)
        int storagePageSize = m_options.PageSize;
        if (m_options.CryptoProvider != null)
        {
            // PageEncryptor adds overhead: nonce + tag
            var overhead = m_options.CryptoProvider.Overhead;
            storagePageSize = m_options.PageSize + overhead;
        }
        
        if (m_options.UseMemoryStorage)
        {
            baseStorage = new MemoryStorage(storagePageSize);
        }
        else if (!string.IsNullOrEmpty(m_options.FilePath))
        {
            baseStorage = new FileStorage(m_options.FilePath, storagePageSize);
        }
        else
        {
            throw new InvalidOperationException("Storage not configured. Use WithFilePath() or WithMemoryStorage().");
        }

        // Wrap with encryption if configured
        if (m_options.CryptoProvider != null)
        {
            var encryptor = new PageEncryptor(m_options.CryptoProvider, m_encryptionSalt);
            return new EncryptedStorage(baseStorage, encryptor);
        }

        return baseStorage;
    }

    private ITransactionalStore BuildTransactionalStoreInternal(IKeyValueStore store)
    {
        LockManager? lockManager = null;
        
        if (m_options.EnableFileLocking)
        {
            lockManager = new LockManager(m_options.LockTimeout);
        }
        
        return new TransactionalStore(
            store, 
            m_options.TransactionJournal, 
            lockManager,
            ownsStore: true);
    }

    #endregion
}

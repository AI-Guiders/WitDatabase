using NUnit.Framework;
using OutWit.Database.AdoNet;

namespace OutWit.Database.AdoNet.Tests;

/// <summary>
/// Comprehensive tests for WitDbConnectionStringBuilder.
/// </summary>
[TestFixture]
public class WitDbConnectionStringBuilderTests
{
    #region Constructor Tests

    [Test]
    public void DefaultConstructorCreatesEmptyBuilder()
    {
        var builder = new WitDbConnectionStringBuilder();

        Assert.That(builder.DataSource, Is.Null);
        Assert.That(builder.ConnectionString, Is.Empty);
    }

    [Test]
    public void ConstructorWithConnectionStringParsesCorrectly()
    {
        var builder = new WitDbConnectionStringBuilder("Data Source=test.db;Password=secret");

        Assert.That(builder.DataSource, Is.EqualTo("test.db"));
        Assert.That(builder.Password, Is.EqualTo("secret"));
    }

    [Test]
    public void ConstructorWithEmptyConnectionStringCreatesEmptyBuilder()
    {
        var builder = new WitDbConnectionStringBuilder("");

        Assert.That(builder.DataSource, Is.Null);
    }

    #endregion

    #region DataSource Property Tests

    [Test]
    public void DataSourceGetSetWorksCorrectly()
    {
        var builder = new WitDbConnectionStringBuilder { DataSource = "mydb.witdb" };

        Assert.That(builder.DataSource, Is.EqualTo("mydb.witdb"));
    }

    [Test]
    public void DataSourceMemoryValueIsPreserved()
    {
        var builder = new WitDbConnectionStringBuilder { DataSource = ":memory:" };

        Assert.That(builder.DataSource, Is.EqualTo(":memory:"));
    }

    [Test]
    public void DataSourceWithPathIsPreserved()
    {
        var builder = new WitDbConnectionStringBuilder { DataSource = @"C:\Data\mydb.witdb" };

        Assert.That(builder.DataSource, Is.EqualTo(@"C:\Data\mydb.witdb"));
    }

    [Test]
    public void DataSourceAppearsInConnectionString()
    {
        var builder = new WitDbConnectionStringBuilder { DataSource = "test.db" };

        Assert.That(builder.ConnectionString, Does.Contain("Data Source=test.db"));
    }

    #endregion

    #region Mode Property Tests

    [Test]
    public void ModeDefaultIsReadWriteCreate()
    {
        var builder = new WitDbConnectionStringBuilder();

        Assert.That(builder.Mode, Is.EqualTo(WitDbConnectionMode.ReadWriteCreate));
    }

    [Test]
    public void ModeGetSetWorksCorrectly()
    {
        var builder = new WitDbConnectionStringBuilder { Mode = WitDbConnectionMode.Memory };

        Assert.That(builder.Mode, Is.EqualTo(WitDbConnectionMode.Memory));
    }

    [Test]
    public void ModeParsesCaseInsensitive()
    {
        var builder = new WitDbConnectionStringBuilder("Mode=readonly");

        Assert.That(builder.Mode, Is.EqualTo(WitDbConnectionMode.ReadOnly));
    }

    [TestCase(WitDbConnectionMode.ReadWriteCreate)]
    [TestCase(WitDbConnectionMode.ReadWrite)]
    [TestCase(WitDbConnectionMode.ReadOnly)]
    [TestCase(WitDbConnectionMode.Memory)]
    public void ModeAllValuesWorkCorrectly(WitDbConnectionMode mode)
    {
        var builder = new WitDbConnectionStringBuilder { Mode = mode };

        Assert.That(builder.Mode, Is.EqualTo(mode));
    }

    #endregion

    #region Store Property Tests (Provider Key)

    [Test]
    public void StoreDefaultIsNull()
    {
        var builder = new WitDbConnectionStringBuilder();

        Assert.That(builder.Store, Is.Null);
    }

    [Test]
    public void StoreGetSetWorksCorrectly()
    {
        var builder = new WitDbConnectionStringBuilder { Store = "lsm" };

        Assert.That(builder.Store, Is.EqualTo("lsm"));
    }

    [Test]
    public void StoreParsesCaseInsensitive()
    {
        var builder = new WitDbConnectionStringBuilder("Store=LSM");

        Assert.That(builder.Store?.ToLowerInvariant(), Is.EqualTo("lsm"));
    }

    [TestCase("btree")]
    [TestCase("lsm")]
    [TestCase("inmemory")]
    public void StoreAllKnownValuesWorkCorrectly(string store)
    {
        var builder = new WitDbConnectionStringBuilder { Store = store };

        Assert.That(builder.Store, Is.EqualTo(store));
    }

    [Test]
    public void StoreCustomProviderKeyIsPreserved()
    {
        var builder = new WitDbConnectionStringBuilder { Store = "my-custom-store" };

        Assert.That(builder.Store, Is.EqualTo("my-custom-store"));
    }

    #endregion

    #region Encryption Property Tests (Provider Key)

    [Test]
    public void EncryptionDefaultIsNull()
    {
        var builder = new WitDbConnectionStringBuilder();

        Assert.That(builder.Encryption, Is.Null);
    }

    [Test]
    public void EncryptionGetSetWorksCorrectly()
    {
        var builder = new WitDbConnectionStringBuilder { Encryption = "aes-gcm" };

        Assert.That(builder.Encryption, Is.EqualTo("aes-gcm"));
    }

    [Test]
    public void EncryptionParsesCaseInsensitive()
    {
        var builder = new WitDbConnectionStringBuilder("Encryption=AES-GCM");

        Assert.That(builder.Encryption?.ToLowerInvariant(), Is.EqualTo("aes-gcm"));
    }

    [TestCase("aes-gcm")]
    [TestCase("chacha20-poly1305")]
    public void EncryptionAllKnownValuesWorkCorrectly(string encryption)
    {
        var builder = new WitDbConnectionStringBuilder { Encryption = encryption };

        Assert.That(builder.Encryption, Is.EqualTo(encryption));
    }

    [Test]
    public void EncryptionCustomProviderKeyIsPreserved()
    {
        var builder = new WitDbConnectionStringBuilder { Encryption = "my-custom-crypto" };

        Assert.That(builder.Encryption, Is.EqualTo("my-custom-crypto"));
    }

    #endregion

    #region Password Property Tests

    [Test]
    public void PasswordDefaultIsNull()
    {
        var builder = new WitDbConnectionStringBuilder();

        Assert.That(builder.Password, Is.Null);
    }

    [Test]
    public void PasswordGetSetWorksCorrectly()
    {
        var builder = new WitDbConnectionStringBuilder { Password = "secret123" };

        Assert.That(builder.Password, Is.EqualTo("secret123"));
    }

    [Test]
    public void PasswordWithSpecialCharactersIsPreserved()
    {
        var builder = new WitDbConnectionStringBuilder { Password = "p@ss=w;rd\"'" };

        Assert.That(builder.Password, Is.EqualTo("p@ss=w;rd\"'"));
    }

    #endregion

    #region User Property Tests

    [Test]
    public void UserDefaultIsNull()
    {
        var builder = new WitDbConnectionStringBuilder();

        Assert.That(builder.User, Is.Null);
    }

    [Test]
    public void UserGetSetWorksCorrectly()
    {
        var builder = new WitDbConnectionStringBuilder { User = "admin" };

        Assert.That(builder.User, Is.EqualTo("admin"));
    }

    [Test]
    public void UserAndPasswordCombinationWorks()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            User = "tenant1",
            Password = "secret123"
        };

        Assert.That(builder.User, Is.EqualTo("tenant1"));
        Assert.That(builder.Password, Is.EqualTo("secret123"));
    }

    [Test]
    public void UserAppearsInConnectionString()
    {
        var builder = new WitDbConnectionStringBuilder { User = "myuser" };

        Assert.That(builder.ConnectionString, Does.Contain("User=myuser"));
    }

    #endregion

    #region IsolationLevel Property Tests

    [Test]
    public void IsolationLevelDefaultIsReadCommitted()
    {
        var builder = new WitDbConnectionStringBuilder();

        Assert.That(builder.IsolationLevel, Is.EqualTo(WitDbIsolationLevel.ReadCommitted));
    }

    [Test]
    public void IsolationLevelGetSetWorksCorrectly()
    {
        var builder = new WitDbConnectionStringBuilder { IsolationLevel = WitDbIsolationLevel.Snapshot };

        Assert.That(builder.IsolationLevel, Is.EqualTo(WitDbIsolationLevel.Snapshot));
    }

    [Test]
    public void IsolationLevelParsesCaseInsensitive()
    {
        var builder = new WitDbConnectionStringBuilder("Isolation Level=snapshot");

        Assert.That(builder.IsolationLevel, Is.EqualTo(WitDbIsolationLevel.Snapshot));
    }

    [TestCase(WitDbIsolationLevel.ReadUncommitted)]
    [TestCase(WitDbIsolationLevel.ReadCommitted)]
    [TestCase(WitDbIsolationLevel.RepeatableRead)]
    [TestCase(WitDbIsolationLevel.Serializable)]
    [TestCase(WitDbIsolationLevel.Snapshot)]
    public void IsolationLevelAllValuesWorkCorrectly(WitDbIsolationLevel level)
    {
        var builder = new WitDbConnectionStringBuilder { IsolationLevel = level };

        Assert.That(builder.IsolationLevel, Is.EqualTo(level));
    }

    #endregion

    #region MVCC Property Tests

    [Test]
    public void MvccDefaultIsTrue()
    {
        var builder = new WitDbConnectionStringBuilder();

        Assert.That(builder.Mvcc, Is.True);
    }

    [Test]
    public void MvccGetSetWorksCorrectly()
    {
        var builder = new WitDbConnectionStringBuilder { Mvcc = false };

        Assert.That(builder.Mvcc, Is.False);
    }

    [Test]
    public void MvccParsesFromConnectionString()
    {
        var builder = new WitDbConnectionStringBuilder("MVCC=false");

        Assert.That(builder.Mvcc, Is.False);
    }

    #endregion

    #region Transactions Property Tests

    [Test]
    public void TransactionsDefaultIsTrue()
    {
        var builder = new WitDbConnectionStringBuilder();

        Assert.That(builder.Transactions, Is.True);
    }

    [Test]
    public void TransactionsGetSetWorksCorrectly()
    {
        var builder = new WitDbConnectionStringBuilder { Transactions = false };

        Assert.That(builder.Transactions, Is.False);
    }

    #endregion

    #region Cache Property Tests (Provider Key)

    [Test]
    public void CacheDefaultIsNull()
    {
        var builder = new WitDbConnectionStringBuilder();

        Assert.That(builder.Cache, Is.Null);
    }

    [Test]
    public void CacheGetSetWorksCorrectly()
    {
        var builder = new WitDbConnectionStringBuilder { Cache = "lru" };

        Assert.That(builder.Cache, Is.EqualTo("lru"));
    }

    [Test]
    public void CacheCustomProviderKeyIsPreserved()
    {
        var builder = new WitDbConnectionStringBuilder { Cache = "arc" };

        Assert.That(builder.Cache, Is.EqualTo("arc"));
    }

    #endregion

    #region Journal Property Tests (Provider Key)

    [Test]
    public void JournalDefaultIsNull()
    {
        var builder = new WitDbConnectionStringBuilder();

        Assert.That(builder.Journal, Is.Null);
    }

    [Test]
    public void JournalGetSetWorksCorrectly()
    {
        var builder = new WitDbConnectionStringBuilder { Journal = "wal" };

        Assert.That(builder.Journal, Is.EqualTo("wal"));
    }

    [TestCase("wal")]
    [TestCase("rollback")]
    public void JournalKnownValuesWorkCorrectly(string journal)
    {
        var builder = new WitDbConnectionStringBuilder { Journal = journal };

        Assert.That(builder.Journal, Is.EqualTo(journal));
    }

    #endregion

    #region Pooling Property Tests

    [Test]
    public void PoolingDefaultIsFalse()
    {
        var builder = new WitDbConnectionStringBuilder();

        Assert.That(builder.Pooling, Is.False);
    }

    [Test]
    public void PoolingGetSetWorksCorrectly()
    {
        var builder = new WitDbConnectionStringBuilder { Pooling = true };

        Assert.That(builder.Pooling, Is.True);
    }

    #endregion

    #region MinPoolSize Property Tests

    [Test]
    public void MinPoolSizeDefaultIs1()
    {
        var builder = new WitDbConnectionStringBuilder();

        Assert.That(builder.MinPoolSize, Is.EqualTo(1));
    }

    [Test]
    public void MinPoolSizeGetSetWorksCorrectly()
    {
        var builder = new WitDbConnectionStringBuilder { MinPoolSize = 5 };

        Assert.That(builder.MinPoolSize, Is.EqualTo(5));
    }

    #endregion

    #region MaxPoolSize Property Tests

    [Test]
    public void MaxPoolSizeDefaultIs100()
    {
        var builder = new WitDbConnectionStringBuilder();

        Assert.That(builder.MaxPoolSize, Is.EqualTo(100));
    }

    [Test]
    public void MaxPoolSizeGetSetWorksCorrectly()
    {
        var builder = new WitDbConnectionStringBuilder { MaxPoolSize = 50 };

        Assert.That(builder.MaxPoolSize, Is.EqualTo(50));
    }

    #endregion

    #region DefaultTimeout Property Tests

    [Test]
    public void DefaultTimeoutDefaultIs30()
    {
        var builder = new WitDbConnectionStringBuilder();

        Assert.That(builder.DefaultTimeout, Is.EqualTo(30));
    }

    [Test]
    public void DefaultTimeoutGetSetWorksCorrectly()
    {
        var builder = new WitDbConnectionStringBuilder { DefaultTimeout = 120 };

        Assert.That(builder.DefaultTimeout, Is.EqualTo(120));
    }

    #endregion

    #region ReadOnly Property Tests

    [Test]
    public void ReadOnlyDefaultIsFalse()
    {
        var builder = new WitDbConnectionStringBuilder();

        Assert.That(builder.ReadOnly, Is.False);
    }

    [Test]
    public void ReadOnlyGetSetWorksCorrectly()
    {
        var builder = new WitDbConnectionStringBuilder { ReadOnly = true };

        Assert.That(builder.ReadOnly, Is.True);
    }

    #endregion

    #region Provider Parameters Tests

    [Test]
    public void GetProviderParametersReturnsNonCoreParameters()
    {
        var builder = new WitDbConnectionStringBuilder { DataSource = "test.db" };
        builder["CustomParam"] = "value";
        builder["AnotherParam"] = 123;
        
        var providerParams = builder.GetProviderParameters().ToList();
        
        Assert.That(providerParams, Has.Count.EqualTo(2));
        Assert.That(providerParams.Any(p => p.Key == "CustomParam" && p.Value?.ToString() == "value"), Is.True);
        Assert.That(providerParams.Any(p => p.Key == "AnotherParam" && p.Value?.ToString() == "123"), Is.True);
    }

    [Test]
    public void GetProviderParametersExcludesCoreParameters()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.db",
            Store = "btree",
            Encryption = "aes-gcm",
            Password = "secret"
        };
        builder["CustomParam"] = "value";
        
        var providerParams = builder.GetProviderParameters().ToList();
        
        Assert.That(providerParams, Has.Count.EqualTo(1));
        Assert.That(providerParams[0].Key, Is.EqualTo("CustomParam"));
    }

    [Test]
    public void ProviderParametersCanContainAnything()
    {
        var builder = new WitDbConnectionStringBuilder { DataSource = "test.db" };
        builder["MemTableSize"] = 67108864;
        builder["BlockSize"] = 8192;
        builder["EnableWal"] = true;
        
        var providerParams = builder.GetProviderParameters().ToDictionary(p => p.Key, p => p.Value);
        
        Assert.That(providerParams["MemTableSize"]?.ToString(), Is.EqualTo("67108864"));
        Assert.That(providerParams["BlockSize"]?.ToString(), Is.EqualTo("8192"));
        Assert.That(providerParams["EnableWal"]?.ToString(), Is.EqualTo("True"));
    }

    #endregion

    #region Validation Tests

    [Test]
    public void ValidateReturnsEmptyForValidBuilder()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.db"
        };

        var errors = builder.Validate();

        Assert.That(errors, Is.Empty);
    }

    [Test]
    public void ValidateReturnsErrorWhenEncryptionWithoutPassword()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.db",
            Encryption = "aes-gcm"
        };

        var errors = builder.Validate();

        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0], Does.Contain("Password is required"));
    }

    [Test]
    public void ValidateReturnsNoErrorWhenEncryptionWithPassword()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.db",
            Encryption = "aes-gcm",
            Password = "secret"
        };

        var errors = builder.Validate();

        Assert.That(errors, Is.Empty);
    }

    [Test]
    public void ValidateReturnsErrorWhenMinPoolSizeGreaterThanMax()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.db",
            MinPoolSize = 50,
            MaxPoolSize = 10
        };

        var errors = builder.Validate();

        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0], Does.Contain("Min Pool Size").And.Contain("Max Pool Size"));
    }

    [Test]
    public void ValidateReturnsErrorWhenDataSourceMissingAndNotMemory()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            Mode = WitDbConnectionMode.ReadWriteCreate
        };

        var errors = builder.Validate();

        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0], Does.Contain("Data Source"));
    }

    [Test]
    public void ValidateNoErrorWhenMemoryModeWithoutDataSource()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            Mode = WitDbConnectionMode.Memory
        };

        var errors = builder.Validate();

        Assert.That(errors, Is.Empty);
    }

    [Test]
    public void ThrowIfInvalidThrowsForInvalidBuilder()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            Encryption = "aes-gcm"
            // Missing password
        };

        Assert.Throws<ArgumentException>(() => builder.ThrowIfInvalid());
    }

    [Test]
    public void ThrowIfInvalidDoesNotThrowForValidBuilder()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.db"
        };

        Assert.DoesNotThrow(() => builder.ThrowIfInvalid());
    }

    #endregion

    #region Connection String Parsing Tests

    [Test]
    public void ComplexConnectionStringParsesCorrectly()
    {
        var connectionString = "Data Source=mydb.witdb;Store=lsm;Encryption=aes-gcm;Password=secret123;" +
                               "User=admin;Isolation Level=Snapshot;MVCC=true";

        var builder = new WitDbConnectionStringBuilder(connectionString);

        Assert.That(builder.DataSource, Is.EqualTo("mydb.witdb"));
        Assert.That(builder.Store?.ToLowerInvariant(), Is.EqualTo("lsm"));
        Assert.That(builder.Encryption?.ToLowerInvariant(), Is.EqualTo("aes-gcm"));
        Assert.That(builder.Password, Is.EqualTo("secret123"));
        Assert.That(builder.User, Is.EqualTo("admin"));
        Assert.That(builder.IsolationLevel, Is.EqualTo(WitDbIsolationLevel.Snapshot));
        Assert.That(builder.Mvcc, Is.True);
    }

    [Test]
    public void LsmOptionsSetViaIndexer()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "./lsm_data",
            Store = "lsm"
        };
        builder["MemTableSize"] = 67108864;
        builder["BlockSize"] = 8192;
        builder["EnableWal"] = true;
        builder["SyncWrites"] = false;
        
        var providerParams = builder.GetProviderParameters().ToDictionary(p => p.Key, p => p.Value);

        Assert.That(builder.Store?.ToLowerInvariant(), Is.EqualTo("lsm"));
        Assert.That(providerParams["MemTableSize"]?.ToString(), Is.EqualTo("67108864"));
        Assert.That(providerParams["BlockSize"]?.ToString(), Is.EqualTo("8192"));
        Assert.That(providerParams["EnableWal"]?.ToString(), Is.EqualTo("True"));
        Assert.That(providerParams["SyncWrites"]?.ToString(), Is.EqualTo("False"));
    }

    [Test]
    public void PoolingConnectionStringParsesCorrectly()
    {
        var connectionString = "Data Source=test.db;Pooling=true;Min Pool Size=5;Max Pool Size=50";

        var builder = new WitDbConnectionStringBuilder(connectionString);

        Assert.That(builder.Pooling, Is.True);
        Assert.That(builder.MinPoolSize, Is.EqualTo(5));
        Assert.That(builder.MaxPoolSize, Is.EqualTo(50));
    }

    #endregion

    #region Connection String Round-Trip Tests

    [Test]
    public void ConnectionStringRoundTripPreservesDataSource()
    {
        var original = new WitDbConnectionStringBuilder { DataSource = "test.db" };
        var parsed = new WitDbConnectionStringBuilder(original.ConnectionString);

        Assert.That(parsed.DataSource, Is.EqualTo(original.DataSource));
    }

    [Test]
    public void ConnectionStringRoundTripPreservesAllSettings()
    {
        var original = new WitDbConnectionStringBuilder
        {
            DataSource = "mydb.witdb",
            Store = "lsm",
            Encryption = "aes-gcm",
            Password = "secret",
            User = "admin",
            IsolationLevel = WitDbIsolationLevel.Snapshot,
            Mvcc = true,
            Transactions = true,
            Cache = "lru",
            Journal = "wal",
            Pooling = true,
            MinPoolSize = 5,
            MaxPoolSize = 50,
            DefaultTimeout = 120,
            ReadOnly = true
        };

        var parsed = new WitDbConnectionStringBuilder(original.ConnectionString);

        Assert.That(parsed.DataSource, Is.EqualTo(original.DataSource));
        Assert.That(parsed.Store, Is.EqualTo(original.Store));
        Assert.That(parsed.Encryption, Is.EqualTo(original.Encryption));
        Assert.That(parsed.Password, Is.EqualTo(original.Password));
        Assert.That(parsed.User, Is.EqualTo(original.User));
        Assert.That(parsed.IsolationLevel, Is.EqualTo(original.IsolationLevel));
        Assert.That(parsed.Mvcc, Is.EqualTo(original.Mvcc));
        Assert.That(parsed.Transactions, Is.EqualTo(original.Transactions));
        Assert.That(parsed.Cache, Is.EqualTo(original.Cache));
        Assert.That(parsed.Journal, Is.EqualTo(original.Journal));
        Assert.That(parsed.Pooling, Is.EqualTo(original.Pooling));
        Assert.That(parsed.MinPoolSize, Is.EqualTo(original.MinPoolSize));
        Assert.That(parsed.MaxPoolSize, Is.EqualTo(original.MaxPoolSize));
        Assert.That(parsed.DefaultTimeout, Is.EqualTo(original.DefaultTimeout));
        Assert.That(parsed.ReadOnly, Is.EqualTo(original.ReadOnly));
    }

    #endregion

    #region Edge Cases

    [Test]
    public void EmptyPasswordIsDistinctFromNull()
    {
        var builder = new WitDbConnectionStringBuilder { Password = "" };

        Assert.That(builder.Password, Is.EqualTo(""));
    }

    [Test]
    public void InvalidEnumValueReturnsDefault()
    {
        var builder = new WitDbConnectionStringBuilder("Isolation Level=InvalidValue");

        Assert.That(builder.IsolationLevel, Is.EqualTo(WitDbIsolationLevel.ReadCommitted));
    }

    [Test]
    public void WhitespaceInValuesIsPreserved()
    {
        var builder = new WitDbConnectionStringBuilder { DataSource = "  path with spaces  " };

        Assert.That(builder.DataSource, Does.Contain("path with spaces"));
    }

    [Test]
    public void NullValuesAreTreatedAsUnset()
    {
        var builder = new WitDbConnectionStringBuilder { DataSource = null };

        Assert.That(builder.DataSource, Is.Null);
    }

    #endregion

    #region Usage Scenario Tests

    [Test]
    public void MinimalConnectionString()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "mydb.witdb"
        };

        Assert.That(builder.Validate(), Is.Empty);
        Assert.That(builder.Store, Is.Null); // Will use default (btree)
        Assert.That(builder.Encryption, Is.Null); // No encryption
    }

    [Test]
    public void InMemoryDatabase()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = ":memory:",
            Mode = WitDbConnectionMode.Memory
        };

        Assert.That(builder.Validate(), Is.Empty);
    }

    [Test]
    public void EncryptedDatabaseWithPassword()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "secure.witdb",
            Encryption = "aes-gcm",
            Password = "MySecurePassword123!"
        };

        Assert.That(builder.Validate(), Is.Empty);
        Assert.That(builder.User, Is.Null);
    }

    [Test]
    public void EncryptedDatabaseWithUserAndPassword()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "tenant.witdb",
            Encryption = "aes-gcm",
            User = "tenant1",
            Password = "TenantSecret123"
        };

        Assert.That(builder.Validate(), Is.Empty);
        Assert.That(builder.User, Is.EqualTo("tenant1"));
        Assert.That(builder.Password, Is.EqualTo("TenantSecret123"));
    }

    [Test]
    public void LsmTreeWithProviderParameters()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "./lsm_data",
            Store = "lsm"
        };
        builder["MemTableSize"] = 64 * 1024 * 1024;
        builder["BlockCacheSize"] = 128 * 1024 * 1024;
        builder["BackgroundCompaction"] = true;

        Assert.That(builder.Validate(), Is.Empty);
        Assert.That(builder.Store, Is.EqualTo("lsm"));
        
        var providerParams = builder.GetProviderParameters().ToDictionary(p => p.Key, p => p.Value);
        Assert.That(providerParams["MemTableSize"]?.ToString(), Is.EqualTo((64 * 1024 * 1024).ToString()));
    }

    [Test]
    public void HighConcurrencyWithMvcc()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "concurrent.witdb",
            Mvcc = true,
            IsolationLevel = WitDbIsolationLevel.Snapshot
        };

        Assert.That(builder.Validate(), Is.Empty);
    }

    [Test]
    public void ConnectionPooledDatabase()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "pooled.witdb",
            Pooling = true,
            MinPoolSize = 5,
            MaxPoolSize = 50
        };

        Assert.That(builder.Validate(), Is.Empty);
    }

    [Test]
    public void BlazorWasmOptimized()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "wasm.witdb",
            Encryption = "aes-gcm",
            Password = "WasmPassword"
        };
        builder["FastEncryption"] = true; // Pass as provider parameter
        builder["CacheSize"] = 500;

        Assert.That(builder.Validate(), Is.Empty);
    }

    [Test]
    public void ChaCha20Encryption()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "secure.witdb",
            Encryption = "chacha20-poly1305",
            Password = "MyPassword"
        };

        Assert.That(builder.Validate(), Is.Empty);
        Assert.That(builder.Encryption, Is.EqualTo("chacha20-poly1305"));
    }

    #endregion
}

using NUnit.Framework;

namespace OutWit.Database.AdoNet.Tests.ConnectionStringBuilder;

/// <summary>
/// Tests for real-world usage scenarios of WitDbConnectionStringBuilder.
/// </summary>
[TestFixture]
public class WitDbConnectionStringBuilderScenariosTests
{
    #region Minimal Configuration Scenarios

    [Test]
    public void MinimalFileDatabaseTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "mydb.witdb"
        };

        Assert.That(builder.Validate(), Is.Empty);
        Assert.That(builder.Store, Is.Null, "Should use default store (btree)");
        Assert.That(builder.Encryption, Is.Null, "Should have no encryption");
        Assert.That(builder.Mvcc, Is.True, "MVCC should be enabled by default");
        Assert.That(builder.Transactions, Is.True, "Transactions should be enabled by default");
    }

    [Test]
    public void InMemoryDatabaseTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = ":memory:",
            Mode = WitDbConnectionMode.Memory
        };

        Assert.That(builder.Validate(), Is.Empty);
    }

    [Test]
    public void MemoryModeOnlyTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            Mode = WitDbConnectionMode.Memory
        };

        Assert.That(builder.Validate(), Is.Empty);
    }

    [Test]
    public void ReadOnlyDatabaseTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "existing.witdb",
            Mode = WitDbConnectionMode.ReadOnly,
            ReadOnly = true
        };

        Assert.That(builder.Validate(), Is.Empty);
        Assert.That(builder.Mode, Is.EqualTo(WitDbConnectionMode.ReadOnly));
        Assert.That(builder.ReadOnly, Is.True);
    }

    #endregion

    #region Encryption Scenarios

    [Test]
    public void PasswordEncryptedDatabaseTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "secure.witdb",
            Encryption = "aes-gcm",
            Password = "MySecurePassword123!"
        };

        Assert.That(builder.Validate(), Is.Empty);
        Assert.That(builder.User, Is.Null, "Should have no user for simple password encryption");
    }

    [Test]
    public void UserPasswordEncryptedDatabaseTest()
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
    public void ChaCha20EncryptionTest()
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

    [Test]
    public void CustomEncryptionProviderTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "custom.witdb",
            Encryption = "my-custom-aead",
            Password = "secret"
        };

        Assert.That(builder.Validate(), Is.Empty);
        Assert.That(builder.Encryption, Is.EqualTo("my-custom-aead"));
    }

    #endregion

    #region Storage Engine Scenarios

    [Test]
    public void BTreeStoreTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "btree.witdb",
            Store = "btree"
        };

        Assert.That(builder.Validate(), Is.Empty);
        Assert.That(builder.Store, Is.EqualTo("btree"));
    }

    [Test]
    public void LsmTreeStoreTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "./lsm_data",
            Store = "lsm"
        };

        Assert.That(builder.Validate(), Is.Empty);
        Assert.That(builder.Store, Is.EqualTo("lsm"));
    }

    [Test]
    public void LsmTreeWithFullConfigurationTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "./lsm_data",
            Store = "lsm"
        };
        builder["MemTableSize"] = 64 * 1024 * 1024;
        builder["BlockSize"] = 8192;
        builder["EnableWal"] = true;
        builder["SyncWrites"] = true;
        builder["CompactionTrigger"] = 4;
        builder["BlockCacheSize"] = 128 * 1024 * 1024;
        builder["BackgroundCompaction"] = true;

        Assert.That(builder.Validate(), Is.Empty);

        var providerParams = builder.GetProviderParameters().ToDictionary(p => p.Key, p => p.Value);
        Assert.That(providerParams.Count, Is.EqualTo(7));
    }

    [Test]
    public void InMemoryStoreTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = ":memory:",
            Store = "inmemory"
        };

        Assert.That(builder.Validate(), Is.Empty);
    }

    [Test]
    public void CustomStoreProviderTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "./custom_data",
            Store = "rocksdb"
        };

        Assert.That(builder.Validate(), Is.Empty);
        Assert.That(builder.Store, Is.EqualTo("rocksdb"));
    }

    #endregion

    #region Transaction and MVCC Scenarios

    [Test]
    public void HighConcurrencyWithMvccTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "concurrent.witdb",
            Mvcc = true,
            IsolationLevel = WitDbIsolationLevel.Snapshot
        };

        Assert.That(builder.Validate(), Is.Empty);
        Assert.That(builder.Mvcc, Is.True);
        Assert.That(builder.IsolationLevel, Is.EqualTo(WitDbIsolationLevel.Snapshot));
    }

    [Test]
    public void SerializableIsolationTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "serial.witdb",
            Mvcc = true,
            IsolationLevel = WitDbIsolationLevel.Serializable
        };

        Assert.That(builder.Validate(), Is.Empty);
        Assert.That(builder.IsolationLevel, Is.EqualTo(WitDbIsolationLevel.Serializable));
    }

    [Test]
    public void TransactionsDisabledTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "fast.witdb",
            Transactions = false
        };

        Assert.That(builder.Validate(), Is.Empty);
        Assert.That(builder.Transactions, Is.False);
    }

    [Test]
    public void MvccDisabledTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "simple.witdb",
            Mvcc = false
        };

        Assert.That(builder.Validate(), Is.Empty);
        Assert.That(builder.Mvcc, Is.False);
    }

    #endregion

    #region Pooling Scenarios

    [Test]
    public void ConnectionPoolingTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "pooled.witdb",
            Pooling = true,
            MinPoolSize = 5,
            MaxPoolSize = 50
        };

        Assert.That(builder.Validate(), Is.Empty);
        Assert.That(builder.Pooling, Is.True);
        Assert.That(builder.MinPoolSize, Is.EqualTo(5));
        Assert.That(builder.MaxPoolSize, Is.EqualTo(50));
    }

    [Test]
    public void PoolingDisabledTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "nopooling.witdb",
            Pooling = false
        };

        Assert.That(builder.Validate(), Is.Empty);
        Assert.That(builder.Pooling, Is.False);
    }

    [Test]
    public void SingleConnectionPoolTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "single.witdb",
            Pooling = true,
            MinPoolSize = 1,
            MaxPoolSize = 1
        };

        Assert.That(builder.Validate(), Is.Empty);
    }

    #endregion

    #region Blazor WebAssembly Scenarios

    [Test]
    public void BlazorWasmOptimizedTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "wasm.witdb",
            Encryption = "aes-gcm",
            Password = "WasmPassword"
        };
        builder["FastEncryption"] = true;
        builder["CacheSize"] = 500;

        Assert.That(builder.Validate(), Is.Empty);
        
        var providerParams = builder.GetProviderParameters().ToDictionary(p => p.Key, p => p.Value);
        Assert.That(providerParams["FastEncryption"]?.ToString(), Is.EqualTo("True"));
        Assert.That(providerParams["CacheSize"]?.ToString(), Is.EqualTo("500"));
    }

    [Test]
    public void BlazorWasmInMemoryTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            Mode = WitDbConnectionMode.Memory,
            Store = "inmemory"
        };
        builder["CacheSize"] = 200;

        Assert.That(builder.Validate(), Is.Empty);
    }

    [Test]
    public void BlazorWasmWithIndexedDbBackendTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "idb://myapp",
            Store = "indexeddb"
        };

        Assert.That(builder.Validate(), Is.Empty);
        Assert.That(builder.Store, Is.EqualTo("indexeddb"));
    }

    #endregion

    #region Full Production Configuration Scenarios

    [Test]
    public void FullProductionConfigurationTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "production.witdb",
            Mode = WitDbConnectionMode.ReadWriteCreate,
            Store = "btree",
            Encryption = "aes-gcm",
            Password = "ProductionSecret!",
            User = "app_service",
            Cache = "lru",
            Journal = "wal",
            IsolationLevel = WitDbIsolationLevel.Snapshot,
            Mvcc = true,
            Transactions = true,
            Pooling = true,
            MinPoolSize = 5,
            MaxPoolSize = 100,
            DefaultTimeout = 60
        };
        builder["CacheSize"] = 5000;
        builder["PageSize"] = 8192;

        Assert.That(builder.Validate(), Is.Empty);
        
        var connectionString = builder.ConnectionString;
        Assert.That(connectionString, Does.Contain("production.witdb"));
        Assert.That(connectionString, Does.Contain("aes-gcm"));
        Assert.That(connectionString, Does.Contain("wal"));
    }

    [Test]
    public void HighPerformanceWriteHeavyTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "./highperf_data",
            Store = "lsm",
            Transactions = false,
            Mvcc = false
        };
        builder["MemTableSize"] = 128 * 1024 * 1024;
        builder["SyncWrites"] = false;
        builder["BackgroundCompaction"] = true;

        Assert.That(builder.Validate(), Is.Empty);
    }

    [Test]
    public void TestingConfigurationTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            Mode = WitDbConnectionMode.Memory,
            Store = "inmemory",
            Transactions = true,
            Mvcc = true,
            IsolationLevel = WitDbIsolationLevel.Serializable
        };

        Assert.That(builder.Validate(), Is.Empty);
    }

    #endregion

    #region Connection String Format Scenarios

    [Test]
    public void ConnectionStringFromEnvironmentTest()
    {
        // Simulating what might come from environment variable
        var connectionString = "Data Source=/var/data/app.witdb;Encryption=aes-gcm;Password=EnvSecret123;Pooling=true";

        var builder = new WitDbConnectionStringBuilder(connectionString);

        Assert.That(builder.Validate(), Is.Empty);
        Assert.That(builder.DataSource, Is.EqualTo("/var/data/app.witdb"));
        Assert.That(builder.Encryption, Is.EqualTo("aes-gcm"));
        Assert.That(builder.Password, Is.EqualTo("EnvSecret123"));
        Assert.That(builder.Pooling, Is.True);
    }

    [Test]
    public void ConnectionStringWithWindowsPathTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = @"C:\ProgramData\MyApp\database.witdb"
        };

        var connectionString = builder.ConnectionString;
        var parsed = new WitDbConnectionStringBuilder(connectionString);

        Assert.That(parsed.DataSource, Does.Contain("ProgramData"));
    }

    [Test]
    public void ConnectionStringWithSpecialPasswordTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.db",
            Encryption = "aes-gcm",
            Password = "P@ss=w;rd\"with'quotes"
        };

        var connectionString = builder.ConnectionString;
        var parsed = new WitDbConnectionStringBuilder(connectionString);

        Assert.That(parsed.Password, Is.EqualTo(builder.Password));
    }

    #endregion
}

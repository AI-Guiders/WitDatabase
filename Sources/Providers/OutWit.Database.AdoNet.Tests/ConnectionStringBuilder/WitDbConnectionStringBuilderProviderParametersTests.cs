using NUnit.Framework;

namespace OutWit.Database.AdoNet.Tests.ConnectionStringBuilder;

/// <summary>
/// Tests for provider parameters (custom/extended parameters) in connection strings.
/// </summary>
[TestFixture]
public class WitDbConnectionStringBuilderProviderParametersTests
{
    #region Setting Custom Parameters

    [Test]
    public void IndexerSetStringWorksCorrectlyTest()
    {
        var builder = new WitDbConnectionStringBuilder { DataSource = "test.db" };
        builder["CustomParam"] = "value";

        Assert.That(builder["CustomParam"]?.ToString(), Is.EqualTo("value"));
    }

    [Test]
    public void IndexerSetIntWorksCorrectlyTest()
    {
        var builder = new WitDbConnectionStringBuilder { DataSource = "test.db" };
        builder["PageSize"] = 8192;

        Assert.That(builder["PageSize"]?.ToString(), Is.EqualTo("8192"));
    }

    [Test]
    public void IndexerSetBoolWorksCorrectlyTest()
    {
        var builder = new WitDbConnectionStringBuilder { DataSource = "test.db" };
        builder["EnableWal"] = true;

        Assert.That(builder["EnableWal"]?.ToString(), Is.EqualTo("True"));
    }

    [Test]
    public void IndexerSetLongWorksCorrectlyTest()
    {
        var builder = new WitDbConnectionStringBuilder { DataSource = "test.db" };
        builder["MemTableSize"] = 64L * 1024 * 1024;

        Assert.That(builder["MemTableSize"]?.ToString(), Is.EqualTo((64L * 1024 * 1024).ToString()));
    }

    [Test]
    public void IndexerSetNullRemovesParameterTest()
    {
        var builder = new WitDbConnectionStringBuilder { DataSource = "test.db" };
        builder["CustomParam"] = "value";
        builder["CustomParam"] = null;

        Assert.That(builder.ContainsKey("CustomParam"), Is.False);
    }

    #endregion

    #region GetProviderParameters Method

    [Test]
    public void GetProviderParametersReturnsCustomParametersTest()
    {
        var builder = new WitDbConnectionStringBuilder { DataSource = "test.db" };
        builder["CustomParam1"] = "value1";
        builder["CustomParam2"] = 123;

        var providerParams = builder.GetProviderParameters().ToList();

        Assert.That(providerParams, Has.Count.EqualTo(2));
        Assert.That(providerParams.Any(p => p.Key == "CustomParam1"), Is.True);
        Assert.That(providerParams.Any(p => p.Key == "CustomParam2"), Is.True);
    }

    [Test]
    public void GetProviderParametersExcludesCoreParametersTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.db",
            Store = "btree",
            Encryption = "aes-gcm",
            Password = "secret",
            User = "admin",
            Cache = "lru",
            Journal = "wal",
            Pooling = true,
            MinPoolSize = 5,
            MaxPoolSize = 50,
            DefaultTimeout = 120,
            ReadOnly = true,
            Mvcc = true,
            Transactions = true
        };
        builder["CustomParam"] = "value";

        var providerParams = builder.GetProviderParameters().ToList();

        Assert.That(providerParams, Has.Count.EqualTo(1));
        Assert.That(providerParams[0].Key, Is.EqualTo("CustomParam"));
    }

    [Test]
    public void GetProviderParametersReturnsEmptyWhenNoCustomParametersTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.db",
            Store = "btree"
        };

        var providerParams = builder.GetProviderParameters().ToList();

        Assert.That(providerParams, Is.Empty);
    }

    [Test]
    public void GetProviderParametersIncludesParametersFromConnectionStringTest()
    {
        var builder = new WitDbConnectionStringBuilder("Data Source=test.db;CustomParam=value");

        var providerParams = builder.GetProviderParameters().ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);

        Assert.That(providerParams.ContainsKey("CustomParam"), Is.True);
        Assert.That(providerParams["CustomParam"]?.ToString(), Is.EqualTo("value"));
    }

    #endregion

    #region LSM-Tree Specific Parameters

    [Test]
    public void LsmParametersCanBeSetViaIndexerTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "./lsm_data",
            Store = "lsm"
        };
        builder["MemTableSize"] = 64 * 1024 * 1024;
        builder["BlockSize"] = 8192;
        builder["EnableWal"] = true;
        builder["SyncWrites"] = false;
        builder["CompactionTrigger"] = 4;
        builder["BlockCacheSize"] = 128 * 1024 * 1024;
        builder["BackgroundCompaction"] = true;

        var providerParams = builder.GetProviderParameters().ToDictionary(p => p.Key, p => p.Value);

        Assert.That(providerParams["MemTableSize"]?.ToString(), Is.EqualTo((64 * 1024 * 1024).ToString()));
        Assert.That(providerParams["BlockSize"]?.ToString(), Is.EqualTo("8192"));
        Assert.That(providerParams["EnableWal"]?.ToString(), Is.EqualTo("True"));
        Assert.That(providerParams["SyncWrites"]?.ToString(), Is.EqualTo("False"));
        Assert.That(providerParams["CompactionTrigger"]?.ToString(), Is.EqualTo("4"));
        Assert.That(providerParams["BlockCacheSize"]?.ToString(), Is.EqualTo((128 * 1024 * 1024).ToString()));
        Assert.That(providerParams["BackgroundCompaction"]?.ToString(), Is.EqualTo("True"));
    }

    [Test]
    public void LsmParametersAppearInConnectionStringTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "./lsm_data",
            Store = "lsm"
        };
        builder["MemTableSize"] = 67108864;

        var connectionString = builder.ConnectionString;

        Assert.That(connectionString, Does.Contain("MemTableSize=67108864"));
    }

    #endregion

    #region WASM/Fast Encryption Parameters

    [Test]
    public void FastEncryptionCanBeSetAsProviderParameterTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "wasm.witdb",
            Encryption = "aes-gcm",
            Password = "secret"
        };
        builder["FastEncryption"] = true;

        var providerParams = builder.GetProviderParameters().ToDictionary(p => p.Key, p => p.Value);

        Assert.That(providerParams["FastEncryption"]?.ToString(), Is.EqualTo("True"));
    }

    [Test]
    public void FastEncryptionAlternateNamingCanBeSetTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "wasm.witdb",
            Encryption = "aes-gcm",
            Password = "secret"
        };
        builder["Fast Encryption"] = true;

        var providerParams = builder.GetProviderParameters().ToDictionary(p => p.Key, p => p.Value);

        Assert.That(providerParams["Fast Encryption"]?.ToString(), Is.EqualTo("True"));
    }

    #endregion

    #region Cache/Page Size Parameters

    [Test]
    public void CacheSizeCanBeSetAsProviderParameterTest()
    {
        var builder = new WitDbConnectionStringBuilder { DataSource = "test.db" };
        builder["CacheSize"] = 500;

        var providerParams = builder.GetProviderParameters().ToDictionary(p => p.Key, p => p.Value);

        Assert.That(providerParams["CacheSize"]?.ToString(), Is.EqualTo("500"));
    }

    [Test]
    public void PageSizeCanBeSetAsProviderParameterTest()
    {
        var builder = new WitDbConnectionStringBuilder { DataSource = "test.db" };
        builder["PageSize"] = 8192;

        var providerParams = builder.GetProviderParameters().ToDictionary(p => p.Key, p => p.Value);

        Assert.That(providerParams["PageSize"]?.ToString(), Is.EqualTo("8192"));
    }

    #endregion

    #region Parameter Type Preservation

    [Test]
    public void ProviderParametersPreserveOriginalTypesTest()
    {
        var builder = new WitDbConnectionStringBuilder { DataSource = "test.db" };
        builder["IntParam"] = 42;
        builder["LongParam"] = 100L;
        builder["BoolParam"] = true;
        builder["StringParam"] = "text";

        // DbConnectionStringBuilder converts values to strings internally
        Assert.That(builder["IntParam"]?.ToString(), Is.EqualTo("42"));
        Assert.That(builder["LongParam"]?.ToString(), Is.EqualTo("100"));
        Assert.That(builder["BoolParam"]?.ToString(), Is.EqualTo("True"));
        Assert.That(builder["StringParam"]?.ToString(), Is.EqualTo("text"));
    }

    [Test]
    public void ProviderParametersAfterRoundTripAreStringsTest()
    {
        var original = new WitDbConnectionStringBuilder { DataSource = "test.db" };
        original["IntParam"] = 42;

        var parsed = new WitDbConnectionStringBuilder(original.ConnectionString);

        // After parsing, all values are strings
        Assert.That(parsed["IntParam"]?.ToString(), Is.EqualTo("42"));
    }

    #endregion

    #region ContainsKey

    [Test]
    public void ContainsKeyReturnsTrueForCoreParametersTest()
    {
        var builder = new WitDbConnectionStringBuilder { DataSource = "test.db" };

        Assert.That(builder.ContainsKey("Data Source"), Is.True);
    }

    [Test]
    public void ContainsKeyReturnsTrueForCustomParametersTest()
    {
        var builder = new WitDbConnectionStringBuilder { DataSource = "test.db" };
        builder["CustomParam"] = "value";

        Assert.That(builder.ContainsKey("CustomParam"), Is.True);
    }

    [Test]
    public void ContainsKeyReturnsFalseForMissingParametersTest()
    {
        var builder = new WitDbConnectionStringBuilder { DataSource = "test.db" };

        Assert.That(builder.ContainsKey("NonExistent"), Is.False);
    }

    #endregion

    #region Keys Collection

    [Test]
    public void KeysContainsAllSetParametersTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.db",
            Store = "btree"
        };
        builder["CustomParam"] = "value";

        var keys = builder.Keys.Cast<string>().ToList();

        Assert.That(keys, Does.Contain("Data Source").IgnoreCase);
        Assert.That(keys, Does.Contain("Store").IgnoreCase);
        Assert.That(keys, Does.Contain("CustomParam"));
    }

    #endregion

    #region TryGetValue

    [Test]
    public void TryGetValueExistingKeyReturnsTrueTest()
    {
        var builder = new WitDbConnectionStringBuilder { DataSource = "test.db" };

        var result = builder.TryGetValue("Data Source", out var value);

        Assert.That(result, Is.True);
        Assert.That(value?.ToString(), Is.EqualTo("test.db"));
    }

    [Test]
    public void TryGetValueMissingKeyReturnsFalseTest()
    {
        var builder = new WitDbConnectionStringBuilder { DataSource = "test.db" };

        var result = builder.TryGetValue("NonExistent", out var value);

        Assert.That(result, Is.False);
        Assert.That(value, Is.Null);
    }

    #endregion
}

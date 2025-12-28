using NUnit.Framework;
using OutWit.Database.AdoNet;

namespace OutWit.Database.AdoNet.Tests;

/// <summary>
/// Tests for WitDb enum types used in connection strings.
/// </summary>
[TestFixture]
public class WitDbEnumTests
{
    #region WitDbConnectionMode Tests

    [Test]
    public void ConnectionModeHasExpectedValues()
    {
        Assert.That(Enum.GetValues<WitDbConnectionMode>(), Has.Length.EqualTo(4));
        Assert.That(Enum.IsDefined(WitDbConnectionMode.ReadWriteCreate), Is.True);
        Assert.That(Enum.IsDefined(WitDbConnectionMode.ReadWrite), Is.True);
        Assert.That(Enum.IsDefined(WitDbConnectionMode.ReadOnly), Is.True);
        Assert.That(Enum.IsDefined(WitDbConnectionMode.Memory), Is.True);
    }

    [TestCase("ReadWriteCreate", WitDbConnectionMode.ReadWriteCreate)]
    [TestCase("READWRITECREATE", WitDbConnectionMode.ReadWriteCreate)]
    [TestCase("readwritecreate", WitDbConnectionMode.ReadWriteCreate)]
    [TestCase("ReadWrite", WitDbConnectionMode.ReadWrite)]
    [TestCase("ReadOnly", WitDbConnectionMode.ReadOnly)]
    [TestCase("Memory", WitDbConnectionMode.Memory)]
    public void ConnectionModeParsesCorrectly(string value, WitDbConnectionMode expected)
    {
        Assert.That(Enum.Parse<WitDbConnectionMode>(value, ignoreCase: true), Is.EqualTo(expected));
    }

    #endregion

    #region WitDbIsolationLevel Tests

    [Test]
    public void IsolationLevelHasExpectedValues()
    {
        Assert.That(Enum.GetValues<WitDbIsolationLevel>(), Has.Length.EqualTo(5));
        Assert.That(Enum.IsDefined(WitDbIsolationLevel.ReadUncommitted), Is.True);
        Assert.That(Enum.IsDefined(WitDbIsolationLevel.ReadCommitted), Is.True);
        Assert.That(Enum.IsDefined(WitDbIsolationLevel.RepeatableRead), Is.True);
        Assert.That(Enum.IsDefined(WitDbIsolationLevel.Serializable), Is.True);
        Assert.That(Enum.IsDefined(WitDbIsolationLevel.Snapshot), Is.True);
    }

    [TestCase("ReadUncommitted", WitDbIsolationLevel.ReadUncommitted)]
    [TestCase("READUNCOMMITTED", WitDbIsolationLevel.ReadUncommitted)]
    [TestCase("ReadCommitted", WitDbIsolationLevel.ReadCommitted)]
    [TestCase("RepeatableRead", WitDbIsolationLevel.RepeatableRead)]
    [TestCase("Serializable", WitDbIsolationLevel.Serializable)]
    [TestCase("Snapshot", WitDbIsolationLevel.Snapshot)]
    [TestCase("SNAPSHOT", WitDbIsolationLevel.Snapshot)]
    public void IsolationLevelParsesCorrectly(string value, WitDbIsolationLevel expected)
    {
        Assert.That(Enum.Parse<WitDbIsolationLevel>(value, ignoreCase: true), Is.EqualTo(expected));
    }

    [Test]
    public void IsolationLevelOrderingIsCorrect()
    {
        // Isolation levels should increase in strictness (matching Core's IsolationLevel)
        Assert.That((int)WitDbIsolationLevel.ReadUncommitted, Is.EqualTo(0));
        Assert.That((int)WitDbIsolationLevel.ReadCommitted, Is.EqualTo(1));
        Assert.That((int)WitDbIsolationLevel.RepeatableRead, Is.EqualTo(2));
        Assert.That((int)WitDbIsolationLevel.Serializable, Is.EqualTo(3));
        Assert.That((int)WitDbIsolationLevel.Snapshot, Is.EqualTo(4));
    }

    #endregion
}

/// <summary>
/// Tests for connection string validation.
/// </summary>
[TestFixture]
public class WitDbConnectionStringValidationTests
{
    #region Valid Connection Strings

    [Test]
    public void MinimalFileConnectionStringIsValid()
    {
        var builder = new WitDbConnectionStringBuilder { DataSource = "test.db" };
        Assert.That(builder.Validate(), Is.Empty);
    }

    [Test]
    public void MinimalMemoryConnectionStringIsValid()
    {
        var builder = new WitDbConnectionStringBuilder { Mode = WitDbConnectionMode.Memory };
        Assert.That(builder.Validate(), Is.Empty);
    }

    [Test]
    public void FullyConfiguredConnectionStringIsValid()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "mydb.witdb",
            Mode = WitDbConnectionMode.ReadWriteCreate,
            Store = "btree",
            Encryption = "aes-gcm",
            Password = "SecurePassword123",
            User = "admin",
            IsolationLevel = WitDbIsolationLevel.Snapshot,
            Mvcc = true,
            Transactions = true,
            Pooling = true,
            MinPoolSize = 1,
            MaxPoolSize = 100,
            DefaultTimeout = 30,
            ReadOnly = false
        };

        Assert.That(builder.Validate(), Is.Empty);
    }

    [Test]
    public void LsmConnectionStringWithProviderParamsIsValid()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "./lsm_data",
            Store = "lsm"
        };
        // All LSM-specific params are now provider parameters
        builder["MemTableSize"] = 64 * 1024 * 1024;
        builder["BlockSize"] = 8192;

        Assert.That(builder.Validate(), Is.Empty);
    }

    #endregion

    #region Invalid Connection Strings

    [Test]
    public void EncryptionWithoutPasswordIsInvalid()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.db",
            Encryption = "aes-gcm"
            // Missing password
        };

        var errors = builder.Validate();
        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0], Does.Contain("Password"));
    }

    [Test]
    public void MinPoolSizeGreaterThanMaxIsInvalid()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.db",
            MinPoolSize = 100,
            MaxPoolSize = 10
        };

        var errors = builder.Validate();
        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0], Does.Contain("Min Pool Size").And.Contain("Max Pool Size"));
    }

    [Test]
    public void MissingDataSourceWithoutMemoryModeIsInvalid()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            Mode = WitDbConnectionMode.ReadWriteCreate
            // Missing DataSource
        };

        var errors = builder.Validate();
        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0], Does.Contain("Data Source"));
    }

    [Test]
    public void MultipleValidationErrorsAreReported()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            // Missing DataSource
            Encryption = "aes-gcm",
            // Missing Password
            MinPoolSize = 100,
            MaxPoolSize = 10 // Invalid
        };

        var errors = builder.Validate();
        Assert.That(errors.Count, Is.GreaterThanOrEqualTo(2));
    }

    #endregion

    #region ThrowIfInvalid Tests

    [Test]
    public void ThrowIfInvalidThrowsForInvalidConnectionString()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            Encryption = "aes-gcm"
            // Missing password
        };

        var ex = Assert.Throws<ArgumentException>(() => builder.ThrowIfInvalid());
        Assert.That(ex!.Message, Does.Contain("Password"));
    }

    [Test]
    public void ThrowIfInvalidDoesNotThrowForValidConnectionString()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.db",
            Encryption = "aes-gcm",
            Password = "secret"
        };

        Assert.DoesNotThrow(() => builder.ThrowIfInvalid());
    }

    #endregion

    #region Edge Cases

    [Test]
    public void MemoryModeWithDataSourceIsValid()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = ":memory:",
            Mode = WitDbConnectionMode.Memory
        };

        Assert.That(builder.Validate(), Is.Empty);
    }

    [Test]
    public void NoEncryptionWithPasswordIsValid()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.db",
            // No encryption specified
            Password = "ignored"
        };

        // Password is ignored when no encryption, but this is valid
        Assert.That(builder.Validate(), Is.Empty);
    }

    [Test]
    public void ZeroPoolSizesAreValid()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.db",
            MinPoolSize = 0,
            MaxPoolSize = 0
        };

        // 0 values are valid (disable pooling)
        Assert.That(builder.Validate(), Is.Empty);
    }

    #endregion
}

/// <summary>
/// Tests for connection string building and parsing.
/// </summary>
[TestFixture]
public class WitDbConnectionStringBuildingTests
{
    #region Building Tests

    [Test]
    public void BuildSimpleConnectionString()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "mydb.witdb"
        };

        var connectionString = builder.ConnectionString;
        Assert.That(connectionString, Does.Contain("Data Source=mydb.witdb"));
    }

    [Test]
    public void BuildEncryptedConnectionString()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "secure.witdb",
            Encryption = "aes-gcm",
            Password = "MyPassword123"
        };

        var connectionString = builder.ConnectionString;
        Assert.That(connectionString, Does.Contain("Encryption=aes-gcm"));
        Assert.That(connectionString, Does.Contain("Password=MyPassword123"));
    }

    [Test]
    public void BuildLsmConnectionStringWithProviderParams()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "./lsm_data",
            Store = "lsm"
        };
        builder["MemTableSize"] = 67108864;

        var connectionString = builder.ConnectionString;
        Assert.That(connectionString, Does.Contain("Store=lsm"));
        Assert.That(connectionString, Does.Contain("MemTableSize=67108864"));
    }

    [Test]
    public void BuildPoolingConnectionString()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "pooled.witdb",
            Pooling = true,
            MinPoolSize = 5,
            MaxPoolSize = 50
        };

        var connectionString = builder.ConnectionString;
        Assert.That(connectionString, Does.Contain("Pooling=True"));
        Assert.That(connectionString, Does.Contain("Min Pool Size=5"));
        Assert.That(connectionString, Does.Contain("Max Pool Size=50"));
    }

    #endregion

    #region Parsing Tests

    [Test]
    public void ParseSimpleConnectionString()
    {
        var connectionString = "Data Source=mydb.witdb";
        var builder = new WitDbConnectionStringBuilder(connectionString);

        Assert.That(builder.DataSource, Is.EqualTo("mydb.witdb"));
    }

    [Test]
    public void ParseComplexConnectionString()
    {
        var connectionString = "Data Source=mydb.witdb;Store=lsm;Encryption=aes-gcm;Password=secret;MVCC=true";
        var builder = new WitDbConnectionStringBuilder(connectionString);

        Assert.That(builder.DataSource, Is.EqualTo("mydb.witdb"));
        Assert.That(builder.Store?.ToLowerInvariant(), Is.EqualTo("lsm"));
        Assert.That(builder.Encryption?.ToLowerInvariant(), Is.EqualTo("aes-gcm"));
        Assert.That(builder.Password, Is.EqualTo("secret"));
        Assert.That(builder.Mvcc, Is.True);
    }

    [Test]
    public void ParseConnectionStringWithSpaces()
    {
        var connectionString = "Data Source = mydb.witdb ; Store = btree";
        var builder = new WitDbConnectionStringBuilder(connectionString);

        Assert.That(builder.DataSource, Is.Not.Null);
        Assert.That(builder.Store?.ToLowerInvariant(), Is.EqualTo("btree"));
    }

    [Test]
    public void ParseConnectionStringPreservesQuotedValues()
    {
        var connectionString = "Data Source=\"path with spaces/db.witdb\";Password=\"pass;word\"";
        var builder = new WitDbConnectionStringBuilder(connectionString);

        Assert.That(builder.DataSource, Does.Contain("path with spaces"));
    }

    #endregion

    #region Modification Tests

    [Test]
    public void ModifyExistingProperty()
    {
        var builder = new WitDbConnectionStringBuilder("Data Source=old.witdb");
        builder.DataSource = "new.witdb";

        Assert.That(builder.ConnectionString, Does.Contain("new.witdb"));
        Assert.That(builder.ConnectionString, Does.Not.Contain("old.witdb"));
    }

    [Test]
    public void AddPropertyToExistingConnectionString()
    {
        var builder = new WitDbConnectionStringBuilder("Data Source=test.db");
        builder.Encryption = "aes-gcm";
        builder.Password = "secret";

        var cs = builder.ConnectionString.ToLowerInvariant();
        Assert.That(cs, Does.Contain("data source=test.db"));
        Assert.That(cs, Does.Contain("encryption=aes-gcm"));
        Assert.That(cs, Does.Contain("password=secret"));
    }

    [Test]
    public void RemovePropertyBySettingNull()
    {
        var builder = new WitDbConnectionStringBuilder("Data Source=test.db;Password=secret");
        builder.Password = null;

        Assert.That(builder.ConnectionString, Does.Not.Contain("Password"));
    }

    #endregion

    #region Clear Tests

    [Test]
    public void ClearRemovesAllProperties()
    {
        var builder = new WitDbConnectionStringBuilder("Data Source=test.db;Password=secret;Store=lsm");
        builder.Clear();

        Assert.That(builder.ConnectionString, Is.Empty);
        Assert.That(builder.DataSource, Is.Null);
    }

    #endregion

    #region Custom Provider Key Tests

    [Test]
    public void CustomStoreProviderKeyIsPreserved()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.db",
            Store = "my-custom-store"
        };

        Assert.That(builder.Store, Is.EqualTo("my-custom-store"));
        Assert.That(builder.ConnectionString, Does.Contain("Store=my-custom-store"));
    }

    [Test]
    public void CustomEncryptionProviderKeyIsPreserved()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.db",
            Encryption = "xchacha20-poly1305",
            Password = "secret"
        };

        Assert.That(builder.Encryption, Is.EqualTo("xchacha20-poly1305"));
        Assert.That(builder.ConnectionString, Does.Contain("Encryption=xchacha20-poly1305"));
    }

    [Test]
    public void CustomCacheProviderKeyIsPreserved()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.db",
            Cache = "arc"
        };

        Assert.That(builder.Cache, Is.EqualTo("arc"));
        Assert.That(builder.ConnectionString, Does.Contain("Cache=arc"));
    }

    #endregion

    #region Provider Parameters Tests

    [Test]
    public void ProviderParametersArePassedThrough()
    {
        var builder = new WitDbConnectionStringBuilder { DataSource = "test.db" };
        builder["CustomParam"] = "value";
        builder["PageSize"] = 8192;
        
        var providerParams = builder.GetProviderParameters().ToDictionary(p => p.Key, p => p.Value);

        Assert.That(providerParams.ContainsKey("CustomParam"), Is.True);
        Assert.That(providerParams["CustomParam"]?.ToString(), Is.EqualTo("value"));
        Assert.That(providerParams["PageSize"]?.ToString(), Is.EqualTo("8192"));
    }

    [Test]
    public void CoreParametersAreNotInProviderParameters()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.db",
            Store = "btree",
            Encryption = "aes-gcm",
            Password = "secret"
        };

        var providerParams = builder.GetProviderParameters().ToDictionary(p => p.Key, p => p.Value);

        Assert.That(providerParams.ContainsKey("Data Source"), Is.False);
        Assert.That(providerParams.ContainsKey("Store"), Is.False);
        Assert.That(providerParams.ContainsKey("Encryption"), Is.False);
        Assert.That(providerParams.ContainsKey("Password"), Is.False);
    }

    #endregion
}

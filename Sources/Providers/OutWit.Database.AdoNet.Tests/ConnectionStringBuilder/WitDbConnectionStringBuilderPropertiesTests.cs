using NUnit.Framework;

namespace OutWit.Database.AdoNet.Tests.ConnectionStringBuilder;

/// <summary>
/// Tests for WitDbConnectionStringBuilder properties - defaults, get/set, and type handling.
/// </summary>
[TestFixture]
public class WitDbConnectionStringBuilderPropertiesTests
{
    #region Constructor Tests

    [Test]
    public void DefaultConstructorCreatesEmptyBuilderTest()
    {
        var builder = new WitDbConnectionStringBuilder();

        Assert.That(builder.DataSource, Is.Null);
        Assert.That(builder.ConnectionString, Is.Empty);
    }

    [Test]
    public void ConstructorWithConnectionStringParsesCorrectlyTest()
    {
        var builder = new WitDbConnectionStringBuilder("Data Source=test.db;Password=secret");

        Assert.That(builder.DataSource, Is.EqualTo("test.db"));
        Assert.That(builder.Password, Is.EqualTo("secret"));
    }

    [Test]
    public void ConstructorWithEmptyStringCreatesEmptyBuilderTest()
    {
        var builder = new WitDbConnectionStringBuilder("");

        Assert.That(builder.DataSource, Is.Null);
        Assert.That(builder.ConnectionString, Is.Empty);
    }

    [Test]
    public void ConstructorWithNullStringCreatesEmptyBuilderTest()
    {
        var builder = new WitDbConnectionStringBuilder(null!);

        Assert.That(builder.DataSource, Is.Null);
    }

    #endregion

    #region DataSource Property

    [Test]
    public void DataSourceDefaultIsNullTest()
    {
        var builder = new WitDbConnectionStringBuilder();
        Assert.That(builder.DataSource, Is.Null);
    }

    [Test]
    public void DataSourceSetGetWorksCorrectlyTest()
    {
        var builder = new WitDbConnectionStringBuilder { DataSource = "mydb.witdb" };
        Assert.That(builder.DataSource, Is.EqualTo("mydb.witdb"));
    }

    [Test]
    public void DataSourceMemoryValueIsPreservedTest()
    {
        var builder = new WitDbConnectionStringBuilder { DataSource = ":memory:" };
        Assert.That(builder.DataSource, Is.EqualTo(":memory:"));
    }

    [Test]
    public void DataSourceWithWindowsPathIsPreservedTest()
    {
        var builder = new WitDbConnectionStringBuilder { DataSource = @"C:\Data\mydb.witdb" };
        Assert.That(builder.DataSource, Is.EqualTo(@"C:\Data\mydb.witdb"));
    }

    [Test]
    public void DataSourceWithUnixPathIsPreservedTest()
    {
        var builder = new WitDbConnectionStringBuilder { DataSource = "/var/data/mydb.witdb" };
        Assert.That(builder.DataSource, Is.EqualTo("/var/data/mydb.witdb"));
    }

    [Test]
    public void DataSourceWithSpacesIsPreservedTest()
    {
        var builder = new WitDbConnectionStringBuilder { DataSource = "path with spaces/db.witdb" };
        Assert.That(builder.DataSource, Does.Contain("path with spaces"));
    }

    [Test]
    public void DataSourceSetNullClearsValueTest()
    {
        var builder = new WitDbConnectionStringBuilder { DataSource = "test.db" };
        builder.DataSource = null;
        Assert.That(builder.DataSource, Is.Null);
    }

    #endregion

    #region Mode Property

    [Test]
    public void ModeDefaultIsReadWriteCreateTest()
    {
        var builder = new WitDbConnectionStringBuilder();
        Assert.That(builder.Mode, Is.EqualTo(WitDbConnectionMode.ReadWriteCreate));
    }

    [TestCase(WitDbConnectionMode.ReadWriteCreate)]
    [TestCase(WitDbConnectionMode.ReadWrite)]
    [TestCase(WitDbConnectionMode.ReadOnly)]
    [TestCase(WitDbConnectionMode.Memory)]
    public void ModeAllValuesWorkCorrectlyTest(WitDbConnectionMode mode)
    {
        var builder = new WitDbConnectionStringBuilder { Mode = mode };
        Assert.That(builder.Mode, Is.EqualTo(mode));
    }

    [TestCase("ReadWriteCreate", WitDbConnectionMode.ReadWriteCreate)]
    [TestCase("READWRITECREATE", WitDbConnectionMode.ReadWriteCreate)]
    [TestCase("readwritecreate", WitDbConnectionMode.ReadWriteCreate)]
    [TestCase("ReadWrite", WitDbConnectionMode.ReadWrite)]
    [TestCase("ReadOnly", WitDbConnectionMode.ReadOnly)]
    [TestCase("readonly", WitDbConnectionMode.ReadOnly)]
    [TestCase("Memory", WitDbConnectionMode.Memory)]
    [TestCase("MEMORY", WitDbConnectionMode.Memory)]
    public void ModeParsesCaseInsensitiveTest(string value, WitDbConnectionMode expected)
    {
        var builder = new WitDbConnectionStringBuilder($"Mode={value}");
        Assert.That(builder.Mode, Is.EqualTo(expected));
    }

    [Test]
    public void ModeInvalidValueReturnsDefaultTest()
    {
        var builder = new WitDbConnectionStringBuilder("Mode=InvalidMode");
        Assert.That(builder.Mode, Is.EqualTo(WitDbConnectionMode.ReadWriteCreate));
    }

    #endregion

    #region Store Property (Provider Key)

    [Test]
    public void StoreDefaultIsNullTest()
    {
        var builder = new WitDbConnectionStringBuilder();
        Assert.That(builder.Store, Is.Null);
    }

    [TestCase("btree")]
    [TestCase("lsm")]
    [TestCase("inmemory")]
    public void StoreKnownValuesWorkCorrectlyTest(string store)
    {
        var builder = new WitDbConnectionStringBuilder { Store = store };
        Assert.That(builder.Store, Is.EqualTo(store));
    }

    [Test]
    public void StoreCustomProviderKeyIsPreservedTest()
    {
        var builder = new WitDbConnectionStringBuilder { Store = "my-custom-store" };
        Assert.That(builder.Store, Is.EqualTo("my-custom-store"));
    }

    [Test]
    public void StoreCaseIsPreservedTest()
    {
        var builder = new WitDbConnectionStringBuilder { Store = "MyCustomStore" };
        Assert.That(builder.Store, Is.EqualTo("MyCustomStore"));
    }

    [Test]
    public void StoreFromConnectionStringPreservesCaseTest()
    {
        var builder = new WitDbConnectionStringBuilder("Store=LSM");
        Assert.That(builder.Store, Is.EqualTo("LSM"));
    }

    #endregion

    #region Encryption Property (Provider Key)

    [Test]
    public void EncryptionDefaultIsNullTest()
    {
        var builder = new WitDbConnectionStringBuilder();
        Assert.That(builder.Encryption, Is.Null);
    }

    [TestCase("aes-gcm")]
    [TestCase("chacha20-poly1305")]
    public void EncryptionKnownValuesWorkCorrectlyTest(string encryption)
    {
        var builder = new WitDbConnectionStringBuilder { Encryption = encryption };
        Assert.That(builder.Encryption, Is.EqualTo(encryption));
    }

    [Test]
    public void EncryptionCustomProviderKeyIsPreservedTest()
    {
        var builder = new WitDbConnectionStringBuilder { Encryption = "my-custom-crypto" };
        Assert.That(builder.Encryption, Is.EqualTo("my-custom-crypto"));
    }

    #endregion

    #region Password Property

    [Test]
    public void PasswordDefaultIsNullTest()
    {
        var builder = new WitDbConnectionStringBuilder();
        Assert.That(builder.Password, Is.Null);
    }

    [Test]
    public void PasswordSetGetWorksCorrectlyTest()
    {
        var builder = new WitDbConnectionStringBuilder { Password = "secret123" };
        Assert.That(builder.Password, Is.EqualTo("secret123"));
    }

    [Test]
    public void PasswordWithSpecialCharactersIsPreservedTest()
    {
        var builder = new WitDbConnectionStringBuilder { Password = "p@ss=w;rd\"'" };
        Assert.That(builder.Password, Is.EqualTo("p@ss=w;rd\"'"));
    }

    [Test]
    public void PasswordEmptyStringIsDistinctFromNullTest()
    {
        var builder = new WitDbConnectionStringBuilder { Password = "" };
        Assert.That(builder.Password, Is.EqualTo(""));
    }

    [Test]
    public void PasswordSetNullClearsValueTest()
    {
        var builder = new WitDbConnectionStringBuilder { Password = "secret" };
        builder.Password = null;
        Assert.That(builder.Password, Is.Null);
    }

    #endregion

    #region User Property

    [Test]
    public void UserDefaultIsNullTest()
    {
        var builder = new WitDbConnectionStringBuilder();
        Assert.That(builder.User, Is.Null);
    }

    [Test]
    public void UserSetGetWorksCorrectlyTest()
    {
        var builder = new WitDbConnectionStringBuilder { User = "admin" };
        Assert.That(builder.User, Is.EqualTo("admin"));
    }

    [Test]
    public void UserWithSpecialCharactersIsPreservedTest()
    {
        var builder = new WitDbConnectionStringBuilder { User = "user@domain.com" };
        Assert.That(builder.User, Is.EqualTo("user@domain.com"));
    }

    #endregion

    #region IsolationLevel Property

    [Test]
    public void IsolationLevelDefaultIsReadCommittedTest()
    {
        var builder = new WitDbConnectionStringBuilder();
        Assert.That(builder.IsolationLevel, Is.EqualTo(WitDbIsolationLevel.ReadCommitted));
    }

    [TestCase(WitDbIsolationLevel.ReadUncommitted)]
    [TestCase(WitDbIsolationLevel.ReadCommitted)]
    [TestCase(WitDbIsolationLevel.RepeatableRead)]
    [TestCase(WitDbIsolationLevel.Serializable)]
    [TestCase(WitDbIsolationLevel.Snapshot)]
    public void IsolationLevelAllValuesWorkCorrectlyTest(WitDbIsolationLevel level)
    {
        var builder = new WitDbConnectionStringBuilder { IsolationLevel = level };
        Assert.That(builder.IsolationLevel, Is.EqualTo(level));
    }

    [TestCase("ReadUncommitted", WitDbIsolationLevel.ReadUncommitted)]
    [TestCase("READUNCOMMITTED", WitDbIsolationLevel.ReadUncommitted)]
    [TestCase("ReadCommitted", WitDbIsolationLevel.ReadCommitted)]
    [TestCase("RepeatableRead", WitDbIsolationLevel.RepeatableRead)]
    [TestCase("Serializable", WitDbIsolationLevel.Serializable)]
    [TestCase("Snapshot", WitDbIsolationLevel.Snapshot)]
    [TestCase("snapshot", WitDbIsolationLevel.Snapshot)]
    public void IsolationLevelParsesCaseInsensitiveTest(string value, WitDbIsolationLevel expected)
    {
        var builder = new WitDbConnectionStringBuilder($"Isolation Level={value}");
        Assert.That(builder.IsolationLevel, Is.EqualTo(expected));
    }

    [Test]
    public void IsolationLevelInvalidValueReturnsDefaultTest()
    {
        var builder = new WitDbConnectionStringBuilder("Isolation Level=InvalidLevel");
        Assert.That(builder.IsolationLevel, Is.EqualTo(WitDbIsolationLevel.ReadCommitted));
    }

    #endregion

    #region Mvcc Property

    [Test]
    public void MvccDefaultIsTrueTest()
    {
        var builder = new WitDbConnectionStringBuilder();
        Assert.That(builder.Mvcc, Is.True);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void MvccSetGetWorksCorrectlyTest(bool value)
    {
        var builder = new WitDbConnectionStringBuilder { Mvcc = value };
        Assert.That(builder.Mvcc, Is.EqualTo(value));
    }

    [TestCase("true", true)]
    [TestCase("TRUE", true)]
    [TestCase("True", true)]
    [TestCase("false", false)]
    [TestCase("FALSE", false)]
    [TestCase("False", false)]
    [TestCase("1", true)]
    [TestCase("0", false)]
    [TestCase("yes", true)]
    [TestCase("no", false)]
    public void MvccParsesBooleanFormatsTest(string value, bool expected)
    {
        var builder = new WitDbConnectionStringBuilder($"MVCC={value}");
        Assert.That(builder.Mvcc, Is.EqualTo(expected));
    }

    #endregion

    #region Transactions Property

    [Test]
    public void TransactionsDefaultIsTrueTest()
    {
        var builder = new WitDbConnectionStringBuilder();
        Assert.That(builder.Transactions, Is.True);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void TransactionsSetGetWorksCorrectlyTest(bool value)
    {
        var builder = new WitDbConnectionStringBuilder { Transactions = value };
        Assert.That(builder.Transactions, Is.EqualTo(value));
    }

    #endregion

    #region Cache Property (Provider Key)

    [Test]
    public void CacheDefaultIsNullTest()
    {
        var builder = new WitDbConnectionStringBuilder();
        Assert.That(builder.Cache, Is.Null);
    }

    [TestCase("clock")]
    [TestCase("lru")]
    public void CacheKnownValuesWorkCorrectlyTest(string cache)
    {
        var builder = new WitDbConnectionStringBuilder { Cache = cache };
        Assert.That(builder.Cache, Is.EqualTo(cache));
    }

    [Test]
    public void CacheCustomProviderKeyIsPreservedTest()
    {
        var builder = new WitDbConnectionStringBuilder { Cache = "arc" };
        Assert.That(builder.Cache, Is.EqualTo("arc"));
    }

    #endregion

    #region Journal Property (Provider Key)

    [Test]
    public void JournalDefaultIsNullTest()
    {
        var builder = new WitDbConnectionStringBuilder();
        Assert.That(builder.Journal, Is.Null);
    }

    [TestCase("wal")]
    [TestCase("rollback")]
    public void JournalKnownValuesWorkCorrectlyTest(string journal)
    {
        var builder = new WitDbConnectionStringBuilder { Journal = journal };
        Assert.That(builder.Journal, Is.EqualTo(journal));
    }

    #endregion

    #region Pooling Properties

    [Test]
    public void PoolingDefaultIsFalseTest()
    {
        var builder = new WitDbConnectionStringBuilder();
        Assert.That(builder.Pooling, Is.False);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void PoolingSetGetWorksCorrectlyTest(bool value)
    {
        var builder = new WitDbConnectionStringBuilder { Pooling = value };
        Assert.That(builder.Pooling, Is.EqualTo(value));
    }

    [Test]
    public void MinPoolSizeDefaultIs1Test()
    {
        var builder = new WitDbConnectionStringBuilder();
        Assert.That(builder.MinPoolSize, Is.EqualTo(1));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(5)]
    [TestCase(100)]
    public void MinPoolSizeSetGetWorksCorrectlyTest(int value)
    {
        var builder = new WitDbConnectionStringBuilder { MinPoolSize = value };
        Assert.That(builder.MinPoolSize, Is.EqualTo(value));
    }

    [Test]
    public void MaxPoolSizeDefaultIs100Test()
    {
        var builder = new WitDbConnectionStringBuilder();
        Assert.That(builder.MaxPoolSize, Is.EqualTo(100));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(50)]
    [TestCase(1000)]
    public void MaxPoolSizeSetGetWorksCorrectlyTest(int value)
    {
        var builder = new WitDbConnectionStringBuilder { MaxPoolSize = value };
        Assert.That(builder.MaxPoolSize, Is.EqualTo(value));
    }

    #endregion

    #region DefaultTimeout Property

    [Test]
    public void DefaultTimeoutDefaultIs30Test()
    {
        var builder = new WitDbConnectionStringBuilder();
        Assert.That(builder.DefaultTimeout, Is.EqualTo(30));
    }

    [TestCase(0)]
    [TestCase(30)]
    [TestCase(120)]
    [TestCase(3600)]
    public void DefaultTimeoutSetGetWorksCorrectlyTest(int value)
    {
        var builder = new WitDbConnectionStringBuilder { DefaultTimeout = value };
        Assert.That(builder.DefaultTimeout, Is.EqualTo(value));
    }

    #endregion

    #region ReadOnly Property

    [Test]
    public void ReadOnlyDefaultIsFalseTest()
    {
        var builder = new WitDbConnectionStringBuilder();
        Assert.That(builder.ReadOnly, Is.False);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ReadOnlySetGetWorksCorrectlyTest(bool value)
    {
        var builder = new WitDbConnectionStringBuilder { ReadOnly = value };
        Assert.That(builder.ReadOnly, Is.EqualTo(value));
    }

    #endregion
}

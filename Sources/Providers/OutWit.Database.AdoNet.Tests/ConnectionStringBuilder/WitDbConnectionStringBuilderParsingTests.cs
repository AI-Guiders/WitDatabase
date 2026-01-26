using NUnit.Framework;

namespace OutWit.Database.AdoNet.Tests.ConnectionStringBuilder;

/// <summary>
/// Tests for connection string parsing, building, and round-trip behavior.
/// </summary>
[TestFixture]
public class WitDbConnectionStringBuilderParsingTests
{
    #region Simple Parsing

    [Test]
    public void ParseSimpleDataSourceTest()
    {
        var builder = new WitDbConnectionStringBuilder("Data Source=mydb.witdb");
        Assert.That(builder.DataSource, Is.EqualTo("mydb.witdb"));
    }

    [Test]
    public void ParseMultiplePropertiesTest()
    {
        var builder = new WitDbConnectionStringBuilder("Data Source=test.db;Password=secret;Store=lsm");

        Assert.That(builder.DataSource, Is.EqualTo("test.db"));
        Assert.That(builder.Password, Is.EqualTo("secret"));
        Assert.That(builder.Store, Is.EqualTo("lsm"));
    }

    [Test]
    public void ParseAllCorePropertiesTest()
    {
        var connectionString = 
            "Data Source=mydb.witdb;" +
            "Mode=ReadWrite;" +
            "Store=lsm;" +
            "Encryption=aes-gcm;" +
            "Password=secret123;" +
            "User=admin;" +
            "Cache=lru;" +
            "Journal=wal;" +
            "Isolation Level=Snapshot;" +
            "MVCC=true;" +
            "Transactions=true;" +
            "Pooling=true;" +
            "Min Pool Size=5;" +
            "Max Pool Size=50;" +
            "Default Timeout=120;" +
            "Read Only=true";

        var builder = new WitDbConnectionStringBuilder(connectionString);

        Assert.That(builder.DataSource, Is.EqualTo("mydb.witdb"));
        Assert.That(builder.Mode, Is.EqualTo(WitDbConnectionMode.ReadWrite));
        Assert.That(builder.Store, Is.EqualTo("lsm"));
        Assert.That(builder.Encryption, Is.EqualTo("aes-gcm"));
        Assert.That(builder.Password, Is.EqualTo("secret123"));
        Assert.That(builder.User, Is.EqualTo("admin"));
        Assert.That(builder.Cache, Is.EqualTo("lru"));
        Assert.That(builder.Journal, Is.EqualTo("wal"));
        Assert.That(builder.IsolationLevel, Is.EqualTo(WitDbIsolationLevel.Snapshot));
        Assert.That(builder.Mvcc, Is.True);
        Assert.That(builder.Transactions, Is.True);
        Assert.That(builder.Pooling, Is.True);
        Assert.That(builder.MinPoolSize, Is.EqualTo(5));
        Assert.That(builder.MaxPoolSize, Is.EqualTo(50));
        Assert.That(builder.DefaultTimeout, Is.EqualTo(120));
        Assert.That(builder.ReadOnly, Is.True);
    }

    #endregion

    #region Whitespace Handling

    [Test]
    public void ParseWithSpacesAroundEqualsTest()
    {
        var builder = new WitDbConnectionStringBuilder("Data Source = mydb.witdb");
        Assert.That(builder.DataSource, Is.Not.Null);
    }

    [Test]
    public void ParseWithSpacesAroundSemicolonsTest()
    {
        var builder = new WitDbConnectionStringBuilder("Data Source=test.db ; Store=btree");
        Assert.That(builder.DataSource, Is.EqualTo("test.db"));
        Assert.That(builder.Store, Is.EqualTo("btree"));
    }

    [Test]
    public void ParseWithLeadingAndTrailingSpacesTest()
    {
        var builder = new WitDbConnectionStringBuilder("  Data Source=test.db;Store=lsm  ");
        Assert.That(builder.DataSource, Is.EqualTo("test.db"));
    }

    #endregion

    #region Quoted Values

    [Test]
    public void ParseQuotedValueWithSpacesTest()
    {
        var builder = new WitDbConnectionStringBuilder("Data Source=\"path with spaces/db.witdb\"");
        Assert.That(builder.DataSource, Does.Contain("path with spaces"));
    }

    [Test]
    public void ParseQuotedValueWithSemicolonTest()
    {
        var builder = new WitDbConnectionStringBuilder("Password=\"pass;word\"");
        Assert.That(builder.Password, Is.EqualTo("pass;word"));
    }

    [Test]
    public void ParseQuotedValueWithEqualsTest()
    {
        var builder = new WitDbConnectionStringBuilder("Password=\"pass=word\"");
        Assert.That(builder.Password, Is.EqualTo("pass=word"));
    }

    [Test]
    public void ParseSingleQuotedValueTest()
    {
        var builder = new WitDbConnectionStringBuilder("Password='secret123'");
        Assert.That(builder.Password, Is.EqualTo("secret123"));
    }

    #endregion

    #region Case Sensitivity

    [Test]
    public void ParseKeysAreCaseInsensitiveTest()
    {
        var builder1 = new WitDbConnectionStringBuilder("DATA SOURCE=test.db");
        var builder2 = new WitDbConnectionStringBuilder("data source=test.db");
        var builder3 = new WitDbConnectionStringBuilder("Data Source=test.db");

        Assert.That(builder1.DataSource, Is.EqualTo("test.db"));
        Assert.That(builder2.DataSource, Is.EqualTo("test.db"));
        Assert.That(builder3.DataSource, Is.EqualTo("test.db"));
    }

    [Test]
    public void ParseValuesPreserveCaseTest()
    {
        var builder = new WitDbConnectionStringBuilder("Store=MyCustomStore");
        Assert.That(builder.Store, Is.EqualTo("MyCustomStore"));
    }

    #endregion

    #region Building Connection Strings

    [Test]
    public void BuildSimpleDataSourceTest()
    {
        var builder = new WitDbConnectionStringBuilder { DataSource = "mydb.witdb" };

        Assert.That(builder.ConnectionString, Does.Contain("Data Source=mydb.witdb"));
    }

    [Test]
    public void BuildMultiplePropertiesTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.db",
            Store = "lsm",
            Password = "secret"
        };

        var cs = builder.ConnectionString;
        Assert.That(cs, Does.Contain("Data Source=test.db"));
        Assert.That(cs, Does.Contain("Store=lsm"));
        Assert.That(cs, Does.Contain("Password=secret"));
    }

    [Test]
    public void BuildBooleanPropertiesUseCorrectFormatTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.db",
            Pooling = true,
            Mvcc = false
        };

        var cs = builder.ConnectionString;
        Assert.That(cs, Does.Contain("Pooling=True"));
        Assert.That(cs, Does.Contain("MVCC=False"));
    }

    [Test]
    public void BuildEnumPropertiesUseCorrectFormatTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.db",
            Mode = WitDbConnectionMode.ReadOnly,
            IsolationLevel = WitDbIsolationLevel.Snapshot
        };

        var cs = builder.ConnectionString;
        Assert.That(cs, Does.Contain("Mode=ReadOnly"));
        Assert.That(cs, Does.Contain("Isolation Level=Snapshot"));
    }

    #endregion

    #region Round-Trip Tests

    [Test]
    public void RoundTripPreservesDataSourceTest()
    {
        var original = new WitDbConnectionStringBuilder { DataSource = "test.db" };
        var parsed = new WitDbConnectionStringBuilder(original.ConnectionString);

        Assert.That(parsed.DataSource, Is.EqualTo(original.DataSource));
    }

    [Test]
    public void RoundTripPreservesAllCorePropertiesTest()
    {
        var original = new WitDbConnectionStringBuilder
        {
            DataSource = "mydb.witdb",
            Mode = WitDbConnectionMode.ReadOnly,
            Store = "lsm",
            Encryption = "aes-gcm",
            Password = "secret",
            User = "admin",
            Cache = "lru",
            Journal = "wal",
            IsolationLevel = WitDbIsolationLevel.Snapshot,
            Mvcc = false,
            Transactions = false,
            Pooling = true,
            MinPoolSize = 5,
            MaxPoolSize = 50,
            DefaultTimeout = 120,
            ReadOnly = true
        };

        var parsed = new WitDbConnectionStringBuilder(original.ConnectionString);

        Assert.That(parsed.DataSource, Is.EqualTo(original.DataSource));
        Assert.That(parsed.Mode, Is.EqualTo(original.Mode));
        Assert.That(parsed.Store, Is.EqualTo(original.Store));
        Assert.That(parsed.Encryption, Is.EqualTo(original.Encryption));
        Assert.That(parsed.Password, Is.EqualTo(original.Password));
        Assert.That(parsed.User, Is.EqualTo(original.User));
        Assert.That(parsed.Cache, Is.EqualTo(original.Cache));
        Assert.That(parsed.Journal, Is.EqualTo(original.Journal));
        Assert.That(parsed.IsolationLevel, Is.EqualTo(original.IsolationLevel));
        Assert.That(parsed.Mvcc, Is.EqualTo(original.Mvcc));
        Assert.That(parsed.Transactions, Is.EqualTo(original.Transactions));
        Assert.That(parsed.Pooling, Is.EqualTo(original.Pooling));
        Assert.That(parsed.MinPoolSize, Is.EqualTo(original.MinPoolSize));
        Assert.That(parsed.MaxPoolSize, Is.EqualTo(original.MaxPoolSize));
        Assert.That(parsed.DefaultTimeout, Is.EqualTo(original.DefaultTimeout));
        Assert.That(parsed.ReadOnly, Is.EqualTo(original.ReadOnly));
    }

    [Test]
    public void RoundTripPreservesCustomParametersTest()
    {
        var original = new WitDbConnectionStringBuilder { DataSource = "test.db" };
        original["CustomParam"] = "value123";
        original["AnotherParam"] = 42;

        var parsed = new WitDbConnectionStringBuilder(original.ConnectionString);

        Assert.That(parsed["CustomParam"]?.ToString(), Is.EqualTo("value123"));
        Assert.That(parsed["AnotherParam"]?.ToString(), Is.EqualTo("42"));
    }

    [Test]
    public void RoundTripPreservesSpecialCharactersInPasswordTest()
    {
        var original = new WitDbConnectionStringBuilder
        {
            DataSource = "test.db",
            Password = "p@ss=w;rd"
        };

        var parsed = new WitDbConnectionStringBuilder(original.ConnectionString);

        Assert.That(parsed.Password, Is.EqualTo(original.Password));
    }

    #endregion

    #region Modification Tests

    [Test]
    public void ModifyExistingPropertyTest()
    {
        var builder = new WitDbConnectionStringBuilder("Data Source=old.witdb");
        builder.DataSource = "new.witdb";

        Assert.That(builder.ConnectionString, Does.Contain("new.witdb"));
        Assert.That(builder.ConnectionString, Does.Not.Contain("old.witdb"));
    }

    [Test]
    public void ModifyAddPropertyToExistingTest()
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
    public void ModifyRemovePropertyBySettingNullTest()
    {
        var builder = new WitDbConnectionStringBuilder("Data Source=test.db;Password=secret");
        builder.Password = null;

        Assert.That(builder.ConnectionString, Does.Not.Contain("Password"));
    }

    [Test]
    public void ClearRemovesAllPropertiesTest()
    {
        var builder = new WitDbConnectionStringBuilder("Data Source=test.db;Password=secret;Store=lsm");
        builder.Clear();

        Assert.That(builder.ConnectionString, Is.Empty);
        Assert.That(builder.DataSource, Is.Null);
        Assert.That(builder.Password, Is.Null);
        Assert.That(builder.Store, Is.Null);
    }

    #endregion

    #region Edge Cases

    [Test]
    public void ParseEmptyValueTest()
    {
        var builder = new WitDbConnectionStringBuilder("Data Source=;Store=lsm");
        Assert.That(builder.DataSource, Is.Null.Or.Empty);
        Assert.That(builder.Store, Is.EqualTo("lsm"));
    }

    [Test]
    public void ParseDuplicateKeysLastWinsTest()
    {
        var builder = new WitDbConnectionStringBuilder("Data Source=first.db;Data Source=second.db");
        Assert.That(builder.DataSource, Is.EqualTo("second.db"));
    }

    [Test]
    public void ParseUnknownKeysArePreservedTest()
    {
        var builder = new WitDbConnectionStringBuilder("Data Source=test.db;UnknownKey=value");
        Assert.That(builder["UnknownKey"]?.ToString(), Is.EqualTo("value"));
    }

    [Test]
    public void ParseTrailingSemicolonTest()
    {
        var builder = new WitDbConnectionStringBuilder("Data Source=test.db;");
        Assert.That(builder.DataSource, Is.EqualTo("test.db"));
    }

    [Test]
    public void ParseMultipleSemicolonsTest()
    {
        var builder = new WitDbConnectionStringBuilder("Data Source=test.db;;Store=lsm");
        Assert.That(builder.DataSource, Is.EqualTo("test.db"));
        Assert.That(builder.Store, Is.EqualTo("lsm"));
    }

    #endregion
}

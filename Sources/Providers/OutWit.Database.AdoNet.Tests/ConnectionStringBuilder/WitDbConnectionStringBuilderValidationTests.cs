using NUnit.Framework;

namespace OutWit.Database.AdoNet.Tests.ConnectionStringBuilder;

/// <summary>
/// Tests for WitDbConnectionStringBuilder validation logic.
/// </summary>
[TestFixture]
public class WitDbConnectionStringBuilderValidationTests
{
    #region Valid Configurations

    [Test]
    public void ValidateMinimalFileDatabaseIsValidTest()
    {
        var builder = new WitDbConnectionStringBuilder { DataSource = "test.db" };
        
        var errors = builder.Validate();
        
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public void ValidateMemoryModeWithoutDataSourceIsValidTest()
    {
        var builder = new WitDbConnectionStringBuilder { Mode = WitDbConnectionMode.Memory };
        
        var errors = builder.Validate();
        
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public void ValidateMemoryModeWithDataSourceIsValidTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = ":memory:",
            Mode = WitDbConnectionMode.Memory
        };

        var errors = builder.Validate();
        
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public void ValidateEncryptionWithPasswordIsValidTest()
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
    public void ValidatePoolingWithValidSizesIsValidTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.db",
            Pooling = true,
            MinPoolSize = 5,
            MaxPoolSize = 50
        };

        var errors = builder.Validate();
        
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public void ValidatePoolingWithEqualSizesIsValidTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.db",
            Pooling = true,
            MinPoolSize = 10,
            MaxPoolSize = 10
        };

        var errors = builder.Validate();
        
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public void ValidateZeroPoolSizesIsValidTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.db",
            MinPoolSize = 0,
            MaxPoolSize = 0
        };

        var errors = builder.Validate();
        
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public void ValidateFullConfigurationIsValidTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "mydb.witdb",
            Mode = WitDbConnectionMode.ReadWriteCreate,
            Store = "btree",
            Encryption = "aes-gcm",
            Password = "SecurePassword123",
            User = "admin",
            Cache = "lru",
            Journal = "wal",
            IsolationLevel = WitDbIsolationLevel.Snapshot,
            Mvcc = true,
            Transactions = true,
            Pooling = true,
            MinPoolSize = 1,
            MaxPoolSize = 100,
            DefaultTimeout = 30,
            ReadOnly = false
        };

        var errors = builder.Validate();
        
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public void ValidatePasswordWithoutEncryptionIsValidTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.db",
            Password = "unused"
        };

        var errors = builder.Validate();
        
        Assert.That(errors, Is.Empty);
    }

    #endregion

    #region Invalid Configurations

    [Test]
    public void ValidateMissingDataSourceWithFileModeReturnsErrorTest()
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
    public void ValidateMissingDataSourceWithReadWriteModeReturnsErrorTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            Mode = WitDbConnectionMode.ReadWrite
        };

        var errors = builder.Validate();

        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0], Does.Contain("Data Source"));
    }

    [Test]
    public void ValidateMissingDataSourceWithReadOnlyModeReturnsErrorTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            Mode = WitDbConnectionMode.ReadOnly
        };

        var errors = builder.Validate();

        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0], Does.Contain("Data Source"));
    }

    [Test]
    public void ValidateEncryptionWithoutPasswordReturnsErrorTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.db",
            Encryption = "aes-gcm"
        };

        var errors = builder.Validate();

        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0], Does.Contain("Password").IgnoreCase);
    }

    [Test]
    public void ValidateEncryptionWithEmptyPasswordReturnsErrorTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.db",
            Encryption = "aes-gcm",
            Password = ""
        };

        var errors = builder.Validate();

        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0], Does.Contain("Password").IgnoreCase);
    }

    [Test]
    public void ValidateMinPoolSizeGreaterThanMaxReturnsErrorTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.db",
            MinPoolSize = 100,
            MaxPoolSize = 10
        };

        var errors = builder.Validate();

        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0], Does.Contain("Min Pool Size"));
        Assert.That(errors[0], Does.Contain("Max Pool Size"));
    }

    [Test]
    public void ValidateMultipleErrorsAllReportedTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            // Missing DataSource (not Memory mode)
            Encryption = "aes-gcm",
            // Missing Password
            MinPoolSize = 100,
            MaxPoolSize = 10 // Invalid
        };

        var errors = builder.Validate();

        Assert.That(errors.Count, Is.GreaterThanOrEqualTo(3));
    }

    #endregion

    #region ThrowIfInvalid Tests

    [Test]
    public void ThrowIfInvalidValidConfigurationDoesNotThrowTest()
    {
        var builder = new WitDbConnectionStringBuilder { DataSource = "test.db" };

        Assert.DoesNotThrow(() => builder.ThrowIfInvalid());
    }

    [Test]
    public void ThrowIfInvalidInvalidConfigurationThrowsArgumentExceptionTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            Encryption = "aes-gcm"
            // Missing password
        };

        var ex = Assert.Throws<ArgumentException>(() => builder.ThrowIfInvalid());
        Assert.That(ex!.Message, Does.Contain("Password").IgnoreCase);
    }

    [Test]
    public void ThrowIfInvalidMultipleErrorsAllIncludedInMessageTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            Encryption = "aes-gcm",
            MinPoolSize = 100,
            MaxPoolSize = 10
        };

        var ex = Assert.Throws<ArgumentException>(() => builder.ThrowIfInvalid());
        
        Assert.That(ex!.Message, Does.Contain("Password").IgnoreCase);
        Assert.That(ex.Message, Does.Contain("Pool Size").IgnoreCase);
    }

    #endregion

    #region Edge Cases

    [Test]
    public void ValidateCustomEncryptionProviderRequiresPasswordTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.db",
            Encryption = "my-custom-encryption"
        };

        var errors = builder.Validate();

        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0], Does.Contain("Password").IgnoreCase);
    }

    [Test]
    public void ValidateNegativePoolSizesAreAcceptedTest()
    {
        // DbConnectionStringBuilder doesn't validate negative values
        // This tests current behavior, not necessarily desired behavior
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.db",
            MinPoolSize = -1,
            MaxPoolSize = -1
        };

        var errors = builder.Validate();
        
        // Current implementation allows negative values
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public void ValidateNegativeTimeoutIsAcceptedTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.db",
            DefaultTimeout = -1
        };

        var errors = builder.Validate();
        
        Assert.That(errors, Is.Empty);
    }

    #endregion
}

/// <summary>
/// Tests for WitDb enum types.
/// </summary>
[TestFixture]
public class WitDbEnumsTests
{
    #region WitDbConnectionMode

    [Test]
    public void WitDbConnectionModeHasExpectedValuesTest()
    {
        var values = Enum.GetValues<WitDbConnectionMode>();
        
        Assert.That(values, Has.Length.EqualTo(4));
        Assert.That(Enum.IsDefined(WitDbConnectionMode.ReadWriteCreate), Is.True);
        Assert.That(Enum.IsDefined(WitDbConnectionMode.ReadWrite), Is.True);
        Assert.That(Enum.IsDefined(WitDbConnectionMode.ReadOnly), Is.True);
        Assert.That(Enum.IsDefined(WitDbConnectionMode.Memory), Is.True);
    }

    [Test]
    public void WitDbConnectionModeDefaultIsReadWriteCreateTest()
    {
        Assert.That(default(WitDbConnectionMode), Is.EqualTo(WitDbConnectionMode.ReadWriteCreate));
    }

    #endregion

    #region WitDbIsolationLevel

    [Test]
    public void WitDbIsolationLevelHasExpectedValuesTest()
    {
        var values = Enum.GetValues<WitDbIsolationLevel>();
        
        Assert.That(values, Has.Length.EqualTo(5));
        Assert.That(Enum.IsDefined(WitDbIsolationLevel.ReadUncommitted), Is.True);
        Assert.That(Enum.IsDefined(WitDbIsolationLevel.ReadCommitted), Is.True);
        Assert.That(Enum.IsDefined(WitDbIsolationLevel.RepeatableRead), Is.True);
        Assert.That(Enum.IsDefined(WitDbIsolationLevel.Serializable), Is.True);
        Assert.That(Enum.IsDefined(WitDbIsolationLevel.Snapshot), Is.True);
    }

    [Test]
    public void WitDbIsolationLevelOrderingMatchesCoreIsolationLevelTest()
    {
        Assert.That((int)WitDbIsolationLevel.ReadUncommitted, Is.EqualTo(0));
        Assert.That((int)WitDbIsolationLevel.ReadCommitted, Is.EqualTo(1));
        Assert.That((int)WitDbIsolationLevel.RepeatableRead, Is.EqualTo(2));
        Assert.That((int)WitDbIsolationLevel.Serializable, Is.EqualTo(3));
        Assert.That((int)WitDbIsolationLevel.Snapshot, Is.EqualTo(4));
    }

    [Test]
    public void WitDbIsolationLevelDefaultIsReadUncommittedTest()
    {
        Assert.That(default(WitDbIsolationLevel), Is.EqualTo(WitDbIsolationLevel.ReadUncommitted));
    }

    #endregion
}

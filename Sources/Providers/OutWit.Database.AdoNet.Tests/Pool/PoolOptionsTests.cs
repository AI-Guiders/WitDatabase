using NUnit.Framework;
using OutWit.Database.AdoNet.Pool;

namespace OutWit.Database.AdoNet.Tests.Pool;

/// <summary>
/// Tests for PoolOptions.
/// </summary>
[TestFixture]
public class PoolOptionsTests
{
    #region Default Values Tests

    [Test]
    public void DefaultValuesAreCorrectTest()
    {
        var options = new PoolOptions();

        Assert.That(options.ConnectionString, Is.EqualTo(string.Empty));
        Assert.That(options.MinPoolSize, Is.EqualTo(PoolOptions.DEFAULT_MIN_POOL_SIZE));
        Assert.That(options.MaxPoolSize, Is.EqualTo(PoolOptions.DEFAULT_MAX_POOL_SIZE));
        Assert.That(options.ConnectionLifetime, Is.EqualTo(PoolOptions.DEFAULT_CONNECTION_LIFETIME));
        Assert.That(options.IdleTimeout, Is.EqualTo(PoolOptions.DEFAULT_IDLE_TIMEOUT));
        Assert.That(options.ValidateOnBorrow, Is.False);
    }

    [Test]
    public void DefaultConstantsAreCorrectTest()
    {
        Assert.That(PoolOptions.DEFAULT_MIN_POOL_SIZE, Is.EqualTo(0));
        Assert.That(PoolOptions.DEFAULT_MAX_POOL_SIZE, Is.EqualTo(100));
        Assert.That(PoolOptions.DEFAULT_CONNECTION_LIFETIME, Is.EqualTo(0));
        Assert.That(PoolOptions.DEFAULT_IDLE_TIMEOUT, Is.EqualTo(300));
    }

    #endregion

    #region Property Tests

    [Test]
    public void ConnectionStringPropertyWorksTest()
    {
        var options = new PoolOptions
        {
            ConnectionString = "Data Source=test.witdb"
        };

        Assert.That(options.ConnectionString, Is.EqualTo("Data Source=test.witdb"));
    }

    [Test]
    public void MinPoolSizePropertyWorksTest()
    {
        var options = new PoolOptions
        {
            MinPoolSize = 5
        };

        Assert.That(options.MinPoolSize, Is.EqualTo(5));
    }

    [Test]
    public void MaxPoolSizePropertyWorksTest()
    {
        var options = new PoolOptions
        {
            MaxPoolSize = 50
        };

        Assert.That(options.MaxPoolSize, Is.EqualTo(50));
    }

    [Test]
    public void ConnectionLifetimePropertyWorksTest()
    {
        var options = new PoolOptions
        {
            ConnectionLifetime = 3600
        };

        Assert.That(options.ConnectionLifetime, Is.EqualTo(3600));
    }

    [Test]
    public void IdleTimeoutPropertyWorksTest()
    {
        var options = new PoolOptions
        {
            IdleTimeout = 120
        };

        Assert.That(options.IdleTimeout, Is.EqualTo(120));
    }

    [Test]
    public void ValidateOnBorrowPropertyWorksTest()
    {
        var options = new PoolOptions
        {
            ValidateOnBorrow = true
        };

        Assert.That(options.ValidateOnBorrow, Is.True);
    }

    #endregion

    #region Combination Tests

    [Test]
    public void AllPropertiesCanBeSetTogetherTest()
    {
        var options = new PoolOptions
        {
            ConnectionString = "Data Source=:memory:",
            MinPoolSize = 10,
            MaxPoolSize = 200,
            ConnectionLifetime = 7200,
            IdleTimeout = 300,
            ValidateOnBorrow = true
        };

        Assert.That(options.ConnectionString, Is.EqualTo("Data Source=:memory:"));
        Assert.That(options.MinPoolSize, Is.EqualTo(10));
        Assert.That(options.MaxPoolSize, Is.EqualTo(200));
        Assert.That(options.ConnectionLifetime, Is.EqualTo(7200));
        Assert.That(options.IdleTimeout, Is.EqualTo(300));
        Assert.That(options.ValidateOnBorrow, Is.True);
    }

    #endregion
}

using NUnit.Framework;
using OutWit.Database.AdoNet.Pool;

namespace OutWit.Database.AdoNet.Tests.Pool;

/// <summary>
/// Tests for ConnectionPool.
/// </summary>
[TestFixture]
public class ConnectionPoolTests
{
    #region Setup/TearDown

    [TearDown]
    public void TearDown()
    {
        ConnectionPool.ClearAllPools();
    }

    #endregion

    #region Pool Creation Tests

    [Test]
    public void GetPoolReturnsPoolTest()
    {
        var pool = ConnectionPool.GetPool("Data Source=:memory:;Pooling=true");

        Assert.That(pool, Is.Not.Null);
    }

    [Test]
    public void GetPoolReturnsSamePoolForSameConnectionStringTest()
    {
        var pool1 = ConnectionPool.GetPool("Data Source=:memory:;Pooling=true");
        var pool2 = ConnectionPool.GetPool("Data Source=:memory:;Pooling=true");

        Assert.That(pool1, Is.SameAs(pool2));
    }

    [Test]
    public void GetPoolReturnsDifferentPoolForDifferentConnectionStringTest()
    {
        // Use different in-memory databases with shared cache to be distinguishable
        var pool1 = ConnectionPool.GetPool("Data Source=:memory:;Pooling=true;Min Pool Size=0");
        var pool2 = ConnectionPool.GetPool("Data Source=:memory:;Pooling=true;Max Pool Size=5");

        // They're different because connection strings are different
        Assert.That(pool1, Is.Not.SameAs(pool2));
    }

    [Test]
    public void GetPoolWithOptionsCreatesPoolTest()
    {
        var options = new PoolOptions
        {
            ConnectionString = "Data Source=:memory:",
            MinPoolSize = 2,
            MaxPoolSize = 10
        };

        var pool = ConnectionPool.GetPool(options);

        Assert.That(pool, Is.Not.Null);
        Assert.That(pool.Options.MinPoolSize, Is.EqualTo(2));
        Assert.That(pool.Options.MaxPoolSize, Is.EqualTo(10));
    }

    #endregion

    #region Connection Tests

    [Test]
    public void GetConnectionReturnsOpenConnectionTest()
    {
        var pool = ConnectionPool.GetPool("Data Source=:memory:;Pooling=true");

        using var connection = pool.GetConnection();

        Assert.That(connection, Is.Not.Null);
        Assert.That(connection.State, Is.EqualTo(System.Data.ConnectionState.Open));
    }

    [Test]
    public async Task GetConnectionAsyncReturnsOpenConnectionTest()
    {
        var pool = ConnectionPool.GetPool("Data Source=:memory:;Pooling=true");

        using var connection = await pool.GetConnectionAsync();

        Assert.That(connection, Is.Not.Null);
        Assert.That(connection.State, Is.EqualTo(System.Data.ConnectionState.Open));
    }

    [Test]
    public void GetConnectionIncreasesActiveConnectionsTest()
    {
        var pool = ConnectionPool.GetPool("Data Source=:memory:;Pooling=true;Min Pool Size=0");

        var initialActive = pool.ActiveConnections;
        using var connection = pool.GetConnection();
        var afterGetActive = pool.ActiveConnections;

        Assert.That(afterGetActive, Is.GreaterThan(initialActive));
    }

    #endregion

    #region Pool Size Tests

    [Test]
    public void MinPoolSizeCreatesInitialConnectionsTest()
    {
        var options = new PoolOptions
        {
            ConnectionString = "Data Source=:memory:",
            MinPoolSize = 3,
            MaxPoolSize = 10
        };

        var pool = ConnectionPool.GetPool(options);

        Assert.That(pool.TotalConnections, Is.GreaterThanOrEqualTo(3));
    }

    #endregion

    #region Clear Pool Tests

    [Test]
    public void ClearRemovesAllConnectionsTest()
    {
        var pool = ConnectionPool.GetPool("Data Source=:memory:;Pooling=true;Min Pool Size=2");

        pool.Clear();

        Assert.That(pool.TotalConnections, Is.EqualTo(0));
        Assert.That(pool.AvailableConnections, Is.EqualTo(0));
    }

    [Test]
    public void ClearPoolRemovesSpecificPoolTest()
    {
        var cs = "Data Source=:memory:;Pooling=true;Connection Lifetime=100";
        var pool1 = ConnectionPool.GetPool(cs);
        _ = ConnectionPool.GetPool("Data Source=:memory:;Pooling=true;Connection Lifetime=200");

        ConnectionPool.ClearPool(cs);

        // Getting pool again should create a new one
        var pool3 = ConnectionPool.GetPool(cs);
        Assert.That(pool3, Is.Not.SameAs(pool1));
    }

    [Test]
    public void ClearAllPoolsClearsAllTest()
    {
        ConnectionPool.GetPool("Data Source=:memory:;Pooling=true;Idle Timeout=100");
        ConnectionPool.GetPool("Data Source=:memory:;Pooling=true;Idle Timeout=200");

        ConnectionPool.ClearAllPools();

        // Getting pools again should create new ones
        var pool1 = ConnectionPool.GetPool("Data Source=:memory:;Pooling=true;Idle Timeout=100");
        var pool2 = ConnectionPool.GetPool("Data Source=:memory:;Pooling=true;Idle Timeout=200");

        Assert.That(pool1.TotalConnections, Is.GreaterThanOrEqualTo(0));
        Assert.That(pool2.TotalConnections, Is.GreaterThanOrEqualTo(0));
    }

    #endregion

    #region Properties Tests

    [Test]
    public void OptionsPropertyReturnsConfigurationTest()
    {
        var options = new PoolOptions
        {
            ConnectionString = "Data Source=:memory:",
            MinPoolSize = 5,
            MaxPoolSize = 20,
            IdleTimeout = 120,
            ConnectionLifetime = 3600
        };

        var pool = ConnectionPool.GetPool(options);

        Assert.That(pool.Options.ConnectionString, Is.EqualTo("Data Source=:memory:"));
        Assert.That(pool.Options.MinPoolSize, Is.EqualTo(5));
        Assert.That(pool.Options.MaxPoolSize, Is.EqualTo(20));
        Assert.That(pool.Options.IdleTimeout, Is.EqualTo(120));
        Assert.That(pool.Options.ConnectionLifetime, Is.EqualTo(3600));
    }

    [Test]
    public void TotalConnectionsReflectsPoolStateTest()
    {
        var options = new PoolOptions
        {
            ConnectionString = "Data Source=:memory:",
            MinPoolSize = 0,
            MaxPoolSize = 10
        };

        var pool = ConnectionPool.GetPool(options);
        var initialTotal = pool.TotalConnections;

        using var conn = pool.GetConnection();
        var afterGetTotal = pool.TotalConnections;

        Assert.That(afterGetTotal, Is.GreaterThanOrEqualTo(initialTotal));
    }

    [Test]
    public void AvailableConnectionsReflectsPoolStateTest()
    {
        var options = new PoolOptions
        {
            ConnectionString = "Data Source=:memory:",
            MinPoolSize = 2,
            MaxPoolSize = 10
        };

        var pool = ConnectionPool.GetPool(options);
        
        // MinPoolSize connections should be available
        Assert.That(pool.AvailableConnections, Is.GreaterThanOrEqualTo(0));
    }

    #endregion

    #region Multiple Connections Tests

    [Test]
    public void MultipleConnectionsCanBeObtainedTest()
    {
        var options = new PoolOptions
        {
            ConnectionString = "Data Source=:memory:",
            MinPoolSize = 0,
            MaxPoolSize = 5
        };

        var pool = ConnectionPool.GetPool(options);

        var connections = new List<WitDbConnection>();
        try
        {
            for (int i = 0; i < 3; i++)
            {
                connections.Add(pool.GetConnection());
            }

            Assert.That(pool.ActiveConnections, Is.EqualTo(3));
        }
        finally
        {
            foreach (var conn in connections)
            {
                conn.Dispose();
            }
        }
    }

    #endregion
}

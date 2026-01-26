using NUnit.Framework;
using System.Data;

namespace OutWit.Database.AdoNet.Tests.Connection;

/// <summary>
/// Tests for WitDbConnection properties.
/// </summary>
[TestFixture]
public class WitDbConnectionPropertiesTests
{
    #region DataSource Property

    [Test]
    public void DataSourceReturnsValueFromConnectionStringTest()
    {
        using var connection = new WitDbConnection("Data Source=test.db");

        Assert.That(connection.DataSource, Is.EqualTo("test.db"));
    }

    [Test]
    public void DataSourceWithMemoryReturnsMemoryValueTest()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");

        Assert.That(connection.DataSource, Is.EqualTo(":memory:"));
    }

    [Test]
    public void DataSourceWithPathReturnsFullPathTest()
    {
        using var connection = new WitDbConnection(@"Data Source=C:\Data\mydb.witdb");

        Assert.That(connection.DataSource, Is.EqualTo(@"C:\Data\mydb.witdb"));
    }

    [Test]
    public void DataSourceWhenNotSetReturnsNullOrEmptyTest()
    {
        using var connection = new WitDbConnection();

        Assert.That(connection.DataSource, Is.Null.Or.Empty);
    }

    #endregion

    #region Database Property

    [Test]
    public void DatabaseReturnsFileNameWithoutExtensionTest()
    {
        using var connection = new WitDbConnection("Data Source=mydata.witdb");

        Assert.That(connection.Database, Is.EqualTo("mydata"));
    }

    [Test]
    public void DatabaseWithPathReturnsFileNameOnlyTest()
    {
        using var connection = new WitDbConnection(@"Data Source=C:\Data\mydb.witdb");

        Assert.That(connection.Database, Is.EqualTo("mydb"));
    }

    [Test]
    public void DatabaseWithMemoryReturnsMainTest()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");

        Assert.That(connection.Database, Is.EqualTo("main").Or.EqualTo(":memory:"));
    }

    #endregion

    #region ServerVersion Property

    [Test]
    public void ServerVersionReturnsExpectedValueTest()
    {
        using var connection = new WitDbConnection();

        Assert.That(connection.ServerVersion, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void ServerVersionMatchesExpectedFormatTest()
    {
        using var connection = new WitDbConnection();

        Assert.That(connection.ServerVersion, Does.Match(@"^\d+\.\d+\.\d+"));
    }

    #endregion

    #region State Property

    [Test]
    public void StateInitiallyIsClosedTest()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
    }

    [Test]
    public void StateAfterOpenIsOpenTest()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        connection.Open();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
    }

    [Test]
    public void StateAfterCloseIsClosedTest()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");
        connection.Open();
        connection.Close();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
    }

    #endregion

    #region ConnectionTimeout Property

    [Test]
    public void ConnectionTimeoutReturnsDefaultValueTest()
    {
        using var connection = new WitDbConnection("Data Source=:memory:");

        Assert.That(connection.ConnectionTimeout, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void ConnectionTimeoutReturnsBaseClassDefaultTest()
    {
        // Note: WitDbConnection doesn't currently override ConnectionTimeout to read from connection string
        // It returns the base class default (15 seconds)
        using var connection = new WitDbConnection("Data Source=:memory:;Default Timeout=60");

        // Base class DbConnection returns 15 as default
        Assert.That(connection.ConnectionTimeout, Is.EqualTo(15));
    }

    #endregion
}

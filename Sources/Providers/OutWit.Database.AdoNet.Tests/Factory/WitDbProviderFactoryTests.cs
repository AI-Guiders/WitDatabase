using NUnit.Framework;
using System.Data.Common;

namespace OutWit.Database.AdoNet.Tests.Factory;

/// <summary>
/// Tests for WitDbProviderFactory.
/// </summary>
[TestFixture]
public class WitDbProviderFactoryTests
{
    #region Singleton Tests

    [Test]
    public void InstanceIsNotNullTest()
    {
        Assert.That(WitDbProviderFactory.Instance, Is.Not.Null);
    }

    [Test]
    public void InstanceIsSingletonTest()
    {
        var instance1 = WitDbProviderFactory.Instance;
        var instance2 = WitDbProviderFactory.Instance;

        Assert.That(instance1, Is.SameAs(instance2));
    }

    [Test]
    public void ProviderInvariantNameIsCorrectTest()
    {
        Assert.That(WitDbProviderFactory.PROVIDER_INVARIANT_NAME, Is.EqualTo("OutWit.Database.AdoNet"));
    }

    #endregion

    #region CreateConnection Tests

    [Test]
    public void CreateConnectionReturnsWitDbConnectionTest()
    {
        var factory = WitDbProviderFactory.Instance;

        using var connection = factory.CreateConnection();

        Assert.That(connection, Is.InstanceOf<WitDbConnection>());
    }

    [Test]
    public void CreateConnectionReturnsNewInstanceTest()
    {
        var factory = WitDbProviderFactory.Instance;

        using var conn1 = factory.CreateConnection();
        using var conn2 = factory.CreateConnection();

        Assert.That(conn1, Is.Not.SameAs(conn2));
    }

    #endregion

    #region CreateCommand Tests

    [Test]
    public void CreateCommandReturnsWitDbCommandTest()
    {
        var factory = WitDbProviderFactory.Instance;

        using var command = factory.CreateCommand();

        Assert.That(command, Is.InstanceOf<WitDbCommand>());
    }

    #endregion

    #region CreateParameter Tests

    [Test]
    public void CreateParameterReturnsWitDbParameterTest()
    {
        var factory = WitDbProviderFactory.Instance;

        var parameter = factory.CreateParameter();

        Assert.That(parameter, Is.InstanceOf<WitDbParameter>());
    }

    #endregion

    #region CreateConnectionStringBuilder Tests

    [Test]
    public void CreateConnectionStringBuilderReturnsWitDbConnectionStringBuilderTest()
    {
        var factory = WitDbProviderFactory.Instance;

        var builder = factory.CreateConnectionStringBuilder();

        Assert.That(builder, Is.InstanceOf<WitDbConnectionStringBuilder>());
    }

    #endregion

    #region CreateDataAdapter Tests

    [Test]
    public void CreateDataAdapterReturnsWitDbDataAdapterTest()
    {
        var factory = WitDbProviderFactory.Instance;

        using var adapter = factory.CreateDataAdapter();

        Assert.That(adapter, Is.InstanceOf<WitDbDataAdapter>());
    }

    [Test]
    public void CanCreateDataAdapterReturnsTrueTest()
    {
        var factory = WitDbProviderFactory.Instance;

        Assert.That(factory.CanCreateDataAdapter, Is.True);
    }

    #endregion

    #region CreateCommandBuilder Tests

    [Test]
    public void CreateCommandBuilderReturnsWitDbCommandBuilderTest()
    {
        var factory = WitDbProviderFactory.Instance;

        using var builder = factory.CreateCommandBuilder();

        Assert.That(builder, Is.InstanceOf<WitDbCommandBuilder>());
    }

    [Test]
    public void CanCreateCommandBuilderReturnsTrueTest()
    {
        var factory = WitDbProviderFactory.Instance;

        Assert.That(factory.CanCreateCommandBuilder, Is.True);
    }

    #endregion

    #region Full Workflow Test

    [Test]
    public void FactoryCreatesWorkingComponentsTest()
    {
        var factory = WitDbProviderFactory.Instance;

        using var connection = factory.CreateConnection();
        connection!.ConnectionString = "Data Source=:memory:";
        connection.Open();

        using var command = factory.CreateCommand();
        command!.Connection = connection;
        command.CommandText = "SELECT 1 + 1";

        var result = command.ExecuteScalar();

        Assert.That(result, Is.EqualTo(2L));
    }

    [Test]
    public void FactoryWithDbProviderFactoriesPatternTest()
    {
        // This tests the pattern that would be used with DbProviderFactories
        DbProviderFactory factory = WitDbProviderFactory.Instance;

        using var connection = factory.CreateConnection();
        Assert.That(connection, Is.Not.Null);

        using var command = factory.CreateCommand();
        Assert.That(command, Is.Not.Null);

        var parameter = factory.CreateParameter();
        Assert.That(parameter, Is.Not.Null);
    }

    #endregion
}

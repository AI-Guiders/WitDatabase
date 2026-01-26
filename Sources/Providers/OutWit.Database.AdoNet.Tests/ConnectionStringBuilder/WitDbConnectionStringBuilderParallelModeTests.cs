using OutWit.Database.AdoNet;

namespace OutWit.Database.AdoNet.Tests.ConnectionStringBuilder;

/// <summary>
/// Tests for WitDbConnectionStringBuilder parallel mode properties.
/// </summary>
[TestFixture]
public class WitDbConnectionStringBuilderParallelModeTests
{
    #region ParallelMode Property Tests

    [Test]
    public void ParallelModeDefaultIsNoneTest()
    {
        var builder = new WitDbConnectionStringBuilder();
        Assert.That(builder.ParallelMode, Is.EqualTo(WitDbParallelMode.None));
    }

    [Test]
    public void ParallelModeCanBeSetTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            ParallelMode = WitDbParallelMode.Auto
        };

        Assert.That(builder.ParallelMode, Is.EqualTo(WitDbParallelMode.Auto));
    }

    [Test]
    public void ParallelModeSerializesToStringTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.witdb",
            ParallelMode = WitDbParallelMode.Buffered
        };

        var cs = builder.ConnectionString;
        Assert.That(cs, Does.Contain("Parallel Mode=Buffered"));
    }

    [Test]
    public void ParallelModeParsesFromStringTest()
    {
        var cs = "Data Source=test.witdb;Parallel Mode=Latched";
        var builder = new WitDbConnectionStringBuilder(cs);

        Assert.That(builder.ParallelMode, Is.EqualTo(WitDbParallelMode.Latched));
    }

    [Test]
    public void ParallelModeParsesFromStringCaseInsensitiveTest()
    {
        var cs = "Data Source=test.witdb;Parallel Mode=AUTO";
        var builder = new WitDbConnectionStringBuilder(cs);

        Assert.That(builder.ParallelMode, Is.EqualTo(WitDbParallelMode.Auto));
    }

    [TestCase(WitDbParallelMode.None)]
    [TestCase(WitDbParallelMode.Auto)]
    [TestCase(WitDbParallelMode.Buffered)]
    [TestCase(WitDbParallelMode.Latched)]
    [TestCase(WitDbParallelMode.Optimistic)]
    public void AllParallelModesRoundTripTest(WitDbParallelMode mode)
    {
        var builder1 = new WitDbConnectionStringBuilder
        {
            DataSource = "test.witdb",
            ParallelMode = mode
        };

        var cs = builder1.ConnectionString;
        var builder2 = new WitDbConnectionStringBuilder(cs);

        Assert.That(builder2.ParallelMode, Is.EqualTo(mode));
    }

    #endregion

    #region MaxWriters Property Tests

    [Test]
    public void MaxWritersDefaultIsProcessorCountTest()
    {
        var builder = new WitDbConnectionStringBuilder();
        Assert.That(builder.MaxWriters, Is.EqualTo(Environment.ProcessorCount));
    }

    [Test]
    public void MaxWritersCanBeSetTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            MaxWriters = 8
        };

        Assert.That(builder.MaxWriters, Is.EqualTo(8));
    }

    [Test]
    public void MaxWritersSerializesToStringTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.witdb",
            MaxWriters = 16
        };

        var cs = builder.ConnectionString;
        Assert.That(cs, Does.Contain("Max Writers=16"));
    }

    [Test]
    public void MaxWritersParsesFromStringTest()
    {
        var cs = "Data Source=test.witdb;Max Writers=32";
        var builder = new WitDbConnectionStringBuilder(cs);

        Assert.That(builder.MaxWriters, Is.EqualTo(32));
    }

    #endregion

    #region Combined Parallel Settings Tests

    [Test]
    public void ParallelModeAndMaxWritersCombinedTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.witdb",
            ParallelMode = WitDbParallelMode.Buffered,
            MaxWriters = 4
        };

        var cs = builder.ConnectionString;
        
        Assert.That(cs, Does.Contain("Parallel Mode=Buffered"));
        Assert.That(cs, Does.Contain("Max Writers=4"));

        var builder2 = new WitDbConnectionStringBuilder(cs);
        Assert.That(builder2.ParallelMode, Is.EqualTo(WitDbParallelMode.Buffered));
        Assert.That(builder2.MaxWriters, Is.EqualTo(4));
    }

    [Test]
    public void ParallelModeNotIncludedInProviderParametersTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.witdb",
            ParallelMode = WitDbParallelMode.Auto,
            MaxWriters = 8
        };

        var providerParams = builder.GetProviderParameters().ToList();
        
        Assert.That(providerParams.Select(p => p.Key), Does.Not.Contain("Parallel Mode"));
        Assert.That(providerParams.Select(p => p.Key), Does.Not.Contain("Max Writers"));
    }

    #endregion

    #region Validation Tests

    [Test]
    public void ValidationFailsForInvalidMaxWritersTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.witdb",
            MaxWriters = 0
        };

        var errors = builder.Validate();
        Assert.That(errors, Has.Count.GreaterThan(0));
        Assert.That(errors[0], Does.Contain("Max Writers"));
    }

    [Test]
    public void ValidationPassesForValidParallelConfigTest()
    {
        var builder = new WitDbConnectionStringBuilder
        {
            DataSource = "test.witdb",
            ParallelMode = WitDbParallelMode.Auto,
            MaxWriters = 4
        };

        var errors = builder.Validate();
        Assert.That(errors, Is.Empty);
    }

    #endregion

    #region Full Connection String Examples

    [Test]
    public void FullConnectionStringWithParallelModeTest()
    {
        var cs = "Data Source=myapp.witdb;Store=btree;Parallel Mode=Auto;Max Writers=8;Cache=clock;CacheSize=5000";
        var builder = new WitDbConnectionStringBuilder(cs);

        Assert.That(builder.DataSource, Is.EqualTo("myapp.witdb"));
        Assert.That(builder.Store, Is.EqualTo("btree"));
        Assert.That(builder.ParallelMode, Is.EqualTo(WitDbParallelMode.Auto));
        Assert.That(builder.MaxWriters, Is.EqualTo(8));
        Assert.That(builder.Cache, Is.EqualTo("clock"));
    }

    [Test]
    public void LsmWithParallelModeConnectionStringTest()
    {
        var cs = "Data Source=./data;Store=lsm;Parallel Mode=Buffered;Max Writers=16";
        var builder = new WitDbConnectionStringBuilder(cs);

        Assert.That(builder.Store, Is.EqualTo("lsm"));
        Assert.That(builder.ParallelMode, Is.EqualTo(WitDbParallelMode.Buffered));
        Assert.That(builder.MaxWriters, Is.EqualTo(16));
    }

    #endregion
}

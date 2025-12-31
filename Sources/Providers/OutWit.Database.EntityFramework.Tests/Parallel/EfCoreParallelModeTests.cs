using Microsoft.EntityFrameworkCore;
using OutWit.Database.AdoNet;
using OutWit.Database.EntityFramework.Extensions;
using OutWit.Database.EntityFramework.Infrastructure;

namespace OutWit.Database.EntityFramework.Tests.Parallel;

/// <summary>
/// Tests for EF Core parallel mode configuration.
/// </summary>
[TestFixture]
public class EfCoreParallelModeTests : IDisposable
{
    #region Fields

    private string m_testDir = null!;

    #endregion

    #region Setup/TearDown

    [SetUp]
    public void SetUp()
    {
        m_testDir = Path.Combine(Path.GetTempPath(), $"efcore_parallel_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(m_testDir);
    }

    [TearDown]
    public void TearDown()
    {
        Dispose();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(m_testDir))
                Directory.Delete(m_testDir, recursive: true);
        }
        catch { }
    }

    #endregion

    #region Extension Configuration Tests

    [Test]
    public void WitDbContextOptionsExtensionWithParallelModeTest()
    {
        var extension = new WitDbContextOptionsExtension()
            .WithConnectionString("Data Source=test.witdb")
            .WithParallelMode(WitDbParallelMode.Auto);

        Assert.That(extension.ParallelMode, Is.EqualTo(WitDbParallelMode.Auto));
        Assert.That(extension.ConnectionString, Is.EqualTo("Data Source=test.witdb"));
    }

    [Test]
    public void WitDbContextOptionsExtensionWithMaxWritersTest()
    {
        var extension = new WitDbContextOptionsExtension()
            .WithConnectionString("Data Source=test.witdb")
            .WithParallelMode(WitDbParallelMode.Latched)
            .WithMaxWriters(8);

        Assert.That(extension.ParallelMode, Is.EqualTo(WitDbParallelMode.Latched));
        Assert.That(extension.MaxWriters, Is.EqualTo(8));
    }

    [Test]
    public void GetEffectiveConnectionStringWithParallelModeTest()
    {
        var extension = new WitDbContextOptionsExtension()
            .WithConnectionString("Data Source=test.witdb")
            .WithParallelMode(WitDbParallelMode.Buffered)
            .WithMaxWriters(4);

        var effectiveCs = extension.GetEffectiveConnectionString();

        Assert.That(effectiveCs, Does.Contain("Parallel Mode=Buffered"));
        Assert.That(effectiveCs, Does.Contain("Max Writers=4"));
    }

    [Test]
    public void GetEffectiveConnectionStringWithoutParallelModeTest()
    {
        var extension = new WitDbContextOptionsExtension()
            .WithConnectionString("Data Source=test.witdb");

        var effectiveCs = extension.GetEffectiveConnectionString();

        Assert.That(effectiveCs, Is.EqualTo("Data Source=test.witdb"));
    }

    #endregion

    #region Builder API Tests

    [Test]
    public void DbContextOptionsBuilderWithParallelWritesTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder();
        var dbPath = Path.Combine(m_testDir, "test.witdb");

        optionsBuilder.UseWitDb($"Data Source={dbPath}", opts =>
        {
            opts.UseParallelWrites(WitDbParallelMode.Auto);
            opts.MaxWriters(8);
        });

        var extension = optionsBuilder.Options.FindExtension<WitDbContextOptionsExtension>();
        Assert.That(extension, Is.Not.Null);
        Assert.That(extension!.ParallelMode, Is.EqualTo(WitDbParallelMode.Auto));
        Assert.That(extension.MaxWriters, Is.EqualTo(8));
    }

    [Test]
    public void DbContextOptionsBuilderUseParallelWritesDefaultTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder();
        var dbPath = Path.Combine(m_testDir, "test.witdb");

        optionsBuilder.UseWitDb($"Data Source={dbPath}", opts =>
        {
            opts.UseParallelWrites(); // Should use Auto
        });

        var extension = optionsBuilder.Options.FindExtension<WitDbContextOptionsExtension>();
        Assert.That(extension, Is.Not.Null);
        Assert.That(extension!.ParallelMode, Is.EqualTo(WitDbParallelMode.Auto));
    }

    #endregion

    #region ExtensionInfo Tests

    [Test]
    public void ExtensionInfoIncludesParallelModeInLogFragmentTest()
    {
        var extension = new WitDbContextOptionsExtension()
            .WithConnectionString("Data Source=test.witdb")
            .WithParallelMode(WitDbParallelMode.Latched)
            .WithMaxWriters(4);

        var logFragment = extension.Info.LogFragment;

        Assert.That(logFragment, Does.Contain("Parallel: Latched"));
        Assert.That(logFragment, Does.Contain("MaxWriters: 4"));
    }

    [Test]
    public void ExtensionInfoGetServiceProviderHashCodeIncludesParallelModeTest()
    {
        var extension1 = new WitDbContextOptionsExtension()
            .WithConnectionString("Data Source=test.witdb")
            .WithParallelMode(WitDbParallelMode.Auto);

        var extension2 = new WitDbContextOptionsExtension()
            .WithConnectionString("Data Source=test.witdb")
            .WithParallelMode(WitDbParallelMode.Latched);

        // Different parallel modes should have different hash codes
        Assert.That(extension1.Info.GetServiceProviderHashCode(), 
            Is.Not.EqualTo(extension2.Info.GetServiceProviderHashCode()));
    }

    [Test]
    public void ExtensionInfoShouldUseSameServiceProviderTest()
    {
        var extension1 = new WitDbContextOptionsExtension()
            .WithConnectionString("Data Source=test.witdb")
            .WithParallelMode(WitDbParallelMode.Auto);

        var extension2 = new WitDbContextOptionsExtension()
            .WithConnectionString("Data Source=test.witdb")
            .WithParallelMode(WitDbParallelMode.Auto);

        Assert.That(extension1.Info.ShouldUseSameServiceProvider(extension2.Info), Is.True);
    }

    [Test]
    public void ExtensionInfoPopulateDebugInfoTest()
    {
        var extension = new WitDbContextOptionsExtension()
            .WithConnectionString("Data Source=test.witdb")
            .WithParallelMode(WitDbParallelMode.Buffered)
            .WithMaxWriters(6);

        var debugInfo = new Dictionary<string, string>();
        extension.Info.PopulateDebugInfo(debugInfo);

        Assert.That(debugInfo["WitDb:ParallelMode"], Is.EqualTo("Buffered"));
        Assert.That(debugInfo["WitDb:MaxWriters"], Is.EqualTo("6"));
    }

    #endregion
}

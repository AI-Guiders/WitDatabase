using OutWit.Database.Core.Builder;
using OutWit.Database.Core.Stores;
using TextEncoding = System.Text.Encoding;

namespace OutWit.Database.Core.Tests.Builder;

/// <summary>
/// Tests for WitDatabaseBuilder parallel mode configuration.
/// </summary>
[TestFixture]
public class WitDatabaseBuilderParallelModeTests : IDisposable
{
    #region Fields

    private string m_testDir = null!;

    #endregion

    #region Setup/TearDown

    [SetUp]
    public void SetUp()
    {
        m_testDir = Path.Combine(Path.GetTempPath(), $"builder_parallel_test_{Guid.NewGuid():N}");
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

    #region Basic Configuration Tests

    [Test]
    public void WithParallelWritesSetsAutoModeTest()
    {
        var builder = new WitDatabaseBuilder()
            .WithMemoryStorage()
            .WithParallelWrites();

        Assert.That(builder.Options.StoreParameters.Get<ParallelMode>("parallelMode"), 
            Is.EqualTo(ParallelMode.Auto));
    }

    [Test]
    public void WithParallelWritesModeSetsModeTest()
    {
        var builder = new WitDatabaseBuilder()
            .WithMemoryStorage()
            .WithParallelWrites(ParallelMode.Buffered);

        Assert.That(builder.Options.StoreParameters.Get<ParallelMode>("parallelMode"), 
            Is.EqualTo(ParallelMode.Buffered));
    }

    [Test]
    public void WithParallelWritesOptionsConfiguresOptionsTest()
    {
        var options = new ParallelModeOptions
        {
            Mode = ParallelMode.Latched,
            MaxWriters = 8,
            UseOptimisticReads = false
        };

        var builder = new WitDatabaseBuilder()
            .WithMemoryStorage()
            .WithParallelWrites(options);

        Assert.That(builder.Options.StoreParameters.Get<ParallelMode>("parallelMode"), 
            Is.EqualTo(ParallelMode.Latched));
        
        var storedOptions = builder.Options.StoreParameters.Get<ParallelModeOptions>("parallelOptions");
        Assert.That(storedOptions, Is.Not.Null);
        Assert.That(storedOptions!.MaxWriters, Is.EqualTo(8));
        Assert.That(storedOptions.UseOptimisticReads, Is.False);
    }

    [Test]
    public void WithParallelWritesActionConfiguresOptionsTest()
    {
        var builder = new WitDatabaseBuilder()
            .WithMemoryStorage()
            .WithParallelWrites(opts =>
            {
                opts.Mode = ParallelMode.Optimistic;
                opts.TrackStatistics = true;
            });

        Assert.That(builder.Options.StoreParameters.Get<ParallelMode>("parallelMode"), 
            Is.EqualTo(ParallelMode.Optimistic));
        
        var storedOptions = builder.Options.StoreParameters.Get<ParallelModeOptions>("parallelOptions");
        Assert.That(storedOptions, Is.Not.Null);
        Assert.That(storedOptions!.TrackStatistics, Is.True);
    }

    [Test]
    public void WithMaxWritersSetsValueTest()
    {
        var builder = new WitDatabaseBuilder()
            .WithMemoryStorage()
            .WithParallelWrites()
            .WithMaxWriters(16);

        Assert.That(builder.Options.StoreParameters.Get<int>("maxWriters"), Is.EqualTo(16));
    }

    [Test]
    public void WithoutParallelWritesDisablesParallelModeTest()
    {
        var builder = new WitDatabaseBuilder()
            .WithMemoryStorage()
            .WithParallelWrites(ParallelMode.Buffered)
            .WithoutParallelWrites();

        Assert.That(builder.Options.StoreParameters.Get<ParallelMode>("parallelMode"), 
            Is.EqualTo(ParallelMode.None));
    }

    #endregion

    #region Validation Tests

    [Test]
    public void WithMaxWritersThrowsForInvalidValueTest()
    {
        var builder = new WitDatabaseBuilder()
            .WithMemoryStorage();

        Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithMaxWriters(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithMaxWriters(-1));
    }

    [Test]
    public void WithParallelWritesThrowsForNullOptionsTest()
    {
        var builder = new WitDatabaseBuilder()
            .WithMemoryStorage();

        Assert.Throws<ArgumentNullException>(() => builder.WithParallelWrites((ParallelModeOptions)null!));
        Assert.Throws<ArgumentNullException>(() => builder.WithParallelWrites((Action<ParallelModeOptions>)null!));
    }

    #endregion

    #region ParallelModeOptions Presets Tests

    [Test]
    public void DefaultPresetHasCorrectValuesTest()
    {
        var options = ParallelModeOptions.Default;

        Assert.That(options.Mode, Is.EqualTo(ParallelMode.None));
        Assert.That(options.UseOptimisticReads, Is.True);
        Assert.That(options.TrackStatistics, Is.False);
    }

    [Test]
    public void HighWriteThroughputPresetHasCorrectValuesTest()
    {
        var options = ParallelModeOptions.HighWriteThroughput;

        Assert.That(options.Mode, Is.EqualTo(ParallelMode.Buffered));
        Assert.That(options.MaxWriters, Is.GreaterThan(1));
        Assert.That(options.BufferSizeThreshold, Is.GreaterThan(64 * 1024));
    }

    [Test]
    public void MixedWorkloadPresetHasCorrectValuesTest()
    {
        var options = ParallelModeOptions.MixedWorkload;

        Assert.That(options.Mode, Is.EqualTo(ParallelMode.Latched));
        Assert.That(options.UseOptimisticReads, Is.True);
    }

    [Test]
    public void DebugPresetHasCorrectValuesTest()
    {
        var options = ParallelModeOptions.Debug;

        Assert.That(options.Mode, Is.EqualTo(ParallelMode.Latched));
        Assert.That(options.UseOptimisticReads, Is.False);
        Assert.That(options.TrackStatistics, Is.True);
        Assert.That(options.LatchTimeout, Is.LessThan(TimeSpan.FromSeconds(30)));
    }

    #endregion

    #region Integration Tests

    [Test]
    public void BuildWithParallelModeBTreeTest()
    {
        var filePath = Path.Combine(m_testDir, "parallel_btree.witdb");
        
        var builder = new WitDatabaseBuilder()
            .WithFilePath(filePath)
            .WithBTree()
            .WithParallelWrites(ParallelMode.Auto)
            .WithoutTransactions();

        // Build should succeed
        using var db = builder.Build();
        Assert.That(db, Is.Not.Null);
    }

    [Test]
    public void BuildWithParallelModeLsmTest()
    {
        var dir = Path.Combine(m_testDir, "parallel_lsm");
        
        var builder = new WitDatabaseBuilder()
            .WithLsmTree(dir)
            .WithParallelWrites(ParallelMode.Buffered)
            .WithoutTransactions();

        // Build should succeed
        using var db = builder.Build();
        Assert.That(db, Is.Not.Null);
    }

    [Test]
    public void BuildWithHighWriteThroughputPresetTest()
    {
        var filePath = Path.Combine(m_testDir, "high_throughput.witdb");
        
        var builder = new WitDatabaseBuilder()
            .WithFilePath(filePath)
            .WithBTree()
            .WithParallelWrites(ParallelModeOptions.HighWriteThroughput)
            .WithoutTransactions();

        using var db = builder.Build();
        Assert.That(db, Is.Not.Null);
    }

    #endregion
}

using OutWit.Database.Core.LSM;
using TextEncoding = System.Text.Encoding;

namespace OutWit.Database.Core.Tests.LSM;

/// <summary>
/// Tests for LsmParallelCompactor parallel compaction functionality.
/// </summary>
[TestFixture]
public class LsmParallelCompactorTests : IDisposable
{
    #region Fields

    private string m_testDir = null!;

    #endregion

    #region Setup/TearDown

    [SetUp]
    public void SetUp()
    {
        m_testDir = Path.Combine(Path.GetTempPath(), $"compactor_test_{Guid.NewGuid():N}");
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

    #region Helper Methods

    private string CreateTestSSTable(int id, int entryCount)
    {
        var path = Path.Combine(m_testDir, $"sst_{id:D6}.sst");
        using var builder = new SSTableBuilder(path, targetBlockSize: 4096);
        
        for (int i = 0; i < entryCount; i++)
        {
            var key = ToBytes($"key_{id}_{i:D5}");
            var value = ToBytes($"value_{id}_{i}");
            builder.Add(key, value);
        }
        
        builder.Finish();
        return path;
    }

    private static byte[] ToBytes(string s) => TextEncoding.UTF8.GetBytes(s);

    #endregion

    #region Basic Compaction Tests

    [Test]
    public async Task TrySubmitCompactsFilesTest()
    {
        var completedResults = new List<CompactionResult>();
        var lockObj = new object();

        await using var compactor = new LsmParallelCompactor(
            m_testDir,
            result => { lock (lockObj) completedResults.Add(result); });

        // Create test SSTables
        var sst1 = CreateTestSSTable(1, 10);
        var sst2 = CreateTestSSTable(2, 10);

        var outputPath = Path.Combine(m_testDir, "compacted.sst");
        var result = compactor.TrySubmit([sst1, sst2], outputPath);
        
        Assert.That(result, Is.True);

        await compactor.WaitForAllAsync();

        Assert.That(completedResults, Has.Count.EqualTo(1));
        Assert.That(completedResults[0].InputFiles, Is.EqualTo(2));
        Assert.That(File.Exists(outputPath), Is.True);
    }

    [Test]
    public async Task SubmitAndWaitAsyncReturnsResultTest()
    {
        await using var compactor = new LsmParallelCompactor(
            m_testDir,
            _ => { });

        var sst1 = CreateTestSSTable(1, 20);
        var sst2 = CreateTestSSTable(2, 30);

        var outputPath = Path.Combine(m_testDir, "compacted.sst");
        var result = await compactor.SubmitAndWaitAsync([sst1, sst2], outputPath);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.InputFiles, Is.EqualTo(2));
        Assert.That(result.OutputEntries, Is.EqualTo(50));
        Assert.That(result.OutputFile, Is.EqualTo(outputPath));
    }

    #endregion

    #region Parallel Compaction Tests

    [Test]
    public async Task MultipleCompactionsRunInParallelTest()
    {
        var completedCount = 0;

        await using var compactor = new LsmParallelCompactor(
            m_testDir,
            _ => Interlocked.Increment(ref completedCount),
            maxParallelCompactions: 2);

        // Create multiple compaction jobs
        var tasks = new List<Task<CompactionResult>>();
        for (int i = 0; i < 4; i++)
        {
            var sst1 = CreateTestSSTable(i * 2, 10);
            var sst2 = CreateTestSSTable(i * 2 + 1, 10);
            var outputPath = Path.Combine(m_testDir, $"compacted_{i}.sst");
            
            tasks.Add(compactor.SubmitAndWaitAsync([sst1, sst2], outputPath));
        }

        var results = await Task.WhenAll(tasks);

        Assert.That(results.All(r => r.IsSuccess), Is.True);
        Assert.That(completedCount, Is.EqualTo(4));
        Assert.That(compactor.CompactionsCompleted, Is.EqualTo(4));
    }

    [Test]
    public async Task ConcurrentSubmissionsTest()
    {
        await using var compactor = new LsmParallelCompactor(
            m_testDir,
            _ => { },
            maxParallelCompactions: 4);

        const int totalJobs = 10;
        var tasks = Enumerable.Range(0, totalJobs).Select(async i =>
        {
            var sst1 = CreateTestSSTable(i * 2, 5);
            var sst2 = CreateTestSSTable(i * 2 + 1, 5);
            var outputPath = Path.Combine(m_testDir, $"compacted_{i}.sst");
            
            return await compactor.SubmitAndWaitAsync([sst1, sst2], outputPath);
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.That(results.All(r => r.IsSuccess), Is.True);
        Assert.That(compactor.CompactionsCompleted, Is.EqualTo(totalJobs));
    }

    #endregion

    #region Statistics Tests

    [Test]
    public async Task StatisticsTrackCorrectlyTest()
    {
        await using var compactor = new LsmParallelCompactor(
            m_testDir,
            _ => { });

        Assert.That(compactor.CompactionsCompleted, Is.EqualTo(0));
        Assert.That(compactor.TotalInputFiles, Is.EqualTo(0));

        var sst1 = CreateTestSSTable(1, 10);
        var sst2 = CreateTestSSTable(2, 15);

        var outputPath = Path.Combine(m_testDir, "compacted.sst");
        await compactor.SubmitAndWaitAsync([sst1, sst2], outputPath);

        Assert.That(compactor.CompactionsCompleted, Is.EqualTo(1));
        Assert.That(compactor.TotalInputFiles, Is.EqualTo(2));
        Assert.That(compactor.TotalOutputEntries, Is.EqualTo(25));
    }

    [Test]
    public async Task TombstonesRemovedTrackedTest()
    {
        await using var compactor = new LsmParallelCompactor(
            m_testDir,
            _ => { });

        // Create SSTable with tombstones
        var sstPath = Path.Combine(m_testDir, "sst_with_tombstones.sst");
        using (var builder = new SSTableBuilder(sstPath, targetBlockSize: 4096))
        {
            builder.Add(ToBytes("key1"), ToBytes("value1"));
            builder.Add(ToBytes("key2"), null); // Tombstone
            builder.Add(ToBytes("key3"), ToBytes("value3"));
            builder.Add(ToBytes("key4"), null); // Tombstone
            builder.Finish();
        }

        var outputPath = Path.Combine(m_testDir, "compacted.sst");
        var result = await compactor.SubmitAndWaitAsync([sstPath], outputPath);

        Assert.That(result.TombstonesRemoved, Is.EqualTo(2));
        Assert.That(result.OutputEntries, Is.EqualTo(2));
        Assert.That(compactor.TotalTombstonesRemoved, Is.EqualTo(2));
    }

    #endregion

    #region Error Handling Tests

    [Test]
    public async Task CompactionWithNonExistentFileFailsTest()
    {
        await using var compactor = new LsmParallelCompactor(
            m_testDir,
            _ => { });

        var nonExistentPath = Path.Combine(m_testDir, "nonexistent.sst");
        var outputPath = Path.Combine(m_testDir, "output.sst");

        Assert.ThrowsAsync<FileNotFoundException>(async () =>
            await compactor.SubmitAndWaitAsync([nonExistentPath], outputPath));
    }

    #endregion

    #region Disposal Tests

    [Test]
    public async Task DisposeWaitsForPendingCompactionsTest()
    {
        var completedCount = 0;

        var compactor = new LsmParallelCompactor(
            m_testDir,
            _ => Interlocked.Increment(ref completedCount));

        // Submit compactions
        for (int i = 0; i < 3; i++)
        {
            var sst1 = CreateTestSSTable(i * 2, 5);
            var sst2 = CreateTestSSTable(i * 2 + 1, 5);
            var outputPath = Path.Combine(m_testDir, $"compacted_{i}.sst");
            
            compactor.TrySubmit([sst1, sst2], outputPath);
        }

        await compactor.DisposeAsync();

        Assert.That(completedCount, Is.EqualTo(3));
    }

    [Test]
    public void DisposedCompactorThrowsTest()
    {
        var compactor = new LsmParallelCompactor(m_testDir, _ => { });
        compactor.Dispose();

        Assert.Throws<ObjectDisposedException>(() => 
            compactor.TrySubmit([], "output.sst"));
    }

    #endregion
}

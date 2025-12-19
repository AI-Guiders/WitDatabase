using OutWit.Database.Core.Concurrency;

namespace OutWit.Database.Core.Tests.Concurrency;

/// <summary>
/// Unit tests for FileLock component.
/// Tests cross-process file locking mechanism.
/// </summary>
[TestFixture]
public class FileLockTests : IDisposable
{
    private string m_testDir = null!;

    [SetUp]
    public void SetUp()
    {
        m_testDir = Path.Combine(Path.GetTempPath(), $"filelock_test_{Guid.NewGuid():N}");
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

    #region Basic Lock Tests

    [Test]
    public void AcquireSharedLock_Succeeds()
    {
        var lockPath = Path.Combine(m_testDir, "test.db");
        using var fileLock = new FileLock(lockPath);
        
        Assert.DoesNotThrow(() => fileLock.AcquireSharedLock());
        Assert.That(fileLock.HasSharedLock, Is.True);
    }

    [Test]
    public void AcquireExclusiveLock_Succeeds()
    {
        var lockPath = Path.Combine(m_testDir, "test.db");
        using var fileLock = new FileLock(lockPath);
        
        Assert.DoesNotThrow(() => fileLock.AcquireExclusiveLock());
        Assert.That(fileLock.HasExclusiveLock, Is.True);
    }

    [Test]
    public async Task AcquireSharedLockAsync_Succeeds()
    {
        var lockPath = Path.Combine(m_testDir, "test.db");
        using var fileLock = new FileLock(lockPath);
        
        await fileLock.AcquireSharedLockAsync();
        
        Assert.That(fileLock.HasSharedLock, Is.True);
    }

    [Test]
    public async Task AcquireExclusiveLockAsync_Succeeds()
    {
        var lockPath = Path.Combine(m_testDir, "test.db");
        using var fileLock = new FileLock(lockPath);
        
        await fileLock.AcquireExclusiveLockAsync();
        
        Assert.That(fileLock.HasExclusiveLock, Is.True);
    }

    #endregion

    #region Exclusive Lock Blocking Tests

    [Test]
    public void ExclusiveLock_BlocksOtherExclusive()
    {
        var lockPath = Path.Combine(m_testDir, "exclusive.db");
        using var fileLock1 = new FileLock(lockPath, TimeSpan.FromMilliseconds(100));
        using var fileLock2 = new FileLock(lockPath, TimeSpan.FromMilliseconds(100));
        
        fileLock1.AcquireExclusiveLock();
        
        Assert.Throws<TimeoutException>(() => fileLock2.AcquireExclusiveLock());
    }

    [Test]
    public void ExclusiveLock_BlocksShared()
    {
        var lockPath = Path.Combine(m_testDir, "excl_blocks_shared.db");
        using var fileLock1 = new FileLock(lockPath, TimeSpan.FromMilliseconds(100));
        using var fileLock2 = new FileLock(lockPath, TimeSpan.FromMilliseconds(100));
        
        fileLock1.AcquireExclusiveLock();
        
        Assert.Throws<TimeoutException>(() => fileLock2.AcquireSharedLock());
    }

    [Test]
    public void SharedLock_BlocksExclusive()
    {
        var lockPath = Path.Combine(m_testDir, "shared_blocks_excl.db");
        using var fileLock1 = new FileLock(lockPath, TimeSpan.FromMilliseconds(100));
        using var fileLock2 = new FileLock(lockPath, TimeSpan.FromMilliseconds(100));
        
        fileLock1.AcquireSharedLock();
        
        Assert.Throws<TimeoutException>(() => fileLock2.AcquireExclusiveLock());
    }

    #endregion

    #region Lock Release Tests

    [Test]
    public void ReleaseLock_AllowsNewLock()
    {
        var lockPath = Path.Combine(m_testDir, "release.db");
        using var fileLock1 = new FileLock(lockPath);
        
        fileLock1.AcquireExclusiveLock();
        fileLock1.ReleaseLock();
        
        Assert.That(fileLock1.HasExclusiveLock, Is.False);
        
        // Should be able to acquire again
        fileLock1.AcquireExclusiveLock();
        Assert.That(fileLock1.HasExclusiveLock, Is.True);
    }

    [Test]
    public void Dispose_ReleasesLock()
    {
        var lockPath = Path.Combine(m_testDir, "dispose_test.db");
        
        {
            using var fileLock = new FileLock(lockPath);
            fileLock.AcquireExclusiveLock();
        }
        
        // New lock should be acquirable
        using var newFileLock = new FileLock(lockPath);
        Assert.DoesNotThrow(() => newFileLock.AcquireExclusiveLock());
    }

    #endregion

    #region Lock File Lifecycle Tests

    [Test]
    public void LockFile_CreatedOnAcquire()
    {
        var lockPath = Path.Combine(m_testDir, "created.db");
        var lockFilePath = lockPath + ".lock";
        
        using var fileLock = new FileLock(lockPath);
        
        Assert.That(File.Exists(lockFilePath), Is.False);
        
        fileLock.AcquireExclusiveLock();
        
        Assert.That(File.Exists(lockFilePath), Is.True);
    }

    #endregion

    #region Timeout and Retry Tests

    [Test]
    public void AcquireLock_RetriesOnContention()
    {
        var lockPath = Path.Combine(m_testDir, "retry.db");
        using var fileLock1 = new FileLock(lockPath);
        
        fileLock1.AcquireExclusiveLock();
        
        // Start releasing in background
        var releaseTask = Task.Run(async () =>
        {
            await Task.Delay(200);
            fileLock1.ReleaseLock();
        });
        
        // This should retry and eventually succeed
        using var fileLock2 = new FileLock(lockPath, TimeSpan.FromSeconds(2));
        Assert.DoesNotThrow(() => fileLock2.AcquireExclusiveLock());
    }

    [Test]
    public void AcquireLock_ThrowsTimeoutException_AfterMaxRetries()
    {
        var lockPath = Path.Combine(m_testDir, "timeout.db");
        using var fileLock1 = new FileLock(lockPath);
        using var fileLock2 = new FileLock(lockPath, TimeSpan.FromMilliseconds(100));
        
        fileLock1.AcquireExclusiveLock();
        
        Assert.Throws<TimeoutException>(() => fileLock2.AcquireExclusiveLock());
    }

    #endregion

    #region Properties Tests

    [Test]
    public void HasExclusiveLock_TrueAfterAcquire()
    {
        var lockPath = Path.Combine(m_testDir, "has_excl.db");
        using var fileLock = new FileLock(lockPath);
        
        Assert.That(fileLock.HasExclusiveLock, Is.False);
        
        fileLock.AcquireExclusiveLock();
        
        Assert.That(fileLock.HasExclusiveLock, Is.True);
    }

    [Test]
    public void HasSharedLock_TrueAfterAcquire()
    {
        var lockPath = Path.Combine(m_testDir, "has_shared.db");
        using var fileLock = new FileLock(lockPath);
        
        Assert.That(fileLock.HasSharedLock, Is.False);
        
        fileLock.AcquireSharedLock();
        
        Assert.That(fileLock.HasSharedLock, Is.True);
    }

    #endregion

    #region Dispose Tests

    [Test]
    public void DoubleDispose_NoThrow()
    {
        var lockPath = Path.Combine(m_testDir, "double_dispose.db");
        var fileLock = new FileLock(lockPath);
        
        Assert.DoesNotThrow(() =>
        {
            fileLock.Dispose();
            fileLock.Dispose();
        });
    }

    [Test]
    public void DisposedLock_ThrowsObjectDisposedException()
    {
        var lockPath = Path.Combine(m_testDir, "disposed.db");
        var fileLock = new FileLock(lockPath);
        fileLock.Dispose();
        
        Assert.Throws<ObjectDisposedException>(() => fileLock.AcquireSharedLock());
        Assert.Throws<ObjectDisposedException>(() => fileLock.AcquireExclusiveLock());
    }

    #endregion
}

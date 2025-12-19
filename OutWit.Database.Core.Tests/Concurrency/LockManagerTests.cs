using OutWit.Database.Core.Concurrency;

namespace OutWit.Database.Core.Tests.Concurrency;

/// <summary>
/// Unit tests for LockManager component.
/// Tests coordination of in-process and file locks.
/// </summary>
[TestFixture]
public class LockManagerTests : IDisposable
{
    private string m_testDir = null!;
    private LockManager m_lockManager = null!;

    [SetUp]
    public void SetUp()
    {
        m_testDir = Path.Combine(Path.GetTempPath(), $"lock_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(m_testDir);
        
        var dbPath = Path.Combine(m_testDir, "test.db");
        m_lockManager = new LockManager(dbPath, TimeSpan.FromSeconds(5));
    }

    [TearDown]
    public void TearDown()
    {
        Dispose();
    }

    public void Dispose()
    {
        m_lockManager?.Dispose();
        try
        {
            if (Directory.Exists(m_testDir))
                Directory.Delete(m_testDir, recursive: true);
        }
        catch { }
    }

    #region Basic Lock Tests

    [Test]
    public void AcquireReadLock_Succeeds()
    {
        using var handle = m_lockManager.AcquireReadLock();
        
        Assert.That(handle, Is.Not.Null);
    }

    [Test]
    public void AcquireWriteLock_Succeeds()
    {
        using var handle = m_lockManager.AcquireWriteLock();
        
        Assert.That(handle, Is.Not.Null);
    }

    [Test]
    public async Task AcquireReadLockAsync_Succeeds()
    {
        await using var handle = await m_lockManager.AcquireReadLockAsync();
        
        Assert.That(handle, Is.Not.Null);
    }

    [Test]
    public async Task AcquireWriteLockAsync_Succeeds()
    {
        await using var handle = await m_lockManager.AcquireWriteLockAsync();
        
        Assert.That(handle, Is.Not.Null);
    }

    #endregion

    #region Multiple Readers Tests

    [Test]
    public void MultipleReaders_AllSucceed()
    {
        var handles = new List<IDisposable>();
        
        for (int i = 0; i < 5; i++)
        {
            handles.Add(m_lockManager.AcquireReadLock());
        }
        
        Assert.That(handles.Count, Is.EqualTo(5));
        
        foreach (var handle in handles)
        {
            handle.Dispose();
        }
    }

    [Test]
    public async Task MultipleReadersAsync_AllSucceed()
    {
        var handles = new List<IAsyncDisposable>();
        
        for (int i = 0; i < 5; i++)
        {
            handles.Add(await m_lockManager.AcquireReadLockAsync());
        }
        
        Assert.That(handles.Count, Is.EqualTo(5));
        
        foreach (var handle in handles)
        {
            await handle.DisposeAsync();
        }
    }

    #endregion

    #region Writer Blocking Tests

    [Test]
    public void WriteLock_BlocksOtherWriters()
    {
        var subDir = Path.Combine(m_testDir, "blocking1");
        Directory.CreateDirectory(subDir);
        var shortTimeoutManager = new LockManager(
            Path.Combine(subDir, "blocking.db"), 
            TimeSpan.FromMilliseconds(200));
        
        try
        {
            using var handle1 = shortTimeoutManager.AcquireWriteLock();
            
            // Another thread trying to acquire should timeout
            var task = Task.Run(() =>
            {
                Assert.Throws<TimeoutException>(() =>
                {
                    using var handle2 = shortTimeoutManager.AcquireWriteLock();
                });
            });
            
            task.Wait(TimeSpan.FromSeconds(1));
        }
        finally
        {
            shortTimeoutManager.Dispose();
        }
    }

    [Test]
    public void WriteLock_BlocksReaders()
    {
        var subDir = Path.Combine(m_testDir, "blocking2");
        Directory.CreateDirectory(subDir);
        var shortTimeoutManager = new LockManager(
            Path.Combine(subDir, "blocking.db"), 
            TimeSpan.FromMilliseconds(200));
        
        try
        {
            using var writeHandle = shortTimeoutManager.AcquireWriteLock();
            
            var task = Task.Run(() =>
            {
                Assert.Throws<TimeoutException>(() =>
                {
                    using var readHandle = shortTimeoutManager.AcquireReadLock();
                });
            });
            
            task.Wait(TimeSpan.FromSeconds(1));
        }
        finally
        {
            shortTimeoutManager.Dispose();
        }
    }

    [Test]
    public void ReadLock_BlocksWriters()
    {
        var subDir = Path.Combine(m_testDir, "blocking3");
        Directory.CreateDirectory(subDir);
        var shortTimeoutManager = new LockManager(
            Path.Combine(subDir, "blocking.db"), 
            TimeSpan.FromMilliseconds(200));
        
        try
        {
            using var readHandle = shortTimeoutManager.AcquireReadLock();
            
            var task = Task.Run(() =>
            {
                Assert.Throws<TimeoutException>(() =>
                {
                    using var writeHandle = shortTimeoutManager.AcquireWriteLock();
                });
            });
            
            task.Wait(TimeSpan.FromSeconds(1));
        }
        finally
        {
            shortTimeoutManager.Dispose();
        }
    }

    #endregion

    #region Lock Release Tests

    [Test]
    public async Task LockRelease_AllowsNextAcquire()
    {
        // Acquire and release
        {
            using var handle = m_lockManager.AcquireWriteLock();
        }
        
        // Should succeed immediately
        await using var newHandle = await m_lockManager.AcquireWriteLockAsync();
        Assert.That(newHandle, Is.Not.Null);
    }

    [Test]
    public async Task ReadLockRelease_AllowsWriter()
    {
        // Multiple readers acquire and release
        for (int i = 0; i < 3; i++)
        {
            using var handle = m_lockManager.AcquireReadLock();
        }
        
        // Writer should succeed
        await using var writeHandle = await m_lockManager.AcquireWriteLockAsync();
        Assert.That(writeHandle, Is.Not.Null);
    }

    #endregion

    #region Concurrent Access Tests

    [Test]
    [Category("Stress")]
    public async Task ConcurrentReaders_NoContention()
    {
        const int readerCount = 20;
        var tasks = new List<Task>();
        var successCount = 0;
        
        for (int i = 0; i < readerCount; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await using var handle = await m_lockManager.AcquireReadLockAsync();
                await Task.Delay(5);
                Interlocked.Increment(ref successCount);
            }));
        }
        
        await Task.WhenAll(tasks);
        
        Assert.That(successCount, Is.EqualTo(readerCount));
    }

    [Test]
    [Category("Stress")]
    public async Task ConcurrentWriters_Serialized()
    {
        const int writerCount = 10;
        var tasks = new List<Task>();
        var counter = 0;
        var concurrentWriters = 0;
        var maxConcurrent = 0;
        
        for (int i = 0; i < writerCount; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await using var handle = await m_lockManager.AcquireWriteLockAsync();
                
                var current = Interlocked.Increment(ref concurrentWriters);
                if (current > maxConcurrent)
                    Interlocked.Exchange(ref maxConcurrent, current);
                
                await Task.Delay(5);
                Interlocked.Increment(ref counter);
                
                Interlocked.Decrement(ref concurrentWriters);
            }));
        }
        
        await Task.WhenAll(tasks);
        
        Assert.That(counter, Is.EqualTo(writerCount));
        Assert.That(maxConcurrent, Is.EqualTo(1), "Only one writer at a time");
    }

    [Test]
    [Category("Stress")]
    [Ignore("Flaky due to file lock timing issues")]
    public async Task MixedOperations_Complete()
    {
        const int operationCount = 20;
        var tasks = new List<Task>();
        var random = new Random(42);
        var completedOps = 0;
        
        for (int i = 0; i < operationCount; i++)
        {
            var isReader = random.Next(3) != 0; // 2/3 readers, 1/3 writers
            
            tasks.Add(Task.Run(async () =>
            {
                if (isReader)
                {
                    await using var handle = await m_lockManager.AcquireReadLockAsync();
                    await Task.Delay(1);
                }
                else
                {
                    await using var handle = await m_lockManager.AcquireWriteLockAsync();
                    await Task.Delay(1);
                }
                Interlocked.Increment(ref completedOps);
            }));
        }
        
        await Task.WhenAll(tasks);
        
        Assert.That(completedOps, Is.EqualTo(operationCount));
    }

    #endregion

    #region Dispose Tests

    [Test]
    public void Dispose_ReleasesResources()
    {
        var manager = new LockManager(Path.Combine(m_testDir, "dispose.db"));
        
        using (var handle = manager.AcquireWriteLock())
        {
            // Hold lock
        }
        
        Assert.DoesNotThrow(() => manager.Dispose());
    }

    [Test]
    public void DoubleDispose_NoThrow()
    {
        var manager = new LockManager(Path.Combine(m_testDir, "double.db"));
        
        Assert.DoesNotThrow(() =>
        {
            manager.Dispose();
            manager.Dispose();
        });
    }

    #endregion

    #region File Lock Integration Tests

    [Test]
    public void UseFileLocking_TrueForPathConstructor()
    {
        var dbPath = Path.Combine(m_testDir, "withfilelock.db");
        var manager = new LockManager(dbPath);
        
        Assert.That(manager.UseFileLocking, Is.True);
        
        manager.Dispose();
    }

    [Test]
    public void UseFileLocking_FalseForNoPathConstructor()
    {
        var manager = new LockManager();
        
        Assert.That(manager.UseFileLocking, Is.False);
        
        manager.Dispose();
    }

    [Test]
    public void InMemoryLockManager_WorksWithoutFileLock()
    {
        var manager = new LockManager(TimeSpan.FromSeconds(1));
        
        using (var handle = manager.AcquireWriteLock())
        {
            Assert.That(handle, Is.Not.Null);
        }
        
        manager.Dispose();
    }

    #endregion

    #region Properties Tests

    [Test]
    public void WaitingReadCount_IsAccessible()
    {
        Assert.That(m_lockManager.WaitingReadCount, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void WaitingWriteCount_IsAccessible()
    {
        Assert.That(m_lockManager.WaitingWriteCount, Is.GreaterThanOrEqualTo(0));
    }

    #endregion
}

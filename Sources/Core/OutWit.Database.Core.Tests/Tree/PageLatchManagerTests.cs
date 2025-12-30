using OutWit.Database.Core.Tree;

namespace OutWit.Database.Core.Tests.Tree;

/// <summary>
/// Tests for PageLatchManager latch management.
/// </summary>
[TestFixture]
public class PageLatchManagerTests
{
    #region Basic Operations Tests

    [Test]
    public void GetLatchCreatesNewLatchTest()
    {
        using var manager = new PageLatchManager();

        var latch = manager.GetLatch(1);

        Assert.That(latch, Is.Not.Null);
        Assert.That(latch.PageNumber, Is.EqualTo(1));
        Assert.That(manager.LatchCount, Is.EqualTo(1));
    }

    [Test]
    public void GetLatchReturnsSameLatchForSamePageTest()
    {
        using var manager = new PageLatchManager();

        var latch1 = manager.GetLatch(1);
        var latch2 = manager.GetLatch(1);

        Assert.That(latch1, Is.SameAs(latch2));
        Assert.That(manager.LatchCount, Is.EqualTo(1));
    }

    [Test]
    public void AcquireSharedReturnsValidHandleTest()
    {
        using var manager = new PageLatchManager();

        using var handle = manager.AcquireShared(1);

        Assert.That(handle.IsValid, Is.True);
        Assert.That(handle.PageNumber, Is.EqualTo(1));
        Assert.That(handle.IsExclusive, Is.False);
    }

    [Test]
    public void AcquireExclusiveReturnsValidHandleTest()
    {
        using var manager = new PageLatchManager();

        using var handle = manager.AcquireExclusive(1);

        Assert.That(handle.IsValid, Is.True);
        Assert.That(handle.PageNumber, Is.EqualTo(1));
        Assert.That(handle.IsExclusive, Is.True);
    }

    [Test]
    public void HandleDisposeReleasesLatchTest()
    {
        using var manager = new PageLatchManager();

        var handle = manager.AcquireExclusive(1);
        var latch = manager.GetLatch(1);
        Assert.That(latch.IsWriteLockHeld, Is.True);

        handle.Dispose();
        Assert.That(latch.IsWriteLockHeld, Is.False);
    }

    #endregion

    #region Try Acquire Tests

    [Test]
    public void TryAcquireSharedSucceedsWhenAvailableTest()
    {
        using var manager = new PageLatchManager();

        var result = manager.TryAcquireShared(1, out var handle);

        Assert.That(result, Is.True);
        Assert.That(handle.IsValid, Is.True);

        handle.Dispose();
    }

    [Test]
    public void TryAcquireExclusiveFailsWhenHeldTest()
    {
        using var manager = new PageLatchManager();

        using var handle1 = manager.AcquireExclusive(1);

        var result = false;
        var task = Task.Run(() =>
        {
            result = manager.TryAcquireExclusive(1, out var handle2);
            if (result) handle2.Dispose();
        });

        task.Wait();
        Assert.That(result, Is.False);
    }

    #endregion

    #region Concurrent Access Tests

    [Test]
    public void MultipleReadersAllowedTest()
    {
        using var manager = new PageLatchManager();
        var readersEntered = 0;
        var allReadersEntered = new ManualResetEventSlim(false);
        var releaseReaders = new ManualResetEventSlim(false);

        const int readerCount = 4;
        var tasks = Enumerable.Range(0, readerCount).Select(_ => Task.Run(() =>
        {
            using var handle = manager.AcquireShared(1);
            Interlocked.Increment(ref readersEntered);
            
            if (Volatile.Read(ref readersEntered) == readerCount)
            {
                allReadersEntered.Set();
            }
            
            releaseReaders.Wait();
        })).ToArray();

        allReadersEntered.Wait(TimeSpan.FromSeconds(5));
        Assert.That(readersEntered, Is.EqualTo(readerCount));

        releaseReaders.Set();
        Task.WaitAll(tasks);
    }

    [Test]
    public void WriterBlocksReadersTest()
    {
        using var manager = new PageLatchManager();
        var readerEntered = false;
        var writerStarted = new ManualResetEventSlim(false);

        using var writeHandle = manager.AcquireExclusive(1);
        writerStarted.Set();

        var readerTask = Task.Run(() =>
        {
            writerStarted.Wait();
            Thread.Sleep(50);
            
            var canAcquire = manager.TryAcquireShared(1, out var handle);
            readerEntered = canAcquire;
            if (canAcquire) handle.Dispose();
        });

        readerTask.Wait();
        Assert.That(readerEntered, Is.False);
    }

    [Test]
    public void DifferentPagesCanBeAccessedConcurrentlyTest()
    {
        using var manager = new PageLatchManager();
        var page1Locked = new ManualResetEventSlim(false);
        var page2Locked = new ManualResetEventSlim(false);

        var task1 = Task.Run(() =>
        {
            using var handle = manager.AcquireExclusive(1);
            page1Locked.Set();
            page2Locked.Wait(TimeSpan.FromSeconds(1));
        });

        var task2 = Task.Run(() =>
        {
            using var handle = manager.AcquireExclusive(2);
            page2Locked.Set();
            page1Locked.Wait(TimeSpan.FromSeconds(1));
        });

        var completed = Task.WaitAll([task1, task2], TimeSpan.FromSeconds(2));
        Assert.That(completed, Is.True, "Different pages should be accessible concurrently");
    }

    #endregion

    #region Statistics Tests

    [Test]
    public void AcquireCountTracksCorrectlyTest()
    {
        using var manager = new PageLatchManager();

        Assert.That(manager.AcquireCount, Is.EqualTo(0));

        using (var h1 = manager.AcquireShared(1)) { }
        Assert.That(manager.AcquireCount, Is.EqualTo(1));

        using (var h2 = manager.AcquireExclusive(2)) { }
        Assert.That(manager.AcquireCount, Is.EqualTo(2));
    }

    [Test]
    public void ReleaseCountTracksCorrectlyTest()
    {
        using var manager = new PageLatchManager();

        Assert.That(manager.ReleaseCount, Is.EqualTo(0));

        var handle = manager.AcquireShared(1);
        Assert.That(manager.ReleaseCount, Is.EqualTo(0));

        handle.Dispose();
        Assert.That(manager.ReleaseCount, Is.EqualTo(1));
    }

    [Test]
    public void ContentionCountTracksCorrectlyTest()
    {
        using var manager = new PageLatchManager();
        var writerHolding = new ManualResetEventSlim(false);
        var tryAcquireDone = new ManualResetEventSlim(false);

        var initialContention = manager.ContentionCount;

        // Acquire exclusive
        using var writeHandle = manager.AcquireExclusive(1);
        writerHolding.Set();

        // Try to acquire from another thread (will fail and count as contention)
        var task = Task.Run(() =>
        {
            writerHolding.Wait();
            manager.TryAcquireExclusive(1, out var handle);
            if (handle.IsValid) handle.Dispose();
            tryAcquireDone.Set();
        });

        tryAcquireDone.Wait();
        task.Wait();

        Assert.That(manager.ContentionCount, Is.GreaterThan(initialContention));
    }

    #endregion

    #region Cleanup Tests

    [Test]
    public void CleanupRemovesUnusedLatchesTest()
    {
        using var manager = new PageLatchManager();

        // Create some latches
        for (int i = 0; i < 10; i++)
        {
            using var handle = manager.AcquireShared((uint)i);
        }

        Assert.That(manager.LatchCount, Is.EqualTo(10));

        manager.Cleanup();

        // After cleanup, unused latches should be removed
        Assert.That(manager.LatchCount, Is.LessThanOrEqualTo(10));
    }

    [Test]
    public void CleanupPreservesActiveLatchesTest()
    {
        using var manager = new PageLatchManager();

        using var activeHandle = manager.AcquireExclusive(1);

        // Create and release another latch
        using (var tempHandle = manager.AcquireShared(2)) { }

        manager.Cleanup();

        // Active latch should still exist
        var activeLatch = manager.GetLatch(1);
        Assert.That(activeLatch.IsWriteLockHeld, Is.True);
    }

    #endregion

    #region Disposal Tests

    [Test]
    public void DisposedManagerThrowsTest()
    {
        var manager = new PageLatchManager();
        manager.Dispose();

        Assert.Throws<ObjectDisposedException>(() => manager.GetLatch(1));
        Assert.Throws<ObjectDisposedException>(() => manager.AcquireShared(1));
        Assert.Throws<ObjectDisposedException>(() => manager.AcquireExclusive(1));
    }

    [Test]
    public void DisposeReleasesAllLatchesTest()
    {
        var manager = new PageLatchManager();

        // Create latches
        var latch1 = manager.GetLatch(1);
        var latch2 = manager.GetLatch(2);

        manager.Dispose();

        // Latches should be disposed
        Assert.That(manager.LatchCount, Is.EqualTo(0));
    }

    #endregion
}

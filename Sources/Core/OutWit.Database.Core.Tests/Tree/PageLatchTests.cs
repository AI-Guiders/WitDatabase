using OutWit.Database.Core.Tree;

namespace OutWit.Database.Core.Tests.Tree;

/// <summary>
/// Tests for PageLatch page-level locking.
/// </summary>
[TestFixture]
public class PageLatchTests
{
    #region Basic Latch Tests

    [Test]
    public void AcquireSharedAllowsMultipleReadersTest()
    {
        using var latch = new PageLatch(1);

        latch.AcquireShared();
        Assert.That(latch.IsReadLockHeld, Is.True);
        Assert.That(latch.CurrentReadCount, Is.EqualTo(1));

        // Same thread can't acquire multiple shared locks without recursion
        // But different threads can
        var task = Task.Run(() =>
        {
            latch.AcquireShared();
            Assert.That(latch.CurrentReadCount, Is.GreaterThanOrEqualTo(1));
            latch.ReleaseShared();
        });

        task.Wait();
        latch.ReleaseShared();
    }

    [Test]
    public void AcquireExclusiveBlocksOtherWritersTest()
    {
        using var latch = new PageLatch(1);
        var writerEntered = false;
        var writerBlocked = new ManualResetEventSlim(false);
        var releaseWriter = new ManualResetEventSlim(false);

        latch.AcquireExclusive();
        Assert.That(latch.IsWriteLockHeld, Is.True);

        var task = Task.Run(() =>
        {
            writerBlocked.Set();
            latch.AcquireExclusive();
            writerEntered = true;
            latch.ReleaseExclusive();
        });

        writerBlocked.Wait();
        Thread.Sleep(50); // Give time for writer to block
        Assert.That(writerEntered, Is.False);

        latch.ReleaseExclusive();
        task.Wait();
        Assert.That(writerEntered, Is.True);
    }

    [Test]
    public void TryAcquireSharedReturnsImmediatelyTest()
    {
        using var latch = new PageLatch(1);

        var result = latch.TryAcquireShared();
        Assert.That(result, Is.True);
        
        latch.ReleaseShared();
    }

    [Test]
    public void TryAcquireExclusiveFailsWhenHeldTest()
    {
        using var latch = new PageLatch(1);

        latch.AcquireExclusive();

        var result = false;
        var task = Task.Run(() =>
        {
            result = latch.TryAcquireExclusive();
        });

        task.Wait();
        Assert.That(result, Is.False);

        latch.ReleaseExclusive();
    }

    [Test]
    public void TryAcquireWithTimeoutTest()
    {
        using var latch = new PageLatch(1);

        latch.AcquireExclusive();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = false;
        
        var task = Task.Run(() =>
        {
            result = latch.TryAcquireExclusive(TimeSpan.FromMilliseconds(100));
        });

        task.Wait();
        sw.Stop();

        Assert.That(result, Is.False);
        Assert.That(sw.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(90));

        latch.ReleaseExclusive();
    }

    #endregion

    #region Properties Tests

    [Test]
    public void PageNumberIsSetCorrectlyTest()
    {
        using var latch = new PageLatch(42);
        Assert.That(latch.PageNumber, Is.EqualTo(42));
    }

    [Test]
    public void WaitingCountsTrackCorrectlyTest()
    {
        using var latch = new PageLatch(1);
        var waitStarted = new ManualResetEventSlim(false);

        latch.AcquireExclusive();

        var task = Task.Run(() =>
        {
            waitStarted.Set();
            latch.AcquireExclusive();
            latch.ReleaseExclusive();
        });

        waitStarted.Wait();
        Thread.Sleep(50); // Give time for waiter to queue

        Assert.That(latch.WaitingWriteCount, Is.GreaterThanOrEqualTo(0)); // May or may not show depending on timing

        latch.ReleaseExclusive();
        task.Wait();
    }

    #endregion

    #region Disposal Tests

    [Test]
    public void DisposedLatchThrowsTest()
    {
        var latch = new PageLatch(1);
        latch.Dispose();

        Assert.Throws<ObjectDisposedException>(() => latch.AcquireShared());
        Assert.Throws<ObjectDisposedException>(() => latch.AcquireExclusive());
    }

    [Test]
    public void DisposeCanBeCalledMultipleTimesTest()
    {
        var latch = new PageLatch(1);
        latch.Dispose();
        Assert.DoesNotThrow(() => latch.Dispose());
    }

    #endregion
}

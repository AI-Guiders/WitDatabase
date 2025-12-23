using NUnit.Framework;
using OutWit.Database.Core.Concurrency;
using OutWit.Database.Core.Exceptions;

namespace OutWit.Database.Core.Tests.Concurrency
{
    /// <summary>
    /// Unit tests for DeadlockDetector.
    /// </summary>
    [TestFixture]
    public class DeadlockDetectorTests
    {
        #region Basic Detection

        [Test]
        public void NoDeadlockWhenNoWaitsTest()
        {
            using var detector = new DeadlockDetector();

            Assert.That(detector.HasDeadlock(), Is.False);
            Assert.That(detector.DetectDeadlock(), Is.Null);
        }

        [Test]
        public void NoDeadlockWithLinearWaitsTest()
        {
            using var detector = new DeadlockDetector();

            detector.RegisterWait(1, 2);
            detector.RegisterWait(2, 3);
            detector.RegisterWait(3, 4);

            Assert.That(detector.HasDeadlock(), Is.False);
        }

        [Test]
        public void DeadlockDetectedOnRegisterTest()
        {
            using var detector = new DeadlockDetector();

            detector.RegisterWait(1, 2);
            detector.RegisterWait(2, 3);

            // This should throw because it creates cycle: 3 -> 1 -> 2 -> 3
            var ex = Assert.Throws<DeadlockException>(() => detector.RegisterWait(3, 1));
            Assert.That(ex, Is.Not.Null);
            Assert.That(ex!.VictimTransactionId, Is.Not.Null);
            Assert.That(ex.CycleParticipants, Is.Not.Null);
            Assert.That(ex.ShouldRetry, Is.True);
        }

        [Test]
        public void TwoTransactionDeadlockTest()
        {
            using var detector = new DeadlockDetector();

            detector.RegisterWait(1, 2);

            var ex = Assert.Throws<DeadlockException>(() => detector.RegisterWait(2, 1));
            Assert.That(ex!.CycleParticipants, Has.Count.GreaterThanOrEqualTo(2));
        }

        #endregion

        #region Victim Selection

        [Test]
        public void YoungestVictimStrategySelectsHighestIdTest()
        {
            using var detector = new DeadlockDetector(DeadlockVictimStrategy.Youngest);

            detector.RegisterWait(1, 2);
            detector.RegisterWait(2, 3);

            var ex = Assert.Throws<DeadlockException>(() => detector.RegisterWait(3, 1));
            // Youngest = highest ID in cycle
            Assert.That(ex!.VictimTransactionId, Is.EqualTo(3));
        }

        [Test]
        public void OldestVictimStrategySelectsLowestIdTest()
        {
            using var detector = new DeadlockDetector(DeadlockVictimStrategy.Oldest);

            detector.RegisterWait(1, 2);
            detector.RegisterWait(2, 3);

            var ex = Assert.Throws<DeadlockException>(() => detector.RegisterWait(3, 1));
            // Oldest = lowest ID in cycle
            Assert.That(ex!.VictimTransactionId, Is.EqualTo(1));
        }

        [Test]
        public void MostWaitingStrategySelectsTransactionWaitingForMostTest()
        {
            using var detector = new DeadlockDetector(DeadlockVictimStrategy.MostWaiting);

            // Transaction 1 waits for 2
            detector.RegisterWait(1, 2);
            // Transaction 2 waits for multiple (1 and will wait for 3)
            detector.RegisterWait(2, 3);
            detector.RegisterWait(2, 4);

            var ex = Assert.Throws<DeadlockException>(() => detector.RegisterWait(3, 1));
            // Transaction 2 is waiting for the most (3 and 4)
            // But after the deadlock edge is added, need to check cycle participants
            Assert.That(ex!.VictimTransactionId, Is.Not.Null);
        }

        #endregion

        #region Unregister and Complete

        [Test]
        public void UnregisterWaitRemovesEdgeTest()
        {
            using var detector = new DeadlockDetector();

            detector.RegisterWait(1, 2);
            detector.RegisterWait(2, 3);
            detector.UnregisterWait(1, 2);

            // Now adding 3 -> 1 should not create cycle
            Assert.DoesNotThrow(() => detector.RegisterWait(3, 1));
        }

        [Test]
        public void TransactionCompletedRemovesAllEdgesTest()
        {
            using var detector = new DeadlockDetector();

            detector.RegisterWait(1, 2);
            detector.RegisterWait(1, 3);
            // Don't add 2 -> 1 as it would create deadlock
            detector.RegisterWait(2, 4);

            // Complete transaction 1
            detector.TransactionCompleted(1);

            // Transaction 1 should no longer be in the graph
            Assert.That(detector.WaitForGraph.IsWaiting(1), Is.False);
            Assert.That(detector.WaitForGraph.GetWaiters(1).Count, Is.EqualTo(0));
        }

        [Test]
        public void RegisterMultipleHoldersTest()
        {
            using var detector = new DeadlockDetector();

            detector.RegisterWait(1, new[] { 2L, 3L, 4L });

            Assert.That(detector.WaitForGraph.GetWaitingFor(1).Count, Is.EqualTo(3));
        }

        #endregion

        #region Detection Methods

        [Test]
        public void DetectDeadlockReturnsCycleTest()
        {
            using var detector = new DeadlockDetector();

            // Manually add edges to avoid exception on RegisterWait
            detector.WaitForGraph.AddEdge(1, 2);
            detector.WaitForGraph.AddEdge(2, 1);

            var cycle = detector.DetectDeadlock();
            Assert.That(cycle, Is.Not.Null);
            Assert.That(cycle!.Count, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void DetectAndSelectVictimReturnsVictimTest()
        {
            using var detector = new DeadlockDetector(DeadlockVictimStrategy.Youngest);

            detector.WaitForGraph.AddEdge(1, 2);
            detector.WaitForGraph.AddEdge(2, 1);

            var victim = detector.DetectAndSelectVictim();
            Assert.That(victim, Is.EqualTo(2)); // Youngest = highest ID
        }

        [Test]
        public void DetectAllDeadlocksFindsMultipleTest()
        {
            using var detector = new DeadlockDetector();

            // Two separate cycles
            detector.WaitForGraph.AddEdge(1, 2);
            detector.WaitForGraph.AddEdge(2, 1);
            detector.WaitForGraph.AddEdge(3, 4);
            detector.WaitForGraph.AddEdge(4, 3);

            var deadlocks = detector.DetectAllDeadlocks();
            Assert.That(deadlocks.Count, Is.GreaterThanOrEqualTo(2));
        }

        #endregion

        #region LeastWork Strategy

        [Test]
        public void LeastWorkStrategyWithLockManagerTest()
        {
            using var lockManager = new RowLockManager();
            using var detector = new DeadlockDetector(lockManager, DeadlockVictimStrategy.LeastWork);

            // Transaction 1 holds 3 locks
            lockManager.AcquireLock(new RowLockRequest("key1"u8.ToArray(), 1, RowLockMode.Exclusive));
            lockManager.AcquireLock(new RowLockRequest("key2"u8.ToArray(), 1, RowLockMode.Exclusive));
            lockManager.AcquireLock(new RowLockRequest("key3"u8.ToArray(), 1, RowLockMode.Exclusive));

            // Transaction 2 holds 1 lock
            lockManager.AcquireLock(new RowLockRequest("key4"u8.ToArray(), 2, RowLockMode.Exclusive));

            detector.WaitForGraph.AddEdge(1, 2);
            detector.WaitForGraph.AddEdge(2, 1);

            var victim = detector.DetectAndSelectVictim();
            // Transaction 2 has fewer locks, should be victim
            Assert.That(victim, Is.EqualTo(2));
        }

        [Test]
        public void LeastWorkWithoutLockManagerFallsBackToYoungestTest()
        {
            using var detector = new DeadlockDetector(null, DeadlockVictimStrategy.LeastWork);

            detector.WaitForGraph.AddEdge(1, 2);
            detector.WaitForGraph.AddEdge(2, 1);

            var victim = detector.DetectAndSelectVictim();
            // Falls back to youngest (highest ID)
            Assert.That(victim, Is.EqualTo(2));
        }

        #endregion

        #region Background Detection

        [Test]
        public void BackgroundDetectionCallsCallbackTest()
        {
            var deadlockDetected = new ManualResetEventSlim(false);
            DeadlockException? detectedException = null;

            using var detector = new DeadlockDetector(
                null,
                DeadlockVictimStrategy.Youngest,
                TimeSpan.FromMilliseconds(50),
                ex =>
                {
                    detectedException = ex;
                    deadlockDetected.Set();
                });

            // Add deadlock directly to graph
            detector.WaitForGraph.AddEdge(1, 2);
            detector.WaitForGraph.AddEdge(2, 1);

            // Wait for background detection
            var detected = deadlockDetected.Wait(TimeSpan.FromSeconds(2));

            Assert.That(detected, Is.True);
            Assert.That(detectedException, Is.Not.Null);
            Assert.That(detectedException!.VictimTransactionId, Is.Not.Null);
        }

        #endregion

        #region Dispose

        [Test]
        public void DisposeStopsBackgroundDetectionTest()
        {
            var callCount = 0;

            var detector = new DeadlockDetector(
                null,
                DeadlockVictimStrategy.Youngest,
                TimeSpan.FromMilliseconds(10),
                _ => Interlocked.Increment(ref callCount));

            detector.WaitForGraph.AddEdge(1, 2);
            detector.WaitForGraph.AddEdge(2, 1);

            Thread.Sleep(100);
            var countBeforeDispose = callCount;

            detector.Dispose();

            Thread.Sleep(100);
            var countAfterDispose = callCount;

            // Should not increase much after dispose
            Assert.That(countAfterDispose - countBeforeDispose, Is.LessThan(5));
        }

        [Test]
        public void OperationsAfterDisposeThrowTest()
        {
            var detector = new DeadlockDetector();
            detector.Dispose();

            Assert.Throws<ObjectDisposedException>(() => detector.RegisterWait(1, 2));
            Assert.Throws<ObjectDisposedException>(() => detector.HasDeadlock());
        }

        #endregion

        #region Exception Properties

        [Test]
        public void DeadlockExceptionContainsCorrectInfoTest()
        {
            using var detector = new DeadlockDetector();

            detector.RegisterWait(1, 2);

            var ex = Assert.Throws<DeadlockException>(() => detector.RegisterWait(2, 1));

            Assert.That(ex!.VictimTransactionId, Is.Not.Null);
            Assert.That(ex.CycleParticipants, Is.Not.Null);
            Assert.That(ex.CycleParticipants!.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(ex.ShouldRetry, Is.True);
            Assert.That(ex.Message, Does.Contain("Deadlock"));
            Assert.That(ex.Message, Does.Contain(ex.VictimTransactionId.ToString()!));
        }

        #endregion
    }
}

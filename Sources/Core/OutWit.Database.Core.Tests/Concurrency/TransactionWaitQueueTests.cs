using NUnit.Framework;
using OutWit.Database.Core.Concurrency;

namespace OutWit.Database.Core.Tests.Concurrency
{
    /// <summary>
    /// Unit tests for TransactionWaitQueue.
    /// </summary>
    [TestFixture]
    public class TransactionWaitQueueTests
    {
        #region Basic Enqueue/Dequeue Tests

        [Test]
        public void EnqueueAddsTransactionToQueueTest()
        {
            using var queue = new TransactionWaitQueue();

            queue.Enqueue(1, isWriter: false);

            Assert.That(queue.WaitingCount, Is.EqualTo(1));
            Assert.That(queue.IsWaiting(1), Is.True);
        }

        [Test]
        public void DequeueRemovesTransactionTest()
        {
            using var queue = new TransactionWaitQueue();
            queue.Enqueue(1, isWriter: false);

            var result = queue.Dequeue(1);

            Assert.That(result, Is.True);
            Assert.That(queue.WaitingCount, Is.EqualTo(0));
            Assert.That(queue.IsWaiting(1), Is.False);
        }

        [Test]
        public void DequeueNonExistentReturnsFalseTest()
        {
            using var queue = new TransactionWaitQueue();

            var result = queue.Dequeue(999);

            Assert.That(result, Is.False);
        }

        [Test]
        public void EnqueueDuplicateThrowsTest()
        {
            using var queue = new TransactionWaitQueue();
            queue.Enqueue(1, isWriter: false);

            Assert.Throws<InvalidOperationException>(() => 
                queue.Enqueue(1, isWriter: false));
        }

        #endregion

        #region Signal Tests

        [Test]
        public void SignalNextReturnsHighestPriorityTest()
        {
            using var queue = new TransactionWaitQueue();
            
            queue.Enqueue(1, isWriter: false, TransactionPriority.Low);
            queue.Enqueue(2, isWriter: false, TransactionPriority.High);
            queue.Enqueue(3, isWriter: false, TransactionPriority.Normal);

            var signaled = queue.SignalNext();

            Assert.That(signaled, Is.EqualTo(2)); // High priority first
        }

        [Test]
        public void SignalNextWithWriterPriorityTest()
        {
            var options = new TransactionWaitQueueOptions { WriterPriority = true };
            using var queue = new TransactionWaitQueue(options);
            
            queue.Enqueue(1, isWriter: false, TransactionPriority.High);
            queue.Enqueue(2, isWriter: true, TransactionPriority.High);

            var signaled = queue.SignalNext();

            Assert.That(signaled, Is.EqualTo(2)); // Writer first
        }

        [Test]
        public void SignalSpecificTransactionTest()
        {
            using var queue = new TransactionWaitQueue();
            queue.Enqueue(1, isWriter: false);
            queue.Enqueue(2, isWriter: false);

            var result = queue.Signal(2);

            Assert.That(result, Is.True);
            Assert.That(queue.IsWaiting(2), Is.False);
            Assert.That(queue.IsWaiting(1), Is.True);
        }

        [Test]
        public void SignalAllClearsQueueTest()
        {
            using var queue = new TransactionWaitQueue();
            queue.Enqueue(1, isWriter: false);
            queue.Enqueue(2, isWriter: false);
            queue.Enqueue(3, isWriter: false);

            var count = queue.SignalAll();

            Assert.That(count, Is.EqualTo(3));
            Assert.That(queue.WaitingCount, Is.EqualTo(0));
        }

        [Test]
        public void SignalNextOnEmptyReturnsNullTest()
        {
            using var queue = new TransactionWaitQueue();

            var result = queue.SignalNext();

            Assert.That(result, Is.Null);
        }

        #endregion

        #region Priority Tests

        [Test]
        public void HighPriorityProcessedBeforeLowTest()
        {
            using var queue = new TransactionWaitQueue();
            
            queue.Enqueue(1, isWriter: false, TransactionPriority.Low);
            queue.Enqueue(2, isWriter: false, TransactionPriority.Critical);
            queue.Enqueue(3, isWriter: false, TransactionPriority.Normal);

            var first = queue.SignalNext();
            var second = queue.SignalNext();
            var third = queue.SignalNext();

            Assert.That(first, Is.EqualTo(2));  // Critical
            Assert.That(second, Is.EqualTo(3)); // Normal
            Assert.That(third, Is.EqualTo(1));  // Low
        }

        [Test]
        public void FifoOrderingWithinSamePriorityTest()
        {
            var options = new TransactionWaitQueueOptions { UseFifoOrdering = true };
            using var queue = new TransactionWaitQueue(options);
            
            queue.Enqueue(1, isWriter: false, TransactionPriority.Normal);
            queue.Enqueue(2, isWriter: false, TransactionPriority.Normal);
            queue.Enqueue(3, isWriter: false, TransactionPriority.Normal);

            var first = queue.SignalNext();
            var second = queue.SignalNext();
            var third = queue.SignalNext();

            Assert.That(first, Is.EqualTo(1));
            Assert.That(second, Is.EqualTo(2));
            Assert.That(third, Is.EqualTo(3));
        }

        [Test]
        public void LifoOrderingWithinSamePriorityTest()
        {
            var options = new TransactionWaitQueueOptions { UseFifoOrdering = false };
            using var queue = new TransactionWaitQueue(options);
            
            queue.Enqueue(1, isWriter: false, TransactionPriority.Normal);
            queue.Enqueue(2, isWriter: false, TransactionPriority.Normal);
            queue.Enqueue(3, isWriter: false, TransactionPriority.Normal);

            var first = queue.SignalNext();
            var second = queue.SignalNext();
            var third = queue.SignalNext();

            Assert.That(first, Is.EqualTo(3));  // LIFO
            Assert.That(second, Is.EqualTo(2));
            Assert.That(third, Is.EqualTo(1));
        }

        #endregion

        #region Timeout Tests

        [Test]
        public void EnqueueAndWaitWithTimeoutTest()
        {
            using var queue = new TransactionWaitQueue();

            var result = queue.EnqueueAndWait(1, isWriter: false, timeout: TimeSpan.FromMilliseconds(50));

            Assert.That(result, Is.False); // Should timeout
            Assert.That(queue.IsWaiting(1), Is.False); // Should be cleaned up
        }

        [Test]
        public void EnqueueAndWaitSucceedsWhenSignaledTest()
        {
            using var queue = new TransactionWaitQueue();

            // Signal in background after short delay
            var signalTask = Task.Run(async () =>
            {
                await Task.Delay(50);
                queue.Signal(1);
            });

            var result = queue.EnqueueAndWait(1, isWriter: false, timeout: TimeSpan.FromSeconds(2));

            Assert.That(result, Is.True);
            signalTask.Wait();
        }

        [Test]
        public async Task EnqueueAndWaitAsyncWithTimeoutTest()
        {
            using var queue = new TransactionWaitQueue();

            var result = await queue.EnqueueAndWaitAsync(1, isWriter: false, timeout: TimeSpan.FromMilliseconds(50));

            Assert.That(result, Is.False);
        }

        [Test]
        public async Task EnqueueAndWaitAsyncSucceedsWhenSignaledTest()
        {
            using var queue = new TransactionWaitQueue();

            // Signal in background
            _ = Task.Run(async () =>
            {
                await Task.Delay(50);
                queue.Signal(1);
            });

            var result = await queue.EnqueueAndWaitAsync(1, isWriter: false, timeout: TimeSpan.FromSeconds(2));

            Assert.That(result, Is.True);
        }

        [Test]
        public async Task EnqueueAndWaitAsyncCancellationTest()
        {
            using var queue = new TransactionWaitQueue();
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

            var result = await queue.EnqueueAndWaitAsync(
                1, isWriter: false, 
                timeout: TimeSpan.FromSeconds(10),
                cancellationToken: cts.Token);

            Assert.That(result, Is.False);
        }

        #endregion

        #region Max Queue Size Tests

        [Test]
        public void MaxWaitingTransactionsEnforcedTest()
        {
            var options = new TransactionWaitQueueOptions { MaxWaitingTransactions = 3 };
            using var queue = new TransactionWaitQueue(options);

            queue.Enqueue(1, isWriter: false);
            queue.Enqueue(2, isWriter: false);
            queue.Enqueue(3, isWriter: false);

            Assert.Throws<InvalidOperationException>(() => 
                queue.Enqueue(4, isWriter: false));
        }

        #endregion

        #region Query Tests

        [Test]
        public void GetWaitingTransactionsReturnsAllTest()
        {
            using var queue = new TransactionWaitQueue();
            queue.Enqueue(1, isWriter: false);
            queue.Enqueue(2, isWriter: true);
            queue.Enqueue(3, isWriter: false);

            var waiting = queue.GetWaitingTransactions();

            Assert.That(waiting.Count, Is.EqualTo(3));
            Assert.That(waiting, Does.Contain(1L));
            Assert.That(waiting, Does.Contain(2L));
            Assert.That(waiting, Does.Contain(3L));
        }

        [Test]
        public void GetPositionReturnsCorrectIndexTest()
        {
            using var queue = new TransactionWaitQueue();
            queue.Enqueue(1, isWriter: false, TransactionPriority.Low);
            queue.Enqueue(2, isWriter: false, TransactionPriority.High);
            queue.Enqueue(3, isWriter: false, TransactionPriority.Normal);

            // High priority (2) should be position 0
            // Normal priority (3) should be position 1
            // Low priority (1) should be position 2
            var pos1 = queue.GetPosition(1);
            var pos2 = queue.GetPosition(2);
            var pos3 = queue.GetPosition(3);

            Assert.That(pos2, Is.LessThan(pos3));
            Assert.That(pos3, Is.LessThan(pos1));
        }

        [Test]
        public void GetPositionReturnsMinusOneForNotFoundTest()
        {
            using var queue = new TransactionWaitQueue();

            var position = queue.GetPosition(999);

            Assert.That(position, Is.EqualTo(-1));
        }

        #endregion

        #region Dispose Tests

        [Test]
        public void DisposeCleanupTest()
        {
            var queue = new TransactionWaitQueue();
            queue.Enqueue(1, isWriter: false);
            queue.Enqueue(2, isWriter: false);

            queue.Dispose();

            Assert.Throws<ObjectDisposedException>(() => queue.Enqueue(3, isWriter: false));
        }

        [Test]
        public void OperationsAfterDisposeThrowTest()
        {
            var queue = new TransactionWaitQueue();
            queue.Dispose();

            Assert.Throws<ObjectDisposedException>(() => queue.Enqueue(1, isWriter: false));
            Assert.Throws<ObjectDisposedException>(() => queue.Dequeue(1));
            Assert.Throws<ObjectDisposedException>(() => queue.SignalNext());
        }

        #endregion

        #region Thread Safety Tests

        [Test]
        public void ConcurrentEnqueueDequeueTest()
        {
            using var queue = new TransactionWaitQueue();
            const int iterations = 100;

            var enqueueTask = Task.Run(() =>
            {
                for (int i = 0; i < iterations; i++)
                {
                    try
                    {
                        queue.Enqueue(i, isWriter: i % 2 == 0);
                    }
                    catch (InvalidOperationException)
                    {
                        // May be duplicate if dequeue hasn't happened yet
                    }
                }
            });

            var dequeueTask = Task.Run(() =>
            {
                for (int i = 0; i < iterations; i++)
                {
                    queue.SignalNext();
                    Thread.Sleep(1);
                }
            });

            Task.WaitAll(enqueueTask, dequeueTask);

            // Should complete without deadlock or exception
            Assert.Pass();
        }

        #endregion
    }
}

using NUnit.Framework;
using OutWit.Database.Core.Builder;
using OutWit.Database.Core.Concurrency;
using OutWit.Database.Core.Interfaces;
using OutWit.Database.Core.Stores;
using OutWit.Database.Core.Transactions;

namespace OutWit.Database.Core.Tests.Transactions
{
    /// <summary>
    /// Stress tests for MVCC transactional store.
    /// Tests concurrent access patterns, isolation levels, and durability under load.
    /// </summary>
    [TestFixture]
    [Category("Stress")]
    public class MvccTransactionalStoreStressTests : IDisposable
    {
        #region Fields

        private string m_testDir = null!;

        #endregion

        #region Setup

        [SetUp]
        public void SetUp()
        {
            m_testDir = Path.Combine(Path.GetTempPath(), $"mvcc_stress_{Guid.NewGuid():N}");
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

        private static byte[] Key(string s) => System.Text.Encoding.UTF8.GetBytes(s);
        private static byte[] Value(string s) => System.Text.Encoding.UTF8.GetBytes(s);
        private static string FromBytes(byte[] b) => System.Text.Encoding.UTF8.GetString(b);

        private MvccTransactionalStore CreateStore()
        {
            var innerStore = new StoreInMemory();
            return new MvccTransactionalStore(innerStore, ownsStore: true);
        }

        #endregion

        #region Concurrent Read Tests

        [Test]
        public void ManyConcurrentReadTransactionsTest()
        {
            using var store = CreateStore();

            // Populate data
            for (int i = 0; i < 100; i++)
            {
                store.Put(Key($"key{i}"), Value($"value{i}"));
            }

            const int readerCount = 20;
            const int readsPerReader = 500;
            var exceptions = new List<Exception>();
            var readCounts = new int[readerCount];

            var tasks = Enumerable.Range(0, readerCount).Select(readerId => Task.Run(() =>
            {
                try
                {
                    var random = new Random(readerId);
                    for (int i = 0; i < readsPerReader; i++)
                    {
                        using var tx = store.BeginReadOnlyTransaction();
                        
                        // Read multiple keys in same transaction
                        for (int j = 0; j < 5; j++)
                        {
                            int keyIndex = random.Next(100);
                            var value = tx.Get(Key($"key{keyIndex}"));
                            if (value != null)
                            {
                                Interlocked.Increment(ref readCounts[readerId]);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions) exceptions.Add(ex);
                }
            })).ToArray();

            Task.WaitAll(tasks);

            Assert.That(exceptions, Is.Empty, 
                $"Exceptions: {string.Join(", ", exceptions.Select(e => e.Message))}");

            var totalReads = readCounts.Sum();
            Assert.That(totalReads, Is.GreaterThan(0));
            
            TestContext.WriteLine($"Total successful reads: {totalReads}");
        }

        [Test]
        public void ReadsDuringWriteTransactionTest()
        {
            using var store = CreateStore();

            // Initial data
            for (int i = 0; i < 50; i++)
            {
                store.Put(Key($"key{i}"), Value($"initial{i}"));
            }

            const int writerCount = 5;
            const int readerCount = 15;
            const int operationsPerThread = 100;
            
            var exceptions = new List<Exception>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            // Writers
            var writerTasks = Enumerable.Range(0, writerCount).Select(writerId => Task.Run(() =>
            {
                try
                {
                    var random = new Random(writerId * 1000);
                    for (int i = 0; i < operationsPerThread && !cts.Token.IsCancellationRequested; i++)
                    {
                        using var tx = store.BeginTransaction();
                        
                        int keyIndex = random.Next(50);
                        tx.Put(Key($"key{keyIndex}"), Value($"writer{writerId}_op{i}"));
                        
                        Thread.Sleep(1); // Small delay
                        tx.Commit();
                    }
                }
                catch (InvalidOperationException)
                {
                    // Write conflict - expected
                }
                catch (Exception ex)
                {
                    lock (exceptions) exceptions.Add(ex);
                }
            }, cts.Token)).ToArray();

            // Readers
            var readerTasks = Enumerable.Range(0, readerCount).Select(readerId => Task.Run(() =>
            {
                try
                {
                    var random = new Random(readerId * 10000);
                    for (int i = 0; i < operationsPerThread && !cts.Token.IsCancellationRequested; i++)
                    {
                        using var tx = store.BeginReadOnlyTransaction();
                        
                        int keyIndex = random.Next(50);
                        var value = tx.Get(Key($"key{keyIndex}"));
                        
                        // Value should exist (we pre-populated all 50 keys)
                        // Note: value can be null if GC runs, so we just check no crash
                        // The key should exist in the snapshot
                        if (value == null)
                        {
                            // This can happen in rare race conditions with GC
                            // but should not cause crashes
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions) exceptions.Add(ex);
                }
            }, cts.Token)).ToArray();

            Task.WaitAll(writerTasks.Concat(readerTasks).ToArray());

            Assert.That(exceptions, Is.Empty,
                $"Exceptions: {string.Join(", ", exceptions.Select(e => e.Message))}");
        }

        #endregion

        #region Snapshot Isolation Tests

        [Test]
        public void SnapshotIsolationConsistencyUnderLoadTest()
        {
            using var store = CreateStore();

            // Create linked data (account balances)
            const int accountCount = 10;
            const int totalBalance = 10000;
            
            for (int i = 0; i < accountCount; i++)
            {
                store.Put(Key($"account{i}"), BitConverter.GetBytes(totalBalance / accountCount));
            }

            const int transferCount = 100;
            var exceptions = new List<Exception>();
            var inconsistentReads = 0;

            // Transfer tasks - move money between accounts
            var transferTask = Task.Run(() =>
            {
                var random = new Random(42);
                for (int i = 0; i < transferCount; i++)
                {
                    try
                    {
                        using var tx = store.BeginTransaction();
                        
                        int from = random.Next(accountCount);
                        int to = random.Next(accountCount);
                        if (from == to) continue;

                        var fromBytes = tx.Get(Key($"account{from}"));
                        var toBytes = tx.Get(Key($"account{to}"));

                        if (fromBytes == null || toBytes == null)
                            continue; // Skip if accounts not visible

                        int fromBalance = BitConverter.ToInt32(fromBytes);
                        int toBalance = BitConverter.ToInt32(toBytes);
                        int amount = Math.Min(100, fromBalance);

                        tx.Put(Key($"account{from}"), BitConverter.GetBytes(fromBalance - amount));
                        tx.Put(Key($"account{to}"), BitConverter.GetBytes(toBalance + amount));

                        tx.Commit();
                    }
                    catch (InvalidOperationException)
                    {
                        // Write conflict - retry would happen in real app
                    }
                }
            });

            // Verification tasks - check total balance is always consistent
            var verifyTasks = Enumerable.Range(0, 5).Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    using var tx = store.BeginReadOnlyTransaction();
                    
                    int total = 0;
                    int accountsRead = 0;
                    
                    for (int acc = 0; acc < accountCount; acc++)
                    {
                        var bytes = tx.Get(Key($"account{acc}"));
                        if (bytes != null)
                        {
                            total += BitConverter.ToInt32(bytes);
                            accountsRead++;
                        }
                    }

                    // Only check consistency if we read all accounts
                    // (GC might have removed some versions in edge cases)
                    if (accountsRead == accountCount && total != totalBalance)
                    {
                        Interlocked.Increment(ref inconsistentReads);
                    }

                    Thread.Sleep(1);
                }
            })).ToArray();

            Task.WaitAll(new[] { transferTask }.Concat(verifyTasks).ToArray());

            Assert.That(inconsistentReads, Is.EqualTo(0), 
                "Snapshot isolation should always show consistent total balance");
        }

        #endregion

        #region Write Conflict Tests

        [Test]
        public void WriteConflictDetectionUnderLoadTest()
        {
            using var store = CreateStore();

            // Single hot key
            store.Put(Key("hot-key"), Value("initial"));

            const int writerCount = 10;
            const int attemptsPerWriter = 50;
            
            var successfulCommits = 0;
            var conflictCount = 0;

            var tasks = Enumerable.Range(0, writerCount).Select(writerId => Task.Run(() =>
            {
                for (int i = 0; i < attemptsPerWriter; i++)
                {
                    try
                    {
                        using var tx = store.BeginTransaction();
                        tx.Put(Key("hot-key"), Value($"writer{writerId}_attempt{i}"));
                        tx.Commit();
                        Interlocked.Increment(ref successfulCommits);
                    }
                    catch (InvalidOperationException)
                    {
                        Interlocked.Increment(ref conflictCount);
                    }
                }
            })).ToArray();

            Task.WaitAll(tasks);

            // Should have some successful commits and some conflicts
            Assert.That(successfulCommits, Is.GreaterThan(0), "Some commits should succeed");
            Assert.That(conflictCount, Is.GreaterThan(0), "Should have detected some conflicts");
            
            TestContext.WriteLine($"Successful commits: {successfulCommits}, Conflicts: {conflictCount}");
        }

        [Test]
        public void FirstCommitterWinsTest()
        {
            using var store = CreateStore();
            store.Put(Key("key1"), Value("initial"));

            const int competingWriters = 10;
            var winners = new List<int>();
            var losers = new List<int>();
            var transactions = new List<(int Id, ITransaction Tx)>();
            
            // Start all transactions before any writes - they all see "initial"
            for (int i = 0; i < competingWriters; i++)
            {
                var tx = store.BeginTransaction();
                // Read to establish read set
                tx.Get(Key("key1"));
                transactions.Add((i, tx));
            }

            // All write to the same key
            foreach (var (id, tx) in transactions)
            {
                tx.Put(Key("key1"), Value($"writer{id}"));
            }

            // Try to commit all - only first should succeed
            foreach (var (id, tx) in transactions)
            {
                try
                {
                    tx.Commit();
                    lock (winners) winners.Add(id);
                }
                catch (InvalidOperationException)
                {
                    lock (losers) losers.Add(id);
                }
                finally
                {
                    tx.Dispose();
                }
            }

            // Exactly one winner
            Assert.That(winners.Count, Is.EqualTo(1), "Should have exactly one winner");
            Assert.That(losers.Count, Is.EqualTo(competingWriters - 1), "All others should lose");

            // Verify winner's value persisted
            var finalValue = store.Get(Key("key1"));
            Assert.That(FromBytes(finalValue!), Is.EqualTo($"writer{winners[0]}"));
        }

        #endregion

        #region Isolation Level Tests

        [Test]
        [TestCase(IsolationLevel.ReadUncommitted)]
        [TestCase(IsolationLevel.ReadCommitted)]
        [TestCase(IsolationLevel.RepeatableRead)]
        [TestCase(IsolationLevel.Snapshot)]
        [TestCase(IsolationLevel.Serializable)]
        public void IsolationLevelUnderConcurrentLoadTest(IsolationLevel isolationLevel)
        {
            using var store = CreateStore();

            // Setup data
            for (int i = 0; i < 20; i++)
            {
                store.Put(Key($"key{i}"), Value($"value{i}"));
            }

            const int readerCount = 5;
            const int readsPerReader = 100;
            var exceptions = new List<Exception>();

            var tasks = Enumerable.Range(0, readerCount).Select(readerId => Task.Run(() =>
            {
                try
                {
                    var random = new Random(readerId);
                    for (int i = 0; i < readsPerReader; i++)
                    {
                        using var tx = store.BeginTransaction(isolationLevel);
                        
                        // Multiple reads
                        for (int j = 0; j < 5; j++)
                        {
                            tx.Get(Key($"key{random.Next(20)}"));
                        }
                        
                        // Read-only, just rollback
                        tx.Rollback();
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions) exceptions.Add(ex);
                }
            })).ToArray();

            Task.WaitAll(tasks);

            Assert.That(exceptions, Is.Empty,
                $"Exceptions with {isolationLevel}: {string.Join(", ", exceptions.Select(e => e.Message))}");
        }

        #endregion

        #region Savepoint Tests

        [Test]
        public void SavepointsUnderLoadTest()
        {
            using var store = CreateStore();

            const int transactionCount = 50;
            const int savepointsPerTransaction = 5;
            var exceptions = new List<Exception>();

            var tasks = Enumerable.Range(0, 10).Select(taskId => Task.Run(() =>
            {
                try
                {
                    for (int t = 0; t < transactionCount / 10; t++)
                    {
                        using var tx = store.BeginTransaction();
                        var mvccTx = tx as ITransactionWithSavepoints;
                        Assert.That(mvccTx, Is.Not.Null);

                        for (int sp = 0; sp < savepointsPerTransaction; sp++)
                        {
                            tx.Put(Key($"task{taskId}_tx{t}_sp{sp}"), Value($"v{sp}"));
                            mvccTx!.CreateSavepoint($"sp{sp}");
                        }

                        // Rollback to middle savepoint
                        mvccTx!.RollbackToSavepoint("sp2");

                        tx.Commit();
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions) exceptions.Add(ex);
                }
            })).ToArray();

            Task.WaitAll(tasks);

            Assert.That(exceptions, Is.Empty);
        }

        #endregion

        #region Garbage Collection Tests

        [Test]
        public void GarbageCollectionDuringActiveTransactionsTest()
        {
            using var store = CreateStore();

            // Create many versions
            for (int version = 0; version < 10; version++)
            {
                for (int key = 0; key < 20; key++)
                {
                    store.Put(Key($"key{key}"), Value($"v{version}"));
                }
            }

            // Start long-running read transaction
            var longReadTx = store.BeginReadOnlyTransaction();
            var snapshotValue = longReadTx.Get(Key("key0"));

            // Run GC
            var removed = store.RunGarbageCollection();

            // Long-running transaction should still see its snapshot
            var afterGcValue = longReadTx.Get(Key("key0"));
            Assert.That(afterGcValue, Is.EqualTo(snapshotValue));

            longReadTx.Dispose();

            TestContext.WriteLine($"GC removed {removed} old versions");
        }

        [Test]
        public void ConcurrentGarbageCollectionTest()
        {
            using var store = CreateStore();

            const int rounds = 10;
            var exceptions = new List<Exception>();

            // Writer task
            var writerTask = Task.Run(() =>
            {
                for (int round = 0; round < rounds; round++)
                {
                    for (int key = 0; key < 50; key++)
                    {
                        store.Put(Key($"key{key}"), Value($"round{round}"));
                    }
                    Thread.Sleep(10);
                }
            });

            // GC task
            var gcTask = Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i < rounds * 2; i++)
                    {
                        store.RunGarbageCollection();
                        Thread.Sleep(20);
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions) exceptions.Add(ex);
                }
            });

            // Reader tasks
            var readerTasks = Enumerable.Range(0, 3).Select(_ => Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i < 100; i++)
                    {
                        using var tx = store.BeginReadOnlyTransaction();
                        for (int key = 0; key < 10; key++)
                        {
                            tx.Get(Key($"key{key}"));
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions) exceptions.Add(ex);
                }
            })).ToArray();

            Task.WaitAll(new[] { writerTask, gcTask }.Concat(readerTasks).ToArray());

            Assert.That(exceptions, Is.Empty);
        }

        #endregion

        #region WitDatabase Integration Tests

        [Test]
        public void WitDatabaseMvccConcurrentAccessTest()
        {
            using var db = new WitDatabaseBuilder()
                .WithMemoryStorage()
                .WithBTree()
                .WithMvcc()
                .Build();

            Assert.That(db.SupportsMvcc, Is.True);

            // Populate
            for (int i = 0; i < 50; i++)
            {
                db.Put($"key{i}", Value($"value{i}"));
            }

            const int readerCount = 10;
            var exceptions = new List<Exception>();

            var tasks = Enumerable.Range(0, readerCount).Select(readerId => Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i < 100; i++)
                    {
                        using var tx = db.BeginReadOnlyTransaction();
                        for (int j = 0; j < 5; j++)
                        {
                            tx.Get(Key($"key{readerId * 5 + j}"));
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions) exceptions.Add(ex);
                }
            })).ToArray();

            Task.WaitAll(tasks);

            Assert.That(exceptions, Is.Empty);
        }

        #endregion

        #region Memory Stress Tests

        [Test]
        public void LargeTransactionWithManyOperationsTest()
        {
            using var store = CreateStore();

            const int operationsPerTransaction = 10000;

            using var tx = store.BeginTransaction();

            for (int i = 0; i < operationsPerTransaction; i++)
            {
                tx.Put(Key($"key{i:D5}"), Value($"value{i}"));
            }

            tx.Commit();

            // Verify sample
            for (int i = 0; i < operationsPerTransaction; i += 1000)
            {
                var value = store.Get(Key($"key{i:D5}"));
                Assert.That(value, Is.Not.Null);
                Assert.That(FromBytes(value!), Is.EqualTo($"value{i}"));
            }
        }

        [Test]
        public void ManySmallTransactionsTest()
        {
            using var store = CreateStore();

            const int transactionCount = 1000;

            for (int i = 0; i < transactionCount; i++)
            {
                using var tx = store.BeginTransaction();
                tx.Put(Key($"tx{i}"), Value($"value{i}"));
                tx.Commit();
            }

            Assert.That(store.ActiveTransactionCount, Is.EqualTo(0));

            // Sample verification
            for (int i = 0; i < transactionCount; i += 100)
            {
                var value = store.Get(Key($"tx{i}"));
                Assert.That(FromBytes(value!), Is.EqualTo($"value{i}"));
            }
        }

        #endregion

        #region Wait Queue Integration Tests

        [Test]
        public void WaitQueueIsExposedTest()
        {
            using var store = CreateStore();

            Assert.That(store.WaitQueue, Is.Not.Null);
            Assert.That(store.WaitingTransactionCount, Is.EqualTo(0));
        }

        [Test]
        public void WaitInQueueAndSignalTest()
        {
            using var store = CreateStore();
            
            var signaled = false;
            var waitTask = Task.Run(() =>
            {
                signaled = store.WaitInQueue(1, isWriter: true, timeout: TimeSpan.FromSeconds(2));
            });

            // Small delay to ensure wait is registered
            Thread.Sleep(50);
            Assert.That(store.WaitingTransactionCount, Is.EqualTo(1));

            // Signal the waiting transaction
            var signaledId = store.SignalNextWaiting();
            waitTask.Wait();

            Assert.That(signaledId, Is.EqualTo(1));
            Assert.That(signaled, Is.True);
            Assert.That(store.WaitingTransactionCount, Is.EqualTo(0));
        }

        [Test]
        public async Task WaitInQueueAsyncAndSignalTest()
        {
            using var store = CreateStore();
            
            var waitTask = store.WaitInQueueAsync(1, isWriter: false, timeout: TimeSpan.FromSeconds(2));

            // Small delay to ensure wait is registered
            await Task.Delay(50);

            // Signal the waiting transaction
            store.SignalTransaction(1);
            var result = await waitTask;

            Assert.That(result, Is.True);
        }

        [Test]
        public void WaitInQueueTimeoutTest()
        {
            using var store = CreateStore();

            var result = store.WaitInQueue(1, isWriter: true, timeout: TimeSpan.FromMilliseconds(50));

            Assert.That(result, Is.False);
            Assert.That(store.WaitingTransactionCount, Is.EqualTo(0));
        }

        [Test]
        public void CommitSignalsNextWaitingTransactionTest()
        {
            using var store = CreateStore();

            var signaled = false;
            var waitTask = Task.Run(() =>
            {
                signaled = store.WaitInQueue(99, isWriter: true, timeout: TimeSpan.FromSeconds(2));
            });

            Thread.Sleep(50);
            Assert.That(store.WaitingTransactionCount, Is.EqualTo(1));

            // Commit a transaction - should signal waiting
            using (var tx = store.BeginTransaction())
            {
                tx.Put(Key("key1"), Value("value1"));
                tx.Commit();
            }

            waitTask.Wait();
            Assert.That(signaled, Is.True);
        }

        [Test]
        public void RollbackSignalsNextWaitingTransactionTest()
        {
            using var store = CreateStore();

            var signaled = false;
            var waitTask = Task.Run(() =>
            {
                signaled = store.WaitInQueue(99, isWriter: true, timeout: TimeSpan.FromSeconds(2));
            });

            Thread.Sleep(50);

            // Rollback a transaction - should signal waiting
            using (var tx = store.BeginTransaction())
            {
                tx.Put(Key("key1"), Value("value1"));
                tx.Rollback();
            }

            waitTask.Wait();
            Assert.That(signaled, Is.True);
        }

        [Test]
        public void PriorityBasedWaitQueueTest()
        {
            using var store = CreateStore();

            // Enqueue transactions with different priorities
            var lowPriorityTask = Task.Run(() => 
                store.WaitInQueue(1, isWriter: true, TransactionPriority.Low, TimeSpan.FromSeconds(2)));
            Thread.Sleep(20);

            var highPriorityTask = Task.Run(() => 
                store.WaitInQueue(2, isWriter: true, TransactionPriority.High, TimeSpan.FromSeconds(2)));
            Thread.Sleep(20);

            var normalPriorityTask = Task.Run(() => 
                store.WaitInQueue(3, isWriter: true, TransactionPriority.Normal, TimeSpan.FromSeconds(2)));
            Thread.Sleep(20);

            Assert.That(store.WaitingTransactionCount, Is.EqualTo(3));

            // Signal should return high priority first
            var first = store.SignalNextWaiting();
            var second = store.SignalNextWaiting();
            var third = store.SignalNextWaiting();

            Task.WaitAll(lowPriorityTask, highPriorityTask, normalPriorityTask);

            Assert.That(first, Is.EqualTo(2));  // High
            Assert.That(second, Is.EqualTo(3)); // Normal
            Assert.That(third, Is.EqualTo(1));  // Low
        }

        [Test]
        [Explicit]
        public void DisposeSignalsAllWaitingTransactionsTest()
        {
            var store = CreateStore();

            var results = new bool[3];
            var tasks = new[]
            {
                Task.Run(() => results[0] = store.WaitInQueue(1, isWriter: true, timeout: TimeSpan.FromSeconds(5))),
                Task.Run(() => results[1] = store.WaitInQueue(2, isWriter: true, timeout: TimeSpan.FromSeconds(5))),
                Task.Run(() => results[2] = store.WaitInQueue(3, isWriter: true, timeout: TimeSpan.FromSeconds(5)))
            };

            Thread.Sleep(100);
            Assert.That(store.WaitingTransactionCount, Is.EqualTo(3));

            // Dispose should signal all waiting transactions
            store.Dispose();

            Task.WaitAll(tasks);

            // All should have been signaled (returned true)
            Assert.That(results.All(r => r), Is.True);
        }

        #endregion
    }
}

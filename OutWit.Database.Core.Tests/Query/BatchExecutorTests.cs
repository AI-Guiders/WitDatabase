using NUnit.Framework;
using OutWit.Database.Core.Interfaces;
using OutWit.Database.Core.Query;
using OutWit.Database.Core.Storage;
using OutWit.Database.Core.Stores;
using TextEncoding = System.Text.Encoding;

namespace OutWit.Database.Core.Tests.Query
{
    /// <summary>
    /// Tests for BatchExecutor and batch execution support.
    /// </summary>
    [TestFixture]
    public class BatchExecutorTests
    {
        #region Fields

        private StoreBTree m_store = null!;

        #endregion

        #region Setup

        [SetUp]
        public void Setup()
        {
            m_store = new StoreBTree(new StorageMemory());
        }

        [TearDown]
        public void TearDown()
        {
            m_store.Dispose();
        }

        #endregion

        #region Helper Methods

        private static byte[] ToBytes(string s) => TextEncoding.UTF8.GetBytes(s);
        private static string ToString(byte[] b) => TextEncoding.UTF8.GetString(b);

        #endregion

        #region Basic Batch Tests

        [Test]
        public void ExecuteBatchWithPutOperationsTest()
        {
            var operations = new BatchOperation[]
            {
                new BatchPutOperation(ToBytes("key1"), ToBytes("value1")),
                new BatchPutOperation(ToBytes("key2"), ToBytes("value2")),
                new BatchPutOperation(ToBytes("key3"), ToBytes("value3"))
            };

            using var reader = m_store.ExecuteBatch(operations);

            Assert.That(reader.ResultSetCount, Is.EqualTo(3));

            // Each Put returns affected count of 1
            while (reader.NextResult())
            {
                Assert.That(reader.RecordsAffected, Is.EqualTo(1));
            }

            // Verify data was inserted
            Assert.That(ToString(m_store.Get(ToBytes("key1"))!), Is.EqualTo("value1"));
            Assert.That(ToString(m_store.Get(ToBytes("key2"))!), Is.EqualTo("value2"));
            Assert.That(ToString(m_store.Get(ToBytes("key3"))!), Is.EqualTo("value3"));
        }

        [Test]
        public void ExecuteBatchWithDeleteOperationsTest()
        {
            // Setup
            m_store.Put(ToBytes("key1"), ToBytes("value1"));
            m_store.Put(ToBytes("key2"), ToBytes("value2"));

            var operations = new BatchOperation[]
            {
                new BatchDeleteOperation(ToBytes("key1")),
                new BatchDeleteOperation(ToBytes("nonexistent")),
                new BatchDeleteOperation(ToBytes("key2"))
            };

            using var reader = m_store.ExecuteBatch(operations);

            Assert.That(reader.ResultSetCount, Is.EqualTo(3));

            Assert.That(reader.NextResult(), Is.True);
            Assert.That(reader.RecordsAffected, Is.EqualTo(1)); // key1 deleted

            Assert.That(reader.NextResult(), Is.True);
            Assert.That(reader.RecordsAffected, Is.EqualTo(0)); // nonexistent

            Assert.That(reader.NextResult(), Is.True);
            Assert.That(reader.RecordsAffected, Is.EqualTo(1)); // key2 deleted
        }

        [Test]
        public void ExecuteBatchWithGetOperationsTest()
        {
            // Setup
            m_store.Put(ToBytes("key1"), ToBytes("value1"));
            m_store.Put(ToBytes("key2"), ToBytes("value2"));

            var operations = new BatchOperation[]
            {
                new BatchGetOperation(ToBytes("key1")),
                new BatchGetOperation(ToBytes("nonexistent")),
                new BatchGetOperation(ToBytes("key2"))
            };

            using var reader = m_store.ExecuteBatch(operations);

            Assert.That(reader.ResultSetCount, Is.EqualTo(3));

            // First Get - key1
            Assert.That(reader.NextResult(), Is.True);
            var results1 = reader.CurrentResult!.ToList();
            Assert.That(results1, Has.Count.EqualTo(1));
            Assert.That(ToString(results1[0].Value), Is.EqualTo("value1"));

            // Second Get - nonexistent (empty result)
            Assert.That(reader.NextResult(), Is.True);
            var results2 = reader.CurrentResult!.ToList();
            Assert.That(results2, Has.Count.EqualTo(0));

            // Third Get - key2
            Assert.That(reader.NextResult(), Is.True);
            var results3 = reader.CurrentResult!.ToList();
            Assert.That(results3, Has.Count.EqualTo(1));
            Assert.That(ToString(results3[0].Value), Is.EqualTo("value2"));
        }

        [Test]
        public void ExecuteBatchWithScanOperationsTest()
        {
            // Setup
            m_store.Put(ToBytes("a1"), ToBytes("v1"));
            m_store.Put(ToBytes("a2"), ToBytes("v2"));
            m_store.Put(ToBytes("b1"), ToBytes("v3"));
            m_store.Put(ToBytes("b2"), ToBytes("v4"));

            var operations = new BatchOperation[]
            {
                new BatchScanOperation(ToBytes("a"), ToBytes("b")), // a1, a2
                new BatchScanOperation(ToBytes("b"), ToBytes("c"))  // b1, b2
            };

            using var reader = m_store.ExecuteBatch(operations);

            Assert.That(reader.ResultSetCount, Is.EqualTo(2));

            // First Scan - a*
            Assert.That(reader.NextResult(), Is.True);
            var results1 = reader.CurrentResult!.ToList();
            Assert.That(results1, Has.Count.EqualTo(2));

            // Second Scan - b*
            Assert.That(reader.NextResult(), Is.True);
            var results2 = reader.CurrentResult!.ToList();
            Assert.That(results2, Has.Count.EqualTo(2));
        }

        #endregion

        #region Mixed Operation Tests

        [Test]
        public void ExecuteBatchWithMixedOperationsTest()
        {
            var operations = new BatchOperation[]
            {
                new BatchPutOperation(ToBytes("key1"), ToBytes("value1")),
                new BatchGetOperation(ToBytes("key1")),
                new BatchPutOperation(ToBytes("key2"), ToBytes("value2")),
                new BatchScanOperation(null, null),
                new BatchDeleteOperation(ToBytes("key1")),
                new BatchGetOperation(ToBytes("key1"))
            };

            using var reader = m_store.ExecuteBatch(operations);

            Assert.That(reader.ResultSetCount, Is.EqualTo(6));

            // Put key1 - affected 1
            Assert.That(reader.NextResult(), Is.True);
            Assert.That(reader.RecordsAffected, Is.EqualTo(1));

            // Get key1 - should find it
            Assert.That(reader.NextResult(), Is.True);
            var get1 = reader.CurrentResult!.ToList();
            Assert.That(get1, Has.Count.EqualTo(1));

            // Put key2 - affected 1
            Assert.That(reader.NextResult(), Is.True);
            Assert.That(reader.RecordsAffected, Is.EqualTo(1));

            // Scan all - should find 2 keys
            Assert.That(reader.NextResult(), Is.True);
            var scan = reader.CurrentResult!.ToList();
            Assert.That(scan, Has.Count.EqualTo(2));

            // Delete key1 - affected 1
            Assert.That(reader.NextResult(), Is.True);
            Assert.That(reader.RecordsAffected, Is.EqualTo(1));

            // Get key1 - should not find it
            Assert.That(reader.NextResult(), Is.True);
            var get2 = reader.CurrentResult!.ToList();
            Assert.That(get2, Has.Count.EqualTo(0));
        }

        #endregion

        #region Edge Cases

        [Test]
        public void ExecuteBatchWithEmptyOperationsTest()
        {
            using var reader = m_store.ExecuteBatch(Array.Empty<BatchOperation>());

            Assert.That(reader.ResultSetCount, Is.EqualTo(0));
            Assert.That(reader.NextResult(), Is.False);
        }

        [Test]
        public void ExecuteBatchWithNullOperationsThrowsTest()
        {
            var executor = new BatchExecutor(m_store);

            Assert.Throws<ArgumentNullException>(() => executor.ExecuteBatch(null!));
        }

        [Test]
        public void BatchExecutorWithNullStoreThrowsTest()
        {
            Assert.Throws<ArgumentNullException>(() => new BatchExecutor(null!));
        }

        #endregion

        #region Params Overload Tests

        [Test]
        public void ExecuteBatchWithParamsTest()
        {
            using var reader = m_store.ExecuteBatch(
                new BatchPutOperation(ToBytes("key1"), ToBytes("value1")),
                new BatchGetOperation(ToBytes("key1"))
            );

            Assert.That(reader.ResultSetCount, Is.EqualTo(2));
        }

        #endregion

        #region Async Tests

        [Test]
        public async Task ExecuteBatchAsyncTest()
        {
            var operations = new BatchOperation[]
            {
                new BatchPutOperation(ToBytes("key1"), ToBytes("value1")),
                new BatchGetOperation(ToBytes("key1"))
            };

            using var reader = await m_store.ExecuteBatchAsync(operations);

            Assert.That(reader.ResultSetCount, Is.EqualTo(2));
        }

        [Test]
        public async Task ExecuteBatchAsyncSupportsCancellationTest()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var operations = new BatchOperation[]
            {
                new BatchPutOperation(ToBytes("key1"), ToBytes("value1"))
            };

            Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await m_store.ExecuteBatchAsync(operations, cts.Token));
        }

        #endregion

        #region Operation Type Tests

        [Test]
        public void BatchPutOperationPropertiesTest()
        {
            var op = new BatchPutOperation(ToBytes("key"), ToBytes("value"));

            Assert.That(op.Type, Is.EqualTo(BatchOperationType.Put));
            Assert.That(ToString(op.Key), Is.EqualTo("key"));
            Assert.That(ToString(op.Value), Is.EqualTo("value"));
        }

        [Test]
        public void BatchDeleteOperationPropertiesTest()
        {
            var op = new BatchDeleteOperation(ToBytes("key"));

            Assert.That(op.Type, Is.EqualTo(BatchOperationType.Delete));
            Assert.That(ToString(op.Key), Is.EqualTo("key"));
        }

        [Test]
        public void BatchGetOperationPropertiesTest()
        {
            var op = new BatchGetOperation(ToBytes("key"));

            Assert.That(op.Type, Is.EqualTo(BatchOperationType.Get));
            Assert.That(ToString(op.Key), Is.EqualTo("key"));
        }

        [Test]
        public void BatchScanOperationPropertiesTest()
        {
            var op = new BatchScanOperation(ToBytes("start"), ToBytes("end"));

            Assert.That(op.Type, Is.EqualTo(BatchOperationType.Scan));
            Assert.That(ToString(op.StartKey!), Is.EqualTo("start"));
            Assert.That(ToString(op.EndKey!), Is.EqualTo("end"));
        }

        [Test]
        public void BatchScanOperationWithNullKeysTest()
        {
            var op = new BatchScanOperation();

            Assert.That(op.StartKey, Is.Null);
            Assert.That(op.EndKey, Is.Null);
        }

        #endregion
    }
}

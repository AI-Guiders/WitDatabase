using NUnit.Framework;
using OutWit.Database.Core.Interfaces;
using OutWit.Database.Core.Storage;
using OutWit.Database.Core.Stores;
using TextEncoding = System.Text.Encoding;

namespace OutWit.Database.Core.Tests.Stores
{
    /// <summary>
    /// Tests for bulk operations extensions.
    /// </summary>
    [TestFixture]
    public class BulkOperationsTests
    {
        #region Fields

        private IKeyValueStore m_store = null!;

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

        private static (byte[] Key, byte[] Value) MakePair(int i) 
            => (ToBytes($"key{i:D4}"), ToBytes($"value{i}"));

        private static IEnumerable<(byte[] Key, byte[] Value)> MakeBatch(int count)
            => Enumerable.Range(0, count).Select(MakePair);

        #endregion

        #region BulkPut Tests

        [Test]
        public void BulkPutInsertsAllItemsTest()
        {
            var items = MakeBatch(100).ToList();

            var count = m_store.BulkPut(items);

            Assert.That(count, Is.EqualTo(100));

            foreach (var (key, value) in items)
            {
                var result = m_store.Get(key);
                Assert.That(result, Is.EqualTo(value));
            }
        }

        [Test]
        public void BulkPutWithEmptyEnumerableReturnsZeroTest()
        {
            var count = m_store.BulkPut(Array.Empty<(byte[], byte[])>());

            Assert.That(count, Is.EqualTo(0));
        }

        [Test]
        public void BulkPutUpdatesExistingKeysTest()
        {
            // Insert initial values
            m_store.Put(ToBytes("key1"), ToBytes("original1"));
            m_store.Put(ToBytes("key2"), ToBytes("original2"));

            // Bulk update
            var items = new[]
            {
                (ToBytes("key1"), ToBytes("updated1")),
                (ToBytes("key2"), ToBytes("updated2")),
                (ToBytes("key3"), ToBytes("new3"))
            };

            var count = m_store.BulkPut(items);

            Assert.That(count, Is.EqualTo(3));
            Assert.That(ToString(m_store.Get(ToBytes("key1"))!), Is.EqualTo("updated1"));
            Assert.That(ToString(m_store.Get(ToBytes("key2"))!), Is.EqualTo("updated2"));
            Assert.That(ToString(m_store.Get(ToBytes("key3"))!), Is.EqualTo("new3"));
        }

        [Test]
        public void BulkPutWithNullStoreThrowsTest()
        {
            IKeyValueStore? nullStore = null;

            Assert.Throws<ArgumentNullException>(() => nullStore!.BulkPut(MakeBatch(10)));
        }

        [Test]
        public void BulkPutWithNullItemsThrowsTest()
        {
            Assert.Throws<ArgumentNullException>(() => m_store.BulkPut(null!));
        }

        [Test]
        public async Task BulkPutAsyncInsertsAllItemsTest()
        {
            var items = MakeBatch(100).ToList();

            var count = await m_store.BulkPutAsync(items);

            Assert.That(count, Is.EqualTo(100));
        }

        [Test]
        public async Task BulkPutAsyncSupportsCancellationTest()
        {
            var items = MakeBatch(1000);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await m_store.BulkPutAsync(items, cts.Token));
        }

        #endregion

        #region BulkDelete Tests

        [Test]
        public void BulkDeleteRemovesAllKeysTest()
        {
            // Setup: insert items
            var items = MakeBatch(50).ToList();
            m_store.BulkPut(items);

            // Delete half
            var keysToDelete = items.Take(25).Select(x => x.Key).ToList();
            var deletedCount = m_store.BulkDelete(keysToDelete);

            Assert.That(deletedCount, Is.EqualTo(25));

            // Verify deleted keys are gone
            foreach (var key in keysToDelete)
            {
                Assert.That(m_store.Get(key), Is.Null);
            }

            // Verify remaining keys still exist
            foreach (var (key, value) in items.Skip(25))
            {
                Assert.That(m_store.Get(key), Is.EqualTo(value));
            }
        }

        [Test]
        public void BulkDeleteWithEmptyEnumerableReturnsZeroTest()
        {
            var count = m_store.BulkDelete(Array.Empty<byte[]>());

            Assert.That(count, Is.EqualTo(0));
        }

        [Test]
        public void BulkDeleteNonExistentKeysReturnsZeroTest()
        {
            var keys = new[]
            {
                ToBytes("nonexistent1"),
                ToBytes("nonexistent2"),
                ToBytes("nonexistent3")
            };

            var count = m_store.BulkDelete(keys);

            Assert.That(count, Is.EqualTo(0));
        }

        [Test]
        public void BulkDeleteMixedExistingAndNonExistentTest()
        {
            m_store.Put(ToBytes("exists1"), ToBytes("value1"));
            m_store.Put(ToBytes("exists2"), ToBytes("value2"));

            var keys = new[]
            {
                ToBytes("exists1"),
                ToBytes("nonexistent"),
                ToBytes("exists2")
            };

            var count = m_store.BulkDelete(keys);

            Assert.That(count, Is.EqualTo(2));
        }

        [Test]
        public void BulkDeleteWithNullStoreThrowsTest()
        {
            IKeyValueStore? nullStore = null;

            Assert.Throws<ArgumentNullException>(() => nullStore!.BulkDelete(new[] { ToBytes("key") }));
        }

        [Test]
        public void BulkDeleteWithNullKeysThrowsTest()
        {
            Assert.Throws<ArgumentNullException>(() => m_store.BulkDelete(null!));
        }

        [Test]
        public async Task BulkDeleteAsyncRemovesAllKeysTest()
        {
            var items = MakeBatch(50).ToList();
            await m_store.BulkPutAsync(items);

            var keysToDelete = items.Select(x => x.Key).ToList();
            var count = await m_store.BulkDeleteAsync(keysToDelete);

            Assert.That(count, Is.EqualTo(50));
        }

        [Test]
        public async Task BulkDeleteAsyncSupportsCancellationTest()
        {
            m_store.BulkPut(MakeBatch(100));

            var keys = Enumerable.Range(0, 100).Select(i => ToBytes($"key{i:D4}"));
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await m_store.BulkDeleteAsync(keys, cts.Token));
        }

        #endregion

        #region BulkPutAndFlush Tests

        [Test]
        public void BulkPutAndFlushInsertsAndFlushesTest()
        {
            var items = MakeBatch(10).ToList();

            var count = m_store.BulkPutAndFlush(items);

            Assert.That(count, Is.EqualTo(10));

            // Verify all items are persisted
            foreach (var (key, value) in items)
            {
                Assert.That(m_store.Get(key), Is.EqualTo(value));
            }
        }

        [Test]
        public void BulkPutAndFlushWithFlushFalseDoesNotFlushTest()
        {
            var items = MakeBatch(10).ToList();

            var count = m_store.BulkPutAndFlush(items, flushAfter: false);

            Assert.That(count, Is.EqualTo(10));
        }

        #endregion

        #region BulkDeleteAndFlush Tests

        [Test]
        public void BulkDeleteAndFlushDeletesAndFlushesTest()
        {
            var items = MakeBatch(10).ToList();
            m_store.BulkPut(items);

            var keys = items.Select(x => x.Key).ToList();
            var count = m_store.BulkDeleteAndFlush(keys);

            Assert.That(count, Is.EqualTo(10));

            foreach (var key in keys)
            {
                Assert.That(m_store.Get(key), Is.Null);
            }
        }

        #endregion

        #region Performance Tests

        [Test]
        public void BulkPutHandlesLargeBatchTest()
        {
            var items = MakeBatch(10000).ToList();

            var count = m_store.BulkPut(items);

            Assert.That(count, Is.EqualTo(10000));

            // Spot check
            Assert.That(m_store.Get(ToBytes("key0000")), Is.Not.Null);
            Assert.That(m_store.Get(ToBytes("key9999")), Is.Not.Null);
        }

        [Test]
        public void BulkDeleteHandlesLargeBatchTest()
        {
            var items = MakeBatch(10000).ToList();
            m_store.BulkPut(items);

            var keys = items.Select(x => x.Key).ToList();
            var count = m_store.BulkDelete(keys);

            Assert.That(count, Is.EqualTo(10000));
        }

        #endregion

        #region Streaming Insert Tests

        [Test]
        public void StreamingPutInsertsAllItemsTest()
        {
            var items = MakeBatch(100).ToList();

            var count = m_store.StreamingPut(items, batchSize: 25);

            Assert.That(count, Is.EqualTo(100));

            foreach (var (key, value) in items)
            {
                Assert.That(m_store.Get(key), Is.EqualTo(value));
            }
        }

        [Test]
        public void StreamingPutReportsProgressTest()
        {
            var items = MakeBatch(100).ToList();
            var progressCalls = new List<int>();

            var count = m_store.StreamingPut(items, batchSize: 25, progress: p => progressCalls.Add(p));

            Assert.That(count, Is.EqualTo(100));
            Assert.That(progressCalls, Is.EqualTo(new[] { 25, 50, 75, 100 }));
        }

        [Test]
        public void StreamingPutWithPartialBatchFlushesRemainingTest()
        {
            var items = MakeBatch(30).ToList();
            var progressCalls = new List<int>();

            var count = m_store.StreamingPut(items, batchSize: 25, progress: p => progressCalls.Add(p));

            Assert.That(count, Is.EqualTo(30));
            Assert.That(progressCalls, Is.EqualTo(new[] { 25, 30 }));
        }

        [Test]
        public void StreamingPutWithNullStoreThrowsTest()
        {
            IKeyValueStore? nullStore = null;

            Assert.Throws<ArgumentNullException>(() => nullStore!.StreamingPut(MakeBatch(10)));
        }

        [Test]
        public void StreamingPutWithNullItemsThrowsTest()
        {
            Assert.Throws<ArgumentNullException>(() => m_store.StreamingPut(null!));
        }

        [Test]
        public void StreamingPutWithZeroBatchSizeThrowsTest()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => m_store.StreamingPut(MakeBatch(10), batchSize: 0));
        }

        [Test]
        public void StreamingPutWithEmptyEnumerableReturnsZeroTest()
        {
            var count = m_store.StreamingPut(Array.Empty<(byte[], byte[])>());

            Assert.That(count, Is.EqualTo(0));
        }

        [Test]
        public async Task StreamingPutAsyncInsertsAllItemsTest()
        {
            var items = MakeBatch(100).ToList();

            var count = await m_store.StreamingPutAsync(items, batchSize: 25);

            Assert.That(count, Is.EqualTo(100));
        }

        [Test]
        public async Task StreamingPutAsyncReportsProgressTest()
        {
            var items = MakeBatch(100).ToList();
            var progressCalls = new List<int>();

            var count = await m_store.StreamingPutAsync(items, batchSize: 25, 
                progress: p => progressCalls.Add(p));

            Assert.That(count, Is.EqualTo(100));
            Assert.That(progressCalls, Is.EqualTo(new[] { 25, 50, 75, 100 }));
        }

        [Test]
        public async Task StreamingPutAsyncSupportsCancellationTest()
        {
            var items = MakeBatch(1000);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await m_store.StreamingPutAsync(items, cancellationToken: cts.Token));
        }

        [Test]
        public async Task StreamingPutAsyncFromAsyncEnumerableTest()
        {
            async IAsyncEnumerable<(byte[] Key, byte[] Value)> GenerateAsync()
            {
                for (int i = 0; i < 50; i++)
                {
                    await Task.Yield();
                    yield return MakePair(i);
                }
            }

            var count = await m_store.StreamingPutAsync(GenerateAsync(), batchSize: 10);

            Assert.That(count, Is.EqualTo(50));
        }

        [Test]
        public async Task StreamingPutAsyncFromAsyncEnumerableReportsProgressTest()
        {
            var progressCalls = new List<int>();

            async IAsyncEnumerable<(byte[] Key, byte[] Value)> GenerateAsync()
            {
                for (int i = 0; i < 30; i++)
                {
                    await Task.Yield();
                    yield return MakePair(i);
                }
            }

            var count = await m_store.StreamingPutAsync(GenerateAsync(), batchSize: 10,
                progress: p => progressCalls.Add(p));

            Assert.That(count, Is.EqualTo(30));
            Assert.That(progressCalls, Is.EqualTo(new[] { 10, 20, 30 }));
        }

        #endregion
    }
}

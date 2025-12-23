using NUnit.Framework;
using OutWit.Database.Core.Interfaces;
using OutWit.Database.Core.Storage;
using OutWit.Database.Core.Stores;
using TextEncoding = System.Text.Encoding;

namespace OutWit.Database.Core.Tests.Stores
{
    /// <summary>
    /// Tests for key-value store statistics.
    /// </summary>
    [TestFixture]
    public class StoreStatisticsTests
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

        #endregion

        #region Count Extension Tests

        [Test]
        public void CountOnEmptyStoreReturnsZeroTest()
        {
            var count = m_store.Count();

            Assert.That(count, Is.EqualTo(0));
        }

        [Test]
        public void CountReturnsCorrectNumberTest()
        {
            for (int i = 0; i < 100; i++)
            {
                m_store.Put(ToBytes($"key{i}"), ToBytes($"value{i}"));
            }

            var count = m_store.Count();

            Assert.That(count, Is.EqualTo(100));
        }

        [Test]
        public void CountAfterDeletesIsCorrectTest()
        {
            for (int i = 0; i < 50; i++)
            {
                m_store.Put(ToBytes($"key{i}"), ToBytes($"value{i}"));
            }

            for (int i = 0; i < 20; i++)
            {
                m_store.Delete(ToBytes($"key{i}"));
            }

            var count = m_store.Count();

            Assert.That(count, Is.EqualTo(30));
        }

        [Test]
        public async Task CountAsyncReturnsCorrectNumberTest()
        {
            for (int i = 0; i < 50; i++)
            {
                await m_store.PutAsync(ToBytes($"key{i}"), ToBytes($"value{i}"));
            }

            var count = await m_store.CountAsync();

            Assert.That(count, Is.EqualTo(50));
        }

        [Test]
        public void CountWithNullStoreThrowsTest()
        {
            IKeyValueStore? nullStore = null;

            Assert.Throws<ArgumentNullException>(() => nullStore!.Count());
        }

        #endregion

        #region IsEmpty Extension Tests

        [Test]
        public void IsEmptyReturnsTrueForEmptyStoreTest()
        {
            Assert.That(m_store.IsEmpty(), Is.True);
        }

        [Test]
        public void IsEmptyReturnsFalseForNonEmptyStoreTest()
        {
            m_store.Put(ToBytes("key"), ToBytes("value"));

            Assert.That(m_store.IsEmpty(), Is.False);
        }

        [Test]
        public void IsEmptyAfterClearingStoreTest()
        {
            m_store.Put(ToBytes("key"), ToBytes("value"));
            m_store.Delete(ToBytes("key"));

            Assert.That(m_store.IsEmpty(), Is.True);
        }

        #endregion

        #region ContainsKey Extension Tests

        [Test]
        public void ContainsKeyReturnsTrueForExistingKeyTest()
        {
            m_store.Put(ToBytes("key"), ToBytes("value"));

            Assert.That(m_store.ContainsKey(ToBytes("key")), Is.True);
        }

        [Test]
        public void ContainsKeyReturnsFalseForMissingKeyTest()
        {
            Assert.That(m_store.ContainsKey(ToBytes("nonexistent")), Is.False);
        }

        [Test]
        public void ContainsKeyReturnsFalseAfterDeleteTest()
        {
            m_store.Put(ToBytes("key"), ToBytes("value"));
            m_store.Delete(ToBytes("key"));

            Assert.That(m_store.ContainsKey(ToBytes("key")), Is.False);
        }

        #endregion

        #region GetApproximateSizeInBytes Tests

        [Test]
        public void GetApproximateSizeInBytesReturnsMinusOneForNonStatisticsStoreTest()
        {
            // StoreBTree does not implement IKeyValueStoreStatistics
            var size = m_store.GetApproximateSizeInBytes();

            // Without native stats, returns -1
            Assert.That(size, Is.EqualTo(-1));
        }

        [Test]
        public void GetApproximateSizeInBytesWithNullStoreThrowsTest()
        {
            IKeyValueStore? nullStore = null;

            Assert.Throws<ArgumentNullException>(() => nullStore!.GetApproximateSizeInBytes());
        }

        #endregion

        #region StoreStatistics Wrapper Tests

        [Test]
        public void GetStatisticsReturnsWrapperTest()
        {
            var stats = m_store.GetStatistics();

            Assert.That(stats, Is.Not.Null);
            Assert.That(stats, Is.InstanceOf<StoreStatistics>());
        }

        [Test]
        public void StoreStatisticsCountMatchesExtensionTest()
        {
            for (int i = 0; i < 25; i++)
            {
                m_store.Put(ToBytes($"key{i}"), ToBytes($"value{i}"));
            }

            var stats = m_store.GetStatistics();

            Assert.That(stats.Count(), Is.EqualTo(25));
            Assert.That(stats.Count(), Is.EqualTo(m_store.Count()));
        }

        [Test]
        public async Task StoreStatisticsCountAsyncWorksTest()
        {
            for (int i = 0; i < 25; i++)
            {
                m_store.Put(ToBytes($"key{i}"), ToBytes($"value{i}"));
            }

            var stats = m_store.GetStatistics();
            var count = await stats.CountAsync();

            Assert.That(count, Is.EqualTo(25));
        }

        [Test]
        public void StoreStatisticsEstimatedKeyCountEqualsCountWhenNoNativeStatsTest()
        {
            for (int i = 0; i < 10; i++)
            {
                m_store.Put(ToBytes($"key{i}"), ToBytes($"value{i}"));
            }

            var stats = m_store.GetStatistics();

            Assert.That(stats.EstimatedKeyCount, Is.EqualTo(10));
        }

        [Test]
        public void StoreStatisticsHasNativeStatisticsIsFalseForBasicStoreTest()
        {
            var stats = m_store.GetStatistics();

            Assert.That(stats.HasNativeStatistics, Is.False);
        }

        [Test]
        public void StoreStatisticsAreStatisticsExactIsTrueWhenComputedTest()
        {
            var stats = m_store.GetStatistics();

            // When computing via scan, statistics are exact
            Assert.That(stats.AreStatisticsExact, Is.True);
        }

        [Test]
        public void StoreStatisticsWithNullStoreThrowsTest()
        {
            Assert.Throws<ArgumentNullException>(() => new StoreStatistics(null!));
        }

        #endregion

        #region IKeyValueStoreStatistics Interface Tests

        [Test]
        public void StoreStatisticsImplementsInterfaceTest()
        {
            var stats = m_store.GetStatistics();

            IKeyValueStoreStatistics iStats = stats;

            Assert.That(iStats.Count(), Is.EqualTo(0));
        }

        #endregion
    }
}

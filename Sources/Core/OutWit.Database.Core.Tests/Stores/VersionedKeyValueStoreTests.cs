using NUnit.Framework;
using OutWit.Database.Core.Interfaces;
using OutWit.Database.Core.Storage;
using OutWit.Database.Core.Stores;
using TextEncoding = System.Text.Encoding;

namespace OutWit.Database.Core.Tests.Stores
{
    /// <summary>
    /// Tests for VersionedKeyValueStore and optimistic concurrency.
    /// </summary>
    [TestFixture]
    public class VersionedKeyValueStoreTests
    {
        #region Fields

        private VersionedKeyValueStore m_store = null!;

        #endregion

        #region Setup

        [SetUp]
        public void Setup()
        {
            var innerStore = new StoreBTree(new StorageMemory());
            m_store = new VersionedKeyValueStore(innerStore, ownsStore: true);
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

        #region Basic Operations Tests

        [Test]
        public void PutAndGetReturnsValueTest()
        {
            m_store.Put(ToBytes("key"), ToBytes("value"));

            var result = m_store.Get(ToBytes("key"));

            Assert.That(result, Is.Not.Null);
            Assert.That(ToString(result!), Is.EqualTo("value"));
        }

        [Test]
        public void GetNonExistentKeyReturnsNullTest()
        {
            var result = m_store.Get(ToBytes("nonexistent"));

            Assert.That(result, Is.Null);
        }

        [Test]
        public void DeleteRemovesKeyTest()
        {
            m_store.Put(ToBytes("key"), ToBytes("value"));

            var deleted = m_store.Delete(ToBytes("key"));

            Assert.That(deleted, Is.True);
            Assert.That(m_store.Get(ToBytes("key")), Is.Null);
        }

        [Test]
        public void DeleteNonExistentReturnsFalseTest()
        {
            var deleted = m_store.Delete(ToBytes("nonexistent"));

            Assert.That(deleted, Is.False);
        }

        #endregion

        #region Version Tests

        [Test]
        public void PutWithVersionReturnsVersionTest()
        {
            var version = m_store.PutWithVersion(ToBytes("key"), ToBytes("value"));

            Assert.That(version, Is.GreaterThan(0));
        }

        [Test]
        public void PutWithVersionIncrementsVersionTest()
        {
            var version1 = m_store.PutWithVersion(ToBytes("key1"), ToBytes("value1"));
            var version2 = m_store.PutWithVersion(ToBytes("key2"), ToBytes("value2"));
            var version3 = m_store.PutWithVersion(ToBytes("key3"), ToBytes("value3"));

            Assert.That(version2, Is.GreaterThan(version1));
            Assert.That(version3, Is.GreaterThan(version2));
        }

        [Test]
        public void GetWithVersionReturnsValueAndVersionTest()
        {
            var putVersion = m_store.PutWithVersion(ToBytes("key"), ToBytes("value"));

            var result = m_store.GetWithVersion(ToBytes("key"));

            Assert.That(result, Is.Not.Null);
            Assert.That(ToString(result!.Value.Value), Is.EqualTo("value"));
            Assert.That(result.Value.Version, Is.EqualTo(putVersion));
        }

        [Test]
        public void GetVersionReturnsCorrectVersionTest()
        {
            var putVersion = m_store.PutWithVersion(ToBytes("key"), ToBytes("value"));

            var version = m_store.GetVersion(ToBytes("key"));

            Assert.That(version, Is.EqualTo(putVersion));
        }

        [Test]
        public void GetVersionReturnsNullForMissingKeyTest()
        {
            var version = m_store.GetVersion(ToBytes("nonexistent"));

            Assert.That(version, Is.Null);
        }

        [Test]
        public void UpdateChangesVersionTest()
        {
            var version1 = m_store.PutWithVersion(ToBytes("key"), ToBytes("value1"));
            var version2 = m_store.PutWithVersion(ToBytes("key"), ToBytes("value2"));

            Assert.That(version2, Is.GreaterThan(version1));

            var currentVersion = m_store.GetVersion(ToBytes("key"));
            Assert.That(currentVersion, Is.EqualTo(version2));
        }

        [Test]
        public void CurrentGlobalVersionUpdatesCorrectlyTest()
        {
            var initialVersion = m_store.CurrentGlobalVersion;

            m_store.Put(ToBytes("key1"), ToBytes("value1"));
            m_store.Put(ToBytes("key2"), ToBytes("value2"));

            Assert.That(m_store.CurrentGlobalVersion, Is.EqualTo(initialVersion + 2));
        }

        #endregion

        #region Conditional Put Tests

        [Test]
        public void ConditionalPutSucceedsWithCorrectVersionTest()
        {
            var version = m_store.PutWithVersion(ToBytes("key"), ToBytes("value1"));

            var (success, newVersion) = m_store.ConditionalPut(
                ToBytes("key"), ToBytes("value2"), version);

            Assert.That(success, Is.True);
            Assert.That(newVersion, Is.GreaterThan(version));
            Assert.That(ToString(m_store.Get(ToBytes("key"))!), Is.EqualTo("value2"));
        }

        [Test]
        public void ConditionalPutFailsWithWrongVersionTest()
        {
            var version = m_store.PutWithVersion(ToBytes("key"), ToBytes("value1"));
            var wrongVersion = version - 1;

            var (success, returnedVersion) = m_store.ConditionalPut(
                ToBytes("key"), ToBytes("value2"), wrongVersion);

            Assert.That(success, Is.False);
            Assert.That(returnedVersion, Is.EqualTo(version));
            Assert.That(ToString(m_store.Get(ToBytes("key"))!), Is.EqualTo("value1"));
        }

        [Test]
        public void ConditionalPutFailsForNewKeyWithNonZeroVersionTest()
        {
            var (success, _) = m_store.ConditionalPut(
                ToBytes("new_key"), ToBytes("value"), expectedVersion: 1);

            Assert.That(success, Is.False);
        }

        [Test]
        public void ConditionalPutForNewKeyWithZeroVersionFailsTest()
        {
            // New key has null version, not 0
            var (success, _) = m_store.ConditionalPut(
                ToBytes("new_key"), ToBytes("value"), expectedVersion: 0);

            Assert.That(success, Is.False);
        }

        #endregion

        #region Conditional Delete Tests

        [Test]
        public void ConditionalDeleteSucceedsWithCorrectVersionTest()
        {
            var version = m_store.PutWithVersion(ToBytes("key"), ToBytes("value"));

            var deleted = m_store.ConditionalDelete(ToBytes("key"), version);

            Assert.That(deleted, Is.True);
            Assert.That(m_store.Get(ToBytes("key")), Is.Null);
        }

        [Test]
        public void ConditionalDeleteFailsWithWrongVersionTest()
        {
            var version = m_store.PutWithVersion(ToBytes("key"), ToBytes("value"));
            var wrongVersion = version - 1;

            var deleted = m_store.ConditionalDelete(ToBytes("key"), wrongVersion);

            Assert.That(deleted, Is.False);
            Assert.That(m_store.Get(ToBytes("key")), Is.Not.Null);
        }

        [Test]
        public void ConditionalDeleteFailsForNonExistentKeyTest()
        {
            var deleted = m_store.ConditionalDelete(ToBytes("nonexistent"), expectedVersion: 1);

            Assert.That(deleted, Is.False);
        }

        #endregion

        #region Scan Tests

        [Test]
        public void ScanReturnsValuesWithoutVersionPrefixTest()
        {
            m_store.Put(ToBytes("key1"), ToBytes("value1"));
            m_store.Put(ToBytes("key2"), ToBytes("value2"));

            var results = m_store.Scan(null, null).ToList();

            Assert.That(results, Has.Count.EqualTo(2));
            Assert.That(ToString(results[0].Value), Is.EqualTo("value1"));
            Assert.That(ToString(results[1].Value), Is.EqualTo("value2"));
        }

        [Test]
        public void ScanWithVersionReturnsVersionInfoTest()
        {
            var v1 = m_store.PutWithVersion(ToBytes("key1"), ToBytes("value1"));
            var v2 = m_store.PutWithVersion(ToBytes("key2"), ToBytes("value2"));

            var results = m_store.ScanWithVersion(null, null).ToList();

            Assert.That(results, Has.Count.EqualTo(2));
            Assert.That(results[0].Version, Is.EqualTo(v1));
            Assert.That(results[1].Version, Is.EqualTo(v2));
        }

        [Test]
        public void ScanExcludesSystemKeysTest()
        {
            m_store.Put(ToBytes("key1"), ToBytes("value1"));
            m_store.Flush(); // This saves global version to a system key

            var results = m_store.Scan(null, null).ToList();

            // Should only see user key, not the version counter system key
            Assert.That(results, Has.Count.EqualTo(1));
        }

        #endregion

        #region Persistence Tests

        [Test]
        public void VersionPersistsAfterFlushTest()
        {
            var version = m_store.PutWithVersion(ToBytes("key"), ToBytes("value"));
            m_store.Flush();

            var result = m_store.GetWithVersion(ToBytes("key"));

            Assert.That(result!.Value.Version, Is.EqualTo(version));
        }

        [Test]
        public void GlobalVersionPersistsAcrossReopenTest()
        {
            using var storage = new StorageMemory();
            long versionBeforeClose;

            using (var innerStore = new StoreBTree(storage, ownsStorage: false))
            using (var store = new VersionedKeyValueStore(innerStore, ownsStore: false))
            {
                store.Put(ToBytes("key1"), ToBytes("value1"));
                store.Put(ToBytes("key2"), ToBytes("value2"));
                versionBeforeClose = store.CurrentGlobalVersion;
                store.Flush();
            }

            using (var innerStore = new StoreBTree(storage, ownsStorage: false))
            using (var store = new VersionedKeyValueStore(innerStore, ownsStore: false))
            {
                Assert.That(store.CurrentGlobalVersion, Is.EqualTo(versionBeforeClose));

                // New puts should continue from last version
                var newVersion = store.PutWithVersion(ToBytes("key3"), ToBytes("value3"));
                Assert.That(newVersion, Is.GreaterThan(versionBeforeClose));
            }
        }

        #endregion

        #region Async Tests

        [Test]
        public async Task GetAsyncReturnsValueTest()
        {
            await m_store.PutAsync(ToBytes("key"), ToBytes("value"));

            var result = await m_store.GetAsync(ToBytes("key"));

            Assert.That(result, Is.Not.Null);
            Assert.That(ToString(result!), Is.EqualTo("value"));
        }

        [Test]
        public async Task GetWithVersionAsyncReturnsVersionTest()
        {
            var version = await m_store.PutWithVersionAsync(ToBytes("key"), ToBytes("value"));

            var result = await m_store.GetWithVersionAsync(ToBytes("key"));

            Assert.That(result!.Value.Version, Is.EqualTo(version));
        }

        [Test]
        public async Task DeleteAsyncRemovesKeyTest()
        {
            await m_store.PutAsync(ToBytes("key"), ToBytes("value"));

            var deleted = await m_store.DeleteAsync(ToBytes("key"));

            Assert.That(deleted, Is.True);
        }

        [Test]
        public async Task ConditionalDeleteAsyncWorksTest()
        {
            var version = await m_store.PutWithVersionAsync(ToBytes("key"), ToBytes("value"));

            var deleted = await m_store.ConditionalDeleteAsync(ToBytes("key"), version);

            Assert.That(deleted, Is.True);
        }

        #endregion

        #region Interface Tests

        [Test]
        public void ImplementsIVersionedKeyValueStoreTest()
        {
            IVersionedKeyValueStore iStore = m_store;

            var version = iStore.PutWithVersion(ToBytes("key"), ToBytes("value"));
            var result = iStore.GetWithVersion(ToBytes("key"));

            Assert.That(result!.Value.Version, Is.EqualTo(version));
        }

        [Test]
        public void ImplementsIKeyValueStoreTest()
        {
            IKeyValueStore iStore = m_store;

            iStore.Put(ToBytes("key"), ToBytes("value"));
            var result = iStore.Get(ToBytes("key"));

            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public void ProviderKeyIncludesVersionedPrefixTest()
        {
            Assert.That(m_store.ProviderKey, Does.StartWith("versioned:"));
        }

        #endregion

        #region Concurrency Tests

        [Test]
        public void OptimisticConcurrencyPreventsLostUpdatesTest()
        {
            // Simulate two concurrent transactions trying to update the same key
            var initialVersion = m_store.PutWithVersion(ToBytes("account"), ToBytes("100"));

            // Transaction 1 reads
            var (value1, version1) = m_store.GetWithVersion(ToBytes("account"))!.Value;
            var balance1 = int.Parse(ToString(value1));

            // Transaction 2 reads same version
            var (value2, version2) = m_store.GetWithVersion(ToBytes("account"))!.Value;
            var balance2 = int.Parse(ToString(value2));

            Assert.That(version1, Is.EqualTo(version2));

            // Transaction 1 commits first
            var newBalance1 = balance1 + 50; // 150
            var (success1, _) = m_store.ConditionalPut(
                ToBytes("account"), ToBytes(newBalance1.ToString()), version1);
            Assert.That(success1, Is.True);

            // Transaction 2 tries to commit - should fail due to version mismatch
            var newBalance2 = balance2 - 30; // Would be 70, but should fail
            var (success2, _) = m_store.ConditionalPut(
                ToBytes("account"), ToBytes(newBalance2.ToString()), version2);
            Assert.That(success2, Is.False);

            // Final balance should be 150, not 70
            var finalBalance = int.Parse(ToString(m_store.Get(ToBytes("account"))!));
            Assert.That(finalBalance, Is.EqualTo(150));
        }

        #endregion
    }
}

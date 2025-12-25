using NUnit.Framework;
using OutWit.Database.Core.Builder;
using OutWit.Database.Core.Providers;

namespace OutWit.Database.Core.Tests.Providers
{
    /// <summary>
    /// Tests for automatic storage type detection.
    /// </summary>
    [TestFixture]
    public class StorageDetectorTests
    {
        #region Fields

        private string m_testDir = null!;

        #endregion

        #region Setup

        [SetUp]
        public void Setup()
        {
            m_testDir = Path.Combine(Path.GetTempPath(), "WitDB_Detector_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_testDir);
        }

        [TearDown]
        public void TearDown()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            
            if (Directory.Exists(m_testDir))
            {
                try { Directory.Delete(m_testDir, true); } catch { }
            }
        }

        #endregion

        #region BTree Detection Tests

        [Test]
        public void DetectsBTreeFileTest()
        {
            var path = Path.Combine(m_testDir, "btree.db");
            
            using (var db = WitDatabase.Create(path))
            {
                db.Put("key"u8, "value"u8);
            }

            var result = StorageDetector.Detect(path);
            
            Assert.That(result.Exists, Is.True);
            Assert.That(result.IsDirectory, Is.False);
            Assert.That(result.StoreType, Is.EqualTo("btree"));
            Assert.That(result.RequiresPassword, Is.False);
        }

        [Test]
        public void DetectsEncryptedBTreeTest()
        {
            var path = Path.Combine(m_testDir, "encrypted.db");
            
            using (var db = WitDatabase.Create(path, "password"))
            {
                db.Put("key"u8, "value"u8);
            }

            var result = StorageDetector.Detect(path);
            
            Assert.That(result.Exists, Is.True);
            Assert.That(result.IsDirectory, Is.False);
            Assert.That(result.RequiresPassword, Is.True);
            Assert.That(result.StoreType, Is.EqualTo("btree")); // Assume BTree for encrypted files
        }

        #endregion

        #region LSM Detection Tests

        [Test]
        public void DetectsLsmDirectoryTest()
        {
            var lsmDir = Path.Combine(m_testDir, "lsm_db");
            
            using (var db = new WitDatabaseBuilder()
                .WithLsmTree(lsmDir)
                .WithTransactions()
                .Build())
            {
                db.Put("key"u8, "value"u8);
            }

            var result = StorageDetector.Detect(lsmDir);
            
            Assert.That(result.Exists, Is.True);
            Assert.That(result.IsDirectory, Is.True);
            Assert.That(result.StoreType, Is.EqualTo("lsm"));
        }

        [Test]
        public void DetectsLsmWithWalOnlyTest()
        {
            var lsmDir = Path.Combine(m_testDir, "lsm_wal");
            
            // Create LSM with small memtable so data stays in WAL
            using (var db = new WitDatabaseBuilder()
                .WithLsmTree(lsmDir, opts =>
                {
                    opts.MemTableSizeLimit = 10 * 1024 * 1024; // Large so no flush
                })
                .Build())
            {
                db.Put("key"u8, "value"u8);
                // Don't flush - data stays in WAL
            }

            var result = StorageDetector.Detect(lsmDir);
            
            Assert.That(result.Exists, Is.True);
            Assert.That(result.IsDirectory, Is.True);
            Assert.That(result.StoreType, Is.EqualTo("lsm"));
        }

        [Test]
        public void DetectsLsmWithSstFilesTest()
        {
            var lsmDir = Path.Combine(m_testDir, "lsm_sst");
            
            using (var db = new WitDatabaseBuilder()
                .WithLsmTree(lsmDir, opts =>
                {
                    opts.MemTableSizeLimit = 1024; // Small so triggers flush
                })
                .Build())
            {
                // Write enough to trigger flush
                for (int i = 0; i < 100; i++)
                {
                    db.Put(System.Text.Encoding.UTF8.GetBytes($"key{i}"), new byte[100]);
                }
            }

            var result = StorageDetector.Detect(lsmDir);
            
            Assert.That(result.Exists, Is.True);
            Assert.That(result.IsDirectory, Is.True);
            Assert.That(result.StoreType, Is.EqualTo("lsm"));
        }

        #endregion

        #region Open Auto-Detection Tests

        [Test]
        public void OpenAutoDetectsLsmTest()
        {
            var lsmDir = Path.Combine(m_testDir, "lsm_open");
            
            using (var db = new WitDatabaseBuilder()
                .WithLsmTree(lsmDir)
                .Build())
            {
                db.Put("key"u8, "value"u8);
            }

            // Open should auto-detect LSM
            using (var db = WitDatabase.Open(lsmDir))
            {
                Assert.That(db.Get("key"u8), Is.EqualTo("value"u8.ToArray()));
            }
        }

        [Test]
        public void OpenAutoDetectsBTreeTest()
        {
            var path = Path.Combine(m_testDir, "btree_open.db");
            
            using (var db = WitDatabase.Create(path))
            {
                db.Put("key"u8, "value"u8);
            }

            using (var db = WitDatabase.Open(path))
            {
                Assert.That(db.Get("key"u8), Is.EqualTo("value"u8.ToArray()));
            }
        }

        [Test]
        public void OpenEncryptedLsmWithPasswordTest()
        {
            var lsmDir = Path.Combine(m_testDir, "lsm_encrypted");
            
            using (var db = new WitDatabaseBuilder()
                .WithLsmTree(lsmDir)
                .WithEncryption("secret")
                .Build())
            {
                db.Put("key"u8, "value"u8);
            }

            using (var db = WitDatabase.Open(lsmDir, "secret"))
            {
                Assert.That(db.Get("key"u8), Is.EqualTo("value"u8.ToArray()));
            }
        }

        [Test]
        public void CreateOrOpenDetectsExistingLsmTest()
        {
            var lsmDir = Path.Combine(m_testDir, "lsm_createopen");
            
            // First call creates LSM
            using (var db = new WitDatabaseBuilder()
                .WithLsmTree(lsmDir)
                .Build())
            {
                db.Put("key"u8, "value"u8);
            }

            // CreateOrOpen should detect and open LSM
            using (var db = WitDatabase.CreateOrOpen(lsmDir))
            {
                Assert.That(db.Get("key"u8), Is.EqualTo("value"u8.ToArray()));
            }
        }

        #endregion

        #region Edge Cases

        [Test]
        public void DetectNonExistentPathTest()
        {
            var result = StorageDetector.Detect(Path.Combine(m_testDir, "nonexistent"));
            
            Assert.That(result.Exists, Is.False);
        }

        [Test]
        public void DetectEmptyDirectoryTest()
        {
            var emptyDir = Path.Combine(m_testDir, "empty");
            Directory.CreateDirectory(emptyDir);

            var result = StorageDetector.Detect(emptyDir);
            
            Assert.That(result.Exists, Is.True);
            Assert.That(result.IsDirectory, Is.True);
            Assert.That(result.StoreType, Is.Null); // Unknown - empty directory
        }

        [Test]
        public void DetectSmallFileTest()
        {
            var path = Path.Combine(m_testDir, "small.db");
            File.WriteAllBytes(path, new byte[10]); // Too small to be valid

            var result = StorageDetector.Detect(path);
            
            Assert.That(result.Exists, Is.True);
            Assert.That(result.IsDirectory, Is.False);
            Assert.That(result.StoreType, Is.Null); // Unknown - too small
        }

        [Test]
        public void OpenNonExistentThrowsTest()
        {
            var path = Path.Combine(m_testDir, "nonexistent.db");
            
            Assert.Throws<FileNotFoundException>(() => WitDatabase.Open(path));
        }

        #endregion

        #region GetDatabaseInfo Tests

        [Test]
        public void GetDatabaseInfoForLsmTest()
        {
            var lsmDir = Path.Combine(m_testDir, "lsm_info");
            
            using (var db = new WitDatabaseBuilder()
                .WithLsmTree(lsmDir)
                .Build())
            {
                db.Put("key"u8, "value"u8);
            }

            var info = WitDatabase.GetDatabaseInfo(lsmDir);
            
            Assert.That(info.Exists, Is.True);
            Assert.That(info.IsDirectory, Is.True);
            Assert.That(info.StoreType, Is.EqualTo("lsm"));
        }

        [Test]
        public void GetDatabaseInfoForBTreeTest()
        {
            var path = Path.Combine(m_testDir, "btree_info.db");
            
            using (var db = WitDatabase.Create(path))
            {
                db.Put("key"u8, "value"u8);
            }

            var info = WitDatabase.GetDatabaseInfo(path);
            
            Assert.That(info.Exists, Is.True);
            Assert.That(info.IsDirectory, Is.False);
            Assert.That(info.StoreType, Is.EqualTo("btree"));
            Assert.That(info.HasTransactions, Is.True);
        }

        #endregion
    }
}

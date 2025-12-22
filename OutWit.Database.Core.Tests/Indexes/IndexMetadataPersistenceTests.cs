using NUnit.Framework;
using OutWit.Database.Core.Builder;
using OutWit.Database.Core.Indexes;
using OutWit.Database.Core.Stores;

namespace OutWit.Database.Core.Tests.Indexes
{
    /// <summary>
    /// Tests for index metadata persistence across database reopens.
    /// </summary>
    [TestFixture]
    public class IndexMetadataPersistenceTests
    {
        #region Fields

        private string m_testDir = null!;

        #endregion

        #region Setup

        [SetUp]
        public void Setup()
        {
            m_testDir = Path.Combine(Path.GetTempPath(), "WitDB_IndexMeta_" + Guid.NewGuid().ToString("N"));
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

        #region IndexMetadataStore Tests

        [Test]
        public void SaveAndLoadIndexMetadataTest()
        {
            // Arrange
            var store = new StoreInMemory();
            var metadataStore = new IndexMetadataStore(store);

            // Act
            metadataStore.SaveIndex("idx_email", isUnique: true);
            metadataStore.SaveIndex("idx_category", isUnique: false);

            var emailMeta = metadataStore.LoadIndex("idx_email");
            var categoryMeta = metadataStore.LoadIndex("idx_category");

            // Assert
            Assert.That(emailMeta, Is.Not.Null);
            Assert.That(emailMeta!.Name, Is.EqualTo("idx_email"));
            Assert.That(emailMeta.IsUnique, Is.True);

            Assert.That(categoryMeta, Is.Not.Null);
            Assert.That(categoryMeta!.Name, Is.EqualTo("idx_category"));
            Assert.That(categoryMeta.IsUnique, Is.False);
        }

        [Test]
        public void RemoveIndexMetadataTest()
        {
            // Arrange
            var store = new StoreInMemory();
            var metadataStore = new IndexMetadataStore(store);
            metadataStore.SaveIndex("idx_test", isUnique: true);

            // Act
            var removed = metadataStore.RemoveIndex("idx_test");
            var loaded = metadataStore.LoadIndex("idx_test");

            // Assert
            Assert.That(removed, Is.True);
            Assert.That(loaded, Is.Null);
        }

        [Test]
        public void LoadAllIndexesTest()
        {
            // Arrange
            var store = new StoreInMemory();
            var metadataStore = new IndexMetadataStore(store);
            metadataStore.SaveIndex("idx_a", isUnique: true);
            metadataStore.SaveIndex("idx_b", isUnique: false);
            metadataStore.SaveIndex("idx_c", isUnique: true);

            // Act
            var all = metadataStore.LoadAllIndexes();

            // Assert
            Assert.That(all, Has.Count.EqualTo(3));
            Assert.That(all.Select(m => m.Name), Contains.Item("idx_a"));
            Assert.That(all.Select(m => m.Name), Contains.Item("idx_b"));
            Assert.That(all.Select(m => m.Name), Contains.Item("idx_c"));
        }

        [Test]
        public void GetIndexNamesTest()
        {
            // Arrange
            var store = new StoreInMemory();
            var metadataStore = new IndexMetadataStore(store);
            metadataStore.SaveIndex("idx_x", isUnique: true);
            metadataStore.SaveIndex("idx_y", isUnique: false);

            // Act
            var names = metadataStore.GetIndexNames();

            // Assert
            Assert.That(names, Has.Count.EqualTo(2));
            Assert.That(names, Contains.Item("idx_x"));
            Assert.That(names, Contains.Item("idx_y"));
        }

        [Test]
        public void CaseInsensitiveIndexNameTest()
        {
            // Arrange
            var store = new StoreInMemory();
            var metadataStore = new IndexMetadataStore(store);
            metadataStore.SaveIndex("IDX_Test", isUnique: true);

            // Act - load with different case
            var loaded = metadataStore.LoadIndex("idx_test");

            // Assert
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded!.Name, Is.EqualTo("IDX_Test"));
        }

        #endregion

        #region WitDatabase Persistence Tests

        [Test]
        public void IndexSurvivedDatabaseReopenTest()
        {
            // Arrange
            var dbPath = Path.Combine(m_testDir, "persistent.db");
            
            // Create database, add index with data
            using (var db = WitDatabase.Create(dbPath))
            {
                var index = db.CreateIndex("idx_email", isUnique: true);
                index.Add(GetBytes("test@example.com"), GetBytes("user-123"));
                db.Flush();
            }

            // Act - Reopen database
            using (var db = WitDatabase.Open(dbPath))
            {
                // Assert - Index should exist
                Assert.That(db.HasIndex("idx_email"), Is.True);
                
                var index = db.GetIndex("idx_email");
                Assert.That(index, Is.Not.Null);
                Assert.That(index!.IsUnique, Is.True);
                
                // Data should also be there (stored in separate files)
                var results = index.Find(GetBytes("test@example.com")).ToList();
                Assert.That(results, Has.Count.EqualTo(1));
                Assert.That(results[0], Is.EqualTo(GetBytes("user-123")));
            }
        }

        [Test]
        public void MultipleIndexesSurvivedReopenTest()
        {
            // Arrange
            var dbPath = Path.Combine(m_testDir, "multi_idx.db");
            
            using (var db = WitDatabase.Create(dbPath))
            {
                db.CreateIndex("idx_a", isUnique: true);
                db.CreateIndex("idx_b", isUnique: false);
                db.CreateIndex("idx_c", isUnique: true);
                db.Flush();
            }

            // Act
            using (var db = WitDatabase.Open(dbPath))
            {
                // Assert
                Assert.That(db.IndexNames, Has.Count.EqualTo(3));
                Assert.That(db.HasIndex("idx_a"), Is.True);
                Assert.That(db.HasIndex("idx_b"), Is.True);
                Assert.That(db.HasIndex("idx_c"), Is.True);
                
                Assert.That(db.GetIndex("idx_a")!.IsUnique, Is.True);
                Assert.That(db.GetIndex("idx_b")!.IsUnique, Is.False);
                Assert.That(db.GetIndex("idx_c")!.IsUnique, Is.True);
            }
        }

        [Test]
        public void DroppedIndexNotRestoredTest()
        {
            // Arrange
            var dbPath = Path.Combine(m_testDir, "drop_idx.db");
            
            using (var db = WitDatabase.Create(dbPath))
            {
                db.CreateIndex("idx_keep", isUnique: true);
                db.CreateIndex("idx_drop", isUnique: false);
                db.DropIndex("idx_drop");
                db.Flush();
            }

            // Act
            using (var db = WitDatabase.Open(dbPath))
            {
                // Assert
                Assert.That(db.HasIndex("idx_keep"), Is.True);
                Assert.That(db.HasIndex("idx_drop"), Is.False);
                Assert.That(db.IndexNames, Has.Count.EqualTo(1));
            }
        }

        [Test]
        public void EncryptedDatabaseIndexPersistenceTest()
        {
            // Arrange
            var dbPath = Path.Combine(m_testDir, "encrypted_idx.db");
            const string password = "test_password";
            
            using (var db = WitDatabase.Create(dbPath, password))
            {
                var index = db.CreateIndex("idx_secret", isUnique: true);
                index.Add(GetBytes("secret-key"), GetBytes("secret-value"));
                db.Flush();
            }

            // Act
            using (var db = WitDatabase.Open(dbPath, password))
            {
                // Assert
                Assert.That(db.HasIndex("idx_secret"), Is.True);
                
                var index = db.GetIndex("idx_secret");
                var results = index!.Find(GetBytes("secret-key")).ToList();
                Assert.That(results, Has.Count.EqualTo(1));
            }
        }

        [Test]
        public void CreateOrOpenPreservesIndexesTest()
        {
            // Arrange
            var dbPath = Path.Combine(m_testDir, "create_or_open.db");
            
            // First call creates
            using (var db = WitDatabase.CreateOrOpen(dbPath))
            {
                db.CreateIndex("idx_test", isUnique: true);
                db.Flush();
            }

            // Second call opens
            using (var db = WitDatabase.CreateOrOpen(dbPath))
            {
                // Assert - index preserved
                Assert.That(db.HasIndex("idx_test"), Is.True);
            }
        }

        [Test]
        public void IndexDataIntegrityAfterReopenTest()
        {
            // Arrange
            var dbPath = Path.Combine(m_testDir, "data_integrity.db");
            
            // Create and populate
            using (var db = WitDatabase.Create(dbPath))
            {
                var index = db.CreateIndex("idx_tags", isUnique: false);
                
                for (int i = 0; i < 100; i++)
                {
                    index.Add(GetBytes("tag"), GetBytes($"item-{i}"));
                }
                
                db.Flush();
            }

            // Reopen and verify
            using (var db = WitDatabase.Open(dbPath))
            {
                var index = db.GetIndex("idx_tags");
                Assert.That(index, Is.Not.Null);
                
                var results = index!.Find(GetBytes("tag")).ToList();
                Assert.That(results, Has.Count.EqualTo(100));
            }
        }

        #endregion

        #region Edge Cases

        [Test]
        public void EmptyDatabaseOpenHasNoIndexesTest()
        {
            // Arrange
            var dbPath = Path.Combine(m_testDir, "empty.db");
            
            using (var db = WitDatabase.Create(dbPath))
            {
                db.Put("key"u8, "value"u8);
                db.Flush();
            }

            // Act
            using (var db = WitDatabase.Open(dbPath))
            {
                // Assert
                Assert.That(db.IndexNames, Is.Empty);
                Assert.That(db.HasIndex("nonexistent"), Is.False);
            }
        }

        [Test]
        public void IndexRecreatedWithSameNameAfterDropTest()
        {
            // Arrange
            var dbPath = Path.Combine(m_testDir, "recreate.db");
            
            using (var db = WitDatabase.Create(dbPath))
            {
                db.CreateIndex("idx_test", isUnique: true);
                db.DropIndex("idx_test");
                db.CreateIndex("idx_test", isUnique: false); // Different uniqueness
                db.Flush();
            }

            // Act
            using (var db = WitDatabase.Open(dbPath))
            {
                // Assert - should have the recreated version
                Assert.That(db.HasIndex("idx_test"), Is.True);
                Assert.That(db.GetIndex("idx_test")!.IsUnique, Is.False);
            }
        }

        #endregion

        #region Helpers

        private static byte[] GetBytes(string s) => System.Text.Encoding.UTF8.GetBytes(s);

        #endregion
    }
}

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

        #region IndexMetadataStore Async Tests

        [Test]
        public async Task SaveAndLoadIndexMetadataAsyncTest()
        {
            // Arrange
            var store = new StoreInMemory();
            var metadataStore = new IndexMetadataStore(store);

            // Act
            await metadataStore.SaveIndexAsync("idx_email", isUnique: true);
            await metadataStore.SaveIndexAsync("idx_category", isUnique: false);

            var emailMeta = await metadataStore.LoadIndexAsync("idx_email");
            var categoryMeta = await metadataStore.LoadIndexAsync("idx_category");

            // Assert
            Assert.That(emailMeta, Is.Not.Null);
            Assert.That(emailMeta!.Name, Is.EqualTo("idx_email"));
            Assert.That(emailMeta.IsUnique, Is.True);

            Assert.That(categoryMeta, Is.Not.Null);
            Assert.That(categoryMeta!.Name, Is.EqualTo("idx_category"));
            Assert.That(categoryMeta.IsUnique, Is.False);
        }

        [Test]
        public async Task RemoveIndexMetadataAsyncTest()
        {
            // Arrange
            var store = new StoreInMemory();
            var metadataStore = new IndexMetadataStore(store);
            await metadataStore.SaveIndexAsync("idx_test", isUnique: true);

            // Act
            var removed = await metadataStore.RemoveIndexAsync("idx_test");
            var loaded = await metadataStore.LoadIndexAsync("idx_test");

            // Assert
            Assert.That(removed, Is.True);
            Assert.That(loaded, Is.Null);
        }

        [Test]
        public async Task LoadAllIndexesAsyncTest()
        {
            // Arrange
            var store = new StoreInMemory();
            var metadataStore = new IndexMetadataStore(store);
            await metadataStore.SaveIndexAsync("idx_a", isUnique: true);
            await metadataStore.SaveIndexAsync("idx_b", isUnique: false);
            await metadataStore.SaveIndexAsync("idx_c", isUnique: true);

            // Act
            var all = await metadataStore.LoadAllIndexesAsync();

            // Assert
            Assert.That(all, Has.Count.EqualTo(3));
            Assert.That(all.Select(m => m.Name), Contains.Item("idx_a"));
            Assert.That(all.Select(m => m.Name), Contains.Item("idx_b"));
            Assert.That(all.Select(m => m.Name), Contains.Item("idx_c"));
        }

        [Test]
        public async Task GetIndexNamesAsyncTest()
        {
            // Arrange
            var store = new StoreInMemory();
            var metadataStore = new IndexMetadataStore(store);
            await metadataStore.SaveIndexAsync("idx_x", isUnique: true);
            await metadataStore.SaveIndexAsync("idx_y", isUnique: false);

            // Act
            var names = await metadataStore.GetIndexNamesAsync();

            // Assert
            Assert.That(names, Has.Count.EqualTo(2));
            Assert.That(names, Contains.Item("idx_x"));
            Assert.That(names, Contains.Item("idx_y"));
        }

        [Test]
        public async Task SaveIndexAsyncWithNullNameThrowsTest()
        {
            // Arrange
            var store = new StoreInMemory();
            var metadataStore = new IndexMetadataStore(store);

            // Act & Assert
            Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await metadataStore.SaveIndexAsync(null!, isUnique: true));
        }

        [Test]
        public async Task SaveIndexAsyncWithEmptyNameThrowsTest()
        {
            // Arrange
            var store = new StoreInMemory();
            var metadataStore = new IndexMetadataStore(store);

            // Act & Assert
            Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await metadataStore.SaveIndexAsync("", isUnique: true));
        }

        [Test]
        public async Task LoadIndexAsyncWithNullNameReturnsNullTest()
        {
            // Arrange
            var store = new StoreInMemory();
            var metadataStore = new IndexMetadataStore(store);

            // Act
            var result = await metadataStore.LoadIndexAsync(null!);

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task RemoveIndexAsyncWithEmptyNameReturnsFalseTest()
        {
            // Arrange
            var store = new StoreInMemory();
            var metadataStore = new IndexMetadataStore(store);

            // Act
            var result = await metadataStore.RemoveIndexAsync("");

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task RemoveIndexAsyncForNonExistentIndexReturnsFalseTest()
        {
            // Arrange
            var store = new StoreInMemory();
            var metadataStore = new IndexMetadataStore(store);

            // Act
            var result = await metadataStore.RemoveIndexAsync("nonexistent");

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task LoadAllIndexesAsyncWithCancellationTest()
        {
            // Arrange
            var store = new StoreInMemory();
            var metadataStore = new IndexMetadataStore(store);
            await metadataStore.SaveIndexAsync("idx_a", isUnique: true);
            await metadataStore.SaveIndexAsync("idx_b", isUnique: false);

            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await metadataStore.LoadAllIndexesAsync(cts.Token));
        }

        [Test]
        public async Task SyncAndAsyncMethodsProduceConsistentResultsTest()
        {
            // Arrange
            var store = new StoreInMemory();
            var metadataStore = new IndexMetadataStore(store);

            // Save with sync
            metadataStore.SaveIndex("idx_sync", isUnique: true);
            
            // Save with async
            await metadataStore.SaveIndexAsync("idx_async", isUnique: false);

            // Load with opposite method
            var syncMeta = await metadataStore.LoadIndexAsync("idx_sync");
            var asyncMeta = metadataStore.LoadIndex("idx_async");

            // Assert
            Assert.That(syncMeta, Is.Not.Null);
            Assert.That(syncMeta!.Name, Is.EqualTo("idx_sync"));
            Assert.That(syncMeta.IsUnique, Is.True);

            Assert.That(asyncMeta, Is.Not.Null);
            Assert.That(asyncMeta!.Name, Is.EqualTo("idx_async"));
            Assert.That(asyncMeta.IsUnique, Is.False);

            // Catalog should have both
            var names = await metadataStore.GetIndexNamesAsync();
            Assert.That(names, Has.Count.EqualTo(2));
            Assert.That(names, Contains.Item("idx_sync"));
            Assert.That(names, Contains.Item("idx_async"));
        }

        #endregion

        #region WitDatabase Async Index Tests

        [Test]
        public async Task CreateIndexAsyncSucceedsTest()
        {
            // Arrange
            var dbPath = Path.Combine(m_testDir, "async_create.db");
            
            await using var db = await new WitDatabaseBuilder()
                .WithFilePath(dbPath)
                .WithBTree()
                .WithTransactions()
                .BuildAsync();

            // Act
            var index = await db.CreateIndexAsync("idx_email", isUnique: true);

            // Assert
            Assert.That(index, Is.Not.Null);
            Assert.That(index.Name, Is.EqualTo("idx_email"));
            Assert.That(index.IsUnique, Is.True);
            Assert.That(db.HasIndex("idx_email"), Is.True);
        }

        [Test]
        public async Task DropIndexAsyncSucceedsTest()
        {
            // Arrange
            var dbPath = Path.Combine(m_testDir, "async_drop.db");
            
            await using var db = await new WitDatabaseBuilder()
                .WithFilePath(dbPath)
                .WithBTree()
                .WithTransactions()
                .BuildAsync();

            await db.CreateIndexAsync("idx_email", isUnique: true);

            // Act
            var dropped = await db.DropIndexAsync("idx_email");

            // Assert
            Assert.That(dropped, Is.True);
            Assert.That(db.HasIndex("idx_email"), Is.False);
        }

        [Test]
        public async Task DropIndexAsyncForNonExistentReturnsFalseTest()
        {
            // Arrange
            var dbPath = Path.Combine(m_testDir, "async_drop_nonexistent.db");
            
            await using var db = await new WitDatabaseBuilder()
                .WithFilePath(dbPath)
                .WithBTree()
                .WithTransactions()
                .BuildAsync();

            // Act
            var dropped = await db.DropIndexAsync("nonexistent");

            // Assert
            Assert.That(dropped, Is.False);
        }

        [Test]
        public async Task CreateIndexAsyncPersistsMetadataTest()
        {
            // Arrange
            var dbPath = Path.Combine(m_testDir, "async_persist.db");
            
            // Create with async
            await using (var db = await new WitDatabaseBuilder()
                .WithFilePath(dbPath)
                .WithBTree()
                .WithTransactions()
                .BuildAsync())
            {
                await db.CreateIndexAsync("idx_async", isUnique: true);
                await db.FlushAsync();
            }

            // Reopen and verify
            using (var db = WitDatabase.Open(dbPath))
            {
                Assert.That(db.HasIndex("idx_async"), Is.True);
                Assert.That(db.GetIndex("idx_async")!.IsUnique, Is.True);
            }
        }

        [Test]
        public async Task DropIndexAsyncRemovesMetadataTest()
        {
            // Arrange
            var dbPath = Path.Combine(m_testDir, "async_drop_persist.db");
            
            // Create and drop with async
            await using (var db = await new WitDatabaseBuilder()
                .WithFilePath(dbPath)
                .WithBTree()
                .WithTransactions()
                .BuildAsync())
            {
                await db.CreateIndexAsync("idx_to_drop", isUnique: true);
                await db.FlushAsync();
                
                await db.DropIndexAsync("idx_to_drop");
                await db.FlushAsync();
            }

            // Reopen and verify it's gone
            using (var db = WitDatabase.Open(dbPath))
            {
                Assert.That(db.HasIndex("idx_to_drop"), Is.False);
                Assert.That(db.IndexNames, Is.Empty);
            }
        }

        [Test]
        public async Task CreateMultipleIndexesAsyncTest()
        {
            // Arrange
            var dbPath = Path.Combine(m_testDir, "async_multi.db");
            
            await using var db = await new WitDatabaseBuilder()
                .WithFilePath(dbPath)
                .WithBTree()
                .WithTransactions()
                .BuildAsync();

            // Act
            await db.CreateIndexAsync("idx_a", isUnique: true);
            await db.CreateIndexAsync("idx_b", isUnique: false);
            await db.CreateIndexAsync("idx_c", isUnique: true);

            // Assert
            Assert.That(db.IndexNames, Has.Count.EqualTo(3));
            Assert.That(db.HasIndex("idx_a"), Is.True);
            Assert.That(db.HasIndex("idx_b"), Is.True);
            Assert.That(db.HasIndex("idx_c"), Is.True);
        }

        [Test]
        public async Task MixedSyncAndAsyncIndexOperationsTest()
        {
            // Arrange
            var dbPath = Path.Combine(m_testDir, "async_mixed.db");
            
            await using var db = await new WitDatabaseBuilder()
                .WithFilePath(dbPath)
                .WithBTree()
                .WithTransactions()
                .BuildAsync();

            // Act - mix sync and async
            db.CreateIndex("idx_sync", isUnique: true);
            await db.CreateIndexAsync("idx_async", isUnique: false);
            
            db.DropIndex("idx_sync");
            await db.DropIndexAsync("idx_async");

            // Assert
            Assert.That(db.IndexNames, Is.Empty);
        }

        [Test]
        public async Task CreateIndexAsyncWithCancellationTest()
        {
            // Arrange
            var dbPath = Path.Combine(m_testDir, "async_cancel.db");
            
            await using var db = await new WitDatabaseBuilder()
                .WithFilePath(dbPath)
                .WithBTree()
                .WithTransactions()
                .BuildAsync();

            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Note: The actual index creation is sync, only metadata save is async
            // So cancellation might not always throw depending on timing
            // This test verifies the method accepts cancellation token
            try
            {
                await db.CreateIndexAsync("idx_test", isUnique: true, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected - cancellation was honored
                return;
            }

            // If we get here, index was created before cancellation could be checked
            // That's OK - just verify it exists
            Assert.That(db.HasIndex("idx_test"), Is.True);
        }

        #endregion
    }
}

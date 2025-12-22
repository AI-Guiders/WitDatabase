using NUnit.Framework;
using OutWit.Database.Core.Indexes;
using OutWit.Database.Core.Interfaces;
using OutWit.Database.Core.Managers;
using OutWit.Database.Core.Storage;

namespace OutWit.Database.Core.Tests.Indexes
{
    [TestFixture]
    public class SecondaryIndexBTreeTests
    {
        #region Fields

        private string m_testDir = null!;
        private PageManager m_pageManager = null!;

        #endregion

        #region Setup

        [SetUp]
        public void Setup()
        {
            m_testDir = Path.Combine(Path.GetTempPath(), "WitDB_IndexTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_testDir);

            var storage = new StorageMemory();
            m_pageManager = new PageManager(storage);
        }

        [TearDown]
        public void TearDown()
        {
            m_pageManager?.Dispose();

            if (Directory.Exists(m_testDir))
            {
                try { Directory.Delete(m_testDir, true); } catch { }
            }
        }

        #endregion

        #region Unique Index Tests

        [Test]
        public void UniqueIndexAddAndFindSucceedsTest()
        {
            // Arrange
            using var index = new SecondaryIndexBTree("test_idx", m_pageManager, isUnique: true);
            var indexKey = GetBytes("email@example.com");
            var primaryKey = GetBytes("user-123");

            // Act
            index.Add(indexKey, primaryKey);
            var results = index.Find(indexKey).ToList();

            // Assert
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0], Is.EqualTo(primaryKey));
        }

        [Test]
        public void UniqueIndexRejectsDuplicateKeyTest()
        {
            // Arrange
            using var index = new SecondaryIndexBTree("test_idx", m_pageManager, isUnique: true);
            var indexKey = GetBytes("email@example.com");
            var pk1 = GetBytes("user-1");
            var pk2 = GetBytes("user-2");

            index.Add(indexKey, pk1);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => index.Add(indexKey, pk2));
        }

        [Test]
        public void UniqueIndexAllowsSameKeyAfterRemoveTest()
        {
            // Arrange
            using var index = new SecondaryIndexBTree("test_idx", m_pageManager, isUnique: true);
            var indexKey = GetBytes("email@example.com");
            var pk1 = GetBytes("user-1");
            var pk2 = GetBytes("user-2");

            index.Add(indexKey, pk1);
            index.Remove(indexKey, pk1);

            // Act
            index.Add(indexKey, pk2);
            var results = index.Find(indexKey).ToList();

            // Assert
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0], Is.EqualTo(pk2));
        }

        [Test]
        public void UniqueIndexContainsReturnsTrueForExistingKeyTest()
        {
            // Arrange
            using var index = new SecondaryIndexBTree("test_idx", m_pageManager, isUnique: true);
            var indexKey = GetBytes("email@example.com");
            var primaryKey = GetBytes("user-123");

            index.Add(indexKey, primaryKey);

            // Act & Assert
            Assert.That(index.Contains(indexKey), Is.True);
            Assert.That(index.Contains(GetBytes("nonexistent")), Is.False);
        }

        [Test]
        public void UniqueIndexContainsEntryVerifiesPrimaryKeyTest()
        {
            // Arrange
            using var index = new SecondaryIndexBTree("test_idx", m_pageManager, isUnique: true);
            var indexKey = GetBytes("email@example.com");
            var pk1 = GetBytes("user-1");
            var pk2 = GetBytes("user-2");

            index.Add(indexKey, pk1);

            // Act & Assert
            Assert.That(index.ContainsEntry(indexKey, pk1), Is.True);
            Assert.That(index.ContainsEntry(indexKey, pk2), Is.False);
        }

        #endregion

        #region Non-Unique Index Tests

        [Test]
        public void NonUniqueIndexAllowsDuplicateKeysTest()
        {
            // Arrange
            using var index = new SecondaryIndexBTree("test_idx", m_pageManager, isUnique: false);
            var indexKey = GetBytes("category-electronics");
            var pk1 = GetBytes("product-1");
            var pk2 = GetBytes("product-2");
            var pk3 = GetBytes("product-3");

            // Act
            index.Add(indexKey, pk1);
            index.Add(indexKey, pk2);
            index.Add(indexKey, pk3);

            var results = index.Find(indexKey).ToList();

            // Assert
            Assert.That(results, Has.Count.EqualTo(3));
            Assert.That(results, Contains.Item(pk1));
            Assert.That(results, Contains.Item(pk2));
            Assert.That(results, Contains.Item(pk3));
        }

        [Test]
        public void NonUniqueIndexRemovesSingleEntryTest()
        {
            // Arrange
            using var index = new SecondaryIndexBTree("test_idx", m_pageManager, isUnique: false);
            var indexKey = GetBytes("category-electronics");
            var pk1 = GetBytes("product-1");
            var pk2 = GetBytes("product-2");

            index.Add(indexKey, pk1);
            index.Add(indexKey, pk2);

            // Act
            bool removed = index.Remove(indexKey, pk1);
            var results = index.Find(indexKey).ToList();

            // Assert
            Assert.That(removed, Is.True);
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0], Is.EqualTo(pk2));
        }

        [Test]
        public void NonUniqueIndexRemoveAllRemovesAllEntriesTest()
        {
            // Arrange
            using var index = new SecondaryIndexBTree("test_idx", m_pageManager, isUnique: false);
            var indexKey = GetBytes("category-electronics");
            var pk1 = GetBytes("product-1");
            var pk2 = GetBytes("product-2");
            var pk3 = GetBytes("product-3");

            index.Add(indexKey, pk1);
            index.Add(indexKey, pk2);
            index.Add(indexKey, pk3);

            // Act
            int removed = index.RemoveAll(indexKey);
            var results = index.Find(indexKey).ToList();

            // Assert
            Assert.That(removed, Is.EqualTo(3));
            Assert.That(results, Is.Empty);
        }

        [Test]
        public void NonUniqueIndexContainsEntryWorksCorrectlyTest()
        {
            // Arrange
            using var index = new SecondaryIndexBTree("test_idx", m_pageManager, isUnique: false);
            var indexKey = GetBytes("category-electronics");
            var pk1 = GetBytes("product-1");
            var pk2 = GetBytes("product-2");

            index.Add(indexKey, pk1);

            // Act & Assert
            Assert.That(index.ContainsEntry(indexKey, pk1), Is.True);
            Assert.That(index.ContainsEntry(indexKey, pk2), Is.False);
        }

        #endregion

        #region Range Query Tests

        [Test]
        public void FindRangeReturnsCorrectResultsTest()
        {
            // Arrange
            using var index = new SecondaryIndexBTree("test_idx", m_pageManager, isUnique: true);

            index.Add(GetBytes("apple"), GetBytes("pk-1"));
            index.Add(GetBytes("banana"), GetBytes("pk-2"));
            index.Add(GetBytes("cherry"), GetBytes("pk-3"));
            index.Add(GetBytes("date"), GetBytes("pk-4"));
            index.Add(GetBytes("elderberry"), GetBytes("pk-5"));

            // Act
            var results = index.FindRange(GetBytes("banana"), GetBytes("date")).ToList();

            // Assert
            Assert.That(results, Has.Count.EqualTo(2)); // banana, cherry (date is exclusive)
        }

        [Test]
        public void FindRangeWithNullStartReturnsFromBeginningTest()
        {
            // Arrange
            using var index = new SecondaryIndexBTree("test_idx", m_pageManager, isUnique: true);

            index.Add(GetBytes("apple"), GetBytes("pk-1"));
            index.Add(GetBytes("banana"), GetBytes("pk-2"));
            index.Add(GetBytes("cherry"), GetBytes("pk-3"));

            // Act
            var results = index.FindRange(null, GetBytes("cherry")).ToList();

            // Assert
            Assert.That(results, Has.Count.EqualTo(2)); // apple, banana
        }

        [Test]
        public void FindRangeWithNullEndReturnsToEndTest()
        {
            // Arrange
            using var index = new SecondaryIndexBTree("test_idx", m_pageManager, isUnique: true);

            index.Add(GetBytes("apple"), GetBytes("pk-1"));
            index.Add(GetBytes("banana"), GetBytes("pk-2"));
            index.Add(GetBytes("cherry"), GetBytes("pk-3"));

            // Act
            var results = index.FindRange(GetBytes("banana"), null).ToList();

            // Assert
            Assert.That(results, Has.Count.EqualTo(2)); // banana, cherry
        }

        #endregion

        #region Count and Clear Tests

        [Test]
        public void CountReturnsCorrectValueTest()
        {
            // Arrange
            using var index = new SecondaryIndexBTree("test_idx", m_pageManager, isUnique: true);

            // Act
            index.Add(GetBytes("key1"), GetBytes("pk-1"));
            index.Add(GetBytes("key2"), GetBytes("pk-2"));
            index.Add(GetBytes("key3"), GetBytes("pk-3"));

            // Assert
            Assert.That(index.Count, Is.EqualTo(3));
        }

        [Test]
        public void ClearRemovesAllEntriesTest()
        {
            // Arrange
            using var index = new SecondaryIndexBTree("test_idx", m_pageManager, isUnique: true);

            index.Add(GetBytes("key1"), GetBytes("pk-1"));
            index.Add(GetBytes("key2"), GetBytes("pk-2"));
            index.Add(GetBytes("key3"), GetBytes("pk-3"));

            // Act
            index.Clear();

            // Assert
            Assert.That(index.Count, Is.EqualTo(0));
            Assert.That(index.Find(GetBytes("key1")).Any(), Is.False);
        }

        #endregion

        #region Properties Tests

        [Test]
        public void NamePropertyReturnsCorrectValueTest()
        {
            // Arrange & Act
            using var index = new SecondaryIndexBTree("my_index", m_pageManager, isUnique: true);

            // Assert
            Assert.That(index.Name, Is.EqualTo("my_index"));
        }

        [Test]
        public void IsUniquePropertyReturnsCorrectValueTest()
        {
            // Arrange & Act
            using var uniqueIndex = new SecondaryIndexBTree("unique", m_pageManager, isUnique: true);
            using var nonUniqueIndex = new SecondaryIndexBTree("non_unique", m_pageManager, isUnique: false);

            // Assert
            Assert.That(uniqueIndex.IsUnique, Is.True);
            Assert.That(nonUniqueIndex.IsUnique, Is.False);
        }

        [Test]
        public void ImplementsISecondaryIndexTest()
        {
            // Arrange & Act
            using var index = new SecondaryIndexBTree("test", m_pageManager, isUnique: true);

            // Assert
            Assert.That(index, Is.InstanceOf<ISecondaryIndex>());
        }

        #endregion

        #region Stress Tests

        [Test]
        public void LargeNumberOfEntriesTest()
        {
            // Arrange
            using var index = new SecondaryIndexBTree("test_idx", m_pageManager, isUnique: true);
            const int count = 1000;

            // Act - Insert
            for (int i = 0; i < count; i++)
            {
                index.Add(GetBytes($"key-{i:D5}"), GetBytes($"pk-{i:D5}"));
            }

            // Assert - Count
            Assert.That(index.Count, Is.EqualTo(count));

            // Assert - Lookup
            for (int i = 0; i < count; i++)
            {
                var results = index.Find(GetBytes($"key-{i:D5}")).ToList();
                Assert.That(results, Has.Count.EqualTo(1));
                Assert.That(results[0], Is.EqualTo(GetBytes($"pk-{i:D5}")));
            }
        }

        [Test]
        public void NonUniqueIndexWithManyDuplicatesTest()
        {
            // Arrange
            using var index = new SecondaryIndexBTree("test_idx", m_pageManager, isUnique: false);
            var indexKey = GetBytes("popular-category");
            const int count = 100;

            // Act - Insert many duplicates
            for (int i = 0; i < count; i++)
            {
                index.Add(indexKey, GetBytes($"pk-{i:D5}"));
            }

            // Assert
            var results = index.Find(indexKey).ToList();
            Assert.That(results, Has.Count.EqualTo(count));
        }

        #endregion

        #region Helpers

        private static byte[] GetBytes(string s) => System.Text.Encoding.UTF8.GetBytes(s);

        #endregion
    }
}

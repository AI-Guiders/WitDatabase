using OutWit.Database.Core.LSM;
using TextEncoding = System.Text.Encoding;

namespace OutWit.Database.Core.Tests.LSM
{
    /// <summary>
    /// Unit tests for SSTable components (SSTableBuilder and SSTableReader).
    /// </summary>
    [TestFixture]
    public class SSTableTests : IDisposable
    {
        private string m_testDir = null!;

        [SetUp]
        public void SetUp()
        {
            m_testDir = Path.Combine(Path.GetTempPath(), $"sst_test_{Guid.NewGuid():N}");
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

        private static byte[] ToBytes(string s) => TextEncoding.UTF8.GetBytes(s);

        #region Basic SSTable Tests

        [Test]
        public void SSTableBuildAndReadTest()
        {
            var sstPath = Path.Combine(m_testDir, "test.sst");
        
            SSTableInfo info;
            using (var builder = new SSTableBuilder(sstPath))
            {
                builder.Add(ToBytes("a"), ToBytes("1"));
                builder.Add(ToBytes("b"), ToBytes("2"));
                builder.Add(ToBytes("c"), ToBytes("3"));
                info = builder.Finish();
            }

            Assert.That(info.EntryCount, Is.EqualTo(3));
            Assert.That(info.HasBloomFilter, Is.True);

            using var reader = new SSTableReader(sstPath);
        
            Assert.That(reader.TryGet(ToBytes("a"), out var value), Is.True);
            Assert.That(value, Is.EqualTo(ToBytes("1")));

            Assert.That(reader.TryGet(ToBytes("b"), out value), Is.True);
            Assert.That(value, Is.EqualTo(ToBytes("2")));

            Assert.That(reader.TryGet(ToBytes("c"), out value), Is.True);
            Assert.That(value, Is.EqualTo(ToBytes("3")));

            Assert.That(reader.TryGet(ToBytes("d"), out _), Is.False);
        }

        [Test]
        public void SSTableScanAllEntriesTest()
        {
            var sstPath = Path.Combine(m_testDir, "scan.sst");
        
            using (var builder = new SSTableBuilder(sstPath))
            {
                for (int i = 0; i < 100; i++)
                {
                    var key = BitConverter.GetBytes(i);
                    var value = BitConverter.GetBytes(i * 10);
                    builder.Add(key, value);
                }
                builder.Finish();
            }

            using var reader = new SSTableReader(sstPath);
            var entries = reader.Scan().ToList();
        
            Assert.That(entries.Count, Is.EqualTo(100));
            Assert.That(reader.EntryCount, Is.EqualTo(100));
        }

        [Test]
        public void SSTableHandlesTombstonesTest()
        {
            var sstPath = Path.Combine(m_testDir, "tombstone.sst");
        
            using (var builder = new SSTableBuilder(sstPath))
            {
                builder.Add(ToBytes("a"), ToBytes("1"));
                builder.Add(ToBytes("b"), null); // Tombstone
                builder.Add(ToBytes("c"), ToBytes("3"));
                builder.Finish();
            }

            using var reader = new SSTableReader(sstPath);
        
            Assert.That(reader.TryGet(ToBytes("b"), out var value), Is.True);
            Assert.That(value, Is.Null); // Tombstone
        }

        [Test]
        public void SSTableScanWithRangeTest()
        {
            var sstPath = Path.Combine(m_testDir, "range.sst");
        
            using (var builder = new SSTableBuilder(sstPath))
            {
                builder.Add(ToBytes("a"), ToBytes("1"));
                builder.Add(ToBytes("b"), ToBytes("2"));
                builder.Add(ToBytes("c"), ToBytes("3"));
                builder.Add(ToBytes("d"), ToBytes("4"));
                builder.Add(ToBytes("e"), ToBytes("5"));
                builder.Finish();
            }

            using var reader = new SSTableReader(sstPath);
            var entries = reader.Scan(ToBytes("b"), ToBytes("d")).ToList();
        
            Assert.That(entries.Count, Is.EqualTo(2));
            Assert.That(entries[0].Key, Is.EqualTo(ToBytes("b")));
            Assert.That(entries[1].Key, Is.EqualTo(ToBytes("c")));
        }

        [Test]
        public void SSTablePropertiesTest()
        {
            var sstPath = Path.Combine(m_testDir, "props.sst");
        
            using (var builder = new SSTableBuilder(sstPath))
            {
                builder.Add(ToBytes("key"), ToBytes("value"));
                builder.Finish();
            }

            using var reader = new SSTableReader(sstPath);
        
            Assert.That(reader.FilePath, Is.EqualTo(sstPath));
            Assert.That(reader.EntryCount, Is.EqualTo(1));
            Assert.That(reader.FileSize, Is.GreaterThan(0));
            Assert.That(reader.IsEncrypted, Is.False);
            Assert.That(reader.HasBloomFilter, Is.True);
        }

        #endregion

        #region Bloom Filter Tests

        [Test]
        public void SSTableBloomFilterSkipsNonExistentKeysTest()
        {
            var sstPath = Path.Combine(m_testDir, "bloom.sst");
        
            using (var builder = new SSTableBuilder(sstPath))
            {
                for (int i = 0; i < 100; i++)
                {
                    builder.Add(BitConverter.GetBytes(i), BitConverter.GetBytes(i * 10));
                }
                var info = builder.Finish();
                Assert.That(info.HasBloomFilter, Is.True);
            }

            using var reader = new SSTableReader(sstPath);
            Assert.That(reader.HasBloomFilter, Is.True);

            // Existing keys should be found
            Assert.That(reader.TryGet(BitConverter.GetBytes(50), out var value), Is.True);
            Assert.That(BitConverter.ToInt32(value!), Is.EqualTo(500));

            // Non-existent keys should be filtered by Bloom filter
            Assert.That(reader.TryGet(BitConverter.GetBytes(999), out _), Is.False);
        }

        [Test]
        public void SSTableBloomFilterIntegrationTest()
        {
            var sstPath = Path.Combine(m_testDir, "bloom_large.sst");
            const int keyCount = 10000;
        
            // Keys must be added in sorted order
            var keys = Enumerable.Range(0, keyCount)
                .Select(i => BitConverter.GetBytes(i))
                .OrderBy(k => k, Comparer<byte[]>.Create((a, b) => a.AsSpan().SequenceCompareTo(b)))
                .ToArray();
        
            using (var builder = new SSTableBuilder(sstPath))
            {
                foreach (var key in keys)
                {
                    builder.Add(key, key);
                }
                builder.Finish();
            }

            using var reader = new SSTableReader(sstPath);
            Assert.That(reader.HasBloomFilter, Is.True);
            Assert.That(reader.EntryCount, Is.EqualTo(keyCount));

            // All existing keys should be found
            for (int i = 0; i < keyCount; i += 100)
            {
                var key = BitConverter.GetBytes(i);
                Assert.That(reader.TryGet(key, out _), Is.True, $"Key {i} should be found");
            }

            // Non-existent keys should not be found
            for (int i = keyCount; i < keyCount + 100; i++)
            {
                var key = BitConverter.GetBytes(i);
                Assert.That(reader.TryGet(key, out _), Is.False, $"Key {i} should NOT be found");
            }
        }

        #endregion

        #region Block Size Tests

        [Test]
        public void SSTableCustomBlockSizeTest()
        {
            var sstPath = Path.Combine(m_testDir, "blocksize.sst");
        
            // Small block size to create multiple blocks
            using (var builder = new SSTableBuilder(sstPath, targetBlockSize: 64))
            {
                for (int i = 0; i < 50; i++)
                {
                    builder.Add(ToBytes($"key{i:D3}"), ToBytes($"value{i:D10}"));
                }
                var info = builder.Finish();
                Assert.That(info.BlockCount, Is.GreaterThan(1));
            }

            using var reader = new SSTableReader(sstPath);
            
            // Should still find all entries
            Assert.That(reader.TryGet(ToBytes("key000"), out _), Is.True);
            Assert.That(reader.TryGet(ToBytes("key025"), out _), Is.True);
            Assert.That(reader.TryGet(ToBytes("key049"), out _), Is.True);
        }

        [Test]
        public void SSTableLargeValuesTest()
        {
            var sstPath = Path.Combine(m_testDir, "large.sst");
            var largeValue = new byte[50_000];
            new Random(42).NextBytes(largeValue);
        
            // Keys must be in sorted order
            using (var builder = new SSTableBuilder(sstPath))
            {
                builder.Add(ToBytes("large"), largeValue);
                builder.Add(ToBytes("small"), ToBytes("tiny"));
                builder.Finish();
            }

            using var reader = new SSTableReader(sstPath);
            
            Assert.That(reader.TryGet(ToBytes("large"), out var value), Is.True);
            Assert.That(value, Is.EqualTo(largeValue));
            
            Assert.That(reader.TryGet(ToBytes("small"), out value), Is.True);
            Assert.That(value, Is.EqualTo(ToBytes("tiny")));
        }

        #endregion

        #region SSTableInfo Tests

        [Test]
        public void SSTableInfoContainsCorrectMetadataTest()
        {
            var sstPath = Path.Combine(m_testDir, "info.sst");
        
            SSTableInfo info;
            using (var builder = new SSTableBuilder(sstPath))
            {
                for (int i = 0; i < 100; i++)
                {
                    builder.Add(BitConverter.GetBytes(i), BitConverter.GetBytes(i));
                }
                info = builder.Finish();
            }

            Assert.That(info.FilePath, Is.EqualTo(sstPath));
            Assert.That(info.EntryCount, Is.EqualTo(100));
            Assert.That(info.FileSize, Is.GreaterThan(0));
            Assert.That(info.BlockCount, Is.GreaterThan(0));
            Assert.That(info.Encrypted, Is.False);
            Assert.That(info.HasBloomFilter, Is.True);
        }

        #endregion
    }
}

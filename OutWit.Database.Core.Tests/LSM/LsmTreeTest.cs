using OutWit.Database.Core.LSM;
using OutWit.Database.Core.Stores;
using TextEncoding = System.Text.Encoding;

namespace OutWit.Database.Core.Tests.LSM
{
    /// <summary>
    /// Unit tests for LSM-Tree components.
    /// </summary>
    [TestFixture]
    public class LsmTreeTest : IDisposable
    {
        private string m_testDir = null!;

        [SetUp]
        public void SetUp()
        {
            m_testDir = Path.Combine(Path.GetTempPath(), $"lsm_test_{Guid.NewGuid():N}");
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

        #region MemTable Tests

        [Test]
        public void MemTablePutAndGetTest()
        {
            using var memTable = new MemTable();
            var key = ToBytes("key1");
            var value = ToBytes("value1");

            memTable.Put(key, value);
        
            Assert.That(memTable.TryGet(key, out var result), Is.True);
            Assert.That(result, Is.EqualTo(value));
        }

        [Test]
        public void MemTableDeleteReturnsTombstoneTest()
        {
            using var memTable = new MemTable();
            var key = ToBytes("key1");
            var value = ToBytes("value1");

            memTable.Put(key, value);
            memTable.Delete(key);
        
            Assert.That(memTable.TryGet(key, out var result), Is.True);
            Assert.That(result, Is.Null); // Tombstone
        }

        [Test]
        public void MemTableScanReturnsSortedRangeTest()
        {
            using var memTable = new MemTable();
        
            memTable.Put(ToBytes("c"), ToBytes("3"));
            memTable.Put(ToBytes("a"), ToBytes("1"));
            memTable.Put(ToBytes("b"), ToBytes("2"));

            var results = memTable.Scan(null, null).ToList();
        
            Assert.That(results.Count, Is.EqualTo(3));
            Assert.That(results[0].Key, Is.EqualTo(ToBytes("a")));
            Assert.That(results[1].Key, Is.EqualTo(ToBytes("b")));
            Assert.That(results[2].Key, Is.EqualTo(ToBytes("c")));
        }

        [Test]
        public void MemTableCountTest()
        {
            using var memTable = new MemTable();
        
            Assert.That(memTable.Count, Is.EqualTo(0));
        
            memTable.Put(ToBytes("a"), ToBytes("1"));
            memTable.Put(ToBytes("b"), ToBytes("2"));
        
            Assert.That(memTable.Count, Is.EqualTo(2));
        }

        [Test]
        public void MemTableApproximateSizeTest()
        {
            using var memTable = new MemTable();
        
            Assert.That(memTable.ApproximateSize, Is.EqualTo(0));
        
            memTable.Put(ToBytes("key"), ToBytes("value"));
        
            Assert.That(memTable.ApproximateSize, Is.GreaterThan(0));
        }

        #endregion

        #region WriteAheadLog Tests

        [Test]
        public void WalAppendAndReplayTest()
        {
            var walPath = Path.Combine(m_testDir, "test.wal");
        
            using (var wal = new WriteAheadLog(walPath, createNew: true))
            {
                wal.AppendPut(ToBytes("key1"), ToBytes("value1"));
                wal.AppendPut(ToBytes("key2"), ToBytes("value2"));
                wal.Sync();
            }

            var entries = new List<(byte[] Key, byte[] Value)>();
            using (var wal = new WriteAheadLog(walPath))
            {
                wal.Replay(
                    onPut: (k, v) => entries.Add((k, v)),
                    onDelete: _ => { }
                );
            }

            Assert.That(entries.Count, Is.EqualTo(2));
            Assert.That(entries[0].Key, Is.EqualTo(ToBytes("key1")));
            Assert.That(entries[0].Value, Is.EqualTo(ToBytes("value1")));
        }

        [Test]
        public void WalRecoversDeleteEntriesTest()
        {
            var walPath = Path.Combine(m_testDir, "test.wal");
        
            using (var wal = new WriteAheadLog(walPath, createNew: true))
            {
                wal.AppendPut(ToBytes("key1"), ToBytes("value1"));
                wal.AppendDelete(ToBytes("key1"));
                wal.Sync();
            }

            var puts = new List<byte[]>();
            var deletes = new List<byte[]>();
            using (var wal = new WriteAheadLog(walPath))
            {
                wal.Replay(
                    onPut: (k, v) => puts.Add(k),
                    onDelete: k => deletes.Add(k)
                );
            }

            Assert.That(puts.Count, Is.EqualTo(1));
            Assert.That(deletes.Count, Is.EqualTo(1));
        }

        [Test]
        public void WalTruncateTest()
        {
            var walPath = Path.Combine(m_testDir, "test.wal");
        
            using var wal = new WriteAheadLog(walPath, createNew: true);
            wal.AppendPut(ToBytes("key1"), ToBytes("value1"));
            wal.Sync();
        
            Assert.That(wal.Size, Is.GreaterThan(12)); // More than just header
        
            wal.Truncate();
        
            Assert.That(wal.Size, Is.EqualTo(12)); // Just header (Magic + EntryCounter)
        }

        #endregion

        #region SSTable Tests

        [Test]
        public void SSTableBuildAndReadTest()
        {
            var sstPath = Path.Combine(m_testDir, "test.sst");
        
            using (var builder = new SSTableBuilder(sstPath))
            {
                builder.Add(ToBytes("a"), ToBytes("1"));
                builder.Add(ToBytes("b"), ToBytes("2"));
                builder.Add(ToBytes("c"), ToBytes("3"));
                builder.Finish();
            }

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
            var sstPath = Path.Combine(m_testDir, "test.sst");
        
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
            var sstPath = Path.Combine(m_testDir, "test.sst");
        
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

        #endregion

        #region BloomFilter Tests

        [Test]
        public void BloomFilterAddAndContainsTest()
        {
            var filter = new BloomFilter(100);
        
            filter.Add(ToBytes("test1"));
            filter.Add(ToBytes("test2"));
        
            Assert.That(filter.MightContain(ToBytes("test1")), Is.True);
            Assert.That(filter.MightContain(ToBytes("test2")), Is.True);
        }

        [Test]
        public void BloomFilterSerializeRoundtripTest()
        {
            var filter = new BloomFilter(100);
            filter.Add(ToBytes("key1"));
            filter.Add(ToBytes("key2"));
        
            var bytes = filter.ToBytes();
            var restored = new BloomFilter(bytes, filter.HashCount, filter.Size);
        
            Assert.That(restored.MightContain(ToBytes("key1")), Is.True);
            Assert.That(restored.MightContain(ToBytes("key2")), Is.True);
        }

        [Test]
        public void BloomFilterClearTest()
        {
            var filter = new BloomFilter(100);
            filter.Add(ToBytes("test"));
        
            Assert.That(filter.MightContain(ToBytes("test")), Is.True);
        
            filter.Clear();
        
            Assert.That(filter.MightContain(ToBytes("test")), Is.False);
        }

        #endregion

        #region LsmTree Integration Tests

        [Test]
        public void LsmTreePutAndGetTest()
        {
            var options = new LsmOptions { EnableWal = false };
            using var tree = new LsmTreeStore(m_testDir, options);

            tree.Put(ToBytes("key1"), ToBytes("value1"));
        
            var result = tree.Get(ToBytes("key1"));
            Assert.That(result, Is.EqualTo(ToBytes("value1")));
        }

        [Test]
        public void LsmTreeDeleteTest()
        {
            var options = new LsmOptions { EnableWal = false };
            using var tree = new LsmTreeStore(m_testDir, options);

            tree.Put(ToBytes("key1"), ToBytes("value1"));
            tree.Delete(ToBytes("key1"));
        
            var result = tree.Get(ToBytes("key1"));
            Assert.That(result, Is.Null);
        }

        [Test]
        public void LsmTreeScanReturnsSortedResultsTest()
        {
            var options = new LsmOptions { EnableWal = false };
            using var tree = new LsmTreeStore(m_testDir, options);

            tree.Put(ToBytes("c"), ToBytes("3"));
            tree.Put(ToBytes("a"), ToBytes("1"));
            tree.Put(ToBytes("b"), ToBytes("2"));

            var results = tree.Scan(null, null).ToList();
        
            Assert.That(results.Count, Is.EqualTo(3));
            Assert.That(results[0].Key, Is.EqualTo(ToBytes("a")));
            Assert.That(results[1].Key, Is.EqualTo(ToBytes("b")));
            Assert.That(results[2].Key, Is.EqualTo(ToBytes("c")));
        }

        [Test]
        public void LsmTreeFlushToSSTableTest()
        {
            var dir = Path.Combine(m_testDir, "flush");
            var options = new LsmOptions 
            { 
                EnableWal = false,
                MemTableSizeLimit = 100
            };
        
            using (var tree = new LsmTreeStore(dir, options))
            {
                for (int i = 0; i < 50; i++)
                {
                    var key = ToBytes($"key{i:D5}");
                    var value = ToBytes($"value{i}");
                    tree.Put(key, value);
                }
                tree.Flush();
            
                Assert.That(tree.SSTableCount, Is.GreaterThan(0));
            }

            // Reopen and verify
            using var tree2 = new LsmTreeStore(dir, new LsmOptions { EnableWal = false });
            var result = tree2.Get(ToBytes("key00000"));
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.EqualTo(ToBytes("value0")));
        }

        [Test]
        public void LsmTreeWalRecoveryTest()
        {
            var dir = Path.Combine(m_testDir, "wal");
            var options = new LsmOptions { EnableWal = true, SyncWrites = true };
        
            using (var tree = new LsmTreeStore(dir, options))
            {
                tree.Put(ToBytes("key1"), ToBytes("value1"));
                tree.Put(ToBytes("key2"), ToBytes("value2"));
            }

            // Reopen - should recover from WAL
            using var tree2 = new LsmTreeStore(dir, options);
        
            Assert.That(tree2.Get(ToBytes("key1")), Is.EqualTo(ToBytes("value1")));
            Assert.That(tree2.Get(ToBytes("key2")), Is.EqualTo(ToBytes("value2")));
        }

        [Test]
        public void LsmTreeMultipleSSTablesTest()
        {
            var dir = Path.Combine(m_testDir, "multi");
            var options = new LsmOptions 
            { 
                EnableWal = false,
                MemTableSizeLimit = 50
            };
        
            using var tree = new LsmTreeStore(dir, options);
        
            for (int i = 0; i < 100; i++)
            {
                tree.Put(BitConverter.GetBytes(i), BitConverter.GetBytes(i * 10));
            }
            tree.Flush();
        
            // Verify all keys are accessible
            for (int i = 0; i < 100; i++)
            {
                var result = tree.Get(BitConverter.GetBytes(i));
                Assert.That(result, Is.Not.Null);
                Assert.That(BitConverter.ToInt32(result!), Is.EqualTo(i * 10));
            }
        }

        #endregion
    }
}

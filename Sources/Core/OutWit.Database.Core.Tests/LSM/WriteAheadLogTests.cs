using OutWit.Database.Core.LSM;
using TextEncoding = System.Text.Encoding;

namespace OutWit.Database.Core.Tests.LSM
{
    /// <summary>
    /// Unit tests for WriteAheadLog component.
    /// </summary>
    [TestFixture]
    public class WriteAheadLogTests : IDisposable
    {
        private string m_testDir = null!;

        [SetUp]
        public void SetUp()
        {
            m_testDir = Path.Combine(Path.GetTempPath(), $"wal_test_{Guid.NewGuid():N}");
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

        [Test]
        public void WalMultipleEntriesReplayOrderTest()
        {
            var walPath = Path.Combine(m_testDir, "order.wal");
            
            using (var wal = new WriteAheadLog(walPath, createNew: true))
            {
                for (int i = 0; i < 100; i++)
                {
                    wal.AppendPut(ToBytes($"key{i:D3}"), ToBytes($"value{i}"));
                }
                wal.Sync();
            }

            var entries = new List<string>();
            using (var wal = new WriteAheadLog(walPath))
            {
                wal.Replay(
                    onPut: (k, _) => entries.Add(TextEncoding.UTF8.GetString(k)),
                    onDelete: _ => { }
                );
            }

            Assert.That(entries.Count, Is.EqualTo(100));
            for (int i = 0; i < 100; i++)
            {
                Assert.That(entries[i], Is.EqualTo($"key{i:D3}"));
            }
        }

        [Test]
        public void WalReopenAndAppendTest()
        {
            var walPath = Path.Combine(m_testDir, "reopen.wal");
            
            // First session
            using (var wal = new WriteAheadLog(walPath, createNew: true))
            {
                wal.AppendPut(ToBytes("key1"), ToBytes("value1"));
                wal.Sync();
            }

            // Reopen and append
            using (var wal = new WriteAheadLog(walPath, createNew: false))
            {
                wal.AppendPut(ToBytes("key2"), ToBytes("value2"));
                wal.Sync();
            }

            // Verify both entries
            var entries = new List<string>();
            using (var wal = new WriteAheadLog(walPath))
            {
                wal.Replay(
                    onPut: (k, _) => entries.Add(TextEncoding.UTF8.GetString(k)),
                    onDelete: _ => { }
                );
            }

            Assert.That(entries, Is.EqualTo(new[] { "key1", "key2" }));
        }

        [Test]
        public void WalIsEncryptedPropertyTest()
        {
            var walPath = Path.Combine(m_testDir, "plain.wal");
            
            using var wal = new WriteAheadLog(walPath, createNew: true);
            
            Assert.That(wal.IsEncrypted, Is.False);
            Assert.That(wal.FilePath, Is.EqualTo(walPath));
        }

        [Test]
        public void WalLargeValueTest()
        {
            var walPath = Path.Combine(m_testDir, "large.wal");
            var largeValue = new byte[100_000];
            new Random(42).NextBytes(largeValue);
            
            using (var wal = new WriteAheadLog(walPath, createNew: true))
            {
                wal.AppendPut(ToBytes("large"), largeValue);
                wal.Sync();
            }

            byte[]? recoveredValue = null;
            using (var wal = new WriteAheadLog(walPath))
            {
                wal.Replay(
                    onPut: (_, v) => recoveredValue = v,
                    onDelete: _ => { }
                );
            }

            Assert.That(recoveredValue, Is.EqualTo(largeValue));
        }
    }
}

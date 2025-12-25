using OutWit.Database.Core.LSM;
using TextEncoding = System.Text.Encoding;

namespace OutWit.Database.Core.Tests.LSM
{
    /// <summary>
    /// Unit tests for Compactor component.
    /// </summary>
    [TestFixture]
    public class CompactorTests : IDisposable
    {
        private string m_testDir = null!;

        [SetUp]
        public void SetUp()
        {
            m_testDir = Path.Combine(Path.GetTempPath(), $"compactor_test_{Guid.NewGuid():N}");
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
        public void CompactorMergesSSTablesTest()
        {
            var sst1Path = Path.Combine(m_testDir, "sst_001.sst");
            var sst2Path = Path.Combine(m_testDir, "sst_002.sst");
            var outputPath = Path.Combine(m_testDir, "sst_003.sst");

            using (var builder = new SSTableBuilder(sst1Path))
            {
                builder.Add(ToBytes("a"), ToBytes("1"));
                builder.Add(ToBytes("c"), ToBytes("3"));
                builder.Finish();
            }

            using (var builder = new SSTableBuilder(sst2Path))
            {
                builder.Add(ToBytes("b"), ToBytes("2"));
                builder.Add(ToBytes("c"), ToBytes("3_new")); // Override
                builder.Finish();
            }

            var compactor = new Compactor(m_testDir);
            var result = compactor.Compact([sst1Path, sst2Path], outputPath);

            Assert.That(result.InputFiles, Is.EqualTo(2));
            Assert.That(result.OutputEntries, Is.EqualTo(3)); // a, b, c

            // Verify merged content
            using var reader = new SSTableReader(outputPath);
            Assert.That(reader.TryGet(ToBytes("a"), out var v), Is.True);
            Assert.That(v, Is.EqualTo(ToBytes("1")));

            Assert.That(reader.TryGet(ToBytes("b"), out v), Is.True);
            Assert.That(v, Is.EqualTo(ToBytes("2")));

            Assert.That(reader.TryGet(ToBytes("c"), out v), Is.True);
            Assert.That(v, Is.EqualTo(ToBytes("3_new"))); // Newest wins
        }

        [Test]
        public void CompactorRemovesTombstonesTest()
        {
            var sst1Path = Path.Combine(m_testDir, "sst_001.sst");
            var sst2Path = Path.Combine(m_testDir, "sst_002.sst");
            var outputPath = Path.Combine(m_testDir, "sst_003.sst");

            using (var builder = new SSTableBuilder(sst1Path))
            {
                builder.Add(ToBytes("a"), ToBytes("1"));
                builder.Add(ToBytes("b"), ToBytes("2"));
                builder.Finish();
            }

            using (var builder = new SSTableBuilder(sst2Path))
            {
                builder.Add(ToBytes("a"), null); // Tombstone
                builder.Finish();
            }

            var compactor = new Compactor(m_testDir);
            var result = compactor.Compact([sst1Path, sst2Path], outputPath);

            Assert.That(result.TombstonesRemoved, Is.EqualTo(1));
            Assert.That(result.OutputEntries, Is.EqualTo(1)); // Only b

            using var reader = new SSTableReader(outputPath);
            Assert.That(reader.TryGet(ToBytes("a"), out _), Is.False); // Removed
            Assert.That(reader.TryGet(ToBytes("b"), out var v), Is.True);
            Assert.That(v, Is.EqualTo(ToBytes("2")));
        }

        [Test]
        public void CompactorEmptyInputTest()
        {
            var outputPath = Path.Combine(m_testDir, "empty.sst");
            
            var compactor = new Compactor(m_testDir);
            var result = compactor.Compact([], outputPath);
            
            Assert.That(result.InputFiles, Is.EqualTo(0));
            Assert.That(result.OutputEntries, Is.EqualTo(0));
        }

        [Test]
        public void CompactorSingleFileTest()
        {
            var inputPath = Path.Combine(m_testDir, "input.sst");
            var outputPath = Path.Combine(m_testDir, "output.sst");

            using (var builder = new SSTableBuilder(inputPath))
            {
                builder.Add(ToBytes("a"), ToBytes("1"));
                builder.Add(ToBytes("b"), ToBytes("2"));
                builder.Finish();
            }

            var compactor = new Compactor(m_testDir);
            var result = compactor.Compact([inputPath], outputPath);

            Assert.That(result.InputFiles, Is.EqualTo(1));
            Assert.That(result.OutputEntries, Is.EqualTo(2));
        }

        [Test]
        public void CompactorManyFilesTest()
        {
            var inputPaths = new List<string>();
            
            // Create 10 SSTables with overlapping keys
            for (int file = 0; file < 10; file++)
            {
                var path = Path.Combine(m_testDir, $"sst_{file:D3}.sst");
                inputPaths.Add(path);
                
                using var builder = new SSTableBuilder(path);
                for (int key = 0; key < 100; key++)
                {
                    builder.Add(BitConverter.GetBytes(key), BitConverter.GetBytes(file * 1000 + key));
                }
                builder.Finish();
            }

            var outputPath = Path.Combine(m_testDir, "merged.sst");
            var compactor = new Compactor(m_testDir);
            var result = compactor.Compact(inputPaths, outputPath);

            Assert.That(result.InputFiles, Is.EqualTo(10));
            Assert.That(result.OutputEntries, Is.EqualTo(100)); // Unique keys

            // Verify newest values (from file 9)
            using var reader = new SSTableReader(outputPath);
            for (int key = 0; key < 100; key += 10)
            {
                Assert.That(reader.TryGet(BitConverter.GetBytes(key), out var v), Is.True);
                Assert.That(BitConverter.ToInt32(v!), Is.EqualTo(9 * 1000 + key));
            }
        }

        [Test]
        public void CompactorCustomBlockSizeTest()
        {
            var sst1Path = Path.Combine(m_testDir, "sst_001.sst");
            var outputPath = Path.Combine(m_testDir, "output.sst");

            using (var builder = new SSTableBuilder(sst1Path))
            {
                for (int i = 0; i < 100; i++)
                {
                    builder.Add(BitConverter.GetBytes(i), BitConverter.GetBytes(i));
                }
                builder.Finish();
            }

            var compactor = new Compactor(m_testDir, blockSize: 128); // Small block size
            var result = compactor.Compact([sst1Path], outputPath);

            Assert.That(result.OutputEntries, Is.EqualTo(100));
            
            using var reader = new SSTableReader(outputPath);
            Assert.That(reader.EntryCount, Is.EqualTo(100));
        }

        [Test]
        public void CompactorAllTombstonesTest()
        {
            var sst1Path = Path.Combine(m_testDir, "sst_001.sst");
            var sst2Path = Path.Combine(m_testDir, "sst_002.sst");
            var outputPath = Path.Combine(m_testDir, "output.sst");

            using (var builder = new SSTableBuilder(sst1Path))
            {
                builder.Add(ToBytes("a"), ToBytes("1"));
                builder.Add(ToBytes("b"), ToBytes("2"));
                builder.Finish();
            }

            using (var builder = new SSTableBuilder(sst2Path))
            {
                builder.Add(ToBytes("a"), null);
                builder.Add(ToBytes("b"), null);
                builder.Finish();
            }

            var compactor = new Compactor(m_testDir);
            var result = compactor.Compact([sst1Path, sst2Path], outputPath);

            Assert.That(result.TombstonesRemoved, Is.EqualTo(2));
            Assert.That(result.OutputEntries, Is.EqualTo(0));
        }

        [Test]
        public void CompactionResultContainsAllFieldsTest()
        {
            var sst1Path = Path.Combine(m_testDir, "sst_001.sst");
            var outputPath = Path.Combine(m_testDir, "output.sst");

            using (var builder = new SSTableBuilder(sst1Path))
            {
                builder.Add(ToBytes("a"), ToBytes("1"));
                builder.Add(ToBytes("b"), null); // Tombstone
                builder.Finish();
            }

            var compactor = new Compactor(m_testDir);
            var result = compactor.Compact([sst1Path], outputPath);

            Assert.That(result.InputFiles, Is.EqualTo(1));
            Assert.That(result.InputEntries, Is.EqualTo(2));
            Assert.That(result.OutputEntries, Is.EqualTo(1));
            Assert.That(result.TombstonesRemoved, Is.EqualTo(1));
            Assert.That(result.OutputFile, Is.EqualTo(outputPath));
        }
    }
}

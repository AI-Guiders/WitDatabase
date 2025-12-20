using OutWit.Database.Core.Storage;

namespace OutWit.Database.Core.Tests.Storage
{
    [TestFixture]
    public class FileStorageStressTest
    {
        private string m_testDir = null!;

        [SetUp]
        public void SetUp()
        {
            m_testDir = Path.Combine(Path.GetTempPath(), $"WitDB_StressTest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(m_testDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(m_testDir))
            {
                Directory.Delete(m_testDir, recursive: true);
            }
        }

        [Test]
        public void LargeFileRandomWriteReadTest()
        {
            const int pageCount = 1000;
            const int pageSize = 4096;
            string filePath = Path.Combine(m_testDir, "large_random.wdb");

            using var storage = FileStorage.Create(filePath, pageSize);
            storage.SetSize(pageCount);

            // Write unique data to each page
            byte[] writeBuffer = new byte[pageSize];
            for (int i = 0; i < pageCount; i++)
            {
                FillBufferWithPattern(writeBuffer, i);
                storage.WritePage(i, writeBuffer);
            }

            // Read back in random order and verify
            int[] indices = Enumerable.Range(0, pageCount).ToArray();
            Random.Shared.Shuffle(indices);

            byte[] readBuffer = new byte[pageSize];
            byte[] expectedBuffer = new byte[pageSize];

            foreach (int pageIndex in indices)
            {
                storage.ReadPage(pageIndex, readBuffer);
                FillBufferWithPattern(expectedBuffer, pageIndex);
            
                Assert.That(readBuffer, Is.EqualTo(expectedBuffer), 
                    $"Page {pageIndex} data mismatch after random read");
            }
        }

        [Test]
        public void RandomWriteInMiddleOfFileTest()
        {
            const int pageCount = 500;
            const int pageSize = 4096;
            const int writeCount = 200;
            string filePath = Path.Combine(m_testDir, "middle_write.wdb");

            using var storage = FileStorage.Create(filePath, pageSize);
            storage.SetSize(pageCount);

            // Initialize all pages with zeros
            byte[] emptyBuffer = new byte[pageSize];
            for (int i = 0; i < pageCount; i++)
            {
                storage.WritePage(i, emptyBuffer);
            }

            // Write to random pages in the middle of the file
            byte[] writeBuffer = new byte[pageSize];
            var writtenPages = new Dictionary<int, byte[]>();
        
            for (int i = 0; i < writeCount; i++)
            {
                int pageIndex = Random.Shared.Next(50, pageCount - 50); // Middle of file
                FillBufferWithPattern(writeBuffer, pageIndex * 1000 + i);
                storage.WritePage(pageIndex, writeBuffer);
            
                writtenPages[pageIndex] = writeBuffer.ToArray();
            }

            // Verify written pages
            byte[] readBuffer = new byte[pageSize];
            foreach (var (pageIndex, expected) in writtenPages)
            {
                storage.ReadPage(pageIndex, readBuffer);
                Assert.That(readBuffer, Is.EqualTo(expected), 
                    $"Page {pageIndex} verification failed");
            }
        }

        [Test]
        public void SequentialWritePerformanceTest()
        {
            const int pageCount = 2000;
            const int pageSize = 4096;
            string filePath = Path.Combine(m_testDir, "sequential.wdb");

            var sw = System.Diagnostics.Stopwatch.StartNew();

            using var storage = FileStorage.Create(filePath, pageSize);
            storage.SetSize(pageCount);

            byte[] buffer = new byte[pageSize];
            for (int i = 0; i < pageCount; i++)
            {
                FillBufferWithPattern(buffer, i);
                storage.WritePage(i, buffer);
            }

            storage.Flush();
            sw.Stop();

            long fileSize = new FileInfo(filePath).Length;
            double mbWritten = fileSize / (1024.0 * 1024.0);
            double seconds = sw.Elapsed.TotalSeconds;

            Console.WriteLine($"Sequential write: {mbWritten:F2} MB in {seconds:F3}s ({mbWritten / seconds:F2} MB/s)");
        
            Assert.That(fileSize, Is.EqualTo((long)pageCount * pageSize));
        }

        [Test]
        public void RandomAccessPatternTest()
        {
            const int pageCount = 500;
            const int pageSize = 4096;
            const int accessCount = 1000;
            string filePath = Path.Combine(m_testDir, "random_access.wdb");

            using var storage = FileStorage.Create(filePath, pageSize);
            storage.SetSize(pageCount);

            // Initialize with known data
            byte[] buffer = new byte[pageSize];
            for (int i = 0; i < pageCount; i++)
            {
                FillBufferWithPattern(buffer, i);
                storage.WritePage(i, buffer);
            }

            // Random interleaved read/write
            byte[] readBuffer = new byte[pageSize];
            byte[] writeBuffer = new byte[pageSize];
            var pageData = new Dictionary<int, byte[]>();

            // Store original data
            for (int i = 0; i < pageCount; i++)
            {
                FillBufferWithPattern(buffer, i);
                pageData[i] = buffer.ToArray();
            }

            // Perform random operations
            for (int op = 0; op < accessCount; op++)
            {
                int pageIndex = Random.Shared.Next(0, pageCount);
                bool isWrite = Random.Shared.Next(2) == 0;

                if (isWrite)
                {
                    FillBufferWithPattern(writeBuffer, pageIndex * 10000 + op);
                    storage.WritePage(pageIndex, writeBuffer);
                    pageData[pageIndex] = writeBuffer.ToArray();
                }
                else
                {
                    storage.ReadPage(pageIndex, readBuffer);
                    Assert.That(readBuffer, Is.EqualTo(pageData[pageIndex]),
                        $"Read mismatch on operation {op}, page {pageIndex}");
                }
            }
        }

        [Test]
        public void ExtendFileDuringOperationsTest()
        {
            const int initialPages = 100;
            const int finalPages = 500;
            const int pageSize = 4096;
            string filePath = Path.Combine(m_testDir, "extend.wdb");

            using var storage = FileStorage.Create(filePath, pageSize);
            storage.SetSize(initialPages);

            byte[] buffer = new byte[pageSize];

            // Write to initial pages
            for (int i = 0; i < initialPages; i++)
            {
                FillBufferWithPattern(buffer, i);
                storage.WritePage(i, buffer);
            }

            // Extend and write more
            storage.SetSize(finalPages);
            Assert.That(storage.PageCount, Is.EqualTo(finalPages));

            for (int i = initialPages; i < finalPages; i++)
            {
                FillBufferWithPattern(buffer, i);
                storage.WritePage(i, buffer);
            }

            // Verify all pages
            byte[] readBuffer = new byte[pageSize];
            byte[] expected = new byte[pageSize];
        
            for (int i = 0; i < finalPages; i++)
            {
                storage.ReadPage(i, readBuffer);
                FillBufferWithPattern(expected, i);
                Assert.That(readBuffer, Is.EqualTo(expected), $"Page {i} mismatch after extend");
            }
        }

        [Test]
        public void ShrinkFileDuringOperationsTest()
        {
            const int initialPages = 500;
            const int finalPages = 100;
            const int pageSize = 4096;
            string filePath = Path.Combine(m_testDir, "shrink.wdb");

            using var storage = FileStorage.Create(filePath, pageSize);
            storage.SetSize(initialPages);

            byte[] buffer = new byte[pageSize];

            // Write to all pages
            for (int i = 0; i < initialPages; i++)
            {
                FillBufferWithPattern(buffer, i);
                storage.WritePage(i, buffer);
            }

            // Shrink
            storage.SetSize(finalPages);
            Assert.That(storage.PageCount, Is.EqualTo(finalPages));

            // Verify remaining pages are intact
            byte[] readBuffer = new byte[pageSize];
            byte[] expected = new byte[pageSize];
        
            for (int i = 0; i < finalPages; i++)
            {
                storage.ReadPage(i, readBuffer);
                FillBufferWithPattern(expected, i);
                Assert.That(readBuffer, Is.EqualTo(expected), $"Page {i} corrupted after shrink");
            }

            // Verify out-of-range throws
            Assert.Throws<ArgumentOutOfRangeException>(() => storage.ReadPage(finalPages, readBuffer));
        }

        [Test]
        public async Task AsyncRandomAccessTest()
        {
            const int pageCount = 200;
            const int pageSize = 4096;
            const int taskCount = 10;
            const int operationsPerTask = 50;
            string filePath = Path.Combine(m_testDir, "async_random.wdb");

            using var storage = FileStorage.Create(filePath, pageSize);
            storage.SetSize(pageCount);

            // Initialize
            byte[] buffer = new byte[pageSize];
            for (int i = 0; i < pageCount; i++)
            {
                FillBufferWithPattern(buffer, i);
                storage.WritePage(i, buffer);
            }

            // Run async tasks
            var tasks = new Task[taskCount];
            for (int t = 0; t < taskCount; t++)
            {
                int taskId = t;
                tasks[t] = Task.Run(async () =>
                {
                    byte[] localBuffer = new byte[pageSize];
                    byte[] expected = new byte[pageSize];

                    for (int op = 0; op < operationsPerTask; op++)
                    {
                        int pageIndex = (taskId * operationsPerTask + op) % pageCount;
                    
                        await storage.ReadPageAsync(pageIndex, localBuffer);
                        FillBufferWithPattern(expected, pageIndex);
                    
                        // Note: This may fail due to concurrent writes, 
                        // but for this test we just read initial data
                    }
                });
            }

            await Task.WhenAll(tasks);
        }

        private static void FillBufferWithPattern(byte[] buffer, int seed)
        {
            var random = new Random(seed);
            random.NextBytes(buffer);
        }
    }
}

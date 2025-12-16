//using System.Diagnostics;
//using OutWit.Database.Core.Pages;
//using OutWit.Database.Core.Storage;

//namespace OutWit.Database.Core.Tests.Benchmarks.Storage;

//[TestFixture]
//public class StorageBenchmarkTest
//{
//    private string _testDir = null!;

//    [SetUp]
//    public void SetUp()
//    {
//        _testDir = Path.Combine(Path.GetTempPath(), $"WitDB_Benchmark_{Guid.NewGuid():N}");
//        Directory.CreateDirectory(_testDir);
//    }

//    [TearDown]
//    public void TearDown()
//    {
//        if (Directory.Exists(_testDir))
//        {
//            Directory.Delete(_testDir, recursive: true);
//        }
//    }

//    [Test]
//    [TestCase(100, 10000)]      // 100 pages (~400KB), 10K ops
//    [TestCase(1000, 10000)]     // 1000 pages (~4MB), 10K ops
//    [TestCase(10000, 10000)]    // 10K pages (~40MB), 10K ops
//    [TestCase(50000, 10000)]    // 50K pages (~200MB), 10K ops
//    public void FileStorageRandomReadBenchmark(int pageCount, int operationCount)
//    {
//        const int pageSize = 4096;
//        string filePath = Path.Combine(_testDir, $"read_benchmark_{pageCount}.wdb");

//        using var storage = FileStorage.Create(filePath, pageSize);
//        storage.SetSize(pageCount);

//        // Initialize with data
//        byte[] writeBuffer = new byte[pageSize];
//        for (int i = 0; i < pageCount; i++)
//        {
//            FillBufferWithPattern(writeBuffer, i);
//            storage.WritePage(i, writeBuffer);
//        }
//        storage.Flush();

//        // Benchmark random reads
//        byte[] readBuffer = new byte[pageSize];
//        int[] randomPages = GenerateRandomIndices(operationCount, pageCount);

//        var sw = Stopwatch.StartNew();
//        foreach (int pageIndex in randomPages)
//        {
//            storage.ReadPage(pageIndex, readBuffer);
//        }
//        sw.Stop();

//        double opsPerSecond = operationCount / sw.Elapsed.TotalSeconds;
//        double mbPerSecond = (operationCount * pageSize / (1024.0 * 1024.0)) / sw.Elapsed.TotalSeconds;
//        double avgLatencyUs = sw.Elapsed.TotalMicroseconds / operationCount;
//        double fileSizeMb = (long)pageCount * pageSize / (1024.0 * 1024.0);

//        Console.WriteLine($"=== Random READ Benchmark ===");
//        Console.WriteLine($"File size:       {fileSizeMb:F1} MB ({pageCount} pages)");
//        Console.WriteLine($"Operations:      {operationCount:N0}");
//        Console.WriteLine($"Total time:      {sw.Elapsed.TotalMilliseconds:F1} ms");
//        Console.WriteLine($"Throughput:      {opsPerSecond:N0} ops/sec");
//        Console.WriteLine($"Bandwidth:       {mbPerSecond:F1} MB/s");
//        Console.WriteLine($"Avg latency:     {avgLatencyUs:F1} µs/op");
//        Console.WriteLine();

//        Assert.Pass();
//    }

//    [Test]
//    [TestCase(100, 10000)]      // 100 pages (~400KB), 10K ops
//    [TestCase(1000, 10000)]     // 1000 pages (~4MB), 10K ops
//    [TestCase(10000, 10000)]    // 10K pages (~40MB), 10K ops
//    [TestCase(50000, 10000)]    // 50K pages (~200MB), 10K ops
//    public void FileStorageRandomWriteBenchmark(int pageCount, int operationCount)
//    {
//        const int pageSize = 4096;
//        string filePath = Path.Combine(_testDir, $"write_benchmark_{pageCount}.wdb");

//        using var storage = FileStorage.Create(filePath, pageSize);
//        storage.SetSize(pageCount);

//        // Benchmark random writes
//        byte[] writeBuffer = new byte[pageSize];
//        int[] randomPages = GenerateRandomIndices(operationCount, pageCount);

//        var sw = Stopwatch.StartNew();
//        for (int i = 0; i < operationCount; i++)
//        {
//            FillBufferWithPattern(writeBuffer, i);
//            storage.WritePage(randomPages[i], writeBuffer);
//        }
//        storage.Flush();
//        sw.Stop();

//        double opsPerSecond = operationCount / sw.Elapsed.TotalSeconds;
//        double mbPerSecond = (operationCount * pageSize / (1024.0 * 1024.0)) / sw.Elapsed.TotalSeconds;
//        double avgLatencyUs = sw.Elapsed.TotalMicroseconds / operationCount;
//        double fileSizeMb = (long)pageCount * pageSize / (1024.0 * 1024.0);

//        Console.WriteLine($"=== Random WRITE Benchmark ===");
//        Console.WriteLine($"File size:       {fileSizeMb:F1} MB ({pageCount} pages)");
//        Console.WriteLine($"Operations:      {operationCount:N0}");
//        Console.WriteLine($"Total time:      {sw.Elapsed.TotalMilliseconds:F1} ms");
//        Console.WriteLine($"Throughput:      {opsPerSecond:N0} ops/sec");
//        Console.WriteLine($"Bandwidth:       {mbPerSecond:F1} MB/s");
//        Console.WriteLine($"Avg latency:     {avgLatencyUs:F1} µs/op");
//        Console.WriteLine();

//        Assert.Pass();
//    }

//    [Test]
//    [TestCase(100, 10000)]      // 100 pages, 10K ops
//    [TestCase(1000, 10000)]     // 1000 pages, 10K ops
//    [TestCase(10000, 10000)]    // 10K pages, 10K ops
//    public void FileStorageMixedReadWriteBenchmark(int pageCount, int operationCount)
//    {
//        const int pageSize = 4096;
//        const double writeRatio = 0.2; // 20% writes, 80% reads
//        string filePath = Path.Combine(_testDir, $"mixed_benchmark_{pageCount}.wdb");

//        using var storage = FileStorage.Create(filePath, pageSize);
//        storage.SetSize(pageCount);

//        // Initialize with data
//        byte[] initBuffer = new byte[pageSize];
//        for (int i = 0; i < pageCount; i++)
//        {
//            FillBufferWithPattern(initBuffer, i);
//            storage.WritePage(i, initBuffer);
//        }
//        storage.Flush();

//        // Benchmark mixed operations
//        byte[] buffer = new byte[pageSize];
//        int[] randomPages = GenerateRandomIndices(operationCount, pageCount);
//        bool[] isWrite = new bool[operationCount];
//        int writeCount = 0;
//        for (int i = 0; i < operationCount; i++)
//        {
//            isWrite[i] = Random.Shared.NextDouble() < writeRatio;
//            if (isWrite[i]) writeCount++;
//        }

//        var sw = Stopwatch.StartNew();
//        for (int i = 0; i < operationCount; i++)
//        {
//            if (isWrite[i])
//            {
//                FillBufferWithPattern(buffer, i);
//                storage.WritePage(randomPages[i], buffer);
//            }
//            else
//            {
//                storage.ReadPage(randomPages[i], buffer);
//            }
//        }
//        storage.Flush();
//        sw.Stop();

//        double opsPerSecond = operationCount / sw.Elapsed.TotalSeconds;
//        double avgLatencyUs = sw.Elapsed.TotalMicroseconds / operationCount;
//        double fileSizeMb = (long)pageCount * pageSize / (1024.0 * 1024.0);
//        int readCount = operationCount - writeCount;

//        Console.WriteLine($"=== Mixed READ/WRITE Benchmark ===");
//        Console.WriteLine($"File size:       {fileSizeMb:F1} MB ({pageCount} pages)");
//        Console.WriteLine($"Operations:      {operationCount:N0} ({readCount} reads, {writeCount} writes)");
//        Console.WriteLine($"Total time:      {sw.Elapsed.TotalMilliseconds:F1} ms");
//        Console.WriteLine($"Throughput:      {opsPerSecond:N0} ops/sec");
//        Console.WriteLine($"Avg latency:     {avgLatencyUs:F1} µs/op");
//        Console.WriteLine();

//        Assert.Pass();
//    }

//    [Test]
//    public void MemoryStorageVsFileStorageComparison()
//    {
//        const int pageCount = 1000;
//        const int pageSize = 4096;
//        const int operationCount = 50000;
//        string filePath = Path.Combine(_testDir, "comparison.wdb");

//        Console.WriteLine($"=== MemoryStorage vs FileStorage Comparison ===");
//        Console.WriteLine($"Pages: {pageCount}, Page size: {pageSize}, Operations: {operationCount:N0}");
//        Console.WriteLine();

//        // Memory Storage
//        using (var memStorage = new MemoryStorage(pageSize, pageCount))
//        {
//            byte[] buffer = new byte[pageSize];
//            int[] randomPages = GenerateRandomIndices(operationCount, pageCount);

//            // Warm up
//            for (int i = 0; i < 1000; i++)
//            {
//                memStorage.ReadPage(randomPages[i], buffer);
//            }

//            var sw = Stopwatch.StartNew();
//            foreach (int page in randomPages)
//            {
//                memStorage.ReadPage(page, buffer);
//            }
//            sw.Stop();

//            double memOps = operationCount / sw.Elapsed.TotalSeconds;
//            Console.WriteLine($"MemoryStorage:   {memOps:N0} ops/sec (random read)");
//        }

//        // File Storage
//        using (var fileStorage = FileStorage.Create(filePath, pageSize))
//        {
//            fileStorage.SetSize(pageCount);

//            byte[] buffer = new byte[pageSize];
//            for (int i = 0; i < pageCount; i++)
//            {
//                FillBufferWithPattern(buffer, i);
//                fileStorage.WritePage(i, buffer);
//            }
//            fileStorage.Flush();

//            int[] randomPages = GenerateRandomIndices(operationCount, pageCount);

//            // Warm up (primes file cache)
//            for (int i = 0; i < 1000; i++)
//            {
//                fileStorage.ReadPage(randomPages[i], buffer);
//            }

//            var sw = Stopwatch.StartNew();
//            foreach (int page in randomPages)
//            {
//                fileStorage.ReadPage(page, buffer);
//            }
//            sw.Stop();

//            double fileOps = operationCount / sw.Elapsed.TotalSeconds;
//            Console.WriteLine($"FileStorage:     {fileOps:N0} ops/sec (random read)");
//        }

//        Console.WriteLine();
//        Assert.Pass();
//    }

//    [Test]
//    public void PageCacheEffectBenchmark()
//    {
//        const int pageSize = 4096;
//        const int operationCount = 10000;
        
//        // Test different cache sizes
//        int[] cacheSizes = [10, 50, 100, 500, 1000];
//        const int totalPages = 1000;
        
//        Console.WriteLine($"=== PageCache Effect Benchmark ===");
//        Console.WriteLine($"Total pages: {totalPages}, Operations: {operationCount:N0}");
//        Console.WriteLine();

//        using var storage = new MemoryStorage(pageSize, totalPages);

//        foreach (int cacheSize in cacheSizes)
//        {
//            using var pageManager = new PageManager(storage, cacheSize);
            
//            // Allocate pages
//            var pages = new List<uint>();
//            for (int i = 0; i < totalPages - 1; i++)
//            {
//                var (pn, _) = pageManager.AllocatePage(PageType.Leaf);
//                pageManager.ReleasePage(pn);
//                pages.Add(pn);
//            }
//            pageManager.Flush();

//            // Random access
//            int[] randomIndices = GenerateRandomIndices(operationCount, pages.Count);

//            var sw = Stopwatch.StartNew();
//            for (int i = 0; i < operationCount; i++)
//            {
//                var page = pageManager.GetPage(pages[randomIndices[i]]);
//                pageManager.ReleasePage(pages[randomIndices[i]]);
//            }
//            sw.Stop();

//            double opsPerSecond = operationCount / sw.Elapsed.TotalSeconds;
//            double hitRate = Math.Min(100.0, (double)cacheSize / totalPages * 100);
            
//            Console.WriteLine($"Cache size {cacheSize,4}: {opsPerSecond:N0} ops/sec (estimated hit rate: ~{hitRate:F0}%)");
//        }

//        Console.WriteLine();
//        Assert.Pass();
//    }

//    private static void FillBufferWithPattern(byte[] buffer, int seed)
//    {
//        var random = new Random(seed);
//        random.NextBytes(buffer);
//    }

//    private static int[] GenerateRandomIndices(int count, int max)
//    {
//        var result = new int[count];
//        for (int i = 0; i < count; i++)
//        {
//            result[i] = Random.Shared.Next(max);
//        }
//        return result;
//    }
//}

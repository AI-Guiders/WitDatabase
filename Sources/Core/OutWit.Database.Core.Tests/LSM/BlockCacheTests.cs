using OutWit.Database.Core.LSM;

namespace OutWit.Database.Core.Tests.LSM
{
    /// <summary>
    /// Unit tests for BlockCache component.
    /// </summary>
    [TestFixture]
    public class BlockCacheTests
    {
        [Test]
        public void BlockCachePutAndGetTest()
        {
            using var cache = new BlockCache(1024 * 1024);
            var data = new byte[] { 1, 2, 3, 4, 5 };
            
            cache.Put("test.sst", 0, data);
            
            Assert.That(cache.TryGet("test.sst", 0, out var result), Is.True);
            Assert.That(result, Is.EqualTo(data));
            Assert.That(cache.Hits, Is.EqualTo(1));
        }

        [Test]
        public void BlockCacheMissTest()
        {
            using var cache = new BlockCache(1024 * 1024);
            
            Assert.That(cache.TryGet("nonexistent.sst", 0, out _), Is.False);
            Assert.That(cache.Misses, Is.EqualTo(1));
        }

        [Test]
        public void BlockCacheEvictionTest()
        {
            // Small cache that can hold ~10 blocks
            using var cache = new BlockCache(1000);
            
            // Add 20 blocks of 100 bytes each
            for (int i = 0; i < 20; i++)
            {
                cache.Put("test.sst", i, new byte[100]);
            }
            
            // Cache should have evicted some entries
            Assert.That(cache.CurrentSizeBytes, Is.LessThanOrEqualTo(1000));
            Assert.That(cache.Count, Is.LessThan(20));
        }

        [Test]
        public void BlockCacheInvalidateTest()
        {
            using var cache = new BlockCache(1024 * 1024);
            
            cache.Put("file1.sst", 0, new byte[100]);
            cache.Put("file1.sst", 1, new byte[100]);
            cache.Put("file2.sst", 0, new byte[100]);
            
            Assert.That(cache.Count, Is.EqualTo(3));
            
            cache.Invalidate("file1.sst");
            
            Assert.That(cache.Count, Is.EqualTo(1));
            Assert.That(cache.TryGet("file1.sst", 0, out _), Is.False);
            Assert.That(cache.TryGet("file2.sst", 0, out _), Is.True);
        }

        [Test]
        public void BlockCacheHitRatioTest()
        {
            using var cache = new BlockCache(1024 * 1024);
            
            cache.Put("test.sst", 0, new byte[100]);
            
            // 2 hits
            cache.TryGet("test.sst", 0, out _);
            cache.TryGet("test.sst", 0, out _);
            
            // 1 miss
            cache.TryGet("test.sst", 1, out _);
            
            Assert.That(cache.Hits, Is.EqualTo(2));
            Assert.That(cache.Misses, Is.EqualTo(1));
            Assert.That(cache.HitRatio, Is.EqualTo(2.0 / 3.0).Within(0.001));
        }

        [Test]
        public void BlockCacheLargeBlockSkippedTest()
        {
            // Cache of 1000 bytes
            using var cache = new BlockCache(1000);
            
            // Try to cache a block that's > 25% of cache size
            cache.Put("test.sst", 0, new byte[500]);
            
            // Should not be cached
            Assert.That(cache.TryGet("test.sst", 0, out _), Is.False);
        }

        [Test]
        public void BlockCacheConcurrentAccessTest()
        {
            using var cache = new BlockCache(10 * 1024 * 1024);
            const int threadCount = 4;
            const int opsPerThread = 1000;

            var tasks = Enumerable.Range(0, threadCount)
                .Select(t => Task.Run(() =>
                {
                    for (int i = 0; i < opsPerThread; i++)
                    {
                        var blockIndex = i % 100;
                        var key = $"file{t}.sst";
                        
                        if (i % 3 == 0)
                        {
                            cache.Put(key, blockIndex, new byte[100]);
                        }
                        else
                        {
                            cache.TryGet(key, blockIndex, out _);
                        }
                    }
                }))
                .ToArray();

            Task.WaitAll(tasks);
            
            // Should complete without exceptions
            Assert.That(cache.Count, Is.GreaterThan(0));
        }

        [Test]
        public void BlockCachePropertiesTest()
        {
            using var cache = new BlockCache(1024 * 1024);
            
            Assert.That(cache.MaxSizeBytes, Is.EqualTo(1024 * 1024));
            Assert.That(cache.CurrentSizeBytes, Is.EqualTo(0));
            Assert.That(cache.Count, Is.EqualTo(0));
            Assert.That(cache.Hits, Is.EqualTo(0));
            Assert.That(cache.Misses, Is.EqualTo(0));
            Assert.That(cache.HitRatio, Is.EqualTo(0));
        }

        [Test]
        public void BlockCacheClearTest()
        {
            using var cache = new BlockCache(1024 * 1024);
            
            cache.Put("file1.sst", 0, new byte[100]);
            cache.Put("file2.sst", 0, new byte[100]);
            
            Assert.That(cache.Count, Is.EqualTo(2));
            
            cache.Clear();
            
            Assert.That(cache.Count, Is.EqualTo(0));
            Assert.That(cache.CurrentSizeBytes, Is.EqualTo(0));
        }

        [Test]
        public void BlockCacheUpdateExistingEntryTest()
        {
            using var cache = new BlockCache(1024 * 1024);
            
            cache.Put("test.sst", 0, new byte[100]);
            var sizeAfterFirst = cache.CurrentSizeBytes;
            
            cache.Put("test.sst", 0, new byte[200]); // Update with larger data
            var sizeAfterUpdate = cache.CurrentSizeBytes;
            
            Assert.That(cache.Count, Is.EqualTo(1));
            Assert.That(sizeAfterUpdate, Is.GreaterThan(sizeAfterFirst));
        }

        [Test]
        public void BlockCacheMultipleFilesTest()
        {
            using var cache = new BlockCache(1024 * 1024);
            
            for (int file = 0; file < 5; file++)
            {
                for (int block = 0; block < 10; block++)
                {
                    cache.Put($"file{file}.sst", block, new byte[50]);
                }
            }
            
            Assert.That(cache.Count, Is.EqualTo(50));
            
            // Invalidate one file
            cache.Invalidate("file2.sst");
            
            Assert.That(cache.Count, Is.EqualTo(40));
        }
    }
}

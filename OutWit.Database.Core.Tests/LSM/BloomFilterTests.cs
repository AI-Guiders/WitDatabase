using OutWit.Database.Core.LSM;
using TextEncoding = System.Text.Encoding;

namespace OutWit.Database.Core.Tests.LSM
{
    /// <summary>
    /// Unit tests for BloomFilter component.
    /// </summary>
    [TestFixture]
    public class BloomFilterTests
    {
        private static byte[] ToBytes(string s) => TextEncoding.UTF8.GetBytes(s);

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

        [Test]
        public void BloomFilterFalsePositiveRateTest()
        {
            const int itemCount = 10000;
            var filter = new BloomFilter(itemCount, 0.01);

            // Add items
            for (int i = 0; i < itemCount; i++)
            {
                filter.Add(BitConverter.GetBytes(i));
            }

            // All added items should be found
            for (int i = 0; i < itemCount; i++)
            {
                Assert.That(filter.MightContain(BitConverter.GetBytes(i)), Is.True);
            }

            // Check false positive rate on non-existent items
            int falsePositives = 0;
            const int testCount = 10000;
            for (int i = itemCount; i < itemCount + testCount; i++)
            {
                if (filter.MightContain(BitConverter.GetBytes(i)))
                    falsePositives++;
            }

            double actualRate = (double)falsePositives / testCount;
            // Allow 3x the expected rate due to statistical variation
            Assert.That(actualRate, Is.LessThan(0.03), $"False positive rate {actualRate:P2} too high");
        }

        [Test]
        public void BloomFilterSerializeAndDeserializeWorksTest()
        {
            // Create filter and add keys
            var filter = new BloomFilter(100, 0.01);
            filter.Add(ToBytes("a"));
            filter.Add(ToBytes("b"));
            filter.Add(ToBytes("c"));
            
            // Serialize
            var bytes = filter.ToBytes();
            
            // Deserialize with ORIGINAL bit size
            var restored = new BloomFilter(bytes, filter.HashCount, filter.Size);
            
            // Verify restored works
            Assert.That(restored.MightContain(ToBytes("a")), Is.True);
            Assert.That(restored.MightContain(ToBytes("b")), Is.True);
            Assert.That(restored.MightContain(ToBytes("c")), Is.True);
            Assert.That(restored.MightContain(ToBytes("d")), Is.False);
        }

        [Test]
        public void BloomFilterPropertiesTest()
        {
            var filter = new BloomFilter(1000, 0.01);
            
            Assert.That(filter.Size, Is.GreaterThan(0));
            Assert.That(filter.HashCount, Is.GreaterThan(0));
        }

        [Test]
        public void BloomFilterEmptyDoesNotContainAnythingTest()
        {
            var filter = new BloomFilter(100);
            
            Assert.That(filter.MightContain(ToBytes("anything")), Is.False);
            Assert.That(filter.MightContain(BitConverter.GetBytes(12345)), Is.False);
        }

        [Test]
        public void BloomFilterSmallCapacityTest()
        {
            // Very small capacity
            var filter = new BloomFilter(10, 0.1);
            
            for (int i = 0; i < 10; i++)
            {
                filter.Add(BitConverter.GetBytes(i));
            }
            
            // All added should be found
            for (int i = 0; i < 10; i++)
            {
                Assert.That(filter.MightContain(BitConverter.GetBytes(i)), Is.True);
            }
        }

        [Test]
        public void BloomFilterLargeCapacityTest()
        {
            const int itemCount = 100_000;
            var filter = new BloomFilter(itemCount, 0.001); // Very low FPR
            
            for (int i = 0; i < itemCount; i++)
            {
                filter.Add(BitConverter.GetBytes(i));
            }
            
            // Sample check
            for (int i = 0; i < itemCount; i += 1000)
            {
                Assert.That(filter.MightContain(BitConverter.GetBytes(i)), Is.True);
            }
        }
    }
}

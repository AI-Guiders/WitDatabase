using OutWit.Database.Core.Storage;

namespace OutWit.Database.Core.Tests.Storage
{
    [TestFixture]
    public class StorageMemoryTest
    {
        [Test]
        public void CreateWithDefaultPageSizeTest()
        {
            using var storage = new StorageMemory();
        
            Assert.That(storage.PageSize, Is.EqualTo(DatabaseConstants.DEFAULT_PAGE_SIZE));
            Assert.That(storage.PageCount, Is.EqualTo(1));
            Assert.That(storage.IsReadOnly, Is.False);
        }

        [Test]
        public void CreateWithCustomPageSizeTest()
        {
            using var storage = new StorageMemory(pageSize: 8192, initialPageCount: 5);
        
            Assert.That(storage.PageSize, Is.EqualTo(8192));
            Assert.That(storage.PageCount, Is.EqualTo(5));
        }

        [Test]
        public void WriteAndReadPageTest()
        {
            using var storage = new StorageMemory();
        
            byte[] writeBuffer = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
            Array.Fill(writeBuffer, (byte)0xAB);
            storage.WritePage(0, writeBuffer);
        
            byte[] readBuffer = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
            storage.ReadPage(0, readBuffer);
        
            Assert.That(readBuffer, Is.EqualTo(writeBuffer));
        }

        [Test]
        public void SetSizeExtendsStorageTest()
        {
            using var storage = new StorageMemory(initialPageCount: 1);
        
            Assert.That(storage.PageCount, Is.EqualTo(1));
        
            storage.SetSize(10);
        
            Assert.That(storage.PageCount, Is.EqualTo(10));
        }

        [Test]
        public void SetSizeShrinksStorageTest()
        {
            using var storage = new StorageMemory(initialPageCount: 10);
        
            storage.SetSize(5);
        
            Assert.That(storage.PageCount, Is.EqualTo(5));
        }

        [Test]
        public void ReadOutOfRangeThrowsTest()
        {
            using var storage = new StorageMemory(initialPageCount: 1);
            byte[] buffer = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
        
            Assert.Throws<ArgumentOutOfRangeException>(() => storage.ReadPage(1, buffer));
            Assert.Throws<ArgumentOutOfRangeException>(() => storage.ReadPage(-1, buffer));
        }

        [Test]
        public void WriteOutOfRangeThrowsTest()
        {
            using var storage = new StorageMemory(initialPageCount: 1);
            byte[] buffer = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
        
            Assert.Throws<ArgumentOutOfRangeException>(() => storage.WritePage(1, buffer));
        }

        [Test]
        public void SmallBufferThrowsTest()
        {
            using var storage = new StorageMemory();
            byte[] smallBuffer = new byte[100];
        
            Assert.Throws<ArgumentException>(() => storage.ReadPage(0, smallBuffer));
            Assert.Throws<ArgumentException>(() => storage.WritePage(0, smallBuffer));
        }

        [Test]
        public void DisposeAndAccessThrowsTest()
        {
            var storage = new StorageMemory();
            storage.Dispose();
        
            byte[] buffer = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
        
            Assert.Throws<ObjectDisposedException>(() => storage.ReadPage(0, buffer));
            Assert.Throws<ObjectDisposedException>(() => storage.WritePage(0, buffer));
        }

        [Test]
        public async Task AsyncReadWriteTest()
        {
            using var storage = new StorageMemory();
        
            byte[] writeBuffer = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
            Array.Fill(writeBuffer, (byte)0xCD);
            await storage.WritePageAsync(0, writeBuffer);
        
            byte[] readBuffer = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
            await storage.ReadPageAsync(0, readBuffer);
        
            Assert.That(readBuffer, Is.EqualTo(writeBuffer));
        }

        [Test]
        public void InvalidPageSizeThrowsTest()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new StorageMemory(pageSize: 100));
            // MaxPageSize is 65536, so anything above that should throw
            Assert.Throws<ArgumentOutOfRangeException>(() => new StorageMemory(pageSize: 70000));
        }

        [Test]
        public void GetDataReturnsUnderlyingMemoryTest()
        {
            using var storage = new StorageMemory(initialPageCount: 2);
        
            ReadOnlyMemory<byte> data = storage.Data;
        
            Assert.That(data.Length, Is.EqualTo(DatabaseConstants.DEFAULT_PAGE_SIZE * 2));
        }
    }
}

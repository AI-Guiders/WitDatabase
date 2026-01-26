using OutWit.Database.Core.Storage;

namespace OutWit.Database.Core.Tests.Storage;

[TestFixture]
public class StorageFileTest
{
    private string m_testDir = null!;

    [SetUp]
    public void SetUp()
    {
        m_testDir = Path.Combine(Path.GetTempPath(), $"WitDB_Test_{Guid.NewGuid():N}");
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

    #region Constructor Tests

    [Test]
    public void ConstructorCreatesNewFileTest()
    {
        string path = Path.Combine(m_testDir, "new.wdb");
        
        using var storage = new StorageFile(path);
        
        Assert.That(File.Exists(path), Is.True);
        Assert.That(storage.PageSize, Is.EqualTo(DatabaseConstants.DEFAULT_PAGE_SIZE));
        Assert.That(storage.PageCount, Is.EqualTo(1));
        Assert.That(storage.IsReadOnly, Is.False);
    }

    [Test]
    public void ConstructorWithCustomPageSizeTest()
    {
        string path = Path.Combine(m_testDir, "custom.wdb");
        
        using var storage = new StorageFile(path, pageSize: 8192);
        
        Assert.That(storage.PageSize, Is.EqualTo(8192));
    }

    [Test]
    public void ConstructorNullPathThrowsTest()
    {
        Assert.Throws<ArgumentNullException>(() => new StorageFile(null!));
        Assert.Throws<ArgumentException>(() => new StorageFile(""));
        Assert.Throws<ArgumentException>(() => new StorageFile("   "));
    }

    [Test]
    public void ConstructorInvalidPageSizeThrowsTest()
    {
        string path = Path.Combine(m_testDir, "invalid.wdb");
        
        Assert.Throws<ArgumentOutOfRangeException>(() => new StorageFile(path, pageSize: 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StorageFile(path, pageSize: 100000));
    }

    #endregion

    #region Create Tests

    [Test]
    public void CreateNewFileTest()
    {
        string path = Path.Combine(m_testDir, "create.wdb");
        
        using var storage = StorageFile.Create(path);
        
        Assert.That(File.Exists(path), Is.True);
        Assert.That(storage.PageCount, Is.EqualTo(1));
    }

    [Test]
    public void CreateExistingFileThrowsTest()
    {
        string path = Path.Combine(m_testDir, "existing.wdb");
        File.WriteAllText(path, "test");
        
        Assert.Throws<IOException>(() => StorageFile.Create(path));
    }

    #endregion

    #region Open Tests

    [Test]
    public void OpenExistingFileTest()
    {
        string path = Path.Combine(m_testDir, "open.wdb");
        
        // Create file first
        using (var storage = StorageFile.Create(path))
        {
            storage.SetSize(5);
        }
        
        // Open it
        using var opened = StorageFile.Open(path);
        
        Assert.That(opened.PageCount, Is.EqualTo(5));
    }

    [Test]
    public void OpenNonExistentFileThrowsTest()
    {
        string path = Path.Combine(m_testDir, "nonexistent.wdb");
        
        Assert.Throws<FileNotFoundException>(() => StorageFile.Open(path));
    }

    [Test]
    public void OpenReadOnlyTest()
    {
        string path = Path.Combine(m_testDir, "readonly.wdb");
        
        using (var storage = StorageFile.Create(path))
        {
            storage.SetSize(5);
        }
        
        using var readOnly = StorageFile.Open(path, readOnly: true);
        
        Assert.That(readOnly.IsReadOnly, Is.True);
        Assert.That(readOnly.PageCount, Is.EqualTo(5));
    }

    #endregion

    #region Read/Write Tests

    [Test]
    public void WriteAndReadPageTest()
    {
        string path = Path.Combine(m_testDir, "readwrite.wdb");
        
        using var storage = StorageFile.Create(path);
        
        byte[] writeBuffer = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
        Array.Fill(writeBuffer, (byte)0xAB);
        storage.WritePage(0, writeBuffer);
        
        byte[] readBuffer = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
        storage.ReadPage(0, readBuffer);
        
        Assert.That(readBuffer, Is.EqualTo(writeBuffer));
    }

    [Test]
    public void ReadPageOutOfRangeThrowsTest()
    {
        string path = Path.Combine(m_testDir, "outofrange.wdb");
        
        using var storage = StorageFile.Create(path);
        byte[] buffer = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
        
        Assert.Throws<ArgumentOutOfRangeException>(() => storage.ReadPage(-1, buffer));
        Assert.Throws<ArgumentOutOfRangeException>(() => storage.ReadPage(1, buffer)); // Only page 0 exists
    }

    [Test]
    public void WritePageNegativeNumberThrowsTest()
    {
        string path = Path.Combine(m_testDir, "negative.wdb");
        
        using var storage = StorageFile.Create(path);
        byte[] buffer = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
        
        Assert.Throws<ArgumentOutOfRangeException>(() => storage.WritePage(-1, buffer));
    }

    [Test]
    public void BufferTooSmallThrowsTest()
    {
        string path = Path.Combine(m_testDir, "small.wdb");
        
        using var storage = StorageFile.Create(path);
        byte[] smallBuffer = new byte[100];
        
        Assert.Throws<ArgumentException>(() => storage.ReadPage(0, smallBuffer));
        Assert.Throws<ArgumentException>(() => storage.WritePage(0, smallBuffer));
    }

    [Test]
    public void WriteToReadOnlyThrowsTest()
    {
        string path = Path.Combine(m_testDir, "writereadonly.wdb");
        
        using (StorageFile.Create(path)) { }
        
        using var readOnly = StorageFile.Open(path, readOnly: true);
        byte[] buffer = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
        
        Assert.Throws<InvalidOperationException>(() => readOnly.WritePage(0, buffer));
    }

    #endregion

    #region Async Read/Write Tests

    [Test]
    public async Task WriteAndReadPageAsyncTest()
    {
        string path = Path.Combine(m_testDir, "async.wdb");
        
        using var storage = StorageFile.Create(path);
        
        byte[] writeBuffer = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
        Array.Fill(writeBuffer, (byte)0xCD);
        await storage.WritePageAsync(0, writeBuffer);
        
        byte[] readBuffer = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
        await storage.ReadPageAsync(0, readBuffer);
        
        Assert.That(readBuffer, Is.EqualTo(writeBuffer));
    }

    [Test]
    public async Task ConcurrentAsyncReadsTest()
    {
        string path = Path.Combine(m_testDir, "concurrent.wdb");
        
        using var storage = StorageFile.Create(path);
        storage.SetSize(10);
        
        // Write unique data to each page
        for (int i = 0; i < 10; i++)
        {
            byte[] data = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
            Array.Fill(data, (byte)i);
            storage.WritePage(i, data);
        }
        
        // Concurrent reads
        var tasks = new Task[10];
        for (int i = 0; i < 10; i++)
        {
            int pageNum = i;
            tasks[i] = Task.Run(async () =>
            {
                byte[] buffer = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
                await storage.ReadPageAsync(pageNum, buffer);
                
                Assert.That(buffer[0], Is.EqualTo((byte)pageNum));
            });
        }
        
        await Task.WhenAll(tasks);
    }

    #endregion

    #region SetSize Tests

    [Test]
    public void SetSizeExtendsFileTest()
    {
        string path = Path.Combine(m_testDir, "extend.wdb");
        
        using var storage = StorageFile.Create(path);
        
        Assert.That(storage.PageCount, Is.EqualTo(1));
        
        storage.SetSize(10);
        
        Assert.That(storage.PageCount, Is.EqualTo(10));
    }

    [Test]
    public void SetSizeShrinksFileTest()
    {
        string path = Path.Combine(m_testDir, "shrink.wdb");
        
        using var storage = StorageFile.Create(path);
        storage.SetSize(10);
        
        Assert.That(storage.PageCount, Is.EqualTo(10));
        
        storage.SetSize(5);
        
        Assert.That(storage.PageCount, Is.EqualTo(5));
    }

    [Test]
    public void SetSizeNegativeThrowsTest()
    {
        string path = Path.Combine(m_testDir, "negsize.wdb");
        
        using var storage = StorageFile.Create(path);
        
        Assert.Throws<ArgumentOutOfRangeException>(() => storage.SetSize(-1));
    }

    [Test]
    public void SetSizeOnReadOnlyThrowsTest()
    {
        string path = Path.Combine(m_testDir, "sizereadonly.wdb");
        
        using (StorageFile.Create(path)) { }
        
        using var readOnly = StorageFile.Open(path, readOnly: true);
        
        Assert.Throws<InvalidOperationException>(() => readOnly.SetSize(10));
    }

    #endregion

    #region Flush Tests

    [Test]
    public void FlushDoesNotThrowTest()
    {
        string path = Path.Combine(m_testDir, "flush.wdb");
        
        using var storage = StorageFile.Create(path);
        
        byte[] buffer = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
        storage.WritePage(0, buffer);
        storage.Flush();
        
        // Should complete without exception
        Assert.Pass();
    }

    [Test]
    public async Task FlushAsyncDoesNotThrowTest()
    {
        string path = Path.Combine(m_testDir, "flushasync.wdb");
        
        using var storage = StorageFile.Create(path);
        
        byte[] buffer = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
        await storage.WritePageAsync(0, buffer);
        await storage.FlushAsync();
        
        // Should complete without exception
        Assert.Pass();
    }

    #endregion

    #region Dispose Tests

    [Test]
    public void DisposeClosesFileTest()
    {
        string path = Path.Combine(m_testDir, "dispose.wdb");
        
        var storage = StorageFile.Create(path);
        storage.Dispose();
        
        // File should be unlocked after dispose
        using var stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite);
        Assert.That(stream.CanWrite, Is.True);
    }

    [Test]
    public void DisposeMultipleTimesDoesNotThrowTest()
    {
        string path = Path.Combine(m_testDir, "multidispose.wdb");
        
        var storage = StorageFile.Create(path);
        
        storage.Dispose();
        storage.Dispose();
        storage.Dispose();
        
        // Should not throw
        Assert.Pass();
    }

    [Test]
    public void OperationsAfterDisposeThrowTest()
    {
        string path = Path.Combine(m_testDir, "afterdispose.wdb");
        
        var storage = StorageFile.Create(path);
        storage.Dispose();
        
        byte[] buffer = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
        
        Assert.Throws<ObjectDisposedException>(() => storage.ReadPage(0, buffer));
        Assert.Throws<ObjectDisposedException>(() => storage.WritePage(0, buffer));
        Assert.Throws<ObjectDisposedException>(() => storage.Flush());
        Assert.Throws<ObjectDisposedException>(() => storage.SetSize(10));
    }

    #endregion

    #region Data Persistence Tests

    [Test]
    public void DataPersistsAcrossOpenCloseTest()
    {
        string path = Path.Combine(m_testDir, "persist.wdb");
        
        byte[] originalData = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
        new Random(42).NextBytes(originalData);
        
        // Write data
        using (var storage = StorageFile.Create(path))
        {
            storage.WritePage(0, originalData);
            storage.Flush();
        }
        
        // Read data after reopen
        using (var storage = StorageFile.Open(path))
        {
            byte[] readData = new byte[DatabaseConstants.DEFAULT_PAGE_SIZE];
            storage.ReadPage(0, readData);
            
            Assert.That(readData, Is.EqualTo(originalData));
        }
    }

    #endregion
}

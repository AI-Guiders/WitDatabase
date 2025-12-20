using OutWit.Database.Core.Cache;

namespace OutWit.Database.Core.Tests.Cache;

[TestFixture]
public class CachedPageTest
{
    private const int PAGE_SIZE = 4096;

    #region Constructor Tests

    [Test]
    public void ConstructorInitializesPropertiesTest()
    {
        var page = new CachedPage(42, PAGE_SIZE);
        
        try
        {
            Assert.That(page.PageNumber, Is.EqualTo(42));
            Assert.That(page.IsDirty, Is.False);
            Assert.That(page.IsDisposed, Is.False);
            Assert.That(page.Data.Length, Is.EqualTo(PAGE_SIZE));
        }
        finally
        {
            page.Dispose();
        }
    }

    [Test]
    public void ConstructorAllocatesBufferFromPoolTest()
    {
        var page = new CachedPage(0, PAGE_SIZE);
        
        try
        {
            // Buffer should be at least PAGE_SIZE (pool may return larger)
            Assert.That(page.Data.Length, Is.GreaterThanOrEqualTo(PAGE_SIZE));
        }
        finally
        {
            page.Dispose();
        }
    }

    #endregion

    #region Data Access Tests

    [Test]
    public void DataPropertyReturnsWritableSpanTest()
    {
        var page = new CachedPage(0, PAGE_SIZE);
        
        try
        {
            page.Data[0] = 0xAB;
            page.Data[PAGE_SIZE - 1] = 0xCD;
            
            Assert.That(page.Data[0], Is.EqualTo(0xAB));
            Assert.That(page.Data[PAGE_SIZE - 1], Is.EqualTo(0xCD));
        }
        finally
        {
            page.Dispose();
        }
    }

    [Test]
    public void ReadOnlyDataPropertyReturnsCorrectDataTest()
    {
        var page = new CachedPage(0, PAGE_SIZE);
        
        try
        {
            page.Data[100] = 0xFF;
            
            Assert.That(page.ReadOnlyData[100], Is.EqualTo(0xFF));
        }
        finally
        {
            page.Dispose();
        }
    }

    [Test]
    public void MemoryPropertyReturnsCorrectMemoryTest()
    {
        var page = new CachedPage(0, PAGE_SIZE);
        
        try
        {
            page.Data[50] = 0x42;
            
            Memory<byte> memory = page.Memory;
            Assert.That(memory.Length, Is.EqualTo(PAGE_SIZE));
            Assert.That(memory.Span[50], Is.EqualTo(0x42));
        }
        finally
        {
            page.Dispose();
        }
    }

    #endregion

    #region Dirty Flag Tests

    [Test]
    public void MarkDirtySetsDirtyFlagTest()
    {
        var page = new CachedPage(0, PAGE_SIZE);
        
        try
        {
            Assert.That(page.IsDirty, Is.False);
            
            page.MarkDirty();
            
            Assert.That(page.IsDirty, Is.True);
        }
        finally
        {
            page.Dispose();
        }
    }

    [Test]
    public void MarkDirtyMultipleTimesTest()
    {
        var page = new CachedPage(0, PAGE_SIZE);
        
        try
        {
            page.MarkDirty();
            page.MarkDirty();
            page.MarkDirty();
            
            Assert.That(page.IsDirty, Is.True);
        }
        finally
        {
            page.Dispose();
        }
    }

    #endregion

    #region Dispose Tests

    [Test]
    public void DisposeSetIsDisposedTest()
    {
        var page = new CachedPage(0, PAGE_SIZE);
        
        Assert.That(page.IsDisposed, Is.False);
        
        page.Dispose();
        
        Assert.That(page.IsDisposed, Is.True);
    }

    [Test]
    public void DisposeMultipleTimesDoesNotThrowTest()
    {
        var page = new CachedPage(0, PAGE_SIZE);
        
        page.Dispose();
        page.Dispose();
        page.Dispose();
        
        // Should not throw
        Assert.Pass();
    }

    [Test]
    public void DataAccessAfterDisposeThrowsTest()
    {
        var page = new CachedPage(0, PAGE_SIZE);
        page.Dispose();
        
        Assert.Throws<ObjectDisposedException>(() => _ = page.Data);
    }

    [Test]
    public void ReadOnlyDataAccessAfterDisposeThrowsTest()
    {
        var page = new CachedPage(0, PAGE_SIZE);
        page.Dispose();
        
        Assert.Throws<ObjectDisposedException>(() => _ = page.ReadOnlyData);
    }

    [Test]
    public void MemoryAccessAfterDisposeThrowsTest()
    {
        var page = new CachedPage(0, PAGE_SIZE);
        page.Dispose();
        
        Assert.Throws<ObjectDisposedException>(() => _ = page.Memory);
    }

    [Test]
    public void MarkDirtyAfterDisposeThrowsTest()
    {
        var page = new CachedPage(0, PAGE_SIZE);
        page.Dispose();
        
        Assert.Throws<ObjectDisposedException>(() => page.MarkDirty());
    }

    #endregion

    #region Page Number Tests

    [Test]
    public void PageNumberIsImmutableTest()
    {
        var page = new CachedPage(12345, PAGE_SIZE);
        
        try
        {
            Assert.That(page.PageNumber, Is.EqualTo(12345));
            
            // PageNumber is read-only, no way to change it
        }
        finally
        {
            page.Dispose();
        }
    }

    [Test]
    public void PageNumberCanBeZeroTest()
    {
        var page = new CachedPage(0, PAGE_SIZE);
        
        try
        {
            Assert.That(page.PageNumber, Is.EqualTo(0));
        }
        finally
        {
            page.Dispose();
        }
    }

    [Test]
    public void PageNumberCanBeLargeTest()
    {
        var page = new CachedPage(long.MaxValue, PAGE_SIZE);
        
        try
        {
            Assert.That(page.PageNumber, Is.EqualTo(long.MaxValue));
        }
        finally
        {
            page.Dispose();
        }
    }

    #endregion

    #region Different Page Sizes Tests

    [Test]
    [TestCase(512)]
    [TestCase(4096)]
    [TestCase(8192)]
    [TestCase(65536)]
    public void DifferentPageSizesTest(int pageSize)
    {
        var page = new CachedPage(0, pageSize);
        
        try
        {
            Assert.That(page.Data.Length, Is.EqualTo(pageSize));
            Assert.That(page.ReadOnlyData.Length, Is.EqualTo(pageSize));
            Assert.That(page.Memory.Length, Is.EqualTo(pageSize));
        }
        finally
        {
            page.Dispose();
        }
    }

    #endregion

    #region Data Integrity Tests

    [Test]
    public void DataPersistsUntilDisposeTest()
    {
        var page = new CachedPage(0, PAGE_SIZE);
        
        try
        {
            // Fill with pattern
            for (int i = 0; i < PAGE_SIZE; i++)
            {
                page.Data[i] = (byte)(i % 256);
            }
            
            // Verify pattern
            for (int i = 0; i < PAGE_SIZE; i++)
            {
                Assert.That(page.Data[i], Is.EqualTo((byte)(i % 256)));
            }
        }
        finally
        {
            page.Dispose();
        }
    }

    [Test]
    public void DataSpanAndMemoryShareSameBufferTest()
    {
        var page = new CachedPage(0, PAGE_SIZE);
        
        try
        {
            page.Data[0] = 0x11;
            Assert.That(page.Memory.Span[0], Is.EqualTo(0x11));
            
            page.Memory.Span[0] = 0x22;
            Assert.That(page.Data[0], Is.EqualTo(0x22));
        }
        finally
        {
            page.Dispose();
        }
    }

    #endregion
}

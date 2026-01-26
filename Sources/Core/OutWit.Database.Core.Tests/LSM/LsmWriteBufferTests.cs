using OutWit.Database.Core.LSM;
using TextEncoding = System.Text.Encoding;

namespace OutWit.Database.Core.Tests.LSM;

/// <summary>
/// Tests for LsmWriteBuffer thread-local buffer functionality.
/// </summary>
[TestFixture]
public class LsmWriteBufferTests
{
    #region Basic Operations Tests

    [Test]
    public void PutAddsEntryToBufferTest()
    {
        using var buffer = new LsmWriteBuffer();

        buffer.Put(ToBytes("key1"), ToBytes("value1"));

        Assert.That(buffer.Count, Is.EqualTo(1));
        Assert.That(buffer.IsEmpty, Is.False);
    }

    [Test]
    public void DeleteAddsEntryToBufferTest()
    {
        using var buffer = new LsmWriteBuffer();

        buffer.Delete(ToBytes("key1"));

        Assert.That(buffer.Count, Is.EqualTo(1));
        Assert.That(buffer.IsEmpty, Is.False);
    }

    [Test]
    public void MultiplePutsAccumulateTest()
    {
        using var buffer = new LsmWriteBuffer();

        for (int i = 0; i < 10; i++)
        {
            buffer.Put(ToBytes($"key{i}"), ToBytes($"value{i}"));
        }

        Assert.That(buffer.Count, Is.EqualTo(10));
    }

    [Test]
    public void ApproximateSizeTracksDataTest()
    {
        using var buffer = new LsmWriteBuffer();

        Assert.That(buffer.ApproximateSize, Is.EqualTo(0));

        buffer.Put(ToBytes("key"), ToBytes("value")); // 3 + 5 = 8 bytes

        Assert.That(buffer.ApproximateSize, Is.EqualTo(8));
    }

    #endregion

    #region Drain Tests

    [Test]
    public void DrainReturnsAllEntriesTest()
    {
        using var buffer = new LsmWriteBuffer();

        buffer.Put(ToBytes("key1"), ToBytes("value1"));
        buffer.Put(ToBytes("key2"), ToBytes("value2"));
        buffer.Delete(ToBytes("key3"));

        var entries = buffer.Drain();

        Assert.That(entries.Count, Is.EqualTo(3));
        Assert.That(entries[0].Key, Is.EqualTo(ToBytes("key1")));
        Assert.That(entries[0].Value, Is.EqualTo(ToBytes("value1")));
        Assert.That(entries[0].IsDelete, Is.False);
        Assert.That(entries[2].IsDelete, Is.True);
    }

    [Test]
    public void DrainClearsBufferTest()
    {
        using var buffer = new LsmWriteBuffer();

        buffer.Put(ToBytes("key1"), ToBytes("value1"));
        buffer.Drain();

        Assert.That(buffer.Count, Is.EqualTo(0));
        Assert.That(buffer.IsEmpty, Is.True);
        Assert.That(buffer.ApproximateSize, Is.EqualTo(0));
    }

    [Test]
    public void DrainEmptyBufferReturnsEmptyListTest()
    {
        using var buffer = new LsmWriteBuffer();

        var entries = buffer.Drain();

        Assert.That(entries, Is.Empty);
    }

    #endregion

    #region Clear Tests

    [Test]
    public void ClearRemovesAllEntriesTest()
    {
        using var buffer = new LsmWriteBuffer();

        buffer.Put(ToBytes("key1"), ToBytes("value1"));
        buffer.Put(ToBytes("key2"), ToBytes("value2"));
        buffer.Clear();

        Assert.That(buffer.Count, Is.EqualTo(0));
        Assert.That(buffer.IsEmpty, Is.True);
        Assert.That(buffer.ApproximateSize, Is.EqualTo(0));
    }

    #endregion

    #region ShouldFlush Tests

    [Test]
    public void ShouldFlushReturnsFalseWhenBelowThresholdTest()
    {
        using var buffer = new LsmWriteBuffer(sizeThreshold: 1000);

        buffer.Put(ToBytes("key"), ToBytes("value"));

        Assert.That(buffer.ShouldFlush, Is.False);
    }

    [Test]
    public void ShouldFlushReturnsTrueWhenAboveThresholdTest()
    {
        using var buffer = new LsmWriteBuffer(sizeThreshold: 100);

        // Add data to exceed threshold
        for (int i = 0; i < 20; i++)
        {
            buffer.Put(ToBytes($"key{i:D10}"), ToBytes($"value{i:D10}"));
        }

        Assert.That(buffer.ShouldFlush, Is.True);
    }

    #endregion

    #region Disposal Tests

    [Test]
    public void DisposeCanBeCalledMultipleTimesTest()
    {
        var buffer = new LsmWriteBuffer();
        buffer.Put(ToBytes("key"), ToBytes("value"));

        buffer.Dispose();
        Assert.DoesNotThrow(() => buffer.Dispose());
    }

    [Test]
    public void DisposedBufferThrowsOnOperationsTest()
    {
        var buffer = new LsmWriteBuffer();
        buffer.Dispose();

        Assert.Throws<ObjectDisposedException>(() => buffer.Put(ToBytes("key"), ToBytes("value")));
        Assert.Throws<ObjectDisposedException>(() => buffer.Delete(ToBytes("key")));
        Assert.Throws<ObjectDisposedException>(() => buffer.Drain());
        Assert.Throws<ObjectDisposedException>(() => buffer.Clear());
    }

    #endregion

    #region Helpers

    private static byte[] ToBytes(string s) => TextEncoding.UTF8.GetBytes(s);

    #endregion
}

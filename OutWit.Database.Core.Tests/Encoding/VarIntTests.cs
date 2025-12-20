using OutWit.Database.Core.Encoding;

namespace OutWit.Database.Core.Tests.Encoding;

[TestFixture]
public class VarIntTest
{
    #region EncodeUnsigned Tests

    [Test]
    [TestCase(0UL, 1)]
    [TestCase(127UL, 1)]
    [TestCase(128UL, 2)]
    [TestCase(16383UL, 2)]
    [TestCase(16384UL, 3)]
    [TestCase(ulong.MaxValue, 10)]
    public void EncodeUnsignedLengthTest(ulong value, int expectedLength)
    {
        byte[] buffer = new byte[VarInt.MAX_LENGTH];
        int bytesWritten = VarInt.EncodeUnsigned(buffer, value);
        
        Assert.That(bytesWritten, Is.EqualTo(expectedLength));
        Assert.That(VarInt.GetEncodedLengthUnsigned(value), Is.EqualTo(expectedLength));
    }

    [Test]
    public void EncodeUnsignedRoundtripTest()
    {
        ulong[] testValues = [0, 1, 127, 128, 255, 256, 16383, 16384, 
            uint.MaxValue, ulong.MaxValue / 2, ulong.MaxValue];
        
        byte[] buffer = new byte[VarInt.MAX_LENGTH];
        
        foreach (ulong original in testValues)
        {
            int written = VarInt.EncodeUnsigned(buffer, original);
            var (decoded, read) = VarInt.DecodeUnsigned(buffer);
            
            Assert.That(decoded, Is.EqualTo(original), $"Roundtrip failed for {original}");
            Assert.That(read, Is.EqualTo(written), $"Bytes read != written for {original}");
        }
    }

    [Test]
    public void EncodeUnsignedBufferTooSmallThrowsTest()
    {
        byte[] smallBuffer = new byte[1];
        
        Assert.Throws<ArgumentException>(() => VarInt.EncodeUnsigned(smallBuffer, 128)); // Needs 2 bytes
        Assert.Throws<ArgumentException>(() => VarInt.EncodeUnsigned(smallBuffer, 16384)); // Needs 3 bytes
    }

    #endregion

    #region Encode (Signed) Tests

    [Test]
    [TestCase(0L, 1)]
    [TestCase(-1L, 1)]
    [TestCase(1L, 1)]
    [TestCase(63L, 1)]
    [TestCase(-64L, 1)]
    [TestCase(64L, 2)]
    [TestCase(-65L, 2)]
    [TestCase(long.MaxValue, 10)]
    [TestCase(long.MinValue, 10)]
    public void EncodeSignedLengthTest(long value, int expectedLength)
    {
        byte[] buffer = new byte[VarInt.MAX_LENGTH];
        int bytesWritten = VarInt.Encode(buffer, value);
        
        Assert.That(bytesWritten, Is.EqualTo(expectedLength));
        Assert.That(VarInt.GetEncodedLength(value), Is.EqualTo(expectedLength));
    }

    [Test]
    public void EncodeSignedRoundtripTest()
    {
        long[] testValues = [0, 1, -1, 63, -64, 64, -65, 127, -128,
            int.MaxValue, int.MinValue, long.MaxValue, long.MinValue];
        
        byte[] buffer = new byte[VarInt.MAX_LENGTH];
        
        foreach (long original in testValues)
        {
            int written = VarInt.Encode(buffer, original);
            var (decoded, read) = VarInt.Decode(buffer);
            
            Assert.That(decoded, Is.EqualTo(original), $"Roundtrip failed for {original}");
            Assert.That(read, Is.EqualTo(written), $"Bytes read != written for {original}");
        }
    }

    #endregion

    #region DecodeUnsigned Tests

    [Test]
    public void DecodeUnsignedEmptyBufferThrowsTest()
    {
        byte[] empty = [];
        
        Assert.Throws<InvalidDataException>(() => VarInt.DecodeUnsigned(empty));
    }

    [Test]
    public void DecodeUnsignedIncompleteVarintThrowsTest()
    {
        // A byte with high bit set indicates more bytes follow
        byte[] incomplete = [0x80]; // High bit set, but no more bytes
        
        Assert.Throws<InvalidDataException>(() => VarInt.DecodeUnsigned(incomplete));
    }

    [Test]
    public void DecodeUnsignedValidDataTest()
    {
        // 0 = 0x00
        var (val0, len0) = VarInt.DecodeUnsigned([0x00]);
        Assert.That(val0, Is.EqualTo(0UL));
        Assert.That(len0, Is.EqualTo(1));
        
        // 127 = 0x7F
        var (val127, len127) = VarInt.DecodeUnsigned([0x7F]);
        Assert.That(val127, Is.EqualTo(127UL));
        Assert.That(len127, Is.EqualTo(1));
        
        // 128 = 0x80 0x01
        var (val128, len128) = VarInt.DecodeUnsigned([0x80, 0x01]);
        Assert.That(val128, Is.EqualTo(128UL));
        Assert.That(len128, Is.EqualTo(2));
        
        // 300 = 0xAC 0x02
        var (val300, len300) = VarInt.DecodeUnsigned([0xAC, 0x02]);
        Assert.That(val300, Is.EqualTo(300UL));
        Assert.That(len300, Is.EqualTo(2));
    }

    #endregion

    #region TryEncode Tests

    [Test]
    public void TryEncodeUnsignedSuccessTest()
    {
        byte[] buffer = new byte[VarInt.MAX_LENGTH];
        
        bool success = VarInt.TryEncodeUnsigned(buffer, 12345, out int bytesWritten);
        
        Assert.That(success, Is.True);
        Assert.That(bytesWritten, Is.GreaterThan(0));
        
        var (decoded, _) = VarInt.DecodeUnsigned(buffer);
        Assert.That(decoded, Is.EqualTo(12345UL));
    }

    [Test]
    public void TryEncodeUnsignedBufferTooSmallTest()
    {
        byte[] smallBuffer = new byte[1];
        
        bool success = VarInt.TryEncodeUnsigned(smallBuffer, 128, out int bytesWritten);
        
        Assert.That(success, Is.False);
        Assert.That(bytesWritten, Is.EqualTo(0));
    }

    [Test]
    public void TryEncodeSignedSuccessTest()
    {
        byte[] buffer = new byte[VarInt.MAX_LENGTH];
        
        bool success = VarInt.TryEncode(buffer, -12345, out int bytesWritten);
        
        Assert.That(success, Is.True);
        Assert.That(bytesWritten, Is.GreaterThan(0));
        
        var (decoded, _) = VarInt.Decode(buffer);
        Assert.That(decoded, Is.EqualTo(-12345L));
    }

    #endregion

    #region TryDecode Tests

    [Test]
    public void TryDecodeUnsignedSuccessTest()
    {
        byte[] buffer = new byte[VarInt.MAX_LENGTH];
        VarInt.EncodeUnsigned(buffer, 54321);
        
        bool success = VarInt.TryDecodeUnsigned(buffer, out ulong value, out int bytesRead);
        
        Assert.That(success, Is.True);
        Assert.That(value, Is.EqualTo(54321UL));
        Assert.That(bytesRead, Is.GreaterThan(0));
    }

    [Test]
    public void TryDecodeUnsignedEmptyBufferTest()
    {
        byte[] empty = [];
        
        bool success = VarInt.TryDecodeUnsigned(empty, out ulong value, out int bytesRead);
        
        Assert.That(success, Is.False);
        Assert.That(value, Is.EqualTo(0UL));
        Assert.That(bytesRead, Is.EqualTo(0));
    }

    [Test]
    public void TryDecodeUnsignedIncompleteTest()
    {
        byte[] incomplete = [0x80]; // High bit set, no continuation
        
        bool success = VarInt.TryDecodeUnsigned(incomplete, out ulong value, out int bytesRead);
        
        Assert.That(success, Is.False);
        Assert.That(value, Is.EqualTo(0UL));
        Assert.That(bytesRead, Is.EqualTo(0));
    }

    [Test]
    public void TryDecodeSignedSuccessTest()
    {
        byte[] buffer = new byte[VarInt.MAX_LENGTH];
        VarInt.Encode(buffer, -54321);
        
        bool success = VarInt.TryDecode(buffer, out long value, out int bytesRead);
        
        Assert.That(success, Is.True);
        Assert.That(value, Is.EqualTo(-54321L));
        Assert.That(bytesRead, Is.GreaterThan(0));
    }

    #endregion

    #region GetEncodedLength Tests

    [Test]
    public void GetEncodedLengthUnsignedBoundariesTest()
    {
        // Test boundary values
        Assert.That(VarInt.GetEncodedLengthUnsigned(0), Is.EqualTo(1));
        Assert.That(VarInt.GetEncodedLengthUnsigned((1UL << 7) - 1), Is.EqualTo(1));
        Assert.That(VarInt.GetEncodedLengthUnsigned(1UL << 7), Is.EqualTo(2));
        Assert.That(VarInt.GetEncodedLengthUnsigned((1UL << 14) - 1), Is.EqualTo(2));
        Assert.That(VarInt.GetEncodedLengthUnsigned(1UL << 14), Is.EqualTo(3));
        Assert.That(VarInt.GetEncodedLengthUnsigned(ulong.MaxValue), Is.EqualTo(10));
    }

    [Test]
    public void GetEncodedLengthSignedZigZagTest()
    {
        // ZigZag encoding: small absolute values should be short
        Assert.That(VarInt.GetEncodedLength(0), Is.EqualTo(1));
        Assert.That(VarInt.GetEncodedLength(-1), Is.EqualTo(1)); // ZigZag: -1 -> 1
        Assert.That(VarInt.GetEncodedLength(1), Is.EqualTo(1)); // ZigZag: 1 -> 2
        Assert.That(VarInt.GetEncodedLength(-64), Is.EqualTo(1)); // ZigZag: -64 -> 127
        Assert.That(VarInt.GetEncodedLength(64), Is.EqualTo(2)); // ZigZag: 64 -> 128
    }

    #endregion

    #region Edge Cases

    [Test]
    public void MaxLengthConstantTest()
    {
        Assert.That(VarInt.MAX_LENGTH, Is.EqualTo(10));
    }

    [Test]
    public void DecodeOnlyReadsNecessaryBytesTest()
    {
        // Buffer has extra data after the varint
        byte[] buffer = [0x05, 0xFF, 0xFF, 0xFF];
        
        var (value, bytesRead) = VarInt.DecodeUnsigned(buffer);
        
        Assert.That(value, Is.EqualTo(5UL));
        Assert.That(bytesRead, Is.EqualTo(1)); // Should only read 1 byte
    }

    [Test]
    public void ConsecutiveVarintsTest()
    {
        byte[] buffer = new byte[20];
        
        int pos = 0;
        pos += VarInt.EncodeUnsigned(buffer.AsSpan(pos), 100);
        pos += VarInt.EncodeUnsigned(buffer.AsSpan(pos), 200);
        pos += VarInt.EncodeUnsigned(buffer.AsSpan(pos), 300);
        
        int readPos = 0;
        var (v1, len1) = VarInt.DecodeUnsigned(buffer.AsSpan(readPos));
        readPos += len1;
        var (v2, len2) = VarInt.DecodeUnsigned(buffer.AsSpan(readPos));
        readPos += len2;
        var (v3, _) = VarInt.DecodeUnsigned(buffer.AsSpan(readPos));
        
        Assert.That(v1, Is.EqualTo(100UL));
        Assert.That(v2, Is.EqualTo(200UL));
        Assert.That(v3, Is.EqualTo(300UL));
    }

    #endregion
}

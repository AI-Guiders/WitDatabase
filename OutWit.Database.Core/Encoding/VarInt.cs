namespace OutWit.Database.Core.Encoding
{
    /// <summary>
    /// Variable-length integer encoding for compact storage of integers.
    /// Uses SQLite-style varint format where each byte uses 7 bits for data
    /// and 1 bit (high bit) as a continuation flag.
    /// </summary>
    /// <remarks>
    /// Format:
    /// - For values 0-127: 1 byte (high bit = 0)
    /// - For values 128-16383: 2 bytes (first byte high bit = 1)
    /// - And so on up to 9 bytes for 64-bit values
    /// </remarks>
    public static class VarInt
    {
        #region Constants

        /// <summary>
        /// Maximum number of bytes needed to encode a 64-bit integer.
        /// For unsigned: 9 bytes. For signed with ZigZag: 10 bytes.
        /// </summary>
        public const int MAX_LENGTH = 10;

        #endregion

        #region Encode

        /// <summary>
        /// Encodes a signed 64-bit integer to the buffer.
        /// Returns the number of bytes written.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if buffer is too small.</exception>
        public static int Encode(Span<byte> buffer, long value)
        {
            return EncodeUnsigned(buffer, ZigZagEncode(value));
        }

        /// <summary>
        /// Encodes an unsigned 64-bit integer to the buffer.
        /// Returns the number of bytes written.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if buffer is too small.</exception>
        public static int EncodeUnsigned(Span<byte> buffer, ulong value)
        {
            int requiredLength = GetEncodedLengthUnsigned(value);
            if (buffer.Length < requiredLength)
                throw new ArgumentException($"Buffer must be at least {requiredLength} bytes for value {value}", nameof(buffer));
            
            int i = 0;
        
            while (value >= 0x80)
            {
                buffer[i++] = (byte)(value | 0x80);
                value >>= 7;
            }
        
            buffer[i++] = (byte)value;
            return i;
        }

        /// <summary>
        /// Tries to encode an unsigned 64-bit integer to the buffer.
        /// Returns true if successful, false if buffer is too small.
        /// </summary>
        public static bool TryEncodeUnsigned(Span<byte> buffer, ulong value, out int bytesWritten)
        {
            int requiredLength = GetEncodedLengthUnsigned(value);
            if (buffer.Length < requiredLength)
            {
                bytesWritten = 0;
                return false;
            }
            
            bytesWritten = EncodeUnsigned(buffer, value);
            return true;
        }

        /// <summary>
        /// Tries to encode a signed 64-bit integer to the buffer.
        /// Returns true if successful, false if buffer is too small.
        /// </summary>
        public static bool TryEncode(Span<byte> buffer, long value, out int bytesWritten)
        {
            return TryEncodeUnsigned(buffer, ZigZagEncode(value), out bytesWritten);
        }

        #endregion

        #region Decode

        /// <summary>
        /// Decodes a signed 64-bit integer from the buffer.
        /// Returns the value and the number of bytes read.
        /// </summary>
        /// <exception cref="InvalidDataException">Thrown if buffer contains invalid varint data.</exception>
        public static (long Value, int BytesRead) Decode(ReadOnlySpan<byte> buffer)
        {
            var (unsignedValue, bytesRead) = DecodeUnsigned(buffer);
            return (ZigZagDecode(unsignedValue), bytesRead);
        }

        /// <summary>
        /// Decodes an unsigned 64-bit integer from the buffer.
        /// Returns the value and the number of bytes read.
        /// </summary>
        /// <exception cref="InvalidDataException">Thrown if buffer contains invalid varint data.</exception>
        public static (ulong Value, int BytesRead) DecodeUnsigned(ReadOnlySpan<byte> buffer)
        {
            if (buffer.IsEmpty)
                throw new InvalidDataException("Cannot decode varint from empty buffer");
            
            ulong result = 0;
            int shift = 0;
            int i = 0;

            while (i < buffer.Length && i < MAX_LENGTH)
            {
                byte b = buffer[i++];
                result |= (ulong)(b & 0x7F) << shift;
            
                if ((b & 0x80) == 0)
                {
                    return (result, i);
                }
            
                shift += 7;
            }

            throw new InvalidDataException("Invalid varint: buffer ended before varint was complete or varint is too long");
        }

        /// <summary>
        /// Tries to decode an unsigned 64-bit integer from the buffer.
        /// Returns true if successful, false if buffer contains invalid data.
        /// </summary>
        public static bool TryDecodeUnsigned(ReadOnlySpan<byte> buffer, out ulong value, out int bytesRead)
        {
            value = 0;
            bytesRead = 0;
            
            if (buffer.IsEmpty)
                return false;
            
            ulong result = 0;
            int shift = 0;
            int i = 0;

            while (i < buffer.Length && i < MAX_LENGTH)
            {
                byte b = buffer[i++];
                result |= (ulong)(b & 0x7F) << shift;
            
                if ((b & 0x80) == 0)
                {
                    value = result;
                    bytesRead = i;
                    return true;
                }
            
                shift += 7;
            }

            return false;
        }

        /// <summary>
        /// Tries to decode a signed 64-bit integer from the buffer.
        /// Returns true if successful, false if buffer contains invalid data.
        /// </summary>
        public static bool TryDecode(ReadOnlySpan<byte> buffer, out long value, out int bytesRead)
        {
            if (TryDecodeUnsigned(buffer, out ulong unsigned, out bytesRead))
            {
                value = ZigZagDecode(unsigned);
                return true;
            }
            
            value = 0;
            return false;
        }

        #endregion

        #region GetLength

        /// <summary>
        /// Gets the number of bytes needed to encode a signed value
        /// </summary>
        public static int GetEncodedLength(long value)
        {
            return GetEncodedLengthUnsigned(ZigZagEncode(value));
        }

        /// <summary>
        /// Gets the number of bytes needed to encode an unsigned value
        /// </summary>
        public static int GetEncodedLengthUnsigned(ulong value)
        {
            if (value < (1UL << 7)) return 1;
            if (value < (1UL << 14)) return 2;
            if (value < (1UL << 21)) return 3;
            if (value < (1UL << 28)) return 4;
            if (value < (1UL << 35)) return 5;
            if (value < (1UL << 42)) return 6;
            if (value < (1UL << 49)) return 7;
            if (value < (1UL << 56)) return 8;
            if (value < (1UL << 63)) return 9;
            return 10;
        }

        #endregion

        #region Tools

        /// <summary>
        /// ZigZag encodes a signed integer to unsigned, mapping negative values
        /// to positive in a way that small absolute values use fewer bytes.
        /// 0 -> 0, -1 -> 1, 1 -> 2, -2 -> 3, 2 -> 4, ...
        /// </summary>
        private static ulong ZigZagEncode(long value)
        {
            return (ulong)((value << 1) ^ (value >> 63));
        }

        /// <summary>
        /// ZigZag decodes an unsigned integer back to signed.
        /// </summary>
        private static long ZigZagDecode(ulong value)
        {
            return (long)(value >> 1) ^ -(long)(value & 1);
        }

        #endregion
    }
}

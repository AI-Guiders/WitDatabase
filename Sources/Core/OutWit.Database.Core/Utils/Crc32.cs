using System.Buffers;
using System.Buffers.Binary;

namespace OutWit.Database.Core.Utils;

/// <summary>
/// CRC32 calculation utility using IEEE polynomial (0xEDB88320).
/// Thread-safe and allocation-free for span inputs.
/// </summary>
public static class Crc32
{
    private static readonly uint[] TABLE = GenerateTable();

    private static uint[] GenerateTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint crc = i;
            for (int j = 0; j < 8; j++)
            {
                crc = (crc >> 1) ^ (0xEDB88320 & (~(crc & 1) + 1));
            }
            table[i] = crc;
        }
        return table;
    }

    /// <summary>
    /// Calculates CRC32 checksum for the given data.
    /// </summary>
    /// <param name="data">The data to calculate CRC32 for.</param>
    /// <returns>The CRC32 checksum.</returns>
    public static uint Calculate(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in data)
        {
            crc = TABLE[(byte)(crc ^ b)] ^ (crc >> 8);
        }
        return ~crc;
    }

    /// <summary>
    /// Calculates CRC32 checksum for the given byte array.
    /// </summary>
    public static uint Calculate(byte[] data)
    {
        return Calculate(data.AsSpan());
    }

    /// <summary>
    /// Verifies that data matches the expected CRC32 checksum.
    /// </summary>
    public static bool Verify(ReadOnlySpan<byte> data, uint expectedCrc)
    {
        return Calculate(data) == expectedCrc;
    }
}

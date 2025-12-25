using System.IO.Hashing;

namespace OutWit.Database.Core.Comparers;

/// <summary>
/// Byte array comparer using lexicographic ordering.
/// Implements both IComparer and IEqualityComparer for use in sorted and hash-based collections.
/// </summary>
public sealed class ByteArrayComparer : IComparer<byte[]>, IEqualityComparer<byte[]>
{
    #region Static

    /// <summary>
    /// Default singleton instance.
    /// </summary>
    public static readonly ByteArrayComparer Default = new();

    #endregion

    #region Functions

    private ByteArrayComparer()
    {

    }

    #endregion

    #region Compare

    /// <summary>
    /// Compares two byte arrays lexicographically.
    /// </summary>
    public int Compare(byte[]? x, byte[]? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        return x.AsSpan().SequenceCompareTo(y.AsSpan());
    }

    /// <summary>
    /// Compares a byte array with a ReadOnlySpan lexicographically.
    /// </summary>
    public int Compare(byte[]? x, ReadOnlySpan<byte> y)
    {
        if (x is null) return -1;
        return x.AsSpan().SequenceCompareTo(y);
    }

    /// <summary>
    /// Compares a ReadOnlySpan with a byte array lexicographically.
    /// </summary>
    public int Compare(ReadOnlySpan<byte> x, byte[]? y)
    {
        if (y is null) return 1;
        return x.SequenceCompareTo(y.AsSpan());
    }



    /// <summary>
    /// Compare two spans without allocation.
    /// </summary>
    public int Compare(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y)
    {
        return x.SequenceCompareTo(y);
    }

    #endregion

    #region Equality

    /// <summary>
    /// Determines whether two byte arrays are equal.
    /// </summary>
    public bool Equals(byte[]? x, byte[]? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;

        return x.AsSpan().SequenceEqual(y.AsSpan());
    }

    /// <summary>
    /// Returns a hash code for the byte array.
    /// Uses XxHash3 for high performance with SIMD acceleration.
    /// </summary>
    public int GetHashCode(byte[] obj)
    {
        ArgumentNullException.ThrowIfNull(obj);
        return GetHashCode(obj.AsSpan());
    }

    /// <summary>
    /// Returns a hash code for the byte span.
    /// Uses XxHash3 which automatically utilizes SIMD instructions when available.
    /// </summary>
    public static int GetHashCode(ReadOnlySpan<byte> data)
    {
        // XxHash3 is significantly faster than FNV-1a, especially for larger data
        // It automatically uses SIMD (SSE2/AVX2) when available
        return unchecked((int)XxHash3.HashToUInt64(data));
    }

    #endregion
}
namespace OutWit.Database.Core.Comparers;

/// <summary>
/// Byte array comparer using lexicographic ordering for LSM-Tree.
/// </summary>
internal sealed class LsmByteArrayComparer : IComparer<byte[]>
{
    public static readonly LsmByteArrayComparer Instance = new();

    private LsmByteArrayComparer() { }

    public int Compare(byte[]? x, byte[]? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        return x.AsSpan().SequenceCompareTo(y.AsSpan());
    }
}
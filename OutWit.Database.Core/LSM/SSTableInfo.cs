namespace OutWit.Database.Core.LSM;

/// <summary>
/// Metadata about an SSTable.
/// </summary>
public sealed class SSTableInfo
{
    public required string FilePath { get; init; }
    public required int EntryCount { get; init; }
    public required long FileSize { get; init; }
    public required int BlockCount { get; init; }
    public bool Encrypted { get; init; }
    public bool HasBloomFilter { get; init; }
}
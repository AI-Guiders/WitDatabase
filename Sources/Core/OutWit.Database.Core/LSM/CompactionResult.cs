namespace OutWit.Database.Core.LSM;

/// <summary>
/// Result of a compaction operation.
/// </summary>
public sealed class CompactionResult
{
    public int InputFiles { get; init; }
    public int InputEntries { get; init; }
    public int OutputEntries { get; init; }
    public int TombstonesRemoved { get; init; }
    public string? OutputFile { get; init; }

    public override string ToString() =>
        $"Compacted {InputFiles} files ({InputEntries} entries) → {OutputEntries} entries, {TombstonesRemoved} tombstones removed";
}
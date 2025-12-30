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
    public string? Error { get; init; }

    /// <summary>
    /// Gets whether the compaction completed successfully.
    /// </summary>
    public bool IsSuccess => Error == null;

    public override string ToString() =>
        Error != null
            ? $"Compaction failed: {Error}"
            : $"Compacted {InputFiles} files ({InputEntries} entries) → {OutputEntries} entries, {TombstonesRemoved} tombstones removed";
}
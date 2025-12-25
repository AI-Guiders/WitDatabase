namespace OutWit.Database.Core.LSM
{
    /// <summary>
    /// Immutable snapshot of LSM statistics at a point in time.
    /// </summary>
    public sealed record LsmStatisticsSnapshot
    {
        public long Gets { get; init; }
        public long Puts { get; init; }
        public long Deletes { get; init; }
        public long Scans { get; init; }
        public long Flushes { get; init; }
        public long Compactions { get; init; }
        public long BytesWritten { get; init; }
        public long BytesRead { get; init; }
        public long BloomFilterHits { get; init; }
        public long BloomFilterMisses { get; init; }

        public double BloomFilterEfficiency =>
            BloomFilterHits + BloomFilterMisses == 0 ? 0.0 :
            (double)BloomFilterHits / (BloomFilterHits + BloomFilterMisses);
    }
}

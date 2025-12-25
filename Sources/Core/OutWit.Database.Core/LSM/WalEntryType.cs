namespace OutWit.Database.Core.LSM
{
    /// <summary>
    /// Types of WAL entries.
    /// </summary>
    public enum WalEntryType : byte
    {
        Put = 1,
        Delete = 2
    }
}
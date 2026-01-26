namespace OutWit.Database.Parser.Schema.Types
{
    /// <summary>
    /// Specifies how a computed column value is stored.
    /// </summary>
    public enum ComputedColumnType
    {
        /// <summary>
        /// Not a computed column.
        /// </summary>
        None,

        /// <summary>
        /// Virtual computed column - value is calculated on each read.
        /// </summary>
        Virtual,

        /// <summary>
        /// Stored computed column - value is calculated and persisted on write.
        /// </summary>
        Stored
    }
}

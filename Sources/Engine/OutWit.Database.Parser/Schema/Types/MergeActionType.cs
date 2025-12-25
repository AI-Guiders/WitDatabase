namespace OutWit.Database.Parser.Schema.Types
{
    /// <summary>
    /// Type of action in a MERGE WHEN clause.
    /// </summary>
    public enum MergeActionType
    {
        /// <summary>
        /// UPDATE SET ... action.
        /// </summary>
        Update,

        /// <summary>
        /// DELETE action.
        /// </summary>
        Delete,

        /// <summary>
        /// INSERT (...) VALUES (...) action.
        /// </summary>
        Insert
    }
}

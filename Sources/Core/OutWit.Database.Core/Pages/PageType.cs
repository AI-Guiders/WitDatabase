namespace OutWit.Database.Core.Pages
{
    /// <summary>
    /// Types of pages in the database file
    /// </summary>
    public enum PageType : byte
    {
        /// <summary>
        /// Free page available for allocation
        /// </summary>
        Free = 0,

        /// <summary>
        /// Internal node of a B-tree (contains keys and child pointers)
        /// </summary>
        Internal = 1,

        /// <summary>
        /// Leaf node of a B-tree (contains actual data cells)
        /// </summary>
        Leaf = 2,

        /// <summary>
        /// Overflow page for large records that don't fit in a single page
        /// </summary>
        Overflow = 3,

        /// <summary>
        /// Free list trunk page (contains pointers to free pages)
        /// </summary>
        FreeList = 4,

        /// <summary>
        /// Schema page containing master catalog (tables, indexes, etc.)
        /// </summary>
        Schema = 5
    }
}

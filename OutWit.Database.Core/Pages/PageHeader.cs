using System.Buffers.Binary;

namespace OutWit.Database.Core.Pages
{
    /// <summary>
    /// Page header structure (16 bytes at the beginning of each page)
    /// </summary>
    /// <remarks>
    /// Layout:
    /// [0]     PageType (1 byte)
    /// [1]     Flags (1 byte)  
    /// [2-3]   CellCount (2 bytes, little-endian)
    /// [4-5]   FreeSpaceStart (2 bytes) - offset where free space begins
    /// [6-7]   FreeSpaceSize (2 bytes) - total fragmented free space
    /// [8-11]  RightChild (4 bytes) - rightmost child pointer for internal nodes
    /// [12-15] Reserved (4 bytes)
    /// </remarks>
    public struct PageHeader
    {
        #region Constants

        /// <summary>
        /// Size of the serialized header in bytes
        /// </summary>
        public const int PAGE_HEDER_SIZE = 16;

        #endregion

        #region Fields

        /// <summary>
        /// Type of this page
        /// </summary>
        public PageType PageType;

        /// <summary>
        /// Page flags (reserved for future use)
        /// </summary>
        public byte Flags;

        /// <summary>
        /// Number of cells in this page
        /// </summary>
        public ushort CellCount;

        /// <summary>
        /// Offset to the start of free space (where cell content area begins from bottom)
        /// </summary>
        public ushort FreeSpaceStart;

        /// <summary>
        /// Total size of fragmented free space within cell content area
        /// </summary>
        public ushort FragmentedFreeSpace;

        /// <summary>
        /// Right child page number (for internal B-tree nodes)
        /// </summary>
        public uint RightChild;

        /// <summary>
        /// Reserved bytes for future use
        /// </summary>
        public uint Reserved;

        #endregion

        #region Functions

        /// <summary>
        /// Writes this header to a byte span
        /// </summary>
        public readonly void WriteTo(Span<byte> buffer)
        {
            if (buffer.Length < PAGE_HEDER_SIZE)
                throw new ArgumentException($"Buffer must be at least {PAGE_HEDER_SIZE} bytes", nameof(buffer));

            buffer[0] = (byte)PageType;
            buffer[1] = Flags;
            BinaryPrimitives.WriteUInt16LittleEndian(buffer[2..], CellCount);
            BinaryPrimitives.WriteUInt16LittleEndian(buffer[4..], FreeSpaceStart);
            BinaryPrimitives.WriteUInt16LittleEndian(buffer[6..], FragmentedFreeSpace);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer[8..], RightChild);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer[12..], Reserved);
        }

        /// <summary>
        /// Reads a header from a byte span
        /// </summary>
        public static PageHeader ReadFrom(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length < PAGE_HEDER_SIZE)
                throw new ArgumentException($"Buffer must be at least {PAGE_HEDER_SIZE} bytes", nameof(buffer));

            return new PageHeader
            {
                PageType = (PageType)buffer[0],
                Flags = buffer[1],
                CellCount = BinaryPrimitives.ReadUInt16LittleEndian(buffer[2..]),
                FreeSpaceStart = BinaryPrimitives.ReadUInt16LittleEndian(buffer[4..]),
                FragmentedFreeSpace = BinaryPrimitives.ReadUInt16LittleEndian(buffer[6..]),
                RightChild = BinaryPrimitives.ReadUInt32LittleEndian(buffer[8..]),
                Reserved = BinaryPrimitives.ReadUInt32LittleEndian(buffer[12..])
            };
        }

        /// <summary>
        /// Creates an empty page header with default values for a new page
        /// </summary>
        public static PageHeader CreateEmpty(PageType pageType, int pageSize)
        {
            return new PageHeader
            {
                PageType = pageType,
                Flags = 0,
                CellCount = 0,
                FreeSpaceStart = (ushort)pageSize, // All space is free initially
                FragmentedFreeSpace = 0,
                RightChild = 0,
                Reserved = 0
            };
        }

        #endregion
    }
}

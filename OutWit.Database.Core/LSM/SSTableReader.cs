using System.Buffers.Binary;
using OutWit.Database.Core.Comparers;
using OutWit.Database.Core.Interfaces;

namespace OutWit.Database.Core.LSM
{
    /// <summary>
    /// Reads immutable SSTable files.
    /// Supports point lookups and range scans via block index.
    /// Handles both encrypted and unencrypted SSTables.
    /// </summary>
    public sealed class SSTableReader : IDisposable
    {
        #region Constants

        private const uint MAGIC = 0x53535431; // "SST1"
        private const uint FLAG_ENCRYPTED = 0x01;
        private const int FOOTER_SIZE_V1 = 8 + 4 + 4 + 4; // IndexOffset + IndexSize + EntryCount + Magic (old format)
        private const int FOOTER_SIZE_V2 = 8 + 4 + 4 + 4 + 4; // IndexOffset + IndexSize + EntryCount + Flags + Magic

        #endregion

        #region Fields

        private readonly FileStream m_stream;
        private readonly IBlockEncryptor? m_encryptor;
        private readonly List<IndexEntry> m_index = new();
        private readonly LsmByteArrayComparer m_comparer = LsmByteArrayComparer.Instance;
        private bool m_encrypted;
        private bool m_disposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Opens an SSTable file for reading.
        /// </summary>
        /// <param name="filePath">Path to the SSTable file.</param>
        /// <param name="encryptor">Optional block encryptor for decryption.</param>
        public SSTableReader(string filePath, IBlockEncryptor? encryptor = null)
        {
            FilePath = filePath;
            m_encryptor = encryptor;
            m_stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096);
            LoadIndex();
        }

        #endregion

        #region Functions

        /// <summary>
        /// Tries to get a value by key.
        /// </summary>
        /// <param name="key">The key to look up.</param>
        /// <param name="value">The value if found (null for tombstone).</param>
        /// <returns>True if key was found (including tombstones).</returns>
        public bool TryGet(ReadOnlySpan<byte> key, out byte[]? value)
        {
            ThrowIfDisposed();
            value = null;

            // Find the block that might contain the key
            var blockIndex = FindBlockForKey(key);
            if (blockIndex < 0)
                return false;

            // Search within the block
            var block = ReadBlock(blockIndex);
            if (block == null) return false; // Decryption failed
            return SearchInBlock(block, key, out value);
        }

        /// <summary>
        /// Scans all entries in the SSTable.
        /// </summary>
        public IEnumerable<(byte[] Key, byte[]? Value)> Scan()
        {
            ThrowIfDisposed();

            for (int i = 0; i < m_index.Count; i++)
            {
                var block = ReadBlock(i);
                if (block == null) continue; // Skip on decryption failure
                foreach (var entry in ParseBlock(block))
                {
                    yield return entry;
                }
            }
        }

        /// <summary>
        /// Scans entries in a key range.
        /// </summary>
        /// <param name="startKey">Start of range (inclusive), or null for beginning.</param>
        /// <param name="endKey">End of range (exclusive), or null for end.</param>
        public IEnumerable<(byte[] Key, byte[]? Value)> Scan(byte[]? startKey, byte[]? endKey)
        {
            ThrowIfDisposed();

            // Find starting block
            int startBlock = 0;
            if (startKey != null)
            {
                startBlock = FindBlockForKey(startKey);
                if (startBlock < 0) startBlock = 0;
            }

            // Scan blocks
            for (int i = startBlock; i < m_index.Count; i++)
            {
                var block = ReadBlock(i);
                if (block == null) continue;
                foreach (var entry in ParseBlock(block))
                {
                    // Skip entries before start
                    if (startKey != null && m_comparer.Compare(entry.Key, startKey) < 0)
                        continue;

                    // Stop at end
                    if (endKey != null && m_comparer.Compare(entry.Key, endKey) >= 0)
                        yield break;

                    yield return entry;
                }
            }
        }

        private int FindBlockForKey(ReadOnlySpan<byte> key)
        {
            // Binary search for the last block where FirstKey <= key
            int left = 0, right = m_index.Count - 1;
            int result = -1;

            while (left <= right)
            {
                int mid = left + (right - left) / 2;
                var cmp = key.SequenceCompareTo(m_index[mid].FirstKey);

                if (cmp >= 0)
                {
                    result = mid;
                    left = mid + 1;
                }
                else
                {
                    right = mid - 1;
                }
            }

            return result;
        }

        private byte[]? ReadBlock(int blockIndex)
        {
            var entry = m_index[blockIndex];
            m_stream.Position = entry.BlockOffset;

            if (m_encrypted)
            {
                // Read encrypted block: [len:4][encrypted data]
                var lenBuf = new byte[4];
                m_stream.ReadExactly(lenBuf);
                var encLen = BinaryPrimitives.ReadInt32LittleEndian(lenBuf);
            
                var encrypted = new byte[encLen];
                m_stream.ReadExactly(encrypted);
            
                if (m_encryptor == null)
                    throw new InvalidOperationException("SSTable is encrypted but no encryptor provided");
            
                return m_encryptor.Decrypt(encrypted, blockIndex);
            }
            else
            {
                var block = new byte[entry.BlockSize];
                m_stream.ReadExactly(block);
                return block;
            }
        }

        private byte[]? ReadIndexBlock(long indexOffset, int indexSize)
        {
            m_stream.Position = indexOffset;

            if (m_encrypted)
            {
                // Read encrypted index block
                var lenBuf = new byte[4];
                m_stream.ReadExactly(lenBuf);
                var encLen = BinaryPrimitives.ReadInt32LittleEndian(lenBuf);
            
                var encrypted = new byte[encLen];
                m_stream.ReadExactly(encrypted);
            
                if (m_encryptor == null)
                    throw new InvalidOperationException("SSTable is encrypted but no encryptor provided");
            
                // Index block uses special fixed block ID
                return m_encryptor.Decrypt(encrypted, SSTableBuilder.INDEX_BLOCK_ID);
            }
            else
            {
                var indexData = new byte[indexSize];
                m_stream.ReadExactly(indexData);
                return indexData;
            }
        }

        private bool SearchInBlock(byte[] block, ReadOnlySpan<byte> targetKey, out byte[]? value)
        {
            value = null;
            int offset = 0;

            while (offset < block.Length)
            {
                // Parse entry
                var keyLen = BinaryPrimitives.ReadInt32LittleEndian(block.AsSpan(offset));
                offset += 4;

                var key = block.AsSpan(offset, keyLen);
                offset += keyLen;

                var valueLen = BinaryPrimitives.ReadInt32LittleEndian(block.AsSpan(offset));
                offset += 4;

                byte[]? entryValue = null;
                if (valueLen >= 0)
                {
                    entryValue = block.AsSpan(offset, valueLen).ToArray();
                    offset += valueLen;
                }

                var cmp = targetKey.SequenceCompareTo(key);
                if (cmp == 0)
                {
                    value = entryValue;
                    return true;
                }
                if (cmp < 0)
                {
                    // Key not in this block (sorted order)
                    return false;
                }
            }

            return false;
        }

        private IEnumerable<(byte[] Key, byte[]? Value)> ParseBlock(byte[] block)
        {
            int offset = 0;

            while (offset < block.Length)
            {
                var keyLen = BinaryPrimitives.ReadInt32LittleEndian(block.AsSpan(offset));
                offset += 4;

                var key = block.AsSpan(offset, keyLen).ToArray();
                offset += keyLen;

                var valueLen = BinaryPrimitives.ReadInt32LittleEndian(block.AsSpan(offset));
                offset += 4;

                byte[]? value = null;
                if (valueLen >= 0)
                {
                    value = block.AsSpan(offset, valueLen).ToArray();
                    offset += valueLen;
                }

                yield return (key, value);
            }
        }

        private void LoadIndex()
        {
            // Try reading new format footer first (with flags), fall back to old format
            if (m_stream.Length < FOOTER_SIZE_V2)
                throw new InvalidDataException("SSTable file is too small");

            // Read footer (new format with flags)
            m_stream.Position = m_stream.Length - FOOTER_SIZE_V2;
            Span<byte> footer = stackalloc byte[FOOTER_SIZE_V2];
            m_stream.ReadExactly(footer);

            var indexOffset = BinaryPrimitives.ReadInt64LittleEndian(footer);
            var indexSize = BinaryPrimitives.ReadInt32LittleEndian(footer.Slice(8));
            EntryCount = BinaryPrimitives.ReadInt32LittleEndian(footer.Slice(12));
            var flags = BinaryPrimitives.ReadUInt32LittleEndian(footer.Slice(16));
            var magic = BinaryPrimitives.ReadUInt32LittleEndian(footer.Slice(20));

            if (magic != MAGIC)
            {
                // Try old format (without flags)
                m_stream.Position = m_stream.Length - FOOTER_SIZE_V1;
                Span<byte> footerV1 = stackalloc byte[FOOTER_SIZE_V1];
                m_stream.ReadExactly(footerV1);

                indexOffset = BinaryPrimitives.ReadInt64LittleEndian(footerV1);
                indexSize = BinaryPrimitives.ReadInt32LittleEndian(footerV1.Slice(8));
                EntryCount = BinaryPrimitives.ReadInt32LittleEndian(footerV1.Slice(12));
                magic = BinaryPrimitives.ReadUInt32LittleEndian(footerV1.Slice(16));
                flags = 0; // Old format assumed unencrypted

                if (magic != MAGIC)
                    throw new InvalidDataException($"Invalid SSTable magic: expected 0x{MAGIC:X8}, got 0x{magic:X8}");
            }

            m_encrypted = (flags & FLAG_ENCRYPTED) != 0;

            if (m_encrypted && m_encryptor == null)
                throw new InvalidOperationException("SSTable is encrypted but no encryptor provided");

            // Read and parse index
            var indexData = ReadIndexBlock(indexOffset, indexSize);
            if (indexData == null)
                throw new InvalidDataException("Failed to decrypt index block");

            ParseIndexBlock(indexData);
        }

        private void ParseIndexBlock(byte[] indexData)
        {
            int offset = 0;
            while (offset < indexData.Length)
            {
                var keyLen = BinaryPrimitives.ReadInt32LittleEndian(indexData.AsSpan(offset));
                offset += 4;

                var firstKey = indexData.AsSpan(offset, keyLen).ToArray();
                offset += keyLen;

                var blockOffset = BinaryPrimitives.ReadInt64LittleEndian(indexData.AsSpan(offset));
                offset += 8;

                var blockSize = BinaryPrimitives.ReadInt32LittleEndian(indexData.AsSpan(offset));
                offset += 4;

                m_index.Add(new IndexEntry
                {
                    FirstKey = firstKey,
                    BlockOffset = blockOffset,
                    BlockSize = blockSize
                });
            }
        }

        #endregion

        #region Tools

        private void ThrowIfDisposed()
        {
            if (m_disposed)
                throw new ObjectDisposedException(nameof(SSTableReader));
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (m_disposed) return;
            m_disposed = true;
            m_stream.Dispose();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the file path.
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// Gets the total number of entries.
        /// </summary>
        public int EntryCount { get; private set; }

        /// <summary>
        /// Gets the file size in bytes.
        /// </summary>
        public long FileSize => m_stream.Length;

        /// <summary>
        /// Gets whether this SSTable is encrypted.
        /// </summary>
        public bool IsEncrypted => m_encrypted;

        #endregion
    }
}

using OutWit.Database.Core.Interfaces;

namespace OutWit.Database.Core.LSM
{
    /// <summary>
    /// Builds immutable SSTable files from sorted key-value pairs.
    /// 
    /// SSTable Format (unencrypted):
    /// [Data Block 1] [Data Block 2] ... [Data Block N] [Index Block] [Footer]
    /// 
    /// SSTable Format (encrypted):
    /// [EncBlockLen:4][EncBlock 1] ... [EncBlockLen:4][Index Block Enc] [Footer]
    /// 
    /// Data Block: [Entry1][Entry2]...[EntryN]
    /// Entry: [KeyLen:4][Key][ValueLen:4][Value] (ValueLen = -1 for tombstone)
    /// 
    /// Index Block: [FirstKey1][BlockOffset1][BlockSize1]...
    /// 
    /// Footer: [IndexOffset:8][IndexSize:4][EntryCount:4][Flags:4][Magic:4]
    /// Flags: bit 0 = encrypted
    /// </summary>
    public sealed class SSTableBuilder : IDisposable
    {
        #region Constants

        private const uint MAGIC = 0x53535431; // "SST1"
        private const uint FLAG_ENCRYPTED = 0x01;
        internal const long INDEX_BLOCK_ID = -1; // Special block ID for index block encryption
        private const int DEFAULT_BLOCK_SIZE = 4096;
        private const int FOOTER_SIZE = 8 + 4 + 4 + 4 + 4; // IndexOffset + IndexSize + EntryCount + Flags + Magic

        #endregion

        #region Fields

        private readonly FileStream m_stream;
        private readonly BinaryWriter m_writer;
        private readonly int m_targetBlockSize;
        private readonly IBlockEncryptor? m_encryptor;
        private readonly List<IndexEntry> m_indexEntries = new();
    
        private MemoryStream m_currentBlock = new();
        private byte[]? m_currentBlockFirstKey;
        private int m_entryCount;
        private int m_blockCounter;
        private bool m_finished;
        private bool m_disposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new SSTableBuilder.
        /// </summary>
        /// <param name="filePath">Output file path.</param>
        /// <param name="targetBlockSize">Target size for data blocks.</param>
        /// <param name="encryptor">Optional block encryptor.</param>
        public SSTableBuilder(string filePath, int targetBlockSize = DEFAULT_BLOCK_SIZE, IBlockEncryptor? encryptor = null)
        {
            FilePath = filePath;
            m_targetBlockSize = targetBlockSize;
            m_encryptor = encryptor;
            m_stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096);
            m_writer = new BinaryWriter(m_stream);
        }

        #endregion

        #region Functions

        /// <summary>
        /// Adds a key-value pair. Must be called in sorted key order!
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value, or null for tombstone.</param>
        public void Add(byte[] key, byte[]? value)
        {
            ThrowIfDisposed();
            if (m_finished)
                throw new InvalidOperationException("SSTableBuilder has already been finished.");

            // Entry format: [KeyLen:4][Key][ValueLen:4][Value]
            // ValueLen = -1 for tombstone
            var entrySize = 4 + key.Length + 4 + (value?.Length ?? 0);

            // Check if we need to start a new block
            if (m_currentBlock.Length > 0 && m_currentBlock.Length + entrySize > m_targetBlockSize)
            {
                FlushBlock();
            }

            // Track first key of block
            if (m_currentBlock.Length == 0)
            {
                m_currentBlockFirstKey = key;
            }

            // Write entry to current block
            var entryWriter = new BinaryWriter(m_currentBlock);
            entryWriter.Write(key.Length);
            entryWriter.Write(key);
            entryWriter.Write(value?.Length ?? -1);
            if (value != null)
            {
                entryWriter.Write(value);
            }

            m_entryCount++;
        }

        /// <summary>
        /// Finishes writing the SSTable and returns metadata.
        /// </summary>
        /// <returns>SSTable metadata.</returns>
        public SSTableInfo Finish()
        {
            ThrowIfDisposed();
            if (m_finished)
                throw new InvalidOperationException("SSTableBuilder has already been finished.");

            // Flush remaining block
            if (m_currentBlock.Length > 0)
            {
                FlushBlock();
            }

            // Write index block
            var indexOffset = m_stream.Position;
            using var indexStream = new MemoryStream();
            using var indexWriter = new BinaryWriter(indexStream);
        
            foreach (var entry in m_indexEntries)
            {
                indexWriter.Write(entry.FirstKey.Length);
                indexWriter.Write(entry.FirstKey);
                indexWriter.Write(entry.BlockOffset);
                indexWriter.Write(entry.BlockSize);
            }
        
            var indexData = indexStream.ToArray();
            WriteIndexBlock(indexData); // Encrypt with special index block ID
            var indexSize = (int)(m_stream.Position - indexOffset);

            // Write footer (never encrypted)
            uint flags = m_encryptor != null ? FLAG_ENCRYPTED : 0;
            m_writer.Write(indexOffset);
            m_writer.Write(indexSize);
            m_writer.Write(m_entryCount);
            m_writer.Write(flags);
            m_writer.Write(MAGIC);

            m_writer.Flush();
            m_finished = true;

            return new SSTableInfo
            {
                FilePath = FilePath,
                EntryCount = m_entryCount,
                FileSize = m_stream.Length,
                BlockCount = m_indexEntries.Count,
                Encrypted = m_encryptor != null
            };
        }

        private void FlushBlock()
        {
            var blockOffset = m_stream.Position;
            var blockData = m_currentBlock.ToArray();
        
            var writtenSize = WriteBlock(blockData);

            m_indexEntries.Add(new IndexEntry
            {
                FirstKey = m_currentBlockFirstKey!,
                BlockOffset = blockOffset,
                BlockSize = writtenSize
            });

            m_currentBlock = new MemoryStream();
            m_currentBlockFirstKey = null;
        }

        private int WriteBlock(byte[] data)
        {
            if (m_encryptor != null)
            {
                // Encrypt block with block counter as block ID
                var encrypted = m_encryptor.Encrypt(data, m_blockCounter++);
                m_writer.Write(encrypted.Length);
                m_stream.Write(encrypted);
                return 4 + encrypted.Length;
            }
            else
            {
                m_stream.Write(data);
                return data.Length;
            }
        }

        private int WriteIndexBlock(byte[] data)
        {
            if (m_encryptor != null)
            {
                // Encrypt index block with special fixed ID
                var encrypted = m_encryptor.Encrypt(data, INDEX_BLOCK_ID);
                m_writer.Write(encrypted.Length);
                m_stream.Write(encrypted);
                return 4 + encrypted.Length;
            }
            else
            {
                m_stream.Write(data);
                return data.Length;
            }
        }

        #endregion

        #region Tools

        private void ThrowIfDisposed()
        {
            if (m_disposed)
                throw new ObjectDisposedException(nameof(SSTableBuilder));
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (m_disposed) return;
            m_disposed = true;

            if (!m_finished && m_entryCount > 0)
            {
                try { Finish(); } catch { /* Best effort */ }
            }

            m_writer.Dispose();
            m_stream.Dispose();
            m_currentBlock.Dispose();
        }

        #endregion
        
        #region Properties

        /// <summary>
        /// Gets the output file path.
        /// </summary>
        public string FilePath { get; }

        #endregion
    }
}


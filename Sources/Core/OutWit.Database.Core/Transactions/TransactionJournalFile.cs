using OutWit.Database.Core.Interfaces;
using System.Buffers.Binary;

namespace OutWit.Database.Core.Transactions
{
    /// <summary>
    /// Individual transaction journal file.
    /// </summary>
    internal sealed class TransactionJournalFile : IDisposable
    {
        #region Fields

        private readonly FileStream m_stream;
        private readonly BinaryWriter m_writer;
        private readonly IBlockEncryptor? m_encryptor;
        private readonly bool m_isEncrypted;
        private long m_entryCounter;
        private bool m_disposed;

        #endregion

        #region Constructors

        public TransactionJournalFile(string path, long transactionId, IBlockEncryptor? encryptor)
        {
            FilePath = path;
            TransactionId = transactionId;
            m_encryptor = encryptor;
            m_isEncrypted = encryptor != null;

            m_stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.WriteThrough);
            m_writer = new BinaryWriter(m_stream);

            // Write header: [Magic:4][TxId:8][Committed:1]
            var magic = m_isEncrypted 
                ? RollbackJournal.MAGIC_ENCRYPTED 
                : RollbackJournal.MAGIC;

            m_writer.Write(magic);
            m_writer.Write(transactionId);
            m_writer.Write(false); // Not committed yet
            m_writer.Flush();
        }

        #endregion

        #region Functions

        public void WriteEntry(RollbackEntryType type, ReadOnlySpan<byte> key, ReadOnlySpan<byte> oldValue)
        {
            // Build entry data: [Type:1][KeyLen:4][Key][ValueLen:4][Value]
            var entryData = new byte[1 + 4 + key.Length + 4 + oldValue.Length];
            var span = entryData.AsSpan();
            span[0] = (byte)type;
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(1), key.Length);
            key.CopyTo(span.Slice(5));
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(5 + key.Length), oldValue.Length);
            oldValue.CopyTo(span.Slice(9 + key.Length));

            if (m_isEncrypted)
            {
                var encrypted = m_encryptor!.Encrypt(entryData, m_entryCounter++);
                m_writer.Write(encrypted.Length);
                m_writer.Write(encrypted);
            }
            else
            {
                m_writer.Write(entryData);
            }
            m_writer.Flush();
        }

        public void MarkCommitted()
        {
            m_stream.Position = 4 + 8; // After magic and txId
            m_writer.Write(true);
            m_writer.Flush();
            m_stream.Flush(flushToDisk: true);
        }

        public void Sync()
        {
            m_writer.Flush();
            m_stream.Flush(flushToDisk: true);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (m_disposed) return;
            m_disposed = true;

            m_writer.Dispose();
            m_stream.Dispose();
        }

        #endregion

        #region Properties

        public string FilePath { get; }
        public long TransactionId { get; }

        #endregion
    }
}

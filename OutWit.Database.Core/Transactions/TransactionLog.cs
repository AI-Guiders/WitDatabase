using System.Buffers.Binary;

namespace OutWit.Database.Core.Transactions;

/// <summary>
/// Write-ahead log for transaction durability.
/// Records all operations before they are applied to the store.
/// </summary>
public sealed class TransactionLog : IDisposable
{
    #region Constants

    internal const uint MAGIC = 0x54584C47; // "TXLG"
    internal const int HEADER_SIZE = 4;

    #endregion

    #region Fields

    private readonly FileStream m_stream;
    private readonly BinaryWriter m_writer;
    private readonly object m_writeLock = new();
    private bool m_disposed;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates or opens a transaction log file.
    /// </summary>
    public TransactionLog(string filePath, bool createNew = false)
    {
        FilePath = filePath;

        var mode = createNew ? FileMode.Create : FileMode.OpenOrCreate;
        m_stream = new FileStream(filePath, mode, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.WriteThrough);
        m_writer = new BinaryWriter(m_stream);

        if (createNew || m_stream.Length == 0)
        {
            m_writer.Write(MAGIC);
            m_writer.Flush();
        }
        else
        {
            ValidateHeader();
        }
    }

    #endregion

    #region Functions

    /// <summary>
    /// Appends a transaction begin marker.
    /// </summary>
    public void AppendBegin(long transactionId)
    {
        ThrowIfDisposed();
        lock (m_writeLock)
        {
            m_stream.Position = m_stream.Length;
            m_writer.Write((byte)LogEntryType.Begin);
            m_writer.Write(transactionId);
            m_writer.Flush();
        }
    }

    /// <summary>
    /// Appends a Put operation to the log.
    /// </summary>
    public void AppendPut(long transactionId, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
    {
        ThrowIfDisposed();
        AppendEntry(transactionId, LogEntryType.Put, key, value);
    }

    /// <summary>
    /// Appends a Delete operation to the log.
    /// </summary>
    public void AppendDelete(long transactionId, ReadOnlySpan<byte> key)
    {
        ThrowIfDisposed();
        AppendEntry(transactionId, LogEntryType.Delete, key, ReadOnlySpan<byte>.Empty);
    }

    /// <summary>
    /// Appends a transaction commit marker.
    /// </summary>
    public void AppendCommit(long transactionId)
    {
        ThrowIfDisposed();
        lock (m_writeLock)
        {
            m_stream.Position = m_stream.Length;
            m_writer.Write((byte)LogEntryType.Commit);
            m_writer.Write(transactionId);
            m_writer.Flush();
            m_stream.Flush(flushToDisk: true);
        }
    }

    /// <summary>
    /// Appends a transaction rollback marker.
    /// </summary>
    public void AppendRollback(long transactionId)
    {
        ThrowIfDisposed();
        lock (m_writeLock)
        {
            m_stream.Position = m_stream.Length;
            m_writer.Write((byte)LogEntryType.Rollback);
            m_writer.Write(transactionId);
            m_writer.Flush();
        }
    }

    /// <summary>
    /// Ensures all pending writes are flushed to disk.
    /// </summary>
    public void Sync()
    {
        ThrowIfDisposed();
        lock (m_writeLock)
        {
            m_writer.Flush();
            m_stream.Flush(flushToDisk: true);
        }
    }

    /// <summary>
    /// Replays all committed transactions from the log.
    /// </summary>
    public int Replay(Action<long, byte[], byte[]> onPut, Action<long, byte[]> onDelete)
    {
        ThrowIfDisposed();

        lock (m_writeLock)
        {
            m_stream.Position = HEADER_SIZE;
            using var reader = new BinaryReader(m_stream, System.Text.Encoding.UTF8, leaveOpen: true);

            // First pass: find committed transactions
            var committedTxs = new HashSet<long>();
            var txOperations = new Dictionary<long, List<(LogEntryType Type, byte[] Key, byte[]? Value)>>();

            while (m_stream.Position < m_stream.Length)
            {
                try
                {
                    var entry = ReadEntry(reader);
                    if (entry == null) break;

                    switch (entry.Value.Type)
                    {
                        case LogEntryType.Begin:
                            txOperations[entry.Value.TransactionId] = new List<(LogEntryType, byte[], byte[]?)>();
                            break;

                        case LogEntryType.Put:
                        case LogEntryType.Delete:
                            if (txOperations.TryGetValue(entry.Value.TransactionId, out var ops))
                            {
                                ops.Add((entry.Value.Type, entry.Value.Key!, entry.Value.Value));
                            }
                            break;

                        case LogEntryType.Commit:
                            committedTxs.Add(entry.Value.TransactionId);
                            break;

                        case LogEntryType.Rollback:
                            txOperations.Remove(entry.Value.TransactionId);
                            break;
                    }
                }
                catch (EndOfStreamException)
                {
                    break;
                }
            }

            // Second pass: replay committed transactions
            int replayedCount = 0;
            foreach (var txId in committedTxs)
            {
                if (txOperations.TryGetValue(txId, out var operations))
                {
                    foreach (var (type, key, value) in operations)
                    {
                        switch (type)
                        {
                            case LogEntryType.Put:
                                onPut(txId, key, value!);
                                replayedCount++;
                                break;
                            case LogEntryType.Delete:
                                onDelete(txId, key);
                                replayedCount++;
                                break;
                        }
                    }
                }
            }

            return replayedCount;
        }
    }

    /// <summary>
    /// Truncates the log (after checkpoint).
    /// </summary>
    public void Truncate()
    {
        ThrowIfDisposed();
        lock (m_writeLock)
        {
            m_stream.SetLength(0);
            m_stream.Position = 0;
            m_writer.Write(MAGIC);
            m_writer.Flush();
        }
    }

    private void AppendEntry(long transactionId, LogEntryType type, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
    {
        lock (m_writeLock)
        {
            m_stream.Position = m_stream.Length;

            // Entry format: [Type:1][TxId:8][KeyLen:4][Key][ValueLen:4][Value]
            m_writer.Write((byte)type);
            m_writer.Write(transactionId);
            m_writer.Write(key.Length);
            m_writer.Write(key);
            m_writer.Write(value.Length);
            if (value.Length > 0)
            {
                m_writer.Write(value);
            }
            m_writer.Flush();
        }
    }

    private (LogEntryType Type, long TransactionId, byte[]? Key, byte[]? Value)? ReadEntry(BinaryReader reader)
    {
        var type = (LogEntryType)reader.ReadByte();
        var txId = reader.ReadInt64();

        switch (type)
        {
            case LogEntryType.Begin:
            case LogEntryType.Commit:
            case LogEntryType.Rollback:
                return (type, txId, null, null);

            case LogEntryType.Put:
            case LogEntryType.Delete:
                var keyLen = reader.ReadInt32();
                if (keyLen < 0 || keyLen > 1024 * 1024) return null;
                var key = reader.ReadBytes(keyLen);

                var valueLen = reader.ReadInt32();
                if (valueLen < 0 || valueLen > 100 * 1024 * 1024) return null;
                var value = valueLen > 0 ? reader.ReadBytes(valueLen) : null;

                return (type, txId, key, value);

            default:
                return null;
        }
    }

    #endregion

    #region Tools

    private void ValidateHeader()
    {
        if (m_stream.Length < HEADER_SIZE)
            throw new InvalidDataException("Transaction log file is too small");

        m_stream.Position = 0;
        Span<byte> header = stackalloc byte[HEADER_SIZE];
        m_stream.ReadExactly(header);

        var magic = BinaryPrimitives.ReadUInt32LittleEndian(header);
        if (magic != MAGIC)
            throw new InvalidDataException($"Invalid transaction log magic");
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(m_disposed, this);
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

    /// <summary>
    /// Gets the file path of this log.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets the current size of the log file in bytes.
    /// </summary>
    public long Size => m_stream.Length;

    #endregion
}

/// <summary>
/// Types of transaction log entries.
/// </summary>
public enum LogEntryType : byte
{
    Begin = 1,
    Put = 2,
    Delete = 3,
    Commit = 4,
    Rollback = 5
}

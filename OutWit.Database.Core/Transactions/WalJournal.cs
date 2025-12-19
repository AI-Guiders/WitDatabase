using System.Buffers.Binary;
using System.Security.Cryptography;
using OutWit.Database.Core.Interfaces;

namespace OutWit.Database.Core.Transactions;

/// <summary>
/// Write-Ahead Log journal implementation.
/// Changes are written to the log BEFORE being applied to the store.
/// On recovery, committed transactions are replayed.
/// Supports optional encryption via IBlockEncryptor.
/// Supports auto-checkpoint when size exceeds threshold.
/// </summary>
public sealed class WalJournal : ITransactionJournal
{
    #region Constants

    private const uint MAGIC = 0x57414C4A; // "WALJ"
    private const uint MAGIC_ENCRYPTED = 0x574A4345; // "WJCE" - encrypted journal
    private const int HEADER_SIZE = 12; // Magic(4) + EntryCounter(8)

    /// <summary>
    /// Default auto-checkpoint threshold: 1MB.
    /// </summary>
    public const long DEFAULT_CHECKPOINT_THRESHOLD = 1024 * 1024;

    #endregion

    #region Fields

    private readonly FileStream m_stream;
    private readonly BinaryWriter m_writer;
    private readonly IBlockEncryptor? m_encryptor;
    private readonly bool m_isEncrypted;
    private readonly object m_writeLock = new();
    private readonly long m_checkpointThreshold;
    private long m_entryCounter;
    private bool m_disposed;
    private long m_sizeAtLastCheckpoint;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates or opens a WAL journal file.
    /// </summary>
    /// <param name="filePath">Path to the journal file.</param>
    /// <param name="encryptor">Optional encryptor for encrypting entries.</param>
    /// <param name="createNew">If true, creates a new journal (overwrites existing).</param>
    /// <param name="checkpointThreshold">Auto-checkpoint size threshold (0 = disabled).</param>
    public WalJournal(string filePath, IBlockEncryptor? encryptor = null, bool createNew = false, long checkpointThreshold = DEFAULT_CHECKPOINT_THRESHOLD)
    {
        FilePath = filePath;
        m_encryptor = encryptor;
        m_isEncrypted = encryptor != null;
        m_checkpointThreshold = checkpointThreshold;

        var mode = createNew ? FileMode.Create : FileMode.OpenOrCreate;
        m_stream = new FileStream(filePath, mode, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.WriteThrough);
        m_writer = new BinaryWriter(m_stream);

        if (createNew || m_stream.Length == 0)
        {
            WriteHeader();
            m_sizeAtLastCheckpoint = HEADER_SIZE;
        }
        else
        {
            ValidateHeader();
            m_sizeAtLastCheckpoint = m_stream.Length;
        }
    }

    #endregion

    #region Functions

    /// <inheritdoc/>
    public void BeginTransaction(long transactionId)
    {
        ThrowIfDisposed();
        AppendControlEntry(WalEntryType.Begin, transactionId);
    }

    /// <inheritdoc/>
    public void LogPut(long transactionId, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value, ReadOnlySpan<byte> oldValue)
    {
        ThrowIfDisposed();
        // WAL doesn't need oldValue - we replay forward on recovery
        AppendDataEntry(transactionId, WalEntryType.Put, key, value);
    }

    /// <inheritdoc/>
    public void LogDelete(long transactionId, ReadOnlySpan<byte> key, ReadOnlySpan<byte> oldValue)
    {
        ThrowIfDisposed();
        // WAL doesn't need oldValue - we replay forward on recovery
        AppendDataEntry(transactionId, WalEntryType.Delete, key, ReadOnlySpan<byte>.Empty);
    }

    /// <inheritdoc/>
    public void CommitTransaction(long transactionId)
    {
        ThrowIfDisposed();
        lock (m_writeLock)
        {
            AppendControlEntryInternal(WalEntryType.Commit, transactionId);
            UpdateHeader();
            m_stream.Flush(flushToDisk: true);
        }
    }

    /// <inheritdoc/>
    public void RollbackTransaction(long transactionId)
    {
        ThrowIfDisposed();
        AppendControlEntry(WalEntryType.Rollback, transactionId);
    }

    /// <inheritdoc/>
    public void Sync()
    {
        ThrowIfDisposed();
        lock (m_writeLock)
        {
            UpdateHeader();
            m_writer.Flush();
            m_stream.Flush(flushToDisk: true);
        }
    }

    /// <inheritdoc/>
    public int Recover(IKeyValueStore store)
    {
        ThrowIfDisposed();

        lock (m_writeLock)
        {
            m_stream.Position = HEADER_SIZE;
            using var reader = new BinaryReader(m_stream, System.Text.Encoding.UTF8, leaveOpen: true);

            // First pass: find committed transactions
            var committedTxs = new HashSet<long>();
            var txOperations = new Dictionary<long, List<(WalEntryType Type, byte[] Key, byte[]? Value)>>();
            long entryId = 0;

            while (m_stream.Position < m_stream.Length)
            {
                try
                {
                    var entry = ReadEntry(reader, entryId++);
                    if (entry == null) break;

                    switch (entry.Value.Type)
                    {
                        case WalEntryType.Begin:
                            txOperations[entry.Value.TransactionId] = new();
                            break;

                        case WalEntryType.Put:
                        case WalEntryType.Delete:
                            if (txOperations.TryGetValue(entry.Value.TransactionId, out var ops))
                            {
                                ops.Add((entry.Value.Type, entry.Value.Key!, entry.Value.Value));
                            }
                            break;

                        case WalEntryType.Commit:
                            committedTxs.Add(entry.Value.TransactionId);
                            break;

                        case WalEntryType.Rollback:
                            txOperations.Remove(entry.Value.TransactionId);
                            break;
                    }
                }
                catch (EndOfStreamException)
                {
                    break;
                }
                catch (CryptographicException)
                {
                    break; // Encryption error - stop
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
                            case WalEntryType.Put:
                                store.Put(key, value!);
                                replayedCount++;
                                break;
                            case WalEntryType.Delete:
                                store.Delete(key);
                                replayedCount++;
                                break;
                        }
                    }
                }
            }

            return replayedCount;
        }
    }

    /// <inheritdoc/>
    public void Checkpoint()
    {
        ThrowIfDisposed();
        lock (m_writeLock)
        {
            m_stream.SetLength(0);
            m_stream.Position = 0;
            m_entryCounter = 0;
            WriteHeader();
            m_sizeAtLastCheckpoint = HEADER_SIZE;
        }
    }

    private void WriteHeader()
    {
        var magic = m_isEncrypted ? MAGIC_ENCRYPTED : MAGIC;
        m_writer.Write(magic);
        m_writer.Write(m_entryCounter);
        m_writer.Flush();
    }

    private void UpdateHeader()
    {
        var pos = m_stream.Position;
        m_stream.Position = 4; // After magic
        m_writer.Write(m_entryCounter);
        m_stream.Position = pos;
    }

    private void AppendControlEntry(WalEntryType type, long transactionId)
    {
        lock (m_writeLock)
        {
            AppendControlEntryInternal(type, transactionId);
        }
    }

    private void AppendControlEntryInternal(WalEntryType type, long transactionId)
    {
        m_stream.Position = m_stream.Length;

        // Control entry format: [Type:1][TxId:8]
        var entryData = new byte[1 + 8];
        entryData[0] = (byte)type;
        BinaryPrimitives.WriteInt64LittleEndian(entryData.AsSpan(1), transactionId);

        WriteEntry(entryData);
    }

    private void AppendDataEntry(long transactionId, WalEntryType type, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
    {
        lock (m_writeLock)
        {
            m_stream.Position = m_stream.Length;

            // Data entry format: [Type:1][TxId:8][KeyLen:4][Key][ValueLen:4][Value]
            var entryData = new byte[1 + 8 + 4 + key.Length + 4 + value.Length];
            var span = entryData.AsSpan();
            span[0] = (byte)type;
            BinaryPrimitives.WriteInt64LittleEndian(span.Slice(1), transactionId);
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(9), key.Length);
            key.CopyTo(span.Slice(13));
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(13 + key.Length), value.Length);
            value.CopyTo(span.Slice(17 + key.Length));

            WriteEntry(entryData);
        }
    }

    private void WriteEntry(byte[] entryData)
    {
        if (m_isEncrypted)
        {
            var encrypted = m_encryptor!.Encrypt(entryData, m_entryCounter);
            m_writer.Write(encrypted.Length);
            m_writer.Write(encrypted);
        }
        else
        {
            m_writer.Write(entryData);
        }
        m_entryCounter++;
        m_writer.Flush();
    }

    private (WalEntryType Type, long TransactionId, byte[]? Key, byte[]? Value)? ReadEntry(BinaryReader reader, long entryId)
    {
        byte[] entryData;

        if (m_isEncrypted)
        {
            var encLen = reader.ReadInt32();
            if (encLen < 0 || encLen > 100 * 1024 * 1024) return null;
            var encrypted = reader.ReadBytes(encLen);
            
            var decrypted = m_encryptor!.Decrypt(encrypted, entryId);
            if (decrypted == null) return null;
            entryData = decrypted;
        }
        else
        {
            // For unencrypted, we need to peek the type to know how much to read
            var type = (WalEntryType)reader.ReadByte();
            var txId = reader.ReadInt64();

            switch (type)
            {
                case WalEntryType.Begin:
                case WalEntryType.Commit:
                case WalEntryType.Rollback:
                    return (type, txId, null, null);

                case WalEntryType.Put:
                case WalEntryType.Delete:
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

        // Parse encrypted entry data
        if (entryData.Length < 9) return null;
        var entryType = (WalEntryType)entryData[0];
        var transactionId = BinaryPrimitives.ReadInt64LittleEndian(entryData.AsSpan(1));

        switch (entryType)
        {
            case WalEntryType.Begin:
            case WalEntryType.Commit:
            case WalEntryType.Rollback:
                return (entryType, transactionId, null, null);

            case WalEntryType.Put:
            case WalEntryType.Delete:
                if (entryData.Length < 17) return null;
                var kLen = BinaryPrimitives.ReadInt32LittleEndian(entryData.AsSpan(9));
                if (kLen < 0 || 13 + kLen + 4 > entryData.Length) return null;
                var k = entryData.AsSpan(13, kLen).ToArray();
                var vLen = BinaryPrimitives.ReadInt32LittleEndian(entryData.AsSpan(13 + kLen));
                var v = vLen > 0 ? entryData.AsSpan(17 + kLen, vLen).ToArray() : null;
                return (entryType, transactionId, k, v);

            default:
                return null;
        }
    }

    #endregion

    #region Tools

    private void ValidateHeader()
    {
        if (m_stream.Length < HEADER_SIZE)
            throw new InvalidDataException("WAL journal file is too small");

        m_stream.Position = 0;
        Span<byte> header = stackalloc byte[HEADER_SIZE];
        m_stream.ReadExactly(header);

        var magic = BinaryPrimitives.ReadUInt32LittleEndian(header);
        
        if (magic != MAGIC && magic != MAGIC_ENCRYPTED)
            throw new InvalidDataException($"Invalid WAL journal magic: got 0x{magic:X8}");
        
        if (m_isEncrypted && magic != MAGIC_ENCRYPTED)
            throw new InvalidDataException("WAL journal is not encrypted but encryptor was provided");
        
        if (!m_isEncrypted && magic == MAGIC_ENCRYPTED)
            throw new InvalidDataException("WAL journal is encrypted but no encryptor was provided");

        m_entryCounter = BinaryPrimitives.ReadInt64LittleEndian(header.Slice(4));
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

        lock (m_writeLock)
        {
            try { UpdateHeader(); } catch { }
        }
        m_writer.Dispose();
        m_stream.Dispose();
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the file path of this journal.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets the current size of the journal file in bytes.
    /// </summary>
    public long Size => m_stream.Length;

    /// <summary>
    /// Gets whether this journal is encrypted.
    /// </summary>
    public bool IsEncrypted => m_isEncrypted;

    /// <summary>
    /// Gets the auto-checkpoint threshold in bytes.
    /// </summary>
    public long CheckpointThreshold => m_checkpointThreshold;

    /// <summary>
    /// Gets whether auto-checkpoint is due (size exceeds threshold).
    /// </summary>
    public bool NeedsCheckpoint => m_checkpointThreshold > 0 && Size > m_sizeAtLastCheckpoint + m_checkpointThreshold;

    #endregion
}

/// <summary>
/// WAL entry types.
/// </summary>
internal enum WalEntryType : byte
{
    Begin = 1,
    Put = 2,
    Delete = 3,
    Commit = 4,
    Rollback = 5
}

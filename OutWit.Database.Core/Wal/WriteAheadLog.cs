using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using OutWit.Database.Core.Interfaces;
using OutWit.Database.Core.Utils;

namespace OutWit.Database.Core.Wal;

/// <summary>
/// Unified Write-Ahead Log implementation.
/// Supports both simple operations (for LSM) and transactions (for BTree).
/// Features:
/// - CRC32 integrity checking
/// - Optional encryption via IBlockEncryptor
/// - ArrayPool for reduced allocations
/// - Transaction support with begin/commit/rollback
/// </summary>
public sealed class WriteAheadLog : IWriteAheadLog
{
    #region Constants

    private const uint MAGIC = 0x57414C32; // "WAL2" - unified WAL v2
    private const uint MAGIC_ENCRYPTED = 0x57414C45; // "WALE" - encrypted
    private const int HEADER_SIZE = 16; // Magic(4) + Version(4) + EntryCounter(8)
    private const int CURRENT_VERSION = 2;

    #endregion

    #region Fields

    private readonly FileStream m_stream;
    private readonly BinaryWriter m_writer;
    private readonly IBlockEncryptor? m_encryptor;
    private readonly bool m_isEncrypted;
    private readonly object m_writeLock = new();
    private long m_entryCounter;
    private bool m_disposed;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates or opens a WAL file.
    /// </summary>
    /// <param name="filePath">Path to the WAL file.</param>
    /// <param name="encryptor">Optional encryptor for encrypting entries.</param>
    /// <param name="createNew">If true, creates a new WAL (overwrites existing).</param>
    public WriteAheadLog(string filePath, IBlockEncryptor? encryptor = null, bool createNew = false)
    {
        FilePath = filePath;
        m_encryptor = encryptor;
        m_isEncrypted = encryptor != null;

        // Ensure directory exists
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var mode = createNew ? FileMode.Create : FileMode.OpenOrCreate;
        m_stream = new FileStream(filePath, mode, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.WriteThrough);
        m_writer = new BinaryWriter(m_stream);

        if (createNew || m_stream.Length == 0)
        {
            WriteHeader();
        }
        else
        {
            ValidateHeader();
        }
    }

    #endregion

    #region IWriteAheadLog Implementation

    /// <inheritdoc/>
    public string FilePath { get; }

    /// <inheritdoc/>
    public long Size => m_stream.Length;

    /// <inheritdoc/>
    public bool IsEncrypted => m_isEncrypted;

    /// <inheritdoc/>
    public long EntryCount => Volatile.Read(ref m_entryCounter);

    /// <inheritdoc/>
    public void AppendPut(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value, long transactionId = 0)
    {
        ThrowIfDisposed();
        AppendDataEntry(WalEntryType.Put, transactionId, key, value);
    }

    /// <inheritdoc/>
    public void AppendDelete(ReadOnlySpan<byte> key, long transactionId = 0)
    {
        ThrowIfDisposed();
        AppendDataEntry(WalEntryType.Delete, transactionId, key, ReadOnlySpan<byte>.Empty);
    }

    /// <inheritdoc/>
    public void AppendBeginTransaction(long transactionId)
    {
        ThrowIfDisposed();
        AppendControlEntry(WalEntryType.BeginTransaction, transactionId);
    }

    /// <inheritdoc/>
    public void AppendCommitTransaction(long transactionId)
    {
        ThrowIfDisposed();
        lock (m_writeLock)
        {
            AppendControlEntryInternal(WalEntryType.CommitTransaction, transactionId);
            UpdateHeader();
            m_stream.Flush(flushToDisk: true);
        }
    }

    /// <inheritdoc/>
    public void AppendRollbackTransaction(long transactionId)
    {
        ThrowIfDisposed();
        AppendControlEntry(WalEntryType.RollbackTransaction, transactionId);
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
    public void Truncate()
    {
        ThrowIfDisposed();
        lock (m_writeLock)
        {
            m_stream.SetLength(0);
            m_stream.Position = 0;
            m_entryCounter = 0;
            WriteHeader();
        }
    }

    /// <inheritdoc/>
    public int Replay(IWalReplayVisitor visitor)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(visitor);

        lock (m_writeLock)
        {
            m_stream.Position = HEADER_SIZE;
            using var reader = new BinaryReader(m_stream, System.Text.Encoding.UTF8, leaveOpen: true);
            int count = 0;
            long entryId = 0;

            while (m_stream.Position < m_stream.Length)
            {
                try
                {
                    var entry = ReadEntry(reader, entryId++);
                    if (entry == null) break;

                    switch (entry.Value.Type)
                    {
                        case WalEntryType.Put:
                            visitor.OnPut(entry.Value.TransactionId, entry.Value.Key!, entry.Value.Value!);
                            break;
                        case WalEntryType.Delete:
                            visitor.OnDelete(entry.Value.TransactionId, entry.Value.Key!);
                            break;
                        case WalEntryType.BeginTransaction:
                            visitor.OnBeginTransaction(entry.Value.TransactionId);
                            break;
                        case WalEntryType.CommitTransaction:
                            visitor.OnCommitTransaction(entry.Value.TransactionId);
                            break;
                        case WalEntryType.RollbackTransaction:
                            visitor.OnRollbackTransaction(entry.Value.TransactionId);
                            break;
                    }
                    count++;
                }
                catch (EndOfStreamException)
                {
                    break;
                }
                catch (CryptographicException)
                {
                    break;
                }
            }

            return count;
        }
    }

    #endregion

    #region Private Methods

    private void WriteHeader()
    {
        var magic = m_isEncrypted ? MAGIC_ENCRYPTED : MAGIC;
        m_writer.Write(magic);
        m_writer.Write(CURRENT_VERSION);
        m_writer.Write(m_entryCounter);
        m_writer.Flush();
    }

    private void UpdateHeader()
    {
        var pos = m_stream.Position;
        m_stream.Position = 8; // After magic and version
        m_writer.Write(m_entryCounter);
        m_stream.Position = pos;
    }

    private void ValidateHeader()
    {
        if (m_stream.Length < HEADER_SIZE)
            throw new InvalidDataException("WAL file is too small");

        m_stream.Position = 0;
        Span<byte> header = stackalloc byte[HEADER_SIZE];
        m_stream.ReadExactly(header);

        var magic = BinaryPrimitives.ReadUInt32LittleEndian(header);
        
        if (magic != MAGIC && magic != MAGIC_ENCRYPTED)
            throw new InvalidDataException($"Invalid WAL magic: got 0x{magic:X8}");

        if (m_isEncrypted && magic != MAGIC_ENCRYPTED)
            throw new InvalidDataException("WAL is not encrypted but encryptor was provided");

        if (!m_isEncrypted && magic == MAGIC_ENCRYPTED)
            throw new InvalidDataException("WAL is encrypted but no encryptor was provided");

        var version = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(4));
        if (version > CURRENT_VERSION)
            throw new InvalidDataException($"WAL version {version} is not supported (max: {CURRENT_VERSION})");

        m_entryCounter = BinaryPrimitives.ReadInt64LittleEndian(header.Slice(8));
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

        // Control entry format: [CRC32:4][Type:1][TxId:8]
        const int entrySize = 4 + 1 + 8;
        Span<byte> buffer = stackalloc byte[entrySize];
        
        // Write entry data (after CRC placeholder)
        buffer[4] = (byte)type;
        BinaryPrimitives.WriteInt64LittleEndian(buffer.Slice(5), transactionId);
        
        // Calculate and write CRC
        var crc = Crc32.Calculate(buffer.Slice(4));
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, crc);

        WriteEntryBuffer(buffer);
    }

    private void AppendDataEntry(WalEntryType type, long transactionId, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
    {
        lock (m_writeLock)
        {
            m_stream.Position = m_stream.Length;

            // Data entry format: [CRC32:4][Type:1][TxId:8][KeyLen:4][Key][ValueLen:4][Value]
            var entryDataSize = 1 + 8 + 4 + key.Length + 4 + value.Length;
            var totalSize = 4 + entryDataSize; // CRC + data
            
            var rentedBuffer = ArrayPool<byte>.Shared.Rent(totalSize);
            try
            {
                var span = rentedBuffer.AsSpan(0, totalSize);
                var dataSpan = span.Slice(4); // After CRC
                
                dataSpan[0] = (byte)type;
                BinaryPrimitives.WriteInt64LittleEndian(dataSpan.Slice(1), transactionId);
                BinaryPrimitives.WriteInt32LittleEndian(dataSpan.Slice(9), key.Length);
                key.CopyTo(dataSpan.Slice(13));
                BinaryPrimitives.WriteInt32LittleEndian(dataSpan.Slice(13 + key.Length), value.Length);
                value.CopyTo(dataSpan.Slice(17 + key.Length));
                
                // Calculate and write CRC
                var crc = Crc32.Calculate(dataSpan);
                BinaryPrimitives.WriteUInt32LittleEndian(span, crc);

                WriteEntryBuffer(span);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }
    }

    private void WriteEntryBuffer(ReadOnlySpan<byte> buffer)
    {
        if (m_isEncrypted)
        {
            var encrypted = m_encryptor!.Encrypt(buffer.ToArray(), m_entryCounter);
            m_writer.Write(encrypted.Length);
            m_writer.Write(encrypted);
        }
        else
        {
            m_stream.Write(buffer);
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
            
            var rentedBuffer = ArrayPool<byte>.Shared.Rent(encLen);
            try
            {
                m_stream.ReadExactly(rentedBuffer.AsSpan(0, encLen));
                var decrypted = m_encryptor!.Decrypt(rentedBuffer.AsSpan(0, encLen).ToArray(), entryId);
                if (decrypted == null) return null;
                entryData = decrypted;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }
        else
        {
            // Read CRC
            var expectedCrc = reader.ReadUInt32();
            
            // Read type to determine entry size
            var type = (WalEntryType)reader.ReadByte();
            var txId = reader.ReadInt64();

            switch (type)
            {
                case WalEntryType.BeginTransaction:
                case WalEntryType.CommitTransaction:
                case WalEntryType.RollbackTransaction:
                    // Verify CRC for control entry
                    Span<byte> controlData = stackalloc byte[9];
                    controlData[0] = (byte)type;
                    BinaryPrimitives.WriteInt64LittleEndian(controlData.Slice(1), txId);
                    if (!Crc32.Verify(controlData, expectedCrc)) return null;
                    return (type, txId, null, null);

                case WalEntryType.Put:
                case WalEntryType.Delete:
                    var keyLen = reader.ReadInt32();
                    if (keyLen < 0 || keyLen > 1024 * 1024) return null;
                    var key = reader.ReadBytes(keyLen);

                    var valueLen = reader.ReadInt32();
                    if (valueLen < 0 || valueLen > 100 * 1024 * 1024) return null;
                    var value = valueLen > 0 ? reader.ReadBytes(valueLen) : Array.Empty<byte>();

                    // Verify CRC
                    var dataLen = 1 + 8 + 4 + keyLen + 4 + valueLen;
                    var rentedBuffer = ArrayPool<byte>.Shared.Rent(dataLen);
                    try
                    {
                        var span = rentedBuffer.AsSpan(0, dataLen);
                        span[0] = (byte)type;
                        BinaryPrimitives.WriteInt64LittleEndian(span.Slice(1), txId);
                        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(9), keyLen);
                        key.CopyTo(span.Slice(13));
                        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(13 + keyLen), valueLen);
                        if (value.Length > 0) value.CopyTo(span.Slice(17 + keyLen));
                        
                        if (!Crc32.Verify(span, expectedCrc)) return null;
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(rentedBuffer);
                    }

                    return (type, txId, key, value);

                default:
                    return null;
            }
        }

        // Parse encrypted data
        if (entryData.Length < 13) return null; // CRC(4) + Type(1) + TxId(8)
        
        var storedCrc = BinaryPrimitives.ReadUInt32LittleEndian(entryData);
        var dataSpan = entryData.AsSpan(4);
        
        if (!Crc32.Verify(dataSpan, storedCrc)) return null;

        var entryType = (WalEntryType)dataSpan[0];
        var transactionId = BinaryPrimitives.ReadInt64LittleEndian(dataSpan.Slice(1));

        switch (entryType)
        {
            case WalEntryType.BeginTransaction:
            case WalEntryType.CommitTransaction:
            case WalEntryType.RollbackTransaction:
                return (entryType, transactionId, null, null);

            case WalEntryType.Put:
            case WalEntryType.Delete:
                if (dataSpan.Length < 17) return null;
                var kLen = BinaryPrimitives.ReadInt32LittleEndian(dataSpan.Slice(9));
                if (kLen < 0 || 13 + kLen + 4 > dataSpan.Length) return null;
                var k = dataSpan.Slice(13, kLen).ToArray();
                var vLen = BinaryPrimitives.ReadInt32LittleEndian(dataSpan.Slice(13 + kLen));
                var v = vLen > 0 ? dataSpan.Slice(17 + kLen, vLen).ToArray() : Array.Empty<byte>();
                return (entryType, transactionId, k, v);

            default:
                return null;
        }
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
}

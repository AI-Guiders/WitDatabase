namespace OutWit.Database.Core.Interfaces;

/// <summary>
/// Entry types for Write-Ahead Log operations.
/// </summary>
public enum WalEntryType : byte
{
    /// <summary>Put operation - insert or update a key-value pair.</summary>
    Put = 1,
    
    /// <summary>Delete operation - remove a key.</summary>
    Delete = 2,
    
    /// <summary>Begin transaction marker.</summary>
    BeginTransaction = 3,
    
    /// <summary>Commit transaction marker.</summary>
    CommitTransaction = 4,
    
    /// <summary>Rollback transaction marker.</summary>
    RollbackTransaction = 5
}

/// <summary>
/// Interface for Write-Ahead Log implementations.
/// Provides durability for key-value operations by writing to persistent storage
/// before applying changes to the main data structure.
/// </summary>
public interface IWriteAheadLog : IDisposable
{
    /// <summary>
    /// Gets the file path of this WAL.
    /// </summary>
    string FilePath { get; }
    
    /// <summary>
    /// Gets the current size of the WAL file in bytes.
    /// </summary>
    long Size { get; }
    
    /// <summary>
    /// Gets whether this WAL is encrypted.
    /// </summary>
    bool IsEncrypted { get; }
    
    /// <summary>
    /// Gets the number of entries written to this WAL.
    /// </summary>
    long EntryCount { get; }
    
    /// <summary>
    /// Appends a Put operation to the log.
    /// </summary>
    /// <param name="key">The key to put.</param>
    /// <param name="value">The value to associate with the key.</param>
    /// <param name="transactionId">Optional transaction ID (0 = no transaction).</param>
    void AppendPut(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value, long transactionId = 0);
    
    /// <summary>
    /// Appends a Delete operation to the log.
    /// </summary>
    /// <param name="key">The key to delete.</param>
    /// <param name="transactionId">Optional transaction ID (0 = no transaction).</param>
    void AppendDelete(ReadOnlySpan<byte> key, long transactionId = 0);
    
    /// <summary>
    /// Appends a transaction begin marker.
    /// </summary>
    /// <param name="transactionId">The transaction ID.</param>
    void AppendBeginTransaction(long transactionId);
    
    /// <summary>
    /// Appends a transaction commit marker.
    /// </summary>
    /// <param name="transactionId">The transaction ID.</param>
    void AppendCommitTransaction(long transactionId);
    
    /// <summary>
    /// Appends a transaction rollback marker.
    /// </summary>
    /// <param name="transactionId">The transaction ID.</param>
    void AppendRollbackTransaction(long transactionId);
    
    /// <summary>
    /// Ensures all pending writes are flushed to disk.
    /// </summary>
    void Sync();
    
    /// <summary>
    /// Truncates the WAL, removing all entries.
    /// Should be called after successful checkpoint/flush.
    /// </summary>
    void Truncate();
    
    /// <summary>
    /// Replays all entries in the log.
    /// </summary>
    /// <param name="visitor">Visitor to receive replay callbacks.</param>
    /// <returns>Number of entries replayed.</returns>
    int Replay(IWalReplayVisitor visitor);
}

/// <summary>
/// Visitor interface for WAL replay operations.
/// </summary>
public interface IWalReplayVisitor
{
    /// <summary>Called for each Put entry during replay.</summary>
    void OnPut(long transactionId, byte[] key, byte[] value);
    
    /// <summary>Called for each Delete entry during replay.</summary>
    void OnDelete(long transactionId, byte[] key);
    
    /// <summary>Called for each BeginTransaction entry during replay.</summary>
    void OnBeginTransaction(long transactionId);
    
    /// <summary>Called for each CommitTransaction entry during replay.</summary>
    void OnCommitTransaction(long transactionId);
    
    /// <summary>Called for each RollbackTransaction entry during replay.</summary>
    void OnRollbackTransaction(long transactionId);
}

/// <summary>
/// Simple replay visitor that only handles Put and Delete operations.
/// Useful for non-transactional WAL replay (e.g., LSM MemTable recovery).
/// </summary>
public class SimpleWalReplayVisitor : IWalReplayVisitor
{
    private readonly Action<byte[], byte[]> m_onPut;
    private readonly Action<byte[]> m_onDelete;

    public SimpleWalReplayVisitor(Action<byte[], byte[]> onPut, Action<byte[]> onDelete)
    {
        m_onPut = onPut ?? throw new ArgumentNullException(nameof(onPut));
        m_onDelete = onDelete ?? throw new ArgumentNullException(nameof(onDelete));
    }

    public void OnPut(long transactionId, byte[] key, byte[] value) => m_onPut(key, value);
    public void OnDelete(long transactionId, byte[] key) => m_onDelete(key);
    public void OnBeginTransaction(long transactionId) { }
    public void OnCommitTransaction(long transactionId) { }
    public void OnRollbackTransaction(long transactionId) { }
}

/// <summary>
/// Transactional replay visitor that tracks transaction state.
/// Only applies operations from committed transactions.
/// </summary>
public class TransactionalWalReplayVisitor : IWalReplayVisitor
{
    private readonly Action<byte[], byte[]> m_onPut;
    private readonly Action<byte[]> m_onDelete;
    private readonly Dictionary<long, List<(bool IsPut, byte[] Key, byte[]? Value)>> m_pendingOps = new();
    private readonly HashSet<long> m_committed = new();
    
    public int ReplayedCount { get; private set; }

    public TransactionalWalReplayVisitor(Action<byte[], byte[]> onPut, Action<byte[]> onDelete)
    {
        m_onPut = onPut ?? throw new ArgumentNullException(nameof(onPut));
        m_onDelete = onDelete ?? throw new ArgumentNullException(nameof(onDelete));
    }

    public void OnPut(long transactionId, byte[] key, byte[] value)
    {
        if (transactionId == 0)
        {
            // Non-transactional - apply immediately
            m_onPut(key, value);
            ReplayedCount++;
        }
        else
        {
            // Buffer for transaction
            if (!m_pendingOps.TryGetValue(transactionId, out var ops))
            {
                ops = new List<(bool, byte[], byte[]?)>();
                m_pendingOps[transactionId] = ops;
            }
            ops.Add((true, key, value));
        }
    }

    public void OnDelete(long transactionId, byte[] key)
    {
        if (transactionId == 0)
        {
            m_onDelete(key);
            ReplayedCount++;
        }
        else
        {
            if (!m_pendingOps.TryGetValue(transactionId, out var ops))
            {
                ops = new List<(bool, byte[], byte[]?)>();
                m_pendingOps[transactionId] = ops;
            }
            ops.Add((false, key, null));
        }
    }

    public void OnBeginTransaction(long transactionId)
    {
        m_pendingOps[transactionId] = new List<(bool, byte[], byte[]?)>();
    }

    public void OnCommitTransaction(long transactionId)
    {
        m_committed.Add(transactionId);
        
        // Apply buffered operations
        if (m_pendingOps.TryGetValue(transactionId, out var ops))
        {
            foreach (var (isPut, key, value) in ops)
            {
                if (isPut)
                    m_onPut(key, value!);
                else
                    m_onDelete(key);
                ReplayedCount++;
            }
            m_pendingOps.Remove(transactionId);
        }
    }

    public void OnRollbackTransaction(long transactionId)
    {
        m_pendingOps.Remove(transactionId);
    }
}

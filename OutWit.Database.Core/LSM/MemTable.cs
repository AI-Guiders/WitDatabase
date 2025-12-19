using OutWit.Database.Core.Comparers;

namespace OutWit.Database.Core.LSM
{
    /// <summary>
    /// In-memory sorted storage for LSM-Tree.
    /// Uses a skip list (via SortedDictionary) for O(log n) operations.
    /// Thread-safe for concurrent reads, single writer.
    /// </summary>
    public sealed class MemTable : IDisposable
    {
        #region Fields

        private readonly SortedDictionary<byte[], byte[]?> m_entries;

        private readonly ReaderWriterLockSlim m_lock = new();

        private readonly LsmByteArrayComparer m_comparer;

        private long m_approximateSize;

        private bool m_disposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new empty MemTable.
        /// </summary>
        public MemTable()
        {
            m_comparer = LsmByteArrayComparer.Instance;
            m_entries = new SortedDictionary<byte[], byte[]?>(m_comparer);
        }

        #endregion

        #region Functions

        /// <summary>
        /// Gets the value for a key.
        /// </summary>
        /// <param name="key">The key to look up.</param>
        /// <param name="value">The value if found.</param>
        /// <returns>True if key exists (value may be null for tombstone).</returns>
        public bool TryGet(ReadOnlySpan<byte> key, out byte[]? value)
        {
            ThrowIfDisposed();
            var keyArray = key.ToArray();

            m_lock.EnterReadLock();
            try
            {
                return m_entries.TryGetValue(keyArray, out value);
            }
            finally
            {
                m_lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Inserts or updates a key-value pair.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        public void Put(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
        {
            ThrowIfDisposed();
            var keyArray = key.ToArray();
            var valueArray = value.ToArray();

            m_lock.EnterWriteLock();
            try
            {
                if (m_entries.TryGetValue(keyArray, out var existingValue))
                {
                    // Update: subtract old size, add new size
                    var oldSize = existingValue?.Length ?? 0;
                    Interlocked.Add(ref m_approximateSize, valueArray.Length - oldSize);
                    m_entries[keyArray] = valueArray;
                }
                else
                {
                    // New entry
                    Interlocked.Add(ref m_approximateSize, keyArray.Length + valueArray.Length);
                    m_entries[keyArray] = valueArray;
                }
            }
            finally
            {
                m_lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Deletes a key by inserting a tombstone (null value).
        /// </summary>
        /// <param name="key">The key to delete.</param>
        public void Delete(ReadOnlySpan<byte> key)
        {
            ThrowIfDisposed();
            var keyArray = key.ToArray();

            m_lock.EnterWriteLock();
            try
            {
                if (m_entries.TryGetValue(keyArray, out var existingValue))
                {
                    // Subtract value size but keep key
                    var oldSize = existingValue?.Length ?? 0;
                    Interlocked.Add(ref m_approximateSize, -oldSize);
                }
                else
                {
                    // New tombstone entry
                    Interlocked.Add(ref m_approximateSize, keyArray.Length);
                }
                m_entries[keyArray] = null; // Tombstone
            }
            finally
            {
                m_lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Returns all entries in sorted order for flushing to SSTable.
        /// </summary>
        /// <returns>Sorted key-value pairs (null value = tombstone).</returns>
        public IEnumerable<(byte[] Key, byte[]? Value)> GetAllEntries()
        {
            ThrowIfDisposed();

            m_lock.EnterReadLock();
            try
            {
                // Return a copy to avoid holding the lock
                var entries = new List<(byte[] Key, byte[]? Value)>(m_entries.Count);
                foreach (var kvp in m_entries)
                {
                    entries.Add((kvp.Key, kvp.Value));
                }
                return entries;
            }
            finally
            {
                m_lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Scans entries in the specified key range.
        /// </summary>
        /// <param name="startKey">Start of range (inclusive), or null for beginning.</param>
        /// <param name="endKey">End of range (exclusive), or null for end.</param>
        /// <returns>Key-value pairs in the range (null value = tombstone).</returns>
        public IEnumerable<(byte[] Key, byte[]? Value)> Scan(byte[]? startKey, byte[]? endKey)
        {
            ThrowIfDisposed();

            m_lock.EnterReadLock();
            try
            {
                var results = new List<(byte[] Key, byte[]? Value)>();

                foreach (var kvp in m_entries)
                {
                    if (startKey != null && m_comparer.Compare(kvp.Key, startKey) < 0)
                        continue;
                    if (endKey != null && m_comparer.Compare(kvp.Key, endKey) >= 0)
                        break;

                    results.Add((kvp.Key, kvp.Value));
                }

                return results;
            }
            finally
            {
                m_lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Clears all entries from the MemTable.
        /// </summary>
        public void Clear()
        {
            ThrowIfDisposed();

            m_lock.EnterWriteLock();
            try
            {
                m_entries.Clear();
                Interlocked.Exchange(ref m_approximateSize, 0);
            }
            finally
            {
                m_lock.ExitWriteLock();
            }
        }

        #endregion

        #region Tools

        private void ThrowIfDisposed()
        {
            if (m_disposed)
                throw new ObjectDisposedException(nameof(MemTable));
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (m_disposed) return;
            m_disposed = true;

            m_lock.EnterWriteLock();
            try
            {
                m_entries.Clear();
            }
            finally
            {
                m_lock.ExitWriteLock();
                m_lock.Dispose();
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the approximate size of the MemTable in bytes.
        /// </summary>
        public long ApproximateSize => Interlocked.Read(ref m_approximateSize);

        /// <summary>
        /// Gets the number of entries (including tombstones).
        /// </summary>
        public int Count
        {
            get
            {
                m_lock.EnterReadLock();
                try
                {
                    return m_entries.Count;
                }
                finally
                {
                    m_lock.ExitReadLock();
                }
            }
        }

        #endregion
    }
}


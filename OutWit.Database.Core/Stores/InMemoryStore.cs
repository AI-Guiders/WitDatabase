using System.Runtime.CompilerServices;
using OutWit.Database.Core.Comparers;
using OutWit.Database.Core.Interfaces;

namespace OutWit.Database.Core.Stores
{
    /// <summary>
    /// In-memory key-value store using SortedDictionary.
    /// Thread-safe for concurrent reads and exclusive writes.
    /// Does not persist data - suitable for testing or temporary storage.
    /// </summary>
    public sealed class InMemoryStore : IKeyValueStore
    {
        #region Fields

        private readonly SortedDictionary<byte[], byte[]> m_data;

        private readonly ReaderWriterLockSlim m_lock = new();

        private bool m_disposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new empty in-memory store.
        /// </summary>
        public InMemoryStore()
        {
            m_data = new SortedDictionary<byte[], byte[]>(ByteArrayComparer.Default);
        }

        #endregion

        #region Get

        /// <inheritdoc/>
        public byte[]? Get(ReadOnlySpan<byte> key)
        {
            ThrowIfDisposed();
            var keyArray = key.ToArray();

            m_lock.EnterReadLock();
            try
            {
                return m_data.GetValueOrDefault(keyArray);
            }
            finally
            {
                m_lock.ExitReadLock();
            }
        }

        /// <inheritdoc/>
        public ValueTask<byte[]?> GetAsync(byte[] key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Get(key));
        }

        #endregion

        #region Put

        /// <inheritdoc/>
        public void Put(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
        {
            ThrowIfDisposed();
            var keyArray = key.ToArray();
            var valueArray = value.ToArray();

            m_lock.EnterWriteLock();
            try
            {
                m_data[keyArray] = valueArray;
            }
            finally
            {
                m_lock.ExitWriteLock();
            }
        }

        /// <inheritdoc/>
        public ValueTask PutAsync(byte[] key, byte[] value, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Put(key, value);
            return ValueTask.CompletedTask;
        }

        #endregion

        #region Delete

        /// <inheritdoc/>
        public bool Delete(ReadOnlySpan<byte> key)
        {
            ThrowIfDisposed();
            var keyArray = key.ToArray();

            m_lock.EnterWriteLock();
            try
            {
                return m_data.Remove(keyArray);
            }
            finally
            {
                m_lock.ExitWriteLock();
            }
        }

        /// <inheritdoc/>
        public ValueTask<bool> DeleteAsync(byte[] key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Delete(key));
        }

        #endregion

        #region Scan

        /// <inheritdoc/>
        public IEnumerable<(byte[] Key, byte[] Value)> Scan(byte[]? startKey, byte[]? endKey)
        {
            ThrowIfDisposed();

            m_lock.EnterReadLock();
            try
            {
                var results = new List<(byte[] Key, byte[] Value)>();
                var comparer = ByteArrayComparer.Default;

                foreach (var kvp in m_data)
                {
                    if (startKey != null && comparer.Compare(kvp.Key, startKey) < 0)
                        continue;
                    if (endKey != null && comparer.Compare(kvp.Key, endKey) >= 0)
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

        /// <inheritdoc/>
        public async IAsyncEnumerable<(byte[] Key, byte[] Value)> ScanAsync(
            byte[]? startKey,
            byte[]? endKey,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var item in Scan(startKey, endKey))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return item;
            }
            await ValueTask.CompletedTask;
        }

        #endregion

        #region Flush

        /// <inheritdoc/>
        public void Flush()
        {
            // No-op for in-memory store
        }

        /// <inheritdoc/>
        public ValueTask FlushAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }

        #endregion

        #region Tools

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(m_disposed, this);
        }

        #endregion

        #region IDisposable

        /// <inheritdoc/>
        public void Dispose()
        {
            if (m_disposed) 
                return;

            m_disposed = true;

            m_lock.EnterWriteLock();
            try
            {
                m_data.Clear();
            }
            finally
            {
                m_lock.ExitWriteLock();
                m_lock.Dispose();
            }
        }

        #endregion
    }
}

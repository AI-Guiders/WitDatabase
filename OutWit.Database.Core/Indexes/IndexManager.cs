using OutWit.Database.Core.Comparers;
using OutWit.Database.Core.Interfaces;
using OutWit.Database.Core.Managers;
using OutWit.Database.Core.Tree;

namespace OutWit.Database.Core.Indexes
{
    /// <summary>
    /// Manages secondary indexes for a table using B+Tree storage.
    /// </summary>
    public sealed class IndexManager : IIndexManager
    {
        #region Fields

        private readonly PageManager m_pageManager;
        private readonly Dictionary<string, SecondaryIndexBTree> m_indexes;
        private readonly object m_lock = new();
        private bool m_disposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new index manager.
        /// </summary>
        /// <param name="pageManager">The page manager for index storage.</param>
        public IndexManager(PageManager pageManager)
        {
            m_pageManager = pageManager ?? throw new ArgumentNullException(nameof(pageManager));
            m_indexes = new Dictionary<string, SecondaryIndexBTree>(StringComparer.OrdinalIgnoreCase);
        }

        #endregion

        #region Index Management

        /// <inheritdoc/>
        public ISecondaryIndex CreateIndex(string name, bool isUnique)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            lock (m_lock)
            {
                if (m_indexes.ContainsKey(name))
                    throw new ArgumentException($"Index '{name}' already exists.", nameof(name));

                var tree = new BTree(m_pageManager);
                var index = new SecondaryIndexBTree(name, tree, isUnique, ownsTree: true);
                m_indexes[name] = index;
                return index;
            }
        }

        /// <inheritdoc/>
        public ISecondaryIndex? GetIndex(string name)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(name))
                return null;

            lock (m_lock)
            {
                return m_indexes.TryGetValue(name, out var index) ? index : null;
            }
        }

        /// <inheritdoc/>
        public bool DropIndex(string name)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(name))
                return false;

            lock (m_lock)
            {
                if (m_indexes.TryGetValue(name, out var index))
                {
                    m_indexes.Remove(name);
                    index.Dispose();
                    return true;
                }
                return false;
            }
        }

        /// <inheritdoc/>
        public bool HasIndex(string name)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(name))
                return false;

            lock (m_lock)
            {
                return m_indexes.ContainsKey(name);
            }
        }

        #endregion

        #region Index Updates

        /// <inheritdoc/>
        public void OnRowInserted(ReadOnlySpan<byte> primaryKey, IReadOnlyDictionary<string, byte[]> indexKeys)
        {
            ThrowIfDisposed();

            if (indexKeys == null)
                return;

            lock (m_lock)
            {
                foreach (var (indexName, indexKey) in indexKeys)
                {
                    if (m_indexes.TryGetValue(indexName, out var index) && indexKey != null)
                    {
                        index.Add(indexKey, primaryKey);
                    }
                }
            }
        }

        /// <inheritdoc/>
        public void OnRowDeleted(ReadOnlySpan<byte> primaryKey, IReadOnlyDictionary<string, byte[]> indexKeys)
        {
            ThrowIfDisposed();

            if (indexKeys == null)
                return;

            lock (m_lock)
            {
                foreach (var (indexName, indexKey) in indexKeys)
                {
                    if (m_indexes.TryGetValue(indexName, out var index) && indexKey != null)
                    {
                        index.Remove(indexKey, primaryKey);
                    }
                }
            }
        }

        /// <inheritdoc/>
        public void OnRowUpdated(
            ReadOnlySpan<byte> primaryKey,
            IReadOnlyDictionary<string, byte[]> oldIndexKeys,
            IReadOnlyDictionary<string, byte[]> newIndexKeys)
        {
            ThrowIfDisposed();

            if (oldIndexKeys == null || newIndexKeys == null)
                return;

            var comparer = ByteArrayComparer.Default;
            var primaryKeyArray = primaryKey.ToArray();

            lock (m_lock)
            {
                foreach (var (indexName, index) in m_indexes)
                {
                    oldIndexKeys.TryGetValue(indexName, out var oldKey);
                    newIndexKeys.TryGetValue(indexName, out var newKey);

                    // Skip if both are null
                    if (oldKey == null && newKey == null)
                        continue;

                    // If keys are the same, no update needed
                    if (oldKey != null && newKey != null && comparer.Equals(oldKey, newKey))
                        continue;

                    // Remove old entry if it exists
                    if (oldKey != null)
                    {
                        index.Remove(oldKey, primaryKeyArray);
                    }

                    // Add new entry if it exists
                    if (newKey != null)
                    {
                        index.Add(newKey, primaryKeyArray);
                    }
                }
            }
        }

        #endregion

        #region Flush

        /// <inheritdoc/>
        public void Flush()
        {
            ThrowIfDisposed();

            lock (m_lock)
            {
                foreach (var index in m_indexes.Values)
                {
                    index.Flush();
                }
            }
        }

        /// <inheritdoc/>
        public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            List<SecondaryIndexBTree> indexes;
            lock (m_lock)
            {
                indexes = m_indexes.Values.ToList();
            }

            foreach (var index in indexes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await index.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!m_disposed)
            {
                lock (m_lock)
                {
                    foreach (var index in m_indexes.Values)
                    {
                        index.Dispose();
                    }
                    m_indexes.Clear();
                }
                m_disposed = true;
            }
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(m_disposed, this);
        }

        #endregion

        #region Properties

        /// <inheritdoc/>
        public IReadOnlyList<string> IndexNames
        {
            get
            {
                lock (m_lock)
                {
                    return m_indexes.Keys.ToList().AsReadOnly();
                }
            }
        }

        /// <inheritdoc/>
        public int IndexCount
        {
            get
            {
                lock (m_lock)
                {
                    return m_indexes.Count;
                }
            }
        }

        #endregion
    }
}

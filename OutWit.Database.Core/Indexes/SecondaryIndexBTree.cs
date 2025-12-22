using System.Buffers.Binary;
using OutWit.Database.Core.Comparers;
using OutWit.Database.Core.Interfaces;
using OutWit.Database.Core.Managers;
using OutWit.Database.Core.Tree;

namespace OutWit.Database.Core.Indexes
{
    /// <summary>
    /// B+Tree based secondary index implementation.
    /// Maps index keys to primary keys for efficient non-primary key lookups.
    /// </summary>
    /// <remarks>
    /// The index stores composite keys: indexKey + separator + primaryKey + indexKeyLength.
    /// For unique indexes, the primaryKey is the value; for non-unique indexes, 
    /// it's part of the key to allow multiple primary keys per index key.
    /// </remarks>
    public sealed class SecondaryIndexBTree : ISecondaryIndex
    {
        #region Constants

        /// <summary>Separator byte between index key and primary key in composite key.</summary>
        private const byte KEY_SEPARATOR = 0x00;

        /// <summary>Maximum composite key size.</summary>
        private const int MAX_COMPOSITE_KEY_SIZE = BTree.MAX_KEY_SIZE;

        #endregion

        #region Fields

        private readonly BTree m_tree;
        private readonly bool m_ownsTree;
        private readonly ByteArrayComparer m_comparer;
        private bool m_disposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new secondary index using the specified B+Tree.
        /// </summary>
        /// <param name="name">The name of the index.</param>
        /// <param name="tree">The underlying B+Tree for storage.</param>
        /// <param name="isUnique">Whether this is a unique index.</param>
        /// <param name="ownsTree">If true, disposes the tree when this index is disposed.</param>
        public SecondaryIndexBTree(string name, BTree tree, bool isUnique, bool ownsTree = true)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            Name = name;
            m_tree = tree ?? throw new ArgumentNullException(nameof(tree));
            IsUnique = isUnique;
            m_ownsTree = ownsTree;
            m_comparer = ByteArrayComparer.Default;
        }

        /// <summary>
        /// Creates a new secondary index with a new B+Tree.
        /// </summary>
        /// <param name="name">The name of the index.</param>
        /// <param name="pageManager">The page manager for storage.</param>
        /// <param name="isUnique">Whether this is a unique index.</param>
        /// <param name="rootPageNumber">Root page number (0 for new tree).</param>
        public SecondaryIndexBTree(string name, PageManager pageManager, bool isUnique, uint rootPageNumber = 0)
            : this(name, new BTree(pageManager, rootPageNumber), isUnique, ownsTree: true)
        {
        }

        #endregion

        #region Lookup

        /// <inheritdoc/>
        public IEnumerable<byte[]> Find(ReadOnlySpan<byte> indexKey)
        {
            ThrowIfDisposed();

            // Convert to array before iterator to avoid span across yield
            var indexKeyArray = indexKey.ToArray();
            return FindInternal(indexKeyArray);
        }

        private IEnumerable<byte[]> FindInternal(byte[] indexKeyArray)
        {
            if (IsUnique)
            {
                // For unique index, the value is the primary key
                var value = m_tree.Search(indexKeyArray);
                if (value != null)
                    yield return value;
            }
            else
            {
                // For non-unique index, scan all entries with this prefix
                var startKey = CreatePrefixStartKey(indexKeyArray);
                var endKey = CreatePrefixEndKey(indexKeyArray);

                foreach (var (key, _) in m_tree.GetRange(startKey, endKey))
                {
                    var (_, primaryKey) = SplitCompositeKey(key);
                    if (primaryKey.Length > 0)
                        yield return primaryKey;
                }
            }
        }

        /// <inheritdoc/>
        public IEnumerable<(byte[] IndexKey, byte[] PrimaryKey)> FindRange(byte[]? startKey, byte[]? endKey)
        {
            ThrowIfDisposed();

            if (IsUnique)
            {
                foreach (var (indexKey, primaryKey) in m_tree.GetRange(startKey, endKey))
                {
                    yield return (indexKey, primaryKey);
                }
            }
            else
            {
                byte[]? scanStart = startKey != null 
                    ? CreatePrefixStartKey(startKey) 
                    : null;
                byte[]? scanEnd = endKey != null 
                    ? CreatePrefixEndKey(endKey) 
                    : null;

                foreach (var (compositeKey, _) in m_tree.GetRange(scanStart, scanEnd))
                {
                    var (indexKey, primaryKey) = SplitCompositeKey(compositeKey);
                    if (primaryKey.Length > 0)
                        yield return (indexKey, primaryKey);
                }
            }
        }

        /// <inheritdoc/>
        public bool Contains(ReadOnlySpan<byte> indexKey)
        {
            ThrowIfDisposed();

            var indexKeyArray = indexKey.ToArray();

            if (IsUnique)
            {
                return m_tree.Search(indexKeyArray) != null;
            }
            else
            {
                var startKey = CreatePrefixStartKey(indexKeyArray);
                var endKey = CreatePrefixEndKey(indexKeyArray);
                return m_tree.GetRange(startKey, endKey).Any();
            }
        }

        /// <inheritdoc/>
        public bool ContainsEntry(ReadOnlySpan<byte> indexKey, ReadOnlySpan<byte> primaryKey)
        {
            ThrowIfDisposed();

            var indexKeyArray = indexKey.ToArray();
            var primaryKeyArray = primaryKey.ToArray();

            if (IsUnique)
            {
                var value = m_tree.Search(indexKeyArray);
                return value != null && m_comparer.Equals(value, primaryKeyArray);
            }
            else
            {
                var compositeKey = CreateCompositeKey(indexKeyArray, primaryKeyArray);
                return m_tree.Search(compositeKey) != null;
            }
        }

        #endregion

        #region Modification

        /// <inheritdoc/>
        public void Add(ReadOnlySpan<byte> indexKey, ReadOnlySpan<byte> primaryKey)
        {
            ThrowIfDisposed();
            ValidateKeySize(indexKey, primaryKey);

            var indexKeyArray = indexKey.ToArray();
            var primaryKeyArray = primaryKey.ToArray();

            if (IsUnique)
            {
                // Check for existing entry with same index key
                var existing = m_tree.Search(indexKeyArray);
                if (existing != null)
                {
                    throw new InvalidOperationException(
                        $"Unique index '{Name}' already contains an entry for this key.");
                }
                
                // Store: indexKey -> primaryKey
                m_tree.Insert(indexKeyArray, primaryKeyArray);
            }
            else
            {
                // Store: compositeKey -> empty value
                var compositeKey = CreateCompositeKey(indexKeyArray, primaryKeyArray);
                m_tree.Insert(compositeKey, ReadOnlySpan<byte>.Empty);
            }
        }

        /// <inheritdoc/>
        public bool Remove(ReadOnlySpan<byte> indexKey, ReadOnlySpan<byte> primaryKey)
        {
            ThrowIfDisposed();

            var indexKeyArray = indexKey.ToArray();
            var primaryKeyArray = primaryKey.ToArray();

            if (IsUnique)
            {
                // Verify the primary key matches before removing
                var existing = m_tree.Search(indexKeyArray);
                if (existing == null || !m_comparer.Equals(existing, primaryKeyArray))
                    return false;

                return m_tree.Delete(indexKeyArray);
            }
            else
            {
                var compositeKey = CreateCompositeKey(indexKeyArray, primaryKeyArray);
                return m_tree.Delete(compositeKey);
            }
        }

        /// <inheritdoc/>
        public int RemoveAll(ReadOnlySpan<byte> indexKey)
        {
            ThrowIfDisposed();

            var indexKeyArray = indexKey.ToArray();

            if (IsUnique)
            {
                return m_tree.Delete(indexKeyArray) ? 1 : 0;
            }
            else
            {
                var startKey = CreatePrefixStartKey(indexKeyArray);
                var endKey = CreatePrefixEndKey(indexKeyArray);
                
                var keysToDelete = m_tree.GetRange(startKey, endKey)
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var key in keysToDelete)
                {
                    m_tree.Delete(key);
                }

                return keysToDelete.Count;
            }
        }

        /// <inheritdoc/>
        public void Clear()
        {
            ThrowIfDisposed();
            
            // Delete all entries
            var allKeys = m_tree.GetAll()
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in allKeys)
            {
                m_tree.Delete(key);
            }
        }

        #endregion

        #region Flush

        /// <inheritdoc/>
        public void Flush()
        {
            ThrowIfDisposed();
            // BTree flushes on Dispose, but we can explicitly call it
            // The underlying PageManager handles flushing
        }

        /// <inheritdoc/>
        public ValueTask FlushAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            Flush();
            return ValueTask.CompletedTask;
        }

        #endregion

        #region Key Management

        /// <summary>
        /// Creates a composite key from index key and primary key.
        /// Format: indexKey + 0x00 + primaryKey + indexKeyLength (4 bytes)
        /// The length is stored at the END so that prefix scanning works correctly.
        /// </summary>
        private static byte[] CreateCompositeKey(byte[] indexKey, ReadOnlySpan<byte> primaryKey)
        {
            // Format: indexKey + separator + primaryKey + indexKeyLength (4 bytes)
            // The length at the end allows us to split the key, and having primaryKey
            // before the length ensures proper lexicographic ordering for prefix scans.
            var composite = new byte[indexKey.Length + 1 + primaryKey.Length + 4];
            indexKey.CopyTo(composite, 0);
            composite[indexKey.Length] = KEY_SEPARATOR;
            primaryKey.CopyTo(composite.AsSpan(indexKey.Length + 1));
            BinaryPrimitives.WriteInt32LittleEndian(composite.AsSpan(indexKey.Length + 1 + primaryKey.Length), indexKey.Length);
            return composite;
        }

        /// <summary>
        /// Splits a composite key into index key and primary key.
        /// </summary>
        private static (byte[] IndexKey, byte[] PrimaryKey) SplitCompositeKey(byte[] compositeKey)
        {
            if (compositeKey.Length < 5)
                return (compositeKey, Array.Empty<byte>());

            // Read the length from the end
            int indexKeyLength = BinaryPrimitives.ReadInt32LittleEndian(compositeKey.AsSpan(compositeKey.Length - 4));
            
            if (indexKeyLength < 0 || indexKeyLength >= compositeKey.Length - 4)
                return (compositeKey, Array.Empty<byte>());

            // Verify separator
            if (compositeKey[indexKeyLength] != KEY_SEPARATOR)
                return (compositeKey, Array.Empty<byte>());

            var indexKey = compositeKey[..indexKeyLength];
            var primaryKey = compositeKey[(indexKeyLength + 1)..(compositeKey.Length - 4)];
            return (indexKey, primaryKey);
        }

        /// <summary>
        /// Creates the start key for prefix scanning (inclusive).
        /// This is the smallest key that starts with the given prefix + separator.
        /// </summary>
        private static byte[] CreatePrefixStartKey(byte[] prefix)
        {
            // prefix + separator (0x00) is the smallest possible composite key for this indexKey
            var startKey = new byte[prefix.Length + 1];
            prefix.CopyTo(startKey, 0);
            startKey[prefix.Length] = KEY_SEPARATOR;
            return startKey;
        }

        /// <summary>
        /// Creates the end key for prefix scanning (exclusive).
        /// This is the first key that does NOT start with the given prefix + separator.
        /// </summary>
        private static byte[] CreatePrefixEndKey(byte[] prefix)
        {
            // prefix + 0x01 is the first key that's lexicographically after all
            // keys starting with prefix + 0x00
            var endKey = new byte[prefix.Length + 1];
            prefix.CopyTo(endKey, 0);
            endKey[prefix.Length] = (byte)(KEY_SEPARATOR + 1);
            return endKey;
        }

        private static void ValidateKeySize(ReadOnlySpan<byte> indexKey, ReadOnlySpan<byte> primaryKey)
        {
            int totalSize = indexKey.Length + 1 + primaryKey.Length + 4;
            if (totalSize > MAX_COMPOSITE_KEY_SIZE)
            {
                throw new ArgumentException(
                    $"Combined key size ({totalSize}) exceeds maximum ({MAX_COMPOSITE_KEY_SIZE})");
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!m_disposed)
            {
                if (m_ownsTree)
                {
                    m_tree.Dispose();
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
        public string Name { get; }

        /// <inheritdoc/>
        public bool IsUnique { get; }

        /// <inheritdoc/>
        public long Count => m_tree.Count();

        /// <summary>
        /// Gets the root page number of the underlying B+Tree.
        /// </summary>
        public uint RootPageNumber => m_tree.RootPageNumber;

        #endregion
    }
}

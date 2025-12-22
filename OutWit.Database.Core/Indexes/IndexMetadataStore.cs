using System.Text.Json;
using OutWit.Database.Core.Interfaces;
using TextEncoding = System.Text.Encoding;

namespace OutWit.Database.Core.Indexes
{
    /// <summary>
    /// Manages persistence of index metadata to the database store.
    /// Stores index definitions (name, isUnique) so they can be recreated on database reopen.
    /// </summary>
    public sealed class IndexMetadataStore
    {
        #region Constants

        /// <summary>
        /// System key prefix for index metadata.
        /// Uses null bytes to ensure it sorts before user data.
        /// </summary>
        public static readonly byte[] SYSTEM_PREFIX = "\0\0_idx_meta_"u8.ToArray();

        /// <summary>
        /// Key for the index catalog (list of all index names).
        /// </summary>
        private static readonly byte[] CATALOG_KEY = CreateKey("_catalog_");

        #endregion

        #region Fields

        private readonly IKeyValueStore m_store;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new index metadata store.
        /// </summary>
        /// <param name="store">The underlying key-value store.</param>
        public IndexMetadataStore(IKeyValueStore store)
        {
            m_store = store ?? throw new ArgumentNullException(nameof(store));
        }

        #endregion

        #region Save/Load

        /// <summary>
        /// Saves metadata for an index.
        /// </summary>
        public void SaveIndex(string name, bool isUnique)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            var metadata = new IndexMetadata { Name = name, IsUnique = isUnique };
            var key = CreateKey(name);
            var value = JsonSerializer.SerializeToUtf8Bytes(metadata);
            
            m_store.Put(key, value);
            
            // Update catalog
            var catalog = LoadCatalog();
            if (!catalog.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                catalog.Add(name);
                SaveCatalog(catalog);
            }
        }

        /// <summary>
        /// Loads metadata for an index.
        /// </summary>
        /// <returns>The index metadata, or null if not found.</returns>
        public IndexMetadata? LoadIndex(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            var key = CreateKey(name);
            var value = m_store.Get(key);
            
            if (value == null)
                return null;

            return JsonSerializer.Deserialize<IndexMetadata>(value);
        }

        /// <summary>
        /// Removes metadata for an index.
        /// </summary>
        public bool RemoveIndex(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            var key = CreateKey(name);
            var removed = m_store.Delete(key);
            
            if (removed)
            {
                var catalog = LoadCatalog();
                catalog.RemoveAll(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
                SaveCatalog(catalog);
            }
            
            return removed;
        }

        /// <summary>
        /// Loads all index metadata.
        /// </summary>
        public IReadOnlyList<IndexMetadata> LoadAllIndexes()
        {
            var catalog = LoadCatalog();
            var result = new List<IndexMetadata>();
            
            foreach (var name in catalog)
            {
                var metadata = LoadIndex(name);
                if (metadata != null)
                {
                    result.Add(metadata);
                }
            }
            
            return result;
        }

        /// <summary>
        /// Gets all index names from the catalog.
        /// </summary>
        public IReadOnlyList<string> GetIndexNames()
        {
            return LoadCatalog().AsReadOnly();
        }

        #endregion

        #region Catalog Management

        private List<string> LoadCatalog()
        {
            var value = m_store.Get(CATALOG_KEY);
            
            if (value == null || value.Length == 0)
                return [];

            try
            {
                return JsonSerializer.Deserialize<List<string>>(value) ?? [];
            }
            catch
            {
                return [];
            }
        }

        private void SaveCatalog(List<string> catalog)
        {
            var value = JsonSerializer.SerializeToUtf8Bytes(catalog);
            m_store.Put(CATALOG_KEY, value);
        }

        #endregion

        #region Key Generation

        private static byte[] CreateKey(string name)
        {
            var nameBytes = TextEncoding.UTF8.GetBytes(name.ToLowerInvariant());
            var key = new byte[SYSTEM_PREFIX.Length + nameBytes.Length];
            SYSTEM_PREFIX.CopyTo(key, 0);
            nameBytes.CopyTo(key, SYSTEM_PREFIX.Length);
            return key;
        }

        #endregion
    }

    /// <summary>
    /// Metadata for a secondary index.
    /// </summary>
    public sealed class IndexMetadata
    {
        /// <summary>
        /// The name of the index.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Whether this is a unique index.
        /// </summary>
        public bool IsUnique { get; set; }
    }
}

using OutWit.Database.Core.Comparers;

namespace OutWit.Database.Core.Transactions
{
    /// <summary>
    /// Represents a savepoint within a transaction.
    /// Stores the state of changes at the time the savepoint was created.
    /// </summary>
    internal sealed class Savepoint
    {
        #region Fields

        private readonly Dictionary<byte[], (byte[]? NewValue, byte[]? OldValue)> m_changesSnapshot;
        private readonly HashSet<byte[]> m_deletedKeysSnapshot;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new savepoint with a snapshot of the current transaction state.
        /// </summary>
        /// <param name="name">The name of the savepoint.</param>
        /// <param name="changes">Current changes in the transaction.</param>
        /// <param name="deletedKeys">Current deleted keys in the transaction.</param>
        public Savepoint(
            string name,
            Dictionary<byte[], (byte[]? NewValue, byte[]? OldValue)> changes,
            HashSet<byte[]> deletedKeys)
        {
            Name = name;
            CreatedAt = DateTime.UtcNow;

            var comparer = ByteArrayComparer.Default;

            // Create deep copies of the state
            m_changesSnapshot = new Dictionary<byte[], (byte[]?, byte[]?)>(comparer);
            foreach (var kvp in changes)
            {
                m_changesSnapshot[kvp.Key.ToArray()] = (
                    kvp.Value.NewValue?.ToArray(),
                    kvp.Value.OldValue?.ToArray()
                );
            }

            m_deletedKeysSnapshot = new HashSet<byte[]>(comparer);
            foreach (var key in deletedKeys)
            {
                m_deletedKeysSnapshot.Add(key.ToArray());
            }
        }

        #endregion

        #region Functions

        /// <summary>
        /// Restores the transaction state to this savepoint.
        /// </summary>
        /// <param name="changes">The changes dictionary to restore.</param>
        /// <param name="deletedKeys">The deleted keys set to restore.</param>
        public void Restore(
            Dictionary<byte[], (byte[]? NewValue, byte[]? OldValue)> changes,
            HashSet<byte[]> deletedKeys)
        {
            changes.Clear();
            deletedKeys.Clear();

            foreach (var kvp in m_changesSnapshot)
            {
                changes[kvp.Key.ToArray()] = (
                    kvp.Value.NewValue?.ToArray(),
                    kvp.Value.OldValue?.ToArray()
                );
            }

            foreach (var key in m_deletedKeysSnapshot)
            {
                deletedKeys.Add(key.ToArray());
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the name of the savepoint.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets when the savepoint was created.
        /// </summary>
        public DateTime CreatedAt { get; }

        /// <summary>
        /// Gets the number of changes at the time of savepoint creation.
        /// </summary>
        public int ChangeCount => m_changesSnapshot.Count;

        #endregion
    }
}

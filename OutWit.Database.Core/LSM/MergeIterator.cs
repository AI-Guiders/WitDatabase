using OutWit.Database.Core.Comparers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OutWit.Database.Core.LSM
{
    /// <summary>
    /// Merge iterator that produces entries from multiple SSTables in sorted order.
    /// For duplicate keys, yields newest first (highest priority).
    /// </summary>
    internal sealed class MergeIterator
    {
        #region Fields

        private readonly List<IEnumerator<(byte[] Key, byte[]? Value)>> m_iterators;

        private readonly List<(byte[] Key, byte[]? Value, int Priority)?> m_current;

        private readonly LsmByteArrayComparer m_comparer;

        #endregion

        #region Constructors

        public MergeIterator(List<SSTableReader> readers, LsmByteArrayComparer comparer)
        {
            m_comparer = comparer;
            m_iterators = readers.Select(r => r.Scan().GetEnumerator()).ToList();
            m_current = new List<(byte[] Key, byte[]? Value, int Priority)?>(readers.Count);

            // Initialize current entries
            for (int i = 0; i < m_iterators.Count; i++)
            {
                if (m_iterators[i].MoveNext())
                {
                    var entry = m_iterators[i].Current;
                    m_current.Add((entry.Key, entry.Value, i));
                }
                else
                {
                    m_current.Add(null);
                }
            }
        }

        #endregion

        #region Functions

        public IEnumerable<(byte[] Key, byte[]? Value, int Priority)> Iterate()
        {
            while (true)
            {
                // Find minimum key
                (byte[] Key, byte[]? Value, int Priority)? minEntry = null;
                int minIndex = -1;

                for (int i = 0; i < m_current.Count; i++)
                {
                    var entry = m_current[i];
                    if (entry == null) continue;

                    if (minEntry == null || m_comparer.Compare(entry.Value.Key, minEntry.Value.Key) < 0)
                    {
                        minEntry = entry;
                        minIndex = i;
                    }
                    else if (m_comparer.Compare(entry.Value.Key, minEntry.Value.Key) == 0)
                    {
                        // Same key - prefer higher priority (newer file)
                        if (entry.Value.Priority > minEntry.Value.Priority)
                        {
                            minEntry = entry;
                            minIndex = i;
                        }
                    }
                }

                if (minEntry == null)
                    yield break;

                // For duplicate keys, advance all iterators with the same key
                var yieldedKey = minEntry.Value.Key;
                for (int i = 0; i < m_current.Count; i++)
                {
                    var entry = m_current[i];
                    if (entry != null && m_comparer.Compare(entry.Value.Key, yieldedKey) == 0)
                    {
                        // Advance this iterator
                        if (m_iterators[i].MoveNext())
                        {
                            var next = m_iterators[i].Current;
                            m_current[i] = (next.Key, next.Value, i);
                        }
                        else
                        {
                            m_current[i] = null;
                        }
                    }
                }

                yield return minEntry.Value;
            }
        }

        #endregion
    }
}

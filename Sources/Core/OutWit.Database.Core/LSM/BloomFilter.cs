using System.Collections;

namespace OutWit.Database.Core.LSM
{
    /// <summary>
    /// Bloom filter for fast negative lookups.
    /// A probabilistic data structure that can tell you if a key is
    /// definitely NOT in a set, or PROBABLY in a set.
    /// </summary>
    public sealed class BloomFilter
    {
        #region Fields

        private readonly BitArray m_bits;

        private readonly int m_hashCount;

        private readonly int m_size;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new Bloom filter.
        /// </summary>
        /// <param name="expectedItems">Expected number of items to add.</param>
        /// <param name="falsePositiveRate">Desired false positive rate (0.0-1.0).</param>
        public BloomFilter(int expectedItems, double falsePositiveRate = 0.01)
        {
            // Calculate optimal size: m = -n * ln(p) / (ln(2)^2)
            m_size = CalculateOptimalSize(expectedItems, falsePositiveRate);
        
            // Calculate optimal hash count: k = (m/n) * ln(2)
            m_hashCount = CalculateOptimalHashCount(m_size, expectedItems);
        
            m_bits = new BitArray(m_size);
        }

        /// <summary>
        /// Creates a Bloom filter from serialized data.
        /// </summary>
        /// <param name="data">Serialized bit data.</param>
        /// <param name="hashCount">Number of hash functions.</param>
        /// <param name="size">Original size in bits (optional, defaults to data.Length * 8).</param>
        public BloomFilter(byte[] data, int hashCount, int size = -1)
        {
            m_size = size > 0 ? size : data.Length * 8;
            m_hashCount = hashCount;
            m_bits = new BitArray(data);
            // Ensure BitArray has correct length
            m_bits.Length = m_size;
        }

        #endregion

        #region Functions

        /// <summary>
        /// Adds a key to the filter.
        /// </summary>
        public void Add(ReadOnlySpan<byte> key)
        {
            var (hash1, hash2) = GetHashes(key);

            for (int i = 0; i < m_hashCount; i++)
            {
                // Double hashing: hash(i) = hash1 + i * hash2
                int combinedHash = Math.Abs((int)((hash1 + (uint)i * hash2) % (uint)m_size));
                m_bits[combinedHash] = true;
            }
        }

        /// <summary>
        /// Checks if a key might be in the filter.
        /// </summary>
        /// <returns>False = definitely not in set. True = probably in set.</returns>
        public bool MightContain(ReadOnlySpan<byte> key)
        {
            var (hash1, hash2) = GetHashes(key);

            for (int i = 0; i < m_hashCount; i++)
            {
                int combinedHash = Math.Abs((int)((hash1 + (uint)i * hash2) % (uint)m_size));
                if (!m_bits[combinedHash])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Serializes the filter to bytes.
        /// </summary>
        public byte[] ToBytes()
        {
            var bytes = new byte[(m_size + 7) / 8];
            m_bits.CopyTo(bytes, 0);
            return bytes;
        }

        /// <summary>
        /// Clears the filter.
        /// </summary>
        public void Clear()
        {
            m_bits.SetAll(false);
        }

        #endregion

        #region Tools

        private (uint Hash1, uint Hash2) GetHashes(ReadOnlySpan<byte> key)
        {
            // Use MurmurHash3-style mixing for two independent hashes
            uint hash1 = 0x811c9dc5; // FNV offset basis
            uint hash2 = 0;

            foreach (var b in key)
            {
                // FNV-1a for hash1
                hash1 ^= b;
                hash1 *= 0x01000193; // FNV prime

                // Different mixing for hash2
                hash2 = RotateLeft(hash2, 5) ^ b;
                hash2 *= 0x85ebca6b;
            }

            // Final mixing
            hash1 ^= hash1 >> 16;
            hash1 *= 0x85ebca6b;
            hash1 ^= hash1 >> 13;

            hash2 ^= hash2 >> 16;
            hash2 *= 0xc2b2ae35;
            hash2 ^= hash2 >> 16;

            return (hash1, hash2);
        }

        private static uint RotateLeft(uint value, int count)
        {
            return (value << count) | (value >> (32 - count));
        }

        private static int CalculateOptimalSize(int n, double p)
        {
            // m = -n * ln(p) / (ln(2)^2)
            double m = -n * Math.Log(p) / (Math.Log(2) * Math.Log(2));
            return Math.Max(64, (int)Math.Ceiling(m));
        }

        private static int CalculateOptimalHashCount(int m, int n)
        {
            // k = (m/n) * ln(2)
            double k = (double)m / n * Math.Log(2);
            return Math.Max(1, Math.Min(16, (int)Math.Round(k)));
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the size of the filter in bits.
        /// </summary>
        public int Size => m_size;

        /// <summary>
        /// Gets the number of hash functions used.
        /// </summary>
        public int HashCount => m_hashCount;

        #endregion
    }
}
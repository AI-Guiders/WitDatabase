using OutWit.Database.Core.Interfaces;

namespace OutWit.Database.Core.Query
{
    /// <summary>
    /// Implementation of IMultiResultReader that wraps multiple result sets.
    /// Supports both pre-materialized and streaming result sets.
    /// </summary>
    public sealed class MultiResultReader : IMultiResultReader
    {
        #region Fields

        private readonly List<ResultSet> m_resultSets;
        private int m_currentIndex = -1;
        private bool m_disposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new MultiResultReader with the specified result sets.
        /// </summary>
        /// <param name="resultSets">The result sets to read.</param>
        public MultiResultReader(IEnumerable<ResultSet> resultSets)
        {
            m_resultSets = resultSets?.ToList() ?? throw new ArgumentNullException(nameof(resultSets));
        }

        /// <summary>
        /// Creates a new MultiResultReader with a single result set.
        /// </summary>
        /// <param name="data">The data for the single result set.</param>
        /// <param name="recordsAffected">Number of records affected (-1 for SELECT).</param>
        public MultiResultReader(IEnumerable<(byte[] Key, byte[] Value)> data, int recordsAffected = -1)
            : this(new[] { new ResultSet(data, recordsAffected) })
        {
        }

        /// <summary>
        /// Creates an empty MultiResultReader.
        /// </summary>
        public static MultiResultReader Empty => new(Array.Empty<ResultSet>());

        #endregion

        #region NextResult

        /// <inheritdoc/>
        public bool NextResult()
        {
            ThrowIfDisposed();

            if (m_currentIndex + 1 < m_resultSets.Count)
            {
                m_currentIndex++;
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public ValueTask<bool> NextResultAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(NextResult());
        }

        #endregion

        #region Tools

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

            // Dispose any disposable result sets
            foreach (var rs in m_resultSets)
            {
                rs.Dispose();
            }
        }

        #endregion

        #region IAsyncDisposable

        public async ValueTask DisposeAsync()
        {
            if (m_disposed) return;
            m_disposed = true;

            foreach (var rs in m_resultSets)
            {
                await rs.DisposeAsync().ConfigureAwait(false);
            }
        }

        #endregion

        #region Properties

        /// <inheritdoc/>
        public IEnumerable<(byte[] Key, byte[] Value)>? CurrentResult
        {
            get
            {
                ThrowIfDisposed();

                if (m_currentIndex < 0 || m_currentIndex >= m_resultSets.Count)
                    return null;

                return m_resultSets[m_currentIndex].Data;
            }
        }

        /// <inheritdoc/>
        public int ResultSetCount => m_resultSets.Count;

        /// <inheritdoc/>
        public int CurrentResultIndex => m_currentIndex;

        /// <inheritdoc/>
        public bool HasMoreResults => m_currentIndex + 1 < m_resultSets.Count;

        /// <inheritdoc/>
        public int RecordsAffected
        {
            get
            {
                if (m_currentIndex < 0 || m_currentIndex >= m_resultSets.Count)
                    return -1;

                return m_resultSets[m_currentIndex].RecordsAffected;
            }
        }

        /// <inheritdoc/>
        public bool IsClosed => m_disposed;

        #endregion
    }

    /// <summary>
    /// Represents a single result set within a multi-result reader.
    /// </summary>
    public sealed class ResultSet : IDisposable, IAsyncDisposable
    {
        #region Fields

        private readonly IEnumerable<(byte[] Key, byte[] Value)> m_data;
        private readonly IDisposable? m_disposable;
        private readonly IAsyncDisposable? m_asyncDisposable;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new result set with the specified data.
        /// </summary>
        /// <param name="data">The result set data.</param>
        /// <param name="recordsAffected">Number of records affected (-1 for SELECT).</param>
        public ResultSet(IEnumerable<(byte[] Key, byte[] Value)> data, int recordsAffected = -1)
        {
            m_data = data ?? throw new ArgumentNullException(nameof(data));
            RecordsAffected = recordsAffected;

            // Track disposable resources
            m_disposable = data as IDisposable;
            m_asyncDisposable = data as IAsyncDisposable;
        }

        /// <summary>
        /// Creates an empty result set with a records affected count.
        /// Useful for DML operations (INSERT, UPDATE, DELETE).
        /// </summary>
        /// <param name="recordsAffected">Number of records affected.</param>
        public static ResultSet Affected(int recordsAffected)
        {
            return new ResultSet(Array.Empty<(byte[], byte[])>(), recordsAffected);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            m_disposable?.Dispose();
        }

        #endregion

        #region IAsyncDisposable

        public async ValueTask DisposeAsync()
        {
            if (m_asyncDisposable != null)
            {
                await m_asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            else
            {
                m_disposable?.Dispose();
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the result set data.
        /// </summary>
        public IEnumerable<(byte[] Key, byte[] Value)> Data => m_data;

        /// <summary>
        /// Gets the number of records affected (-1 for SELECT).
        /// </summary>
        public int RecordsAffected { get; }

        #endregion
    }
}

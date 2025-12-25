using OutWit.Database.Core.Interfaces;

namespace OutWit.Database.Core.Query
{
    /// <summary>
    /// Executes batches of operations against a key-value store.
    /// Produces multiple result sets for Get/Scan operations.
    /// </summary>
    public sealed class BatchExecutor : IBatchExecutor
    {
        #region Fields

        private readonly IKeyValueStore m_store;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new BatchExecutor for the specified store.
        /// </summary>
        /// <param name="store">The key-value store to execute against.</param>
        public BatchExecutor(IKeyValueStore store)
        {
            m_store = store ?? throw new ArgumentNullException(nameof(store));
        }

        #endregion

        #region ExecuteBatch

        /// <inheritdoc/>
        public IMultiResultReader ExecuteBatch(IEnumerable<BatchOperation> operations)
        {
            if (operations == null)
                throw new ArgumentNullException(nameof(operations));

            var resultSets = new List<ResultSet>();

            foreach (var operation in operations)
            {
                var resultSet = ExecuteOperation(operation);
                resultSets.Add(resultSet);
            }

            return new MultiResultReader(resultSets);
        }

        /// <inheritdoc/>
        public ValueTask<IMultiResultReader> ExecuteBatchAsync(
            IEnumerable<BatchOperation> operations,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(ExecuteBatch(operations));
        }

        #endregion

        #region Execute Operations

        private ResultSet ExecuteOperation(BatchOperation operation)
        {
            return operation.Type switch
            {
                BatchOperationType.Put => ExecutePut((BatchPutOperation)operation),
                BatchOperationType.Delete => ExecuteDelete((BatchDeleteOperation)operation),
                BatchOperationType.Get => ExecuteGet((BatchGetOperation)operation),
                BatchOperationType.Scan => ExecuteScan((BatchScanOperation)operation),
                _ => throw new ArgumentException($"Unknown operation type: {operation.Type}")
            };
        }

        private ResultSet ExecutePut(BatchPutOperation op)
        {
            m_store.Put(op.Key, op.Value);
            return ResultSet.Affected(1);
        }

        private ResultSet ExecuteDelete(BatchDeleteOperation op)
        {
            var deleted = m_store.Delete(op.Key);
            return ResultSet.Affected(deleted ? 1 : 0);
        }

        private ResultSet ExecuteGet(BatchGetOperation op)
        {
            var value = m_store.Get(op.Key);
            if (value == null)
            {
                return new ResultSet(Array.Empty<(byte[], byte[])>());
            }
            
            return new ResultSet(new[] { (op.Key, value) });
        }

        private ResultSet ExecuteScan(BatchScanOperation op)
        {
            var data = m_store.Scan(op.StartKey, op.EndKey).ToList();
            return new ResultSet(data);
        }

        #endregion
    }

    /// <summary>
    /// Extension methods for batch execution.
    /// </summary>
    public static class BatchExecutorExtensions
    {
        /// <summary>
        /// Executes a batch of operations on a key-value store.
        /// </summary>
        public static IMultiResultReader ExecuteBatch(
            this IKeyValueStore store,
            IEnumerable<BatchOperation> operations)
        {
            var executor = new BatchExecutor(store);
            return executor.ExecuteBatch(operations);
        }

        /// <summary>
        /// Executes a batch of operations on a key-value store.
        /// </summary>
        public static IMultiResultReader ExecuteBatch(
            this IKeyValueStore store,
            params BatchOperation[] operations)
        {
            return store.ExecuteBatch((IEnumerable<BatchOperation>)operations);
        }

        /// <summary>
        /// Executes a batch of operations asynchronously.
        /// </summary>
        public static ValueTask<IMultiResultReader> ExecuteBatchAsync(
            this IKeyValueStore store,
            IEnumerable<BatchOperation> operations,
            CancellationToken cancellationToken = default)
        {
            var executor = new BatchExecutor(store);
            return executor.ExecuteBatchAsync(operations, cancellationToken);
        }
    }
}

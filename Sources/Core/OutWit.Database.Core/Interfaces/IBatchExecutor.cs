using OutWit.Database.Core.Query;

namespace OutWit.Database.Core.Interfaces
{
    /// <summary>
    /// Represents a single operation in a batch.
    /// </summary>
    public abstract class BatchOperation
    {
        /// <summary>
        /// Gets the operation type.
        /// </summary>
        public abstract BatchOperationType Type { get; }
    }

    /// <summary>
    /// Types of batch operations.
    /// </summary>
    public enum BatchOperationType
    {
        /// <summary>
        /// Put (insert/update) operation.
        /// </summary>
        Put,

        /// <summary>
        /// Delete operation.
        /// </summary>
        Delete,

        /// <summary>
        /// Get operation (returns result set).
        /// </summary>
        Get,

        /// <summary>
        /// Scan operation (returns result set).
        /// </summary>
        Scan
    }

    /// <summary>
    /// Put operation in a batch.
    /// </summary>
    public sealed class BatchPutOperation : BatchOperation
    {
        /// <summary>
        /// Creates a new Put operation.
        /// </summary>
        public BatchPutOperation(byte[] key, byte[] value)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <inheritdoc/>
        public override BatchOperationType Type => BatchOperationType.Put;

        /// <summary>
        /// The key to put.
        /// </summary>
        public byte[] Key { get; }

        /// <summary>
        /// The value to put.
        /// </summary>
        public byte[] Value { get; }
    }

    /// <summary>
    /// Delete operation in a batch.
    /// </summary>
    public sealed class BatchDeleteOperation : BatchOperation
    {
        /// <summary>
        /// Creates a new Delete operation.
        /// </summary>
        public BatchDeleteOperation(byte[] key)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
        }

        /// <inheritdoc/>
        public override BatchOperationType Type => BatchOperationType.Delete;

        /// <summary>
        /// The key to delete.
        /// </summary>
        public byte[] Key { get; }
    }

    /// <summary>
    /// Get operation in a batch.
    /// </summary>
    public sealed class BatchGetOperation : BatchOperation
    {
        /// <summary>
        /// Creates a new Get operation.
        /// </summary>
        public BatchGetOperation(byte[] key)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
        }

        /// <inheritdoc/>
        public override BatchOperationType Type => BatchOperationType.Get;

        /// <summary>
        /// The key to get.
        /// </summary>
        public byte[] Key { get; }
    }

    /// <summary>
    /// Scan operation in a batch.
    /// </summary>
    public sealed class BatchScanOperation : BatchOperation
    {
        /// <summary>
        /// Creates a new Scan operation.
        /// </summary>
        public BatchScanOperation(byte[]? startKey = null, byte[]? endKey = null)
        {
            StartKey = startKey;
            EndKey = endKey;
        }

        /// <inheritdoc/>
        public override BatchOperationType Type => BatchOperationType.Scan;

        /// <summary>
        /// The start key (inclusive). Null means start from the beginning.
        /// </summary>
        public byte[]? StartKey { get; }

        /// <summary>
        /// The end key (exclusive). Null means scan to the end.
        /// </summary>
        public byte[]? EndKey { get; }
    }

    /// <summary>
    /// Interface for executing batches of operations with multiple result sets.
    /// </summary>
    public interface IBatchExecutor
    {
        /// <summary>
        /// Executes a batch of operations and returns a reader for the results.
        /// Get and Scan operations produce result sets.
        /// Put and Delete operations produce affected row counts.
        /// </summary>
        /// <param name="operations">The operations to execute.</param>
        /// <returns>A reader for the multiple result sets.</returns>
        IMultiResultReader ExecuteBatch(IEnumerable<BatchOperation> operations);

        /// <summary>
        /// Executes a batch of operations asynchronously.
        /// </summary>
        /// <param name="operations">The operations to execute.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A reader for the multiple result sets.</returns>
        ValueTask<IMultiResultReader> ExecuteBatchAsync(
            IEnumerable<BatchOperation> operations,
            CancellationToken cancellationToken = default);
    }
}

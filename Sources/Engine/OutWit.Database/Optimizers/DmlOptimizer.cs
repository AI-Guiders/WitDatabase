using OutWit.Database.Context;
using OutWit.Database.Definitions;
using OutWit.Database.Expressions;
using OutWit.Database.Interfaces;
using OutWit.Database.Iterators;
using OutWit.Database.Model;
using OutWit.Database.Parser.Expressions;
using OutWit.Database.Parser.Schema.Types;
using OutWit.Database.Sql;
using OutWit.Database.Types;
using OutWit.Database.Values;

namespace OutWit.Database.Optimizers;

/// <summary>
/// Optimizes UPDATE and DELETE statements by using indexes when available.
/// Analyzes WHERE clause to determine if direct row access or index scan is more efficient.
/// </summary>
public sealed class DmlOptimizer
{
    #region Constants

    /// <summary>
    /// Minimum estimated rows before considering optimization.
    /// For very small tables, full scan may be faster.
    /// </summary>
    private const long MIN_ROWS_FOR_OPTIMIZATION = 50;

    #endregion

    #region Fields

    private readonly ContextExecution m_context;
    private readonly OptimizerQuery m_queryOptimizer;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a new DML optimizer.
    /// </summary>
    /// <param name="context">The execution context.</param>
    public DmlOptimizer(ContextExecution context)
    {
        m_context = context;
        m_queryOptimizer = new OptimizerQuery();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Creates an optimized iterator for UPDATE/DELETE statements.
    /// Attempts to use primary key lookup or index scan instead of full table scan.
    /// </summary>
    /// <param name="tableName">The target table name.</param>
    /// <param name="tableAlias">The table alias (or null).</param>
    /// <param name="whereClause">The WHERE clause expression (or null for all rows).</param>
    /// <returns>An optimized iterator.</returns>
    public IResultIterator CreateOptimizedIterator(
        string tableName, 
        string? tableAlias, 
        WitSqlExpression? whereClause)
    {
        var alias = tableAlias ?? tableName;
        
        // If no WHERE clause, must do full scan
        if (whereClause == null)
        {
            return WrapWithAlias(m_context.Database.CreateTableScan(tableName), alias);
        }

        // Get table definition
        var table = m_context.Database.GetTable(tableName);
        if (table == null)
        {
            return WrapWithAlias(m_context.Database.CreateTableScan(tableName), alias);
        }

        // Try to extract primary key equality condition
        var pkCondition = TryExtractPrimaryKeyCondition(whereClause, table);
        if (pkCondition != null)
        {
            // Use direct row lookup by primary key - O(log n) via PK index
            var iterator = CreatePrimaryKeyIterator(tableName, table, pkCondition.Value, whereClause);
            if (iterator != null)
            {
                return WrapWithAlias(iterator, alias);
            }
        }

        // Try to use secondary index
        var indexes = m_context.Database.GetTableIndexes(tableName).ToList();
        if (indexes.Count > 0)
        {
            // Estimate row count (cheap heuristic)
            long estimatedRows = EstimateRowCount(tableName);
            
            if (estimatedRows >= MIN_ROWS_FOR_OPTIMIZATION)
            {
                var strategy = m_queryOptimizer.FindBestIndex(tableName, whereClause, indexes, estimatedRows);
                if (strategy != null)
                {
                    var indexIterator = CreateIndexIterator(tableName, strategy);
                    
                    // Apply remaining WHERE predicates not covered by index
                    var filteredIterator = ApplyRemainingFilter(indexIterator, whereClause, strategy);
                    return WrapWithAlias(filteredIterator, alias);
                }
            }
        }

        // Fall back to full scan with filter
        var scanIterator = m_context.Database.CreateTableScan(tableName);
        var filterIterator = new IteratorFilter(scanIterator, whereClause, m_context);
        return WrapWithAlias(filterIterator, alias);
    }

    #endregion

    #region Primary Key Optimization

    /// <summary>
    /// Tries to extract a primary key equality condition from WHERE clause.
    /// Returns the PK value if found, null otherwise.
    /// </summary>
    private PrimaryKeyCondition? TryExtractPrimaryKeyCondition(WitSqlExpression whereClause, DefinitionTable table)
    {
        // Find primary key column(s)
        var pkColumns = table.Columns.Where(c => c.IsPrimaryKey).ToList();
        if (pkColumns.Count != 1)
        {
            // Composite PK or no PK - can't optimize simply
            return null;
        }

        var pkColumn = pkColumns[0];
        
        // Check if WHERE is a simple equality on PK: Id = @value
        if (whereClause is WitSqlExpressionBinary binary && 
            binary.Operator == BinaryOperatorType.Equal)
        {
            // Check for column = value pattern
            if (binary.Left is WitSqlExpressionColumnRef leftCol &&
                leftCol.ColumnName.Equals(pkColumn.Name, StringComparison.OrdinalIgnoreCase))
            {
                return new PrimaryKeyCondition
                {
                    ColumnName = pkColumn.Name,
                    ColumnType = pkColumn.Type,
                    ValueExpression = binary.Right,
                    FullCondition = whereClause
                };
            }
            
            // Check for value = column pattern (reversed)
            if (binary.Right is WitSqlExpressionColumnRef rightCol &&
                rightCol.ColumnName.Equals(pkColumn.Name, StringComparison.OrdinalIgnoreCase))
            {
                return new PrimaryKeyCondition
                {
                    ColumnName = pkColumn.Name,
                    ColumnType = pkColumn.Type,
                    ValueExpression = binary.Left,
                    FullCondition = whereClause
                };
            }
        }

        // Check for AND conditions containing PK equality
        if (whereClause is WitSqlExpressionBinary andBinary && 
            andBinary.Operator == BinaryOperatorType.And)
        {
            // Try left side
            var left = TryExtractPrimaryKeyCondition(andBinary.Left, table);
            if (left != null)
            {
                return new PrimaryKeyCondition
                {
                    ColumnName = left.Value.ColumnName,
                    ColumnType = left.Value.ColumnType,
                    ValueExpression = left.Value.ValueExpression,
                    FullCondition = whereClause,  // Full condition includes additional predicates
                    HasAdditionalPredicates = true
                };
            }

            // Try right side
            var right = TryExtractPrimaryKeyCondition(andBinary.Right, table);
            if (right != null)
            {
                return new PrimaryKeyCondition
                {
                    ColumnName = right.Value.ColumnName,
                    ColumnType = right.Value.ColumnType,
                    ValueExpression = right.Value.ValueExpression,
                    FullCondition = whereClause,
                    HasAdditionalPredicates = true
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Creates an iterator for primary key lookup using the implicit PK index.
    /// </summary>
    private IResultIterator? CreatePrimaryKeyIterator(
        string tableName,
        DefinitionTable table,
        PrimaryKeyCondition pkCondition,
        WitSqlExpression fullWhereClause)
    {
        // Evaluate the PK value
        var evaluator = new ExpressionEvaluator(m_context);
        var dummyRow = new WitSqlRow([], []);
        
        WitSqlValue pkValue;
        try
        {
            pkValue = evaluator.Evaluate(pkCondition.ValueExpression, dummyRow);
        }
        catch
        {
            // If evaluation fails (e.g., references other columns), fall back to scan
            return null;
        }

        if (pkValue.IsNull)
        {
            // Null PK won't match anything, return empty iterator
            return new IteratorEmpty(BuildTableSchema(table));
        }

        // Check for implicit PK index first (created automatically for PRIMARY KEY columns)
        var pkIndexName = $"_PK_{tableName}";
        var pkIndex = m_context.Database.GetIndex(pkIndexName);
        
        if (pkIndex != null)
        {
            // Use the PK index for O(log n) lookup
            var indexIterator = m_context.Database.CreateIndexSeek(tableName, pkIndexName, [pkValue]);
            
            // If there are additional predicates, apply them as a filter
            if (pkCondition.HasAdditionalPredicates)
            {
                return new IteratorFilter(indexIterator, fullWhereClause, m_context);
            }
            
            return indexIterator;
        }

        // Fallback: For single-column AUTOINCREMENT PK without explicit index,
        // the PK value equals _rowid, so we can use direct lookup
        var pkColumn = table.Columns.FirstOrDefault(c => c.IsPrimaryKey);
        if (pkColumn is { IsAutoIncrement: true })
        {
            long rowId = pkValue.AsInt64();
            var row = m_context.Database.GetRowById(tableName, rowId);

            if (row == null)
            {
                return new IteratorEmpty(BuildTableSchema(table));
            }

            // If there are additional predicates, verify they match
            if (pkCondition.HasAdditionalPredicates)
            {
                var result = evaluator.Evaluate(fullWhereClause, row.Value);
                if (!result.AsBool())
                {
                    return new IteratorEmpty(BuildTableSchema(table));
                }
            }

            return new IteratorSingleRow(row.Value.Values.ToArray(), row.Value.ColumnNames.ToArray());
        }

        // No optimization available
        return null;
    }

    private IReadOnlyList<WitSqlColumnInfo> BuildTableSchema(DefinitionTable table)
    {
        var schema = new List<WitSqlColumnInfo>(table.Columns.Count + 1);
        
        // Add _rowid
        schema.Add(new WitSqlColumnInfo
        {
            Name = "_rowid",
            Type = WitSqlType.Integer,
            IsNullable = false,
            TableName = table.Name
        });

        foreach (var col in table.Columns)
        {
            schema.Add(new WitSqlColumnInfo
            {
                Name = col.Name,
                Type = col.Type.ToSqlType(),
                IsNullable = col.Nullable,
                TableName = table.Name
            });
        }

        return schema;
    }

    #endregion

    #region Index Optimization

    /// <summary>
    /// Creates an index-based iterator.
    /// </summary>
    private IResultIterator CreateIndexIterator(string tableName, IndexStrategy strategy)
    {
        var evaluator = new ExpressionEvaluator(m_context);
        var dummyRow = new WitSqlRow([], []);

        switch (strategy.AccessType)
        {
            case IndexAccessType.Seek:
                var seekValue = evaluator.Evaluate(strategy.SeekValue!, dummyRow);
                return m_context.Database.CreateIndexSeek(tableName, strategy.IndexName, [seekValue]);

            case IndexAccessType.RangeScan:
                WitSqlValue? startValue = null;
                WitSqlValue? endValue = null;
                
                if (strategy.RangeStart != null)
                    startValue = evaluator.Evaluate(strategy.RangeStart, dummyRow);
                
                if (strategy.RangeEnd != null)
                    endValue = evaluator.Evaluate(strategy.RangeEnd, dummyRow);

                return m_context.Database.CreateIndexRangeScan(
                    tableName,
                    strategy.IndexName,
                    startValue,
                    strategy.RangeStartInclusive,
                    endValue,
                    strategy.RangeEndInclusive);

            default:
                throw new NotSupportedException($"Index access type not supported: {strategy.AccessType}");
        }
    }

    /// <summary>
    /// Applies remaining WHERE predicates not covered by the index.
    /// </summary>
    private IResultIterator ApplyRemainingFilter(
        IResultIterator iterator,
        WitSqlExpression whereClause,
        IndexStrategy strategy)
    {
        // If the index covered the entire WHERE clause, no additional filtering needed
        // For now, we apply the full WHERE as a safety measure
        // A more sophisticated implementation would remove the indexed predicates
        return new IteratorFilter(iterator, whereClause, m_context);
    }

    #endregion

    #region Helpers

    private long EstimateRowCount(string tableName)
    {
        // Quick estimation without full scan
        // In a real implementation, this would use statistics
        try
        {
            using var iterator = m_context.Database.CreateTableScan(tableName);
            iterator.Open();
            
            long count = 0;
            const long sampleLimit = 100;
            
            while (iterator.MoveNext() && count < sampleLimit)
            {
                count++;
            }
            
            // If we hit the limit, assume table is larger
            if (count >= sampleLimit)
                return count * 10;
            
            return count;
        }
        catch
        {
            return 100; // Default estimate
        }
    }

    private static IResultIterator WrapWithAlias(IResultIterator iterator, string alias)
    {
        return new IteratorAlias(iterator, alias);
    }

    #endregion

    #region Nested Types

    /// <summary>
    /// Represents a primary key condition extracted from WHERE clause.
    /// </summary>
    private readonly struct PrimaryKeyCondition
    {
        public required string ColumnName { get; init; }
        public required WitDataType ColumnType { get; init; }
        public required WitSqlExpression ValueExpression { get; init; }
        public required WitSqlExpression FullCondition { get; init; }
        public bool HasAdditionalPredicates { get; init; }
    }

    #endregion
}

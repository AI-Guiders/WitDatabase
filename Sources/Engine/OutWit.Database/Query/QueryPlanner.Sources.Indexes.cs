using OutWit.Database.Definitions;
using OutWit.Database.Expressions;
using OutWit.Database.Interfaces;
using OutWit.Database.Iterators;
using OutWit.Database.Values;

namespace OutWit.Database.Query;

/// <summary>
/// Index optimization and iterator creation for QueryPlanner.
/// </summary>
public sealed partial class QueryPlanner
{
    #region Optimized Table Iterator

    /// <summary>
    /// Creates an optimized table iterator, potentially using an index.
    /// </summary>
    private IResultIterator CreateOptimizedTableIterator(string tableName, string alias, Parser.Expressions.WitSqlExpression? whereClause)
    {
        // Get table definition (may be null for mocked databases in tests)
        var table = m_context.Database.GetTable(tableName);
        
        // If table definition is not available, fall back to simple table scan
        if (table == null)
        {
            return WrapWithAlias(m_context.Database.CreateTableScan(tableName), alias);
        }

        // Get available indexes for this table
        var indexes = m_context.Database.GetTableIndexes(tableName).ToList();

        // Try to find the best index strategy
        IndexStrategy? strategy = null;
        if (whereClause != null && indexes.Count > 0)
        {
            // Estimate row count (we don't have statistics, so use a heuristic)
            long estimatedRowCount = EstimateTableRowCount(tableName);
            
            if (estimatedRowCount >= MIN_ROWS_FOR_INDEX)
            {
                strategy = m_optimizer.FindBestIndex(tableName, whereClause, indexes, estimatedRowCount);
            }
        }

        IResultIterator iterator;

        if (strategy != null)
        {
            // Use index-based access
            iterator = CreateIndexIterator(tableName, strategy);
        }
        else
        {
            // Fall back to table scan
            iterator = m_context.Database.CreateTableScan(tableName);
        }

        return WrapWithAlias(iterator, alias);
    }

    /// <summary>
    /// Creates an index-based iterator based on the strategy.
    /// </summary>
    private IResultIterator CreateIndexIterator(string tableName, IndexStrategy strategy)
    {
        var evaluator = new ExpressionEvaluator(m_context);
        var dummyRow = new WitSqlRow([], []);

        switch (strategy.AccessType)
        {
            case IndexAccessType.Seek:
                // Equality lookup
                var seekValue = evaluator.Evaluate(strategy.SeekValue!, dummyRow);
                return m_context.Database.CreateIndexSeek(
                    tableName, 
                    strategy.IndexName, 
                    [seekValue]);

            case IndexAccessType.RangeScan:
                // Range scan - explicitly handle nullable WitSqlValue
                WitSqlValue? startValue = null;
                WitSqlValue? endValue = null;
                
                if (strategy.RangeStart != null)
                {
                    startValue = evaluator.Evaluate(strategy.RangeStart, dummyRow);
                }
                
                if (strategy.RangeEnd != null)
                {
                    endValue = evaluator.Evaluate(strategy.RangeEnd, dummyRow);
                }

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
    /// Estimates the row count for a table.
    /// Without statistics, we use a simple heuristic.
    /// </summary>
    private long EstimateTableRowCount(string tableName)
    {
        // TODO: Implement proper statistics collection
        // For now, do a quick scan to count rows (expensive but accurate)
        // In a real implementation, we would maintain statistics
        
        try
        {
            using var iterator = m_context.Database.CreateTableScan(tableName);
            iterator.Open();
            
            long count = 0;
            const long sampleLimit = 1000; // Sample up to 1000 rows
            
            while (iterator.MoveNext() && count < sampleLimit)
            {
                count++;
            }
            
            // If we hit the limit, estimate based on sample
            if (count >= sampleLimit)
            {
                // Assume table is larger, return a higher estimate
                return count * 10;
            }
            
            return count;
        }
        catch
        {
            // If counting fails, return a default estimate
            return 100;
        }
    }

    #endregion
}

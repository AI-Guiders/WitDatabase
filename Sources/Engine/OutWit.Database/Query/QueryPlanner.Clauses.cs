using OutWit.Database.Core.Interfaces;
using OutWit.Database.Expressions;
using OutWit.Database.Interfaces;
using OutWit.Database.Iterators;
using OutWit.Database.Parser.Expressions;
using OutWit.Database.Parser.Schema.Clauses;
using OutWit.Database.Parser.Schema.Types;
using OutWit.Database.Parser.Statements;
using OutWit.Database.Values;

namespace OutWit.Database.Query;

/// <summary>
/// SQL clause application for QueryPlanner (WHERE, ORDER BY, LIMIT, etc.).
/// </summary>
public sealed partial class QueryPlanner
{
    #region WHERE Clause

    private IResultIterator ApplyWhereClause(IResultIterator iterator, WitSqlExpression? whereClause, WitSqlStatementSelect? select = null)
    {
        if (whereClause == null)
            return iterator;

        // Check if we already used an index - in that case, we might still need
        // residual filtering for predicates not covered by the index
        // The index iterator handles the indexed predicate, but other predicates need filtering
        
        return new IteratorFilter(iterator, whereClause, m_context);
    }

    #endregion

    #region FOR UPDATE/SHARE Locking

    private IResultIterator ApplyLockingClause(IResultIterator iterator, WitSqlStatementSelect select)
    {
        if (select.ForClause == null || select.ForClause.LockingType == LockingType.None)
            return iterator;

        // FOR UPDATE/SHARE requires an active MVCC transaction
        var transaction = m_context.Database.CurrentTransaction;
        if (transaction == null)
        {
            throw new InvalidOperationException(
                "FOR UPDATE/FOR SHARE requires an active transaction. " +
                "Start a transaction with BEGIN TRANSACTION first.");
        }

        if (transaction is not IMvccTransaction mvccTransaction)
        {
            throw new InvalidOperationException(
                "FOR UPDATE/FOR SHARE requires MVCC transaction support. " +
                "The current transaction type does not support row-level locking.");
        }

        // Get the table name from FROM clause
        var tableName = GetPrimaryTableName(select);
        if (tableName == null)
        {
            throw new InvalidOperationException(
                "FOR UPDATE/FOR SHARE requires a table in the FROM clause.");
        }

        return new IteratorLocking(iterator, select.ForClause, mvccTransaction, tableName);
    }

    #endregion

    #region ORDER BY Clause

    private IResultIterator ApplyOrderByClause(IResultIterator iterator, IReadOnlyList<ClauseOrderByItem>? orderByClause)
    {
        if (orderByClause == null || orderByClause.Count == 0)
            return iterator;

        return new IteratorSort(iterator, orderByClause, m_context);
    }

    #endregion

    #region LIMIT/OFFSET Clause

    private IResultIterator ApplyLimitClause(IResultIterator iterator, WitSqlExpression? limitCount, WitSqlExpression? limitOffset)
    {
        if (limitCount == null)
            return iterator;

        var evaluator = new ExpressionEvaluator(m_context);
        var dummyRow = new WitSqlRow([], []);

        var limit = evaluator.Evaluate(limitCount, dummyRow).AsInt64();
        var offset = limitOffset != null
            ? evaluator.Evaluate(limitOffset, dummyRow).AsInt64()
            : 0;

        return new IteratorLimit(iterator, limit, offset);
    }

    #endregion

    #region Projection

    private IResultIterator ApplyProjection(IResultIterator iterator, IReadOnlyList<ClauseSelectItem> selectList)
    {
        // Skip projection for SELECT *
        if (IsSelectStar(selectList))
            return iterator;

        return new IteratorProject(iterator, selectList, m_context);
    }

    #endregion

    #region DISTINCT

    private static IResultIterator ApplyDistinct(IResultIterator iterator, bool isDistinct)
    {
        if (!isDistinct)
            return iterator;

        return new IteratorDistinct(iterator);
    }

    #endregion

    #region Set Operations (UNION, INTERSECT, EXCEPT)

    private IResultIterator ApplySetOperations(IResultIterator iterator, WitSqlStatementSelect select)
    {
        if (select.SetOperations == null || select.SetOperations.Count == 0)
            return iterator;

        foreach (var setOp in select.SetOperations)
        {
            var rightIterator = Plan(setOp.RightQuery);
            iterator = CreateSetOperationIterator(iterator, rightIterator, setOp);
        }

        return iterator;
    }

    private static IResultIterator CreateSetOperationIterator(
        IResultIterator left,
        IResultIterator right,
        ClauseSetOperation setOp)
    {
        return setOp.OperationType switch
        {
            SetOperationType.Union => new IteratorUnion(left, right, setOp.IsAll),
            SetOperationType.Intersect => new IteratorIntersect(left, right, setOp.IsAll),
            SetOperationType.Except => new IteratorExcept(left, right, setOp.IsAll),
            _ => throw new NotSupportedException($"Set operation {setOp.OperationType} not supported")
        };
    }

    #endregion
}

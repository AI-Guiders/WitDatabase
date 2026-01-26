using System.Data;
using System.Linq.Expressions;
using OutWit.Database.Context;
using OutWit.Database.Definitions;
using OutWit.Database.Expressions;
using OutWit.Database.Interfaces;
using OutWit.Database.Iterators;
using OutWit.Database.Parser;
using OutWit.Database.Parser.Expressions;
using OutWit.Database.Parser.Schema;
using OutWit.Database.Parser.Schema.ColumnConstraints;
using OutWit.Database.Parser.Schema.TableSources;
using OutWit.Database.Parser.Schema.Types;
using OutWit.Database.Parser.Statements;
using OutWit.Database.Query;
using OutWit.Database.Sql;
using OutWit.Database.Types;
using OutWit.Database.Values;

namespace OutWit.Database.Statements;

/// <summary>
/// Executes WitSql statements against the database.
/// </summary>
public sealed partial class StatementExecutor
{
    #region Constants

    /// <summary>
    /// Maximum number of expressions to cache.
    /// </summary>
    private const int MAX_EXPRESSION_CACHE_SIZE = 256;

    #endregion

    #region Fields

    private readonly ContextExecution m_context;
    private readonly QueryPlanner m_planner;
    
    /// <summary>
    /// Cache for parsed SQL expressions (CHECK constraints, computed columns, etc.).
    /// Key is the SQL expression string, value is the parsed expression.
    /// </summary>
    private readonly Dictionary<string, WitSqlExpression> m_expressionCache = new(StringComparer.Ordinal);

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a new statement executor.
    /// </summary>
    /// <param name="context">The execution context.</param>
    public StatementExecutor(ContextExecution context)
    {
        m_context = context;
        m_planner = new QueryPlanner(context);
    }

    #endregion

    #region Execute

    /// <summary>
    /// Executes a WitSql statement and returns the result.
    /// </summary>
    /// <param name="statement">The statement to execute.</param>
    /// <returns>The execution result.</returns>
    public WitSqlResult Execute(WitSqlStatement statement)
    {
        return statement switch
        {
            // DML
            WitSqlStatementSelect select => ExecuteSelect(select),
            WitSqlStatementInsert insert => ExecuteInsert(insert),
            WitSqlStatementUpdate update => ExecuteUpdate(update),
            WitSqlStatementDelete delete => ExecuteDelete(delete),
            WitSqlStatementTruncate truncate => ExecuteTruncate(truncate),
            WitSqlStatementMerge merge => ExecuteMerge(merge),
            
            // DDL - Tables
            WitSqlStatementCreateTable createTable => ExecuteCreateTable(createTable),
            WitSqlStatementDropTable dropTable => ExecuteDropTable(dropTable),
            WitSqlStatementAlterTable alterTable => ExecuteAlterTable(alterTable),
            
            // DDL - Indexes
            WitSqlStatementCreateIndex createIndex => ExecuteCreateIndex(createIndex),
            WitSqlStatementDropIndex dropIndex => ExecuteDropIndex(dropIndex),
            
            // DDL - Views
            WitSqlStatementCreateView createView => ExecuteCreateView(createView),
            WitSqlStatementDropView dropView => ExecuteDropView(dropView),
            
            // DDL - Triggers
            WitSqlStatementCreateTrigger createTrigger => ExecuteCreateTrigger(createTrigger),
            WitSqlStatementDropTrigger dropTrigger => ExecuteDropTrigger(dropTrigger),
            
            // DDL - Sequences
            WitSqlStatementCreateSequence createSequence => ExecuteCreateSequence(createSequence),
            WitSqlStatementDropSequence dropSequence => ExecuteDropSequence(dropSequence),
            WitSqlStatementAlterSequence alterSequence => ExecuteAlterSequence(alterSequence),
            
            // Transaction Control
            WitSqlStatementBeginTransaction beginTx => ExecuteBeginTransaction(beginTx),
            WitSqlStatementCommit commit => ExecuteCommit(commit),
            WitSqlStatementRollback rollback => ExecuteRollback(rollback),
            WitSqlStatementSavepoint savepoint => ExecuteSavepoint(savepoint),
            WitSqlStatementReleaseSavepoint release => ExecuteReleaseSavepoint(release),
            WitSqlStatementSetTransaction setTx => ExecuteSetTransaction(setTx),
            
            // Query Analysis
            WitSqlStatementExplain explain => ExecuteExplain(explain),
            
            _ => throw new NotSupportedException($"Statement type not supported: {statement.GetType().Name}")
        };
    }

    #endregion

    #region Expression Cache

    /// <summary>
    /// Gets a parsed expression from cache or parses it and adds to cache.
    /// </summary>
    /// <param name="expressionSql">The SQL expression string.</param>
    /// <returns>The parsed expression.</returns>
    private WitSqlExpression GetOrParseExpression(string expressionSql)
    {
        if (m_expressionCache.TryGetValue(expressionSql, out var cached))
            return cached;

        var parsed = WitSql.ParseExpression(expressionSql);

        // Only cache if we haven't exceeded the limit
        if (m_expressionCache.Count < MAX_EXPRESSION_CACHE_SIZE)
        {
            m_expressionCache[expressionSql] = parsed;
        }

        return parsed;
    }

    /// <summary>
    /// Clears the expression cache.
    /// </summary>
    public void ClearExpressionCache()
    {
        m_expressionCache.Clear();
    }

    #endregion

    #region Helpers

    private IEnumerable<WitSqlRow> EnumerateRows(IResultIterator iterator)
    {
        try
        {
            while (iterator.MoveNext())
            {
                yield return iterator.Current;
            }
        }
        finally
        {
            iterator.Dispose();
        }
    }

    #endregion
}


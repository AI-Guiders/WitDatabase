using OutWit.Database.Definitions;
using OutWit.Database.Expressions;
using OutWit.Database.Parser.Expressions;
using OutWit.Database.Parser.Schema.Clauses;
using OutWit.Database.Types;
using OutWit.Database.Values;

namespace OutWit.Database.Statements;

/// <summary>
/// RETURNING clause support for DML statements.
/// </summary>
public sealed partial class StatementExecutor
{
    #region RETURNING Clause

    /// <summary>
    /// Builds a row for RETURNING clause based on the source row and select list.
    /// </summary>
    private WitSqlRow BuildReturningRow(WitSqlRow sourceRow, IReadOnlyList<ClauseSelectItem> returningClause, DefinitionTable table)
    {
        var evaluator = new ExpressionEvaluator(m_context);
        var values = new List<WitSqlValue>();
        var names = new List<string>();

        foreach (var item in returningClause)
        {
            if (item.IsStar)
            {
                // RETURNING * - add all columns from the table
                foreach (var col in table.Columns)
                {
                    values.Add(sourceRow[col.Name]);
                    names.Add(col.Name);
                }
            }
            else if (item.Expression != null)
            {
                var value = evaluator.Evaluate(item.Expression, sourceRow);
                values.Add(value);

                var name = item.Alias ?? GetExpressionName(item.Expression);
                names.Add(name);
            }
        }

        return new WitSqlRow([.. values], [.. names]);
    }

    /// <summary>
    /// Builds the schema for RETURNING clause.
    /// </summary>
    private IReadOnlyList<WitSqlColumnInfo> BuildReturningSchema(IReadOnlyList<ClauseSelectItem> returningClause, DefinitionTable table)
    {
        var schema = new List<WitSqlColumnInfo>();

        foreach (var item in returningClause)
        {
            if (item.IsStar)
            {
                // RETURNING * - add all columns from the table
                foreach (var col in table.Columns)
                {
                    schema.Add(new WitSqlColumnInfo
                    {
                        Name = col.Name,
                        Type = col.Type.ToSqlType()
                    });
                }
            }
            else if (item.Expression != null)
            {
                var name = item.Alias ?? GetExpressionName(item.Expression);
                var type = InferExpressionType(item.Expression, table);
                schema.Add(new WitSqlColumnInfo { Name = name, Type = type });
            }
        }

        return schema;
    }

    /// <summary>
    /// Gets a name for an expression (column name or generated name).
    /// </summary>
    private static string GetExpressionName(WitSqlExpression expression)
    {
        return expression switch
        {
            WitSqlExpressionColumnRef colRef => colRef.ColumnName,
            WitSqlExpressionFunctionCall func => func.FunctionName,
            _ => "column"
        };
    }

    /// <summary>
    /// Infers the SQL type of an expression based on the table schema.
    /// </summary>
    private static WitSqlType InferExpressionType(WitSqlExpression expression, DefinitionTable table)
    {
        if (expression is WitSqlExpressionColumnRef colRef)
        {
            var col = table.GetColumn(colRef.ColumnName);
            if (col != null)
            {
                return col.Type.ToSqlType();
            }
        }

        // Default to Text for complex expressions
        return WitSqlType.Text;
    }

    #endregion
}

using OutWit.Database.Parser.Expressions;
using OutWit.Database.Parser.Generated;
using OutWit.Database.Parser.Schema;
using OutWit.Database.Parser.Schema.AlterActions;
using OutWit.Database.Parser.Schema.Clauses;
using OutWit.Database.Parser.Schema.ColumnConstraints;
using OutWit.Database.Parser.Schema.TableConstraints;
using OutWit.Database.Parser.Schema.Types;
using OutWit.Database.Parser.Statements;

namespace OutWit.Database.Parser.Visitor;

internal sealed partial class WitSqlVisitor
{
    #region CREATE TABLE Statement

    public override WitSqlStatementCreateTable VisitCreateTableStatement(WitSqlParser.CreateTableStatementContext context)
    {
        var columns = new List<WitSqlColumn>();
        var constraints = new List<TableConstraint>();

        foreach (var element in context.tableElement())
        {
            if (element.columnDefinition() is { } colDef)
            {
                columns.Add(VisitColumnDefinition(colDef));
            }
            else if (element.tableConstraint() is { } tableCons)
            {
                constraints.Add(VisitTableConstraint(tableCons));
            }
        }

        return new WitSqlStatementCreateTable
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            TableName = context.tableName().GetText(),
            IfNotExists = context.EXISTS() != null,
            Columns = columns,
            Constraints = constraints.Count > 0 ? constraints : null
        };
    }

    private WitSqlColumn VisitColumnDefinition(WitSqlParser.ColumnDefinitionContext context)
    {
        return context switch
        {
            WitSqlParser.RegularColumnContext regular => new WitSqlColumn
            {
                Name = regular.columnName().GetText(),
                DataType = VisitDataType(regular.dataType()),
                Constraints = regular.columnConstraint()?.Select(VisitColumnConstraint).ToList()
            },
            WitSqlParser.ComputedColumnContext computed => new WitSqlColumn
            {
                Name = computed.columnName().GetText(),
                ComputedExpression = VisitExpression(computed.expression()),
                ComputedType = computed.STORED() != null ? ComputedColumnType.Stored
                             : computed.VIRTUAL() != null ? ComputedColumnType.Virtual
                             : ComputedColumnType.Virtual // Default is Virtual
            },
            _ => throw new InvalidOperationException($"Unknown column definition type: {context.GetType()}")
        };
    }

    public override WitSqlDataType VisitDataType(WitSqlParser.DataTypeContext context)
    {
        var typeName = context.typeName().GetText().ToUpperInvariant();
        int? length = null;
        int? precision = null;
        int? scale = null;

        var typeParams = context.typeParam();
        if (typeParams.Length >= 1)
        {
            var firstParam = typeParams[0].GetText();
            if (firstParam.Equals("MAX", StringComparison.OrdinalIgnoreCase))
                length = int.MaxValue;
            else if (int.TryParse(firstParam, out var len))
                length = len;
        }
        if (typeParams.Length >= 2 && int.TryParse(typeParams[1].GetText(), out var s))
        {
            precision = length;
            scale = s;
            length = null;
        }

        return new WitSqlDataType
        {
            TypeName = typeName,
            Length = length,
            Precision = precision,
            Scale = scale
        };
    }

    private ColumnConstraint VisitColumnConstraint(WitSqlParser.ColumnConstraintContext context)
    {
        return context switch
        {
            WitSqlParser.NullConstraintContext nullCtx => new ColumnConstraintNotNull
            {
                IsNotNull = nullCtx.NOT() != null
            },
            WitSqlParser.PrimaryKeyConstraintContext pk => new ColumnConstraintPrimaryKey
            {
                AutoIncrement = pk.AUTOINCREMENT() != null
            },
            WitSqlParser.UniqueConstraintContext => new ColumnConstraintUnique(),
            WitSqlParser.DefaultConstraintContext def => new ColumnConstraintDefault
            {
                Value = def.expression() != null
                    ? VisitExpression(def.expression())
                    : VisitLiteral(def.literal())
            },
            WitSqlParser.CheckConstraintContext check => new ColumnConstraintCheck
            {
                Condition = VisitExpression(check.expression())
            },
            WitSqlParser.ReferencesConstraintContext refs => new ColumnConstraintReferences
            {
                ForeignTable = refs.tableName().GetText(),
                ForeignColumn = refs.columnName()?.GetText(),
                OnDelete = GetReferenceAction(refs.referenceOption(), isDelete: true),
                OnUpdate = GetReferenceAction(refs.referenceOption(), isDelete: false)
            },
            _ => throw new InvalidOperationException($"Unknown column constraint: {context.GetType()}")
        };
    }

    private static ReferenceActionType GetReferenceAction(WitSqlParser.ReferenceOptionContext[] options, bool isDelete)
    {
        foreach (var opt in options)
        {
            bool matches = isDelete ? opt.DELETE() != null : opt.UPDATE() != null;
            if (!matches) continue;

            var action = opt.referenceAction();
            if (action.CASCADE() != null) return ReferenceActionType.Cascade;
            if (action.RESTRICT() != null) return ReferenceActionType.Restrict;
            if (action.NULL() != null) return ReferenceActionType.SetNull;
            if (action.DEFAULT() != null) return ReferenceActionType.SetDefault;
        }
        return ReferenceActionType.NoAction;
    }

    private TableConstraint VisitTableConstraint(WitSqlParser.TableConstraintContext context)
    {
        return context switch
        {
            WitSqlParser.TablePrimaryKeyContext pk => new TableConstraintPrimaryKey
            {
                Name = pk.constraintName()?.GetText(),
                Columns = pk.columnName().Select(c => c.GetText()).ToList()
            },
            WitSqlParser.TableUniqueContext uniq => new TableConstraintUnique
            {
                Name = uniq.constraintName()?.GetText(),
                Columns = uniq.columnName().Select(c => c.GetText()).ToList()
            },
            WitSqlParser.TableForeignKeyContext fk => ParseTableForeignKey(fk),
            WitSqlParser.TableCheckContext check => new TableConstraintCheck
            {
                Name = check.constraintName()?.GetText(),
                Condition = VisitExpression(check.expression())
            },
            _ => throw new InvalidOperationException($"Unknown table constraint: {context.GetType()}")
        };
    }

    private TableConstraintForeignKey ParseTableForeignKey(WitSqlParser.TableForeignKeyContext fk)
    {
        var constraintName = fk.constraintName()?.GetText();

        var allColumnNames = fk.columnName();
        var referencesToken = fk.REFERENCES();

        var localColumns = new List<string>();
        var foreignColumns = new List<string>();

        int referencesPos = referencesToken.Symbol.StartIndex;

        foreach (var col in allColumnNames)
        {
            if (col.Stop.StopIndex < referencesPos)
            {
                localColumns.Add(col.GetText());
            }
            else
            {
                foreignColumns.Add(col.GetText());
            }
        }

        return new TableConstraintForeignKey
        {
            Name = constraintName,
            Columns = localColumns,
            ForeignTable = fk.tableName().GetText(),
            ForeignColumns = foreignColumns.Count > 0 ? foreignColumns : null,
            OnDelete = GetReferenceAction(fk.referenceOption(), isDelete: true),
            OnUpdate = GetReferenceAction(fk.referenceOption(), isDelete: false)
        };
    }

    #endregion

    #region DROP TABLE Statement

    public override WitSqlStatementDropTable VisitDropTableStatement(WitSqlParser.DropTableStatementContext context)
    {
        return new WitSqlStatementDropTable
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            TableName = context.tableName().GetText(),
            IfExists = context.EXISTS() != null
        };
    }

    #endregion

    #region ALTER TABLE Statement

    public override WitSqlStatementAlterTable VisitAlterTableStatement(WitSqlParser.AlterTableStatementContext context)
    {
        return new WitSqlStatementAlterTable
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            TableName = context.tableName().GetText(),
            Action = VisitAlterAction(context.alterAction())
        };
    }

    private AlterAction VisitAlterAction(WitSqlParser.AlterActionContext context)
    {
        return context switch
        {
            WitSqlParser.AlterAddColumnContext add => new AlterActionAddColumn
            {
                WitSqlColumn = VisitColumnDefinition(add.columnDefinition())
            },
            WitSqlParser.AlterAddConstraintContext addCons => ParseAlterAddConstraint(addCons),
            WitSqlParser.AlterDropColumnContext drop => new AlterActionDropColumn
            {
                ColumnName = drop.columnName().GetText()
            },
            WitSqlParser.AlterDropConstraintContext dropCons => new AlterActionDropConstraint
            {
                ConstraintName = dropCons.constraintName().GetText()
            },
            WitSqlParser.AlterRenameTableContext rename => new AlterActionRenameTable
            {
                NewName = rename.tableName().GetText()
            },
            WitSqlParser.AlterRenameColumnContext renameCol => new AlterActionRenameColumn
            {
                OldName = renameCol.columnName(0).GetText(),
                NewName = renameCol.columnName(1).GetText()
            },
            WitSqlParser.AlterAlterColumnContext alterCol => ParseAlterColumnAction(alterCol),
            _ => throw new InvalidOperationException($"Unknown alter action: {context.GetType()}")
        };
    }

    private AlterActionAddConstraint ParseAlterAddConstraint(WitSqlParser.AlterAddConstraintContext context)
    {
        var constraint = VisitTableConstraint(context.tableConstraint());
        
        // If the alterAction has a constraintName but the tableConstraint doesn't,
        // use the one from alterAction
        if (constraint.Name == null && context.constraintName() != null)
        {
            // We need to create a new constraint with the name set
            constraint = constraint switch
            {
                TableConstraintPrimaryKey pk => new TableConstraintPrimaryKey
                {
                    Name = context.constraintName().GetText(),
                    Columns = pk.Columns
                },
                TableConstraintUnique uniq => new TableConstraintUnique
                {
                    Name = context.constraintName().GetText(),
                    Columns = uniq.Columns
                },
                TableConstraintForeignKey fk => new TableConstraintForeignKey
                {
                    Name = context.constraintName().GetText(),
                    Columns = fk.Columns,
                    ForeignTable = fk.ForeignTable,
                    ForeignColumns = fk.ForeignColumns,
                    OnDelete = fk.OnDelete,
                    OnUpdate = fk.OnUpdate
                },
                TableConstraintCheck check => new TableConstraintCheck
                {
                    Name = context.constraintName().GetText(),
                    Condition = check.Condition
                },
                _ => constraint
            };
        }
        
        return new AlterActionAddConstraint
        {
            Constraint = constraint
        };
    }

    private AlterActionAlterColumn ParseAlterColumnAction(WitSqlParser.AlterAlterColumnContext context)
    {
        var columnName = context.columnName().GetText();
        var actionContext = context.alterColumnAction();

        return actionContext switch
        {
            WitSqlParser.AlterColumnTypeContext tc => new AlterActionAlterColumn
            {
                ColumnName = columnName,
                NewType = VisitDataType(tc.dataType())
            },
            WitSqlParser.AlterColumnSetDefaultContext sd => new AlterActionAlterColumn
            {
                ColumnName = columnName,
                NewDefault = VisitExpression(sd.expression())
            },
            WitSqlParser.AlterColumnDropDefaultContext => new AlterActionAlterColumn
            {
                ColumnName = columnName,
                DropDefault = true
            },
            WitSqlParser.AlterColumnSetNotNullContext => new AlterActionAlterColumn
            {
                ColumnName = columnName,
                SetNotNull = true
            },
            WitSqlParser.AlterColumnDropNotNullContext => new AlterActionAlterColumn
            {
                ColumnName = columnName,
                SetNotNull = false
            },
            _ => throw new InvalidOperationException($"Unknown alter column action: {actionContext.GetType()}")
        };
    }

    #endregion

    #region CREATE/DROP INDEX Statements

    public override WitSqlStatementCreateIndex VisitCreateIndexStatement(WitSqlParser.CreateIndexStatementContext context)
    {
        var elements = context.indexElement().Select(VisitIndexElement).ToList();

        var includeColumns = context.includeClause()?.columnName()
            .Select(c => c.GetText())
            .ToList();

        WitSqlExpression? whereClause = null;
        if (context.whereClause() is { } where)
        {
            whereClause = VisitExpression(where.expression());
        }

        return new WitSqlStatementCreateIndex
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            IndexName = context.indexName().GetText(),
            TableName = context.tableName().GetText(),
            IsUnique = context.UNIQUE() != null,
            IfNotExists = context.EXISTS() != null,
            Elements = elements,
            IncludeColumns = includeColumns,
            WhereClause = whereClause
        };
    }

    private ClauseIndexElement VisitIndexElement(WitSqlParser.IndexElementContext context)
    {
        return context switch
        {
            WitSqlParser.IndexColumnElementContext col => new ClauseIndexElement
            {
                ColumnName = col.columnName().GetText(),
                Descending = col.DESC() != null
            },
            WitSqlParser.IndexExpressionElementContext expr => new ClauseIndexElement
            {
                Expression = VisitExpression(expr.expression()),
                Descending = expr.DESC() != null
            },
            _ => throw new InvalidOperationException($"Unknown index element type: {context.GetType()}")
        };
    }

    public override WitSqlStatementDropIndex VisitDropIndexStatement(WitSqlParser.DropIndexStatementContext context)
    {
        return new WitSqlStatementDropIndex
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            IndexName = context.indexName().GetText(),
            IfExists = context.EXISTS() != null
        };
    }

    #endregion

    #region CREATE/DROP VIEW Statements

    public override WitSqlStatementCreateView VisitCreateViewStatement(WitSqlParser.CreateViewStatementContext context)
    {
        var columnNames = context.columnName()?.Select(c => c.GetText()).ToList();

        return new WitSqlStatementCreateView
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            ViewName = context.viewName().GetText(),
            IfNotExists = context.EXISTS() != null,
            ColumnNames = columnNames,
            Query = VisitQueryExpression(context.queryExpression())
        };
    }

    public override WitSqlStatementDropView VisitDropViewStatement(WitSqlParser.DropViewStatementContext context)
    {
        return new WitSqlStatementDropView
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            ViewName = context.viewName().GetText(),
            IfExists = context.EXISTS() != null
        };
    }

    #endregion

    #region CREATE/DROP TRIGGER Statements

    public override WitSqlStatementCreateTrigger VisitCreateTriggerStatement(WitSqlParser.CreateTriggerStatementContext context)
    {
        var timing = context.triggerTime().BEFORE() != null ? TriggerTimingType.Before :
                     context.triggerTime().AFTER() != null ? TriggerTimingType.After :
                     TriggerTimingType.InsteadOf;

        var evt = context.triggerEvent().INSERT() != null ? TriggerEventType.Insert :
                  context.triggerEvent().DELETE() != null ? TriggerEventType.Delete :
                  TriggerEventType.Update;

        var updateColumns = context.triggerEvent().UPDATE() != null && context.triggerEvent().columnName().Length > 0
            ? context.triggerEvent().columnName().Select(c => c.GetText()).ToList()
            : null;

        var body = new List<WitSqlStatement>();
        var bodyStatements = context.statement();
        foreach (var stmtCtx in bodyStatements)
        {
            var stmt = VisitStatement(stmtCtx);
            if (stmt != null)
                body.Add(stmt);
        }

        return new WitSqlStatementCreateTrigger
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            TriggerName = context.triggerName().GetText(),
            IfNotExists = context.EXISTS() != null,
            Time = timing,
            Event = evt,
            UpdateColumns = updateColumns,
            TableName = context.tableName().GetText(),
            ForEachRow = context.ROW() != null,
            WhenCondition = context.expression() != null ? VisitExpression(context.expression()) : null,
            Body = body
        };
    }

    public override WitSqlStatementDropTrigger VisitDropTriggerStatement(WitSqlParser.DropTriggerStatementContext context)
    {
        return new WitSqlStatementDropTrigger
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            TriggerName = context.triggerName().GetText(),
            IfExists = context.EXISTS() != null
        };
    }

    #endregion

    #region CREATE/DROP/ALTER SEQUENCE Statements

    public override WitSqlStatementCreateSequence VisitCreateSequenceStatement(WitSqlParser.CreateSequenceStatementContext context)
    {
        long startWith = 1;
        if (context.INTEGER_LITERAL() != null)
        {
            startWith = long.Parse(context.INTEGER_LITERAL().GetText());
        }

        return new WitSqlStatementCreateSequence
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            SequenceName = context.sequenceName().GetText(),
            IfNotExists = context.EXISTS() != null,
            StartWith = startWith
        };
    }

    public override WitSqlStatementDropSequence VisitDropSequenceStatement(WitSqlParser.DropSequenceStatementContext context)
    {
        return new WitSqlStatementDropSequence
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            SequenceName = context.sequenceName().GetText(),
            IfExists = context.EXISTS() != null
        };
    }

    public override WitSqlStatementAlterSequence VisitAlterSequenceStatement(WitSqlParser.AlterSequenceStatementContext context)
    {
        long? restartWith = null;
        if (context.INTEGER_LITERAL() != null)
        {
            restartWith = long.Parse(context.INTEGER_LITERAL().GetText());
        }

        return new WitSqlStatementAlterSequence
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            SequenceName = context.sequenceName().GetText(),
            RestartWith = restartWith
        };
    }

    #endregion

    #region TRUNCATE TABLE Statement

    public override WitSqlStatementTruncate VisitTruncateTableStatement(WitSqlParser.TruncateTableStatementContext context)
    {
        return new WitSqlStatementTruncate
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            TableName = context.tableName().GetText()
        };
    }

    #endregion

    #region SIGNAL Statement

    public override WitSqlStatementSignal VisitSignalStatement(WitSqlParser.SignalStatementContext context)
    {
        // Parse SQLSTATE code - remove quotes from string literal
        var sqlStateText = context.STRING_LITERAL().GetText();
        var sqlState = sqlStateText.Substring(1, sqlStateText.Length - 2);

        // Parse optional MESSAGE_TEXT expression
        WitSqlExpression? messageText = null;
        if (context.expression() is { } expr)
        {
            messageText = VisitExpression(expr);
        }

        return new WitSqlStatementSignal
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            SqlState = sqlState,
            MessageText = messageText
        };
    }

    #endregion
}

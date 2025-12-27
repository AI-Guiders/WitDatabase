using OutWit.Database.Definitions;
using OutWit.Database.Parser.Schema.Types;
using OutWit.Database.Parser.Serializers;
using OutWit.Database.Parser.Statements;
using OutWit.Database.Types;

namespace OutWit.Database.Statements;

/// <summary>
/// DDL execution for TRIGGER operations (CREATE, DROP).
/// </summary>
public sealed partial class StatementExecutor
{
    #region CREATE TRIGGER

    private WitSqlResult ExecuteCreateTrigger(WitSqlStatementCreateTrigger createTrigger)
    {
        // Check IF NOT EXISTS
        if (createTrigger.IfNotExists && m_context.Database.GetTrigger(createTrigger.TriggerName) != null)
            return new WitSqlResult();

        // Serialize WHEN condition if present
        string? whenConditionSql = createTrigger.WhenCondition != null 
            ? WitSqlExpressionSerializer.Serialize(createTrigger.WhenCondition) 
            : null;

        // Use original body SQL if available, otherwise serialize
        var bodySql = createTrigger.BodyWitSql ?? SerializeTriggerBody(createTrigger.Body);

        var triggerMetadata = new DefinitionTrigger
        {
            Name = createTrigger.TriggerName,
            TableName = createTrigger.TableName,
            Time = MapTriggerTiming(createTrigger.Time),
            Event = MapTriggerEvent(createTrigger.Event),
            UpdateColumns = createTrigger.UpdateColumns,
            ForEachRow = createTrigger.ForEachRow,
            WhenCondition = whenConditionSql,
            Body = bodySql
        };

        m_context.Database.CreateTrigger(triggerMetadata);
        return new WitSqlResult();
    }

    #endregion

    #region DROP TRIGGER

    private WitSqlResult ExecuteDropTrigger(WitSqlStatementDropTrigger dropTrigger)
    {
        if (dropTrigger.IfExists && m_context.Database.GetTrigger(dropTrigger.TriggerName) == null)
            return new WitSqlResult();

        m_context.Database.DropTrigger(dropTrigger.TriggerName);
        return new WitSqlResult();
    }

    #endregion

    #region Helpers

    private static string SerializeTriggerBody(IReadOnlyList<WitSqlStatement> statements)
    {
        var parts = statements.Select(WitSqlStatementSerializer.Serialize);
        return string.Join("; ", parts);
    }

    private static TriggerTime MapTriggerTiming(TriggerTimingType timing)
    {
        return timing switch
        {
            TriggerTimingType.Before => TriggerTime.Before,
            TriggerTimingType.After => TriggerTime.After,
            TriggerTimingType.InsteadOf => TriggerTime.InsteadOf,
            _ => TriggerTime.After
        };
    }

    private static TriggerEvent MapTriggerEvent(TriggerEventType evt)
    {
        return evt switch
        {
            TriggerEventType.Insert => TriggerEvent.Insert,
            TriggerEventType.Update => TriggerEvent.Update,
            TriggerEventType.Delete => TriggerEvent.Delete,
            _ => TriggerEvent.Insert
        };
    }

    #endregion
}

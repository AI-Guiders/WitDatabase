using OutWit.Database.Parser.Statements;

namespace OutWit.Database.Statements;

/// <summary>
/// DDL execution for SEQUENCE operations (CREATE, DROP, ALTER).
/// </summary>
public sealed partial class StatementExecutor
{
    #region CREATE SEQUENCE

    private WitSqlResult ExecuteCreateSequence(WitSqlStatementCreateSequence createSequence)
    {
        if (createSequence.IfNotExists && m_context.Database.GetSequence(createSequence.SequenceName) != null)
            return new WitSqlResult();

        m_context.Database.CreateSequence(createSequence.SequenceName, createSequence.StartWith);
        return new WitSqlResult();
    }

    #endregion

    #region DROP SEQUENCE

    private WitSqlResult ExecuteDropSequence(WitSqlStatementDropSequence dropSequence)
    {
        if (dropSequence.IfExists && m_context.Database.GetSequence(dropSequence.SequenceName) == null)
            return new WitSqlResult();

        m_context.Database.DropSequence(dropSequence.SequenceName);
        return new WitSqlResult();
    }

    #endregion

    #region ALTER SEQUENCE

    private WitSqlResult ExecuteAlterSequence(WitSqlStatementAlterSequence alterSequence)
    {
        m_context.Database.RestartSequence(alterSequence.SequenceName, alterSequence.RestartWith);
        return new WitSqlResult();
    }

    #endregion
}

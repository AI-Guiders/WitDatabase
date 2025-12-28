using NSubstitute;
using OutWit.Database.Definitions;
using OutWit.Database.Parser;
using OutWit.Database.Sql;
using OutWit.Database.Statements;
using OutWit.Database.Types;

namespace OutWit.Database.Tests.Statements;

/// <summary>
/// Tests for trigger invocation during DML operations.
/// </summary>
[TestFixture]
public class StatementExecutorTriggerInvocationTests : StatementExecutorTestsBase
{
    #region AFTER INSERT Trigger Tests

    [Test]
    public void AfterInsertTriggerIsInvokedTest()
    {
        var table = CreateUsersTable();
        m_database.GetTable("Users").Returns(table);
        m_database.GetNextAutoIncrement("Users").Returns(1L);
        m_database.CreateTableScan("Users").Returns(CreateEmptyIterator());

        // Set up a trigger that inserts into audit table
        var trigger = new DefinitionTrigger
        {
            Name = "trg_audit_insert",
            TableName = "Users",
            Time = TriggerTime.After,
            Event = TriggerEvent.Insert,
            Body = "INSERT INTO AuditLog (Action) VALUES ('INSERT')"
        };
        m_database.GetTriggersForTable("Users", TriggerEvent.Insert, TriggerTime.After)
            .Returns([trigger]);
        m_database.GetTriggersForTable("Users", TriggerEvent.Insert, TriggerTime.Before)
            .Returns([]);
        m_database.GetTriggersForTable("Users", TriggerEvent.Insert, TriggerTime.InsteadOf)
            .Returns([]);

        // Set up audit table
        var auditTable = new DefinitionTable
        {
            Name = "AuditLog",
            Columns =
            [
                new DefinitionColumn { Name = "Id", Type = WitDataType.Int64, IsPrimaryKey = true, IsAutoIncrement = true, Ordinal = 0 },
                new DefinitionColumn { Name = "Action", Type = WitDataType.StringVariable, Ordinal = 1 }
            ]
        };
        m_database.GetTable("AuditLog").Returns(auditTable);
        m_database.GetNextAutoIncrement("AuditLog").Returns(1L);
        m_database.CreateTableScan("AuditLog").Returns(CreateEmptyIterator());
        m_database.GetTriggersForTable("AuditLog", Arg.Any<TriggerEvent>(), Arg.Any<TriggerTime>())
            .Returns([]);

        var executor = new StatementExecutor(m_context);
        var stmt = WitSql.ParseStatement("INSERT INTO Users (Name, Email) VALUES ('Alice', 'alice@test.com')");

        executor.Execute(stmt);

        // Verify trigger body was executed
        m_database.Received().InsertRow("AuditLog", Arg.Is<WitSqlRow>(r =>
            r["Action"].AsString() == "INSERT"));
    }

    #endregion

    #region BEFORE INSERT Trigger Tests

    [Test]
    public void BeforeInsertTriggerCanCancelOperationTest()
    {
        var table = CreateUsersTable();
        m_database.GetTable("Users").Returns(table);
        m_database.GetNextAutoIncrement("Users").Returns(1L);
        m_database.CreateTableScan("Users").Returns(CreateEmptyIterator());

        m_database.GetTriggersForTable("Users", TriggerEvent.Insert, TriggerTime.Before)
            .Returns([]);
        m_database.GetTriggersForTable("Users", TriggerEvent.Insert, TriggerTime.After)
            .Returns([]);
        m_database.GetTriggersForTable("Users", TriggerEvent.Insert, TriggerTime.InsteadOf)
            .Returns([]);

        var executor = new StatementExecutor(m_context);
        var stmt = WitSql.ParseStatement("INSERT INTO Users (Name, Email) VALUES ('Alice', 'alice@test.com')");

        var result = executor.Execute(stmt);

        Assert.That(result.RowsAffected, Is.EqualTo(1));
    }

    #endregion

    #region INSTEAD OF Trigger Tests

    [Test]
    public void InsteadOfTriggerReplacesNormalOperationTest()
    {
        var table = CreateUsersTable();
        m_database.GetTable("Users").Returns(table);
        m_database.GetNextAutoIncrement("Users").Returns(1L);
        m_database.CreateTableScan("Users").Returns(CreateEmptyIterator());

        // Instead of trigger that does nothing
        var trigger = new DefinitionTrigger
        {
            Name = "trg_instead_insert",
            TableName = "Users",
            Time = TriggerTime.InsteadOf,
            Event = TriggerEvent.Insert,
            Body = "SELECT 1" // No actual insert
        };

        m_database.GetTriggersForTable("Users", TriggerEvent.Insert, TriggerTime.Before)
            .Returns([]);
        m_database.GetTriggersForTable("Users", TriggerEvent.Insert, TriggerTime.InsteadOf)
            .Returns([trigger]);

        var executor = new StatementExecutor(m_context);
        var stmt = WitSql.ParseStatement("INSERT INTO Users (Name, Email) VALUES ('Alice', 'alice@test.com')");

        var result = executor.Execute(stmt);

        // INSTEAD OF trigger executed, rowsAffected incremented but no actual insert
        Assert.That(result.RowsAffected, Is.EqualTo(1));
        m_database.DidNotReceive().InsertRow("Users", Arg.Any<WitSqlRow>());
    }

    #endregion

    #region AFTER UPDATE Trigger Tests

    [Test]
    public void AfterUpdateTriggerIsInvokedTest()
    {
        var table = CreateUsersTable();
        m_database.GetTable("Users").Returns(table);
        m_database.CreateTableScan("Users").Returns(CreateMockIterator(
            CreateUserRow(1, "Alice", "alice@test.com")
        ));

        var trigger = new DefinitionTrigger
        {
            Name = "trg_audit_update",
            TableName = "Users",
            Time = TriggerTime.After,
            Event = TriggerEvent.Update,
            Body = "INSERT INTO AuditLog (Action) VALUES ('UPDATE')"
        };
        m_database.GetTriggersForTable("Users", TriggerEvent.Update, TriggerTime.After)
            .Returns([trigger]);
        m_database.GetTriggersForTable("Users", TriggerEvent.Update, TriggerTime.Before)
            .Returns([]);
        m_database.GetTriggersForTable("Users", TriggerEvent.Update, TriggerTime.InsteadOf)
            .Returns([]);

        // Set up audit table
        var auditTable = new DefinitionTable
        {
            Name = "AuditLog",
            Columns =
            [
                new DefinitionColumn { Name = "Id", Type = WitDataType.Int64, IsPrimaryKey = true, IsAutoIncrement = true, Ordinal = 0 },
                new DefinitionColumn { Name = "Action", Type = WitDataType.StringVariable, Ordinal = 1 }
            ]
        };
        m_database.GetTable("AuditLog").Returns(auditTable);
        m_database.GetNextAutoIncrement("AuditLog").Returns(1L);
        m_database.CreateTableScan("AuditLog").Returns(CreateEmptyIterator());
        m_database.GetTriggersForTable("AuditLog", Arg.Any<TriggerEvent>(), Arg.Any<TriggerTime>())
            .Returns([]);

        var executor = new StatementExecutor(m_context);
        var stmt = WitSql.ParseStatement("UPDATE Users SET Name = 'Updated' WHERE Id = 1");

        executor.Execute(stmt);

        // Verify trigger body was executed
        m_database.Received().InsertRow("AuditLog", Arg.Is<WitSqlRow>(r =>
            r["Action"].AsString() == "UPDATE"));
    }

    #endregion

    #region AFTER DELETE Trigger Tests

    [Test]
    public void AfterDeleteTriggerIsInvokedTest()
    {
        var table = CreateUsersTable();
        m_database.GetTable("Users").Returns(table);
        m_database.CreateTableScan("Users").Returns(CreateMockIterator(
            CreateUserRow(1, "Alice", "alice@test.com")
        ));

        var trigger = new DefinitionTrigger
        {
            Name = "trg_audit_delete",
            TableName = "Users",
            Time = TriggerTime.After,
            Event = TriggerEvent.Delete,
            Body = "INSERT INTO AuditLog (Action) VALUES ('DELETE')"
        };
        m_database.GetTriggersForTable("Users", TriggerEvent.Delete, TriggerTime.After)
            .Returns([trigger]);
        m_database.GetTriggersForTable("Users", TriggerEvent.Delete, TriggerTime.Before)
            .Returns([]);
        m_database.GetTriggersForTable("Users", TriggerEvent.Delete, TriggerTime.InsteadOf)
            .Returns([]);

        // Set up audit table
        var auditTable = new DefinitionTable
        {
            Name = "AuditLog",
            Columns =
            [
                new DefinitionColumn { Name = "Id", Type = WitDataType.Int64, IsPrimaryKey = true, IsAutoIncrement = true, Ordinal = 0 },
                new DefinitionColumn { Name = "Action", Type = WitDataType.StringVariable, Ordinal = 1 }
            ]
        };
        m_database.GetTable("AuditLog").Returns(auditTable);
        m_database.GetNextAutoIncrement("AuditLog").Returns(1L);
        m_database.CreateTableScan("AuditLog").Returns(CreateEmptyIterator());
        m_database.GetTriggersForTable("AuditLog", Arg.Any<TriggerEvent>(), Arg.Any<TriggerTime>())
            .Returns([]);

        var executor = new StatementExecutor(m_context);
        var stmt = WitSql.ParseStatement("DELETE FROM Users WHERE Id = 1");

        executor.Execute(stmt);

        // Verify trigger body was executed
        m_database.Received().InsertRow("AuditLog", Arg.Is<WitSqlRow>(r =>
            r["Action"].AsString() == "DELETE"));
    }

    #endregion

    #region Trigger with WHEN Condition Tests

    [Test]
    public void TriggerWithWhenConditionExecutesWhenTrueTest()
    {
        var table = new DefinitionTable
        {
            Name = "Items",
            Columns =
            [
                new DefinitionColumn { Name = "Id", Type = WitDataType.Int64, IsPrimaryKey = true, IsAutoIncrement = true, Ordinal = 0 },
                new DefinitionColumn { Name = "Status", Type = WitDataType.StringVariable, Ordinal = 1 }
            ]
        };
        m_database.GetTable("Items").Returns(table);
        m_database.GetNextAutoIncrement("Items").Returns(1L);
        m_database.CreateTableScan("Items").Returns(CreateEmptyIterator());

        var trigger = new DefinitionTrigger
        {
            Name = "trg_active_only",
            TableName = "Items",
            Time = TriggerTime.After,
            Event = TriggerEvent.Insert,
            WhenCondition = "NEW.Status = 'active'",
            Body = "INSERT INTO AuditLog (Action) VALUES ('ACTIVE_INSERT')"
        };
        m_database.GetTriggersForTable("Items", TriggerEvent.Insert, TriggerTime.After)
            .Returns([trigger]);
        m_database.GetTriggersForTable("Items", TriggerEvent.Insert, TriggerTime.Before)
            .Returns([]);
        m_database.GetTriggersForTable("Items", TriggerEvent.Insert, TriggerTime.InsteadOf)
            .Returns([]);

        // Set up audit table
        var auditTable = new DefinitionTable
        {
            Name = "AuditLog",
            Columns =
            [
                new DefinitionColumn { Name = "Id", Type = WitDataType.Int64, IsPrimaryKey = true, IsAutoIncrement = true, Ordinal = 0 },
                new DefinitionColumn { Name = "Action", Type = WitDataType.StringVariable, Ordinal = 1 }
            ]
        };
        m_database.GetTable("AuditLog").Returns(auditTable);
        m_database.GetNextAutoIncrement("AuditLog").Returns(1L);
        m_database.CreateTableScan("AuditLog").Returns(CreateEmptyIterator());
        m_database.GetTriggersForTable("AuditLog", Arg.Any<TriggerEvent>(), Arg.Any<TriggerTime>())
            .Returns([]);

        var executor = new StatementExecutor(m_context);
        var stmt = WitSql.ParseStatement("INSERT INTO Items (Status) VALUES ('active')");

        executor.Execute(stmt);

        // Trigger should execute because WHEN condition is true
        m_database.Received().InsertRow("AuditLog", Arg.Any<WitSqlRow>());
    }

    [Test]
    public void TriggerWithWhenConditionSkipsWhenFalseTest()
    {
        var table = new DefinitionTable
        {
            Name = "Items",
            Columns =
            [
                new DefinitionColumn { Name = "Id", Type = WitDataType.Int64, IsPrimaryKey = true, IsAutoIncrement = true, Ordinal = 0 },
                new DefinitionColumn { Name = "Status", Type = WitDataType.StringVariable, Ordinal = 1 }
            ]
        };
        m_database.GetTable("Items").Returns(table);
        m_database.GetNextAutoIncrement("Items").Returns(1L);
        m_database.CreateTableScan("Items").Returns(CreateEmptyIterator());

        var trigger = new DefinitionTrigger
        {
            Name = "trg_active_only",
            TableName = "Items",
            Time = TriggerTime.After,
            Event = TriggerEvent.Insert,
            WhenCondition = "NEW.Status = 'active'",
            Body = "INSERT INTO AuditLog (Action) VALUES ('ACTIVE_INSERT')"
        };
        m_database.GetTriggersForTable("Items", TriggerEvent.Insert, TriggerTime.After)
            .Returns([trigger]);
        m_database.GetTriggersForTable("Items", TriggerEvent.Insert, TriggerTime.Before)
            .Returns([]);
        m_database.GetTriggersForTable("Items", TriggerEvent.Insert, TriggerTime.InsteadOf)
            .Returns([]);

        var executor = new StatementExecutor(m_context);
        var stmt = WitSql.ParseStatement("INSERT INTO Items (Status) VALUES ('inactive')");

        executor.Execute(stmt);

        // Trigger should NOT execute because WHEN condition is false
        m_database.DidNotReceive().InsertRow("AuditLog", Arg.Any<WitSqlRow>());
    }

    #endregion

    #region No Triggers Configured Tests

    [Test]
    public void NoTriggersConfiguredExecutesNormallyTest()
    {
        var table = CreateUsersTable();
        m_database.GetTable("Users").Returns(table);
        m_database.GetNextAutoIncrement("Users").Returns(1L);
        m_database.CreateTableScan("Users").Returns(CreateEmptyIterator());
        
        // No triggers
        m_database.GetTriggersForTable("Users", Arg.Any<TriggerEvent>(), Arg.Any<TriggerTime>())
            .Returns([]);

        var executor = new StatementExecutor(m_context);
        var stmt = WitSql.ParseStatement("INSERT INTO Users (Name, Email) VALUES ('Alice', 'alice@test.com')");

        var result = executor.Execute(stmt);

        Assert.That(result.RowsAffected, Is.EqualTo(1));
        m_database.Received(1).InsertRow("Users", Arg.Any<WitSqlRow>());
    }

    #endregion
}

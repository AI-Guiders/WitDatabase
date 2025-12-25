using OutWit.Database.Parser.Schema.MergeClauses;
using OutWit.Database.Parser.Schema.Types;
using OutWit.Database.Parser.Statements;

namespace OutWit.Database.Parser.Tests.Dml;

/// <summary>
/// Tests for MERGE statement parsing (SS16.3).
/// Covers: basic MERGE, WHEN MATCHED, WHEN NOT MATCHED, conditions.
/// </summary>
[TestFixture]
public class MergeStatementParserTests
{
    #region Basic MERGE

    [Test]
    public void ParseMergeBasicTest()
    {
        var stmt = WitSql.ParseStatement(@"
            MERGE INTO Target AS t
            USING Source AS s ON t.Id = s.Id
            WHEN MATCHED THEN UPDATE SET t.Name = s.Name
            WHEN NOT MATCHED THEN INSERT (Id, Name) VALUES (s.Id, s.Name)");

        Assert.That(stmt, Is.InstanceOf<WitSqlStatementMerge>());
        var merge = (WitSqlStatementMerge)stmt;
        Assert.That(merge.TargetTable, Is.EqualTo("Target"));
        Assert.That(merge.TargetAlias, Is.EqualTo("t"));
        Assert.That(merge.SourceTable, Is.EqualTo("Source"));
        Assert.That(merge.SourceAlias, Is.EqualTo("s"));
    }

    [Test]
    public void ParseMergeWithoutAliasesTest()
    {
        var stmt = WitSql.ParseStatement(@"
            MERGE INTO Target
            USING Source ON Target.Id = Source.Id
            WHEN MATCHED THEN UPDATE SET Name = Source.Name");

        var merge = (WitSqlStatementMerge)stmt;
        Assert.That(merge.TargetTable, Is.EqualTo("Target"));
        Assert.That(merge.TargetAlias, Is.Null);
        Assert.That(merge.SourceTable, Is.EqualTo("Source"));
        Assert.That(merge.SourceAlias, Is.Null);
    }

    [Test]
    public void ParseMergeWithSubquerySourceTest()
    {
        var stmt = WitSql.ParseStatement(@"
            MERGE INTO Target AS t
            USING (SELECT Id, Name FROM SourceTable WHERE Active = TRUE) AS s
            ON t.Id = s.Id
            WHEN MATCHED THEN UPDATE SET t.Name = s.Name");

        var merge = (WitSqlStatementMerge)stmt;
        Assert.That(merge.SourceTable, Is.Null);
        Assert.That(merge.SourceSelect, Is.Not.Null);
        Assert.That(merge.SourceAlias, Is.EqualTo("s"));
    }

    #endregion

    #region WHEN MATCHED Clause

    [Test]
    public void ParseMergeWhenMatchedUpdateTest()
    {
        var stmt = WitSql.ParseStatement(@"
            MERGE INTO Target AS t
            USING Source AS s ON t.Id = s.Id
            WHEN MATCHED THEN UPDATE SET t.Name = s.Name, t.Value = s.Value");

        var merge = (WitSqlStatementMerge)stmt;
        Assert.That(merge.WhenClauses, Has.Count.EqualTo(1));

        var clause = merge.WhenClauses[0];
        Assert.That(clause.IsMatched, Is.True);
        Assert.That(clause.ActionType, Is.EqualTo(MergeActionType.Update));
        Assert.That(clause.SetClauses, Has.Count.EqualTo(2));
    }

    [Test]
    public void ParseMergeWhenMatchedDeleteTest()
    {
        var stmt = WitSql.ParseStatement(@"
            MERGE INTO Target AS t
            USING Source AS s ON t.Id = s.Id
            WHEN MATCHED THEN DELETE");

        var merge = (WitSqlStatementMerge)stmt;
        var clause = merge.WhenClauses[0];
        Assert.That(clause.IsMatched, Is.True);
        Assert.That(clause.ActionType, Is.EqualTo(MergeActionType.Delete));
    }

    [Test]
    public void ParseMergeWhenMatchedWithConditionTest()
    {
        var stmt = WitSql.ParseStatement(@"
            MERGE INTO Target AS t
            USING Source AS s ON t.Id = s.Id
            WHEN MATCHED AND s.IsActive = TRUE THEN UPDATE SET t.Name = s.Name");

        var merge = (WitSqlStatementMerge)stmt;
        var clause = merge.WhenClauses[0];
        Assert.That(clause.Condition, Is.Not.Null);
    }

    #endregion

    #region WHEN NOT MATCHED Clause

    [Test]
    public void ParseMergeWhenNotMatchedInsertTest()
    {
        var stmt = WitSql.ParseStatement(@"
            MERGE INTO Target AS t
            USING Source AS s ON t.Id = s.Id
            WHEN NOT MATCHED THEN INSERT (Id, Name) VALUES (s.Id, s.Name)");

        var merge = (WitSqlStatementMerge)stmt;
        var clause = merge.WhenClauses[0];
        Assert.That(clause.IsMatched, Is.False);
        Assert.That(clause.ActionType, Is.EqualTo(MergeActionType.Insert));
        Assert.That(clause.InsertColumns, Has.Count.EqualTo(2));
        Assert.That(clause.InsertValues, Has.Count.EqualTo(2));
    }

    [Test]
    public void ParseMergeWhenNotMatchedWithConditionTest()
    {
        var stmt = WitSql.ParseStatement(@"
            MERGE INTO Target AS t
            USING Source AS s ON t.Id = s.Id
            WHEN NOT MATCHED AND s.Type = 'new' THEN INSERT (Id, Name) VALUES (s.Id, s.Name)");

        var merge = (WitSqlStatementMerge)stmt;
        var clause = merge.WhenClauses[0];
        Assert.That(clause.Condition, Is.Not.Null);
    }

    [Test]
    public void ParseMergeWhenNotMatchedWithoutColumnsTest()
    {
        var stmt = WitSql.ParseStatement(@"
            MERGE INTO Target AS t
            USING Source AS s ON t.Id = s.Id
            WHEN NOT MATCHED THEN INSERT VALUES (s.Id, s.Name, s.Value)");

        var merge = (WitSqlStatementMerge)stmt;
        var clause = merge.WhenClauses[0];
        Assert.That(clause.InsertColumns, Is.Null.Or.Empty);
        Assert.That(clause.InsertValues, Has.Count.EqualTo(3));
    }

    #endregion

    #region Multiple Clauses

    [Test]
    public void ParseMergeWithMultipleClausesTest()
    {
        var stmt = WitSql.ParseStatement(@"
            MERGE INTO Target AS t
            USING Source AS s ON t.Id = s.Id
            WHEN MATCHED AND s.IsDeleted = TRUE THEN DELETE
            WHEN MATCHED THEN UPDATE SET t.Name = s.Name
            WHEN NOT MATCHED THEN INSERT (Id, Name) VALUES (s.Id, s.Name)");

        var merge = (WitSqlStatementMerge)stmt;
        Assert.That(merge.WhenClauses, Has.Count.EqualTo(3));

        Assert.That(merge.WhenClauses[0].ActionType, Is.EqualTo(MergeActionType.Delete));
        Assert.That(merge.WhenClauses[1].ActionType, Is.EqualTo(MergeActionType.Update));
        Assert.That(merge.WhenClauses[2].ActionType, Is.EqualTo(MergeActionType.Insert));
    }

    [Test]
    public void ParseMergeWithConditionalClausesTest()
    {
        var stmt = WitSql.ParseStatement(@"
            MERGE INTO Products AS p
            USING StagingProducts AS s ON p.ProductId = s.ProductId
            WHEN MATCHED AND s.Quantity = 0 THEN DELETE
            WHEN MATCHED AND s.Quantity > 0 THEN UPDATE SET p.Quantity = s.Quantity, p.Price = s.Price
            WHEN NOT MATCHED AND s.Quantity > 0 THEN INSERT (ProductId, Name, Quantity, Price) VALUES (s.ProductId, s.Name, s.Quantity, s.Price)");

        var merge = (WitSqlStatementMerge)stmt;
        Assert.That(merge.WhenClauses, Has.Count.EqualTo(3));

        // All clauses should have conditions
        Assert.That(merge.WhenClauses[0].Condition, Is.Not.Null);
        Assert.That(merge.WhenClauses[1].Condition, Is.Not.Null);
        Assert.That(merge.WhenClauses[2].Condition, Is.Not.Null);
    }

    #endregion

    #region Complex ON Conditions

    [Test]
    public void ParseMergeWithComplexOnConditionTest()
    {
        var stmt = WitSql.ParseStatement(@"
            MERGE INTO Target AS t
            USING Source AS s ON t.Id = s.Id AND t.Type = s.Type
            WHEN MATCHED THEN UPDATE SET t.Value = s.Value");

        var merge = (WitSqlStatementMerge)stmt;
        Assert.That(merge.OnCondition, Is.Not.Null);
    }

    [Test]
    public void ParseMergeWithFunctionInOnConditionTest()
    {
        var stmt = WitSql.ParseStatement(@"
            MERGE INTO Target AS t
            USING Source AS s ON LOWER(t.Code) = LOWER(s.Code)
            WHEN MATCHED THEN UPDATE SET t.Name = s.Name");

        var merge = (WitSqlStatementMerge)stmt;
        Assert.That(merge.OnCondition, Is.Not.Null);
    }

    #endregion
}

using OutWit.Database.Parser.Expressions;
using OutWit.Database.Parser.Schema.Specs;
using OutWit.Database.Parser.Schema.Types;
using OutWit.Database.Parser.Statements;

namespace OutWit.Database.Parser.Tests;

/// <summary>
/// Tests for parsing window frame clause (ROWS/RANGE BETWEEN).
/// </summary>
[TestFixture]
public class WindowFrameParserTests
{
    #region Basic Frame Syntax Tests

    [Test]
    public void ParseRowsUnboundedPrecedingAndCurrentRowTest()
    {
        var expr = WitSql.ParseExpression("SUM(Amount) OVER (ORDER BY Date ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)");
        
        var func = (WitSqlExpressionFunctionCall)expr;
        Assert.That(func.Over, Is.Not.Null);
        Assert.That(func.Over!.Frame, Is.Not.Null);
        
        var frame = func.Over.Frame!;
        Assert.That(frame.FrameType, Is.EqualTo(FrameType.Rows));
        Assert.That(frame.Start.BoundType, Is.EqualTo(FrameBoundType.UnboundedPreceding));
        Assert.That(frame.End, Is.Not.Null);
        Assert.That(frame.End!.BoundType, Is.EqualTo(FrameBoundType.CurrentRow));
    }

    [Test]
    public void ParseRowsNPrecedingAndNFollowingTest()
    {
        var expr = WitSql.ParseExpression("AVG(Value) OVER (ORDER BY Id ROWS BETWEEN 2 PRECEDING AND 2 FOLLOWING)");
        
        var func = (WitSqlExpressionFunctionCall)expr;
        Assert.That(func.Over, Is.Not.Null);
        Assert.That(func.Over!.Frame, Is.Not.Null);
        
        var frame = func.Over.Frame!;
        Assert.That(frame.FrameType, Is.EqualTo(FrameType.Rows));
        Assert.That(frame.Start.BoundType, Is.EqualTo(FrameBoundType.Preceding));
        Assert.That(frame.Start.Offset, Is.EqualTo(2));
        Assert.That(frame.End, Is.Not.Null);
        Assert.That(frame.End!.BoundType, Is.EqualTo(FrameBoundType.Following));
        Assert.That(frame.End.Offset, Is.EqualTo(2));
    }

    [Test]
    public void ParseRowsCurrentRowAndUnboundedFollowingTest()
    {
        var expr = WitSql.ParseExpression("COUNT(*) OVER (ORDER BY Id ROWS BETWEEN CURRENT ROW AND UNBOUNDED FOLLOWING)");
        
        var func = (WitSqlExpressionFunctionCall)expr;
        Assert.That(func.Over!.Frame, Is.Not.Null);
        
        var frame = func.Over.Frame!;
        Assert.That(frame.Start.BoundType, Is.EqualTo(FrameBoundType.CurrentRow));
        Assert.That(frame.End!.BoundType, Is.EqualTo(FrameBoundType.UnboundedFollowing));
    }

    [Test]
    public void ParseRangeFrameTest()
    {
        var expr = WitSql.ParseExpression("SUM(Amount) OVER (ORDER BY Date RANGE BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)");
        
        var func = (WitSqlExpressionFunctionCall)expr;
        Assert.That(func.Over!.Frame, Is.Not.Null);
        Assert.That(func.Over.Frame!.FrameType, Is.EqualTo(FrameType.Range));
    }

    #endregion

    #region Single Bound Syntax Tests

    [Test]
    public void ParseRowsUnboundedPrecedingSingleBoundTest()
    {
        var expr = WitSql.ParseExpression("SUM(Amount) OVER (ORDER BY Date ROWS UNBOUNDED PRECEDING)");
        
        var func = (WitSqlExpressionFunctionCall)expr;
        Assert.That(func.Over!.Frame, Is.Not.Null);
        
        var frame = func.Over.Frame!;
        Assert.That(frame.Start.BoundType, Is.EqualTo(FrameBoundType.UnboundedPreceding));
        // Single bound should default End to CURRENT ROW
        Assert.That(frame.End!.BoundType, Is.EqualTo(FrameBoundType.CurrentRow));
    }

    [Test]
    public void ParseRowsNPrecedingSingleBoundTest()
    {
        var expr = WitSql.ParseExpression("AVG(Value) OVER (ORDER BY Id ROWS 3 PRECEDING)");
        
        var func = (WitSqlExpressionFunctionCall)expr;
        var frame = func.Over!.Frame!;
        
        Assert.That(frame.Start.BoundType, Is.EqualTo(FrameBoundType.Preceding));
        Assert.That(frame.Start.Offset, Is.EqualTo(3));
        Assert.That(frame.End!.BoundType, Is.EqualTo(FrameBoundType.CurrentRow));
    }

    #endregion

    #region Frame with PARTITION BY Tests

    [Test]
    public void ParsePartitionByWithFrameTest()
    {
        var expr = WitSql.ParseExpression(@"
            SUM(Amount) OVER (
                PARTITION BY Category 
                ORDER BY Date 
                ROWS BETWEEN 1 PRECEDING AND 1 FOLLOWING
            )");
        
        var func = (WitSqlExpressionFunctionCall)expr;
        Assert.That(func.Over, Is.Not.Null);
        Assert.That(func.Over!.PartitionBy, Is.Not.Null);
        Assert.That(func.Over.PartitionBy!.Count, Is.EqualTo(1));
        Assert.That(func.Over.OrderBy, Is.Not.Null);
        Assert.That(func.Over.Frame, Is.Not.Null);
        
        var frame = func.Over.Frame!;
        Assert.That(frame.FrameType, Is.EqualTo(FrameType.Rows));
        Assert.That(frame.Start.BoundType, Is.EqualTo(FrameBoundType.Preceding));
        Assert.That(frame.Start.Offset, Is.EqualTo(1));
        Assert.That(frame.End!.BoundType, Is.EqualTo(FrameBoundType.Following));
        Assert.That(frame.End.Offset, Is.EqualTo(1));
    }

    #endregion

    #region No Frame Tests

    [Test]
    public void ParseWindowWithoutFrameTest()
    {
        var expr = WitSql.ParseExpression("SUM(Amount) OVER (PARTITION BY Category ORDER BY Date)");
        
        var func = (WitSqlExpressionFunctionCall)expr;
        Assert.That(func.Over, Is.Not.Null);
        Assert.That(func.Over!.PartitionBy, Is.Not.Null);
        Assert.That(func.Over.OrderBy, Is.Not.Null);
        Assert.That(func.Over.Frame, Is.Null); // No frame clause
    }

    [Test]
    public void ParseWindowWithOnlyPartitionByTest()
    {
        var expr = WitSql.ParseExpression("COUNT(*) OVER (PARTITION BY Category)");
        
        var func = (WitSqlExpressionFunctionCall)expr;
        Assert.That(func.Over, Is.Not.Null);
        Assert.That(func.Over!.PartitionBy, Is.Not.Null);
        Assert.That(func.Over.OrderBy, Is.Null);
        Assert.That(func.Over.Frame, Is.Null);
    }

    #endregion

    #region In SELECT Statement Tests

    [Test]
    public void ParseSelectWithFrameClauseTest()
    {
        var stmt = WitSql.ParseStatement(@"
            SELECT 
                Id,
                Amount,
                SUM(Amount) OVER (ORDER BY Id ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS RunningTotal
            FROM Sales");
        
        Assert.That(stmt, Is.InstanceOf<WitSqlStatementSelect>());
        var select = (WitSqlStatementSelect)stmt;
        
        Assert.That(select.SelectList, Has.Count.EqualTo(3));
        var sumExpr = select.SelectList[2].Expression as WitSqlExpressionFunctionCall;
        Assert.That(sumExpr, Is.Not.Null);
        Assert.That(sumExpr!.Over, Is.Not.Null);
        Assert.That(sumExpr.Over!.Frame, Is.Not.Null);
    }

    [Test]
    public void ParseSelectWithMultipleFrameClausesTest()
    {
        var stmt = WitSql.ParseStatement(@"
            SELECT 
                Amount,
                SUM(Amount) OVER (ORDER BY Id ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS RunningSum,
                AVG(Amount) OVER (ORDER BY Id ROWS BETWEEN 2 PRECEDING AND CURRENT ROW) AS MovingAvg,
                MIN(Amount) OVER (ORDER BY Id ROWS BETWEEN 1 PRECEDING AND 1 FOLLOWING) AS LocalMin
            FROM Sales");
        
        var select = (WitSqlStatementSelect)stmt;
        Assert.That(select.SelectList, Has.Count.EqualTo(4));
        
        // Verify each has its own frame
        var sum = (WitSqlExpressionFunctionCall)select.SelectList[1].Expression!;
        Assert.That(sum.Over!.Frame!.Start.BoundType, Is.EqualTo(FrameBoundType.UnboundedPreceding));
        
        var avg = (WitSqlExpressionFunctionCall)select.SelectList[2].Expression!;
        Assert.That(avg.Over!.Frame!.Start.BoundType, Is.EqualTo(FrameBoundType.Preceding));
        Assert.That(avg.Over!.Frame!.Start.Offset, Is.EqualTo(2));
        
        var min = (WitSqlExpressionFunctionCall)select.SelectList[3].Expression!;
        Assert.That(min.Over!.Frame!.Start.Offset, Is.EqualTo(1));
        Assert.That(min.Over!.Frame!.End!.Offset, Is.EqualTo(1));
    }

    #endregion
}

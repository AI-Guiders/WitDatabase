using OutWit.Database.Iterators;
using OutWit.Database.Parser.Expressions;
using OutWit.Database.Parser.Schema.Clauses;
using OutWit.Database.Parser.Schema.Types;
using OutWit.Database.Types;
using OutWit.Database.Values;

namespace OutWit.Database.Tests.Iterators;

[TestFixture]
public class IteratorGroupByTests : IteratorTestsBase
{
    #region COUNT Tests

    [Test]
    public void GroupByCountAllRowsTest()
    {
        var source = CreateMockIterator(
            CreateRowWithInts(("Id", 1)),
            CreateRowWithInts(("Id", 2)),
            CreateRowWithInts(("Id", 3))
        );

        var selectList = new List<ClauseSelectItem>
        {
            new()
            {
                Expression = new WitSqlExpressionFunctionCall { FunctionName = "COUNT", IsStar = true },
                Alias = "Total"
            }
        };

        var iterator = new IteratorGroupBy(source, null, selectList, m_context);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0]["Total"].AsInt64(), Is.EqualTo(3));
    }

    [Test]
    public void GroupByCountByGroupTest()
    {
        var source = CreateMockIterator(
            CreateRow(("Category", WitSqlValue.FromText("A")), ("Value", WitSqlValue.FromInt(1))),
            CreateRow(("Category", WitSqlValue.FromText("A")), ("Value", WitSqlValue.FromInt(2))),
            CreateRow(("Category", WitSqlValue.FromText("B")), ("Value", WitSqlValue.FromInt(3)))
        );

        var groupBy = new List<WitSqlExpression>
        {
            new WitSqlExpressionColumnRef { ColumnName = "Category" }
        };

        var selectList = new List<ClauseSelectItem>
        {
            new() { Expression = new WitSqlExpressionColumnRef { ColumnName = "Category" } },
            new()
            {
                Expression = new WitSqlExpressionFunctionCall { FunctionName = "COUNT", IsStar = true },
                Alias = "Count"
            }
        };

        var iterator = new IteratorGroupBy(source, groupBy, selectList, m_context);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Has.Count.EqualTo(2));
        
        var categoryA = rows.FirstOrDefault(r => r["Category"].AsString() == "A");
        var categoryB = rows.FirstOrDefault(r => r["Category"].AsString() == "B");
        
        Assert.That(categoryA["Count"].AsInt64(), Is.EqualTo(2));
        Assert.That(categoryB["Count"].AsInt64(), Is.EqualTo(1));
    }

    [Test]
    public void GroupByCountDistinctTest()
    {
        var source = CreateMockIterator(
            CreateRow(("Value", WitSqlValue.FromInt(1))),
            CreateRow(("Value", WitSqlValue.FromInt(1))),
            CreateRow(("Value", WitSqlValue.FromInt(2))),
            CreateRow(("Value", WitSqlValue.FromInt(2))),
            CreateRow(("Value", WitSqlValue.FromInt(3)))
        );

        var selectList = new List<ClauseSelectItem>
        {
            new()
            {
                Expression = new WitSqlExpressionFunctionCall
                {
                    FunctionName = "COUNT",
                    IsDistinct = true,
                    Arguments = [new WitSqlExpressionColumnRef { ColumnName = "Value" }]
                },
                Alias = "DistinctCount"
            }
        };

        var iterator = new IteratorGroupBy(source, null, selectList, m_context);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0]["DistinctCount"].AsInt64(), Is.EqualTo(3));
    }

    #endregion

    #region SUM Tests

    [Test]
    public void GroupBySumTest()
    {
        var source = CreateMockIterator(
            CreateRowWithInts(("Value", 10)),
            CreateRowWithInts(("Value", 20)),
            CreateRowWithInts(("Value", 30))
        );

        var selectList = new List<ClauseSelectItem>
        {
            new()
            {
                Expression = new WitSqlExpressionFunctionCall
                {
                    FunctionName = "SUM",
                    Arguments = [new WitSqlExpressionColumnRef { ColumnName = "Value" }]
                },
                Alias = "Total"
            }
        };

        var iterator = new IteratorGroupBy(source, null, selectList, m_context);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0]["Total"].AsInt64(), Is.EqualTo(60));
    }

    [Test]
    public void GroupBySumByGroupTest()
    {
        var source = CreateMockIterator(
            CreateRow(("Category", WitSqlValue.FromText("A")), ("Value", WitSqlValue.FromInt(10))),
            CreateRow(("Category", WitSqlValue.FromText("A")), ("Value", WitSqlValue.FromInt(20))),
            CreateRow(("Category", WitSqlValue.FromText("B")), ("Value", WitSqlValue.FromInt(100)))
        );

        var groupBy = new List<WitSqlExpression>
        {
            new WitSqlExpressionColumnRef { ColumnName = "Category" }
        };

        var selectList = new List<ClauseSelectItem>
        {
            new() { Expression = new WitSqlExpressionColumnRef { ColumnName = "Category" } },
            new()
            {
                Expression = new WitSqlExpressionFunctionCall
                {
                    FunctionName = "SUM",
                    Arguments = [new WitSqlExpressionColumnRef { ColumnName = "Value" }]
                },
                Alias = "Sum"
            }
        };

        var iterator = new IteratorGroupBy(source, groupBy, selectList, m_context);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Has.Count.EqualTo(2));
        
        var categoryA = rows.FirstOrDefault(r => r["Category"].AsString() == "A");
        var categoryB = rows.FirstOrDefault(r => r["Category"].AsString() == "B");
        
        Assert.That(categoryA["Sum"].AsInt64(), Is.EqualTo(30));
        Assert.That(categoryB["Sum"].AsInt64(), Is.EqualTo(100));
    }

    #endregion

    #region AVG Tests

    [Test]
    public void GroupByAvgTest()
    {
        var source = CreateMockIterator(
            CreateRowWithInts(("Value", 10)),
            CreateRowWithInts(("Value", 20)),
            CreateRowWithInts(("Value", 30))
        );

        var selectList = new List<ClauseSelectItem>
        {
            new()
            {
                Expression = new WitSqlExpressionFunctionCall
                {
                    FunctionName = "AVG",
                    Arguments = [new WitSqlExpressionColumnRef { ColumnName = "Value" }]
                },
                Alias = "Average"
            }
        };

        var iterator = new IteratorGroupBy(source, null, selectList, m_context);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0]["Average"].AsDouble(), Is.EqualTo(20.0));
    }

    #endregion

    #region MIN/MAX Tests

    [Test]
    public void GroupByMinMaxTest()
    {
        var source = CreateMockIterator(
            CreateRowWithInts(("Value", 30)),
            CreateRowWithInts(("Value", 10)),
            CreateRowWithInts(("Value", 20))
        );

        var selectList = new List<ClauseSelectItem>
        {
            new()
            {
                Expression = new WitSqlExpressionFunctionCall
                {
                    FunctionName = "MIN",
                    Arguments = [new WitSqlExpressionColumnRef { ColumnName = "Value" }]
                },
                Alias = "MinValue"
            },
            new()
            {
                Expression = new WitSqlExpressionFunctionCall
                {
                    FunctionName = "MAX",
                    Arguments = [new WitSqlExpressionColumnRef { ColumnName = "Value" }]
                },
                Alias = "MaxValue"
            }
        };

        var iterator = new IteratorGroupBy(source, null, selectList, m_context);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0]["MinValue"].AsInt64(), Is.EqualTo(10));
        Assert.That(rows[0]["MaxValue"].AsInt64(), Is.EqualTo(30));
    }

    #endregion

    #region GROUP_CONCAT Tests

    [Test]
    public void GroupByGroupConcatTest()
    {
        var source = CreateMockIterator(
            CreateRow(("Name", WitSqlValue.FromText("Alice"))),
            CreateRow(("Name", WitSqlValue.FromText("Bob"))),
            CreateRow(("Name", WitSqlValue.FromText("Charlie")))
        );

        var selectList = new List<ClauseSelectItem>
        {
            new()
            {
                Expression = new WitSqlExpressionFunctionCall
                {
                    FunctionName = "GROUP_CONCAT",
                    Arguments = [new WitSqlExpressionColumnRef { ColumnName = "Name" }]
                },
                Alias = "Names"
            }
        };

        var iterator = new IteratorGroupBy(source, null, selectList, m_context);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Has.Count.EqualTo(1));
        var names = rows[0]["Names"].AsString();
        Assert.That(names, Does.Contain("Alice"));
        Assert.That(names, Does.Contain("Bob"));
        Assert.That(names, Does.Contain("Charlie"));
    }

    #endregion

    #region Edge Cases

    [Test]
    public void GroupByEmptySourceReturnsOneRowForAggregatesTest()
    {
        var source = CreateMockIterator();

        var selectList = new List<ClauseSelectItem>
        {
            new()
            {
                Expression = new WitSqlExpressionFunctionCall { FunctionName = "COUNT", IsStar = true },
                Alias = "Count"
            }
        };

        var iterator = new IteratorGroupBy(source, null, selectList, m_context);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0]["Count"].AsInt64(), Is.EqualTo(0));
    }

    [Test]
    public void GroupByWithNullValuesTest()
    {
        var source = CreateMockIterator(
            CreateRow(("Value", WitSqlValue.FromInt(10))),
            CreateRow(("Value", WitSqlValue.Null)),
            CreateRow(("Value", WitSqlValue.FromInt(20)))
        );

        var selectList = new List<ClauseSelectItem>
        {
            new()
            {
                Expression = new WitSqlExpressionFunctionCall
                {
                    FunctionName = "COUNT",
                    Arguments = [new WitSqlExpressionColumnRef { ColumnName = "Value" }]
                },
                Alias = "Count"
            },
            new()
            {
                Expression = new WitSqlExpressionFunctionCall
                {
                    FunctionName = "SUM",
                    Arguments = [new WitSqlExpressionColumnRef { ColumnName = "Value" }]
                },
                Alias = "Sum"
            }
        };

        var iterator = new IteratorGroupBy(source, null, selectList, m_context);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0]["Count"].AsInt64(), Is.EqualTo(2)); // NULL not counted
        Assert.That(rows[0]["Sum"].AsInt64(), Is.EqualTo(30)); // NULL not summed
    }

    [Test]
    public void GroupBySchemaHasCorrectTypesTest()
    {
        var source = CreateMockIterator(CreateRowWithInts(("Value", 1)));

        var selectList = new List<ClauseSelectItem>
        {
            new()
            {
                Expression = new WitSqlExpressionFunctionCall { FunctionName = "COUNT", IsStar = true },
                Alias = "Count"
            },
            new()
            {
                Expression = new WitSqlExpressionFunctionCall
                {
                    FunctionName = "SUM",
                    Arguments = [new WitSqlExpressionColumnRef { ColumnName = "Value" }]
                },
                Alias = "Sum"
            }
        };

        var iterator = new IteratorGroupBy(source, null, selectList, m_context);

        Assert.That(iterator.Schema[0].Type, Is.EqualTo(WitSqlType.Integer)); // COUNT returns integer
        Assert.That(iterator.Schema[1].Type, Is.EqualTo(WitSqlType.Real)); // SUM returns real
    }

    [Test]
    public void GroupByResetWorksCorrectlyTest()
    {
        var source = CreateMockIterator(
            CreateRowWithInts(("Value", 10)),
            CreateRowWithInts(("Value", 20))
        );

        var selectList = new List<ClauseSelectItem>
        {
            new()
            {
                Expression = new WitSqlExpressionFunctionCall
                {
                    FunctionName = "SUM",
                    Arguments = [new WitSqlExpressionColumnRef { ColumnName = "Value" }]
                },
                Alias = "Sum"
            }
        };

        var iterator = new IteratorGroupBy(source, null, selectList, m_context);

        var rows1 = CollectAllRows(iterator);
        Assert.That(rows1[0]["Sum"].AsInt64(), Is.EqualTo(30));

        iterator.Reset();
        var rows2 = CollectAllRows(iterator);
        Assert.That(rows2[0]["Sum"].AsInt64(), Is.EqualTo(30));
    }

    [Test]
    public void GroupByMultipleGroupColumnsTest()
    {
        var source = CreateMockIterator(
            CreateRow(("Year", WitSqlValue.FromInt(2024)), ("Month", WitSqlValue.FromInt(1)), ("Sales", WitSqlValue.FromInt(100))),
            CreateRow(("Year", WitSqlValue.FromInt(2024)), ("Month", WitSqlValue.FromInt(1)), ("Sales", WitSqlValue.FromInt(200))),
            CreateRow(("Year", WitSqlValue.FromInt(2024)), ("Month", WitSqlValue.FromInt(2)), ("Sales", WitSqlValue.FromInt(150)))
        );

        var groupBy = new List<WitSqlExpression>
        {
            new WitSqlExpressionColumnRef { ColumnName = "Year" },
            new WitSqlExpressionColumnRef { ColumnName = "Month" }
        };

        var selectList = new List<ClauseSelectItem>
        {
            new() { Expression = new WitSqlExpressionColumnRef { ColumnName = "Year" } },
            new() { Expression = new WitSqlExpressionColumnRef { ColumnName = "Month" } },
            new()
            {
                Expression = new WitSqlExpressionFunctionCall
                {
                    FunctionName = "SUM",
                    Arguments = [new WitSqlExpressionColumnRef { ColumnName = "Sales" }]
                },
                Alias = "TotalSales"
            }
        };

        var iterator = new IteratorGroupBy(source, groupBy, selectList, m_context);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Has.Count.EqualTo(2));
        
        var jan = rows.FirstOrDefault(r => r["Month"].AsInt64() == 1);
        var feb = rows.FirstOrDefault(r => r["Month"].AsInt64() == 2);
        
        Assert.That(jan["TotalSales"].AsInt64(), Is.EqualTo(300));
        Assert.That(feb["TotalSales"].AsInt64(), Is.EqualTo(150));
    }

    #endregion

    #region P0.1 Optimization Tests - Conditional AllRows Storage

    [Test]
    public void GroupByWithoutHavingProducesCorrectResultsTest()
    {
        // Verifies that GROUP BY without HAVING still works correctly
        // even though AllRows is not populated
        var source = CreateMockIterator(
            CreateRow(("Category", WitSqlValue.FromText("A")), ("Value", WitSqlValue.FromInt(10))),
            CreateRow(("Category", WitSqlValue.FromText("A")), ("Value", WitSqlValue.FromInt(20))),
            CreateRow(("Category", WitSqlValue.FromText("B")), ("Value", WitSqlValue.FromInt(30))),
            CreateRow(("Category", WitSqlValue.FromText("B")), ("Value", WitSqlValue.FromInt(40))),
            CreateRow(("Category", WitSqlValue.FromText("C")), ("Value", WitSqlValue.FromInt(50)))
        );

        var groupBy = new List<WitSqlExpression>
        {
            new WitSqlExpressionColumnRef { ColumnName = "Category" }
        };

        var selectList = new List<ClauseSelectItem>
        {
            new() { Expression = new WitSqlExpressionColumnRef { ColumnName = "Category" } },
            new()
            {
                Expression = new WitSqlExpressionFunctionCall { FunctionName = "COUNT", IsStar = true },
                Alias = "Count"
            },
            new()
            {
                Expression = new WitSqlExpressionFunctionCall
                {
                    FunctionName = "SUM",
                    Arguments = [new WitSqlExpressionColumnRef { ColumnName = "Value" }]
                },
                Alias = "Sum"
            },
            new()
            {
                Expression = new WitSqlExpressionFunctionCall
                {
                    FunctionName = "AVG",
                    Arguments = [new WitSqlExpressionColumnRef { ColumnName = "Value" }]
                },
                Alias = "Avg"
            }
        };

        // No HAVING clause - P0.1 optimization should skip AllRows storage
        var iterator = new IteratorGroupBy(source, groupBy, selectList, m_context, havingClause: null);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Has.Count.EqualTo(3));
        
        var categoryA = rows.First(r => r["Category"].AsString() == "A");
        var categoryB = rows.First(r => r["Category"].AsString() == "B");
        var categoryC = rows.First(r => r["Category"].AsString() == "C");
        
        Assert.That(categoryA["Count"].AsInt64(), Is.EqualTo(2));
        Assert.That(categoryA["Sum"].AsInt64(), Is.EqualTo(30));
        Assert.That(categoryA["Avg"].AsDouble(), Is.EqualTo(15.0));
        
        Assert.That(categoryB["Count"].AsInt64(), Is.EqualTo(2));
        Assert.That(categoryB["Sum"].AsInt64(), Is.EqualTo(70));
        Assert.That(categoryB["Avg"].AsDouble(), Is.EqualTo(35.0));
        
        Assert.That(categoryC["Count"].AsInt64(), Is.EqualTo(1));
        Assert.That(categoryC["Sum"].AsInt64(), Is.EqualTo(50));
        Assert.That(categoryC["Avg"].AsDouble(), Is.EqualTo(50.0));
    }

    [Test]
    public void GroupByWithHavingFiltersGroupsCorrectlyTest()
    {
        // Verifies that GROUP BY with HAVING still works correctly
        var source = CreateMockIterator(
            CreateRow(("Category", WitSqlValue.FromText("A")), ("Value", WitSqlValue.FromInt(10))),
            CreateRow(("Category", WitSqlValue.FromText("A")), ("Value", WitSqlValue.FromInt(20))),
            CreateRow(("Category", WitSqlValue.FromText("B")), ("Value", WitSqlValue.FromInt(30))),
            CreateRow(("Category", WitSqlValue.FromText("C")), ("Value", WitSqlValue.FromInt(5)))
        );

        var groupBy = new List<WitSqlExpression>
        {
            new WitSqlExpressionColumnRef { ColumnName = "Category" }
        };

        var selectList = new List<ClauseSelectItem>
        {
            new() { Expression = new WitSqlExpressionColumnRef { ColumnName = "Category" } },
            new()
            {
                Expression = new WitSqlExpressionFunctionCall
                {
                    FunctionName = "SUM",
                    Arguments = [new WitSqlExpressionColumnRef { ColumnName = "Value" }]
                },
                Alias = "Sum"
            }
        };

        // HAVING SUM(Value) > 20 - should filter out Category C (Sum=5)
        var havingClause = new WitSqlExpressionBinary
        {
            Left = new WitSqlExpressionFunctionCall
            {
                FunctionName = "SUM",
                Arguments = [new WitSqlExpressionColumnRef { ColumnName = "Value" }]
            },
            Operator = BinaryOperatorType.GreaterThan,
            Right = new WitSqlExpressionLiteral { Type = LiteralType.Integer, Value = 20L }
        };

        var iterator = new IteratorGroupBy(source, groupBy, selectList, m_context, havingClause);
        var rows = CollectAllRows(iterator);

        // Only A (Sum=30) and B (Sum=30) should pass HAVING filter
        Assert.That(rows, Has.Count.EqualTo(2));
        Assert.That(rows.Select(r => r["Category"].AsString()), Does.Contain("A"));
        Assert.That(rows.Select(r => r["Category"].AsString()), Does.Contain("B"));
        Assert.That(rows.Select(r => r["Category"].AsString()), Does.Not.Contain("C"));
    }

    [Test]
    public void GroupByWithMultipleAggregatesWithoutHavingTest()
    {
        // Complex aggregation without HAVING - verifies P0.1 optimization
        var source = CreateMockIterator(
            CreateRow(("Region", WitSqlValue.FromText("North")), ("Amount", WitSqlValue.FromReal(100.0)), ("Qty", WitSqlValue.FromInt(5))),
            CreateRow(("Region", WitSqlValue.FromText("North")), ("Amount", WitSqlValue.FromReal(200.0)), ("Qty", WitSqlValue.FromInt(10))),
            CreateRow(("Region", WitSqlValue.FromText("South")), ("Amount", WitSqlValue.FromReal(150.0)), ("Qty", WitSqlValue.FromInt(3)))
        );

        var groupBy = new List<WitSqlExpression>
        {
            new WitSqlExpressionColumnRef { ColumnName = "Region" }
        };

        var selectList = new List<ClauseSelectItem>
        {
            new() { Expression = new WitSqlExpressionColumnRef { ColumnName = "Region" } },
            new()
            {
                Expression = new WitSqlExpressionFunctionCall { FunctionName = "COUNT", IsStar = true },
                Alias = "SalesCount"
            },
            new()
            {
                Expression = new WitSqlExpressionFunctionCall
                {
                    FunctionName = "SUM",
                    Arguments = [new WitSqlExpressionColumnRef { ColumnName = "Amount" }]
                },
                Alias = "TotalAmount"
            },
            new()
            {
                Expression = new WitSqlExpressionFunctionCall
                {
                    FunctionName = "AVG",
                    Arguments = [new WitSqlExpressionColumnRef { ColumnName = "Amount" }]
                },
                Alias = "AvgAmount"
            },
            new()
            {
                Expression = new WitSqlExpressionFunctionCall
                {
                    FunctionName = "MIN",
                    Arguments = [new WitSqlExpressionColumnRef { ColumnName = "Qty" }]
                },
                Alias = "MinQty"
            },
            new()
            {
                Expression = new WitSqlExpressionFunctionCall
                {
                    FunctionName = "MAX",
                    Arguments = [new WitSqlExpressionColumnRef { ColumnName = "Qty" }]
                },
                Alias = "MaxQty"
            }
        };

        var iterator = new IteratorGroupBy(source, groupBy, selectList, m_context, havingClause: null);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Has.Count.EqualTo(2));
        
        var north = rows.First(r => r["Region"].AsString() == "North");
        var south = rows.First(r => r["Region"].AsString() == "South");
        
        Assert.That(north["SalesCount"].AsInt64(), Is.EqualTo(2));
        Assert.That(north["TotalAmount"].AsDouble(), Is.EqualTo(300.0));
        Assert.That(north["AvgAmount"].AsDouble(), Is.EqualTo(150.0));
        Assert.That(north["MinQty"].AsInt64(), Is.EqualTo(5));
        Assert.That(north["MaxQty"].AsInt64(), Is.EqualTo(10));
        
        Assert.That(south["SalesCount"].AsInt64(), Is.EqualTo(1));
        Assert.That(south["TotalAmount"].AsDouble(), Is.EqualTo(150.0));
        Assert.That(south["AvgAmount"].AsDouble(), Is.EqualTo(150.0));
        Assert.That(south["MinQty"].AsInt64(), Is.EqualTo(3));
        Assert.That(south["MaxQty"].AsInt64(), Is.EqualTo(3));
    }

    #endregion

    #region P0.2 Optimization Tests - Struct-based Composite Key

    [Test]
    public void GroupBySingleColumnKeyWorksTest()
    {
        var source = CreateMockIterator(
            CreateRow(("Key", WitSqlValue.FromText("A")), ("Value", WitSqlValue.FromInt(1))),
            CreateRow(("Key", WitSqlValue.FromText("B")), ("Value", WitSqlValue.FromInt(2))),
            CreateRow(("Key", WitSqlValue.FromText("A")), ("Value", WitSqlValue.FromInt(3)))
        );

        var groupBy = new List<WitSqlExpression>
        {
            new WitSqlExpressionColumnRef { ColumnName = "Key" }
        };

        var selectList = new List<ClauseSelectItem>
        {
            new() { Expression = new WitSqlExpressionColumnRef { ColumnName = "Key" } },
            new()
            {
                Expression = new WitSqlExpressionFunctionCall
                {
                    FunctionName = "SUM",
                    Arguments = [new WitSqlExpressionColumnRef { ColumnName = "Value" }]
                },
                Alias = "Sum"
            }
        };

        var iterator = new IteratorGroupBy(source, groupBy, selectList, m_context);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Has.Count.EqualTo(2));
        Assert.That(rows.First(r => r["Key"].AsString() == "A")["Sum"].AsInt64(), Is.EqualTo(4));
        Assert.That(rows.First(r => r["Key"].AsString() == "B")["Sum"].AsInt64(), Is.EqualTo(2));
    }

    [Test]
    public void GroupByThreeColumnsKeyWorksTest()
    {
        var source = CreateMockIterator(
            CreateRow(("A", WitSqlValue.FromInt(1)), ("B", WitSqlValue.FromText("X")), ("C", WitSqlValue.FromReal(1.0)), ("Value", WitSqlValue.FromInt(10))),
            CreateRow(("A", WitSqlValue.FromInt(1)), ("B", WitSqlValue.FromText("X")), ("C", WitSqlValue.FromReal(1.0)), ("Value", WitSqlValue.FromInt(20))),
            CreateRow(("A", WitSqlValue.FromInt(1)), ("B", WitSqlValue.FromText("Y")), ("C", WitSqlValue.FromReal(1.0)), ("Value", WitSqlValue.FromInt(30)))
        );

        var groupBy = new List<WitSqlExpression>
        {
            new WitSqlExpressionColumnRef { ColumnName = "A" },
            new WitSqlExpressionColumnRef { ColumnName = "B" },
            new WitSqlExpressionColumnRef { ColumnName = "C" }
        };

        var selectList = new List<ClauseSelectItem>
        {
            new() { Expression = new WitSqlExpressionColumnRef { ColumnName = "A" } },
            new() { Expression = new WitSqlExpressionColumnRef { ColumnName = "B" } },
            new()
            {
                Expression = new WitSqlExpressionFunctionCall
                {
                    FunctionName = "SUM",
                    Arguments = [new WitSqlExpressionColumnRef { ColumnName = "Value" }]
                },
                Alias = "Sum"
            }
        };

        var iterator = new IteratorGroupBy(source, groupBy, selectList, m_context);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Has.Count.EqualTo(2));
        Assert.That(rows.First(r => r["B"].AsString() == "X")["Sum"].AsInt64(), Is.EqualTo(30));
        Assert.That(rows.First(r => r["B"].AsString() == "Y")["Sum"].AsInt64(), Is.EqualTo(30));
    }

    [Test]
    public void GroupByFourColumnsKeyWorksTest()
    {
        var source = CreateMockIterator(
            CreateRow(("A", WitSqlValue.FromInt(1)), ("B", WitSqlValue.FromInt(2)), ("C", WitSqlValue.FromInt(3)), ("D", WitSqlValue.FromInt(4)), ("Value", WitSqlValue.FromInt(100))),
            CreateRow(("A", WitSqlValue.FromInt(1)), ("B", WitSqlValue.FromInt(2)), ("C", WitSqlValue.FromInt(3)), ("D", WitSqlValue.FromInt(4)), ("Value", WitSqlValue.FromInt(200))),
            CreateRow(("A", WitSqlValue.FromInt(1)), ("B", WitSqlValue.FromInt(2)), ("C", WitSqlValue.FromInt(3)), ("D", WitSqlValue.FromInt(5)), ("Value", WitSqlValue.FromInt(300)))
        );

        var groupBy = new List<WitSqlExpression>
        {
            new WitSqlExpressionColumnRef { ColumnName = "A" },
            new WitSqlExpressionColumnRef { ColumnName = "B" },
            new WitSqlExpressionColumnRef { ColumnName = "C" },
            new WitSqlExpressionColumnRef { ColumnName = "D" }
        };

        var selectList = new List<ClauseSelectItem>
        {
            new() { Expression = new WitSqlExpressionColumnRef { ColumnName = "D" } },
            new()
            {
                Expression = new WitSqlExpressionFunctionCall
                {
                    FunctionName = "SUM",
                    Arguments = [new WitSqlExpressionColumnRef { ColumnName = "Value" }]
                },
                Alias = "Sum"
            }
        };

        var iterator = new IteratorGroupBy(source, groupBy, selectList, m_context);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Has.Count.EqualTo(2));
        Assert.That(rows.First(r => r["D"].AsInt64() == 4)["Sum"].AsInt64(), Is.EqualTo(300));
        Assert.That(rows.First(r => r["D"].AsInt64() == 5)["Sum"].AsInt64(), Is.EqualTo(300));
    }

    [Test]
    public void GroupByFiveColumnsKeyWorksTest()
    {
        // Tests the fallback path for 5+ GROUP BY columns
        var source = CreateMockIterator(
            CreateRow(
                ("A", WitSqlValue.FromInt(1)), 
                ("B", WitSqlValue.FromInt(2)), 
                ("C", WitSqlValue.FromInt(3)), 
                ("D", WitSqlValue.FromInt(4)), 
                ("E", WitSqlValue.FromInt(5)), 
                ("Value", WitSqlValue.FromInt(100))),
            CreateRow(
                ("A", WitSqlValue.FromInt(1)), 
                ("B", WitSqlValue.FromInt(2)), 
                ("C", WitSqlValue.FromInt(3)), 
                ("D", WitSqlValue.FromInt(4)), 
                ("E", WitSqlValue.FromInt(5)), 
                ("Value", WitSqlValue.FromInt(200)))
        );

        var groupBy = new List<WitSqlExpression>
        {
            new WitSqlExpressionColumnRef { ColumnName = "A" },
            new WitSqlExpressionColumnRef { ColumnName = "B" },
            new WitSqlExpressionColumnRef { ColumnName = "C" },
            new WitSqlExpressionColumnRef { ColumnName = "D" },
            new WitSqlExpressionColumnRef { ColumnName = "E" }
        };

        var selectList = new List<ClauseSelectItem>
        {
            new()
            {
                Expression = new WitSqlExpressionFunctionCall
                {
                    FunctionName = "SUM",
                    Arguments = [new WitSqlExpressionColumnRef { ColumnName = "Value" }]
                },
                Alias = "Sum"
            }
        };

        var iterator = new IteratorGroupBy(source, groupBy, selectList, m_context);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0]["Sum"].AsInt64(), Is.EqualTo(300));
    }

    [Test]
    public void GroupByWithNullKeyValuesTest()
    {
        var source = CreateMockIterator(
            CreateRow(("Key", WitSqlValue.FromText("A")), ("Value", WitSqlValue.FromInt(1))),
            CreateRow(("Key", WitSqlValue.Null), ("Value", WitSqlValue.FromInt(2))),
            CreateRow(("Key", WitSqlValue.Null), ("Value", WitSqlValue.FromInt(3))),
            CreateRow(("Key", WitSqlValue.FromText("A")), ("Value", WitSqlValue.FromInt(4)))
        );

        var groupBy = new List<WitSqlExpression>
        {
            new WitSqlExpressionColumnRef { ColumnName = "Key" }
        };

        var selectList = new List<ClauseSelectItem>
        {
            new() { Expression = new WitSqlExpressionColumnRef { ColumnName = "Key" } },
            new()
            {
                Expression = new WitSqlExpressionFunctionCall
                {
                    FunctionName = "SUM",
                    Arguments = [new WitSqlExpressionColumnRef { ColumnName = "Value" }]
                },
                Alias = "Sum"
            }
        };

        var iterator = new IteratorGroupBy(source, groupBy, selectList, m_context);
        var rows = CollectAllRows(iterator);

        // NULL values should form their own group
        Assert.That(rows, Has.Count.EqualTo(2));
        Assert.That(rows.First(r => r["Key"].AsString() == "A")["Sum"].AsInt64(), Is.EqualTo(5));
        Assert.That(rows.First(r => r["Key"].IsNull)["Sum"].AsInt64(), Is.EqualTo(5));
    }

    #endregion
}

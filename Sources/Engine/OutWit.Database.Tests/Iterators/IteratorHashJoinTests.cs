using OutWit.Database.Iterators;
using OutWit.Database.Parser.Expressions;
using OutWit.Database.Parser.Schema.Types;
using OutWit.Database.Sql;
using OutWit.Database.Types;
using OutWit.Database.Values;

namespace OutWit.Database.Tests.Iterators;

[TestFixture]
public class IteratorHashJoinTests : IteratorTestsBase
{
    #region Helper Methods

    private static IReadOnlyList<WitSqlColumnInfo> CreateSchemaWithTable(string tableName, params string[] columns)
    {
        return columns.Select(c => new WitSqlColumnInfo
        {
            Name = c,
            Type = WitSqlType.Integer,
            TableName = tableName
        }).ToList();
    }

    private static IteratorHashJoin.JoinKeyPair CreateJoinKey(string leftTable, string leftCol, string rightTable, string rightCol)
    {
        return new IteratorHashJoin.JoinKeyPair
        {
            LeftKey = new WitSqlExpressionColumnRef { TableName = leftTable, ColumnName = leftCol },
            RightKey = new WitSqlExpressionColumnRef { TableName = rightTable, ColumnName = rightCol }
        };
    }

    private static WitSqlExpression CreateResidualCondition(string leftTable, string leftCol, string rightTable, string rightCol)
    {
        return new WitSqlExpressionBinary
        {
            Left = new WitSqlExpressionColumnRef { TableName = leftTable, ColumnName = leftCol },
            Operator = BinaryOperatorType.Equal,
            Right = new WitSqlExpressionColumnRef { TableName = rightTable, ColumnName = rightCol }
        };
    }

    #endregion

    #region INNER JOIN Tests

    [Test]
    public void InnerJoinReturnsMatchingRowsOnlyTest()
    {
        var leftSchema = CreateSchemaWithTable("Users", "Id", "Name");
        var left = CreateMockIterator(leftSchema,
            CreateRow(("Id", WitSqlValue.FromInt(1)), ("Name", WitSqlValue.FromText("Alice"))),
            CreateRow(("Id", WitSqlValue.FromInt(2)), ("Name", WitSqlValue.FromText("Bob"))),
            CreateRow(("Id", WitSqlValue.FromInt(3)), ("Name", WitSqlValue.FromText("Charlie")))
        );

        var rightSchema = CreateSchemaWithTable("Orders", "UserId", "Amount");
        var right = CreateMockIterator(rightSchema,
            CreateRow(("UserId", WitSqlValue.FromInt(1)), ("Amount", WitSqlValue.FromInt(100))),
            CreateRow(("UserId", WitSqlValue.FromInt(1)), ("Amount", WitSqlValue.FromInt(200))),
            CreateRow(("UserId", WitSqlValue.FromInt(3)), ("Amount", WitSqlValue.FromInt(300)))
        );

        var joinKeys = new[] { CreateJoinKey("Users", "Id", "Orders", "UserId") };
        var iterator = new IteratorHashJoin(left, right, JoinType.Inner, joinKeys, null, m_context);
        var rows = CollectAllRows(iterator);

        // Alice has 2 orders, Charlie has 1, Bob has none
        Assert.That(rows, Has.Count.EqualTo(3));
    }

    [Test]
    public void InnerJoinWithNoMatchesReturnsEmptyTest()
    {
        var leftSchema = CreateSchemaWithTable("A", "Id");
        var left = CreateMockIterator(leftSchema,
            CreateRowWithInts(("Id", 1)),
            CreateRowWithInts(("Id", 2))
        );

        var rightSchema = CreateSchemaWithTable("B", "AId");
        var right = CreateMockIterator(rightSchema,
            CreateRowWithInts(("AId", 100)),
            CreateRowWithInts(("AId", 200))
        );

        var joinKeys = new[] { CreateJoinKey("A", "Id", "B", "AId") };
        var iterator = new IteratorHashJoin(left, right, JoinType.Inner, joinKeys, null, m_context);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Is.Empty);
    }

    [Test]
    public void InnerJoinPreservesColumnValuesTest()
    {
        var leftSchema = CreateSchemaWithTable("L", "Id", "Name");
        var left = CreateMockIterator(leftSchema,
            CreateRow(("Id", WitSqlValue.FromInt(1)), ("Name", WitSqlValue.FromText("Test")))
        );

        var rightSchema = CreateSchemaWithTable("R", "LId", "Value");
        var right = CreateMockIterator(rightSchema,
            CreateRow(("LId", WitSqlValue.FromInt(1)), ("Value", WitSqlValue.FromInt(999)))
        );

        var joinKeys = new[] { CreateJoinKey("L", "Id", "R", "LId") };
        var iterator = new IteratorHashJoin(left, right, JoinType.Inner, joinKeys, null, m_context);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0]["Name"].AsString(), Is.EqualTo("Test"));
        Assert.That(rows[0]["Value"].AsInt64(), Is.EqualTo(999));
    }

    [Test]
    public void InnerJoinWithDuplicateKeysReturnsAllCombinationsTest()
    {
        var leftSchema = CreateSchemaWithTable("L", "Id");
        var left = CreateMockIterator(leftSchema,
            CreateRowWithInts(("Id", 1)),
            CreateRowWithInts(("Id", 1)),
            CreateRowWithInts(("Id", 2))
        );

        var rightSchema = CreateSchemaWithTable("R", "LId", "Seq");
        var right = CreateMockIterator(rightSchema,
            CreateRow(("LId", WitSqlValue.FromInt(1)), ("Seq", WitSqlValue.FromInt(1))),
            CreateRow(("LId", WitSqlValue.FromInt(1)), ("Seq", WitSqlValue.FromInt(2)))
        );

        var joinKeys = new[] { CreateJoinKey("L", "Id", "R", "LId") };
        var iterator = new IteratorHashJoin(left, right, JoinType.Inner, joinKeys, null, m_context);
        var rows = CollectAllRows(iterator);

        // 2 left rows with Id=1 × 2 right rows with LId=1 = 4 rows
        Assert.That(rows, Has.Count.EqualTo(4));
    }

    #endregion

    #region LEFT JOIN Tests

    [Test]
    public void LeftJoinReturnsAllLeftRowsTest()
    {
        var leftSchema = CreateSchemaWithTable("Users", "Id");
        var left = CreateMockIterator(leftSchema,
            CreateRowWithInts(("Id", 1)),
            CreateRowWithInts(("Id", 2)),
            CreateRowWithInts(("Id", 3))
        );

        var rightSchema = CreateSchemaWithTable("Orders", "UserId");
        var right = CreateMockIterator(rightSchema,
            CreateRowWithInts(("UserId", 1))
        );

        var joinKeys = new[] { CreateJoinKey("Users", "Id", "Orders", "UserId") };
        var iterator = new IteratorHashJoin(left, right, JoinType.Left, joinKeys, null, m_context);
        var rows = CollectAllRows(iterator);

        // All 3 left rows should be present
        Assert.That(rows, Has.Count.EqualTo(3));
    }

    [Test]
    public void LeftJoinPadsUnmatchedWithNullsTest()
    {
        var leftSchema = CreateSchemaWithTable("L", "Id");
        var left = CreateMockIterator(leftSchema,
            CreateRowWithInts(("Id", 1)),
            CreateRowWithInts(("Id", 2))
        );

        var rightSchema = CreateSchemaWithTable("R", "LId", "Value");
        var right = CreateMockIterator(rightSchema,
            CreateRow(("LId", WitSqlValue.FromInt(1)), ("Value", WitSqlValue.FromInt(100)))
        );

        var joinKeys = new[] { CreateJoinKey("L", "Id", "R", "LId") };
        var iterator = new IteratorHashJoin(left, right, JoinType.Left, joinKeys, null, m_context);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Has.Count.EqualTo(2));

        // First row should have Value = 100
        var matchedRow = rows.First(r => r["Id"].AsInt64() == 1);
        Assert.That(matchedRow["Value"].AsInt64(), Is.EqualTo(100));

        // Second row should have Value = NULL
        var unmatchedRow = rows.First(r => r["Id"].AsInt64() == 2);
        Assert.That(unmatchedRow["Value"].IsNull, Is.True);
    }

    [Test]
    public void LeftJoinWithEmptyRightReturnsAllLeftWithNullsTest()
    {
        var leftSchema = CreateSchemaWithTable("L", "Id");
        var left = CreateMockIterator(leftSchema,
            CreateRowWithInts(("Id", 1)),
            CreateRowWithInts(("Id", 2))
        );

        var rightSchema = CreateSchemaWithTable("R", "LId");
        var right = CreateMockIterator(rightSchema);

        var joinKeys = new[] { CreateJoinKey("L", "Id", "R", "LId") };
        var iterator = new IteratorHashJoin(left, right, JoinType.Left, joinKeys, null, m_context);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Has.Count.EqualTo(2));
        Assert.That(rows.All(r => r["LId"].IsNull), Is.True);
    }

    [Test]
    public void LeftJoinWithMultipleMatchesReturnsAllTest()
    {
        var leftSchema = CreateSchemaWithTable("L", "Id");
        var left = CreateMockIterator(leftSchema,
            CreateRowWithInts(("Id", 1))
        );

        var rightSchema = CreateSchemaWithTable("R", "LId", "Seq");
        var right = CreateMockIterator(rightSchema,
            CreateRow(("LId", WitSqlValue.FromInt(1)), ("Seq", WitSqlValue.FromInt(1))),
            CreateRow(("LId", WitSqlValue.FromInt(1)), ("Seq", WitSqlValue.FromInt(2))),
            CreateRow(("LId", WitSqlValue.FromInt(1)), ("Seq", WitSqlValue.FromInt(3)))
        );

        var joinKeys = new[] { CreateJoinKey("L", "Id", "R", "LId") };
        var iterator = new IteratorHashJoin(left, right, JoinType.Left, joinKeys, null, m_context);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Has.Count.EqualTo(3));
    }

    #endregion

    #region NULL Handling Tests

    [Test]
    public void InnerJoinWithNullKeyDoesNotMatchTest()
    {
        var leftSchema = CreateSchemaWithTable("L", "Id");
        var left = CreateMockIterator(leftSchema,
            CreateRow(("Id", WitSqlValue.FromInt(1))),
            CreateRow(("Id", WitSqlValue.Null))
        );

        var rightSchema = CreateSchemaWithTable("R", "LId");
        var right = CreateMockIterator(rightSchema,
            CreateRow(("LId", WitSqlValue.FromInt(1))),
            CreateRow(("LId", WitSqlValue.Null))
        );

        var joinKeys = new[] { CreateJoinKey("L", "Id", "R", "LId") };
        var iterator = new IteratorHashJoin(left, right, JoinType.Inner, joinKeys, null, m_context);
        var rows = CollectAllRows(iterator);

        // Only non-NULL keys match (Id=1 with LId=1)
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0]["Id"].AsInt64(), Is.EqualTo(1));
    }

    [Test]
    public void LeftJoinWithNullKeyReturnsUnmatchedRowTest()
    {
        var leftSchema = CreateSchemaWithTable("L", "Id");
        var left = CreateMockIterator(leftSchema,
            CreateRow(("Id", WitSqlValue.FromInt(1))),
            CreateRow(("Id", WitSqlValue.Null))
        );

        var rightSchema = CreateSchemaWithTable("R", "LId", "Value");
        var right = CreateMockIterator(rightSchema,
            CreateRow(("LId", WitSqlValue.FromInt(1)), ("Value", WitSqlValue.FromInt(100)))
        );

        var joinKeys = new[] { CreateJoinKey("L", "Id", "R", "LId") };
        var iterator = new IteratorHashJoin(left, right, JoinType.Left, joinKeys, null, m_context);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Has.Count.EqualTo(2));

        // Row with NULL Id should have NULL right columns
        var nullRow = rows.First(r => r["Id"].IsNull);
        Assert.That(nullRow["Value"].IsNull, Is.True);
    }

    #endregion

    #region Multi-Column Key Tests

    [Test]
    public void InnerJoinWithMultipleKeysMatchesCorrectlyTest()
    {
        var leftSchema = CreateSchemaWithTable("L", "A", "B", "Name");
        var left = CreateMockIterator(leftSchema,
            CreateRow(("A", WitSqlValue.FromInt(1)), ("B", WitSqlValue.FromInt(10)), ("Name", WitSqlValue.FromText("One"))),
            CreateRow(("A", WitSqlValue.FromInt(1)), ("B", WitSqlValue.FromInt(20)), ("Name", WitSqlValue.FromText("Two"))),
            CreateRow(("A", WitSqlValue.FromInt(2)), ("B", WitSqlValue.FromInt(10)), ("Name", WitSqlValue.FromText("Three")))
        );

        var rightSchema = CreateSchemaWithTable("R", "X", "Y", "Value");
        var right = CreateMockIterator(rightSchema,
            CreateRow(("X", WitSqlValue.FromInt(1)), ("Y", WitSqlValue.FromInt(10)), ("Value", WitSqlValue.FromInt(100))),
            CreateRow(("X", WitSqlValue.FromInt(1)), ("Y", WitSqlValue.FromInt(30)), ("Value", WitSqlValue.FromInt(200)))
        );

        var joinKeys = new[]
        {
            CreateJoinKey("L", "A", "R", "X"),
            CreateJoinKey("L", "B", "R", "Y")
        };
        var iterator = new IteratorHashJoin(left, right, JoinType.Inner, joinKeys, null, m_context);
        var rows = CollectAllRows(iterator);

        // Only (A=1, B=10) matches (X=1, Y=10)
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0]["Name"].AsString(), Is.EqualTo("One"));
        Assert.That(rows[0]["Value"].AsInt64(), Is.EqualTo(100));
    }

    #endregion

    #region Build Side Selection Tests

    [Test]
    public void HashJoinWithBuildLeftProducesCorrectResultsTest()
    {
        var leftSchema = CreateSchemaWithTable("L", "Id");
        var left = CreateMockIterator(leftSchema,
            CreateRowWithInts(("Id", 1)),
            CreateRowWithInts(("Id", 2))
        );

        var rightSchema = CreateSchemaWithTable("R", "LId", "Value");
        var right = CreateMockIterator(rightSchema,
            CreateRow(("LId", WitSqlValue.FromInt(1)), ("Value", WitSqlValue.FromInt(100))),
            CreateRow(("LId", WitSqlValue.FromInt(2)), ("Value", WitSqlValue.FromInt(200)))
        );

        var joinKeys = new[] { CreateJoinKey("L", "Id", "R", "LId") };
        var iterator = new IteratorHashJoin(left, right, JoinType.Inner, joinKeys, null, m_context, buildLeft: true);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Has.Count.EqualTo(2));
        
        var row1 = rows.First(r => r["Id"].AsInt64() == 1);
        Assert.That(row1["Value"].AsInt64(), Is.EqualTo(100));

        var row2 = rows.First(r => r["Id"].AsInt64() == 2);
        Assert.That(row2["Value"].AsInt64(), Is.EqualTo(200));
    }

    #endregion

    #region Schema Tests

    [Test]
    public void HashJoinSchemaContainsBothSidesColumnsTest()
    {
        var leftSchema = CreateSchemaWithTable("L", "Id", "Name");
        var left = CreateMockIterator(leftSchema);

        var rightSchema = CreateSchemaWithTable("R", "LId", "Value");
        var right = CreateMockIterator(rightSchema);

        var joinKeys = new[] { CreateJoinKey("L", "Id", "R", "LId") };
        var iterator = new IteratorHashJoin(left, right, JoinType.Inner, joinKeys, null, m_context);

        Assert.That(iterator.Schema, Has.Count.EqualTo(4));
        Assert.That(iterator.Schema.Any(c => c.Name == "Id" && c.TableName == "L"), Is.True);
        Assert.That(iterator.Schema.Any(c => c.Name == "Name" && c.TableName == "L"), Is.True);
        Assert.That(iterator.Schema.Any(c => c.Name == "LId" && c.TableName == "R"), Is.True);
        Assert.That(iterator.Schema.Any(c => c.Name == "Value" && c.TableName == "R"), Is.True);
    }

    #endregion

    #region Reset Tests

    [Test]
    public void HashJoinResetWorksCorrectlyTest()
    {
        var leftSchema = CreateSchemaWithTable("L", "Id");
        var left = CreateMockIterator(leftSchema,
            CreateRowWithInts(("Id", 1)),
            CreateRowWithInts(("Id", 2))
        );

        var rightSchema = CreateSchemaWithTable("R", "LId");
        var right = CreateMockIterator(rightSchema,
            CreateRowWithInts(("LId", 1)),
            CreateRowWithInts(("LId", 2))
        );

        var joinKeys = new[] { CreateJoinKey("L", "Id", "R", "LId") };
        var iterator = new IteratorHashJoin(left, right, JoinType.Inner, joinKeys, null, m_context);

        var rows1 = CollectAllRows(iterator);
        Assert.That(rows1, Has.Count.EqualTo(2));

        iterator.Reset();
        var rows2 = CollectAllRows(iterator);
        Assert.That(rows2, Has.Count.EqualTo(2));
    }

    #endregion

    #region Edge Cases

    [Test]
    public void HashJoinWithBothSidesEmptyReturnsEmptyTest()
    {
        var leftSchema = CreateSchemaWithTable("L", "Id");
        var left = CreateMockIterator(leftSchema);

        var rightSchema = CreateSchemaWithTable("R", "LId");
        var right = CreateMockIterator(rightSchema);

        var joinKeys = new[] { CreateJoinKey("L", "Id", "R", "LId") };
        var iterator = new IteratorHashJoin(left, right, JoinType.Inner, joinKeys, null, m_context);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Is.Empty);
    }

    [Test]
    public void HashJoinWithEmptyLeftReturnsEmptyForInnerTest()
    {
        var leftSchema = CreateSchemaWithTable("L", "Id");
        var left = CreateMockIterator(leftSchema);

        var rightSchema = CreateSchemaWithTable("R", "LId");
        var right = CreateMockIterator(rightSchema,
            CreateRowWithInts(("LId", 1))
        );

        var joinKeys = new[] { CreateJoinKey("L", "Id", "R", "LId") };
        var iterator = new IteratorHashJoin(left, right, JoinType.Inner, joinKeys, null, m_context);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Is.Empty);
    }

    [Test]
    public void HashJoinWithSingleRowEachReturnsCorrectResultTest()
    {
        var leftSchema = CreateSchemaWithTable("L", "Id");
        var left = CreateMockIterator(leftSchema,
            CreateRowWithInts(("Id", 1))
        );

        var rightSchema = CreateSchemaWithTable("R", "LId");
        var right = CreateMockIterator(rightSchema,
            CreateRowWithInts(("LId", 1))
        );

        var joinKeys = new[] { CreateJoinKey("L", "Id", "R", "LId") };
        var iterator = new IteratorHashJoin(left, right, JoinType.Inner, joinKeys, null, m_context);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Has.Count.EqualTo(1));
    }

    [Test]
    public void HashJoinThrowsForUnsupportedJoinTypeTest()
    {
        var leftSchema = CreateSchemaWithTable("L", "Id");
        var left = CreateMockIterator(leftSchema);

        var rightSchema = CreateSchemaWithTable("R", "LId");
        var right = CreateMockIterator(rightSchema);

        var joinKeys = new[] { CreateJoinKey("L", "Id", "R", "LId") };

        Assert.Throws<ArgumentException>(() =>
            new IteratorHashJoin(left, right, JoinType.Right, joinKeys, null, m_context));
    }

    [Test]
    public void HashJoinThrowsForEmptyJoinKeysTest()
    {
        var leftSchema = CreateSchemaWithTable("L", "Id");
        var left = CreateMockIterator(leftSchema);

        var rightSchema = CreateSchemaWithTable("R", "LId");
        var right = CreateMockIterator(rightSchema);

        var joinKeys = Array.Empty<IteratorHashJoin.JoinKeyPair>();

        Assert.Throws<ArgumentException>(() =>
            new IteratorHashJoin(left, right, JoinType.Inner, joinKeys, null, m_context));
    }

    #endregion

    #region Large Dataset Tests

    [Test]
    public void HashJoinHandlesLargeDatasetTest()
    {
        var leftSchema = CreateSchemaWithTable("L", "Id");
        var leftRows = Enumerable.Range(1, 1000)
            .Select(i => CreateRowWithInts(("Id", i)))
            .ToArray();
        var left = CreateMockIterator(leftSchema, leftRows);

        var rightSchema = CreateSchemaWithTable("R", "LId", "Value");
        // Every 10th row matches
        var rightRows = Enumerable.Range(1, 100)
            .Select(i => CreateRow(("LId", WitSqlValue.FromInt(i * 10)), ("Value", WitSqlValue.FromInt(i * 100))))
            .ToArray();
        var right = CreateMockIterator(rightSchema, rightRows);

        var joinKeys = new[] { CreateJoinKey("L", "Id", "R", "LId") };
        var iterator = new IteratorHashJoin(left, right, JoinType.Inner, joinKeys, null, m_context);
        var rows = CollectAllRows(iterator);

        // 100 matching rows (10, 20, 30, ..., 1000)
        Assert.That(rows, Has.Count.EqualTo(100));
    }

    #endregion
}

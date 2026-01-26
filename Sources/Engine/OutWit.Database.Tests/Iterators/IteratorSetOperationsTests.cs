using OutWit.Database.Iterators;
using OutWit.Database.Sql;
using OutWit.Database.Types;
using OutWit.Database.Values;

namespace OutWit.Database.Tests.Iterators;

[TestFixture]
public class IteratorSetOperationsTests : IteratorTestsBase
{
    #region UNION Tests

    [Test]
    public void UnionAllReturnsBothSidesRowsTest()
    {
        var left = CreateMockIterator(
            CreateRowWithInts(("Id", 1)),
            CreateRowWithInts(("Id", 2))
        );
        var right = CreateMockIterator(
            CreateRowWithInts(("Id", 3)),
            CreateRowWithInts(("Id", 4))
        );

        var iterator = new IteratorUnion(left, right, isAll: true);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Has.Count.EqualTo(4));
    }

    [Test]
    public void UnionAllPreservesDuplicatesTest()
    {
        var left = CreateMockIterator(
            CreateRowWithInts(("Id", 1)),
            CreateRowWithInts(("Id", 2))
        );
        var right = CreateMockIterator(
            CreateRowWithInts(("Id", 1)),
            CreateRowWithInts(("Id", 2))
        );

        var iterator = new IteratorUnion(left, right, isAll: true);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Has.Count.EqualTo(4));
    }

    [Test]
    public void UnionRemovesDuplicatesTest()
    {
        var left = CreateMockIterator(
            CreateRowWithInts(("Id", 1)),
            CreateRowWithInts(("Id", 2))
        );
        var right = CreateMockIterator(
            CreateRowWithInts(("Id", 1)),
            CreateRowWithInts(("Id", 3))
        );

        var iterator = new IteratorUnion(left, right, isAll: false);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Has.Count.EqualTo(3)); // 1, 2, 3
    }

    [Test]
    public void UnionWithEmptyLeftReturnsRightTest()
    {
        var left = CreateMockIterator();
        var right = CreateMockIterator(
            CreateRowWithInts(("Id", 1)),
            CreateRowWithInts(("Id", 2))
        );

        var iterator = new IteratorUnion(left, right, isAll: false);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Has.Count.EqualTo(2));
    }

    [Test]
    public void UnionWithEmptyRightReturnsLeftTest()
    {
        var left = CreateMockIterator(
            CreateRowWithInts(("Id", 1)),
            CreateRowWithInts(("Id", 2))
        );
        var right = CreateMockIterator();

        var iterator = new IteratorUnion(left, right, isAll: false);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Has.Count.EqualTo(2));
    }

    #endregion

    #region INTERSECT Tests

    [Test]
    public void IntersectReturnsCommonRowsTest()
    {
        var left = CreateMockIterator(
            CreateRowWithInts(("Id", 1)),
            CreateRowWithInts(("Id", 2)),
            CreateRowWithInts(("Id", 3))
        );
        var right = CreateMockIterator(
            CreateRowWithInts(("Id", 2)),
            CreateRowWithInts(("Id", 3)),
            CreateRowWithInts(("Id", 4))
        );

        var iterator = new IteratorIntersect(left, right, isAll: false);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Has.Count.EqualTo(2)); // 2, 3
    }

    [Test]
    public void IntersectWithNoCommonRowsReturnsEmptyTest()
    {
        var left = CreateMockIterator(
            CreateRowWithInts(("Id", 1)),
            CreateRowWithInts(("Id", 2))
        );
        var right = CreateMockIterator(
            CreateRowWithInts(("Id", 3)),
            CreateRowWithInts(("Id", 4))
        );

        var iterator = new IteratorIntersect(left, right, isAll: false);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Is.Empty);
    }

    [Test]
    public void IntersectAllHandlesDuplicatesTest()
    {
        var left = CreateMockIterator(
            CreateRowWithInts(("Id", 1)),
            CreateRowWithInts(("Id", 1)),
            CreateRowWithInts(("Id", 2))
        );
        var right = CreateMockIterator(
            CreateRowWithInts(("Id", 1)),
            CreateRowWithInts(("Id", 1)),
            CreateRowWithInts(("Id", 1))
        );

        var iterator = new IteratorIntersect(left, right, isAll: true);
        var rows = CollectAllRows(iterator);

        // Left has 2 copies of 1, right has 3 copies -> should return 2
        Assert.That(rows, Has.Count.EqualTo(2));
    }

    [Test]
    public void IntersectWithEmptyLeftReturnsEmptyTest()
    {
        var left = CreateMockIterator();
        var right = CreateMockIterator(
            CreateRowWithInts(("Id", 1))
        );

        var iterator = new IteratorIntersect(left, right, isAll: false);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Is.Empty);
    }

    #endregion

    #region EXCEPT Tests

    [Test]
    public void ExceptReturnsLeftMinusRightTest()
    {
        var left = CreateMockIterator(
            CreateRowWithInts(("Id", 1)),
            CreateRowWithInts(("Id", 2)),
            CreateRowWithInts(("Id", 3))
        );
        var right = CreateMockIterator(
            CreateRowWithInts(("Id", 2)),
            CreateRowWithInts(("Id", 4))
        );

        var iterator = new IteratorExcept(left, right, isAll: false);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Has.Count.EqualTo(2)); // 1, 3
    }

    [Test]
    public void ExceptRemovesDuplicatesFromLeftTest()
    {
        var left = CreateMockIterator(
            CreateRowWithInts(("Id", 1)),
            CreateRowWithInts(("Id", 1)),
            CreateRowWithInts(("Id", 2))
        );
        var right = CreateMockIterator(
            CreateRowWithInts(("Id", 3))
        );

        var iterator = new IteratorExcept(left, right, isAll: false);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Has.Count.EqualTo(2)); // 1, 2 (duplicates removed)
    }

    [Test]
    public void ExceptAllPreservesDuplicatesTest()
    {
        var left = CreateMockIterator(
            CreateRowWithInts(("Id", 1)),
            CreateRowWithInts(("Id", 1)),
            CreateRowWithInts(("Id", 1)),
            CreateRowWithInts(("Id", 2))
        );
        var right = CreateMockIterator(
            CreateRowWithInts(("Id", 1))
        );

        var iterator = new IteratorExcept(left, right, isAll: true);
        var rows = CollectAllRows(iterator);

        // Left has 3 copies of 1, right has 1 -> should return 2 copies of 1 + 1 copy of 2
        Assert.That(rows, Has.Count.EqualTo(3));
    }

    [Test]
    public void ExceptWithAllRowsMatchingReturnsEmptyTest()
    {
        var left = CreateMockIterator(
            CreateRowWithInts(("Id", 1)),
            CreateRowWithInts(("Id", 2))
        );
        var right = CreateMockIterator(
            CreateRowWithInts(("Id", 1)),
            CreateRowWithInts(("Id", 2)),
            CreateRowWithInts(("Id", 3))
        );

        var iterator = new IteratorExcept(left, right, isAll: false);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Is.Empty);
    }

    [Test]
    public void ExceptWithEmptyRightReturnsLeftTest()
    {
        var left = CreateMockIterator(
            CreateRowWithInts(("Id", 1)),
            CreateRowWithInts(("Id", 2))
        );
        var right = CreateMockIterator();

        var iterator = new IteratorExcept(left, right, isAll: false);
        var rows = CollectAllRows(iterator);

        Assert.That(rows, Has.Count.EqualTo(2));
    }

    #endregion

    #region Schema Tests

    [Test]
    public void UnionUsesLeftSchemaTest()
    {
        var leftSchema = new List<WitSqlColumnInfo>
        {
            new() { Name = "Id", Type = WitSqlType.Integer },
            new() { Name = "Name", Type = WitSqlType.Text }
        };
        var left = CreateMockIterator(leftSchema);

        var rightSchema = new List<WitSqlColumnInfo>
        {
            new() { Name = "UserId", Type = WitSqlType.Integer },
            new() { Name = "UserName", Type = WitSqlType.Text }
        };
        var right = CreateMockIterator(rightSchema);

        var iterator = new IteratorUnion(left, right, isAll: false);

        Assert.That(iterator.Schema[0].Name, Is.EqualTo("Id"));
        Assert.That(iterator.Schema[1].Name, Is.EqualTo("Name"));
    }

    #endregion

    #region Reset Tests

    [Test]
    public void UnionResetWorksCorrectlyTest()
    {
        var left = CreateMockIterator(
            CreateRowWithInts(("Id", 1))
        );
        var right = CreateMockIterator(
            CreateRowWithInts(("Id", 2))
        );

        var iterator = new IteratorUnion(left, right, isAll: false);

        var rows1 = CollectAllRows(iterator);
        Assert.That(rows1, Has.Count.EqualTo(2));

        iterator.Reset();

        var rows2 = CollectAllRows(iterator);
        Assert.That(rows2, Has.Count.EqualTo(2));
    }

    #endregion
}

using OutWit.Database.Types;
using OutWit.Database.Values;

namespace OutWit.Database.Tests.Values;

/// <summary>
/// Tests for WitSqlValue - core functionality, static instances, and ToString.
/// </summary>
[TestFixture]
public class WitSqlValueTests
{
    #region Static Instances Tests

    [Test]
    public void NullIsNullTypeTest()
    {
        Assert.That(WitSqlValue.Null.Type, Is.EqualTo(WitSqlType.Null));
        Assert.That(WitSqlValue.Null.IsNull, Is.True);
    }

    [Test]
    public void TrueIsBooleanTrueTest()
    {
        Assert.That(WitSqlValue.True.Type, Is.EqualTo(WitSqlType.Boolean));
        Assert.That(WitSqlValue.True.AsBool(), Is.True);
    }

    [Test]
    public void FalseIsBooleanFalseTest()
    {
        Assert.That(WitSqlValue.False.Type, Is.EqualTo(WitSqlType.Boolean));
        Assert.That(WitSqlValue.False.AsBool(), Is.False);
    }

    #endregion

    #region IsTrue and IsFalse Tests

    [Test]
    public void IsTrueForNullReturnsFalseTest()
    {
        Assert.That(WitSqlValue.Null.IsTrue, Is.False);
        Assert.That(WitSqlValue.Null.IsFalse, Is.True);
    }

    [Test]
    public void IsTrueForBooleanTrueReturnsTrueTest()
    {
        Assert.That(WitSqlValue.True.IsTrue, Is.True);
        Assert.That(WitSqlValue.True.IsFalse, Is.False);
    }

    [Test]
    public void IsTrueForBooleanFalseReturnsFalseTest()
    {
        Assert.That(WitSqlValue.False.IsTrue, Is.False);
        Assert.That(WitSqlValue.False.IsFalse, Is.True);
    }

    [Test]
    public void IsTrueForNonZeroIntegerReturnsTrueTest()
    {
        Assert.That(WitSqlValue.FromInt(1).IsTrue, Is.True);
        Assert.That(WitSqlValue.FromInt(42).IsTrue, Is.True);
        Assert.That(WitSqlValue.FromInt(-1).IsTrue, Is.True);
    }

    [Test]
    public void IsTrueForZeroIntegerReturnsFalseTest()
    {
        Assert.That(WitSqlValue.FromInt(0).IsTrue, Is.False);
        Assert.That(WitSqlValue.FromInt(0).IsFalse, Is.True);
    }

    [Test]
    public void IsTrueForNonZeroRealReturnsTrueTest()
    {
        Assert.That(WitSqlValue.FromReal(1.0).IsTrue, Is.True);
        Assert.That(WitSqlValue.FromReal(0.1).IsTrue, Is.True);
        Assert.That(WitSqlValue.FromReal(-0.5).IsTrue, Is.True);
    }

    [Test]
    public void IsTrueForZeroRealReturnsFalseTest()
    {
        Assert.That(WitSqlValue.FromReal(0.0).IsTrue, Is.False);
        Assert.That(WitSqlValue.FromReal(0.0).IsFalse, Is.True);
    }

    [Test]
    public void IsTrueForNonEmptyTextReturnsTrueTest()
    {
        Assert.That(WitSqlValue.FromText("hello").IsTrue, Is.True);
        Assert.That(WitSqlValue.FromText("1").IsTrue, Is.True);
        Assert.That(WitSqlValue.FromText("true").IsTrue, Is.True);
    }

    [Test]
    public void IsTrueForEmptyOrFalsyTextReturnsFalseTest()
    {
        Assert.That(WitSqlValue.FromText("").IsTrue, Is.False);
        Assert.That(WitSqlValue.FromText("0").IsTrue, Is.False);
        Assert.That(WitSqlValue.FromText("false").IsTrue, Is.False);
        Assert.That(WitSqlValue.FromText("FALSE").IsTrue, Is.False);
    }

    [Test]
    public void IsTrueForNonZeroDecimalReturnsTrueTest()
    {
        Assert.That(WitSqlValue.FromDecimal(1m).IsTrue, Is.True);
        Assert.That(WitSqlValue.FromDecimal(0.01m).IsTrue, Is.True);
    }

    [Test]
    public void IsTrueForZeroDecimalReturnsFalseTest()
    {
        Assert.That(WitSqlValue.FromDecimal(0m).IsTrue, Is.False);
    }

    [Test]
    public void IsTrueForBlobReturnsTrueTest()
    {
        // Non-null blobs are truthy
        Assert.That(WitSqlValue.FromBlob([1, 2, 3]).IsTrue, Is.True);
        Assert.That(WitSqlValue.FromBlob([]).IsTrue, Is.True); // Even empty blob is truthy (not null)
    }

    [Test]
    public void IsTrueForGuidReturnsTrueTest()
    {
        // Non-null guids are truthy
        Assert.That(WitSqlValue.FromGuid(Guid.NewGuid()).IsTrue, Is.True);
        Assert.That(WitSqlValue.FromGuid(Guid.Empty).IsTrue, Is.True); // Even empty guid is truthy (not null)
    }

    #endregion

    #region ToString Tests

    [Test]
    public void ToStringFormatsCorrectlyTest()
    {
        Assert.That(WitSqlValue.Null.ToString(), Is.EqualTo("NULL"));
        Assert.That(WitSqlValue.FromInt(42).ToString(), Is.EqualTo("Integer:42"));
        Assert.That(WitSqlValue.FromReal(3.14).ToString(), Is.EqualTo("Real:3.14"));
        Assert.That(WitSqlValue.FromText("hello").ToString(), Is.EqualTo("Text:hello"));
        Assert.That(WitSqlValue.True.ToString(), Is.EqualTo("Boolean:true"));
    }

    #endregion

    #region ToObject Tests

    [Test]
    public void ToObjectReturnsCorrectTypesTest()
    {
        Assert.That(WitSqlValue.Null.ToObject(), Is.Null);
        Assert.That(WitSqlValue.FromInt(42).ToObject(), Is.EqualTo(42L));
        Assert.That(WitSqlValue.FromReal(3.14).ToObject(), Is.EqualTo(3.14));
        Assert.That(WitSqlValue.FromText("hello").ToObject(), Is.EqualTo("hello"));
        Assert.That(WitSqlValue.True.ToObject(), Is.EqualTo(true));
        Assert.That(WitSqlValue.FromDecimal(123m).ToObject(), Is.EqualTo(123m));
    }

    #endregion
}

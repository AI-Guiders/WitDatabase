using NUnit.Framework;
using System.Data;

namespace OutWit.Database.AdoNet.Tests.Parameter;

/// <summary>
/// Tests for WitDbParameter.
/// </summary>
[TestFixture]
public class WitDbParameterTests
{
    #region Constructor Tests

    [Test]
    public void DefaultConstructorCreatesParameterTest()
    {
        var param = new WitDbParameter();

        Assert.That(param.ParameterName, Is.Empty);
        Assert.That(param.Value, Is.Null);
        Assert.That(param.DbType, Is.EqualTo(DbType.Object));
        Assert.That(param.Direction, Is.EqualTo(ParameterDirection.Input));
        Assert.That(param.IsNullable, Is.True);
    }

    [Test]
    public void ConstructorWithNameAndValueSetsPropertiesTest()
    {
        var param = new WitDbParameter("@id", 42);

        Assert.That(param.ParameterName, Is.EqualTo("@id"));
        Assert.That(param.Value, Is.EqualTo(42));
    }

    [Test]
    public void ConstructorWithNameAndTypeSetsPropertiesTest()
    {
        var param = new WitDbParameter("@name", DbType.String);

        Assert.That(param.ParameterName, Is.EqualTo("@name"));
        Assert.That(param.DbType, Is.EqualTo(DbType.String));
    }

    [Test]
    public void ConstructorWithNameTypeAndSizeSetsPropertiesTest()
    {
        var param = new WitDbParameter("@name", DbType.String, 100);

        Assert.That(param.ParameterName, Is.EqualTo("@name"));
        Assert.That(param.DbType, Is.EqualTo(DbType.String));
        Assert.That(param.Size, Is.EqualTo(100));
    }

    [Test]
    public void ConstructorWithSourceColumnSetsPropertyTest()
    {
        var param = new WitDbParameter("@name", DbType.String, 100, "NameColumn");

        Assert.That(param.SourceColumn, Is.EqualTo("NameColumn"));
    }

    #endregion

    #region DbType Inference Tests

    [TestCase(42, DbType.Int32)]
    [TestCase(42L, DbType.Int64)]
    [TestCase("text", DbType.String)]
    [TestCase(true, DbType.Boolean)]
    [TestCase(3.14f, DbType.Single)]
    [TestCase(3.14d, DbType.Double)]
    public void DbTypeIsInferredFromValueTest(object value, DbType expectedType)
    {
        var param = new WitDbParameter("@p", value);

        Assert.That(param.DbType, Is.EqualTo(expectedType));
    }

    [Test]
    public void DbTypeInferredFromDecimalTest()
    {
        var param = new WitDbParameter("@p", 123.45m);

        Assert.That(param.DbType, Is.EqualTo(DbType.Decimal));
    }

    [Test]
    public void DbTypeInferredFromDateTimeTest()
    {
        var param = new WitDbParameter("@p", DateTime.Now);

        Assert.That(param.DbType, Is.EqualTo(DbType.DateTime));
    }

    [Test]
    public void DbTypeInferredFromGuidTest()
    {
        var param = new WitDbParameter("@p", Guid.NewGuid());

        Assert.That(param.DbType, Is.EqualTo(DbType.Guid));
    }

    [Test]
    public void DbTypeInferredFromByteArrayTest()
    {
        var param = new WitDbParameter("@p", new byte[] { 1, 2, 3 });

        Assert.That(param.DbType, Is.EqualTo(DbType.Binary));
    }

    [Test]
    public void DbTypeInferredFromDateOnlyTest()
    {
        var param = new WitDbParameter("@p", DateOnly.FromDateTime(DateTime.Now));

        Assert.That(param.DbType, Is.EqualTo(DbType.Date));
    }

    [Test]
    public void DbTypeInferredFromTimeOnlyTest()
    {
        var param = new WitDbParameter("@p", TimeOnly.FromDateTime(DateTime.Now));

        Assert.That(param.DbType, Is.EqualTo(DbType.Time));
    }

    [Test]
    public void ExplicitDbTypeNotOverriddenByValueTest()
    {
        var param = new WitDbParameter("@p", DbType.String);
        param.Value = 42;

        Assert.That(param.DbType, Is.EqualTo(DbType.String));
    }

    #endregion

    #region ResetDbType Tests

    [Test]
    public void ResetDbTypeSetsToObjectTest()
    {
        var param = new WitDbParameter("@p", DbType.String);
        
        param.ResetDbType();

        Assert.That(param.DbType, Is.EqualTo(DbType.Object));
    }

    [Test]
    public void ResetDbTypeAllowsInferenceAgainTest()
    {
        var param = new WitDbParameter("@p", DbType.String);
        param.ResetDbType();
        param.Value = 42;

        Assert.That(param.DbType, Is.EqualTo(DbType.Int32));
    }

    #endregion

    #region Direction Tests

    [Test]
    public void DirectionDefaultIsInputTest()
    {
        var param = new WitDbParameter();

        Assert.That(param.Direction, Is.EqualTo(ParameterDirection.Input));
    }

    [Test]
    public void DirectionOutputThrowsTest()
    {
        var param = new WitDbParameter();

        Assert.Throws<NotSupportedException>(() => param.Direction = ParameterDirection.Output);
    }

    [Test]
    public void DirectionInputOutputThrowsTest()
    {
        var param = new WitDbParameter();

        Assert.Throws<NotSupportedException>(() => param.Direction = ParameterDirection.InputOutput);
    }

    [Test]
    public void DirectionReturnValueThrowsTest()
    {
        var param = new WitDbParameter();

        Assert.Throws<NotSupportedException>(() => param.Direction = ParameterDirection.ReturnValue);
    }

    #endregion

    #region Clone Tests

    [Test]
    public void CloneCreatesIdenticalParameterTest()
    {
        var original = new WitDbParameter("@name", DbType.String, 100, "SourceCol")
        {
            Value = "Test",
            IsNullable = false,
            Precision = 10,
            Scale = 2
        };

        var clone = original.Clone();

        Assert.That(clone.ParameterName, Is.EqualTo(original.ParameterName));
        Assert.That(clone.DbType, Is.EqualTo(original.DbType));
        Assert.That(clone.Size, Is.EqualTo(original.Size));
        Assert.That(clone.SourceColumn, Is.EqualTo(original.SourceColumn));
        Assert.That(clone.Value, Is.EqualTo(original.Value));
        Assert.That(clone.IsNullable, Is.EqualTo(original.IsNullable));
        Assert.That(clone.Precision, Is.EqualTo(original.Precision));
        Assert.That(clone.Scale, Is.EqualTo(original.Scale));
    }

    [Test]
    public void CloneIsIndependentTest()
    {
        var original = new WitDbParameter("@name", "Original");
        var clone = original.Clone();

        clone.Value = "Modified";

        Assert.That(original.Value, Is.EqualTo("Original"));
        Assert.That(clone.Value, Is.EqualTo("Modified"));
    }

    [Test]
    public void ICloneableCloneReturnsWitDbParameterTest()
    {
        var original = new WitDbParameter("@p", 42);
        var clone = ((ICloneable)original).Clone();

        Assert.That(clone, Is.InstanceOf<WitDbParameter>());
    }

    #endregion

    #region Null Value Tests

    [Test]
    public void NullValueIsHandledTest()
    {
        var param = new WitDbParameter("@p", null);

        Assert.That(param.Value, Is.Null);
        Assert.That(param.DbType, Is.EqualTo(DbType.Object));
    }

    [Test]
    public void DBNullValueIsHandledTest()
    {
        var param = new WitDbParameter("@p", DBNull.Value);

        Assert.That(param.Value, Is.EqualTo(DBNull.Value));
    }

    #endregion

    #region Size, Precision, Scale Tests

    [Test]
    public void SizeCanBeSetTest()
    {
        var param = new WitDbParameter();
        param.Size = 255;

        Assert.That(param.Size, Is.EqualTo(255));
    }

    [Test]
    public void PrecisionCanBeSetTest()
    {
        var param = new WitDbParameter();
        param.Precision = 18;

        Assert.That(param.Precision, Is.EqualTo(18));
    }

    [Test]
    public void ScaleCanBeSetTest()
    {
        var param = new WitDbParameter();
        param.Scale = 4;

        Assert.That(param.Scale, Is.EqualTo(4));
    }

    #endregion

    #region SourceColumn Tests

    [Test]
    public void SourceColumnDefaultIsEmptyTest()
    {
        var param = new WitDbParameter();

        Assert.That(param.SourceColumn, Is.Empty);
    }

    [Test]
    public void SourceColumnNullMappingDefaultIsFalseTest()
    {
        var param = new WitDbParameter();

        Assert.That(param.SourceColumnNullMapping, Is.False);
    }

    [Test]
    public void SourceVersionDefaultIsCurrentTest()
    {
        var param = new WitDbParameter();

        Assert.That(param.SourceVersion, Is.EqualTo(DataRowVersion.Current));
    }

    #endregion
}

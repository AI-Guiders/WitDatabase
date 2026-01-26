using NUnit.Framework;

namespace OutWit.Database.AdoNet.Tests.Exceptions;

/// <summary>
/// Tests for WitDbException.
/// </summary>
[TestFixture]
public class WitDbExceptionTests
{
    #region Constructor Tests

    [Test]
    public void DefaultConstructorCreatesExceptionTest()
    {
        var ex = new WitDbException();

        Assert.That(ex.Message, Is.Not.Empty);
        Assert.That(ex.WitErrorCode, Is.EqualTo(0));
    }

    [Test]
    public void ConstructorWithMessageSetsMessageTest()
    {
        var ex = new WitDbException("Test error message");

        Assert.That(ex.Message, Is.EqualTo("Test error message"));
    }

    [Test]
    public void ConstructorWithInnerExceptionSetsInnerTest()
    {
        var inner = new InvalidOperationException("Inner");
        var ex = new WitDbException("Outer", inner);

        Assert.That(ex.InnerException, Is.SameAs(inner));
    }

    [Test]
    public void ConstructorWithErrorCodeSetsCodeTest()
    {
        var ex = new WitDbException("Error", WitDbException.ERROR_SYNTAX);

        Assert.That(ex.WitErrorCode, Is.EqualTo(WitDbException.ERROR_SYNTAX));
        Assert.That(ex.ErrorCode, Is.EqualTo(WitDbException.ERROR_SYNTAX));
    }

    [Test]
    public void ConstructorWithErrorCodeAndInnerExceptionWorksTest()
    {
        var inner = new InvalidOperationException("Inner");
        var ex = new WitDbException("Error", WitDbException.ERROR_IO, inner);

        Assert.That(ex.WitErrorCode, Is.EqualTo(WitDbException.ERROR_IO));
        Assert.That(ex.InnerException, Is.SameAs(inner));
    }

    #endregion

    #region Error Constants Tests

    [Test]
    public void ErrorGeneralIsDefinedTest()
    {
        Assert.That(WitDbException.ERROR_GENERAL, Is.EqualTo(1));
    }

    [Test]
    public void ErrorSyntaxIsDefinedTest()
    {
        Assert.That(WitDbException.ERROR_SYNTAX, Is.EqualTo(2));
    }

    [Test]
    public void ErrorConstraintIsDefinedTest()
    {
        Assert.That(WitDbException.ERROR_CONSTRAINT, Is.EqualTo(3));
    }

    [Test]
    public void ErrorTypeIsDefinedTest()
    {
        Assert.That(WitDbException.ERROR_TYPE, Is.EqualTo(4));
    }

    [Test]
    public void ErrorIoIsDefinedTest()
    {
        Assert.That(WitDbException.ERROR_IO, Is.EqualTo(5));
    }

    [Test]
    public void ErrorTransactionIsDefinedTest()
    {
        Assert.That(WitDbException.ERROR_TRANSACTION, Is.EqualTo(6));
    }

    [Test]
    public void ErrorTimeoutIsDefinedTest()
    {
        Assert.That(WitDbException.ERROR_TIMEOUT, Is.EqualTo(7));
    }

    [Test]
    public void ErrorLockIsDefinedTest()
    {
        Assert.That(WitDbException.ERROR_LOCK, Is.EqualTo(8));
    }

    #endregion

    #region FromException Tests

    [Test]
    public void FromExceptionWrapsExceptionTest()
    {
        var original = new InvalidOperationException("Original error");

        var witEx = WitDbException.FromException(original);

        Assert.That(witEx.InnerException, Is.SameAs(original));
        Assert.That(witEx.Message, Is.EqualTo("Original error"));
    }

    [Test]
    public void FromExceptionReturnsWitDbExceptionUnchangedTest()
    {
        var original = new WitDbException("WitDb error", WitDbException.ERROR_SYNTAX);

        var result = WitDbException.FromException(original);

        Assert.That(result, Is.SameAs(original));
    }

    [Test]
    public void FromExceptionDetectsConstraintErrorTest()
    {
        var original = new InvalidOperationException("constraint violation occurred");

        var witEx = WitDbException.FromException(original);

        Assert.That(witEx.WitErrorCode, Is.EqualTo(WitDbException.ERROR_CONSTRAINT));
    }

    [Test]
    public void FromExceptionDetectsSyntaxErrorTest()
    {
        var original = new InvalidOperationException("syntax error near SELECT");

        var witEx = WitDbException.FromException(original);

        Assert.That(witEx.WitErrorCode, Is.EqualTo(WitDbException.ERROR_SYNTAX));
    }

    [Test]
    public void FromExceptionDetectsTypeErrorTest()
    {
        var original = new InvalidCastException("Cannot convert");

        var witEx = WitDbException.FromException(original);

        Assert.That(witEx.WitErrorCode, Is.EqualTo(WitDbException.ERROR_TYPE));
    }

    [Test]
    public void FromExceptionDetectsIoErrorTest()
    {
        var original = new IOException("File not found");

        var witEx = WitDbException.FromException(original);

        Assert.That(witEx.WitErrorCode, Is.EqualTo(WitDbException.ERROR_IO));
    }

    [Test]
    public void FromExceptionDetectsTimeoutErrorTest()
    {
        var original = new TimeoutException("Operation timed out");

        var witEx = WitDbException.FromException(original);

        Assert.That(witEx.WitErrorCode, Is.EqualTo(WitDbException.ERROR_TIMEOUT));
    }

    [Test]
    public void FromExceptionDefaultsToGeneralErrorTest()
    {
        var original = new ArgumentException("Some argument issue");

        var witEx = WitDbException.FromException(original);

        Assert.That(witEx.WitErrorCode, Is.EqualTo(WitDbException.ERROR_GENERAL));
    }

    #endregion

    #region Inheritance Tests

    [Test]
    public void InheritsFromDbExceptionTest()
    {
        var ex = new WitDbException("Test");

        Assert.That(ex, Is.InstanceOf<System.Data.Common.DbException>());
    }

    [Test]
    public void InheritsFromSystemExceptionTest()
    {
        var ex = new WitDbException("Test");

        Assert.That(ex, Is.InstanceOf<System.Exception>());
    }

    #endregion
}

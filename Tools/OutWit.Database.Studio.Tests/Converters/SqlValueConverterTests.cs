using System.Globalization;
using NUnit.Framework;
using OutWit.Database.Studio.Converters;

namespace OutWit.Database.Studio.Tests.Converters;

/// <summary>
/// Tests for SqlValueConverter.
/// </summary>
[TestFixture]
public class SqlValueConverterTests
{
    #region Fields

    private SqlValueConverter m_converter = null!;

    #endregion

    #region Setup

    [SetUp]
    public void Setup()
    {
        m_converter = new SqlValueConverter();
    }

    #endregion

    #region Null/DBNull Tests

    [Test]
    public void ConvertNullReturnsNullDisplayTextTest()
    {
        var result = m_converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.That(result, Is.EqualTo(SqlValueConverter.NULL_DISPLAY_TEXT));
    }

    [Test]
    public void ConvertDbNullReturnsNullDisplayTextTest()
    {
        var result = m_converter.Convert(DBNull.Value, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.That(result, Is.EqualTo(SqlValueConverter.NULL_DISPLAY_TEXT));
    }

    #endregion

    #region Byte Array Tests

    [Test]
    public void ConvertEmptyByteArrayReturnsEmptyIndicatorTest()
    {
        var result = m_converter.Convert(Array.Empty<byte>(), typeof(string), null, CultureInfo.InvariantCulture);

        Assert.That(result, Is.EqualTo("(empty)"));
    }

    [Test]
    public void ConvertSmallByteArrayReturnsHexStringTest()
    {
        var bytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

        var result = m_converter.Convert(bytes, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.That(result, Is.EqualTo("0xDEADBEEF"));
    }

    [Test]
    public void ConvertLargeByteArrayReturnsTruncatedHexWithSizeTest()
    {
        var bytes = new byte[32];
        for (var i = 0; i < bytes.Length; i++)
            bytes[i] = (byte)i;

        var result = m_converter.Convert(bytes, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.That(result, Does.StartWith("0x"));
        Assert.That(result, Does.EndWith("(32 bytes)"));
        Assert.That(result, Does.Contain("..."));
    }

    #endregion

    #region DateTime Tests

    [Test]
    public void ConvertDateTimeReturnsFormattedStringTest()
    {
        var dateTime = new DateTime(2025, 6, 15, 14, 30, 45);

        var result = m_converter.Convert(dateTime, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.That(result, Is.EqualTo("2025-06-15 14:30:45"));
    }

    [Test]
    public void ConvertDateOnlyReturnsFormattedStringTest()
    {
        var date = new DateOnly(2025, 6, 15);

        var result = m_converter.Convert(date, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.That(result, Is.EqualTo("2025-06-15"));
    }

    [Test]
    public void ConvertTimeOnlyReturnsFormattedStringTest()
    {
        var time = new TimeOnly(14, 30, 45);

        var result = m_converter.Convert(time, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.That(result, Is.EqualTo("14:30:45"));
    }

    [Test]
    public void ConvertTimeSpanReturnsFormattedStringTest()
    {
        var timeSpan = new TimeSpan(2, 30, 45);

        var result = m_converter.Convert(timeSpan, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.That(result, Is.EqualTo("02:30:45"));
    }

    #endregion

    #region Boolean Tests

    [Test]
    public void ConvertTrueReturnsTrueStringTest()
    {
        var result = m_converter.Convert(true, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.That(result, Is.EqualTo("true"));
    }

    [Test]
    public void ConvertFalseReturnsFalseStringTest()
    {
        var result = m_converter.Convert(false, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.That(result, Is.EqualTo("false"));
    }

    #endregion

    #region Other Types Tests

    [Test]
    public void ConvertStringReturnsOriginalValueTest()
    {
        var result = m_converter.Convert("Hello World", typeof(string), null, CultureInfo.InvariantCulture);

        Assert.That(result, Is.EqualTo("Hello World"));
    }

    [Test]
    public void ConvertIntegerReturnsOriginalValueTest()
    {
        var result = m_converter.Convert(42, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.That(result, Is.EqualTo(42));
    }

    [Test]
    public void ConvertDecimalReturnsOriginalValueTest()
    {
        var result = m_converter.Convert(123.45m, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.That(result, Is.EqualTo(123.45m));
    }

    #endregion

    #region ConvertBack Tests

    [Test]
    public void ConvertBackNullDisplayTextReturnsNullTest()
    {
        var result = m_converter.ConvertBack(SqlValueConverter.NULL_DISPLAY_TEXT, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void ConvertBackNonNullValueReturnsValueTest()
    {
        var result = m_converter.ConvertBack("Hello", typeof(string), null, CultureInfo.InvariantCulture);

        Assert.That(result, Is.EqualTo("Hello"));
    }

    #endregion
}

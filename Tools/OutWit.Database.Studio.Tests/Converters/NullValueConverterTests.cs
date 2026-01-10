using System.Globalization;
using NUnit.Framework;
using OutWit.Database.Studio.Converters;

namespace OutWit.Database.Studio.Tests.Converters;

/// <summary>
/// Tests for NullValueConverter.
/// </summary>
[TestFixture]
public class NullValueConverterTests
{
    #region Fields

    private NullValueConverter m_converter = null!;

    #endregion

    #region Setup

    [SetUp]
    public void Setup()
    {
        m_converter = new NullValueConverter();
    }

    #endregion

    #region Convert Tests

    [Test]
    public void ConvertNullReturnsNullDisplayTextTest()
    {
        // Act
        var result = m_converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.EqualTo(NullValueConverter.NULL_DISPLAY_TEXT));
    }

    [Test]
    public void ConvertEmptyStringReturnsNullDisplayTextTest()
    {
        // Act
        var result = m_converter.Convert("", typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.EqualTo(NullValueConverter.NULL_DISPLAY_TEXT));
    }

    [Test]
    public void ConvertNonEmptyStringReturnsOriginalValueTest()
    {
        // Act
        var result = m_converter.Convert("Hello", typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.EqualTo("Hello"));
    }

    [Test]
    public void ConvertNumberReturnsStringRepresentationTest()
    {
        // Act
        var result = m_converter.Convert(42, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.EqualTo("42"));
    }

    #endregion

    #region ConvertBack Tests

    [Test]
    public void ConvertBackNullDisplayTextReturnsNullTest()
    {
        // Act
        var result = m_converter.ConvertBack(NullValueConverter.NULL_DISPLAY_TEXT, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void ConvertBackNonNullValueReturnsValueTest()
    {
        // Act
        var result = m_converter.ConvertBack("Hello", typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.EqualTo("Hello"));
    }

    #endregion
}

/// <summary>
/// Tests for IsNullOrEmptyConverter.
/// </summary>
[TestFixture]
public class IsNullOrEmptyConverterTests
{
    #region Fields

    private IsNullOrEmptyConverter m_converter = null!;

    #endregion

    #region Setup

    [SetUp]
    public void Setup()
    {
        m_converter = new IsNullOrEmptyConverter();
    }

    #endregion

    #region Convert Tests

    [Test]
    public void ConvertNullReturnsTrueTest()
    {
        // Act
        var result = m_converter.Convert(null, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void ConvertEmptyStringReturnsTrueTest()
    {
        // Act
        var result = m_converter.Convert("", typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void ConvertNonEmptyStringReturnsFalseTest()
    {
        // Act
        var result = m_converter.Convert("Hello", typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void ConvertBackReturnsValueAsIsTest()
    {
        // Act
        var result = m_converter.ConvertBack(true, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.That(result, Is.EqualTo(true));
    }

    #endregion
}

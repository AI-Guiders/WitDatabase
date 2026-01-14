using NUnit.Framework;
using OutWit.Database.Studio.Themes;

namespace OutWit.Database.Studio.Tests.Themes;

/// <summary>
/// Tests for SqlEditorTheme functionality.
/// </summary>
[TestFixture]
public class SqlEditorThemeTests
{
    #region Color Tests

    [Test]
    public void BackgroundColorReturnsValidColorTest()
    {
        // Act
        var color = SqlEditorTheme.BackgroundColor;

        // Assert
        Assert.That(color.A, Is.GreaterThan(0));
    }

    [Test]
    public void ForegroundColorReturnsValidColorTest()
    {
        // Act
        var color = SqlEditorTheme.ForegroundColor;

        // Assert
        Assert.That(color.A, Is.GreaterThan(0));
    }

    [Test]
    public void LineNumbersColorReturnsValidColorTest()
    {
        // Act
        var color = SqlEditorTheme.LineNumbersColor;

        // Assert
        Assert.That(color.A, Is.GreaterThan(0));
    }

    #endregion

    #region Brush Tests

    [Test]
    public void BackgroundBrushReturnsNotNullTest()
    {
        // Act
        var brush = SqlEditorTheme.BackgroundBrush;

        // Assert
        Assert.That(brush, Is.Not.Null);
    }

    [Test]
    public void ForegroundBrushReturnsNotNullTest()
    {
        // Act
        var brush = SqlEditorTheme.ForegroundBrush;

        // Assert
        Assert.That(brush, Is.Not.Null);
    }

    [Test]
    public void LineNumbersBrushReturnsNotNullTest()
    {
        // Act
        var brush = SqlEditorTheme.LineNumbersBrush;

        // Assert
        Assert.That(brush, Is.Not.Null);
    }

    #endregion
}

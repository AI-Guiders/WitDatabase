using NUnit.Framework;
using OutWit.Database.Studio.Ui.Icons;

namespace OutWit.Database.Studio.Tests.Ui;

/// <summary>
/// Tests for StudioIcons path constants.
/// </summary>
[TestFixture]
public class StudioIconsTests
{
    #region Path Constants Tests

    [Test]
    public void AllPathConstantsAreNotEmptyTest()
    {
        // Assert - verify key path constants are defined
        Assert.That(StudioIcons.PATH_MENU_NEW_DATABASE, Is.Not.Null.And.Not.Empty);
        Assert.That(StudioIcons.PATH_MENU_OPEN_DATABASE, Is.Not.Null.And.Not.Empty);
        Assert.That(StudioIcons.PATH_MENU_CLOSE_DATABASE, Is.Not.Null.And.Not.Empty);
        Assert.That(StudioIcons.PATH_MENU_EXIT, Is.Not.Null.And.Not.Empty);
        Assert.That(StudioIcons.PATH_QUERY_EXECUTE, Is.Not.Null.And.Not.Empty);
        Assert.That(StudioIcons.PATH_QUERY_STOP, Is.Not.Null.And.Not.Empty);
        Assert.That(StudioIcons.PATH_DB_DATABASE, Is.Not.Null.And.Not.Empty);
        Assert.That(StudioIcons.PATH_DB_TABLE, Is.Not.Null.And.Not.Empty);
        Assert.That(StudioIcons.PATH_DB_VIEW, Is.Not.Null.And.Not.Empty);
        Assert.That(StudioIcons.PATH_DB_INDEX, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void ThemePathConstantsAreDefinedTest()
    {
        // Assert
        Assert.That(StudioIcons.PATH_THEME_DARK, Is.Not.Null.And.Not.Empty);
        Assert.That(StudioIcons.PATH_THEME_LIGHT, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void LinkPathConstantsAreDefinedTest()
    {
        // Assert
        Assert.That(StudioIcons.PATH_LINK_WEB, Is.Not.Null.And.Not.Empty);
        Assert.That(StudioIcons.PATH_LINK_GITHUB, Is.Not.Null.And.Not.Empty);
        Assert.That(StudioIcons.PATH_LINK_PERSON, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void TableEditorPathConstantsAreDefinedTest()
    {
        // Assert
        Assert.That(StudioIcons.PATH_TABLE_EDITOR_ADD_ROW, Is.Not.Null.And.Not.Empty);
        Assert.That(StudioIcons.PATH_TABLE_EDITOR_DELETE_ROW, Is.Not.Null.And.Not.Empty);
        Assert.That(StudioIcons.PATH_TABLE_EDITOR_COMMIT, Is.Not.Null.And.Not.Empty);
        Assert.That(StudioIcons.PATH_TABLE_EDITOR_ROLLBACK, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void WorkspaceTabPathConstantsAreDefinedTest()
    {
        // Assert
        Assert.That(StudioIcons.PATH_TAB_QUERY, Is.Not.Null.And.Not.Empty);
        Assert.That(StudioIcons.PATH_TAB_TABLE_EDIT, Is.Not.Null.And.Not.Empty);
        Assert.That(StudioIcons.PATH_TAB_STRUCTURE, Is.Not.Null.And.Not.Empty);
        Assert.That(StudioIcons.PATH_TAB_PIN, Is.Not.Null.And.Not.Empty);
        Assert.That(StudioIcons.PATH_TAB_UNPIN, Is.Not.Null.And.Not.Empty);
    }

    #endregion

    #region Path Format Tests

    [Test]
    public void PathConstantsAreValidSvgPathsTest()
    {
        // SVG paths should start with a letter command like M, L, C, etc.
        Assert.That(StudioIcons.PATH_QUERY_EXECUTE[0], Is.AnyOf('M', 'm', 'L', 'l', 'H', 'h', 'V', 'v', 'C', 'c', 'S', 's', 'Q', 'q', 'T', 't', 'A', 'a', 'Z', 'z'));
        Assert.That(StudioIcons.PATH_DB_TABLE[0], Is.AnyOf('M', 'm', 'L', 'l', 'H', 'h', 'V', 'v', 'C', 'c', 'S', 's', 'Q', 'q', 'T', 't', 'A', 'a', 'Z', 'z'));
    }

    #endregion
}

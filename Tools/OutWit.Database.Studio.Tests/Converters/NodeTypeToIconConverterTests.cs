using NUnit.Framework;
using OutWit.Database.Studio.Converters;
using OutWit.Database.Studio.Models;
using OutWit.Database.Studio.Ui.Icons;
using System.Globalization;

namespace OutWit.Database.Studio.Tests.Converters;

/// <summary>
/// Tests for <see cref="NodeTypeToIconConverter"/>.
/// </summary>
[TestFixture]
public class NodeTypeToIconConverterTests
{
    #region Fields

    private NodeTypeToIconConverter m_converter = null!;

    #endregion

    #region Setup

    [SetUp]
    public void Setup()
    {
        m_converter = new NodeTypeToIconConverter();
    }

    #endregion

    #region Convert Tests

    [Test]
    public void ConvertDatabaseNodeTypeReturnsDatabaseIconTest()
    {
        var result = m_converter.Convert(DatabaseNodeType.Database, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.TypeOf<string>());
        Assert.That(result, Is.EqualTo(StudioIcons.PATH_DB_DATABASE));
    }

    [Test]
    public void ConvertTablesFolderNodeTypeReturnsFolderIconTest()
    {
        var result = m_converter.Convert(DatabaseNodeType.TablesFolder, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.TypeOf<string>());
        Assert.That(result, Is.EqualTo(StudioIcons.PATH_COMMON_FOLDER));
    }

    [Test]
    public void ConvertTableNodeTypeReturnsTableIconTest()
    {
        var result = m_converter.Convert(DatabaseNodeType.Table, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.TypeOf<string>());
        Assert.That(result, Is.EqualTo(StudioIcons.PATH_DB_TABLE));
    }

    [Test]
    public void ConvertViewsFolderNodeTypeReturnsFolderIconTest()
    {
        var result = m_converter.Convert(DatabaseNodeType.ViewsFolder, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.TypeOf<string>());
        Assert.That(result, Is.EqualTo(StudioIcons.PATH_COMMON_FOLDER));
    }

    [Test]
    public void ConvertViewNodeTypeReturnsViewIconTest()
    {
        var result = m_converter.Convert(DatabaseNodeType.View, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.TypeOf<string>());
        Assert.That(result, Is.EqualTo(StudioIcons.PATH_DB_VIEW));
    }

    [Test]
    public void ConvertIndexesFolderNodeTypeReturnsFolderIconTest()
    {
        var result = m_converter.Convert(DatabaseNodeType.IndexesFolder, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.TypeOf<string>());
        Assert.That(result, Is.EqualTo(StudioIcons.PATH_COMMON_FOLDER));
    }

    [Test]
    public void ConvertIndexNodeTypeReturnsIndexIconTest()
    {
        var result = m_converter.Convert(DatabaseNodeType.Index, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.TypeOf<string>());
        Assert.That(result, Is.EqualTo(StudioIcons.PATH_DB_INDEX));
    }

    [Test]
    public void ConvertTriggersFolderNodeTypeReturnsFolderIconTest()
    {
        var result = m_converter.Convert(DatabaseNodeType.TriggersFolder, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.TypeOf<string>());
        Assert.That(result, Is.EqualTo(StudioIcons.PATH_COMMON_FOLDER));
    }

    [Test]
    public void ConvertTriggerNodeTypeReturnsTriggerIconTest()
    {
        var result = m_converter.Convert(DatabaseNodeType.Trigger, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.TypeOf<string>());
        Assert.That(result, Is.EqualTo(StudioIcons.PATH_DB_TRIGGER));
    }

    [Test]
    public void ConvertSequencesFolderNodeTypeReturnsFolderIconTest()
    {
        var result = m_converter.Convert(DatabaseNodeType.SequencesFolder, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.TypeOf<string>());
        Assert.That(result, Is.EqualTo(StudioIcons.PATH_COMMON_FOLDER));
    }

    [Test]
    public void ConvertSequenceNodeTypeReturnsSequenceIconTest()
    {
        var result = m_converter.Convert(DatabaseNodeType.Sequence, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.TypeOf<string>());
        Assert.That(result, Is.EqualTo(StudioIcons.PATH_DB_SEQUENCE));
    }

    [Test]
    public void ConvertNullValueReturnsDefaultIconTest()
    {
        var result = m_converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.TypeOf<string>());
        Assert.That(result, Is.EqualTo(StudioIcons.PATH_DB_DATABASE));
    }

    [Test]
    public void ConvertBackThrowsNotImplementedExceptionTest()
    {
        Assert.Throws<NotImplementedException>(() => 
            m_converter.ConvertBack(StudioIcons.PATH_DB_TABLE, typeof(DatabaseNodeType), null, CultureInfo.InvariantCulture));
    }

    #endregion
}

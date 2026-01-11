using Avalonia.Data.Converters;
using OutWit.Database.Studio.Models;
using OutWit.Database.Studio.Ui.Icons;
using System.Globalization;

namespace OutWit.Database.Studio.Converters;

/// <summary>
/// Converts DatabaseNodeType to SVG path data string from StudioIcons.
/// Returns string that PathIcon can parse via its built-in converter.
/// </summary>
public class NodeTypeToIconConverter : IValueConverter
{
    #region IValueConverter

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DatabaseNodeType nodeType)
            return StudioIcons.PATH_DB_DATABASE;

        return nodeType switch
        {
            DatabaseNodeType.Database => StudioIcons.PATH_DB_DATABASE,
            DatabaseNodeType.TablesFolder => StudioIcons.PATH_COMMON_FOLDER,
            DatabaseNodeType.Table => StudioIcons.PATH_DB_TABLE,
            DatabaseNodeType.ViewsFolder => StudioIcons.PATH_COMMON_FOLDER,
            DatabaseNodeType.View => StudioIcons.PATH_DB_VIEW,
            DatabaseNodeType.IndexesFolder => StudioIcons.PATH_COMMON_FOLDER,
            DatabaseNodeType.Index => StudioIcons.PATH_DB_INDEX,
            DatabaseNodeType.TriggersFolder => StudioIcons.PATH_COMMON_FOLDER,
            DatabaseNodeType.Trigger => StudioIcons.PATH_DB_TRIGGER,
            DatabaseNodeType.SequencesFolder => StudioIcons.PATH_COMMON_FOLDER,
            DatabaseNodeType.Sequence => StudioIcons.PATH_DB_SEQUENCE,
            _ => StudioIcons.PATH_DB_DATABASE
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    #endregion
}

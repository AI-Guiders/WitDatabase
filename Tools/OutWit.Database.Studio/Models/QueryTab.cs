using OutWit.Common.Abstract;
using System.Data;

namespace OutWit.Database.Studio.Models;

/// <summary>
/// Represents a query editor tab with its content and state.
/// </summary>
public class QueryTab : ModelBase
{
    #region Model Base

    public override bool Is(ModelBase modelBase, double tolerance = 1E-07)
    {
        if (modelBase is not QueryTab other)
            return false;

        return Id == other.Id
            && Title == other.Title
            && SqlText == other.SqlText
            && FilePath == other.FilePath;
    }

    public override QueryTab Clone()
    {
        return new QueryTab
        {
            Id = Id,
            Title = Title,
            SqlText = SqlText,
            FilePath = FilePath,
            IsModified = IsModified
        };
    }

    #endregion

    #region Properties

    /// <summary>
    /// Unique identifier for the tab.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Display title of the tab.
    /// </summary>
    public string Title { get; set; } = "New Query";

    /// <summary>
    /// SQL text content of the query.
    /// </summary>
    public string SqlText { get; set; } = string.Empty;

    /// <summary>
    /// File path if the query is saved to a file.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Indicates if the tab has unsaved changes.
    /// </summary>
    public bool IsModified { get; set; }

    /// <summary>
    /// Gets the display title with modification indicator.
    /// </summary>
    public string DisplayTitle => IsModified ? $"{Title} *" : Title;

    /// <summary>
    /// Result data view from query execution.
    /// </summary>
    public DataView? ResultDataView { get; set; }

    /// <summary>
    /// Error message from query execution.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Number of rows affected by the query.
    /// </summary>
    public int RowsAffected { get; set; }

    /// <summary>
    /// Execution time in milliseconds.
    /// </summary>
    public double ExecutionTimeMs { get; set; }

    /// <summary>
    /// Gets whether the tab has results to display.
    /// </summary>
    public bool HasResults => ResultDataView != null && ResultDataView.Count > 0;

    /// <summary>
    /// Gets whether the query execution was successful.
    /// </summary>
    public bool IsSuccess => string.IsNullOrEmpty(ErrorMessage);

    /// <summary>
    /// Gets whether there are messages to display.
    /// </summary>
    public bool HasMessages => !string.IsNullOrEmpty(ErrorMessage) || RowsAffected > 0;

    #endregion
}

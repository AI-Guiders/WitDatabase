using OutWit.Common.Aspects;
using OutWit.Common.MVVM.ViewModels;
using OutWit.Database.Studio.ViewModels;

namespace OutWit.Database.Studio.Models;

/// <summary>
/// Column mapping for import operations.
/// </summary>
public class ColumnMapping : ViewModelBase<ApplicationViewModel>
{
    #region Constructors

    public ColumnMapping(ApplicationViewModel appVm, string sourceColumn) 
        : base(appVm)
    {
        SourceColumn = sourceColumn;
    }

    #endregion

    #region Properties

    [Notify]
    public string SourceColumn { get; set; }

    [Notify]
    public string? TargetColumn { get; set; }

    [Notify]
    public bool IsIncluded { get; set; } = true;

    #endregion
}

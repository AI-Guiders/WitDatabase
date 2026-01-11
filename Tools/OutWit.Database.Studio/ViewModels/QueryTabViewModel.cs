using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using OutWit.Common.Aspects;
using OutWit.Common.Locker;
using OutWit.Common.MVVM.Commands;
using OutWit.Common.MVVM.ViewModels;
using OutWit.Common.Utils;
using OutWit.Database.Studio.Services;

namespace OutWit.Database.Studio.ViewModels;

/// <summary>
/// Represents a query editor tab with its content and state.
/// </summary>
public class QueryTabViewModel : ViewModelBase<ApplicationViewModel>
{
    #region Constructors

    public QueryTabViewModel(ApplicationViewModel applicationViewModel)
        : base(applicationViewModel)
    {
        InitCommands();
        InitEvents();
    }

    #endregion

    #region Initialization

    private void InitCommands()
    {
        CopyRowsCommand = new RelayCommandAsync(CopyRowsAsync);
        CopyRowsAsInsertCommand = new RelayCommandAsync(CopyRowsAsInsertAsync);
        CopyRowsAsCsvCommand = new RelayCommandAsync(CopyRowsAsCsvAsync);
        CopyAllRowsCommand = new RelayCommandAsync(CopyAllRowsAsync);
        CopyAllRowsAsInsertCommand = new RelayCommandAsync(CopyAllRowsAsInsertAsync);
    }

    private void InitEvents()
    {
        PropertyChanged += OnPropertyChanged;
    }

    #endregion

    #region Functions

    /// <summary>
    /// Sets the result data for display.
    /// </summary>
    public void SetResultData(DataTable? data)
    {
        using var locker = GlobalLocker.Lock(nameof(QueryTabViewModel));

        ResultData = data;

        if (data == null || data.Rows.Count == 0)
        {
            TotalRowCount = 0;
            CurrentView = null;
            return;
        }

        TotalRowCount = data.Rows.Count;
        CurrentView = new DataView(data);

        UpdateStatus();
    }

    /// <summary>
    /// Clears all results.
    /// </summary>
    public void ClearResults()
    {
        using var locker = GlobalLocker.Lock(nameof(QueryTabViewModel));

        ResultData?.Dispose();
        ResultData = null;
        CurrentView = null;
        TotalRowCount = 0;
        RowsAffected = 0;
        ExecutionTimeMs = 0;
        ErrorMessage = null;
        SelectedRows = null;

        UpdateStatus();
    }

    private async Task CopyRowsAsync()
    {
        if (!CanCopyRows)
            return;

        var rows = GetSelectedOrVisibleRows();
        var csv = Export.RowsToCsv(rows, ResultData!, includeHeaders: false);
        await SetClipboardTextAsync(csv);
    }

    private async Task CopyRowsAsCsvAsync()
    {
        if (!CanCopyRows)
            return;

        var rows = GetSelectedOrVisibleRows();
        var csv = Export.RowsToCsv(rows, ResultData!, includeHeaders: true);
        await SetClipboardTextAsync(csv);
    }

    private async Task CopyRowsAsInsertAsync()
    {
        if (!CanCopyRows)
            return;

        var rows = GetSelectedOrVisibleRows();
        var sql = Export.RowsToInsertStatements(rows, ResultData!, "TableName");
        await SetClipboardTextAsync(sql);
    }

    private async Task CopyAllRowsAsync()
    {
        if (!HasResults || ResultData == null)
            return;

        var csv = Export.ToCsv(ResultData, includeHeaders: true);
        await SetClipboardTextAsync(csv);
    }

    private async Task CopyAllRowsAsInsertAsync()
    {
        if (!HasResults || ResultData == null)
            return;

        var sql = Export.ToInsertStatements(ResultData, "TableName");
        await SetClipboardTextAsync(sql);
    }

    private async Task SetClipboardTextAsync(string text)
    {
        var mainWindow = ApplicationVm.MainWindow;
        if (mainWindow == null)
            return;

        var clipboard = TopLevel.GetTopLevel(mainWindow)?.Clipboard;
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    private IEnumerable<DataRowView> GetSelectedOrVisibleRows()
    {
        if (SelectedRows != null && SelectedRows.Count > 0)
            return SelectedRows.Cast<DataRowView>();

        if (CurrentView != null)
            return CurrentView.Cast<DataRowView>();

        return [];
    }

    #endregion

    #region Tools

    private void UpdateStatus()
    {
        HasResults = TotalRowCount > 0;
        IsSuccess = string.IsNullOrEmpty(ErrorMessage);
        HasMessages = !string.IsNullOrEmpty(ErrorMessage) || RowsAffected > 0;

        DisplayTitle = IsModified ? $"{Title} *" : Title;
        
        var selectedCount = SelectedRows?.Count ?? 0;
        CanCopyRows = HasResults && (selectedCount > 0 || CurrentView?.Count > 0);
    }

    #endregion

    #region Event Handlers

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (GlobalLocker.IsLocked(nameof(QueryTabViewModel)))
            return;

        if (e.IsProperty((QueryTabViewModel vm) => vm.TotalRowCount))
            UpdateStatus();

        if (e.IsProperty((QueryTabViewModel vm) => vm.ErrorMessage))
            UpdateStatus();

        if (e.IsProperty((QueryTabViewModel vm) => vm.RowsAffected))
            UpdateStatus();

        if (e.IsProperty((QueryTabViewModel vm) => vm.IsModified))
            UpdateStatus();

        if (e.IsProperty((QueryTabViewModel vm) => vm.Title))
            UpdateStatus();

        if (e.IsProperty((QueryTabViewModel vm) => vm.CurrentView))
            UpdateStatus();

        if (e.IsProperty((QueryTabViewModel vm) => vm.SelectedRows))
            UpdateStatus();
    }

    #endregion

    #region Properties

    /// <summary>
    /// Display title of the tab.
    /// </summary>
    [Notify]
    public string Title { get; set; } = "New Query";

    /// <summary>
    /// SQL text content of the query.
    /// </summary>
    [Notify]
    public string SqlText { get; set; } = string.Empty;

    /// <summary>
    /// Currently selected text in the SQL editor.
    /// </summary>
    [Notify]
    public string? SelectedText { get; set; }

    /// <summary>
    /// File path if the query is saved to a file.
    /// </summary>
    [Notify]
    public string? FilePath { get; set; }

    /// <summary>
    /// Indicates if the tab has unsaved changes.
    /// </summary>
    [Notify]
    public bool IsModified { get; set; }

    /// <summary>
    /// Gets the display title with modification indicator.
    /// </summary>
    [Notify]
    public string DisplayTitle { get; private set; } = "";

    /// <summary>
    /// Full result data as DataTable.
    /// </summary>
    [Notify]
    public DataTable? ResultData { get; private set; }

    /// <summary>
    /// Current view for display (supports sorting).
    /// </summary>
    [Notify]
    public DataView? CurrentView { get; set; }

    /// <summary>
    /// Error message from query execution.
    /// </summary>
    [Notify]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Number of rows affected by the query.
    /// </summary>
    [Notify]
    public int RowsAffected { get; set; }

    /// <summary>
    /// Execution time in milliseconds.
    /// </summary>
    [Notify]
    public double ExecutionTimeMs { get; set; }

    /// <summary>
    /// Total number of rows in the result set.
    /// </summary>
    [Notify]
    public int TotalRowCount { get; set; }

    /// <summary>
    /// Gets whether the tab has results to display.
    /// </summary>
    [Notify]
    public bool HasResults { get; private set; }

    /// <summary>
    /// Gets whether the query execution was successful.
    /// </summary>
    [Notify]
    public bool IsSuccess { get; private set; }

    /// <summary>
    /// Gets whether there are messages to display.
    /// </summary>
    [Notify]
    public bool HasMessages { get; private set; }
    
    /// <summary>
    /// Gets whether rows can be copied.
    /// </summary>
    [Notify]
    public bool CanCopyRows { get; private set; }

    /// <summary>
    /// Currently selected rows in the DataGrid.
    /// </summary>
    [Notify]
    public IList? SelectedRows { get; set; }

    #endregion

    #region Commands

    public ICommand CopyRowsCommand { get; private set; } = null!;
    
    public ICommand CopyRowsAsCsvCommand { get; private set; } = null!;
    
    public ICommand CopyRowsAsInsertCommand { get; private set; } = null!;
    
    public ICommand CopyAllRowsCommand { get; private set; } = null!;
    
    public ICommand CopyAllRowsAsInsertCommand { get; private set; } = null!;

    #endregion

    #region Services

    private IExportService Export => ApplicationVm.Export;

    #endregion
}

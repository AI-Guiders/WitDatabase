using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using OutWit.Common.Aspects;
using OutWit.Common.Locker;
using OutWit.Common.MVVM.Commands;
using OutWit.Common.MVVM.Table;
using OutWit.Common.MVVM.ViewModels;
using OutWit.Common.Utils;

namespace OutWit.Database.Studio.ViewModels;

/// <summary>
/// Represents a query editor tab with its content and state.
/// </summary>
public class QueryTabViewModel : ViewModelBase<ApplicationViewModel>
{
    #region Constants

    private const int DEFAULT_PAGE_SIZE = 100;
    private const string NULL_DISPLAY = "NULL";

    #endregion

    #region Constructors

    public QueryTabViewModel(ApplicationViewModel applicationViewModel)
        : base(applicationViewModel)
    {
        InitDefaults();
        InitCommands();
        InitEvents();
    }

    #endregion

    #region Initialization

    private void InitDefaults()
    {
        PageSize = DEFAULT_PAGE_SIZE;
    }

    private void InitCommands()
    {
        FirstPageCommand = new RelayCommand(GoToFirstPage);
        PreviousPageCommand = new RelayCommand(GoToPreviousPage);
        NextPageCommand = new RelayCommand(GoToNextPage);
        LastPageCommand = new RelayCommand(GoToLastPage);
        
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

    #region Pagination Handlers

    private void GoToFirstPage()
    {
        if (!CanGoToPreviousPage) 
            return;

        using var locker = GlobalLocker.Lock(nameof(QueryTabViewModel));

        CurrentPage = 1;
        ApplyPagination();
        UpdateStatus();
    }

    private void GoToPreviousPage()
    {
        if (!CanGoToPreviousPage) 
            return;

        using var locker = GlobalLocker.Lock(nameof(QueryTabViewModel));

        CurrentPage--;
        ApplyPagination();
        UpdateStatus();
    }

    private void GoToNextPage()
    {
        if (!CanGoToNextPage)
            return;

        using var locker = GlobalLocker.Lock(nameof(QueryTabViewModel));

        CurrentPage++;
        ApplyPagination();
        UpdateStatus();
    }

    private void GoToLastPage()
    {
        if (!CanGoToNextPage) 
            return;

        using var locker = GlobalLocker.Lock(nameof(QueryTabViewModel));

        CurrentPage = TotalPages;
        ApplyPagination();
        UpdateStatus();
    }

    #endregion

    #region Copy Handlers

    private async Task CopyRowsAsync()
    {
        if (!CanCopyRows)
            return;

        var csv = RowsToCsv(GetSelectedOrAllRows(), includeHeaders: false);
        await SetClipboardTextAsync(csv);
    }

    private async Task CopyRowsAsCsvAsync()
    {
        if (!CanCopyRows)
            return;

        var csv = RowsToCsv(GetSelectedOrAllRows(), includeHeaders: true);
        await SetClipboardTextAsync(csv);
    }

    private async Task CopyRowsAsInsertAsync()
    {
        if (!CanCopyRows)
            return;

        var sql = RowsToInsertStatements(GetSelectedOrAllRows());
        await SetClipboardTextAsync(sql);
    }

    private async Task CopyAllRowsAsync()
    {
        if (!HasResults || ResultTable == null)
            return;

        var allRows = ResultTable.Pages.SelectMany(p => p.Rows).ToList();
        var csv = RowsToCsv(allRows, includeHeaders: true);
        await SetClipboardTextAsync(csv);
    }

    private async Task CopyAllRowsAsInsertAsync()
    {
        if (!HasResults || ResultTable == null)
            return;

        var allRows = ResultTable.Pages.SelectMany(p => p.Rows).ToList();
        var sql = RowsToInsertStatements(allRows);
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

    #endregion

    #region Functions

    /// <summary>
    /// Sets the full result data and applies pagination.
    /// </summary>
    public void SetResultData(TableView? data)
    {
        using var locker = GlobalLocker.Lock(nameof(QueryTabViewModel));

        ResultTable = data;

        if (data == null || data.Pages.Count == 0)
        {
            TotalRowCount = 0;
            CurrentPage = 1;
            HeaderRow = null;
            ResultPage = null;
            return;
        }

        HeaderRow = data.HeaderRow;
        TotalRowCount = data.Pages.Sum(page => page.Rows.Count);

        ResetPagination();
        UpdateStatus();
    }

    /// <summary>
    /// Applies pagination to show the current page of results.
    /// </summary>
    private void ApplyPagination()
    {
        if (ResultTable == null || ResultTable.Pages.Count == 0)
        {
            ResultPage = null;
            return;
        }

        var allRows = ResultTable.Pages.SelectMany(page => page.Rows).ToList();
        var startIndex = (CurrentPage - 1) * PageSize;
        var endIndex = Math.Min(startIndex + PageSize, allRows.Count);

        var page = new TableViewPage(CurrentPage);
        for (var i = startIndex; i < endIndex; i++)
        {
            page.Add(allRows[i]);
        }

        ResultPage = page;
    }

    /// <summary>
    /// Clears all results and resets pagination.
    /// </summary>
    public void ClearResults()
    {
        using var locker = GlobalLocker.Lock(nameof(QueryTabViewModel));

        ResultTable = null;
        HeaderRow = null;
        ResultPage = null;
        TotalRowCount = 0;
        CurrentPage = 1;
        RowsAffected = 0;
        ExecutionTimeMs = 0;
        ErrorMessage = null;
        SelectedRows = null;

        UpdateStatus();
    }

    private IReadOnlyList<TableViewRow> GetSelectedOrAllRows()
    {
        if (SelectedRows != null && SelectedRows.Count > 0)
            return SelectedRows.ToList();

        if (ResultPage != null)
            return ResultPage.Rows.ToList();

        return [];
    }

    private void ResetPagination()
    {
        using var locker = GlobalLocker.Lock(nameof(QueryTabViewModel));

        CurrentPage = 1;
        ApplyPagination();
    }

    #endregion

    #region Export Functions

    private string RowsToCsv(IReadOnlyList<TableViewRow> rows, bool includeHeaders)
    {
        if (rows.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();

        // Headers
        if (includeHeaders && HeaderRow != null)
        {
            var headers = HeaderRow.Values.Select(v => EscapeCsvField(v.Text ?? ""));
            sb.AppendLine(string.Join(",", headers));
        }

        // Rows
        foreach (var row in rows)
        {
            var values = row.Values.Select(v => EscapeCsvField(v.Text ?? ""));
            sb.AppendLine(string.Join(",", values));
        }

        return sb.ToString();
    }

    private string RowsToInsertStatements(IReadOnlyList<TableViewRow> rows)
    {
        if (rows.Count == 0 || HeaderRow == null)
            return string.Empty;

        var sb = new StringBuilder();
        var tableName = "TableName"; // Placeholder since we don't know the table name from SELECT results
        var columns = string.Join(", ", HeaderRow.Values.Select(v => v.Text ?? ""));

        foreach (var row in rows)
        {
            var values = row.Values.Select(v => FormatSqlValue(v.Text));
            sb.AppendLine($"INSERT INTO {tableName} ({columns}) VALUES ({string.Join(", ", values)});");
        }

        return sb.ToString();
    }

    private static string EscapeCsvField(string? field)
    {
        if (string.IsNullOrEmpty(field))
            return string.Empty;

        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }

    private static string FormatSqlValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return NULL_DISPLAY;

        // Check if it's a number
        if (double.TryParse(value, out _))
            return value;

        // Escape single quotes and wrap in quotes
        return $"'{value.Replace("'", "''")}'";
    }

    #endregion

    #region Tools

    private void UpdateStatus()
    {
        TotalPages = TotalRowCount > 0 
            ? (int)Math.Ceiling((double)TotalRowCount / PageSize) : 1;

        CanGoToPreviousPage = CurrentPage > 1;
        CanGoToNextPage = CurrentPage < TotalPages;

        DisplayedRowStart = TotalRowCount > 0 
            ? (CurrentPage - 1) * PageSize + 1
            : 0;

        DisplayedRowEnd = TotalRowCount > 0 
            ? Math.Min(CurrentPage * PageSize, TotalRowCount) 
            : 0;

        HasResults = TotalRowCount > 0;
        IsSuccess = string.IsNullOrEmpty(ErrorMessage);
        HasMessages = !string.IsNullOrEmpty(ErrorMessage) || RowsAffected > 0;

        DisplayTitle = IsModified ? $"{Title} *" : Title;
        
        var selectedCount = SelectedRows?.Count ?? 0;
        CanCopyRows = HasResults && (selectedCount > 0 || ResultPage?.Rows.Count > 0);
    }

    #endregion

    #region Event Handlers

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if(GlobalLocker.IsLocked(nameof(QueryTabViewModel)))
            return;

        if (e.IsProperty((QueryTabViewModel vm)=>vm.CurrentPage))
            UpdateStatus();

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

        if (e.IsProperty((QueryTabViewModel vm) => vm.ResultPage))
            UpdateStatus();

        if (e.IsProperty((QueryTabViewModel vm) => vm.SelectedRows))
            UpdateStatus();

        if (e.IsProperty((QueryTabViewModel vm) => vm.PageSize))
        {
            ResetPagination();
            UpdateStatus();
        }
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
    /// Header row with column names.
    /// </summary>
    [Notify]
    public TableViewRow? HeaderRow { get; set; }

    /// <summary>
    /// Current page of results for display.
    /// </summary>
    [Notify]
    public TableViewPage? ResultPage { get; set; }

    /// <summary>
    /// Result table with query results.
    /// </summary>
    [Notify]
    public TableView? ResultTable { get; private set; }

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
    public ObservableCollection<TableViewRow>? SelectedRows { get; set; }

    #endregion

    #region Pagination Properties

    /// <summary>
    /// Current page number (1-based).
    /// </summary>
    [Notify]
    public int CurrentPage { get; set; }

    /// <summary>
    /// Number of rows per page.
    /// </summary>
    [Notify]
    public int PageSize { get; set; }

    /// <summary>
    /// Total number of rows in the result set.
    /// </summary>
    [Notify]
    public int TotalRowCount { get; set; }

    /// <summary>
    /// Total number of pages.
    /// </summary>
    [Notify]
    public int TotalPages { get; private set; }

    /// <summary>
    /// First row number displayed (1-based).
    /// </summary>
    [Notify]
    public int DisplayedRowStart { get; private set; }

    /// <summary>
    /// Last row number displayed (1-based).
    /// </summary>
    [Notify]
    public int DisplayedRowEnd { get; private set; }

    /// <summary>
    /// Whether navigation to previous page is available.
    /// </summary>
    [Notify]
    public bool CanGoToPreviousPage { get; private set; }

    /// <summary>
    /// Whether navigation to next page is available.
    /// </summary>
    [Notify]
    public bool CanGoToNextPage { get; private set; }

    #endregion

    #region Commands

    public ICommand FirstPageCommand { get; private set; } = null!;

    public ICommand PreviousPageCommand { get; private set; } = null!;

    public ICommand NextPageCommand { get; private set; } = null!;

    public ICommand LastPageCommand { get; private set; } = null!;
    
    public ICommand CopyRowsCommand { get; private set; } = null!;
    
    public ICommand CopyRowsAsCsvCommand { get; private set; } = null!;
    
    public ICommand CopyRowsAsInsertCommand { get; private set; } = null!;
    
    public ICommand CopyAllRowsCommand { get; private set; } = null!;
    
    public ICommand CopyAllRowsAsInsertCommand { get; private set; } = null!;

    #endregion
}

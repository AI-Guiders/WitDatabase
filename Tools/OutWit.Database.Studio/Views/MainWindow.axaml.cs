using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Media;
using OutWit.Database.Studio.ViewModels;

namespace OutWit.Database.Studio.Views;

/// <summary>
/// Main application window.
/// </summary>
public partial class MainWindow : Window
{
    #region Constructors

    public MainWindow()
    {
        DataContext = ApplicationViewModel
            .Instance
            .ResetOwnerWindow(this);

        InitializeComponent();
        
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    #endregion

    #region Event Handlers

    private async void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var settings = await ApplicationViewModel.Instance.Settings.LoadAsync();
            
            Width = settings.WindowWidth;
            Height = settings.WindowHeight;
            
            if (settings.WindowState == "Maximized")
            {
                WindowState = WindowState.Maximized;
            }
            
            await ApplicationViewModel.Instance.MainWindowVm.InitializeAsync();
            
            ApplicationViewModel.Instance.MainWindowVm.RecentFiles.CollectionChanged += OnRecentFilesChanged;
            UpdateRecentFilesMenu();
            UpdateRecentFilesList();
        }
        catch
        {
            // Use defaults on error
        }
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        try
        {
            var state = WindowState == WindowState.Maximized ? "Maximized" : "Normal";
            var width = WindowState == WindowState.Normal ? Width : 1200;
            var height = WindowState == WindowState.Normal ? Height : 800;
            
            await ApplicationViewModel.Instance.MainWindowVm.SaveWindowStateAsync(width, height, state);
        }
        catch
        {
            // Ignore save errors on close
        }
    }

    private void OnRecentFilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateRecentFilesMenu();
        UpdateRecentFilesList();
    }

    #endregion

    #region Functions

    private void UpdateRecentFilesMenu()
    {
        RecentFilesMenu.Items.Clear();
        
        var recentFiles = ApplicationViewModel.Instance.MainWindowVm.RecentFiles;
        
        foreach (var file in recentFiles)
        {
            var menuItem = new MenuItem
            {
                Header = file.FileName,
                Command = ApplicationViewModel.Instance.MainWindowVm.OpenRecentCommand,
                CommandParameter = file.FilePath
            };
            menuItem.SetValue(ToolTip.TipProperty, file.FilePath);
            
            RecentFilesMenu.Items.Add(menuItem);
        }
    }

    private void UpdateRecentFilesList()
    {
        RecentFilesList.Children.Clear();
        
        var recentFiles = ApplicationViewModel.Instance.MainWindowVm.RecentFiles;
        
        foreach (var file in recentFiles)
        {
            var border = new Border
            {
                Padding = new Avalonia.Thickness(8, 6),
                CornerRadius = new Avalonia.CornerRadius(4),
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                Background = Brushes.Transparent
            };
            
            var stack = new StackPanel { Orientation = Avalonia.Layout.Orientation.Vertical, Spacing = 2 };
            
            stack.Children.Add(new TextBlock 
            { 
                Text = file.FileName,
                FontWeight = FontWeight.Medium
            });
            
            stack.Children.Add(new TextBlock 
            { 
                Text = file.Directory,
                FontSize = 11,
                Foreground = Brushes.Gray,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            
            border.Child = stack;
            border.SetValue(ToolTip.TipProperty, file.FilePath);
            
            // Capture file path for click handler
            var filePath = file.FilePath;
            
            // Hover effect
            border.PointerEntered += (_, _) => border.Background = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0));
            border.PointerExited += (_, _) => border.Background = Brushes.Transparent;
            
            // Click handler
            border.PointerPressed += (_, _) =>
            {
                ApplicationViewModel.Instance.MainWindowVm.OpenRecentCommand.Execute(filePath);
            };
            
            RecentFilesList.Children.Add(border);
        }
    }

    #endregion
}
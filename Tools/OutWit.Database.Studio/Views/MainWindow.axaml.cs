using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using OutWit.Database.Studio.Ui.Icons;
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
        ThemeToggleButton.Click += OnThemeToggleClick;
        
        if (Application.Current != null)
        {
            Application.Current.ActualThemeVariantChanged += OnThemeChanged;
        }
    }

    #endregion

    #region Event Handlers

    private async void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var settings = await ApplicationViewModel.Instance.Settings.LoadAsync();
            
            App.Current?.ApplyTheme(settings.Theme);
            UpdateThemeButton();
            
            // Apply window size
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

    private async void OnThemeToggleClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var isDark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
        var newTheme = isDark ? "Light" : "Dark";
        
        App.Current?.ApplyTheme(newTheme);
        
        // Save to settings
        try
        {
            var settings = await ApplicationViewModel.Instance.Settings.LoadAsync();
            settings.Theme = newTheme;
            await ApplicationViewModel.Instance.Settings.SaveAsync(settings);
        }
        catch
        {
            // Ignore save errors
        }
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        UpdateThemeButton();
    }

    #endregion

    #region Functions

    private void UpdateThemeButton()
    {
        var isDark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
        
        ThemeIcon.Data = Avalonia.Media.Geometry.Parse(isDark ? StudioIcons.PATH_THEME_LIGHT : StudioIcons.PATH_THEME_DARK);
        ThemeText.Text = isDark ? "Light" : "Dark";
    }

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
            
            var filePath = file.FilePath;
            
            border.PointerEntered += (_, _) => border.Background = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0));
            border.PointerExited += (_, _) => border.Background = Brushes.Transparent;
            
            border.PointerPressed += (_, _) =>
            {
                ApplicationViewModel.Instance.MainWindowVm.OpenRecentCommand.Execute(filePath);
            };
            
            RecentFilesList.Children.Add(border);
        }
    }

    #endregion
}
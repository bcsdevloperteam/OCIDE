using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using OCIDE.Services;

namespace OCIDE;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    private AppConfig _config;

    public MainWindow()
    {
        InitializeComponent();
        Wpf.Ui.Appearance.SystemThemeWatcher.Watch(this);

        _config = SettingsManager.Load();
        
        this.Width = _config.WindowWidth;
        this.Height = _config.WindowHeight;
        if (!double.IsNaN(_config.WindowTop)) this.Top = _config.WindowTop;
        if (!double.IsNaN(_config.WindowLeft)) this.Left = _config.WindowLeft;
        this.WindowState = _config.WindowState;


        this.Loaded += MainWindow_Loaded;
        this.Closing += MainWindow_Closing;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Master Syncing: Restore opened folder and file
        if (!string.IsNullOrEmpty(_config.LastOpenedFolder))
        {
            AppTitleBar.Title = $"OCIDE - {System.IO.Path.GetFileName(_config.LastOpenedFolder)}";
            ProjectsMenuItem.Visibility = Visibility.Collapsed;
            
            // TODO: Load folder into Explorer view
            ExplorerMenuItem.Visibility = Visibility.Visible;
            SearchMenuItem.Visibility = Visibility.Visible;
            SourceControlMenuItem.Visibility = Visibility.Visible;
            AIExtensionsMenuItem.Visibility = Visibility.Visible;

            RootNavigation.Navigate(typeof(OCIDE.Pages.ExplorerPage));
        }
        else
        {
            AppTitleBar.Title = "OCIDE";
            RootNavigation.Navigate(typeof(OCIDE.Pages.ProjectsPage));
        }
        
        if (!string.IsNullOrEmpty(_config.LastOpenedFile))
        {
            // TODO: Load file into CustomTextEditor
        }
    }
    
    public void OpenProject(string folderPath)
    {
        _config.LastOpenedFolder = folderPath;
        
        if (_config.RecentProjects.Contains(folderPath))
            _config.RecentProjects.Remove(folderPath);
        _config.RecentProjects.Insert(0, folderPath);
        if (_config.RecentProjects.Count > 10)
            _config.RecentProjects.RemoveAt(10);
            
        SettingsManager.Save(_config);

        AppTitleBar.Title = $"OCIDE - {System.IO.Path.GetFileName(folderPath)}";
        ProjectsMenuItem.Visibility = Visibility.Collapsed;

        ExplorerMenuItem.Visibility = Visibility.Visible;
        SearchMenuItem.Visibility = Visibility.Visible;
        SourceControlMenuItem.Visibility = Visibility.Visible;
        AIExtensionsMenuItem.Visibility = Visibility.Visible;

        RootNavigation.Navigate(typeof(OCIDE.Pages.ExplorerPage));
    }

    private void RootNavigation_BackRequested(object sender, RoutedEventArgs e)
    {
        // Close project and go back to Project Manager
        _config.LastOpenedFolder = string.Empty;
        _config.LastOpenedFile = string.Empty;
        SettingsManager.Save(_config);

        AppTitleBar.Title = "OCIDE";
        ProjectsMenuItem.Visibility = Visibility.Visible;
        ExplorerMenuItem.Visibility = Visibility.Collapsed;
        SearchMenuItem.Visibility = Visibility.Collapsed;
        SourceControlMenuItem.Visibility = Visibility.Collapsed;
        AIExtensionsMenuItem.Visibility = Visibility.Collapsed;

        RootNavigation.Navigate(typeof(OCIDE.Pages.ProjectsPage));
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _config.WindowState = this.WindowState;
        if (this.WindowState == WindowState.Normal)
        {
            _config.WindowWidth = this.Width;
            _config.WindowHeight = this.Height;
            _config.WindowTop = this.Top;
            _config.WindowLeft = this.Left;
        }
        else
        {
            _config.WindowWidth = this.RestoreBounds.Width;
            _config.WindowHeight = this.RestoreBounds.Height;
            _config.WindowTop = this.RestoreBounds.Top;
            _config.WindowLeft = this.RestoreBounds.Left;
        }

        // Master Syncing: SettingsManager saves state automatically on close
        SettingsManager.Save(_config);
    }
}
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
        
        public static MainWindow Instance { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
            Instance = this;
            
            // Removed custom titlebar icon per user request
            
            Wpf.Ui.Appearance.SystemThemeWatcher.Watch(this);

            // Initialize Extension Host
            OCIDE.Extensibility.ExtensionHost.Instance.Context = new OCIDE.Extensibility.OcideContext();
            OCIDE.Extensibility.ExtensionHost.Instance.DiscoverExtensions();
            OCIDE.Extensibility.EventAggregator.Publish("onStartup");

            _config = SettingsManager.Load();
            
            this.Width = _config.WindowWidth;
            this.Height = _config.WindowHeight;
            if (!double.IsNaN(_config.WindowTop)) this.Top = _config.WindowTop;
            if (!double.IsNaN(_config.WindowLeft)) this.Left = _config.WindowLeft;
            this.WindowState = _config.WindowState;

            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
            this.Closed += MainWindow_Closed;
            
            EditorTabs.SelectionChanged += EditorTabs_SelectionChanged;
            
            SetupDynamicLogo();
        }

        private void EditorTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source == EditorTabs && EditorTabs.SelectedItem is TabItem tab && tab.Content is OCIDE.Editor.CustomTextEditor editor)
            {
                UpdateStatusBar(editor);
            }
            else if (EditorTabs.Items.Count == 0 || EditorTabs.SelectedItem == null)
            {
                StatusLanguageText.Text = "Plain Text";
                StatusLnColText.Text = "Ln 1, Col 1";
            }
        }

        public void UpdateStatusBar(OCIDE.Editor.CustomTextEditor editor)
        {
            if (editor == null) return;
            
            string lang = string.IsNullOrEmpty(editor.LanguageId) ? "Plain Text" : char.ToUpper(editor.LanguageId[0]) + editor.LanguageId.Substring(1);
            if (lang.ToLower() == "csharp") lang = "C#";
            StatusLanguageText.Text = lang;
            
            int line = editor.TextArea.Caret.Line;
            int col = editor.TextArea.Caret.Column;
            StatusLnColText.Text = $"Ln {line}, Col {col}";
            
            editor.TextArea.Caret.PositionChanged -= Caret_PositionChanged;
            editor.TextArea.Caret.PositionChanged += Caret_PositionChanged;
        }

        private void Caret_PositionChanged(object? sender, EventArgs e)
        {
            if (EditorTabs.SelectedItem is TabItem tab && tab.Content is OCIDE.Editor.CustomTextEditor editor)
            {
                int line = editor.TextArea.Caret.Line;
                int col = editor.TextArea.Caret.Column;
                StatusLnColText.Text = $"Ln {line}, Col {col}";
            }
        }

        private System.Windows.Threading.DispatcherTimer _logoTimer;
        private bool _isFirstLogo = true;

        private void SetupDynamicLogo()
        {
            UpdateLogo();

            _logoTimer = new System.Windows.Threading.DispatcherTimer();
            _logoTimer.Interval = TimeSpan.FromMinutes(30);
            _logoTimer.Tick += (s, e) => UpdateLogo();
            _logoTimer.Start();
        }

        private void UpdateLogo()
        {
            try
            {
                string logoFolder = GetLogoFolder();
                if (string.IsNullOrEmpty(logoFolder)) return;

                string logoPath = System.IO.Path.Combine(logoFolder, _isFirstLogo ? "1.png" : "2.png");
                if (System.IO.File.Exists(logoPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(logoPath, UriKind.Absolute);
                    bitmap.EndInit();
                    
                    this.Icon = bitmap;
                    
                    if (AppTitleBar.Icon != null || true) // Wpf.Ui TitleBar also has an Icon property sometimes, but let's check what type it is. In Wpf.Ui v2 it's an IconElement
                    {
                        var imageIcon = new Wpf.Ui.Controls.ImageIcon { Source = bitmap };
                        AppTitleBar.Icon = imageIcon;
                    }
                }
                
                _isFirstLogo = !_isFirstLogo;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update logo: {ex.Message}");
            }
        }

        private string GetLogoFolder()
        {
            string current = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(current))
            {
                string potential = System.IO.Path.Combine(current, "logo.app");
                if (System.IO.Directory.Exists(potential))
                    return potential;
                current = System.IO.Path.GetDirectoryName(current);
            }
            return string.Empty;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_config.LastOpenedFolder))
            {
                AppTitleBar.Title = $"OCIDE - {System.IO.Path.GetFileName(_config.LastOpenedFolder)}";
                AppTitleText.Text = AppTitleBar.Title;
                ProjectsMenuItem.Visibility = Visibility.Collapsed;
                HomeTitlebarButton.Visibility = Visibility.Visible;
                
                ExplorerMenuItem.Visibility = Visibility.Visible;
                SearchMenuItem.Visibility = Visibility.Visible;
                ExtensionsMenuItem.Visibility = Visibility.Visible;
                SourceControlMenuItem.Visibility = Visibility.Visible;
                AIExtensionsMenuItem.Visibility = Visibility.Visible;
                
                EditorBorder.Visibility = Visibility.Visible;
                EditorTabs.Visibility = Visibility.Visible;
                RootNavigation.SetValue(Grid.ColumnSpanProperty, 1);

                RootNavigation.Navigate(typeof(OCIDE.Pages.ExplorerPage));
            }
            else
            {
                AppTitleBar.Title = "OCIDE";
                AppTitleText.Text = AppTitleBar.Title;
                HomeTitlebarButton.Visibility = Visibility.Collapsed;
                
                EditorBorder.Visibility = Visibility.Collapsed;
                EditorTabs.Visibility = Visibility.Collapsed;
                RootNavigation.SetValue(Grid.ColumnSpanProperty, 2);
                
                RootNavigation.Navigate(typeof(OCIDE.Pages.ProjectsPage));
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
        AppTitleText.Text = AppTitleBar.Title;
        ProjectsMenuItem.Visibility = Visibility.Collapsed;
        HomeTitlebarButton.Visibility = Visibility.Visible;

        ExplorerMenuItem.Visibility = Visibility.Visible;
        SearchMenuItem.Visibility = Visibility.Visible;
        SourceControlMenuItem.Visibility = Visibility.Visible;
        AIExtensionsMenuItem.Visibility = Visibility.Visible;

        EditorBorder.Visibility = Visibility.Visible;
        EditorTabs.Visibility = Visibility.Visible;
        RootNavigation.SetValue(Grid.ColumnSpanProperty, 1);

        RootNavigation.Navigate(typeof(OCIDE.Pages.ExplorerPage));
    }

    private void HomeButton_Click(object sender, RoutedEventArgs e)
    {
        CloseProjectAndGoHome();
    }

    private void RootNavigation_BackRequested(object sender, RoutedEventArgs e)
    {
        CloseProjectAndGoHome();
    }

    private void CloseProjectAndGoHome()
    {
        // Close project and go back to Project Manager
        _config.LastOpenedFolder = string.Empty;
        _config.LastOpenedFile = string.Empty;
        SettingsManager.Save(_config);

        AppTitleBar.Title = "OCIDE";
        AppTitleText.Text = AppTitleBar.Title;
        ProjectsMenuItem.Visibility = Visibility.Visible;
        HomeTitlebarButton.Visibility = Visibility.Collapsed;
        
        ExplorerMenuItem.Visibility = Visibility.Collapsed;
        SearchMenuItem.Visibility = Visibility.Collapsed;
        SourceControlMenuItem.Visibility = Visibility.Collapsed;
        AIExtensionsMenuItem.Visibility = Visibility.Collapsed;
        
        EditorBorder.Visibility = Visibility.Collapsed;
        EditorTabs.Visibility = Visibility.Collapsed;
        RootNavigation.SetValue(Grid.ColumnSpanProperty, 2);

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

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        OCIDE.Extensibility.ExtensionHost.Instance.DeactivateAll();
        Application.Current.Shutdown();
        Environment.Exit(0);
    }

    public void OpenEditorTab(string tabTitle, UserControl contentControl)
    {
        var headerPanel = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        headerPanel.Children.Add(new System.Windows.Controls.TextBlock { Text = tabTitle, Margin = new Thickness(0,0,10,0), VerticalAlignment = VerticalAlignment.Center });
        
        var closeBtn = new System.Windows.Controls.Button { Content = "X", Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = System.Windows.Media.Brushes.DarkGray, Padding = new Thickness(2), Cursor = System.Windows.Input.Cursors.Hand };
        var tabItem = new System.Windows.Controls.TabItem
        {
            Header = headerPanel,
            Content = contentControl
        };
        
        closeBtn.Click += (s, e) => {
            EditorTabs.Items.Remove(tabItem);
            if (EditorTabs.Items.Count == 0) EditorTabs.Visibility = Visibility.Collapsed;
        };
        headerPanel.Children.Add(closeBtn);

        AttachTabContextMenu(tabItem);

        EditorTabs.Items.Add(tabItem);
        EditorTabs.SelectedItem = tabItem;
        EditorTabs.Visibility = Visibility.Visible;
    }

    public void AttachTabContextMenu(System.Windows.Controls.TabItem tabItem, string filePath = "")
    {
        var menu = new ContextMenu();
        
        var closeItem = new MenuItem { Header = "Close" };
        closeItem.Click += (s, e) => { 
            EditorTabs.Items.Remove(tabItem); 
            if (EditorTabs.Items.Count == 0) EditorTabs.Visibility = Visibility.Collapsed; 
        };
        menu.Items.Add(closeItem);

        var closeOthersItem = new MenuItem { Header = "Close Others" };
        closeOthersItem.Click += (s, e) => {
            var toRemove = new System.Collections.Generic.List<TabItem>();
            foreach(TabItem t in EditorTabs.Items) { if (t != tabItem) toRemove.Add(t); }
            foreach(var t in toRemove) EditorTabs.Items.Remove(t);
        };
        menu.Items.Add(closeOthersItem);

        var closeAllItem = new MenuItem { Header = "Close All" };
        closeAllItem.Click += (s, e) => {
            EditorTabs.Items.Clear();
            EditorTabs.Visibility = Visibility.Collapsed;
        };
        menu.Items.Add(closeAllItem);

        if (!string.IsNullOrEmpty(filePath))
        {
            menu.Items.Add(new Separator());
            var copyPathItem = new MenuItem { Header = "Copy Full Path" };
            copyPathItem.Click += (s, e) => System.Windows.Clipboard.SetText(filePath);
            menu.Items.Add(copyPathItem);
        }

        tabItem.ContextMenu = menu;
    }

    public void AddSidebarPanel(string iconName, string title, UserControl panelControl)
    {
        var navItem = new Wpf.Ui.Controls.NavigationViewItem
        {
            Content = title,
            Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.Apps24 }
        };
        RootNavigation.MenuItems.Add(navItem);

        // A simple way to wire it without RootFrame
        navItem.Click += (s, e) => {
            // We can just open it in a tab for now, since injecting into NavigationView's content frame dynamically is complex
            OpenEditorTab(title, panelControl);
        };
    }

    public void AddMenuItem(string topLevelMenu, string itemName, Action onClick)
    {
        System.Diagnostics.Debug.WriteLine($"Plugin requested menu item: {topLevelMenu} -> {itemName}");
    }

    public string GetActiveFileContent()
    {
        if (EditorTabs.SelectedItem is System.Windows.Controls.TabItem tab && tab.Content is OCIDE.Editor.CustomTextEditor editor)
        {
            return editor.Text;
        }
        return string.Empty;
    }

    public void InsertTextAtCursor(string text)
    {
        if (EditorTabs.SelectedItem is System.Windows.Controls.TabItem tab && tab.Content is OCIDE.Editor.CustomTextEditor editor)
        {
            editor.Document.Insert(editor.CaretOffset, text);
        }
    }

    public void ShowNotification(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
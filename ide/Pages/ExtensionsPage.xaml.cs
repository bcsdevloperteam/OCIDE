using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OCIDE.Editor;

namespace OCIDE.Pages
{
    public partial class ExtensionsPage : Page
    {
        public ObservableCollection<ExtensionItem> Extensions { get; set; } = new ObservableCollection<ExtensionItem>();

        public ExtensionsPage()
        {
            InitializeComponent();
            ExtensionsListView.ItemsSource = Extensions;
            LoadExtensions();
        }

        private async void LoadExtensions()
        {
            Extensions.Clear();
            var catalog = await ExtensionManager.GetCatalogAsync();
            foreach (var ext in catalog)
            {
                Extensions.Add(new ExtensionItem
                {
                    Id = ext.Id,
                    Name = ext.Name,
                    Description = ext.Description,
                    Author = ext.Author,
                    IsInstalled = ExtensionManager.IsInstalled(ext.Id)
                });
            }
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string query = SearchBox.Text.ToLower();
                // Filter logic can be added later
            }
        }

        private async void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.Button btn && btn.Tag is string id)
            {
                var item = Extensions.FirstOrDefault(x => x.Id == id);
                if (item != null)
                {
                    if (item.IsInstalled)
                    {
                        ExtensionManager.Uninstall(id);
                        item.IsInstalled = false;
                    }
                    else
                    {
                        btn.IsEnabled = false;
                        // Cannot change btn.Content without breaking the Binding, so just wait
                        bool success = await ExtensionManager.InstallAsync(id);
                        btn.IsEnabled = true;
                        if (success)
                        {
                            item.IsInstalled = true;
                            // Since we just installed an extension, let's also prompt the user to restart
                            System.Windows.MessageBox.Show($"Successfully installed {item.Name}! Please restart OCIDE to load it.", "Install Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            System.Windows.MessageBox.Show($"Failed to install {item.Name}. Check the logs.", "Install Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
        }

        private void ExtensionsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ExtensionsListView.SelectedItem is ExtensionItem item)
            {
                string readmeContent = $"# {item.Name}\n\n{item.Description}\n\n*No README.md found in package.*";
                
                string localReadmePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "extenshions", "Packages", item.Id, "README.md");
                
                if (System.IO.File.Exists(localReadmePath))
                {
                    readmeContent = System.IO.File.ReadAllText(localReadmePath);
                }
                
                var uc = new UserControl();
                var grid = new Grid { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30)) };
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                // Header
                var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(30) };
                
                var icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.PuzzlePiece24, FontSize = 64, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 150, 255)), Margin = new Thickness(0,0,25,0) };
                headerPanel.Children.Add(icon);

                var titlePanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                titlePanel.Children.Add(new TextBlock { Text = item.Name, FontSize = 28, FontWeight = FontWeights.Bold, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White) });
                titlePanel.Children.Add(new TextBlock { Text = $"By {item.Author}", FontSize = 14, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray), Margin = new Thickness(0, 5, 0, 0) });
                titlePanel.Children.Add(new TextBlock { Text = item.Description, FontSize = 14, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 200, 200)), Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap });
                
                headerPanel.Children.Add(titlePanel);
                Grid.SetRow(headerPanel, 0);
                grid.Children.Add(headerPanel);

                // Content
                var border = new Border { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 37, 38)), Margin = new Thickness(30, 0, 30, 30), CornerRadius = new CornerRadius(8), Padding = new Thickness(20) };
                var sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
                var textBlock = new TextBlock 
                { 
                    Text = readmeContent, 
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 220, 220)),
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize = 14
                };
                sv.Content = textBlock;
                border.Child = sv;
                
                Grid.SetRow(border, 1);
                grid.Children.Add(border);

                uc.Content = grid;

                MainWindow.Instance.OpenEditorTab($"Ext: {item.Name}", uc);
                
                // Clear selection
                ExtensionsListView.SelectedItem = null;
            }
        }
    }

    public class ExtensionItem : System.ComponentModel.INotifyPropertyChanged
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }

        private bool _isInstalled;
        public bool IsInstalled
        {
            get => _isInstalled;
            set
            {
                _isInstalled = value;
                OnPropertyChanged(nameof(IsInstalled));
                OnPropertyChanged(nameof(ActionText));
                OnPropertyChanged(nameof(ButtonAppearance));
            }
        }

        public string ActionText => IsInstalled ? "Uninstall" : "Install";
        public string ButtonAppearance => IsInstalled ? "Secondary" : "Primary";

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }
}

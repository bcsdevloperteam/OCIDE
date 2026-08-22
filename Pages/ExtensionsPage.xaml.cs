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
                        btn.Content = "Installing...";
                        btn.IsEnabled = false;
                        bool success = await ExtensionManager.InstallAsync(id);
                        btn.IsEnabled = true;
                        if (success)
                        {
                            item.IsInstalled = true;
                        }
                    }
                }
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

using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using OCIDE.Services;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;

namespace OCIDE.Pages
{
    public class ProjectItem
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Type { get; set; } = "Local Folder";
    }

    public partial class ProjectsPage : Page
    {
        public ProjectsPage()
        {
            InitializeComponent();
            LoadRecentProjects();
        }

        private void LoadRecentProjects()
        {
            var config = SettingsManager.Load();
            if (config.RecentProjects.Count > 0)
            {
                EmptyStateGrid.Visibility = Visibility.Collapsed;
                PopulatedStateGrid.Visibility = Visibility.Visible;

                var items = new List<ProjectItem>();
                foreach (var path in config.RecentProjects)
                {
                    var item = new ProjectItem { Path = path, Name = Path.GetFileName(path) };
                    string configPath = Path.Combine(path, ".ocide", "project.json");
                    if (File.Exists(configPath))
                    {
                        try
                        {
                            string json = File.ReadAllText(configPath);
                            using var doc = JsonDocument.Parse(json);
                            if (doc.RootElement.TryGetProperty("Name", out var nameProp)) item.Name = nameProp.GetString() ?? item.Name;
                            if (doc.RootElement.TryGetProperty("Type", out var typeProp)) item.Type = typeProp.GetString() ?? item.Type;
                        }
                        catch { }
                    }
                    items.Add(item);
                }
                RecentProjectsListView.ItemsSource = items;
            }
            else
            {
                EmptyStateGrid.Visibility = Visibility.Visible;
                PopulatedStateGrid.Visibility = Visibility.Collapsed;
            }
        }

        private void RecentProjectsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RecentProjectsListView.SelectedItem is ProjectItem project)
            {
                if (Application.Current.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.OpenProject(project.Path);
                }
            }
        }

        private void AddProject_Click(object sender, RoutedEventArgs e)
        {
            var wizard = new Windows.ProjectWizardWindow();
            wizard.Owner = Application.Current.MainWindow;

            if (wizard.ShowDialog() == true)
            {
                if (Application.Current.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.OpenProject(wizard.CreatedFolderPath);
                }
            }
        }
    }
}

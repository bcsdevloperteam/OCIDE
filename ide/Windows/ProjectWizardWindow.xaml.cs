using System.Windows;
using Microsoft.Win32;
using System.IO;

namespace OCIDE.Windows
{
    public partial class ProjectWizardWindow : Wpf.Ui.Controls.FluentWindow
    {
        public string CreatedFolderPath { get; private set; } = string.Empty;

        public ProjectWizardWindow()
        {
            InitializeComponent();
            Wpf.Ui.Appearance.SystemThemeWatcher.Watch(this);
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Project Location"
            };

            if (dialog.ShowDialog() == true)
            {
                LocationTextBox.Text = dialog.FolderName;
            }
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            string projectName = ProjectNameTextBox.Text.Trim();
            string location = LocationTextBox.Text.Trim();

            if (string.IsNullOrEmpty(projectName) || string.IsNullOrEmpty(location))
            {
                MessageBox.Show("Please enter a project name and location.", "Missing Info", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string fullPath = Path.Combine(location, projectName);

            try
            {
                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                }
                
                string projectType = ((System.Windows.Controls.ComboBoxItem)ProjectTypeComboBox.SelectedItem).Content.ToString();
                bool useVcs = VcsToggleSwitch.IsChecked == true;

                var projectConfig = new
                {
                    Name = projectName,
                    Type = projectType,
                    UseVCS = useVcs,
                    CreatedAt = System.DateTime.UtcNow
                };

                string configFolder = Path.Combine(fullPath, ".ocide");
                if (!Directory.Exists(configFolder))
                {
                    Directory.CreateDirectory(configFolder);
                }

                string configJson = System.Text.Json.JsonSerializer.Serialize(projectConfig, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(configFolder, "project.json"), configJson);
                
                // TODO: Initialize Git if VcsToggleSwitch.IsChecked == true
                // TODO: Scaffold .NET project if ProjectTypeComboBox is .NET
                
                CreatedFolderPath = fullPath;
                this.DialogResult = true;
                this.Close();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Failed to create project: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}

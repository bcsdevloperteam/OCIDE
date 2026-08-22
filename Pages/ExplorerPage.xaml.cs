using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using OCIDE.Services;
using OCIDE.Models;
using Wpf.Ui.Controls;

namespace OCIDE.Pages
{
    public partial class ExplorerPage : Page
    {
        private ObservableCollection<FileSystemItem> _rootItems;

        public ExplorerPage()
        {
            InitializeComponent();
            this.Loaded += ExplorerPage_Loaded;
            this.IsVisibleChanged += (s, e) => {
                if (this.IsVisible) ExplorerPage_Loaded(this, new RoutedEventArgs());
            };
            _rootItems = new ObservableCollection<FileSystemItem>();
            FileTreeView.ItemsSource = _rootItems;
        }

        private void ExplorerPage_Loaded(object sender, RoutedEventArgs e)
        {
            var config = SettingsManager.Load();
            if (!string.IsNullOrEmpty(config.LastOpenedFolder) && Directory.Exists(config.LastOpenedFolder))
            {
                _rootItems.Clear();
                var root = new FileSystemItem
                {
                    Name = Path.GetFileName(config.LastOpenedFolder),
                    FullPath = config.LastOpenedFolder,
                    IsDirectory = true,
                    IsExpanded = true
                };
                root.LoadChildren();
                _rootItems.Add(root);
            }
        }

        private void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is System.Windows.Controls.TreeViewItem tvi && tvi.DataContext is FileSystemItem item)
            {
                if (item.Children.Count == 1 && item.Children[0].Name == string.Empty)
                {
                    item.LoadChildren();
                }
            }
        }

        private void TreeViewItem_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.TreeViewItem tvi)
            {
                tvi.IsSelected = true;
                // e.Handled = true; // Do not set handled so ContextMenu still opens!
            }
        }

        private void FileTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is FileSystemItem item && !item.IsDirectory)
            {
                var editorTabs = MainWindow.Instance.EditorTabs;

                // Check if already open
                foreach (System.Windows.Controls.TabItem tab in editorTabs.Items)
                {
                    if (tab.Content is OCIDE.Editor.CustomTextEditor editor && editor.FilePath == item.FullPath)
                    {
                        editorTabs.SelectedItem = tab;
                        return;
                    }
                }

                // Create new tab
                var newEditor = new OCIDE.Editor.CustomTextEditor();
                newEditor.LoadFile(item.FullPath);

                var headerPanel = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
                
                // Add Icon to tab
                var iconCtrl = new System.Windows.Controls.ContentControl { Content = OCIDE.Services.IconManager.GetIcon(item.Name, false), Margin = new Thickness(0,0,8,0), VerticalAlignment = VerticalAlignment.Center };
                headerPanel.Children.Add(iconCtrl);
                
                headerPanel.Children.Add(new System.Windows.Controls.TextBlock { Text = item.Name, Margin = new Thickness(0,0,10,0), VerticalAlignment = VerticalAlignment.Center });
                
                var closeBtn = new System.Windows.Controls.Button { Content = "X", Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = System.Windows.Media.Brushes.DarkGray, Padding = new Thickness(2), Cursor = System.Windows.Input.Cursors.Hand };
                var newTab = new System.Windows.Controls.TabItem
                {
                    Header = headerPanel,
                    Content = newEditor
                };

                closeBtn.Click += (s, ev) => 
                {
                    editorTabs.Items.Remove(newTab);
                };
                headerPanel.Children.Add(closeBtn);

                editorTabs.Items.Add(newTab);
                editorTabs.SelectedItem = newTab;
            }
        }

        private FileSystemItem? GetSelectedItem()
        {
            return FileTreeView.SelectedItem as FileSystemItem;
        }

        private FileSystemItem? GetClickedItem(object sender)
        {
            if (sender is System.Windows.Controls.MenuItem menuItem && menuItem.Parent is ContextMenu contextMenu)
            {
                if (contextMenu.PlacementTarget is System.Windows.Controls.TreeViewItem tvi)
                {
                    return tvi.DataContext as FileSystemItem;
                }
                else if (contextMenu.PlacementTarget is FrameworkElement fe)
                {
                    return fe.DataContext as FileSystemItem;
                }
            }
            
            // Try DataContext directly just in case
            if (sender is System.Windows.Controls.MenuItem mi && mi.DataContext is FileSystemItem item)
            {
                return item;
            }

            return GetSelectedItem();
        }

        private void NewFile_Click(object sender, RoutedEventArgs e)
        {
            var item = GetClickedItem(sender) ?? _rootItems.FirstOrDefault();
            if (item == null) return;
            string dir = item.IsDirectory ? item.FullPath : Path.GetDirectoryName(item.FullPath) ?? "";
            string name = Prompt("New File Name");
            if (!string.IsNullOrEmpty(name))
            {
                try
                {
                    string content = "";
                    if (name.EndsWith(".html", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".htm", StringComparison.OrdinalIgnoreCase))
                    {
                        content = "<!DOCTYPE html>\r\n<html lang=\"en\">\r\n<head>\r\n    <meta charset=\"UTF-8\">\r\n    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">\r\n    <title>Document</title>\r\n</head>\r\n<body>\r\n    \r\n</body>\r\n</html>";
                    }
                    
                    File.WriteAllText(Path.Combine(dir, name), content);
                    RefreshParent(item);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed to create file: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void NewFolder_Click(object sender, RoutedEventArgs e)
        {
            var item = GetClickedItem(sender) ?? _rootItems.FirstOrDefault();
            if (item == null) return;
            string dir = item.IsDirectory ? item.FullPath : Path.GetDirectoryName(item.FullPath) ?? "";
            string name = Prompt("New Folder Name");
            if (!string.IsNullOrEmpty(name))
            {
                try
                {
                    Directory.CreateDirectory(Path.Combine(dir, name));
                    RefreshParent(item);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed to create folder: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Rename_Click(object sender, RoutedEventArgs e)
        {
            var item = GetClickedItem(sender);
            if (item == null || item == _rootItems.FirstOrDefault()) return; // don't rename root
            
            string newName = Prompt("Rename", item.Name);
            if (!string.IsNullOrEmpty(newName) && newName != item.Name)
            {
                try
                {
                    string dir = Path.GetDirectoryName(item.FullPath) ?? "";
                    string newPath = Path.Combine(dir, newName);
                    if (item.IsDirectory)
                        Directory.Move(item.FullPath, newPath);
                    else
                        File.Move(item.FullPath, newPath);
                        
                    item.Name = newName;
                    item.FullPath = newPath;
                    RefreshParent(item); // Ensure tree syncs perfectly
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed to rename: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var item = GetClickedItem(sender);
            if (item == null || item == _rootItems.FirstOrDefault()) return;

            var result = System.Windows.MessageBox.Show($"Are you sure you want to permanently delete '{item.Name}'?", "Confirm Delete", System.Windows.MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == System.Windows.MessageBoxResult.Yes)
            {
                try
                {
                    if (item.IsDirectory)
                        Directory.Delete(item.FullPath, true);
                    else
                        File.Delete(item.FullPath);
                        
                    ExplorerPage_Loaded(this, new RoutedEventArgs()); 
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed to delete: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            ExplorerPage_Loaded(this, new RoutedEventArgs());
        }

        private void RefreshParent(FileSystemItem child)
        {
            ExplorerPage_Loaded(this, new RoutedEventArgs());
        }

        private string Prompt(string title, string defaultText = "")
        {
            var promptWindow = new OCIDE.Windows.PromptWindow(title, defaultText)
            {
                Owner = Application.Current.MainWindow
            };
            
            if (promptWindow.ShowDialog() == true)
            {
                return promptWindow.InputText;
            }
            return string.Empty;
        }
    }
}

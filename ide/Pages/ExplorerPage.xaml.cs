using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.ObjectModel;
using OCIDE.Services;
using OCIDE.Models;
using Wpf.Ui.Controls;
using System.Linq;

namespace OCIDE.Pages
{
    public partial class ExplorerPage : Page
    {
        private ObservableCollection<FileSystemItem> _rootItems;
        private FileSystemWatcher? _fileWatcher;
        private Point _dragStartPoint;
        private bool _isDragging;

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

                if (_fileWatcher != null)
                {
                    _fileWatcher.EnableRaisingEvents = false;
                    _fileWatcher.Dispose();
                }

                _fileWatcher = new FileSystemWatcher(config.LastOpenedFolder)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true
                };

                _fileWatcher.Created += FileWatcher_Changed;
                _fileWatcher.Deleted += FileWatcher_Changed;
                _fileWatcher.Renamed += FileWatcher_Changed;
            }
        }

        private void FileWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (_rootItems.Count > 0 && Directory.Exists(_rootItems[0].FullPath))
                {
                    _rootItems[0].LoadChildren();
                }
            });
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
            }
        }

        private static System.Collections.Generic.List<FrameworkElement> FindLocalVisualChildren(DependencyObject depObj)
        {
            var list = new System.Collections.Generic.List<FrameworkElement>();
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child is System.Windows.Controls.TreeViewItem) continue;
                    
                    if (child is FrameworkElement fe)
                    {
                        list.Add(fe);
                    }
                    list.AddRange(FindLocalVisualChildren(child));
                }
            }
            return list;
        }

        private void TreeViewItem_Selected(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TreeViewItem tvi)
            {
                Application.Current.Dispatcher.InvokeAsync(() => {
                    var elements = FindLocalVisualChildren(tvi);
                    var indicator = elements.FirstOrDefault(r => (r is System.Windows.Shapes.Rectangle || r is Border) && r.Width > 0 && r.Width <= 6 && r.HorizontalAlignment == HorizontalAlignment.Left);
                    if (indicator != null)
                    {
                        indicator.RenderTransformOrigin = new Point(0.5, 0.5);
                        var scaleTransform = new ScaleTransform(1, 0);
                        indicator.RenderTransform = scaleTransform;
                        
                        var anim = new System.Windows.Media.Animation.DoubleAnimation {
                            From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(200),
                            EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                        };
                        scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
                        
                        var opacityAnim = new System.Windows.Media.Animation.DoubleAnimation {
                            From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(200)
                        };
                        indicator.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
                    }
                }, System.Windows.Threading.DispatcherPriority.Loaded); // Wait for template to apply
            }
        }

        private void TreeViewItem_Unselected(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TreeViewItem tvi)
            {
                Application.Current.Dispatcher.InvokeAsync(() => {
                    var elements = FindLocalVisualChildren(tvi);
                    var indicator = elements.FirstOrDefault(r => (r is System.Windows.Shapes.Rectangle || r is Border) && r.Width > 0 && r.Width <= 6 && r.HorizontalAlignment == HorizontalAlignment.Left);
                    if (indicator != null)
                    {
                        var scaleTransform = indicator.RenderTransform as ScaleTransform;
                        if (scaleTransform == null)
                        {
                            scaleTransform = new ScaleTransform(1, 1);
                            indicator.RenderTransform = scaleTransform;
                        }
                        
                        var anim = new System.Windows.Media.Animation.DoubleAnimation {
                            From = 1, To = 0, Duration = TimeSpan.FromMilliseconds(150),
                            EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn }
                        };
                        scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
                        
                        var opacityAnim = new System.Windows.Media.Animation.DoubleAnimation {
                            From = 1, To = 0, Duration = TimeSpan.FromMilliseconds(150)
                        };
                        indicator.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
                    }
                });
            }
        }

        private void FileTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is FileSystemItem item && !item.IsDirectory)
            {
                var editorTabs = MainWindow.Instance.EditorTabs;

                foreach (System.Windows.Controls.TabItem tab in editorTabs.Items)
                {
                    if (tab.Content is OCIDE.Editor.CustomTextEditor editor && editor.FilePath == item.FullPath)
                    {
                        editorTabs.SelectedItem = tab;
                        return;
                    }
                }

                var newEditor = new OCIDE.Editor.CustomTextEditor();
                newEditor.LoadFile(item.FullPath);

                var headerPanel = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
                
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

                MainWindow.Instance.AttachTabContextMenu(newTab, item.FullPath);

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
            if (item == null || item == _rootItems.FirstOrDefault()) return; 
            
            string newName = Prompt("Rename", item.Name);
            if (!string.IsNullOrEmpty(newName) && newName != item.Name)
            {
                try
                {
                    string dir = Path.GetDirectoryName(item.FullPath) ?? "";
                    string newPath = Path.Combine(dir, newName);
                    string oldPath = item.FullPath;
                    if (item.IsDirectory)
                        Directory.Move(oldPath, newPath);
                    else
                        File.Move(oldPath, newPath);
                        
                    item.Name = newName;
                    item.FullPath = newPath;
                    RefreshParent(item); 
                    
                    var editorTabs = MainWindow.Instance.EditorTabs;
                    foreach (System.Windows.Controls.TabItem tab in editorTabs.Items)
                    {
                        if (tab.Content is OCIDE.Editor.CustomTextEditor editor)
                        {
                            if (editor.FilePath == oldPath)
                            {
                                editor.UpdateFilePath(newPath);
                                if (tab.Header is System.Windows.Controls.StackPanel sp)
                                {
                                    foreach (var child in sp.Children)
                                    {
                                        if (child is System.Windows.Controls.TextBlock tb)
                                        {
                                            tb.Text = newName;
                                            break;
                                        }
                                    }
                                }
                            }
                            else if (editor.FilePath.StartsWith(oldPath + Path.DirectorySeparatorChar))
                            {
                                string newSubPath = newPath + editor.FilePath.Substring(oldPath.Length);
                                editor.UpdateFilePath(newSubPath);
                            }
                        }
                    }
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
                    string oldPath = item.FullPath;
                    if (item.IsDirectory)
                        Directory.Delete(oldPath, true);
                    else
                        File.Delete(oldPath);
                        
                    ExplorerPage_Loaded(this, new RoutedEventArgs()); 
                    
                    var editorTabs = MainWindow.Instance.EditorTabs;
                    var tabsToRemove = new System.Collections.Generic.List<System.Windows.Controls.TabItem>();
                    foreach (System.Windows.Controls.TabItem tab in editorTabs.Items)
                    {
                        if (tab.Content is OCIDE.Editor.CustomTextEditor editor)
                        {
                            if (editor.FilePath == oldPath || editor.FilePath.StartsWith(oldPath + Path.DirectorySeparatorChar))
                            {
                                tabsToRemove.Add(tab);
                            }
                        }
                    }
                    foreach (var tab in tabsToRemove)
                    {
                        editorTabs.Items.Remove(tab);
                    }
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

        private void CopyPath_Click(object sender, RoutedEventArgs e)
        {
            var item = GetClickedItem(sender);
            if (item != null)
            {
                System.Windows.Clipboard.SetText(item.FullPath);
            }
        }

        private void RevealInExplorer_Click(object sender, RoutedEventArgs e)
        {
            var item = GetClickedItem(sender);
            if (item != null)
            {
                string path = item.IsDirectory ? item.FullPath : Path.GetDirectoryName(item.FullPath) ?? "";
                if (Directory.Exists(path))
                {
                    System.Diagnostics.Process.Start("explorer.exe", path);
                }
            }
        }

        private void TreeViewItem_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            _isDragging = false;
        }

        private void TreeViewItem_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                Point position = e.GetPosition(null);
                if (Math.Abs(position.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(position.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    if (!_isDragging && sender is System.Windows.Controls.TreeViewItem tvi && tvi.DataContext is FileSystemItem item)
                    {
                        if (item == _rootItems.FirstOrDefault()) return; 

                        _isDragging = true;
                        DragDrop.DoDragDrop(tvi, item.FullPath, DragDropEffects.Move);
                    }
                }
            }
        }

        private void TreeViewItem_DragEnter(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.StringFormat))
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void FileTreeView_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.StringFormat))
            {
                string sourcePath = (string)e.Data.GetData(DataFormats.StringFormat);
                
                FileSystemItem? targetItem = null;
                
                var targetElement = e.OriginalSource as DependencyObject;
                while (targetElement != null && !(targetElement is System.Windows.Controls.TreeViewItem))
                {
                    targetElement = VisualTreeHelper.GetParent(targetElement);
                }
                
                if (targetElement is System.Windows.Controls.TreeViewItem tvi)
                {
                    targetItem = tvi.DataContext as FileSystemItem;
                }
                
                targetItem ??= _rootItems.FirstOrDefault();

                if (targetItem != null && !string.IsNullOrEmpty(sourcePath))
                {
                    string targetDir = targetItem.IsDirectory ? targetItem.FullPath : Path.GetDirectoryName(targetItem.FullPath) ?? "";
                    
                    if (string.Equals(Path.GetDirectoryName(sourcePath), targetDir, StringComparison.OrdinalIgnoreCase))
                        return; 

                    try
                    {
                        string fileName = Path.GetFileName(sourcePath);
                        string destPath = Path.Combine(targetDir, fileName);
                        
                        if (Directory.Exists(sourcePath))
                            Directory.Move(sourcePath, destPath);
                        else if (File.Exists(sourcePath))
                            File.Move(sourcePath, destPath);
                            
                        RefreshParent(targetItem); 
                        
                        var editorTabs = MainWindow.Instance.EditorTabs;
                        foreach (System.Windows.Controls.TabItem tab in editorTabs.Items)
                        {
                            if (tab.Content is OCIDE.Editor.CustomTextEditor editor)
                            {
                                if (editor.FilePath == sourcePath)
                                {
                                    editor.UpdateFilePath(destPath);
                                }
                                else if (editor.FilePath.StartsWith(sourcePath + Path.DirectorySeparatorChar))
                                {
                                    string newSubPath = destPath + editor.FilePath.Substring(sourcePath.Length);
                                    editor.UpdateFilePath(newSubPath);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show($"Failed to move file: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }
}

using System;
using System.Windows.Controls;
using OCIDE.Editor;
using System.Diagnostics;

namespace OCIDE.Extensibility
{
    public class OcideContext : IOcideContext
    {
        public ICommandRegistry Commands { get; } = new CommandRegistry();
        public IEditorManager Editor { get; } = new EditorManager();
        public IWindowRegistry Windows { get; } = new WindowRegistry();
        public ILogger Logger { get; } = new Logger();
    }

    public class CommandRegistry : ICommandRegistry
    {
        private System.Collections.Generic.Dictionary<string, Action> _commands = new System.Collections.Generic.Dictionary<string, Action>();

        public void RegisterCommand(string commandId, Action execute)
        {
            _commands[commandId] = execute;
        }

        public void ExecuteCommand(string commandId)
        {
            if (_commands.TryGetValue(commandId, out var action))
            {
                action?.Invoke();
            }
            else
            {
                Debug.WriteLine($"[WARNING] Command '{commandId}' not found.");
            }
        }
    }

    public class EditorManager : IEditorManager
    {
        public string CurrentFilePath
        {
            get
            {
                string path = null;
                System.Windows.Application.Current.Dispatcher.Invoke(() => {
                    var mainWindow = (MainWindow)System.Windows.Application.Current.MainWindow;
                    if (mainWindow.EditorTabs.SelectedItem is TabItem tab && tab.Content is CustomTextEditor editor)
                    {
                        path = editor.FilePath;
                    }
                });
                return path ?? string.Empty;
            }
        }
        
        public string CurrentLanguage
        {
            get
            {
                string lang = null;
                System.Windows.Application.Current.Dispatcher.Invoke(() => {
                    var mainWindow = (MainWindow)System.Windows.Application.Current.MainWindow;
                    if (mainWindow.EditorTabs.SelectedItem is TabItem tab && tab.Content is CustomTextEditor editor && editor.SyntaxHighlighting != null)
                    {
                        lang = editor.SyntaxHighlighting.Name.ToLower();
                    }
                });
                return lang ?? string.Empty;
            }
        }
        public void InsertText(int offset, string text)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => {
                var mainWindow = (MainWindow)System.Windows.Application.Current.MainWindow;
                if (mainWindow.EditorTabs.SelectedItem is TabItem tab && tab.Content is CustomTextEditor editor)
                {
                    editor.Document.Insert(offset, text);
                }
            });
        }
        public void InvokeOnUIThread(Action action) {
            System.Windows.Application.Current.Dispatcher.Invoke(action);
        }
    }

    public class WindowRegistry : IWindowRegistry
    {
        public void AddTab(string title, Control content)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => {
                var mainWindow = (MainWindow)System.Windows.Application.Current.MainWindow;
                var tabItem = new TabItem
                {
                    Header = title,
                    Content = content,
                    Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2D2D2D")),
                    Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#CCCCCC"))
                };
                
                // Add close button to header (simplified)
                var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
                headerPanel.Children.Add(new TextBlock { Text = title, VerticalAlignment = System.Windows.VerticalAlignment.Center });
                
                var closeBtn = new Button { Content = "X", Margin = new System.Windows.Thickness(10, 0, 0, 0), Background = System.Windows.Media.Brushes.Transparent, Foreground = System.Windows.Media.Brushes.DarkGray, BorderThickness = new System.Windows.Thickness(0) };
                closeBtn.Click += (s, e) => {
                    mainWindow.EditorTabs.Items.Remove(tabItem);
                    if (mainWindow.EditorTabs.Items.Count == 0) mainWindow.EditorTabs.Visibility = System.Windows.Visibility.Collapsed;
                };
                headerPanel.Children.Add(closeBtn);
                tabItem.Header = headerPanel;

                mainWindow.EditorTabs.Items.Add(tabItem);
                mainWindow.EditorTabs.Visibility = System.Windows.Visibility.Visible;
                mainWindow.EditorTabs.SelectedItem = tabItem;
            });
        }
    }

    public class Logger : ILogger
    {
        public void LogInfo(string message) => Debug.WriteLine($"[INFO] {message}");
        public void LogError(string message) => Debug.WriteLine($"[ERROR] {message}");
    }
}

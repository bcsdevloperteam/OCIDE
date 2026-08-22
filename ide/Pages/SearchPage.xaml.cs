using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OCIDE.Pages
{
    public partial class SearchPage : Page
    {
        private ObservableCollection<SearchResultFile> _searchResults = new ObservableCollection<SearchResultFile>();

        public SearchPage()
        {
            InitializeComponent();
            ResultsTreeView.ItemsSource = _searchResults;
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                PerformSearch(SearchBox.Text);
            }
        }

        private void PerformSearch(string query)
        {
            _searchResults.Clear();
            if (string.IsNullOrWhiteSpace(query)) return;

            string folder = OCIDE.Services.SettingsManager.Load().LastOpenedFolder;
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;

            bool matchCase = MatchCaseCheckBox.IsChecked == true;
            StringComparison comp = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            try
            {
                var files = Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    // Basic ignore for bin/obj/git
                    if (file.Contains("\\.git\\") || file.Contains("\\bin\\") || file.Contains("\\obj\\") || file.Contains("\\.vs\\"))
                        continue;

                    string[] lines = File.ReadAllLines(file);
                    var matches = new List<SearchResultMatch>();

                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].Contains(query, comp))
                        {
                            matches.Add(new SearchResultMatch
                            {
                                LineNumber = i + 1,
                                LineText = lines[i].Trim(),
                                FilePath = file
                            });
                        }
                    }

                    if (matches.Count > 0)
                    {
                        _searchResults.Add(new SearchResultFile
                        {
                            FilePath = file,
                            Matches = new ObservableCollection<SearchResultMatch>(matches)
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Search failed: {ex.Message}", "Search Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResultsTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is SearchResultMatch match)
            {
                var editorTabs = MainWindow.Instance.EditorTabs;
                
                // Check if already open
                foreach (System.Windows.Controls.TabItem tab in editorTabs.Items)
                {
                    if (tab.Content is OCIDE.Editor.CustomTextEditor editor && editor.FilePath == match.FilePath)
                    {
                        editorTabs.SelectedItem = tab;
                        
                        // Scroll to line
                        var line = editor.Document.GetLineByNumber(Math.Min(match.LineNumber, editor.Document.LineCount));
                        if (line != null)
                        {
                            editor.ScrollToLine(match.LineNumber);
                            editor.Select(line.Offset, line.Length);
                        }
                        return;
                    }
                }

                // Create new tab
                var newEditor = new OCIDE.Editor.CustomTextEditor();
                newEditor.LoadFile(match.FilePath);

                var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
                headerPanel.Children.Add(new TextBlock { Text = Path.GetFileName(match.FilePath), Margin = new Thickness(0,0,10,0), VerticalAlignment = VerticalAlignment.Center });
                
                var closeBtn = new Button { Content = "X", Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = System.Windows.Media.Brushes.DarkGray, Padding = new Thickness(2), Cursor = Cursors.Hand };
                var newTab = new TabItem
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

                // Wait for layout, then scroll to line
                newEditor.Loaded += (s, ev) =>
                {
                    var line = newEditor.Document.GetLineByNumber(Math.Min(match.LineNumber, newEditor.Document.LineCount));
                    if (line != null)
                    {
                        newEditor.ScrollToLine(match.LineNumber);
                        newEditor.Select(line.Offset, line.Length);
                    }
                };
            }
        }
    }

    public class SearchResultFile
    {
        public string FilePath { get; set; } = string.Empty;
        public string Header => System.IO.Path.GetFileName(FilePath);
        public Wpf.Ui.Controls.SymbolRegular Icon => Wpf.Ui.Controls.SymbolRegular.Document24;
        public ObservableCollection<SearchResultMatch> Matches { get; set; } = new ObservableCollection<SearchResultMatch>();
    }

    public class SearchResultMatch
    {
        public string FilePath { get; set; } = string.Empty;
        public int LineNumber { get; set; }
        public string LineText { get; set; } = string.Empty;
        public string Header => $"{LineNumber}: {LineText}";
        public Wpf.Ui.Controls.SymbolRegular Icon => Wpf.Ui.Controls.SymbolRegular.TextAlignLeft24;
        
        // Return null for Matches so TreeView knows it's a leaf node
        public ObservableCollection<SearchResultMatch>? Matches => null;
    }
}

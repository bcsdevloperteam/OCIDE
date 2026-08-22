using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;

namespace OCIDE.Editor
{
    public class CustomTextEditor : TextEditor
    {
        private string _currentFilePath = string.Empty;
        public string FilePath => _currentFilePath;
        private DispatcherTimer _autoSaveTimer;
        private bool _isInternalChange = false;
        private AutocompleteManager _autocompleteManager;

        public CustomTextEditor()
        {
            FontFamily = new FontFamily("Consolas");
            FontSize = 14;
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E"));
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F0F0"));
            BorderThickness = new Thickness(0);
            Padding = new Thickness(20, 10, 10, 10);
            
            ShowLineNumbers = true;
            LineNumbersForeground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAAAAA"));

            _autocompleteManager = new AutocompleteManager(this);

            _autoSaveTimer = new DispatcherTimer();
            _autoSaveTimer.Interval = TimeSpan.FromSeconds(1);
            _autoSaveTimer.Tick += AutoSaveTimer_Tick;

            this.TextChanged += CustomTextEditor_TextChanged;
            this.TextArea.TextEntering += TextArea_TextEntering;
            this.TextArea.TextEntered += TextArea_TextEntered;
        }

        private void TextArea_TextEntering(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text)) return;
            char c = e.Text[0];

            // If user types a closing character that is already immediately to the right of the caret, step over it instead of duplicating it
            if (c == '}' || c == ']' || c == ')' || c == '"' || c == '\'' || c == '*' || c == '_' || c == '`')
            {
                if (this.CaretOffset < this.Document.TextLength)
                {
                    if (this.Document.GetCharAt(this.CaretOffset) == c)
                    {
                        this.CaretOffset++;
                        e.Handled = true;
                    }
                }
            }
        }

        private void TextArea_TextEntered(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text)) return;

            char c = e.Text[0];
            string closing = "";

            if (c == '{') closing = "}";
            else if (c == '[') closing = "]";
            else if (c == '(') closing = ")";
            else if (c == '"') closing = "\"";
            else if (c == '\'') closing = "'";
            else if (c == '*')
            {
                if (this.SyntaxHighlighting != null && this.SyntaxHighlighting.Name.Contains("markdown", StringComparison.OrdinalIgnoreCase))
                    closing = "*";
            }
            else if (c == '_')
            {
                if (this.SyntaxHighlighting != null && this.SyntaxHighlighting.Name.Contains("markdown", StringComparison.OrdinalIgnoreCase))
                    closing = "_";
            }
            else if (c == '`')
            {
                closing = "`";
                if (this.SyntaxHighlighting != null && this.SyntaxHighlighting.Name.Contains("markdown", StringComparison.OrdinalIgnoreCase))
                {
                    int offset = this.CaretOffset;
                    if (offset >= 3 && this.Document.GetText(offset - 3, 3) == "```")
                    {
                        closing = Environment.NewLine + Environment.NewLine + "```";
                    }
                }
            }
            else if (c == '\n' || c == '\r')
            {
                // Auto-indentation logic
                var doc = this.Document;
                var currentLine = doc.GetLineByOffset(this.CaretOffset);
                if (currentLine.LineNumber > 1)
                {
                    var prevLine = doc.GetLineByNumber(currentLine.LineNumber - 1);
                    string prevLineText = doc.GetText(prevLine);
                    
                    // Keep previous indentation
                    string indent = "";
                    foreach (char ch in prevLineText)
                    {
                        if (ch == ' ' || ch == '\t') indent += ch;
                        else break;
                    }

                    // Smart indent for HTML tags: if we pressed enter between <div> and </div>
                    bool htmlSmartIndent = false;
                    if (this.SyntaxHighlighting != null && this.SyntaxHighlighting.Name.Contains("html", StringComparison.OrdinalIgnoreCase))
                    {
                        string textBefore = doc.GetText(prevLine.Offset, prevLine.Length);
                        string textAfter = doc.GetText(currentLine.Offset, currentLine.Length);
                        
                        // If prev line ends with > and current line starts with </
                        if (textBefore.TrimEnd().EndsWith(">") && textAfter.TrimStart().StartsWith("</"))
                        {
                            htmlSmartIndent = true;
                            string extraIndent = indent + "    ";
                            int caret = this.CaretOffset;
                            doc.Insert(caret, extraIndent + Environment.NewLine + indent);
                            this.CaretOffset = caret + extraIndent.Length; // Place caret right after extraIndent
                        }
                    }

                    // Python specific smart indent based on dataset
                    if (!htmlSmartIndent && this.SyntaxHighlighting != null && this.SyntaxHighlighting.Name == "PythonDark")
                    {
                        string trimmed = prevLineText.Trim();
                        var blockKeywords = new[] { "def", "class", "if", "elif", "else", "for", "while", "try", "except", "finally", "with" };
                        
                        // If it ends with ':' and starts with a block keyword, indent!
                        if (trimmed.EndsWith(":"))
                        {
                            string firstWord = trimmed.Split(' ')[0].Split(':')[0];
                            if (Array.IndexOf(blockKeywords, firstWord) != -1)
                            {
                                indent += "    "; // Add 4 spaces
                            }
                        }
                    }
                    
                    // CSS and JS specific smart indent based on dataset
                    if (this.SyntaxHighlighting != null && (this.SyntaxHighlighting.Name == "CssDark" || this.SyntaxHighlighting.Name == "JavaScriptDark"))
                    {
                        string trimmed = prevLineText.Trim();
                        if (trimmed.EndsWith("{"))
                        {
                            indent += "    ";
                        }
                    }

                    if (!htmlSmartIndent && !string.IsNullOrEmpty(indent))
                    {
                        int caret = this.CaretOffset;
                        doc.Insert(caret, indent);
                        this.CaretOffset = caret + indent.Length;
                    }
                }
            }
            // CSS specific formatting helpers
            else if (this.SyntaxHighlighting != null && this.SyntaxHighlighting.Name == "CssDark")
            {
                if (c == ':')
                {
                    closing = " "; // Auto add space after colon
                }
                else if (c == ';')
                {
                    closing = "\n"; // Auto add newline after semicolon
                }
            }
            
            // Shared CSS/JS dedent logic for '}'
            if (c == '}' && this.SyntaxHighlighting != null && (this.SyntaxHighlighting.Name == "CssDark" || this.SyntaxHighlighting.Name == "JavaScriptDark"))
            {
                // Dedent current line if it's empty spaces
                var doc = this.Document;
                var currentLine = doc.GetLineByOffset(this.CaretOffset);
                string lineText = doc.GetText(currentLine);
                if (lineText.Trim() == "}")
                {
                    // Remove 4 spaces of indentation if possible
                    if (lineText.StartsWith("    "))
                    {
                        doc.Remove(currentLine.Offset, 4);
                    }
                }
            }
            // Simple HTML tag autoclosing (if we type '>')
            else if (c == '>')
            {
                // Simple heuristic: if we typed > and it's an html file
                if (this.SyntaxHighlighting != null && this.SyntaxHighlighting.Name.Contains("html", StringComparison.OrdinalIgnoreCase))
                {
                    // Find the last opened tag before this position
                    int offset = this.CaretOffset;
                    string textBefore = this.Document.GetText(0, offset);
                    int lastOpenBracket = textBefore.LastIndexOf('<');
                    if (lastOpenBracket >= 0)
                    {
                        string tagSection = textBefore.Substring(lastOpenBracket);
                        string tagName = tagSection.Trim('<', '>').Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                        
                        // Make sure it doesn't contain a newline and is a valid opening tag
                        if (!tagSection.Contains("\n") && !tagSection.Contains("\r") && 
                            !tagName.StartsWith("/") && !string.IsNullOrWhiteSpace(tagName) && !tagName.EndsWith("/"))
                        {
                            string t = tagName.ToLower();
                            
                            // User's explicit dataset rules
                            var closeAfterFalse = new[] { "img", "br", "hr", "input", "meta", "link", "base", "col", "embed", "source", "track", "wbr", "area", "param" };
                            var closeAfterTrue = new[] { "html", "head", "title", "body", "header", "footer", "nav", "main", "article", "section", "aside", "details", "summary", "dialog", "div", "span", "p", "a", "b", "i", "u", "s", "strong", "em", "mark", "small", "sub", "sup", "pre", "code", "blockquote", "h1", "h2", "h3", "h4", "h5", "h6", "ul", "ol", "li", "dl", "dt", "dd", "table", "thead", "tbody", "tfoot", "tr", "th", "td", "caption", "form", "label", "select", "option", "optgroup", "textarea", "button", "fieldset", "legend", "datalist", "output", "progress", "meter", "script", "noscript", "style", "canvas", "svg", "audio", "video", "iframe", "object", "picture", "map", "figure", "figcaption" };

                            // If it's explicitly marked as false, don't close it
                            if (Array.IndexOf(closeAfterFalse, t) != -1)
                            {
                                // Do nothing
                            }
                            // If it's explicitly marked as true, OR it's an unknown custom tag (like <my-tag>), auto-close it
                            else 
                            {
                                closing = $"</{tagName}>";
                            }
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(closing))
            {
                int currentOffset = this.CaretOffset;
                this.Document.Insert(currentOffset, closing);
                
                if (closing.StartsWith(Environment.NewLine))
                {
                    this.CaretOffset = currentOffset + Environment.NewLine.Length;
                }
                else
                {
                    this.CaretOffset = currentOffset; // Put caret back between the brackets/quotes
                }
            }
        }

        public void LoadFile(string filePath)
        {
            if (!File.Exists(filePath)) return;
            
            _isInternalChange = true;
            _currentFilePath = filePath;
            
            try
            {
                this.Text = File.ReadAllText(filePath);
                
                string ext = Path.GetExtension(filePath).ToLower();
                if (ext == ".cs")
                    this.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
                else if (ext == ".xml" || ext == ".xaml")
                    this.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("XML");
                else if (ext == ".html" || ext == ".htm")
                    this.SyntaxHighlighting = GetDarkHtmlHighlighting();
                else if (ext == ".js")
                    this.SyntaxHighlighting = GetDarkJavaScriptHighlighting();
                else if (ext == ".json")
                    this.SyntaxHighlighting = GetDarkJsonHighlighting();
                else if (ext == ".css")
                    this.SyntaxHighlighting = GetDarkCssHighlighting();
                else if (ext == ".py")
                    this.SyntaxHighlighting = GetDarkPythonHighlighting();
                else if (ext == ".md" || ext == ".markdown")
                    this.SyntaxHighlighting = GetDarkMarkdownHighlighting();
                else
                    this.SyntaxHighlighting = null;

                // Fire extension lifecycle events
                if (this.SyntaxHighlighting != null)
                {
                    OCIDE.Extensibility.EventAggregator.Publish("onLanguage", this.SyntaxHighlighting.Name.ToLower());
                }
                OCIDE.Extensibility.EventAggregator.Publish("onFileOpen", ext);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load file {filePath}: {ex.ToString()}");
            }
            finally
            {
                _isInternalChange = false;
            }
        }

        private static IHighlightingDefinition _darkPython;
        private static IHighlightingDefinition GetDarkPythonHighlighting()
        {
            if (_darkPython != null) return _darkPython;
            _darkPython = ThemeLoader.LoadFromJson("Themes/python.json", true);
            return _darkPython;
        }

        private static IHighlightingDefinition _darkHtml;
        private static IHighlightingDefinition GetDarkHtmlHighlighting()
        {
            if (_darkHtml != null) return _darkHtml;
            _darkHtml = ThemeLoader.LoadFromJson("Themes/html.json", true);
            return _darkHtml;
        }

        private static IHighlightingDefinition _darkJs;
        private static IHighlightingDefinition GetDarkJavaScriptHighlighting()
        {
            if (_darkJs != null) return _darkJs;
            _darkJs = ThemeLoader.LoadFromJson("Themes/javascript.json", true);
            return _darkJs;
        }

        private static IHighlightingDefinition _darkJson;
        private static IHighlightingDefinition GetDarkJsonHighlighting()
        {
            if (_darkJson != null) return _darkJson;
            _darkJson = ThemeLoader.LoadFromJson("Themes/json.json", true);
            return _darkJson;
        }

        private static IHighlightingDefinition _darkCss;
        private static IHighlightingDefinition GetDarkCssHighlighting()
        {
            if (_darkCss != null) return _darkCss;
            _darkCss = ThemeLoader.LoadFromJson("Themes/css.json", true);
            return _darkCss;
        }

        private static IHighlightingDefinition _darkMarkdown;
        private static IHighlightingDefinition GetDarkMarkdownHighlighting()
        {
            if (_darkMarkdown != null) return _darkMarkdown;
            _darkMarkdown = ThemeLoader.LoadFromJson("Themes/markdown.json", true);
            return _darkMarkdown;
        }

        private void CustomTextEditor_TextChanged(object? sender, EventArgs e)
        {
            if (_isInternalChange || string.IsNullOrEmpty(_currentFilePath)) return;

            _autoSaveTimer.Stop();
            _autoSaveTimer.Start();
        }

        private void AutoSaveTimer_Tick(object? sender, EventArgs e)
        {
            _autoSaveTimer.Stop();
            SaveFile();
        }

        private void SaveFile()
        {
            if (string.IsNullOrEmpty(_currentFilePath)) return;

            try
            {
                File.WriteAllText(_currentFilePath, this.Text);
            }
            catch { /* Silently ignore auto-save errors */ }
        }
    }
}

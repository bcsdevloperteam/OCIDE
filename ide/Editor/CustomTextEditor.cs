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
        
        public OmniSharp.Extensions.LanguageServer.Protocol.Client.ILanguageClient? LspClient { get; private set; }
        public string LanguageId { get; private set; } = string.Empty;

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

            this.Options.ConvertTabsToSpaces = true;
            this.Options.IndentationSize = 4;
            this.Options.EnableRectangularSelection = true;
            this.Options.HighlightCurrentLine = true;

            _autocompleteManager = new AutocompleteManager(this);

            _autoSaveTimer = new DispatcherTimer();
            _autoSaveTimer.Interval = TimeSpan.FromSeconds(1);
            _autoSaveTimer.Tick += AutoSaveTimer_Tick;

            // Add Color Swatch Generator for inline CSS color previews
            this.TextArea.TextView.ElementGenerators.Add(new ColorSwatchGenerator());

            this.TextChanged += CustomTextEditor_TextChanged;
            this.TextArea.TextEntering += TextArea_TextEntering;
            this.TextArea.TextEntered += TextArea_TextEntered;
        }

        protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            if ((e.Key == System.Windows.Input.Key.Tab || e.Key == System.Windows.Input.Key.Enter) && e.KeyboardDevice.Modifiers == System.Windows.Input.ModifierKeys.None)
            {
                if (this.SyntaxHighlighting != null && this.SyntaxHighlighting.Name.Contains("html", StringComparison.OrdinalIgnoreCase))
                {
                    var doc = this.Document;
                    int offset = this.CaretOffset;
                    var line = doc.GetLineByOffset(offset);
                    string textBefore = doc.GetText(line.Offset, offset - line.Offset);
                    
                    var standardTags = new System.Collections.Generic.HashSet<string>();
                    var selfClosing = new System.Collections.Generic.HashSet<string>();
                    
                    try 
                    {
                        string jsonPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Datasets", "emmet.json");
                        if (System.IO.File.Exists(jsonPath))
                        {
                            string json = System.IO.File.ReadAllText(jsonPath);
                            using (var docJson = System.Text.Json.JsonDocument.Parse(json))
                            {
                                var root = docJson.RootElement.GetProperty("html");
                                var std = root.GetProperty("standardTags");
                                foreach (var item in std.EnumerateArray())
                                    standardTags.Add(item.GetString());
                                    
                                var self = root.GetProperty("selfClosingTags");
                                foreach (var item in self.EnumerateArray())
                                    selfClosing.Add(item.GetString());
                            }
                        }
                    } 
                    catch { }
                    
                    if (standardTags.Count == 0)
                    {
                        standardTags = new System.Collections.Generic.HashSet<string>(new[] { 
                            "div", "span", "p", "a", "b", "i", "u", "strong", "em", "h1", "h2", "h3", "ul", "li", "table", "tr", "td",
                            "form", "input", "button", "html", "head", "body", "title", "script", "style", "link", "meta"
                        });
                        selfClosing = new System.Collections.Generic.HashSet<string>(new[] { "img", "br", "hr", "input", "meta", "link" });
                    }

                    string lineText = doc.GetText(line.Offset, offset - line.Offset);
                    
                    // Custom syntax: tag.attr value -> <tag attr="value"></tag>
                    var customMatch = System.Text.RegularExpressions.Regex.Match(lineText, @"([a-zA-Z0-9]+)\.([a-zA-Z0-9_-]+)\s+([a-zA-Z0-9_.-]+)$");
                    if (customMatch.Success)
                    {
                        string cTagName = customMatch.Groups[1].Value.ToLower();
                        string cAttrName = customMatch.Groups[2].Value;
                        string cAttrValue = customMatch.Groups[3].Value;
                        
                        if (standardTags.Contains(cTagName))
                        {
                            bool isSelfClosing = selfClosing.Contains(cTagName);
                            string html = $"<{cTagName} {cAttrName}=\"{cAttrValue}\"";
                            if (isSelfClosing) html += ">";
                            else html += $"></{cTagName}>";
                            
                            int replaceStart = line.Offset + customMatch.Index;
                            doc.Replace(replaceStart, customMatch.Length, html);
                            
                            if (isSelfClosing)
                                this.CaretOffset = replaceStart + html.Length;
                            else
                                this.CaretOffset = replaceStart + html.IndexOf('>') + 1;
                            
                            e.Handled = true;
                            return;
                        }
                    }

                    int wordStart = -1;
                    for (int i = lineText.Length - 1; i >= 0; i--)
                    {
                        char c = lineText[i];
                        if (char.IsWhiteSpace(c) || c == '<' || c == '>')
                        {
                            wordStart = i + 1;
                            break;
                        }
                    }
                    if (wordStart == -1) wordStart = 0;
                    
                    string word = lineText.Substring(wordStart);
                    if (word.Length > 0 && (char.IsLetter(word[0]) || word[0] == '!'))
                    {
                        string tagName = "";
                        string id = "";
                        string classes = "";
                        
                        int dotIdx = word.IndexOf('.');
                        int hashIdx = word.IndexOf('#');
                        
                        int tagEnd = word.Length;
                        if (dotIdx != -1 && (hashIdx == -1 || dotIdx < hashIdx)) tagEnd = Math.Min(tagEnd, dotIdx);
                        if (hashIdx != -1 && (dotIdx == -1 || hashIdx < dotIdx)) tagEnd = Math.Min(tagEnd, hashIdx);
                        
                        tagName = word.Substring(0, tagEnd);
                        
                        if (hashIdx != -1)
                        {
                            int end = dotIdx != -1 && dotIdx > hashIdx ? dotIdx : word.Length;
                            id = word.Substring(hashIdx + 1, end - hashIdx - 1);
                        }
                        
                        if (dotIdx != -1)
                        {
                            classes = word.Substring(dotIdx + 1);
                            if (hashIdx > dotIdx) {
                                classes = word.Substring(dotIdx + 1, hashIdx - dotIdx - 1);
                            }
                            classes = classes.Replace(".", " ");
                        }

                        // Check snippets from JSON
                        try 
                        {
                            string jsonPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Datasets", "emmet.json");
                            if (System.IO.File.Exists(jsonPath))
                            {
                                string json = System.IO.File.ReadAllText(jsonPath);
                                using (var docJson = System.Text.Json.JsonDocument.Parse(json))
                                {
                                    var root = docJson.RootElement.GetProperty("html");
                                    if (root.TryGetProperty("snippets", out var snippetsObj))
                                    {
                                        if (snippetsObj.TryGetProperty(tagName, out var snippetProp))
                                        {
                                            string html = snippetProp.GetString();
                                            doc.Replace(line.Offset + wordStart, word.Length, html);
                                            int bodyIndex = html.IndexOf("<body>");
                                            if (bodyIndex != -1)
                                            {
                                                int innerOffset = html.IndexOf('\n', bodyIndex);
                                                if (innerOffset != -1)
                                                    this.CaretOffset = line.Offset + wordStart + innerOffset + 5;
                                                else
                                                    this.CaretOffset = line.Offset + wordStart + bodyIndex + 6;
                                            }
                                            else
                                            {
                                                this.CaretOffset = line.Offset + wordStart + html.Length;
                                            }
                                            e.Handled = true;
                                            return;
                                        }
                                    }
                                }
                            }
                        } 
                        catch { }
                        
                        if (standardTags.Contains(tagName.ToLower()))
                        {
                            bool isSelfClosing = selfClosing.Contains(tagName.ToLower());
                            
                            string html = $"<{tagName}";
                            if (!string.IsNullOrEmpty(id)) html += $" id=\"{id}\"";
                            if (!string.IsNullOrEmpty(classes)) html += $" class=\"{classes}\"";
                            
                            if (isSelfClosing)
                                html += ">";
                            else
                                html += "></" + tagName + ">";
                                
                            doc.Replace(line.Offset + wordStart, word.Length, html);
                            
                            if (isSelfClosing)
                                this.CaretOffset = line.Offset + wordStart + html.Length;
                            else
                                this.CaretOffset = line.Offset + wordStart + html.IndexOf('>') + 1;
                            
                            e.Handled = true;
                            return;
                        }
                    }
                }
            }

            if (e.Key == System.Windows.Input.Key.Tab && e.KeyboardDevice.Modifiers == System.Windows.Input.ModifierKeys.None)
            {
                if (this.SelectionLength == 0 && this.CaretOffset < this.Document.TextLength)
                {
                    char nextChar = this.Document.GetCharAt(this.CaretOffset);
                    if (nextChar == '}' || nextChar == ']' || nextChar == ')' || nextChar == '"' || nextChar == '\'' || nextChar == '`')
                    {
                        bool canJump = false;
                        if (this.CaretOffset + 1 == this.Document.TextLength) canJump = true;
                        else
                        {
                            char afterNext = this.Document.GetCharAt(this.CaretOffset + 1);
                            if (char.IsWhiteSpace(afterNext) || afterNext == '}' || afterNext == ']' || afterNext == ')' || afterNext == ';' || afterNext == ',')
                                canJump = true;
                        }

                        if (canJump)
                        {
                            this.CaretOffset++;
                            e.Handled = true;
                            return;
                        }
                    }
                }
            }
            
            // Alt+Up / Alt+Down to move lines
            if (e.KeyboardDevice.Modifiers == System.Windows.Input.ModifierKeys.Alt)
            {
                if (e.Key == System.Windows.Input.Key.Up)
                {
                    var line = this.Document.GetLineByOffset(this.CaretOffset);
                    if (line.LineNumber > 1)
                    {
                        int col = this.CaretOffset - line.Offset;
                        var prevLine = this.Document.GetLineByNumber(line.LineNumber - 1);
                        string lineText = this.Document.GetText(line);
                        string prevLineText = this.Document.GetText(prevLine);
                        
                        this.Document.Replace(prevLine.Offset, prevLine.Length, lineText);
                        this.Document.Replace(line.Offset, line.Length, prevLineText);
                        this.CaretOffset = prevLine.Offset + Math.Min(col, lineText.Length);
                        e.Handled = true;
                        return;
                    }
                }
                else if (e.Key == System.Windows.Input.Key.Down)
                {
                    var line = this.Document.GetLineByOffset(this.CaretOffset);
                    if (line.LineNumber < this.Document.LineCount)
                    {
                        int col = this.CaretOffset - line.Offset;
                        var nextLine = this.Document.GetLineByNumber(line.LineNumber + 1);
                        string lineText = this.Document.GetText(line);
                        string nextLineText = this.Document.GetText(nextLine);
                        
                        this.Document.Replace(line.Offset, line.Length, nextLineText);
                        this.Document.Replace(nextLine.Offset, nextLine.Length, lineText);
                        this.CaretOffset = nextLine.Offset + Math.Min(col, lineText.Length);
                        e.Handled = true;
                        return;
                    }
                }
            }
            
            // Ctrl+D to duplicate line
            if (e.KeyboardDevice.Modifiers == System.Windows.Input.ModifierKeys.Control && e.Key == System.Windows.Input.Key.D)
            {
                var line = this.Document.GetLineByOffset(this.CaretOffset);
                string lineText = this.Document.GetText(line);
                this.Document.Insert(line.EndOffset, Environment.NewLine + lineText);
                e.Handled = true;
                return;
            }

            // Ctrl+Shift+K to delete line
            if (e.KeyboardDevice.Modifiers == (System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift) && e.Key == System.Windows.Input.Key.K)
            {
                var line = this.Document.GetLineByOffset(this.CaretOffset);
                this.Document.Remove(line.Offset, line.TotalLength);
                e.Handled = true;
                return;
            }

            // Ctrl+/ to Toggle Line Comment
            if (e.KeyboardDevice.Modifiers == System.Windows.Input.ModifierKeys.Control && e.Key == System.Windows.Input.Key.OemQuestion)
            {
                ToggleLineComment();
                e.Handled = true;
                return;
            }

            base.OnPreviewKeyDown(e);
        }

        private void ToggleLineComment()
        {
            if (this.SyntaxHighlighting == null) return;
            
            string lang = this.SyntaxHighlighting.Name.ToLower();
            string commentStr = "//";
            bool isBlockComment = false;
            
            if (lang.Contains("html") || lang.Contains("xml") || lang.Contains("markdown"))
            {
                commentStr = "<!--";
                isBlockComment = true;
            }
            else if (lang.Contains("python"))
            {
                commentStr = "#";
            }
            else if (lang.Contains("css"))
            {
                commentStr = "/*";
                isBlockComment = true;
            }

            int startLine = this.Document.GetLineByOffset(this.SelectionStart).LineNumber;
            int endLine = this.Document.GetLineByOffset(this.SelectionStart + this.SelectionLength).LineNumber;
            
            // If the selection ends at the very beginning of the endLine, we don't want to comment out the endLine
            if (this.SelectionLength > 0 && this.SelectionStart + this.SelectionLength == this.Document.GetLineByNumber(endLine).Offset)
            {
                endLine--;
            }

            using (this.Document.RunUpdate())
            {
                for (int i = startLine; i <= endLine; i++)
                {
                    var line = this.Document.GetLineByNumber(i);
                    string text = this.Document.GetText(line);
                    
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    if (isBlockComment)
                    {
                        string closeStr = commentStr == "<!--" ? "-->" : "*/";
                        string trimmed = text.Trim();
                        if (trimmed.StartsWith(commentStr) && trimmed.EndsWith(closeStr))
                        {
                            // Uncomment
                            int openIdx = text.IndexOf(commentStr);
                            int closeIdx = text.LastIndexOf(closeStr);
                            
                            text = text.Remove(closeIdx, closeStr.Length);
                            // Also remove space if exists
                            if (text.Length > openIdx + commentStr.Length && text[openIdx + commentStr.Length] == ' ')
                            {
                                text = text.Remove(openIdx + commentStr.Length, 1);
                            }
                            text = text.Remove(openIdx, commentStr.Length);
                            
                            // Remove space before closing tag if exists
                            if (text.EndsWith(" ") && closeIdx > 0)
                            {
                                text = text.Remove(text.Length - 1);
                            }
                            
                            this.Document.Replace(line.Offset, line.Length, text);
                        }
                        else
                        {
                            // Comment
                            int firstCharIdx = text.Length - text.TrimStart().Length;
                            text = text.Insert(firstCharIdx, commentStr + " ") + " " + closeStr;
                            this.Document.Replace(line.Offset, line.Length, text);
                        }
                    }
                    else
                    {
                        if (text.TrimStart().StartsWith(commentStr))
                        {
                            // Uncomment
                            int idx = text.IndexOf(commentStr);
                            int removeLen = commentStr.Length;
                            if (text.Length > idx + removeLen && text[idx + removeLen] == ' ') removeLen++;
                            text = text.Remove(idx, removeLen);
                            this.Document.Replace(line.Offset, line.Length, text);
                        }
                        else
                        {
                            // Comment
                            int firstCharIdx = text.Length - text.TrimStart().Length;
                            text = text.Insert(firstCharIdx, commentStr + " ");
                            this.Document.Replace(line.Offset, line.Length, text);
                        }
                    }
                }
            }
        }

        private void TextArea_TextEntering(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text)) return;
            char c = e.Text[0];

            // Smart Selection Wrapping
            if (this.SelectionLength > 0)
            {
                if (c == '"' || c == '\'' || c == '(' || c == '{' || c == '[' || c == '<' || c == '`' || c == '*' || c == '_')
                {
                    char closingChar = c switch {
                        '(' => ')',
                        '{' => '}',
                        '[' => ']',
                        '<' => '>',
                        _ => c
                    };
                    
                    string selectedText = this.SelectedText;
                    int oldStart = this.SelectionStart;
                    this.Document.Replace(this.SelectionStart, this.SelectionLength, c + selectedText + closingChar);
                    
                    // Keep the text selected inside the wrappers
                    this.SelectionStart = oldStart + 1;
                    this.SelectionLength = selectedText.Length;
                    
                    e.Handled = true;
                    return;
                }
            }

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
            else if (c == '\n') // IMPORTANT: Only check \n to avoid double-firing on \r\n
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

                    // Smart indent for brackets { } [ ] ( ) (Applies to all languages)
                    bool bracketSmartIndent = false;
                    if (!htmlSmartIndent)
                    {
                        string textBefore = doc.GetText(prevLine.Offset, prevLine.Length);
                        string textAfter = doc.GetText(currentLine.Offset, currentLine.Length);

                        string trimmedBefore = textBefore.TrimEnd();
                        string trimmedAfter = textAfter.TrimStart();
                        
                        if ((trimmedBefore.EndsWith("{") && trimmedAfter.StartsWith("}")) ||
                            (trimmedBefore.EndsWith("[") && trimmedAfter.StartsWith("]")) ||
                            (trimmedBefore.EndsWith("(") && trimmedAfter.StartsWith(")")))
                        {
                            bracketSmartIndent = true;
                            string extraIndent = indent + "    ";
                            int caret = this.CaretOffset;
                            doc.Insert(caret, extraIndent + Environment.NewLine + indent);
                            this.CaretOffset = caret + extraIndent.Length; 
                        }
                    }

                    // Python specific smart indent based on dataset
                    if (!htmlSmartIndent && !bracketSmartIndent && this.SyntaxHighlighting != null && this.SyntaxHighlighting.Name == "PythonDark")
                    {
                        string trimmed = prevLineText.Trim();
                        var blockKeywords = new System.Collections.Generic.List<string>();
                        
                        try 
                        {
                            string jsonPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Datasets", "editor-config.json");
                            if (System.IO.File.Exists(jsonPath))
                            {
                                string json = System.IO.File.ReadAllText(jsonPath);
                                using (var docJson = System.Text.Json.JsonDocument.Parse(json))
                                {
                                    var smartIndent = docJson.RootElement.GetProperty("smartIndent");
                                    var pyKeys = smartIndent.GetProperty("pythonBlockKeywords");
                                    foreach (var item in pyKeys.EnumerateArray())
                                        blockKeywords.Add(item.GetString());
                                }
                            }
                        } 
                        catch { }
                        
                        if (blockKeywords.Count == 0)
                        {
                            blockKeywords.AddRange(new[] { "def", "class", "if", "elif", "else", "for", "while", "try", "except", "finally", "with" });
                        }
                        
                        // If it ends with ':' and starts with a block keyword, indent!
                        if (trimmed.EndsWith(":"))
                        {
                            string firstWord = trimmed.Split(' ')[0].Split(':')[0];
                            if (blockKeywords.Contains(firstWord))
                            {
                                indent += "    "; // Add 4 spaces
                            }
                        }
                    }
                    
                    // General bracket indent for CSS/JS/C# etc if they just hit enter after { [ (
                    if (!htmlSmartIndent && !bracketSmartIndent)
                    {
                        string trimmed = prevLineText.Trim();
                        if (trimmed.EndsWith("{") || trimmed.EndsWith("[") || trimmed.EndsWith("("))
                        {
                            indent += "    ";
                        }
                    }

                    if (!htmlSmartIndent && !bracketSmartIndent && !string.IsNullOrEmpty(indent))
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
            
            // Dedent logic for '}'
            if (c == '}')
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
                            if (tagName.StartsWith("!") || tagName.StartsWith("?"))
                            {
                                // Do not auto-close comments (<!--), doctypes (<!DOCTYPE), or XML declarations (<?)
                            }
                            else
                            {
                                string t = tagName.ToLower();
                                
                                // User's explicit dataset rules
                                var closeAfterFalse = new[] { "img", "br", "hr", "input", "meta", "link", "base", "col", "embed", "source", "track", "wbr", "area", "param" };
                                
                                // If it's explicitly marked as false, don't close it
                                if (Array.IndexOf(closeAfterFalse, t) != -1)
                                {
                                    // Do nothing
                                }
                                else 
                                {
                                    closing = $"</{tagName}>";
                                }
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

        public void UpdateFilePath(string newPath)
        {
            _currentFilePath = newPath;
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
                    LanguageId = this.SyntaxHighlighting.Name.ToLower();
                    OCIDE.Extensibility.EventAggregator.Publish("onLanguage", LanguageId);
                }
                else
                {
                    LanguageId = ext.TrimStart('.');
                }
                OCIDE.Extensibility.EventAggregator.Publish("onFileOpen", ext);

                // Start LSP asynchronously
                InitializeLspAsync(LanguageId, filePath, this.Text);
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

        private async void InitializeLspAsync(string languageId, string filePath, string content)
        {
            if (string.IsNullOrEmpty(languageId)) return;
            
            string workspacePath = Path.GetDirectoryName(filePath) ?? string.Empty;
            var config = OCIDE.Services.SettingsManager.Load();
            if (!string.IsNullOrEmpty(config.LastOpenedFolder))
            {
                workspacePath = config.LastOpenedFolder;
            }

            try
            {
                LspClient = await OCIDE.Services.LspManager.Instance.GetOrStartClientAsync(languageId, workspacePath);
                
                if (LspClient != null)
                {
                    LspClient.SendNotification("textDocument/didOpen", new OmniSharp.Extensions.LanguageServer.Protocol.Models.DidOpenTextDocumentParams
                    {
                        TextDocument = new OmniSharp.Extensions.LanguageServer.Protocol.Models.TextDocumentItem
                        {
                            Uri = OmniSharp.Extensions.LanguageServer.Protocol.DocumentUri.FromFileSystemPath(filePath),
                            LanguageId = languageId,
                            Version = 1,
                            Text = content
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LSP DidOpen Error: {ex.Message}");
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

        private int _documentVersion = 1;

        private void CustomTextEditor_TextChanged(object? sender, EventArgs e)
        {
            if (_isInternalChange || string.IsNullOrEmpty(_currentFilePath)) return;

            _autoSaveTimer.Stop();
            _autoSaveTimer.Start();

            if (LspClient != null)
            {
                _documentVersion++;
                try
                {
                    LspClient.SendNotification("textDocument/didChange", new OmniSharp.Extensions.LanguageServer.Protocol.Models.DidChangeTextDocumentParams
                    {
                        TextDocument = new OmniSharp.Extensions.LanguageServer.Protocol.Models.OptionalVersionedTextDocumentIdentifier
                        {
                            Uri = OmniSharp.Extensions.LanguageServer.Protocol.DocumentUri.FromFileSystemPath(_currentFilePath),
                            Version = _documentVersion
                        },
                        ContentChanges = new[]
                        {
                            new OmniSharp.Extensions.LanguageServer.Protocol.Models.TextDocumentContentChangeEvent
                            {
                                Text = this.Text
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"LSP DidChange Error: {ex.Message}");
                }
            }
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol;

namespace OCIDE.Editor
{
    public class AutocompleteManager
    {
        private CompletionWindow _completionWindow;
        private TextEditor _editor;

        public AutocompleteManager(TextEditor editor)
        {
            _editor = editor;
            _editor.TextArea.TextEntered += TextArea_TextEntered;
            _editor.TextArea.TextEntering += TextArea_TextEntering;
        }

        private async void TextArea_TextEntered(object sender, TextCompositionEventArgs e)
        {
            if (e.Text.Length > 0 && (char.IsLetterOrDigit(e.Text[0]) || e.Text == "."))
            {
                if (_completionWindow == null)
                {
                    await TriggerAutocompleteAsync();
                }
            }
        }

        private async Task TriggerAutocompleteAsync()
        {
            if (_editor is CustomTextEditor customEditor && customEditor.LspClient != null && !string.IsNullOrEmpty(customEditor.FilePath))
            {
                try
                {
                    var position = new Position(customEditor.TextArea.Caret.Line - 1, customEditor.TextArea.Caret.Column - 1);
                    var completionParams = new CompletionParams
                    {
                        TextDocument = new TextDocumentIdentifier { Uri = DocumentUri.FromFileSystemPath(customEditor.FilePath) },
                        Position = position
                    };

                    var completions = await customEditor.LspClient.TextDocument.RequestCompletion(completionParams);
                    if (completions == null || !completions.Any()) return;

                    ShowCompletionWindow(completions.Select(c => new SimpleCompletionData(c.InsertText ?? c.Label, c.Detail ?? c.Label)), 0);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"LSP Autocomplete error: {ex.Message}");
                }
            }
            else
            {
                // Native Fallback / Offline Autocomplete
                string lang = _editor.SyntaxHighlighting?.Name?.ToLower() ?? "";
                
                // Context-aware language switching for HTML (embedded CSS/JS)
                if (lang.Contains("html"))
                {
                    int currentOffset = _editor.CaretOffset;
                    string textBeforeCursor = _editor.Document.GetText(0, currentOffset);
                    
                    int lastStyleOpen = textBeforeCursor.LastIndexOf("<style", StringComparison.OrdinalIgnoreCase);
                    int lastStyleClose = textBeforeCursor.LastIndexOf("</style>", StringComparison.OrdinalIgnoreCase);
                    
                    int lastScriptOpen = textBeforeCursor.LastIndexOf("<script", StringComparison.OrdinalIgnoreCase);
                    int lastScriptClose = textBeforeCursor.LastIndexOf("</script>", StringComparison.OrdinalIgnoreCase);
                    
                    // If the last opened tag was <style> and it hasn't been closed yet
                    if (lastStyleOpen > lastStyleClose && lastStyleOpen > lastScriptOpen)
                    {
                        lang = "css";
                    }
                    // If the last opened tag was <script> and it hasn't been closed yet
                    else if (lastScriptOpen > lastScriptClose && lastScriptOpen > lastStyleOpen)
                    {
                        lang = "javascript";
                    }
                }

                var data = new List<ICompletionData>();
                
                try 
                {
                    if (lang.Contains("html") || lang.Contains("xml"))
                    {
                        string emmetPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Datasets", "emmet.json");
                        if (System.IO.File.Exists(emmetPath))
                        {
                            string json = System.IO.File.ReadAllText(emmetPath);
                            using (var docJson = System.Text.Json.JsonDocument.Parse(json))
                            {
                                var std = docJson.RootElement.GetProperty("html").GetProperty("standardTags");
                                foreach (var item in std.EnumerateArray())
                                {
                                    string tag = item.GetString();
                                    data.Add(new SimpleCompletionData(tag, $"HTML <{tag}> tag"));
                                }
                            }
                        }
                    }
                    else if (lang.Contains("css"))
                    {
                        string cssPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Datasets", "css-properties.json");
                        if (System.IO.File.Exists(cssPath))
                        {
                            string json = System.IO.File.ReadAllText(cssPath);
                            using (var docJson = System.Text.Json.JsonDocument.Parse(json))
                            {
                                var props = docJson.RootElement.GetProperty("properties");
                                foreach (var item in props.EnumerateArray())
                                {
                                    string prop = item.GetString();
                                    data.Add(new SimpleCompletionData(prop, $"CSS Property: {prop}"));
                                }
                            }
                        }
                    }
                } 
                catch { }

                // Fallback to legacy snippets
                string extId = "";
                if (lang.Contains("python")) extId = "lang.python";
                else if (lang.Contains("javascript") || lang.Contains("js")) extId = "lang.js";
                
                if (!string.IsNullOrEmpty(extId) && ExtensionManager.IsInstalled(extId))
                {
                    var extensions = ExtensionManager.GetInstalledExtensions();
                    var ext = extensions.FirstOrDefault(x => x.Id == extId);
                    if (ext != null && ext.Contributes?.Snippets != null)
                    {
                        foreach (var kvp in ext.Contributes.Snippets)
                        {
                            foreach (var snippet in kvp.Value)
                            {
                                data.Add(new SimpleCompletionData(kvp.Key, snippet));
                            }
                        }
                    }
                }

                if (data.Any())
                {
                    // Filter based on the word we are currently typing
                    int offset = _editor.CaretOffset;
                    var doc = _editor.Document;
                    var line = doc.GetLineByOffset(offset);
                    string textBefore = doc.GetText(line.Offset, offset - line.Offset);
                    
                    int wordStart = -1;
                    for (int i = textBefore.Length - 1; i >= 0; i--)
                    {
                        char c = textBefore[i];
                        if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
                        {
                            wordStart = i + 1;
                            break;
                        }
                    }
                    if (wordStart == -1) wordStart = 0;
                    string word = textBefore.Substring(wordStart).ToLower();
                    
                    if (!string.IsNullOrEmpty(word))
                    {
                        var filteredData = data.Where(d => d.Text.ToLower().StartsWith(word)).ToList();
                        ShowCompletionWindow(filteredData, word.Length);
                    }
                    else
                    {
                        ShowCompletionWindow(data, 0);
                    }
                }
            }
        }

        private void ShowCompletionWindow(IEnumerable<ICompletionData> completionDataList, int replaceLength)
        {
            // Must run on UI thread
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (!completionDataList.Any()) return;

                _completionWindow = new CompletionWindow(_editor.TextArea);
                
                if (replaceLength > 0)
                {
                    _completionWindow.StartOffset = _editor.CaretOffset - replaceLength;
                }
                IList<ICompletionData> data = _completionWindow.CompletionList.CompletionData;
                foreach (var item in completionDataList)
                {
                    data.Add(item);
                }

                _completionWindow.Show();
                _completionWindow.Closed += delegate {
                    _completionWindow = null;
                };
            });
        }

        private void TextArea_TextEntering(object sender, TextCompositionEventArgs e)
        {
            if (e.Text.Length > 0 && _completionWindow != null)
            {
                if (!char.IsLetterOrDigit(e.Text[0]) && e.Text[0] != '.')
                {
                    _completionWindow.CompletionList.RequestInsertion(e);
                }
            }
        }
    }

    public class SimpleCompletionData : ICompletionData
    {
        public SimpleCompletionData(string text, string description)
        {
            Text = text;
            Description = description;
        }

        public System.Windows.Media.ImageSource Image => null;
        public string Text { get; private set; }
        public object Content => Text;
        public object Description { get; private set; }
        public double Priority => 0;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            textArea.Document.Replace(completionSegment, Text);
        }
    }
}

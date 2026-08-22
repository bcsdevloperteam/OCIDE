using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

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

        private void TextArea_TextEntered(object sender, TextCompositionEventArgs e)
        {
            if (_editor.SyntaxHighlighting == null) return;
            string lang = _editor.SyntaxHighlighting.Name.ToLower();
            
            // Map highlighting name to extension ID
            string extId = "";
            if (lang.Contains("python")) extId = "lang.python";
            else if (lang.Contains("javascript") || lang.Contains("js")) extId = "lang.js";
            
            if (string.IsNullOrEmpty(extId)) return;
            
            if (!ExtensionManager.IsInstalled(extId)) return; // Extension not installed

            // Check if we typed a valid character to trigger autocomplete (letters)
            if (e.Text.Length > 0 && char.IsLetterOrDigit(e.Text[0]))
            {
                if (_completionWindow == null)
                {
                    var extensions = ExtensionManager.GetInstalledExtensions();
                    var ext = extensions.FirstOrDefault(x => x.Id == extId);
                    if (ext == null || ext.Contributes?.Snippets == null || ext.Contributes.Snippets.Count == 0) return;

                    _completionWindow = new CompletionWindow(_editor.TextArea);
                    IList<ICompletionData> data = _completionWindow.CompletionList.CompletionData;

                    foreach (var kvp in ext.Contributes.Snippets)
                    {
                        foreach (var snippet in kvp.Value)
                        {
                            data.Add(new SimpleCompletionData(kvp.Key, snippet));
                        }
                    }

                    _completionWindow.Show();
                    _completionWindow.Closed += delegate {
                        _completionWindow = null;
                    };
                }
            }
        }

        private void TextArea_TextEntering(object sender, TextCompositionEventArgs e)
        {
            if (e.Text.Length > 0 && _completionWindow != null)
            {
                if (!char.IsLetterOrDigit(e.Text[0]))
                {
                    // Whenever a non-letter is typed while the completion window is open,
                    // insert the currently selected item.
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

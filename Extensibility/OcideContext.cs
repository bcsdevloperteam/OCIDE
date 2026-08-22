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
        public void RegisterCommand(string commandId, Action execute) { /* stub */ }
        public void ExecuteCommand(string commandId) { /* stub */ }
    }

    public class EditorManager : IEditorManager
    {
        public string CurrentFilePath => string.Empty; // stub
        public string CurrentLanguage => string.Empty; // stub
        public void InsertText(int offset, string text) { /* stub */ }
        public void InvokeOnUIThread(Action action) {
            System.Windows.Application.Current.Dispatcher.Invoke(action);
        }
    }

    public class WindowRegistry : IWindowRegistry
    {
        public void AddTab(string title, Control content) { /* stub */ }
    }

    public class Logger : ILogger
    {
        public void LogInfo(string message) => Debug.WriteLine($"[INFO] {message}");
        public void LogError(string message) => Debug.WriteLine($"[ERROR] {message}");
    }
}

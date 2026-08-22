using System;
using System.Windows.Controls;

namespace OCIDE.Extensibility
{
    public interface IExtension
    {
        void Activate(IOcideContext context);
        void Deactivate();
    }

    public interface IOcideContext
    {
        ICommandRegistry Commands { get; }
        IEditorManager Editor { get; }
        IWindowRegistry Windows { get; }
        ILogger Logger { get; }
    }

    public interface ICommandRegistry
    {
        void RegisterCommand(string commandId, Action execute);
        void ExecuteCommand(string commandId);
    }

    public interface IEditorManager
    {
        string CurrentFilePath { get; }
        string CurrentLanguage { get; }
        void InsertText(int offset, string text);
        void InvokeOnUIThread(Action action);
    }

    public interface IWindowRegistry
    {
        void AddTab(string title, Control content);
    }

    public interface ILogger
    {
        void LogInfo(string message);
        void LogError(string message);
    }
}

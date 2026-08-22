using System.Configuration;
using System.Data;
using System.Windows;

namespace OCIDE;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) => {
            System.IO.File.WriteAllText("CRASH_AppDomain.txt", e.ExceptionObject.ToString());
        };
        this.DispatcherUnhandledException += (s, e) => {
            System.IO.File.WriteAllText("CRASH_Dispatcher.txt", e.Exception.ToString());
            e.Handled = true;
        };
    }
}

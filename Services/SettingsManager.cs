using System;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace OCIDE.Services;

public class AppConfig
{
    public double WindowTop { get; set; } = double.NaN;
    public double WindowLeft { get; set; } = double.NaN;
    public double WindowWidth { get; set; } = 1100;
    public double WindowHeight { get; set; } = 700;
    public WindowState WindowState { get; set; } = WindowState.Normal;
    
    public string LastOpenedFolder { get; set; } = string.Empty;
    public string LastOpenedFile { get; set; } = string.Empty;
    public System.Collections.Generic.List<string> RecentProjects { get; set; } = new System.Collections.Generic.List<string>();
}

public static class SettingsManager
{
    private static readonly string SettingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OCIDE");
    private static readonly string SettingsFile = Path.Combine(SettingsFolder, "settings.json");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                string json = File.ReadAllText(SettingsFile);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
        }
        catch { }
        return new AppConfig();
    }

    public static void Save(AppConfig config)
    {
        try
        {
            if (!Directory.Exists(SettingsFolder))
            {
                Directory.CreateDirectory(SettingsFolder);
            }
            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFile, json);
        }
        catch { }
    }
}

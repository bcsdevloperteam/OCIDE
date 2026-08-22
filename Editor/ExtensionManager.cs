using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace OCIDE.Editor
{
    public class ExtensionManifest
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public string Version { get; set; }
        public Dictionary<string, string[]> Snippets { get; set; }
    }

    public static class ExtensionManager
    {
        private static readonly string ExtensionsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Extensions");
        
        static ExtensionManager()
        {
            if (!Directory.Exists(ExtensionsDir))
            {
                Directory.CreateDirectory(ExtensionsDir);
            }
        }

        public static async Task<List<ExtensionManifest>> GetCatalogAsync()
        {
            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    // This is the URL to the GitHub repository where the extensions catalog will be hosted.
                    // Replace 'bcsdevloperteam/OCIDE' with the actual repository where you upload catalog.json.
                    string url = "https://raw.githubusercontent.com/bcsdevloperteam/OCIDE/main/catalog.json";
                    
                    // We add a dummy user-agent because some servers reject requests without one
                    client.DefaultRequestHeaders.Add("User-Agent", "OCIDE-Extension-Manager");
                    
                    string json = await client.GetStringAsync(url);
                    var catalog = JsonSerializer.Deserialize<List<ExtensionManifest>>(json);
                    return catalog ?? new List<ExtensionManifest>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to fetch catalog from GitHub: {ex.Message}");
                return new List<ExtensionManifest>(); // Return empty list if network fails or repo doesn't exist yet
            }
        }

        public static bool IsInstalled(string id)
        {
            return File.Exists(Path.Combine(ExtensionsDir, id + ".json"));
        }

        public static async Task<bool> InstallAsync(string id)
        {
            // Simulate downloading
            await Task.Delay(1000);
            var catalog = await GetCatalogAsync();
            var ext = catalog.FirstOrDefault(x => x.Id == id);
            if (ext != null)
            {
                string json = JsonSerializer.Serialize(ext, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(ExtensionsDir, id + ".json"), json);
                return true;
            }
            return false;
        }

        public static void Uninstall(string id)
        {
            string path = Path.Combine(ExtensionsDir, id + ".json");
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        public static List<ExtensionManifest> GetInstalledExtensions()
        {
            var list = new List<ExtensionManifest>();
            foreach (var file in Directory.GetFiles(ExtensionsDir, "*.json"))
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var ext = JsonSerializer.Deserialize<ExtensionManifest>(json);
                    if (ext != null) list.Add(ext);
                }
                catch { }
            }
            return list;
        }
    }
}

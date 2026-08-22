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
        public string Main { get; set; } // Path to DLL
        public List<string> ActivationEvents { get; set; } = new List<string>();
        public ExtensionContributes Contributes { get; set; } = new ExtensionContributes();
    }

    public class ExtensionContributes
    {
        public Dictionary<string, string[]> Snippets { get; set; } = new Dictionary<string, string[]>();
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
                    string url = "https://raw.githubusercontent.com/bcsdevloperteam/ocide-extenshions/main/catalog.json";
                    
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
            try
            {
                var catalog = await GetCatalogAsync();
                var ext = catalog.FirstOrDefault(x => x.Id == id);
                if (ext != null)
                {
                    // Save the JSON manifest
                    string json = JsonSerializer.Serialize(ext, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(Path.Combine(ExtensionsDir, id + ".json"), json);

                    // If it's an executable extension, download the DLL
                    if (!string.IsNullOrEmpty(ext.Main))
                    {
                        using (var client = new System.Net.Http.HttpClient())
                        {
                            client.DefaultRequestHeaders.Add("User-Agent", "OCIDE-Extension-Manager");
                            
                            // Construct raw GitHub URL to the DLL inside the repository
                            string dllUrl = $"https://raw.githubusercontent.com/bcsdevloperteam/ocide-extenshions/main/Packages/{id}/{ext.Main}";
                            
                            byte[] dllBytes = await client.GetByteArrayAsync(dllUrl);
                            File.WriteAllBytes(Path.Combine(ExtensionsDir, ext.Main), dllBytes);
                        }
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to install extension {id}: {ex.Message}");
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

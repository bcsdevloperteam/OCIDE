using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;

namespace OCIDE.Services
{
    public class LspConfig
    {
        public string ExecutablePath { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
    }

    public class LspManager
    {
        public static LspManager Instance { get; } = new LspManager();

        private Dictionary<string, ILanguageClient> _clients = new Dictionary<string, ILanguageClient>();
        private Dictionary<string, LspConfig> _configs = new Dictionary<string, LspConfig>();

        private LspManager()
        {
            LoadConfigs();
        }

        private void LoadConfigs()
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lsp_config.json");
            if (File.Exists(configPath))
            {
                try
                {
                    var json = File.ReadAllText(configPath);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, LspConfig>>(json);
                    if (dict != null) _configs = dict;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load LSP configs: {ex.Message}");
                }
            }
            else
            {
                _configs = new Dictionary<string, LspConfig>
                {
                    { "python", new LspConfig { ExecutablePath = "node", Arguments = "C:\\path\\to\\pyright\\langserver.index.js --stdio" } },
                    { "csharp", new LspConfig { ExecutablePath = "C:\\path\\to\\omnisharp\\OmniSharp.exe", Arguments = "-lsp" } },
                    { "javascript", new LspConfig { ExecutablePath = "typescript-language-server", Arguments = "--stdio" } }
                };
                try
                {
                    File.WriteAllText(configPath, JsonSerializer.Serialize(_configs, new JsonSerializerOptions { WriteIndented = true }));
                }
                catch { }
            }
        }

        public async Task<ILanguageClient?> GetOrStartClientAsync(string languageId, string workspacePath)
        {
            languageId = languageId.ToLower();
            
            // Map common extension/names to language id
            if (languageId == "c#") languageId = "csharp";
            if (languageId == "js") languageId = "javascript";
            if (languageId == "py") languageId = "python";
            if (languageId == "pythondark") languageId = "python";
            if (languageId == "javascriptdark") languageId = "javascript";
            
            if (_clients.ContainsKey(languageId))
                return _clients[languageId];

            if (!_configs.TryGetValue(languageId, out var config) || string.IsNullOrEmpty(config.ExecutablePath))
                return null;

            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = config.ExecutablePath,
                    Arguments = config.Arguments,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = workspacePath
                };

                var process = new Process { StartInfo = processInfo };
                if (!process.Start())
                    return null;

                var client = LanguageClient.Create(options =>
                {
                    options
                        .WithInput(process.StandardOutput.BaseStream)
                        .WithOutput(process.StandardInput.BaseStream)
                        .WithRootPath(workspacePath);
                });

                await client.Initialize(default);
                _clients[languageId] = client;
                
                return client;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LSP Start Error for {languageId}: {ex.Message}");
                return null;
            }
        }

        public void ShutdownAll()
        {
            foreach (var client in _clients.Values)
            {
                try
                {
                    client.Dispose();
                }
                catch { }
            }
            _clients.Clear();
        }
    }
}

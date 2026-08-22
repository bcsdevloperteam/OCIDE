using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OCIDE.Editor;

namespace OCIDE.Extensibility
{
    public enum ExtensionState
    {
        Unloaded,
        Active,
        Faulted
    }

    public class ExtensionMetadata
    {
        public ExtensionManifest Manifest { get; set; }
        public ExtensionState State { get; set; }
        public string DirectoryPath { get; set; }
        public IExtension Instance { get; set; }
        public ExtensionLoadContext LoadContext { get; set; }
    }

    public class ExtensionHost
    {
        public static ExtensionHost Instance { get; } = new ExtensionHost();

        private Dictionary<string, ExtensionMetadata> _extensions = new Dictionary<string, ExtensionMetadata>();
        private Dictionary<string, List<string>> _activationMap = new Dictionary<string, List<string>>();

        public IOcideContext Context { get; set; }

        private ExtensionHost()
        {
            // Register wildcard listener
            EventAggregator.Subscribe("onLanguage", (lang) => ActivateEvent($"onLanguage:{lang}"));
            EventAggregator.Subscribe("onFileOpen", (ext) => ActivateEvent($"onFileOpen:{ext}"));
            EventAggregator.Subscribe("onStartup", (_) => ActivateEvent("onStartup"));
        }

        public void DiscoverExtensions()
        {
            _extensions.Clear();
            _activationMap.Clear();

            var manifests = ExtensionManager.GetInstalledExtensions();
            foreach (var manifest in manifests)
            {
                var meta = new ExtensionMetadata
                {
                    Manifest = manifest,
                    State = ExtensionState.Unloaded,
                    DirectoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Extensions") // For now, single dir
                };

                _extensions[manifest.Id] = meta;

                foreach (var evt in manifest.ActivationEvents)
                {
                    if (!_activationMap.ContainsKey(evt))
                    {
                        _activationMap[evt] = new List<string>();
                    }
                    _activationMap[evt].Add(manifest.Id);
                }
            }
        }

        public void ActivateEvent(string eventName)
        {
            if (_activationMap.TryGetValue(eventName, out var extIds))
            {
                foreach (var id in extIds.ToList())
                {
                    ActivateExtension(id);
                }
            }
            
            // Also trigger wildcard events (e.g. '*' for onStartup or all languages)
            if (_activationMap.TryGetValue("*", out var allIds))
            {
                foreach (var id in allIds.ToList())
                {
                    ActivateExtension(id);
                }
            }
        }

        public void ActivateExtension(string id)
        {
            if (!_extensions.TryGetValue(id, out var meta)) return;
            if (meta.State != ExtensionState.Unloaded) return; // Already active or faulted

            try
            {
                // If it has a DLL, load it dynamically
                if (!string.IsNullOrEmpty(meta.Manifest.Main))
                {
                    ExtensionLoader.LoadAndActivate(meta, Context);
                }
                
                meta.State = ExtensionState.Active;
                System.Diagnostics.Debug.WriteLine($"Activated Extension: {id}");
            }
            catch (Exception ex)
            {
                meta.State = ExtensionState.Faulted;
                Context?.Logger?.LogError($"Failed to activate {id}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"CRASH in Extension {id}: {ex.ToString()}");
            }
        }

        public void DeactivateAll()
        {
            foreach (var kvp in _extensions)
            {
                if (kvp.Value.State == ExtensionState.Active)
                {
                    try
                    {
                        kvp.Value.Instance?.Deactivate();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error deactivating {kvp.Key}: {ex.Message}");
                    }
                    finally
                    {
                        ExtensionLoader.Unload(kvp.Value);
                        kvp.Value.State = ExtensionState.Unloaded;
                    }
                }
            }
        }
    }
}

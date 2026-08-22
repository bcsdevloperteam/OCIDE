using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace OCIDE.Extensibility
{
    public class ExtensionLoadContext : AssemblyLoadContext
    {
        private AssemblyDependencyResolver _resolver;

        public ExtensionLoadContext(string pluginPath) : base(isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(pluginPath);
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            string assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }
            return null;
        }
    }

    public static class ExtensionLoader
    {
        public static void LoadAndActivate(ExtensionMetadata meta, IOcideContext context)
        {
            string dllPath = Path.Combine(meta.DirectoryPath, meta.Manifest.Main);
            if (!File.Exists(dllPath))
            {
                throw new FileNotFoundException($"Extension DLL not found: {dllPath}");
            }

            // Create a collectible load context so we can unload it later
            meta.LoadContext = new ExtensionLoadContext(dllPath);
            
            // Load the assembly
            Assembly assembly = meta.LoadContext.LoadFromAssemblyPath(dllPath);
            
            // Find the class implementing IExtension
            var type = assembly.GetTypes().FirstOrDefault(t => typeof(IExtension).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
            if (type == null)
            {
                throw new InvalidOperationException($"No class implementing IExtension found in {dllPath}");
            }

            // Instantiate and activate
            meta.Instance = (IExtension)Activator.CreateInstance(type);
            meta.Instance.Activate(context);
        }

        public static void Unload(ExtensionMetadata meta)
        {
            meta.Instance = null;
            if (meta.LoadContext != null)
            {
                meta.LoadContext.Unload();
                meta.LoadContext = null;
            }
            
            // Force GC to clean up collectible assemblies
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }
}

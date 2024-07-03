using System.Diagnostics;
using System.IO;
using SwarmUI.Utils;

namespace SwarmUI.Core.Systems;

public class ExtensionManager: IExtensionManager
{
    private readonly List<string> _sources = [];
    private readonly List<Extension> _extensions = [];

    internal ExtensionManager()
    {
        AddExtensionPath("src/BuiltinExtensions");
    }

    public void AddExtensionPath(string path)
    {
        Debug.Assert(path != null);

        if (_sources.Contains(path))
        {
            Logs.Warning($"Duplicate extension path to {path} requested. Path registration ignored.");
            return;
        }

        if (!Directory.Exists(path))
        {
            Logs.Error($"Missing/invalid extension path: {path}");
            throw new DirectoryNotFoundException();
        }

        Logs.Info($"Registering extension path: {path}");
        _sources.Add(path);
    }

    public void Setup()
    {
        var validAssemblies = AppDomain.CurrentDomain.GetAssemblies().ToList().SelectMany(x => x.GetTypes())
            .Where(t => typeof(Extension).IsAssignableFrom(t) && !t.IsAbstract);

        foreach (Type typeInfo in validAssemblies)
        {
            try
            {
                Logs.Init($"Prepping extension: {typeInfo.FullName}...");
                Extension extension = Activator.CreateInstance(typeInfo) as Extension;
                extension.ExtensionName = typeInfo.Name;

                _extensions.Add(extension);

                if (typeInfo.Namespace.StartsWith("SwarmUI."))
                {
                    ProcessExtensionIn(_sources[0], extension, typeInfo);
                }
                else
                {
                    for (int i = 1; i < _sources.Count; ++i)
                    {
                        ProcessExtensionIn(_sources[i], extension, typeInfo);
                    }
                }

            }
            catch (Exception ex)
            {
                Logs.Error($"Failed to create extension of type {typeInfo.FullName}: {ex}");
            }
        }

        RunOnAll(e => e.OnFirstInit());
    }

    private static void ProcessExtensionIn(string path, Extension extension, Type typeInfo)
    {
        var entries = Directory.EnumerateDirectories(path)
            .Select(s => s.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).ToArray();
        foreach (var entry in entries)
        {
            var target = $"{entry}/{typeInfo.Name}.cs";
            if (File.Exists(target))
            {
                if (extension.FilePath is not null)
                {
                    Logs.Error($"Multiple extensions with the same name {typeInfo.Name}! Something will break.");
                }
                extension.FilePath = $"{entry}/";
            }
        }

        if (extension.FilePath is null)
        {
            Logs.Error($"Could not determine path for extension {typeInfo.Name} - is the classname mismatched from the filename? Searched in {string.Join(", ", entries)} for '{typeInfo.Name}.cs'");
        }
    }

    public void Shutdown()
    {
        RunOnAll(e => e.OnShutdown());
    }

    public void RunOnAll(Action<Extension> action)
    {
        foreach (var extension in _extensions)
        {
            try
            {
                action(extension);
            }
            catch (Exception ex)
            {
                Logs.Error($"Failed to run event on extension {extension.GetType().FullName}: {ex}");
            }
        }
    }

}

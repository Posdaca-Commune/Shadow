using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Shadow.Abstractions;

namespace Shadow.Plugins;

internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private static readonly string[] SharedAssemblyPrefixes =
    [
        "Avalonia",
        "FluentAvalonia",
        "CommunityToolkit.Mvvm",
    ];

    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginAssemblyPath)
        : base(isCollectible: false)
    {
        _resolver = new AssemblyDependencyResolver(pluginAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name is "Shadow.Abstractions"
            || SharedAssemblyPrefixes.Any(prefix => assemblyName.Name?.StartsWith(prefix, StringComparison.Ordinal) == true))
        {
            return assemblyName.Name is "Shadow.Abstractions"
                ? typeof(IShadowPlugin).Assembly
                : Assembly.Load(assemblyName);
        }

        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        return assemblyPath is null ? null : LoadFromAssemblyPath(assemblyPath);
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return libraryPath is null ? 0 : LoadUnmanagedDllFromPath(libraryPath);
    }
}

using System;
using System.Collections.Generic;
using System.Runtime.Loader;

namespace PSOpenAD.Native;

internal static class NativeResolver
{
    private static readonly Dictionary<string, LibraryInfo> NativeHandles = new();

    static NativeResolver()
    {
        AssemblyLoadContext.Default.ResolvingUnmanagedDll += (_, libraryName) => NativeHandles.TryGetValue(libraryName, out var library) ? library.Handle : IntPtr.Zero;
    }

    public static LibraryInfo? CacheLibrary(string id, string[] paths)
    {
        string? envOverride = Environment.GetEnvironmentVariable(id.ToUpperInvariant().Replace(".", "_"));
        if (!String.IsNullOrWhiteSpace(envOverride))
            paths = new[] { envOverride };

        foreach (string libPath in paths)
        {
            try
            {
                NativeHandles[id] = new LibraryInfo(id, libPath);
                return NativeHandles[id];
            }
            catch (DllNotFoundException) { }
        }

        return null;
    }
}
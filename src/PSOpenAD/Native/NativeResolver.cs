using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Loader;

namespace PSOpenAD;

internal sealed class NativeResolver : IDisposable
{
    private readonly Dictionary<string, LibraryInfo> NativeHandles = new();

    public NativeResolver()
    {
        AssemblyLoadContext.Default.ResolvingUnmanagedDll += ImportResolver;
    }

    public LibraryInfo? CacheLibrary(string id, string[] paths)
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

    private IntPtr ImportResolver(Assembly assembly, string libraryName)
    {
        if (NativeHandles.ContainsKey(libraryName))
            return NativeHandles[libraryName].Handle;

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (KeyValuePair<string, LibraryInfo> native in NativeHandles)
            native.Value.Dispose();

        AssemblyLoadContext.Default.ResolvingUnmanagedDll -= ImportResolver;
        GC.SuppressFinalize(this);
    }
    ~NativeResolver() { Dispose(); }
}
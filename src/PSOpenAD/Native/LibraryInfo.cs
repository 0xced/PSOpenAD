using System;
using System.Runtime.InteropServices;

namespace PSOpenAD.Native;

internal sealed class LibraryInfo
{
    public string Id { get; }
    public string Path { get; }
    public IntPtr Handle { get; }

    public LibraryInfo(string id, string path)
    {
        Id = id;
        Path = path;
        Handle = NativeLibrary.Load(path);
    }
}

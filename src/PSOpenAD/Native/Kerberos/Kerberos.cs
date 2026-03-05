namespace PSOpenAD.Native;

internal partial class Kerberos
{
    private const string LIB_KRB5 = "PSOpenAD.libkrb5";

    static Kerberos()
    {
        LibraryInfo? krb5Lib = NativeResolver.CacheLibrary(LIB_KRB5, [
            "/System/Library/PrivateFrameworks/Heimdal.framework/Heimdal", // macOS Heimdal Framework
            "libkrb5.so.3", // MIT krb5
            "libkrb5.so.26", "libkrb5.so", // Heimdal
        ]);

        IsAvailable = krb5Lib != null;
    }

    public static bool IsAvailable { get; private set; }
}
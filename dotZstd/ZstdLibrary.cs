using System.Reflection;
using System.Runtime.InteropServices;

namespace nebulae.dotZstd;

internal static class ZstdLibrary
{
    private static bool _isLoaded;

    internal static void Init()
    {
        if (_isLoaded)
            return;

        NativeLibrary.SetDllImportResolver(typeof(ZstdLibrary).Assembly, Resolve);

        _isLoaded = true;
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != "libzstd")
            return IntPtr.Zero;

        var libName = GetPlatformLibraryName();
        var assemblyDir = Path.GetDirectoryName(typeof(ZstdLibrary).Assembly.Location)!;
        var fullPath = Path.Combine(assemblyDir, libName);

        if (!File.Exists(fullPath))
            throw new DllNotFoundException($"Could not find native Zstandard library at {fullPath}");

        return NativeLibrary.Load(fullPath);
    }

    private static string GetPlatformLibraryName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Path.Combine("runtimes", "win-x64", "native", "libzstd.dll");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return Path.Combine("runtimes", "linux-x64", "native", "libzstd.so");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                return Path.Combine("runtimes", "osx-arm64", "native", "libzstd.dylib");

            return Path.Combine("runtimes", "osx-x64", "native", "libzstd.dylib");
        }

        throw new PlatformNotSupportedException("Unsupported platform");
    }
}

using System.Reflection;
using System.Runtime.InteropServices;

namespace nebulae.dotZstd;

internal static class ZstdLibrary
{
    private static bool _isLoaded;

    internal static void Init()
    {
        if (_isLoaded) return;
        NativeLibrary.SetDllImportResolver(typeof(ZstdLibrary).Assembly, Resolve);
        _isLoaded = true;
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        // Load libzstd
        if (libraryName == "libzstd")
        {
            var fullPath = Path.Combine(
                Path.GetDirectoryName(assembly.Location)!,
                GetPlatformLibraryPath("libzstd"));
            if (!File.Exists(fullPath))
                throw new DllNotFoundException($"Could not find native Zstandard library at {fullPath}");
            return NativeLibrary.Load(fullPath);
        }

        // Load the shim
        if (libraryName == "zstd_dict_shim")
        {
            // Ensure libzstd is loaded first so the shim binds to the same module
            _ = Resolve("libzstd", assembly, searchPath);

            var fullPath = Path.Combine(
                Path.GetDirectoryName(assembly.Location)!,
                GetPlatformLibraryPath("zstd_dict_shim"));
            if (!File.Exists(fullPath))
                throw new DllNotFoundException($"Could not find zstd dict shim at {fullPath}");
            return NativeLibrary.Load(fullPath);
        }

        return IntPtr.Zero;
    }

    private static string GetPlatformLibraryPath(string which)
    {
        // map (name, OS) -> file name
        string file =
            which == "libzstd"
            ? (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "libzstd.dll"
              : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "libzstd.so"
              : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "libzstd.dylib"
              : throw new PlatformNotSupportedException())
            : /* which == shim */
              (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "zstd_dict_shim.dll"
              : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "zstd_dict_shim.so"
              : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "zstd_dict_shim.dylib"
              : throw new PlatformNotSupportedException());

        // paths under runtimes/<rid>/native/
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Path.Combine("runtimes", "win-x64", "native", file);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return Path.Combine("runtimes", "linux-x64", "native", file);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var rid = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
            return Path.Combine("runtimes", rid, "native", file);
        }

        throw new PlatformNotSupportedException("Unsupported platform");
    }
}

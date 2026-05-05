using QmlSharp.Host.Engine;

namespace {{RootNamespace}};

internal static class Program
{
    public static int Main(string[] args)
    {
        string distDirectory = Path.Combine(AppContext.BaseDirectory, "dist");
        string nativeLibraryPath = Path.Combine(distDirectory, "native", GetNativeLibraryName());
        string rootQmlPath = Path.Combine(
            distDirectory,
            "qml",
            "QmlSharp",
            "{{ProjectName}}",
            "{{ViewName}}.qml");

        using QmlSharpEngine engine = new(nativeLibraryPath);
        engine.Initialize(distDirectory, args, rootQmlPath);
        return engine.Run(rootQmlPath);
    }

    private static string GetNativeLibraryName()
    {
        if (OperatingSystem.IsWindows())
        {
            return "qmlsharp_native.dll";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "libqmlsharp_native.dylib";
        }

        return "libqmlsharp_native.so";
    }
}

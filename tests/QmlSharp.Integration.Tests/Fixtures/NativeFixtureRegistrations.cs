using System.Runtime.InteropServices;

namespace QmlSharp.Integration.Tests.Fixtures
{
    internal sealed class NativeFixtureRegistrations
    {
        private static readonly Lock CacheLock = new();
        private static readonly StringComparer NativePathComparer = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        private static readonly Dictionary<string, NativeFixtureRegistrations> RegistrationsByPath = new(NativePathComparer);

        private readonly IntPtr libraryHandle;
        private readonly RegisterCounterViewModelDelegate registerCounterViewModel;

        private NativeFixtureRegistrations(string fullNativeLibraryPath)
        {
            libraryHandle = NativeLibrary.Load(fullNativeLibraryPath);
            try
            {
                IntPtr export = NativeLibrary.GetExport(
                    libraryHandle,
                    "qmlsharp_test_register_registration_counter_view_model");
                registerCounterViewModel = Marshal.GetDelegateForFunctionPointer<RegisterCounterViewModelDelegate>(export);
            }
            catch
            {
                NativeLibrary.Free(libraryHandle);
                throw;
            }
        }

        public static NativeFixtureRegistrations ForLibrary(string nativeLibraryPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(nativeLibraryPath);
            string fullPath = Path.GetFullPath(nativeLibraryPath);

            lock (CacheLock)
            {
                if (!RegistrationsByPath.TryGetValue(fullPath, out NativeFixtureRegistrations? registrations))
                {
                    registrations = new NativeFixtureRegistrations(fullPath);
                    RegistrationsByPath.Add(fullPath, registrations);
                }

                return registrations;
            }
        }

        public int RegisterRegistrationCounterViewModel(
            string moduleUri,
            int versionMajor,
            int versionMinor,
            string typeName)
        {
            int result = registerCounterViewModel(moduleUri, versionMajor, versionMinor, typeName);
            GC.KeepAlive(libraryHandle);
            return result;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int RegisterCounterViewModelDelegate(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string moduleUri,
            int versionMajor,
            int versionMinor,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string typeName);
    }
}

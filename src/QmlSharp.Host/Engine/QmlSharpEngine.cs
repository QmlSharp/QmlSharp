using System.Collections.ObjectModel;
using System.Globalization;
using QmlSharp.Compiler;
using QmlSharp.Host.ArtifactValidation;
using QmlSharp.Host.Commands;
using QmlSharp.Host.DevTools;
using QmlSharp.Host.Effects;
using QmlSharp.Host.ErrorOverlay;
using QmlSharp.Host.Exceptions;
using QmlSharp.Host.HotReload;
using QmlSharp.Host.Interop;
using QmlSharp.Host.Metrics;
using QmlSharp.Host.StateSynchronization;
using ManagedInstanceRegistry = QmlSharp.Host.Instances.InstanceRegistry;

namespace QmlSharp.Host.Engine
{
    /// <summary>Public entry point for the managed native-host facade.</summary>
    public sealed class QmlSharpEngine : IDisposable
    {
        private readonly IArtifactValidator artifactValidator;
        private readonly INativeHostInterop interop;
        private readonly bool ownsInterop;
        private readonly QmlSharpTypeRegistrationCallback? defaultTypeRegistrationCallback;
        private readonly Lock syncRoot = new();
        private readonly Dictionary<string, string> schemaIdsByClassName = new(StringComparer.Ordinal);
        private readonly Dictionary<string, StateSyncSchemaMetadata> stateSchemasById = new(StringComparer.Ordinal);
        private IntPtr engineHandle;
        private ErrorOverlayController? errorOverlay;
        private bool disposed;

        public QmlSharpEngine()
            : this(new ArtifactValidator(), new UnconfiguredNativeHostInterop(), ownsInterop: false, defaultTypeRegistrationCallback: null)
        {
        }

        public QmlSharpEngine(string nativeLibraryPath)
            : this(nativeLibraryPath, defaultTypeRegistrationCallback: null)
        {
        }

        public QmlSharpEngine(
            string nativeLibraryPath,
            QmlSharpTypeRegistrationCallback? defaultTypeRegistrationCallback)
            : this(new ArtifactValidator(), new NativeHostLibrary(nativeLibraryPath), ownsInterop: true, defaultTypeRegistrationCallback)
        {
        }

        internal QmlSharpEngine(IArtifactValidator artifactValidator)
            : this(artifactValidator, new UnconfiguredNativeHostInterop(), ownsInterop: false, defaultTypeRegistrationCallback: null)
        {
        }

        internal QmlSharpEngine(
            IArtifactValidator artifactValidator,
            INativeHostInterop interop,
            bool ownsInterop = false,
            QmlSharpTypeRegistrationCallback? defaultTypeRegistrationCallback = null)
        {
            ArgumentNullException.ThrowIfNull(artifactValidator);
            ArgumentNullException.ThrowIfNull(interop);
            this.artifactValidator = artifactValidator;
            this.interop = interop;
            this.ownsInterop = ownsInterop;
            this.defaultTypeRegistrationCallback = defaultTypeRegistrationCallback;

            Instances = new ManagedInstanceRegistry();
            Commands = new CommandRouter(Instances);
            State = new StateSync(Instances, interop);
            Effects = new EffectRouter(Instances, interop);
            HotReload = new HotReloadCoordinator(Instances, interop, State, Commands, Effects);
            DevTools = new DevToolsFacade(Instances);
        }

        /// <summary>The native ABI version this managed host expects.</summary>
        public int ExpectedAbiVersion => NativeHostAbi.SupportedAbiVersion;

        /// <summary>Whether the native engine has been initialized and not shut down.</summary>
        public bool IsInitialized => engineHandle != IntPtr.Zero;

        /// <summary>The managed instance registry for runtime instance tracking.</summary>
        public ManagedInstanceRegistry Instances { get; }

        /// <summary>The command router for handling QML command callbacks.</summary>
        public CommandRouter Commands { get; }

        /// <summary>The state synchronization coordinator.</summary>
        public StateSync State { get; private set; }

        /// <summary>The effect router for dispatching managed effects to QML.</summary>
        public EffectRouter Effects { get; }

        /// <summary>The hot reload coordinator.</summary>
        public HotReloadCoordinator HotReload { get; private set; }

        /// <summary>The managed dev-tools inspection facade.</summary>
        public DevToolsFacade DevTools { get; }

        /// <summary>The native error overlay controller for development diagnostics.</summary>
        public ErrorOverlayController ErrorOverlay => errorOverlay ?? throw new EngineNotInitializedException();

        /// <summary>Validates startup artifacts before native engine initialization.</summary>
        public ArtifactValidationResult ValidateStartupArtifacts(string distDirectory, string? rootQmlFilePath = null)
        {
            return artifactValidator.Validate(new ArtifactValidationRequest(distDirectory, rootQmlFilePath));
        }

        /// <summary>Validates startup artifacts and throws if fatal diagnostics would prevent startup.</summary>
        public ArtifactValidationResult EnsureStartupArtifactsValid(string distDirectory, string? rootQmlFilePath = null)
        {
            ArtifactValidationResult result = ValidateStartupArtifacts(distDirectory, rootQmlFilePath);
            if (!result.IsValid)
            {
                throw new ArtifactValidationException(result);
            }

            return result;
        }

        /// <summary>Validates artifacts, initializes the native engine, and installs managed callbacks.</summary>
        public void Initialize(string distDirectory, string[]? args = null, string? rootQmlFilePath = null)
        {
            _ = EnsureStartupArtifactsValid(distDirectory, rootQmlFilePath);
            Initialize(args);
        }

        /// <summary>Initializes the native Qt engine and installs managed callbacks.</summary>
        public void Initialize(string[]? args = null)
        {
            lock (syncRoot)
            {
                ThrowIfDisposed();
                if (engineHandle != IntPtr.Zero)
                {
                    return;
                }

                int actualAbiVersion = interop.GetAbiVersion();
                if (actualAbiVersion != NativeHostAbi.SupportedAbiVersion)
                {
                    throw new AbiVersionMismatchException(NativeHostAbi.SupportedAbiVersion, actualAbiVersion);
                }

                IReadOnlyList<string> arguments = args is null
                    ? Array.Empty<string>()
                    : Array.AsReadOnly(args);
                IntPtr initializedHandle = interop.EngineInit(arguments);
                if (initializedHandle == IntPtr.Zero)
                {
                    string detail = interop.GetLastError() ?? "Native engine initialization returned a null handle.";
                    throw new NativeHostException(-1, detail);
                }

                engineHandle = initializedHandle;
                interop.SetInstanceCallbacks(OnNativeInstanceCreated, OnNativeInstanceDestroyed);
                interop.SetCommandCallback(OnNativeCommand);
                errorOverlay = new ErrorOverlayController(interop, engineHandle);
            }
        }

        /// <summary>Registers all ViewModel schemas using the default native type registration callback.</summary>
        public void RegisterTypes(IReadOnlyList<ViewModelSchema> schemas)
        {
            RegisterTypes(schemas, ResolveDefaultTypeRegistrationCallback());
        }

        /// <summary>Registers all ViewModel schemas using the supplied generated type registration callback.</summary>
        public void RegisterTypes(
            IReadOnlyList<ViewModelSchema> schemas,
            QmlSharpTypeRegistrationCallback registerCallback)
        {
            ArgumentNullException.ThrowIfNull(schemas);
            ArgumentNullException.ThrowIfNull(registerCallback);
            foreach (IGrouping<ModuleKey, ViewModelSchema> module in schemas
                         .GroupBy(static schema => new ModuleKey(schema.ModuleUri, schema.ModuleVersion.Major, schema.ModuleVersion.Minor))
                         .OrderBy(static module => module.Key.ModuleUri, StringComparer.Ordinal)
                         .ThenBy(static module => module.Key.VersionMajor)
                         .ThenBy(static module => module.Key.VersionMinor))
            {
                RegisterModule(module.Key.ModuleUri, module.Key.VersionMajor, module.Key.VersionMinor, module.ToArray(), registerCallback);
            }
        }

        /// <summary>Registers one QML module from schema metadata using the default native callback.</summary>
        public void RegisterModule(
            string moduleUri,
            int versionMajor,
            int versionMinor,
            IReadOnlyList<ViewModelSchema> schemas)
        {
            RegisterModule(moduleUri, versionMajor, versionMinor, schemas, ResolveDefaultTypeRegistrationCallback());
        }

        /// <summary>Registers one QML module from schema metadata using the supplied generated native callback.</summary>
        public void RegisterModule(
            string moduleUri,
            int versionMajor,
            int versionMinor,
            IReadOnlyList<ViewModelSchema> schemas,
            QmlSharpTypeRegistrationCallback registerCallback)
        {
            ArgumentNullException.ThrowIfNull(schemas);
            ArgumentNullException.ThrowIfNull(registerCallback);
            QmlSharpTypeRegistrationEntry[] entries = schemas
                .Select(schema => CreateRegistrationEntry(schema, registerCallback))
                .OrderBy(static entry => entry.TypeName, StringComparer.Ordinal)
                .ToArray();
            RegisterModule(moduleUri, versionMajor, versionMinor, entries);
            RegisterStateMetadata(schemas);
        }

        /// <summary>Registers one generated native type using a caller-provided type registration callback.</summary>
        public void RegisterType(
            string moduleUri,
            int versionMajor,
            int versionMinor,
            QmlSharpTypeRegistrationEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);
            EnsureInitialized();
            int resultCode = ExecuteNative(() => interop.RegisterType(
                engineHandle,
                moduleUri,
                versionMajor,
                versionMinor,
                entry.TypeName,
                entry.SchemaId,
                entry.CompilerSlotKey,
                entry.RegisterCallback));
            NativeErrorMapper.ThrowIfFailed(resultCode, interop, "qmlsharp_register_type");
        }

        /// <summary>Registers generated native types for one QML module.</summary>
        public void RegisterModule(
            string moduleUri,
            int versionMajor,
            int versionMinor,
            IReadOnlyList<QmlSharpTypeRegistrationEntry> entries)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(moduleUri);
            ArgumentOutOfRangeException.ThrowIfNegative(versionMajor);
            ArgumentOutOfRangeException.ThrowIfNegative(versionMinor);
            ArgumentNullException.ThrowIfNull(entries);
            EnsureInitialized();

            int resultCode = ExecuteNative(() => interop.RegisterModule(
                engineHandle,
                moduleUri,
                versionMajor,
                versionMinor,
                entries));
            NativeErrorMapper.ThrowIfFailed(resultCode, interop, "qmlsharp_register_module");
        }

        /// <summary>Loads the caller-selected root QML file and enters the Qt event loop.</summary>
        public int Run(string qmlPath)
        {
            LoadRootQml(qmlPath);
            int exitCode = ExecuteNative(() => interop.EngineExec(engineHandle));
            if (exitCode < 0)
            {
                NativeErrorMapper.ThrowIfFailed(exitCode, interop, "qmlsharp_engine_exec");
            }

            return exitCode;
        }

        /// <summary>Loads or reloads the caller-selected root QML file without entering the event loop.</summary>
        public void LoadRootQml(string qmlPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(qmlPath);
            EnsureInitialized();
            string fullPath = Path.GetFullPath(qmlPath);
            if (!File.Exists(fullPath))
            {
                throw new QmlLoadException(fullPath, "The QML source file was not found.");
            }

            int resultCode = ExecuteNative(() => interop.ReloadQml(engineHandle, fullPath));
            NativeErrorMapper.ThrowIfFailed(resultCode, interop, "qmlsharp_reload_qml", qmlPath: fullPath);
        }

        /// <summary>Marks a managed instance ready and opens the native pre-ready command queue.</summary>
        public void InstanceReady(string instanceId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
            EnsureInitialized();
            _ = Commands.MarkReady(instanceId);
            _ = ExecuteNative(() =>
            {
                interop.InstanceReady(instanceId);
                return 0;
            });
        }

        /// <summary>Registers a command handler for a named command on one instance.</summary>
        public void RegisterCommandHandler(string instanceId, string commandName, Action<CommandInvocation> handler)
        {
            Commands.RegisterCommandHandler(instanceId, commandName, handler);
        }

        /// <summary>Registers an asynchronous command handler for a named command on one instance.</summary>
        public void RegisterCommandHandler(string instanceId, int commandId, string commandName, Func<CommandInvocation, Task> handler)
        {
            Commands.RegisterCommandHandler(instanceId, commandId, commandName, handler);
        }

        /// <summary>Pushes one managed state value to the native QObject instance.</summary>
        public void SyncState(string instanceId, string propertyName, object? value)
        {
            State.Push(instanceId, propertyName, value);
        }

        /// <summary>Dispatches one named effect to a specific instance.</summary>
        public EffectDispatchResult DispatchEffect(string instanceId, string effectName, object? payload = null)
        {
            return Effects.Dispatch(instanceId, effectName, payload);
        }

        /// <summary>Broadcasts one named effect to every active instance of a ViewModel class.</summary>
        public EffectDispatchResult BroadcastEffect(string className, string effectName, object? payload = null)
        {
            return Effects.Broadcast(className, effectName, payload);
        }

        /// <summary>Runs the coordinated hot reload protocol against the active native engine.</summary>
        public Task<HotReloadResult> ReloadAsync(string qmlSourcePath, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            return HotReload.ReloadAsync(engineHandle, qmlSourcePath, cancellationToken);
        }

        /// <summary>Gets native dev-tools JSON for one instance.</summary>
        public string? GetNativeInstanceInfoJson(string instanceId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
            EnsureInitialized();
            string? payload = ExecuteNative(() => interop.GetNativeInstanceInfo(instanceId));
            if (payload is null)
            {
                string? detail = interop.GetLastError();
                if (!string.IsNullOrWhiteSpace(detail))
                {
                    throw new InstanceNotFoundException(instanceId);
                }
            }

            return payload;
        }

        /// <summary>Gets native dev-tools JSON for all currently active native instances.</summary>
        public string GetNativeAllInstancesJson()
        {
            EnsureInitialized();
            return ExecuteNative(interop.GetNativeAllInstances) ?? "[]";
        }

        /// <summary>Gets native runtime metrics JSON.</summary>
        public string GetNativeMetricsJson()
        {
            EnsureInitialized();
            return ExecuteNative(interop.GetNativeMetrics) ?? "{}";
        }

        /// <summary>Gets managed runtime metrics.</summary>
        public RuntimeMetrics GetRuntimeMetrics()
        {
            return DevTools.GetRuntimeMetrics();
        }

        /// <summary>Shuts down the native engine. Repeated calls are harmless.</summary>
        public void Shutdown()
        {
            lock (syncRoot)
            {
                ThrowIfDisposed();
                if (engineHandle == IntPtr.Zero)
                {
                    return;
                }

                IntPtr handleToShutdown = engineHandle;
                engineHandle = IntPtr.Zero;
                errorOverlay = null;
                try
                {
                    interop.EngineShutdown(handleToShutdown);
                }
                finally
                {
                    interop.SetCommandCallback(null);
                    interop.SetInstanceCallbacks(null, null);
                }
            }
        }

        /// <summary>Shuts down native resources and disposes managed coordinators.</summary>
        public void Dispose()
        {
            if (!disposed)
            {
                Shutdown();
                Commands.Dispose();
                Effects.Dispose();
                Instances.Dispose();
                if (ownsInterop && interop is IDisposable disposableInterop)
                {
                    disposableInterop.Dispose();
                }

                disposed = true;
            }

            GC.SuppressFinalize(this);
        }

        private QmlSharpTypeRegistrationCallback ResolveDefaultTypeRegistrationCallback()
        {
            return defaultTypeRegistrationCallback
                ?? throw new InvalidOperationException(
                    "A generated native type registration callback is required to register schemas.");
        }

        private void RegisterStateMetadata(IReadOnlyList<ViewModelSchema> schemas)
        {
            foreach (ViewModelSchema schema in schemas)
            {
                string schemaId = GetSchemaId(schema);
                schemaIdsByClassName[schema.ClassName] = schemaId;
                StateSyncSchemaMetadata metadata = new(
                    schemaId,
                    schema.Properties.Select(static property => new StateSyncPropertyMetadata(
                        property.Name,
                        MapStateValueKind(property.Type))));
                stateSchemasById[schemaId] = metadata;
            }

            ReadOnlyCollection<StateSyncSchemaMetadata> schemaMetadata = stateSchemasById.Values
                .OrderBy(static schema => schema.SchemaId, StringComparer.Ordinal)
                .ToArray()
                .AsReadOnly();
            State = new StateSync(Instances, interop, schemaMetadata);
            HotReload = new HotReloadCoordinator(Instances, interop, State, Commands, Effects);
        }

        private static QmlSharpTypeRegistrationEntry CreateRegistrationEntry(
            ViewModelSchema schema,
            QmlSharpTypeRegistrationCallback registerCallback)
        {
            ArgumentNullException.ThrowIfNull(schema);
            return new QmlSharpTypeRegistrationEntry(
                schema.ClassName,
                GetSchemaId(schema),
                schema.CompilerSlotKey,
                registerCallback);
        }

        private static string GetSchemaId(ViewModelSchema schema)
        {
            return schema.ClassName;
        }

        private static StateSyncValueKind MapStateValueKind(string typeName)
        {
            return typeName.ToLowerInvariant() switch
            {
                "string" => StateSyncValueKind.String,
                "int" or "int32" => StateSyncValueKind.Int32,
                "double" or "single" or "float" => StateSyncValueKind.Double,
                "bool" or "boolean" => StateSyncValueKind.Boolean,
                _ => StateSyncValueKind.Json
            };
        }

        private T ExecuteNative<T>(Func<T> operation)
        {
            if (interop.IsOnMainThread)
            {
                return operation();
            }

            using ManualResetEventSlim completed = new();
            T? result = default;
            Exception? exception = null;
            interop.PostToMainThread(() =>
            {
                try
                {
                    result = operation();
                }
                catch (Exception capturedException) when (!IsCriticalException(capturedException))
                {
                    exception = capturedException;
                }
                finally
                {
                    completed.Set();
                }
            });
            completed.Wait();

            if (exception is not null)
            {
                throw exception;
            }

            return result!;
        }

        private void OnNativeInstanceCreated(string instanceId, string className, string compilerSlotKey)
        {
            string schemaId = schemaIdsByClassName.TryGetValue(className, out string? foundSchemaId)
                ? foundSchemaId
                : className;
            _ = Instances.Register(instanceId, className, schemaId, compilerSlotKey, IntPtr.Zero, IntPtr.Zero);
        }

        private void OnNativeInstanceDestroyed(string instanceId)
        {
            Commands.ClearInstance(instanceId);
            Effects.ClearInstance(instanceId);
            _ = Instances.Unregister(instanceId);
        }

        private void OnNativeCommand(string instanceId, string commandName, string argsJson)
        {
            _ = Commands.OnCommand(instanceId, commandName, argsJson);
        }

        private void EnsureInitialized()
        {
            ThrowIfDisposed();
            if (engineHandle == IntPtr.Zero)
            {
                throw new EngineNotInitializedException();
            }
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
        }

        private static bool IsCriticalException(Exception exception)
        {
            return exception is OutOfMemoryException
                or StackOverflowException
                or AccessViolationException
                or AppDomainUnloadedException
                or BadImageFormatException
                or CannotUnloadAppDomainException
                or InvalidProgramException
                or ThreadAbortException;
        }

        private readonly record struct ModuleKey(string ModuleUri, int VersionMajor, int VersionMinor);

        private sealed class UnconfiguredNativeHostInterop : INativeHostInterop
        {
            public bool IsOnMainThread => true;

            public int GetAbiVersion()
            {
                throw MissingNativeLibrary();
            }

            public IntPtr EngineInit(IReadOnlyList<string> arguments)
            {
                throw MissingNativeLibrary();
            }

            public void EngineShutdown(IntPtr engineHandle)
            {
            }

            public int EngineExec(IntPtr engineHandle)
            {
                throw MissingNativeLibrary();
            }

            public string? GetLastError()
            {
                return null;
            }

            public void FreeString(IntPtr pointer)
            {
            }

            public void PostToMainThread(Action callback)
            {
                throw MissingNativeLibrary();
            }

            public int RegisterType(
                IntPtr engineHandle,
                string moduleUri,
                int versionMajor,
                int versionMinor,
                string typeName,
                string schemaId,
                string compilerSlotKey,
                QmlSharpTypeRegistrationCallback registerCallback)
            {
                throw MissingNativeLibrary();
            }

            public int RegisterModule(
                IntPtr engineHandle,
                string moduleUri,
                int versionMajor,
                int versionMinor,
                IReadOnlyList<QmlSharpTypeRegistrationEntry> entries)
            {
                throw MissingNativeLibrary();
            }

            public void SetInstanceCallbacks(
                Action<string, string, string>? onCreated,
                Action<string>? onDestroyed)
            {
                throw MissingNativeLibrary();
            }

            public void InstanceReady(string instanceId)
            {
                throw MissingNativeLibrary();
            }

            public void SetCommandCallback(Action<string, string, string>? callback)
            {
                throw MissingNativeLibrary();
            }

            public int SyncStateString(string instanceId, string propertyName, string? value)
            {
                throw MissingNativeLibrary();
            }

            public int SyncStateInt(string instanceId, string propertyName, int value)
            {
                throw MissingNativeLibrary();
            }

            public int SyncStateDouble(string instanceId, string propertyName, double value)
            {
                throw MissingNativeLibrary();
            }

            public int SyncStateBool(string instanceId, string propertyName, bool value)
            {
                throw MissingNativeLibrary();
            }

            public int SyncStateJson(string instanceId, string propertyName, string jsonValue)
            {
                throw MissingNativeLibrary();
            }

            public int SyncStateBatch(string instanceId, string propertiesJson)
            {
                throw MissingNativeLibrary();
            }

            public int DispatchEffect(string instanceId, string effectName, string payloadJson)
            {
                throw MissingNativeLibrary();
            }

            public int BroadcastEffect(string className, string effectName, string payloadJson)
            {
                throw MissingNativeLibrary();
            }

            public string? CaptureSnapshot(IntPtr engineHandle)
            {
                throw MissingNativeLibrary();
            }

            public int ReloadQml(IntPtr engineHandle, string qmlSourcePath)
            {
                throw MissingNativeLibrary();
            }

            public void RestoreSnapshot(IntPtr engineHandle, string snapshotJson)
            {
                throw MissingNativeLibrary();
            }

            public void ShowError(
                IntPtr engineHandle,
                string title,
                string message,
                string? filePath,
                int line,
                int column)
            {
                throw MissingNativeLibrary();
            }

            public void HideError(IntPtr engineHandle)
            {
                throw MissingNativeLibrary();
            }

            public string? GetNativeInstanceInfo(string instanceId)
            {
                throw MissingNativeLibrary();
            }

            public string? GetNativeAllInstances()
            {
                throw MissingNativeLibrary();
            }

            public string? GetNativeMetrics()
            {
                throw MissingNativeLibrary();
            }

            private static InvalidOperationException MissingNativeLibrary()
            {
                return new InvalidOperationException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "A caller-provided native library path is required to initialize {0}.",
                        nameof(QmlSharpEngine)));
            }
        }
    }
}

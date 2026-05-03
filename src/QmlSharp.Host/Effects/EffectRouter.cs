using System.Globalization;
using System.Text.Json;
using QmlSharp.Host.Diagnostics;
using QmlSharp.Host.Exceptions;
using QmlSharp.Host.InstanceRegistry;
using QmlSharp.Host.Instances;
using QmlSharp.Host.Interop;
using ManagedInstanceRegistry = QmlSharp.Host.Instances.InstanceRegistry;

namespace QmlSharp.Host.Effects
{
    /// <summary>Routes managed effects to native QObject signal emission.</summary>
    public sealed class EffectRouter : IDisposable
    {
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General);

        private readonly ManagedInstanceRegistry registry;
        private readonly INativeHostInterop interop;
        private readonly Action<RuntimeDiagnostic>? diagnostics;
        private readonly Lock syncRoot = new();
        private readonly Dictionary<EffectKey, EffectRegistration> registrationsById = [];
        private readonly Dictionary<EffectNameKey, EffectRegistration> registrationsByName = [];
        private bool disposed;

        /// <summary>Initializes a new effect router.</summary>
        internal EffectRouter(
            ManagedInstanceRegistry registry,
            INativeHostInterop interop,
            Action<RuntimeDiagnostic>? diagnostics = null)
        {
            ArgumentNullException.ThrowIfNull(registry);
            ArgumentNullException.ThrowIfNull(interop);
            this.registry = registry;
            this.interop = interop;
            this.diagnostics = diagnostics;
        }

        /// <summary>Registers an effect exposed by a specific instance and schema effect ID.</summary>
        public void RegisterEffect(string instanceId, int effectId, string effectName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
            ArgumentOutOfRangeException.ThrowIfNegative(effectId);
            ArgumentException.ThrowIfNullOrWhiteSpace(effectName);

            if (registry.FindById(instanceId) is null)
            {
                throw new InstanceNotFoundException(instanceId);
            }

            EffectRegistration registration = new(instanceId, effectId, effectName);
            EffectKey idKey = new(instanceId, effectId);
            EffectNameKey nameKey = new(instanceId, effectName);

            lock (syncRoot)
            {
                ThrowIfDisposed();
                if (registrationsById.ContainsKey(idKey) || registrationsByName.ContainsKey(nameKey))
                {
                    throw new EffectRoutingException(
                        instanceId,
                        effectId,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Effect '{0}' ({1}) is already registered for instance '{2}'.",
                            effectName,
                            effectId,
                            instanceId));
                }

                registrationsById.Add(idKey, registration);
                registrationsByName.Add(nameKey, registration);
            }
        }

        /// <summary>Dispatches an effect to a specific instance by effect name.</summary>
        public EffectDispatchResult Dispatch(string instanceId, string effectName, object? payload = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
            ArgumentException.ThrowIfNullOrWhiteSpace(effectName);

            if (!TrySerializePayload(payload, out string payloadJson, out RuntimeDiagnostic? payloadDiagnostic, instanceId))
            {
                Report(payloadDiagnostic);
                return new EffectDispatchResult(EffectDispatchStatus.InvalidPayload, payloadDiagnostic?.Message, payloadDiagnostic);
            }

            ManagedViewModelInstance? instance = registry.FindById(instanceId);
            if (instance is null)
            {
                RuntimeDiagnostic diagnostic = CreateDiagnostic(
                    RuntimeDiagnosticSeverity.Warning,
                    instanceId,
                    string.Format(CultureInfo.InvariantCulture, "Effect '{0}' targeted an unknown instance.", effectName));
                Report(diagnostic);
                return new EffectDispatchResult(EffectDispatchStatus.UnknownInstance, diagnostic.Message, diagnostic);
            }

            EffectRegistration registration;
            lock (syncRoot)
            {
                if (disposed)
                {
                    return new EffectDispatchResult(EffectDispatchStatus.Disposed, "Effect router is disposed.");
                }

                if (!registrationsByName.TryGetValue(new EffectNameKey(instanceId, effectName), out EffectRegistration? foundRegistration))
                {
                    RuntimeDiagnostic diagnostic = CreateDiagnostic(
                        RuntimeDiagnosticSeverity.Warning,
                        instanceId,
                        string.Format(CultureInfo.InvariantCulture, "Effect '{0}' is not registered for instance '{1}'.", effectName, instanceId));
                    Report(diagnostic);
                    return new EffectDispatchResult(EffectDispatchStatus.UnknownEffect, diagnostic.Message, diagnostic);
                }

                registration = foundRegistration;
            }

            return DispatchRegistered(instance, registration, payloadJson);
        }

        /// <summary>Dispatches an effect to a specific instance by schema effect ID.</summary>
        public EffectDispatchResult Dispatch(string instanceId, int effectId, object? payload = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
            ArgumentOutOfRangeException.ThrowIfNegative(effectId);

            if (!TrySerializePayload(payload, out string payloadJson, out RuntimeDiagnostic? payloadDiagnostic, instanceId))
            {
                Report(payloadDiagnostic);
                return new EffectDispatchResult(EffectDispatchStatus.InvalidPayload, payloadDiagnostic?.Message, payloadDiagnostic);
            }

            ManagedViewModelInstance? instance = registry.FindById(instanceId);
            if (instance is null)
            {
                RuntimeDiagnostic diagnostic = CreateDiagnostic(
                    RuntimeDiagnosticSeverity.Warning,
                    instanceId,
                    string.Format(CultureInfo.InvariantCulture, "Effect ID '{0}' targeted an unknown instance.", effectId));
                Report(diagnostic);
                return new EffectDispatchResult(EffectDispatchStatus.UnknownInstance, diagnostic.Message, diagnostic);
            }

            EffectRegistration registration;
            lock (syncRoot)
            {
                if (disposed)
                {
                    return new EffectDispatchResult(EffectDispatchStatus.Disposed, "Effect router is disposed.");
                }

                if (!registrationsById.TryGetValue(new EffectKey(instanceId, effectId), out EffectRegistration? foundRegistration))
                {
                    RuntimeDiagnostic diagnostic = CreateDiagnostic(
                        RuntimeDiagnosticSeverity.Warning,
                        instanceId,
                        string.Format(CultureInfo.InvariantCulture, "Effect ID '{0}' is not registered for instance '{1}'.", effectId, instanceId));
                    Report(diagnostic);
                    return new EffectDispatchResult(EffectDispatchStatus.UnknownEffect, diagnostic.Message, diagnostic);
                }

                registration = foundRegistration;
            }

            return DispatchRegistered(instance, registration, payloadJson);
        }

        /// <summary>Broadcasts an effect to all active instances of the given ViewModel class.</summary>
        public EffectDispatchResult Broadcast(string className, string effectName, object? payload = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(className);
            ArgumentException.ThrowIfNullOrWhiteSpace(effectName);

            if (!TrySerializePayload(payload, out string payloadJson, out RuntimeDiagnostic? payloadDiagnostic, instanceId: null))
            {
                Report(payloadDiagnostic);
                return new EffectDispatchResult(EffectDispatchStatus.InvalidPayload, payloadDiagnostic?.Message, payloadDiagnostic);
            }

            IReadOnlyList<ManagedViewModelInstance> instances = registry.FindByClassName(className);
            if (instances.Count == 0)
            {
                RuntimeDiagnostic diagnostic = CreateDiagnostic(
                    RuntimeDiagnosticSeverity.Warning,
                    instanceId: null,
                    string.Format(CultureInfo.InvariantCulture, "Effect broadcast targeted class '{0}' with no active instances.", className));
                Report(diagnostic);
                return new EffectDispatchResult(EffectDispatchStatus.UnknownInstance, diagnostic.Message, diagnostic);
            }

            IReadOnlyList<ManagedViewModelInstance> targets;
            lock (syncRoot)
            {
                if (disposed)
                {
                    return new EffectDispatchResult(EffectDispatchStatus.Disposed, "Effect router is disposed.");
                }

                targets = instances
                    .Where(instance => registrationsByName.ContainsKey(new EffectNameKey(instance.InstanceId, effectName)))
                    .ToArray();
            }

            if (targets.Count == 0)
            {
                RuntimeDiagnostic diagnostic = CreateDiagnostic(
                    RuntimeDiagnosticSeverity.Warning,
                    instanceId: null,
                    string.Format(CultureInfo.InvariantCulture, "Effect '{0}' is not registered for class '{1}'.", effectName, className));
                Report(diagnostic);
                return new EffectDispatchResult(EffectDispatchStatus.UnknownEffect, diagnostic.Message, diagnostic);
            }

            int resultCode = ExecuteNative(() => interop.BroadcastEffect(className, effectName, payloadJson));
            if (resultCode != 0)
            {
                return NativeFailure(resultCode, instanceId: null);
            }

            foreach (ManagedViewModelInstance instance in targets)
            {
                _ = registry.RecordEffectEmitted(instance.InstanceId);
            }

            return EffectDispatchResult.Broadcast();
        }

        /// <summary>Clears effect registrations for one instance.</summary>
        public void ClearInstance(string instanceId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

            lock (syncRoot)
            {
                if (disposed)
                {
                    return;
                }

                foreach (EffectKey key in registrationsById.Keys
                             .Where(key => string.Equals(key.InstanceId, instanceId, StringComparison.Ordinal))
                             .ToArray())
                {
                    _ = registrationsById.Remove(key);
                }

                foreach (EffectNameKey key in registrationsByName.Keys
                             .Where(key => string.Equals(key.InstanceId, instanceId, StringComparison.Ordinal))
                             .ToArray())
                {
                    _ = registrationsByName.Remove(key);
                }
            }
        }

        internal EffectRouterSnapshot CaptureForHotReload(string instanceId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

            lock (syncRoot)
            {
                ThrowIfDisposed();
                EffectRouterSnapshot.Registration[] registrations = registrationsByName
                    .Where(registration => string.Equals(registration.Key.InstanceId, instanceId, StringComparison.Ordinal))
                    .Select(static registration => new EffectRouterSnapshot.Registration(
                        registration.Value.EffectId,
                        registration.Value.EffectName))
                    .ToArray();

                return new EffectRouterSnapshot(registrations);
            }
        }

        internal void RestoreForHotReload(string newInstanceId, EffectRouterSnapshot snapshot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(newInstanceId);
            ArgumentNullException.ThrowIfNull(snapshot);

            lock (syncRoot)
            {
                ThrowIfDisposed();
                foreach (EffectKey key in registrationsById.Keys
                             .Where(key => string.Equals(key.InstanceId, newInstanceId, StringComparison.Ordinal))
                             .ToArray())
                {
                    _ = registrationsById.Remove(key);
                }

                foreach (EffectNameKey key in registrationsByName.Keys
                             .Where(key => string.Equals(key.InstanceId, newInstanceId, StringComparison.Ordinal))
                             .ToArray())
                {
                    _ = registrationsByName.Remove(key);
                }

                foreach (EffectRouterSnapshot.Registration registrationSnapshot in snapshot.Registrations)
                {
                    EffectRegistration registration = new(newInstanceId, registrationSnapshot.EffectId, registrationSnapshot.EffectName);
                    registrationsById[new EffectKey(newInstanceId, registration.EffectId)] = registration;
                    registrationsByName[new EffectNameKey(newInstanceId, registration.EffectName)] = registration;
                }
            }
        }

        /// <summary>Clears all effect registrations.</summary>
        public void Dispose()
        {
            lock (syncRoot)
            {
                if (disposed)
                {
                    return;
                }

                registrationsById.Clear();
                registrationsByName.Clear();
                disposed = true;
            }
        }

        private EffectDispatchResult DispatchRegistered(
            ManagedViewModelInstance instance,
            EffectRegistration registration,
            string payloadJson)
        {
            if (instance.State == InstanceState.Destroyed)
            {
                return new EffectDispatchResult(EffectDispatchStatus.UnknownInstance, "Effect targeted a destroyed instance.");
            }

            int resultCode = ExecuteNative(() => interop.DispatchEffect(instance.InstanceId, registration.EffectName, payloadJson));
            if (resultCode != 0)
            {
                return NativeFailure(resultCode, instance.InstanceId);
            }

            _ = registry.RecordEffectEmitted(instance.InstanceId);
            return EffectDispatchResult.Dispatched();
        }

        private int ExecuteNative(Func<int> operation)
        {
            if (interop.IsOnMainThread)
            {
                return operation();
            }

            using ManualResetEventSlim completed = new();
            int resultCode = 0;
            Exception? exception = null;
            interop.PostToMainThread(() =>
            {
                try
                {
                    resultCode = operation();
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

            return resultCode;
        }

        private EffectDispatchResult NativeFailure(int resultCode, string? instanceId)
        {
            string message = interop.GetLastError()
                ?? string.Format(CultureInfo.InvariantCulture, "Native effect dispatch failed with error code {0}.", resultCode);
            RuntimeDiagnostic diagnostic = CreateDiagnostic(RuntimeDiagnosticSeverity.Error, instanceId, message);
            Report(diagnostic);
            return new EffectDispatchResult(EffectDispatchStatus.NativeFailure, message, diagnostic);
        }

        private bool TrySerializePayload(
            object? payload,
            out string payloadJson,
            out RuntimeDiagnostic? diagnostic,
            string? instanceId)
        {
            try
            {
                payloadJson = SerializePayload(payload);
                diagnostic = null;
                return true;
            }
            catch (JsonException exception)
            {
                payloadJson = "{}";
                diagnostic = CreateDiagnostic(
                    RuntimeDiagnosticSeverity.Error,
                    instanceId,
                    "Effect payload is not valid JSON: " + exception.Message);
                return false;
            }
            catch (NotSupportedException exception)
            {
                payloadJson = "{}";
                diagnostic = CreateDiagnostic(
                    RuntimeDiagnosticSeverity.Error,
                    instanceId,
                    "Effect payload is not serializable as JSON: " + exception.Message);
                return false;
            }
        }

        private static string SerializePayload(object? payload)
        {
            if (payload is null)
            {
                return "{}";
            }

            if (payload is JsonElement jsonElement)
            {
                return jsonElement.GetRawText();
            }

            if (payload is JsonDocument jsonDocument)
            {
                return jsonDocument.RootElement.GetRawText();
            }

            if (payload is string payloadJson)
            {
                if (string.IsNullOrWhiteSpace(payloadJson))
                {
                    return "{}";
                }

                using JsonDocument document = JsonDocument.Parse(payloadJson);
                return document.RootElement.GetRawText();
            }

            return JsonSerializer.Serialize(payload, SerializerOptions);
        }

        private RuntimeDiagnostic CreateDiagnostic(RuntimeDiagnosticSeverity severity, string? instanceId, string message)
        {
            return new RuntimeDiagnostic(
                severity,
                message,
                EnginePhase: "effect-routing",
                InstanceId: instanceId);
        }

        private void Report(RuntimeDiagnostic? diagnostic)
        {
            if (diagnostic is not null)
            {
                diagnostics?.Invoke(diagnostic);
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

        private readonly record struct EffectKey(string InstanceId, int EffectId);

        private readonly record struct EffectNameKey(string InstanceId, string EffectName);
    }
}

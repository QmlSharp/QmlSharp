using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using QmlSharp.Host.Exceptions;
using QmlSharp.Host.Instances;
using QmlSharp.Host.Interop;
using ManagedInstanceRegistry = QmlSharp.Host.Instances.InstanceRegistry;

namespace QmlSharp.Host.StateSynchronization
{
    /// <summary>Coordinates managed state updates with the native QObject state-sync ABI.</summary>
    public sealed class StateSync
    {
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General);

        private readonly ManagedInstanceRegistry registry;
        private readonly INativeHostInterop interop;
        private readonly IReadOnlyDictionary<string, StateSyncSchemaMetadata> schemasById;

        internal StateSync(
            ManagedInstanceRegistry registry,
            INativeHostInterop interop,
            IEnumerable<StateSyncSchemaMetadata>? schemas = null)
        {
            ArgumentNullException.ThrowIfNull(registry);
            ArgumentNullException.ThrowIfNull(interop);

            this.registry = registry;
            this.interop = interop;
            schemasById = CreateSchemaMap(schemas);
        }

        /// <summary>Pushes a value and selects a typed fast path when the runtime value supports one.</summary>
        public void Push(string instanceId, string propertyName, object? value)
        {
            ManagedViewModelInstance instance = ValidateInstanceAndProperty(instanceId, propertyName, out StateSyncPropertyMetadata? property);
            if (value is string stringValue)
            {
                PushString(instance, propertyName, stringValue);
                return;
            }

            if (value is int intValue)
            {
                PushInt(instance, propertyName, intValue);
                return;
            }

            if (value is double doubleValue)
            {
                PushDouble(instance, propertyName, doubleValue);
                return;
            }

            if (value is bool boolValue)
            {
                PushBool(instance, propertyName, boolValue);
                return;
            }

            if (value is null && property?.ValueKind == StateSyncValueKind.String)
            {
                PushString(instance, propertyName, null);
                return;
            }

            PushJson(instance, propertyName, SerializeJsonValue(value));
        }

        /// <summary>Pushes a string property through the UTF-8 string fast path.</summary>
        public void PushString(string instanceId, string propertyName, string? value)
        {
            ManagedViewModelInstance instance = ValidateInstanceAndProperty(instanceId, propertyName, out _);
            PushString(instance, propertyName, value);
        }

        /// <summary>Pushes a 32-bit integer property through the integer fast path.</summary>
        public void PushInt(string instanceId, string propertyName, int value)
        {
            ManagedViewModelInstance instance = ValidateInstanceAndProperty(instanceId, propertyName, out _);
            PushInt(instance, propertyName, value);
        }

        /// <summary>Pushes a double property through the double fast path.</summary>
        public void PushDouble(string instanceId, string propertyName, double value)
        {
            ManagedViewModelInstance instance = ValidateInstanceAndProperty(instanceId, propertyName, out _);
            PushDouble(instance, propertyName, value);
        }

        /// <summary>Pushes a boolean property through the boolean fast path.</summary>
        public void PushBool(string instanceId, string propertyName, bool value)
        {
            ManagedViewModelInstance instance = ValidateInstanceAndProperty(instanceId, propertyName, out _);
            PushBool(instance, propertyName, value);
        }

        /// <summary>Pushes a JSON-encoded property value through the fallback path.</summary>
        public void PushJson(string instanceId, string propertyName, string jsonValue)
        {
            ManagedViewModelInstance instance = ValidateInstanceAndProperty(instanceId, propertyName, out _);
            PushJson(instance, propertyName, jsonValue);
        }

        /// <summary>Pushes several property values as one JSON object and applies the snapshot all-or-nothing.</summary>
        public void PushBatch(string instanceId, IReadOnlyDictionary<string, object?> properties)
        {
            ArgumentNullException.ThrowIfNull(properties);
            ManagedViewModelInstance instance = ValidateInstance(instanceId);
            if (properties.Count == 0)
            {
                return;
            }

            foreach (string propertyName in properties.Keys)
            {
                _ = ValidateProperty(instance, propertyName);
            }

            SortedDictionary<string, object?> sortedProperties = new(StringComparer.Ordinal);
            Dictionary<string, object?> snapshotValues = new(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object?> property in properties)
            {
                sortedProperties.Add(property.Key, property.Value);
                snapshotValues.Add(property.Key, CloneSnapshotValue(property.Value));
            }

            string propertiesJson = JsonSerializer.Serialize(sortedProperties, SerializerOptions);
            int resultCode = ExecuteNative(() => interop.SyncStateBatch(instance.InstanceId, propertiesJson));
            ThrowIfNativeFailed(resultCode, instance.InstanceId, propertyName: null);

            if (!registry.UpdatePropertyStates(instance.InstanceId, new ReadOnlyDictionary<string, object?>(snapshotValues)))
            {
                throw new InstanceNotFoundException(instance.InstanceId);
            }
        }

        private static IReadOnlyDictionary<string, StateSyncSchemaMetadata> CreateSchemaMap(IEnumerable<StateSyncSchemaMetadata>? schemas)
        {
            if (schemas is null)
            {
                return new ReadOnlyDictionary<string, StateSyncSchemaMetadata>(
                    new Dictionary<string, StateSyncSchemaMetadata>(StringComparer.Ordinal));
            }

            Dictionary<string, StateSyncSchemaMetadata> map = new(StringComparer.Ordinal);
            foreach (StateSyncSchemaMetadata schema in schemas)
            {
                map.Add(schema.SchemaId, schema);
            }

            return new ReadOnlyDictionary<string, StateSyncSchemaMetadata>(map);
        }

        private ManagedViewModelInstance ValidateInstanceAndProperty(
            string instanceId,
            string propertyName,
            out StateSyncPropertyMetadata? property)
        {
            ManagedViewModelInstance instance = ValidateInstance(instanceId);
            property = ValidateProperty(instance, propertyName);
            return instance;
        }

        private ManagedViewModelInstance ValidateInstance(string instanceId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
            ManagedViewModelInstance? instance = registry.FindById(instanceId);
            if (instance is null)
            {
                throw new InstanceNotFoundException(instanceId);
            }

            return instance;
        }

        private StateSyncPropertyMetadata? ValidateProperty(ManagedViewModelInstance instance, string propertyName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
            if (!schemasById.TryGetValue(instance.SchemaId, out StateSyncSchemaMetadata? schema))
            {
                return null;
            }

            if (schema.TryFindProperty(propertyName, out StateSyncPropertyMetadata property))
            {
                return property;
            }

            throw new PropertyNotFoundException(
                instance.InstanceId,
                propertyName,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Property '{0}' is not declared by schema '{1}' for instance '{2}'.",
                    propertyName,
                    instance.SchemaId,
                    instance.InstanceId));
        }

        private void PushString(ManagedViewModelInstance instance, string propertyName, string? value)
        {
            int resultCode = ExecuteNative(() => interop.SyncStateString(instance.InstanceId, propertyName, value));
            ThrowIfNativeFailed(resultCode, instance.InstanceId, propertyName);
            UpdateSnapshot(instance.InstanceId, propertyName, value);
        }

        private void PushInt(ManagedViewModelInstance instance, string propertyName, int value)
        {
            int resultCode = ExecuteNative(() => interop.SyncStateInt(instance.InstanceId, propertyName, value));
            ThrowIfNativeFailed(resultCode, instance.InstanceId, propertyName);
            UpdateSnapshot(instance.InstanceId, propertyName, value);
        }

        private void PushDouble(ManagedViewModelInstance instance, string propertyName, double value)
        {
            int resultCode = ExecuteNative(() => interop.SyncStateDouble(instance.InstanceId, propertyName, value));
            ThrowIfNativeFailed(resultCode, instance.InstanceId, propertyName);
            UpdateSnapshot(instance.InstanceId, propertyName, value);
        }

        private void PushBool(ManagedViewModelInstance instance, string propertyName, bool value)
        {
            int resultCode = ExecuteNative(() => interop.SyncStateBool(instance.InstanceId, propertyName, value));
            ThrowIfNativeFailed(resultCode, instance.InstanceId, propertyName);
            UpdateSnapshot(instance.InstanceId, propertyName, value);
        }

        private void PushJson(ManagedViewModelInstance instance, string propertyName, string jsonValue)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(jsonValue);
            JsonElement snapshotValue = ParseJsonValue(jsonValue);
            int resultCode = ExecuteNative(() => interop.SyncStateJson(instance.InstanceId, propertyName, jsonValue));
            ThrowIfNativeFailed(resultCode, instance.InstanceId, propertyName);
            UpdateSnapshot(instance.InstanceId, propertyName, snapshotValue);
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
                catch (Exception capturedException)
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

        private void UpdateSnapshot(string instanceId, string propertyName, object? value)
        {
            if (!registry.UpdatePropertyState(instanceId, propertyName, value))
            {
                throw new InstanceNotFoundException(instanceId);
            }
        }

        private void ThrowIfNativeFailed(int resultCode, string instanceId, string? propertyName)
        {
            if (resultCode == 0)
            {
                return;
            }

            string message = interop.GetLastError()
                ?? string.Format(CultureInfo.InvariantCulture, "Native state synchronization failed with error code {0}.", resultCode);
            if (resultCode == -3)
            {
                throw new InstanceNotFoundException(instanceId);
            }

            if (resultCode == -7 && propertyName is not null)
            {
                throw new PropertyNotFoundException(instanceId, propertyName, message);
            }

            if (resultCode == -8)
            {
                throw new NativeJsonException(message);
            }

            throw new NativeHostException(resultCode, message);
        }

        private static JsonElement ParseJsonValue(string jsonValue)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(jsonValue);
                return document.RootElement.Clone();
            }
            catch (JsonException exception)
            {
                throw new NativeJsonException("State JSON payload is not valid JSON: " + exception.Message, exception);
            }
        }

        private static string SerializeJsonValue(object? value)
        {
            if (value is JsonElement jsonElement)
            {
                return jsonElement.GetRawText();
            }

            if (value is JsonDocument jsonDocument)
            {
                return jsonDocument.RootElement.GetRawText();
            }

            return JsonSerializer.Serialize(value, SerializerOptions);
        }

        private static object? CloneSnapshotValue(object? value)
        {
            if (value is JsonElement jsonElement)
            {
                return jsonElement.Clone();
            }

            if (value is JsonDocument jsonDocument)
            {
                return jsonDocument.RootElement.Clone();
            }

            return value;
        }
    }
}

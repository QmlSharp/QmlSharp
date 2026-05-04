#pragma warning disable MA0048

namespace QmlSharp.Host.Exceptions
{
    /// <summary>Base exception for native-host failures.</summary>
    public class NativeHostException : Exception
    {
        public NativeHostException(int errorCode, string message)
            : base(message)
        {
            ErrorCode = errorCode;
        }

        public NativeHostException(int errorCode, string message, Exception innerException)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }

        /// <summary>The C ABI error code associated with the failure.</summary>
        public int ErrorCode { get; }
    }

    /// <summary>Thrown when the loaded native library does not match the managed ABI contract.</summary>
    public sealed class AbiVersionMismatchException : NativeHostException
    {
        public AbiVersionMismatchException(int expectedVersion, int actualVersion)
            : base(-1, $"QmlSharp native ABI mismatch. Expected {expectedVersion}, got {actualVersion}.")
        {
            ExpectedVersion = expectedVersion;
            ActualVersion = actualVersion;
        }

        /// <summary>The ABI version required by the managed host.</summary>
        public int ExpectedVersion { get; }

        /// <summary>The ABI version reported by the native library.</summary>
        public int ActualVersion { get; }
    }

    /// <summary>Thrown when a native-host API is used before the Qt engine has initialized.</summary>
    public sealed class EngineNotInitializedException : NativeHostException
    {
        public EngineNotInitializedException()
            : base(-4, "QmlSharp engine is not initialized. Call Initialize() first.")
        {
        }

        public EngineNotInitializedException(string message)
            : base(-4, message)
        {
        }
    }

    /// <summary>Thrown when a native-host API rejects its arguments before doing work.</summary>
    public class NativeInvalidArgumentException : NativeHostException
    {
        public NativeInvalidArgumentException(string message)
            : base(-2, message)
        {
        }
    }

    /// <summary>Thrown when a native-host API rejects its arguments before doing work.</summary>
    public sealed class InvalidNativeArgumentException : NativeInvalidArgumentException
    {
        public InvalidNativeArgumentException(string message)
            : base(message)
        {
        }
    }

    /// <summary>Thrown when a referenced runtime instance is not registered.</summary>
    public sealed class InstanceNotFoundException : NativeHostException
    {
        public InstanceNotFoundException(string instanceId)
            : base(-3, $"Instance '{instanceId}' was not found in the managed instance registry.")
        {
            InstanceId = instanceId;
        }

        /// <summary>The missing instance identifier.</summary>
        public string InstanceId { get; }
    }

    /// <summary>Thrown when the native host cannot load or reload a root QML file.</summary>
    public sealed class QmlLoadException : NativeHostException
    {
        public QmlLoadException(string qmlPath, string detail)
            : base(-5, $"Failed to load QML from '{qmlPath}': {detail}")
        {
            QmlPath = qmlPath;
        }

        /// <summary>The root QML path that failed to load.</summary>
        public string QmlPath { get; }
    }

    /// <summary>Thrown when the native host rejects a generated QML type registration.</summary>
    public sealed class TypeRegistrationException : NativeHostException
    {
        public TypeRegistrationException(string message)
            : base(-6, message)
        {
        }
    }

    /// <summary>Thrown when a state synchronization property name is not part of the known schema.</summary>
    public sealed class PropertyNotFoundException : NativeHostException
    {
        public PropertyNotFoundException(string instanceId, string propertyName)
            : base(-7, $"Property '{propertyName}' was not found for instance '{instanceId}'.")
        {
            InstanceId = instanceId;
            PropertyName = propertyName;
        }

        public PropertyNotFoundException(string instanceId, string propertyName, string message)
            : base(-7, message)
        {
            InstanceId = instanceId;
            PropertyName = propertyName;
        }

        /// <summary>The instance identifier whose schema rejected the property.</summary>
        public string InstanceId { get; }

        /// <summary>The rejected property name.</summary>
        public string PropertyName { get; }
    }

    /// <summary>Thrown when JSON state payloads are invalid or rejected by the native host.</summary>
    public sealed class NativeJsonException : NativeHostException
    {
        public NativeJsonException(string message)
            : base(-8, message)
        {
        }

        public NativeJsonException(string message, Exception innerException)
            : base(-8, message, innerException)
        {
        }
    }

    /// <summary>Thrown when command routing registration fails before native callback dispatch.</summary>
    public sealed class CommandRoutingException : NativeHostException
    {
        public CommandRoutingException(string instanceId, int commandId, string message)
            : base(-9, message)
        {
            InstanceId = instanceId;
            CommandId = commandId;
        }

        /// <summary>The instance identifier associated with the command registration.</summary>
        public string InstanceId { get; }

        /// <summary>The command identifier associated with the registration failure.</summary>
        public int CommandId { get; }
    }

    /// <summary>Thrown when effect routing registration fails before native dispatch.</summary>
    public sealed class EffectRoutingException : NativeHostException
    {
        public EffectRoutingException(string instanceId, int effectId, string message)
            : base(-10, message)
        {
            InstanceId = instanceId;
            EffectId = effectId;
        }

        /// <summary>The instance identifier associated with the effect registration.</summary>
        public string InstanceId { get; }

        /// <summary>The effect identifier associated with the registration failure.</summary>
        public int EffectId { get; }
    }
}

#pragma warning restore MA0048

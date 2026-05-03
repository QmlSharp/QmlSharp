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
}

#pragma warning restore MA0048

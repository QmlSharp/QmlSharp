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
}

#pragma warning restore MA0048

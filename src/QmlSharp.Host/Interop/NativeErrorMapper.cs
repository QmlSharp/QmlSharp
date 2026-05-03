using System.Globalization;
using QmlSharp.Host.Exceptions;

namespace QmlSharp.Host.Interop
{
    internal static class NativeErrorMapper
    {
        internal static void ThrowIfFailed(
            int resultCode,
            INativeHostInterop interop,
            string operation,
            string? instanceId = null,
            string? qmlPath = null)
        {
            ArgumentNullException.ThrowIfNull(interop);
            ArgumentException.ThrowIfNullOrWhiteSpace(operation);
            if (resultCode == 0)
            {
                return;
            }

            string detail = interop.GetLastError()
                ?? string.Format(CultureInfo.InvariantCulture, "{0} failed with native error code {1}.", operation, resultCode);
            throw Create(resultCode, detail, instanceId, qmlPath);
        }

        internal static NativeHostException Create(
            int resultCode,
            string detail,
            string? instanceId = null,
            string? qmlPath = null)
        {
            return resultCode switch
            {
                -2 => new NativeInvalidArgumentException(detail),
                -3 => new InstanceNotFoundException(string.IsNullOrWhiteSpace(instanceId) ? "unknown" : instanceId),
                -4 => new EngineNotInitializedException(detail),
                -5 => new QmlLoadException(string.IsNullOrWhiteSpace(qmlPath) ? "unknown" : qmlPath, detail),
                -8 => new NativeJsonException(detail),
                _ => new NativeHostException(resultCode, detail)
            };
        }
    }
}

using QmlSharp.Host.Interop;

namespace QmlSharp.Host.ErrorOverlay
{
    /// <summary>Managed facade for the native host error-overlay hooks.</summary>
    public sealed class ErrorOverlayController
    {
        private readonly INativeHostInterop interop;
        private readonly IntPtr engineHandle;
        private readonly Lock syncRoot = new();
        private bool visible;

        internal ErrorOverlayController(INativeHostInterop interop, IntPtr engineHandle)
        {
            ArgumentNullException.ThrowIfNull(interop);
            this.interop = interop;
            this.engineHandle = engineHandle;
        }

        /// <summary>Whether the managed facade believes the overlay is visible.</summary>
        public bool IsVisible
        {
            get
            {
                lock (syncRoot)
                {
                    return visible;
                }
            }
        }

        /// <summary>Shows or replaces the native error overlay.</summary>
        public void Show(ErrorOverlayPayload payload)
        {
            ArgumentNullException.ThrowIfNull(payload);
            if (string.IsNullOrWhiteSpace(payload.Message))
            {
                throw new ArgumentException("Overlay message is required.", nameof(payload));
            }

            ErrorOverlaySourceLocation? sourceLocation = payload.SourceLocation;
            int line = NormalizeLocationValue(sourceLocation?.Line);
            int column = NormalizeLocationValue(sourceLocation?.Column);
            string title = CreateTitle(payload);

            _ = ExecuteNative(() =>
            {
                interop.ShowError(engineHandle, title, payload.Message, sourceLocation?.FilePath, line, column);
                return 0;
            });

            lock (syncRoot)
            {
                visible = true;
            }
        }

        /// <summary>Hides the native error overlay. Repeated calls are harmless.</summary>
        public void Hide()
        {
            bool shouldHide;
            lock (syncRoot)
            {
                shouldHide = visible;
                visible = false;
            }

            if (!shouldHide)
            {
                return;
            }

            _ = ExecuteNative(() =>
            {
                interop.HideError(engineHandle);
                return 0;
            });
        }

        private int ExecuteNative(Func<int> operation)
        {
            if (interop.IsOnMainThread)
            {
                return operation();
            }

            using ManualResetEventSlim completed = new();
            int result = 0;
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

            return result;
        }

        private static string CreateTitle(ErrorOverlayPayload payload)
        {
            string severity = payload.Severity.ToString();
            return string.IsNullOrWhiteSpace(payload.DiagnosticCode)
                ? severity
                : severity + " " + payload.DiagnosticCode;
        }

        private static int NormalizeLocationValue(int? value)
        {
            return value.GetValueOrDefault() > 0 ? value.GetValueOrDefault() : 0;
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
    }
}

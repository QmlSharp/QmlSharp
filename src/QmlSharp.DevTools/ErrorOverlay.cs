#pragma warning disable MA0048

using QmlSharp.Compiler;

namespace QmlSharp.DevTools
{
    /// <summary>
    /// Controls the native error overlay lifecycle for compilation and hot reload failures.
    /// </summary>
    public sealed class ErrorOverlay : IErrorOverlay
    {
        private readonly IErrorOverlayNativeHost nativeHost;
        private readonly Lock syncRoot = new();
        private bool isVisible;

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorOverlay"/> class.
        /// </summary>
        /// <param name="nativeHost">Native host overlay calls.</param>
        public ErrorOverlay(IErrorOverlayNativeHost nativeHost)
        {
            ArgumentNullException.ThrowIfNull(nativeHost);

            this.nativeHost = nativeHost;
        }

        /// <inheritdoc />
        public bool IsVisible
        {
            get
            {
                lock (syncRoot)
                {
                    return isVisible;
                }
            }
        }

        /// <inheritdoc />
        public void Show(OverlayError error)
        {
            OverlayError normalizedError = Validate(error);

            lock (syncRoot)
            {
                if (isVisible)
                {
                    nativeHost.HideError();
                    isVisible = false;
                }

                nativeHost.ShowError(
                    normalizedError.Title,
                    normalizedError.Message,
                    normalizedError.FilePath,
                    ToNativePosition(normalizedError.Line),
                    ToNativePosition(normalizedError.Column));
                isVisible = true;
            }
        }

        /// <inheritdoc />
        public void Show(IReadOnlyList<OverlayError> errors)
        {
            ArgumentNullException.ThrowIfNull(errors);

            if (errors.Count == 0)
            {
                throw new ArgumentException("At least one overlay error is required.", nameof(errors));
            }

            Show(errors[0]);
        }

        /// <inheritdoc />
        public void Hide()
        {
            lock (syncRoot)
            {
                if (!isVisible)
                {
                    return;
                }

                nativeHost.HideError();
                isVisible = false;
            }
        }

        /// <summary>
        /// Shows compiler diagnostics through the overlay after mapping them to overlay errors.
        /// </summary>
        /// <param name="diagnostics">Compiler diagnostics to display.</param>
        public void ShowDiagnostics(IReadOnlyList<CompilerDiagnostic> diagnostics)
        {
            ImmutableArray<OverlayError> errors = ErrorOverlayDiagnosticMapper.MapDiagnostics(diagnostics);
            if (errors.IsEmpty)
            {
                throw new ArgumentException(
                    "At least one warning, error, or fatal diagnostic is required.",
                    nameof(diagnostics));
            }

            Show(errors);
        }

        /// <summary>
        /// Applies compilation lifecycle behavior: hide on success, show diagnostics on failure.
        /// </summary>
        /// <param name="result">Compilation result.</param>
        public void ApplyCompilationResult(CompilationResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            if (result.Success)
            {
                Hide();
                return;
            }

            ImmutableArray<OverlayError> errors = ErrorOverlayDiagnosticMapper.MapDiagnostics(result.Diagnostics);
            if (errors.IsEmpty)
            {
                Show(new OverlayError("Compilation Error", "Compilation failed.", null, null, null));
                return;
            }

            Show(errors);
        }

        /// <summary>
        /// Applies hot reload lifecycle behavior: hide on success, show a reload failure on failure.
        /// </summary>
        /// <param name="result">Hot reload result.</param>
        public void ApplyHotReloadResult(HotReloadResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            if (result.Success)
            {
                Hide();
                return;
            }

            string message = string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? "Hot reload failed."
                : result.ErrorMessage;

            Show(new OverlayError("Hot Reload Error", message, null, null, null));
        }

        private static OverlayError Validate(OverlayError error)
        {
            ArgumentNullException.ThrowIfNull(error);

            if (string.IsNullOrWhiteSpace(error.Title))
            {
                throw new ArgumentException("Overlay error title is required.", nameof(error));
            }

            if (string.IsNullOrWhiteSpace(error.Message))
            {
                throw new ArgumentException("Overlay error message is required.", nameof(error));
            }

            return error;
        }

        private static int ToNativePosition(int? position)
        {
            return position.HasValue && position.Value > 0
                ? position.Value
                : 0;
        }
    }

    /// <summary>
    /// Maps compiler diagnostics to typed overlay errors.
    /// </summary>
    public static class ErrorOverlayDiagnosticMapper
    {
        /// <summary>
        /// Maps displayable diagnostics to overlay errors, preserving deterministic diagnostic order.
        /// </summary>
        /// <param name="diagnostics">Compiler diagnostics to map.</param>
        /// <returns>Overlay errors for warnings, errors, and fatal diagnostics.</returns>
        public static ImmutableArray<OverlayError> MapDiagnostics(IReadOnlyList<CompilerDiagnostic> diagnostics)
        {
            ArgumentNullException.ThrowIfNull(diagnostics);

            ImmutableArray<OverlayError>.Builder builder = ImmutableArray.CreateBuilder<OverlayError>(diagnostics.Count);
            for (int index = 0; index < diagnostics.Count; index++)
            {
                CompilerDiagnostic diagnostic = diagnostics[index];
                if (diagnostic.Severity == DiagnosticSeverity.Info)
                {
                    continue;
                }

                builder.Add(MapDisplayableDiagnostic(diagnostic));
            }

            return builder.ToImmutable();
        }

        private static OverlayError MapDisplayableDiagnostic(CompilerDiagnostic diagnostic)
        {
            OverlaySeverity severity = diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Fatal
                ? OverlaySeverity.Error
                : OverlaySeverity.Warning;
            string title = severity == OverlaySeverity.Error
                ? "Compilation Error"
                : "Compilation Warning";

            return new OverlayError(
                title,
                diagnostic.Code + ": " + diagnostic.Message,
                diagnostic.Location?.FilePath,
                diagnostic.Location?.Line,
                diagnostic.Location?.Column,
                severity);
        }
    }
}

#pragma warning restore MA0048

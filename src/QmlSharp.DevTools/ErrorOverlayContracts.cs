#pragma warning disable MA0048

namespace QmlSharp.DevTools
{
    /// <summary>
    /// Displays compilation and hot reload errors through the native error overlay.
    /// </summary>
    public interface IErrorOverlay
    {
        /// <summary>Shows the error overlay with the given error details.</summary>
        /// <param name="error">The error to display.</param>
        void Show(OverlayError error);

        /// <summary>Shows the error overlay with multiple errors.</summary>
        /// <param name="errors">Errors to display; the first error is primary.</param>
        void Show(IReadOnlyList<OverlayError> errors);

        /// <summary>Hides the error overlay if visible.</summary>
        void Hide();

        /// <summary>Gets a value indicating whether the overlay is visible.</summary>
        bool IsVisible { get; }
    }

    /// <summary>
    /// Mockable native-host overlay surface used by <see cref="IErrorOverlay"/>.
    /// </summary>
    public interface IErrorOverlayNativeHost
    {
        /// <summary>Shows a native overlay error.</summary>
        /// <param name="title">Overlay title.</param>
        /// <param name="message">Overlay message.</param>
        /// <param name="filePath">Optional source file path.</param>
        /// <param name="line">One-based source line, or 0 when unknown.</param>
        /// <param name="column">One-based source column, or 0 when unknown.</param>
        void ShowError(string title, string message, string? filePath, int line, int column);

        /// <summary>Hides the native overlay error.</summary>
        void HideError();
    }

    /// <summary>An error to display in the overlay.</summary>
    /// <param name="Title">Error title.</param>
    /// <param name="Message">Error message body.</param>
    /// <param name="FilePath">Source file path.</param>
    /// <param name="Line">One-based source line.</param>
    /// <param name="Column">One-based source column.</param>
    /// <param name="Severity">Error severity.</param>
    public sealed record OverlayError(
        string Title,
        string Message,
        string? FilePath,
        int? Line,
        int? Column,
        OverlaySeverity Severity = OverlaySeverity.Error);

    /// <summary>Severity levels for overlay errors.</summary>
    public enum OverlaySeverity
    {
        /// <summary>Warning overlay.</summary>
        Warning,

        /// <summary>Error overlay.</summary>
        Error,
    }

#pragma warning restore MA0048
}

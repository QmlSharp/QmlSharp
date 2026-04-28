using System.Globalization;
using QmlSharp.Qml.Ast;

namespace QmlSharp.Qml.Emitter
{
    internal static class QmlValueFormatter
    {
        internal static string FormatNumber(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "QML numeric literals must be finite.");
            }

            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        internal static string FormatString(StringLiteral value, ResolvedEmitOptions options)
        {
            ArgumentNullException.ThrowIfNull(value);
            ArgumentNullException.ThrowIfNull(options);

            return options.QuoteStringLiteral(value.Value);
        }
    }
}

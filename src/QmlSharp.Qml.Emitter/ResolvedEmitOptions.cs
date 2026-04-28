using System.Globalization;

namespace QmlSharp.Qml.Emitter
{
    /// <summary>
    /// Fully resolved emit options with defaults applied and validation complete.
    /// </summary>
    internal sealed record ResolvedEmitOptions
    {
        internal required string IndentString { get; init; }

        internal required string NewlineString { get; init; }

        internal required char QuoteChar { get; init; }

        internal required QuoteStyle QuoteStyle { get; init; }

        internal required bool EmitComments { get; init; }

        internal required bool Normalize { get; init; }

        internal required bool SortImports { get; init; }

        internal required bool TrailingNewline { get; init; }

        internal required bool InsertBlankLinesBetweenSections { get; init; }

        internal required bool InsertBlankLinesBetweenObjects { get; init; }

        internal required bool InsertBlankLinesBetweenFunctions { get; init; }

        internal required bool EmitGeneratedHeader { get; init; }

        internal required string GeneratedHeaderText { get; init; }

        internal required bool SingleLineEmptyObjects { get; init; }

        internal required SemicolonRule SemicolonRule { get; init; }

        internal required int MaxLineWidth { get; init; }

        internal string OptionalSemicolonSuffix => SemicolonRule == SemicolonRule.Always ? ";" : string.Empty;

        internal string GetSemicolonSuffix(bool syntacticallyRequired)
        {
            if (SemicolonRule == SemicolonRule.Always || (SemicolonRule == SemicolonRule.Essential && syntacticallyRequired))
            {
                return ";";
            }

            return string.Empty;
        }

        internal static ResolvedEmitOptions From(EmitOptions? options)
        {
            EmitOptions source = options ?? new EmitOptions();
            Validate(source);

            return new ResolvedEmitOptions
            {
                IndentString = source.IndentStyle == IndentStyle.Tabs
                    ? "\t"
                    : new string(' ', source.IndentSize),
                NewlineString = source.Newline == NewlineStyle.CrLf ? "\r\n" : "\n",
                QuoteChar = source.QuoteStyle == QuoteStyle.Single ? '\'' : '"',
                QuoteStyle = source.QuoteStyle,
                EmitComments = source.EmitComments,
                Normalize = source.Normalize,
                SortImports = source.SortImports,
                TrailingNewline = source.TrailingNewline,
                InsertBlankLinesBetweenSections = source.InsertBlankLinesBetweenSections,
                InsertBlankLinesBetweenObjects = source.InsertBlankLinesBetweenObjects,
                InsertBlankLinesBetweenFunctions = source.InsertBlankLinesBetweenFunctions,
                EmitGeneratedHeader = source.EmitGeneratedHeader,
                GeneratedHeaderText = source.GeneratedHeaderText,
                SingleLineEmptyObjects = source.SingleLineEmptyObjects,
                SemicolonRule = source.SemicolonRule,
                MaxLineWidth = source.MaxLineWidth,
            };
        }

        internal string EscapeStringLiteral(string value)
        {
            ArgumentNullException.ThrowIfNull(value);

            return StringLiteralEscaper.Escape(value, QuoteChar);
        }

        internal string QuoteStringLiteral(string value)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{QuoteChar}{EscapeStringLiteral(value)}{QuoteChar}");
        }

        private static void Validate(EmitOptions options)
        {
            if (!Enum.IsDefined(options.IndentStyle))
            {
                throw new ArgumentOutOfRangeException(nameof(options), options.IndentStyle, "IndentStyle must be a defined value.");
            }

            if (options.IndentSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), options.IndentSize, "IndentSize must be greater than zero.");
            }

            if (!Enum.IsDefined(options.Newline))
            {
                throw new ArgumentOutOfRangeException(nameof(options), options.Newline, "Newline must be a defined value.");
            }

            if (options.MaxLineWidth < -1 || options.MaxLineWidth == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), options.MaxLineWidth, "MaxLineWidth must be -1 or a positive value.");
            }

            if (!Enum.IsDefined(options.QuoteStyle))
            {
                throw new ArgumentOutOfRangeException(nameof(options), options.QuoteStyle, "QuoteStyle must be a defined value.");
            }

            if (options.GeneratedHeaderText is null)
            {
                throw new ArgumentException("GeneratedHeaderText cannot be null.", nameof(options));
            }

            if (!Enum.IsDefined(options.SemicolonRule))
            {
                throw new ArgumentOutOfRangeException(nameof(options), options.SemicolonRule, "SemicolonRule must be a defined value.");
            }
        }
    }
}

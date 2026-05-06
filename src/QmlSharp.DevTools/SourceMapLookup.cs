using QmlSharp.Compiler;

#pragma warning disable MA0048

namespace QmlSharp.DevTools
{
    /// <summary>
    /// Looks up original source locations for diagnostics reported against generated QML.
    /// </summary>
    public interface ISourceMapLookup
    {
        /// <summary>Finds the original source location for a generated QML location.</summary>
        /// <param name="generatedLocation">Generated QML source location.</param>
        /// <returns>The original source location when a matching source map exists; otherwise null.</returns>
        SourceLocation? FindSourceLocation(SourceLocation generatedLocation);
    }

    /// <summary>
    /// In-memory source-map lookup for dev-console diagnostic formatting.
    /// </summary>
    public sealed class SourceMapLookup : ISourceMapLookup
    {
        private readonly ImmutableArray<SourceMap> sourceMaps;

        /// <summary>Gets an empty lookup that never maps locations.</summary>
        public static SourceMapLookup Empty { get; } = new(ImmutableArray<SourceMap>.Empty);

        /// <summary>
        /// Initializes a source-map lookup from compiler source maps.
        /// </summary>
        /// <param name="sourceMaps">Source maps to query.</param>
        public SourceMapLookup(IEnumerable<SourceMap> sourceMaps)
        {
            ArgumentNullException.ThrowIfNull(sourceMaps);

            this.sourceMaps = sourceMaps
                .OrderBy(static sourceMap => sourceMap.OutputFilePath, StringComparer.Ordinal)
                .ThenBy(static sourceMap => sourceMap.SourceFilePath, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        /// <summary>
        /// Initializes a source-map lookup from compiler source maps.
        /// </summary>
        /// <param name="sourceMaps">Source maps to query.</param>
        public SourceMapLookup(ImmutableArray<SourceMap> sourceMaps)
            : this(sourceMaps.IsDefault ? ImmutableArray<SourceMap>.Empty : sourceMaps.AsEnumerable())
        {
        }

        /// <inheritdoc />
        public SourceLocation? FindSourceLocation(SourceLocation generatedLocation)
        {
            ArgumentNullException.ThrowIfNull(generatedLocation);

            if (generatedLocation.FilePath is null || !generatedLocation.Line.HasValue || !generatedLocation.Column.HasValue)
            {
                return null;
            }

            string generatedFilePath = generatedLocation.FilePath;
            int generatedLine = generatedLocation.Line.Value;
            int generatedColumn = generatedLocation.Column.Value;

            foreach (SourceMap sourceMap in sourceMaps)
            {
                if (!OutputPathMatches(sourceMap.OutputFilePath, generatedFilePath))
                {
                    continue;
                }

                SourceMapMapping? mapping = sourceMap.Mappings
                    .Where(candidate => candidate.OutputLine == generatedLine)
                    .OrderBy(candidate => Math.Abs(candidate.OutputColumn - generatedColumn))
                    .ThenBy(static candidate => candidate.OutputColumn)
                    .ThenBy(static candidate => candidate.SourceFilePath, StringComparer.Ordinal)
                    .ThenBy(static candidate => candidate.SourceLine)
                    .ThenBy(static candidate => candidate.SourceColumn)
                    .FirstOrDefault();

                if (mapping is not null)
                {
                    return new SourceLocation(mapping.SourceFilePath, mapping.SourceLine, mapping.SourceColumn);
                }
            }

            return null;
        }

        private static bool OutputPathMatches(string sourceMapPath, string diagnosticPath)
        {
            if (StringComparer.Ordinal.Equals(sourceMapPath, diagnosticPath))
            {
                return true;
            }

            string normalizedSourceMapPath = NormalizePath(sourceMapPath);
            string normalizedDiagnosticPath = NormalizePath(diagnosticPath);
            if (StringComparer.Ordinal.Equals(normalizedSourceMapPath, normalizedDiagnosticPath))
            {
                return true;
            }

            return StringComparer.Ordinal.Equals(
                Path.GetFileName(normalizedSourceMapPath),
                Path.GetFileName(normalizedDiagnosticPath));
        }

        private static string NormalizePath(string path)
        {
            string normalized = path.Replace('\\', '/');
            return Path.IsPathFullyQualified(path)
                ? Path.GetFullPath(path).Replace('\\', '/')
                : normalized;
        }
    }
}

#pragma warning restore MA0048

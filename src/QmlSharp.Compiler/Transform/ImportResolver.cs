namespace QmlSharp.Compiler
{
    /// <summary>Default resolver for C# DSL namespaces that become QML imports.</summary>
    public sealed class ImportResolver : IImportResolver
    {
        private readonly Dictionary<string, ImportMapping> mappings = new(StringComparer.Ordinal);

        /// <summary>Initializes a new instance of the <see cref="ImportResolver"/> class.</summary>
        public ImportResolver()
        {
            RegisterMapping("QmlSharp.QtQuick", "QtQuick");
            RegisterMapping("QmlSharp.QtQuick.Controls", "QtQuick.Controls");
            RegisterMapping("QmlSharp.QtQuick.Layouts", "QtQuick.Layouts");
            RegisterMapping("QmlSharp.QtQml", "QtQml");
        }

        /// <inheritdoc/>
        public ResolvedImport? ResolveSingle(DiscoveredImport import, CompilerOptions options)
        {
            ArgumentNullException.ThrowIfNull(import);
            ArgumentNullException.ThrowIfNull(options);

            if (!mappings.TryGetValue(import.CSharpNamespace, out ImportMapping? mapping))
            {
                return null;
            }

            QmlVersion version = mapping.Version ?? options.ModuleVersion;
            return new ResolvedImport(import.CSharpNamespace, mapping.QmlModuleUri, version, mapping.Alias);
        }

        /// <inheritdoc/>
        public ImmutableArray<ResolvedImport> Resolve(ImmutableArray<DiscoveredImport> imports, CompilerOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            Dictionary<string, ResolvedImport> resolved = new(StringComparer.Ordinal);

            foreach (DiscoveredImport import in imports)
            {
                ResolvedImport? candidate = ResolveSingle(import, options);
                if (candidate is null)
                {
                    continue;
                }

                string key = CreateKey(candidate.QmlModuleUri, candidate.Alias);
                if (!resolved.TryGetValue(key, out ResolvedImport? existing)
                    || StringComparer.Ordinal.Compare(candidate.CSharpNamespace, existing.CSharpNamespace) < 0)
                {
                    resolved[key] = candidate;
                }
            }

            return resolved.Values
                .OrderBy(import => import.QmlModuleUri, StringComparer.Ordinal)
                .ThenBy(import => import.Alias, StringComparer.Ordinal)
                .ThenBy(import => import.CSharpNamespace, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        /// <inheritdoc/>
        public void RegisterMapping(string csharpNamespace, string qmlModuleUri, QmlVersion? version = null, string? alias = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(csharpNamespace);
            ArgumentException.ThrowIfNullOrWhiteSpace(qmlModuleUri);

            if (version is not null && (version.Major < 0 || version.Minor < 0))
            {
                throw new ArgumentException("Import mapping versions must be non-negative.", nameof(version));
            }

            string? normalizedAlias = string.IsNullOrWhiteSpace(alias) ? null : alias;
            mappings[csharpNamespace] = new ImportMapping(qmlModuleUri, version, normalizedAlias);
        }

        private sealed record ImportMapping(string QmlModuleUri, QmlVersion? Version, string? Alias);

        private static string CreateKey(string qmlModuleUri, string? alias)
        {
            return string.Concat(qmlModuleUri, "\u001f", alias ?? string.Empty);
        }
    }
}

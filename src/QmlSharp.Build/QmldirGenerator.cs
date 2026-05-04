using System.Globalization;
using System.Text;
using QmlSharp.Compiler;

namespace QmlSharp.Build
{
    /// <summary>Generates deterministic qmldir content from module schemas.</summary>
    public sealed class QmldirGenerator : IQmldirGenerator
    {
        /// <inheritdoc />
        public string Generate(
            string moduleUri,
            QmlVersion version,
            ImmutableArray<ViewModelSchema> schemas)
        {
            return Generate(moduleUri, version, schemas, ImmutableArray<string>.Empty);
        }

        /// <inheritdoc />
        public string Generate(
            string moduleUri,
            QmlVersion version,
            ImmutableArray<ViewModelSchema> schemas,
            ImmutableArray<string> qmlFiles)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(moduleUri);
            ArgumentNullException.ThrowIfNull(version);

            StringBuilder builder = new();
            builder.Append("module ");
            builder.Append(moduleUri);
            builder.Append('\n');
            builder.Append("typeinfo ");
            builder.Append(ModuleMetadataPaths.GetQmltypesFileName(moduleUri));
            builder.Append('\n');

            foreach (QmldirViewEntry entry in CreateViewEntries(version, schemas, qmlFiles))
            {
                builder.Append(entry.Name);
                builder.Append(' ');
                builder.Append(entry.Version);
                builder.Append(' ');
                builder.Append(entry.FileName);
                builder.Append('\n');
            }

            return builder.ToString();
        }

        private static ImmutableArray<QmldirViewEntry> CreateViewEntries(
            QmlVersion version,
            ImmutableArray<ViewModelSchema> schemas,
            ImmutableArray<string> qmlFiles)
        {
            string versionText = string.Create(
                CultureInfo.InvariantCulture,
                $"{version.Major}.{version.Minor}");
            ImmutableArray<QmldirViewEntry> entries = qmlFiles.IsDefaultOrEmpty
                ? CreateEntriesFromSchemas(schemas, versionText)
                : CreateEntriesFromQmlFiles(qmlFiles, versionText);

            return entries
                .GroupBy(static entry => entry.FileName, StringComparer.Ordinal)
                .Select(static group => group
                    .OrderBy(static entry => entry.Name, StringComparer.Ordinal)
                    .First())
                .OrderBy(static entry => entry.Name, StringComparer.Ordinal)
                .ThenBy(static entry => entry.FileName, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static ImmutableArray<QmldirViewEntry> CreateEntriesFromQmlFiles(
            ImmutableArray<string> qmlFiles,
            string versionText)
        {
            ImmutableArray<QmldirViewEntry>.Builder builder = ImmutableArray.CreateBuilder<QmldirViewEntry>();
            foreach (string qmlFile in qmlFiles)
            {
                string fileName = Path.GetFileName(qmlFile);
                if (!fileName.EndsWith(".qml", StringComparison.Ordinal))
                {
                    continue;
                }

                string viewName = Path.GetFileNameWithoutExtension(fileName);
                builder.Add(new QmldirViewEntry(viewName, versionText, ModuleMetadataPaths.ToQmlRelativePath(fileName)));
            }

            return builder.ToImmutable();
        }

        private static ImmutableArray<QmldirViewEntry> CreateEntriesFromSchemas(
            ImmutableArray<ViewModelSchema> schemas,
            string versionText)
        {
            ImmutableArray<QmldirViewEntry>.Builder builder = ImmutableArray.CreateBuilder<QmldirViewEntry>();
            foreach (ViewModelSchema schema in schemas.IsDefault ? ImmutableArray<ViewModelSchema>.Empty : schemas)
            {
                string viewName = DeriveViewName(schema);
                builder.Add(new QmldirViewEntry(viewName, versionText, $"{viewName}.qml"));
            }

            return builder.ToImmutable();
        }

        private static string DeriveViewName(ViewModelSchema schema)
        {
            int separatorIndex = schema.CompilerSlotKey.IndexOf("::", StringComparison.Ordinal);
            if (separatorIndex > 0)
            {
                return schema.CompilerSlotKey[..separatorIndex];
            }

            const string viewModelSuffix = "ViewModel";
            if (schema.ClassName.EndsWith(viewModelSuffix, StringComparison.Ordinal) &&
                schema.ClassName.Length > viewModelSuffix.Length)
            {
                return schema.ClassName[..^viewModelSuffix.Length];
            }

            return schema.ClassName;
        }

        private sealed record QmldirViewEntry(string Name, string Version, string FileName);
    }
}

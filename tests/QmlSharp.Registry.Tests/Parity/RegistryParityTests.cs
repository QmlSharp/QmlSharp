using System.Text.Json;
using System.Text.Json.Serialization;
using QmlSharp.Registry.Normalization;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Registry.Tests.Parity
{
    public sealed class RegistryParityTests
    {
        private static readonly Lazy<BuildResult> ParityBuild = new(CreateParityBuild);

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true,
        };

        [Fact]
        public void Parity_representative_types_are_queryable_by_qml_name()
        {
            BuildResult result = GetParityBuild();

            QmlType? item = result.Query!.FindTypeByQmlName("QtQuick", "Item");
            QmlType? rectangle = result.Query.FindTypeByQmlName("QtQuick", "Rectangle");
            QmlType? button = result.Query.FindTypeByQmlName("QtQuick.Controls", "Button");

            Assert.NotNull(item);
            Assert.NotNull(rectangle);
            Assert.NotNull(button);
            Assert.Equal("QQuickItem", item!.QualifiedName);
            Assert.Equal("QQuickRectangle", rectangle!.QualifiedName);
            Assert.Equal("QQuickButton", button!.QualifiedName);
        }

        [Fact]
        public void Parity_normalized_registry_subset_matches_expected_json()
        {
            QmlRegistry registry = GetParityBuild().TypeRegistry!.Registry;

            string actualJson = Serialize(ProjectNormalizedRegistrySubset(registry));
            string expectedJson = ReadFixture("parity", "expected", "normalized-registry-subset.json");

            Assert.Equal(NormalizeJson(expectedJson), NormalizeJson(actualJson));
        }

        [Fact]
        public void Parity_type_name_mappings_match_expected_json()
        {
            TypeNameMapper mapper = new();

            string actualJson = Serialize(new
            {
                forward = new[]
                {
                    new { cppType = "QString", qmlType = mapper.ToQmlName("QString") },
                    new { cppType = "QColor", qmlType = mapper.ToQmlName("QColor") },
                    new { cppType = "QPointF", qmlType = mapper.ToQmlName("QPointF") },
                    new { cppType = "QSizeF", qmlType = mapper.ToQmlName("QSizeF") },
                    new { cppType = "QRectF", qmlType = mapper.ToQmlName("QRectF") },
                    new { cppType = "QVariantList", qmlType = mapper.ToQmlName("QVariantList") },
                },
                reverse = new[]
                {
                    new { qmlType = "string", cppType = mapper.ToCppName("string") },
                    new { qmlType = "color", cppType = mapper.ToCppName("color") },
                    new { qmlType = "point", cppType = mapper.ToCppName("point") },
                    new { qmlType = "size", cppType = mapper.ToCppName("size") },
                    new { qmlType = "rect", cppType = mapper.ToCppName("rect") },
                    new { qmlType = "list", cppType = mapper.ToCppName("list") },
                },
            });

            string expectedJson = ReadFixture("parity", "expected", "type-name-mappings.json");
            Assert.Equal(NormalizeJson(expectedJson), NormalizeJson(actualJson));
        }

        [Fact]
        public void Parity_inheritance_chains_match_expected_json()
        {
            IRegistryQuery query = GetParityBuild().Query!;

            string actualJson = Serialize(new
            {
                chains = new[]
                {
                    new
                    {
                        qualifiedName = "QQuickRectangle",
                        types = query.GetInheritanceChain("QQuickRectangle").Select(type => type.QualifiedName).ToArray(),
                    },
                    new
                    {
                        qualifiedName = "QQuickButton",
                        types = query.GetInheritanceChain("QQuickButton").Select(type => type.QualifiedName).ToArray(),
                    },
                    new
                    {
                        qualifiedName = "QQuickPalette",
                        types = query.GetInheritanceChain("QQuickPalette").Select(type => type.QualifiedName).ToArray(),
                    },
                },
            });

            string expectedJson = ReadFixture("parity", "expected", "inheritance-chains.json");
            Assert.Equal(NormalizeJson(expectedJson), NormalizeJson(actualJson));
        }

        [Fact]
        public void Parity_attached_singleton_and_value_type_metadata_matches_expected_categories()
        {
            IRegistryQuery query = GetParityBuild().Query!;

            QmlType attachedTypeOwner = Assert.Single(query.GetAttachedTypes());
            Assert.Equal("QQuickItem", attachedTypeOwner.QualifiedName);
            Assert.Equal("QQuickKeysAttached", attachedTypeOwner.AttachedType);

            QmlType singleton = Assert.Single(query.GetSingletonTypes());
            Assert.Equal("QQuickPalette", singleton.QualifiedName);
            Assert.False(singleton.IsCreatable);

            Assert.Equal(
                ["QColor", "QPointF", "QRectF", "QSizeF"],
                query.GetValueTypes()
                    .Select(type => type.QualifiedName)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray());
        }

        private static BuildResult CreateParityBuild()
        {
            BuildResult result = new RegistryBuilder().Build(new BuildConfig(
                QtDir: GetParityQtDir(),
                SnapshotPath: null,
                ForceRebuild: true,
                ModuleFilter: null,
                IncludeInternal: false));

            if (!result.IsSuccess || result.TypeRegistry is null || result.Query is null)
            {
                throw new InvalidOperationException($"Parity fixture build failed:{Environment.NewLine}{FormatDiagnostics(result.Diagnostics)}");
            }

            return result;
        }

        private static BuildResult GetParityBuild()
        {
            return ParityBuild.Value;
        }

        private static object ProjectNormalizedRegistrySubset(QmlRegistry registry)
        {
            return new
            {
                formatVersion = registry.FormatVersion,
                qtVersion = registry.QtVersion,
                types = new[]
                {
                    ProjectType(registry.TypesByQualifiedName["QQuickItem"]),
                    ProjectType(registry.TypesByQualifiedName["QQuickRectangle"]),
                    ProjectType(registry.TypesByQualifiedName["QQuickButton"]),
                    ProjectType(registry.TypesByQualifiedName["QQuickPalette"]),
                    ProjectType(registry.TypesByQualifiedName["QColor"]),
                },
            };
        }

        private static object ProjectType(QmlType type)
        {
            return new
            {
                qualifiedName = type.QualifiedName,
                qmlName = type.QmlName,
                moduleUri = type.ModuleUri,
                accessSemantics = type.AccessSemantics.ToString().ToLowerInvariant(),
                prototype = type.Prototype,
                defaultProperty = type.DefaultProperty,
                attachedType = type.AttachedType,
                extension = type.Extension,
                isSingleton = type.IsSingleton,
                isCreatable = type.IsCreatable,
                exports = type.Exports
                    .OrderBy(exportItem => exportItem.Module, StringComparer.Ordinal)
                    .ThenBy(exportItem => exportItem.Name, StringComparer.Ordinal)
                    .ThenBy(exportItem => exportItem.Version.Major)
                    .ThenBy(exportItem => exportItem.Version.Minor)
                    .Select(exportItem => $"{exportItem.Module}/{exportItem.Name} {exportItem.Version}")
                    .ToArray(),
                properties = type.Properties
                    .OrderBy(property => property.Name, StringComparer.Ordinal)
                    .Select(property => new
                    {
                        name = property.Name,
                        typeName = property.TypeName,
                        isReadonly = property.IsReadonly,
                        isList = property.IsList,
                        isRequired = property.IsRequired,
                        notifySignal = property.NotifySignal,
                    })
                    .ToArray(),
                signals = type.Signals
                    .OrderBy(signal => signal.Name, StringComparer.Ordinal)
                    .ThenBy(signal => BuildSignature(signal.Parameters.Select(parameter => parameter.TypeName)))
                    .Select(signal => new
                    {
                        name = signal.Name,
                        parameterTypes = signal.Parameters.Select(parameter => parameter.TypeName).ToArray(),
                    })
                    .ToArray(),
                methods = type.Methods
                    .OrderBy(method => method.Name, StringComparer.Ordinal)
                    .ThenBy(method => BuildSignature(method.Parameters.Select(parameter => parameter.TypeName)))
                    .Select(method => new
                    {
                        name = method.Name,
                        returnType = method.ReturnType,
                        parameterTypes = method.Parameters.Select(parameter => parameter.TypeName).ToArray(),
                    })
                    .ToArray(),
            };
        }

        private static string BuildSignature(IEnumerable<string> parameterTypes)
        {
            return string.Join(",", parameterTypes);
        }

        private static string FormatDiagnostics(IEnumerable<QmlSharp.Registry.Diagnostics.RegistryDiagnostic> diagnostics)
        {
            return string.Join(
                Environment.NewLine,
                diagnostics.Select(diagnostic => $"{diagnostic.Severity}: {diagnostic.Code}: {diagnostic.Message}"));
        }

        private static string GetParityQtDir()
        {
            return Path.GetFullPath(Path.Join(AppContext.BaseDirectory, "fixtures", "parity", "qt-sdk"));
        }

        private static string NormalizeLineEndings(string value)
        {
            return value.Replace("\r\n", "\n", StringComparison.Ordinal);
        }

        private static string NormalizeJson(string value)
        {
            return NormalizeLineEndings(value).TrimEnd();
        }

        private static string ReadFixture(params string[] relativeSegments)
        {
            string filePath = Path.Join([AppContext.BaseDirectory, "fixtures", .. relativeSegments]);
            return File.ReadAllText(filePath);
        }

        private static string Serialize(object value)
        {
            return JsonSerializer.Serialize(value, JsonOptions);
        }
    }
}

using System.Buffers.Binary;
using QmlSharp.Registry.Diagnostics;
using QmlSharp.Registry.Querying;
using QmlSharp.Registry.Snapshots;
using QmlSharp.Registry.Tests.Helpers;

namespace QmlSharp.Registry.Tests.Snapshots
{
    public sealed class RegistrySnapshotTests
    {
        private static readonly RegistrySnapshot Snapshot = new();
        private const int SnapshotMagicLength = 7;
        private const int HeaderLengthFieldSize = sizeof(int);

        [Fact]
        public void SNP_01_Round_trip_serialize_then_deserialize_preserves_registry_value_graph()
        {
            QmlRegistry registry = RegistryFixtures.CreateQueryFixture();

            byte[] bytes = Snapshot.Serialize(registry);
            QmlRegistry roundTripped = Snapshot.Deserialize(bytes);

            AssertRegistriesEquivalent(registry, roundTripped);
            Assert.Equal(bytes, Snapshot.Serialize(roundTripped));
        }

        [Fact]
        public void SNP_02_Round_trip_preserves_all_types()
        {
            QmlRegistry registry = RegistryFixtures.CreateQueryFixture();

            QmlRegistry roundTripped = Snapshot.Deserialize(Snapshot.Serialize(registry));

            Assert.Equal(registry.TypesByQualifiedName.Count, roundTripped.TypesByQualifiedName.Count);
            Assert.Equal(
                registry.TypesByQualifiedName.Keys.OrderBy(name => name, StringComparer.Ordinal),
                roundTripped.TypesByQualifiedName.Keys.OrderBy(name => name, StringComparer.Ordinal));
        }

        [Fact]
        public void SNP_03_Round_trip_preserves_all_modules()
        {
            QmlRegistry registry = RegistryFixtures.CreateQueryFixture();

            QmlRegistry roundTripped = Snapshot.Deserialize(Snapshot.Serialize(registry));

            Assert.Equal(registry.Modules.Length, roundTripped.Modules.Length);
            Assert.Equal(
                registry.Modules.Select(module => module.Uri),
                roundTripped.Modules.Select(module => module.Uri));
        }

        [Fact]
        public void SNP_04_Round_trip_preserves_format_version()
        {
            QmlRegistry registry = RegistryFixtures.CreateQueryFixture();

            QmlRegistry roundTripped = Snapshot.Deserialize(Snapshot.Serialize(registry));

            Assert.Equal(registry.FormatVersion, roundTripped.FormatVersion);
        }

        [Fact]
        public void SNP_05_Round_trip_preserves_qt_version()
        {
            QmlRegistry registry = RegistryFixtures.CreateQueryFixture();

            QmlRegistry roundTripped = Snapshot.Deserialize(Snapshot.Serialize(registry));

            Assert.Equal(registry.QtVersion, roundTripped.QtVersion);
            Assert.Equal(registry.BuildTimestamp, roundTripped.BuildTimestamp);
        }

        [Fact]
        public void SNP_06_Save_to_file_and_load_from_file_round_trips_registry()
        {
            QmlRegistry registry = RegistryFixtures.CreateQueryFixture();
            using TemporaryDirectory temporaryDirectory = new();
            string snapshotPath = Path.Join(temporaryDirectory.Path, "registry.snapshot.bin");

            Snapshot.SaveToFile(registry, snapshotPath);
            QmlRegistry loaded = Snapshot.LoadFromFile(snapshotPath);

            Assert.True(File.Exists(snapshotPath));
            AssertRegistriesEquivalent(registry, loaded);
        }

        [Fact]
        public void SNP_07_CheckValidity_on_valid_snapshot_returns_true_with_version_info()
        {
            QmlRegistry registry = RegistryFixtures.CreateQueryFixture();
            using TemporaryDirectory temporaryDirectory = new();
            string snapshotPath = Path.Join(temporaryDirectory.Path, "registry.snapshot.bin");

            Snapshot.SaveToFile(registry, snapshotPath);

            SnapshotValidity validity = Snapshot.CheckValidity(snapshotPath);

            Assert.True(validity.IsValid);
            Assert.Equal(registry.FormatVersion, validity.FormatVersion);
            Assert.Equal(registry.QtVersion, validity.QtVersion);
            Assert.Equal(registry.BuildTimestamp, validity.BuildTimestamp);
            Assert.Null(validity.ErrorMessage);
        }

        [Fact]
        public void SNP_08_CheckValidity_on_incompatible_format_version_returns_SnapshotVersionMismatch()
        {
            QmlRegistry registry = RegistryFixtures.CreateQueryFixture() with { FormatVersion = 99 };
            using TemporaryDirectory temporaryDirectory = new();
            string snapshotPath = Path.Join(temporaryDirectory.Path, "registry.snapshot.bin");

            Snapshot.SaveToFile(registry, snapshotPath);

            SnapshotValidity validity = Snapshot.CheckValidity(snapshotPath);

            Assert.False(validity.IsValid);
            Assert.Equal(99, validity.FormatVersion);
            Assert.Equal(registry.QtVersion, validity.QtVersion);
            Assert.Contains(DiagnosticCodes.SnapshotVersionMismatch, validity.ErrorMessage, StringComparison.Ordinal);

            NotSupportedException exception = Assert.Throws<NotSupportedException>(() => Snapshot.LoadFromFile(snapshotPath));
            Assert.Contains(DiagnosticCodes.SnapshotVersionMismatch, exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void SNP_09_CheckValidity_on_corrupt_snapshot_returns_SnapshotCorrupt()
        {
            QmlRegistry registry = RegistryFixtures.CreateQueryFixture();
            byte[] bytes = Snapshot.Serialize(registry);
            bytes[^1] ^= 0x5A;

            using TemporaryDirectory temporaryDirectory = new();
            string snapshotPath = Path.Join(temporaryDirectory.Path, "registry.snapshot.bin");
            File.WriteAllBytes(snapshotPath, bytes);

            SnapshotValidity validity = Snapshot.CheckValidity(snapshotPath);

            Assert.False(validity.IsValid);
            Assert.Equal(0, validity.FormatVersion);
            Assert.Contains(DiagnosticCodes.SnapshotCorrupt, validity.ErrorMessage, StringComparison.Ordinal);
            Assert.DoesNotContain(
                $"{DiagnosticCodes.SnapshotCorrupt}: {DiagnosticCodes.SnapshotCorrupt}:",
                validity.ErrorMessage,
                StringComparison.Ordinal);

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() => Snapshot.LoadFromFile(snapshotPath));
            Assert.Contains(DiagnosticCodes.SnapshotCorrupt, exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void CheckValidity_on_header_metadata_corruption_returns_SnapshotCorrupt()
        {
            QmlRegistry registry = RegistryFixtures.CreateQueryFixture();
            byte[] bytes = Snapshot.Serialize(registry);
            ReplaceFirstUtf8(bytes, registry.QtVersion, CreateSameLengthDifferentText(registry.QtVersion));

            using TemporaryDirectory temporaryDirectory = new();
            string snapshotPath = Path.Join(temporaryDirectory.Path, "registry.snapshot.bin");
            File.WriteAllBytes(snapshotPath, bytes);

            SnapshotValidity validity = Snapshot.CheckValidity(snapshotPath);

            Assert.False(validity.IsValid);
            Assert.Equal(0, validity.FormatVersion);
            Assert.Contains(DiagnosticCodes.SnapshotCorrupt, validity.ErrorMessage, StringComparison.Ordinal);
            Assert.Contains("envelope checksum", validity.ErrorMessage, StringComparison.Ordinal);

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() => Snapshot.LoadFromFile(snapshotPath));
            Assert.Contains("envelope checksum", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void CheckValidity_on_oversized_metadata_length_returns_SnapshotCorrupt()
        {
            byte[] bytes = Snapshot.Serialize(RegistryFixtures.CreateQueryFixture());
            BinaryPrimitives.WriteInt32LittleEndian(
                bytes.AsSpan(SnapshotMagicLength, HeaderLengthFieldSize),
                int.MaxValue);

            using TemporaryDirectory temporaryDirectory = new();
            string snapshotPath = Path.Join(temporaryDirectory.Path, "registry.snapshot.bin");
            File.WriteAllBytes(snapshotPath, bytes);

            SnapshotValidity validity = Snapshot.CheckValidity(snapshotPath);

            Assert.False(validity.IsValid);
            Assert.Equal(0, validity.FormatVersion);
            Assert.Contains(DiagnosticCodes.SnapshotCorrupt, validity.ErrorMessage, StringComparison.Ordinal);
            Assert.Contains("metadata extends beyond", validity.ErrorMessage, StringComparison.Ordinal);
        }

        [Fact]
        public void Deserialize_rebuilds_query_indexes_so_loaded_registry_queries_match_original()
        {
            QmlRegistry registry = RegistryFixtures.CreateQueryFixture();
            QmlRegistry loaded = Snapshot.Deserialize(Snapshot.Serialize(registry));
            IRegistryQuery originalQuery = new RegistryQuery(registry);
            IRegistryQuery loadedQuery = new RegistryQuery(loaded);

            Assert.Equal(
                originalQuery.GetInheritanceChain("QQuickRectangle").Select(type => type.QualifiedName),
                loadedQuery.GetInheritanceChain("QQuickRectangle").Select(type => type.QualifiedName));
            Assert.Equal(
                originalQuery.GetAllProperties("QQuickRectangle").Select(property => property.Property.Name),
                loadedQuery.GetAllProperties("QQuickRectangle").Select(property => property.Property.Name));
            Assert.Equal(
                originalQuery.GetCreatableTypes().Select(type => type.QualifiedName),
                loadedQuery.GetCreatableTypes().Select(type => type.QualifiedName));

            QmlType? loadedType = loadedQuery.FindTypeByQmlName("QtQuick.Controls", "Button");
            Assert.NotNull(loadedType);
            Assert.Equal("QQuickButton", loadedType!.QualifiedName);
        }

        [Fact]
        public void Serialize_is_deterministic_for_equivalent_registry_content()
        {
            QmlRegistry registry = RegistryFixtures.CreateQueryFixture();

            byte[] first = Snapshot.Serialize(registry);
            byte[] second = Snapshot.Serialize(registry);

            Assert.Equal(first, second);
        }

        private static void AssertRegistriesEquivalent(QmlRegistry expected, QmlRegistry actual)
        {
            Assert.Equal(expected.FormatVersion, actual.FormatVersion);
            Assert.Equal(expected.QtVersion, actual.QtVersion);
            Assert.Equal(expected.BuildTimestamp, actual.BuildTimestamp);

            Assert.Equal(expected.Modules.Length, actual.Modules.Length);
            for (int index = 0; index < expected.Modules.Length; index++)
            {
                AssertModulesEquivalent(expected.Modules[index], actual.Modules[index]);
            }

            KeyValuePair<string, QmlType>[] expectedTypes = expected.TypesByQualifiedName
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToArray();
            KeyValuePair<string, QmlType>[] actualTypes = actual.TypesByQualifiedName
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(expectedTypes.Length, actualTypes.Length);
            for (int index = 0; index < expectedTypes.Length; index++)
            {
                Assert.Equal(expectedTypes[index].Key, actualTypes[index].Key);
                AssertTypesEquivalent(expectedTypes[index].Value, actualTypes[index].Value);
            }

            Assert.Equal(expected.Builtins.Length, actual.Builtins.Length);
            for (int index = 0; index < expected.Builtins.Length; index++)
            {
                AssertTypesEquivalent(expected.Builtins[index], actual.Builtins[index]);
            }
        }

        private static void AssertModulesEquivalent(QmlModule expected, QmlModule actual)
        {
            Assert.Equal(expected.Uri, actual.Uri);
            Assert.Equal(expected.Version, actual.Version);
            Assert.Equal(expected.Dependencies.ToArray(), actual.Dependencies.ToArray());
            Assert.Equal(expected.Imports.ToArray(), actual.Imports.ToArray());
            Assert.Equal(expected.Types.Length, actual.Types.Length);

            for (int index = 0; index < expected.Types.Length; index++)
            {
                Assert.Equal(expected.Types[index].QualifiedName, actual.Types[index].QualifiedName);
                Assert.Equal(expected.Types[index].QmlName, actual.Types[index].QmlName);
                Assert.Equal(expected.Types[index].ExportVersion, actual.Types[index].ExportVersion);
            }
        }

        private static void AssertTypesEquivalent(QmlType expected, QmlType actual)
        {
            Assert.Equal(expected.QualifiedName, actual.QualifiedName);
            Assert.Equal(expected.QmlName, actual.QmlName);
            Assert.Equal(expected.ModuleUri, actual.ModuleUri);
            Assert.Equal(expected.AccessSemantics, actual.AccessSemantics);
            Assert.Equal(expected.Prototype, actual.Prototype);
            Assert.Equal(expected.DefaultProperty, actual.DefaultProperty);
            Assert.Equal(expected.AttachedType, actual.AttachedType);
            Assert.Equal(expected.Extension, actual.Extension);
            Assert.Equal(expected.IsSingleton, actual.IsSingleton);
            Assert.Equal(expected.IsCreatable, actual.IsCreatable);
            Assert.Equal(expected.Exports.Length, actual.Exports.Length);
            Assert.Equal(expected.Properties.Length, actual.Properties.Length);
            Assert.Equal(expected.Signals.Length, actual.Signals.Length);
            Assert.Equal(expected.Methods.Length, actual.Methods.Length);
            Assert.Equal(expected.Enums.Length, actual.Enums.Length);
            Assert.Equal(expected.Interfaces.ToArray(), actual.Interfaces.ToArray());

            AssertExportsEquivalent(expected, actual);
            AssertPropertiesEquivalent(expected, actual);
            AssertSignalsEquivalent(expected, actual);
            AssertMethodsEquivalent(expected, actual);
            AssertEnumsEquivalent(expected, actual);
        }

        private static void AssertExportsEquivalent(QmlType expected, QmlType actual)
        {
            for (int index = 0; index < expected.Exports.Length; index++)
            {
                Assert.Equal(expected.Exports[index].Module, actual.Exports[index].Module);
                Assert.Equal(expected.Exports[index].Name, actual.Exports[index].Name);
                Assert.Equal(expected.Exports[index].Version, actual.Exports[index].Version);
            }
        }

        private static void AssertPropertiesEquivalent(QmlType expected, QmlType actual)
        {
            for (int index = 0; index < expected.Properties.Length; index++)
            {
                Assert.Equal(expected.Properties[index], actual.Properties[index]);
            }
        }

        private static void AssertSignalsEquivalent(QmlType expected, QmlType actual)
        {
            for (int index = 0; index < expected.Signals.Length; index++)
            {
                Assert.Equal(expected.Signals[index].Name, actual.Signals[index].Name);
                Assert.Equal(expected.Signals[index].Parameters.ToArray(), actual.Signals[index].Parameters.ToArray());
            }
        }

        private static void AssertMethodsEquivalent(QmlType expected, QmlType actual)
        {
            for (int index = 0; index < expected.Methods.Length; index++)
            {
                Assert.Equal(expected.Methods[index].Name, actual.Methods[index].Name);
                Assert.Equal(expected.Methods[index].ReturnType, actual.Methods[index].ReturnType);
                Assert.Equal(expected.Methods[index].Parameters.ToArray(), actual.Methods[index].Parameters.ToArray());
            }
        }

        private static void AssertEnumsEquivalent(QmlType expected, QmlType actual)
        {
            for (int index = 0; index < expected.Enums.Length; index++)
            {
                Assert.Equal(expected.Enums[index].Name, actual.Enums[index].Name);
                Assert.Equal(expected.Enums[index].IsFlag, actual.Enums[index].IsFlag);
                Assert.Equal(expected.Enums[index].Values.ToArray(), actual.Enums[index].Values.ToArray());
            }
        }

        private static void ReplaceFirstUtf8(byte[] bytes, string oldValue, string newValue)
        {
            byte[] oldBytes = System.Text.Encoding.UTF8.GetBytes(oldValue);
            byte[] newBytes = System.Text.Encoding.UTF8.GetBytes(newValue);
            Assert.Equal(oldBytes.Length, newBytes.Length);

            int index = bytes.AsSpan().IndexOf(oldBytes);
            Assert.True(index >= 0);

            newBytes.CopyTo(bytes.AsSpan(index, newBytes.Length));
        }

        private static string CreateSameLengthDifferentText(string value)
        {
            Assert.False(string.IsNullOrEmpty(value));

            char replacement = value[^1] == '0' ? '1' : '0';
            return value[..^1] + replacement;
        }

        private sealed class TemporaryDirectory : IDisposable
        {
            public TemporaryDirectory()
            {
                Path = Directory.CreateTempSubdirectory("qmlsharp-registry-snapshot-").FullName;
            }

            public string Path { get; }

            public void Dispose()
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
        }
    }
}

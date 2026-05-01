using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using QmlSharp.Registry.Diagnostics;
using QmlSharp.Registry.Querying;
using QmlSharp.Registry.Snapshots;
using QmlSharp.Registry.Tests.Helpers;

namespace QmlSharp.Registry.Tests.Snapshots
{
    public sealed class RegistrySnapshotTests
    {
        private static readonly RegistrySnapshot Snapshot = new();
        private static readonly byte[] SnapshotMagic = "QRSNP1\0"u8.ToArray();
        private const int SnapshotMagicLength = 7;
        private const int HeaderLengthFieldSize = sizeof(int);

        public static TheoryData<string, string> CorruptNestedPayloadCases => new()
        {
            { "module-missing-uri", "Snapshot module is missing its URI." },
            { "module-missing-fields", "Snapshot module 'QtQuick' is missing one or more required fields." },
            { "module-type-missing-fields", "Snapshot module type is missing one or more required fields." },
            { "type-missing-qualified-name", "Snapshot type is missing its qualified name." },
            { "type-invalid-access-semantics", "contains an invalid access semantics value." },
            { "type-missing-collections", "is missing one or more required collections." },
            { "type-export-missing-fields", "Snapshot type export is missing one or more required fields." },
            { "property-missing-name", "Snapshot property is missing one or more required fields." },
            { "property-missing-fields", "Snapshot property is missing one or more required fields." },
            { "signal-missing-name", "Snapshot signal is missing one or more required fields." },
            { "signal-missing-fields", "Snapshot signal is missing one or more required fields." },
            { "method-missing-name", "Snapshot method is missing one or more required fields." },
            { "method-missing-fields", "Snapshot method is missing one or more required fields." },
            { "parameter-missing-name", "Snapshot parameter is missing one or more required fields." },
            { "parameter-missing-fields", "Snapshot parameter is missing one or more required fields." },
            { "enum-missing-name", "Snapshot enum is missing one or more required fields." },
            { "enum-missing-fields", "Snapshot enum is missing one or more required fields." },
            { "enum-value-missing-name", "Snapshot enum value is missing its name." },
        };

        public static TheoryData<string> MissingTopLevelPayloadCollections => new()
        {
            "modules",
            "types",
            "builtins",
        };

        public static TheoryData<string, string> InvalidMetadataCases => new()
        {
            { "missing-qt-version", "Qt version" },
            { "invalid-build-timestamp", "build timestamp" },
            { "negative-payload-length", "negative payload length" },
            { "unsupported-payload-compression", "unsupported payload compression" },
        };

        public static TheoryData<string, string> DuplicatePayloadCases => new()
        {
            { "modules", "Duplicate module URI" },
            { "types", "Duplicate qualified type" },
            { "builtins", "Duplicate builtin type" },
        };

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

        [Theory]
        [MemberData(nameof(CorruptNestedPayloadCases))]
        public void Deserialize_with_corrupt_nested_payload_fields_returns_snapshot_corrupt(string caseName, string expectedMessage)
        {
            byte[] bytes = CreateMutatedSnapshot(payload => ApplyNestedPayloadMutation(payload, caseName));

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() => Snapshot.Deserialize(bytes));

            Assert.Contains(DiagnosticCodes.SnapshotCorrupt, exception.Message, StringComparison.Ordinal);
            Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
        }

        [Theory]
        [MemberData(nameof(MissingTopLevelPayloadCollections))]
        public void Deserialize_with_missing_top_level_payload_collections_returns_snapshot_corrupt(string propertyName)
        {
            byte[] bytes = CreateMutatedSnapshot(payload => payload[propertyName] = null);

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() => Snapshot.Deserialize(bytes));

            Assert.Contains(DiagnosticCodes.SnapshotCorrupt, exception.Message, StringComparison.Ordinal);
            Assert.Contains("missing one or more required collections", exception.Message, StringComparison.Ordinal);
        }

        [Theory]
        [MemberData(nameof(DuplicatePayloadCases))]
        public void Deserialize_with_duplicate_payload_entries_returns_snapshot_corrupt(string propertyName, string expectedMessage)
        {
            byte[] bytes = CreateMutatedSnapshot(payload =>
            {
                JsonArray items = payload[propertyName]!.AsArray();
                items.Add(items[0]!.DeepClone());
            });

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() => Snapshot.Deserialize(bytes));

            Assert.Contains(DiagnosticCodes.SnapshotCorrupt, exception.Message, StringComparison.Ordinal);
            Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
        }

        [Theory]
        [MemberData(nameof(InvalidMetadataCases))]
        public void CheckValidity_with_invalid_metadata_fields_returns_snapshot_corrupt(string caseName, string expectedMessage)
        {
            using TemporaryDirectory temporaryDirectory = new();
            string snapshotPath = Path.Join(temporaryDirectory.Path, $"{caseName}.snapshot.bin");
            byte[] bytes = CreateMutatedSnapshot(
                payloadMutation: null,
                metadataMutation: metadata => ApplyMetadataMutation(metadata, caseName));
            File.WriteAllBytes(snapshotPath, bytes);

            SnapshotValidity validity = Snapshot.CheckValidity(snapshotPath);

            Assert.False(validity.IsValid);
            Assert.Equal(0, validity.FormatVersion);
            Assert.Contains(DiagnosticCodes.SnapshotCorrupt, validity.ErrorMessage, StringComparison.Ordinal);
            Assert.Contains(expectedMessage, validity.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Deserialize_with_invalid_payload_json_returns_snapshot_corrupt()
        {
            byte[] bytes = CreateSnapshotWithRawPayload("{"u8.ToArray());

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() => Snapshot.Deserialize(bytes));

            Assert.Contains(DiagnosticCodes.SnapshotCorrupt, exception.Message, StringComparison.Ordinal);
            Assert.Contains("Failed to parse snapshot payload JSON", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Deserialize_with_empty_payload_json_returns_snapshot_corrupt()
        {
            byte[] bytes = CreateSnapshotWithRawPayload("null"u8.ToArray());

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() => Snapshot.Deserialize(bytes));

            Assert.Contains(DiagnosticCodes.SnapshotCorrupt, exception.Message, StringComparison.Ordinal);
            Assert.Contains("Snapshot payload JSON was empty", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Deserialize_with_invalid_metadata_json_returns_snapshot_corrupt()
        {
            byte[] bytes = CreateSnapshotWithRawMetadata("{"u8.ToArray());

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() => Snapshot.Deserialize(bytes));

            Assert.Contains(DiagnosticCodes.SnapshotCorrupt, exception.Message, StringComparison.Ordinal);
            Assert.Contains("Failed to parse snapshot metadata JSON", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Deserialize_with_empty_metadata_json_returns_snapshot_corrupt()
        {
            byte[] bytes = CreateSnapshotWithRawMetadata("null"u8.ToArray());

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() => Snapshot.Deserialize(bytes));

            Assert.Contains(DiagnosticCodes.SnapshotCorrupt, exception.Message, StringComparison.Ordinal);
            Assert.Contains("Snapshot metadata JSON was empty", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Deserialize_with_invalid_magic_header_returns_snapshot_corrupt()
        {
            byte[] bytes = Snapshot.Serialize(RegistryFixtures.CreateQueryFixture());
            bytes[0] ^= 0x5A;

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() => Snapshot.Deserialize(bytes));

            Assert.Contains(DiagnosticCodes.SnapshotCorrupt, exception.Message, StringComparison.Ordinal);
            Assert.Contains("expected magic header", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Deserialize_with_zero_metadata_length_returns_snapshot_corrupt()
        {
            byte[] bytes = Snapshot.Serialize(RegistryFixtures.CreateQueryFixture());
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(SnapshotMagicLength, HeaderLengthFieldSize), 0);

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() => Snapshot.Deserialize(bytes));

            Assert.Contains(DiagnosticCodes.SnapshotCorrupt, exception.Message, StringComparison.Ordinal);
            Assert.Contains("metadata length must be greater than zero", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Deserialize_with_payload_length_mismatch_returns_snapshot_corrupt()
        {
            byte[] bytes = CreateMutatedSnapshot(
                payloadMutation: null,
                metadataMutation: metadata => metadata["payloadLength"] = metadata["payloadLength"]!.GetValue<int>() + 1);

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() => Snapshot.Deserialize(bytes));

            Assert.Contains(DiagnosticCodes.SnapshotCorrupt, exception.Message, StringComparison.Ordinal);
            Assert.Contains("payload length does not match the file size", exception.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void File_based_APIs_reject_null_or_whitespace_paths(string? filePath)
        {
            QmlRegistry registry = RegistryFixtures.CreateQueryFixture();

            _ = Assert.Throws<ArgumentException>(() => Snapshot.SaveToFile(registry, filePath!));
            _ = Assert.Throws<ArgumentException>(() => Snapshot.LoadFromFile(filePath!));
            _ = Assert.Throws<ArgumentException>(() => Snapshot.CheckValidity(filePath!));
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
                Assert.Equal(expected.Enums[index].Alias, actual.Enums[index].Alias);
                Assert.Equal(expected.Enums[index].IsScoped, actual.Enums[index].IsScoped);
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

        private static byte[] CreateMutatedSnapshot(Action<JsonObject>? payloadMutation = null, Action<JsonObject>? metadataMutation = null)
        {
            byte[] bytes = Snapshot.Serialize(RegistryFixtures.CreateQueryFixture());
            (JsonObject metadata, JsonObject payload) = ReadSnapshot(bytes);

            payloadMutation?.Invoke(payload);

            byte[] payloadBytes = CompressPayload(JsonSerializer.SerializeToUtf8Bytes(payload));
            metadata["payloadLength"] = payloadBytes.Length;
            metadata["payloadSha256"] = ComputeSha256(payloadBytes);
            metadataMutation?.Invoke(metadata);
            metadata["envelopeSha256"] = string.Empty;
            metadata["envelopeSha256"] = ComputeEnvelopeSha256(metadata, payloadBytes);

            return WriteSnapshot(metadata, payloadBytes);
        }

        private static (JsonObject Metadata, JsonObject Payload) ReadSnapshot(byte[] bytes)
        {
            int metadataLength = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(SnapshotMagicLength, HeaderLengthFieldSize));
            int metadataOffset = SnapshotMagicLength + HeaderLengthFieldSize;
            byte[] metadataBytes = bytes[metadataOffset..(metadataOffset + metadataLength)];
            byte[] payloadBytes = bytes[(metadataOffset + metadataLength)..];

            JsonObject metadata = JsonNode.Parse(metadataBytes)!.AsObject();
            JsonObject payload = JsonNode.Parse(DecompressPayload(payloadBytes))!.AsObject();

            return (metadata, payload);
        }

        private static byte[] WriteSnapshot(JsonObject metadata, byte[] payloadBytes)
        {
            byte[] metadataBytes = JsonSerializer.SerializeToUtf8Bytes(metadata);
            return WriteRawSnapshot(metadataBytes, payloadBytes);
        }

        private static byte[] WriteRawSnapshot(byte[] metadataBytes, byte[] payloadBytes)
        {
            byte[] bytes = new byte[SnapshotMagic.Length + HeaderLengthFieldSize + metadataBytes.Length + payloadBytes.Length];

            Buffer.BlockCopy(SnapshotMagic, 0, bytes, 0, SnapshotMagic.Length);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(SnapshotMagic.Length, HeaderLengthFieldSize), metadataBytes.Length);
            Buffer.BlockCopy(metadataBytes, 0, bytes, SnapshotMagic.Length + HeaderLengthFieldSize, metadataBytes.Length);
            Buffer.BlockCopy(payloadBytes, 0, bytes, SnapshotMagic.Length + HeaderLengthFieldSize + metadataBytes.Length, payloadBytes.Length);

            return bytes;
        }

        private static byte[] CreateSnapshotWithRawPayload(byte[] rawPayloadJsonBytes)
        {
            byte[] bytes = Snapshot.Serialize(RegistryFixtures.CreateQueryFixture());
            (JsonObject metadata, _) = ReadSnapshot(bytes);
            byte[] payloadBytes = CompressPayload(rawPayloadJsonBytes);

            metadata["payloadLength"] = payloadBytes.Length;
            metadata["payloadSha256"] = ComputeSha256(payloadBytes);
            metadata["envelopeSha256"] = string.Empty;
            metadata["envelopeSha256"] = ComputeEnvelopeSha256(metadata, payloadBytes);

            return WriteSnapshot(metadata, payloadBytes);
        }

        private static byte[] CreateSnapshotWithRawMetadata(byte[] rawMetadataJsonBytes)
        {
            byte[] bytes = Snapshot.Serialize(RegistryFixtures.CreateQueryFixture());
            int metadataLength = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(SnapshotMagicLength, HeaderLengthFieldSize));
            int payloadOffset = SnapshotMagicLength + HeaderLengthFieldSize + metadataLength;
            byte[] payloadBytes = bytes[payloadOffset..];

            return WriteRawSnapshot(rawMetadataJsonBytes, payloadBytes);
        }

        private static void ApplyNestedPayloadMutation(JsonObject payload, string caseName)
        {
            JsonArray modules = payload["modules"]!.AsArray();
            JsonArray types = payload["types"]!.AsArray();
            JsonArray builtins = payload["builtins"]!.AsArray();
            JsonObject firstModule = modules[0]!.AsObject();
            JsonObject firstType = types[0]!.AsObject();
            JsonObject richType = types
                .Select(node => node!.AsObject())
                .First(type =>
                    type["exports"]!.AsArray().Count > 0
                    && type["properties"]!.AsArray().Count > 0
                    && type["signals"]!.AsArray().Count > 0
                    && type["methods"]!.AsArray().Count > 0
                    && type["enums"]!.AsArray().Count > 0);
            JsonObject typeWithSignalParameters = types
                .Select(node => node!.AsObject())
                .First(type => type["signals"]!.AsArray().Any(signal => signal!["parameters"]!.AsArray().Count > 0));
            JsonObject typeWithEnums = types
                .Select(node => node!.AsObject())
                .First(type => type["enums"]!.AsArray().Any(@enum => @enum!["values"]!.AsArray().Count > 0));

            switch (caseName)
            {
                case "module-missing-uri":
                    firstModule["uri"] = null;
                    break;
                case "module-missing-fields":
                    firstModule["version"] = null;
                    break;
                case "module-type-missing-fields":
                    firstModule["types"]!.AsArray()[0]!.AsObject()["qmlName"] = null;
                    break;
                case "type-missing-qualified-name":
                    firstType["qualifiedName"] = null;
                    break;
                case "type-invalid-access-semantics":
                    firstType["accessSemantics"] = 999;
                    break;
                case "type-missing-collections":
                    firstType["exports"] = null;
                    break;
                case "type-export-missing-fields":
                    richType["exports"]!.AsArray()[0]!.AsObject()["module"] = null;
                    break;
                case "property-missing-name":
                    richType["properties"]!.AsArray()[0]!.AsObject()["name"] = null;
                    break;
                case "property-missing-fields":
                    richType["properties"]!.AsArray()[0]!.AsObject()["typeName"] = null;
                    break;
                case "signal-missing-name":
                    richType["signals"]!.AsArray()[0]!.AsObject()["name"] = null;
                    break;
                case "signal-missing-fields":
                    richType["signals"]!.AsArray()[0]!.AsObject()["parameters"] = null;
                    break;
                case "method-missing-name":
                    richType["methods"]!.AsArray()[0]!.AsObject()["name"] = null;
                    break;
                case "method-missing-fields":
                    richType["methods"]!.AsArray()[0]!.AsObject()["parameters"] = null;
                    break;
                case "parameter-missing-name":
                    typeWithSignalParameters["signals"]!.AsArray()
                        .First(signal => signal!["parameters"]!.AsArray().Count > 0)!
                        .AsObject()["parameters"]!
                        .AsArray()[0]!
                        .AsObject()["name"] = null;
                    break;
                case "parameter-missing-fields":
                    typeWithSignalParameters["signals"]!.AsArray()
                        .First(signal => signal!["parameters"]!.AsArray().Count > 0)!
                        .AsObject()["parameters"]!
                        .AsArray()[0]!
                        .AsObject()["typeName"] = null;
                    break;
                case "enum-missing-name":
                    richType["enums"]!.AsArray()[0]!.AsObject()["name"] = null;
                    break;
                case "enum-missing-fields":
                    richType["enums"]!.AsArray()[0]!.AsObject()["values"] = null;
                    break;
                case "enum-value-missing-name":
                    typeWithEnums["enums"]!.AsArray()
                        .First(@enum => @enum!["values"]!.AsArray().Count > 0)!
                        .AsObject()["values"]!
                        .AsArray()[0]!
                        .AsObject()["name"] = null;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(caseName), caseName, "Unknown corruption case.");
            }

            payload["modules"] = modules;
            payload["types"] = types;
            payload["builtins"] = builtins;
        }

        private static void ApplyMetadataMutation(JsonObject metadata, string caseName)
        {
            switch (caseName)
            {
                case "missing-qt-version":
                    metadata["qtVersion"] = null;
                    break;
                case "invalid-build-timestamp":
                    metadata["buildTimestamp"] = "not-a-timestamp";
                    break;
                case "negative-payload-length":
                    metadata["payloadLength"] = -1;
                    break;
                case "unsupported-payload-compression":
                    metadata["payloadCompression"] = "zip";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(caseName), caseName, "Unknown metadata corruption case.");
            }
        }

        private static byte[] CompressPayload(byte[] payloadBytes)
        {
            using MemoryStream output = new();
            using (BrotliStream compressor = new(output, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                compressor.Write(payloadBytes, 0, payloadBytes.Length);
            }

            return output.ToArray();
        }

        private static byte[] DecompressPayload(byte[] payloadBytes)
        {
            using MemoryStream input = new(payloadBytes);
            using BrotliStream decompressor = new(input, CompressionMode.Decompress);
            using MemoryStream output = new();
            decompressor.CopyTo(output);
            return output.ToArray();
        }

        private static string ComputeSha256(byte[] bytes)
        {
            return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        }

        private static string ComputeEnvelopeSha256(JsonObject metadata, byte[] payloadBytes)
        {
            JsonObject metadataForHash = (JsonObject)metadata.DeepClone();
            metadataForHash["envelopeSha256"] = string.Empty;
            byte[] metadataBytes = JsonSerializer.SerializeToUtf8Bytes(metadataForHash);

            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            hash.AppendData(metadataBytes);
            hash.AppendData(payloadBytes);
            return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
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

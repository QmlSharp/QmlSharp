using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using QmlSharp.Registry.Diagnostics;

namespace QmlSharp.Registry.Snapshots
{
    internal sealed class RegistrySnapshot : IRegistrySnapshot
    {
        private static readonly byte[] Magic = "QRSNP1\0"u8.ToArray();
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        };

        private const int HeaderLengthFieldSize = sizeof(int);
        private const int SupportedFormatVersion = 1;
        private const string PayloadCompression = "br";

        public byte[] Serialize(QmlRegistry registry)
        {
            ArgumentNullException.ThrowIfNull(registry);

            byte[] payloadJsonBytes = JsonSerializer.SerializeToUtf8Bytes(
                SnapshotPayload.FromRegistry(registry),
                JsonOptions);
            byte[] payloadBytes = CompressPayload(payloadJsonBytes);

            string payloadSha256 = ComputeSha256(payloadBytes);
            SnapshotMetadata metadata = new()
            {
                FormatVersion = registry.FormatVersion,
                QtVersion = registry.QtVersion,
                BuildTimestamp = registry.BuildTimestamp.ToString("O", CultureInfo.InvariantCulture),
                PayloadLength = payloadBytes.Length,
                PayloadSha256 = payloadSha256,
                PayloadCompression = PayloadCompression,
                EnvelopeSha256 = string.Empty,
            };
            metadata = metadata with { EnvelopeSha256 = ComputeEnvelopeSha256(metadata, payloadBytes) };

            byte[] metadataBytes = JsonSerializer.SerializeToUtf8Bytes(metadata, JsonOptions);
            byte[] lengthBytes = new byte[HeaderLengthFieldSize];
            BinaryPrimitives.WriteInt32LittleEndian(lengthBytes, metadataBytes.Length);

            byte[] result = new byte[Magic.Length + HeaderLengthFieldSize + metadataBytes.Length + payloadBytes.Length];
            int offset = 0;

            Buffer.BlockCopy(Magic, 0, result, offset, Magic.Length);
            offset += Magic.Length;
            Buffer.BlockCopy(lengthBytes, 0, result, offset, lengthBytes.Length);
            offset += lengthBytes.Length;
            Buffer.BlockCopy(metadataBytes, 0, result, offset, metadataBytes.Length);
            offset += metadataBytes.Length;
            Buffer.BlockCopy(payloadBytes, 0, result, offset, payloadBytes.Length);

            return result;
        }

        public QmlRegistry Deserialize(byte[] data)
        {
            ArgumentNullException.ThrowIfNull(data);

            Envelope envelope = ReadEnvelope(data, validatePayloadHash: true, throwOnVersionMismatch: true);
            SnapshotPayload payload = DeserializePayload(DecompressPayload(envelope.Payload.Span));

            return BuildRegistry(payload, envelope.Metadata);
        }

        public void SaveToFile(QmlRegistry registry, string filePath)
        {
            ArgumentNullException.ThrowIfNull(registry);
            EnsureFilePath(filePath);

            string? directoryPath = Path.GetDirectoryName(Path.GetFullPath(filePath));
            if (!string.IsNullOrEmpty(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            File.WriteAllBytes(filePath, Serialize(registry));
        }

        public QmlRegistry LoadFromFile(string filePath)
        {
            EnsureFilePath(filePath);
            return Deserialize(File.ReadAllBytes(filePath));
        }

        public SnapshotValidity CheckValidity(string filePath)
        {
            EnsureFilePath(filePath);

            try
            {
                Envelope envelope = ReadEnvelope(File.ReadAllBytes(filePath), validatePayloadHash: true, throwOnVersionMismatch: false);
                if (envelope.Metadata.FormatVersion != SupportedFormatVersion)
                {
                    return CreateInvalidValidity(
                        DiagnosticCodes.SnapshotVersionMismatch,
                        $"Unsupported snapshot format version {envelope.Metadata.FormatVersion}. Expected {SupportedFormatVersion}.",
                        envelope.Metadata.FormatVersion,
                        envelope.Metadata.QtVersion,
                        envelope.Metadata.BuildTimestamp);
                }

                return new SnapshotValidity(
                    IsValid: true,
                    FormatVersion: envelope.Metadata.FormatVersion,
                    QtVersion: envelope.Metadata.QtVersion,
                    BuildTimestamp: envelope.Metadata.BuildTimestamp,
                    ErrorMessage: null);
            }
            catch (InvalidDataException exception)
            {
                return CreateInvalidValidity(
                    DiagnosticCodes.SnapshotCorrupt,
                    NormalizeDiagnosticMessage(DiagnosticCodes.SnapshotCorrupt, exception.Message),
                    0,
                    null,
                    null);
            }
            catch (IOException exception)
            {
                return CreateInvalidValidity(
                    DiagnosticCodes.SnapshotCorrupt,
                    NormalizeDiagnosticMessage(DiagnosticCodes.SnapshotCorrupt, exception.Message),
                    0,
                    null,
                    null);
            }
            catch (NotSupportedException exception)
            {
                return CreateInvalidValidity(
                    DiagnosticCodes.SnapshotCorrupt,
                    NormalizeDiagnosticMessage(DiagnosticCodes.SnapshotCorrupt, exception.Message),
                    0,
                    null,
                    null);
            }
            catch (UnauthorizedAccessException exception)
            {
                return CreateInvalidValidity(
                    DiagnosticCodes.SnapshotCorrupt,
                    NormalizeDiagnosticMessage(DiagnosticCodes.SnapshotCorrupt, exception.Message),
                    0,
                    null,
                    null);
            }
        }

        private static QmlRegistry BuildRegistry(SnapshotPayload payload, ParsedMetadata metadata)
        {
            SnapshotModule[] moduleModels = payload.Modules
                ?? throw CreateCorruptException("Snapshot payload is missing the modules collection.");
            SnapshotType[] typeModels = payload.Types
                ?? throw CreateCorruptException("Snapshot payload is missing the types collection.");
            SnapshotType[] builtinModels = payload.Builtins
                ?? throw CreateCorruptException("Snapshot payload is missing the builtins collection.");

            List<QmlModule> modules = [];
            HashSet<string> seenModules = new(StringComparer.Ordinal);

            foreach (QmlModule module in moduleModels.Select(static moduleModel => moduleModel.ToModel()))
            {
                if (!seenModules.Add(module.Uri))
                {
                    throw CreateCorruptException($"Duplicate module URI '{module.Uri}' found in snapshot payload.");
                }

                modules.Add(module);
            }

            ImmutableDictionary<string, QmlType>.Builder typesByQualifiedName = ImmutableDictionary.CreateBuilder<string, QmlType>(StringComparer.Ordinal);
            foreach (QmlType duplicateType in typeModels
                .Select(static typeModel => typeModel.ToModel())
                .Where(type => !typesByQualifiedName.TryAdd(type.QualifiedName, type)))
            {
                throw CreateCorruptException($"Duplicate qualified type '{duplicateType.QualifiedName}' found in snapshot payload.");
            }

            List<QmlType> builtins = [];
            HashSet<string> seenBuiltins = new(StringComparer.Ordinal);
            foreach (QmlType builtin in builtinModels.Select(static builtinModel => builtinModel.ToModel()))
            {
                if (!seenBuiltins.Add(builtin.QualifiedName))
                {
                    throw CreateCorruptException($"Duplicate builtin type '{builtin.QualifiedName}' found in snapshot payload.");
                }

                builtins.Add(builtin);
            }

            return new QmlRegistry(
                Modules: modules.ToImmutableArray(),
                TypesByQualifiedName: typesByQualifiedName.ToImmutable(),
                Builtins: builtins.ToImmutableArray(),
                FormatVersion: metadata.FormatVersion,
                QtVersion: metadata.QtVersion,
                BuildTimestamp: metadata.BuildTimestamp)
                .WithLookupIndexes();
        }

        private static SnapshotPayload DeserializePayload(ReadOnlyMemory<byte> payloadBytes)
        {
            SnapshotPayload? payload;

            try
            {
                payload = JsonSerializer.Deserialize<SnapshotPayload>(payloadBytes.Span, JsonOptions);
            }
            catch (JsonException exception)
            {
                throw CreateCorruptException($"Failed to parse snapshot payload JSON: {exception.Message}");
            }
            catch (NotSupportedException exception)
            {
                throw CreateCorruptException($"Unsupported snapshot payload shape: {exception.Message}");
            }

            if (payload is null)
            {
                throw CreateCorruptException("Snapshot payload JSON was empty.");
            }

            if (payload.Modules is null || payload.Types is null || payload.Builtins is null)
            {
                throw CreateCorruptException("Snapshot payload is missing one or more required collections.");
            }

            return payload;
        }

        private static Envelope ReadEnvelope(byte[] data, bool validatePayloadHash, bool throwOnVersionMismatch)
        {
            if (data.Length < Magic.Length + HeaderLengthFieldSize)
            {
                throw CreateCorruptException("Snapshot file is too small to contain a valid header.");
            }

            ReadOnlySpan<byte> dataSpan = data;
            if (!dataSpan[..Magic.Length].SequenceEqual(Magic))
            {
                throw CreateCorruptException("Snapshot file does not contain the expected magic header.");
            }

            int metadataLength = BinaryPrimitives.ReadInt32LittleEndian(dataSpan.Slice(Magic.Length, HeaderLengthFieldSize));
            if (metadataLength <= 0)
            {
                throw CreateCorruptException("Snapshot header metadata length must be greater than zero.");
            }

            int metadataOffset = Magic.Length + HeaderLengthFieldSize;
            if (metadataLength > data.Length - metadataOffset)
            {
                throw CreateCorruptException("Snapshot header metadata extends beyond the end of the file.");
            }

            ParsedMetadata metadata = ParseMetadata(dataSpan.Slice(metadataOffset, metadataLength));
            if (throwOnVersionMismatch && metadata.FormatVersion != SupportedFormatVersion)
            {
                throw CreateVersionMismatchException(metadata.FormatVersion);
            }

            int payloadOffset = metadataOffset + metadataLength;
            int availablePayloadLength = data.Length - payloadOffset;
            if (metadata.PayloadLength != availablePayloadLength)
            {
                throw CreateCorruptException("Snapshot payload length does not match the file size.");
            }

            ReadOnlyMemory<byte> payload = data.AsMemory(payloadOffset, metadata.PayloadLength);
            if (validatePayloadHash)
            {
                string actualHash = ComputeSha256(payload.Span);
                if (!StringComparer.Ordinal.Equals(actualHash, metadata.PayloadSha256))
                {
                    throw CreateCorruptException("Snapshot payload checksum does not match the recorded metadata.");
                }

                string actualEnvelopeHash = ComputeEnvelopeSha256(metadata, payload.Span);
                if (!StringComparer.Ordinal.Equals(actualEnvelopeHash, metadata.EnvelopeSha256))
                {
                    throw CreateCorruptException("Snapshot envelope checksum does not match the recorded metadata.");
                }
            }

            return new Envelope(metadata, payload);
        }

        private static ParsedMetadata ParseMetadata(ReadOnlySpan<byte> metadataBytes)
        {
            SnapshotMetadata? metadata;

            try
            {
                metadata = JsonSerializer.Deserialize<SnapshotMetadata>(metadataBytes, JsonOptions);
            }
            catch (JsonException exception)
            {
                throw CreateCorruptException($"Failed to parse snapshot metadata JSON: {exception.Message}");
            }
            catch (NotSupportedException exception)
            {
                throw CreateCorruptException($"Unsupported snapshot metadata shape: {exception.Message}");
            }

            if (metadata is null)
            {
                throw CreateCorruptException("Snapshot metadata JSON was empty.");
            }

            (string qtVersion, DateTimeOffset buildTimestamp, string payloadSha256, string envelopeSha256) =
                ReadRequiredMetadataFields(metadata);

            return new ParsedMetadata(
                metadata.FormatVersion,
                qtVersion,
                buildTimestamp,
                metadata.PayloadLength,
                payloadSha256,
                PayloadCompression,
                envelopeSha256);
        }

        private static (string QtVersion, DateTimeOffset BuildTimestamp, string PayloadSha256, string EnvelopeSha256)
            ReadRequiredMetadataFields(SnapshotMetadata metadata)
        {
            string qtVersion = ReadRequiredText(metadata.QtVersion, "Snapshot metadata is missing the Qt version.");
            string buildTimestampText = ReadRequiredText(metadata.BuildTimestamp, "Snapshot metadata is missing the build timestamp.");
            if (!DateTimeOffset.TryParseExact(
                buildTimestampText,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTimeOffset buildTimestamp))
            {
                throw CreateCorruptException("Snapshot metadata contains an invalid build timestamp.");
            }

            if (metadata.PayloadLength < 0)
            {
                throw CreateCorruptException("Snapshot metadata contains a negative payload length.");
            }

            string payloadSha256 = ReadSha256(metadata.PayloadSha256, "Snapshot metadata contains an invalid payload checksum.");
            string envelopeSha256 = ReadSha256(metadata.EnvelopeSha256, "Snapshot metadata contains an invalid envelope checksum.");
            if (!string.Equals(metadata.PayloadCompression, PayloadCompression, StringComparison.Ordinal))
            {
                throw CreateCorruptException($"Snapshot metadata contains an unsupported payload compression '{metadata.PayloadCompression}'.");
            }

            return (qtVersion, buildTimestamp, payloadSha256, envelopeSha256);
        }

        private static string ReadRequiredText(string? value, string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw CreateCorruptException(errorMessage);
            }

            return value!;
        }

        private static string ReadSha256(string? value, string errorMessage)
        {
            if (!IsSha256Hex(value))
            {
                throw CreateCorruptException(errorMessage);
            }

            return value!;
        }

        private static SnapshotValidity CreateInvalidValidity(
            string code,
            string message,
            int formatVersion,
            string? qtVersion,
            DateTimeOffset? buildTimestamp)
        {
            return new SnapshotValidity(
                IsValid: false,
                FormatVersion: formatVersion,
                QtVersion: qtVersion,
                BuildTimestamp: buildTimestamp,
                ErrorMessage: BuildMessage(code, message));
        }

        private static InvalidDataException CreateCorruptException(string message)
        {
            return new InvalidDataException(BuildMessage(DiagnosticCodes.SnapshotCorrupt, message));
        }

        private static NotSupportedException CreateVersionMismatchException(int actualFormatVersion)
        {
            return new NotSupportedException(BuildMessage(
                DiagnosticCodes.SnapshotVersionMismatch,
                $"Unsupported snapshot format version {actualFormatVersion}. Expected {SupportedFormatVersion}."));
        }

        private static string BuildMessage(string code, string message)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{code}: {message}");
        }

        private static string NormalizeDiagnosticMessage(string diagnosticCode, string message)
        {
            string prefix = diagnosticCode + ": ";
            return message.StartsWith(prefix, StringComparison.Ordinal)
                ? message[prefix.Length..]
                : message;
        }

        private static string ComputeSha256(ReadOnlySpan<byte> bytes)
        {
            return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        }

        private static byte[] CompressPayload(ReadOnlySpan<byte> payloadBytes)
        {
            using MemoryStream output = new();
            using (BrotliStream compressor = new(output, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                compressor.Write(payloadBytes);
            }

            return output.ToArray();
        }

        private static byte[] DecompressPayload(ReadOnlySpan<byte> payloadBytes)
        {
            try
            {
                using MemoryStream input = new(payloadBytes.ToArray());
                using BrotliStream decompressor = new(input, CompressionMode.Decompress);
                using MemoryStream output = new();
                decompressor.CopyTo(output);
                return output.ToArray();
            }
            catch (InvalidDataException exception)
            {
                throw CreateCorruptException($"Failed to decompress snapshot payload: {exception.Message}");
            }
            catch (IOException exception)
            {
                throw CreateCorruptException($"Failed to decompress snapshot payload: {exception.Message}");
            }
        }

        private static string ComputeEnvelopeSha256(ParsedMetadata metadata, ReadOnlySpan<byte> payloadBytes)
        {
            SnapshotMetadata metadataForHash = new()
            {
                FormatVersion = metadata.FormatVersion,
                QtVersion = metadata.QtVersion,
                BuildTimestamp = metadata.BuildTimestamp.ToString("O", CultureInfo.InvariantCulture),
                PayloadLength = metadata.PayloadLength,
                PayloadSha256 = metadata.PayloadSha256,
                PayloadCompression = metadata.PayloadCompression,
                EnvelopeSha256 = string.Empty,
            };

            return ComputeEnvelopeSha256(metadataForHash, payloadBytes);
        }

        private static string ComputeEnvelopeSha256(SnapshotMetadata metadata, ReadOnlySpan<byte> payloadBytes)
        {
            SnapshotMetadata metadataForHash = metadata with { EnvelopeSha256 = string.Empty };
            byte[] metadataBytes = JsonSerializer.SerializeToUtf8Bytes(metadataForHash, JsonOptions);

            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            hash.AppendData(metadataBytes);
            hash.AppendData(payloadBytes);
            return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }

        private static bool IsSha256Hex(string? value)
        {
            return value is { Length: 64 }
                && value.All(static character =>
                    character is >= '0' and <= '9'
                    || character is >= 'a' and <= 'f'
                    || character is >= 'A' and <= 'F');
        }

        private static void EnsureFilePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path must not be null, empty, or whitespace.", nameof(filePath));
            }
        }

        private sealed record Envelope(ParsedMetadata Metadata, ReadOnlyMemory<byte> Payload);

        private sealed record ParsedMetadata(
            int FormatVersion,
            string QtVersion,
            DateTimeOffset BuildTimestamp,
            int PayloadLength,
            string PayloadSha256,
            string PayloadCompression,
            string EnvelopeSha256);

        private sealed record SnapshotMetadata
        {
            public int FormatVersion { get; init; }

            public string? QtVersion { get; init; }

            public string? BuildTimestamp { get; init; }

            public int PayloadLength { get; init; }

            public string? PayloadSha256 { get; init; }

            public string? PayloadCompression { get; init; }

            public string? EnvelopeSha256 { get; init; }
        }

        private sealed class SnapshotPayload
        {
            public SnapshotModule[]? Modules { get; init; }

            public SnapshotType[]? Types { get; init; }

            public SnapshotType[]? Builtins { get; init; }

            public static SnapshotPayload FromRegistry(QmlRegistry registry)
            {
                return new SnapshotPayload
                {
                    Modules = registry.Modules
                        .OrderBy(module => module.Uri, StringComparer.Ordinal)
                        .Select(SnapshotModule.FromModel)
                        .ToArray(),
                    Types = registry.TypesByQualifiedName.Values
                        .OrderBy(type => type.QualifiedName, StringComparer.Ordinal)
                        .Select(SnapshotType.FromModel)
                        .ToArray(),
                    Builtins = registry.Builtins
                        .OrderBy(type => type.QualifiedName, StringComparer.Ordinal)
                        .Select(SnapshotType.FromModel)
                        .ToArray(),
                };
            }
        }

        private sealed class SnapshotModule
        {
            public string? Uri { get; init; }

            public SnapshotVersion? Version { get; init; }

            public string[]? Dependencies { get; init; }

            public string[]? Imports { get; init; }

            public SnapshotModuleType[]? Types { get; init; }

            public static SnapshotModule FromModel(QmlModule module)
            {
                return new SnapshotModule
                {
                    Uri = module.Uri,
                    Version = SnapshotVersion.FromModel(module.Version),
                    Dependencies = module.Dependencies.ToArray(),
                    Imports = module.Imports.ToArray(),
                    Types = module.Types.Select(SnapshotModuleType.FromModel).ToArray(),
                };
            }

            public QmlModule ToModel()
            {
                if (string.IsNullOrWhiteSpace(Uri))
                {
                    throw CreateCorruptException("Snapshot module is missing its URI.");
                }

                if (Version is null || Dependencies is null || Imports is null || Types is null)
                {
                    throw CreateCorruptException($"Snapshot module '{Uri}' is missing one or more required fields.");
                }

                return new QmlModule(
                    Uri,
                    Version.ToModel(),
                    Dependencies.ToImmutableArray(),
                    Imports.ToImmutableArray(),
                    Types.Select(type => type.ToModel()).ToImmutableArray());
            }
        }

        private sealed class SnapshotModuleType
        {
            public string? QualifiedName { get; init; }

            public string? QmlName { get; init; }

            public SnapshotVersion? ExportVersion { get; init; }

            public static SnapshotModuleType FromModel(QmlModuleType type)
            {
                return new SnapshotModuleType
                {
                    QualifiedName = type.QualifiedName,
                    QmlName = type.QmlName,
                    ExportVersion = SnapshotVersion.FromModel(type.ExportVersion),
                };
            }

            public QmlModuleType ToModel()
            {
                if (string.IsNullOrWhiteSpace(QualifiedName) || string.IsNullOrWhiteSpace(QmlName) || ExportVersion is null)
                {
                    throw CreateCorruptException("Snapshot module type is missing one or more required fields.");
                }

                return new QmlModuleType(QualifiedName, QmlName, ExportVersion.ToModel());
            }
        }

        private sealed class SnapshotType
        {
            public string? QualifiedName { get; init; }

            public string? QmlName { get; init; }

            public string? ModuleUri { get; init; }

            public AccessSemantics AccessSemantics { get; init; }

            public string? Prototype { get; init; }

            public string? DefaultProperty { get; init; }

            public string? AttachedType { get; init; }

            public string? Extension { get; init; }

            public bool IsSingleton { get; init; }

            public bool IsCreatable { get; init; }

            public SnapshotTypeExport[]? Exports { get; init; }

            public SnapshotProperty[]? Properties { get; init; }

            public SnapshotSignal[]? Signals { get; init; }

            public SnapshotMethod[]? Methods { get; init; }

            public SnapshotEnum[]? Enums { get; init; }

            public string[]? Interfaces { get; init; }

            public static SnapshotType FromModel(QmlType type)
            {
                return new SnapshotType
                {
                    QualifiedName = type.QualifiedName,
                    QmlName = type.QmlName,
                    ModuleUri = type.ModuleUri,
                    AccessSemantics = type.AccessSemantics,
                    Prototype = type.Prototype,
                    DefaultProperty = type.DefaultProperty,
                    AttachedType = type.AttachedType,
                    Extension = type.Extension,
                    IsSingleton = type.IsSingleton,
                    IsCreatable = type.IsCreatable,
                    Exports = type.Exports.Select(SnapshotTypeExport.FromModel).ToArray(),
                    Properties = type.Properties.Select(SnapshotProperty.FromModel).ToArray(),
                    Signals = type.Signals.Select(SnapshotSignal.FromModel).ToArray(),
                    Methods = type.Methods.Select(SnapshotMethod.FromModel).ToArray(),
                    Enums = type.Enums.Select(SnapshotEnum.FromModel).ToArray(),
                    Interfaces = type.Interfaces.ToArray(),
                };
            }

            public QmlType ToModel()
            {
                if (string.IsNullOrWhiteSpace(QualifiedName))
                {
                    throw CreateCorruptException("Snapshot type is missing its qualified name.");
                }

                if (!Enum.IsDefined(AccessSemantics))
                {
                    throw CreateCorruptException($"Snapshot type '{QualifiedName}' contains an invalid access semantics value.");
                }

                if (Exports is null || Properties is null || Signals is null || Methods is null || Enums is null || Interfaces is null)
                {
                    throw CreateCorruptException($"Snapshot type '{QualifiedName}' is missing one or more required collections.");
                }

                return new QmlType(
                    QualifiedName,
                    QmlName,
                    ModuleUri,
                    AccessSemantics,
                    Prototype,
                    DefaultProperty,
                    AttachedType,
                    Extension,
                    IsSingleton,
                    IsCreatable,
                    Exports.Select(export => export.ToModel()).ToImmutableArray(),
                    Properties.Select(property => property.ToModel()).ToImmutableArray(),
                    Signals.Select(signal => signal.ToModel()).ToImmutableArray(),
                    Methods.Select(method => method.ToModel()).ToImmutableArray(),
                    Enums.Select(@enum => @enum.ToModel()).ToImmutableArray(),
                    Interfaces.ToImmutableArray());
            }
        }

        private sealed class SnapshotTypeExport
        {
            public string? Module { get; init; }

            public string? Name { get; init; }

            public SnapshotVersion? Version { get; init; }

            public static SnapshotTypeExport FromModel(QmlTypeExport export)
            {
                return new SnapshotTypeExport
                {
                    Module = export.Module,
                    Name = export.Name,
                    Version = SnapshotVersion.FromModel(export.Version),
                };
            }

            public QmlTypeExport ToModel()
            {
                if (string.IsNullOrWhiteSpace(Module) || string.IsNullOrWhiteSpace(Name) || Version is null)
                {
                    throw CreateCorruptException("Snapshot type export is missing one or more required fields.");
                }

                return new QmlTypeExport(Module, Name, Version.ToModel());
            }
        }

        private sealed class SnapshotProperty
        {
            public string? Name { get; init; }

            public string? TypeName { get; init; }

            public bool IsReadonly { get; init; }

            public bool IsList { get; init; }

            public bool IsRequired { get; init; }

            public string? DefaultValue { get; init; }

            public string? NotifySignal { get; init; }

            public static SnapshotProperty FromModel(QmlProperty property)
            {
                return new SnapshotProperty
                {
                    Name = property.Name,
                    TypeName = property.TypeName,
                    IsReadonly = property.IsReadonly,
                    IsList = property.IsList,
                    IsRequired = property.IsRequired,
                    DefaultValue = property.DefaultValue,
                    NotifySignal = property.NotifySignal,
                };
            }

            public QmlProperty ToModel()
            {
                if (Name is null || TypeName is null)
                {
                    throw CreateCorruptException("Snapshot property is missing one or more required fields.");
                }

                return new QmlProperty(Name, TypeName, IsReadonly, IsList, IsRequired, DefaultValue, NotifySignal);
            }
        }

        private sealed class SnapshotSignal
        {
            public string? Name { get; init; }

            public SnapshotParameter[]? Parameters { get; init; }

            public static SnapshotSignal FromModel(QmlSignal signal)
            {
                return new SnapshotSignal
                {
                    Name = signal.Name,
                    Parameters = signal.Parameters.Select(SnapshotParameter.FromModel).ToArray(),
                };
            }

            public QmlSignal ToModel()
            {
                if (Name is null || Parameters is null)
                {
                    throw CreateCorruptException("Snapshot signal is missing one or more required fields.");
                }

                return new QmlSignal(Name, Parameters.Select(parameter => parameter.ToModel()).ToImmutableArray());
            }
        }

        private sealed class SnapshotMethod
        {
            public string? Name { get; init; }

            public string? ReturnType { get; init; }

            public SnapshotParameter[]? Parameters { get; init; }

            public static SnapshotMethod FromModel(QmlMethod method)
            {
                return new SnapshotMethod
                {
                    Name = method.Name,
                    ReturnType = method.ReturnType,
                    Parameters = method.Parameters.Select(SnapshotParameter.FromModel).ToArray(),
                };
            }

            public QmlMethod ToModel()
            {
                if (Name is null || Parameters is null)
                {
                    throw CreateCorruptException("Snapshot method is missing one or more required fields.");
                }

                return new QmlMethod(Name, ReturnType, Parameters.Select(parameter => parameter.ToModel()).ToImmutableArray());
            }
        }

        private sealed class SnapshotParameter
        {
            public string? Name { get; init; }

            public string? TypeName { get; init; }

            public static SnapshotParameter FromModel(QmlParameter parameter)
            {
                return new SnapshotParameter
                {
                    Name = parameter.Name,
                    TypeName = parameter.TypeName,
                };
            }

            public QmlParameter ToModel()
            {
                if (Name is null || TypeName is null)
                {
                    throw CreateCorruptException("Snapshot parameter is missing one or more required fields.");
                }

                return new QmlParameter(Name, TypeName);
            }
        }

        private sealed class SnapshotEnum
        {
            public string? Name { get; init; }

            public bool IsFlag { get; init; }

            public SnapshotEnumValue[]? Values { get; init; }

            public static SnapshotEnum FromModel(QmlEnum @enum)
            {
                return new SnapshotEnum
                {
                    Name = @enum.Name,
                    IsFlag = @enum.IsFlag,
                    Values = @enum.Values.Select(SnapshotEnumValue.FromModel).ToArray(),
                };
            }

            public QmlEnum ToModel()
            {
                if (Name is null || Values is null)
                {
                    throw CreateCorruptException("Snapshot enum is missing one or more required fields.");
                }

                return new QmlEnum(Name, IsFlag, Values.Select(value => value.ToModel()).ToImmutableArray());
            }
        }

        private sealed class SnapshotEnumValue
        {
            public string? Name { get; init; }

            public int? Value { get; init; }

            public static SnapshotEnumValue FromModel(QmlEnumValue value)
            {
                return new SnapshotEnumValue
                {
                    Name = value.Name,
                    Value = value.Value,
                };
            }

            public QmlEnumValue ToModel()
            {
                if (Name is null)
                {
                    throw CreateCorruptException("Snapshot enum value is missing its name.");
                }

                return new QmlEnumValue(Name, Value);
            }
        }

        private sealed class SnapshotVersion
        {
            public int Major { get; init; }

            public int Minor { get; init; }

            public static SnapshotVersion FromModel(QmlVersion version)
            {
                return new SnapshotVersion
                {
                    Major = version.Major,
                    Minor = version.Minor,
                };
            }

            public QmlVersion ToModel()
            {
                return new QmlVersion(Major, Minor);
            }
        }
    }
}

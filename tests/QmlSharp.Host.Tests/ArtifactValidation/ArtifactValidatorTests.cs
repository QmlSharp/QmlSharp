using System.Text.Json;
using System.Text.Json.Nodes;
using QmlSharp.Host.ArtifactValidation;
using QmlSharp.Host.Interop;

namespace QmlSharp.Host.Tests.ArtifactValidation
{
    public sealed class ArtifactValidatorTests
    {
        [Fact]
        public void Validate_ValidDistWithRootQml_IsValid()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            FakeAbiVersionReader abiVersionReader = new();
            ArtifactValidator validator = new(abiVersionReader);

            ArtifactValidationResult result = validator.Validate(new ArtifactValidationRequest(
                fixture.Path,
                System.IO.Path.Join("qml", "QmlSharp", "TestApp", "CounterView.qml")));

            Assert.True(result.IsValid);
            Assert.Empty(result.Diagnostics);
            Assert.Equal(1, abiVersionReader.Calls);
            Assert.EndsWith("qmlsharp_native.dll", abiVersionReader.LastPath, StringComparison.Ordinal);
        }

        [Fact]
        public void Validate_MissingManifest_EmitsAv001Error()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            File.Delete(fixture.InDist("manifest.json"));

            ArtifactValidationResult result = CreateValidator().Validate(fixture.Path);

            ArtifactDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.False(result.IsValid);
            AssertDiagnostic(diagnostic, ArtifactValidationCodes.ManifestMissing, ArtifactDiagnosticSeverity.Error, fixture.InDist("manifest.json"));
        }

        [Fact]
        public void Validate_MalformedManifest_EmitsAv001WithFilePath()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            File.WriteAllText(fixture.InDist("manifest.json"), "{ not-json");

            ArtifactValidationResult result = CreateValidator().Validate(fixture.Path);

            ArtifactDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.False(result.IsValid);
            AssertDiagnostic(diagnostic, ArtifactValidationCodes.ManifestMissing, ArtifactDiagnosticSeverity.Error, fixture.InDist("manifest.json"));
            Assert.Contains("valid JSON", diagnostic.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Validate_MissingNativeLibrary_EmitsAv002ErrorAndDoesNotReadAbi()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            File.Delete(fixture.InDist("native", "qmlsharp_native.dll"));
            FakeAbiVersionReader abiVersionReader = new();

            ArtifactValidationResult result = new ArtifactValidator(abiVersionReader).Validate(fixture.Path);

            ArtifactDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.False(result.IsValid);
            Assert.Equal(0, abiVersionReader.Calls);
            AssertDiagnostic(diagnostic, ArtifactValidationCodes.NativeLibraryMissing, ArtifactDiagnosticSeverity.Error, fixture.InDist("native", "qmlsharp_native.dll"));
        }

        [Fact]
        public void Validate_NativeLibraryPathOutsideDist_EmitsAv002ErrorAndDoesNotReadAbi()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            string outsideNativeLibrary = System.IO.Path.Join(
                System.IO.Path.GetTempPath(),
                "qmlsharp-native-outside-" + Guid.NewGuid().ToString("N") + ".dll");
            File.WriteAllText(outsideNativeLibrary, "not a real native library");
            FakeAbiVersionReader abiVersionReader = new();

            try
            {
                WriteManifestProperty(fixture, "nativeLib", outsideNativeLibrary);

                ArtifactValidationResult result = new ArtifactValidator(abiVersionReader).Validate(fixture.Path);

                ArtifactDiagnostic diagnostic = Assert.Single(result.Diagnostics, DiagnosticWithCode(ArtifactValidationCodes.NativeLibraryMissing));
                Assert.False(result.IsValid);
                Assert.Equal(0, abiVersionReader.Calls);
                AssertDiagnostic(diagnostic, ArtifactValidationCodes.NativeLibraryMissing, ArtifactDiagnosticSeverity.Error, outsideNativeLibrary);
                Assert.Contains("dist directory", diagnostic.Message, StringComparison.Ordinal);
            }
            finally
            {
                File.Delete(outsideNativeLibrary);
            }
        }

        [Fact]
        public void Validate_ManagedAssemblyPathOutsideDist_EmitsAv001Error()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            string outsideManagedAssembly = System.IO.Path.Join(
                System.IO.Path.GetTempPath(),
                "qmlsharp-managed-outside-" + Guid.NewGuid().ToString("N") + ".dll");
            File.WriteAllText(outsideManagedAssembly, "not a real managed assembly");

            try
            {
                WriteManifestProperty(fixture, "managedAssembly", outsideManagedAssembly);

                ArtifactValidationResult result = CreateValidator().Validate(fixture.Path);

                ArtifactDiagnostic diagnostic = Assert.Single(result.Diagnostics, DiagnosticWithCode(ArtifactValidationCodes.ManifestMissing));
                Assert.False(result.IsValid);
                AssertDiagnostic(diagnostic, ArtifactValidationCodes.ManifestMissing, ArtifactDiagnosticSeverity.Error, outsideManagedAssembly);
                Assert.Contains("dist directory", diagnostic.Message, StringComparison.Ordinal);
            }
            finally
            {
                File.Delete(outsideManagedAssembly);
            }
        }

        [Fact]
        public void Validate_IncompatibleAbi_EmitsAv003Error()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            ArtifactValidator validator = new(new FakeAbiVersionReader(NativeHostAbi.SupportedAbiVersion + 1));

            ArtifactValidationResult result = validator.Validate(fixture.Path);

            ArtifactDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.False(result.IsValid);
            AssertDiagnostic(diagnostic, ArtifactValidationCodes.AbiVersionMismatch, ArtifactDiagnosticSeverity.Error, fixture.InDist("native", "qmlsharp_native.dll"));
        }

        [Fact]
        public void Validate_MissingSchemaForManifestViewModel_EmitsAv004Error()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            File.Delete(fixture.InDist("schemas", "CounterViewModel.schema.json"));

            ArtifactValidationResult result = CreateValidator().Validate(fixture.Path);

            ArtifactDiagnostic diagnostic = result.Diagnostics.First(diagnostic =>
                diagnostic.Code == ArtifactValidationCodes.SchemaMissing &&
                diagnostic.FilePath == System.IO.Path.GetFullPath(fixture.InDist("schemas", "CounterViewModel.schema.json")));
            Assert.False(result.IsValid);
            AssertDiagnostic(diagnostic, ArtifactValidationCodes.SchemaMissing, ArtifactDiagnosticSeverity.Error, fixture.InDist("schemas", "CounterViewModel.schema.json"));
        }

        [Fact]
        public void Validate_MalformedSchemaJson_EmitsAv004WithFilePath()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            string schemaPath = fixture.InDist("schemas", "CounterViewModel.schema.json");
            File.WriteAllText(schemaPath, "{ not-json");

            ArtifactValidationResult result = CreateValidator().Validate(fixture.Path);

            ArtifactDiagnostic diagnostic = result.Diagnostics.First(diagnostic =>
                diagnostic.Code == ArtifactValidationCodes.SchemaMissing &&
                diagnostic.FilePath == System.IO.Path.GetFullPath(schemaPath));
            Assert.False(result.IsValid);
            AssertDiagnostic(diagnostic, ArtifactValidationCodes.SchemaMissing, ArtifactDiagnosticSeverity.Error, schemaPath);
            Assert.Contains("valid JSON", diagnostic.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Validate_EmptySchemaDirectory_EmitsAv004Error()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            foreach (string schemaFile in Directory.GetFiles(fixture.InDist("schemas"), "*.schema.json"))
            {
                File.Delete(schemaFile);
            }

            ArtifactValidationResult result = CreateValidator().Validate(fixture.Path);

            ArtifactDiagnostic diagnostic = result.Diagnostics.First(diagnostic =>
                diagnostic.Code == ArtifactValidationCodes.SchemaMissing &&
                diagnostic.FilePath == System.IO.Path.GetFullPath(fixture.InDist("schemas")));
            Assert.False(result.IsValid);
            AssertDiagnostic(diagnostic, ArtifactValidationCodes.SchemaMissing, ArtifactDiagnosticSeverity.Error, fixture.InDist("schemas"));
        }

        [Fact]
        public void Validate_DuplicateSchemaIds_EmitsAv004Error()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            File.Copy(
                fixture.InDist("schemas", "CounterViewModel.schema.json"),
                fixture.InDist("schemas", "DuplicateCounter.schema.json"));

            ArtifactValidationResult result = CreateValidator().Validate(fixture.Path);

            ArtifactDiagnostic diagnostic = Assert.Single(result.Diagnostics, DiagnosticWithCode(ArtifactValidationCodes.SchemaMissing));
            Assert.False(result.IsValid);
            Assert.Contains("Duplicate schema id", diagnostic.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Validate_MissingEventBindings_EmitsAv005WarningAndRemainsValid()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            File.Delete(fixture.InDist("event-bindings.json"));

            ArtifactValidationResult result = CreateValidator().Validate(fixture.Path);

            ArtifactDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.True(result.IsValid);
            AssertDiagnostic(diagnostic, ArtifactValidationCodes.EventBindingsMissing, ArtifactDiagnosticSeverity.Warning, fixture.InDist("event-bindings.json"));
        }

        [Fact]
        public void Validate_EventBindingsCommandWithoutSchemaMatch_EmitsAv006WarningAndRemainsValid()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            File.WriteAllText(
                fixture.InDist("event-bindings.json"),
                """
                {
                  "schemaVersion": "1.0",
                  "commands": [
                    {
                      "viewModelClass": "CounterViewModel",
                      "commandName": "increment",
                      "commandId": 123,
                      "parameterTypes": []
                    }
                  ],
                  "effects": []
                }
                """);

            ArtifactValidationResult result = CreateValidator().Validate(fixture.Path);

            ArtifactDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.True(result.IsValid);
            AssertDiagnostic(diagnostic, ArtifactValidationCodes.EventBindingCommandMissing, ArtifactDiagnosticSeverity.Warning, fixture.InDist("event-bindings.json"));
        }

        [Fact]
        public void Validate_EventBindingsCommandWithWrongParameterTypes_EmitsAv006Warning()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            File.WriteAllText(
                fixture.InDist("event-bindings.json"),
                """
                {
                  "schemaVersion": "1.0",
                  "commands": [
                    {
                      "viewModelClass": "TodoViewModel",
                      "commandName": "removeItem",
                      "commandId": 1743999692,
                      "parameterTypes": [
                        "string"
                      ]
                    }
                  ],
                  "effects": []
                }
                """);

            ArtifactValidationResult result = CreateValidator().Validate(fixture.Path);

            ArtifactDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.True(result.IsValid);
            AssertDiagnostic(diagnostic, ArtifactValidationCodes.EventBindingCommandMissing, ArtifactDiagnosticSeverity.Warning, fixture.InDist("event-bindings.json"));
        }

        [Fact]
        public void Validate_MalformedEventBindings_EmitsAv005WarningWithFilePath()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            File.WriteAllText(fixture.InDist("event-bindings.json"), "{ not-json");

            ArtifactValidationResult result = CreateValidator().Validate(fixture.Path);

            ArtifactDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.True(result.IsValid);
            AssertDiagnostic(diagnostic, ArtifactValidationCodes.EventBindingsMissing, ArtifactDiagnosticSeverity.Warning, fixture.InDist("event-bindings.json"));
            Assert.Contains("invalid", diagnostic.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Validate_MissingRootQml_EmitsAv004Error()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            string rootQml = System.IO.Path.Join("qml", "QmlSharp", "TestApp", "Missing.qml");

            ArtifactValidationResult result = CreateValidator().Validate(new ArtifactValidationRequest(fixture.Path, rootQml));

            ArtifactDiagnostic diagnostic = Assert.Single(result.Diagnostics, DiagnosticWithCode(ArtifactValidationCodes.SchemaMissing));
            Assert.False(result.IsValid);
            AssertDiagnostic(diagnostic, ArtifactValidationCodes.SchemaMissing, ArtifactDiagnosticSeverity.Error, fixture.InDist("qml", "QmlSharp", "TestApp", "Missing.qml"));
        }

        [Fact]
        public void ValidateSchemaRegistrationResult_NativeFailure_EmitsAv007Error()
        {
            ArtifactValidator validator = CreateValidator();
            string schemaPath = System.IO.Path.Join(System.IO.Path.GetTempPath(), "CounterViewModel.schema.json");

            ArtifactValidationResult result = validator.ValidateSchemaRegistrationResult(schemaPath, -6, "duplicate type");

            ArtifactDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.False(result.IsValid);
            AssertDiagnostic(diagnostic, ArtifactValidationCodes.SchemaRegistrationFailed, ArtifactDiagnosticSeverity.Error, schemaPath);
            Assert.Contains("duplicate type", diagnostic.Message, StringComparison.Ordinal);
        }

        private static ArtifactValidator CreateValidator()
        {
            return new ArtifactValidator(new FakeAbiVersionReader());
        }

        private static Predicate<ArtifactDiagnostic> DiagnosticWithCode(string code)
        {
            return diagnostic => StringComparer.Ordinal.Equals(diagnostic.Code, code);
        }

        private static void AssertDiagnostic(ArtifactDiagnostic diagnostic, string code, ArtifactDiagnosticSeverity severity, string? filePath)
        {
            Assert.Equal(code, diagnostic.Code);
            Assert.Equal(severity, diagnostic.Severity);
            Assert.Equal(string.IsNullOrWhiteSpace(filePath) ? null : System.IO.Path.GetFullPath(filePath), diagnostic.FilePath);
        }

        private static void WriteManifestProperty(ArtifactFixture fixture, string propertyName, string value)
        {
            string manifestPath = fixture.InDist("manifest.json");
            JsonObject manifest = JsonNode.Parse(File.ReadAllText(manifestPath))?.AsObject()
                ?? throw new InvalidOperationException("Fixture manifest must be a JSON object.");
            manifest[propertyName] = value;
            File.WriteAllText(manifestPath, manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}

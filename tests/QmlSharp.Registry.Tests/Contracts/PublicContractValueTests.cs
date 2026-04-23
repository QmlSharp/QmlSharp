using QmlSharp.Registry.Diagnostics;
using QmlSharp.Registry.Scanning;
using QmlSharp.Registry.Tests.Helpers;

namespace QmlSharp.Registry.Tests.Contracts
{
    public sealed class PublicContractValueTests
    {
        [Fact]
        public void QmlVersion_value_equality_uses_constructor_values()
        {
            QmlVersion left = new QmlVersion(2, 15);
            QmlVersion right = new QmlVersion(2, 15);

            Assert.Equal(left, right);
            Assert.Equal("2.15", left.ToString());
        }

        [Fact]
        public void RegistryDiagnostic_value_equality_tracks_location_and_code()
        {
            RegistryDiagnostic left = new RegistryDiagnostic(
                DiagnosticSeverity.Error,
                DiagnosticCodes.QmltypesSyntaxError,
                "Unexpected token.",
                @"fixtures\qmltypes\minimal.qmltypes",
                12,
                5);
            RegistryDiagnostic right = new RegistryDiagnostic(
                DiagnosticSeverity.Error,
                DiagnosticCodes.QmltypesSyntaxError,
                "Unexpected token.",
                @"fixtures\qmltypes\minimal.qmltypes",
                12,
                5);

            Assert.Equal(left, right);
        }

        [Fact]
        public void QmlProperty_value_equality_uses_constructor_values()
        {
            QmlProperty left = RegistryFixtures.CreateProperty();
            QmlProperty right = RegistryFixtures.CreateProperty();

            Assert.Equal(left, right);
        }

        [Fact]
        public void ParseResult_success_requires_a_value_and_no_error_diagnostics()
        {
            ParseResult<string> success = new ParseResult<string>(
                Value: "ok",
                Diagnostics:
                [
                    new RegistryDiagnostic(DiagnosticSeverity.Warning, DiagnosticCodes.TypeConflict, "warning", null, null, null),
                ]);
            ParseResult<string> failure = new ParseResult<string>(
                Value: "still-built",
                Diagnostics:
                [
                    new RegistryDiagnostic(DiagnosticSeverity.Error, DiagnosticCodes.QmltypesSyntaxError, "error", null, null, null),
                ]);
            ParseResult<string> empty = new ParseResult<string>(
                Value: null,
                Diagnostics: ImmutableArray<RegistryDiagnostic>.Empty);
            ParseResult<string> defaultDiagnostics = new ParseResult<string>(
                Value: "ok",
                Diagnostics: default);

            Assert.True(success.IsSuccess);
            Assert.False(failure.IsSuccess);
            Assert.False(empty.IsSuccess);
            Assert.True(defaultDiagnostics.IsSuccess);
        }

        [Fact]
        public void NormalizeResult_success_treats_default_diagnostics_as_empty()
        {
            QmlRegistry registry = RegistryFixtures.CreateMinimalInheritanceFixture();
            NormalizeResult result = new NormalizeResult(Registry: registry, Diagnostics: default);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public void BuildResult_success_requires_a_registry_query_and_no_error_diagnostics()
        {
            QmlRegistry registry = RegistryFixtures.CreateMinimalInheritanceFixture();
            BuildResult success = new BuildResult(
                TypeRegistry: new StubTypeRegistry(registry),
                Query: new StubRegistryQuery(registry),
                Diagnostics: ImmutableArray<RegistryDiagnostic>.Empty);
            BuildResult successWithDefaultDiagnostics = new BuildResult(
                TypeRegistry: new StubTypeRegistry(registry),
                Query: new StubRegistryQuery(registry),
                Diagnostics: default);
            BuildResult failure = new BuildResult(
                TypeRegistry: new StubTypeRegistry(registry),
                Query: new StubRegistryQuery(registry),
                Diagnostics:
                [
                    new RegistryDiagnostic(DiagnosticSeverity.Error, DiagnosticCodes.InvalidQtDir, "bad qt", null, null, null),
                ]);
            BuildResult missingQuery = new BuildResult(
                TypeRegistry: new StubTypeRegistry(registry),
                Query: null,
                Diagnostics: ImmutableArray<RegistryDiagnostic>.Empty);

            Assert.True(success.IsSuccess);
            Assert.True(successWithDefaultDiagnostics.IsSuccess);
            Assert.False(failure.IsSuccess);
            Assert.False(missingQuery.IsSuccess);
        }

        [Fact]
        public void Representative_required_reference_members_are_non_nullable()
        {
            PropertyNullabilityExpectation[] expectations =
            [
                ExpectProperty(typeof(QmlRegistry), nameof(QmlRegistry.QtVersion)),
                ExpectProperty(typeof(QmlModule), nameof(QmlModule.Uri)),
                ExpectProperty(typeof(QmlType), nameof(QmlType.QualifiedName)),
                ExpectProperty(typeof(QmlProperty), nameof(QmlProperty.Name)),
                ExpectProperty(typeof(QmlSignal), nameof(QmlSignal.Name)),
                ExpectProperty(typeof(QmlMethod), nameof(QmlMethod.Name)),
                ExpectProperty(typeof(QmlParameter), nameof(QmlParameter.Name)),
                ExpectProperty(typeof(QmlEnum), nameof(QmlEnum.Name)),
                ExpectProperty(typeof(QmlEnumValue), nameof(QmlEnumValue.Name)),
                ExpectProperty(typeof(RegistryDiagnostic), nameof(RegistryDiagnostic.Code)),
                ExpectProperty(typeof(RegistryDiagnostic), nameof(RegistryDiagnostic.Message)),
                ExpectProperty(typeof(ScannerConfig), nameof(ScannerConfig.QtDir)),
                ExpectProperty(typeof(BuildConfig), nameof(BuildConfig.QtDir)),
                ExpectProperty(typeof(BuildProgress), nameof(BuildProgress.Detail), expected: NullabilityState.Nullable),
                ExpectProperty(typeof(ScanValidation), nameof(ScanValidation.ErrorMessage), expected: NullabilityState.Nullable),
                ExpectProperty(typeof(SnapshotValidity), nameof(SnapshotValidity.ErrorMessage), expected: NullabilityState.Nullable),
            ];

            Assert.All(expectations, expectation => expectation.AssertMatches());
        }

        private static PropertyNullabilityExpectation ExpectProperty(Type type, string propertyName, NullabilityState expected = NullabilityState.NotNull)
        {
            return new PropertyNullabilityExpectation(type, propertyName, expected);
        }

        private sealed class PropertyNullabilityExpectation
        {
            private static readonly NullabilityInfoContext Context = new();

            public PropertyNullabilityExpectation(Type type, string propertyName, NullabilityState expected)
            {
                Type = type;
                PropertyName = propertyName;
                Expected = expected;
            }

            public Type Type { get; }

            public string PropertyName { get; }

            public NullabilityState Expected { get; }

            public void AssertMatches()
            {
                PropertyInfo? property = Type.GetProperty(PropertyName, BindingFlags.Instance | BindingFlags.Public);
                Assert.NotNull(property);

                NullabilityInfo nullability = Context.Create(property!);
                Assert.Equal(Expected, nullability.ReadState);
            }
        }
    }
}

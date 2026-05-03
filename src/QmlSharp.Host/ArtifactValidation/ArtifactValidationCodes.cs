namespace QmlSharp.Host.ArtifactValidation
{
    /// <summary>Stable startup artifact validation diagnostic codes.</summary>
    public static class ArtifactValidationCodes
    {
        /// <summary>The startup manifest is missing or structurally invalid.</summary>
        public const string ManifestMissing = "AV-001";

        /// <summary>The manifest-referenced native library is missing.</summary>
        public const string NativeLibraryMissing = "AV-002";

        /// <summary>The native ABI version does not match the managed host.</summary>
        public const string AbiVersionMismatch = "AV-003";

        /// <summary>A required schema or convention-driven runtime artifact is missing or invalid.</summary>
        public const string SchemaMissing = "AV-004";

        /// <summary>The optional event-bindings artifact is missing or invalid.</summary>
        public const string EventBindingsMissing = "AV-005";

        /// <summary>An event-bindings command entry does not match schema command metadata.</summary>
        public const string EventBindingCommandMissing = "AV-006";

        /// <summary>Native schema registration failed.</summary>
        public const string SchemaRegistrationFailed = "AV-007";
    }
}

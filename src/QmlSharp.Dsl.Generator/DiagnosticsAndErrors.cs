#pragma warning disable MA0048

namespace QmlSharp.Dsl.Generator
{
    /// <summary>Diagnostic code constants for the DSL generator.</summary>
    public static class DslDiagnosticCodes
    {
        public const string UnresolvedBaseType = "DSL001";
        public const string CircularInheritance = "DSL002";
        public const string MaxDepthExceeded = "DSL003";
        public const string UnmappedQmlType = "DSL010";
        public const string AmbiguousTypeMapping = "DSL011";
        public const string UnsupportedPropertyType = "DSL020";
        public const string GroupedPropertyConflict = "DSL021";
        public const string UnsupportedSignalParameter = "DSL030";
        public const string UnsupportedMethodSignature = "DSL040";
        public const string MethodPropertyNameCollision = "DSL041";
        public const string DuplicateEnumMember = "DSL050";
        public const string EnumNameCollision = "DSL051";
        public const string UnresolvedAttachedType = "DSL060";
        public const string ReservedWordCollision = "DSL070";
        public const string TypeNameCollision = "DSL071";
        public const string CrossModuleNameCollision = "DSL072";
        public const string EmitFailure = "DSL080";
        public const string EmptyModule = "DSL090";
        public const string MissingDependency = "DSL091";
        public const string SkippedType = "DSL100";
        public const string DeprecatedType = "DSL101";
    }

    /// <summary>Base exception for DSL generation errors.</summary>
    public class DslGenerationException : Exception
    {
        public DslGenerationException(
            string message,
            string diagnosticCode,
            string? typeName = null,
            string? moduleUri = null,
            Exception? inner = null)
            : base(message, inner)
        {
            DiagnosticCode = diagnosticCode;
            TypeName = typeName;
            ModuleUri = moduleUri;
        }

        public string? TypeName { get; }

        public string? ModuleUri { get; }

        public string DiagnosticCode { get; }
    }

    /// <summary>Thrown when a type cannot be resolved.</summary>
    public sealed class TypeResolutionException : DslGenerationException
    {
        public TypeResolutionException(string typeName, string unresolvedBase)
            : base(
                $"Cannot resolve base type '{unresolvedBase}' for '{typeName}'",
                DslDiagnosticCodes.UnresolvedBaseType,
                typeName)
        {
            UnresolvedTypeName = unresolvedBase;
        }

        public string UnresolvedTypeName { get; }
    }

    /// <summary>Thrown when a circular inheritance chain is detected.</summary>
    public sealed class CircularInheritanceException : DslGenerationException
    {
        public CircularInheritanceException(ImmutableArray<string> chain)
            : base(
                $"Circular inheritance detected: {string.Join(" -> ", chain)}",
                DslDiagnosticCodes.CircularInheritance)
        {
            Chain = chain;
        }

        public ImmutableArray<string> Chain { get; }
    }

    /// <summary>Thrown when a name collision cannot be automatically resolved.</summary>
    public sealed class NameCollisionException : DslGenerationException
    {
        public NameCollisionException(string name, string existingOwner, string newOwner)
            : base(
                $"Name collision: '{name}' already registered by '{existingOwner}', requested by '{newOwner}'",
                DslDiagnosticCodes.TypeNameCollision,
                newOwner)
        {
            ConflictingName = name;
            ExistingOwner = existingOwner;
        }

        public string ConflictingName { get; }

        public string ExistingOwner { get; }
    }
}

#pragma warning restore MA0048

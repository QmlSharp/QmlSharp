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

    /// <summary>Thrown when inheritance resolution exceeds the configured maximum depth.</summary>
    public sealed class MaxDepthExceededException : DslGenerationException
    {
        public MaxDepthExceededException(string typeName, int maxDepth)
            : base(
                $"Inheritance depth for '{typeName}' exceeded maximum depth {maxDepth}",
                DslDiagnosticCodes.MaxDepthExceeded,
                typeName)
        {
            MaxDepth = maxDepth;
        }

        public int MaxDepth { get; }
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

    /// <summary>Thrown when a property cannot be represented in generated C# metadata.</summary>
    public sealed class UnsupportedPropertyTypeException : DslGenerationException
    {
        public UnsupportedPropertyTypeException(string propertyName, string propertyType, QmlSharp.Registry.QmlType declaringType)
            : base(
                $"Property '{propertyName}' on '{declaringType.QualifiedName}' has unsupported type '{propertyType}'.",
                DslDiagnosticCodes.UnsupportedPropertyType,
                declaringType.QualifiedName,
                declaringType.ModuleUri)
        {
            PropertyName = propertyName;
            PropertyType = propertyType;
        }

        public string PropertyName { get; }

        public string PropertyType { get; }
    }

    /// <summary>Thrown when grouped and direct property names conflict.</summary>
    public sealed class GroupedPropertyConflictException : DslGenerationException
    {
        public GroupedPropertyConflictException(string groupName)
            : base(
                $"Grouped property '{groupName}' conflicts with a direct property of the same name.",
                DslDiagnosticCodes.GroupedPropertyConflict)
        {
            GroupName = groupName;
        }

        public string GroupName { get; }
    }

    /// <summary>Thrown when a signal parameter cannot be represented in generated C# metadata.</summary>
    public sealed class UnsupportedSignalParameterException : DslGenerationException
    {
        public UnsupportedSignalParameterException(string signalName, string parameterName, string parameterType, QmlSharp.Registry.QmlType declaringType)
            : base(
                $"Signal '{signalName}' on '{declaringType.QualifiedName}' has unsupported parameter '{parameterName}' of type '{parameterType}'.",
                DslDiagnosticCodes.UnsupportedSignalParameter,
                declaringType.QualifiedName,
                declaringType.ModuleUri)
        {
            SignalName = signalName;
            ParameterName = parameterName;
            ParameterType = parameterType;
        }

        public string SignalName { get; }

        public string ParameterName { get; }

        public string ParameterType { get; }
    }

    /// <summary>Thrown when a QML method signature cannot be represented in generated C# metadata.</summary>
    public sealed class UnsupportedMethodSignatureException : DslGenerationException
    {
        public UnsupportedMethodSignatureException(string methodName, string reason, QmlSharp.Registry.QmlType declaringType)
            : base(
                $"Method '{methodName}' on '{declaringType.QualifiedName}' has an unsupported signature: {reason}.",
                DslDiagnosticCodes.UnsupportedMethodSignature,
                declaringType.QualifiedName,
                declaringType.ModuleUri)
        {
            MethodName = methodName;
            Reason = reason;
        }

        public string MethodName { get; }

        public string Reason { get; }
    }

    /// <summary>Describes a property/method name collision found during method generation.</summary>
    public sealed class MethodPropertyNameCollisionException : DslGenerationException
    {
        public MethodPropertyNameCollisionException(string methodName, string ownerType)
            : base(
                $"Method '{methodName}' collides with a generated property name on '{ownerType}'.",
                DslDiagnosticCodes.MethodPropertyNameCollision,
                ownerType)
        {
            MethodName = methodName;
        }

        public string MethodName { get; }
    }

    /// <summary>Thrown when an enum contains duplicate generated member names.</summary>
    public sealed class DuplicateEnumMemberException : DslGenerationException
    {
        public DuplicateEnumMemberException(string enumName, string memberName, QmlSharp.Registry.QmlType ownerType)
            : base(
                $"Enum '{enumName}' on '{ownerType.QualifiedName}' contains duplicate generated member '{memberName}'.",
                DslDiagnosticCodes.DuplicateEnumMember,
                ownerType.QualifiedName,
                ownerType.ModuleUri)
        {
            EnumName = enumName;
            MemberName = memberName;
        }

        public string EnumName { get; }

        public string MemberName { get; }
    }

    /// <summary>Thrown when two QML enums resolve to the same generated C# enum name.</summary>
    public sealed class EnumNameCollisionException : DslGenerationException
    {
        public EnumNameCollisionException(string enumName, QmlSharp.Registry.QmlType ownerType)
            : base(
                $"Enum name '{enumName}' collides within generated type '{ownerType.QualifiedName}'.",
                DslDiagnosticCodes.EnumNameCollision,
                ownerType.QualifiedName,
                ownerType.ModuleUri)
        {
            EnumName = enumName;
        }

        public string EnumName { get; }
    }

    /// <summary>Thrown when an attached type declaration cannot be resolved to an attached surface.</summary>
    public sealed class UnresolvedAttachedTypeException : DslGenerationException
    {
        public UnresolvedAttachedTypeException(string ownerTypeName, string attachedTypeName, string? moduleUri)
            : base(
                $"Cannot resolve attached type '{attachedTypeName}' declared by '{ownerTypeName}'.",
                DslDiagnosticCodes.UnresolvedAttachedType,
                ownerTypeName,
                moduleUri)
        {
            AttachedTypeName = attachedTypeName;
        }

        public string AttachedTypeName { get; }
    }

    /// <summary>Thrown when a ViewModel schema fixture cannot be represented as generator metadata.</summary>
    public sealed class ViewModelSchemaException : FormatException
    {
        public ViewModelSchemaException(string message, string fieldPath, Exception? inner = null)
            : base(message, inner)
        {
            FieldPath = fieldPath;
        }

        public string FieldPath { get; }
    }
}

#pragma warning restore MA0048

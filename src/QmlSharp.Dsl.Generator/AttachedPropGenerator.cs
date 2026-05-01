using QmlSharp.Registry;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Dsl.Generator
{
    /// <summary>
    /// Generates structured metadata for QML attached property builder surfaces.
    /// </summary>
    public sealed class AttachedPropGenerator : IAttachedPropGenerator
    {
        public GeneratedAttachedType Generate(QmlType attachedType, GenerationContext context)
        {
            ArgumentNullException.ThrowIfNull(attachedType);
            ArgumentNullException.ThrowIfNull(context);

            ResolvedType resolvedType = new InheritanceResolver(context.Options.Inheritance).Resolve(attachedType, context.Registry);
            string methodName = DeriveAttachedMethodName(attachedType);
            string builderInterfaceName = $"I{methodName}AttachedBuilder";

            return new GeneratedAttachedType(
                TypeName: attachedType.QualifiedName,
                MethodName: methodName,
                ResolvedType: resolvedType,
                Properties: GenerateProperties(resolvedType, builderInterfaceName, context),
                Signals: GenerateSignals(resolvedType, builderInterfaceName, context),
                BuilderInterfaceName: builderInterfaceName);
        }

        public IReadOnlyList<QmlType> GetAllAttachedTypes(IRegistryQuery registry)
        {
            ArgumentNullException.ThrowIfNull(registry);

            Dictionary<string, QmlType> attachedTypes = new(StringComparer.Ordinal);
            foreach (QmlType attachedType in registry.FindTypes(type => !string.IsNullOrWhiteSpace(type.AttachedType))
                         .OrderBy(type => type.AttachedType, StringComparer.Ordinal)
                         .ThenBy(type => type.QualifiedName, StringComparer.Ordinal)
                         .Select(ownerType => ResolveAttachedType(ownerType, registry)))
            {
                attachedTypes[attachedType.QualifiedName] = attachedType;
            }

            return attachedTypes.Values
                .OrderBy(type => type.QualifiedName, StringComparer.Ordinal)
                .ToArray();
        }

        private static ImmutableArray<GeneratedProperty> GenerateProperties(
            ResolvedType resolvedType,
            string builderInterfaceName,
            GenerationContext context)
        {
            ImmutableArray<GeneratedProperty>.Builder properties = ImmutableArray.CreateBuilder<GeneratedProperty>();
            foreach (ResolvedProperty property in resolvedType.AllProperties.OrderBy(property => property.Property.Name, StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(property.Property.TypeName)
                    || string.Equals(context.TypeMapper.GetSetterType(property.Property), "void", StringComparison.Ordinal))
                {
                    throw new UnsupportedPropertyTypeException(
                        property.Property.Name,
                        property.Property.TypeName,
                        property.DeclaredBy);
                }

                string name = context.NameRegistry.RegisterPropertyName(property.Property.Name, builderInterfaceName);
                string csharpType = context.TypeMapper.GetSetterType(property.Property);
                string setterSignature = property.Property.IsReadonly
                    ? string.Empty
                    : $"{builderInterfaceName} {name}({csharpType} value)";
                string? bindSignature = property.Property.IsReadonly || !context.Options.Properties.GenerateBindMethods
                    ? null
                    : $"{builderInterfaceName} {name}Bind(string expr)";

                properties.Add(new GeneratedProperty(
                    Name: name,
                    SetterSignature: setterSignature,
                    BindSignature: bindSignature,
                    XmlDoc: $"<summary>Sets the QML attached {property.Property.Name} property.</summary>",
                    DeclaredBy: property.DeclaredBy,
                    IsReadOnly: property.Property.IsReadonly,
                    IsRequired: property.Property.IsRequired,
                    CSharpType: csharpType));
            }

            return properties.ToImmutable();
        }

        private static ImmutableArray<GeneratedSignal> GenerateSignals(
            ResolvedType resolvedType,
            string builderInterfaceName,
            GenerationContext context)
        {
            ImmutableArray<GeneratedSignal>.Builder signals = ImmutableArray.CreateBuilder<GeneratedSignal>();
            foreach (ResolvedSignal signal in resolvedType.AllSignals.OrderBy(signal => BuildSignalKey(signal.Signal), StringComparer.Ordinal))
            {
                ImmutableArray<GeneratedParameter> parameters = MapSignalParameters(signal, context);
                string handlerName = $"{context.Options.Signals.HandlerPrefix}{MemberNameUtilities.ToPascalCase(signal.Signal.Name)}";
                string delegateType = GetDelegateType(parameters, context.Options.Signals.SimplifyNoArgHandlers);

                signals.Add(new GeneratedSignal(
                    SignalName: signal.Signal.Name,
                    HandlerName: handlerName,
                    HandlerSignature: $"{builderInterfaceName} {handlerName}({delegateType} handler)",
                    XmlDoc: $"<summary>Handles the QML attached {signal.Signal.Name} signal.</summary>",
                    DeclaredBy: signal.DeclaredBy,
                    Parameters: parameters));
            }

            return signals.ToImmutable();
        }

        private static ImmutableArray<GeneratedParameter> MapSignalParameters(ResolvedSignal signal, GenerationContext context)
        {
            ImmutableArray<GeneratedParameter>.Builder parameters = ImmutableArray.CreateBuilder<GeneratedParameter>();
            foreach (QmlParameter parameter in signal.Signal.Parameters)
            {
                if (string.IsNullOrWhiteSpace(parameter.TypeName)
                    || string.Equals(parameter.TypeName, "void", StringComparison.Ordinal))
                {
                    throw new UnsupportedSignalParameterException(
                        signal.Signal.Name,
                        parameter.Name,
                        parameter.TypeName,
                        signal.DeclaredBy);
                }

                string csharpType = context.TypeMapper.GetParameterType(parameter);
                if (string.Equals(csharpType, "void", StringComparison.Ordinal))
                {
                    throw new UnsupportedSignalParameterException(
                        signal.Signal.Name,
                        parameter.Name,
                        parameter.TypeName,
                        signal.DeclaredBy);
                }

                parameters.Add(new GeneratedParameter(
                    Name: MemberNameUtilities.ToSafeParameterName(parameter.Name, context.NameRegistry),
                    CSharpType: csharpType,
                    QmlType: parameter.TypeName));
            }

            return parameters.ToImmutable();
        }

        private static QmlType ResolveAttachedType(QmlType ownerType, IRegistryQuery registry)
        {
            string attachedTypeName = ownerType.AttachedType!;
            QmlType? attachedType = registry.FindTypeByQualifiedName(attachedTypeName);
            if (attachedType is not null)
            {
                return attachedType;
            }

            if (ownerType.ModuleUri is not null)
            {
                attachedType = registry.FindTypeByQmlName(ownerType.ModuleUri, attachedTypeName);
                if (attachedType is not null)
                {
                    return attachedType;
                }
            }

            QmlType[] globalMatches = registry.FindTypes(type =>
                    string.Equals(type.QmlName, attachedTypeName, StringComparison.Ordinal)
                    || string.Equals(type.QualifiedName, attachedTypeName, StringComparison.Ordinal))
                .ToArray();
            if (globalMatches.Length == 1)
            {
                return globalMatches[0];
            }

            throw new UnresolvedAttachedTypeException(ownerType.QualifiedName, attachedTypeName, ownerType.ModuleUri);
        }

        private static string DeriveAttachedMethodName(QmlType attachedType)
        {
            string typeName = attachedType.QmlName ?? attachedType.QualifiedName;
            if (typeName.EndsWith("Attached", StringComparison.Ordinal))
            {
                typeName = typeName[..^"Attached".Length];
            }

            string[] prefixes = ["QQuick", "Qml", "Q"];
            string? matchingPrefix = prefixes
                .Where(prefix => typeName.StartsWith(prefix, StringComparison.Ordinal) && typeName.Length > prefix.Length)
                .FirstOrDefault();
            if (matchingPrefix is not null)
            {
                typeName = typeName[matchingPrefix.Length..];
            }

            return MemberNameUtilities.ToPascalCase(typeName);
        }

        private static string GetDelegateType(ImmutableArray<GeneratedParameter> parameters, bool simplifyNoArgHandlers)
        {
            if (parameters.IsDefaultOrEmpty)
            {
                return simplifyNoArgHandlers ? "Action" : "Action<object>";
            }

            return $"Action<{string.Join(", ", parameters.Select(parameter => parameter.CSharpType))}>";
        }

        private static string BuildSignalKey(QmlSignal signal)
        {
            return $"{signal.Name}({string.Join(";", signal.Parameters.Select(parameter => $"{parameter.TypeName.Length}:{parameter.TypeName}"))})";
        }
    }
}

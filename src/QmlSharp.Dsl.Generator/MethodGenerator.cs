using QmlSharp.Registry;

namespace QmlSharp.Dsl.Generator
{
    /// <summary>
    /// Generates structured QML method metadata for generated C# builder surfaces.
    /// </summary>
    public sealed class MethodGenerator : IMethodGenerator
    {
        public GeneratedMethod Generate(ResolvedMethod method, QmlType ownerType, GenerationContext context)
        {
            ArgumentNullException.ThrowIfNull(method);
            ArgumentNullException.ThrowIfNull(ownerType);
            ArgumentNullException.ThrowIfNull(context);

            if (string.IsNullOrWhiteSpace(method.Method.Name))
            {
                throw new UnsupportedMethodSignatureException("<empty>", "method name is empty", method.DeclaredBy);
            }

            ImmutableArray<GeneratedParameter> parameters = MapParameters(method, context);
            string builderInterfaceName = MemberNameUtilities.GetBuilderInterfaceName(ownerType);
            string methodName = context.NameRegistry.RegisterMethodName(method.Method.Name, builderInterfaceName);
            string returnType = context.TypeMapper.GetReturnType(method.Method);
            string signatureReturnType = string.Equals(returnType, "void", StringComparison.Ordinal)
                ? builderInterfaceName
                : returnType;
            string parameterList = string.Join(", ", parameters.Select(parameter => $"{parameter.CSharpType} {parameter.Name}"));

            return new GeneratedMethod(
                Name: methodName,
                Signature: $"{signatureReturnType} {methodName}({parameterList})",
                Parameters: parameters,
                ReturnType: returnType,
                XmlDoc: $"<summary>Invokes the QML {method.Method.Name} method.</summary>",
                DeclaredBy: method.DeclaredBy,
                IsConstructor: IsConstructorLikeMethod(method.Method, ownerType));
        }

        public ImmutableArray<GeneratedMethod> GenerateAll(
            ResolvedType type,
            GenerationContext context)
        {
            ArgumentNullException.ThrowIfNull(type);
            ArgumentNullException.ThrowIfNull(context);

            if (type.AllMethods.IsDefaultOrEmpty)
            {
                return ImmutableArray<GeneratedMethod>.Empty;
            }

            return type.AllMethods
                .OrderBy(method => BuildMethodKey(method.Method), StringComparer.Ordinal)
                .Select(method => Generate(method, type.Type, context))
                .ToImmutableArray();
        }

        private static ImmutableArray<GeneratedParameter> MapParameters(ResolvedMethod method, GenerationContext context)
        {
            ImmutableArray<GeneratedParameter>.Builder parameters = ImmutableArray.CreateBuilder<GeneratedParameter>();
            foreach (QmlParameter parameter in method.Method.Parameters)
            {
                if (string.IsNullOrWhiteSpace(parameter.TypeName))
                {
                    throw new UnsupportedMethodSignatureException(method.Method.Name, $"parameter '{parameter.Name}' has no type", method.DeclaredBy);
                }

                string csharpType = context.TypeMapper.GetParameterType(parameter);
                if (string.Equals(csharpType, "void", StringComparison.Ordinal))
                {
                    throw new UnsupportedMethodSignatureException(method.Method.Name, $"parameter '{parameter.Name}' maps to void", method.DeclaredBy);
                }

                parameters.Add(new GeneratedParameter(
                    Name: MemberNameUtilities.ToSafeParameterName(parameter.Name, context.NameRegistry),
                    CSharpType: csharpType,
                    QmlType: parameter.TypeName));
            }

            return parameters.ToImmutable();
        }

        private static bool IsConstructorLikeMethod(QmlMethod method, QmlType ownerType)
        {
            string ownerQmlName = ownerType.QmlName ?? ownerType.QualifiedName;
            return string.Equals(method.Name, ownerQmlName, StringComparison.Ordinal)
                || string.Equals(method.Name, ownerType.QualifiedName, StringComparison.Ordinal)
                || string.Equals(method.Name, $"create{ownerQmlName}", StringComparison.Ordinal);
        }

        private static string BuildMethodKey(QmlMethod method)
        {
            return $"{method.Name}({string.Join(";", method.Parameters.Select(parameter => $"{parameter.TypeName.Length}:{parameter.TypeName}"))})";
        }
    }
}

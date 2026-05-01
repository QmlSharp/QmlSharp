using QmlSharp.Registry;

namespace QmlSharp.Dsl.Generator
{
    /// <summary>
    /// Generates structured signal handler metadata for generated C# builder surfaces.
    /// </summary>
    public sealed class SignalGenerator : ISignalGenerator
    {
        public GeneratedSignal Generate(ResolvedSignal signal, GenerationContext context)
        {
            ArgumentNullException.ThrowIfNull(signal);
            ArgumentNullException.ThrowIfNull(context);

            return Generate(signal, signal.DeclaredBy, context);
        }

        public GeneratedSignal Generate(ResolvedSignal signal, QmlType ownerType, GenerationContext context)
        {
            ArgumentNullException.ThrowIfNull(signal);
            ArgumentNullException.ThrowIfNull(ownerType);
            ArgumentNullException.ThrowIfNull(context);

            ImmutableArray<GeneratedParameter> parameters = MapParameters(signal, context);
            string handlerName = $"{context.Options.Signals.HandlerPrefix}{MemberNameUtilities.ToPascalCase(signal.Signal.Name)}";
            string builderInterfaceName = MemberNameUtilities.GetBuilderInterfaceName(ownerType);
            string delegateType = GetDelegateType(parameters, context.Options.Signals.SimplifyNoArgHandlers);

            return new GeneratedSignal(
                SignalName: signal.Signal.Name,
                HandlerName: handlerName,
                HandlerSignature: $"{builderInterfaceName} {handlerName}({delegateType} handler)",
                XmlDoc: $"<summary>Handles the QML {signal.Signal.Name} signal.</summary>",
                DeclaredBy: signal.DeclaredBy,
                Parameters: parameters);
        }

        public ImmutableArray<GeneratedSignal> GenerateAll(
            ImmutableArray<ResolvedSignal> signals,
            GenerationContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (signals.IsDefaultOrEmpty)
            {
                return ImmutableArray<GeneratedSignal>.Empty;
            }

            return signals
                .OrderBy(signal => BuildSignalKey(signal.Signal), StringComparer.Ordinal)
                .Select(signal => Generate(signal, context))
                .ToImmutableArray();
        }

        public ImmutableArray<GeneratedSignal> GenerateAll(
            ResolvedType type,
            GenerationContext context)
        {
            ArgumentNullException.ThrowIfNull(type);
            ArgumentNullException.ThrowIfNull(context);

            if (type.AllSignals.IsDefaultOrEmpty)
            {
                return ImmutableArray<GeneratedSignal>.Empty;
            }

            return type.AllSignals
                .OrderBy(signal => BuildSignalKey(signal.Signal), StringComparer.Ordinal)
                .Select(signal => Generate(signal, type.Type, context))
                .ToImmutableArray();
        }

        private static ImmutableArray<GeneratedParameter> MapParameters(ResolvedSignal signal, GenerationContext context)
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

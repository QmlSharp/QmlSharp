using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace QmlSharp.Compiler
{
    /// <summary>
    /// Extracts ViewModel runtime schemas from Roslyn-discovered ViewModel types.
    /// </summary>
    public sealed class ViewModelExtractor : IViewModelExtractor
    {
        private const string ExtractPhase = "Extract";
        private const string SchemaVersion = "1.0";
        private const int InternalSchemaVersion = 2;
        private const string StateAttributeMetadataName = "QmlSharp.Core.StateAttribute";
        private const string CommandAttributeMetadataName = "QmlSharp.Core.CommandAttribute";
        private const string EffectAttributeMetadataName = "QmlSharp.Core.EffectAttribute";
        private const string ViewMetadataName = "QmlSharp.Core.View`1";

        /// <inheritdoc />
        public ViewModelSchema Extract(DiscoveredViewModel viewModel, ProjectContext context, IIdAllocator idAllocator)
        {
            ArgumentNullException.ThrowIfNull(viewModel);
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(idAllocator);

            ViewBinding viewBinding = ResolveViewBinding(viewModel, context);
            return ExtractCore(viewModel, context, idAllocator, viewBinding);
        }

        /// <inheritdoc />
        public ViewModelSchema Extract(DiscoveredView view, ProjectContext context, IIdAllocator idAllocator)
        {
            ArgumentNullException.ThrowIfNull(view);
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(idAllocator);

            DiscoveredViewModel viewModel = new(
                view.ViewModelTypeName,
                view.FilePath,
                view.ViewModelSymbol);

            return ExtractCore(viewModel, context, idAllocator, new ViewBinding(view.ClassName, 0));
        }

        private ViewModelSchema ExtractCore(
            DiscoveredViewModel viewModel,
            ProjectContext context,
            IIdAllocator idAllocator,
            ViewBinding viewBinding)
        {
            ImmutableArray<CompilerDiagnostic> diagnostics = Validate(viewModel, context);
            foreach (CompilerDiagnostic diagnostic in diagnostics)
            {
                context.Diagnostics.Report(diagnostic);
            }

            INamedTypeSymbol symbol = viewModel.TypeSymbol;
            ImmutableArray<StateEntry> properties = ExtractProperties(symbol, context.Compilation, idAllocator, diagnostics);
            ImmutableArray<CommandEntry> commands = ExtractCommands(symbol, context.Compilation, idAllocator, diagnostics);
            ImmutableArray<EffectEntry> effects = ExtractEffects(symbol, idAllocator, diagnostics);

            return new ViewModelSchema(
                SchemaVersion,
                viewModel.ClassName,
                DeriveModuleName(context.Options.ModuleUriPrefix),
                context.Options.ModuleUriPrefix,
                context.Options.ModuleVersion,
                InternalSchemaVersion,
                idAllocator.GenerateSlotKey(viewBinding.ViewClassName, viewBinding.SlotIndex),
                properties,
                commands,
                effects,
                ExtractLifecycle(symbol));
        }

        /// <inheritdoc />
        public ImmutableArray<ViewModelSchema> ExtractAll(
            ImmutableArray<DiscoveredViewModel> viewModels,
            ProjectContext context,
            IIdAllocator idAllocator)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(idAllocator);

            ImmutableArray<ViewModelSchema>.Builder schemas = ImmutableArray.CreateBuilder<ViewModelSchema>();
            foreach (DiscoveredViewModel viewModel in viewModels
                .OrderBy(static model => model.ClassName, StringComparer.Ordinal)
                .ThenBy(static model => model.FilePath, StringComparer.Ordinal))
            {
                schemas.Add(Extract(viewModel, context, idAllocator));
            }

            return schemas.ToImmutable();
        }

        /// <inheritdoc />
        public ImmutableArray<CompilerDiagnostic> Validate(DiscoveredViewModel viewModel, ProjectContext context)
        {
            ArgumentNullException.ThrowIfNull(viewModel);
            ArgumentNullException.ThrowIfNull(context);

            ImmutableArray<CompilerDiagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<CompilerDiagnostic>();
            INamedTypeSymbol symbol = viewModel.TypeSymbol;

            ValidateStates(symbol, diagnostics);
            ValidateCommands(symbol, context.Compilation, diagnostics);
            ValidateEffects(symbol, diagnostics);

            return diagnostics
                .OrderBy(static diagnostic => diagnostic.Location?.FilePath ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(static diagnostic => diagnostic.Location?.Line ?? 0)
                .ThenBy(static diagnostic => diagnostic.Location?.Column ?? 0)
                .ThenBy(static diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(static diagnostic => diagnostic.Message, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static ImmutableArray<StateEntry> ExtractProperties(
            INamedTypeSymbol symbol,
            Compilation compilation,
            IIdAllocator idAllocator,
            ImmutableArray<CompilerDiagnostic> diagnostics)
        {
            ImmutableArray<StateEntry>.Builder properties = ImmutableArray.CreateBuilder<StateEntry>();
            foreach (IPropertySymbol property in GetStateProperties(symbol))
            {
                if (HasDiagnosticForMember(diagnostics, property))
                {
                    continue;
                }

                string qmlName = ToCamelCase(property.Name);
                properties.Add(new StateEntry(
                    qmlName,
                    MapType(property.Type)!,
                    ExtractDefaultValue(property, compilation),
                    ReadBoolAttributeArgument(property, StateAttributeMetadataName, "Readonly"),
                    idAllocator.AllocateMemberId(symbol.Name, property.Name),
                    property.Name,
                    ReadBoolAttributeArgument(property, StateAttributeMetadataName, "Deferred")));
            }

            return properties
                .OrderBy(static property => property.Name, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static ImmutableArray<CommandEntry> ExtractCommands(
            INamedTypeSymbol symbol,
            Compilation compilation,
            IIdAllocator idAllocator,
            ImmutableArray<CompilerDiagnostic> diagnostics)
        {
            ImmutableArray<CommandEntry>.Builder commands = ImmutableArray.CreateBuilder<CommandEntry>();
            INamedTypeSymbol? taskType = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
            foreach (IMethodSymbol method in GetCommandMethods(symbol))
            {
                if (HasDiagnosticForMember(diagnostics, method))
                {
                    continue;
                }

                ImmutableArray<ParameterEntry> parameters = method.Parameters
                    .Select(static parameter => new ParameterEntry(parameter.Name, MapType(parameter.Type)!))
                    .ToImmutableArray();
                commands.Add(new CommandEntry(
                    ToCamelCase(method.Name),
                    parameters,
                    idAllocator.AllocateCommandId(symbol.Name, method.Name),
                    method.Name,
                    IsTaskReturn(method.ReturnType, taskType)));
            }

            return commands
                .OrderBy(static command => command.Name, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static ImmutableArray<EffectEntry> ExtractEffects(
            INamedTypeSymbol symbol,
            IIdAllocator idAllocator,
            ImmutableArray<CompilerDiagnostic> diagnostics)
        {
            ImmutableArray<EffectEntry>.Builder effects = ImmutableArray.CreateBuilder<EffectEntry>();
            foreach (IEventSymbol eventSymbol in GetEffectEvents(symbol))
            {
                if (HasDiagnosticForMember(diagnostics, eventSymbol))
                {
                    continue;
                }

                ImmutableArray<ParameterEntry> parameters = ExtractEffectParameters(eventSymbol);
                string payloadType = parameters.Length == 0 ? "void" : parameters[0].Type;
                effects.Add(new EffectEntry(
                    ToCamelCase(eventSymbol.Name),
                    payloadType,
                    idAllocator.AllocateEffectId(symbol.Name, eventSymbol.Name),
                    parameters,
                    eventSymbol.Name));
            }

            return effects
                .OrderBy(static effect => effect.Name, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static LifecycleInfo ExtractLifecycle(INamedTypeSymbol symbol)
        {
            bool onMounted = HasLifecycleMethod(symbol, "OnMounted");
            bool onUnmounting = HasLifecycleMethod(symbol, "OnUnmounting");
            bool hotReload = HasLifecycleMethod(symbol, "OnHotReload");
            return new LifecycleInfo(onMounted, onUnmounting, hotReload);
        }

        private static void ValidateStates(INamedTypeSymbol symbol, ImmutableArray<CompilerDiagnostic>.Builder diagnostics)
        {
            Dictionary<string, IPropertySymbol> seenNames = new(StringComparer.Ordinal);
            foreach (IPropertySymbol property in GetStateProperties(symbol))
            {
                if (property.IsStatic)
                {
                    AddDiagnostic(diagnostics, DiagnosticCodes.StaticMemberNotAllowed, property, $"'{property.Name}' is static.");
                    continue;
                }

                if (property.DeclaredAccessibility != Accessibility.Public
                    || property.GetMethod is null
                    || property.GetMethod.DeclaredAccessibility != Accessibility.Public)
                {
                    AddDiagnostic(diagnostics, DiagnosticCodes.InvalidStateAttribute, property, $"'{property.Name}' must be a public instance property.");
                    continue;
                }

                string qmlName = ToCamelCase(property.Name);
                if (seenNames.TryGetValue(qmlName, out IPropertySymbol? previousProperty))
                {
                    AddDiagnostic(diagnostics, DiagnosticCodes.DuplicateStateName, property, $"'{property.Name}' duplicates '{previousProperty.Name}' as '{qmlName}'.");
                    continue;
                }

                seenNames.Add(qmlName, property);
                if (MapType(property.Type) is null)
                {
                    AddDiagnostic(diagnostics, DiagnosticCodes.UnsupportedStateType, property, $"'{property.Type.ToDisplayString()}' is not supported for '{property.Name}'.");
                }
            }
        }

        private static void ValidateCommands(
            INamedTypeSymbol symbol,
            Compilation compilation,
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics)
        {
            Dictionary<string, IMethodSymbol> seenNames = new(StringComparer.Ordinal);
            INamedTypeSymbol? taskType = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
            foreach (IMethodSymbol method in GetCommandMethods(symbol))
            {
                if (method.IsStatic)
                {
                    AddDiagnostic(diagnostics, DiagnosticCodes.StaticMemberNotAllowed, method, $"'{method.Name}' is static.");
                    continue;
                }

                if (method.DeclaredAccessibility != Accessibility.Public || method.IsGenericMethod)
                {
                    AddDiagnostic(diagnostics, DiagnosticCodes.InvalidCommandAttribute, method, $"'{method.Name}' must be a public non-generic instance method.");
                    continue;
                }

                if (method.ReturnsVoid is false && !IsTaskReturn(method.ReturnType, taskType))
                {
                    AddDiagnostic(diagnostics, DiagnosticCodes.CommandMustBeVoid, method, $"'{method.Name}' returns '{method.ReturnType.ToDisplayString()}'.");
                    continue;
                }

                string qmlName = ToCamelCase(method.Name);
                if (seenNames.TryGetValue(qmlName, out IMethodSymbol? previousMethod))
                {
                    AddDiagnostic(diagnostics, DiagnosticCodes.DuplicateCommandName, method, $"'{method.Name}' duplicates '{previousMethod.Name}' as '{qmlName}'.");
                    continue;
                }

                seenNames.Add(qmlName, method);
                foreach (IParameterSymbol parameter in method.Parameters.Where(static parameter => MapType(parameter.Type) is null))
                {
                    AddDiagnostic(diagnostics, DiagnosticCodes.InvalidCommandAttribute, method, $"Parameter '{parameter.Name}' has unsupported type '{parameter.Type.ToDisplayString()}'.");
                }
            }
        }

        private static void ValidateEffects(INamedTypeSymbol symbol, ImmutableArray<CompilerDiagnostic>.Builder diagnostics)
        {
            Dictionary<string, IEventSymbol> seenNames = new(StringComparer.Ordinal);
            foreach (IEventSymbol eventSymbol in GetEffectEvents(symbol))
            {
                if (eventSymbol.IsStatic)
                {
                    AddDiagnostic(diagnostics, DiagnosticCodes.StaticMemberNotAllowed, eventSymbol, $"'{eventSymbol.Name}' is static.");
                    continue;
                }

                if (eventSymbol.DeclaredAccessibility != Accessibility.Public)
                {
                    AddDiagnostic(diagnostics, DiagnosticCodes.InvalidEffectAttribute, eventSymbol, $"'{eventSymbol.Name}' must be a public instance event.");
                    continue;
                }

                string qmlName = ToCamelCase(eventSymbol.Name);
                if (seenNames.TryGetValue(qmlName, out IEventSymbol? previousEvent))
                {
                    AddDiagnostic(diagnostics, DiagnosticCodes.DuplicateEffectName, eventSymbol, $"'{eventSymbol.Name}' duplicates '{previousEvent.Name}' as '{qmlName}'.");
                    continue;
                }

                seenNames.Add(qmlName, eventSymbol);
                ImmutableArray<ITypeSymbol>? payloadTypes = TryGetEffectPayloadTypes(eventSymbol);
                if (payloadTypes is null)
                {
                    AddDiagnostic(diagnostics, DiagnosticCodes.EffectMustBeEvent, eventSymbol, $"'{eventSymbol.Name}' must use System.Action or System.Action<T>.");
                    continue;
                }

                if (payloadTypes.Value.Length > 1)
                {
                    AddDiagnostic(diagnostics, DiagnosticCodes.InvalidEffectAttribute, eventSymbol, $"'{eventSymbol.Name}' has multiple payload values, which are not supported.");
                    continue;
                }

                foreach (ITypeSymbol payloadType in payloadTypes.Value.Where(static payloadType => MapType(payloadType) is null))
                {
                    AddDiagnostic(diagnostics, DiagnosticCodes.InvalidEffectAttribute, eventSymbol, $"Payload type '{payloadType.ToDisplayString()}' is not supported.");
                }
            }
        }

        private static ImmutableArray<IPropertySymbol> GetStateProperties(INamedTypeSymbol symbol)
        {
            return EnumerateMembers(symbol)
                .OfType<IPropertySymbol>()
                .Where(static property => HasAttribute(property, StateAttributeMetadataName))
                .OrderBy(static property => property.Name, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static ImmutableArray<IMethodSymbol> GetCommandMethods(INamedTypeSymbol symbol)
        {
            return EnumerateMembers(symbol)
                .OfType<IMethodSymbol>()
                .Where(static method => HasAttribute(method, CommandAttributeMetadataName))
                .OrderBy(static method => method.Name, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static ImmutableArray<IEventSymbol> GetEffectEvents(INamedTypeSymbol symbol)
        {
            return EnumerateMembers(symbol)
                .OfType<IEventSymbol>()
                .Where(static eventSymbol => HasAttribute(eventSymbol, EffectAttributeMetadataName))
                .OrderBy(static eventSymbol => eventSymbol.Name, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static IEnumerable<ISymbol> EnumerateMembers(INamedTypeSymbol symbol)
        {
            for (INamedTypeSymbol? current = symbol; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
            {
                foreach (ISymbol member in current.GetMembers())
                {
                    yield return member;
                }
            }
        }

        private static ImmutableArray<ParameterEntry> ExtractEffectParameters(IEventSymbol eventSymbol)
        {
            ImmutableArray<ITypeSymbol>? payloadTypes = TryGetEffectPayloadTypes(eventSymbol);
            if (payloadTypes is null || payloadTypes.Value.Length == 0)
            {
                return ImmutableArray<ParameterEntry>.Empty;
            }

            return ImmutableArray.Create(new ParameterEntry("payload", MapType(payloadTypes.Value[0])!));
        }

        private static ImmutableArray<ITypeSymbol>? TryGetEffectPayloadTypes(IEventSymbol eventSymbol)
        {
            if (eventSymbol.Type is not INamedTypeSymbol delegateType)
            {
                return null;
            }

            if (!StringComparer.Ordinal.Equals(delegateType.ContainingNamespace.ToDisplayString(), "System")
                || !StringComparer.Ordinal.Equals(delegateType.Name, "Action"))
            {
                return null;
            }

            return delegateType.TypeArguments;
        }

        private static string? MapType(ITypeSymbol type)
        {
            return type.SpecialType switch
            {
                SpecialType.System_Boolean => "bool",
                SpecialType.System_Byte => "int",
                SpecialType.System_SByte => "int",
                SpecialType.System_Int16 => "int",
                SpecialType.System_UInt16 => "int",
                SpecialType.System_Int32 => "int",
                SpecialType.System_UInt32 => "int",
                SpecialType.System_Int64 => "int",
                SpecialType.System_UInt64 => "int",
                SpecialType.System_Single => "double",
                SpecialType.System_Double => "double",
                SpecialType.System_Decimal => "double",
                SpecialType.System_String => "string",
                SpecialType.System_Object => "json",
                _ => MapNamedType(type),
            };
        }

        private static string? MapNamedType(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol arrayType)
            {
                return MapListType(arrayType.ElementType);
            }

            if (type is not INamedTypeSymbol namedType)
            {
                return null;
            }

            string displayName = namedType.ToDisplayString();
            if (StringComparer.Ordinal.Equals(displayName, "System.Text.Json.JsonElement")
                || StringComparer.Ordinal.Equals(displayName, "System.Text.Json.JsonDocument")
                || displayName.StartsWith("System.Text.Json.Nodes.Json", StringComparison.Ordinal))
            {
                return "json";
            }

            if (namedType.IsGenericType && namedType.TypeArguments.Length == 1 && IsSupportedListType(namedType))
            {
                return MapListType(namedType.TypeArguments[0]);
            }

            return null;
        }

        private static bool IsSupportedListType(INamedTypeSymbol namedType)
        {
            string originalDefinition = namedType.OriginalDefinition.ToDisplayString();
            return StringComparer.Ordinal.Equals(originalDefinition, "System.Collections.Generic.IReadOnlyList<T>")
                || StringComparer.Ordinal.Equals(originalDefinition, "System.Collections.Generic.IList<T>")
                || StringComparer.Ordinal.Equals(originalDefinition, "System.Collections.Generic.List<T>")
                || StringComparer.Ordinal.Equals(originalDefinition, "System.Collections.Generic.IEnumerable<T>")
                || StringComparer.Ordinal.Equals(originalDefinition, "System.Collections.Immutable.ImmutableArray<T>");
        }

        private static string? MapListType(ITypeSymbol elementType)
        {
            string? elementQmlType = MapType(elementType);
            return elementQmlType is null ? null : $"list<{elementQmlType}>";
        }

        private static string? ExtractDefaultValue(IPropertySymbol property, Compilation compilation)
        {
            SyntaxReference? firstSyntaxReference = property.DeclaringSyntaxReferences.FirstOrDefault();
            if (firstSyntaxReference?.GetSyntax() is PropertyDeclarationSyntax syntax && syntax.Initializer is not null)
            {
                SemanticModel semanticModel = compilation.GetSemanticModel(syntax.SyntaxTree);
                Optional<object?> constant = semanticModel.GetConstantValue(syntax.Initializer.Value);
                if (constant.HasValue)
                {
                    return FormatDefaultValue(constant.Value, property.Type);
                }

                return null;
            }

            return DefaultValueForType(property.Type);
        }

        private static string? DefaultValueForType(ITypeSymbol type)
        {
            return type.SpecialType switch
            {
                SpecialType.System_Boolean => "false",
                SpecialType.System_Byte => "0",
                SpecialType.System_SByte => "0",
                SpecialType.System_Int16 => "0",
                SpecialType.System_UInt16 => "0",
                SpecialType.System_Int32 => "0",
                SpecialType.System_UInt32 => "0",
                SpecialType.System_Int64 => "0",
                SpecialType.System_UInt64 => "0",
                SpecialType.System_Single => "0",
                SpecialType.System_Double => "0",
                SpecialType.System_Decimal => "0",
                SpecialType.System_String => string.Empty,
                SpecialType.System_Object => "null",
                _ => null,
            };
        }

        private static string? FormatDefaultValue(object? value, ITypeSymbol type)
        {
            if (value is null)
            {
                return "null";
            }

            return type.SpecialType switch
            {
                SpecialType.System_Boolean => ((bool)value) ? "true" : "false",
                SpecialType.System_Byte => Convert.ToString(value, CultureInfo.InvariantCulture),
                SpecialType.System_SByte => Convert.ToString(value, CultureInfo.InvariantCulture),
                SpecialType.System_Int16 => Convert.ToString(value, CultureInfo.InvariantCulture),
                SpecialType.System_UInt16 => Convert.ToString(value, CultureInfo.InvariantCulture),
                SpecialType.System_Int32 => Convert.ToString(value, CultureInfo.InvariantCulture),
                SpecialType.System_UInt32 => Convert.ToString(value, CultureInfo.InvariantCulture),
                SpecialType.System_Int64 => Convert.ToString(value, CultureInfo.InvariantCulture),
                SpecialType.System_UInt64 => Convert.ToString(value, CultureInfo.InvariantCulture),
                SpecialType.System_Single => Convert.ToString(value, CultureInfo.InvariantCulture),
                SpecialType.System_Double => Convert.ToString(value, CultureInfo.InvariantCulture),
                SpecialType.System_Decimal => Convert.ToString(value, CultureInfo.InvariantCulture),
                SpecialType.System_String => (string)value,
                _ => null,
            };
        }

        private static bool IsTaskReturn(ITypeSymbol returnType, INamedTypeSymbol? taskType)
        {
            return taskType is not null && SymbolEqualityComparer.Default.Equals(returnType, taskType);
        }

        private static bool HasLifecycleMethod(INamedTypeSymbol symbol, string methodName)
        {
            return symbol.GetMembers(methodName)
                .OfType<IMethodSymbol>()
                .Any(static method => !method.IsStatic
                    && method.DeclaredAccessibility == Accessibility.Public
                    && method.Parameters.Length == 0
                    && method.ReturnsVoid);
        }

        private static bool HasDiagnosticForMember(ImmutableArray<CompilerDiagnostic> diagnostics, ISymbol symbol)
        {
            SourceLocation? location = GetSourceLocation(symbol);
            return diagnostics.Any(diagnostic =>
                diagnostic.Location is not null
                && location is not null
                && StringComparer.Ordinal.Equals(diagnostic.Location.FilePath, location.FilePath)
                && diagnostic.Location.Line == location.Line
                && diagnostic.Location.Column == location.Column);
        }

        private static bool HasAttribute(ISymbol symbol, string metadataName)
        {
            return symbol.GetAttributes().Any(attribute =>
                StringComparer.Ordinal.Equals(attribute.AttributeClass?.ToDisplayString(), metadataName));
        }

        private static bool ReadBoolAttributeArgument(ISymbol symbol, string metadataName, string argumentName)
        {
            AttributeData? attribute = symbol.GetAttributes()
                .FirstOrDefault(candidate => StringComparer.Ordinal.Equals(candidate.AttributeClass?.ToDisplayString(), metadataName));
            return attribute?.NamedArguments
                .Where(argument => StringComparer.Ordinal.Equals(argument.Key, argumentName))
                .Select(static argument => argument.Value.Value is bool value ? value : (bool?)null)
                .FirstOrDefault(static value => value.HasValue) ?? false;
        }

        private static void AddDiagnostic(
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics,
            string code,
            ISymbol symbol,
            string details)
        {
            diagnostics.Add(new CompilerDiagnostic(
                code,
                DiagnosticSeverity.Error,
                DiagnosticMessageCatalog.FormatMessage(code, details),
                GetSourceLocation(symbol),
                ExtractPhase));
        }

        private static SourceLocation? GetSourceLocation(ISymbol symbol)
        {
            Location? location = symbol.Locations.FirstOrDefault(static candidate => candidate.IsInSource);
            if (location is null)
            {
                return null;
            }

            FileLinePositionSpan lineSpan = location.GetLineSpan();
            return SourceLocation.Partial(
                lineSpan.Path,
                lineSpan.StartLinePosition.Line + 1,
                lineSpan.StartLinePosition.Character + 1);
        }

        private static string DeriveModuleName(string moduleUri)
        {
            string[] parts = moduleUri.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length == 0 ? moduleUri : parts[^1];
        }

        private static string ToCamelCase(string name)
        {
            if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
            {
                return name;
            }

            return string.Create(
                name.Length,
                name,
                static (span, value) =>
                {
                    span[0] = char.ToLowerInvariant(value[0]);
                    value.AsSpan(1).CopyTo(span[1..]);
                });
        }

        private static ViewBinding ResolveViewBinding(DiscoveredViewModel viewModel, ProjectContext context)
        {
            INamedTypeSymbol? viewBaseSymbol = context.Compilation.GetTypeByMetadataName(ViewMetadataName);
            if (viewBaseSymbol is null)
            {
                return new ViewBinding(viewModel.ClassName, 0);
            }

            INamedTypeSymbol? boundView = EnumerateNamedTypes(context)
                .Where(candidate => IsViewBoundTo(candidate, viewBaseSymbol, viewModel.TypeSymbol))
                .OrderBy(static candidate => GetSourceFilePath(candidate), StringComparer.Ordinal)
                .ThenBy(static candidate => candidate.Name, StringComparer.Ordinal)
                .FirstOrDefault();

            return new ViewBinding(boundView?.Name ?? viewModel.ClassName, 0);
        }

        private static IEnumerable<INamedTypeSymbol> EnumerateNamedTypes(ProjectContext context)
        {
            foreach (SyntaxTree syntaxTree in context.Compilation.SyntaxTrees.Where(syntaxTree => ContainsSourceFile(context, syntaxTree.FilePath)))
            {
                SemanticModel semanticModel = context.Compilation.GetSemanticModel(syntaxTree);
                SyntaxNode root = syntaxTree.GetRoot();
                foreach (INamedTypeSymbol symbol in root.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .Select(classDeclaration => semanticModel.GetDeclaredSymbol(classDeclaration))
                    .Where(static declaredSymbol => declaredSymbol is INamedTypeSymbol)
                    .Cast<INamedTypeSymbol>())
                {
                    yield return symbol;
                }
            }
        }

        private static bool IsViewBoundTo(
            INamedTypeSymbol candidate,
            INamedTypeSymbol viewBaseSymbol,
            INamedTypeSymbol viewModelSymbol)
        {
            for (INamedTypeSymbol? current = candidate.BaseType; current is not null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, viewBaseSymbol)
                    && current.TypeArguments.Length == 1
                    && SymbolEqualityComparer.Default.Equals(current.TypeArguments[0], viewModelSymbol))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsSourceFile(ProjectContext context, string filePath)
        {
            return context.SourceFiles.Any(sourceFile =>
                StringComparer.Ordinal.Equals(NormalizePath(sourceFile), NormalizePath(filePath)));
        }

        private static string GetSourceFilePath(INamedTypeSymbol symbol)
        {
            return symbol.Locations
                .Where(static location => location.IsInSource)
                .OrderBy(static location => location.SourceTree?.FilePath ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(static location => location.GetLineSpan().StartLinePosition.Line)
                .ThenBy(static location => location.GetLineSpan().StartLinePosition.Character)
                .Select(static location => location.SourceTree?.FilePath ?? string.Empty)
                .FirstOrDefault() ?? string.Empty;
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }

        private sealed record ViewBinding(string ViewClassName, int SlotIndex);
    }
}

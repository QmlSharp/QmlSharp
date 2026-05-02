using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using QmlSharp.Qml.Ast;
using QmlSharp.Registry;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Compiler
{
    /// <summary>
    /// Roslyn-backed implementation of DSL call extraction and DSL IR to QML AST lowering.
    /// </summary>
    public sealed class DslTransformer : IDslTransformer
    {
        private const string TransformPhase = "Transform";

        private static readonly ImmutableHashSet<string> AttachedCallbackNames =
            ImmutableHashSet.Create(StringComparer.Ordinal, "Layout", "Keys", "Drag");

        private ImmutableArray<CompilerDiagnostic> lastDiagnostics = ImmutableArray<CompilerDiagnostic>.Empty;

        /// <inheritdoc />
        public DslTransformResult Transform(DiscoveredView view, ProjectContext context, IRegistryQuery registry)
        {
            ArgumentNullException.ThrowIfNull(view);
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(registry);

            InvocationExpressionSyntax? invocation = FindBuildReturnInvocation(view);
            if (invocation is null)
            {
                CompilerDiagnostic diagnostic = CreateDiagnostic(
                    DiagnosticCodes.InvalidCallChain,
                    "Build() must return a fluent DSL invocation expression.",
                    SourceLocation.FileOnly(view.FilePath));

                ObjectDefinitionNode emptyRoot = new()
                {
                    TypeName = "Invalid",
                };

                return new DslTransformResult(
                    new QmlDocument { RootObject = emptyRoot },
                    new DslCallNode(
                        "Invalid",
                        ImmutableArray<DslPropertyCall>.Empty,
                        ImmutableArray<DslBindingCall>.Empty,
                        ImmutableArray<DslSignalHandlerCall>.Empty,
                        ImmutableArray<DslGroupedCall>.Empty,
                        ImmutableArray<DslAttachedCall>.Empty,
                        ImmutableArray<DslCallNode>.Empty,
                        SourceLocation: SourceLocation.FileOnly(view.FilePath)),
                    ImmutableArray<SourceMapping>.Empty,
                    ImmutableArray.Create(diagnostic));
            }

            SemanticModel semanticModel = context.Compilation.GetSemanticModel(invocation.SyntaxTree);
            DslCallNode callTree = ExtractCallTree(invocation, semanticModel);
            return TransformCallTree(callTree, registry);
        }

        /// <inheritdoc />
        public DslCallNode ExtractCallTree(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
        {
            ArgumentNullException.ThrowIfNull(invocation);
            ArgumentNullException.ThrowIfNull(semanticModel);

            ImmutableArray<InvocationExpressionSyntax> chain = FlattenInvocationChain(invocation);
            if (chain.IsDefaultOrEmpty)
            {
                return CreateInvalidCallNode(invocation);
            }

            InvocationExpressionSyntax factoryInvocation = chain[0];
            string typeName = GetInvocationName(factoryInvocation);
            DslCallNode current = new(
                typeName,
                ImmutableArray<DslPropertyCall>.Empty,
                ImmutableArray<DslBindingCall>.Empty,
                ImmutableArray<DslSignalHandlerCall>.Empty,
                ImmutableArray<DslGroupedCall>.Empty,
                ImmutableArray<DslAttachedCall>.Empty,
                ImmutableArray<DslCallNode>.Empty,
                SourceLocation: ToSourceLocation(factoryInvocation));

            for (int index = 1; index < chain.Length; index++)
            {
                current = ApplyChainInvocation(current, chain[index], semanticModel);
            }

            return current;
        }

        /// <inheritdoc />
        public ObjectDefinitionNode ToAstNode(DslCallNode callNode, IRegistryQuery registry)
        {
            ArgumentNullException.ThrowIfNull(callNode);
            ArgumentNullException.ThrowIfNull(registry);

            DslTransformResult result = TransformCallTree(callNode, registry);
            lastDiagnostics = result.Diagnostics;
            return result.Document.RootObject;
        }

        /// <summary>
        /// Converts a DSL call tree into a complete document and diagnostics. This pure IR path is used by tests.
        /// </summary>
        /// <param name="callNode">The root DSL call node.</param>
        /// <param name="registry">The QML type registry.</param>
        /// <returns>The transform result.</returns>
        public DslTransformResult TransformCallTree(DslCallNode callNode, IRegistryQuery registry)
        {
            ArgumentNullException.ThrowIfNull(callNode);
            ArgumentNullException.ThrowIfNull(registry);

            ImmutableArray<CompilerDiagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<CompilerDiagnostic>();
            ImmutableArray<SourceMapping>.Builder mappings = ImmutableArray.CreateBuilder<SourceMapping>();
            ObjectDefinitionNode root = ToAstNode(callNode, registry, diagnostics, mappings);
            ImmutableArray<CompilerDiagnostic> diagnosticArray = diagnostics
                .OrderBy(static diagnostic => diagnostic.Location?.FilePath ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(static diagnostic => diagnostic.Location?.Line ?? 0)
                .ThenBy(static diagnostic => diagnostic.Location?.Column ?? 0)
                .ThenBy(static diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ToImmutableArray();

            lastDiagnostics = diagnosticArray;

            return new DslTransformResult(
                new QmlDocument { RootObject = root },
                callNode,
                mappings.ToImmutable(),
                diagnosticArray);
        }

        /// <summary>Gets diagnostics produced by the most recent AST conversion call.</summary>
        public ImmutableArray<CompilerDiagnostic> LastDiagnostics => lastDiagnostics;

        private static DslCallNode ApplyChainInvocation(
            DslCallNode current,
            InvocationExpressionSyntax invocation,
            SemanticModel semanticModel)
        {
            string methodName = GetInvocationName(invocation);
            SourceLocation? location = ToSourceLocation(invocation);

            if (StringComparer.Ordinal.Equals(methodName, "Id"))
            {
                string? id = TryGetStringArgument(invocation);
                return current with { Id = id, SourceLocation = current.SourceLocation };
            }

            if (StringComparer.Ordinal.Equals(methodName, "Child"))
            {
                return ApplyChildInvocation(current, invocation, semanticModel);
            }

            if (StringComparer.Ordinal.Equals(methodName, "Children"))
            {
                return ApplyChildrenInvocation(current, invocation, semanticModel);
            }

            if (methodName.StartsWith("On", StringComparison.Ordinal) && methodName.Length > 2)
            {
                DslSignalHandlerCall handler = ExtractSignalHandler(methodName, invocation, semanticModel, location);
                return current with { SignalHandlers = current.SignalHandlers.Add(handler) };
            }

            if (IsCallbackArgument(invocation))
            {
                return ApplyCallbackInvocation(current, methodName, invocation, semanticModel, location);
            }

            if (methodName.EndsWith("Bind", StringComparison.Ordinal) && methodName.Length > 4)
            {
                string propertyName = methodName[..^4];
                DslBindingCall binding = new(propertyName, TryGetStringArgument(invocation) ?? string.Empty, location);
                return current with { Bindings = current.Bindings.Add(binding) };
            }

            return ApplyPropertyInvocation(current, methodName, invocation, semanticModel, location);
        }

        private static DslCallNode ApplyChildInvocation(
            DslCallNode current,
            InvocationExpressionSyntax invocation,
            SemanticModel semanticModel)
        {
            DslCallNode? child = TryExtractSingleChild(invocation, semanticModel);
            return child is null
                ? current
                : current with { Children = current.Children.Add(child) };
        }

        private static DslCallNode ApplyChildrenInvocation(
            DslCallNode current,
            InvocationExpressionSyntax invocation,
            SemanticModel semanticModel)
        {
            ImmutableArray<DslCallNode>.Builder children = current.Children.ToBuilder();
            foreach (ArgumentSyntax argument in invocation.ArgumentList.Arguments)
            {
                DslCallNode? child = TryExtractChild(argument.Expression, semanticModel);
                if (child is not null)
                {
                    children.Add(child);
                }
            }

            return current with { Children = children.ToImmutable() };
        }

        private static DslCallNode ApplyCallbackInvocation(
            DslCallNode current,
            string methodName,
            InvocationExpressionSyntax invocation,
            SemanticModel semanticModel,
            SourceLocation? location)
        {
            ImmutableArray<DslPropertyCall> properties = ExtractCallbackProperties(invocation, semanticModel);
            if (IsAttachedCallback(methodName))
            {
                DslAttachedCall attached = new(methodName, properties, location);
                return current with { AttachedProperties = current.AttachedProperties.Add(attached) };
            }

            DslGroupedCall grouped = new(methodName, properties, location);
            return current with { GroupedProperties = current.GroupedProperties.Add(grouped) };
        }

        private static DslCallNode ApplyPropertyInvocation(
            DslCallNode current,
            string methodName,
            InvocationExpressionSyntax invocation,
            SemanticModel semanticModel,
            SourceLocation? location)
        {
            object? value = invocation.ArgumentList.Arguments.Count == 0
                ? null
                : ExtractArgumentValue(invocation.ArgumentList.Arguments[0].Expression, semanticModel);

            DslPropertyCall property = new(methodName, value, location);
            return current with { Properties = current.Properties.Add(property) };
        }

        private static ImmutableArray<InvocationExpressionSyntax> FlattenInvocationChain(InvocationExpressionSyntax invocation)
        {
            ImmutableArray<InvocationExpressionSyntax>.Builder calls = ImmutableArray.CreateBuilder<InvocationExpressionSyntax>();
            InvocationExpressionSyntax? current = invocation;

            while (current is not null)
            {
                calls.Add(current);
                current = current.Expression is MemberAccessExpressionSyntax memberAccess
                    ? memberAccess.Expression as InvocationExpressionSyntax
                    : null;
            }

            ImmutableArray<InvocationExpressionSyntax> reversed = calls.ToImmutable();
            return reversed.Reverse().ToImmutableArray();
        }

        private static string GetInvocationName(InvocationExpressionSyntax invocation)
        {
            return invocation.Expression switch
            {
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                GenericNameSyntax generic => generic.Identifier.ValueText,
                MemberAccessExpressionSyntax memberAccess => memberAccess.Name switch
                {
                    IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                    GenericNameSyntax generic => generic.Identifier.ValueText,
                    _ => memberAccess.Name.ToString(),
                },
                _ => invocation.Expression.ToString(),
            };
        }

        private static DslCallNode? TryExtractSingleChild(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
        {
            if (invocation.ArgumentList.Arguments.Count != 1)
            {
                return null;
            }

            return TryExtractChild(invocation.ArgumentList.Arguments[0].Expression, semanticModel);
        }

        private static DslCallNode? TryExtractChild(ExpressionSyntax expression, SemanticModel semanticModel)
        {
            return expression is InvocationExpressionSyntax childInvocation
                ? new DslTransformer().ExtractCallTree(childInvocation, semanticModel)
                : null;
        }

        private static bool IsCallbackArgument(InvocationExpressionSyntax invocation)
        {
            return invocation.ArgumentList.Arguments.Count == 1
                && invocation.ArgumentList.Arguments[0].Expression is SimpleLambdaExpressionSyntax;
        }

        private static bool IsAttachedCallback(string methodName)
        {
            return AttachedCallbackNames.Contains(methodName);
        }

        private static ImmutableArray<DslPropertyCall> ExtractCallbackProperties(
            InvocationExpressionSyntax invocation,
            SemanticModel semanticModel)
        {
            if (invocation.ArgumentList.Arguments[0].Expression is not SimpleLambdaExpressionSyntax lambda
                || lambda.ExpressionBody is not InvocationExpressionSyntax bodyInvocation)
            {
                return ImmutableArray<DslPropertyCall>.Empty;
            }

            ImmutableArray<InvocationExpressionSyntax> chain = FlattenInvocationChain(bodyInvocation);
            ImmutableArray<DslPropertyCall>.Builder properties = ImmutableArray.CreateBuilder<DslPropertyCall>();

            foreach (InvocationExpressionSyntax call in chain)
            {
                if (call.ArgumentList.Arguments.Count == 0)
                {
                    continue;
                }

                string methodName = GetInvocationName(call);
                object? value = ExtractArgumentValue(call.ArgumentList.Arguments[0].Expression, semanticModel);
                properties.Add(new DslPropertyCall(methodName, value, ToSourceLocation(call)));
            }

            return properties.ToImmutable();
        }

        private static DslSignalHandlerCall ExtractSignalHandler(
            string methodName,
            InvocationExpressionSyntax invocation,
            SemanticModel semanticModel,
            SourceLocation? location)
        {
            if (invocation.ArgumentList.Arguments.Count == 0)
            {
                return new DslSignalHandlerCall(methodName, string.Empty, location);
            }

            ExpressionSyntax expression = invocation.ArgumentList.Arguments[0].Expression;
            if (expression is SimpleLambdaExpressionSyntax lambda)
            {
                string body = lambda.ExpressionBody?.ToString()
                    ?? lambda.Block?.ToString()
                    ?? string.Empty;
                return new DslSignalHandlerCall(methodName, body, location);
            }

            if (expression is ParenthesizedLambdaExpressionSyntax parenthesizedLambda)
            {
                string body = parenthesizedLambda.ExpressionBody?.ToString()
                    ?? parenthesizedLambda.Block?.ToString()
                    ?? string.Empty;
                return new DslSignalHandlerCall(methodName, body, location);
            }

            if (TryCreateCommandReference(expression, out DslCommandReference? commandReference)
                && commandReference is not null)
            {
                string body = $"{commandReference.ReceiverName}.{ToCamelCase(commandReference.MethodName)}()";
                return new DslSignalHandlerCall(methodName, body, location, commandReference);
            }

            object? value = ExtractArgumentValue(expression, semanticModel);
            return new DslSignalHandlerCall(methodName, value as string ?? expression.ToString(), location);
        }

        private static object? ExtractArgumentValue(ExpressionSyntax expression, SemanticModel semanticModel)
        {
            if (expression is MemberAccessExpressionSyntax memberAccess)
            {
                return ExtractMemberAccessValue(memberAccess);
            }

            Optional<object?> constant = semanticModel.GetConstantValue(expression);
            if (constant.HasValue)
            {
                return constant.Value;
            }

            return expression switch
            {
                LiteralExpressionSyntax literal when literal.Token.Value is null => null,
                InvocationExpressionSyntax invocation => new DslTransformer().ExtractCallTree(invocation, semanticModel),
                IdentifierNameSyntax identifier => new DslStateReference(string.Empty, identifier.Identifier.ValueText, ToSourceLocation(identifier)),
                _ => expression.ToString(),
            };
        }

        private static object ExtractMemberAccessValue(MemberAccessExpressionSyntax memberAccess)
        {
            if (memberAccess.Expression is IdentifierNameSyntax receiver)
            {
                return new DslStateReference(receiver.Identifier.ValueText, memberAccess.Name.Identifier.ValueText, ToSourceLocation(memberAccess));
            }

            if (memberAccess.Expression is MemberAccessExpressionSyntax nested
                && nested.Expression is IdentifierNameSyntax enumOwner)
            {
                return new DslEnumReference(
                    enumOwner.Identifier.ValueText,
                    nested.Name.Identifier.ValueText,
                    memberAccess.Name.Identifier.ValueText,
                    ToSourceLocation(memberAccess));
            }

            return new DslEnumReference(string.Empty, null, memberAccess.ToString(), ToSourceLocation(memberAccess));
        }

        private static bool TryCreateCommandReference(ExpressionSyntax expression, out DslCommandReference? commandReference)
        {
            if (expression is MemberAccessExpressionSyntax memberAccess
                && memberAccess.Expression is IdentifierNameSyntax receiver)
            {
                commandReference = new DslCommandReference(
                    receiver.Identifier.ValueText,
                    memberAccess.Name.Identifier.ValueText,
                    ToSourceLocation(memberAccess));
                return true;
            }

            commandReference = null;
            return false;
        }

        private static string? TryGetStringArgument(InvocationExpressionSyntax invocation)
        {
            if (invocation.ArgumentList.Arguments.Count != 1)
            {
                return null;
            }

            ExpressionSyntax expression = invocation.ArgumentList.Arguments[0].Expression;
            return expression is LiteralExpressionSyntax literal && literal.Token.Value is string value
                ? value
                : null;
        }

        private static ObjectDefinitionNode ToAstNode(
            DslCallNode callNode,
            IRegistryQuery registry,
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics,
            ImmutableArray<SourceMapping>.Builder mappings)
        {
            QmlType? type = FindType(registry, callNode.TypeName);
            if (type is null)
            {
                diagnostics.Add(CreateDiagnostic(
                    DiagnosticCodes.UnknownQmlType,
                    $"Type '{callNode.TypeName}' could not be resolved.",
                    callNode.SourceLocation));
            }

            ImmutableArray<AstNode>.Builder members = ImmutableArray.CreateBuilder<AstNode>();
            AddSourceMapping(mappings, callNode.SourceLocation, callNode.TypeName, NodeKind.ObjectDefinition);

            if (!string.IsNullOrWhiteSpace(callNode.Id))
            {
                members.Add(new IdAssignmentNode { Id = callNode.Id });
            }

            if (type is not null)
            {
                AddPropertyMembers(callNode, type, registry, diagnostics, mappings, members);
                AddBindingMembers(callNode, type, registry, diagnostics, mappings, members);
                AddSignalMembers(callNode, type, registry, diagnostics, mappings, members);
                AddGroupedMembers(callNode, type, registry, diagnostics, mappings, members);
                AddAttachedMembers(callNode, registry, diagnostics, mappings, members);
            }

            foreach (DslCallNode child in callNode.Children)
            {
                if (!IsLikelyQmlTypeName(child.TypeName))
                {
                    diagnostics.Add(CreateDiagnostic(
                        DiagnosticCodes.InvalidChildType,
                        $"Child type '{child.TypeName}' is not a valid QML object type reference.",
                        child.SourceLocation));
                    continue;
                }

                ObjectDefinitionNode childNode = ToAstNode(child, registry, diagnostics, mappings);
                members.Add(childNode);
            }

            return new ObjectDefinitionNode
            {
                TypeName = callNode.TypeName,
                Members = members.ToImmutable(),
            };
        }

        private static void AddPropertyMembers(
            DslCallNode callNode,
            QmlType type,
            IRegistryQuery registry,
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics,
            ImmutableArray<SourceMapping>.Builder mappings,
            ImmutableArray<AstNode>.Builder members)
        {
            foreach (DslPropertyCall property in callNode.Properties)
            {
                string qmlName = ToCamelCase(property.Name);
                ResolvedProperty? resolvedProperty = registry.FindProperty(type.QualifiedName, qmlName);
                if (resolvedProperty is null)
                {
                    diagnostics.Add(CreateDiagnostic(
                        DiagnosticCodes.InvalidPropertyValue,
                        $"Property '{qmlName}' does not exist on '{callNode.TypeName}'.",
                        property.SourceLocation));
                    continue;
                }

                if (resolvedProperty.Property.IsReadonly)
                {
                    diagnostics.Add(CreateDiagnostic(
                        DiagnosticCodes.InvalidPropertyValue,
                        $"Property '{qmlName}' is readonly.",
                        property.SourceLocation));
                    continue;
                }

                if (TryReportUnresolvedReference(property.Value, property.SourceLocation, diagnostics))
                {
                    continue;
                }

                BindingValue? value = ToBindingValue(property.Value);
                if (value is null)
                {
                    diagnostics.Add(CreateDiagnostic(
                        DiagnosticCodes.InvalidPropertyValue,
                        $"Property '{qmlName}' uses an unsupported value expression.",
                        property.SourceLocation));
                    continue;
                }

                if (!IsValueCompatible(value, resolvedProperty.Property.TypeName))
                {
                    diagnostics.Add(CreateDiagnostic(
                        DiagnosticCodes.PropertyTypeMismatch,
                        $"Property '{qmlName}' expects '{resolvedProperty.Property.TypeName}'.",
                        property.SourceLocation));
                    continue;
                }

                members.Add(new BindingNode { PropertyName = qmlName, Value = value });
                AddSourceMapping(mappings, property.SourceLocation, qmlName, NodeKind.Binding);
            }
        }

        private static void AddBindingMembers(
            DslCallNode callNode,
            QmlType type,
            IRegistryQuery registry,
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics,
            ImmutableArray<SourceMapping>.Builder mappings,
            ImmutableArray<AstNode>.Builder members)
        {
            foreach (DslBindingCall binding in callNode.Bindings)
            {
                string qmlName = ToCamelCase(binding.Name);
                ResolvedProperty? resolvedProperty = registry.FindProperty(type.QualifiedName, qmlName);
                if (string.IsNullOrWhiteSpace(binding.Expression))
                {
                    diagnostics.Add(CreateDiagnostic(
                        DiagnosticCodes.BindExpressionEmpty,
                        $"Binding expression for '{qmlName}' cannot be empty.",
                        binding.SourceLocation));
                    continue;
                }

                if (resolvedProperty is null)
                {
                    diagnostics.Add(CreateDiagnostic(
                        DiagnosticCodes.InvalidPropertyValue,
                        $"Property '{qmlName}' does not exist on '{callNode.TypeName}'.",
                        binding.SourceLocation));
                    continue;
                }

                if (resolvedProperty.Property.IsReadonly)
                {
                    diagnostics.Add(CreateDiagnostic(
                        DiagnosticCodes.InvalidPropertyValue,
                        $"Property '{qmlName}' is readonly.",
                        binding.SourceLocation));
                    continue;
                }

                members.Add(new BindingNode { PropertyName = qmlName, Value = Values.Expression(binding.Expression) });
                AddSourceMapping(mappings, binding.SourceLocation, qmlName, NodeKind.Binding);
            }
        }

        private static void AddSignalMembers(
            DslCallNode callNode,
            QmlType type,
            IRegistryQuery registry,
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics,
            ImmutableArray<SourceMapping>.Builder mappings,
            ImmutableArray<AstNode>.Builder members)
        {
            foreach (DslSignalHandlerCall signal in callNode.SignalHandlers)
            {
                string signalName = SignalNameFromHandler(signal.Name);
                if (registry.FindSignal(type.QualifiedName, signalName) is null)
                {
                    diagnostics.Add(CreateDiagnostic(
                        DiagnosticCodes.UnknownSignal,
                        $"Signal '{signalName}' does not exist on '{callNode.TypeName}'.",
                        signal.SourceLocation));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(signal.Body))
                {
                    diagnostics.Add(CreateDiagnostic(
                        DiagnosticCodes.UnsupportedDslPattern,
                        $"Signal handler '{signal.Name}' has no supported body.",
                        signal.SourceLocation));
                    continue;
                }

                members.Add(new SignalHandlerNode
                {
                    HandlerName = HandlerNameFromSignal(signalName),
                    Form = SignalHandlerForm.Block,
                    Code = signal.Body,
                });
                AddSourceMapping(mappings, signal.SourceLocation, signalName, NodeKind.SignalHandler);
            }
        }

        private static void AddGroupedMembers(
            DslCallNode callNode,
            QmlType type,
            IRegistryQuery registry,
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics,
            ImmutableArray<SourceMapping>.Builder mappings,
            ImmutableArray<AstNode>.Builder members)
        {
            foreach (DslGroupedCall grouped in callNode.GroupedProperties)
            {
                string groupName = ToCamelCase(grouped.Name);
                if (registry.FindProperty(type.QualifiedName, groupName) is null)
                {
                    diagnostics.Add(CreateDiagnostic(
                        DiagnosticCodes.InvalidPropertyValue,
                        $"Grouped property '{groupName}' does not exist on '{callNode.TypeName}'.",
                        grouped.SourceLocation));
                    continue;
                }

                ImmutableArray<BindingNode> bindings = grouped.Properties
                    .Select(property => new BindingNode
                    {
                        PropertyName = ToCamelCase(property.Name),
                        Value = ToBindingValue(property.Value) ?? Values.Expression(property.Value?.ToString() ?? string.Empty),
                    })
                    .ToImmutableArray();

                members.Add(new GroupedBindingNode { GroupName = groupName, Bindings = bindings });
                AddSourceMapping(mappings, grouped.SourceLocation, groupName, NodeKind.GroupedBinding);
            }
        }

        private static void AddAttachedMembers(
            DslCallNode callNode,
            IRegistryQuery registry,
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics,
            ImmutableArray<SourceMapping>.Builder mappings,
            ImmutableArray<AstNode>.Builder members)
        {
            foreach (DslAttachedCall attached in callNode.AttachedProperties)
            {
                if (FindType(registry, attached.TypeName) is null)
                {
                    diagnostics.Add(CreateDiagnostic(
                        DiagnosticCodes.UnknownAttachedType,
                        $"Attached type '{attached.TypeName}' could not be resolved.",
                        attached.SourceLocation));
                    continue;
                }

                ImmutableArray<BindingNode> bindings = attached.Properties
                    .Select(property => new BindingNode
                    {
                        PropertyName = ToCamelCase(property.Name),
                        Value = ToBindingValue(property.Value) ?? Values.Expression(property.Value?.ToString() ?? string.Empty),
                    })
                    .ToImmutableArray();

                members.Add(new AttachedBindingNode { AttachedTypeName = attached.TypeName, Bindings = bindings });
                AddSourceMapping(mappings, attached.SourceLocation, attached.TypeName, NodeKind.AttachedBinding);
            }
        }

        private static BindingValue? ToBindingValue(object? value)
        {
            return value switch
            {
                null => Values.Null(),
                int number => Values.Number(number),
                long number => Values.Number(number),
                double number => Values.Number(number),
                float number => Values.Number(number),
                decimal number => Values.Number(decimal.ToDouble(number)),
                string text => Values.String(text),
                bool flag => Values.Boolean(flag),
                DslCallNode objectNode => Values.Object(ToAstNodeWithoutValidation(objectNode)),
                DslStateReference stateReference => Values.Expression(FormatStateReference(stateReference)),
                DslEnumReference enumReference => Values.Enum(
                    string.IsNullOrWhiteSpace(enumReference.EnumName)
                        ? enumReference.TypeName
                        : $"{enumReference.TypeName}.{enumReference.EnumName}",
                    enumReference.MemberName),
                _ => null,
            };
        }

        private static bool TryReportUnresolvedReference(
            object? value,
            SourceLocation? location,
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics)
        {
            if (value is DslEnumReference enumReference && string.IsNullOrWhiteSpace(enumReference.TypeName))
            {
                diagnostics.Add(CreateDiagnostic(
                    DiagnosticCodes.UnresolvedTypeReference,
                    $"Enum reference '{enumReference.MemberName}' could not be resolved to an owner type.",
                    location));
                return true;
            }

            return false;
        }

        private static bool IsLikelyQmlTypeName(string typeName)
        {
            return !string.IsNullOrWhiteSpace(typeName) && char.IsUpper(typeName[0]);
        }

        private static ObjectDefinitionNode ToAstNodeWithoutValidation(DslCallNode callNode)
        {
            ImmutableArray<AstNode>.Builder members = ImmutableArray.CreateBuilder<AstNode>();
            foreach (DslPropertyCall property in callNode.Properties)
            {
                BindingValue? value = ToBindingValue(property.Value);
                if (value is not null)
                {
                    members.Add(new BindingNode { PropertyName = ToCamelCase(property.Name), Value = value });
                }
            }

            foreach (DslCallNode child in callNode.Children)
            {
                members.Add(ToAstNodeWithoutValidation(child));
            }

            return new ObjectDefinitionNode { TypeName = callNode.TypeName, Members = members.ToImmutable() };
        }

        private static bool IsValueCompatible(BindingValue value, string qmlType)
        {
            if (value is ScriptExpression or EnumReference or ObjectValue or NullLiteral)
            {
                return true;
            }

            string normalizedType = qmlType.ToLowerInvariant();
            return value switch
            {
                NumberLiteral => normalizedType is "int" or "double" or "real" or "number",
                StringLiteral => normalizedType is "string" or "url" or "color" or "font" or "var" or "variant",
                BooleanLiteral => normalizedType is "bool" or "boolean",
                _ => true,
            };
        }

        private static string FormatStateReference(DslStateReference stateReference)
        {
            string memberName = ToCamelCase(stateReference.MemberName);
            return string.IsNullOrWhiteSpace(stateReference.ReceiverName)
                ? memberName
                : $"{stateReference.ReceiverName}.{memberName}";
        }

        private static QmlType? FindType(IRegistryQuery registry, string typeName)
        {
            return registry.FindTypes(type =>
                    StringComparer.Ordinal.Equals(type.QmlName, typeName)
                    || StringComparer.Ordinal.Equals(type.QualifiedName, typeName)
                    || StringComparer.Ordinal.Equals(type.QualifiedName.Split('.').Last(), typeName))
                .OrderBy(static type => type.ModuleUri, StringComparer.Ordinal)
                .ThenBy(static type => type.QualifiedName, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private static InvocationExpressionSyntax? FindBuildReturnInvocation(DiscoveredView view)
        {
            foreach (SyntaxReference syntaxReference in view.TypeSymbol.DeclaringSyntaxReferences)
            {
                if (syntaxReference.GetSyntax() is not ClassDeclarationSyntax classDeclaration)
                {
                    continue;
                }

                MethodDeclarationSyntax? buildMethod = classDeclaration.Members
                    .OfType<MethodDeclarationSyntax>()
                    .Where(method => StringComparer.Ordinal.Equals(method.Identifier.ValueText, "Build"))
                    .FirstOrDefault();

                if (buildMethod?.ExpressionBody?.Expression is InvocationExpressionSyntax expressionInvocation)
                {
                    return expressionInvocation;
                }

                ReturnStatementSyntax? returnStatement = buildMethod?.Body?.Statements
                    .OfType<ReturnStatementSyntax>()
                    .FirstOrDefault();

                if (returnStatement?.Expression is InvocationExpressionSyntax returnInvocation)
                {
                    return returnInvocation;
                }
            }

            return null;
        }

        private static DslCallNode CreateInvalidCallNode(SyntaxNode node)
        {
            return new DslCallNode(
                "Invalid",
                ImmutableArray<DslPropertyCall>.Empty,
                ImmutableArray<DslBindingCall>.Empty,
                ImmutableArray<DslSignalHandlerCall>.Empty,
                ImmutableArray<DslGroupedCall>.Empty,
                ImmutableArray<DslAttachedCall>.Empty,
                ImmutableArray<DslCallNode>.Empty,
                SourceLocation: ToSourceLocation(node));
        }

        private static CompilerDiagnostic CreateDiagnostic(string code, string details, SourceLocation? location)
        {
            return new CompilerDiagnostic(
                code,
                DiagnosticSeverity.Error,
                DiagnosticMessageCatalog.FormatMessage(code, details),
                location,
                TransformPhase);
        }

        private static void AddSourceMapping(
            ImmutableArray<SourceMapping>.Builder mappings,
            SourceLocation? source,
            string symbol,
            NodeKind nodeKind)
        {
            if (source is null)
            {
                return;
            }

            mappings.Add(new SourceMapping(
                source,
                SourceLocation.LineColumn(1, 1),
                symbol,
                nodeKind.ToString()));
        }

        private static SourceLocation? ToSourceLocation(SyntaxNode node)
        {
            Location location = node.GetLocation();
            if (!location.IsInSource)
            {
                return null;
            }

            FileLinePositionSpan lineSpan = location.GetLineSpan();
            return SourceLocation.Partial(
                lineSpan.Path,
                lineSpan.StartLinePosition.Line + 1,
                lineSpan.StartLinePosition.Character + 1);
        }

        private static string SignalNameFromHandler(string handlerName)
        {
            string withoutPrefix = handlerName.StartsWith("On", StringComparison.Ordinal) && handlerName.Length > 2
                ? handlerName[2..]
                : handlerName;

            return ToCamelCase(withoutPrefix);
        }

        private static string HandlerNameFromSignal(string signalName)
        {
            if (string.IsNullOrEmpty(signalName))
            {
                return "on";
            }

            return $"on{char.ToUpperInvariant(signalName[0])}{signalName[1..]}";
        }

        private static string ToCamelCase(string name)
        {
            if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
            {
                return name;
            }

            return $"{char.ToLowerInvariant(name[0])}{name[1..]}";
        }
    }
}

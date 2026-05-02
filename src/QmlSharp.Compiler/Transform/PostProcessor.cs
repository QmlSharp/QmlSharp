using System.Text.RegularExpressions;
using QmlSharp.Qml.Ast;

namespace QmlSharp.Compiler
{
    /// <summary>Turns the raw DSL AST into the V2 ViewModel-backed QML shape.</summary>
    public sealed class PostProcessor : IPostProcessor
    {
        private const string Phase = "postprocess";
        private const string EffectRouterMarker = "__qmlsharp_effect_router";
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(200);
        private static readonly Regex ExplicitViewModelMemberPattern = new(
            @"\b(?<receiver>Vm|vm)\.(?<member>[A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.CultureInvariant | RegexOptions.Compiled,
            RegexTimeout);

        /// <inheritdoc/>
        public PostProcessResult Process(
            QmlDocument document,
            DiscoveredView view,
            ViewModelSchema schema,
            ImmutableArray<ResolvedImport> imports,
            CompilerOptions options)
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentNullException.ThrowIfNull(view);
            ArgumentNullException.ThrowIfNull(schema);
            ArgumentNullException.ThrowIfNull(options);

            ImmutableArray<CompilerDiagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<CompilerDiagnostic>();
            ImmutableArray<InjectedNode>.Builder injectedNodes = ImmutableArray.CreateBuilder<InjectedNode>();
            string viewModelId = GetViewModelId(schema);
            SchemaLookup lookup = SchemaLookup.Create(schema);

            ObjectDefinitionNode rewrittenRoot = RewriteObject(document.RootObject, viewModelId, lookup, diagnostics);
            ObjectDefinitionNode rootWithViewModel = EnsureViewModelInstance(rewrittenRoot, schema, viewModelId, diagnostics, injectedNodes);
            ObjectDefinitionNode rootWithEffects = EnsureEffectRouting(rootWithViewModel, schema, viewModelId, diagnostics, injectedNodes);
            ObjectDefinitionNode rootWithLifecycle = EnsureLifecycleHooks(rootWithEffects, schema, viewModelId, injectedNodes);

            ImmutableArray<ImportNode> resolvedImports = ResolveImports(
                document.Imports,
                imports,
                schema,
                needsQtQmlImport: schema.Lifecycle.OnMounted || schema.Lifecycle.OnUnmounting || schema.Effects.Length > 0,
                diagnostics,
                injectedNodes);

            QmlDocument processed = document with
            {
                Imports = resolvedImports,
                RootObject = rootWithLifecycle,
            };

            return new PostProcessResult(processed, injectedNodes.ToImmutable(), diagnostics.ToImmutable());
        }

        private static ObjectDefinitionNode RewriteObject(
            ObjectDefinitionNode node,
            string viewModelId,
            SchemaLookup lookup,
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics)
        {
            ImmutableArray<AstNode>.Builder members = ImmutableArray.CreateBuilder<AstNode>(node.Members.Length);
            foreach (AstNode member in node.Members)
            {
                members.Add(RewriteNode(member, viewModelId, lookup, diagnostics));
            }

            return node with { Members = members.ToImmutable() };
        }

        private static AstNode RewriteNode(
            AstNode node,
            string viewModelId,
            SchemaLookup lookup,
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics)
        {
            return node switch
            {
                BindingNode binding => binding with
                {
                    Value = RewriteBindingValue(binding.Value, viewModelId, lookup, diagnostics),
                },
                GroupedBindingNode grouped => grouped with
                {
                    Bindings = grouped.Bindings.Select(binding => (BindingNode)RewriteNode(binding, viewModelId, lookup, diagnostics)).ToImmutableArray(),
                },
                AttachedBindingNode attached => attached with
                {
                    Bindings = attached.Bindings.Select(binding => (BindingNode)RewriteNode(binding, viewModelId, lookup, diagnostics)).ToImmutableArray(),
                },
                ArrayBindingNode array => array with
                {
                    Elements = array.Elements.Select(value => RewriteBindingValue(value, viewModelId, lookup, diagnostics)).ToImmutableArray(),
                },
                BehaviorOnNode behavior => behavior with
                {
                    Animation = RewriteObject(behavior.Animation, viewModelId, lookup, diagnostics),
                },
                SignalHandlerNode handler => RewriteSignalHandler(handler, viewModelId, lookup, diagnostics),
                ObjectDefinitionNode child => RewriteObject(child, viewModelId, lookup, diagnostics),
                InlineComponentNode inline => inline with
                {
                    Body = RewriteObject(inline.Body, viewModelId, lookup, diagnostics),
                },
                PropertyDeclarationNode property when property.InitialValue is not null => property with
                {
                    InitialValue = RewriteBindingValue(property.InitialValue, viewModelId, lookup, diagnostics),
                },
                _ => node,
            };
        }

        private static BindingValue RewriteBindingValue(
            BindingValue value,
            string viewModelId,
            SchemaLookup lookup,
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics)
        {
            return value switch
            {
                ScriptExpression expression => expression with
                {
                    Code = RewriteStateExpression(expression.Code, viewModelId, lookup, diagnostics),
                },
                ScriptBlock block => block with
                {
                    Code = RewriteStateExpression(block.Code, viewModelId, lookup, diagnostics),
                },
                ObjectValue objectValue => objectValue with
                {
                    Object = RewriteObject(objectValue.Object, viewModelId, lookup, diagnostics),
                },
                ArrayValue array => array with
                {
                    Elements = array.Elements.Select(element => RewriteBindingValue(element, viewModelId, lookup, diagnostics)).ToImmutableArray(),
                },
                _ => value,
            };
        }

        private static SignalHandlerNode RewriteSignalHandler(
            SignalHandlerNode handler,
            string viewModelId,
            SchemaLookup lookup,
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics)
        {
            string code = handler.Code;
            foreach (Match match in ExplicitViewModelMemberPattern.Matches(handler.Code))
            {
                string memberName = match.Groups["member"].Value;
                string? commandName = lookup.FindCommand(memberName);
                if (commandName is null)
                {
                    diagnostics.Add(CreateDiagnostic(
                        DiagnosticCodes.CommandTargetNotFound,
                        $"Command '{memberName}' is not declared by the ViewModel schema."));
                    continue;
                }

                code = ReplaceFirst(code, match.Value, $"{viewModelId}.{commandName}");
            }

            return handler with { Code = code };
        }

        private static string RewriteStateExpression(
            string code,
            string viewModelId,
            SchemaLookup lookup,
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics)
        {
            string rewritten = code;
            foreach (Match match in ExplicitViewModelMemberPattern.Matches(code))
            {
                string memberName = match.Groups["member"].Value;
                string? propertyName = lookup.FindProperty(memberName);
                if (propertyName is null)
                {
                    diagnostics.Add(CreateDiagnostic(
                        DiagnosticCodes.BindingTargetNotFound,
                        $"State property '{memberName}' is not declared by the ViewModel schema."));
                    continue;
                }

                rewritten = ReplaceFirst(rewritten, match.Value, $"{viewModelId}.{propertyName}");
            }

            foreach (string propertyName in lookup.PropertyNames)
            {
                string pattern = $@"(?<![\w.]){Regex.Escape(propertyName)}\b";
                rewritten = Regex.Replace(
                    rewritten,
                    pattern,
                    $"{viewModelId}.{propertyName}",
                    RegexOptions.CultureInvariant,
                    RegexTimeout);
            }

            return rewritten;
        }

        private static ObjectDefinitionNode EnsureViewModelInstance(
            ObjectDefinitionNode root,
            ViewModelSchema schema,
            string viewModelId,
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics,
            ImmutableArray<InjectedNode>.Builder injectedNodes)
        {
            ImmutableArray<AstNode>.Builder members = ImmutableArray.CreateBuilder<AstNode>();
            ImmutableArray<ObjectDefinitionNode> viewModelInstances = root.Members
                .OfType<ObjectDefinitionNode>()
                .Where(child => string.Equals(child.TypeName, schema.ClassName, StringComparison.Ordinal))
                .ToImmutableArray();
            int matchingIdCount = CountId(root, viewModelId);

            if (matchingIdCount > 1)
            {
                diagnostics.Add(CreateDiagnostic(
                    DiagnosticCodes.SlotKeyCollision,
                    $"Slot id '{viewModelId}' appears more than once."));
            }

            ObjectDefinitionNode? existing = viewModelInstances.FirstOrDefault(child => HasId(child, viewModelId));
            if (existing is null)
            {
                ObjectDefinitionNode? conflictingInstance = viewModelInstances.FirstOrDefault();
                if (conflictingInstance is not null)
                {
                    diagnostics.Add(CreateDiagnostic(
                        DiagnosticCodes.ViewModelInstanceConflict,
                        $"Existing '{schema.ClassName}' instance does not use generated id '{viewModelId}'."));
                }

                ObjectDefinitionNode generated = new()
                {
                    TypeName = schema.ClassName,
                    Members = ImmutableArray.Create<AstNode>(new IdAssignmentNode { Id = viewModelId }),
                };
                members.Add(generated);
                injectedNodes.Add(new InjectedNode("ViewModelInstance", $"{schema.ClassName} {viewModelId}"));
            }

            members.AddRange(root.Members);
            return root with { Members = members.ToImmutable() };
        }

        private static ObjectDefinitionNode EnsureEffectRouting(
            ObjectDefinitionNode root,
            ViewModelSchema schema,
            string viewModelId,
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics,
            ImmutableArray<InjectedNode>.Builder injectedNodes)
        {
            if (schema.Effects.IsDefaultOrEmpty)
            {
                return root;
            }

            if (HasGeneratedEffectRouter(root, viewModelId))
            {
                return root;
            }

            if (HasConflictingEffectHandler(root, viewModelId))
            {
                diagnostics.Add(CreateDiagnostic(
                    DiagnosticCodes.EffectHandlerConflict,
                    $"Existing effect routing for '{viewModelId}' conflicts with generated V2 routing."));
                return root;
            }

            ImmutableArray<AstNode>.Builder members = ImmutableArray.CreateBuilder<AstNode>();
            members.AddRange(root.Members);

            members.Add(new ObjectDefinitionNode
            {
                TypeName = "Connections",
                Members = ImmutableArray.Create<AstNode>(
                    new BindingNode
                    {
                        PropertyName = "target",
                        Value = Values.Expression(viewModelId),
                    },
                    new FunctionDeclarationNode
                    {
                        Name = "onEffectDispatched",
                        Parameters = ImmutableArray.Create(
                            new ParameterDeclaration("effectName", "string"),
                            new ParameterDeclaration("payloadJson", "string")),
                        Body = CreateEffectRouterBody(schema),
                    }),
            });

            injectedNodes.Add(new InjectedNode("EffectHandler", $"{viewModelId}.effectDispatched"));
            return root with { Members = members.ToImmutable() };
        }

        private static ObjectDefinitionNode EnsureLifecycleHooks(
            ObjectDefinitionNode root,
            ViewModelSchema schema,
            string viewModelId,
            ImmutableArray<InjectedNode>.Builder injectedNodes)
        {
            if (!schema.Lifecycle.OnMounted && !schema.Lifecycle.OnUnmounting)
            {
                return root;
            }

            ImmutableArray<BindingNode>.Builder lifecycleBindings = ImmutableArray.CreateBuilder<BindingNode>();
            if (schema.Lifecycle.OnMounted)
            {
                lifecycleBindings.Add(new BindingNode
                {
                    PropertyName = "onCompleted",
                    Value = Values.Block($"{viewModelId}.onMounted();"),
                });
            }

            if (schema.Lifecycle.OnUnmounting)
            {
                lifecycleBindings.Add(new BindingNode
                {
                    PropertyName = "onDestruction",
                    Value = Values.Block($"{viewModelId}.onUnmounting();"),
                });
            }

            ImmutableArray<AstNode>.Builder members = ImmutableArray.CreateBuilder<AstNode>();
            bool merged = false;
            foreach (AstNode member in root.Members)
            {
                if (member is AttachedBindingNode attached && string.Equals(attached.AttachedTypeName, "Component", StringComparison.Ordinal))
                {
                    members.Add(attached with { Bindings = MergeBindings(attached.Bindings, lifecycleBindings.ToImmutable()) });
                    merged = true;
                }
                else
                {
                    members.Add(member);
                }
            }

            if (!merged)
            {
                members.Add(new AttachedBindingNode
                {
                    AttachedTypeName = "Component",
                    Bindings = lifecycleBindings.ToImmutable(),
                });
            }

            injectedNodes.Add(new InjectedNode("LifecycleHook", viewModelId));
            return root with { Members = members.ToImmutable() };
        }

        private static ImmutableArray<ImportNode> ResolveImports(
            ImmutableArray<ImportNode> originalImports,
            ImmutableArray<ResolvedImport> resolvedImports,
            ViewModelSchema schema,
            bool needsQtQmlImport,
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics,
            ImmutableArray<InjectedNode>.Builder injectedNodes)
        {
            Dictionary<string, ImportEntry> imports = new(StringComparer.Ordinal);

            foreach (ImportNode import in originalImports)
            {
                AddImport(imports, import, diagnostics, injectedNodes, isInjected: false);
            }

            foreach (ResolvedImport resolved in resolvedImports.OrderBy(import => import.QmlModuleUri, StringComparer.Ordinal).ThenBy(import => import.Alias, StringComparer.Ordinal))
            {
                AddImport(imports, CreateModuleImport(resolved.QmlModuleUri, resolved.Version, resolved.Alias), diagnostics, injectedNodes, isInjected: true);
            }

            AddImport(imports, CreateModuleImport(schema.ModuleUri, schema.ModuleVersion, alias: null), diagnostics, injectedNodes, isInjected: true);

            if (needsQtQmlImport)
            {
                AddImport(imports, CreateModuleImport("QtQml", schema.ModuleVersion, alias: null), diagnostics, injectedNodes, isInjected: true);
            }

            return imports.Values
                .OrderBy(entry => entry.Import.ImportKind)
                .ThenBy(entry => entry.DisplayName, StringComparer.Ordinal)
                .ThenBy(entry => entry.Import.Qualifier, StringComparer.Ordinal)
                .Select(entry => entry.Import)
                .ToImmutableArray();
        }

        private static void AddImport(
            Dictionary<string, ImportEntry> imports,
            ImportNode import,
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics,
            ImmutableArray<InjectedNode>.Builder injectedNodes,
            bool isInjected)
        {
            string displayName = GetImportDisplayName(import);
            string key = CreateImportKey(import, displayName);
            if (imports.TryGetValue(key, out ImportEntry? existing))
            {
                if (!StringComparer.Ordinal.Equals(existing.Import.Version, import.Version))
                {
                    diagnostics.Add(CreateDiagnostic(
                        DiagnosticCodes.ImportConflict,
                        $"Import '{displayName}' has conflicting versions '{existing.Import.Version}' and '{import.Version}'."));
                }

                return;
            }

            imports.Add(key, new ImportEntry(displayName, import));
            if (isInjected)
            {
                injectedNodes.Add(new InjectedNode("ImportStatement", displayName));
            }
        }

        private static ImportNode CreateModuleImport(string moduleUri, QmlVersion version, string? alias)
        {
            return new ImportNode
            {
                ImportKind = ImportKind.Module,
                ModuleUri = moduleUri,
                Version = $"{version.Major}.{version.Minor}",
                Qualifier = alias,
            };
        }

        private static ImmutableArray<BindingNode> MergeBindings(
            ImmutableArray<BindingNode> existing,
            ImmutableArray<BindingNode> additions)
        {
            ImmutableArray<BindingNode>.Builder merged = ImmutableArray.CreateBuilder<BindingNode>();
            merged.AddRange(existing);

            foreach (BindingNode addition in additions)
            {
                bool alreadyPresent = existing.Any(binding =>
                    string.Equals(binding.PropertyName, addition.PropertyName, StringComparison.Ordinal)
                    && string.Equals(BindingValueCode(binding.Value), BindingValueCode(addition.Value), StringComparison.Ordinal));
                if (!alreadyPresent)
                {
                    merged.Add(addition);
                }
            }

            return merged.ToImmutable();
        }

        private static string BindingValueCode(BindingValue value)
        {
            return value switch
            {
                ScriptExpression expression => expression.Code,
                ScriptBlock block => block.Code,
                StringLiteral literal => literal.Value,
                NumberLiteral literal => literal.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                BooleanLiteral literal => literal.Value ? "true" : "false",
                NullLiteral => "null",
                EnumReference enumReference => $"{enumReference.TypeName}.{enumReference.MemberName}",
                _ => value.ToString() ?? string.Empty,
            };
        }

        private static string CreateEffectRouterBody(ViewModelSchema schema)
        {
            ImmutableArray<string> effectNames = schema.Effects.Select(effect => effect.Name).Order(StringComparer.Ordinal).ToImmutableArray();
            string cases = string.Join(
                Environment.NewLine,
                effectNames.Select(effectName => $"        case \"{effectName}\":{Environment.NewLine}            break;"));
            return $"{EffectRouterMarker}{Environment.NewLine}switch (effectName) {{{Environment.NewLine}{cases}{Environment.NewLine}}}";
        }

        private static bool HasGeneratedEffectRouter(ObjectDefinitionNode root, string viewModelId)
        {
            return root.Members.OfType<ObjectDefinitionNode>().Any(child =>
                string.Equals(child.TypeName, "Connections", StringComparison.Ordinal)
                && HasTargetBinding(child, viewModelId)
                && child.Members.OfType<FunctionDeclarationNode>().Any(function =>
                    string.Equals(function.Name, "onEffectDispatched", StringComparison.Ordinal) && function.Body.Contains(EffectRouterMarker, StringComparison.Ordinal)));
        }

        private static bool HasConflictingEffectHandler(ObjectDefinitionNode root, string viewModelId)
        {
            return root.Members.OfType<ObjectDefinitionNode>().Any(child =>
                string.Equals(child.TypeName, "Connections", StringComparison.Ordinal)
                && HasTargetBinding(child, viewModelId)
                && (child.Members.OfType<FunctionDeclarationNode>().Any(function => string.Equals(function.Name, "onEffectDispatched", StringComparison.Ordinal))
                    || child.Members.OfType<SignalHandlerNode>().Any(handler => string.Equals(handler.HandlerName, "onEffectDispatched", StringComparison.Ordinal))));
        }

        private static bool HasTargetBinding(ObjectDefinitionNode node, string viewModelId)
        {
            return node.Members.OfType<BindingNode>().Any(binding =>
                string.Equals(binding.PropertyName, "target", StringComparison.Ordinal)
                && binding.Value is ScriptExpression expression
                && string.Equals(expression.Code, viewModelId, StringComparison.Ordinal));
        }

        private static bool HasId(ObjectDefinitionNode node, string id)
        {
            return node.Members.OfType<IdAssignmentNode>().Any(assignment => string.Equals(assignment.Id, id, StringComparison.Ordinal));
        }

        private static int CountId(ObjectDefinitionNode node, string id)
        {
            int count = node.Members.OfType<IdAssignmentNode>().Count(assignment => string.Equals(assignment.Id, id, StringComparison.Ordinal));
            foreach (ObjectDefinitionNode child in node.Members.OfType<ObjectDefinitionNode>())
            {
                count += CountId(child, id);
            }

            return count;
        }

        private static string GetViewModelId(ViewModelSchema schema)
        {
            int separator = schema.CompilerSlotKey.LastIndexOf("::", StringComparison.Ordinal);
            if (separator >= 0 && separator + 2 < schema.CompilerSlotKey.Length)
            {
                return schema.CompilerSlotKey[(separator + 2)..];
            }

            return "__qmlsharp_vm0";
        }

        private static string ReplaceFirst(string input, string oldValue, string newValue)
        {
            int index = input.IndexOf(oldValue, StringComparison.Ordinal);
            if (index < 0)
            {
                return input;
            }

            return string.Concat(input.AsSpan(0, index), newValue, input.AsSpan(index + oldValue.Length));
        }

        private static CompilerDiagnostic CreateDiagnostic(string code, string details)
        {
            return new CompilerDiagnostic(
                code,
                DiagnosticSeverity.Error,
                DiagnosticMessageCatalog.FormatMessage(code, details),
                Location: null,
                Phase);
        }

        private sealed record ImportEntry(string DisplayName, ImportNode Import);

        private static string GetImportDisplayName(ImportNode import)
        {
            return import.ImportKind == ImportKind.Module
                ? import.ModuleUri ?? string.Empty
                : import.Path ?? string.Empty;
        }

        private static string CreateImportKey(ImportNode import, string displayName)
        {
            return string.Concat((int)import.ImportKind, "\u001f", displayName, "\u001f", import.Qualifier ?? string.Empty);
        }

        private sealed record SchemaLookup(
            ImmutableDictionary<string, string> Properties,
            ImmutableDictionary<string, string> Commands)
        {
            public ImmutableArray<string> PropertyNames { get; init; } = ImmutableArray<string>.Empty;

            public static SchemaLookup Create(ViewModelSchema schema)
            {
                ImmutableDictionary<string, string>.Builder properties = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
                foreach (StateEntry property in schema.Properties)
                {
                    properties[property.Name] = property.Name;
                    if (!string.IsNullOrWhiteSpace(property.SourceName))
                    {
                        properties[property.SourceName] = property.Name;
                    }
                }

                ImmutableDictionary<string, string>.Builder commands = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
                foreach (CommandEntry command in schema.Commands)
                {
                    commands[command.Name] = command.Name;
                    if (!string.IsNullOrWhiteSpace(command.SourceName))
                    {
                        commands[command.SourceName] = command.Name;
                    }
                }

                return new SchemaLookup(properties.ToImmutable(), commands.ToImmutable())
                {
                    PropertyNames = schema.Properties.Select(property => property.Name).Order(StringComparer.Ordinal).ToImmutableArray(),
                };
            }

            public string? FindProperty(string name)
            {
                return Properties.TryGetValue(name, out string? propertyName) ? propertyName : null;
            }

            public string? FindCommand(string name)
            {
                return Commands.TryGetValue(name, out string? commandName) ? commandName : null;
            }
        }
    }
}

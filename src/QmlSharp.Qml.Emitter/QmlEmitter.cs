using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using QmlSharp.Qml.Ast;

namespace QmlSharp.Qml.Emitter
{
    /// <summary>
    /// Default QML emitter implementation.
    /// </summary>
    public sealed class QmlEmitter : IQmlEmitter
    {
        /// <inheritdoc/>
        public string Emit(QmlDocument document, EmitOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(document);

            ResolvedEmitOptions resolvedOptions = ResolvedEmitOptions.From(options);
            EmitContext context = new(resolvedOptions);

            EmitDocument(document, context);

            return FinalizeOutput(context.Writer.GetOutput(), resolvedOptions);
        }

        /// <inheritdoc/>
        public string EmitFragment(AstNode node, FragmentEmitOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(node);
            _ = ResolveFragmentOptions(options);

            throw new NotSupportedException("QML fragment emission is implemented in later 03-qml-emitter steps.");
        }

        /// <inheritdoc/>
        public EmitResult EmitWithSourceMap(QmlDocument document, EmitOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(document);
            _ = ResolvedEmitOptions.From(options);

            throw new NotSupportedException("QML source-map emission is implemented in later 03-qml-emitter steps.");
        }

        private static ResolvedEmitOptions ResolveFragmentOptions(FragmentEmitOptions? options)
        {
            if (options?.IndentLevel < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), options.IndentLevel, "Fragment indentation level cannot be negative.");
            }

            return ResolvedEmitOptions.From(options?.Options);
        }

        private static void EmitDocument(QmlDocument document, EmitContext context)
        {
            ObjectDefinitionNode rootObject = document.RootObject
                ?? throw new InvalidOperationException("QML documents require a root object.");

            if (context.Options.EmitGeneratedHeader)
            {
                context.Writer.WriteLine($"// {context.Options.GeneratedHeaderText}");
                WriteSectionSeparator(context);
            }

            EmitHeaderComments(document, context);

            bool hasPragmas = document.Pragmas.Length > 0;
            bool hasImports = document.Imports.Length > 0;

            for (int index = 0; index < document.Pragmas.Length; index++)
            {
                EmitPragma(document.Pragmas[index], context);
            }

            if (hasPragmas)
            {
                WriteSectionSeparator(context);
            }

            ImmutableArray<ImportNode> imports = context.Options.SortImports
                ? EmitOrdering.SortImports(document.Imports)
                : document.Imports;

            for (int index = 0; index < imports.Length; index++)
            {
                EmitImport(imports[index], context);
            }

            if (hasImports)
            {
                WriteSectionSeparator(context);
            }

            EmitObject(rootObject, context);
        }

        private static void EmitHeaderComments(QmlDocument document, EmitContext context)
        {
            if (!context.Options.EmitComments || document.LeadingComments.IsDefaultOrEmpty)
            {
                return;
            }

            for (int index = 0; index < document.LeadingComments.Length; index++)
            {
                context.Writer.WriteLine(document.LeadingComments[index].Text);
            }

            WriteSectionSeparator(context);
        }

        private static void EmitPragma(PragmaNode pragma, EmitContext context)
        {
            string text = pragma.Value is null
                ? $"pragma {pragma.Name}"
                : $"pragma {pragma.Name}: {pragma.Value}";

            context.Writer.WriteLine(text);
        }

        private static void EmitImport(ImportNode import, EmitContext context)
        {
            string text = import.ImportKind switch
            {
                ImportKind.Module => EmitModuleImport(import),
                ImportKind.Directory => EmitPathImport(import),
                ImportKind.JavaScript => EmitPathImport(import),
                _ => throw new NotSupportedException($"Unsupported import kind '{import.ImportKind}'."),
            };

            context.Writer.WriteLine(text);
        }

        private static string EmitModuleImport(ImportNode import)
        {
            if (string.IsNullOrWhiteSpace(import.ModuleUri))
            {
                throw new InvalidOperationException("Module imports require a module URI.");
            }

            string text = $"import {import.ModuleUri}";
            if (!string.IsNullOrWhiteSpace(import.Version))
            {
                text = $"{text} {import.Version}";
            }

            if (!string.IsNullOrWhiteSpace(import.Qualifier))
            {
                text = $"{text} as {import.Qualifier}";
            }

            return text;
        }

        private static string EmitPathImport(ImportNode import)
        {
            if (string.IsNullOrWhiteSpace(import.Path))
            {
                throw new InvalidOperationException("Directory and JavaScript imports require a path.");
            }

            if (import.ImportKind == ImportKind.JavaScript && string.IsNullOrWhiteSpace(import.Qualifier))
            {
                throw new InvalidOperationException("JavaScript imports require a qualifier.");
            }

            string escapedPath = StringLiteralEscaper.Escape(import.Path, '"');
            string text = $"import \"{escapedPath}\"";
            if (!string.IsNullOrWhiteSpace(import.Qualifier))
            {
                text = $"{text} as {import.Qualifier}";
            }

            return text;
        }

        private static void EmitObject(ObjectDefinitionNode? obj, EmitContext context)
        {
            ArgumentNullException.ThrowIfNull(obj);

            ImmutableArray<AstNode> members = GetObjectMembers(obj, context);

            if (members.Length == 0 && context.Options.SingleLineEmptyObjects)
            {
                context.Writer.WriteLine($"{obj.TypeName} {{}}");
                return;
            }

            context.Writer.WriteLine($"{obj.TypeName} {{");
            context.Writer.Indent();

            for (int index = 0; index < members.Length; index++)
            {
                AstNode member = members[index];
                EmitObjectMember(member, context);

                if (ShouldWriteBlankLineBetweenMembers(member, members, index, context))
                {
                    context.Writer.WriteLine();
                }
            }

            context.Writer.Dedent();
            context.Writer.WriteLine("}");
        }

        private static ImmutableArray<AstNode> GetObjectMembers(ObjectDefinitionNode obj, EmitContext context)
        {
            return context.Options.Normalize
                ? EmitOrdering.NormalizeMembers(obj.Members)
                : obj.Members;
        }

        private static void EmitObjectMember(AstNode member, EmitContext context)
        {
            switch (member)
            {
                case IdAssignmentNode id:
                    context.Writer.WriteLine($"id: {id.Id}{context.GetSemicolonSuffix()}");
                    break;
                case BindingNode binding:
                    EmitBinding(binding, context);
                    break;
                case GroupedBindingNode grouped:
                    EmitGroupedBinding(grouped, context);
                    break;
                case AttachedBindingNode attached:
                    EmitAttachedBinding(attached, context);
                    break;
                case ArrayBindingNode arrayBinding:
                    EmitArrayBinding(arrayBinding, context);
                    break;
                case BehaviorOnNode behavior:
                    EmitBehaviorOn(behavior, context);
                    break;
                case ObjectDefinitionNode child:
                    EmitObject(child, context);
                    break;
                case CommentNode comment when context.Options.EmitComments:
                    context.Writer.WriteLine(comment.Text);
                    break;
                case CommentNode:
                    break;
                default:
                    throw new NotSupportedException($"Emission for AST node kind '{member.Kind}' is implemented in a later 03-qml-emitter step.");
            }
        }

        private static void EmitBinding(BindingNode binding, EmitContext context)
        {
            EmitNamedValue(binding.PropertyName, binding.Value, context, context.GetSemicolonSuffix());
        }

        private static void EmitNamedValue(string name, BindingValue value, EmitContext context, string suffix)
        {
            if (TryFormatInlineBindingValue(value, context, out string? inlineValue))
            {
                context.Writer.WriteLine($"{name}: {inlineValue}{suffix}");
                return;
            }

            context.Writer.WriteIndent();
            context.Writer.Write($"{name}: ");
            EmitMultilineBindingValue(value, context, suffix);
        }

        private static void EmitGroupedBinding(GroupedBindingNode grouped, EmitContext context)
        {
            if (grouped.Bindings.IsDefaultOrEmpty)
            {
                context.Writer.WriteLine($"{grouped.GroupName} {{}}");
                return;
            }

            context.Writer.WriteLine($"{grouped.GroupName} {{");
            context.Writer.Indent();

            for (int index = 0; index < grouped.Bindings.Length; index++)
            {
                EmitBinding(grouped.Bindings[index], context);
            }

            context.Writer.Dedent();
            context.Writer.WriteLine("}");
        }

        private static void EmitAttachedBinding(AttachedBindingNode attached, EmitContext context)
        {
            if (attached.Bindings.IsDefaultOrEmpty)
            {
                context.Writer.WriteLine($"{attached.AttachedTypeName} {{}}");
                return;
            }

            if (attached.Bindings.Length == 1)
            {
                BindingNode binding = attached.Bindings[0];
                EmitNamedValue($"{attached.AttachedTypeName}.{binding.PropertyName}", binding.Value, context, context.GetSemicolonSuffix());
                return;
            }

            context.Writer.WriteLine($"{attached.AttachedTypeName} {{");
            context.Writer.Indent();

            for (int index = 0; index < attached.Bindings.Length; index++)
            {
                EmitBinding(attached.Bindings[index], context);
            }

            context.Writer.Dedent();
            context.Writer.WriteLine("}");
        }

        private static void EmitArrayBinding(ArrayBindingNode arrayBinding, EmitContext context)
        {
            EmitArrayValue(arrayBinding.PropertyName, arrayBinding.Elements, context, context.GetSemicolonSuffix(), multilineForMultipleElements: true);
        }

        private static void EmitBehaviorOn(BehaviorOnNode behavior, EmitContext context)
        {
            context.Writer.WriteLine($"Behavior on {behavior.PropertyName} {{");
            context.Writer.Indent();
            EmitObject(behavior.Animation, context);
            context.Writer.Dedent();
            context.Writer.WriteLine("}");
        }

        private static bool TryFormatInlineBindingValue(BindingValue value, EmitContext context, [NotNullWhen(true)] out string? text)
        {
            text = value switch
            {
                NumberLiteral number => QmlValueFormatter.FormatNumber(number.Value),
                StringLiteral stringLiteral => QmlValueFormatter.FormatString(stringLiteral, context.Options),
                BooleanLiteral boolean => boolean.Value ? "true" : "false",
                NullLiteral => "null",
                EnumReference enumReference => $"{enumReference.TypeName}.{enumReference.MemberName}",
                ScriptExpression expression when !ContainsLineBreak(expression.Code) => expression.Code,
                ScriptBlock block when !ContainsLineBreak(block.Code) => $"{{ {block.Code} }}",
                ObjectValue objectValue => TryFormatInlineObject(objectValue.Object, context),
                ArrayValue arrayValue => TryFormatInlineArray(arrayValue.Elements, context),
                _ => null,
            };

            return text is not null;
        }

        private static void EmitMultilineBindingValue(BindingValue value, EmitContext context, string suffix)
        {
            switch (value)
            {
                case ScriptExpression expression:
                    EmitMultilineExpression(expression.Code, context, suffix);
                    break;
                case ScriptBlock block:
                    EmitScriptBlock(block.Code, context, suffix);
                    break;
                case ObjectValue objectValue:
                    EmitObjectValue(objectValue.Object, context, suffix);
                    break;
                case ArrayValue arrayValue:
                    EmitArrayElements(arrayValue.Elements, context, suffix);
                    break;
                default:
                    if (!TryFormatInlineBindingValue(value, context, out string? inlineValue))
                    {
                        throw new NotSupportedException($"Binding value kind '{value.Kind}' is not supported by the emitter.");
                    }

                    context.Writer.Write(inlineValue);
                    context.Writer.Write(suffix);
                    context.Writer.WriteLine();
                    break;
            }
        }

        private static string? TryFormatInlineObject(ObjectDefinitionNode obj, EmitContext context)
        {
            ImmutableArray<AstNode> members = GetObjectMembers(obj, context);
            if (members.Length == 0)
            {
                return $"{obj.TypeName} {{}}";
            }

            if (members.Length != 1 || members[0] is not BindingNode binding)
            {
                return null;
            }

            if (!TryFormatInlineBindingValue(binding.Value, context, out string? valueText))
            {
                return null;
            }

            return $"{obj.TypeName} {{ {binding.PropertyName}: {valueText}{context.GetSemicolonSuffix()} }}";
        }

        private static string? TryFormatInlineArray(ImmutableArray<BindingValue> elements, EmitContext context)
        {
            if (elements.IsDefaultOrEmpty)
            {
                return "[]";
            }

            string[] formattedElements = new string[elements.Length];
            for (int index = 0; index < elements.Length; index++)
            {
                if (!TryFormatInlineBindingValue(elements[index], context, out string? elementText))
                {
                    return null;
                }

                formattedElements[index] = elementText;
            }

            return $"[{string.Join(", ", formattedElements)}]";
        }

        private static void EmitMultilineExpression(string code, EmitContext context, string suffix)
        {
            string[] lines = SplitCodeLines(code);
            if (lines.Length == 0)
            {
                context.Writer.Write(suffix);
                context.Writer.WriteLine();
                return;
            }

            if (lines.Length == 1)
            {
                context.Writer.Write(lines[0]);
                context.Writer.Write(suffix);
                context.Writer.WriteLine();
                return;
            }

            context.Writer.Write(lines[0]);
            context.Writer.WriteLine();

            context.Writer.Indent();
            for (int index = 1; index < lines.Length; index++)
            {
                string lineSuffix = index + 1 == lines.Length ? suffix : string.Empty;
                context.Writer.WriteLine($"{lines[index]}{lineSuffix}");
            }

            context.Writer.Dedent();
        }

        private static void EmitScriptBlock(string code, EmitContext context, string suffix)
        {
            string[] lines = SplitCodeLines(code);

            context.Writer.Write("{");
            context.Writer.WriteLine();
            context.Writer.Indent();

            for (int index = 0; index < lines.Length; index++)
            {
                context.Writer.WriteLine(lines[index]);
            }

            context.Writer.Dedent();
            context.Writer.WriteLine($"}}{suffix}");
        }

        private static void EmitObjectValue(ObjectDefinitionNode obj, EmitContext context, string suffix)
        {
            ImmutableArray<AstNode> members = GetObjectMembers(obj, context);

            if (members.Length == 0 && context.Options.SingleLineEmptyObjects)
            {
                context.Writer.Write($"{obj.TypeName} {{}}");
                context.Writer.Write(suffix);
                context.Writer.WriteLine();
                return;
            }

            context.Writer.Write($"{obj.TypeName} {{");
            context.Writer.WriteLine();
            context.Writer.Indent();

            for (int index = 0; index < members.Length; index++)
            {
                AstNode member = members[index];
                EmitObjectMember(member, context);

                if (ShouldWriteBlankLineBetweenMembers(member, members, index, context))
                {
                    context.Writer.WriteLine();
                }
            }

            context.Writer.Dedent();
            context.Writer.WriteLine($"}}{suffix}");
        }

        private static void EmitArrayValue(
            string propertyName,
            ImmutableArray<BindingValue> elements,
            EmitContext context,
            string suffix,
            bool multilineForMultipleElements)
        {
            if (!multilineForMultipleElements || elements.Length <= 1)
            {
                BindingValue value = new ArrayValue(elements);
                if (TryFormatInlineBindingValue(value, context, out string? inlineValue))
                {
                    context.Writer.WriteLine($"{propertyName}: {inlineValue}{suffix}");
                    return;
                }
            }

            context.Writer.WriteIndent();
            context.Writer.Write($"{propertyName}: ");
            EmitArrayElements(elements, context, suffix);
        }

        private static void EmitArrayElements(ImmutableArray<BindingValue> elements, EmitContext context, string suffix)
        {
            if (elements.IsDefaultOrEmpty)
            {
                context.Writer.Write("[]");
                context.Writer.Write(suffix);
                context.Writer.WriteLine();
                return;
            }

            context.Writer.Write("[");
            context.Writer.WriteLine();
            context.Writer.Indent();

            for (int index = 0; index < elements.Length; index++)
            {
                EmitArrayElement(elements[index], context, index + 1 < elements.Length);
            }

            context.Writer.Dedent();
            context.Writer.WriteLine($"]{suffix}");
        }

        private static void EmitArrayElement(BindingValue element, EmitContext context, bool hasFollowingElement)
        {
            string suffix = hasFollowingElement ? "," : string.Empty;
            if (TryFormatInlineBindingValue(element, context, out string? inlineValue))
            {
                context.Writer.WriteLine($"{inlineValue}{suffix}");
                return;
            }

            context.Writer.WriteIndent();
            EmitMultilineBindingValue(element, context, suffix);
        }

        private static string[] SplitCodeLines(string code)
        {
            return code
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n');
        }

        private static bool ContainsLineBreak(string value)
        {
            return value.Contains('\n', StringComparison.Ordinal) || value.Contains('\r', StringComparison.Ordinal);
        }

        private static bool ShouldWriteBlankLineBetweenMembers(
            AstNode member,
            ImmutableArray<AstNode> members,
            int index,
            EmitContext context)
        {
            if (!context.Options.InsertBlankLinesBetweenObjects || member is not ObjectDefinitionNode)
            {
                return false;
            }

            return index + 1 < members.Length && members[index + 1] is ObjectDefinitionNode;
        }

        private static void WriteSectionSeparator(EmitContext context)
        {
            if (context.Options.InsertBlankLinesBetweenSections)
            {
                context.Writer.WriteLine();
            }
        }

        private static string FinalizeOutput(string output, ResolvedEmitOptions options)
        {
            if (options.TrailingNewline || !output.EndsWith(options.NewlineString, StringComparison.Ordinal))
            {
                return output;
            }

            return output[..^options.NewlineString.Length];
        }
    }
}

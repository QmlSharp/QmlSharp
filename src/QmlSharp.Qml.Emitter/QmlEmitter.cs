using System.Collections.Immutable;
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
                    context.Writer.WriteLine($"{binding.PropertyName}: {EmitPrimitiveBindingValue(binding.Value, context)}{context.GetSemicolonSuffix()}");
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

        private static string EmitPrimitiveBindingValue(BindingValue value, EmitContext context)
        {
            return value switch
            {
                NumberLiteral number => QmlValueFormatter.FormatNumber(number.Value),
                StringLiteral text => QmlValueFormatter.FormatString(text, context.Options),
                BooleanLiteral boolean => boolean.Value ? "true" : "false",
                NullLiteral => "null",
                _ => throw new NotSupportedException($"Binding value kind '{value.Kind}' is implemented in a later 03-qml-emitter step."),
            };
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

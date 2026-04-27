using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using static QmlSharp.Qml.Ast.Serialization.JsonAstSerializationHelpers;

#pragma warning disable MA0048

namespace QmlSharp.Qml.Ast.Serialization
{
    /// <summary>
    /// AST to and from JSON serialization contract.
    /// </summary>
    public interface IQmlAstSerializer
    {
        /// <summary>
        /// Serializes a document to compact JSON.
        /// </summary>
        /// <param name="document">Document to serialize.</param>
        /// <returns>JSON text.</returns>
        string ToJson(QmlDocument document);

        /// <summary>
        /// Serializes a document to pretty JSON.
        /// </summary>
        /// <param name="document">Document to serialize.</param>
        /// <returns>Indented JSON text.</returns>
        string ToPrettyJson(QmlDocument document);

        /// <summary>
        /// Deserializes JSON into a document.
        /// </summary>
        /// <param name="json">JSON text.</param>
        /// <returns>Deserialized document.</returns>
        QmlDocument FromJson(string json);

        /// <summary>
        /// Deep-clones a document.
        /// </summary>
        /// <param name="document">Document to clone.</param>
        /// <returns>Cloned document.</returns>
        QmlDocument Clone(QmlDocument document);
    }

    /// <summary>
    /// Exception thrown when AST JSON serialization or deserialization fails.
    /// </summary>
    public sealed class QmlAstSerializationException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="QmlAstSerializationException"/> class.
        /// </summary>
        /// <param name="message">Failure message.</param>
        public QmlAstSerializationException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="QmlAstSerializationException"/> class.
        /// </summary>
        /// <param name="message">Failure message.</param>
        /// <param name="inner">Inner exception.</param>
        public QmlAstSerializationException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    /// <summary>
    /// Default <see cref="IQmlAstSerializer"/> implementation backed by <see cref="System.Text.Json"/>.
    /// </summary>
    public sealed class QmlAstSerializer : IQmlAstSerializer
    {
        private static readonly JsonSerializerOptions CompactOptions = CreateOptions(writeIndented: false);
        private static readonly JsonSerializerOptions PrettyOptions = CreateOptions(writeIndented: true);

        /// <inheritdoc/>
        public string ToJson(QmlDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);

            return JsonSerializer.Serialize((AstNode)document, CompactOptions);
        }

        /// <inheritdoc/>
        public string ToPrettyJson(QmlDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);

            return JsonSerializer.Serialize((AstNode)document, PrettyOptions);
        }

        /// <inheritdoc/>
        public QmlDocument FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new QmlAstSerializationException("AST JSON cannot be null, empty, or whitespace.");
            }

            try
            {
                AstNode? node = JsonSerializer.Deserialize<AstNode>(json, CompactOptions);
                return node as QmlDocument
                    ?? throw new QmlAstSerializationException("AST JSON root must have kind 'Document'.");
            }
            catch (QmlAstSerializationException)
            {
                throw;
            }
            catch (JsonException ex)
            {
                throw new QmlAstSerializationException($"Invalid AST JSON: {ex.Message}", ex);
            }
            catch (NotSupportedException ex)
            {
                throw new QmlAstSerializationException($"Unsupported AST JSON shape: {ex.Message}", ex);
            }
        }

        /// <inheritdoc/>
        public QmlDocument Clone(QmlDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);

            return FromJson(ToJson(document));
        }

        private static JsonSerializerOptions CreateOptions(bool writeIndented)
        {
            JsonSerializerOptions options = new()
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = writeIndented,
            };

            options.Converters.Add(new JsonStringEnumConverter<NodeKind>());
            options.Converters.Add(new JsonStringEnumConverter<BindingValueKind>());
            options.Converters.Add(new JsonStringEnumConverter<PragmaName>());
            options.Converters.Add(new JsonStringEnumConverter<ImportKind>());
            options.Converters.Add(new JsonStringEnumConverter<SignalHandlerForm>());
            options.Converters.Add(new AstNodeJsonConverter());
            options.Converters.Add(new BindingValueJsonConverter());
            return options;
        }
    }

    /// <summary>
    /// Polymorphic JSON converter for <see cref="AstNode"/>.
    /// </summary>
    public sealed class AstNodeJsonConverter : JsonConverter<AstNode>
    {
        /// <inheritdoc/>
        public override AstNode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            JsonElement root = document.RootElement;
            NodeKind kind = ReadDiscriminator<NodeKind>(root, "AST node");

            return kind switch
            {
                NodeKind.Document => ReadDocument(root, options),
                NodeKind.Import => ReadImport(root, options),
                NodeKind.Pragma => ReadPragma(root, options),
                NodeKind.ObjectDefinition => ReadObjectDefinition(root, options),
                NodeKind.InlineComponent => ReadInlineComponent(root, options),
                NodeKind.PropertyDeclaration => ReadPropertyDeclaration(root, options),
                NodeKind.PropertyAlias => ReadPropertyAlias(root, options),
                NodeKind.Binding => ReadBinding(root, options),
                NodeKind.GroupedBinding => ReadGroupedBinding(root, options),
                NodeKind.AttachedBinding => ReadAttachedBinding(root, options),
                NodeKind.ArrayBinding => ReadArrayBinding(root, options),
                NodeKind.BehaviorOn => ReadBehaviorOn(root, options),
                NodeKind.SignalDeclaration => ReadSignalDeclaration(root, options),
                NodeKind.SignalHandler => ReadSignalHandler(root, options),
                NodeKind.FunctionDeclaration => ReadFunctionDeclaration(root, options),
                NodeKind.EnumDeclaration => ReadEnumDeclaration(root, options),
                NodeKind.IdAssignment => ReadIdAssignment(root, options),
                NodeKind.Comment => ReadComment(root, options),
                _ => throw new JsonException($"Unknown AST node kind '{kind}'."),
            };
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, AstNode value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(value);

            writer.WriteStartObject();
            writer.WriteString("kind", value.Kind.ToString());
            WriteNodeCommon(writer, value, options);
            WriteNodeBody(writer, value, options);
            writer.WriteEndObject();
        }

        private static void WriteNodeBody(Utf8JsonWriter writer, AstNode value, JsonSerializerOptions options)
        {
            if (TryWriteDocumentLevelBody(writer, value, options)
                || TryWriteBindingBody(writer, value, options)
                || TryWriteDeclarationBody(writer, value, options)
                || TryWriteLeafBody(writer, value, options))
            {
                return;
            }

            throw new JsonException($"Unsupported AST node type '{value.GetType().Name}'.");
        }

        private static bool TryWriteDocumentLevelBody(Utf8JsonWriter writer, AstNode value, JsonSerializerOptions options)
        {
            switch (value)
            {
                case QmlDocument document:
                    WriteDocumentBody(writer, document, options);
                    return true;

                case ImportNode importNode:
                    WriteImportBody(writer, importNode, options);
                    return true;

                case PragmaNode pragmaNode:
                    WritePragmaBody(writer, pragmaNode, options);
                    return true;

                case ObjectDefinitionNode objectDefinitionNode:
                    writer.WriteString("typeName", objectDefinitionNode.TypeName);
                    WriteArray(writer, "members", objectDefinitionNode.Members, options);
                    return true;

                case InlineComponentNode inlineComponentNode:
                    writer.WriteString("name", inlineComponentNode.Name);
                    WriteRequired(writer, "body", inlineComponentNode.Body, options);
                    return true;

                default:
                    return false;
            }
        }

        private static bool TryWriteDeclarationBody(Utf8JsonWriter writer, AstNode value, JsonSerializerOptions options)
        {
            switch (value)
            {
                case PropertyDeclarationNode propertyDeclarationNode:
                    WritePropertyDeclarationBody(writer, propertyDeclarationNode, options);
                    return true;

                case PropertyAliasNode propertyAliasNode:
                    writer.WriteString("name", propertyAliasNode.Name);
                    writer.WriteString("target", propertyAliasNode.Target);
                    WriteOptional(writer, "isDefault", propertyAliasNode.IsDefault, options, defaultValue: false);
                    return true;

                case SignalDeclarationNode signalDeclarationNode:
                    writer.WriteString("name", signalDeclarationNode.Name);
                    WriteArray(writer, "parameters", signalDeclarationNode.Parameters, options);
                    return true;

                case SignalHandlerNode signalHandlerNode:
                    WriteSignalHandlerBody(writer, signalHandlerNode, options);
                    return true;

                case FunctionDeclarationNode functionDeclarationNode:
                    writer.WriteString("name", functionDeclarationNode.Name);
                    WriteArray(writer, "parameters", functionDeclarationNode.Parameters, options);
                    WriteOptional(writer, "returnType", functionDeclarationNode.ReturnType, options);
                    writer.WriteString("body", functionDeclarationNode.Body);
                    return true;

                case EnumDeclarationNode enumDeclarationNode:
                    writer.WriteString("name", enumDeclarationNode.Name);
                    WriteArray(writer, "members", enumDeclarationNode.Members, options);
                    return true;

                default:
                    return false;
            }
        }

        private static bool TryWriteBindingBody(Utf8JsonWriter writer, AstNode value, JsonSerializerOptions options)
        {
            switch (value)
            {
                case BindingNode bindingNode:
                    writer.WriteString("propertyName", bindingNode.PropertyName);
                    WriteRequired(writer, "value", bindingNode.Value, options);
                    return true;

                case GroupedBindingNode groupedBindingNode:
                    writer.WriteString("groupName", groupedBindingNode.GroupName);
                    WriteArray(writer, "bindings", groupedBindingNode.Bindings, options);
                    return true;

                case AttachedBindingNode attachedBindingNode:
                    writer.WriteString("attachedTypeName", attachedBindingNode.AttachedTypeName);
                    WriteArray(writer, "bindings", attachedBindingNode.Bindings, options);
                    return true;

                case ArrayBindingNode arrayBindingNode:
                    writer.WriteString("propertyName", arrayBindingNode.PropertyName);
                    WriteArray(writer, "elements", arrayBindingNode.Elements, options);
                    return true;

                case BehaviorOnNode behaviorOnNode:
                    writer.WriteString("propertyName", behaviorOnNode.PropertyName);
                    WriteRequired(writer, "animation", behaviorOnNode.Animation, options);
                    return true;

                default:
                    return false;
            }
        }

        private static bool TryWriteLeafBody(Utf8JsonWriter writer, AstNode value, JsonSerializerOptions options)
        {
            switch (value)
            {
                case IdAssignmentNode idAssignmentNode:
                    writer.WriteString("id", idAssignmentNode.Id);
                    return true;

                case CommentNode commentNode:
                    writer.WriteString("text", commentNode.Text);
                    WriteOptional(writer, "isBlock", commentNode.IsBlock, options, defaultValue: false);
                    return true;

                default:
                    return false;
            }
        }

        private static QmlDocument ReadDocument(JsonElement root, JsonSerializerOptions options)
        {
            QmlDocument document = new()
            {
                Pragmas = ReadOptionalArray<PragmaNode>(root, "pragmas", options),
                Imports = ReadOptionalArray<ImportNode>(root, "imports", options),
                RootObject = ReadRequired<ObjectDefinitionNode>(root, "rootObject", options),
            };

            return ReadNodeCommon(document, root, options);
        }

        private static ImportNode ReadImport(JsonElement root, JsonSerializerOptions options)
        {
            return ReadNodeCommon(new ImportNode
            {
                ImportKind = ReadRequired<ImportKind>(root, "importKind", options),
                ModuleUri = ReadOptional<string>(root, "moduleUri", options),
                Version = ReadOptional<string>(root, "version", options),
                Path = ReadOptional<string>(root, "path", options),
                Qualifier = ReadOptional<string>(root, "qualifier", options),
            }, root, options);
        }

        private static PragmaNode ReadPragma(JsonElement root, JsonSerializerOptions options)
        {
            return ReadNodeCommon(new PragmaNode
            {
                Name = ReadRequired<PragmaName>(root, "name", options),
                Value = ReadOptional<string>(root, "value", options),
            }, root, options);
        }

        private static ObjectDefinitionNode ReadObjectDefinition(JsonElement root, JsonSerializerOptions options)
        {
            return ReadNodeCommon(new ObjectDefinitionNode
            {
                TypeName = ReadRequired<string>(root, "typeName", options),
                Members = ReadOptionalArray<AstNode>(root, "members", options),
            }, root, options);
        }

        private static InlineComponentNode ReadInlineComponent(JsonElement root, JsonSerializerOptions options)
        {
            return ReadNodeCommon(new InlineComponentNode
            {
                Name = ReadRequired<string>(root, "name", options),
                Body = ReadRequired<ObjectDefinitionNode>(root, "body", options),
            }, root, options);
        }

        private static PropertyDeclarationNode ReadPropertyDeclaration(JsonElement root, JsonSerializerOptions options)
        {
            return ReadNodeCommon(new PropertyDeclarationNode
            {
                Name = ReadRequired<string>(root, "name", options),
                TypeName = ReadRequired<string>(root, "typeName", options),
                IsDefault = ReadOptional(root, "isDefault", options, defaultValue: false),
                IsRequired = ReadOptional(root, "isRequired", options, defaultValue: false),
                IsReadonly = ReadOptional(root, "isReadonly", options, defaultValue: false),
                InitialValue = ReadOptional<BindingValue>(root, "initialValue", options),
            }, root, options);
        }

        private static PropertyAliasNode ReadPropertyAlias(JsonElement root, JsonSerializerOptions options)
        {
            return ReadNodeCommon(new PropertyAliasNode
            {
                Name = ReadRequired<string>(root, "name", options),
                Target = ReadRequired<string>(root, "target", options),
                IsDefault = ReadOptional(root, "isDefault", options, defaultValue: false),
            }, root, options);
        }

        private static BindingNode ReadBinding(JsonElement root, JsonSerializerOptions options)
        {
            return ReadNodeCommon(new BindingNode
            {
                PropertyName = ReadRequired<string>(root, "propertyName", options),
                Value = ReadRequired<BindingValue>(root, "value", options),
            }, root, options);
        }

        private static GroupedBindingNode ReadGroupedBinding(JsonElement root, JsonSerializerOptions options)
        {
            return ReadNodeCommon(new GroupedBindingNode
            {
                GroupName = ReadRequired<string>(root, "groupName", options),
                Bindings = ReadOptionalArray<BindingNode>(root, "bindings", options),
            }, root, options);
        }

        private static AttachedBindingNode ReadAttachedBinding(JsonElement root, JsonSerializerOptions options)
        {
            return ReadNodeCommon(new AttachedBindingNode
            {
                AttachedTypeName = ReadRequired<string>(root, "attachedTypeName", options),
                Bindings = ReadOptionalArray<BindingNode>(root, "bindings", options),
            }, root, options);
        }

        private static ArrayBindingNode ReadArrayBinding(JsonElement root, JsonSerializerOptions options)
        {
            return ReadNodeCommon(new ArrayBindingNode
            {
                PropertyName = ReadRequired<string>(root, "propertyName", options),
                Elements = ReadOptionalArray<BindingValue>(root, "elements", options),
            }, root, options);
        }

        private static BehaviorOnNode ReadBehaviorOn(JsonElement root, JsonSerializerOptions options)
        {
            return ReadNodeCommon(new BehaviorOnNode
            {
                PropertyName = ReadRequired<string>(root, "propertyName", options),
                Animation = ReadRequired<ObjectDefinitionNode>(root, "animation", options),
            }, root, options);
        }

        private static SignalDeclarationNode ReadSignalDeclaration(JsonElement root, JsonSerializerOptions options)
        {
            return ReadNodeCommon(new SignalDeclarationNode
            {
                Name = ReadRequired<string>(root, "name", options),
                Parameters = ReadOptionalArray<ParameterDeclaration>(root, "parameters", options),
            }, root, options);
        }

        private static SignalHandlerNode ReadSignalHandler(JsonElement root, JsonSerializerOptions options)
        {
            return ReadNodeCommon(new SignalHandlerNode
            {
                HandlerName = ReadRequired<string>(root, "handlerName", options),
                Form = ReadRequired<SignalHandlerForm>(root, "form", options),
                Code = ReadRequired<string>(root, "code", options),
                Parameters = ReadOptionalNullableArray<string>(root, "parameters", options),
            }, root, options);
        }

        private static FunctionDeclarationNode ReadFunctionDeclaration(JsonElement root, JsonSerializerOptions options)
        {
            return ReadNodeCommon(new FunctionDeclarationNode
            {
                Name = ReadRequired<string>(root, "name", options),
                Body = ReadRequired<string>(root, "body", options),
                ReturnType = ReadOptional<string>(root, "returnType", options),
                Parameters = ReadOptionalArray<ParameterDeclaration>(root, "parameters", options),
            }, root, options);
        }

        private static EnumDeclarationNode ReadEnumDeclaration(JsonElement root, JsonSerializerOptions options)
        {
            return ReadNodeCommon(new EnumDeclarationNode
            {
                Name = ReadRequired<string>(root, "name", options),
                Members = ReadRequired<ImmutableArray<EnumMember>>(root, "members", options),
            }, root, options);
        }

        private static IdAssignmentNode ReadIdAssignment(JsonElement root, JsonSerializerOptions options)
        {
            return ReadNodeCommon(new IdAssignmentNode
            {
                Id = ReadRequired<string>(root, "id", options),
            }, root, options);
        }

        private static CommentNode ReadComment(JsonElement root, JsonSerializerOptions options)
        {
            return ReadNodeCommon(new CommentNode
            {
                Text = ReadRequired<string>(root, "text", options),
                IsBlock = ReadOptional(root, "isBlock", options, defaultValue: false),
            }, root, options);
        }

        private static void WriteDocumentBody(Utf8JsonWriter writer, QmlDocument document, JsonSerializerOptions options)
        {
            WriteArray(writer, "pragmas", document.Pragmas, options);
            WriteArray(writer, "imports", document.Imports, options);
            WriteRequired(writer, "rootObject", document.RootObject, options);
        }

        private static void WriteImportBody(Utf8JsonWriter writer, ImportNode importNode, JsonSerializerOptions options)
        {
            WriteRequired(writer, "importKind", importNode.ImportKind, options);
            WriteOptional(writer, "moduleUri", importNode.ModuleUri, options);
            WriteOptional(writer, "version", importNode.Version, options);
            WriteOptional(writer, "path", importNode.Path, options);
            WriteOptional(writer, "qualifier", importNode.Qualifier, options);
        }

        private static void WritePragmaBody(Utf8JsonWriter writer, PragmaNode pragmaNode, JsonSerializerOptions options)
        {
            WriteRequired(writer, "name", pragmaNode.Name, options);
            WriteOptional(writer, "value", pragmaNode.Value, options);
        }

        private static void WritePropertyDeclarationBody(Utf8JsonWriter writer, PropertyDeclarationNode propertyDeclarationNode, JsonSerializerOptions options)
        {
            writer.WriteString("name", propertyDeclarationNode.Name);
            writer.WriteString("typeName", propertyDeclarationNode.TypeName);
            WriteOptional(writer, "isDefault", propertyDeclarationNode.IsDefault, options, defaultValue: false);
            WriteOptional(writer, "isRequired", propertyDeclarationNode.IsRequired, options, defaultValue: false);
            WriteOptional(writer, "isReadonly", propertyDeclarationNode.IsReadonly, options, defaultValue: false);
            WriteOptional(writer, "initialValue", propertyDeclarationNode.InitialValue, options);
        }

        private static void WriteSignalHandlerBody(Utf8JsonWriter writer, SignalHandlerNode signalHandlerNode, JsonSerializerOptions options)
        {
            writer.WriteString("handlerName", signalHandlerNode.HandlerName);
            WriteRequired(writer, "form", signalHandlerNode.Form, options);
            writer.WriteString("code", signalHandlerNode.Code);
            WriteOptionalArray(writer, "parameters", signalHandlerNode.Parameters, options);
        }
    }

    /// <summary>
    /// Polymorphic JSON converter for <see cref="BindingValue"/>.
    /// </summary>
    public sealed class BindingValueJsonConverter : JsonConverter<BindingValue>
    {
        /// <inheritdoc/>
        public override BindingValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            JsonElement root = document.RootElement;
            BindingValueKind kind = ReadDiscriminator<BindingValueKind>(root, "binding value");

            return kind switch
            {
                BindingValueKind.NumberLiteral => new NumberLiteral(ReadRequired<double>(root, "value", options)),
                BindingValueKind.StringLiteral => new StringLiteral(ReadRequired<string>(root, "value", options)),
                BindingValueKind.BooleanLiteral => new BooleanLiteral(ReadRequired<bool>(root, "value", options)),
                BindingValueKind.NullLiteral => NullLiteral.Instance,
                BindingValueKind.EnumReference => new EnumReference(
                    ReadRequired<string>(root, "typeName", options),
                    ReadRequired<string>(root, "memberName", options)),
                BindingValueKind.ScriptExpression => new ScriptExpression(ReadRequired<string>(root, "code", options)),
                BindingValueKind.ScriptBlock => new ScriptBlock(ReadRequired<string>(root, "code", options)),
                BindingValueKind.ObjectValue => new ObjectValue(ReadRequired<ObjectDefinitionNode>(root, "object", options)),
                BindingValueKind.ArrayValue => new ArrayValue(ReadOptionalArray<BindingValue>(root, "elements", options)),
                _ => throw new JsonException($"Unknown binding value kind '{kind}'."),
            };
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, BindingValue value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(value);

            writer.WriteStartObject();
            writer.WriteString("kind", value.Kind.ToString());

            switch (value)
            {
                case NumberLiteral numberLiteral:
                    writer.WriteNumber("value", numberLiteral.Value);
                    break;

                case StringLiteral stringLiteral:
                    writer.WriteString("value", stringLiteral.Value);
                    break;

                case BooleanLiteral booleanLiteral:
                    writer.WriteBoolean("value", booleanLiteral.Value);
                    break;

                case NullLiteral:
                    break;

                case EnumReference enumReference:
                    writer.WriteString("typeName", enumReference.TypeName);
                    writer.WriteString("memberName", enumReference.MemberName);
                    break;

                case ScriptExpression scriptExpression:
                    writer.WriteString("code", scriptExpression.Code);
                    break;

                case ScriptBlock scriptBlock:
                    writer.WriteString("code", scriptBlock.Code);
                    break;

                case ObjectValue objectValue:
                    WriteRequired(writer, "object", objectValue.Object, options);
                    break;

                case ArrayValue arrayValue:
                    WriteArray(writer, "elements", arrayValue.Elements, options);
                    break;

                default:
                    throw new JsonException($"Unsupported binding value type '{value.GetType().Name}'.");
            }

            writer.WriteEndObject();
        }
    }

    internal static class JsonAstSerializationHelpers
    {
        public static TKind ReadDiscriminator<TKind>(JsonElement root, string valueName)
            where TKind : struct, Enum
        {
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException($"Expected {valueName} JSON object.");
            }

            if (!root.TryGetProperty("kind", out JsonElement kindElement))
            {
                throw new JsonException($"{ToSentenceCase(valueName)} JSON is missing required 'kind' discriminator.");
            }

            if (kindElement.ValueKind != JsonValueKind.String)
            {
                throw new JsonException($"{ToSentenceCase(valueName)} discriminator 'kind' must be a string.");
            }

            string? kindText = kindElement.GetString();
            if (string.IsNullOrWhiteSpace(kindText))
            {
                throw new JsonException($"{ToSentenceCase(valueName)} discriminator 'kind' cannot be empty.");
            }

            if (!Enum.TryParse(kindText, ignoreCase: false, out TKind kind))
            {
                throw new JsonException($"Unknown {valueName} kind '{kindText}'.");
            }

            return kind;
        }

        public static TNode ReadNodeCommon<TNode>(TNode node, JsonElement root, JsonSerializerOptions options)
            where TNode : AstNode
        {
            return node with
            {
                Span = ReadOptional<SourceSpan?>(root, "span", options),
                LeadingComments = ReadOptionalArray<CommentNode>(root, "leadingComments", options),
                TrailingComment = ReadOptional<CommentNode>(root, "trailingComment", options),
            };
        }

        public static T ReadRequired<T>(JsonElement root, string propertyName, JsonSerializerOptions options)
        {
            if (!root.TryGetProperty(propertyName, out JsonElement propertyElement))
            {
                throw new JsonException($"Required property '{propertyName}' is missing.");
            }

            if (propertyElement.ValueKind == JsonValueKind.Null)
            {
                throw new JsonException($"Required property '{propertyName}' cannot be null.");
            }

            T? value = propertyElement.Deserialize<T>(options);
            return value ?? throw new JsonException($"Required property '{propertyName}' could not be deserialized.");
        }

        public static T? ReadOptional<T>(JsonElement root, string propertyName, JsonSerializerOptions options)
        {
            if (!root.TryGetProperty(propertyName, out JsonElement propertyElement)
                || propertyElement.ValueKind == JsonValueKind.Null)
            {
                return default;
            }

            return propertyElement.Deserialize<T>(options);
        }

        public static T ReadOptional<T>(JsonElement root, string propertyName, JsonSerializerOptions options, T defaultValue)
        {
            if (!root.TryGetProperty(propertyName, out JsonElement propertyElement)
                || propertyElement.ValueKind == JsonValueKind.Null)
            {
                return defaultValue;
            }

            T? value = propertyElement.Deserialize<T>(options);
            return value ?? defaultValue;
        }

        public static ImmutableArray<T> ReadOptionalArray<T>(JsonElement root, string propertyName, JsonSerializerOptions options)
        {
            if (!root.TryGetProperty(propertyName, out JsonElement propertyElement)
                || propertyElement.ValueKind == JsonValueKind.Null)
            {
                return ImmutableArray<T>.Empty;
            }

            ImmutableArray<T>? values = propertyElement.Deserialize<ImmutableArray<T>>(options);
            return values ?? ImmutableArray<T>.Empty;
        }

        public static ImmutableArray<T>? ReadOptionalNullableArray<T>(JsonElement root, string propertyName, JsonSerializerOptions options)
        {
            if (!root.TryGetProperty(propertyName, out JsonElement propertyElement)
                || propertyElement.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            ImmutableArray<T>? values = propertyElement.Deserialize<ImmutableArray<T>>(options);
            return values ?? ImmutableArray<T>.Empty;
        }

        public static void WriteNodeCommon(Utf8JsonWriter writer, AstNode value, JsonSerializerOptions options)
        {
            WriteOptional(writer, "span", value.Span, options);
            if (!value.LeadingComments.IsDefaultOrEmpty)
            {
                WriteArray(writer, "leadingComments", value.LeadingComments, options);
            }

            WriteOptional(writer, "trailingComment", value.TrailingComment, options);
        }

        public static void WriteRequired<T>(Utf8JsonWriter writer, string propertyName, T value, JsonSerializerOptions options)
        {
            writer.WritePropertyName(propertyName);
            JsonSerializer.Serialize(writer, value, options);
        }

        public static void WriteOptional<T>(Utf8JsonWriter writer, string propertyName, T? value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                return;
            }

            WriteRequired(writer, propertyName, value, options);
        }

        public static void WriteOptional<T>(Utf8JsonWriter writer, string propertyName, T value, JsonSerializerOptions options, T defaultValue)
            where T : struct, IEquatable<T>
        {
            if (value.Equals(defaultValue))
            {
                return;
            }

            WriteRequired(writer, propertyName, value, options);
        }

        public static void WriteArray<T>(Utf8JsonWriter writer, string propertyName, ImmutableArray<T> values, JsonSerializerOptions options)
        {
            writer.WritePropertyName(propertyName);
            JsonSerializer.Serialize(writer, values.IsDefault ? ImmutableArray<T>.Empty : values, options);
        }

        public static void WriteOptionalArray<T>(Utf8JsonWriter writer, string propertyName, ImmutableArray<T>? values, JsonSerializerOptions options)
        {
            if (!values.HasValue)
            {
                return;
            }

            WriteArray(writer, propertyName, values.Value, options);
        }

        private static string ToSentenceCase(string value)
        {
            if (value.Length == 0)
            {
                return value;
            }

            return string.Concat(value[0].ToString().ToUpperInvariant(), value.AsSpan(1));
        }
    }
}

#pragma warning restore MA0048

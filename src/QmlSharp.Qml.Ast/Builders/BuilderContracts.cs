using System.Collections.Immutable;

#pragma warning disable MA0048

namespace QmlSharp.Qml.Ast.Builders
{
    /// <summary>
    /// Top-level builder for constructing <see cref="QmlDocument"/> instances.
    /// </summary>
    public sealed class QmlDocumentBuilder
    {
        private readonly ImmutableArray<PragmaNode>.Builder _pragmas = ImmutableArray.CreateBuilder<PragmaNode>();
        private readonly ImmutableArray<ImportNode>.Builder _imports = ImmutableArray.CreateBuilder<ImportNode>();
        private ObjectDefinitionNode? _rootObject;

        public QmlDocumentBuilder AddPragma(PragmaName name, string? value = null)
        {
            _pragmas.Add(new PragmaNode { Name = name, Value = value });
            return this;
        }

        public QmlDocumentBuilder AddModuleImport(string moduleUri, string? version = null, string? qualifier = null)
        {
            _imports.Add(new ImportNode
            {
                ImportKind = ImportKind.Module,
                ModuleUri = moduleUri,
                Version = version,
                Qualifier = qualifier,
            });
            return this;
        }

        public QmlDocumentBuilder AddDirectoryImport(string path, string? qualifier = null)
        {
            _imports.Add(new ImportNode
            {
                ImportKind = ImportKind.Directory,
                Path = path,
                Qualifier = qualifier,
            });
            return this;
        }

        public QmlDocumentBuilder AddJavaScriptImport(string path, string qualifier)
        {
            _imports.Add(new ImportNode
            {
                ImportKind = ImportKind.JavaScript,
                Path = path,
                Qualifier = qualifier,
            });
            return this;
        }

        public QmlDocumentBuilder AddImport(ImportNode import)
        {
            _imports.Add(import);
            return this;
        }

        public QmlDocumentBuilder SetRootObject(string typeName, Action<QmlObjectBuilder> configure)
        {
            QmlObjectBuilder builder = new(typeName);
            configure(builder);
            _rootObject = builder.Build();
            return this;
        }

        public QmlDocumentBuilder SetRootObject(ObjectDefinitionNode rootObject)
        {
            _rootObject = rootObject;
            return this;
        }

        public QmlDocument Build()
        {
            if (_rootObject is null)
            {
                throw new InvalidOperationException("Root object must be set before building a QmlDocument.");
            }

            return new QmlDocument
            {
                Pragmas = _pragmas.ToImmutable(),
                Imports = _imports.ToImmutable(),
                RootObject = _rootObject,
            };
        }
    }

    /// <summary>
    /// Builder for constructing <see cref="ObjectDefinitionNode"/> instances with a fluent API.
    /// </summary>
    public sealed class QmlObjectBuilder
    {
        private readonly string _typeName;
        private readonly ImmutableArray<AstNode>.Builder _members = ImmutableArray.CreateBuilder<AstNode>();

        public QmlObjectBuilder(string typeName)
        {
            _typeName = typeName;
        }

        public QmlObjectBuilder Id(string id)
        {
            _members.Add(new IdAssignmentNode { Id = id });
            return this;
        }

        public QmlObjectBuilder Binding(string propertyName, BindingValue value)
        {
            _members.Add(new BindingNode { PropertyName = propertyName, Value = value });
            return this;
        }

        public QmlObjectBuilder PropertyDeclaration(
            string name,
            string typeName,
            BindingValue? initialValue = null,
            bool isDefault = false,
            bool isRequired = false,
            bool isReadonly = false)
        {
            _members.Add(new PropertyDeclarationNode
            {
                Name = name,
                TypeName = typeName,
                InitialValue = initialValue,
                IsDefault = isDefault,
                IsRequired = isRequired,
                IsReadonly = isReadonly,
            });
            return this;
        }

        public QmlObjectBuilder PropertyAlias(string name, string target, bool isDefault = false)
        {
            _members.Add(new PropertyAliasNode { Name = name, Target = target, IsDefault = isDefault });
            return this;
        }

        public QmlObjectBuilder GroupedBinding(string groupName, Action<GroupedBindingBuilder> configure)
        {
            GroupedBindingBuilder builder = new();
            configure(builder);
            _members.Add(new GroupedBindingNode
            {
                GroupName = groupName,
                Bindings = builder.Build(),
            });
            return this;
        }

        public QmlObjectBuilder AttachedBinding(string attachedTypeName, Action<AttachedBindingBuilder> configure)
        {
            AttachedBindingBuilder builder = new();
            configure(builder);
            _members.Add(new AttachedBindingNode
            {
                AttachedTypeName = attachedTypeName,
                Bindings = builder.Build(),
            });
            return this;
        }

        public QmlObjectBuilder ArrayBinding(string propertyName, params BindingValue[] elements)
        {
            _members.Add(new ArrayBindingNode
            {
                PropertyName = propertyName,
                Elements = [.. elements],
            });
            return this;
        }

        public QmlObjectBuilder BehaviorOn(string propertyName, string animationType, Action<QmlObjectBuilder>? configure = null)
        {
            QmlObjectBuilder animBuilder = new(animationType);
            configure?.Invoke(animBuilder);
            _members.Add(new BehaviorOnNode
            {
                PropertyName = propertyName,
                Animation = animBuilder.Build(),
            });
            return this;
        }

        public QmlObjectBuilder SignalDeclaration(string name, params ParameterDeclaration[] parameters)
        {
            _members.Add(new SignalDeclarationNode
            {
                Name = name,
                Parameters = [.. parameters],
            });
            return this;
        }

        public QmlObjectBuilder SignalHandler(string handlerName, SignalHandlerForm form, string code, ImmutableArray<string>? parameters = null)
        {
            _members.Add(new SignalHandlerNode
            {
                HandlerName = handlerName,
                Form = form,
                Code = code,
                Parameters = parameters,
            });
            return this;
        }

        public QmlObjectBuilder FunctionDeclaration(
            string name,
            string body,
            string? returnType = null,
            params ParameterDeclaration[] parameters)
        {
            _members.Add(new FunctionDeclarationNode
            {
                Name = name,
                Body = body,
                ReturnType = returnType,
                Parameters = [.. parameters],
            });
            return this;
        }

        public QmlObjectBuilder EnumDeclaration(string name, params EnumMember[] members)
        {
            _members.Add(new EnumDeclarationNode
            {
                Name = name,
                Members = [.. members],
            });
            return this;
        }

        public QmlObjectBuilder InlineComponent(string name, string typeName, Action<QmlObjectBuilder> configure)
        {
            QmlObjectBuilder bodyBuilder = new(typeName);
            configure(bodyBuilder);
            _members.Add(new InlineComponentNode
            {
                Name = name,
                Body = bodyBuilder.Build(),
            });
            return this;
        }

        public QmlObjectBuilder Child(string typeName, Action<QmlObjectBuilder>? configure = null)
        {
            QmlObjectBuilder childBuilder = new(typeName);
            configure?.Invoke(childBuilder);
            _members.Add(childBuilder.Build());
            return this;
        }

        public QmlObjectBuilder Comment(string text, bool isBlock = false)
        {
            _members.Add(new CommentNode { Text = text, IsBlock = isBlock });
            return this;
        }

        public ObjectDefinitionNode Build()
        {
            return new ObjectDefinitionNode
            {
                TypeName = _typeName,
                Members = _members.ToImmutable(),
            };
        }
    }

    /// <summary>
    /// Builder for grouped bindings within a <see cref="QmlObjectBuilder"/>.
    /// </summary>
    public sealed class GroupedBindingBuilder
    {
        private readonly ImmutableArray<BindingNode>.Builder _bindings = ImmutableArray.CreateBuilder<BindingNode>();

        public GroupedBindingBuilder Binding(string propertyName, BindingValue value)
        {
            _bindings.Add(new BindingNode { PropertyName = propertyName, Value = value });
            return this;
        }

        internal ImmutableArray<BindingNode> Build() => _bindings.ToImmutable();
    }

    /// <summary>
    /// Builder for attached bindings within a <see cref="QmlObjectBuilder"/>.
    /// </summary>
    public sealed class AttachedBindingBuilder
    {
        private readonly ImmutableArray<BindingNode>.Builder _bindings = ImmutableArray.CreateBuilder<BindingNode>();

        public AttachedBindingBuilder Binding(string propertyName, BindingValue value)
        {
            _bindings.Add(new BindingNode { PropertyName = propertyName, Value = value });
            return this;
        }

        internal ImmutableArray<BindingNode> Build() => _bindings.ToImmutable();
    }
}

#pragma warning restore MA0048

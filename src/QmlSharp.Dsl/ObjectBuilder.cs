using System.Collections.Immutable;
using QmlSharp.Qml.Ast;

namespace QmlSharp.Dsl
{
    /// <summary>
    /// Mutable runtime implementation behind generated fluent builder interfaces.
    /// </summary>
    public sealed class ObjectBuilder : IObjectBuilder
    {
        private readonly ImmutableArray<AstNode>.Builder _members = ImmutableArray.CreateBuilder<AstNode>();

        /// <summary>Initializes a new instance of the <see cref="ObjectBuilder"/> class.</summary>
        /// <param name="qmlTypeName">The QML type represented by this builder.</param>
        public ObjectBuilder(string qmlTypeName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(qmlTypeName);
            QmlTypeName = qmlTypeName;
        }

        /// <inheritdoc/>
        public string QmlTypeName { get; }

        /// <inheritdoc/>
        public IObjectBuilder Id(string id)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);
            _members.Add(new IdAssignmentNode { Id = id });
            return this;
        }

        /// <inheritdoc/>
        public IObjectBuilder Child(IObjectBuilder child)
        {
            ArgumentNullException.ThrowIfNull(child);
            _members.Add(child.Build());
            return this;
        }

        /// <inheritdoc/>
        public IObjectBuilder Children(params IObjectBuilder[] children)
        {
            ArgumentNullException.ThrowIfNull(children);

            foreach (IObjectBuilder child in children)
            {
                _ = Child(child);
            }

            return this;
        }

        /// <inheritdoc/>
        public IObjectBuilder SetProperty(string propertyName, object? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
            _members.Add(new BindingNode
            {
                PropertyName = propertyName,
                Value = QmlValueConverter.ToBindingValue(value),
            });
            return this;
        }

        /// <inheritdoc/>
        public IObjectBuilder SetBinding(string propertyName, string expression)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
            ArgumentException.ThrowIfNullOrWhiteSpace(expression);
            _members.Add(new BindingNode
            {
                PropertyName = propertyName,
                Value = Values.Expression(expression),
            });
            return this;
        }

        /// <inheritdoc/>
        public IObjectBuilder AddGrouped(string groupName, Action<IPropertyCollector> configure)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(groupName);
            ArgumentNullException.ThrowIfNull(configure);

            PropertyCollector collector = new();
            configure(collector);
            _members.Add(new GroupedBindingNode
            {
                GroupName = groupName,
                Bindings = collector.ToBindings(),
            });
            return this;
        }

        /// <inheritdoc/>
        public IObjectBuilder AddAttached(string attachedTypeName, Action<IPropertyCollector> configure)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(attachedTypeName);
            ArgumentNullException.ThrowIfNull(configure);

            PropertyCollector collector = new();
            configure(collector);
            _members.Add(new AttachedBindingNode
            {
                AttachedTypeName = attachedTypeName,
                Bindings = collector.ToBindings(),
            });
            return this;
        }

        /// <inheritdoc/>
        public IObjectBuilder HandleSignal(string handlerName, string body)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(handlerName);
            ArgumentException.ThrowIfNullOrWhiteSpace(body);
            _members.Add(new SignalHandlerNode
            {
                HandlerName = handlerName,
                Form = SignalHandlerForm.Block,
                Code = body,
            });
            return this;
        }

        /// <inheritdoc/>
        public ObjectDefinitionNode Build()
        {
            return new ObjectDefinitionNode
            {
                TypeName = QmlTypeName,
                Members = _members.ToImmutable(),
            };
        }
    }
}

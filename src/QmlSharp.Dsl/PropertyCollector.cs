using System.Collections.Immutable;
using QmlSharp.Qml.Ast;

#pragma warning disable MA0048

namespace QmlSharp.Dsl
{
    /// <summary>
    /// A collected grouped or attached property entry.
    /// </summary>
    /// <param name="PropertyName">The QML property or handler name.</param>
    /// <param name="Value">The AST value collected for the property.</param>
    public sealed record PropertyCollectionEntry(string PropertyName, BindingValue Value);

    /// <summary>
    /// Runtime collector for grouped and attached property callback builders.
    /// </summary>
    public sealed class PropertyCollector : IPropertyCollector
    {
        private readonly ImmutableArray<PropertyCollectionEntry>.Builder _entries =
            ImmutableArray.CreateBuilder<PropertyCollectionEntry>();

        /// <inheritdoc/>
        public ImmutableArray<PropertyCollectionEntry> Entries => _entries.ToImmutable();

        /// <inheritdoc/>
        public IPropertyCollector SetProperty(string propertyName, object? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
            _entries.Add(new PropertyCollectionEntry(propertyName, QmlValueConverter.ToBindingValue(value)));
            return this;
        }

        /// <inheritdoc/>
        public IPropertyCollector SetBinding(string propertyName, string expression)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
            ArgumentException.ThrowIfNullOrWhiteSpace(expression);
            _entries.Add(new PropertyCollectionEntry(propertyName, Values.Expression(expression)));
            return this;
        }

        /// <inheritdoc/>
        public IPropertyCollector HandleSignal(string handlerName, string body)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(handlerName);
            ArgumentException.ThrowIfNullOrWhiteSpace(body);
            _entries.Add(new PropertyCollectionEntry(handlerName, Values.Expression(body)));
            return this;
        }

        internal ImmutableArray<BindingNode> ToBindings()
        {
            ImmutableArray<BindingNode>.Builder bindings = ImmutableArray.CreateBuilder<BindingNode>(_entries.Count);

            foreach (PropertyCollectionEntry entry in _entries)
            {
                bindings.Add(new BindingNode
                {
                    PropertyName = entry.PropertyName,
                    Value = entry.Value,
                });
            }

            return bindings.ToImmutable();
        }
    }

    /// <summary>
    /// Factory for generated grouped and attached property collector interfaces.
    /// </summary>
    public static class PropertyCollectorFactory
    {
        /// <summary>Creates a collector proxy for a generated collector interface.</summary>
        /// <typeparam name="TCollector">Generated collector interface type.</typeparam>
        /// <param name="metadata">Metadata used to validate and map generated methods.</param>
        /// <returns>A collector implementing <typeparamref name="TCollector"/>.</returns>
        public static TCollector Create<TCollector>(PropertyCollectorMetadata metadata)
            where TCollector : class, IPropertyCollector
        {
            ArgumentNullException.ThrowIfNull(metadata);
            return DispatchProxyFactory.CreateCollectorProxy<TCollector>(metadata);
        }
    }
}

#pragma warning restore MA0048

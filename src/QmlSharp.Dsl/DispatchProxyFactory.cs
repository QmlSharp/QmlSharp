using System.Reflection;

#pragma warning disable IDE0058
#pragma warning disable MA0006
#pragma warning disable MA0048
#pragma warning disable MA0051

namespace QmlSharp.Dsl
{
    internal static class DispatchProxyFactory
    {
        public static TBuilder CreateObjectProxy<TBuilder>(string qmlTypeName, ObjectBuilderMetadata metadata)
            where TBuilder : class, IObjectBuilder
        {
            TBuilder proxy = DispatchProxy.Create<TBuilder, ObjectBuilderProxy>();
            ObjectBuilderProxy implementation = (ObjectBuilderProxy)(object)proxy;
            implementation.Configure(qmlTypeName, metadata);
            return proxy;
        }

        public static TCollector CreateCollectorProxy<TCollector>(PropertyCollectorMetadata metadata)
            where TCollector : class, IPropertyCollector
        {
            TCollector proxy = DispatchProxy.Create<TCollector, PropertyCollectorProxy>();
            PropertyCollectorProxy implementation = (PropertyCollectorProxy)(object)proxy;
            implementation.Configure(metadata);
            return proxy;
        }

        public static IPropertyCollector CreateCollectorProxy(Type collectorType, PropertyCollectorMetadata metadata)
        {
            MethodInfo createMethod = typeof(DispatchProxyFactory)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(static method =>
                    method.Name == nameof(CreateCollectorProxy)
                    && method.IsGenericMethodDefinition
                    && method.GetParameters().Length == 1)
                .MakeGenericMethod(collectorType);
            return (IPropertyCollector)createMethod.Invoke(null, new object[] { metadata })!;
        }
    }

    internal class ObjectBuilderProxy : DispatchProxy
    {
        private ObjectBuilder? _builder;
        private ObjectBuilderMetadata _metadata = ObjectBuilderMetadata.Empty;

        public void Configure(string qmlTypeName, ObjectBuilderMetadata metadata)
        {
            _builder = new ObjectBuilder(qmlTypeName);
            _metadata = metadata;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is null)
            {
                throw new InvalidOperationException("Builder proxy invocation did not include method metadata.");
            }

            ObjectBuilder builder = _builder ?? throw new InvalidOperationException("Builder proxy is not configured.");
            object?[] arguments = args ?? [];
            string methodName = targetMethod.Name;

            if (HandleObjectMethod(builder, targetMethod, arguments, out object? objectResult))
            {
                return objectResult;
            }

            if (methodName == nameof(IObjectBuilder.Id))
            {
                builder.Id(GetRequiredArgument<string>(arguments, 0, methodName));
                return ReturnBuilder(targetMethod);
            }

            if (methodName == nameof(IObjectBuilder.Child))
            {
                builder.Child(GetRequiredArgument<IObjectBuilder>(arguments, 0, methodName));
                return ReturnBuilder(targetMethod);
            }

            if (methodName == nameof(IObjectBuilder.Children))
            {
                builder.Children(GetRequiredArgument<IObjectBuilder[]>(arguments, 0, methodName));
                return ReturnBuilder(targetMethod);
            }

            if (methodName == nameof(IObjectBuilder.SetProperty))
            {
                builder.SetProperty(GetRequiredArgument<string>(arguments, 0, methodName), arguments.Length > 1 ? arguments[1] : null);
                return ReturnBuilder(targetMethod);
            }

            if (methodName == nameof(IObjectBuilder.SetBinding))
            {
                builder.SetBinding(
                    GetRequiredArgument<string>(arguments, 0, methodName),
                    GetRequiredArgument<string>(arguments, 1, methodName));
                return ReturnBuilder(targetMethod);
            }

            if (methodName == nameof(IObjectBuilder.AddGrouped))
            {
                builder.AddGrouped(
                    GetRequiredArgument<string>(arguments, 0, methodName),
                    GetRequiredArgument<Action<IPropertyCollector>>(arguments, 1, methodName));
                return ReturnBuilder(targetMethod);
            }

            if (methodName == nameof(IObjectBuilder.AddAttached))
            {
                builder.AddAttached(
                    GetRequiredArgument<string>(arguments, 0, methodName),
                    GetRequiredArgument<Action<IPropertyCollector>>(arguments, 1, methodName));
                return ReturnBuilder(targetMethod);
            }

            if (methodName == nameof(IObjectBuilder.HandleSignal))
            {
                builder.HandleSignal(
                    GetRequiredArgument<string>(arguments, 0, methodName),
                    GetRequiredArgument<string>(arguments, 1, methodName));
                return ReturnBuilder(targetMethod);
            }

            if (TryHandleGeneratedCallback(builder, targetMethod, arguments))
            {
                return ReturnBuilder(targetMethod);
            }

            if (TryHandleGeneratedBinding(builder, methodName, arguments))
            {
                return ReturnBuilder(targetMethod);
            }

            if (TryHandleGeneratedSignal(builder, methodName, arguments))
            {
                return ReturnBuilder(targetMethod);
            }

            if (TryHandleGeneratedProperty(builder, methodName, arguments))
            {
                return ReturnBuilder(targetMethod);
            }

            throw new MissingMethodException($"The builder method '{methodName}' is not supported by the QmlSharp DSL runtime.");
        }

        private bool HandleObjectMethod(
            ObjectBuilder builder,
            MethodInfo targetMethod,
            object?[] arguments,
            out object? result)
        {
            result = null;

            if (targetMethod.Name == $"get_{nameof(IObjectBuilder.QmlTypeName)}")
            {
                result = builder.QmlTypeName;
                return true;
            }

            if (targetMethod.Name == nameof(IObjectBuilder.Build))
            {
                result = builder.Build();
                return true;
            }

            if (targetMethod.DeclaringType == typeof(object))
            {
                if (targetMethod.Name == nameof(ToString))
                {
                    result = builder.QmlTypeName;
                    return true;
                }

                if (targetMethod.Name == nameof(GetHashCode))
                {
                    result = GetHashCode();
                    return true;
                }

                if (targetMethod.Name == nameof(Equals))
                {
                    result = ReferenceEquals(this, arguments[0]);
                    return true;
                }
            }

            return false;
        }

        private bool TryHandleGeneratedBinding(ObjectBuilder builder, string methodName, object?[] arguments)
        {
            if (!methodName.EndsWith("Bind", StringComparison.Ordinal) || arguments.Length != 1 || arguments[0] is not string expression)
            {
                return false;
            }

            string propertyMethodName = methodName[..^4];
            PropertyMethodMetadata? metadata = FindProperty(propertyMethodName);
            if (metadata is not null && !metadata.SupportsBinding)
            {
                throw new InvalidOperationException($"Property method '{propertyMethodName}' does not support expression bindings.");
            }

            builder.SetBinding(metadata?.PropertyName ?? NameConventions.ToQmlPropertyName(propertyMethodName), expression);
            return true;
        }

        private bool TryHandleGeneratedProperty(ObjectBuilder builder, string methodName, object?[] arguments)
        {
            if (arguments.Length != 1)
            {
                return false;
            }

            PropertyMethodMetadata? metadata = FindProperty(methodName);
            if (metadata is not null && !metadata.SupportsValue)
            {
                throw new InvalidOperationException($"Property method '{methodName}' does not support literal values.");
            }

            builder.SetProperty(metadata?.PropertyName ?? NameConventions.ToQmlPropertyName(methodName), arguments[0]);
            return true;
        }

        private bool TryHandleGeneratedSignal(ObjectBuilder builder, string methodName, object?[] arguments)
        {
            if (!methodName.StartsWith("On", StringComparison.Ordinal) || arguments.Length != 1 || arguments[0] is not string body)
            {
                return false;
            }

            if (FindProperty(methodName) is not null)
            {
                return false;
            }

            SignalMethodMetadata? metadata = FindSignal(methodName);
            builder.HandleSignal(metadata?.HandlerName ?? NameConventions.ToQmlSignalHandlerName(methodName), body);
            return true;
        }

        private bool TryHandleGeneratedCallback(ObjectBuilder builder, MethodInfo targetMethod, object?[] arguments)
        {
            if (arguments.Length != 1 || !TryGetCallbackTargetType(targetMethod, out Type? collectorType))
            {
                return false;
            }

            string methodName = targetMethod.Name;
            GroupedPropertyMethodMetadata? grouped = FindGrouped(methodName);
            AttachedPropertyMethodMetadata? attached = FindAttached(methodName);

            if (grouped is null && attached is null)
            {
                return false;
            }

            IPropertyCollector collector = DispatchProxyFactory.CreateCollectorProxy(
                collectorType!,
                grouped?.Collector ?? attached!.Collector);
            InvokeCollectorCallback(arguments[0], collector);

            if (grouped is not null)
            {
                builder.AddGrouped(grouped.GroupName, CopyCollector(collector));
                return true;
            }

            builder.AddAttached(attached!.AttachedTypeName, CopyCollector(collector));
            return true;
        }

        private static bool TryGetCallbackTargetType(MethodInfo method, out Type? collectorType)
        {
            collectorType = null;
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != 1 || !parameters[0].ParameterType.IsGenericType)
            {
                return false;
            }

            Type parameterType = parameters[0].ParameterType;
            if (parameterType.GetGenericTypeDefinition() != typeof(Action<>))
            {
                return false;
            }

            collectorType = parameterType.GetGenericArguments()[0];
            return typeof(IPropertyCollector).IsAssignableFrom(collectorType);
        }

        private static void InvokeCollectorCallback(object? callback, IPropertyCollector collector)
        {
            if (callback is null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            MethodInfo invokeMethod = callback.GetType().GetMethod("Invoke")
                ?? throw new InvalidOperationException("Collector callback does not have an Invoke method.");
            invokeMethod.Invoke(callback, new object[] { collector });
        }

        private static Action<IPropertyCollector> CopyCollector(IPropertyCollector source)
        {
            return target =>
            {
                foreach (PropertyCollectionEntry entry in source.Entries)
                {
                    target.SetProperty(entry.PropertyName, entry.Value);
                }
            };
        }

        private PropertyMethodMetadata? FindProperty(string methodName)
        {
            return _metadata.Properties
                .Where(property => StringComparer.Ordinal.Equals(property.MethodName, methodName))
                .FirstOrDefault();
        }

        private GroupedPropertyMethodMetadata? FindGrouped(string methodName)
        {
            return _metadata.GroupedProperties
                .Where(grouped => StringComparer.Ordinal.Equals(grouped.MethodName, methodName))
                .FirstOrDefault();
        }

        private AttachedPropertyMethodMetadata? FindAttached(string methodName)
        {
            return _metadata.AttachedProperties
                .Where(attached => StringComparer.Ordinal.Equals(attached.MethodName, methodName))
                .FirstOrDefault();
        }

        private SignalMethodMetadata? FindSignal(string methodName)
        {
            return _metadata.Signals
                .Where(signal => StringComparer.Ordinal.Equals(signal.MethodName, methodName))
                .FirstOrDefault();
        }

        private object? ReturnBuilder(MethodInfo targetMethod)
        {
            return targetMethod.ReturnType == typeof(void) ? null : this;
        }

        private static T GetRequiredArgument<T>(object?[] arguments, int index, string methodName)
        {
            if (arguments.Length <= index || arguments[index] is not T value)
            {
                throw new ArgumentException(
                    $"Method '{methodName}' requires argument {index} of type {typeof(T).Name}.",
                    nameof(arguments));
            }

            return value;
        }
    }

    internal class PropertyCollectorProxy : DispatchProxy
    {
        private readonly PropertyCollector _collector = new();
        private PropertyCollectorMetadata _metadata = PropertyCollectorMetadata.Empty;

        public void Configure(PropertyCollectorMetadata metadata)
        {
            _metadata = metadata;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is null)
            {
                throw new InvalidOperationException("Property collector proxy invocation did not include method metadata.");
            }

            object?[] arguments = args ?? [];
            string methodName = targetMethod.Name;

            if (methodName == $"get_{nameof(IPropertyCollector.Entries)}")
            {
                return _collector.Entries;
            }

            if (methodName == nameof(IPropertyCollector.SetProperty))
            {
                _collector.SetProperty(
                    GetRequiredArgument<string>(arguments, 0, methodName),
                    arguments.Length > 1 ? arguments[1] : null);
                return ReturnCollector(targetMethod);
            }

            if (methodName == nameof(IPropertyCollector.SetBinding))
            {
                _collector.SetBinding(
                    GetRequiredArgument<string>(arguments, 0, methodName),
                    GetRequiredArgument<string>(arguments, 1, methodName));
                return ReturnCollector(targetMethod);
            }

            if (methodName == nameof(IPropertyCollector.HandleSignal))
            {
                _collector.HandleSignal(
                    GetRequiredArgument<string>(arguments, 0, methodName),
                    GetRequiredArgument<string>(arguments, 1, methodName));
                return ReturnCollector(targetMethod);
            }

            if (TryHandleGeneratedBinding(methodName, arguments))
            {
                return ReturnCollector(targetMethod);
            }

            if (TryHandleGeneratedSignal(methodName, arguments))
            {
                return ReturnCollector(targetMethod);
            }

            if (TryHandleGeneratedProperty(methodName, arguments))
            {
                return ReturnCollector(targetMethod);
            }

            throw new MissingMethodException($"The collector method '{methodName}' is not supported by the QmlSharp DSL runtime.");
        }

        private bool TryHandleGeneratedBinding(string methodName, object?[] arguments)
        {
            if (!methodName.EndsWith("Bind", StringComparison.Ordinal) || arguments.Length != 1 || arguments[0] is not string expression)
            {
                return false;
            }

            string propertyMethodName = methodName[..^4];
            PropertyMethodMetadata? metadata = FindProperty(propertyMethodName);
            if (metadata is null && _metadata.Properties.Length > 0)
            {
                return false;
            }

            if (metadata is not null && !metadata.SupportsBinding)
            {
                throw new InvalidOperationException($"Property method '{propertyMethodName}' does not support expression bindings.");
            }

            _collector.SetBinding(metadata?.PropertyName ?? NameConventions.ToQmlPropertyName(propertyMethodName), expression);
            return true;
        }

        private bool TryHandleGeneratedProperty(string methodName, object?[] arguments)
        {
            if (arguments.Length != 1)
            {
                return false;
            }

            PropertyMethodMetadata? metadata = FindProperty(methodName);
            if (metadata is null && _metadata.Properties.Length > 0)
            {
                return false;
            }

            if (metadata is not null && !metadata.SupportsValue)
            {
                throw new InvalidOperationException($"Property method '{methodName}' does not support literal values.");
            }

            _collector.SetProperty(metadata?.PropertyName ?? NameConventions.ToQmlPropertyName(methodName), arguments[0]);
            return true;
        }

        private bool TryHandleGeneratedSignal(string methodName, object?[] arguments)
        {
            if (!methodName.StartsWith("On", StringComparison.Ordinal) || arguments.Length != 1 || arguments[0] is not string body)
            {
                return false;
            }

            if (FindProperty(methodName) is not null)
            {
                return false;
            }

            SignalMethodMetadata? metadata = FindSignal(methodName);
            if (metadata is null && _metadata.Signals.Length > 0)
            {
                return false;
            }

            _collector.HandleSignal(metadata?.HandlerName ?? NameConventions.ToQmlSignalHandlerName(methodName), body);
            return true;
        }

        private PropertyMethodMetadata? FindProperty(string methodName)
        {
            return _metadata.Properties
                .Where(property => StringComparer.Ordinal.Equals(property.MethodName, methodName))
                .FirstOrDefault();
        }

        private SignalMethodMetadata? FindSignal(string methodName)
        {
            return _metadata.Signals
                .Where(signal => StringComparer.Ordinal.Equals(signal.MethodName, methodName))
                .FirstOrDefault();
        }

        private object? ReturnCollector(MethodInfo targetMethod)
        {
            return targetMethod.ReturnType == typeof(void) ? null : this;
        }

        private static T GetRequiredArgument<T>(object?[] arguments, int index, string methodName)
        {
            if (arguments.Length <= index || arguments[index] is not T value)
            {
                throw new ArgumentException(
                    $"Method '{methodName}' requires argument {index} of type {typeof(T).Name}.",
                    nameof(arguments));
            }

            return value;
        }
    }
}

#pragma warning restore MA0051
#pragma warning restore MA0048
#pragma warning restore MA0006
#pragma warning restore IDE0058

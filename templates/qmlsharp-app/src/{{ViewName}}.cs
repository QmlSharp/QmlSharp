using QmlSharp.Core;
using QmlSharp.Dsl;

namespace {{RootNamespace}};

public sealed class {{ViewName}} : View<{{ViewModelName}}>
{
    public override object Build() =>
        Column().Children(
            Text().TextBind("{{StateBindingExpression}}"),
            Button().Text("{{ButtonText}}").OnClicked(() => Vm.{{CommandName}}()));

    private static IObjectBuilder Column() => throw new InvalidOperationException("Compiler-only QML DSL factory.");

    private static IObjectBuilder Text() => throw new InvalidOperationException("Compiler-only QML DSL factory.");

    private static IObjectBuilder Button() => throw new InvalidOperationException("Compiler-only QML DSL factory.");
}

internal static class {{ProjectName}}DslExtensions
{
    public static IObjectBuilder Text(this IObjectBuilder builder, string value) => builder.SetProperty("text", value);

    public static IObjectBuilder TextBind(this IObjectBuilder builder, string expression) => builder.SetBinding("text", expression);

    public static IObjectBuilder OnClicked(this IObjectBuilder builder, Action handler) => builder.HandleSignal("onClicked", "{{CommandNameLower}}()");
}

using QmlSharp.Core;
using QmlSharp.Dsl;

namespace {{RootNamespace}};

public sealed class LibraryView : View<LibraryViewModel>
{
    public override object Build() =>
        Text().TextBind("Vm.Label");

    private static IObjectBuilder Text() => throw new InvalidOperationException("Compiler-only QML DSL factory.");
}

internal static class {{ProjectName}}LibraryDslExtensions
{
    public static IObjectBuilder TextBind(this IObjectBuilder builder, string expression) => builder.SetBinding("text", expression);
}

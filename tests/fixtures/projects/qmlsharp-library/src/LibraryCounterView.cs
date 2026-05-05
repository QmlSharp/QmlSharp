using QmlSharp.Core;
using QmlSharp.Dsl;

namespace CounterLibrary;

public sealed class LibraryCounterView : View<LibraryCounterViewModel>
{
    public override object Build() =>
        Column().Children(
            Text().TextBind("Vm.Value.toString()"));

    private static IObjectBuilder Column() => throw new NotImplementedException();
    private static IObjectBuilder Text() => throw new NotImplementedException();
}

internal static class LibraryDslExtensions
{
    public static IObjectBuilder TextBind(this IObjectBuilder builder, string expression) => builder;
}

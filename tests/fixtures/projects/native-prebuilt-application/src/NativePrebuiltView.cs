using QmlSharp.Core;
using QmlSharp.Dsl;

namespace NativePrebuiltApplication;

public sealed class NativePrebuiltView : View<NativePrebuiltViewModel>
{
    public override object Build() =>
        Column().Children(Text().TextBind("Vm.Status"));

    private static IObjectBuilder Column() => throw new NotImplementedException();
    private static IObjectBuilder Text() => throw new NotImplementedException();
}

internal static class NativePrebuiltDslExtensions
{
    public static IObjectBuilder TextBind(this IObjectBuilder builder, string expression) => builder;
}

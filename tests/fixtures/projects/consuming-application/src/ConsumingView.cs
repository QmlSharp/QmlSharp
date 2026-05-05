using QmlSharp.Core;
using QmlSharp.Dsl;

namespace ConsumingApplication;

public sealed class ConsumingView : View<ConsumingViewModel>
{
    public override object Build() =>
        Column().Children(Text().TextBind("Vm.Title"));

    private static IObjectBuilder Column() => throw new NotImplementedException();
    private static IObjectBuilder Text() => throw new NotImplementedException();
}

internal static class ConsumingDslExtensions
{
    public static IObjectBuilder TextBind(this IObjectBuilder builder, string expression) => builder;
}

using QmlSharp.Core;
using QmlSharp.Dsl;

namespace AssetsApplication;

public sealed class AssetsView : View<AssetsViewModel>
{
    public override object Build() =>
        Column().Children(
            Text().TextBind("Vm.Title"),
            Image().Source("qrc:/images/logo.png"));

    private static IObjectBuilder Column() => throw new NotImplementedException();
    private static IObjectBuilder Text() => throw new NotImplementedException();
    private static IObjectBuilder Image() => throw new NotImplementedException();
}

internal static class AssetsDslExtensions
{
    public static IObjectBuilder TextBind(this IObjectBuilder builder, string expression) => builder;

    public static IObjectBuilder Source(this IObjectBuilder builder, string value) => builder;
}

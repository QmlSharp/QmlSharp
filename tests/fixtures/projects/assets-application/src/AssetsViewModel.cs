using QmlSharp.Core;

namespace AssetsApplication;

[ViewModel]
public sealed class AssetsViewModel
{
    [State]
    public string Title { get; set; } = "Assets";
}

using QmlSharp.Core;

namespace NativePrebuiltApplication;

[ViewModel]
public sealed class NativePrebuiltViewModel
{
    [State]
    public string Status { get; set; } = "prebuilt";
}

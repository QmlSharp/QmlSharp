using QmlSharp.Core;

namespace {{RootNamespace}};

[ViewModel]
public sealed class LibraryViewModel
{
    [State]
    public string Label { get; private set; } = "{{ProjectName}} library item";

    [Command]
    public void ResetLabel()
    {
        Label = "{{ProjectName}} library item";
    }
}

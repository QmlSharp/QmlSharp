using QmlSharp.Core;

namespace {{RootNamespace}};

[ViewModel]
public sealed class {{ViewModelName}}
{
    [State]
    public {{StateType}} {{StateName}} { get; private set; } = {{StateInitialValue}};

    [Command]
    public void {{CommandName}}()
    {
{{CommandBody}}
    }
}

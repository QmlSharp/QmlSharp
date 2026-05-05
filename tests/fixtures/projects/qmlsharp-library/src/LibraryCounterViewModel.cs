using QmlSharp.Core;

namespace CounterLibrary;

[ViewModel]
public sealed class LibraryCounterViewModel
{
    [State]
    public int Value { get; set; }

    [Command]
    public void Bump()
    {
        Value++;
    }
}

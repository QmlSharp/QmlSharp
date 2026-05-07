using QmlSharp.Core;

namespace DevToolsCounterFlow;

[ViewModel]
public sealed class CounterViewModel
{
    [State]
    public int Count { get; set; }

    [Command]
    public void Increment()
    {
        Count++;
    }

    [Command]
    public void Decrement()
    {
        Count--;
    }
}

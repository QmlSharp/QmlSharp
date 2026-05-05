using QmlSharp.Core;

namespace ConsumingApplication;

[ViewModel]
public sealed class ConsumingViewModel
{
    [State]
    public string Title { get; set; } = "Uses library";
}

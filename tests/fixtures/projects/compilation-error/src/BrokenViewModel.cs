using QmlSharp.Core;

namespace CompilationErrorApplication;

[ViewModel]
public sealed class BrokenViewModel
{
    [State]
    public MissingType Broken { get; set; } = default!;
}

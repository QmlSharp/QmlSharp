namespace QmlSharp.Core
{
    /// <summary>
    /// Marks a ViewModel method as a fire-and-forget command callable from QML.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class CommandAttribute : Attribute
    {
    }
}

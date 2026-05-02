namespace QmlSharp.Core
{
    /// <summary>
    /// Marks a ViewModel event as an effect signal emitted to QML.
    /// </summary>
    [AttributeUsage(AttributeTargets.Event)]
    public sealed class EffectAttribute : Attribute
    {
    }
}

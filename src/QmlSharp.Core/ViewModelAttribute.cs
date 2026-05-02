namespace QmlSharp.Core
{
    /// <summary>
    /// Marks a class as a QmlSharp ViewModel for compiler discovery.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ViewModelAttribute : Attribute
    {
    }
}

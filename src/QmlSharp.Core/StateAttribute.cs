namespace QmlSharp.Core
{
    /// <summary>
    /// Marks a ViewModel property as reactive state exposed to QML.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class StateAttribute : Attribute
    {
        /// <summary>
        /// Gets a value indicating whether the generated QML property is read-only.
        /// </summary>
        public bool Readonly { get; init; }

        /// <summary>
        /// Gets a value indicating whether initial state may be sent lazily.
        /// </summary>
        public bool Deferred { get; init; }
    }
}

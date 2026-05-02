namespace QmlSharp.Core
{
    /// <summary>
    /// Base type for user-authored QmlSharp views bound to a ViewModel type.
    /// </summary>
    /// <typeparam name="TViewModel">The ViewModel type used by the view.</typeparam>
    public abstract class View<TViewModel>
        where TViewModel : class
    {
        /// <summary>
        /// Gets the ViewModel expression sentinel used by compiler analysis.
        /// </summary>
        protected TViewModel Vm => throw new InvalidOperationException("ViewModel access is compiler-only.");

        /// <summary>
        /// Builds the view DSL object tree.
        /// </summary>
        /// <returns>The DSL object tree root.</returns>
        public abstract object Build();
    }
}

namespace QmlSharp.Build
{
    internal sealed class BuildStageException : Exception
    {
        public BuildStageException(string message)
            : base(message)
        {
        }

        public BuildStageException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}

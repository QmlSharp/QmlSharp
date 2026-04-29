#pragma warning disable MA0048

namespace QmlSharp.Qt.Tools
{
    /// <summary>Thrown when no Qt installation can be found.</summary>
    public sealed class QtInstallationNotFoundError : Exception
    {
        /// <summary>Create a new Qt installation discovery error.</summary>
        public QtInstallationNotFoundError(string message, ImmutableArray<string> attemptedSteps)
            : base(message)
        {
            AttemptedSteps = attemptedSteps;
        }

        /// <summary>Discovery steps attempted and their results.</summary>
        public ImmutableArray<string> AttemptedSteps { get; }
    }

    /// <summary>Thrown when a specific Qt tool is not found at the expected path.</summary>
    public sealed class QtToolNotFoundError : Exception
    {
        /// <summary>Create a new missing-tool error.</summary>
        public QtToolNotFoundError(string toolName, string expectedPath)
            : base($"Qt tool '{toolName}' not found at '{expectedPath}'")
        {
            ToolName = toolName;
            ExpectedPath = expectedPath;
        }

        /// <summary>Name of the missing tool.</summary>
        public string ToolName { get; }

        /// <summary>Expected path where the tool was looked for.</summary>
        public string ExpectedPath { get; }
    }

    /// <summary>Thrown when a Qt tool process exceeds its timeout.</summary>
    public sealed class QtToolTimeoutError : Exception
    {
        /// <summary>Create a new timeout error.</summary>
        public QtToolTimeoutError(
            string toolName,
            TimeSpan timeout,
            string partialStdout,
            string partialStderr)
            : base($"Qt tool '{toolName}' timed out after {timeout.TotalSeconds}s")
        {
            ToolName = toolName;
            Timeout = timeout;
            PartialStdout = partialStdout;
            PartialStderr = partialStderr;
        }

        /// <summary>Name of the tool that timed out.</summary>
        public string ToolName { get; }

        /// <summary>Configured timeout that was exceeded.</summary>
        public TimeSpan Timeout { get; }

        /// <summary>Partial stdout captured before timeout.</summary>
        public string PartialStdout { get; }

        /// <summary>Partial stderr captured before timeout.</summary>
        public string PartialStderr { get; }
    }
}

#pragma warning restore MA0048

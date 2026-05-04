#pragma warning disable MA0048

namespace QmlSharp.Build
{
    /// <summary>CLI options for the build command.</summary>
    public sealed record BuildCommandOptions
    {
        /// <summary>Force full rebuild, ignoring incremental cache.</summary>
        public bool Force { get; init; }

        /// <summary>Only compile files matching this glob pattern.</summary>
        public string? Files { get; init; }

        /// <summary>Validate config and report what would change without building.</summary>
        public bool DryRun { get; init; }

        /// <summary>Output structured JSON progress and result.</summary>
        public bool Json { get; init; }

        /// <summary>Build as reusable library.</summary>
        public bool Library { get; init; }

        /// <summary>Path to the project directory.</summary>
        public string ProjectDir { get; init; } = ".";
    }

    /// <summary>CLI options for the dev command.</summary>
    public sealed record DevCommandOptions
    {
        /// <summary>Run without launching the Qt window.</summary>
        public bool Headless { get; init; }

        /// <summary>Override entry point path.</summary>
        public string? Entry { get; init; }

        /// <summary>Path to the project directory.</summary>
        public string ProjectDir { get; init; } = ".";
    }

    /// <summary>CLI options for the doctor command.</summary>
    public sealed record DoctorCommandOptions
    {
        /// <summary>Attempt to fix issues automatically.</summary>
        public bool Fix { get; init; }

        /// <summary>Path to the project directory.</summary>
        public string ProjectDir { get; init; } = ".";
    }

    /// <summary>CLI options for the init command.</summary>
    public sealed record InitCommandOptions
    {
        /// <summary>Project template name.</summary>
        public string Template { get; init; } = "default";

        /// <summary>Target directory for the new project.</summary>
        public string TargetDir { get; init; } = ".";
    }

    /// <summary>CLI options for the clean command.</summary>
    public sealed record CleanCommandOptions
    {
        /// <summary>Also clear the incremental compilation cache.</summary>
        public bool Cache { get; init; }

        /// <summary>Path to the project directory.</summary>
        public string ProjectDir { get; init; } = ".";
    }
}

#pragma warning restore MA0048

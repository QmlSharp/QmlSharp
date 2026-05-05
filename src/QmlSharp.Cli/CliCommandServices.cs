using QmlSharp.Build;

namespace QmlSharp.Cli
{
    /// <summary>Service bundle used by QmlSharp CLI command registration.</summary>
    public sealed record CliCommandServices
    {
        /// <summary>Configuration loader used by build and dev commands.</summary>
        public required IConfigLoader ConfigLoader { get; init; }

        /// <summary>Build pipeline used by the build command.</summary>
        public required IBuildPipeline BuildPipeline { get; init; }

        /// <summary>Development session used by the dev command.</summary>
        public required IDevSession DevSession { get; init; }

        /// <summary>Doctor service used by the doctor command.</summary>
        public required IDoctor Doctor { get; init; }

        /// <summary>Init service used by the init command.</summary>
        public required IInitService InitService { get; init; }

        /// <summary>Clean service used by the clean command.</summary>
        public required ICleanService CleanService { get; init; }

        /// <summary>Creates default command services for the current build-system implementation wave.</summary>
        public static CliCommandServices CreateDefault()
        {
            return new CliCommandServices
            {
                ConfigLoader = new ConfigLoader(),
                BuildPipeline = new BuildPipeline(),
                DevSession = new CommandShellDevSession(),
                Doctor = new CommandShellDoctor(),
                InitService = new InitService(),
                CleanService = new CleanService(),
            };
        }
    }
}

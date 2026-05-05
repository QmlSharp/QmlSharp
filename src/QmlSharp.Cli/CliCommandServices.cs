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

        /// <summary>Optional factory for project-directory-aware doctor services.</summary>
        public Func<string, IDoctor>? DoctorFactory { get; init; }

        /// <summary>Init service used by the init command.</summary>
        public required IInitService InitService { get; init; }

        /// <summary>Clean service used by the clean command.</summary>
        public required ICleanService CleanService { get; init; }

        /// <summary>Creates default command services for the current build-system implementation wave.</summary>
        public static CliCommandServices CreateDefault()
        {
            ConfigLoader configLoader = new();
            BuildPipeline buildPipeline = new();

            return new CliCommandServices
            {
                ConfigLoader = configLoader,
                BuildPipeline = buildPipeline,
                DevSession = new DevSession(configLoader, buildPipeline),
                Doctor = new Doctor(),
                DoctorFactory = static projectDir => new Doctor(projectDir),
                InitService = new InitService(),
                CleanService = new CleanService(),
            };
        }

        /// <summary>Creates a doctor for a parsed project directory.</summary>
        public IDoctor CreateDoctor(string projectDir)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectDir);
            return DoctorFactory is null ? Doctor : DoctorFactory(projectDir);
        }
    }
}

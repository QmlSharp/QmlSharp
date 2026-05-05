namespace QmlSharp.Build
{
    internal interface IDoctorEnvironment
    {
        string CurrentDirectory { get; }

        PlatformTarget CurrentPlatform { get; }

        string? GetEnvironmentVariable(string name);

        bool FileExists(string path);

        IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption);

        Stream OpenRead(string path);

        Task<DoctorProcessResult> RunAsync(
            string executablePath,
            ImmutableArray<string> arguments,
            string? workingDirectory,
            CancellationToken cancellationToken);
    }
}

namespace QmlSharp.Build.Tests.Infrastructure
{
    public sealed class MockCMakeBuilder : ICMakeBuilder
    {
        private readonly Queue<CMakeStepResult> configureResults = new();
        private readonly Queue<CMakeStepResult> buildResults = new();
        private readonly bool createLibraryOnBuild;

        public MockCMakeBuilder(bool createLibraryOnBuild = true)
        {
            this.createLibraryOnBuild = createLibraryOnBuild;
        }

        public string? LastConfigureBuildDir { get; private set; }
        public string? LastConfigurePreset { get; private set; }
        public string? LastBuildDir { get; private set; }
        public int ConfigureCallCount { get; private set; }
        public int BuildCallCount { get; private set; }

        public void EnqueueConfigureResult(CMakeStepResult result)
        {
            configureResults.Enqueue(result);
        }

        public void EnqueueBuildResult(CMakeStepResult result)
        {
            buildResults.Enqueue(result);
        }

        public Task<CMakeStepResult> ConfigureAsync(
            string buildDir,
            string preset,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ConfigureCallCount++;
            LastConfigureBuildDir = buildDir;
            LastConfigurePreset = preset;
            return Task.FromResult(configureResults.Count == 0 ? Success() : configureResults.Dequeue());
        }

        public Task<CMakeStepResult> BuildAsync(
            string buildDir,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BuildCallCount++;
            LastBuildDir = buildDir;
            CMakeStepResult result = buildResults.Count == 0 ? Success() : buildResults.Dequeue();
            if (result.Success && createLibraryOnBuild)
            {
                string outputPath = GetOutputLibraryPath(buildDir);
                string? outputDirectory = System.IO.Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrWhiteSpace(outputDirectory))
                {
                    _ = Directory.CreateDirectory(outputDirectory);
                }

                File.WriteAllText(outputPath, "native");
            }

            return Task.FromResult(result);
        }

        public string GetOutputLibraryPath(string buildDir)
        {
            return System.IO.Path.Join(buildDir, "qmlsharp_native.dll");
        }

        private static CMakeStepResult Success()
        {
            return new CMakeStepResult(true, string.Empty, string.Empty, TimeSpan.Zero, 0);
        }
    }
}

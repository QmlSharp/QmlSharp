namespace QmlSharp.Build.Tests.Infrastructure
{
    public sealed class MockCMakeBuilder : ICMakeBuilder
    {
        public string? LastConfigureBuildDir { get; private set; }
        public string? LastConfigurePreset { get; private set; }
        public string? LastBuildDir { get; private set; }

        public Task<CMakeStepResult> ConfigureAsync(string buildDir, string preset)
        {
            LastConfigureBuildDir = buildDir;
            LastConfigurePreset = preset;
            return Task.FromResult(Success());
        }

        public Task<CMakeStepResult> BuildAsync(string buildDir)
        {
            LastBuildDir = buildDir;
            return Task.FromResult(Success());
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

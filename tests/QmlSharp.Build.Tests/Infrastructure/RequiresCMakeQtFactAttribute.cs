namespace QmlSharp.Build.Tests.Infrastructure
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    internal sealed class RequiresCMakeQtFactAttribute : FactAttribute
    {
        public RequiresCMakeQtFactAttribute()
        {
            string? qtDir = Environment.GetEnvironmentVariable("QT_DIR");
            if (string.IsNullOrWhiteSpace(qtDir))
            {
                Skip = "QT_DIR is not set.";
                return;
            }

            string qtConfigPath = Path.Join(qtDir, "lib", "cmake", "Qt6", "Qt6Config.cmake");
            if (!File.Exists(qtConfigPath))
            {
                Skip = "QT_DIR does not contain lib/cmake/Qt6/Qt6Config.cmake.";
                return;
            }

            if (!CanFindExecutable("cmake"))
            {
                Skip = "cmake was not found on PATH.";
            }
        }

        private static bool CanFindExecutable(string name)
        {
            string? path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (GetCandidateNames(name).Any(candidateName => File.Exists(Path.Join(directory, candidateName))))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<string> GetCandidateNames(string name)
        {
            yield return name;
            if (OperatingSystem.IsWindows())
            {
                yield return name + ".exe";
                yield return name + ".cmd";
                yield return name + ".bat";
            }
        }
    }
}

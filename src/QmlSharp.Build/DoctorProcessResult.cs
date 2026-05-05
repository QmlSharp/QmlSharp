namespace QmlSharp.Build
{
    internal sealed record DoctorProcessResult(
        bool Started,
        int ExitCode,
        string Stdout,
        string Stderr)
    {
        public bool Success => Started && ExitCode == 0;

        public string CombinedOutput => string.Join(
            "\n",
            new[] { Stdout.Trim(), Stderr.Trim() }.Where(static value => value.Length > 0));
    }
}

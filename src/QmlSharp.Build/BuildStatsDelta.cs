namespace QmlSharp.Build
{
    internal sealed record BuildStatsDelta
    {
        public static BuildStatsDelta Empty { get; } = new();

        public int FilesCompiled { get; init; }

        public int SchemasGenerated { get; init; }

        public int CppFilesGenerated { get; init; }

        public int AssetsCollected { get; init; }

        public bool NativeLibBuilt { get; init; }
    }
}

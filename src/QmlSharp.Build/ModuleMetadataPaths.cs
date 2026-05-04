using System.Text;

namespace QmlSharp.Build
{
    internal static class ModuleMetadataPaths
    {
        public static string GetQmltypesFileName(string moduleUri)
        {
            ValidateModuleUri(moduleUri);

            StringBuilder builder = new(moduleUri.Length + ".qmltypes".Length);
            foreach (char character in moduleUri)
            {
                builder.Append(character == '.' ? '_' : char.ToLowerInvariant(character));
            }

            builder.Append(".qmltypes");
            return builder.ToString();
        }

        public static string GetModuleRelativeDirectory(string moduleUri)
        {
            ValidateModuleUri(moduleUri);
            string[] segments = moduleUri.Split('.');
            return string.Join(Path.DirectorySeparatorChar, segments);
        }

        public static string GetModuleDirectory(string outputDir, string moduleUri)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(outputDir);
            string moduleRelativeDirectory = GetModuleRelativeDirectory(moduleUri);
            if (Path.IsPathRooted(moduleRelativeDirectory))
            {
                throw new ArgumentException("Module URI must resolve to a relative directory.", nameof(moduleUri));
            }

            return Path.Join(outputDir, "qml", moduleRelativeDirectory);
        }

        public static string ToQmlRelativePath(string path)
        {
            return path.Replace('\\', '/');
        }

        private static void ValidateModuleUri(string moduleUri)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(moduleUri);
            if (Path.IsPathRooted(moduleUri) || moduleUri.Contains('/') || moduleUri.Contains('\\'))
            {
                throw new ArgumentException("Module URI must not contain rooted paths or directory separators.", nameof(moduleUri));
            }

            string[] segments = moduleUri.Split('.');
            if (segments.Any(static segment => string.IsNullOrWhiteSpace(segment) || segment is "." or ".."))
            {
                throw new ArgumentException("Module URI must not contain empty or traversal segments.", nameof(moduleUri));
            }

            if (segments.Any(static segment => segment.Contains(':')))
            {
                throw new ArgumentException("Module URI segments must not contain drive or scheme separators.", nameof(moduleUri));
            }
        }
    }
}

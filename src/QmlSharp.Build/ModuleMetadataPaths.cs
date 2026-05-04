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
            return Path.Combine(segments);
        }

        public static string GetModuleDirectory(string outputDir, string moduleUri)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(outputDir);
            return Path.Combine(outputDir, "qml", GetModuleRelativeDirectory(moduleUri));
        }

        public static string ToQmlRelativePath(string path)
        {
            return path.Replace('\\', '/');
        }

        private static void ValidateModuleUri(string moduleUri)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(moduleUri);
            string[] segments = moduleUri.Split('.');
            if (segments.Any(static segment => string.IsNullOrWhiteSpace(segment)))
            {
                throw new ArgumentException("Module URI must not contain empty segments.", nameof(moduleUri));
            }
        }
    }
}

#pragma warning disable MA0048

namespace QmlSharp.Build
{
    /// <summary>Maps schema QML type names to generated C++ QObject surface types.</summary>
    public static class CppTypeMap
    {
        /// <summary>Returns the generated C++ type for a schema QML type name.</summary>
        public static string ToCppType(string qmlType)
        {
            return TryMap(qmlType, out CppTypeMapping mapping)
                ? mapping.CppType
                : "QVariant";
        }

        /// <summary>Returns the generated default-value literal for a schema QML type name.</summary>
        public static string DefaultValue(string qmlType)
        {
            CppTypeMapping mapping = Map(qmlType);
            return mapping.CppType switch
            {
                "int" => "0",
                "double" => "0.0",
                "bool" => "false",
                "QString" => "QString()",
                "QUrl" => "QUrl()",
                "QColor" => "QColor()",
                "QDate" => "QDate()",
                "QVariantList" => "QVariantList()",
                "QPointF" => "QPointF()",
                "QRectF" => "QRectF()",
                "QSizeF" => "QSizeF()",
                _ => "QVariant()",
            };
        }

        /// <summary>Returns true when the type has a typed state-sync fast path.</summary>
        public static bool HasFastPath(string qmlType)
        {
            return Map(qmlType).FastPath is not CppFastPath.Json;
        }

        internal static CppTypeMapping Map(string qmlType)
        {
            return TryMap(qmlType, out CppTypeMapping mapping)
                ? mapping
                : new CppTypeMapping("QVariant", CppFastPath.Json, IsSupported: false);
        }

        internal static bool IsSupported(string qmlType)
        {
            return TryMap(qmlType, out CppTypeMapping _);
        }

        private static bool TryMap(string qmlType, out CppTypeMapping mapping)
        {
            string normalized = Normalize(qmlType);
            mapping = normalized switch
            {
                "int" => new CppTypeMapping("int", CppFastPath.Int, IsSupported: true),
                "double" or "real" or "number" => new CppTypeMapping("double", CppFastPath.Double, IsSupported: true),
                "bool" => new CppTypeMapping("bool", CppFastPath.Bool, IsSupported: true),
                "string" => new CppTypeMapping("QString", CppFastPath.String, IsSupported: true),
                "url" => new CppTypeMapping("QUrl", CppFastPath.Json, IsSupported: true),
                "color" => new CppTypeMapping("QColor", CppFastPath.Json, IsSupported: true),
                "date" => new CppTypeMapping("QDate", CppFastPath.Json, IsSupported: true),
                "var" or "variant" => new CppTypeMapping("QVariant", CppFastPath.Json, IsSupported: true),
                "list" => new CppTypeMapping("QVariantList", CppFastPath.Json, IsSupported: true),
                "point" => new CppTypeMapping("QPointF", CppFastPath.Json, IsSupported: true),
                "rect" => new CppTypeMapping("QRectF", CppFastPath.Json, IsSupported: true),
                "size" => new CppTypeMapping("QSizeF", CppFastPath.Json, IsSupported: true),
                _ => new CppTypeMapping("QVariant", CppFastPath.Json, IsSupported: false),
            };
            return mapping.IsSupported;
        }

        private static string Normalize(string qmlType)
        {
            if (string.IsNullOrWhiteSpace(qmlType))
            {
                return string.Empty;
            }

            string normalized = qmlType.Trim().ToLowerInvariant();
            return normalized.StartsWith("list<", StringComparison.Ordinal)
                ? "list"
                : normalized;
        }
    }

    internal sealed record CppTypeMapping(string CppType, CppFastPath FastPath, bool IsSupported);

    internal enum CppFastPath
    {
        Json,
        Int,
        Double,
        Bool,
        String,
    }
}

#pragma warning restore MA0048

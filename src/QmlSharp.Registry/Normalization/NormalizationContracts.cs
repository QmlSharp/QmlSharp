#pragma warning disable MA0048

using QmlSharp.Registry.Parsing;

namespace QmlSharp.Registry.Normalization
{
    /// <summary>
    /// Maps between C++ type names and QML type names.
    /// Contains 23+ built-in mappings and supports custom user-defined mappings.
    /// </summary>
    public interface ITypeNameMapper
    {
        string ToQmlName(string cppTypeName);

        string ToCppName(string qmlTypeName);

        bool HasMapping(string cppTypeName);

        IReadOnlyDictionary<string, string> GetAllMappings();

        ITypeNameMapper WithCustomMappings(IReadOnlyDictionary<string, string> customMappings);
    }

    /// <summary>
    /// Merges parsed data from three sources (qmltypes, qmldir, metatypes)
    /// into a unified QmlRegistry using the qmltypes-primary strategy.
    /// </summary>
    public interface ITypeNormalizer
    {
        NormalizeResult Normalize(
            IReadOnlyList<RawQmltypesFile> qmltypesFiles,
            IReadOnlyList<(string ModuleUri, RawQmldirFile File)> qmldirFiles,
            IReadOnlyList<RawMetatypesFile> metatypesFiles,
            ITypeNameMapper typeNameMapper);
    }
}

#pragma warning restore MA0048

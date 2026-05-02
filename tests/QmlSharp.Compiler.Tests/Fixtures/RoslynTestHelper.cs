using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using QmlSharp.Core;
using QmlSharp.Dsl;

namespace QmlSharp.Compiler.Tests.Fixtures
{
    internal static class RoslynTestHelper
    {
        public static CSharpCompilation CreateCompilation(params string[] sources)
        {
            ImmutableArray<(string FileName, string Source)>.Builder namedSources =
                ImmutableArray.CreateBuilder<(string FileName, string Source)>(sources.Length);

            for (int index = 0; index < sources.Length; index++)
            {
                namedSources.Add(($"Source{index}.cs", sources[index]));
            }

            return CreateCompilation(namedSources.ToImmutable());
        }

        public static CSharpCompilation CreateCompilation(ImmutableArray<(string FileName, string Source)> sources)
        {
            IEnumerable<SyntaxTree> syntaxTrees = sources.Select(source =>
                CSharpSyntaxTree.ParseText(
                    source.Source,
                    new CSharpParseOptions(LanguageVersion.Latest),
                    path: source.FileName));

            return CSharpCompilation.Create(
                "QmlSharp.Compiler.Tests.InMemory",
                syntaxTrees,
                CreateMetadataReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        }

        public static (CSharpCompilation Compilation, SemanticModel Model) CreateWithModel(string source)
        {
            CSharpCompilation compilation = CreateCompilation(source);
            SyntaxTree syntaxTree = compilation.SyntaxTrees.Single();
            SemanticModel model = compilation.GetSemanticModel(syntaxTree);
            return (compilation, model);
        }

        public static ProjectContext CreateContext(ImmutableArray<(string FileName, string Source)> sources)
        {
            CSharpCompilation compilation = CreateCompilation(sources);
            return new CSharpAnalyzer().CreateInMemoryProjectContext(
                CompilerTestFixtures.DefaultOptions,
                compilation,
                sources.Select(static source => source.FileName).ToImmutableArray());
        }

        private static ImmutableArray<MetadataReference> CreateMetadataReferences()
        {
            string? trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
            if (trustedPlatformAssemblies is null)
            {
                throw new InvalidOperationException("Trusted platform assemblies are unavailable.");
            }

            ImmutableArray<MetadataReference>.Builder references = ImmutableArray.CreateBuilder<MetadataReference>();
            foreach (string path in trustedPlatformAssemblies.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }

            references.Add(MetadataReference.CreateFromFile(typeof(View<>).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(IObjectBuilder).Assembly.Location));

            return references
                .GroupBy(reference => reference.Display, StringComparer.OrdinalIgnoreCase)
                .Select(grouping => grouping.First())
                .ToImmutableArray();
        }
    }
}

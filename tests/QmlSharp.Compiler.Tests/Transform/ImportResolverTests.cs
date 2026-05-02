using QmlSharp.Compiler.Tests.Fixtures;

namespace QmlSharp.Compiler.Tests.Transform
{
    public sealed class ImportResolverTests
    {
        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void IM_01_MapsQtQuickNamespaceToQtQuickImport()
        {
            ImportResolver resolver = new();

            ResolvedImport? resolved = resolver.ResolveSingle(Import("QmlSharp.QtQuick"), CompilerTestFixtures.DefaultOptions);

            Assert.NotNull(resolved);
            Assert.Equal("QtQuick", resolved.QmlModuleUri);
            Assert.Equal(new QmlVersion(1, 0), resolved.Version);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void IM_02_MapsQtQuickControlsNamespaceToControlsImport()
        {
            ImportResolver resolver = new();

            ResolvedImport? resolved = resolver.ResolveSingle(Import("QmlSharp.QtQuick.Controls"), CompilerTestFixtures.DefaultOptions);

            Assert.NotNull(resolved);
            Assert.Equal("QtQuick.Controls", resolved.QmlModuleUri);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void IM_03_MapsQtQuickLayoutsNamespaceToLayoutsImport()
        {
            ImportResolver resolver = new();

            ResolvedImport? resolved = resolver.ResolveSingle(Import("QmlSharp.QtQuick.Layouts"), CompilerTestFixtures.DefaultOptions);

            Assert.NotNull(resolved);
            Assert.Equal("QtQuick.Layouts", resolved.QmlModuleUri);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void IM_04_MapsQtQmlNamespaceToQtQmlImport()
        {
            ImportResolver resolver = new();

            ResolvedImport? resolved = resolver.ResolveSingle(Import("QmlSharp.QtQml"), CompilerTestFixtures.DefaultOptions);

            Assert.NotNull(resolved);
            Assert.Equal("QtQml", resolved.QmlModuleUri);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void IM_05_IgnoresNonDslNamespaces()
        {
            ImportResolver resolver = new();

            ResolvedImport? resolved = resolver.ResolveSingle(Import("System.Collections.Generic"), CompilerTestFixtures.DefaultOptions);

            Assert.Null(resolved);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void IM_06_SupportsCustomMappingsWithAliasAndVersion()
        {
            ImportResolver resolver = new();
            resolver.RegisterMapping("Company.Controls", "Company.Controls", new QmlVersion(2, 7), "Cc");

            ResolvedImport? resolved = resolver.ResolveSingle(Import("Company.Controls"), CompilerTestFixtures.DefaultOptions);

            Assert.NotNull(resolved);
            Assert.Equal("Company.Controls", resolved.QmlModuleUri);
            Assert.Equal(new QmlVersion(2, 7), resolved.Version);
            Assert.Equal("Cc", resolved.Alias);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void IM_07_DeduplicatesImportsAndPreservesAliases()
        {
            ImportResolver resolver = new();
            resolver.RegisterMapping("Company.Primary", "Company.Controls", alias: "Controls");
            resolver.RegisterMapping("Company.Secondary", "Company.Controls", alias: "Controls");

            ImmutableArray<ResolvedImport> resolved = resolver.Resolve(
                ImmutableArray.Create(
                    Import("QmlSharp.QtQuick"),
                    Import("QmlSharp.QtQuick"),
                    Import("Company.Primary"),
                    Import("Company.Secondary")),
                CompilerTestFixtures.DefaultOptions);

            Assert.Equal(2, resolved.Length);
            Assert.Contains(resolved, import => string.Equals(import.QmlModuleUri, "Company.Controls", StringComparison.Ordinal) && string.Equals(import.Alias, "Controls", StringComparison.Ordinal));
            Assert.Contains(resolved, import => string.Equals(import.QmlModuleUri, "QtQuick", StringComparison.Ordinal) && import.Alias is null);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void IM_08_StampsCompilerOptionVersionsAndOrdersStably()
        {
            ImportResolver resolver = new();
            CompilerOptions options = CompilerTestFixtures.DefaultOptions with { ModuleVersion = new QmlVersion(6, 11) };

            ImmutableArray<ResolvedImport> first = resolver.Resolve(
                ImmutableArray.Create(
                    Import("QmlSharp.QtQuick.Controls"),
                    Import("System"),
                    Import("QmlSharp.QtQml"),
                    Import("QmlSharp.QtQuick")),
                options);
            ImmutableArray<ResolvedImport> second = resolver.Resolve(
                ImmutableArray.Create(
                    Import("QmlSharp.QtQuick"),
                    Import("QmlSharp.QtQml"),
                    Import("QmlSharp.QtQuick.Controls")),
                options);

            Assert.All(first, import => Assert.Equal(new QmlVersion(6, 11), import.Version));
            Assert.Equal(first.Select(import => import.QmlModuleUri), second.Select(import => import.QmlModuleUri));
            Assert.Equal(new[] { "QtQml", "QtQuick", "QtQuick.Controls" }, first.Select(import => import.QmlModuleUri).ToArray());
        }

        private static DiscoveredImport Import(string csharpNamespace)
        {
            return new DiscoveredImport(csharpNamespace, "CounterView.cs", 1);
        }
    }
}

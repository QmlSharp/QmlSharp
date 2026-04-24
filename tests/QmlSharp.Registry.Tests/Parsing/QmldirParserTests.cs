using QmlSharp.Registry.Diagnostics;
using QmlSharp.Registry.Parsing;

namespace QmlSharp.Registry.Tests.Parsing
{
    public sealed class QmldirParserTests
    {
        [Fact]
        public void QDP_01_Parse_empty_qmldir_file_returns_empty_collections()
        {
            ParseResult<RawQmldirFile> result = CreateParser().ParseContent(string.Empty, @"fixtures\qmldir\empty-qmldir");

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value);
            Assert.Equal(@"fixtures\qmldir\empty-qmldir", result.Value!.SourcePath);
            Assert.Null(result.Value.Module);
            Assert.Empty(result.Value.Plugins);
            Assert.Null(result.Value.Classname);
            Assert.Empty(result.Value.Imports);
            Assert.Empty(result.Value.Depends);
            Assert.Empty(result.Value.TypeEntries);
            Assert.Empty(result.Value.Designersupported);
            Assert.Null(result.Value.Typeinfo);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public void QDP_02_Parse_module_directive()
        {
            ParseResult<RawQmldirFile> result = CreateParser().Parse(GetFixturePath("minimal-qmldir"));

            Assert.True(result.IsSuccess);
            Assert.Equal("QtQuick", result.Value!.Module);
        }

        [Fact]
        public void QDP_03_Parse_plugin_directive()
        {
            ParseResult<RawQmldirFile> result = CreateParser().Parse(GetFixturePath("minimal-qmldir"));

            RawQmldirPlugin plugin = Assert.Single(result.Value!.Plugins);
            Assert.Equal("qtquickplugin", plugin.Name);
            Assert.Null(plugin.Path);
        }

        [Fact]
        public void QDP_04_Parse_plugin_with_path()
        {
            ParseResult<RawQmldirFile> result = CreateParser().Parse(GetFixturePath("full-qmldir"));

            RawQmldirPlugin plugin = Assert.Single(result.Value!.Plugins);
            Assert.Equal("qtquickcontrols2plugin", plugin.Name);
            Assert.Equal("plugins/qtquickcontrols2plugin", plugin.Path);
        }

        [Fact]
        public void QDP_05_Parse_classname_directive()
        {
            Assert.Equal("QtQuickControls2Plugin", ParseFixture("full-qmldir").Classname);
        }

        [Fact]
        public void QDP_06_Parse_single_import_directive()
        {
            RawQmldirImport import = Assert.Single(ParseFixture("full-qmldir").Imports);
            Assert.Equal("QtQuick", import.Module);
            Assert.Equal("2.15", import.Version);
        }

        [Fact]
        public void QDP_07_Parse_multiple_import_directives()
        {
            RawQmldirFile file = ParseContent(
                """
                module QtQuick.Controls
                import QtQuick 2.15
                import QtQml 2.15
                """);

            Assert.Equal(
                [("QtQuick", "2.15"), ("QtQml", "2.15")],
                file.Imports.Select(import => (import.Module, import.Version)).ToArray());
        }

        [Fact]
        public void QDP_08_Parse_depends_directive()
        {
            RawQmldirImport depends = Assert.Single(ParseFixture("full-qmldir").Depends);
            Assert.Equal("QtQml", depends.Module);
            Assert.Equal("2.15", depends.Version);
        }

        [Fact]
        public void QDP_09_Parse_multiple_depends_directives()
        {
            RawQmldirFile file = ParseContent(
                """
                module QtQuick.Controls
                depends QtQml 2.15
                depends QtQuick 2.15
                """);

            Assert.Equal(
                [("QtQml", "2.15"), ("QtQuick", "2.15")],
                file.Depends.Select(depends => (depends.Module, depends.Version)).ToArray());
        }

        [Fact]
        public void QDP_10_Parse_type_entry_name_version_and_file_path()
        {
            RawQmldirTypeEntry entry = Assert.Single(ParseFixture("minimal-qmldir").TypeEntries);
            Assert.Equal("Item", entry.Name);
            Assert.Equal("2.15", entry.Version);
            Assert.Equal("Item.qml", entry.FilePath);
            Assert.False(entry.IsSingleton);
            Assert.False(entry.IsInternal);
            Assert.Null(entry.StyleSelector);
        }

        [Fact]
        public void QDP_11_Parse_multiple_type_entries()
        {
            Assert.Equal(
                ["Button", "Control"],
                ParseFixture("qtquick-controls-qmldir").TypeEntries.Select(entry => entry.Name).ToArray());
        }

        [Fact]
        public void QDP_12_Parse_singleton_type_entry()
        {
            RawQmldirTypeEntry entry = Assert.Single(
                ParseFixture("full-qmldir").TypeEntries.Where(entry => entry.IsSingleton));

            Assert.Equal("Palette", entry.Name);
            Assert.Equal("2.15", entry.Version);
            Assert.Equal("Palette.qml", entry.FilePath);
        }

        [Fact]
        public void QDP_13_Parse_internal_type_entry()
        {
            RawQmldirTypeEntry entry = Assert.Single(
                ParseFixture("full-qmldir").TypeEntries.Where(entry => entry.IsInternal));

            Assert.Equal("Impl", entry.Name);
            Assert.Equal("2.15", entry.Version);
            Assert.Equal("impl/Impl.qml", entry.FilePath);
            Assert.False(entry.IsSingleton);
        }

        [Fact]
        public void Parse_type_entry_with_style_selector()
        {
            RawQmldirFile file = ParseContent(
                """
                module QtQuick.Controls
                Button 2.15 +Material/Button.qml
                singleton Palette 2.15 +Universal/Palette.qml
                internal Impl 2.15 +Fusion/impl/Impl.qml
                """);

            Assert.Collection(
                file.TypeEntries,
                entry =>
                {
                    Assert.Equal("Button", entry.Name);
                    Assert.Equal("+Material/Button.qml", entry.FilePath);
                    Assert.Equal("Material", entry.StyleSelector);
                    Assert.False(entry.IsSingleton);
                    Assert.False(entry.IsInternal);
                },
                entry =>
                {
                    Assert.Equal("Palette", entry.Name);
                    Assert.Equal("+Universal/Palette.qml", entry.FilePath);
                    Assert.Equal("Universal", entry.StyleSelector);
                    Assert.True(entry.IsSingleton);
                    Assert.False(entry.IsInternal);
                },
                entry =>
                {
                    Assert.Equal("Impl", entry.Name);
                    Assert.Equal("+Fusion/impl/Impl.qml", entry.FilePath);
                    Assert.Equal("Fusion", entry.StyleSelector);
                    Assert.False(entry.IsSingleton);
                    Assert.True(entry.IsInternal);
                });
        }

        [Fact]
        public void QDP_14_Parse_designersupported_directive()
        {
            Assert.Equal(["true"], ParseFixture("full-qmldir").Designersupported.ToArray());
        }

        [Fact]
        public void QDP_15_Parse_typeinfo_directive()
        {
            Assert.Equal("plugins.qmltypes", ParseFixture("full-qmldir").Typeinfo);
        }

        [Fact]
        public void QDP_16_Parse_comment_lines()
        {
            RawQmldirFile file = ParseContent(
                """
                # leading comment
                module QtQuick
                # trailing comment
                plugin qtquickplugin
                """);

            Assert.Equal("QtQuick", file.Module);
            Assert.Equal("qtquickplugin", Assert.Single(file.Plugins).Name);
        }

        [Fact]
        public void QDP_17_Parse_empty_lines_between_directives()
        {
            RawQmldirFile file = ParseContent(
                """

                module QtQuick

                plugin qtquickplugin

                typeinfo plugins.qmltypes

                """);

            Assert.Equal("QtQuick", file.Module);
            Assert.Equal("qtquickplugin", Assert.Single(file.Plugins).Name);
            Assert.Equal("plugins.qmltypes", file.Typeinfo);
        }

        [Fact]
        public void QDP_18_Parse_qmldir_with_all_supported_directive_types()
        {
            RawQmldirFile file = ParseFixture("full-qmldir");

            Assert.Equal("QtQuick.Controls", file.Module);
            Assert.Equal(("qtquickcontrols2plugin", "plugins/qtquickcontrols2plugin"), (file.Plugins[0].Name, file.Plugins[0].Path));
            Assert.Equal("QtQuickControls2Plugin", file.Classname);
            Assert.Equal(("QtQuick", "2.15"), (file.Imports[0].Module, file.Imports[0].Version));
            Assert.Equal(("QtQml", "2.15"), (file.Depends[0].Module, file.Depends[0].Version));
            Assert.Equal(["Palette", "Impl"], file.TypeEntries.Select(entry => entry.Name).ToArray());
            Assert.Equal(["true"], file.Designersupported.ToArray());
            Assert.Equal("plugins.qmltypes", file.Typeinfo);
        }

        [Fact]
        public void QDP_19_Parse_qtquick_controls_qmldir_fixture()
        {
            ParseResult<RawQmldirFile> result = CreateParser().Parse(GetFixturePath("qtquick-controls-qmldir"));

            Assert.True(result.IsSuccess);
            Assert.Equal("QtQuick.Controls", result.Value!.Module);
            Assert.Equal(["true"], result.Value.Designersupported.ToArray());
            Assert.Equal(["Button", "Control"], result.Value.TypeEntries.Select(entry => entry.Name).ToArray());
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public void QDP_20_Unknown_directive_produces_REG021_warning_with_source_coordinates()
        {
            ParseResult<RawQmldirFile> result = CreateParser().ParseContent(
                "module QtQuick.Controls\r\n  mystery directive\r\n",
                @"fixtures\qmldir\unknown-qmldir");

            Assert.True(result.IsSuccess);

            RegistryDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Equal(DiagnosticCodes.QmldirUnknownDirective, diagnostic.Code);
            Assert.Equal(@"fixtures\qmldir\unknown-qmldir", diagnostic.FilePath);
            Assert.Equal(2, diagnostic.Line);
            Assert.Equal(3, diagnostic.Column);
        }

        [Fact]
        public void Parse_reports_REG020_for_syntax_errors_with_source_coordinates()
        {
            ParseResult<RawQmldirFile> result = CreateParser().ParseContent(
                "module QtQuick.Controls\r\n  singleton Palette 2.15\r\n",
                @"fixtures\qmldir\invalid-qmldir");

            Assert.False(result.IsSuccess);
            Assert.NotNull(result.Value);

            RegistryDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal(DiagnosticCodes.QmldirSyntaxError, diagnostic.Code);
            Assert.Equal(@"fixtures\qmldir\invalid-qmldir", diagnostic.FilePath);
            Assert.Equal(2, diagnostic.Line);
            Assert.Equal(3, diagnostic.Column);
        }

        [Fact]
        public void Parse_internal_type_entry_without_version_uses_empty_version()
        {
            RawQmldirFile file = ParseContent(
                """
                module QtQuick.Controls
                internal Impl impl/Impl.qml
                """);

            RawQmldirTypeEntry entry = Assert.Single(file.TypeEntries);
            Assert.Equal("Impl", entry.Name);
            Assert.Equal(string.Empty, entry.Version);
            Assert.Equal("impl/Impl.qml", entry.FilePath);
            Assert.True(entry.IsInternal);
        }

        [Theory]
        [InlineData("module", DiagnosticCodes.QmldirSyntaxError)]
        [InlineData("plugin", DiagnosticCodes.QmldirSyntaxError)]
        [InlineData("classname QtQuickControls2Plugin Extra", DiagnosticCodes.QmldirSyntaxError)]
        [InlineData("import QtQuick 2.15 Extra", DiagnosticCodes.QmldirSyntaxError)]
        [InlineData("depends QtQuick 2.15 Extra", DiagnosticCodes.QmldirSyntaxError)]
        [InlineData("singleton Palette 2.15", DiagnosticCodes.QmldirSyntaxError)]
        [InlineData("designersupported false", DiagnosticCodes.QmldirSyntaxError)]
        [InlineData("typeinfo plugins.qmltypes extra", DiagnosticCodes.QmldirSyntaxError)]
        [InlineData("Button 2.15", DiagnosticCodes.QmldirSyntaxError)]
        [InlineData("button 2.15 Button.qml", DiagnosticCodes.QmldirUnknownDirective)]
        public void Parse_malformed_directives_report_expected_diagnostics(string line, string expectedCode)
        {
            ParseResult<RawQmldirFile> result = CreateParser().ParseContent(
                string.Create(System.Globalization.CultureInfo.InvariantCulture, $"module QtQuick.Controls\n{line}\n"),
                @"fixtures\qmldir\malformed-qmldir");

            RegistryDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(expectedCode, diagnostic.Code);
            Assert.Equal(@"fixtures\qmldir\malformed-qmldir", diagnostic.FilePath);
            Assert.Equal(2, diagnostic.Line);
            Assert.Equal(1, diagnostic.Column);
        }

        private static IQmldirParser CreateParser()
        {
            return new QmldirParser();
        }

        private static RawQmldirFile ParseFixture(string fixtureName)
        {
            ParseResult<RawQmldirFile> result = CreateParser().Parse(GetFixturePath(fixtureName));
            Assert.True(result.IsSuccess);
            return result.Value!;
        }

        private static RawQmldirFile ParseContent(string content)
        {
            ParseResult<RawQmldirFile> result = CreateParser().ParseContent(content, @"fixtures\qmldir\inline-qmldir");
            Assert.True(result.IsSuccess);
            return result.Value!;
        }

        private static string GetFixturePath(string fixtureName)
        {
            if (Path.IsPathRooted(fixtureName))
            {
                throw new ArgumentException("Fixture name must be a relative path.", nameof(fixtureName));
            }

            return Path.Join(AppContext.BaseDirectory, "fixtures", "qmldir", fixtureName);
        }
    }
}

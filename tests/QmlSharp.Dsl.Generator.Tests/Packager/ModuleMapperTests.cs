using QmlSharp.Dsl.Generator.Tests.Fixtures;

namespace QmlSharp.Dsl.Generator.Tests.Packager
{
    public sealed class ModuleMapperTests
    {
        [Theory]
        [Trait("Category", TestCategories.Contract)]
        [InlineData("QtQml", "QmlSharp.QtQml", 0)]
        [InlineData("QtQuick", "QmlSharp.QtQuick", 0)]
        [InlineData("QtQuick.Controls", "QmlSharp.QtQuick.Controls", 0)]
        [InlineData("QtQuick.Layouts", "QmlSharp.QtQuick.Layouts", 0)]
        [InlineData("QtQuick.Window", "QmlSharp.QtQuick.Window", 1)]
        [InlineData("QtQuick.Dialogs", "QmlSharp.QtQuick.Dialogs", 1)]
        public void ToPackageName_P0AndP1Modules_ReturnsReadmeMapping(string moduleUri, string packageName, int priority)
        {
            ModuleMapper mapper = new();

            Assert.Equal(packageName, mapper.ToPackageName(moduleUri));
            Assert.Equal(priority, mapper.GetPriority(moduleUri));
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void ToPackageName_DottedModuleUri_PreservesDottedPackageNesting()
        {
            ModuleMapper mapper = new();

            string packageName = mapper.ToPackageName("QtQuick.Controls");

            Assert.Equal("QmlSharp.QtQuick.Controls", packageName);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void ToModuleUri_KnownPackageName_ReturnsQtModuleUri()
        {
            ModuleMapper mapper = new();

            string moduleUri = mapper.ToModuleUri("QmlSharp.QtQuick");

            Assert.Equal("QtQuick", moduleUri);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void ToNamespace_ModuleUriWithDashesAndSpaces_ReturnsSafeNamespace()
        {
            ModuleMapper mapper = new();

            string generatedNamespace = mapper.ToNamespace("QtQuick.Custom Controls.Experimental-API");

            Assert.Equal("QmlSharp.QtQuick.CustomControls.ExperimentalAPI", generatedNamespace);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void CustomMapping_OverridesBuiltInMappingAndSupportsReverseLookup()
        {
            ModuleMapper mapper = new(
                customMappings: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["QtQuick"] = "Company.Ui.Quick",
                },
                customPriorities: new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["QtQuick"] = 5,
                });

            Assert.Equal("Company.Ui.Quick", mapper.ToPackageName("QtQuick"));
            Assert.Equal("QtQuick", mapper.ToModuleUri("Company.Ui.Quick"));
            Assert.Equal(5, mapper.GetPriority("QtQuick"));
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void GetAllMappings_ReturnsStableKnownMappings()
        {
            ModuleMapper mapper = new();

            IReadOnlyDictionary<string, string> mappings = mapper.GetAllMappings();

            Assert.Equal(
                ["QtQml", "QtQuick", "QtQuick.Controls", "QtQuick.Dialogs", "QtQuick.Layouts", "QtQuick.Window"],
                mappings.Keys.ToArray());
        }
    }
}

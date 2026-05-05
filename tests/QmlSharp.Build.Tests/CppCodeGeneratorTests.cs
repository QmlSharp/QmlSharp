using QmlSharp.Build.Tests.Infrastructure;

namespace QmlSharp.Build.Tests
{
    public sealed class CppCodeGeneratorTests
    {
        [Fact]
        public void CG01_GenerateHeader_IntProperty_EmitsQProperty()
        {
            string header = GenerateHeader(Property("count", "int"));

            Assert.Contains(
                "Q_PROPERTY(int count READ count WRITE setCount NOTIFY countChanged)",
                header,
                StringComparison.Ordinal);
        }

        [Fact]
        public void CG02_GenerateHeader_ReadonlyStringProperty_HasNoWriteAccessor()
        {
            string header = GenerateHeader(Property("label", "string", readOnly: true, defaultValue: "Ready"));

            Assert.Contains(
                "Q_PROPERTY(QString label READ label NOTIFY labelChanged)",
                header,
                StringComparison.Ordinal);
            Assert.DoesNotContain("WRITE setLabel", header, StringComparison.Ordinal);
        }

        [Fact]
        public void CG03_GenerateHeader_BoolProperty_EmitsQProperty()
        {
            string header = GenerateHeader(Property("active", "bool"));

            Assert.Contains(
                "Q_PROPERTY(bool active READ active WRITE setActive NOTIFY activeChanged)",
                header,
                StringComparison.Ordinal);
        }

        [Fact]
        public void CG04_GenerateHeader_DoubleProperty_EmitsQProperty()
        {
            string header = GenerateHeader(Property("value", "double"));

            Assert.Contains(
                "Q_PROPERTY(double value READ value WRITE setValue NOTIFY valueChanged)",
                header,
                StringComparison.Ordinal);
        }

        [Fact]
        public void CG05_GenerateHeader_VariantProperty_EmitsQVariantProperty()
        {
            string header = GenerateHeader(Property("data", "var"));

            Assert.Contains(
                "Q_PROPERTY(QVariant data READ data WRITE setData NOTIFY dataChanged)",
                header,
                StringComparison.Ordinal);
        }

        [Fact]
        public void CG06_GenerateHeader_CommandWithoutParameters_EmitsInvokable()
        {
            ViewModelSchema schema = BuildTestFixtures.CreateCounterSchema();
            CppCodeGenerator generator = new();

            string header = generator.GenerateHeader(schema, CreateOptions());

            Assert.Contains("Q_INVOKABLE void increment();", header, StringComparison.Ordinal);
        }

        [Fact]
        public void CG07_GenerateHeader_CommandWithIntParameter_EmitsTypedInvokable()
        {
            ViewModelSchema schema = BuildTestFixtures.CreateCounterSchema() with
            {
                Commands = ImmutableArray.Create(new CommandEntry(
                    "reset",
                    ImmutableArray.Create(new ParameterEntry("value", "int")),
                    44)),
            };
            CppCodeGenerator generator = new();

            string header = generator.GenerateHeader(schema, CreateOptions());

            Assert.Contains("Q_INVOKABLE void reset(int value);", header, StringComparison.Ordinal);
        }

        [Fact]
        public void CG08_GenerateHeader_QmlElementMacro_IsPresent()
        {
            string header = GenerateHeader(Property("count", "int"));

            Assert.Contains("Q_OBJECT", header, StringComparison.Ordinal);
            Assert.Contains("QML_ELEMENT", header, StringComparison.Ordinal);
        }

        [Fact]
        public void CG09_GenerateImplementation_ConstructorNotifiesNativeHost()
        {
            string implementation = GenerateImplementation(Property("count", "int"));

            Assert.Contains("qmlsharp::notify_instance_created", implementation, StringComparison.Ordinal);
            Assert.Contains("QStringLiteral(\"CounterViewModel\")", implementation, StringComparison.Ordinal);
            Assert.Contains("m_compilerSlotKey", implementation, StringComparison.Ordinal);
        }

        [Fact]
        public void CG10_GenerateImplementation_DestructorNotifiesNativeHost()
        {
            string implementation = GenerateImplementation(Property("count", "int"));

            Assert.Contains("CounterViewModel::~CounterViewModel()", implementation, StringComparison.Ordinal);
            Assert.Contains("qmlsharp::notify_instance_destroyed(m_instanceId);", implementation, StringComparison.Ordinal);
        }

        [Fact]
        public void CG11_GenerateImplementation_SetPropertyFromManagedInt_HasTypedBranch()
        {
            string implementation = GenerateImplementation(Property("count", "int"));

            Assert.Contains("void CounterViewModel::setPropertyFromManagedInt", implementation, StringComparison.Ordinal);
            Assert.Contains("if (qstrcmp(propertyName, \"count\") == 0)", implementation, StringComparison.Ordinal);
            Assert.Contains("setCount(value);", implementation, StringComparison.Ordinal);
        }

        [Fact]
        public void CG12_GenerateImplementation_SetPropertyFromManagedString_UsesUtf8Conversion()
        {
            string implementation = GenerateImplementation(Property("label", "string"));

            Assert.Contains("void CounterViewModel::setPropertyFromManagedString", implementation, StringComparison.Ordinal);
            Assert.Contains("QString::fromUtf8(value == nullptr ? \"\" : value)", implementation, StringComparison.Ordinal);
            Assert.Contains("setLabel(qmlsharpValue);", implementation, StringComparison.Ordinal);
        }

        [Fact]
        public void CG13_GenerateCMakeLists_ListsGeneratedSourcesDeterministically()
        {
            CppCodeGenerator generator = new();
            ImmutableArray<ViewModelSchema> schemas = ImmutableArray.Create(CreateTodoSchema(), BuildTestFixtures.CreateCounterSchema());

            string cmake = generator.GenerateCMakeLists(schemas, CreateOptions());

            Assert.Contains("CounterViewModel.cpp\n", cmake, StringComparison.Ordinal);
            Assert.Contains("CounterViewModel.h\n", cmake, StringComparison.Ordinal);
            Assert.Contains("TodoViewModel.cpp\n", cmake, StringComparison.Ordinal);
            Assert.Contains("type_registration.cpp\n", cmake, StringComparison.Ordinal);
            Assert.True(
                cmake.IndexOf("CounterViewModel.cpp", StringComparison.Ordinal) <
                cmake.IndexOf("TodoViewModel.cpp", StringComparison.Ordinal));
            Assert.Contains("qmlsharp_abi.h", cmake, StringComparison.Ordinal);
        }

        [Fact]
        public void CG14_GenerateCMakeLists_FindsQt6QuickAndQml()
        {
            CppCodeGenerator generator = new();

            string cmake = generator.GenerateCMakeLists(
                ImmutableArray.Create(BuildTestFixtures.CreateCounterSchema()),
                CreateOptions());

            Assert.Contains("find_package(Qt6 REQUIRED COMPONENTS Quick Qml)", cmake, StringComparison.Ordinal);
            Assert.Contains("Qt6::Quick", cmake, StringComparison.Ordinal);
            Assert.Contains("Qt6::Qml", cmake, StringComparison.Ordinal);
        }

        [Fact]
        public void CG15_GenerateTypeRegistration_EmitsQmlRegisterTypeDispatch()
        {
            CppCodeGenerator generator = new();

            string registration = generator.GenerateTypeRegistration(
                ImmutableArray.Create(BuildTestFixtures.CreateCounterSchema()),
                CreateOptions());

            Assert.Contains("qmlsharp_register_generated_type", registration, StringComparison.Ordinal);
            Assert.Contains("qmlRegisterType<CounterViewModel>", registration, StringComparison.Ordinal);
            Assert.Contains("qstrcmp(typeName, \"CounterViewModel\")", registration, StringComparison.Ordinal);
        }

        [Fact]
        public void CG16_GenerateUnsupportedType_FallsBackToQVariantAndReportsB021()
        {
            CppCodeGenerator generator = new();
            ViewModelSchema schema = SchemaWithProperties(Property("data", "UnsupportedThing"));

            CppGenerationResult result = generator.Generate(ImmutableArray.Create(schema), CreateOptions());

            BuildDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(BuildDiagnosticCode.UnsupportedCppType, diagnostic.Code);
            Assert.Equal(BuildDiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("UnsupportedThing", diagnostic.Message, StringComparison.Ordinal);
            Assert.Contains("CounterViewModel", diagnostic.Message, StringComparison.Ordinal);
            Assert.Contains("Q_PROPERTY(QVariant data READ data WRITE setData NOTIFY dataChanged)", result.Files.Values.Single(static content => content.Contains("class CounterViewModel", StringComparison.Ordinal)), StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("int", "int", true)]
        [InlineData("double", "double", true)]
        [InlineData("real", "double", true)]
        [InlineData("number", "double", true)]
        [InlineData("bool", "bool", true)]
        [InlineData("string", "QString", true)]
        [InlineData("url", "QUrl", false)]
        [InlineData("color", "QColor", false)]
        [InlineData("date", "QDate", false)]
        [InlineData("var", "QVariant", false)]
        [InlineData("variant", "QVariant", false)]
        [InlineData("list", "QVariantList", false)]
        [InlineData("list<string>", "QVariantList", false)]
        [InlineData("point", "QPointF", false)]
        [InlineData("rect", "QRectF", false)]
        [InlineData("size", "QSizeF", false)]
        public void TypeMap_DesignCppTypes_AreCovered(string qmlType, string cppType, bool fastPath)
        {
            Assert.Equal(cppType, CppTypeMap.ToCppType(qmlType));
            Assert.Equal(fastPath, CppTypeMap.HasFastPath(qmlType));
        }

        [Fact]
        public void Generate_RepeatedRuns_AreDeterministic()
        {
            CppCodeGenerator generator = new();
            ImmutableArray<ViewModelSchema> firstSchemas =
                ImmutableArray.Create(CreateTodoSchema(), BuildTestFixtures.CreateCounterSchema());
            ImmutableArray<ViewModelSchema> secondSchemas =
                ImmutableArray.Create(BuildTestFixtures.CreateCounterSchema(), CreateTodoSchema());

            CppGenerationResult first = generator.Generate(firstSchemas, CreateOptions());
            CppGenerationResult second = generator.Generate(secondSchemas, CreateOptions());

            Assert.Equal(first.Files.OrderBy(static pair => pair.Key, StringComparer.Ordinal), second.Files.OrderBy(static pair => pair.Key, StringComparer.Ordinal));
            Assert.Equal(first.HeaderFiles.AsEnumerable(), second.HeaderFiles.AsEnumerable());
            Assert.Equal(first.ImplementationFiles.AsEnumerable(), second.ImplementationFiles.AsEnumerable());
            Assert.DoesNotContain(first.Files.Values, static content => content.Contains('\r'));
        }

        [Fact]
        public void Generate_PatternMatchesMergedNativeHostContract()
        {
            CppCodeGenerator generator = new();
            ViewModelSchema schema = BuildTestFixtures.CreateCounterSchema();

            string header = generator.GenerateHeader(schema, CreateOptions());
            string implementation = generator.GenerateImplementation(schema, CreateOptions());
            string registration = generator.GenerateTypeRegistration(ImmutableArray.Create(schema), CreateOptions());

            Assert.Contains("#include \"qmlsharp/qmlsharp_abi.h\"", header, StringComparison.Ordinal);
            Assert.Contains("#include \"qmlsharp_instances.h\"", implementation, StringComparison.Ordinal);
            Assert.Contains("qmlsharp::notify_instance_created", implementation, StringComparison.Ordinal);
            Assert.Contains("qmlsharp::dispatch_command", implementation, StringComparison.Ordinal);
            Assert.Contains("emitEffectDispatched", header, StringComparison.Ordinal);
            Assert.Contains("qmlsharp_register_generated_type", registration, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", "Golden")]
        public void CGG1_GenerateCounterViewModelHeader_MatchesGolden()
        {
            CppCodeGenerator generator = new();

            string header = generator.GenerateHeader(BuildTestFixtures.CreateCounterSchema(), CreateOptions());

            GoldenFileHelper.AssertMatchesOrUpdate("CounterViewModel.h", header);
        }

        [Fact]
        [Trait("Category", "Golden")]
        public void CGG2_GenerateCounterViewModelImplementation_MatchesGolden()
        {
            CppCodeGenerator generator = new();

            string implementation = generator.GenerateImplementation(BuildTestFixtures.CreateCounterSchema(), CreateOptions());

            GoldenFileHelper.AssertMatchesOrUpdate("CounterViewModel.cpp", implementation);
        }

        [Fact]
        [Trait("Category", "Golden")]
        public void CGG3_GenerateCMakeLists_MatchesGolden()
        {
            CppCodeGenerator generator = new();

            string cmake = generator.GenerateCMakeLists(
                ImmutableArray.Create(BuildTestFixtures.CreateCounterSchema()),
                CreateOptions());

            GoldenFileHelper.AssertMatchesOrUpdate("CMakeLists.txt", cmake);
        }

        [Fact]
        [Trait("Category", "Golden")]
        public void CGG4_GenerateTypeRegistration_MatchesGolden()
        {
            CppCodeGenerator generator = new();

            string registration = generator.GenerateTypeRegistration(
                ImmutableArray.Create(BuildTestFixtures.CreateCounterSchema()),
                CreateOptions());

            GoldenFileHelper.AssertMatchesOrUpdate("type_registration.cpp", registration);
        }

        [Fact]
        public void GoldenUpdateWorkflow_IsReviewerVisible()
        {
            Assert.Contains("MSBUILDDISABLENODEREUSE", GoldenFileHelper.UpdateWorkflow, StringComparison.Ordinal);
            Assert.Contains("QMLSHARP_UPDATE_GOLDENS", GoldenFileHelper.UpdateWorkflow, StringComparison.Ordinal);
            Assert.Contains("UpdateGoldens=true", GoldenFileHelper.UpdateWorkflow, StringComparison.Ordinal);
            Assert.Contains("--filter \"Golden\"", GoldenFileHelper.UpdateWorkflow, StringComparison.Ordinal);
            Assert.Contains("-m:1", GoldenFileHelper.UpdateWorkflow, StringComparison.Ordinal);
        }

        private static string GenerateHeader(StateEntry property)
        {
            CppCodeGenerator generator = new();
            return generator.GenerateHeader(SchemaWithProperties(property), CreateOptions());
        }

        private static string GenerateImplementation(StateEntry property)
        {
            CppCodeGenerator generator = new();
            return generator.GenerateImplementation(SchemaWithProperties(property), CreateOptions());
        }

        private static ViewModelSchema SchemaWithProperties(params StateEntry[] properties)
        {
            return BuildTestFixtures.CreateCounterSchema() with
            {
                Properties = ImmutableArray.Create(properties),
                Commands = ImmutableArray<CommandEntry>.Empty,
                Effects = ImmutableArray<EffectEntry>.Empty,
            };
        }

        private static StateEntry Property(
            string name,
            string type,
            bool readOnly = false,
            string? defaultValue = null)
        {
            return new StateEntry(name, type, defaultValue, readOnly, 1);
        }

        private static ViewModelSchema CreateTodoSchema()
        {
            return BuildTestFixtures.CreateCounterSchema() with
            {
                ClassName = "TodoViewModel",
                CompilerSlotKey = "TodoView::__qmlsharp_vm0",
                Properties = ImmutableArray.Create(Property("title", "string")),
            };
        }

        private static CppGenerationOptions CreateOptions()
        {
            return BuildTestFixtures.CreateDefaultCppOptions("dist") with
            {
                AbiSourceDir = "${CMAKE_CURRENT_LIST_DIR}/../../../native",
            };
        }
    }
}

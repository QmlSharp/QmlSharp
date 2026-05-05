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
        public void CG02_GenerateHeader_ReadonlyStringProperty_HasWritableMetaPropertyForManagedSync()
        {
            string header = GenerateHeader(Property("label", "string", readOnly: true, defaultValue: "Ready"));

            Assert.Contains(
                "Q_PROPERTY(QString label READ label WRITE setLabel NOTIFY labelChanged)",
                header,
                StringComparison.Ordinal);
            Assert.Contains("void setLabel(const QString& value);", header, StringComparison.Ordinal);
        }

        [Fact]
        public void CG02B_GenerateImplementation_ReadonlyStringProperty_UsesSetterForManagedSync()
        {
            string implementation = GenerateImplementation(Property("label", "string", readOnly: true, defaultValue: "Ready"));

            Assert.Contains("void CounterViewModel::setLabel(const QString& value)", implementation, StringComparison.Ordinal);
            Assert.Contains("setLabel(qmlsharpValue);", implementation, StringComparison.Ordinal);
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
        public void GenerateImplementation_AllMappedStateTypes_EmitsDefaultsAndManagedConversions()
        {
            CppCodeGenerator generator = new();
            ViewModelSchema schema = SchemaWithProperties(
                Property("count", "int", defaultValue: "7"),
                Property("ratio", "double", defaultValue: "1.5"),
                Property("realValue", "real", defaultValue: "2.5"),
                Property("numberValue", "number", defaultValue: "3.5"),
                Property("enabled", "bool", defaultValue: "true"),
                Property("disabled", "bool", defaultValue: "not-bool"),
                Property("title", "string", defaultValue: "\"Hello \\\"Qt\\\"\""),
                Property("invalidJson", "string", defaultValue: "\"bad\\x\""),
                Property("nothing", "string", defaultValue: "null"),
                Property("location", "url"),
                Property("tint", "color"),
                Property("createdOn", "date"),
                Property("data", "var"),
                Property("payload", "variant"),
                Property("items", "list"),
                Property("names", "list<string>"),
                Property("origin", "point"),
                Property("bounds", "rect"),
                Property("extent", "size"),
                Property("opaque", "UnsupportedThing", defaultValue: "ignored"));

            string implementation = generator.GenerateImplementation(schema, CreateOptions());

            Assert.Contains("m_count(7)", implementation, StringComparison.Ordinal);
            Assert.Contains("m_ratio(1.5)", implementation, StringComparison.Ordinal);
            Assert.Contains("m_realValue(2.5)", implementation, StringComparison.Ordinal);
            Assert.Contains("m_numberValue(3.5)", implementation, StringComparison.Ordinal);
            Assert.Contains("m_enabled(true)", implementation, StringComparison.Ordinal);
            Assert.Contains("m_disabled(false)", implementation, StringComparison.Ordinal);
            Assert.Contains("m_title(QStringLiteral(\"Hello \\\"Qt\\\"\"))", implementation, StringComparison.Ordinal);
            Assert.Contains("m_invalidJson(QStringLiteral(\"bad\\\\x\"))", implementation, StringComparison.Ordinal);
            Assert.Contains("m_nothing(QStringLiteral(\"\"))", implementation, StringComparison.Ordinal);
            Assert.Contains("m_opaque(QVariant())", implementation, StringComparison.Ordinal);
            Assert.Contains("setCount(value.toInt());", implementation, StringComparison.Ordinal);
            Assert.Contains("setRatio(value.toDouble());", implementation, StringComparison.Ordinal);
            Assert.Contains("setEnabled(value.toBool());", implementation, StringComparison.Ordinal);
            Assert.Contains("setTitle(value.toString());", implementation, StringComparison.Ordinal);
            Assert.Contains("setLocation(value.toUrl());", implementation, StringComparison.Ordinal);
            Assert.Contains("setTint(value.value<QColor>());", implementation, StringComparison.Ordinal);
            Assert.Contains("setCreatedOn(value.toDate());", implementation, StringComparison.Ordinal);
            Assert.Contains("setItems(value.toList());", implementation, StringComparison.Ordinal);
            Assert.Contains("setOrigin(value.toPointF());", implementation, StringComparison.Ordinal);
            Assert.Contains("setBounds(value.toRectF());", implementation, StringComparison.Ordinal);
            Assert.Contains("setExtent(value.toSizeF());", implementation, StringComparison.Ordinal);
            Assert.Contains("setCount(value);", implementation, StringComparison.Ordinal);
            Assert.Contains("setRatio(value);", implementation, StringComparison.Ordinal);
            Assert.Contains("setEnabled(value);", implementation, StringComparison.Ordinal);
            Assert.Contains("setTitle(qmlsharpValue);", implementation, StringComparison.Ordinal);
        }

        [Fact]
        public void GenerateHeaderAndImplementation_UnsafeIdentifiers_AreSanitizedDeterministically()
        {
            CppCodeGenerator generator = new();
            ViewModelSchema schema = BuildTestFixtures.CreateCounterSchema() with
            {
                CompilerSlotKey = "Line\nTab\tReturn\rQuote\"Slash\\",
                Properties = ImmutableArray.Create(
                    Property("class", "int"),
                    Property("1st-value", "string"),
                    Property(string.Empty, "bool")),
                Commands = ImmutableArray.Create(new CommandEntry(
                    "operator",
                    ImmutableArray.Create(
                        new ParameterEntry("1arg", "int"),
                        new ParameterEntry("with space", "url"),
                        new ParameterEntry("class", "bool")),
                    7)),
                Effects = ImmutableArray<EffectEntry>.Empty,
            };

            string header = generator.GenerateHeader(schema, CreateOptions());
            string implementation = generator.GenerateImplementation(schema, CreateOptions());

            Assert.Contains("Q_PROPERTY(int class READ class_ WRITE setClass_ NOTIFY class_Changed)", header, StringComparison.Ordinal);
            Assert.Contains("Q_PROPERTY(QString 1st-value READ _st_value WRITE set_st_value NOTIFY _st_valueChanged)", header, StringComparison.Ordinal);
            Assert.Contains("Q_PROPERTY(bool  READ qmlsharpValue WRITE setQmlsharpValue NOTIFY qmlsharpValueChanged)", header, StringComparison.Ordinal);
            Assert.Contains("Q_INVOKABLE void operator_(int _arg, const QUrl& with_space, bool class_);", header, StringComparison.Ordinal);
            Assert.Contains("void CounterViewModel::operator_(int _arg, const QUrl& with_space, bool class_)", implementation, StringComparison.Ordinal);
            Assert.Contains("QVariant::fromValue(_arg)", implementation, StringComparison.Ordinal);
            Assert.Contains("QVariant::fromValue(with_space)", implementation, StringComparison.Ordinal);
            Assert.Contains("QVariant::fromValue(class_)", implementation, StringComparison.Ordinal);
            Assert.Contains("QStringLiteral(\"Line\\nTab\\tReturn\\rQuote\\\"Slash\\\\\")", implementation, StringComparison.Ordinal);
        }

        [Fact]
        public void GenerateHeader_CppKeywords_AreEscapedForAccessors()
        {
            foreach (string keyword in CppKeywords)
            {
                string header = GenerateHeader(Property(keyword, "int"));

                Assert.Contains($"READ {keyword}_", header, StringComparison.Ordinal);
                Assert.Contains($"NOTIFY {keyword}_Changed", header, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void GenerateCMakeLists_CustomOptions_AreEscapedAndDeduplicated()
        {
            CppCodeGenerator generator = new();
            CppGenerationOptions options = CreateOptions() with
            {
                ProjectName = "1 bad.name",
                AbiSourceDir = "C:\\native \"root\"",
                QtModules = ImmutableArray.Create(" Qt6::Quick ", "Qml", string.Empty, "Qt6::Quick"),
            };

            string cmake = generator.GenerateCMakeLists(
                ImmutableArray.Create(BuildTestFixtures.CreateCounterSchema()),
                options);

            Assert.Contains("project(_1_bad_name LANGUAGES CXX)", cmake, StringComparison.Ordinal);
            Assert.Contains("find_package(Qt6 REQUIRED COMPONENTS Quick Qml)", cmake, StringComparison.Ordinal);
            Assert.Contains("Qt6::Quick", cmake, StringComparison.Ordinal);
            Assert.Contains("Qml", cmake, StringComparison.Ordinal);
            Assert.Contains("C:/native \\\"root\\\"", cmake, StringComparison.Ordinal);
        }

        [Fact]
        public void Generate_EmptySchemaCollection_StillEmitsBuildAndRegistrationFiles()
        {
            CppCodeGenerator generator = new();
            CppGenerationOptions options = CreateOptions() with
            {
                ProjectName = string.Empty,
                QtModules = default,
            };

            CppGenerationResult result = generator.Generate(default, options);

            Assert.Empty(result.HeaderFiles);
            Assert.Empty(result.ImplementationFiles);
            Assert.Contains(Path.Join("dist", "native", "generated", "CMakeLists.txt"), result.Files.Keys);
            Assert.Contains(Path.Join("dist", "native", "generated", "type_registration.cpp"), result.Files.Keys);
            Assert.Contains("project(qmlsharp_native LANGUAGES CXX)", result.Files[result.CMakeListsPath], StringComparison.Ordinal);
            Assert.Contains("find_package(Qt6 REQUIRED COMPONENTS Quick Qml)", result.Files[result.CMakeListsPath], StringComparison.Ordinal);
            Assert.Contains("return -6;", result.Files[result.TypeRegistrationPath], StringComparison.Ordinal);
        }

        [Fact]
        public void Generate_DefaultSchemaMemberArrays_AreTreatedAsEmptyCollections()
        {
            CppCodeGenerator generator = new();
            ViewModelSchema emptyMembers = BuildTestFixtures.CreateCounterSchema() with
            {
                Properties = default,
                Commands = default,
                Effects = default,
            };
            ViewModelSchema defaultParameterMembers = BuildTestFixtures.CreateCounterSchema() with
            {
                Properties = ImmutableArray<StateEntry>.Empty,
                Commands = ImmutableArray.Create(new CommandEntry("ping", default, 11)),
                Effects = ImmutableArray.Create(new EffectEntry("saved", "string", 12, default)),
            };

            string emptyHeader = generator.GenerateHeader(emptyMembers, CreateOptions());
            string commandHeader = generator.GenerateHeader(defaultParameterMembers, CreateOptions());
            string commandImplementation = generator.GenerateImplementation(defaultParameterMembers, CreateOptions());
            CppGenerationResult emptyResult = generator.Generate(ImmutableArray.Create(emptyMembers), CreateOptions());
            CppGenerationResult parameterResult = generator.Generate(
                ImmutableArray.Create(defaultParameterMembers),
                CreateOptions());

            Assert.DoesNotContain("countChanged", emptyHeader, StringComparison.Ordinal);
            Assert.Contains("Q_INVOKABLE void ping();", commandHeader, StringComparison.Ordinal);
            Assert.Contains("qmlsharp::dispatch_command(m_instanceId, QStringLiteral(\"ping\"), QStringLiteral(\"[]\"));", commandImplementation, StringComparison.Ordinal);
            Assert.Empty(emptyResult.Diagnostics);
            Assert.Empty(parameterResult.Diagnostics);
        }

        [Fact]
        public void Generate_UnsupportedCommandAndEffectTypes_ReportB021Locations()
        {
            CppCodeGenerator generator = new();
            ViewModelSchema schema = BuildTestFixtures.CreateCounterSchema() with
            {
                Properties = ImmutableArray<StateEntry>.Empty,
                Commands = ImmutableArray.Create(new CommandEntry(
                    "send",
                    ImmutableArray.Create(new ParameterEntry("payload", "opaque-command")),
                    9)),
                Effects = ImmutableArray.Create(new EffectEntry(
                    "saved",
                    "opaque-effect",
                    10,
                    ImmutableArray.Create(new ParameterEntry("reason", "opaque-effect-parameter")))),
            };

            CppGenerationResult result = generator.Generate(ImmutableArray.Create(schema), CreateOptions());

            Assert.Collection(
                result.Diagnostics.OrderBy(static diagnostic => diagnostic.Message, StringComparer.Ordinal),
                command =>
                {
                    Assert.Equal(BuildDiagnosticCode.UnsupportedCppType, command.Code);
                    Assert.Contains("Command 'send' parameter 'payload'", command.Message, StringComparison.Ordinal);
                },
                parameter =>
                {
                    Assert.Equal(BuildDiagnosticCode.UnsupportedCppType, parameter.Code);
                    Assert.Contains("Effect 'saved' parameter 'reason'", parameter.Message, StringComparison.Ordinal);
                },
                payload =>
                {
                    Assert.Equal(BuildDiagnosticCode.UnsupportedCppType, payload.Code);
                    Assert.Contains("Effect 'saved' payload", payload.Message, StringComparison.Ordinal);
                });
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

        private static readonly ImmutableArray<string> CppKeywords = ImmutableArray.Create(
            "alignas",
            "alignof",
            "and",
            "and_eq",
            "asm",
            "auto",
            "bitand",
            "bitor",
            "bool",
            "break",
            "case",
            "catch",
            "char",
            "class",
            "compl",
            "const",
            "consteval",
            "constexpr",
            "constinit",
            "const_cast",
            "continue",
            "co_await",
            "co_return",
            "co_yield",
            "decltype",
            "default",
            "delete",
            "do",
            "double",
            "dynamic_cast",
            "else",
            "enum",
            "explicit",
            "export",
            "extern",
            "false",
            "float",
            "for",
            "friend",
            "goto",
            "if",
            "inline",
            "int",
            "long",
            "mutable",
            "namespace",
            "new",
            "noexcept",
            "not",
            "not_eq",
            "nullptr",
            "operator",
            "or",
            "or_eq",
            "private",
            "protected",
            "public",
            "register",
            "reinterpret_cast",
            "requires",
            "return",
            "short",
            "signed",
            "sizeof",
            "static",
            "static_assert",
            "static_cast",
            "struct",
            "switch",
            "template",
            "this",
            "thread_local",
            "throw",
            "true",
            "try",
            "typedef",
            "typeid",
            "typename",
            "union",
            "unsigned",
            "using",
            "virtual",
            "void",
            "volatile",
            "wchar_t",
            "while",
            "xor",
            "xor_eq");
    }
}

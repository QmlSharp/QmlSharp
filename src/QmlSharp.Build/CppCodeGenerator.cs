using System.Globalization;
using System.Text;
using System.Text.Json;
using QmlSharp.Compiler;

namespace QmlSharp.Build
{
    /// <summary>Generates deterministic C++ QObject shell code from compiler schemas.</summary>
    public sealed class CppCodeGenerator : ICppCodeGenerator
    {
        private static readonly ImmutableArray<string> DefaultQtModules =
            ImmutableArray.Create("Qt6::Quick", "Qt6::Qml");

        private static readonly ImmutableArray<string> NativeCoreSources = ImmutableArray.Create(
            "src/qmlsharp_abi.cpp",
            "src/qmlsharp_diagnostics.cpp",
            "src/qmlsharp_diagnostics.h",
            "src/qmlsharp_effects.cpp",
            "src/qmlsharp_effects.h",
            "src/qmlsharp_engine.cpp",
            "src/qmlsharp_engine.h",
            "src/qmlsharp_error_overlay.cpp",
            "src/qmlsharp_error_overlay.h",
            "src/qmlsharp_errors.cpp",
            "src/qmlsharp_errors.h",
            "src/qmlsharp_hot_reload.cpp",
            "src/qmlsharp_hot_reload.h",
            "src/qmlsharp_instances.cpp",
            "src/qmlsharp_instances.h",
            "src/qmlsharp_metrics.cpp",
            "src/qmlsharp_metrics.h",
            "src/qmlsharp_state.cpp",
            "src/qmlsharp_state.h",
            "src/qmlsharp_type_registry.cpp",
            "src/qmlsharp_type_registry.h",
            "include/qmlsharp/qmlsharp_abi.h",
            "include/qmlsharp/qmlsharp_export.h");

        /// <inheritdoc />
        public CppGenerationResult Generate(
            ImmutableArray<ViewModelSchema> schemas,
            CppGenerationOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            ImmutableArray<ViewModelSchema> orderedSchemas = SortSchemas(schemas).ToImmutableArray();
            string generatedRoot = GetGeneratedRoot(options);
            ImmutableDictionary<string, string>.Builder files =
                ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
            ImmutableArray<string>.Builder headerFiles = ImmutableArray.CreateBuilder<string>(orderedSchemas.Length);
            ImmutableArray<string>.Builder implementationFiles =
                ImmutableArray.CreateBuilder<string>(orderedSchemas.Length);

            foreach (ViewModelSchema schema in orderedSchemas)
            {
                string headerPath = Path.Join(generatedRoot, $"{schema.ClassName}.h");
                string implementationPath = Path.Join(generatedRoot, $"{schema.ClassName}.cpp");

                files.Add(headerPath, GenerateHeader(schema, options));
                files.Add(implementationPath, GenerateImplementation(schema, options));
                headerFiles.Add(headerPath);
                implementationFiles.Add(implementationPath);
            }

            string cmakeListsPath = Path.Join(generatedRoot, "CMakeLists.txt");
            string typeRegistrationPath = Path.Join(generatedRoot, "type_registration.cpp");
            files.Add(cmakeListsPath, GenerateCMakeLists(orderedSchemas, options));
            files.Add(typeRegistrationPath, GenerateTypeRegistration(orderedSchemas, options));

            return new CppGenerationResult
            {
                Files = files.ToImmutable(),
                HeaderFiles = headerFiles.ToImmutable(),
                ImplementationFiles = implementationFiles.ToImmutable(),
                CMakeListsPath = cmakeListsPath,
                TypeRegistrationPath = typeRegistrationPath,
                Diagnostics = CreateUnsupportedTypeDiagnostics(orderedSchemas),
            };
        }

        /// <inheritdoc />
        public string GenerateHeader(ViewModelSchema schema, CppGenerationOptions options)
        {
            ArgumentNullException.ThrowIfNull(schema);
            ArgumentNullException.ThrowIfNull(options);

            StringBuilder builder = new();
            AppendGeneratedHeader(builder);
            builder.Append("#pragma once\n\n");
            AppendHeaderIncludes(builder);
            AppendHeaderClassOpening(builder, schema);
            AppendHeaderProperties(builder, schema);
            AppendHeaderPublicMembers(builder, schema);
            AppendHeaderSignals(builder, schema);
            AppendHeaderPrivateMembers(builder, schema);
            builder.Append("};\n");
            return builder.ToString();
        }

        private static void AppendHeaderIncludes(StringBuilder builder)
        {
            builder.Append("#include \"qmlsharp/qmlsharp_abi.h\"\n\n");
            builder.Append("#include <qqml.h>\n\n");
            builder.Append("#include <QColor>\n");
            builder.Append("#include <QDate>\n");
            builder.Append("#include <QObject>\n");
            builder.Append("#include <QPointF>\n");
            builder.Append("#include <QRectF>\n");
            builder.Append("#include <QSizeF>\n");
            builder.Append("#include <QString>\n");
            builder.Append("#include <QUrl>\n");
            builder.Append("#include <QVariant>\n");
            builder.Append("#include <QVariantList>\n");
            builder.Append('\n');
        }

        private static void AppendHeaderClassOpening(StringBuilder builder, ViewModelSchema schema)
        {
            builder.Append("class ");
            builder.Append(schema.ClassName);
            builder.Append(" : public QObject {\n");
            builder.Append("    Q_OBJECT\n");
            builder.Append("    QML_ELEMENT\n\n");
        }

        private static void AppendHeaderProperties(StringBuilder builder, ViewModelSchema schema)
        {
            builder.Append("    Q_PROPERTY(QString instanceId READ instanceId CONSTANT)\n");
            builder.Append("    Q_PROPERTY(QString compilerSlotKey READ compilerSlotKey CONSTANT)\n");
            foreach (StateEntry property in SortProperties(schema.Properties))
            {
                AppendQProperty(builder, property);
            }
        }

        private static void AppendQProperty(StringBuilder builder, StateEntry property)
        {
            CppTypeMapping mapping = CppTypeMap.Map(property.Type);
            builder.Append("    Q_PROPERTY(");
            builder.Append(mapping.CppType);
            builder.Append(' ');
            builder.Append(property.Name);
            builder.Append(" READ ");
            builder.Append(GetterName(property));
            if (!property.ReadOnly)
            {
                builder.Append(" WRITE ");
                builder.Append(SetterName(property));
            }

            builder.Append(" NOTIFY ");
            builder.Append(ChangedSignalName(property));
            builder.Append(")\n");
        }

        private static void AppendHeaderPublicMembers(StringBuilder builder, ViewModelSchema schema)
        {
            builder.Append("\npublic:\n");
            builder.Append("    explicit ");
            builder.Append(schema.ClassName);
            builder.Append("(QObject* parent = nullptr);\n");
            builder.Append("    ~");
            builder.Append(schema.ClassName);
            builder.Append("() override;\n\n");
            builder.Append("    QString instanceId() const;\n");
            builder.Append("    QString compilerSlotKey() const;\n");
            AppendHeaderAccessors(builder, schema);
            AppendHeaderManagedSetters(builder);
            AppendHeaderCommands(builder, schema);
            builder.Append("    Q_INVOKABLE void emitEffectDispatched(");
            builder.Append("const QString& effectName, const QString& payloadJson);\n\n");
        }

        private static void AppendHeaderAccessors(StringBuilder builder, ViewModelSchema schema)
        {
            foreach (StateEntry property in SortProperties(schema.Properties))
            {
                CppTypeMapping mapping = CppTypeMap.Map(property.Type);
                builder.Append("    ");
                builder.Append(mapping.CppType);
                builder.Append(' ');
                builder.Append(GetterName(property));
                builder.Append("() const;\n");
                if (!property.ReadOnly)
                {
                    builder.Append("    void ");
                    builder.Append(SetterName(property));
                    builder.Append('(');
                    builder.Append(SetterParameterType(mapping));
                    builder.Append(" value);\n");
                }
            }

            if (!schema.Properties.IsDefaultOrEmpty)
            {
                builder.Append('\n');
            }
        }

        private static void AppendHeaderManagedSetters(StringBuilder builder)
        {
            builder.Append("    void setPropertyFromManaged(const char* propertyName, const char* jsonValue);\n");
            builder.Append("    void setPropertyFromManagedInt(const char* propertyName, int value);\n");
            builder.Append("    void setPropertyFromManagedDouble(const char* propertyName, double value);\n");
            builder.Append("    void setPropertyFromManagedBool(const char* propertyName, bool value);\n");
            builder.Append("    void setPropertyFromManagedString(const char* propertyName, const char* value);\n\n");
        }

        private static void AppendHeaderCommands(StringBuilder builder, ViewModelSchema schema)
        {
            foreach (CommandEntry command in SortCommands(schema.Commands))
            {
                builder.Append("    Q_INVOKABLE void ");
                builder.Append(SafeIdentifier(command.Name));
                builder.Append('(');
                builder.Append(CommandParameterList(command));
                builder.Append(");\n");
            }

            if (!schema.Commands.IsDefaultOrEmpty)
            {
                builder.Append('\n');
            }
        }

        private static void AppendHeaderSignals(StringBuilder builder, ViewModelSchema schema)
        {
            builder.Append("signals:\n");
            foreach (StateEntry property in SortProperties(schema.Properties))
            {
                builder.Append("    void ");
                builder.Append(ChangedSignalName(property));
                builder.Append("();\n");
            }

            builder.Append("    void effectDispatched(const QString& effectName, const QString& payloadJson);\n\n");
        }

        private static void AppendHeaderPrivateMembers(StringBuilder builder, ViewModelSchema schema)
        {
            builder.Append("private:\n");
            builder.Append("    QString m_instanceId;\n");
            builder.Append("    QString m_compilerSlotKey;\n");
            foreach (StateEntry property in SortProperties(schema.Properties))
            {
                CppTypeMapping mapping = CppTypeMap.Map(property.Type);
                builder.Append("    ");
                builder.Append(mapping.CppType);
                builder.Append(' ');
                builder.Append(MemberName(property));
                builder.Append(" = ");
                builder.Append(DefaultValueLiteral(property, mapping));
                builder.Append(";\n");
            }
        }

        /// <inheritdoc />
        public string GenerateImplementation(ViewModelSchema schema, CppGenerationOptions options)
        {
            ArgumentNullException.ThrowIfNull(schema);
            ArgumentNullException.ThrowIfNull(options);

            StringBuilder builder = new();
            AppendGeneratedHeader(builder);
            builder.Append("#include \"");
            builder.Append(schema.ClassName);
            builder.Append(".h\"\n\n");
            builder.Append("#include <QByteArray>\n");
            builder.Append("#include <QJsonArray>\n");
            builder.Append("#include <QJsonDocument>\n");
            builder.Append("#include <QJsonParseError>\n");
            builder.Append("#include <QJsonValue>\n");
            builder.Append("#include <QUuid>\n");
            builder.Append("#include <QtGlobal>\n\n");
            builder.Append("#include \"qmlsharp_instances.h\"\n\n");
            AppendConstructor(builder, schema);
            AppendDestructor(builder, schema);
            AppendAccessors(builder, schema);
            AppendCommands(builder, schema);
            AppendManagedStateSetters(builder, schema);
            AppendEffectHook(builder, schema);
            return builder.ToString();
        }

        /// <inheritdoc />
        public string GenerateCMakeLists(
            ImmutableArray<ViewModelSchema> schemas,
            CppGenerationOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            ImmutableArray<ViewModelSchema> orderedSchemas = SortSchemas(schemas).ToImmutableArray();
            ImmutableArray<string> qtModules = GetQtModules(options);
            string projectName = SafeCMakeTargetName(options.ProjectName);
            string nativeSourceDir = ToCMakePath(options.AbiSourceDir);

            StringBuilder builder = new();
            AppendCMakePreamble(builder, projectName, qtModules);
            AppendCMakeNativeSources(builder, nativeSourceDir);
            AppendCMakeTargetSources(builder, projectName, orderedSchemas);
            AppendCMakeTargetConfiguration(builder, projectName, qtModules);
            return builder.ToString();
        }

        private static void AppendCMakePreamble(
            StringBuilder builder,
            string projectName,
            ImmutableArray<string> qtModules)
        {
            builder.Append("cmake_minimum_required(VERSION 3.26 FATAL_ERROR)\n\n");
            builder.Append("project(");
            builder.Append(projectName);
            builder.Append(" LANGUAGES CXX)\n\n");
            builder.Append("set(CMAKE_AUTOMOC ON)\n");
            builder.Append("set(CMAKE_CXX_STANDARD 20)\n");
            builder.Append("set(CMAKE_CXX_STANDARD_REQUIRED ON)\n");
            builder.Append("set(CMAKE_CXX_EXTENSIONS OFF)\n\n");
            builder.Append("find_package(Qt6 REQUIRED COMPONENTS ");
            builder.Append(string.Join(' ', qtModules.Select(StripQtTargetPrefix)));
            builder.Append(")\n\n");
        }

        private static void AppendCMakeNativeSources(StringBuilder builder, string nativeSourceDir)
        {
            builder.Append("set(QMLSHARP_NATIVE_SOURCE_DIR \"");
            builder.Append(EscapeCMakeString(nativeSourceDir));
            builder.Append("\")\n\n");
            builder.Append("set(QMLSHARP_NATIVE_CORE_SOURCES\n");
            foreach (string source in NativeCoreSources)
            {
                builder.Append("  \"${QMLSHARP_NATIVE_SOURCE_DIR}/");
                builder.Append(source.Replace('\\', '/'));
                builder.Append("\"\n");
            }

            builder.Append(")\n\n");
        }

        private static void AppendCMakeTargetSources(
            StringBuilder builder,
            string projectName,
            ImmutableArray<ViewModelSchema> schemas)
        {
            builder.Append("add_library(");
            builder.Append(projectName);
            builder.Append(" SHARED\n");
            builder.Append("  ${QMLSHARP_NATIVE_CORE_SOURCES}\n");
            foreach (ViewModelSchema schema in schemas)
            {
                builder.Append("  ");
                builder.Append(schema.ClassName);
                builder.Append(".cpp\n");
                builder.Append("  ");
                builder.Append(schema.ClassName);
                builder.Append(".h\n");
            }

            builder.Append("  type_registration.cpp\n");
            builder.Append(")\n\n");
        }

        private static void AppendCMakeTargetConfiguration(
            StringBuilder builder,
            string projectName,
            ImmutableArray<string> qtModules)
        {
            builder.Append("target_compile_features(");
            builder.Append(projectName);
            builder.Append(" PRIVATE cxx_std_20)\n");
            builder.Append("target_compile_definitions(");
            builder.Append(projectName);
            builder.Append(" PRIVATE QMLSHARP_NATIVE_BUILD)\n");
            AppendCMakeIncludes(builder, projectName);
            AppendCMakeLinks(builder, projectName, qtModules);
        }

        private static void AppendCMakeIncludes(StringBuilder builder, string projectName)
        {
            builder.Append("target_include_directories(");
            builder.Append(projectName);
            builder.Append("\n");
            builder.Append("  PRIVATE\n");
            builder.Append("    ${CMAKE_CURRENT_SOURCE_DIR}\n");
            builder.Append("    ${QMLSHARP_NATIVE_SOURCE_DIR}/include\n");
            builder.Append("    ${QMLSHARP_NATIVE_SOURCE_DIR}/src\n");
            builder.Append(")\n");
        }

        private static void AppendCMakeLinks(
            StringBuilder builder,
            string projectName,
            ImmutableArray<string> qtModules)
        {
            builder.Append("target_link_libraries(");
            builder.Append(projectName);
            builder.Append("\n");
            builder.Append("  PRIVATE\n");
            foreach (string module in qtModules)
            {
                builder.Append("    ");
                builder.Append(module);
                builder.Append('\n');
            }

            builder.Append(")\n");
        }

        /// <inheritdoc />
        public string GenerateTypeRegistration(
            ImmutableArray<ViewModelSchema> schemas,
            CppGenerationOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            ImmutableArray<ViewModelSchema> orderedSchemas = SortSchemas(schemas).ToImmutableArray();
            StringBuilder builder = new();
            AppendGeneratedHeader(builder);
            AppendTypeRegistrationIncludes(builder, orderedSchemas);
            AppendTypeRegistrationCallbacks(builder, orderedSchemas);
            AppendTypeRegistrationExport(builder, orderedSchemas);
            return builder.ToString();
        }

        private static void AppendTypeRegistrationIncludes(
            StringBuilder builder,
            ImmutableArray<ViewModelSchema> schemas)
        {
            builder.Append("#include \"qmlsharp/qmlsharp_abi.h\"\n\n");
            builder.Append("#include <qqml.h>\n");
            builder.Append("#include <stdint.h>\n");
            builder.Append('\n');
            builder.Append("#include <QtGlobal>\n\n");

            foreach (ViewModelSchema schema in schemas)
            {
                builder.Append("#include \"");
                builder.Append(schema.ClassName);
                builder.Append(".h\"\n");
            }

            if (!schemas.IsEmpty)
            {
                builder.Append('\n');
            }
        }

        private static void AppendTypeRegistrationCallbacks(
            StringBuilder builder,
            ImmutableArray<ViewModelSchema> schemas)
        {
            builder.Append("namespace {\n");
            foreach (ViewModelSchema schema in schemas)
            {
                string prefix = string.Concat("int32_t QMLSHARP_CALL register", schema.ClassName, "(");
                builder.Append(prefix);
                builder.Append("const char* moduleUri, int32_t versionMajor, int32_t versionMinor,\n");
                builder.Append(' ', prefix.Length);
                builder.Append("const char* typeName) {\n");
                builder.Append("    return qmlRegisterType<");
                builder.Append(schema.ClassName);
                builder.Append(">(moduleUri, versionMajor, versionMinor, typeName);\n");
                builder.Append("}\n\n");
            }

            builder.Append("}  // namespace\n\n");
        }

        private static void AppendTypeRegistrationExport(
            StringBuilder builder,
            ImmutableArray<ViewModelSchema> schemas)
        {
            const string prefix =
                "extern \"C\" QMLSHARP_API int32_t QMLSHARP_CALL qmlsharp_register_generated_type(";
            builder.Append(prefix);
            builder.Append("const char* moduleUri,\n");
            builder.Append(' ', prefix.Length);
            builder.Append("int32_t versionMajor,\n");
            builder.Append(' ', prefix.Length);
            builder.Append("int32_t versionMinor,\n");
            builder.Append(' ', prefix.Length);
            builder.Append("const char* typeName) {\n");
            builder.Append("    if (moduleUri == nullptr || typeName == nullptr) {\n");
            builder.Append("        return -2;\n");
            builder.Append("    }\n\n");
            foreach (ViewModelSchema schema in schemas)
            {
                AppendTypeRegistrationDispatch(builder, schema);
            }

            builder.Append("    return -6;\n");
            builder.Append("}\n");
        }

        private static void AppendTypeRegistrationDispatch(StringBuilder builder, ViewModelSchema schema)
        {
            builder.Append("    if (qstrcmp(typeName, \"");
            builder.Append(EscapeCppStringLiteral(schema.ClassName));
            builder.Append("\") == 0) {\n");
            builder.Append("        return register");
            builder.Append(schema.ClassName);
            builder.Append("(moduleUri, versionMajor, versionMinor, typeName);\n");
            builder.Append("    }\n\n");
        }

        private static void AppendConstructor(StringBuilder builder, ViewModelSchema schema)
        {
            builder.Append(schema.ClassName);
            builder.Append("::");
            builder.Append(schema.ClassName);
            builder.Append("(QObject* parent)\n");
            ImmutableArray<string>.Builder initializers = ImmutableArray.CreateBuilder<string>();
            initializers.Add("QObject(parent)");
            initializers.Add("m_instanceId(QUuid::createUuid().toString(QUuid::WithoutBraces))");
            initializers.Add(string.Concat(
                "m_compilerSlotKey(QStringLiteral(\"",
                EscapeCppStringLiteral(schema.CompilerSlotKey),
                "\"))"));

            foreach (StateEntry property in SortProperties(schema.Properties))
            {
                CppTypeMapping mapping = CppTypeMap.Map(property.Type);
                initializers.Add(string.Concat(
                    MemberName(property),
                    "(",
                    DefaultValueLiteral(property, mapping),
                    ")"));
            }

            for (int index = 0; index < initializers.Count; index++)
            {
                builder.Append(index == 0 ? "    : " : "      ");
                builder.Append(initializers[index]);
                builder.Append(index == initializers.Count - 1 ? " {\n" : ",\n");
            }

            builder.Append("    qmlsharp::notify_instance_created(this, m_instanceId, QStringLiteral(\"");
            builder.Append(EscapeCppStringLiteral(schema.ClassName));
            builder.Append("\"), m_compilerSlotKey);\n");
            builder.Append("}\n\n");
        }

        private static void AppendDestructor(StringBuilder builder, ViewModelSchema schema)
        {
            builder.Append(schema.ClassName);
            builder.Append("::~");
            builder.Append(schema.ClassName);
            builder.Append("() {\n");
            builder.Append("    qmlsharp::notify_instance_destroyed(m_instanceId);\n");
            builder.Append("}\n\n");
        }

        private static void AppendAccessors(StringBuilder builder, ViewModelSchema schema)
        {
            builder.Append("QString ");
            builder.Append(schema.ClassName);
            builder.Append("::instanceId() const {\n");
            builder.Append("    return m_instanceId;\n");
            builder.Append("}\n\n");
            builder.Append("QString ");
            builder.Append(schema.ClassName);
            builder.Append("::compilerSlotKey() const {\n");
            builder.Append("    return m_compilerSlotKey;\n");
            builder.Append("}\n\n");

            foreach (StateEntry property in SortProperties(schema.Properties))
            {
                CppTypeMapping mapping = CppTypeMap.Map(property.Type);
                builder.Append(mapping.CppType);
                builder.Append(' ');
                builder.Append(schema.ClassName);
                builder.Append("::");
                builder.Append(GetterName(property));
                builder.Append("() const {\n");
                builder.Append("    return ");
                builder.Append(MemberName(property));
                builder.Append(";\n");
                builder.Append("}\n\n");

                if (!property.ReadOnly)
                {
                    builder.Append("void ");
                    builder.Append(schema.ClassName);
                    builder.Append("::");
                    builder.Append(SetterName(property));
                    builder.Append('(');
                    builder.Append(SetterParameterType(mapping));
                    builder.Append(" value) {\n");
                    AppendSetterBody(builder, property, "value", mapping, indent: 4);
                    builder.Append("}\n\n");
                }
            }
        }

        private static void AppendCommands(StringBuilder builder, ViewModelSchema schema)
        {
            foreach (CommandEntry command in SortCommands(schema.Commands))
            {
                builder.Append("void ");
                builder.Append(schema.ClassName);
                builder.Append("::");
                builder.Append(SafeIdentifier(command.Name));
                builder.Append('(');
                builder.Append(CommandParameterList(command));
                builder.Append(") {\n");

                ImmutableArray<ParameterEntry> parameters = NormalizeParameters(command.Parameters);
                if (parameters.IsEmpty)
                {
                    builder.Append("    qmlsharp::dispatch_command(m_instanceId, QStringLiteral(\"");
                    builder.Append(EscapeCppStringLiteral(command.Name));
                    builder.Append("\"), QStringLiteral(\"[]\"));\n");
                }
                else
                {
                    builder.Append("    QJsonArray args;\n");
                    foreach (ParameterEntry parameter in parameters)
                    {
                        builder.Append("    args.append(QJsonValue::fromVariant(QVariant::fromValue(");
                        builder.Append(SafeIdentifier(parameter.Name));
                        builder.Append(")));\n");
                    }

                    builder.Append("    const QString argsJson = ");
                    builder.Append("QString::fromUtf8(QJsonDocument(args).toJson(QJsonDocument::Compact));\n");
                    builder.Append("    qmlsharp::dispatch_command(m_instanceId, QStringLiteral(\"");
                    builder.Append(EscapeCppStringLiteral(command.Name));
                    builder.Append("\"), argsJson);\n");
                }

                builder.Append("}\n\n");
            }
        }

        private static void AppendManagedStateSetters(StringBuilder builder, ViewModelSchema schema)
        {
            builder.Append("void ");
            builder.Append(schema.ClassName);
            builder.Append("::setPropertyFromManaged(const char* propertyName, const char* jsonValue) {\n");
            builder.Append("    if (propertyName == nullptr || jsonValue == nullptr) {\n");
            builder.Append("        return;\n");
            builder.Append("    }\n\n");
            builder.Append("    QJsonParseError parseError;\n");
            builder.Append("    const QJsonDocument document = QJsonDocument::fromJson(QByteArray(jsonValue), &parseError);\n");
            builder.Append("    if (parseError.error != QJsonParseError::NoError || document.isNull()) {\n");
            builder.Append("        return;\n");
            builder.Append("    }\n\n");
            builder.Append("    const QVariant value = document.toVariant();\n");

            bool hasAnyJsonBranch = false;
            foreach (StateEntry property in SortProperties(schema.Properties))
            {
                CppTypeMapping mapping = CppTypeMap.Map(property.Type);
                AppendPropertyNameBranchPrefix(builder, property, hasAnyJsonBranch, indent: 4);
                AppendPropertyAssignment(builder, property, ConvertFromVariantExpression(mapping), mapping, indent: 8);
                builder.Append("        return;\n");
                builder.Append("    }\n");
                hasAnyJsonBranch = true;
            }

            if (!hasAnyJsonBranch)
            {
                builder.Append("    Q_UNUSED(value);\n");
            }

            builder.Append("}\n\n");
            AppendTypedManagedSetter(builder, schema, CppFastPath.Int, "Int", "int", "value");
            AppendTypedManagedSetter(builder, schema, CppFastPath.Double, "Double", "double", "value");
            AppendTypedManagedSetter(builder, schema, CppFastPath.Bool, "Bool", "bool", "value");
            AppendStringManagedSetter(builder, schema);
        }

        private static void AppendTypedManagedSetter(
            StringBuilder builder,
            ViewModelSchema schema,
            CppFastPath fastPath,
            string suffix,
            string valueType,
            string valueExpression)
        {
            builder.Append("void ");
            builder.Append(schema.ClassName);
            builder.Append("::setPropertyFromManaged");
            builder.Append(suffix);
            builder.Append("(const char* propertyName, ");
            builder.Append(valueType);
            builder.Append(" value) {\n");
            builder.Append("    if (propertyName == nullptr) {\n");
            builder.Append("        return;\n");
            builder.Append("    }\n\n");

            bool hasBranch = false;
            foreach (StateEntry property in SortProperties(schema.Properties))
            {
                CppTypeMapping mapping = CppTypeMap.Map(property.Type);
                if (mapping.FastPath != fastPath)
                {
                    continue;
                }

                AppendPropertyNameBranchPrefix(builder, property, hasBranch, indent: 4);
                AppendPropertyAssignment(builder, property, valueExpression, mapping, indent: 8);
                builder.Append("        return;\n");
                builder.Append("    }\n");
                hasBranch = true;
            }

            if (!hasBranch)
            {
                builder.Append("    Q_UNUSED(value);\n");
            }

            builder.Append("}\n\n");
        }

        private static void AppendStringManagedSetter(StringBuilder builder, ViewModelSchema schema)
        {
            builder.Append("void ");
            builder.Append(schema.ClassName);
            builder.Append("::setPropertyFromManagedString(const char* propertyName, const char* value) {\n");
            builder.Append("    if (propertyName == nullptr) {\n");
            builder.Append("        return;\n");
            builder.Append("    }\n\n");
            builder.Append("    const QString qmlsharpValue = QString::fromUtf8(value == nullptr ? \"\" : value);\n");

            bool hasBranch = false;
            foreach (StateEntry property in SortProperties(schema.Properties))
            {
                CppTypeMapping mapping = CppTypeMap.Map(property.Type);
                if (mapping.FastPath != CppFastPath.String)
                {
                    continue;
                }

                AppendPropertyNameBranchPrefix(builder, property, hasBranch, indent: 4);
                AppendPropertyAssignment(builder, property, "qmlsharpValue", mapping, indent: 8);
                builder.Append("        return;\n");
                builder.Append("    }\n");
                hasBranch = true;
            }

            if (!hasBranch)
            {
                builder.Append("    Q_UNUSED(qmlsharpValue);\n");
            }

            builder.Append("}\n\n");
        }

        private static void AppendEffectHook(StringBuilder builder, ViewModelSchema schema)
        {
            builder.Append("void ");
            builder.Append(schema.ClassName);
            builder.Append("::emitEffectDispatched(const QString& effectName, const QString& payloadJson) {\n");
            builder.Append("    emit effectDispatched(effectName, payloadJson);\n");
            builder.Append("}\n");
        }

        private static void AppendPropertyNameBranchPrefix(
            StringBuilder builder,
            StateEntry property,
            bool hasPriorBranch,
            int indent)
        {
            builder.Append(' ', indent);
            builder.Append(hasPriorBranch ? "else if" : "if");
            builder.Append(" (qstrcmp(propertyName, \"");
            builder.Append(EscapeCppStringLiteral(property.Name));
            builder.Append("\") == 0) {\n");
        }

        private static void AppendPropertyAssignment(
            StringBuilder builder,
            StateEntry property,
            string valueExpression,
            CppTypeMapping mapping,
            int indent)
        {
            if (!property.ReadOnly)
            {
                builder.Append(' ', indent);
                builder.Append(SetterName(property));
                builder.Append('(');
                builder.Append(valueExpression);
                builder.Append(");\n");
                return;
            }

            AppendSetterBody(builder, property, valueExpression, mapping, indent);
        }

        private static void AppendSetterBody(
            StringBuilder builder,
            StateEntry property,
            string valueExpression,
            CppTypeMapping mapping,
            int indent)
        {
            builder.Append(' ', indent);
            builder.Append("if (");
            if (string.Equals(mapping.CppType, "double", StringComparison.Ordinal))
            {
                builder.Append("qFuzzyCompare(");
                builder.Append(MemberName(property));
                builder.Append(" + 1.0, ");
                builder.Append(valueExpression);
                builder.Append(" + 1.0)");
            }
            else
            {
                builder.Append(MemberName(property));
                builder.Append(" == ");
                builder.Append(valueExpression);
            }

            builder.Append(") {\n");
            builder.Append(' ', indent + 4);
            builder.Append("return;\n");
            builder.Append(' ', indent);
            builder.Append("}\n\n");
            builder.Append(' ', indent);
            builder.Append(MemberName(property));
            builder.Append(" = ");
            builder.Append(valueExpression);
            builder.Append(";\n");
            builder.Append(' ', indent);
            builder.Append("emit ");
            builder.Append(ChangedSignalName(property));
            builder.Append("();\n");
        }

        private static string CommandParameterList(CommandEntry command)
        {
            return string.Join(
                ", ",
                NormalizeParameters(command.Parameters)
                    .Select(static parameter => $"{CommandParameterType(CppTypeMap.Map(parameter.Type))} {SafeIdentifier(parameter.Name)}"));
        }

        private static string CommandParameterType(CppTypeMapping mapping)
        {
            return mapping.CppType switch
            {
                "int" or "double" or "bool" => mapping.CppType,
                _ => $"const {mapping.CppType}&",
            };
        }

        private static string SetterParameterType(CppTypeMapping mapping)
        {
            return mapping.CppType switch
            {
                "int" or "double" or "bool" => mapping.CppType,
                _ => $"const {mapping.CppType}&",
            };
        }

        private static string ConvertFromVariantExpression(CppTypeMapping mapping)
        {
            return mapping.CppType switch
            {
                "int" => "value.toInt()",
                "double" => "value.toDouble()",
                "bool" => "value.toBool()",
                "QString" => "value.toString()",
                "QUrl" => "value.toUrl()",
                "QColor" => "value.value<QColor>()",
                "QDate" => "value.toDate()",
                "QVariantList" => "value.toList()",
                "QPointF" => "value.toPointF()",
                "QRectF" => "value.toRectF()",
                "QSizeF" => "value.toSizeF()",
                _ => "value",
            };
        }

        private static string DefaultValueLiteral(StateEntry property, CppTypeMapping mapping)
        {
            string? defaultValue = property.DefaultValue;
            if (string.IsNullOrWhiteSpace(defaultValue))
            {
                return CppTypeMap.DefaultValue(property.Type);
            }

            return mapping.CppType switch
            {
                "int" => defaultValue,
                "double" => defaultValue,
                "bool" => bool.TryParse(defaultValue, out bool parsed) && parsed ? "true" : "false",
                "QString" => $"QStringLiteral(\"{EscapeCppStringLiteral(ParseStringDefault(defaultValue))}\")",
                _ => CppTypeMap.DefaultValue(property.Type),
            };
        }

        private static string ParseStringDefault(string defaultValue)
        {
            if (defaultValue.Length >= 2 && defaultValue[0] == '"' && defaultValue[^1] == '"')
            {
                try
                {
                    return JsonSerializer.Deserialize<string>(defaultValue) ?? string.Empty;
                }
                catch (JsonException)
                {
                    return defaultValue[1..^1];
                }
            }

            return string.Equals(defaultValue, "null", StringComparison.Ordinal)
                ? string.Empty
                : defaultValue;
        }

        private static ImmutableArray<BuildDiagnostic> CreateUnsupportedTypeDiagnostics(
            ImmutableArray<ViewModelSchema> schemas)
        {
            ImmutableArray<BuildDiagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<BuildDiagnostic>();
            foreach (ViewModelSchema schema in schemas)
            {
                foreach (StateEntry property in NormalizeProperties(schema.Properties))
                {
                    AddUnsupportedTypeDiagnostic(
                        diagnostics,
                        schema,
                        property.Type,
                        $"Property '{property.Name}'");
                }

                foreach (CommandEntry command in NormalizeCommands(schema.Commands))
                {
                    foreach (ParameterEntry parameter in NormalizeParameters(command.Parameters))
                    {
                        AddUnsupportedTypeDiagnostic(
                            diagnostics,
                            schema,
                            parameter.Type,
                            $"Command '{command.Name}' parameter '{parameter.Name}'");
                    }
                }

                foreach (EffectEntry effect in NormalizeEffects(schema.Effects))
                {
                    AddUnsupportedTypeDiagnostic(
                        diagnostics,
                        schema,
                        effect.PayloadType,
                        $"Effect '{effect.Name}' payload");
                    foreach (ParameterEntry parameter in NormalizeParameters(effect.Parameters))
                    {
                        AddUnsupportedTypeDiagnostic(
                            diagnostics,
                            schema,
                            parameter.Type,
                            $"Effect '{effect.Name}' parameter '{parameter.Name}'");
                    }
                }
            }

            return diagnostics.ToImmutable();
        }

        private static void AddUnsupportedTypeDiagnostic(
            ImmutableArray<BuildDiagnostic>.Builder diagnostics,
            ViewModelSchema schema,
            string qmlType,
            string location)
        {
            if (CppTypeMap.IsSupported(qmlType))
            {
                return;
            }

            diagnostics.Add(new BuildDiagnostic(
                BuildDiagnosticCode.UnsupportedCppType,
                BuildDiagnosticSeverity.Warning,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{location} on ViewModel '{schema.ClassName}' uses unsupported C++ type '{qmlType}' and falls back to QVariant."),
                BuildPhase.CppCodeGenAndBuild,
                $"{schema.ClassName}.schema.json"));
        }

        private static ImmutableArray<string> GetQtModules(CppGenerationOptions options)
        {
            ImmutableArray<string> configuredModules = options.QtModules.IsDefaultOrEmpty
                ? DefaultQtModules
                : options.QtModules;
            return configuredModules
                .Where(static module => !string.IsNullOrWhiteSpace(module))
                .Select(static module => module.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static string StripQtTargetPrefix(string module)
        {
            return module.StartsWith("Qt6::", StringComparison.Ordinal)
                ? module["Qt6::".Length..]
                : module;
        }

        private static IEnumerable<ViewModelSchema> SortSchemas(ImmutableArray<ViewModelSchema> schemas)
        {
            return (schemas.IsDefault ? ImmutableArray<ViewModelSchema>.Empty : schemas)
                .OrderBy(static schema => schema.ClassName, StringComparer.Ordinal)
                .ThenBy(static schema => schema.CompilerSlotKey, StringComparer.Ordinal);
        }

        private static IEnumerable<StateEntry> SortProperties(ImmutableArray<StateEntry> properties)
        {
            return NormalizeProperties(properties)
                .OrderBy(static property => property.Name, StringComparer.Ordinal)
                .ThenBy(static property => property.MemberId);
        }

        private static IEnumerable<CommandEntry> SortCommands(ImmutableArray<CommandEntry> commands)
        {
            return NormalizeCommands(commands)
                .OrderBy(static command => command.Name, StringComparer.Ordinal)
                .ThenBy(static command => command.CommandId);
        }

        private static ImmutableArray<StateEntry> NormalizeProperties(ImmutableArray<StateEntry> properties)
        {
            return properties.IsDefault ? ImmutableArray<StateEntry>.Empty : properties;
        }

        private static ImmutableArray<CommandEntry> NormalizeCommands(ImmutableArray<CommandEntry> commands)
        {
            return commands.IsDefault ? ImmutableArray<CommandEntry>.Empty : commands;
        }

        private static ImmutableArray<EffectEntry> NormalizeEffects(ImmutableArray<EffectEntry> effects)
        {
            return effects.IsDefault ? ImmutableArray<EffectEntry>.Empty : effects;
        }

        private static ImmutableArray<ParameterEntry> NormalizeParameters(ImmutableArray<ParameterEntry> parameters)
        {
            return parameters.IsDefault ? ImmutableArray<ParameterEntry>.Empty : parameters;
        }

        private static string GetGeneratedRoot(CppGenerationOptions options)
        {
            return Path.Join(options.OutputDir, "native", "generated");
        }

        private static string GetterName(StateEntry property)
        {
            return SafeIdentifier(property.Name);
        }

        private static string SetterName(StateEntry property)
        {
            return $"set{PascalIdentifier(property.Name)}";
        }

        private static string ChangedSignalName(StateEntry property)
        {
            return $"{SafeIdentifier(property.Name)}Changed";
        }

        private static string MemberName(StateEntry property)
        {
            return $"m_{SafeIdentifier(property.Name)}";
        }

        private static string PascalIdentifier(string value)
        {
            string identifier = SafeIdentifier(value);
            if (identifier.Length == 0)
            {
                return "Value";
            }

            return char.ToUpperInvariant(identifier[0]) + identifier[1..];
        }

        private static string SafeIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "qmlsharpValue";
            }

            StringBuilder builder = new(value.Length + 1);
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool valid = index == 0
                    ? character == '_' || char.IsLetter(character)
                    : character == '_' || char.IsLetterOrDigit(character);
                builder.Append(valid ? character : '_');
            }

            if (builder.Length == 0 || char.IsDigit(builder[0]))
            {
                builder.Insert(0, '_');
            }

            string identifier = builder.ToString();
            return IsCppKeyword(identifier) ? $"{identifier}_" : identifier;
        }

        private static string SafeCMakeTargetName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "qmlsharp_native";
            }

            StringBuilder builder = new(value.Length);
            foreach (char character in value)
            {
                builder.Append(char.IsLetterOrDigit(character) || character is '_' or '-' ? character : '_');
            }

            string targetName = builder.ToString();
            return char.IsDigit(targetName[0]) ? $"_{targetName}" : targetName;
        }

        private static bool IsCppKeyword(string identifier)
        {
            return identifier is "alignas" or "alignof" or "and" or "and_eq" or "asm" or "auto" or "bitand"
                or "bitor" or "bool" or "break" or "case" or "catch" or "char" or "class" or "compl"
                or "const" or "consteval" or "constexpr" or "constinit" or "const_cast" or "continue"
                or "co_await" or "co_return" or "co_yield" or "decltype" or "default" or "delete" or "do"
                or "double" or "dynamic_cast" or "else" or "enum" or "explicit" or "export" or "extern"
                or "false" or "float" or "for" or "friend" or "goto" or "if" or "inline" or "int" or "long"
                or "mutable" or "namespace" or "new" or "noexcept" or "not" or "not_eq" or "nullptr"
                or "operator" or "or" or "or_eq" or "private" or "protected" or "public" or "register"
                or "reinterpret_cast" or "requires" or "return" or "short" or "signed" or "sizeof"
                or "static" or "static_assert" or "static_cast" or "struct" or "switch" or "template"
                or "this" or "thread_local" or "throw" or "true" or "try" or "typedef" or "typeid"
                or "typename" or "union" or "unsigned" or "using" or "virtual" or "void" or "volatile"
                or "wchar_t" or "while" or "xor" or "xor_eq";
        }

        private static string EscapeCppStringLiteral(string value)
        {
            StringBuilder builder = new(value.Length);
            foreach (char character in value)
            {
                switch (character)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        builder.Append(character);
                        break;
                }
            }

            return builder.ToString();
        }

        private static string EscapeCMakeString(string value)
        {
            return value
                .Replace("\\", "/", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal);
        }

        private static string ToCMakePath(string path)
        {
            return path.Replace('\\', '/');
        }

        private static void AppendGeneratedHeader(StringBuilder builder)
        {
            builder.Append("// GENERATED — DO NOT EDIT\n");
        }
    }
}

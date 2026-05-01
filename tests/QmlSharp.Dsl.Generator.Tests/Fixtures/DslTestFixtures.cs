using QmlSharp.Registry;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Dsl.Generator.Tests.Fixtures
{
    internal static class DslTestFixtures
    {
        public static IRegistryQuery CreateMinimalFixture()
        {
            return CreateQuery(
                modules:
                [
                    CreateModule(
                        "QtQuick",
                        "QQuickItem",
                        "Item",
                        "QQuickRectangle",
                        "Rectangle",
                        "QQuickText",
                        "Text"),
                ],
                types:
                [
                    CreateQObjectType(),
                    CreateItemType(),
                    CreateRectangleType(),
                    CreateTextType(),
                ]);
        }

        public static IRegistryQuery CreateQtQuickFixture()
        {
            return CreateMinimalFixture();
        }

        public static IRegistryQuery CreateQtQuickControlsFixture()
        {
            return CreateQuery(
                modules:
                [
                    CreateModule(
                        "QtQuick",
                        "QQuickItem",
                        "Item",
                        "QQuickRectangle",
                        "Rectangle",
                        "QQuickText",
                        "Text"),
                    CreateModule("QtQuick.Controls", "QQuickButton", "Button"),
                ],
                types:
                [
                    CreateQObjectType(),
                    CreateItemType(),
                    CreateRectangleType(),
                    CreateTextType(),
                    CreateButtonType(),
                ]);
        }

        public static IRegistryQuery CreateP0Fixture()
        {
            return CreateP0ScaleFixture();
        }

        public static IRegistryQuery CreateP0ScaleFixture()
        {
            ImmutableArray<QmlType> qtQmlTypes = CreateQtQmlP0Types();
            ImmutableArray<QmlType> qtQuickTypes = CreateQtQuickP0Types();
            ImmutableArray<QmlType> controlsTypes = CreateQtQuickControlsP0Types();
            ImmutableArray<QmlType> layoutTypes = CreateQtQuickLayoutsP0Types();
            QmlType keysAttached = CreateKeysAttachedType();
            QmlType layoutAttached = CreateLayoutAttachedType();

            return CreateQuery(
                modules:
                [
                    CreateModule("QtQml", qtQmlTypes),
                    CreateModule("QtQuick", qtQuickTypes),
                    CreateModule("QtQuick.Controls", controlsTypes, dependencies: ["QtQuick"]),
                    CreateModule("QtQuick.Layouts", layoutTypes, dependencies: ["QtQuick"]),
                ],
                types: qtQmlTypes
                    .Concat(qtQuickTypes)
                    .Concat(controlsTypes)
                    .Concat(layoutTypes)
                    .Concat([keysAttached, layoutAttached])
                    .ToImmutableArray());
        }

        public static IRegistryQuery CreateCircularInheritanceFixture()
        {
            return CreateQuery(
                modules:
                [
                    CreateModule("QtQuick.Circular", "A", "A", "B", "B", "C", "C"),
                ],
                types:
                [
                    CreateQObjectType("A", "A", "QtQuick.Circular", "C", isCreatable: true),
                    CreateQObjectType("B", "B", "QtQuick.Circular", "A", isCreatable: true),
                    CreateQObjectType("C", "C", "QtQuick.Circular", "B", isCreatable: true),
                ]);
        }

        public static IRegistryQuery CreateAttachedTypesFixture()
        {
            return CreateQuery(
                modules:
                [
                    CreateModule("QtQuick", "QQuickItem", "Item"),
                    CreateModule("QtQuick.Layouts", "QQuickLayout", "Layout"),
                ],
                types:
                [
                    CreateQObjectType(),
                    CreateItemType(attachedType: "QQuickKeysAttached"),
                    CreateKeysAttachedType(),
                    CreateLayoutType(attachedType: "QQuickLayoutAttached"),
                    CreateLayoutAttachedType(),
                ]);
        }

        public static string CreateCounterViewModelSchema()
        {
            return """
                {
                  "schemaVersion": "1.0",
                  "className": "CounterViewModel",
                  "moduleUri": "QmlSharp.TestApp",
                  "compilerSlotKey": "CounterView::__qmlsharp_vm0",
                  "properties": [
                    { "name": "count", "type": "int", "readOnly": false }
                  ],
                  "commands": [
                    { "name": "increment", "parameters": [] }
                  ],
                  "effects": [
                    { "name": "showToast", "payloadType": "string" }
                  ]
                }
                """;
        }

        public static CodeEmitOptions DefaultEmitOptions { get; } = new(
            GenerateXmlDoc: true,
            MarkDeprecated: true,
            HeaderComment: "// <auto-generated />");

        public static GenerationOptions DefaultOptions { get; } = new(
            TypeMapper: null,
            Inheritance: new InheritanceOptions(MaxDepth: 32, IncludeQtObjectProperties: true),
            Properties: new PropertyOptions(
                GenerateBindMethods: true,
                GenerateReadonlyGetters: true,
                GenerateGroupedBuilders: true),
            Signals: new SignalOptions(HandlerPrefix: "On", SimplifyNoArgHandlers: true),
            Enums: new EnumOptions(GenerateFlagHelpers: true),
            Filter: new FilterOptions(
                CreatableOnly: false,
                ExcludeTypes: ImmutableArray<string>.Empty,
                ExcludeInternal: true,
                ExcludeDeprecated: true,
                VersionRange: null),
            ViewModel: new ViewModelOptions(Enabled: true, ProxyPrefix: "Qml"),
            Emit: DefaultEmitOptions,
            Packager: new PackagerOptions(
                OutputDir: "generated",
                PackageVersion: "0.1.0",
                PackagePrefix: "QmlSharp",
                GenerateReadme: true,
                GenerateProjectFile: true));

        public static GeneratedTypeCode CreateGeneratedRectangleMetadata()
        {
            QmlType rectangle = CreateRectangleType();
            QmlType item = CreateItemType();
            QmlType layoutAttached = CreateLayoutAttachedType();

            return new GeneratedTypeCode(
                QmlName: "Rectangle",
                ModuleUri: "QtQuick",
                FactoryName: "Rectangle",
                PropsInterfaceName: "IRectangleProps",
                BuilderInterfaceName: "IRectangleBuilder",
                FactoryMethodCode: "public static IRectangleBuilder Rectangle() => ObjectFactory.Create<IRectangleBuilder>(\"Rectangle\");",
                Properties:
                [
                    new GeneratedProperty(
                        Name: "Width",
                        SetterSignature: "IRectangleBuilder Width(double value)",
                        BindSignature: "IRectangleBuilder WidthBind(string expr)",
                        XmlDoc: "<summary>Sets width.</summary>",
                        DeclaredBy: item,
                        IsReadOnly: false,
                        IsRequired: false,
                        CSharpType: "double"),
                    new GeneratedProperty(
                        Name: "Color",
                        SetterSignature: "IRectangleBuilder Color(QmlColor value)",
                        BindSignature: "IRectangleBuilder ColorBind(string expr)",
                        XmlDoc: "<summary>Sets color.</summary>",
                        DeclaredBy: rectangle,
                        IsReadOnly: false,
                        IsRequired: false,
                        CSharpType: "QmlColor"),
                    new GeneratedProperty(
                        Name: "BorderColor",
                        SetterSignature: "IRectangleBuilder BorderColor(QmlColor value)",
                        BindSignature: "IRectangleBuilder BorderColorBind(string expr)",
                        XmlDoc: "<summary>Sets border.color.</summary>",
                        DeclaredBy: rectangle,
                        IsReadOnly: false,
                        IsRequired: false,
                        CSharpType: "QmlColor"),
                    new GeneratedProperty(
                        Name: "BorderWidth",
                        SetterSignature: "IRectangleBuilder BorderWidth(double value)",
                        BindSignature: "IRectangleBuilder BorderWidthBind(string expr)",
                        XmlDoc: "<summary>Sets border.width.</summary>",
                        DeclaredBy: rectangle,
                        IsReadOnly: false,
                        IsRequired: false,
                        CSharpType: "double"),
                ],
                Signals:
                [
                    new GeneratedSignal(
                        SignalName: "colorChanged",
                        HandlerName: "OnColorChanged",
                        HandlerSignature: "IRectangleBuilder OnColorChanged(Action handler)",
                        XmlDoc: "<summary>Handles colorChanged.</summary>",
                        DeclaredBy: rectangle,
                        Parameters: ImmutableArray<GeneratedParameter>.Empty),
                ],
                Methods: ImmutableArray<GeneratedMethod>.Empty,
                Enums: ImmutableArray<GeneratedEnum>.Empty,
                AttachedTypes:
                [
                    new GeneratedAttachedType(
                        TypeName: "QQuickLayoutAttached",
                        MethodName: "Layout",
                        ResolvedType: new ResolvedType(
                            layoutAttached,
                            [layoutAttached],
                            layoutAttached.Properties.Select(property => new ResolvedProperty(property, layoutAttached, false)).ToImmutableArray(),
                            ImmutableArray<ResolvedSignal>.Empty,
                            ImmutableArray<ResolvedMethod>.Empty,
                            ImmutableArray<QmlEnum>.Empty,
                            AttachedType: null,
                            ExtensionType: null),
                        Properties:
                        [
                            new GeneratedProperty(
                                Name: "FillWidth",
                                SetterSignature: "ILayoutAttachedBuilder FillWidth(bool value)",
                                BindSignature: "ILayoutAttachedBuilder FillWidthBind(string expr)",
                                XmlDoc: "<summary>Sets fillWidth.</summary>",
                                DeclaredBy: layoutAttached,
                                IsReadOnly: false,
                                IsRequired: false,
                                CSharpType: "bool"),
                            new GeneratedProperty(
                                Name: "FillHeight",
                                SetterSignature: "ILayoutAttachedBuilder FillHeight(bool value)",
                                BindSignature: "ILayoutAttachedBuilder FillHeightBind(string expr)",
                                XmlDoc: "<summary>Sets fillHeight.</summary>",
                                DeclaredBy: layoutAttached,
                                IsReadOnly: false,
                                IsRequired: false,
                                CSharpType: "bool"),
                        ],
                        Signals: ImmutableArray<GeneratedSignal>.Empty,
                        BuilderInterfaceName: "ILayoutAttachedBuilder"),
                ],
                DefaultProperty: new DefaultPropertyInfo("data", "Item", IsList: true, GenerateChildMethod: true, GenerateChildrenMethod: true),
                IsCreatable: true,
                IsDeprecated: false);
        }

        public static GeneratedTypeCode CreateGeneratedTextMetadata()
        {
            QmlType text = CreateTextType();
            QmlType item = CreateItemType();

            return new GeneratedTypeCode(
                QmlName: "Text",
                ModuleUri: "QtQuick",
                FactoryName: "Text",
                PropsInterfaceName: "ITextProps",
                BuilderInterfaceName: "ITextBuilder",
                FactoryMethodCode: "public static ITextBuilder Text() => ObjectFactory.Create<ITextBuilder>(\"Text\");",
                Properties:
                [
                    new GeneratedProperty(
                        Name: "LineCount",
                        SetterSignature: string.Empty,
                        BindSignature: null,
                        XmlDoc: "<summary>Gets lineCount.</summary>",
                        DeclaredBy: text,
                        IsReadOnly: true,
                        IsRequired: false,
                        CSharpType: "int"),
                    new GeneratedProperty(
                        Name: "Text",
                        SetterSignature: "ITextBuilder Text(string value)",
                        BindSignature: "ITextBuilder TextBind(string expr)",
                        XmlDoc: "<summary>Sets text.</summary>",
                        DeclaredBy: text,
                        IsReadOnly: false,
                        IsRequired: false,
                        CSharpType: "string"),
                    new GeneratedProperty(
                        Name: "Width",
                        SetterSignature: "ITextBuilder Width(double value)",
                        BindSignature: "ITextBuilder WidthBind(string expr)",
                        XmlDoc: "<summary>Sets width.</summary>",
                        DeclaredBy: item,
                        IsReadOnly: false,
                        IsRequired: false,
                        CSharpType: "double"),
                    new GeneratedProperty(
                        Name: "WrapMode",
                        SetterSignature: "ITextBuilder WrapMode(TextWrapMode value)",
                        BindSignature: "ITextBuilder WrapModeBind(string expr)",
                        XmlDoc: "<summary>Sets wrapMode.</summary>",
                        DeclaredBy: text,
                        IsReadOnly: false,
                        IsRequired: false,
                        CSharpType: "TextWrapMode"),
                ],
                Signals:
                [
                    new GeneratedSignal(
                        SignalName: "textChanged",
                        HandlerName: "OnTextChanged",
                        HandlerSignature: "ITextBuilder OnTextChanged(Action handler)",
                        XmlDoc: "<summary>Handles textChanged.</summary>",
                        DeclaredBy: text,
                        Parameters: ImmutableArray<GeneratedParameter>.Empty),
                ],
                Methods:
                [
                    new GeneratedMethod(
                        Name: "Append",
                        Signature: "ITextBuilder Append(string value)",
                        Parameters: [new GeneratedParameter("value", "string", "string")],
                        ReturnType: "void",
                        XmlDoc: "<summary>Invokes append.</summary>",
                        DeclaredBy: text,
                        IsConstructor: false),
                ],
                Enums:
                [
                    new GeneratedEnum(
                        Name: "TextWrapMode",
                        Alias: null,
                        IsFlag: false,
                        IsScoped: true,
                        Members:
                        [
                            new GeneratedEnumMember("NoWrap", 0),
                            new GeneratedEnumMember("WordWrap", 1),
                        ],
                        Code: "public enum TextWrapMode\n{\n    NoWrap = 0,\n    WordWrap = 1\n}",
                        OwnerType: text),
                ],
                AttachedTypes: ImmutableArray<GeneratedAttachedType>.Empty,
                DefaultProperty: null,
                IsCreatable: true,
                IsDeprecated: false);
        }

        public static GeneratedTypeCode CreateGeneratedButtonMetadata()
        {
            QmlType button = CreateButtonType();
            QmlType item = CreateItemType();

            return new GeneratedTypeCode(
                QmlName: "Button",
                ModuleUri: "QtQuick.Controls",
                FactoryName: "Button",
                PropsInterfaceName: "IButtonProps",
                BuilderInterfaceName: "IButtonBuilder",
                FactoryMethodCode: "public static IButtonBuilder Button() => ObjectFactory.Create<IButtonBuilder>(\"Button\");",
                Properties:
                [
                    new GeneratedProperty(
                        Name: "Checked",
                        SetterSignature: "IButtonBuilder Checked(bool value)",
                        BindSignature: "IButtonBuilder CheckedBind(string expr)",
                        XmlDoc: "<summary>Sets checked.</summary>",
                        DeclaredBy: button,
                        IsReadOnly: false,
                        IsRequired: false,
                        CSharpType: "bool"),
                    new GeneratedProperty(
                        Name: "Text",
                        SetterSignature: "IButtonBuilder Text(string value)",
                        BindSignature: "IButtonBuilder TextBind(string expr)",
                        XmlDoc: "<summary>Sets text.</summary>",
                        DeclaredBy: button,
                        IsReadOnly: false,
                        IsRequired: false,
                        CSharpType: "string"),
                    new GeneratedProperty(
                        Name: "Width",
                        SetterSignature: "IButtonBuilder Width(double value)",
                        BindSignature: "IButtonBuilder WidthBind(string expr)",
                        XmlDoc: "<summary>Sets width.</summary>",
                        DeclaredBy: item,
                        IsReadOnly: false,
                        IsRequired: false,
                        CSharpType: "double"),
                ],
                Signals:
                [
                    new GeneratedSignal(
                        SignalName: "clicked",
                        HandlerName: "OnClicked",
                        HandlerSignature: "IButtonBuilder OnClicked(Action handler)",
                        XmlDoc: "<summary>Handles clicked.</summary>",
                        DeclaredBy: button,
                        Parameters: ImmutableArray<GeneratedParameter>.Empty),
                ],
                Methods:
                [
                    new GeneratedMethod(
                        Name: "Click",
                        Signature: "IButtonBuilder Click()",
                        Parameters: ImmutableArray<GeneratedParameter>.Empty,
                        ReturnType: "void",
                        XmlDoc: "<summary>Invokes click.</summary>",
                        DeclaredBy: button,
                        IsConstructor: false),
                ],
                Enums: ImmutableArray<GeneratedEnum>.Empty,
                AttachedTypes: ImmutableArray<GeneratedAttachedType>.Empty,
                DefaultProperty: null,
                IsCreatable: true,
                IsDeprecated: false);
        }

        public static GeneratedOutputTempDirectory CreateGeneratedOutputTempDirectory()
        {
            return GeneratedOutputTempDirectory.Create();
        }

        private static TestRegistryQuery CreateQuery(IReadOnlyList<QmlModule> modules, IReadOnlyList<QmlType> types)
        {
            return new TestRegistryQuery(modules, types.OrderBy(type => type.QualifiedName, StringComparer.Ordinal).ToArray(), "6.11.0");
        }

        private static QmlModule CreateModule(string uri, params string[] qualifiedNameAndQmlNamePairs)
        {
            if (qualifiedNameAndQmlNamePairs.Length % 2 != 0)
            {
                throw new ArgumentException("Module type pairs must contain qualified-name and QML-name entries.", nameof(qualifiedNameAndQmlNamePairs));
            }

            ImmutableArray<QmlModuleType>.Builder types = ImmutableArray.CreateBuilder<QmlModuleType>();
            for (int index = 0; index < qualifiedNameAndQmlNamePairs.Length; index += 2)
            {
                types.Add(new QmlModuleType(
                    QualifiedName: qualifiedNameAndQmlNamePairs[index],
                    QmlName: qualifiedNameAndQmlNamePairs[index + 1],
                    ExportVersion: new QmlVersion(2, 15)));
            }

            return new QmlModule(
                Uri: uri,
                Version: new QmlVersion(2, 15),
                Dependencies: ImmutableArray<string>.Empty,
                Imports: ImmutableArray<string>.Empty,
                Types: types.ToImmutable());
        }

        private static QmlModule CreateModule(
            string uri,
            ImmutableArray<QmlType> types,
            ImmutableArray<string>? dependencies = null)
        {
            return new QmlModule(
                Uri: uri,
                Version: new QmlVersion(2, 15),
                Dependencies: dependencies ?? ImmutableArray<string>.Empty,
                Imports: ImmutableArray<string>.Empty,
                Types: types
                    .Where(static type => type.QmlName is not null)
                    .OrderBy(static type => type.QmlName, StringComparer.Ordinal)
                    .ThenBy(static type => type.QualifiedName, StringComparer.Ordinal)
                    .Select(static type => new QmlModuleType(
                        type.QualifiedName,
                        type.QmlName!,
                        new QmlVersion(2, 15)))
                    .ToImmutableArray());
        }

        private static ImmutableArray<QmlType> CreateQtQmlP0Types()
        {
            string[] creatableNames =
            [
                "QtObject",
                "Binding",
                "Connections",
                "Component",
                "Timer",
                "State",
                "Transition",
                "PropertyChanges",
            ];
            string[] nonCreatableNames =
            [
                "StateGroup",
                "AnimationGroup",
            ];

            return CreateNamedTypes(
                "QtQml",
                "QQml",
                "QObject",
                creatableNames,
                nonCreatableNames,
                prototype: "QObject",
                includeBaseProperties: false,
                extraProperties: [CreateProperty("enabled", "bool"), CreateProperty("objectName", "string")],
                extraSignals: [CreateSignal("completed")],
                extraMethods: [CreateMethod("restart", "void")],
                extraEnums: [CreateEnum("Status", "Ready", "Loading", "Error")],
                rootTypes: [CreateQObjectType("QObject", "QtObject", "QtQml", null, isCreatable: true)]);
        }

        private static ImmutableArray<QmlType> CreateQtQuickP0Types()
        {
            string[] creatableNames =
            [
                "Rectangle", "Text", "Image", "MouseArea", "Flickable", "ListView", "GridView", "PathView",
                "Repeater", "Loader", "Canvas", "AnimatedImage", "BorderImage", "ShaderEffect", "ItemDelegate",
                "FocusScope", "Flow", "Grid", "Row", "Column", "TextInput", "TextEdit", "MultiPointTouchArea",
                "PinchArea", "DragHandler", "TapHandler", "HoverHandler", "WheelHandler", "SpriteSequence",
                "AnimatedSprite", "ParticleSystem", "Emitter", "ImageParticle", "Age", "Attractor", "Gravity",
                "Wander", "ScaleAffector", "OpacityAnimator", "XAnimator", "YAnimator", "RotationAnimator",
                "ScaleAnimator", "SequentialAnimation", "ParallelAnimation", "NumberAnimation", "ColorAnimation",
                "SmoothedAnimation", "SpringAnimation", "PauseAnimation", "PropertyAnimation", "PathAnimation",
                "ParentAnimation", "AnchorAnimation", "ScriptAction", "StateChangeScript", "Behavior",
                "Gradient", "GradientStop", "Path", "PathLine", "PathQuad", "PathCubic", "PathArc",
                "PathAttribute", "PathPercent", "PathSvg", "PathCurve", "PathMultiline", "PathMove",
            ];
            string[] nonCreatableNames =
            [
                "Item", "Animation", "TransitionBase", "PointerHandler", "ShapePath", "TextMetrics",
                "FontMetrics", "Shortcut", "Accessible", "Palette",
            ];

            return CreateNamedTypes(
                "QtQuick",
                "QQuick",
                "QQuickItem",
                creatableNames,
                nonCreatableNames,
                prototype: "QQuickItem",
                includeBaseProperties: true,
                extraProperties:
                [
                    CreateProperty("width", "double"),
                    CreateProperty("height", "double"),
                    CreateProperty("visible", "bool"),
                    CreateProperty("opacity", "double"),
                    CreateProperty("color", "color"),
                    CreateProperty("border.width", "double"),
                    CreateProperty("border.color", "color"),
                    CreateProperty("font.family", "string"),
                    CreateProperty("font.pixelSize", "int"),
                ],
                extraSignals: [CreateSignal("visibleChanged"), CreateSignal("pressed", new QmlParameter("event", "var"))],
                extraMethods: [CreateMethod("forceActiveFocus", "void"), CreateMethod("mapToItem", "point", new QmlParameter("x", "double"), new QmlParameter("y", "double"))],
                extraEnums: [CreateEnum("HAlignment", "AlignLeft", "AlignRight", "AlignHCenter")],
                attachedOwner: "QQuickItem",
                attachedType: "QQuickKeysAttached",
                rootTypes: [CreateItemType(attachedType: "QQuickKeysAttached")]);
        }

        private static ImmutableArray<QmlType> CreateQtQuickControlsP0Types()
        {
            string[] creatableNames =
            [
                "Button", "Label", "TextField", "TextArea", "CheckBox", "RadioButton", "Switch", "Slider",
                "RangeSlider", "Dial", "ComboBox", "SpinBox", "ProgressBar", "BusyIndicator", "ScrollBar",
                "ScrollView", "Pane", "Frame", "GroupBox", "Page", "PageIndicator", "StackView", "SwipeView",
                "TabBar", "TabButton", "ToolBar", "ToolButton", "ToolSeparator", "Menu", "MenuItem",
                "MenuSeparator", "Drawer", "Dialog", "DialogButtonBox", "Popup", "ToolTip", "ApplicationWindow",
                "Action", "ActionGroup", "RoundButton", "DelayButton", "Tumbler", "Calendar", "TreeView",
                "HorizontalHeaderView", "VerticalHeaderView",
            ];
            string[] nonCreatableNames =
            [
                "Control", "AbstractButton", "Container", "ControlSkin",
            ];

            return CreateNamedTypes(
                "QtQuick.Controls",
                "QQuickControl",
                "QQuickItem",
                creatableNames,
                nonCreatableNames,
                prototype: "QQuickItem",
                includeBaseProperties: false,
                extraProperties:
                [
                    CreateProperty("text", "string"),
                    CreateProperty("checked", "bool"),
                    CreateProperty("enabled", "bool"),
                    CreateProperty("spacing", "double"),
                    CreateProperty("font.family", "string"),
                    CreateProperty("font.pixelSize", "int"),
                ],
                extraSignals: [CreateSignal("clicked"), CreateSignal("accepted")],
                extraMethods: [CreateMethod("toggle", "void")],
                extraEnums: [CreateEnum("Position", "Header", "Footer", "OnlyOne")]);
        }

        private static ImmutableArray<QmlType> CreateQtQuickLayoutsP0Types()
        {
            string[] creatableNames =
            [
                "Layout", "RowLayout", "ColumnLayout", "GridLayout", "StackLayout", "FlowLayout",
            ];
            string[] nonCreatableNames =
            [
                "LayoutItem",
            ];

            return CreateNamedTypes(
                "QtQuick.Layouts",
                "QQuick",
                "QQuickItem",
                creatableNames,
                nonCreatableNames,
                prototype: "QQuickItem",
                includeBaseProperties: false,
                extraProperties:
                [
                    CreateProperty("spacing", "double"),
                    CreateProperty("columns", "int"),
                    CreateProperty("rows", "int"),
                ],
                extraSignals: [CreateSignal("layoutChanged")],
                extraMethods: [CreateMethod("invalidate", "void")],
                extraEnums: [CreateEnum("LayoutDirection", "LeftToRight", "RightToLeft")],
                attachedOwner: "QQuickLayout",
                attachedType: "QQuickLayoutAttached");
        }

        private static ImmutableArray<QmlType> CreateNamedTypes(
            string moduleUri,
            string qualifiedPrefix,
            string rootQualifiedName,
            IReadOnlyList<string> creatableNames,
            IReadOnlyList<string> nonCreatableNames,
            string prototype,
            bool includeBaseProperties,
            ImmutableArray<QmlProperty> extraProperties,
            ImmutableArray<QmlSignal> extraSignals,
            ImmutableArray<QmlMethod> extraMethods,
            ImmutableArray<QmlEnum> extraEnums,
            string? attachedOwner = null,
            string? attachedType = null,
            ImmutableArray<QmlType>? rootTypes = null)
        {
            ImmutableArray<QmlType>.Builder types = ImmutableArray.CreateBuilder<QmlType>();
            if (rootTypes.HasValue)
            {
                types.AddRange(rootTypes.Value);
            }

            foreach (string name in creatableNames.Order(StringComparer.Ordinal))
            {
                string qualifiedName = string.Equals(name, "QtObject", StringComparison.Ordinal)
                    ? "QObject"
                    : $"{qualifiedPrefix}{name}";
                if (types.Any(type => string.Equals(type.QualifiedName, qualifiedName, StringComparison.Ordinal)))
                {
                    continue;
                }

                types.Add(CreateP0Type(
                    qualifiedName,
                    name,
                    moduleUri,
                    string.Equals(qualifiedName, rootQualifiedName, StringComparison.Ordinal) ? "QObject" : prototype,
                    isCreatable: true,
                    properties: SelectP0Properties(name, includeBaseProperties, extraProperties),
                    signals: extraSignals,
                    methods: extraMethods,
                    enums: CreateTypeSpecificEnums(name, extraEnums),
                    attachedType: string.Equals(qualifiedName, attachedOwner, StringComparison.Ordinal) ? attachedType : null,
                    defaultProperty: IsVisualContainer(name) ? "data" : null));
            }

            foreach (string name in nonCreatableNames.Order(StringComparer.Ordinal))
            {
                string qualifiedName = string.Equals(name, "Item", StringComparison.Ordinal)
                    ? rootQualifiedName
                    : $"{qualifiedPrefix}{name}";
                if (types.Any(type => string.Equals(type.QualifiedName, qualifiedName, StringComparison.Ordinal)))
                {
                    continue;
                }

                types.Add(CreateP0Type(
                    qualifiedName,
                    name,
                    moduleUri,
                    prototype,
                    isCreatable: false,
                    properties: SelectP0Properties(name, includeBaseProperties, extraProperties),
                    signals: extraSignals,
                    methods: extraMethods,
                    enums: CreateTypeSpecificEnums(name, extraEnums),
                    attachedType: string.Equals(qualifiedName, attachedOwner, StringComparison.Ordinal) ? attachedType : null,
                    defaultProperty: IsVisualContainer(name) ? "data" : null));
            }

            return types
                .OrderBy(static type => type.ModuleUri, StringComparer.Ordinal)
                .ThenBy(static type => type.QmlName, StringComparer.Ordinal)
                .ThenBy(static type => type.QualifiedName, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static ImmutableArray<QmlEnum> CreateTypeSpecificEnums(string typeName, ImmutableArray<QmlEnum> enums)
        {
            return enums
                .Select(qmlEnum => qmlEnum with { Name = $"{typeName}{qmlEnum.Name}" })
                .ToImmutableArray();
        }

        private static QmlType CreateP0Type(
            string qualifiedName,
            string qmlName,
            string moduleUri,
            string? prototype,
            bool isCreatable,
            ImmutableArray<QmlProperty> properties,
            ImmutableArray<QmlSignal> signals,
            ImmutableArray<QmlMethod> methods,
            ImmutableArray<QmlEnum> enums,
            string? attachedType,
            string? defaultProperty)
        {
            return new QmlType(
                QualifiedName: qualifiedName,
                QmlName: qmlName,
                ModuleUri: moduleUri,
                AccessSemantics: AccessSemantics.Reference,
                Prototype: prototype,
                DefaultProperty: defaultProperty,
                AttachedType: attachedType,
                Extension: null,
                IsSingleton: false,
                IsCreatable: isCreatable,
                Exports: CreateExports(moduleUri, qmlName),
                Properties: properties,
                Signals: signals,
                Methods: methods,
                Enums: enums,
                Interfaces: ImmutableArray<string>.Empty);
        }

        private static ImmutableArray<QmlProperty> SelectP0Properties(
            string typeName,
            bool includeBaseProperties,
            ImmutableArray<QmlProperty> commonProperties)
        {
            ImmutableArray<QmlProperty>.Builder properties = ImmutableArray.CreateBuilder<QmlProperty>();
            properties.Add(CreateProperty("objectName", "string"));
            if (includeBaseProperties || IsVisualContainer(typeName))
            {
                properties.Add(CreateProperty("width", "double"));
                properties.Add(CreateProperty("height", "double"));
            }

            foreach (QmlProperty property in commonProperties)
            {
                if (!properties.Any(existing => string.Equals(existing.Name, property.Name, StringComparison.Ordinal)))
                {
                    properties.Add(property);
                }
            }

            return properties.ToImmutable();
        }

        private static bool IsVisualContainer(string name)
        {
            return name.Contains("View", StringComparison.Ordinal)
                || name.Contains("Layout", StringComparison.Ordinal)
                || string.Equals(name, "Rectangle", StringComparison.Ordinal)
                || string.Equals(name, "Item", StringComparison.Ordinal)
                || string.Equals(name, "Pane", StringComparison.Ordinal)
                || string.Equals(name, "Page", StringComparison.Ordinal)
                || string.Equals(name, "ApplicationWindow", StringComparison.Ordinal);
        }

        private static QmlEnum CreateEnum(string name, params string[] values)
        {
            return new QmlEnum(
                name,
                IsFlag: false,
                values
                    .Select((value, index) => new QmlEnumValue(value, index))
                    .ToImmutableArray(),
                Alias: null,
                IsScoped: true);
        }

        private static QmlType CreateQObjectType(
            string qualifiedName = "QObject",
            string? qmlName = null,
            string? moduleUri = null,
            string? prototype = null,
            bool isCreatable = false)
        {
            return new QmlType(
                QualifiedName: qualifiedName,
                QmlName: qmlName,
                ModuleUri: moduleUri,
                AccessSemantics: AccessSemantics.Reference,
                Prototype: prototype,
                DefaultProperty: null,
                AttachedType: null,
                Extension: null,
                IsSingleton: false,
                IsCreatable: isCreatable,
                Exports: CreateExports(moduleUri, qmlName),
                Properties: ImmutableArray<QmlProperty>.Empty,
                Signals: ImmutableArray<QmlSignal>.Empty,
                Methods: ImmutableArray<QmlMethod>.Empty,
                Enums: ImmutableArray<QmlEnum>.Empty,
                Interfaces: ImmutableArray<string>.Empty);
        }

        private static QmlType CreateItemType(string? attachedType = null)
        {
            return new QmlType(
                QualifiedName: "QQuickItem",
                QmlName: "Item",
                ModuleUri: "QtQuick",
                AccessSemantics: AccessSemantics.Reference,
                Prototype: "QObject",
                DefaultProperty: "data",
                AttachedType: attachedType,
                Extension: null,
                IsSingleton: false,
                IsCreatable: true,
                Exports: CreateExports("QtQuick", "Item"),
                Properties:
                [
                    CreateProperty("width", "double"),
                    CreateProperty("height", "double"),
                    CreateProperty("visible", "bool"),
                ],
                Signals:
                [
                    CreateSignal("widthChanged"),
                    CreateSignal("visibleChanged"),
                ],
                Methods:
                [
                    CreateMethod("forceActiveFocus", "void"),
                ],
                Enums:
                [
                    new QmlEnum("TransformOrigin", false, [new QmlEnumValue("TopLeft", 0), new QmlEnumValue("Center", 1)]),
                ],
                Interfaces: ImmutableArray<string>.Empty);
        }

        private static QmlType CreateRectangleType()
        {
            return new QmlType(
                QualifiedName: "QQuickRectangle",
                QmlName: "Rectangle",
                ModuleUri: "QtQuick",
                AccessSemantics: AccessSemantics.Reference,
                Prototype: "QQuickItem",
                DefaultProperty: "data",
                AttachedType: null,
                Extension: null,
                IsSingleton: false,
                IsCreatable: true,
                Exports: CreateExports("QtQuick", "Rectangle"),
                Properties:
                [
                    CreateProperty("color", "color"),
                    CreateProperty("radius", "double"),
                    CreateProperty("border.width", "double"),
                    CreateProperty("border.color", "color"),
                ],
                Signals:
                [
                    CreateSignal("colorChanged"),
                ],
                Methods: ImmutableArray<QmlMethod>.Empty,
                Enums: ImmutableArray<QmlEnum>.Empty,
                Interfaces: ImmutableArray<string>.Empty);
        }

        private static QmlType CreateTextType()
        {
            return new QmlType(
                QualifiedName: "QQuickText",
                QmlName: "Text",
                ModuleUri: "QtQuick",
                AccessSemantics: AccessSemantics.Reference,
                Prototype: "QQuickItem",
                DefaultProperty: null,
                AttachedType: null,
                Extension: null,
                IsSingleton: false,
                IsCreatable: true,
                Exports: CreateExports("QtQuick", "Text"),
                Properties:
                [
                    CreateProperty("text", "string"),
                    CreateProperty("wrapMode", "int"),
                ],
                Signals: [CreateSignal("textChanged")],
                Methods: [CreateMethod("append", "void", new QmlParameter("value", "string"))],
                Enums: ImmutableArray<QmlEnum>.Empty,
                Interfaces: ImmutableArray<string>.Empty);
        }

        private static QmlType CreateButtonType()
        {
            return new QmlType(
                QualifiedName: "QQuickButton",
                QmlName: "Button",
                ModuleUri: "QtQuick.Controls",
                AccessSemantics: AccessSemantics.Reference,
                Prototype: "QQuickItem",
                DefaultProperty: null,
                AttachedType: null,
                Extension: null,
                IsSingleton: false,
                IsCreatable: true,
                Exports: CreateExports("QtQuick.Controls", "Button"),
                Properties: [CreateProperty("text", "string"), CreateProperty("checked", "bool")],
                Signals: [CreateSignal("clicked")],
                Methods: [CreateMethod("click", "void")],
                Enums: ImmutableArray<QmlEnum>.Empty,
                Interfaces: ImmutableArray<string>.Empty);
        }

        private static QmlType CreateLayoutType(string? attachedType = null)
        {
            return new QmlType(
                QualifiedName: "QQuickLayout",
                QmlName: "Layout",
                ModuleUri: "QtQuick.Layouts",
                AccessSemantics: AccessSemantics.Reference,
                Prototype: "QQuickItem",
                DefaultProperty: "data",
                AttachedType: attachedType,
                Extension: null,
                IsSingleton: false,
                IsCreatable: true,
                Exports: CreateExports("QtQuick.Layouts", "Layout"),
                Properties: [CreateProperty("spacing", "double")],
                Signals: ImmutableArray<QmlSignal>.Empty,
                Methods: ImmutableArray<QmlMethod>.Empty,
                Enums: ImmutableArray<QmlEnum>.Empty,
                Interfaces: ImmutableArray<string>.Empty);
        }

        private static QmlType CreateKeysAttachedType()
        {
            return new QmlType(
                QualifiedName: "QQuickKeysAttached",
                QmlName: null,
                ModuleUri: null,
                AccessSemantics: AccessSemantics.Reference,
                Prototype: "QObject",
                DefaultProperty: null,
                AttachedType: null,
                Extension: null,
                IsSingleton: false,
                IsCreatable: false,
                Exports: ImmutableArray<QmlTypeExport>.Empty,
                Properties: [CreateProperty("enabled", "bool")],
                Signals: [CreateSignal("pressed", new QmlParameter("event", "var"))],
                Methods: ImmutableArray<QmlMethod>.Empty,
                Enums: ImmutableArray<QmlEnum>.Empty,
                Interfaces: ImmutableArray<string>.Empty);
        }

        private static QmlType CreateLayoutAttachedType()
        {
            return new QmlType(
                QualifiedName: "QQuickLayoutAttached",
                QmlName: null,
                ModuleUri: null,
                AccessSemantics: AccessSemantics.Reference,
                Prototype: "QObject",
                DefaultProperty: null,
                AttachedType: null,
                Extension: null,
                IsSingleton: false,
                IsCreatable: false,
                Exports: ImmutableArray<QmlTypeExport>.Empty,
                Properties: [CreateProperty("fillWidth", "bool"), CreateProperty("fillHeight", "bool")],
                Signals: ImmutableArray<QmlSignal>.Empty,
                Methods: ImmutableArray<QmlMethod>.Empty,
                Enums: ImmutableArray<QmlEnum>.Empty,
                Interfaces: ImmutableArray<string>.Empty);
        }

        private static QmlProperty CreateProperty(string name, string typeName)
        {
            return new QmlProperty(
                Name: name,
                TypeName: typeName,
                IsReadonly: false,
                IsList: false,
                IsRequired: false,
                DefaultValue: null,
                NotifySignal: $"{name.Replace(".", string.Empty, StringComparison.Ordinal)}Changed");
        }

        private static QmlSignal CreateSignal(string name, params QmlParameter[] parameters)
        {
            return new QmlSignal(name, parameters.ToImmutableArray());
        }

        private static QmlMethod CreateMethod(string name, string? returnType, params QmlParameter[] parameters)
        {
            return new QmlMethod(name, returnType, parameters.ToImmutableArray());
        }

        private static ImmutableArray<QmlTypeExport> CreateExports(string? moduleUri, string? qmlName)
        {
            if (moduleUri is null || qmlName is null)
            {
                return ImmutableArray<QmlTypeExport>.Empty;
            }

            return [new QmlTypeExport(moduleUri, qmlName, new QmlVersion(2, 15))];
        }
    }
}

namespace QmlSharp.Qml.Ast.Tests.Helpers
{
    internal sealed class TestTypeChecker : ITypeChecker
    {
        private readonly HashSet<string> _types;
        private readonly HashSet<string> _attachedTypes;
        private readonly HashSet<string> _modules;
        private readonly Dictionary<string, HashSet<string>> _propertiesByType;
        private readonly Dictionary<string, HashSet<string>> _signalsByType;
        private readonly Dictionary<string, HashSet<string>> _requiredPropertiesByType;
        private readonly Dictionary<string, HashSet<string>> _readonlyPropertiesByType;
        private readonly Dictionary<string, HashSet<string>> _enumMembersByType;

        public TestTypeChecker()
        {
            _types = new HashSet<string>(StringComparer.Ordinal)
            {
                "Item",
                "Rectangle",
                "Text",
                "Image",
                "Layout",
                "Button",
                "string",
                "int",
                "bool",
                "real",
                "QtQuick.Item",
                "QtQuick.Rectangle",
                "QtQuick.Text",
                "QtQuick.Image",
                "QtQuick.Layout",
                "QtQuick.Controls.Button",
            };

            _attachedTypes = new HashSet<string>(StringComparer.Ordinal)
            {
                "Layout",
                "Keys",
            };

            _modules = new HashSet<string>(StringComparer.Ordinal)
            {
                "QtQuick",
                "QtQuick.Controls",
            };

            _propertiesByType = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                ["Item"] = new HashSet<string>(StringComparer.Ordinal) { "width", "height", "visible", "opacity", "x", "y" },
                ["Rectangle"] = new HashSet<string>(StringComparer.Ordinal) { "width", "height", "visible", "opacity", "x", "y", "color", "radius", "border" },
                ["Text"] = new HashSet<string>(StringComparer.Ordinal) { "width", "height", "visible", "opacity", "x", "y", "text", "font", "color" },
                ["Image"] = new HashSet<string>(StringComparer.Ordinal) { "width", "height", "visible", "opacity", "x", "y", "fillMode", "source", "sourceSize" },
                ["Button"] = new HashSet<string>(StringComparer.Ordinal) { "width", "height", "visible", "opacity", "x", "y", "text" },
                ["QtQuick.Controls.Button"] = new HashSet<string>(StringComparer.Ordinal) { "width", "height", "visible", "opacity", "x", "y", "text" },
                ["Layout"] = new HashSet<string>(StringComparer.Ordinal) { "fillWidth", "fillHeight", "alignment" },
                ["Keys"] = new HashSet<string>(StringComparer.Ordinal) { "enabled" },
            };

            _signalsByType = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                ["Item"] = new HashSet<string>(StringComparer.Ordinal) { "clicked", "pressed", "released" },
                ["Rectangle"] = new HashSet<string>(StringComparer.Ordinal) { "clicked" },
                ["Text"] = new HashSet<string>(StringComparer.Ordinal) { "linkActivated" },
            };

            _requiredPropertiesByType = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                ["Text"] = new HashSet<string>(StringComparer.Ordinal) { "text" },
            };

            _readonlyPropertiesByType = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                ["Image"] = new HashSet<string>(StringComparer.Ordinal) { "sourceSize" },
            };

            _enumMembersByType = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                ["Image"] = new HashSet<string>(StringComparer.Ordinal) { "Stretch", "PreserveAspectFit", "PreserveAspectCrop" },
                ["Qt"] = new HashSet<string>(StringComparer.Ordinal) { "AlignLeft", "AlignRight", "AlignCenter" },
            };
        }

        public bool HasType(string typeName)
        {
            return _types.Contains(typeName);
        }

        public bool HasProperty(string typeName, string propertyName)
        {
            return _propertiesByType.TryGetValue(typeName, out HashSet<string>? properties)
                && properties.Contains(propertyName);
        }

        public bool HasSignal(string typeName, string signalName)
        {
            return _signalsByType.TryGetValue(typeName, out HashSet<string>? signals)
                && signals.Contains(signalName);
        }

        public bool IsAttachedType(string typeName)
        {
            return _attachedTypes.Contains(typeName);
        }

        public bool IsPropertyRequired(string typeName, string propertyName)
        {
            return _requiredPropertiesByType.TryGetValue(typeName, out HashSet<string>? properties)
                && properties.Contains(propertyName);
        }

        public bool IsPropertyReadonly(string typeName, string propertyName)
        {
            return _readonlyPropertiesByType.TryGetValue(typeName, out HashSet<string>? properties)
                && properties.Contains(propertyName);
        }

        public bool HasEnumMember(string typeName, string memberName)
        {
            return _enumMembersByType.TryGetValue(typeName, out HashSet<string>? members)
                && members.Contains(memberName);
        }

        public bool HasModule(string moduleUri)
        {
            return _modules.Contains(moduleUri);
        }
    }
}

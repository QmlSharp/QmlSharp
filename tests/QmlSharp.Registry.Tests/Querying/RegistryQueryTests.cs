using QmlSharp.Registry.Querying;
using QmlSharp.Registry.Tests.Helpers;

namespace QmlSharp.Registry.Tests.Querying
{
    public sealed class RegistryQueryTests
    {
        private static readonly IRegistryQuery Query = new RegistryQuery(RegistryFixtures.CreateQueryFixture());

        [Fact]
        public void RQY_01_FindModule_with_valid_uri_returns_module()
        {
            QmlModule? module = Query.FindModule("QtQuick");

            Assert.NotNull(module);
            Assert.Equal("QtQuick", module!.Uri);
            Assert.Equal(new QmlVersion(2, 15), module.Version);
        }

        [Fact]
        public void RQY_02_FindModule_with_invalid_uri_returns_null()
        {
            Assert.Null(Query.FindModule("QtQuick.Unknown"));
        }

        [Fact]
        public void RQY_03_GetAllModules_returns_all_modules()
        {
            IReadOnlyList<QmlModule> modules = Query.GetAllModules();

            Assert.Equal(2, modules.Count);
            Assert.Equal(["QtQuick", "QtQuick.Controls"], modules.Select(module => module.Uri).ToArray());
        }

        [Fact]
        public void RQY_04_GetModuleTypes_returns_types_in_module()
        {
            IReadOnlyList<QmlType> types = Query.GetModuleTypes("QtQuick");

            Assert.Equal(["Item", "Rectangle", "Text"], types.Select(type => type.QmlName!).ToArray());
        }

        [Fact]
        public void RQY_05_GetModuleTypes_for_empty_module_returns_empty_list()
        {
            Assert.Empty(Query.GetModuleTypes("QtQuick.Layouts"));
        }

        [Fact]
        public void RQY_06_FindTypeByQualifiedName_returns_matching_type()
        {
            QmlType? type = Query.FindTypeByQualifiedName("QQuickItem");

            Assert.NotNull(type);
            Assert.Equal("Item", type!.QmlName);
        }

        [Fact]
        public void RQY_07_FindTypeByQualifiedName_for_unknown_type_returns_null()
        {
            Assert.Null(Query.FindTypeByQualifiedName("QQuickMissing"));
        }

        [Fact]
        public void RQY_08_FindTypeByQmlName_returns_matching_type()
        {
            QmlType? type = Query.FindTypeByQmlName("QtQuick", "Item");

            Assert.NotNull(type);
            Assert.Equal("QQuickItem", type!.QualifiedName);
        }

        [Fact]
        public void RQY_09_FindTypeByQmlName_for_unknown_module_returns_null()
        {
            Assert.Null(Query.FindTypeByQmlName("QtQuick.Unknown", "Item"));
        }

        [Fact]
        public void RQY_10_FindTypes_with_predicate_returns_matching_types()
        {
            IReadOnlyList<QmlType> types = Query.FindTypes(type => type.IsSingleton);

            QmlType singleton = Assert.Single(types);
            Assert.Equal("QmlSharpPalette", singleton.QualifiedName);
        }

        [Fact]
        public void RQY_11_GetInheritanceChain_for_leaf_type_returns_full_chain()
        {
            IReadOnlyList<QmlType> chain = Query.GetInheritanceChain("QQuickRectangle");

            Assert.Equal(["QQuickRectangle", "QQuickItem", "QObject"], chain.Select(type => type.QualifiedName).ToArray());
        }

        [Fact]
        public void RQY_12_GetInheritanceChain_for_root_type_returns_single_element_chain()
        {
            IReadOnlyList<QmlType> chain = Query.GetInheritanceChain("QObject");

            QmlType type = Assert.Single(chain);
            Assert.Equal("QObject", type.QualifiedName);
        }

        [Fact]
        public void RQY_13_GetInheritanceChain_for_unknown_type_returns_empty_list()
        {
            Assert.Empty(Query.GetInheritanceChain("QQuickUnknown"));
        }

        [Fact]
        public void RQY_14_InheritsFrom_returns_true_for_direct_base_type()
        {
            Assert.True(Query.InheritsFrom("QQuickRectangle", "QQuickItem"));
        }

        [Fact]
        public void RQY_15_InheritsFrom_returns_true_for_indirect_base_type()
        {
            Assert.True(Query.InheritsFrom("QQuickRectangle", "QObject"));
        }

        [Fact]
        public void RQY_16_InheritsFrom_returns_false_when_type_is_not_a_base_type()
        {
            Assert.False(Query.InheritsFrom("QQuickRectangle", "QQuickText"));
            Assert.False(Query.InheritsFrom("QQuickRectangle", "QQuickRectangle"));
        }

        [Fact]
        public void RQY_17_FindProperty_returns_own_property_when_declared_on_type()
        {
            ResolvedProperty? property = Query.FindProperty("QQuickRectangle", "color");

            Assert.NotNull(property);
            Assert.Equal("QQuickRectangle", property!.DeclaringType.QualifiedName);
            Assert.False(property.IsInherited);
        }

        [Fact]
        public void RQY_18_FindProperty_returns_inherited_property_when_declared_on_base_type()
        {
            ResolvedProperty? property = Query.FindProperty("QQuickRectangle", "height");

            Assert.NotNull(property);
            Assert.Equal("QQuickItem", property!.DeclaringType.QualifiedName);
            Assert.True(property.IsInherited);
        }

        [Fact]
        public void RQY_19_FindProperty_for_nonexistent_property_returns_null()
        {
            Assert.Null(Query.FindProperty("QQuickRectangle", "noSuchProp"));
        }

        [Fact]
        public void RQY_20_GetAllProperties_includes_inherited_properties_without_duplicates()
        {
            IReadOnlyList<ResolvedProperty> properties = Query.GetAllProperties("QQuickRectangle");

            Assert.Equal(5, properties.Count);
            Assert.Contains(properties, property => property.Property.Name == "color" && property.DeclaringType.QualifiedName == "QQuickRectangle");
            Assert.Contains(properties, property => property.Property.Name == "height" && property.DeclaringType.QualifiedName == "QQuickItem");
            _ = Assert.Single(properties, property => property.Property.Name == "width");
        }

        [Fact]
        public void RQY_21_FindSignal_returns_own_signal_when_declared_on_type()
        {
            ResolvedSignal? signal = Query.FindSignal("QQuickRectangle", "colorChanged");

            Assert.NotNull(signal);
            Assert.Equal("QQuickRectangle", signal!.DeclaringType.QualifiedName);
            Assert.False(signal.IsInherited);
        }

        [Fact]
        public void RQY_22_FindSignal_returns_inherited_signal_when_declared_on_base_type()
        {
            ResolvedSignal? signal = Query.FindSignal("QQuickRectangle", "visibleChanged");

            Assert.NotNull(signal);
            Assert.Equal("QQuickItem", signal!.DeclaringType.QualifiedName);
            Assert.True(signal.IsInherited);
        }

        [Fact]
        public void RQY_23_FindMethods_returns_own_method_when_declared_on_type()
        {
            IReadOnlyList<ResolvedMethod> methods = Query.FindMethods("QQuickRectangle", "startAnimation");

            ResolvedMethod method = Assert.Single(methods);
            Assert.Equal("QQuickRectangle", method.DeclaringType.QualifiedName);
            Assert.False(method.IsInherited);
        }

        [Fact]
        public void RQY_24_GetAllMethods_includes_methods_from_all_ancestors_and_overloads()
        {
            IReadOnlyList<ResolvedMethod> methods = Query.GetAllMethods("QQuickRectangle");

            Assert.Equal(4, methods.Count);
            Assert.Contains(methods, method => method.Method.Name == "forceLayout" && method.DeclaringType.QualifiedName == "QQuickItem");
            Assert.Equal(2, methods.Count(method => method.Method.Name == "contains"));
        }

        [Fact]
        public void RQY_25_GetCreatableTypes_returns_only_reference_types_with_exports()
        {
            IReadOnlyList<QmlType> types = Query.GetCreatableTypes();

            Assert.Equal(["QQuickButton", "QQuickItem", "QQuickRectangle", "QQuickText"], types.Select(type => type.QualifiedName).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        }

        [Fact]
        public void RQY_26_GetValueTypes_returns_only_value_types()
        {
            QmlType valueType = Assert.Single(Query.GetValueTypes());

            Assert.Equal("QColor", valueType.QualifiedName);
        }

        [Fact]
        public void RQY_27_GetSingletonTypes_returns_only_singletons()
        {
            QmlType singleton = Assert.Single(Query.GetSingletonTypes());

            Assert.Equal("QmlSharpPalette", singleton.QualifiedName);
        }

        [Fact]
        public void RQY_28_GetAttachedTypes_returns_only_types_with_attached_type_references()
        {
            QmlType attached = Assert.Single(Query.GetAttachedTypes());

            Assert.Equal("QQuickItem", attached.QualifiedName);
            Assert.Equal("QQuickKeysAttached", attached.AttachedType);
        }

        [Fact]
        public void RQY_29_GetSequenceTypes_returns_only_sequence_types()
        {
            QmlType sequence = Assert.Single(Query.GetSequenceTypes());

            Assert.Equal("QVariantList", sequence.QualifiedName);
        }

        [Fact]
        public void RQY_30_Property_shadowing_prefers_the_derived_type_property()
        {
            ResolvedProperty? property = Query.FindProperty("QQuickRectangle", "width");

            Assert.NotNull(property);
            Assert.Equal("QQuickRectangle", property!.DeclaringType.QualifiedName);
            Assert.Equal("int", property.Property.TypeName);
            Assert.False(property.IsInherited);
        }

        [Fact]
        public void FindMethods_returns_matching_overloads_from_the_type_and_its_ancestors()
        {
            IReadOnlyList<ResolvedMethod> methods = Query.FindMethods("QQuickRectangle", "contains");

            Assert.Equal(2, methods.Count);
            Assert.Collection(
                methods,
                first =>
                {
                    Assert.Equal("QQuickRectangle", first.DeclaringType.QualifiedName);
                    Assert.False(first.IsInherited);
                    _ = Assert.Single(first.Method.Parameters);
                    Assert.Equal("point", first.Method.Parameters[0].TypeName);
                },
                second =>
                {
                    Assert.Equal("QQuickItem", second.DeclaringType.QualifiedName);
                    Assert.True(second.IsInherited);
                    Assert.Equal(2, second.Method.Parameters.Length);
                });
        }

        [Fact]
        public void Member_queries_for_unknown_types_return_null_or_empty_collections_as_specified()
        {
            Assert.Null(Query.FindProperty("QQuickUnknown", "width"));
            Assert.Empty(Query.GetAllProperties("QQuickUnknown"));
            Assert.Null(Query.FindSignal("QQuickUnknown", "visibleChanged"));
            Assert.Empty(Query.GetAllSignals("QQuickUnknown"));
            Assert.Empty(Query.FindMethods("QQuickUnknown", "forceLayout"));
            Assert.Empty(Query.GetAllMethods("QQuickUnknown"));
        }

        [Fact]
        public void GetAllSignals_preserves_signal_overloads_and_deduplicates_matching_signatures()
        {
            QmlRegistry registry = RegistryFixtures.CreateQueryFixture();
            QmlType item = registry.TypesByQualifiedName["QQuickItem"];
            QmlType rectangle = registry.TypesByQualifiedName["QQuickRectangle"];
            QmlType overloadedItem = item with
            {
                Signals =
                [
                    .. item.Signals,
                    RegistryFixtures.CreateSignal("colorChanged"),
                    RegistryFixtures.CreateSignal("colorChanged", new QmlParameter("name", "string")),
                ],
            };
            QmlType overloadedRectangle = rectangle with
            {
                Signals =
                [
                    .. rectangle.Signals,
                    RegistryFixtures.CreateSignal("colorChanged", new QmlParameter("color", "color")),
                ],
            };
            IRegistryQuery query = new RegistryQuery((registry with
            {
                TypesByQualifiedName = registry.TypesByQualifiedName
                    .SetItem(overloadedItem.QualifiedName, overloadedItem)
                    .SetItem(overloadedRectangle.QualifiedName, overloadedRectangle),
            }).WithLookupIndexes());

            IReadOnlyList<ResolvedSignal> colorChangedSignals = query.GetAllSignals("QQuickRectangle")
                .Where(signal => signal.Signal.Name == "colorChanged")
                .ToArray();

            Assert.Collection(
                colorChangedSignals,
                first =>
                {
                    Assert.Equal("QQuickRectangle", first.DeclaringType.QualifiedName);
                    Assert.Empty(first.Signal.Parameters);
                },
                second =>
                {
                    Assert.Equal("QQuickRectangle", second.DeclaringType.QualifiedName);
                    Assert.Equal("color", Assert.Single(second.Signal.Parameters).TypeName);
                },
                third =>
                {
                    Assert.Equal("QQuickItem", third.DeclaringType.QualifiedName);
                    Assert.Equal("string", Assert.Single(third.Signal.Parameters).TypeName);
                    Assert.True(third.IsInherited);
                });
        }

        [Fact]
        public void FindSignal_returns_first_matching_overload_without_losing_other_overloads()
        {
            QmlRegistry registry = RegistryFixtures.CreateQueryFixture();
            QmlType rectangle = registry.TypesByQualifiedName["QQuickRectangle"] with
            {
                Signals =
                [
                    RegistryFixtures.CreateSignal("overloaded"),
                    RegistryFixtures.CreateSignal("overloaded", new QmlParameter("value", "double")),
                ],
            };
            IRegistryQuery query = new RegistryQuery((registry with
            {
                TypesByQualifiedName = registry.TypesByQualifiedName.SetItem(rectangle.QualifiedName, rectangle),
            }).WithLookupIndexes());

            ResolvedSignal? signal = query.FindSignal("QQuickRectangle", "overloaded");
            ResolvedSignal[] overloads = query.GetAllSignals("QQuickRectangle")
                .Where(candidate => candidate.Signal.Name == "overloaded")
                .ToArray();

            Assert.NotNull(signal);
            Assert.Empty(signal!.Signal.Parameters);
            Assert.Equal(2, overloads.Length);
        }

        [Fact]
        public void GetCreatableTypes_uses_explicit_IsCreatable_flag_not_exports_only()
        {
            QmlRegistry registry = RegistryFixtures.CreateQueryFixture();
            QmlType exportedButNotCreatable = RegistryFixtures.CreateButtonType() with
            {
                QualifiedName = "QQuickHiddenButton",
                QmlName = "HiddenButton",
                IsCreatable = false,
                Exports = [new QmlTypeExport("QtQuick.Controls", "HiddenButton", new QmlVersion(2, 15))],
            };
            IRegistryQuery query = new RegistryQuery((registry with
            {
                TypesByQualifiedName = registry.TypesByQualifiedName.Add(exportedButNotCreatable.QualifiedName, exportedButNotCreatable),
            }).WithLookupIndexes());

            Assert.DoesNotContain(query.GetCreatableTypes(), type => type.QualifiedName == "QQuickHiddenButton");
        }

        [Fact]
        public void Query_methods_return_null_or_empty_for_null_and_empty_inputs()
        {
            Assert.Null(Query.FindModule(null!));
            Assert.Null(Query.FindModule(string.Empty));
            Assert.Empty(Query.GetModuleTypes(null!));
            Assert.Empty(Query.GetModuleTypes(string.Empty));
            Assert.Null(Query.FindTypeByQualifiedName(null!));
            Assert.Null(Query.FindTypeByQualifiedName(string.Empty));
            Assert.Null(Query.FindTypeByQmlName(null!, "Item"));
            Assert.Null(Query.FindTypeByQmlName("QtQuick", null!));
            Assert.Null(Query.FindTypeByQmlName(string.Empty, "Item"));
            Assert.Null(Query.FindTypeByQmlName("QtQuick", string.Empty));
            Assert.Empty(Query.GetInheritanceChain(null!));
            Assert.Empty(Query.GetInheritanceChain(string.Empty));
            Assert.False(Query.InheritsFrom(null!, "QObject"));
            Assert.False(Query.InheritsFrom("QQuickItem", null!));
            Assert.False(Query.InheritsFrom(string.Empty, "QObject"));
            Assert.False(Query.InheritsFrom("QQuickItem", string.Empty));
            Assert.Null(Query.FindProperty(null!, "width"));
            Assert.Null(Query.FindProperty("QQuickItem", null!));
            Assert.Null(Query.FindProperty(string.Empty, "width"));
            Assert.Null(Query.FindProperty("QQuickItem", string.Empty));
            Assert.Empty(Query.GetAllProperties(null!));
            Assert.Empty(Query.GetAllProperties(string.Empty));
            Assert.Null(Query.FindSignal(null!, "visibleChanged"));
            Assert.Null(Query.FindSignal("QQuickItem", null!));
            Assert.Null(Query.FindSignal(string.Empty, "visibleChanged"));
            Assert.Null(Query.FindSignal("QQuickItem", string.Empty));
            Assert.Empty(Query.GetAllSignals(null!));
            Assert.Empty(Query.GetAllSignals(string.Empty));
            Assert.Empty(Query.FindMethods(null!, "contains"));
            Assert.Empty(Query.FindMethods("QQuickItem", null!));
            Assert.Empty(Query.FindMethods(string.Empty, "contains"));
            Assert.Empty(Query.FindMethods("QQuickItem", string.Empty));
            Assert.Empty(Query.GetAllMethods(null!));
            Assert.Empty(Query.GetAllMethods(string.Empty));
        }

        [Fact]
        public void FindTypes_with_null_predicate_throws_argument_null_exception()
        {
            _ = Assert.Throws<ArgumentNullException>(() =>
            {
                _ = Query.FindTypes(null!);
            });
        }
    }
}

using QmlSharp.Qt.Tools.Tests.Helpers;

namespace QmlSharp.Qt.Tools.Tests.Contracts
{
    [Trait("Category", TestCategories.Smoke)]
    public sealed class QmlLintCategoryContractTests
    {
        [Fact]
        public void QmlLintCategory_ContainsAll59Qt611Categories()
        {
            string[] expected =
            [
                nameof(QmlLintCategory.Compiler),
                nameof(QmlLintCategory.ControlsSanity),
                nameof(QmlLintCategory.Deferred),
                nameof(QmlLintCategory.Deprecated),
                nameof(QmlLintCategory.DuplicatePropertyBinding),
                nameof(QmlLintCategory.EnforceSignalHandlers),
                nameof(QmlLintCategory.ImportFailure),
                nameof(QmlLintCategory.InheritanceCycle),
                nameof(QmlLintCategory.LintPluginWarning),
                nameof(QmlLintCategory.Multiline),
                nameof(QmlLintCategory.PrefixedImportType),
                nameof(QmlLintCategory.PropertyAliasCycles),
                nameof(QmlLintCategory.ReadOnlyProperty),
                nameof(QmlLintCategory.RequiredProperty),
                nameof(QmlLintCategory.RestrictedType),
                nameof(QmlLintCategory.SignalHandlerParameters),
                nameof(QmlLintCategory.Syntax),
                nameof(QmlLintCategory.TopLevelComponent),
                nameof(QmlLintCategory.Uncreateable),
                nameof(QmlLintCategory.UnexpectedType),
                nameof(QmlLintCategory.UnqualifiedAccess),
                nameof(QmlLintCategory.UnresolvedAlias),
                nameof(QmlLintCategory.UnresolvedType),
                nameof(QmlLintCategory.UnusedImports),
                nameof(QmlLintCategory.WithStatement),
                nameof(QmlLintCategory.AccessSingleton),
                nameof(QmlLintCategory.AttachedPropertyReuse),
                nameof(QmlLintCategory.CaseSensitiveType),
                nameof(QmlLintCategory.DuplicatedName),
                nameof(QmlLintCategory.ExtraParenthesis),
                nameof(QmlLintCategory.ForbiddenType),
                nameof(QmlLintCategory.MissingEnumEntry),
                nameof(QmlLintCategory.MissingProperty),
                nameof(QmlLintCategory.MissingType),
                nameof(QmlLintCategory.NonListProperty),
                nameof(QmlLintCategory.NotAnObject),
                nameof(QmlLintCategory.OverriddenSignal),
                nameof(QmlLintCategory.PropertyAssignment),
                nameof(QmlLintCategory.RecursionDepthError),
                nameof(QmlLintCategory.StoreProperty),
                nameof(QmlLintCategory.TypeAnnotation),
                nameof(QmlLintCategory.TypePropertiesDefault),
                nameof(QmlLintCategory.UnnecessaryDot),
                nameof(QmlLintCategory.UnusedValue),
                nameof(QmlLintCategory.VarUsedBeforeDeclaration),
                nameof(QmlLintCategory.QdsExactlyOneTopLevelItem),
                nameof(QmlLintCategory.QdsNoInlineComponents),
                nameof(QmlLintCategory.QdsNoListProperty),
                nameof(QmlLintCategory.QdsNoPropertyAccessFromOtherFile),
                nameof(QmlLintCategory.QdsNoReferenceOutside),
                nameof(QmlLintCategory.QdsNoUnsupportedType),
                nameof(QmlLintCategory.AnchorCombinations),
                nameof(QmlLintCategory.LayoutChildren),
                nameof(QmlLintCategory.MissingGridItemColumns),
                nameof(QmlLintCategory.MissingGridItemRows),
                nameof(QmlLintCategory.NegativeShiftAmount),
                nameof(QmlLintCategory.OverlappingAnchors),
                nameof(QmlLintCategory.PropertyChanges),
                nameof(QmlLintCategory.ShiftOverflow),
            ];

            string[] actual = Enum.GetNames<QmlLintCategory>();

            Assert.Equal(59, actual.Length);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void QmlLintCategory_CliNameMappingsCoverAllQt611Categories()
        {
            KeyValuePair<QmlLintCategory, string>[] expected =
            [
                KeyValuePair.Create(QmlLintCategory.Compiler, "compiler"),
                KeyValuePair.Create(QmlLintCategory.ControlsSanity, "controls-sanity"),
                KeyValuePair.Create(QmlLintCategory.Deferred, "deferred"),
                KeyValuePair.Create(QmlLintCategory.Deprecated, "deprecated"),
                KeyValuePair.Create(QmlLintCategory.DuplicatePropertyBinding, "duplicate-property-binding"),
                KeyValuePair.Create(QmlLintCategory.EnforceSignalHandlers, "enforce-signal-handlers"),
                KeyValuePair.Create(QmlLintCategory.ImportFailure, "import-failure"),
                KeyValuePair.Create(QmlLintCategory.InheritanceCycle, "inheritance-cycle"),
                KeyValuePair.Create(QmlLintCategory.LintPluginWarning, "lint-plugin-warning"),
                KeyValuePair.Create(QmlLintCategory.Multiline, "multiline"),
                KeyValuePair.Create(QmlLintCategory.PrefixedImportType, "prefixed-import-type"),
                KeyValuePair.Create(QmlLintCategory.PropertyAliasCycles, "property-alias-cycles"),
                KeyValuePair.Create(QmlLintCategory.ReadOnlyProperty, "read-only-property"),
                KeyValuePair.Create(QmlLintCategory.RequiredProperty, "required-property"),
                KeyValuePair.Create(QmlLintCategory.RestrictedType, "restricted-type"),
                KeyValuePair.Create(QmlLintCategory.SignalHandlerParameters, "signal-handler-parameters"),
                KeyValuePair.Create(QmlLintCategory.Syntax, "syntax"),
                KeyValuePair.Create(QmlLintCategory.TopLevelComponent, "top-level-component"),
                KeyValuePair.Create(QmlLintCategory.Uncreateable, "uncreateable"),
                KeyValuePair.Create(QmlLintCategory.UnexpectedType, "unexpected-type"),
                KeyValuePair.Create(QmlLintCategory.UnqualifiedAccess, "unqualified"),
                KeyValuePair.Create(QmlLintCategory.UnresolvedAlias, "unresolved-alias"),
                KeyValuePair.Create(QmlLintCategory.UnresolvedType, "unresolved-type"),
                KeyValuePair.Create(QmlLintCategory.UnusedImports, "unused-imports"),
                KeyValuePair.Create(QmlLintCategory.WithStatement, "with"),
                KeyValuePair.Create(QmlLintCategory.AccessSingleton, "access-singleton"),
                KeyValuePair.Create(QmlLintCategory.AttachedPropertyReuse, "attached-property-reuse"),
                KeyValuePair.Create(QmlLintCategory.CaseSensitiveType, "case-sensitive-type"),
                KeyValuePair.Create(QmlLintCategory.DuplicatedName, "duplicated-name"),
                KeyValuePair.Create(QmlLintCategory.ExtraParenthesis, "extra-parenthesis"),
                KeyValuePair.Create(QmlLintCategory.ForbiddenType, "forbidden-type"),
                KeyValuePair.Create(QmlLintCategory.MissingEnumEntry, "missing-enum-entry"),
                KeyValuePair.Create(QmlLintCategory.MissingProperty, "missing-property"),
                KeyValuePair.Create(QmlLintCategory.MissingType, "missing-type"),
                KeyValuePair.Create(QmlLintCategory.NonListProperty, "non-list-property"),
                KeyValuePair.Create(QmlLintCategory.NotAnObject, "not-an-object"),
                KeyValuePair.Create(QmlLintCategory.OverriddenSignal, "overridden-signal"),
                KeyValuePair.Create(QmlLintCategory.PropertyAssignment, "property-assignment"),
                KeyValuePair.Create(QmlLintCategory.RecursionDepthError, "recursion-depth-error"),
                KeyValuePair.Create(QmlLintCategory.StoreProperty, "store-property"),
                KeyValuePair.Create(QmlLintCategory.TypeAnnotation, "type-annotation"),
                KeyValuePair.Create(QmlLintCategory.TypePropertiesDefault, "type-properties-default"),
                KeyValuePair.Create(QmlLintCategory.UnnecessaryDot, "unnecessary-dot"),
                KeyValuePair.Create(QmlLintCategory.UnusedValue, "unused-value"),
                KeyValuePair.Create(QmlLintCategory.VarUsedBeforeDeclaration, "var-used-before-declaration"),
                KeyValuePair.Create(QmlLintCategory.QdsExactlyOneTopLevelItem, "qds-exactly-one-top-level-item"),
                KeyValuePair.Create(QmlLintCategory.QdsNoInlineComponents, "qds-no-inline-components"),
                KeyValuePair.Create(QmlLintCategory.QdsNoListProperty, "qds-no-list-property"),
                KeyValuePair.Create(QmlLintCategory.QdsNoPropertyAccessFromOtherFile, "qds-no-property-access-from-other-file"),
                KeyValuePair.Create(QmlLintCategory.QdsNoReferenceOutside, "qds-no-reference-outside"),
                KeyValuePair.Create(QmlLintCategory.QdsNoUnsupportedType, "qds-no-unsupported-type"),
                KeyValuePair.Create(QmlLintCategory.AnchorCombinations, "anchor-combinations"),
                KeyValuePair.Create(QmlLintCategory.LayoutChildren, "layout-children"),
                KeyValuePair.Create(QmlLintCategory.MissingGridItemColumns, "missing-grid-item-columns"),
                KeyValuePair.Create(QmlLintCategory.MissingGridItemRows, "missing-grid-item-rows"),
                KeyValuePair.Create(QmlLintCategory.NegativeShiftAmount, "negative-shift-amount"),
                KeyValuePair.Create(QmlLintCategory.OverlappingAnchors, "overlapping-anchors"),
                KeyValuePair.Create(QmlLintCategory.PropertyChanges, "property-changes"),
                KeyValuePair.Create(QmlLintCategory.ShiftOverflow, "shift-overflow"),
            ];

            Assert.Equal(59, QmlLintCategoryExtensions.GetCliNames().Length);

            foreach (KeyValuePair<QmlLintCategory, string> pair in expected)
            {
                Assert.Equal(pair.Value, pair.Key.ToCliName());
                Assert.True(QmlLintCategoryExtensions.TryParseCliName(pair.Value, out QmlLintCategory parsed));
                Assert.Equal(pair.Key, parsed);
            }
        }

        [Fact]
        public void QmlLintCategory_UnknownFutureCliNameDoesNotParse()
        {
            Assert.False(QmlLintCategoryExtensions.TryParseCliName("future-category", out QmlLintCategory parsed));
            Assert.Equal(default, parsed);
        }
    }
}

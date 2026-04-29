namespace QmlSharp.Qt.Tools
{
    /// <summary>Converts qmllint warning categories to and from Qt CLI names.</summary>
    public static class QmlLintCategoryExtensions
    {
        private static readonly ImmutableArray<KeyValuePair<QmlLintCategory, string>> CategoryCliNames =
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

        private static readonly Lazy<ImmutableDictionary<string, QmlLintCategory>> CategoryByCliName =
            new(CreateCategoryByCliName);

        /// <summary>Convert a category enum value to the qmllint CLI category name.</summary>
        public static string ToCliName(this QmlLintCategory category)
        {
            foreach (KeyValuePair<QmlLintCategory, string> pair in CategoryCliNames)
            {
                if (pair.Key == category)
                {
                    return pair.Value;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown qmllint category.");
        }

        /// <summary>Try to convert a qmllint CLI category name back to an enum value.</summary>
        public static bool TryParseCliName(string? cliName, out QmlLintCategory category)
        {
            if (string.IsNullOrWhiteSpace(cliName))
            {
                category = default;
                return false;
            }

            return CategoryByCliName.Value.TryGetValue(cliName.Trim(), out category);
        }

        /// <summary>Gets all known Qt 6.11 qmllint CLI category names in enum order.</summary>
        public static ImmutableArray<string> GetCliNames()
        {
            ImmutableArray<string>.Builder builder = ImmutableArray.CreateBuilder<string>(CategoryCliNames.Length);

            foreach (KeyValuePair<QmlLintCategory, string> pair in CategoryCliNames)
            {
                builder.Add(pair.Value);
            }

            return builder.MoveToImmutable();
        }

        private static ImmutableDictionary<string, QmlLintCategory> CreateCategoryByCliName()
        {
            ImmutableDictionary<string, QmlLintCategory>.Builder builder =
                ImmutableDictionary.CreateBuilder<string, QmlLintCategory>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<QmlLintCategory, string> pair in CategoryCliNames)
            {
                builder[pair.Value] = pair.Key;
            }

            return builder.ToImmutable();
        }
    }
}

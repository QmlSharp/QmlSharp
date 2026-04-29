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
    }
}

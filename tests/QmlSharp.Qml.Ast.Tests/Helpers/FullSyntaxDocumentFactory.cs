namespace QmlSharp.Qml.Ast.Tests.Helpers
{
    internal static class FullSyntaxDocumentFactory
    {
        public static QmlDocument Create()
        {
            return new QmlDocument();
        }

        public static ImmutableArray<PragmaName> AllPragmas()
        {
            return [.. Enum.GetValues<PragmaName>()];
        }

        public static ImmutableArray<ImportKind> AllImportKinds()
        {
            return [.. Enum.GetValues<ImportKind>()];
        }

        public static ImmutableArray<SignalHandlerForm> AllSignalHandlerForms()
        {
            return [.. Enum.GetValues<SignalHandlerForm>()];
        }
    }
}

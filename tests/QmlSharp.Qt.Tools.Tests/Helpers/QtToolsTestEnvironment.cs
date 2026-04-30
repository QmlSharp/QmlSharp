namespace QmlSharp.Qt.Tools.Tests.Helpers
{
    internal static class QtToolsTestEnvironment
    {
        public const string QtDirVariableName = "QT_DIR";
        public const string PathVariableName = "PATH";
        public const string DefaultRequiredToolName = "qmlformat";
        public const string RequiresQtTraitName = "Category";

        public const string QtSdkUnavailableReason =
            "Qt SDK not available (set QT_DIR to a Qt 6.11 SDK root containing the required tools, or put the Qt bin directory on PATH; checked QT_DIR and PATH fallback).";
    }
}

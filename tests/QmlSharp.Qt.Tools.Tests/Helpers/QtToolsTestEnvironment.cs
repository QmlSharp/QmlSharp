namespace QmlSharp.Qt.Tools.Tests.Helpers
{
    internal static class QtToolsTestEnvironment
    {
        public const string QtDirVariableName = "QT_DIR";

        public const string QtSdkUnavailableReason =
            "Qt SDK not available (set QT_DIR to a Qt 6.11 SDK root or put Qt tools on PATH).";
    }
}

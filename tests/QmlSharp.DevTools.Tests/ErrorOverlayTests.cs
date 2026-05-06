namespace QmlSharp.DevTools.Tests
{
    public sealed class ErrorOverlayTests
    {
        [Fact]
        [Trait("TestId", "EOV-01")]
        public void Show_SingleError_CallsNativeShowError()
        {
            RecordingOverlayNativeHost nativeHost = new();
            ErrorOverlay overlay = new(nativeHost);

            overlay.Show(CreateError());

            OverlayHostCall call = Assert.Single(nativeHost.Calls);
            Assert.Equal(OverlayHostCallKind.Show, call.Kind);
            Assert.Equal("Compilation Error", call.Title);
            Assert.Equal("Unexpected token", call.Message);
            Assert.Equal("src/App.cs", call.FilePath);
            Assert.Equal(10, call.Line);
            Assert.Equal(5, call.Column);
        }

        [Fact]
        [Trait("TestId", "EOV-02")]
        public void Show_MultipleErrors_ShowsFirstAsPrimary()
        {
            RecordingOverlayNativeHost nativeHost = new();
            ErrorOverlay overlay = new(nativeHost);

            OverlayError primary = CreateError(message: "First error");
            OverlayError secondary = CreateError(filePath: "src/Other.cs", line: 2, column: 3, message: "Second error");

            overlay.Show(new[] { primary, secondary });

            OverlayHostCall call = Assert.Single(nativeHost.Calls);
            Assert.Equal("First error", call.Message);
            Assert.Equal("src/App.cs", call.FilePath);
            Assert.Equal(10, call.Line);
            Assert.Equal(5, call.Column);
        }

        [Fact]
        [Trait("TestId", "EOV-03")]
        public void Hide_WhenVisible_CallsNativeHideError()
        {
            RecordingOverlayNativeHost nativeHost = new();
            ErrorOverlay overlay = new(nativeHost);
            overlay.Show(CreateError());

            overlay.Hide();

            Assert.Equal(
                new[] { OverlayHostCallKind.Show, OverlayHostCallKind.Hide },
                nativeHost.Calls.Select(static call => call.Kind));
            Assert.False(overlay.IsVisible);
        }

        [Fact]
        [Trait("TestId", "EOV-04")]
        public void Hide_WhenNotVisible_NoOp()
        {
            RecordingOverlayNativeHost nativeHost = new();
            ErrorOverlay overlay = new(nativeHost);

            overlay.Hide();

            Assert.Empty(nativeHost.Calls);
            Assert.False(overlay.IsVisible);
        }

        [Fact]
        [Trait("TestId", "EOV-05")]
        public void Show_WhenAlreadyVisible_ReplacesOverlay()
        {
            RecordingOverlayNativeHost nativeHost = new();
            ErrorOverlay overlay = new(nativeHost);

            overlay.Show(CreateError(message: "First error"));
            overlay.Show(CreateError(filePath: "src/Replacement.cs", line: 20, column: 8, message: "Replacement error"));

            Assert.Equal(
                new[] { OverlayHostCallKind.Show, OverlayHostCallKind.Hide, OverlayHostCallKind.Show },
                nativeHost.Calls.Select(static call => call.Kind));
            OverlayHostCall replacementCall = nativeHost.Calls[2];
            Assert.Equal("Replacement error", replacementCall.Message);
            Assert.Equal("src/Replacement.cs", replacementCall.FilePath);
            Assert.Equal(20, replacementCall.Line);
            Assert.Equal(8, replacementCall.Column);
            Assert.True(overlay.IsVisible);
        }

        [Fact]
        [Trait("TestId", "EOV-06")]
        public void IsVisible_AfterShow_ReturnsTrue()
        {
            ErrorOverlay overlay = new(new RecordingOverlayNativeHost());

            overlay.Show(CreateError());

            Assert.True(overlay.IsVisible);
        }

        [Fact]
        [Trait("TestId", "EOV-07")]
        public void IsVisible_AfterHide_ReturnsFalse()
        {
            ErrorOverlay overlay = new(new RecordingOverlayNativeHost());
            overlay.Show(CreateError());

            overlay.Hide();

            Assert.False(overlay.IsVisible);
        }

        [Fact]
        [Trait("TestId", "EOV-08")]
        public void Show_NullFilePath_ShowsWithoutFileInfo()
        {
            RecordingOverlayNativeHost nativeHost = new();
            ErrorOverlay overlay = new(nativeHost);

            overlay.Show(CreateError(filePath: null, line: null, column: null));

            OverlayHostCall call = Assert.Single(nativeHost.Calls);
            Assert.Equal(OverlayHostCallKind.Show, call.Kind);
            Assert.Null(call.FilePath);
            Assert.Equal(0, call.Line);
            Assert.Equal(0, call.Column);
        }

        [Fact]
        public void Constructor_NullNativeHost_Throws()
        {
            _ = Assert.Throws<ArgumentNullException>(() => new ErrorOverlay(null!));
        }

        [Fact]
        public void Show_NullError_Throws()
        {
            ErrorOverlay overlay = new(new RecordingOverlayNativeHost());

            _ = Assert.Throws<ArgumentNullException>(() => overlay.Show((OverlayError)null!));
        }

        [Fact]
        public void Show_EmptyErrorList_Throws()
        {
            ErrorOverlay overlay = new(new RecordingOverlayNativeHost());

            _ = Assert.Throws<ArgumentException>(() => overlay.Show(Array.Empty<OverlayError>()));
        }

        [Fact]
        public void Show_NullErrorList_Throws()
        {
            ErrorOverlay overlay = new(new RecordingOverlayNativeHost());

            _ = Assert.Throws<ArgumentNullException>(() => overlay.Show((IReadOnlyList<OverlayError>)null!));
        }

        [Fact]
        public void Show_BlankTitle_ThrowsWithoutNativeCall()
        {
            RecordingOverlayNativeHost nativeHost = new();
            ErrorOverlay overlay = new(nativeHost);
            OverlayError error = CreateError() with { Title = " " };

            _ = Assert.Throws<ArgumentException>(() => overlay.Show(error));

            Assert.Empty(nativeHost.Calls);
            Assert.False(overlay.IsVisible);
        }

        [Fact]
        public void Show_BlankMessage_ThrowsWithoutNativeCall()
        {
            RecordingOverlayNativeHost nativeHost = new();
            ErrorOverlay overlay = new(nativeHost);
            OverlayError error = CreateError(message: " ");

            _ = Assert.Throws<ArgumentException>(() => overlay.Show(error));

            Assert.Empty(nativeHost.Calls);
            Assert.False(overlay.IsVisible);
        }

        [Fact]
        public void Show_NonPositiveSourcePosition_MapsToUnknownNativePosition()
        {
            RecordingOverlayNativeHost nativeHost = new();
            ErrorOverlay overlay = new(nativeHost);

            overlay.Show(CreateError(line: 0, column: -3));

            OverlayHostCall call = Assert.Single(nativeHost.Calls);
            Assert.Equal(0, call.Line);
            Assert.Equal(0, call.Column);
        }

        [Fact]
        public void MapDiagnostics_ErrorDiagnostic_MapsCompilerFieldsToOverlayError()
        {
            CompilerDiagnostic diagnostic = new(
                DiagnosticCodes.InvalidStateAttribute,
                DiagnosticSeverity.Error,
                "Type mismatch",
                new SourceLocation("src/Counter.cs", 42, 8));

            ImmutableArray<OverlayError> errors = ErrorOverlayDiagnosticMapper.MapDiagnostics(new[] { diagnostic });

            OverlayError error = Assert.Single(errors);
            Assert.Equal("Compilation Error", error.Title);
            Assert.Equal("QMLSHARP-A001: Type mismatch", error.Message);
            Assert.Equal("src/Counter.cs", error.FilePath);
            Assert.Equal(42, error.Line);
            Assert.Equal(8, error.Column);
            Assert.Equal(OverlaySeverity.Error, error.Severity);
        }

        [Fact]
        public void MapDiagnostics_WarningDiagnostic_MapsWarningSeverity()
        {
            CompilerDiagnostic diagnostic = new(
                DiagnosticCodes.QtValidationFailed,
                DiagnosticSeverity.Warning,
                "Unused import",
                SourceLocation.FileOnly("src/App.cs"));

            ImmutableArray<OverlayError> errors = ErrorOverlayDiagnosticMapper.MapDiagnostics(new[] { diagnostic });

            OverlayError error = Assert.Single(errors);
            Assert.Equal("Compilation Warning", error.Title);
            Assert.Equal("QMLSHARP-C005: Unused import", error.Message);
            Assert.Equal("src/App.cs", error.FilePath);
            Assert.Null(error.Line);
            Assert.Null(error.Column);
            Assert.Equal(OverlaySeverity.Warning, error.Severity);
        }

        [Fact]
        public void MapDiagnostics_FatalDiagnostic_MapsAsErrorOverlay()
        {
            CompilerDiagnostic diagnostic = new(
                DiagnosticCodes.InternalError,
                DiagnosticSeverity.Fatal,
                "Compiler crashed");

            ImmutableArray<OverlayError> errors = ErrorOverlayDiagnosticMapper.MapDiagnostics(new[] { diagnostic });

            OverlayError error = Assert.Single(errors);
            Assert.Equal("Compilation Error", error.Title);
            Assert.Equal("QMLSHARP-G001: Compiler crashed", error.Message);
            Assert.Equal(OverlaySeverity.Error, error.Severity);
        }

        [Fact]
        public void MapDiagnostics_InfoDiagnostic_IsNotDisplayed()
        {
            CompilerDiagnostic diagnostic = new(
                DiagnosticCodes.ProjectLoadFailed,
                DiagnosticSeverity.Info,
                "Project loaded");

            ImmutableArray<OverlayError> errors = ErrorOverlayDiagnosticMapper.MapDiagnostics(new[] { diagnostic });

            Assert.Empty(errors);
        }

        [Fact]
        public void MapDiagnostics_NullDiagnostics_Throws()
        {
            _ = Assert.Throws<ArgumentNullException>(() => ErrorOverlayDiagnosticMapper.MapDiagnostics(null!));
        }

        [Fact]
        public void ShowDiagnostics_EmptyDiagnostics_ThrowsWithoutNativeCall()
        {
            RecordingOverlayNativeHost nativeHost = new();
            ErrorOverlay overlay = new(nativeHost);

            _ = Assert.Throws<ArgumentException>(() => overlay.ShowDiagnostics(Array.Empty<CompilerDiagnostic>()));

            Assert.Empty(nativeHost.Calls);
            Assert.False(overlay.IsVisible);
        }

        [Fact]
        public void ShowDiagnostics_AllInfoDiagnostics_ThrowsWithoutNativeCall()
        {
            RecordingOverlayNativeHost nativeHost = new();
            ErrorOverlay overlay = new(nativeHost);
            CompilerDiagnostic diagnostic = new(
                DiagnosticCodes.ProjectLoadFailed,
                DiagnosticSeverity.Info,
                "Project loaded");

            _ = Assert.Throws<ArgumentException>(() => overlay.ShowDiagnostics(new[] { diagnostic }));

            Assert.Empty(nativeHost.Calls);
            Assert.False(overlay.IsVisible);
        }

        [Fact]
        public void ShowDiagnostics_NullDiagnostics_Throws()
        {
            ErrorOverlay overlay = new(new RecordingOverlayNativeHost());

            _ = Assert.Throws<ArgumentNullException>(() => overlay.ShowDiagnostics(null!));
        }

        [Fact]
        public void ShowDiagnostics_PartialLocation_AllowsNullFilePath()
        {
            RecordingOverlayNativeHost nativeHost = new();
            ErrorOverlay overlay = new(nativeHost);
            CompilerDiagnostic diagnostic = new(
                DiagnosticCodes.RoslynCompilationFailed,
                DiagnosticSeverity.Error,
                "Missing semicolon",
                SourceLocation.LineColumn(7, 3));

            overlay.ShowDiagnostics(new[] { diagnostic });

            OverlayHostCall call = Assert.Single(nativeHost.Calls);
            Assert.Null(call.FilePath);
            Assert.Equal(7, call.Line);
            Assert.Equal(3, call.Column);
        }

        [Fact]
        public void ApplyCompilationResult_Success_HidesVisibleOverlay()
        {
            RecordingOverlayNativeHost nativeHost = new();
            ErrorOverlay overlay = new(nativeHost);
            overlay.Show(CreateError());

            overlay.ApplyCompilationResult(new CompilationResult());

            Assert.Equal(
                new[] { OverlayHostCallKind.Show, OverlayHostCallKind.Hide },
                nativeHost.Calls.Select(static call => call.Kind));
            Assert.False(overlay.IsVisible);
        }

        [Fact]
        public void ApplyCompilationResult_Failure_ShowsDiagnosticOverlay()
        {
            RecordingOverlayNativeHost nativeHost = new();
            ErrorOverlay overlay = new(nativeHost);
            CompilerDiagnostic diagnostic = new(
                DiagnosticCodes.InvalidCommandAttribute,
                DiagnosticSeverity.Error,
                "Command must be instance member",
                new SourceLocation("src/Commands.cs", 12, 4));
            CompilationResult result = CompilationResult.FromUnits(
                ImmutableArray<CompilationUnit>.Empty,
                ImmutableArray.Create(diagnostic));

            overlay.ApplyCompilationResult(result);

            OverlayHostCall call = Assert.Single(nativeHost.Calls);
            Assert.Equal(OverlayHostCallKind.Show, call.Kind);
            Assert.Equal("Compilation Error", call.Title);
            Assert.Equal("QMLSHARP-A002: Command must be instance member", call.Message);
            Assert.Equal("src/Commands.cs", call.FilePath);
            Assert.Equal(12, call.Line);
            Assert.Equal(4, call.Column);
            Assert.True(overlay.IsVisible);
        }

        [Fact]
        public void ApplyCompilationResult_NullResult_Throws()
        {
            ErrorOverlay overlay = new(new RecordingOverlayNativeHost());

            _ = Assert.Throws<ArgumentNullException>(() => overlay.ApplyCompilationResult(null!));
        }

        [Fact]
        public void ApplyCompilationResult_FailureWithOnlyInfoDiagnostics_ShowsGenericFallback()
        {
            RecordingOverlayNativeHost nativeHost = new();
            ErrorOverlay overlay = new(nativeHost);
            CompilerDiagnostic diagnostic = new(
                DiagnosticCodes.ProjectLoadFailed,
                DiagnosticSeverity.Info,
                "Project loaded");
            CompilationResult result = new()
            {
                Units = ImmutableArray.Create(CreateFailedCompilationUnit()),
                Diagnostics = ImmutableArray.Create(diagnostic),
            };

            overlay.ApplyCompilationResult(result);

            OverlayHostCall call = Assert.Single(nativeHost.Calls);
            Assert.Equal("Compilation Error", call.Title);
            Assert.Equal("Compilation failed.", call.Message);
            Assert.Null(call.FilePath);
            Assert.Equal(0, call.Line);
            Assert.Equal(0, call.Column);
        }

        [Fact]
        public void ApplyHotReloadResult_Success_HidesVisibleOverlay()
        {
            RecordingOverlayNativeHost nativeHost = new();
            ErrorOverlay overlay = new(nativeHost);
            overlay.Show(CreateError());

            overlay.ApplyHotReloadResult(CreateHotReloadResult(success: true, errorMessage: null));

            Assert.Equal(
                new[] { OverlayHostCallKind.Show, OverlayHostCallKind.Hide },
                nativeHost.Calls.Select(static call => call.Kind));
            Assert.False(overlay.IsVisible);
        }

        [Fact]
        public void ApplyHotReloadResult_Failure_ShowsHotReloadOverlay()
        {
            RecordingOverlayNativeHost nativeHost = new();
            ErrorOverlay overlay = new(nativeHost);

            overlay.ApplyHotReloadResult(CreateHotReloadResult(success: false, errorMessage: "Reload failed during hydrate"));

            OverlayHostCall call = Assert.Single(nativeHost.Calls);
            Assert.Equal(OverlayHostCallKind.Show, call.Kind);
            Assert.Equal("Hot Reload Error", call.Title);
            Assert.Equal("Reload failed during hydrate", call.Message);
            Assert.Null(call.FilePath);
            Assert.Equal(0, call.Line);
            Assert.Equal(0, call.Column);
            Assert.True(overlay.IsVisible);
        }

        [Fact]
        public void ApplyHotReloadResult_NullResult_Throws()
        {
            ErrorOverlay overlay = new(new RecordingOverlayNativeHost());

            _ = Assert.Throws<ArgumentNullException>(() => overlay.ApplyHotReloadResult(null!));
        }

        [Fact]
        public void ApplyHotReloadResult_FailureWithBlankMessage_ShowsGenericFallback()
        {
            RecordingOverlayNativeHost nativeHost = new();
            ErrorOverlay overlay = new(nativeHost);

            overlay.ApplyHotReloadResult(CreateHotReloadResult(success: false, errorMessage: " "));

            OverlayHostCall call = Assert.Single(nativeHost.Calls);
            Assert.Equal("Hot Reload Error", call.Title);
            Assert.Equal("Hot reload failed.", call.Message);
            Assert.True(overlay.IsVisible);
        }

        private static OverlayError CreateError(
            string? filePath = "src/App.cs",
            int? line = 10,
            int? column = 5,
            string message = "Unexpected token")
        {
            return new OverlayError("Compilation Error", message, filePath, line, column);
        }

        private static HotReloadResult CreateHotReloadResult(bool success, string? errorMessage)
        {
            return new HotReloadResult(
                success,
                InstancesMatched: success ? 1 : 0,
                InstancesOrphaned: 0,
                InstancesNew: 0,
                new HotReloadPhases(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero),
                TimeSpan.Zero,
                errorMessage,
                success ? null : HotReloadStep.Hydrate);
        }

        private static CompilationUnit CreateFailedCompilationUnit()
        {
            CompilerDiagnostic diagnostic = new(
                DiagnosticCodes.InvalidStateAttribute,
                DiagnosticSeverity.Error,
                "Type mismatch");
            return new CompilationUnit
            {
                SourceFilePath = "src/Broken.cs",
                ViewClassName = "BrokenView",
                ViewModelClassName = "BrokenViewModel",
                Diagnostics = ImmutableArray.Create(diagnostic),
            };
        }

        private sealed class RecordingOverlayNativeHost : IErrorOverlayNativeHost
        {
            public List<OverlayHostCall> Calls { get; } = new();

            public void ShowError(string title, string message, string? filePath, int line, int column)
            {
                Calls.Add(new OverlayHostCall(OverlayHostCallKind.Show, title, message, filePath, line, column));
            }

            public void HideError()
            {
                Calls.Add(new OverlayHostCall(OverlayHostCallKind.Hide, null, null, null, 0, 0));
            }
        }

        private sealed record OverlayHostCall(
            OverlayHostCallKind Kind,
            string? Title,
            string? Message,
            string? FilePath,
            int Line,
            int Column);

        private enum OverlayHostCallKind
        {
            Show,
            Hide,
        }
    }
}

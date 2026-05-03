using QmlSharp.Host.ArtifactValidation;
using QmlSharp.Host.Engine;

namespace QmlSharp.Host.Tests.ArtifactValidation
{
    public sealed class QmlSharpEngineArtifactValidationTests
    {
        [Fact]
        public void EnsureStartupArtifactsValid_WarningsOnly_DoesNotThrow()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            File.Delete(fixture.InDist("event-bindings.json"));
            QmlSharpEngine engine = new(new ArtifactValidator(new FakeAbiVersionReader()));

            ArtifactValidationResult result = engine.EnsureStartupArtifactsValid(fixture.Path);

            Assert.True(result.IsValid);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Severity == ArtifactDiagnosticSeverity.Warning);
        }

        [Fact]
        public void EnsureStartupArtifactsValid_Error_ThrowsBeforeStartup()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            File.Delete(fixture.InDist("manifest.json"));
            QmlSharpEngine engine = new(new ArtifactValidator(new FakeAbiVersionReader()));

            ArtifactValidationException exception = Assert.Throws<ArtifactValidationException>(
                () => engine.EnsureStartupArtifactsValid(fixture.Path));

            Assert.False(exception.Result.IsValid);
            Assert.Contains(exception.Result.Diagnostics, diagnostic => diagnostic.Code == ArtifactValidationCodes.ManifestMissing);
        }
    }
}

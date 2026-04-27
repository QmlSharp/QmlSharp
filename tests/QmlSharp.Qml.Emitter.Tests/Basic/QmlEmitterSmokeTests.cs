using QmlSharp.Qml.Emitter.Tests.Helpers;

namespace QmlSharp.Qml.Emitter.Tests.Basic
{
    public sealed class QmlEmitterSmokeTests
    {
        [Fact]
        [Trait("Category", TestCategories.Smoke)]
        public void Emitter_CanBeInstantiated_AsPublicContract()
        {
            IQmlEmitter emitter = new QmlEmitter();

            QmlEmitter typedEmitter = Assert.IsType<QmlEmitter>(emitter);

            Assert.Same(emitter, typedEmitter);
        }

        [Fact]
        [Trait("Category", TestCategories.Smoke)]
        public void Emitter_MinimalDocument_CurrentlyThrowsDocumentedUnsupportedException()
        {
            IQmlEmitter emitter = new QmlEmitter();
            QmlDocument document = AstFixtureFactory.MinimalDocument();

            NotSupportedException exception = Assert.Throws<NotSupportedException>(() => emitter.Emit(document));

            Assert.Contains("later 03-qml-emitter steps", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void Emitter_NullInputs_ThrowArgumentNullExceptionBeforeStub()
        {
            IQmlEmitter emitter = new QmlEmitter();

            ArgumentNullException emitException = Assert.Throws<ArgumentNullException>(
                () => emitter.Emit(null!));
            ArgumentNullException fragmentException = Assert.Throws<ArgumentNullException>(
                () => emitter.EmitFragment(null!));
            ArgumentNullException sourceMapException = Assert.Throws<ArgumentNullException>(
                () => emitter.EmitWithSourceMap(null!));

            Assert.Equal("document", emitException.ParamName);
            Assert.Equal("node", fragmentException.ParamName);
            Assert.Equal("document", sourceMapException.ParamName);
        }
    }
}

using QmlSharp.Host.Exceptions;
using QmlSharp.Host.Interop;
using QmlSharp.Host.Tests.StateSynchronization;

namespace QmlSharp.Host.Tests.Interop
{
    public sealed class NativeErrorMapperTests
    {
        [Theory]
        [InlineData(-1, typeof(NativeHostException))]
        [InlineData(-2, typeof(InvalidNativeArgumentException))]
        [InlineData(-3, typeof(InstanceNotFoundException))]
        [InlineData(-4, typeof(EngineNotInitializedException))]
        [InlineData(-5, typeof(QmlLoadException))]
        [InlineData(-6, typeof(TypeRegistrationException))]
        [InlineData(-7, typeof(PropertyNotFoundException))]
        [InlineData(-8, typeof(NativeJsonException))]
        public void Create_DocumentedNativeErrorCode_MapsToApprovedManagedException(int resultCode, Type expectedType)
        {
            NativeHostException exception = NativeErrorMapper.Create(
                resultCode,
                "native failure",
                instanceId: "11111111-1111-4111-8111-111111111111",
                qmlPath: "Main.qml");

            Assert.IsType(expectedType, exception);
            Assert.Equal(resultCode, exception.ErrorCode);
        }

        [Fact]
        public void Create_InvalidArgumentCode_PreservesLegacyCatchCompatibility()
        {
            NativeHostException exception = NativeErrorMapper.Create(-2, "native failure");

            _ = Assert.IsType<InvalidNativeArgumentException>(exception);
            _ = Assert.IsAssignableFrom<NativeInvalidArgumentException>(exception);
        }

        [Fact]
        public void ThrowIfFailed_UsesNativeLastErrorDetail()
        {
            FakeNativeHostInterop interop = new()
            {
                LastError = "registration callback failed"
            };

            TypeRegistrationException exception = Assert.Throws<TypeRegistrationException>(() =>
                NativeErrorMapper.ThrowIfFailed(-6, interop, "qmlsharp_register_type"));

            Assert.Contains("registration callback failed", exception.Message, StringComparison.Ordinal);
        }
    }
}

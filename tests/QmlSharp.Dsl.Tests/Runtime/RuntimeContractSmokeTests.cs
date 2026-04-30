using System.Runtime.InteropServices;
using QmlSharp.Core;
using QmlSharp.Dsl.Tests.Fixtures;

namespace QmlSharp.Dsl.Tests.Runtime
{
    public sealed class RuntimeContractSmokeTests
    {
        [Fact]
        [Trait("Category", TestCategories.Smoke)]
        public void QmlValueContracts_ConstructExpectedValues()
        {
            QmlColor color = "red";
            QmlPoint point = new(1, 2);
            QmlSize size = new(100, 50);
            QmlRect rect = new(1, 2, 3, 4);
            Vector4 vector = new(1, 2, 3, 4);

            Assert.Equal("red", color.StringValue);
            Assert.Equal(1, point.X);
            Assert.Equal(50, size.Height);
            Assert.Equal(4, rect.Height);
            Assert.Equal(3, vector.Z);
        }

        [Fact]
        [Trait("Category", TestCategories.Smoke)]
        public void QmlColor_IsManagedValueContract_NotNativeLayoutContract()
        {
            Assert.Empty(typeof(QmlColor).GetCustomAttributes(typeof(StructLayoutAttribute), inherit: false));
        }
    }
}

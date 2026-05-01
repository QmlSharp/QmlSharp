using System.Runtime.InteropServices;
using QmlSharp.Core;
using QmlSharp.Dsl.Tests.Fixtures;
using QmlSharp.Qml.Ast;

namespace QmlSharp.Dsl.Tests.Values
{
    public sealed class ValueTypeTests
    {
        [Fact]
        [Trait("Category", TestCategories.Values)]
        public void QmlColor_FromString_StoresStringValue()
        {
            QmlColor color = "red";

            Assert.Equal("red", color.StringValue);
            Assert.Null(color.R);
            Assert.Empty(typeof(QmlColor).GetCustomAttributes(typeof(StructLayoutAttribute), inherit: false));
        }

        [Fact]
        [Trait("Category", TestCategories.Values)]
        public void CommonGeometryTypes_ExposeExpectedComponents()
        {
            QmlPoint point = new(1, 2);
            QmlSize size = new(100, 50);
            QmlRect rect = new(1, 2, 3, 4);

            Assert.Equal(1, point.X);
            Assert.Equal(50, size.Height);
            Assert.Equal(4, rect.Height);
        }

        [Fact]
        [Trait("Category", TestCategories.Values)]
        public void VectorQuaternionAndMatrixTypes_ExposeExpectedComponents()
        {
            Vector2 vector2 = new(1, 2);
            Vector3 vector3 = new(1, 2, 3);
            Vector4 vector4 = new(1, 2, 3, 4);
            Quaternion quaternion = new(5, 6, 7, 8);
            Matrix4x4 matrix = new(
                1,
                2,
                3,
                4,
                5,
                6,
                7,
                8,
                9,
                10,
                11,
                12,
                13,
                14,
                15,
                16);

            Assert.Equal(2, vector2.Y);
            Assert.Equal(3, vector3.Z);
            Assert.Equal(4, vector4.W);
            Assert.Equal(5, quaternion.Scalar);
            Assert.Equal(16, matrix.M44);
        }

        [Fact]
        [Trait("Category", TestCategories.Values)]
        public void QmlFont_StoresOptionalProperties()
        {
            QmlFont font = new()
            {
                Family = "Arial",
                PointSize = 12,
                Bold = true,
            };

            Assert.Equal("Arial", font.Family);
            Assert.Equal(12, font.PointSize);
            Assert.True(font.Bold);
            Assert.Null(font.PixelSize);
        }

        [Fact]
        [Trait("Category", TestCategories.Values)]
        public void Builder_LowersValueTypesToDeterministicAstExpressions()
        {
            ObjectDefinitionNode node = ObjectFactory.Create("Item")
                .SetProperty("position", new QmlPoint(1, 2))
                .SetProperty("size", new QmlSize(100, 50))
                .SetProperty("font", new QmlFont { Family = "Arial", PixelSize = 14, Bold = true })
                .Build();

            AssertExpression(node.Members[0], "Qt.point(1, 2)");
            AssertExpression(node.Members[1], "Qt.size(100, 50)");
            AssertExpression(node.Members[2], "Qt.font({family: \"Arial\", pixelSize: 14, bold: true})");
        }

        [Fact]
        [Trait("Category", TestCategories.Values)]
        public void Builder_LowersEnumTokenToEnumReference()
        {
            ObjectDefinitionNode node = ObjectFactory.Create("Text")
                .SetProperty("elide", QmlEnum.Create("Text", "ElideMode", "ElideRight"))
                .Build();

            BindingNode binding = Assert.IsType<BindingNode>(Assert.Single(node.Members));
            EnumReference enumReference = Assert.IsType<EnumReference>(binding.Value);
            Assert.Equal("Text", enumReference.TypeName);
            Assert.Equal("ElideRight", enumReference.MemberName);
        }

        private static void AssertExpression(AstNode member, string expected)
        {
            BindingNode binding = Assert.IsType<BindingNode>(member);
            ScriptExpression expression = Assert.IsType<ScriptExpression>(binding.Value);
            Assert.Equal(expected, expression.Code);
        }
    }
}

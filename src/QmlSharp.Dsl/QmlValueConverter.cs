using System.Collections;
using System.Collections.Immutable;
using System.Globalization;
using QmlSharp.Core;
using QmlSharp.Qml.Ast;

namespace QmlSharp.Dsl
{
    internal static class QmlValueConverter
    {
        public static BindingValue ToBindingValue(object? value)
        {
            return value switch
            {
                null => Values.Null(),
                BindingValue bindingValue => bindingValue,
                QmlEnumToken token => token.ToBindingValue(),
                IObjectBuilder builder => Values.Object(builder.Build()),
                ObjectDefinitionNode node => Values.Object(node),
                string text => Values.String(text),
                bool flag => Values.Boolean(flag),
                byte number => Values.Number(number),
                sbyte number => Values.Number(number),
                short number => Values.Number(number),
                ushort number => Values.Number(number),
                int number => Values.Number(number),
                uint number => Values.Number(number),
                long number => Values.Number(number),
                ulong number => Values.Number(number),
                float number => Values.Number(number),
                double number => Values.Number(number),
                decimal number => Values.Number((double)number),
                QmlColor color => ConvertColor(color),
                QmlPoint point => Values.Expression(FormatFunction("Qt.point", point.X, point.Y)),
                QmlSize size => Values.Expression(FormatFunction("Qt.size", size.Width, size.Height)),
                QmlRect rect => Values.Expression(FormatFunction("Qt.rect", rect.X, rect.Y, rect.Width, rect.Height)),
                Vector2 vector => Values.Expression(FormatFunction("Qt.vector2d", vector.X, vector.Y)),
                Vector3 vector => Values.Expression(FormatFunction("Qt.vector3d", vector.X, vector.Y, vector.Z)),
                Vector4 vector => Values.Expression(FormatFunction("Qt.vector4d", vector.X, vector.Y, vector.Z, vector.W)),
                Quaternion quaternion => Values.Expression(FormatFunction("Qt.quaternion", quaternion.Scalar, quaternion.X, quaternion.Y, quaternion.Z)),
                Matrix4x4 matrix => Values.Expression(FormatFunction(
                    "Qt.matrix4x4",
                    matrix.M11,
                    matrix.M12,
                    matrix.M13,
                    matrix.M14,
                    matrix.M21,
                    matrix.M22,
                    matrix.M23,
                    matrix.M24,
                    matrix.M31,
                    matrix.M32,
                    matrix.M33,
                    matrix.M34,
                    matrix.M41,
                    matrix.M42,
                    matrix.M43,
                    matrix.M44)),
                QmlFont font => Values.Expression(FormatFont(font)),
                Enum enumValue => Values.Enum(enumValue.GetType().Name, enumValue.ToString()),
                IEnumerable values when value is not string => ConvertArray(values),
                _ => Values.Expression(System.Text.Json.JsonSerializer.Serialize(value)),
            };
        }

        private static BindingValue ConvertColor(QmlColor color)
        {
            if (color.StringValue is not null)
            {
                return Values.String(color.StringValue);
            }

            if (color.R.HasValue && color.G.HasValue && color.B.HasValue)
            {
                byte alpha = color.A ?? byte.MaxValue;
                return Values.String(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"#{color.R.Value:X2}{color.G.Value:X2}{color.B.Value:X2}{alpha:X2}"));
            }

            return Values.Null();
        }

        private static BindingValue ConvertArray(IEnumerable values)
        {
            ImmutableArray<BindingValue>.Builder elements = ImmutableArray.CreateBuilder<BindingValue>();
            foreach (object? element in values)
            {
                elements.Add(ToBindingValue(element));
            }

            return Values.Array(elements.ToImmutable());
        }

        private static string FormatFunction(string functionName, params double[] values)
        {
            string[] formatted = new string[values.Length];
            for (int index = 0; index < values.Length; index++)
            {
                formatted[index] = values[index].ToString("G17", CultureInfo.InvariantCulture);
            }

            return string.Create(CultureInfo.InvariantCulture, $"{functionName}({string.Join(", ", formatted)})");
        }

        private static string FormatFont(QmlFont font)
        {
            List<string> fields = [];
            AddString(fields, "family", font.Family);
            AddNumber(fields, "pointSize", font.PointSize);
            AddNumber(fields, "pixelSize", font.PixelSize);
            AddNumber(fields, "weight", font.Weight);
            AddBoolean(fields, "bold", font.Bold);
            AddBoolean(fields, "italic", font.Italic);
            AddBoolean(fields, "underline", font.Underline);
            AddBoolean(fields, "strikeout", font.Strikeout);
            return string.Create(CultureInfo.InvariantCulture, $"Qt.font({{{string.Join(", ", fields)}}})");
        }

        private static void AddString(List<string> fields, string name, string? value)
        {
            if (value is null)
            {
                return;
            }

            string escaped = System.Text.Json.JsonSerializer.Serialize(value);
            fields.Add(string.Create(CultureInfo.InvariantCulture, $"{name}: {escaped}"));
        }

        private static void AddNumber(List<string> fields, string name, double? value)
        {
            if (value.HasValue)
            {
                fields.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{name}: {value.Value.ToString("G17", CultureInfo.InvariantCulture)}"));
            }
        }

        private static void AddNumber(List<string> fields, string name, int? value)
        {
            if (value.HasValue)
            {
                fields.Add(string.Create(CultureInfo.InvariantCulture, $"{name}: {value.Value.ToString(CultureInfo.InvariantCulture)}"));
            }
        }

        private static void AddBoolean(List<string> fields, string name, bool? value)
        {
            if (value.HasValue)
            {
                fields.Add(string.Create(CultureInfo.InvariantCulture, $"{name}: {value.Value.ToString().ToLowerInvariant()}"));
            }
        }
    }
}

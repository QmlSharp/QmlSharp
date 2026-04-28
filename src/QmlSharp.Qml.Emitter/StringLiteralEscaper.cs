using System.Globalization;
using System.Text;

namespace QmlSharp.Qml.Emitter
{
    internal static class StringLiteralEscaper
    {
        internal static string Escape(string value, char quoteChar)
        {
            ArgumentNullException.ThrowIfNull(value);

            StringBuilder builder = new(value.Length);
            foreach (char character in value)
            {
                AppendEscaped(builder, character, quoteChar);
            }

            return builder.ToString();
        }

        private static void AppendEscaped(StringBuilder builder, char character, char quoteChar)
        {
            switch (character)
            {
                case '\\':
                    builder.Append(@"\\");
                    break;
                case '\r':
                    builder.Append(@"\r");
                    break;
                case '\n':
                    builder.Append(@"\n");
                    break;
                case '\t':
                    builder.Append(@"\t");
                    break;
                case '\b':
                    builder.Append(@"\b");
                    break;
                case '\f':
                    builder.Append(@"\f");
                    break;
                default:
                    if (character == quoteChar)
                    {
                        builder.Append('\\');
                        builder.Append(character);
                    }
                    else if (char.IsControl(character))
                    {
                        builder.Append(CultureInfo.InvariantCulture, $"\\u{(int)character:X4}");
                    }
                    else
                    {
                        builder.Append(character);
                    }

                    break;
            }
        }
    }
}

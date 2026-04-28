using System.Globalization;
using QmlSharp.Qml.Emitter.Tests.Helpers;

namespace QmlSharp.Qml.Emitter.Tests.Options
{
    public sealed class QmlWriterTests
    {
        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void Writer_IndentAndDedent_WriteConfiguredIndentation()
        {
            ResolvedEmitOptions options = ResolvedEmitOptions.From(new EmitOptions { IndentSize = 2 });
            QmlWriter writer = new(options);

            writer.WriteLine("Item {");
            writer.Indent();
            writer.WriteLine("width: 100");
            writer.Dedent();
            writer.WriteLine("}");

            Assert.Equal("Item {\n  width: 100\n}\n", writer.GetOutput());
            Assert.Equal(4, writer.Line);
            Assert.Equal(1, writer.Column);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void Writer_NewlineStyleCrLf_WritesConfiguredNewline()
        {
            ResolvedEmitOptions options = ResolvedEmitOptions.From(new EmitOptions { Newline = NewlineStyle.CrLf });
            QmlWriter writer = new(options);

            writer.WriteLine("Item {}");
            writer.WriteLine("Text {}");

            Assert.Equal("Item {}\r\nText {}\r\n", writer.GetOutput());
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void Writer_WriteBlankLine_WritesTwoConfiguredNewlines()
        {
            ResolvedEmitOptions options = ResolvedEmitOptions.From(null);
            QmlWriter writer = new(options);

            writer.Write("a");
            writer.WriteBlankLine();
            writer.Write("b");

            Assert.Equal("a\n\nb", writer.GetOutput());
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void Writer_WriteRawText_DoesNotInsertIndentation()
        {
            ResolvedEmitOptions options = ResolvedEmitOptions.From(new EmitOptions { IndentSize = 2 });
            QmlWriter writer = new(options);

            writer.Indent();
            writer.Write("raw");

            Assert.Equal("raw", writer.GetOutput());
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void Writer_DedentBelowZero_ThrowsInvalidOperationException()
        {
            ResolvedEmitOptions options = ResolvedEmitOptions.From(null);
            QmlWriter writer = new(options);

            _ = Assert.Throws<InvalidOperationException>(writer.Dedent);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void Writer_GetSpanFrom_ReturnsStableOneBasedOutputSpan()
        {
            ResolvedEmitOptions options = ResolvedEmitOptions.From(null);
            QmlWriter writer = new(options);

            (int Line, int Column) start = writer.GetPosition();
            writer.Write("abc");

            OutputSpan span = writer.GetSpanFrom(start);

            Assert.Equal(1, span.StartLine);
            Assert.Equal(1, span.StartColumn);
            Assert.Equal(1, span.EndLine);
            Assert.Equal(3, span.EndColumn);
        }

        [Theory]
        [InlineData(QuoteStyle.Double, "say \"hi\"\nnext", "\"say \\\"hi\\\"\\nnext\"")]
        [InlineData(QuoteStyle.Single, "it's ok\\done", "'it\\'s ok\\\\done'")]
        [Trait("Category", TestCategories.Contract)]
        public void Writer_StringLiteralEscaping_UsesConfiguredQuoteStyle(
            QuoteStyle quoteStyle,
            string value,
            string expected)
        {
            ResolvedEmitOptions options = ResolvedEmitOptions.From(new EmitOptions { QuoteStyle = quoteStyle });

            Assert.Equal(expected, options.QuoteStringLiteral(value));
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void Writer_InvariantNumberFormatting_IgnoresCurrentCulture()
        {
            CultureInfo originalCulture = CultureInfo.CurrentCulture;
            CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;

            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");

                Assert.Equal("1234.5", QmlValueFormatter.FormatNumber(1234.5));
                Assert.Equal("42", QmlValueFormatter.FormatNumber(42));
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
                CultureInfo.CurrentUICulture = originalUiCulture;
            }
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void Writer_NonFiniteNumberFormatting_ThrowsArgumentOutOfRangeException()
        {
            _ = Assert.Throws<ArgumentOutOfRangeException>(() => QmlValueFormatter.FormatNumber(double.NaN));
            _ = Assert.Throws<ArgumentOutOfRangeException>(() => QmlValueFormatter.FormatNumber(double.PositiveInfinity));
        }
    }
}

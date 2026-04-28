using System.Globalization;
using System.Text;

namespace QmlSharp.Qml.Emitter
{
    /// <summary>
    /// Internal writer that owns indentation, newline, and output-position tracking.
    /// </summary>
    internal sealed class QmlWriter
    {
        private readonly StringBuilder _builder = new();
        private readonly ResolvedEmitOptions _options;
        private int _indentLevel;

        internal QmlWriter(ResolvedEmitOptions options, int initialIndentLevel = 0)
        {
            ArgumentNullException.ThrowIfNull(options);

            if (initialIndentLevel < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialIndentLevel), initialIndentLevel, "Initial indentation level cannot be negative.");
            }

            _options = options;
            _indentLevel = initialIndentLevel;
        }

        internal int Line { get; private set; } = 1;

        internal int Column { get; private set; } = 1;

        internal int IndentLevel => _indentLevel;

        internal void Write(string text)
        {
            ArgumentNullException.ThrowIfNull(text);

            _builder.Append(text);
            AdvancePosition(text);
        }

        internal void WriteInvariant(FormattableString value)
        {
            ArgumentNullException.ThrowIfNull(value);

            Write(value.ToString(CultureInfo.InvariantCulture));
        }

        internal void WriteLine(string text)
        {
            WriteIndent();
            Write(text);
            WriteLine();
        }

        internal void WriteLine()
        {
            Write(_options.NewlineString);
        }

        internal void WriteBlankLine()
        {
            WriteLine();
            WriteLine();
        }

        internal void WriteIndent()
        {
            if (_indentLevel == 0)
            {
                return;
            }

            for (int index = 0; index < _indentLevel; index++)
            {
                Write(_options.IndentString);
            }
        }

        internal void Indent()
        {
            _indentLevel++;
        }

        internal void Dedent()
        {
            if (_indentLevel == 0)
            {
                throw new InvalidOperationException("Cannot decrease indentation below zero.");
            }

            _indentLevel--;
        }

        internal (int Line, int Column) GetPosition()
        {
            return (Line, Column);
        }

        internal OutputSpan GetSpanFrom((int Line, int Column) start)
        {
            return new OutputSpan
            {
                StartLine = start.Line,
                StartColumn = start.Column,
                EndLine = Line,
                EndColumn = Math.Max(1, Column - 1),
            };
        }

        internal string GetOutput()
        {
            return _builder.ToString();
        }

        private void AdvancePosition(string text)
        {
            for (int index = 0; index < text.Length; index++)
            {
                char character = text[index];
                if (character == '\r')
                {
                    if (index + 1 < text.Length && text[index + 1] == '\n')
                    {
                        index++;
                    }

                    AdvanceLine();
                }
                else if (character == '\n')
                {
                    AdvanceLine();
                }
                else
                {
                    Column++;
                }
            }
        }

        private void AdvanceLine()
        {
            Line++;
            Column = 1;
        }
    }
}

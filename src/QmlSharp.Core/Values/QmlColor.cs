namespace QmlSharp.Core
{
    /// <summary>QML color value that can be expressed as a string or RGBA components.</summary>
    public readonly record struct QmlColor
    {
        public string? StringValue { get; init; }

        public byte? R { get; init; }

        public byte? G { get; init; }

        public byte? B { get; init; }

        public byte? A { get; init; }

        public static implicit operator QmlColor(string color)
        {
            return new QmlColor
            {
                StringValue = color,
            };
        }
    }
}

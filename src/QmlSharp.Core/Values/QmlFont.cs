namespace QmlSharp.Core
{
    /// <summary>QML font specification.</summary>
    public sealed record QmlFont
    {
        public string? Family { get; init; }

        public double? PointSize { get; init; }

        public double? PixelSize { get; init; }

        public int? Weight { get; init; }

        public bool? Bold { get; init; }

        public bool? Italic { get; init; }

        public bool? Underline { get; init; }

        public bool? Strikeout { get; init; }
    }
}

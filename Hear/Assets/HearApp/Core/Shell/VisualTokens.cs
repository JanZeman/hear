using UnityEngine;
using UnityEngine.UIElements;

namespace HearApp.Core.Shell
{
    /// <summary>
    /// Mirrors `Art/Reference/brand/hear-visual-tokens.json` (HEAR Visual Bible v0.1,
    /// schema `hear.visual-tokens.v0.1`) as compile-time-safe C# values, so shell code never
    /// hardcodes a color/spacing/radius/type-size/motion-duration ad hoc. Values here are
    /// provisional per the bible ("may be optically tuned later") - if the JSON changes, update
    /// this file to match rather than letting the two drift apart.
    /// </summary>
    public static class VisualTokens
    {
        public static class Colors
        {
            public static readonly Color Pearl0 = FromHex("#FCFCFF");
            public static readonly Color Pearl50 = FromHex("#F5F6FB");
            public static readonly Color Mist100 = FromHex("#ECEEF5");
            public static readonly Color Slate400 = FromHex("#8B93A7");
            public static readonly Color Ink700 = FromHex("#33405B");
            public static readonly Color Ink900 = FromHex("#17223C");

            public static readonly Color AuroraPearl = FromHex("#E9F3FF");
            public static readonly Color AuroraSky = FromHex("#A8D9F2");
            public static readonly Color AuroraBlue = FromHex("#6DA8E8");
            public static readonly Color AuroraIris = FromHex("#8C79E8");
            public static readonly Color AuroraViolet = FromHex("#6957D8");
        }

        /// <summary>8-based spacing rhythm with one 4-unit exception, in logical pixels.</summary>
        public static class Spacing
        {
            public const float XS = 4f;
            public const float S = 8f;
            public const float M = 12f;
            public const float L = 16f;
            public const float XL = 24f;
            public const float XXL = 32f;
            public const float XXXL = 48f;
            public const float Huge = 64f;
        }

        public static class Radius
        {
            public const float S = 10f;
            public const float M = 16f;
            public const float L = 24f;
            public const float XL = 32f;
            public const float Pill = 999f;
        }

        /// <summary>Semantic type styles: (font size in logical px, font weight).</summary>
        public readonly struct TypeStyle
        {
            public readonly int Size;
            public readonly FontStyle Style;

            public TypeStyle(int size, int weight)
            {
                Size = size;
                // UI Toolkit's runtime text engine only exposes FontStyle (Normal/Bold/...), not
                // arbitrary numeric weights, without a variable-font FontAsset. Anything >= 600
                // (the bible's "semibold"/600 weight) maps to Bold; the exact numeric weight is
                // preserved in hear-visual-tokens.json for when a real weighted font is wired up.
                Style = weight >= 600 ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        public static class Type
        {
            public static readonly TypeStyle Display = new(36, 600);
            public static readonly TypeStyle Title = new(28, 600);
            public static readonly TypeStyle Headline = new(20, 600);
            public static readonly TypeStyle Body = new(16, 400);
            public static readonly TypeStyle BodyStrong = new(16, 600);
            public static readonly TypeStyle Caption = new(13, 400);
            public static readonly TypeStyle Label = new(15, 600);
        }

        /// <summary>Motion timing tokens in milliseconds (use the midpoint of each range).</summary>
        public static class MotionMs
        {
            public const int Instant = 115;
            public const int Fast = 210;
            public const int Normal = 360;
            public const int Scene = 550;
        }

        /// <summary>Logical-width breakpoints per hear-home-screen-handoff-v1.0 metadata/home-layout.json:
        /// Compact &lt;= 599, Medium 600-959, Wide &gt;= 960.</summary>
        public static class Responsive
        {
            public const float CompactMaxExclusive = 600f;
            public const float MediumMin = 600f;
            public const float MediumMaxInclusive = 959f;
            public const float WideMin = 960f;
        }

        private static Color FromHex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var color) ? color : Color.magenta;
        }
    }
}

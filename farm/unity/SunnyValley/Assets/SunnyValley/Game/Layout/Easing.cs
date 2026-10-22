using System;

namespace SunnyValley.Layout
{
    /// <summary>Easing curves on 0..1 for the tween system (UI/Tweener.cs). Plain C#, tested without Unity.</summary>
    public static class Easing
    {
        public enum Kind
        {
            Linear,
            OutQuad,
            InQuad,
            InOutQuad,
            OutCubic,
            InCubic,
            OutBack,
            InBack,
            OutElastic,
        }

        public static float Clamp01(float t) => t < 0 ? 0 : t > 1 ? 1 : t;

        public static float Apply(Kind kind, float t)
        {
            t = Clamp01(t);
            switch (kind)
            {
                case Kind.OutQuad: return 1 - (1 - t) * (1 - t);
                case Kind.InQuad: return t * t;
                case Kind.InOutQuad: return t < 0.5f ? 2 * t * t : 1 - (float)Math.Pow(-2 * t + 2, 2) / 2;
                case Kind.OutCubic: return 1 - (float)Math.Pow(1 - t, 3);
                case Kind.InCubic: return t * t * t;
                case Kind.OutBack: return OutBack(t);
                case Kind.InBack: return InBack(t);
                case Kind.OutElastic: return OutElastic(t);
                default: return t;
            }
        }

        /// <summary>Overshoots a little and settles: a popup or a button popping in.</summary>
        public static float OutBack(float t, float s = 1.70158f)
        {
            float u = t - 1;
            return 1 + (s + 1) * u * u * u + s * u * u;
        }

        /// <summary>Pulls back a little, then goes: a popup leaving.</summary>
        public static float InBack(float t, float s = 1.70158f) => (s + 1) * t * t * t - s * t * t;

        public static float OutElastic(float t)
        {
            if (t <= 0)
                return 0;
            if (t >= 1)
                return 1;
            return (float)(Math.Pow(2, -10 * t) * Math.Sin((t * 10 - 0.75) * (2 * Math.PI / 3)) + 1);
        }

        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}

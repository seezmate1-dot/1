using UnityEngine;

namespace LastHearth.Game
{
    /// <summary>
    /// A tiny tween kit for the IMGUI screen: easing curves on 0..1 and the progress of something that
    /// started at a moment and lasts so long. Everything is time based (Tween.Now, unscaled real time),
    /// so animations look the same at 30 and at 120 frames a second. No packages needed.
    /// </summary>
    public static class Tween
    {
        /// <summary>The animation clock: real seconds since start, not slowed by Time.timeScale.</summary>
        public static float Now => Time.realtimeSinceStartup;

        public static float Clamp01(float t) => t < 0 ? 0 : t > 1 ? 1 : t;

        /// <summary>How far an animation that started at start and lasts duration is: 0..1.</summary>
        public static float Progress(float start, float duration, float now) =>
            duration <= 0 ? 1 : Clamp01((now - start) / duration);

        public static float Progress(float start, float duration) => Progress(start, duration, Now);

        /// <summary>True while an animation that started at start is still running.</summary>
        public static bool Running(float start, float duration) => Now - start >= 0 && Now - start < duration;

        public static float Lerp(float a, float b, float t) => a + (b - a) * t;

        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) => new Vector2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);

        /// <summary>A point on the curve from a to b bending through the control point c.</summary>
        public static Vector2 Bezier(Vector2 a, Vector2 c, Vector2 b, float t)
        {
            float u = 1 - t;
            return new Vector2(u * u * a.x + 2 * u * t * c.x + t * t * b.x, u * u * a.y + 2 * u * t * c.y + t * t * b.y);
        }

        // ------------------------------------------------------------------ easing curves (t: 0..1)

        public static float InQuad(float t) => t * t;

        public static float OutQuad(float t) => 1 - (1 - t) * (1 - t);

        public static float InCubic(float t) => t * t * t;

        public static float OutCubic(float t)
        {
            float u = 1 - t;
            return 1 - u * u * u;
        }

        public static float InOutCubic(float t) => t < 0.5f ? 4 * t * t * t : 1 - Pow3(-2 * t + 2) / 2;

        /// <summary>Overshoots a little and settles: the pop of a window or a button.</summary>
        public static float OutBack(float t, float overshoot = 1.70158f)
        {
            float u = t - 1;
            return 1 + (overshoot + 1) * u * u * u + overshoot * u * u;
        }

        /// <summary>A springy settle: the bump of a badge.</summary>
        public static float OutElastic(float t)
        {
            if (t <= 0)
                return 0;
            if (t >= 1)
                return 1;
            return Mathf.Pow(2, -10 * t) * Mathf.Sin((t * 10 - 0.75f) * (2 * Mathf.PI / 3)) + 1;
        }

        /// <summary>0 → 1 → 0 over the time: a flash that comes and goes.</summary>
        public static float Bump(float t) => Mathf.Sin(Clamp01(t) * Mathf.PI);

        static float Pow3(float v) => v * v * v;

        /// <summary>A colour with its transparency multiplied (for fading whole groups).</summary>
        public static Color Fade(Color c, float alpha) => new Color(c.r, c.g, c.b, c.a * alpha);

        /// <summary>A rectangle grown (or shrunk) around its centre.</summary>
        public static Rect Scale(Rect r, float s) =>
            new Rect(r.center.x - r.width * s / 2, r.center.y - r.height * s / 2, r.width * s, r.height * s);
    }
}

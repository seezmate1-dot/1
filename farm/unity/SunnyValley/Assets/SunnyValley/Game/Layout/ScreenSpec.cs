using System;

namespace SunnyValley.Layout
{
    /// <summary>
    /// A phone or tablet screen in pixels: its size, density and safe area (Screen.safeArea: the part not covered
    /// by the camera cut-out, the rounded corners and the gesture bar; origin at the lower left, Y up).
    /// </summary>
    public sealed class ScreenSpec
    {
        public readonly string Name;
        public readonly int Width;
        public readonly int Height;

        /// <summary>Dots per inch; Screen.dpi (0 when unknown, then a typical 400 is assumed).</summary>
        public readonly float Dpi;

        /// <summary>The safe area in pixels, origin lower left (Unity's Screen.safeArea).</summary>
        public readonly Box SafeArea;

        public ScreenSpec(string name, int width, int height, float dpi, Box safeArea)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentException("screen size must be positive");
            Name = name;
            Width = width;
            Height = height;
            Dpi = dpi > 0 ? dpi : 400;
            // a broken safe area (some devices report zero or garbage) falls back to the whole screen
            var full = new Box(0, 0, width, height);
            SafeArea = safeArea.W > width * 0.5f && safeArea.H > height * 0.5f && full.Contains(safeArea, 1f) ? safeArea : full;
        }

        /// <summary>A screen with insets in pixels: top (cut-out, status bar), bottom (gesture bar), sides.</summary>
        public static ScreenSpec WithInsets(string name, int width, int height, float dpi, int top = 0, int bottom = 0, int left = 0, int right = 0) =>
            new ScreenSpec(name, width, height, dpi, new Box(left, bottom, width - left - right, height - top - bottom));

        /// <summary>Pixels in one density-independent point (Android dp, iOS pt): dpi / 160.</summary>
        public float PixelsPerPoint => Dpi / 160f;

        public override string ToString() => $"{Name} {Width}×{Height} safe {SafeArea}";
    }

    /// <summary>
    /// How the canvas sits on a screen: Unity's CanvasScaler in «Scale With Screen Size» mode with the reference
    /// resolution 1080×1920. Phones taller than 16:9 match the width (the canvas stays 1080 wide and grows in height),
    /// wider screens (tablets 4:3, 16:10) match the height (1920 high, wider). Everything in the layout is in canvas units.
    /// </summary>
    public sealed class CanvasFit
    {
        public const float ReferenceWidth = 1080;
        public const float ReferenceHeight = 1920;

        public readonly ScreenSpec Screen;

        /// <summary>CanvasScaler.matchWidthOrHeight: 0 = match width, 1 = match height.</summary>
        public readonly float Match;

        /// <summary>Pixels per canvas unit (Canvas.scaleFactor).</summary>
        public readonly float Scale;

        /// <summary>The canvas size in units.</summary>
        public readonly float Width;
        public readonly float Height;

        /// <summary>The safe area in canvas units.</summary>
        public readonly Box Safe;

        public CanvasFit(ScreenSpec screen)
        {
            Screen = screen;
            Match = MatchFor(screen.Width, screen.Height);
            Scale = ScaleFor(screen.Width, screen.Height, Match);
            Width = screen.Width / Scale;
            Height = screen.Height / Scale;
            var s = screen.SafeArea;
            Safe = new Box(s.X / Scale, s.Y / Scale, s.W / Scale, s.H / Scale);
        }

        /// <summary>Width for screens at least as tall as 16:9, height for wider ones (tablets).</summary>
        public static float MatchFor(int width, int height) =>
            height * ReferenceWidth >= width * ReferenceHeight - 0.5f ? 0f : 1f;

        /// <summary>CanvasScaler's formula: a log-space blend of the width and height ratios.</summary>
        public static float ScaleFor(int width, int height, float match)
        {
            double logW = Math.Log(width / ReferenceWidth, 2);
            double logH = Math.Log(height / ReferenceHeight, 2);
            return (float)Math.Pow(2, logW + (logH - logW) * match);
        }

        /// <summary>Density-independent points (dp/pt) in one canvas unit on this screen.</summary>
        public float PointsPerUnit => Scale / Screen.PixelsPerPoint;

        /// <summary>The size of a length in canvas units on the physical screen, in points.</summary>
        public float ToPoints(float units) => units * PointsPerUnit;

        /// <summary>The canvas units that make a given number of points on this screen.</summary>
        public float FromPoints(float points) => points / PointsPerUnit;

        /// <summary>The whole canvas.</summary>
        public Box Canvas => new Box(0, 0, Width, Height);
    }
}

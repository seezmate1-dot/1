using System;
using System.Collections.Generic;

namespace SunnyValley.Layout
{
    /// <summary>
    /// Fits text into its rectangle: tries the preferred size and steps down a unit at a time to the minimum,
    /// wrapping into up to MaxLines lines. If nothing fits even at the minimum, Fits becomes false (the layout
    /// check and the tests fail) instead of the text silently spilling out, which is what broke the previous game.
    /// </summary>
    public static class TextFit
    {
        /// <summary>Spare width kept free, so a pixel of rounding on a phone never pushes a word onto the next line.</summary>
        public const float Slack = 0.03f;

        public static void Fit(UiNode node, float scale)
        {
            var t = node.Text;
            if (t == null)
                return;
            Fit(t, node.Rect.W, node.Rect.H, scale);
        }

        public static void Fit(TextSpec t, float width, float height, float scale)
        {
            int max = (int)Math.Floor(t.Size);
            int min = (int)Math.Ceiling(Math.Min(t.MinSize, t.Size));
            float usable = width * (1 - Slack);
            for (int size = max; size >= min; size--)
            {
                if (TryLines(t.Value, size, usable, height, t.MaxLines, scale, out var lines))
                {
                    t.FittedSize = size;
                    t.Lines = lines;
                    t.Fits = true;
                    return;
                }
            }
            t.FittedSize = min;
            t.Lines = FontMetrics.Wrap(t.Value ?? "", min, usable, scale);
            t.Fits = false;
        }

        static bool TryLines(string text, int size, float width, float height, int maxLines, float scale, out List<string> lines)
        {
            lines = maxLines <= 1
                ? new List<string> { (text ?? "").Replace('\n', ' ') }
                : FontMetrics.Wrap(text ?? "", size, width, scale);
            if (lines.Count > Math.Max(1, maxLines))
                return false;
            if (lines.Count * FontMetrics.LineHeight(size) > height + 0.5f)
                return false;
            foreach (string line in lines)
            {
                if (FontMetrics.LineWidth(line, size, scale) > width)
                    return false;
            }
            return true;
        }

        /// <summary>Fits every label of a resolved tree.</summary>
        public static void FitAll(UiNode root, float scale)
        {
            foreach (var n in root.All())
                Fit(n, scale);
        }
    }
}

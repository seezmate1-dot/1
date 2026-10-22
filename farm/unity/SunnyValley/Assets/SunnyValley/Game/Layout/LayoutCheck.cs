using System;
using System.Collections.Generic;

namespace SunnyValley.Layout
{
    /// <summary>
    /// The rules every layout must keep on every screen. The tests run them for many phones and tablets; the game
    /// runs them too in development builds and logs what breaks on the actual device.
    /// <list type="number">
    /// <item>Everything except full-bleed backgrounds stays inside the safe area.</item>
    /// <item>Children stay inside their parents (badges marked Overhang may stick out, but not out of the safe area).</item>
    /// <item>No two interactive elements overlap (a parent and its own child may).</item>
    /// <item>Every interactive element is at least 44 points on its short side; board cells at least 36.</item>
    /// <item>Every label fits its rectangle at no less than its minimum size, and that size is readable (8.5 pt).</item>
    /// </list>
    /// </summary>
    public static class LayoutCheck
    {
        const float Eps = 0.5f;

        public static List<string> Problems(UiLayout layout)
        {
            var problems = new List<string>();
            var fit = layout.Fit;
            var safe = fit.Safe;
            var visible = new List<UiNode>();
            foreach (var n in layout.Root.All())
            {
                if (!n.InheritsFlag(NodeFlags.Hidden))
                    visible.Add(n);
            }

            foreach (var n in visible)
            {
                if (n.Kind == NodeKind.Root)
                    continue;
                string where = $"{layout.Name} on {fit.Screen.Name}: {n.Id}";
                if (n.Rect.W < 0 || n.Rect.H < 0)
                    problems.Add($"{where} has a negative size {n.Rect}");

                var scroll = n.ScrollAbove();
                bool bleed = n.InheritsFlag(NodeFlags.FullBleed);

                // 1. the safe area
                if (!bleed)
                {
                    bool inside = scroll != null ? safe.ContainsX(n.Rect, Eps) : safe.Contains(n.Rect, Eps);
                    if (!inside)
                        problems.Add($"{where} {n.Rect} leaves the safe area {safe}");
                }

                // 2. inside the parent
                var p = n.Parent;
                if (p != null && p.Kind != NodeKind.Root && !n.Has(NodeFlags.Overhang))
                {
                    bool inside = p.Kind == NodeKind.Scroll || scroll != null ? p.Rect.ContainsX(n.Rect, Eps) : p.Rect.Contains(n.Rect, Eps);
                    if (!inside)
                        problems.Add($"{where} {n.Rect} sticks out of {p.Id} {p.Rect}");
                }

                // 4. touch size
                if (n.Kind == NodeKind.Board)
                {
                    float cell = fit.ToPoints(n.Cell);
                    if (cell < Tokens.MinCellPoints - 0.01f)
                        problems.Add($"{where}: a cell is {cell:0.#} pt, less than {Tokens.MinCellPoints}");
                }
                else if (n.Interactive && n.Kind != NodeKind.Scroll)
                {
                    float side = fit.ToPoints(n.Rect.MinSide);
                    if (side < Tokens.MinTouchPoints - 0.01f)
                        problems.Add($"{where} is {side:0.#} pt on its short side, less than {Tokens.MinTouchPoints} for a finger");
                }

                // 5. text
                if (n.Text != null)
                {
                    if (!n.Text.Fits)
                        problems.Add($"{where}: «{n.Text.Value}» does not fit {n.Rect} even at {n.Text.MinSize}");
                    float pts = fit.ToPoints(n.Text.FittedSize);
                    if (pts < Tokens.MinReadablePoints - 0.01f)
                        problems.Add($"{where}: «{n.Text.Value}» at {n.Text.FittedSize} is {pts:0.#} pt, too small to read");
                }
            }

            // 3. interactive elements never overlap (each clipped to the list it scrolls in)
            var hits = new List<(UiNode node, Box box)>();
            foreach (var n in visible)
            {
                if (!n.Interactive)
                    continue;
                var box = n.Rect;
                var scroll = n.ScrollAbove();
                if (scroll != null)
                {
                    box = Clip(box, scroll.Rect);
                    if (box.W <= 0 || box.H <= 0)
                        continue;
                }
                hits.Add((n, box));
            }
            for (int i = 0; i < hits.Count; i++)
            {
                for (int j = i + 1; j < hits.Count; j++)
                {
                    var a = hits[i];
                    var b = hits[j];
                    if (a.node.IsAncestorOf(b.node) || b.node.IsAncestorOf(a.node))
                        continue;
                    float area = a.box.OverlapArea(b.box);
                    if (area > Eps)
                        problems.Add($"{layout.Name} on {fit.Screen.Name}: {a.node.Id} {a.box} and {b.node.Id} {b.box} overlap");
                }
            }
            return problems;
        }

        static Box Clip(Box a, Box clip) =>
            Box.FromMinMax(Math.Max(a.X, clip.X), Math.Max(a.Y, clip.Y), Math.Min(a.XMax, clip.XMax), Math.Min(a.YMax, clip.YMax));

        /// <summary>The smallest gap between two interactive elements that are not nested (for reports).</summary>
        public static float MinGap(UiLayout layout)
        {
            var list = new List<UiNode>();
            foreach (var n in layout.Root.All())
            {
                if (n.Interactive && n.ScrollAbove() == null && !n.InheritsFlag(NodeFlags.Hidden))
                    list.Add(n);
            }
            float min = float.MaxValue;
            for (int i = 0; i < list.Count; i++)
            {
                for (int j = i + 1; j < list.Count; j++)
                {
                    if (list[i].IsAncestorOf(list[j]) || list[j].IsAncestorOf(list[i]))
                        continue;
                    min = Math.Min(min, list[i].Rect.Gap(list[j].Rect));
                }
            }
            return min;
        }
    }
}

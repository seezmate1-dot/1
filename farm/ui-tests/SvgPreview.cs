using System.Globalization;
using System.Linq;
using System.Text;
using SunnyValley.Layout;

namespace SunnyValley.Ui.Tests
{
    /// <summary>
    /// Draws a resolved layout as an SVG wireframe: shapes in their colours, labels at their fitted size, the areas
    /// outside the safe area tinted red, interactive elements outlined. Not the game's look, but exactly its geometry.
    /// </summary>
    public static class SvgPreview
    {
        static string F(float v) => v.ToString("0.#", CultureInfo.InvariantCulture);

        static string Rgba(uint c, out string opacity)
        {
            opacity = F(((c & 0xFF) / 255f));
            return "#" + (c >> 8).ToString("X6");
        }

        public static string Render(UiLayout L)
        {
            var fit = L.Fit;
            float W = fit.Width, H = fit.Height;
            var sb = new StringBuilder();
            sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {F(W)} {F(H)}\" width=\"{F(fit.Screen.Width / 3f)}\" height=\"{F(fit.Screen.Height / 3f)}\" font-family=\"Nunito, Arial, sans-serif\" font-weight=\"800\">\n");
            sb.Append($"<rect width=\"{F(W)}\" height=\"{F(H)}\" fill=\"#334\"/>\n");
            foreach (var n in L.Root.All())
            {
                if (n.InheritsFlag(NodeFlags.Hidden) || n.Kind == NodeKind.Root)
                    continue;
                var r = n.Rect;
                float x = r.X, y = H - r.YMax;
                string fill = Rgba(n.Color, out string op);
                bool drawShape = n.Kind == NodeKind.Panel || n.Kind == NodeKind.Button && n.Shape != Shape.None || n.Kind == NodeKind.Plate ||
                                 n.Kind == NodeKind.Bar || n.Kind == NodeKind.Dim || n.Kind == NodeKind.World || n.Kind == NodeKind.Board;
                if (drawShape)
                {
                    float rad = n.Shape == Shape.Circle ? r.MinSide / 2 : n.Shape == Shape.RoundRect ? n.Radius : 0;
                    if (n.Depth > 0)
                    {
                        string dc = Rgba(n.DepthColor, out string dop);
                        sb.Append($"<rect x=\"{F(x)}\" y=\"{F(y + n.Depth)}\" width=\"{F(r.W)}\" height=\"{F(r.H)}\" rx=\"{F(rad)}\" fill=\"{dc}\" fill-opacity=\"{dop}\"/>\n");
                    }
                    sb.Append($"<rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(r.W)}\" height=\"{F(r.H)}\" rx=\"{F(rad)}\" fill=\"{fill}\" fill-opacity=\"{op}\"/>\n");
                    if (n.Kind == NodeKind.Bar)
                        sb.Append($"<rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(System.Math.Max(r.H, r.W * n.Value))}\" height=\"{F(r.H)}\" rx=\"{F(rad)}\" fill=\"#6CC04A\"/>\n");
                    if (n.Kind == NodeKind.Board)
                    {
                        for (int c = 0; c < n.Cols; c++)
                        for (int k = 0; k < n.Rows; k++)
                        {
                            string cell = (c + k) % 2 == 0 ? "#B9E08F" : "#A5D17A";
                            sb.Append($"<rect x=\"{F(x + c * n.Cell + 2)}\" y=\"{F(y + k * n.Cell + 2)}\" width=\"{F(n.Cell - 4)}\" height=\"{F(n.Cell - 4)}\" rx=\"10\" fill=\"{cell}\"/>\n");
                        }
                    }
                }
                if (n.Kind == NodeKind.Icon)
                    sb.Append($"<circle cx=\"{F(r.CenterX)}\" cy=\"{F(H - r.CenterY)}\" r=\"{F(r.MinSide / 2 - 2)}\" fill=\"{IconColour(n.Icon)}\" stroke=\"#0004\" stroke-width=\"3\"/>\n");
                if (n.Text != null)
                {
                    var t = n.Text;
                    string tc = Rgba(t.Color, out _);
                    float lh = FontMetrics.LineHeight(t.FittedSize);
                    float top = y + (r.H - lh * t.Lines.Count) / 2;
                    string anchor = t.Align == TextAlign.Left ? "start" : t.Align == TextAlign.Right ? "end" : "middle";
                    float tx = t.Align == TextAlign.Left ? x : t.Align == TextAlign.Right ? r.XMax : r.CenterX;
                    for (int i = 0; i < t.Lines.Count; i++)
                    {
                        float baseline = top + lh * i + t.FittedSize * 1.011f;
                        string stroke = t.Outline ? " stroke=\"#3a2410\" stroke-width=\"4\" paint-order=\"stroke\"" : "";
                        sb.Append($"<text x=\"{F(tx)}\" y=\"{F(baseline)}\" font-size=\"{t.FittedSize}\" text-anchor=\"{anchor}\" fill=\"{tc}\"{stroke}>{Escape(t.Lines[i])}</text>\n");
                    }
                    if (!t.Fits)
                        sb.Append($"<rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(r.W)}\" height=\"{F(r.H)}\" fill=\"none\" stroke=\"red\" stroke-width=\"6\"/>\n");
                }
                if (n.Interactive && n.Kind != NodeKind.Scroll)
                    sb.Append($"<rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(r.W)}\" height=\"{F(r.H)}\" fill=\"none\" stroke=\"#00e0ff\" stroke-width=\"2\" stroke-dasharray=\"8 6\"/>\n");
            }
            // the parts of the screen outside the safe area: cut-out, status bar, gesture bar
            var s = fit.Safe;
            sb.Append($"<rect x=\"0\" y=\"0\" width=\"{F(W)}\" height=\"{F(H - s.YMax)}\" fill=\"#ff0000\" fill-opacity=\"0.25\"/>\n");
            sb.Append($"<rect x=\"0\" y=\"{F(H - s.Y)}\" width=\"{F(W)}\" height=\"{F(s.Y)}\" fill=\"#ff0000\" fill-opacity=\"0.25\"/>\n");
            sb.Append($"<text x=\"12\" y=\"{F(H - 12)}\" font-size=\"28\" fill=\"#fff\" stroke=\"#000\" stroke-width=\"3\" paint-order=\"stroke\">{Escape(L.Name + " — " + fit.Screen.Name)}</text>\n");
            sb.Append("</svg>\n");
            return sb.ToString();
        }

        static string IconColour(string icon)
        {
            switch (icon)
            {
                case Icons.Coin: return "#FFC928";
                case Icons.Star: return "#FFD54A";
                case Icons.Crystal: return "#4FD8E8";
                case Icons.Heart: return "#EE5A3C";
                case Icons.Sun: return "#FFE066";
                default: return "#ffffffcc";
            }
        }

        static string Escape(string s) => new string(s.Where(c => c != '\u0000').ToArray()).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}

using System;

namespace SunnyValley.Game.UI
{
    /// <summary>
    /// Placeholder pictures for the chapter 1 farm until the painted art exists (farm/tools/art/chapter1_art.json):
    /// each sprite of chapter1_layout.json names a kind of shape ("house", "fence_broken", "weeds", "boat"…) and a
    /// colour, and this paints it from simple shapes with soft edges and a darker rim, in the same cheerful palette.
    /// "Broken" and "old" kinds come out greyer and crooked, so the before → after of a task reads even without art.
    /// Plain C# (no UnityEngine); returns RGBA pixels, row 0 at the bottom. Coordinates are 0..1 of the picture, Y up.
    /// </summary>
    public static class FarmPainter
    {
        delegate float Sdf(float x, float y);

        sealed class Canvas
        {
            public readonly int W, H;
            public readonly float[] R, G, B, A;

            public Canvas(int w, int h)
            {
                W = w;
                H = h;
                R = new float[w * h];
                G = new float[w * h];
                B = new float[w * h];
                A = new float[w * h];
            }

            /// <summary>Lays a shape (distance in pixels, given 0..1 coordinates) in a colour, only over its box.</summary>
            public void Fill(float x0, float y0, float x1, float y1, Sdf sdf, uint rgba)
            {
                float cr = (rgba >> 24 & 255) / 255f, cg = (rgba >> 16 & 255) / 255f, cb = (rgba >> 8 & 255) / 255f, ca = (rgba & 255) / 255f;
                int px0 = Math.Max(0, (int)(Math.Min(x0, x1) * W) - 2), px1 = Math.Min(W - 1, (int)(Math.Max(x0, x1) * W) + 2);
                int py0 = Math.Max(0, (int)(Math.Min(y0, y1) * H) - 2), py1 = Math.Min(H - 1, (int)(Math.Max(y0, y1) * H) + 2);
                for (int y = py0; y <= py1; y++)
                for (int x = px0; x <= px1; x++)
                {
                    float d = sdf((x + 0.5f) / W, (y + 0.5f) / H);
                    float a = Clamp01(0.5f - d) * ca;
                    if (a <= 0)
                        continue;
                    int i = y * W + x;
                    float outA = a + A[i] * (1 - a);
                    R[i] = (cr * a + R[i] * A[i] * (1 - a)) / outA;
                    G[i] = (cg * a + G[i] * A[i] * (1 - a)) / outA;
                    B[i] = (cb * a + B[i] * A[i] * (1 - a)) / outA;
                    A[i] = outA;
                }
            }

            public uint[] Pixels()
            {
                var px = new uint[W * H];
                for (int i = 0; i < px.Length; i++)
                    px[i] = (uint)(R[i] * 255) << 24 | (uint)(G[i] * 255) << 16 | (uint)(B[i] * 255) << 8 | (uint)(A[i] * 255);
                return px;
            }

            // ------------------------------------------------------------------ shapes, each with a darker rim

            float Rim => 2.2f;

            /// <summary>A box from (x0, y0) to (x1, y1), corners rounded by r (in 0..1 of the shorter side).</summary>
            public void Box(float x0, float y0, float x1, float y1, uint color, float r = 0.02f, bool rim = true)
            {
                float cx = (x0 + x1) / 2, cy = (y0 + y1) / 2, hw = Math.Abs(x1 - x0) / 2 * W, hh = Math.Abs(y1 - y0) / 2 * H;
                float rr = Math.Min(Math.Min(hw, hh), r * Math.Min(W, H));
                Sdf s = (x, y) =>
                {
                    float qx = Math.Abs((x - cx) * W) - hw + rr, qy = Math.Abs((y - cy) * H) - hh + rr;
                    return Len(Math.Max(qx, 0), Math.Max(qy, 0)) + Math.Min(Math.Max(qx, qy), 0) - rr;
                };
                if (rim)
                    Fill(x0, y0, x1, y1, (x, y) => s(x, y) - Rim, Darker(color));
                Fill(x0, y0, x1, y1, s, color);
            }

            public void Ellipse(float cx, float cy, float rx, float ry, uint color, bool rim = true)
            {
                float a = rx * W, b = ry * H;
                Sdf s = (x, y) => (Len((x - cx) * W / a, (y - cy) * H / b) - 1) * Math.Min(a, b);
                if (rim)
                    Fill(cx - rx, cy - ry, cx + rx, cy + ry, (x, y) => s(x, y) - Rim, Darker(color));
                Fill(cx - rx, cy - ry, cx + rx, cy + ry, s, color);
            }

            /// <summary>A convex polygon (points in 0..1, counter-clockwise or not).</summary>
            public void Poly(uint color, bool rim, params float[] p)
            {
                int n = p.Length / 2;
                float x0 = 1, y0 = 1, x1 = 0, y1 = 0;
                for (int i = 0; i < n; i++)
                {
                    x0 = Math.Min(x0, p[2 * i]);
                    x1 = Math.Max(x1, p[2 * i]);
                    y0 = Math.Min(y0, p[2 * i + 1]);
                    y1 = Math.Max(y1, p[2 * i + 1]);
                }
                Sdf s = (x, y) => PolyDist(p, x * W, y * H, W, H);
                if (rim)
                    Fill(x0, y0, x1, y1, (x, y) => s(x, y) - Rim, Darker(color));
                Fill(x0, y0, x1, y1, s, color);
            }

            /// <summary>A thick stroke from (ax, ay) to (bx, by), width in pixels.</summary>
            public void Line(float ax, float ay, float bx, float by, float width, uint color, bool rim = true)
            {
                Sdf s = (x, y) =>
                {
                    float px = (x - ax) * W, py = (y - ay) * H, dx = (bx - ax) * W, dy = (by - ay) * H;
                    float h = Clamp01((px * dx + py * dy) / Math.Max(1e-4f, dx * dx + dy * dy));
                    return Len(px - dx * h, py - dy * h) - width / 2;
                };
                float pad = width / Math.Min(W, H);
                float x0 = Math.Min(ax, bx) - pad, x1 = Math.Max(ax, bx) + pad, y0 = Math.Min(ay, by) - pad, y1 = Math.Max(ay, by) + pad;
                if (rim)
                    Fill(x0, y0, x1, y1, (x, y) => s(x, y) - Rim, Darker(color));
                Fill(x0, y0, x1, y1, s, color);
            }
        }

        static float PolyDist(float[] p, float x, float y, float W, float H)
        {
            int n = p.Length / 2;
            float d = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = p[2 * i] * W, yi = p[2 * i + 1] * H, xj = p[2 * j] * W, yj = p[2 * j + 1] * H;
                float ex = xj - xi, ey = yj - yi, wx = x - xi, wy = y - yi;
                float h = Clamp01((wx * ex + wy * ey) / Math.Max(1e-4f, ex * ex + ey * ey));
                d = Math.Min(d, Len(wx - ex * h, wy - ey * h));
                if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi)
                    inside = !inside;
            }
            return inside ? -d : d;
        }

        static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
        static float Len(float x, float y) => (float)Math.Sqrt(x * x + y * y);

        // ------------------------------------------------------------------ colours

        public static uint Parse(string hex, uint fallback = 0xB98C5AFF)
        {
            if (string.IsNullOrEmpty(hex))
                return fallback;
            string h = hex.TrimStart('#');
            if (h.Length == 6)
                h += "FF";
            return uint.TryParse(h, System.Globalization.NumberStyles.HexNumber, null, out uint v) ? v : fallback;
        }

        static uint Mix(uint c, uint d, float k)
        {
            float Ch(uint v, int s) => (v >> s & 255);
            uint r = (uint)(Ch(c, 24) + (Ch(d, 24) - Ch(c, 24)) * k), g = (uint)(Ch(c, 16) + (Ch(d, 16) - Ch(c, 16)) * k);
            uint b = (uint)(Ch(c, 8) + (Ch(d, 8) - Ch(c, 8)) * k);
            return r << 24 | g << 16 | b << 8 | (c & 255);
        }

        static uint Darker(uint c) => Mix(c, 0x2B1D10FF, 0.45f);
        static uint Lighter(uint c) => Mix(c, 0xFFFFFFFF, 0.35f);
        static uint Grey(uint c) => Mix(c, 0x8C8578FF, 0.6f);

        const uint Wood = 0xC8955AFF, WoodDark = 0x8A5A2BFF, Roof = 0xD9573AFF, Glass = 0x9ED9F6FF, Leaf = 0x4E9A34FF, White = 0xFFFFFFFF;

        // ------------------------------------------------------------------ painting

        /// <summary>The placeholder of a kind at w×h pixels in a colour (#RRGGBB).</summary>
        public static uint[] Paint(string kind, int w, int h, string color)
        {
            var c = new Canvas(w, h);
            uint col = Parse(color);
            bool old = kind != null && (kind.EndsWith("_old") || kind.EndsWith("_broken") || kind.EndsWith("_closed"));
            if (old)
                col = Grey(col);
            switch (kind)
            {
                case "house": case "house_old": Building(c, col, old ? Grey(0xA0523AFF) : col, old, 0.72f); break;
                case "barn": case "barn_old": Barn(c, col, old); break;
                case "shed": case "shed_old": Building(c, old ? col : Wood, old ? Grey(Roof) : col, old, 0.6f); break;
                case "greenhouse": Greenhouse(c); break;
                case "tower": case "tower_old": Tower(c, col, old); break;
                case "gazebo": Gazebo(c, col); break;
                case "fence": case "fence_broken": Fence(c, col, old); break;
                case "gate": case "gate_broken": Gate(c, col, old); break;
                case "weeds": case "reeds": Clump(c, col, kind == "reeds"); break;
                case "tree": case "tree_dry": case "tree_bloom": Tree(c, kind == "tree_dry" ? 0x7A6A4AFF : Leaf, kind == "tree_dry", kind == "tree_bloom"); break;
                case "saplings": for (int i = 0; i < 3; i++) TreeAt(c, 0.2f + i * 0.3f, 0.08f, 0.13f, 0.5f, Leaf, false, false); break;
                case "sprouts": Flat(c, 0x6E4A2AFF, 4, 0); Dots(c, col, 6, 5); break;
                case "flowers": Flat(c, 0x6E4A2AFF, 0, 0); Dots(c, col, 7, 2, true); break;
                case "lilies": Pads(c, col); break;
                case "path": Flat(c, col, 6, 0, 0.36f); break;
                case "beds": Flat(c, col, 4, 0); break;
                case "borders": Borders(c, col); break;
                case "patches": Flat(c, col, 3, 0, 0.4f); break;
                case "porch": case "porch_broken": Porch(c, col, old); break;
                case "bridge": case "bridge_broken": Bridge(c, col, old); break;
                case "pier": Pier(c, col); break;
                case "window": case "window_broken": Window(c, old); break;
                case "curtains": Curtains(c, col); break;
                case "door": c.Box(0.12f, 0.02f, 0.88f, 0.98f, col, 0.2f); c.Ellipse(0.75f, 0.5f, 0.06f, 0.04f, 0xE8C040FF); break;
                case "doors_open": c.Box(0.05f, 0.02f, 0.95f, 0.95f, 0x5A3A1EFF, 0.04f); c.Box(0.18f, 0.02f, 0.82f, 0.85f, col, 0.04f); break;
                case "doors_closed": c.Box(0.05f, 0.02f, 0.95f, 0.95f, col, 0.04f); c.Line(0.1f, 0.2f, 0.9f, 0.8f, 12, WoodDark); c.Line(0.1f, 0.8f, 0.9f, 0.2f, 12, WoodDark); break;
                case "sign": Sign(c, col); break;
                case "post": Post(c, col); break;
                case "birdhouse": c.Line(0.5f, 0.02f, 0.5f, 0.4f, 8, WoodDark); c.Box(0.2f, 0.38f, 0.8f, 0.8f, col, 0.06f); c.Poly(Roof, true, 0.12f, 0.78f, 0.88f, 0.78f, 0.5f, 0.98f); c.Ellipse(0.5f, 0.58f, 0.1f, 0.08f, 0x2B1D10FF); break;
                case "smoke": case "dust": Puffs(c, kind == "dust" ? 0xC9A46AFF : 0xF2F2F2FF); break;
                case "animal": Dog(c, col); break;
                case "birds": Birds(c, col); break;
                case "boat": case "boat_broken": Boat(c, col, old); break;
                case "sails": case "sails_broken": Sails(c, col, old); break;
                case "swing": c.Line(0.25f, 0.98f, 0.3f, 0.25f, 4, 0x8A7A5AFF, false); c.Line(0.75f, 0.98f, 0.7f, 0.25f, 4, 0x8A7A5AFF, false); c.Box(0.2f, 0.18f, 0.8f, 0.28f, col, 0.2f); break;
                case "scarecrow": Scarecrow(c, col); break;
                case "garland": Garland(c, col); break;
                case "cart": case "cart_broken": Cart(c, col, old); break;
                case "machine": Machine(c, col); break;
                case "shelves": Shelves(c, col); break;
                case "table": Table(c, col); break;
                case "chest": c.Box(0.1f, 0.05f, 0.9f, 0.6f, col, 0.06f); c.Box(0.1f, 0.55f, 0.9f, 0.9f, Lighter(col), 0.2f); c.Box(0.44f, 0.4f, 0.56f, 0.6f, 0xE8C040FF, 0.03f); break;
                case "stones": c.Ellipse(0.35f, 0.4f, 0.3f, 0.36f, col); c.Ellipse(0.65f, 0.38f, 0.3f, 0.34f, Lighter(col)); c.Ellipse(0.65f, 0.38f, 0.06f, 0.07f, Darker(col)); break;
                case "gears": c.Ellipse(0.38f, 0.5f, 0.32f, 0.4f, col); c.Ellipse(0.38f, 0.5f, 0.12f, 0.15f, Darker(col)); c.Box(0.68f, 0.05f, 0.92f, 0.45f, 0x6A7A8AFF, 0.08f); break;
                case "nests": c.Box(0.05f, 0.05f, 0.95f, 0.5f, Wood, 0.05f); for (int i = 0; i < 3; i++) c.Ellipse(0.22f + i * 0.28f, 0.55f, 0.13f, 0.2f, col); break;
                case "basket": c.Ellipse(0.5f, 0.55f, 0.32f, 0.25f, 0xFFF3E0FF); c.Ellipse(0.38f, 0.62f, 0.12f, 0.17f, 0xE0B080FF); c.Ellipse(0.62f, 0.62f, 0.12f, 0.17f, 0xF0D0A0FF); c.Box(0.12f, 0.05f, 0.88f, 0.55f, col, 0.2f); break;
                case "trough": c.Box(0.05f, 0.08f, 0.6f, 0.5f, col, 0.06f); c.Box(0.1f, 0.4f, 0.55f, 0.52f, 0xE8C040FF, 0.04f, false); c.Ellipse(0.8f, 0.25f, 0.17f, 0.15f, 0x9AB0C0FF); break;
                case "barrel": case "barrel_broken": Barrel(c, col, old); break;
                case "crate": c.Box(0.1f, 0.05f, 0.9f, 0.85f, col, 0.04f); c.Line(0.15f, 0.45f, 0.85f, 0.45f, 6, Darker(col), false); break;
                case "junk": Junk(c, col); break;
                case "pile": Pile(c, col); break;
                default: c.Box(0.08f, 0.04f, 0.92f, 0.92f, col, 0.1f); break;
            }
            return c.Pixels();
        }

        static void Building(Canvas c, uint walls, uint roof, bool old, float wallTop)
        {
            c.Box(0.1f, 0.02f, 0.9f, wallTop, walls, 0.02f);
            c.Box(0.1f, 0.02f, 0.9f, 0.12f, Darker(walls), 0.01f, false);
            c.Poly(roof, true, 0.02f, wallTop - 0.02f, 0.98f, wallTop - 0.02f, 0.8f, 0.98f, 0.2f, 0.98f);
            c.Box(0.68f, wallTop + 0.08f, 0.78f, 0.96f, Darker(roof), 0.02f);
            if (old)
            {
                c.Ellipse(0.4f, wallTop + 0.14f, 0.07f, 0.05f, 0x2B1D10FF, false);
                c.Ellipse(0.6f, wallTop + 0.2f, 0.05f, 0.04f, 0x2B1D10FF, false);
                c.Line(0.15f, 0.3f, 0.85f, 0.32f, 6, Darker(walls), false);
            }
        }

        static void Barn(Canvas c, uint col, bool old)
        {
            c.Box(0.06f, 0.02f, 0.94f, 0.62f, col, 0.02f);
            c.Poly(old ? Grey(0x6A5A4AFF) : Darker(col), true, 0.02f, 0.6f, 0.98f, 0.6f, 0.8f, 0.86f, 0.5f, 0.98f, 0.2f, 0.86f);
            c.Box(0.4f, 0.66f, 0.6f, 0.8f, old ? 0x5A4A3AFF : White, 0.02f);
            c.Box(0.32f, 0.02f, 0.68f, 0.44f, old ? 0x5A4A3AFF : White, 0.02f);
            c.Box(0.35f, 0.02f, 0.65f, 0.41f, Darker(col), 0.02f, false);
            if (old)
            {
                c.Ellipse(0.3f, 0.8f, 0.06f, 0.04f, 0x2B1D10FF, false);
                c.Ellipse(0.72f, 0.74f, 0.05f, 0.03f, 0x2B1D10FF, false);
            }
        }

        static void Greenhouse(Canvas c)
        {
            c.Box(0.08f, 0.02f, 0.92f, 0.58f, 0xBFE8F0C0, 0.02f);
            c.Poly(0xD8F2F8C0, true, 0.04f, 0.56f, 0.96f, 0.56f, 0.5f, 0.98f);
            for (int i = 1; i < 4; i++)
                c.Line(0.08f + i * 0.21f, 0.02f, 0.08f + i * 0.21f, 0.58f, 5, White, false);
            for (int i = 0; i < 4; i++)
                c.Ellipse(0.2f + i * 0.2f, 0.12f, 0.06f, 0.08f, Leaf);
        }

        static void Tower(Canvas c, uint col, bool old)
        {
            c.Poly(col, true, 0.22f, 0.02f, 0.78f, 0.02f, 0.68f, 0.78f, 0.32f, 0.78f);
            c.Poly(old ? Grey(0x8A5A3AFF) : 0x8A5A3AFF, true, 0.26f, 0.76f, 0.74f, 0.76f, 0.5f, 0.98f);
            c.Box(0.42f, 0.02f, 0.58f, 0.24f, old ? 0x6A5A4AFFu : 0x5A3A1EFFu, 0.05f);
            if (old)
            {
                c.Line(0.4f, 0.04f, 0.6f, 0.22f, 6, WoodDark);
                c.Line(0.4f, 0.22f, 0.6f, 0.04f, 6, WoodDark);
                c.Line(0.35f, 0.5f, 0.45f, 0.4f, 3, Darker(col), false);
            }
            c.Ellipse(0.5f, 0.52f, 0.06f, 0.05f, old ? 0x2B1D10FF : Glass);
        }

        static void Gazebo(Canvas c, uint col)
        {
            c.Ellipse(0.5f, 0.1f, 0.44f, 0.08f, Lighter(col));
            for (int i = 0; i < 4; i++)
                c.Line(0.14f + i * 0.24f, 0.08f, 0.14f + i * 0.24f, 0.6f, 9, col);
            c.Poly(Roof, true, 0.04f, 0.58f, 0.96f, 0.58f, 0.5f, 0.98f);
        }

        static void Fence(Canvas c, uint col, bool broken)
        {
            int posts = 5;
            for (int i = 0; i < posts; i++)
            {
                float x = 0.06f + i * 0.88f / (posts - 1);
                if (broken && i == 2)
                    continue;
                float tilt = broken && i % 2 == 1 ? 0.06f : 0;
                c.Line(x, 0.04f, x + tilt, 0.9f, 12, col);
            }
            if (broken)
            {
                c.Line(0.04f, 0.6f, 0.4f, 0.55f, 10, col);
                c.Line(0.6f, 0.3f, 0.96f, 0.4f, 10, col);
            }
            else
            {
                c.Line(0.03f, 0.65f, 0.97f, 0.65f, 10, col);
                c.Line(0.03f, 0.32f, 0.97f, 0.32f, 10, col);
            }
        }

        static void Gate(Canvas c, uint col, bool broken)
        {
            if (broken)
            {
                c.Poly(col, true, 0.05f, 0.1f, 0.9f, 0.05f, 0.95f, 0.45f, 0.1f, 0.5f);
                c.Line(0.1f, 0.15f, 0.9f, 0.4f, 6, Darker(col), false);
                c.Line(0.8f, 0.1f, 0.88f, 0.9f, 14, col);
                return;
            }
            c.Box(0.04f, 0.02f, 0.14f, 0.92f, col, 0.04f);
            c.Box(0.86f, 0.02f, 0.96f, 0.92f, col, 0.04f);
            c.Poly(Roof, true, 0.0f, 0.86f, 1.0f, 0.86f, 0.9f, 0.99f, 0.1f, 0.99f);
            c.Poly(Lighter(col), true, 0.14f, 0.1f, 0.34f, 0.04f, 0.34f, 0.62f, 0.14f, 0.7f);
            c.Poly(Lighter(col), true, 0.86f, 0.1f, 0.66f, 0.04f, 0.66f, 0.62f, 0.86f, 0.7f);
        }

        static void Clump(Canvas c, uint col, bool reeds)
        {
            var rng = new Random(reeds ? 7 : 3);
            for (int i = 0; i < 14; i++)
            {
                float x = 0.1f + (float)rng.NextDouble() * 0.8f;
                float top = 0.45f + (float)rng.NextDouble() * 0.5f;
                uint shade = i % 3 == 0 ? Lighter(col) : col;
                c.Line(x, 0.05f, x + ((float)rng.NextDouble() - 0.5f) * 0.25f, top, reeds ? 6 : 9, shade);
                if (reeds && i % 2 == 0)
                    c.Ellipse(x, top - 0.08f, 0.025f, 0.08f, 0x7A4A1EFF);
            }
            if (!reeds)
            {
                c.Ellipse(0.35f, 0.2f, 0.25f, 0.2f, col);
                c.Ellipse(0.65f, 0.22f, 0.25f, 0.22f, Lighter(col));
            }
        }

        static void Tree(Canvas c, uint crown, bool dry, bool bloom) => TreeAt(c, 0.5f, 0.02f, 0.4f, 0.98f, crown, dry, bloom);

        static void TreeAt(Canvas c, float x, float y0, float r, float top, uint crown, bool dry, bool bloom)
        {
            float trunkTop = y0 + (top - y0) * 0.45f;
            c.Box(x - r * 0.12f, y0, x + r * 0.12f, trunkTop + 0.05f, 0x7A4A1EFF, 0.02f);
            if (dry)
            {
                c.Line(x, trunkTop, x - r * 0.8f, top - 0.1f, 8, 0x6A4A2AFF);
                c.Line(x, trunkTop, x + r * 0.7f, top - 0.05f, 8, 0x6A4A2AFF);
                c.Line(x, trunkTop, x + r * 0.1f, top, 7, 0x6A4A2AFF);
                c.Ellipse(x - r * 0.3f, top - 0.25f, r * 0.4f, (top - y0) * 0.15f, Mix(crown, Leaf, 0.3f));
                return;
            }
            float cy = trunkTop + (top - trunkTop) * 0.5f, ry = (top - trunkTop) * 0.55f;
            c.Ellipse(x, cy, r, ry, crown);
            c.Ellipse(x - r * 0.35f, cy + ry * 0.3f, r * 0.4f, ry * 0.35f, Lighter(crown), false);
            if (bloom)
            {
                var rng = new Random(11);
                for (int i = 0; i < 18; i++)
                    c.Ellipse(x + ((float)rng.NextDouble() - 0.5f) * r * 1.6f, cy + ((float)rng.NextDouble() - 0.5f) * ry * 1.4f, 0.03f, 0.025f,
                        i % 2 == 0 ? 0xFFFFFFFF : 0xF4A6C0FF, false);
            }
        }

        /// <summary>A flat iso strip of ground (a path, beds, planks) with stripes across it.</summary>
        static void Flat(Canvas c, uint col, int stripes, int unused, float narrow = 1)
        {
            float hw = 0.48f * narrow;
            c.Poly(col, true, 0.5f - hw, 0.02f, 0.5f + hw, 0.02f, 0.5f + hw * 0.7f, 0.98f, 0.5f - hw * 0.7f, 0.98f);
            for (int i = 1; i <= stripes; i++)
            {
                float y = i / (stripes + 1f);
                float w = hw * (1 - 0.3f * y);
                c.Line(0.5f - w + 0.02f, y, 0.5f + w - 0.02f, y, 4, Darker(col), false);
            }
        }

        static void Borders(Canvas c, uint col)
        {
            for (int i = 0; i < 3; i++)
            {
                float x0 = 0.04f + i * 0.32f;
                c.Box(x0, 0.04f, x0 + 0.28f, 0.95f, col, 0.04f);
                c.Box(x0 + 0.03f, 0.08f, x0 + 0.25f, 0.91f, 0x6E4A2AFF, 0.03f, false);
            }
        }

        static void Dots(Canvas c, uint col, int cols, int rows, bool flowers = false)
        {
            for (int r = 0; r < rows; r++)
            for (int k = 0; k < cols; k++)
            {
                float x = 0.14f + k * 0.72f / Math.Max(1, cols - 1), y = 0.15f + r * 0.7f / Math.Max(1, rows);
                uint tint = flowers ? new[] { 0xFF6F91FFu, 0xFFD54AFFu, 0xFFFFFFFFu, 0xA070E0FFu }[(r + k) % 4] : col;
                c.Ellipse(x, y + (flowers ? 0.25f : 0), flowers ? 0.05f : 0.035f, flowers ? 0.14f : 0.06f, tint);
            }
        }

        static void Pads(Canvas c, uint col)
        {
            c.Ellipse(0.3f, 0.45f, 0.25f, 0.3f, col);
            c.Ellipse(0.7f, 0.5f, 0.22f, 0.28f, Lighter(col));
            c.Ellipse(0.32f, 0.55f, 0.08f, 0.14f, 0xFF8FB0FF);
        }

        static void Porch(Canvas c, uint col, bool broken)
        {
            for (int i = 0; i < 3; i++)
            {
                float y0 = 0.04f + i * 0.28f, inset = i * 0.08f;
                if (broken && i == 1)
                {
                    c.Box(0.1f + inset, y0, 0.45f, y0 + 0.24f, col, 0.02f);
                    continue;
                }
                c.Box(0.06f + inset, y0, 0.94f - inset, y0 + 0.26f, i % 2 == 0 ? col : Lighter(col), 0.02f);
            }
            if (!broken)
            {
                c.Line(0.08f, 0.3f, 0.2f, 0.98f, 8, col);
                c.Line(0.92f, 0.3f, 0.8f, 0.98f, 8, col);
            }
        }

        static void Bridge(Canvas c, uint col, bool broken)
        {
            c.Poly(col, true, 0.02f, 0.2f, 0.98f, 0.2f, 0.8f, 0.55f, 0.2f, 0.55f);
            if (broken)
                c.Poly(0x2B1D1060, false, 0.42f, 0.2f, 0.6f, 0.2f, 0.58f, 0.55f, 0.45f, 0.55f);
            c.Line(0.05f, 0.5f, 0.3f, 0.95f, 8, col);
            c.Line(0.95f, 0.5f, broken ? 0.85f : 0.7f, broken ? 0.7f : 0.95f, 8, col);
            if (!broken)
                c.Line(0.3f, 0.95f, 0.7f, 0.95f, 8, col);
        }

        static void Pier(Canvas c, uint col)
        {
            for (int i = 0; i < 4; i++)
                c.Line(0.15f + i * 0.23f, 0.02f, 0.15f + i * 0.23f, 0.4f, 8, WoodDark);
            c.Poly(col, true, 0.05f, 0.35f, 0.95f, 0.35f, 0.85f, 0.7f, 0.15f, 0.7f);
            c.Line(0.7f, 0.7f, 0.95f, 0.98f, 3, 0x5A3A1EFF, false);
            c.Box(0.25f, 0.62f, 0.4f, 0.8f, 0x8A9AAAFF, 0.03f);
        }

        static void Window(Canvas c, bool broken)
        {
            c.Box(0.06f, 0.06f, 0.94f, 0.94f, White, 0.05f);
            c.Box(0.14f, 0.14f, 0.86f, 0.86f, broken ? 0x3A2A1AFF : Glass, 0.03f, false);
            if (broken)
            {
                c.Line(0.05f, 0.25f, 0.95f, 0.75f, 12, 0xA07A4AFF);
                c.Line(0.05f, 0.75f, 0.95f, 0.25f, 12, 0xA07A4AFF);
            }
            else
            {
                c.Line(0.5f, 0.14f, 0.5f, 0.86f, 6, White, false);
                c.Line(0.14f, 0.5f, 0.86f, 0.5f, 6, White, false);
                c.Line(0.22f, 0.7f, 0.35f, 0.82f, 4, 0xFFFFFFA0, false);
            }
        }

        static void Curtains(Canvas c, uint col)
        {
            c.Poly(col, true, 0.1f, 0.92f, 0.42f, 0.92f, 0.2f, 0.5f, 0.12f, 0.12f);
            c.Poly(col, true, 0.9f, 0.92f, 0.58f, 0.92f, 0.8f, 0.5f, 0.88f, 0.12f);
            c.Box(0.06f, 0.86f, 0.94f, 0.96f, Darker(col), 0.02f);
        }

        static void Sign(Canvas c, uint col)
        {
            c.Line(0.2f, 0.02f, 0.2f, 0.7f, 10, WoodDark);
            c.Line(0.8f, 0.02f, 0.8f, 0.7f, 10, WoodDark);
            c.Box(0.05f, 0.45f, 0.95f, 0.92f, col, 0.06f);
            c.Ellipse(0.5f, 0.68f, 0.12f, 0.1f, 0xFFC928FF);
        }

        static void Post(Canvas c, uint col)
        {
            c.Box(0.4f, 0.02f, 0.6f, 0.08f, Darker(col), 0.02f);
            c.Line(0.5f, 0.05f, 0.5f, 0.78f, 9, col);
            c.Box(0.25f, 0.75f, 0.75f, 0.95f, col, 0.04f);
            c.Ellipse(0.5f, 0.85f, 0.16f, 0.07f, 0xFFE27AFF, false);
        }

        static void Puffs(Canvas c, uint col)
        {
            c.Ellipse(0.35f, 0.35f, 0.28f, 0.26f, col, false);
            c.Ellipse(0.62f, 0.45f, 0.3f, 0.3f, col, false);
            c.Ellipse(0.48f, 0.68f, 0.26f, 0.24f, Lighter(col), false);
        }

        static void Dog(Canvas c, uint col)
        {
            c.Ellipse(0.45f, 0.32f, 0.3f, 0.24f, col);
            c.Ellipse(0.7f, 0.62f, 0.2f, 0.22f, col);
            c.Ellipse(0.62f, 0.72f, 0.07f, 0.14f, Darker(col));
            c.Ellipse(0.76f, 0.66f, 0.03f, 0.03f, 0x2B1D10FF, false);
            c.Ellipse(0.88f, 0.58f, 0.04f, 0.035f, 0x2B1D10FF, false);
            c.Line(0.18f, 0.35f, 0.06f, 0.6f, 8, col);
        }

        static void Birds(Canvas c, uint col)
        {
            for (int i = 0; i < 3; i++)
            {
                float x = 0.2f + i * 0.3f, y = 0.3f + (i % 2) * 0.1f;
                c.Ellipse(x, y, 0.13f, 0.17f, col);
                c.Ellipse(x + 0.08f, y + 0.2f, 0.07f, 0.09f, col);
                c.Poly(0xFF9A3CFF, false, x + 0.13f, y + 0.22f, x + 0.19f, y + 0.19f, x + 0.13f, y + 0.17f);
            }
        }

        static void Boat(Canvas c, uint col, bool old)
        {
            c.Poly(col, true, 0.02f, 0.6f, 0.98f, 0.6f, 0.8f, 0.08f, 0.2f, 0.08f);
            c.Box(0.06f, 0.52f, 0.94f, 0.62f, old ? Grey(Wood) : White, 0.04f);
            if (old)
            {
                c.Line(0.3f, 0.2f, 0.4f, 0.45f, 4, 0x2B1D10FF, false);
                c.Line(0.6f, 0.15f, 0.55f, 0.4f, 4, 0x2B1D10FF, false);
            }
            else
            {
                c.Line(0.25f, 0.65f, 0.05f, 0.95f, 6, Wood);
            }
        }

        static void Sails(Canvas c, uint col, bool broken)
        {
            const float cx = 0.5f, cy = 0.45f;
            float[] ang = { 45, 135, 225, 315 };
            for (int i = 0; i < 4; i++)
            {
                if (broken && i == 1)
                    continue;
                double a = ang[i] * Math.PI / 180;
                float ex = cx + (float)Math.Cos(a) * 0.48f, ey = cy + (float)Math.Sin(a) * 0.5f;
                c.Line(cx, cy, ex, ey, 8, WoodDark);
                if (!broken || i % 2 == 0)
                {
                    double b = a + 0.35;
                    float fx = cx + (float)Math.Cos(b) * 0.42f, fy = cy + (float)Math.Sin(b) * 0.44f;
                    float mx = cx + (float)Math.Cos(a) * 0.15f, my = cy + (float)Math.Sin(a) * 0.15f;
                    float nx = cx + (float)Math.Cos(b) * 0.14f, ny = cy + (float)Math.Sin(b) * 0.14f;
                    c.Poly(broken ? Grey(col) : col, true, mx, my, ex, ey, fx, fy, nx, ny);
                }
            }
            c.Ellipse(cx, cy, 0.06f, 0.06f, WoodDark);
        }

        static void Scarecrow(Canvas c, uint col)
        {
            c.Line(0.5f, 0.02f, 0.5f, 0.8f, 9, WoodDark);
            c.Line(0.12f, 0.6f, 0.88f, 0.6f, 9, WoodDark);
            c.Poly(0x6CA0D0FF, true, 0.3f, 0.66f, 0.7f, 0.66f, 0.62f, 0.3f, 0.38f, 0.3f);
            c.Ellipse(0.5f, 0.78f, 0.16f, 0.09f, 0xF0D8A0FF);
            c.Box(0.25f, 0.84f, 0.75f, 0.88f, col, 0.02f);
            c.Box(0.36f, 0.86f, 0.64f, 0.97f, col, 0.04f);
        }

        static void Garland(Canvas c, uint col)
        {
            c.Line(0.04f, 0.02f, 0.04f, 0.95f, 10, WoodDark);
            c.Line(0.96f, 0.02f, 0.96f, 0.95f, 10, WoodDark);
            uint[] tints = { col, 0xFFD54AFF, 0x6CC04AFF, 0x47A8EEFF };
            for (int i = 0; i < 9; i++)
            {
                float t = (i + 0.5f) / 9, x = 0.06f + t * 0.88f, y = 0.9f - 0.25f * (float)Math.Sin(t * Math.PI);
                c.Line(x - 0.05f, y + 0.01f, x + 0.05f, y - 0.01f, 3, 0x5A3A1EFF, false);
                c.Poly(tints[i % 4], true, x - 0.04f, y, x + 0.04f, y, x, y - 0.12f);
            }
        }

        static void Cart(Canvas c, uint col, bool broken)
        {
            c.Box(0.12f, 0.3f, 0.88f, 0.7f, col, 0.04f);
            if (!broken)
                c.Box(0.16f, 0.62f, 0.84f, 0.78f, 0xE8C870FF, 0.1f);
            c.Ellipse(0.3f, 0.25f, 0.13f, 0.2f, WoodDark);
            c.Ellipse(0.3f, 0.25f, 0.05f, 0.08f, Wood, false);
            if (!broken)
            {
                c.Ellipse(0.72f, 0.25f, 0.13f, 0.2f, WoodDark);
                c.Ellipse(0.72f, 0.25f, 0.05f, 0.08f, Wood, false);
            }
            else
            {
                c.Ellipse(0.8f, 0.1f, 0.15f, 0.08f, WoodDark);
            }
            c.Line(0.88f, 0.45f, 0.99f, 0.3f, 6, WoodDark);
        }

        static void Machine(Canvas c, uint col)
        {
            c.Box(0.25f, 0.2f, 0.8f, 0.62f, col, 0.04f);
            c.Box(0.45f, 0.55f, 0.75f, 0.9f, col, 0.04f);
            c.Box(0.5f, 0.62f, 0.7f, 0.82f, Glass, 0.02f, false);
            c.Poly(0x8A8A8AFF, true, 0.02f, 0.1f, 0.25f, 0.1f, 0.25f, 0.45f, 0.08f, 0.4f);
            c.Box(0.2f, 0.02f, 0.9f, 0.2f, 0x3A3A3AFF, 0.1f);
        }

        static void Shelves(Canvas c, uint col)
        {
            c.Box(0.06f, 0.02f, 0.94f, 0.96f, col, 0.03f);
            for (int r = 0; r < 3; r++)
            {
                float y = 0.1f + r * 0.3f;
                c.Box(0.1f, y, 0.9f, y + 0.04f, Darker(col), 0.01f, false);
                for (int k = 0; k < 3; k++)
                    c.Box(0.16f + k * 0.26f, y + 0.04f, 0.3f + k * 0.26f, y + 0.22f, new[] { 0xE8505AFFu, 0xFFC928FFu, 0x6CC04AFFu }[(r + k) % 3], 0.05f);
            }
        }

        static void Table(Canvas c, uint col)
        {
            c.Line(0.18f, 0.02f, 0.2f, 0.5f, 8, WoodDark);
            c.Line(0.82f, 0.02f, 0.8f, 0.5f, 8, WoodDark);
            c.Poly(col, true, 0.02f, 0.45f, 0.98f, 0.45f, 0.86f, 0.7f, 0.14f, 0.7f);
            c.Ellipse(0.5f, 0.8f, 0.12f, 0.14f, 0xE0A040FF);
            c.Ellipse(0.25f, 0.68f, 0.08f, 0.05f, 0xC0703AFF);
            c.Ellipse(0.75f, 0.68f, 0.08f, 0.05f, White);
        }

        static void Barrel(Canvas c, uint col, bool broken)
        {
            if (broken)
            {
                c.Line(0.2f, 0.05f, 0.35f, 0.75f, 12, col);
                c.Line(0.5f, 0.05f, 0.5f, 0.6f, 12, col);
                c.Line(0.75f, 0.05f, 0.85f, 0.45f, 12, col);
                c.Ellipse(0.5f, 0.12f, 0.4f, 0.08f, 0x6A6A6AFF);
                return;
            }
            c.Box(0.15f, 0.04f, 0.85f, 0.9f, col, 0.15f);
            c.Box(0.12f, 0.25f, 0.88f, 0.31f, 0x6A6A6AFF, 0.02f, false);
            c.Box(0.12f, 0.65f, 0.88f, 0.71f, 0x6A6A6AFF, 0.02f, false);
            c.Ellipse(0.5f, 0.88f, 0.33f, 0.06f, 0x5CB8E8FF);
        }

        static void Junk(Canvas c, uint col)
        {
            c.Box(0.05f, 0.04f, 0.45f, 0.45f, Grey(Wood), 0.03f);
            c.Ellipse(0.66f, 0.2f, 0.28f, 0.18f, 0x3A3A3AFF);
            c.Ellipse(0.66f, 0.2f, 0.12f, 0.08f, Grey(0x6A6A6AFF), false);
            c.Line(0.15f, 0.5f, 0.7f, 0.75f, 10, Grey(WoodDark));
            c.Box(0.4f, 0.4f, 0.65f, 0.66f, col, 0.1f);
            c.Line(0.6f, 0.6f, 0.95f, 0.5f, 8, Grey(Wood));
        }

        static void Pile(Canvas c, uint col)
        {
            c.Ellipse(0.3f, 0.3f, 0.26f, 0.28f, col);
            c.Ellipse(0.7f, 0.3f, 0.26f, 0.28f, Lighter(col));
            c.Ellipse(0.5f, 0.62f, 0.24f, 0.28f, col);
            c.Line(0.42f, 0.84f, 0.58f, 0.84f, 6, Darker(col), false);
        }

        // ------------------------------------------------------------------ portraits and fog

        /// <summary>A portrait placeholder: a round frame with a face (or the diary), the colours from the character id.</summary>
        public static uint[] Portrait(string who, int size)
        {
            var c = new Canvas(size, size);
            uint hair, shirt, skin = 0xF5CBA7FF, back = 0xFFE9B0FF;
            switch (who)
            {
                case "anya": hair = 0x8A4A1EFF; shirt = 0x47A8EEFF; break;
                case "matvey": hair = 0xF2F2F2FF; shirt = 0xC8453AFF; break;
                case "vera": hair = 0xE8505AFF; shirt = 0xFF9A3CFF; break;
                case "grachev": hair = 0x2B2B2BFF; shirt = 0x6A6A7AFF; back = 0xD8D8E8FF; break;
                case "pasha": hair = 0xF0C850FF; shirt = 0x6CC04AFF; break;
                case "nina": hair = 0xB0B0B0FF; shirt = 0xA070E0FF; break;
                case "fedot": hair = 0x6A3A1AFF; shirt = 0xB7834EFF; break;
                case "lida": hair = 0xD9573AFF; shirt = 0x2369A8FF; break;
                case "sharik":
                    c.Ellipse(0.5f, 0.5f, 0.48f, 0.48f, back, false);
                    Dog(c, 0x8A5A2BFF);
                    return c.Pixels();
                case "diary":
                    c.Ellipse(0.5f, 0.5f, 0.48f, 0.48f, back, false);
                    c.Box(0.22f, 0.18f, 0.78f, 0.82f, 0x47A8EEFF, 0.04f);
                    c.Box(0.28f, 0.22f, 0.74f, 0.78f, 0xF6F0E0FF, 0.02f, false);
                    for (int i = 0; i < 5; i++)
                        c.Line(0.33f, 0.32f + i * 0.1f, 0.68f, 0.32f + i * 0.1f, 3, 0x9EC0E0FF, false);
                    c.Line(0.6f, 0.82f, 0.62f, 0.95f, 6, 0xEE5A3CFF, false);
                    return c.Pixels();
                default: hair = 0x6A4A2AFF; shirt = 0x86C95AFF; break;
            }
            c.Ellipse(0.5f, 0.5f, 0.48f, 0.48f, back, false);
            c.Ellipse(0.5f, 0.06f, 0.4f, 0.26f, shirt);
            c.Ellipse(0.5f, 0.58f, 0.25f, 0.28f, hair);
            c.Ellipse(0.5f, 0.5f, 0.21f, 0.24f, skin);
            c.Ellipse(0.42f, 0.52f, 0.025f, 0.035f, 0x2B1D10FF, false);
            c.Ellipse(0.58f, 0.52f, 0.025f, 0.035f, 0x2B1D10FF, false);
            c.Line(0.43f, 0.4f, 0.57f, 0.4f, 4, 0xC0504AFF, false);
            c.Ellipse(0.36f, 0.43f, 0.04f, 0.025f, 0xF4A0A0A0, false);
            c.Ellipse(0.64f, 0.43f, 0.04f, 0.025f, 0xF4A0A0A0, false);
            if (who == "matvey" || who == "fedot")
                c.Ellipse(0.5f, 0.34f, 0.14f, 0.1f, hair, false);
            if (who == "matvey")
                c.Box(0.28f, 0.7f, 0.72f, 0.8f, 0x6A6A5AFF, 0.3f);
            if (who == "anya")
                c.Box(0.3f, 0.74f, 0.7f, 0.8f, 0xE8C870FF, 0.3f);
            return c.Pixels();
        }

        /// <summary>A soft white cloud for the fog over closed zones.</summary>
        public static uint[] Cloud(int w, int h)
        {
            var c = new Canvas(w, h);
            c.Ellipse(0.3f, 0.4f, 0.26f, 0.32f, 0xF4F7F2F0, false);
            c.Ellipse(0.55f, 0.55f, 0.3f, 0.4f, 0xFFFFFFF0, false);
            c.Ellipse(0.75f, 0.4f, 0.22f, 0.3f, 0xF0F4EEF0, false);
            c.Box(0.12f, 0.12f, 0.88f, 0.42f, 0xF4F7F2F0, 0.2f, false);
            return c.Pixels();
        }
    }
}

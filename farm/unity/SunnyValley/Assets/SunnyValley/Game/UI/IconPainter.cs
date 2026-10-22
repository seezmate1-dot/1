using System;
using System.Collections.Generic;

namespace SunnyValley.Game.UI
{
    /// <summary>
    /// Paints the placeholder icons from simple shapes (discs, boxes, stars, polygons, strokes) with soft edges and a
    /// darker rim, in the bright cartoon colours of the style (GDD §10). Plain C# (no UnityEngine) so it can be
    /// previewed outside Unity; returns RGBA pixels, row 0 at the bottom like a Unity texture. Coordinates are 0..1, Y up.
    /// </summary>
    public static class IconPainter
    {
        public const int Size = 128;

        public static readonly string[] Names =
        {
            "coin", "star", "star.empty", "crystal", "heart", "tasks", "events", "orders", "shop", "settings", "pause", "close",
            "shovel", "rake", "can", "shuffle", "pitchfork", "bomb", "rainbow", "sun", "lock", "check",
            "tomato", "corn", "apple", "carrot", "eggplant", "blueberry",
        };

        delegate float Sdf(float x, float y);

        sealed class Canvas
        {
            public readonly float[] R = new float[Size * Size], G = new float[Size * Size], B = new float[Size * Size], A = new float[Size * Size];

            /// <summary>Lays a shape in a colour over what is painted (alpha over).</summary>
            public void Fill(Sdf sdf, uint rgba)
            {
                float cr = (rgba >> 24 & 255) / 255f, cg = (rgba >> 16 & 255) / 255f, cb = (rgba >> 8 & 255) / 255f, ca = (rgba & 255) / 255f;
                for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float d = sdf((x + 0.5f) / Size, (y + 0.5f) / Size) * Size;
                    float a = Clamp01(0.5f - d) * ca;
                    if (a <= 0)
                        continue;
                    int i = y * Size + x;
                    float outA = a + A[i] * (1 - a);
                    R[i] = (cr * a + R[i] * A[i] * (1 - a)) / outA;
                    G[i] = (cg * a + G[i] * A[i] * (1 - a)) / outA;
                    B[i] = (cb * a + B[i] * A[i] * (1 - a)) / outA;
                    A[i] = outA;
                }
            }

            /// <summary>A shape with a darker rim: the rim colour grown by w, then the face.</summary>
            public void Rimmed(Sdf sdf, uint face, uint rim, float w = 0.035f)
            {
                Fill((x, y) => sdf(x, y) - w, rim);
                Fill(sdf, face);
            }

            public uint[] Pixels()
            {
                var px = new uint[Size * Size];
                for (int i = 0; i < px.Length; i++)
                    px[i] = (uint)(R[i] * 255) << 24 | (uint)(G[i] * 255) << 16 | (uint)(B[i] * 255) << 8 | (uint)(A[i] * 255);
                return px;
            }
        }

        static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
        static float Len(float x, float y) => (float)Math.Sqrt(x * x + y * y);

        // ------------------------------------------------------------------ shapes (distance in 0..1 units)

        static Sdf Disc(float cx, float cy, float r) => (x, y) => Len(x - cx, y - cy) - r;

        static Sdf Ellipse(float cx, float cy, float rx, float ry) =>
            (x, y) => (Len((x - cx) / rx, (y - cy) / ry) - 1) * Math.Min(rx, ry);

        static Sdf Box(float cx, float cy, float hw, float hh, float r = 0) => (x, y) =>
        {
            float qx = Math.Abs(x - cx) - hw + r, qy = Math.Abs(y - cy) - hh + r;
            return Len(Math.Max(qx, 0), Math.Max(qy, 0)) + Math.Min(Math.Max(qx, qy), 0) - r;
        };

        static Sdf Ring(float cx, float cy, float r, float w) => (x, y) => Math.Abs(Len(x - cx, y - cy) - r) - w;

        /// <summary>A stroke from a to b, w thick on each side, round ends.</summary>
        static Sdf Line(float ax, float ay, float bx, float by, float w) => (x, y) =>
        {
            float px = x - ax, py = y - ay, dx = bx - ax, dy = by - ay;
            float h = Clamp01((px * dx + py * dy) / (dx * dx + dy * dy));
            return Len(px - dx * h, py - dy * h) - w;
        };

        /// <summary>Any simple polygon (Inigo Quilez's distance).</summary>
        static Sdf Poly(params float[] v)
        {
            int n = v.Length / 2;
            return (x, y) =>
            {
                float d = (x - v[0]) * (x - v[0]) + (y - v[1]) * (y - v[1]);
                float s = 1;
                for (int i = 0, j = n - 1; i < n; j = i, i++)
                {
                    float ex = v[2 * j] - v[2 * i], ey = v[2 * j + 1] - v[2 * i + 1];
                    float wx = x - v[2 * i], wy = y - v[2 * i + 1];
                    float h = Clamp01((wx * ex + wy * ey) / (ex * ex + ey * ey));
                    float bx = wx - ex * h, by = wy - ey * h;
                    d = Math.Min(d, bx * bx + by * by);
                    bool c1 = y >= v[2 * i + 1], c2 = y < v[2 * j + 1], c3 = ex * wy > ey * wx;
                    if (c1 && c2 && c3 || !c1 && !c2 && !c3)
                        s = -s;
                }
                return s * (float)Math.Sqrt(d);
            };
        }

        static Sdf Star(float cx, float cy, float ro, float ri, int points, float turn = 0)
        {
            var v = new float[points * 4];
            for (int i = 0; i < points * 2; i++)
            {
                double a = Math.PI / 2 + turn + i * Math.PI / points;
                float r = i % 2 == 0 ? ro : ri;
                v[2 * i] = cx + r * (float)Math.Cos(a);
                v[2 * i + 1] = cy + r * (float)Math.Sin(a);
            }
            return Poly(v);
        }

        static Sdf Union(params Sdf[] s) => (x, y) =>
        {
            float d = float.MaxValue;
            foreach (var f in s)
                d = Math.Min(d, f(x, y));
            return d;
        };

        static Sdf Minus(Sdf a, Sdf b) => (x, y) => Math.Max(a(x, y), -b(x, y));

        static Sdf Round(Sdf s, float r) => (x, y) => s(x, y) - r;

        static Sdf Heart(float cx, float cy, float s) => Round(Union(
            Disc(cx - 0.17f * s, cy + 0.12f * s, 0.2f * s),
            Disc(cx + 0.17f * s, cy + 0.12f * s, 0.2f * s),
            Poly(cx - 0.36f * s, cy + 0.06f * s, cx + 0.36f * s, cy + 0.06f * s, cx, cy - 0.36f * s)), 0.02f);

        static Sdf Gear(float cx, float cy) => Minus((x, y) =>
        {
            float dx = x - cx, dy = y - cy;
            double a = Math.Atan2(dy, dx);
            float tooth = Clamp01((float)Math.Cos(8 * a) * 2.5f + 0.5f);
            return Len(dx, dy) - (0.31f + 0.08f * tooth);
        }, Disc(cx, cy, 0.13f));

        // ------------------------------------------------------------------ colours

        const uint White = 0xFFFFFFFF, Outline = 0x5A3512FF, Gold = 0xFFC928FF, GoldDark = 0xD98E00FF, Red = 0xEE4A35FF, RedDark = 0xA8331FFF;
        const uint Green = 0x5DBB3FFF, GreenDark = 0x2F7A1EFF, Brown = 0x9A6232FF, BrownDark = 0x5E3816FF, Blue = 0x47A8EEFF, BlueDark = 0x2369A8FF;
        const uint Cyan = 0x4FD8E8FF, CyanDark = 0x1E8FA8FF, Gray = 0xC9CED6FF, GrayDark = 0x6F7884FF, Purple = 0x8E5BD6FF, PurpleDark = 0x5A2E9AFF;
        const uint Orange = 0xFF8A2AFF, OrangeDark = 0xC0561AFF, Shine = 0xFFFFFF70;

        public static uint[] Paint(string name)
        {
            var c = new Canvas();
            switch (name)
            {
                case "coin":
                    c.Rimmed(Disc(.5f, .5f, .4f), Gold, GoldDark, .05f);
                    c.Fill(Ring(.5f, .5f, .28f, .025f), GoldDark);
                    c.Fill(Box(.5f, .5f, .05f, .15f, .04f), GoldDark);
                    c.Fill(Ellipse(.38f, .66f, .1f, .05f), Shine);
                    break;
                case "star":
                    c.Rimmed(Round(Star(.5f, .48f, .44f, .2f, 5), .03f), Gold, GoldDark, .045f);
                    c.Fill(Ellipse(.42f, .6f, .07f, .04f), Shine);
                    break;
                case "star.empty":
                    c.Rimmed(Round(Star(.5f, .48f, .44f, .2f, 5), .03f), 0x00000040, 0x00000060, .045f);
                    break;
                case "crystal":
                    c.Rimmed(Poly(.5f, .92f, .86f, .6f, .5f, .08f, .14f, .6f), Cyan, CyanDark, .04f);
                    c.Fill(Poly(.5f, .92f, .62f, .6f, .5f, .08f, .38f, .6f), 0xFFFFFF40);
                    break;
                case "heart":
                    c.Rimmed(Heart(.5f, .5f, 1.15f), Red, RedDark, .045f);
                    c.Fill(Ellipse(.33f, .66f, .08f, .05f), Shine);
                    break;
                case "tasks":
                    c.Rimmed(Box(.5f, .46f, .3f, .38f, .06f), White, Outline, .035f);
                    c.Fill(Box(.5f, .84f, .14f, .07f, .03f), Brown);
                    for (int i = 0; i < 3; i++)
                    {
                        c.Fill(Disc(.32f, .62f - i * .17f, .04f), Green);
                        c.Fill(Line(.42f, .62f - i * .17f, .68f, .62f - i * .17f, .025f), GrayDark);
                    }
                    break;
                case "events":
                    c.Fill(Line(.28f, .1f, .28f, .9f, .04f), BrownDark);
                    c.Rimmed(Poly(.3f, .88f, .84f, .7f, .3f, .5f), Red, RedDark, .03f);
                    c.Fill(Star(.48f, .69f, .1f, .045f, 5), Gold);
                    break;
                case "orders":
                    c.Rimmed(Box(.5f, .44f, .36f, .3f, .04f), 0xD9A066FF, BrownDark, .035f);
                    c.Fill(Box(.5f, .44f, .07f, .3f), 0xB27A40FF);
                    c.Fill(Line(.14f, .58f, .86f, .58f, .015f), BrownDark);
                    break;
                case "shop":
                    c.Fill(Ring(.5f, .64f, .17f, .035f), BrownDark);
                    c.Rimmed(Box(.5f, .4f, .32f, .28f, .08f), Orange, OrangeDark, .035f);
                    c.Fill(Star(.5f, .4f, .12f, .055f, 5), Gold);
                    break;
                case "settings":
                    c.Rimmed(Gear(.5f, .5f), White, 0x2369A8FF, .03f);
                    break;
                case "pause":
                    c.Rimmed(Box(.37f, .5f, .07f, .26f, .05f), White, 0x2369A8FF, .03f);
                    c.Rimmed(Box(.63f, .5f, .07f, .26f, .05f), White, 0x2369A8FF, .03f);
                    break;
                case "close":
                    c.Rimmed(Union(Line(.28f, .28f, .72f, .72f, .07f), Line(.28f, .72f, .72f, .28f, .07f)), White, RedDark, .03f);
                    break;
                case "check":
                    c.Fill(Union(Line(.22f, .52f, .42f, .3f, .08f), Line(.42f, .3f, .8f, .74f, .08f)), White);
                    break;
                case "shovel":
                    c.Fill(Line(.62f, .62f, .86f, .86f, .045f), Brown);
                    c.Rimmed(Poly(.2f, .2f, .5f, .32f, .62f, .62f, .32f, .5f), Gray, GrayDark, .03f);
                    break;
                case "rake":
                    c.Fill(Line(.2f, .2f, .72f, .72f, .04f), Brown);
                    c.Rimmed(Line(.56f, .88f, .88f, .56f, .04f), Gray, GrayDark, .02f);
                    for (int i = 0; i < 4; i++)
                    {
                        float t = i / 3f, x = .58f + t * .28f, y = .86f - t * .28f;
                        c.Rimmed(Line(x, y, x + .1f, y + .1f, .025f), Gray, GrayDark, .015f);
                    }
                    break;
                case "can":
                    c.Fill(Ring(.42f, .66f, .14f, .03f), BlueDark);
                    c.Fill(Line(.62f, .4f, .9f, .62f, .045f), BlueDark);
                    c.Rimmed(Box(.42f, .36f, .24f, .22f, .06f), Blue, BlueDark, .035f);
                    c.Fill(Disc(.92f, .64f, .05f), BlueDark);
                    break;
                case "shuffle":
                    c.Rimmed(Union(Line(.2f, .3f, .72f, .7f, .045f), Poly(.66f, .82f, .86f, .74f, .74f, .56f)), Purple, PurpleDark, .025f);
                    c.Rimmed(Union(Line(.2f, .7f, .72f, .3f, .045f), Poly(.66f, .18f, .86f, .26f, .74f, .44f)), Purple, PurpleDark, .025f);
                    break;
                case "pitchfork":
                    c.Fill(Line(.5f, .08f, .5f, .56f, .04f), Brown);
                    c.Rimmed(Union(Line(.3f, .58f, .7f, .58f, .035f), Line(.3f, .58f, .3f, .9f, .03f), Line(.5f, .58f, .5f, .92f, .03f),
                        Line(.7f, .58f, .7f, .9f, .03f)), Gray, GrayDark, .02f);
                    break;
                case "bomb":
                    c.Fill(Line(.62f, .62f, .76f, .8f, .03f), BrownDark);
                    c.Rimmed(Disc(.44f, .42f, .3f), 0x3A3A48FF, 0x15151CFF, .03f);
                    c.Fill(Ellipse(.34f, .52f, .08f, .05f), 0xFFFFFF50);
                    c.Fill(Star(.8f, .84f, .12f, .05f, 6), Orange);
                    c.Fill(Disc(.8f, .84f, .04f), Gold);
                    break;
                case "rainbow":
                    c.Rimmed(Round(Star(.5f, .5f, .44f, .3f, 12), .03f), Gold, GoldDark, .02f);
                    c.Rimmed(Disc(.5f, .5f, .2f), 0x7A4A1EFF, BrownDark, .02f);
                    break;
                case "sun":
                    c.Fill(Round(Star(.5f, .5f, .48f, .34f, 14), .01f), 0xFFB22EFF);
                    c.Rimmed(Disc(.5f, .5f, .32f), 0xFFE066FF, 0xFFB22EFF, .02f);
                    c.Fill(Ellipse(.42f, .6f, .1f, .06f), Shine);
                    break;
                case "lock":
                    c.Fill(Ring(.5f, .6f, .16f, .045f), GrayDark);
                    c.Rimmed(Box(.5f, .38f, .26f, .2f, .05f), Gold, GoldDark, .03f);
                    c.Fill(Disc(.5f, .4f, .05f), GoldDark);
                    break;
                case "tomato":
                    c.Rimmed(Ellipse(.5f, .44f, .38f, .34f), 0xF0432EFF, 0xA82515FF, .035f);
                    c.Fill(Round(Star(.5f, .76f, .2f, .07f, 5), .01f), Green);
                    c.Fill(Ellipse(.36f, .55f, .08f, .05f), Shine);
                    break;
                case "corn":
                    c.Fill(Poly(.2f, .1f, .5f, .5f, .3f, .2f), Green);
                    c.Fill(Poly(.8f, .1f, .5f, .5f, .7f, .2f), Green);
                    c.Rimmed(Ellipse(.5f, .54f, .2f, .38f), 0xFFD83AFF, 0xD99A00FF, .03f);
                    for (int i = 0; i < 4; i++)
                        c.Fill(Line(.36f, .32f + i * .12f, .64f, .32f + i * .12f, .008f), 0xD99A0080);
                    break;
                case "apple":
                    c.Fill(Line(.5f, .72f, .54f, .88f, .025f), BrownDark);
                    c.Fill(Ellipse(.64f, .82f, .1f, .05f), Green);
                    c.Rimmed(Union(Disc(.38f, .46f, .28f), Disc(.62f, .46f, .28f)), 0x7ED13AFF, 0x3E8A1CFF, .035f);
                    c.Fill(Ellipse(.36f, .58f, .07f, .05f), Shine);
                    break;
                case "carrot":
                    c.Fill(Union(Line(.5f, .7f, .36f, .92f, .035f), Line(.5f, .7f, .5f, .95f, .035f), Line(.5f, .7f, .64f, .92f, .035f)), Green);
                    c.Rimmed(Round(Poly(.3f, .72f, .7f, .72f, .5f, .06f), .04f), Orange, OrangeDark, .03f);
                    c.Fill(Line(.42f, .5f, .52f, .5f, .012f), OrangeDark);
                    c.Fill(Line(.46f, .34f, .56f, .34f, .012f), OrangeDark);
                    break;
                case "eggplant":
                    c.Rimmed((x, y) => Ellipse(0, 0, .22f, .38f)((x - .5f) * .87f + (y - .45f) * .5f, -(x - .5f) * .5f + (y - .45f) * .87f),
                        Purple, PurpleDark, .03f);
                    c.Fill(Round(Star(.66f, .74f, .14f, .06f, 5, .5f), .01f), Green);
                    c.Fill(Ellipse(.42f, .44f, .05f, .1f), Shine);
                    break;
                case "blueberry":
                    c.Rimmed(Disc(.5f, .48f, .36f), 0x4A6FE0FF, 0x26388AFF, .035f);
                    c.Fill(Round(Star(.5f, .62f, .11f, .05f, 5), .01f), 0x26388AFF);
                    c.Fill(Ellipse(.36f, .58f, .08f, .05f), Shine);
                    break;
                default:
                    // unknown: a grey disc with a question mark-ish dot, visibly a placeholder
                    c.Rimmed(Disc(.5f, .5f, .36f), Gray, GrayDark, .03f);
                    c.Fill(Disc(.5f, .5f, .08f), GrayDark);
                    break;
            }
            return c.Pixels();
        }

        /// <summary>The names of the six board pieces, for the board view.</summary>
        public static IReadOnlyList<string> Pieces => SunnyValley.Layout.Icons.Pieces;
    }
}

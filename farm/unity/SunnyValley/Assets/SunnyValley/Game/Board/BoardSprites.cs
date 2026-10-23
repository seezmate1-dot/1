using System;
using SunnyValley.Core.Match3;
using SunnyValley.Game.UI;
using UnityEngine;

namespace SunnyValley.Game.Match3View
{
    /// <summary>
    /// Every picture the board uses: the painted art (Resources/Art: tiles, specials, obstacles) and a few shapes
    /// painted in code once (soft glow, sparkle, ring, streak, grass, chevron). Cached; built on first use.
    /// </summary>
    public static class BoardSprites
    {
        static readonly Sprite[] tiles = new Sprite[TileColors.Max];
        static Sprite pitchfork, bee, bomb, rainbow, chick;
        static Sprite ice, chain, honey, stone, weed, bed, basket;
        static readonly Sprite[] crates = new Sprite[3];
        static Sprite glow, spark, ring, streak, grass, chevron, dot;

        /// <summary>Colours of the crops for particles, glows and beams (tomato, corn, apple, carrot, eggplant, blueberry).</summary>
        public static readonly Color[] CropColors =
        {
            new Color(0.93f, 0.25f, 0.18f), new Color(1f, 0.82f, 0.2f), new Color(0.45f, 0.82f, 0.25f),
            new Color(1f, 0.55f, 0.15f), new Color(0.6f, 0.3f, 0.75f), new Color(0.3f, 0.45f, 0.95f),
        };

        public static Color ColorOf(TileColor c) => c == TileColor.None ? new Color(1f, 0.95f, 0.6f) : CropColors[(int)c];

        static Sprite Art(ref Sprite slot, string name)
        {
            if (slot == null)
                slot = ShapeSprites.Icon(name);
            return slot;
        }

        static Sprite Load(ref Sprite slot, string path)
        {
            if (slot != null)
                return slot;
            slot = Resources.Load<Sprite>(path);
            if (slot == null)
            {
                var tex = Resources.Load<Texture2D>(path);
                if (tex != null)
                    slot = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect);
            }
            if (slot == null)
                slot = Dot;
            return slot;
        }

        public static Sprite Tile(TileColor c)
        {
            if (c == TileColor.None)
                return Rainbow;
            int i = (int)c;
            if (tiles[i] == null)
                tiles[i] = ShapeSprites.Icon(TileColors.Names[i]);
            return tiles[i];
        }

        public static Sprite Pitchfork => Art(ref pitchfork, "pitchfork");
        public static Sprite Bomb => Art(ref bomb, "bomb");
        public static Sprite Rainbow => Art(ref rainbow, "rainbow");
        public static Sprite Bee => Load(ref bee, "Art/Specials/Bee");
        public static Sprite Chick => Load(ref chick, "Art/Obstacles/Chick");
        public static Sprite Ice => Load(ref ice, "Art/Obstacles/Ice");
        public static Sprite Chain => Load(ref chain, "Art/Obstacles/Chain");
        public static Sprite Honey => Load(ref honey, "Art/Obstacles/Honey");
        public static Sprite Stone => Load(ref stone, "Art/Obstacles/Stone");
        public static Sprite Weed => Load(ref weed, "Art/Obstacles/Weed");
        public static Sprite Bed => Load(ref bed, "Art/Obstacles/Bed");
        public static Sprite Basket => Load(ref basket, "Art/Obstacles/Basket");

        public static Sprite Crate(int layers)
        {
            int i = Mathf.Clamp(layers, 1, 3) - 1;
            return Load(ref crates[i], "Art/Obstacles/Crate" + (i + 1));
        }

        /// <summary>The picture of a piece: crop, special or chick.</summary>
        public static Sprite Piece(PieceKind kind, TileColor color, SpecialKind special)
        {
            if (kind == PieceKind.Chick)
                return Chick;
            switch (special)
            {
                case SpecialKind.LineH:
                case SpecialKind.LineV: return Pitchfork;
                case SpecialKind.Bee: return Bee;
                case SpecialKind.Bomb: return Bomb;
                case SpecialKind.Rainbow: return Rainbow;
                default: return Tile(color);
            }
        }

        public static Sprite Special(SpecialKind special) => Piece(PieceKind.Tile, TileColor.Tomato, special);

        public static Sprite Obstacle(ObstacleKind kind, int layers)
        {
            switch (kind)
            {
                case ObstacleKind.Crate: return Crate(layers);
                case ObstacleKind.Ice: return Ice;
                case ObstacleKind.Chain: return Chain;
                case ObstacleKind.Honey: return Honey;
                case ObstacleKind.Stone: return Stone;
                case ObstacleKind.Weed: return Weed;
                case ObstacleKind.Bed: return Bed;
                case ObstacleKind.Basket: return Basket;
                case ObstacleKind.Grass: return Grass;
                default: return Dot;
            }
        }

        // ------------------------------------------------------------------ painted in code

        /// <summary>A soft round glow: white in the middle fading to nothing at the edge.</summary>
        public static Sprite Glow => glow != null ? glow : glow = Paint(64, 64, (x, y) =>
        {
            float d = Mathf.Sqrt(x * x + y * y);
            float a = Mathf.Clamp01(1 - d);
            return new Color(1, 1, 1, a * a);
        });

        /// <summary>A four-pointed sparkle.</summary>
        public static Sprite Spark => spark != null ? spark : spark = Paint(64, 64, (x, y) =>
        {
            float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
            float star = Mathf.Clamp01(1 - (Mathf.Sqrt(ax) + Mathf.Sqrt(ay)) * 0.95f);
            float core = Mathf.Clamp01(1 - Mathf.Sqrt(x * x + y * y) * 3f);
            float a = Mathf.Clamp01(star * 2.2f + core);
            return new Color(1, 1, 1, a);
        });

        /// <summary>A thin soft ring (shockwaves, selection).</summary>
        public static Sprite Ring => ring != null ? ring : ring = Paint(128, 128, (x, y) =>
        {
            float d = Mathf.Sqrt(x * x + y * y);
            float a = Mathf.Clamp01(1 - Mathf.Abs(d - 0.86f) / 0.12f);
            return new Color(1, 1, 1, a * a);
        });

        /// <summary>A horizontal streak: bright core, soft ends (pitchfork trails, beams).</summary>
        public static Sprite Streak => streak != null ? streak : streak = Paint(128, 32, (x, y) =>
        {
            float across = Mathf.Clamp01(1 - Mathf.Abs(y));
            float along = Mathf.Clamp01((1 - Mathf.Abs(x)) * 4f);
            float a = across * across * along;
            float white = Mathf.Clamp01(1 - Mathf.Abs(y) * 2.5f);
            return new Color(1, 1, 1, Mathf.Clamp01(a + white * along * 0.5f));
        });

        /// <summary>A white disc with a soft edge (debris, drops).</summary>
        public static Sprite Dot => dot != null ? dot : dot = Paint(32, 32, (x, y) =>
        {
            float d = Mathf.Sqrt(x * x + y * y);
            return new Color(1, 1, 1, Mathf.Clamp01((1 - d) * 8f));
        });

        /// <summary>A small chevron pointing down (spawners, chick exits).</summary>
        public static Sprite Chevron => chevron != null ? chevron : chevron = Paint(64, 64, (x, y) =>
        {
            // a V: distance to the two strokes
            float yy = -y + 0.2f;
            float d = Mathf.Abs(yy - Mathf.Abs(x) * 0.9f + 0.35f);
            float inside = Mathf.Abs(x) < 0.75f ? 1 : 0;
            return new Color(1, 1, 1, Mathf.Clamp01((0.16f - d) * 14f) * inside);
        });

        /// <summary>A lawn tile under the pieces: a rounded square of grass with lighter blades.</summary>
        public static Sprite Grass => grass != null ? grass : grass = Paint(128, 128, (x, y) =>
        {
            float qx = Mathf.Abs(x) - 0.82f, qy = Mathf.Abs(y) - 0.82f;
            float box = Mathf.Sqrt(Mathf.Max(qx, 0) * Mathf.Max(qx, 0) + Mathf.Max(qy, 0) * Mathf.Max(qy, 0)) + Mathf.Min(Mathf.Max(qx, qy), 0) - 0.14f;
            float a = Mathf.Clamp01(-box * 40f);
            float u = (x + 1) * 0.5f, v = (y + 1) * 0.5f;
            // blades: a repeating tuft pattern
            float bx = u * 7f, by = v * 6f;
            float fx = bx - Mathf.Floor(bx) - 0.5f, fy = by - Mathf.Floor(by);
            float blade = Mathf.Clamp01(1 - Mathf.Abs(fx + (fy - 0.5f) * 0.35f) * 9f) * fy;
            float shade = 0.82f + 0.18f * v + blade * 0.35f;
            return new Color(0.42f * shade, 0.74f * shade, 0.26f * shade, a);
        });

        delegate Color Pixel(float x, float y);

        /// <summary>Paints w×h pixels; the function gets x, y in -1..1 (y up).</summary>
        static Sprite Paint(int w, int h, Pixel f)
        {
            var px = new Color32[w * h];
            for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                float x = (i + 0.5f) / w * 2 - 1, y = (j + 0.5f) / h * 2 - 1;
                px[j * w + i] = f(x, y);
            }
            var tex = ShapeSprites.Upload(w, h, px);
            var s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect);
            s.name = "board." + w + "x" + h;
            return s;
        }

        /// <summary>Paints everything now (during loading, not in the middle of the first move).</summary>
        public static void Warm()
        {
            ShapeSprites.Register("grass", Grass);
            _ = Glow;
            _ = Spark;
            _ = Ring;
            _ = Streak;
            _ = Dot;
            _ = Chevron;
            _ = Grass;
            for (int i = 0; i < TileColors.Max; i++)
                Tile((TileColor)i);
            _ = Pitchfork;
            _ = Bee;
            _ = Bomb;
            _ = Rainbow;
            _ = Chick;
            for (int i = 1; i <= 3; i++)
                Crate(i);
            _ = Ice;
            _ = Chain;
            _ = Honey;
            _ = Stone;
            _ = Weed;
            _ = Bed;
            _ = Basket;
        }
    }
}

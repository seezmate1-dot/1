using System;
using System.Collections.Generic;
using UnityEngine;

namespace SunnyValley.Game.UI
{
    /// <summary>
    /// Sprites drawn in code, so the interface needs no art to run: a rounded rectangle for 9-slicing (any corner
    /// radius from one sprite, via Image.pixelsPerUnitMultiplier), a circle, and placeholder icons painted from signed
    /// distance shapes with smooth edges. The real art replaces the icons later by name (Resources/Icons/&lt;name&gt;).
    /// </summary>
    public static class ShapeSprites
    {
        /// <summary>The corner radius of the rounded-rectangle sprite in its own pixels.</summary>
        public const int SliceRadius = 64;

        static Sprite roundRect;
        static Sprite circle;
        static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();

        /// <summary>A white rounded rectangle with 64-pixel corners and a 9-slice border.</summary>
        public static Sprite RoundRect
        {
            get
            {
                if (roundRect == null)
                {
                    int r = SliceRadius, size = 2 * r + 4;
                    var tex = Paint(size, size, (x, y) => RoundBox(x - size / 2f, y - size / 2f, size / 2f, size / 2f, r));
                    roundRect = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0,
                        SpriteMeshType.FullRect, new Vector4(r, r, r, r));
                    roundRect.name = "RoundRect";
                }
                return roundRect;
            }
        }

        /// <summary>A white disc.</summary>
        public static Sprite Circle
        {
            get
            {
                if (circle == null)
                {
                    const int size = 256;
                    var tex = Paint(size, size, (x, y) => Length(x - size / 2f, y - size / 2f) - (size / 2f - 1));
                    circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect);
                    circle.name = "Circle";
                }
                return circle;
            }
        }

        /// <summary>
        /// Painted art in Resources/Art for the icon names the layouts use (farm/tools art pipeline). Names without art
        /// look in Resources/Icons/&lt;name&gt;, and without that get a placeholder painted in code (IconPainter).
        /// </summary>
        public static readonly Dictionary<string, string> ArtPaths = new Dictionary<string, string>
        {
            { "coin", "Art/Currency/Coin" }, { "star", "Art/Currency/Star" }, { "crystal", "Art/Currency/Crystal" },
            { "heart", "Art/Currency/Heart" },
            { "tomato", "Art/Tiles/Tomato" }, { "corn", "Art/Tiles/Corn" }, { "apple", "Art/Tiles/Apple" },
            { "carrot", "Art/Tiles/Carrot" }, { "eggplant", "Art/Tiles/Eggplant" }, { "blueberry", "Art/Tiles/Blueberry" },
            { "pitchfork", "Art/Specials/Pitchfork" }, { "bomb", "Art/Specials/Bomb" }, { "rainbow", "Art/Specials/Rainbow" },
            { "shovel", "Art/Boosters/Shovel" }, { "rake", "Art/Boosters/Rake" }, { "can", "Art/Boosters/WateringCan" },
            { "shuffle", "Art/Boosters/Shuffle" },
            { "crate", "Art/Obstacles/Crate1" }, { "ice", "Art/Obstacles/Ice" }, { "chain", "Art/Obstacles/Chain" },
            { "honey", "Art/Obstacles/Honey" }, { "stone", "Art/Obstacles/Stone" }, { "weed", "Art/Obstacles/Weed" },
            { "bed", "Art/Obstacles/Bed" }, { "basket", "Art/Obstacles/Basket" }, { "chick", "Art/Obstacles/Chick" },
            { "bee", "Art/Specials/Bee" },
        };

        /// <summary>The icon by name: painted art if there is any, else a placeholder painted in code.</summary>
        public static Sprite Icon(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            if (icons.TryGetValue(name, out var cached) && cached != null)
                return cached;
            var art = LoadArt(ArtPaths.TryGetValue(name, out string path) ? path : "Icons/" + name);
            var sprite = art != null ? art : PaintIcon(name);
            icons[name] = sprite;
            return sprite;
        }

        /// <summary>Puts a sprite made elsewhere (the board's painted grass) under an icon name.</summary>
        public static void Register(string name, Sprite sprite)
        {
            if (!string.IsNullOrEmpty(name) && sprite != null)
                icons[name] = sprite;
        }

        static Sprite LoadArt(string path)
        {
            var sprite = Resources.Load<Sprite>(path);
            if (sprite != null)
                return sprite;
            // imported as a plain texture (no .meta yet): make the sprite here
            var tex = Resources.Load<Texture2D>(path);
            if (tex == null)
                return null;
            sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect);
            sprite.name = path;
            return sprite;
        }

        /// <summary>All icon names the screens use, for warming the cache during loading.</summary>
        public static IEnumerable<string> KnownIcons()
        {
            foreach (string n in IconPainter.Names)
                yield return n;
        }

        // ------------------------------------------------------------------ painting

        /// <summary>A signed distance in pixels: negative inside, positive outside.</summary>
        public delegate float Sdf(float x, float y);

        /// <summary>Paints a white mask from a distance function with a one-pixel soft edge.</summary>
        public static Texture2D Paint(int w, int h, Sdf sdf)
        {
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = sdf(x + 0.5f, y + 0.5f);
                byte a = (byte)(Mathf.Clamp01(0.5f - d) * 255);
                px[y * w + x] = new Color32(255, 255, 255, a);
            }
            return Upload(w, h, px);
        }

        public static Texture2D Upload(int w, int h, Color32[] px)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        static Sprite PaintIcon(string name)
        {
            const int size = IconPainter.Size;
            var rgba = IconPainter.Paint(name);
            var px = new Color32[rgba.Length];
            for (int i = 0; i < px.Length; i++)
            {
                uint v = rgba[i];
                px[i] = new Color32((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
            }
            var tex = Upload(size, size, px);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            return sprite;
        }

        public static float Length(float x, float y) => Mathf.Sqrt(x * x + y * y);

        /// <summary>A box of half sizes hw, hh centred at the origin with corners rounded by r.</summary>
        public static float RoundBox(float x, float y, float hw, float hh, float r)
        {
            float qx = Math.Abs(x) - hw + r, qy = Math.Abs(y) - hh + r;
            return Length(Math.Max(qx, 0), Math.Max(qy, 0)) + Math.Min(Math.Max(qx, qy), 0) - r;
        }
    }
}

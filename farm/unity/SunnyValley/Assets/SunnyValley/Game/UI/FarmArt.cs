using System.Collections.Generic;
using SunnyValley.Core.Meta;
using UnityEngine;

namespace SunnyValley.Game.UI
{
    /// <summary>
    /// The pictures of the chapter 1 farm: the painted art from Resources/Art/&lt;name&gt; (Ch1/Backgrounds/Yard,
    /// Ch1/Looks/PorchStone…; farm/tools/art/chapter1_art.json lists them) when it exists, otherwise a placeholder
    /// painted in code (FarmPainter) from the sprite's shape kind and colour in chapter1_layout.json. So the farm plays
    /// today and gets prettier picture by picture as the art lands, with no code change.
    /// </summary>
    public static class FarmArt
    {
        static FarmLayoutDef layout;
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, SpriteDef> byArt = new Dictionary<string, SpriteDef>();

        /// <summary>The placement of chapter 1 (Resources/Meta/chapter1_layout.json).</summary>
        public static FarmLayoutDef Layout
        {
            get
            {
                if (layout != null)
                    return layout;
                var text = Resources.Load<TextAsset>(FarmLayoutDef.Resource);
                layout = text != null ? FarmLayoutDef.Parse(text.text) : new FarmLayoutDef();
                foreach (var z in layout.Zones)
                foreach (var s in z.Sprites)
                {
                    if (!byArt.ContainsKey(s.Art))
                        byArt[s.Art] = s;
                    if (s.Looks != null)
                    {
                        foreach (var a in s.Looks.Values)
                        {
                            if (!byArt.ContainsKey(a))
                                byArt[a] = s;
                        }
                    }
                }
                return layout;
            }
        }

        /// <summary>The painted picture, or null.</summary>
        public static Sprite Painted(string art)
        {
            if (string.IsNullOrEmpty(art))
                return null;
            string key = "art:" + art;
            if (cache.TryGetValue(key, out var s))
                return s;
            s = Resources.Load<Sprite>("Art/" + art);
            if (s == null)
            {
                var tex = Resources.Load<Texture2D>("Art/" + art);
                if (tex != null)
                {
                    s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0f), 100, 0, SpriteMeshType.FullRect);
                    s.name = art;
                }
            }
            cache[key] = s;
            return s;
        }

        public static bool HasArt(string art) => Painted(art) != null;

        /// <summary>A sprite's picture: the art, else its placeholder at the sprite's aspect.</summary>
        public static Sprite For(string art, string kind, string color, float w, float h)
        {
            var painted = Painted(art);
            if (painted != null)
                return painted;
            string key = "ph:" + art + ":" + kind + ":" + color;
            if (cache.TryGetValue(key, out var s) && s != null)
                return s;
            float k = 256f / Mathf.Max(1, Mathf.Max(w, h));
            int pw = Mathf.Max(16, Mathf.RoundToInt(w * k)), ph = Mathf.Max(16, Mathf.RoundToInt(h * k));
            s = Make(FarmPainter.Paint(kind, pw, ph, color), pw, ph, art);
            cache[key] = s;
            return s;
        }

        /// <summary>A picture by its art name alone (picker cards: «farm/Ch1/Looks/PorchStone»).</summary>
        public static Sprite ByArt(string art)
        {
            var l = Layout;
            if (byArt.TryGetValue(art, out var def))
                return For(art, def.Ph, def.Color, def.W, def.H);
            return Painted(art) ?? For(art, null, null, 256, 256);
        }

        /// <summary>A speaker's portrait (character id), painted or a placeholder face.</summary>
        public static Sprite Portrait(string who)
        {
            string art = who != null && Layout.Portraits.TryGetValue(who, out var a) ? a : null;
            var painted = Painted(art);
            if (painted != null)
                return painted;
            string key = "portrait:" + who;
            if (cache.TryGetValue(key, out var s) && s != null)
                return s;
            s = Make(FarmPainter.Portrait(who, 256), 256, 256, key);
            cache[key] = s;
            return s;
        }

        public static Sprite Cloud(int i)
        {
            var fog = Layout.Fog;
            string art = fog.Count > 0 ? fog[i % fog.Count] : null;
            var painted = Painted(art);
            if (painted != null)
                return painted;
            string key = "cloud";
            if (cache.TryGetValue(key, out var s) && s != null)
                return s;
            s = Make(FarmPainter.Cloud(256, 160), 256, 160, key);
            cache[key] = s;
            return s;
        }

        public static Sprite Dust() => For(Layout.Dust ?? "Ch1/Fx/Dust", "dust", "#C9A46A", 256, 256);

        static Sprite Make(uint[] rgba, int w, int h, string name)
        {
            var px = new Color32[rgba.Length];
            for (int i = 0; i < px.Length; i++)
            {
                uint v = rgba[i];
                px[i] = new Color32((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
            }
            var tex = ShapeSprites.Upload(w, h, px);
            var s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 100, 0, SpriteMeshType.FullRect);
            s.name = name;
            return s;
        }

        /// <summary>#RRGGBB as a colour.</summary>
        public static Color32 Color(string hex) => UiView.ToColor(FarmPainter.Parse(hex, 0x86C95AFF));
    }
}

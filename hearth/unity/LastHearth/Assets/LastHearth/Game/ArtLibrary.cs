using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace LastHearth.Game
{
    /// <summary>
    /// The rendered art of Resources/Art (hearth/tools/art/render_all.py): rooms, the mountain, the
    /// map, decorations and animated sprite sheets of characters, with Art.json describing them.
    /// </summary>
    public static class ArtLibrary
    {
        public sealed class SheetInfo
        {
            public string[] Rows = new string[0];
            public int Frames = 8;
            public int Size = 192;
            public float Height = 2.2f;
        }

        public sealed class FireInfo
        {
            public int Frames;
            public float X, Y, W, H;
        }

        public sealed class Place
        {
            public string Name = "";
            public float X, Y;
        }

        public sealed class MapInfo
        {
            public List<Place> Acts = new List<Place>();
            /// <summary>Where the fortress, the arena, the hunt, the raids, the routes and the events stand (0..1 of the map).</summary>
            public Dictionary<string, Place> Places = new Dictionary<string, Place>();
        }

        public sealed class DecorInfo
        {
            public string Name = "";
            public bool Premium;
            public float Height, Width;
        }

        public sealed class Manifest
        {
            public Dictionary<string, SheetInfo> Sheets = new Dictionary<string, SheetInfo>();
            public FireInfo Fire;
            public MapInfo Map;
            public Dictionary<string, DecorInfo> Decor = new Dictionary<string, DecorInfo>();
        }

        static Manifest manifest;
        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

        public static Manifest Data
        {
            get
            {
                if (manifest == null)
                {
                    var asset = UnityEngine.Resources.Load<TextAsset>("Art/Art");
                    manifest = asset == null ? new Manifest() : JsonConvert.DeserializeObject<Manifest>(asset.text) ?? new Manifest();
                }
                return manifest;
            }
        }

        /// <summary>A texture of Resources/Art by its path ("Rooms/Kitchen"); null when the art is missing.</summary>
        public static Texture2D Tex(string path)
        {
            if (!textures.TryGetValue(path, out var t))
                textures[path] = t = UnityEngine.Resources.Load<Texture2D>("Art/" + path);
            return t;
        }

        public static void Draw(Rect r, string path)
        {
            var t = Tex(path);
            if (t != null)
                GUI.DrawTexture(r, t);
        }

        static readonly Dictionary<string, GUIStyle> sliced = new Dictionary<string, GUIStyle>();

        /// <summary>Draws a frame picture as nine slices: the corners (border pixels) keep their size, the
        /// middle stretches (windows, plates, the title plank).</summary>
        public static void DrawSliced(Rect r, string path, int border)
        {
            string key = path + "#" + border;
            if (!sliced.TryGetValue(key, out var style))
            {
                var t = Tex(path);
                if (t == null)
                    return;
                style = new GUIStyle { border = new RectOffset(border, border, border, border) };
                style.normal.background = t;
                sliced[key] = style;
            }
            GUI.Box(r, "", style);
        }

        public static SheetInfo Sheet(string name) => Data.Sheets.TryGetValue(name, out var s) ? s : null;

        /// <summary>
        /// Draws one frame of a character's animation. feet: the point under the character; height:
        /// how tall the character should look on screen. time picks the frame; flip mirrors it (left).
        /// </summary>
        public static void DrawCharacter(string sheet, string row, float time, Vector2 feet, float height, bool flip = false, float fps = 9f, bool hold = false)
        {
            var info = Sheet(sheet);
            var tex = Tex("Sheets/" + sheet);
            if (info == null || tex == null)
                return;
            int r = System.Array.IndexOf(info.Rows, row);
            if (r < 0)
                r = 0;
            int frame = (int)(time * fps);
            frame = hold ? Mathf.Min(frame, info.Frames - 1) : frame % info.Frames;
            // a frame shows the character at 1/1.5 of its height (render margin), feet at ~0.24 from the bottom
            float size = height * 1.5f;
            var rect = new Rect(feet.x - size / 2, feet.y - size * 0.76f, size, size);
            float w = 1f / info.Frames, h = 1f / info.Rows.Length;
            float u = frame * w, v = 1f - (r + 1) * h;
            GUI.DrawTextureWithTexCoords(rect, tex, flip ? new Rect(u + w, v, -w, h) : new Rect(u, v, w, h));
        }

        public static string HeroSheet(LastHearth.Core.HeroClass c) => "Hero" + c;

        public static string DwellerSheet(int dwellerId) => "Dweller" + (dwellerId * 7 + 3) % 10;

        public static string CreatureSheet(LastHearth.Core.Combat.MonsterFamily f) => "Creature" + f;
    }
}

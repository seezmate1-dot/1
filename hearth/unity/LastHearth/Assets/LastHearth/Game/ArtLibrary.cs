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
            /// <summary>Points on the painted water where the sun glints come and go (0..1 of the map; UI/Glint).</summary>
            public List<Place> Sea = new List<Place>();
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
            /// <summary>Doors between the rooms and ladders between the floors are painted (Rooms/Door, Rooms/Ladder).</summary>
            public bool Passages;
            /// <summary>A stair tower beside the cutaway joins the floors and open arches join the rooms (Rooms/Stairs,
            /// Rooms/Arch), instead of the doors and ladders.</summary>
            public bool Stairs;
            /// <summary>The rooms are painted interiors that sit in the rock's opening (Rooms/Frame).</summary>
            public bool Framed;
            /// <summary>The loading screen's painted key art is there (Splash).</summary>
            public bool Splash;
            /// <summary>Every room has pictures of its richer looks, Rooms/Type_2 and Rooms/Type_3 (RoomArt).</summary>
            public bool RoomTiers;
            /// <summary>Rubble over the rock of an obstacle cell is painted (Rooms/Rubble, transparent).</summary>
            public bool Rubble;
            /// <summary>Scaffolding over a room that is being built or upgraded is painted (Rooms/Scaffold, transparent).</summary>
            public bool Scaffold;
            /// <summary>The places on the surface around the mountain gate are painted (Scene/Merchant, Scene/Portal,
            /// Scene/Arena, Scene/Board, Scene/Pier, Scene/Pen; 512 x 512, transparent, the object on the bottom
            /// edge): they open their windows from the fortress instead of the buttons along the right edge.</summary>
            public bool Scene;
            /// <summary>The pictures of the item kinds are painted (Items/&lt;Kind&gt;, 256 x 256, transparent; ItemIcon):
            /// without them an item is a plate in its rarity's colour with the kind's short name.</summary>
            public bool ItemArt;
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

        /// <summary>Draws a frame picture as nine slices: the corners (border pixels of the picture) keep
        /// their shape, the edges and the middle stretch (windows, plates, the title plank). The frames
        /// are rendered at twice the interface's size, so a corner takes border / 2 points on screen and
        /// stays sharp on a dense phone screen.</summary>
        public static void DrawSliced(Rect r, string path, int border, float corner = -1)
        {
            var t = Tex(path);
            if (t == null)
                return;
            // a border wider than half the picture would turn the middle inside out
            border = Mathf.Min(border, Mathf.Min(t.width, t.height) / 2 - 1);
            if (corner < 0)
                corner = border / 2f;
            corner = Mathf.Min(corner, Mathf.Min(r.width, r.height) / 2);
            float bu = border / (float)t.width, bv = border / (float)t.height;
            float[] xs = { r.x, r.x + corner, r.xMax - corner, r.xMax };
            float[] ys = { r.y, r.y + corner, r.yMax - corner, r.yMax };
            float[] us = { 0, bu, 1 - bu, 1 };
            float[] vs = { 1, 1 - bv, bv, 0 }; // texture coordinates run upwards
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    float w = xs[i + 1] - xs[i], h = ys[j + 1] - ys[j];
                    if (w > 0.01f && h > 0.01f)
                        GUI.DrawTextureWithTexCoords(new Rect(xs[i], ys[j], w, h), t, new Rect(us[i], vs[j + 1], us[i + 1] - us[i], vs[j] - vs[j + 1]));
                }
            }
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

        /// <summary>The look of a room by its level: 1 plain, 2 furnished (from level 4), 3 rich (from level 8).</summary>
        public static int RoomTier(int level) => level >= 8 ? 3 : level >= 4 ? 2 : 1;

        /// <summary>The picture of a room at its level; the base picture until the tier pictures exist.</summary>
        public static string RoomArt(LastHearth.Core.RoomType type, int level)
        {
            int tier = Data.RoomTiers ? RoomTier(level) : 1;
            return "Rooms/" + type + (tier > 1 ? "_" + tier : "");
        }

        public static string HeroSheet(LastHearth.Core.HeroClass c) => "Hero" + c;

        /// <summary>The picture of an item kind (GDD 8): one per kind, drawn only when Art.json has "itemArt".</summary>
        public static string ItemIcon(LastHearth.Core.ItemKind kind) => "Items/" + kind;

        public static string DwellerSheet(int dwellerId) => "Dweller" + (dwellerId * 7 + 3) % 10;

        public static string CreatureSheet(LastHearth.Core.Combat.MonsterFamily f) => "Creature" + f;
    }
}

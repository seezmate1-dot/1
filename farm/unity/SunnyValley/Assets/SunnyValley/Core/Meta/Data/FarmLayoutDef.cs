using System.Collections.Generic;

namespace SunnyValley.Core.Meta
{
    // Where chapter 1 stands on the farm map: Resources/Meta/chapter1_layout.json, written by
    // farm/tools/art/chapter1_art.py together with the art manifest (farm/tools/art/chapter1_art.json).

    /// <summary>The farm scene of chapter 1: the map, its zones with their backgrounds and sprites.</summary>
    public sealed class FarmLayoutDef
    {
        public const string Resource = "Meta/chapter1_layout";

        public FarmMapDef Map = new FarmMapDef();
        public List<ZoneLayoutDef> Zones = new List<ZoneLayoutDef>();
        /// <summary>Cloud pictures covering the zones not open yet.</summary>
        public List<string> Fog = new List<string>();
        /// <summary>The puff of dust a task builds in.</summary>
        public string Dust;
        public string Sparkle;
        /// <summary>Character id → portrait picture.</summary>
        public Dictionary<string, string> Portraits = new Dictionary<string, string>();

        public static FarmLayoutDef Parse(string json) => MetaJson.Read<FarmLayoutDef>(json);

        public ZoneLayoutDef Zone(string id) => Zones.Find(z => z.Id == id);

        /// <summary>The sprites a task shows, hides or changes, with their zones.</summary>
        public List<(ZoneLayoutDef zone, SpriteDef sprite)> SpritesOf(string taskId)
        {
            var r = new List<(ZoneLayoutDef, SpriteDef)>();
            foreach (var z in Zones)
            foreach (var s in z.Sprites)
                if (s.Task == taskId || s.Until == taskId) r.Add((z, s));
            return r;
        }
    }

    public sealed class FarmMapDef
    {
        public float W = 5600;
        public float H = 4400;
        /// <summary>The grass around the zones, #RRGGBB.</summary>
        public string Ground = "#79B94E";
    }

    /// <summary>A zone's rectangle on the map (lower left origin), its background picture and its sprites.</summary>
    public sealed class ZoneLayoutDef
    {
        public string Id;
        public float X, Y, W, H;
        public string Background;
        /// <summary>Shapes painted for the ground until the background picture exists.</summary>
        public List<PlaceholderShapeDef> Ph = new List<PlaceholderShapeDef>();
        public List<SpriteDef> Sprites = new List<SpriteDef>();
        /// <summary>Where the camera looks at the zone, in its units (null: its centre).</summary>
        public PointDef Camera;

        public (float x, float y) CameraPoint => Camera != null ? (X + Camera.X, Y + Camera.Y) : (X + W / 2, Y + H / 2);
    }

    public sealed class PointDef
    {
        public float X, Y;
    }

    public sealed class PlaceholderShapeDef
    {
        /// <summary>"rect" or "ellipse"; X, Y its centre.</summary>
        public string Shape;
        public float X, Y, W, H;
        public string Color;
    }

    /// <summary>One picture on the farm, standing on its bottom centre (X, Y) in the zone's units.</summary>
    public sealed class SpriteDef
    {
        public const string After = "after";
        public const string Before = "before";

        public string Id;
        public string Task;
        /// <summary>"after": appears when the task is done; "before": a broken thing or junk the task removes.</summary>
        public string Show = After;
        /// <summary>An "after" sprite that goes away again when this later task is done (a tree that blooms).</summary>
        public string Until;
        public float X, Y, W, H;
        /// <summary>Draw order: larger is nearer (in front).</summary>
        public float Z;
        /// <summary>The picture (Resources/Art/&lt;Art&gt;); for a task with looks, the first look's.</summary>
        public string Art;
        /// <summary>Choice id → picture, for the tasks with three looks.</summary>
        public Dictionary<string, string> Looks;
        /// <summary>A tap on it offers «Сменить вид».</summary>
        public bool Tap;
        /// <summary>The placeholder painted until the art exists: a kind of shape and its colour.</summary>
        public string Ph;
        public string Color;

        public bool IsBefore => Show == Before;

        /// <summary>Visible on the farm given which tasks are done.</summary>
        public bool Visible(System.Func<string, bool> done)
        {
            if (IsBefore) return !done(Task);
            return done(Task) && (Until == null || !done(Until));
        }

        /// <summary>The picture for the chosen look (the default one without a choice).</summary>
        public string ArtFor(string choice) =>
            Looks != null && choice != null && Looks.TryGetValue(choice, out var a) ? a : Art;
    }
}

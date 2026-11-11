using System.Collections.Generic;
using SunnyValley.Layout;
using UnityEngine;
using LShape = SunnyValley.Layout.Shape;

namespace SunnyValley.Game.UI
{
    /// <summary>
    /// The painted interface skin (Resources/Art/UI, drawn by farm/art-candidates/fable/ui_skin.py): wooden panels,
    /// glossy buttons with a darker lower edge, dark currency plates, progress bars, round buttons, badges, board
    /// cells. <see cref="For"/> picks the sprites for a layout node by its role (shape, kind, colour, depth); a role
    /// without a sprite — or a missing file — returns nothing and UiView draws the plain code shape as before.
    ///
    /// Every rounded sprite has 64-pixel corners like <see cref="ShapeSprites.RoundRect"/>, so the same
    /// Image.pixelsPerUnitMultiplier = 64 / radius shows any corner radius. The 9-slice borders live in the table
    /// here, not in .meta files: the sprite is made from the texture with Sprite.Create, so a fresh checkout works.
    /// «Tint» sprites are grey and get multiplied by the node's colour (any colour, also changed later through
    /// UiView.Face), with a white «Gloss» sprite over them; buttons in the named colours have full-colour sprites.
    /// </summary>
    public static class UiSkin
    {
        /// <summary>Turn off to see the plain code-drawn interface (debugging, comparisons).</summary>
        public static bool Enabled = true;

        const string Root = "Art/UI/";

        /// <summary>The size a sprite was drawn at and its 9-slice border in those pixels: left, bottom, right, top.</summary>
        readonly struct Slice
        {
            public readonly int W, H;
            public readonly Vector4 Border;

            public Slice(int w, int h, float left, float bottom, float right, float top)
            {
                W = w;
                H = h;
                Border = new Vector4(left, bottom, right, top);
            }
        }

        // farm/art-candidates/fable/UI/slices.json
        static readonly Dictionary<string, Slice> Slices = new Dictionary<string, Slice>
        {
            { "ButtonYellow", new Slice(144, 164, 64, 84, 64, 64) }, { "ButtonGreen", new Slice(144, 164, 64, 84, 64, 64) },
            { "ButtonBlue", new Slice(144, 164, 64, 84, 64, 64) }, { "ButtonRed", new Slice(144, 164, 64, 84, 64, 64) },
            { "ButtonGrey", new Slice(144, 164, 64, 84, 64, 64) }, { "ButtonTint", new Slice(144, 164, 64, 84, 64, 64) },
            { "ButtonGloss", new Slice(144, 164, 64, 84, 64, 64) },
            { "Panel", new Slice(144, 162, 64, 82, 64, 64) }, { "Inset", new Slice(144, 144, 64, 64, 64, 64) },
            { "Plate", new Slice(144, 144, 64, 64, 64, 64) },
            { "BarTrack", new Slice(144, 144, 64, 64, 64, 64) }, { "BarFill", new Slice(144, 144, 64, 64, 64, 64) },
            { "BarGloss", new Slice(144, 144, 64, 64, 64, 64) },
            { "RoundTint", new Slice(256, 272, 0, 0, 0, 0) }, { "RoundGloss", new Slice(256, 272, 0, 0, 0, 0) },
            { "DotTint", new Slice(128, 128, 0, 0, 0, 0) }, { "DotGloss", new Slice(128, 128, 0, 0, 0, 0) },
            { "BoosterSlot", new Slice(256, 256, 0, 0, 0, 0) },
            { "CellLight", new Slice(128, 128, 0, 0, 0, 0) }, { "CellDark", new Slice(128, 128, 0, 0, 0, 0) },
        };

        /// <summary>The part of a round button's sprite under the disc (its darker lower edge), as a share of the disc.</summary>
        public const float RoundDrop = 16f / 256f;

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        /// <summary>A skin sprite by name, or null when the file is not there (the caller falls back to code shapes).</summary>
        public static Sprite Get(string name)
        {
            if (!Enabled || string.IsNullOrEmpty(name))
                return null;
            if (cache.TryGetValue(name, out var sprite))
                return sprite;
            // the texture itself, whether Unity imported the file as a sprite or as a plain texture
            var tex = Resources.Load<Texture2D>(Root + name);
            if (tex != null && Slices.TryGetValue(name, out var s))
            {
                // an import may have resized the picture: the border and the pixel density follow
                float k = (float)tex.width / s.W;
                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100 * k, 0,
                    SpriteMeshType.FullRect, s.Border * k);
                sprite.name = "skin." + name;
            }
            cache[name] = sprite;
            return sprite;
        }

        /// <summary>How a node is drawn with the skin.</summary>
        public struct Look
        {
            /// <summary>The main sprite; null: no skin for this node.</summary>
            public Sprite Base;
            /// <summary>White highlights over the base (never tinted), or null.</summary>
            public Sprite Gloss;
            /// <summary>The base is grey and takes the node's colour; otherwise it is drawn as painted.</summary>
            public bool Tint;
            /// <summary>9-sliced with the node's radius; otherwise stretched over the rectangle.</summary>
            public bool Sliced;
            /// <summary>How far the picture reaches under the node's rectangle (the lower edge), in canvas units.</summary>
            public float Drop;
        }

        /// <summary>The skin for a shaped node (UiView.MakeShape), by its role.</summary>
        public static Look For(UiNode n)
        {
            var look = new Look();
            if (!Enabled)
                return look;
            if (n.Shape == LShape.Circle)
            {
                // the round sprites are stretched over the node: only for square nodes (all of them are)
                if (Mathf.Abs(n.Rect.W - n.Rect.H) > 1f)
                    return look;
                if (n.Id.Contains("booster") && n.Id.EndsWith(".disc"))
                {
                    // a booster's slot: a wooden square frame under the tool instead of a coloured disc
                    look.Base = Get("BoosterSlot");
                }
                else if (n.Depth > 0)
                {
                    look.Base = Get("RoundTint");
                    look.Gloss = Get("RoundGloss");
                    look.Tint = true;
                    look.Drop = n.Rect.H * RoundDrop;
                }
                else
                {
                    look.Base = Get("DotTint");
                    look.Gloss = Get("DotGloss");
                    look.Tint = true;
                }
                return look;
            }
            if (n.Shape != LShape.RoundRect)
                return look;

            look.Sliced = true;
            if (n.Kind == NodeKind.Bar)
            {
                look.Base = Get("BarTrack");
            }
            else if (n.Color == Tokens.Plate)
            {
                look.Base = Get("Plate");
            }
            else if (n.Color == Tokens.Wood && n.Depth > 0)
            {
                look.Base = Get("Panel");
                look.Drop = n.Depth;
            }
            else if (n.Depth > 0)
            {
                look.Drop = n.Depth;
                // only real buttons get a painted colour: panels with a depth (toggle tracks, cards) change colour later
                string named = n.Kind == NodeKind.Button ? ButtonName(n.Color) : null;
                look.Base = named != null ? Get(named) : null;
                if (look.Base == null)
                {
                    look.Base = Get("ButtonTint");
                    look.Gloss = Get("ButtonGloss");
                    look.Tint = true;
                }
            }
            else if (n.Kind == NodeKind.Panel && (n.Color & 0xFF) == 0xFF)
            {
                // an opaque flat panel inside a popup: a sunken well in its colour
                look.Base = Get("Inset");
                look.Tint = true;
            }
            return look;
        }

        static string ButtonName(uint color)
        {
            switch (color)
            {
                case Tokens.Yellow: return "ButtonYellow";
                case Tokens.Green: return "ButtonGreen";
                case Tokens.Blue: return "ButtonBlue";
                case Tokens.Red: return "ButtonRed";
                case Tokens.TrackOff: return "ButtonGrey";
                default: return null;
            }
        }

        /// <summary>Loads every sprite now (during loading, not when the first popup opens).</summary>
        public static void Warm()
        {
            foreach (var name in new List<string>(Slices.Keys))
                Get(name);
        }
    }
}

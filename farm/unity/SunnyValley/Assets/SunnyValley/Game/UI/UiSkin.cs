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
            { "ButtonYellow", new Slice(144, 180, 64, 100, 64, 64) }, { "ButtonGreen", new Slice(144, 180, 64, 100, 64, 64) },
            { "ButtonBlue", new Slice(144, 180, 64, 100, 64, 64) }, { "ButtonRed", new Slice(144, 180, 64, 100, 64, 64) },
            { "ButtonGrey", new Slice(144, 180, 64, 100, 64, 64) }, { "ButtonTint", new Slice(144, 180, 64, 100, 64, 64) },
            { "ButtonGloss", new Slice(144, 180, 64, 100, 64, 64) },
            { "Panel", new Slice(144, 174, 64, 94, 64, 64) }, { "PanelDark", new Slice(144, 174, 64, 94, 64, 64) },
            { "Inset", new Slice(144, 144, 64, 64, 64, 64) },
            { "Ribbon", new Slice(220, 112, 76, 0, 76, 0) }, { "Rays", new Slice(256, 256, 0, 0, 0, 0) },
            { "Shine", new Slice(128, 128, 0, 0, 0, 0) },
            { "Plate", new Slice(144, 144, 64, 64, 64, 64) },
            { "BarTrack", new Slice(144, 144, 64, 64, 64, 64) }, { "BarFill", new Slice(144, 144, 64, 64, 64, 64) },
            { "BarGloss", new Slice(144, 144, 64, 64, 64, 64) },
            { "RoundTint", new Slice(256, 272, 0, 0, 0, 0) }, { "RoundGloss", new Slice(256, 272, 0, 0, 0, 0) },
            { "DotTint", new Slice(128, 128, 0, 0, 0, 0) }, { "DotGloss", new Slice(128, 128, 0, 0, 0, 0) },
            { "BoosterSlot", new Slice(256, 256, 0, 0, 0, 0) },
            { "CellLight", new Slice(128, 128, 0, 0, 0, 0) }, { "CellDark", new Slice(128, 128, 0, 0, 0, 0) },
        };

        /// <summary>The soft shadow painted under buttons and panels, in sprite pixels (at 64-pixel corners).</summary>
        const float ShadowPixels = 10;

        static float Shadow(UiNode n) => ShadowPixels * Mathf.Min(n.Radius, n.Rect.H / 2) / ShapeSprites.SliceRadius;

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
                // the moves counter is a dark well in a wooden rim (light digits on it), the rest light wood
                look.Base = IsMoves(n) ? Get("PanelDark") : null;
                if (look.Base == null)
                    look.Base = Get("Panel");
                look.Drop = n.Depth + Shadow(n);
            }
            else if (n.Depth > 0)
            {
                look.Drop = n.Depth + Shadow(n);
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

        static bool IsMoves(UiNode n) => n != null && n.Id.EndsWith(".moves");

        /// <summary>The main button of a screen (the big yellow «Играть»): a light sweeps across it now and then.</summary>
        public static bool IsMainButton(UiNode n) =>
            n.Kind == NodeKind.Button && n.Shape == LShape.RoundRect && n.Color == Tokens.Yellow && n.Rect.H >= Tokens.PlayHeight - 1;

        // ------------------------------------------------------------------ icons

        /// <summary>
        /// How much larger than its layout rectangle an icon is drawn, and where it moves (canvas units): in a polished
        /// casual game the icon dominates its button and breaks out of the frame, the disc behind it is secondary.
        /// Only the picture grows — the layout, the tap areas and the tests' geometry stay as they are.
        /// </summary>
        public static float IconBoost(UiNode n, out Vector2 shift)
        {
            shift = Vector2.zero;
            var p = n.Parent;
            if (!Enabled || p == null || n.Kind != NodeKind.Icon)
                return 1;
            float h = n.Rect.H;
            if (p.Shape == LShape.Circle && p.Id.EndsWith(".disc"))
            {
                if (p.Id.Contains("booster"))
                {
                    // a tool stands in its slot and sticks out of the top
                    shift = new Vector2(0, h * 0.14f);
                    return 1.5f;
                }
                shift = new Vector2(0, h * 0.05f);
                return 1.36f;
            }
            if (p.Shape == LShape.Circle && p.Kind == NodeKind.Button)
                return 1.14f;
            if (p.Shape == LShape.RoundRect && p.Color == Tokens.Plate && p.Id.EndsWith(".bg"))
            {
                // a currency's icon is taller than its pill and hangs over the left end
                shift = new Vector2(-h * 0.08f, 0);
                return 1.34f;
            }
            if (n.Id.Contains(".goal") && n.Id.EndsWith(".icon"))
            {
                shift = new Vector2(0, -h * 0.03f);
                return 1.16f;
            }
            return 1;
        }

        /// <summary>A big earned star gets golden rays behind it.</summary>
        public static Sprite RaysFor(UiNode n) =>
            Enabled && n.Kind == NodeKind.Icon && n.Icon == Icons.Star && n.Rect.W >= 150 ? Get("Rays") : null;

        // ------------------------------------------------------------------ backings: pictures under a label

        /// <summary>A popup's title sits on a ribbon.</summary>
        public static bool IsRibbonTitle(UiNode n) =>
            n.Kind == NodeKind.Label && n.Id.EndsWith(".panel.title") && n.Rect.W >= 2 * 76 + 40 && Get("Ribbon") != null;

        /// <summary>A goal's counter sits in a small dark pill.</summary>
        public static bool IsGoalCount(UiNode n) =>
            n.Kind == NodeKind.Label && n.Id.EndsWith(".count") && n.Parent != null && n.Parent.Id.Contains(".goal") && Get("Plate") != null;

        // ------------------------------------------------------------------ text

        /// <summary>How a label is drawn: its colour, and whether it gets a thick dark stroke with a drop shadow.</summary>
        public struct TextLook
        {
            public Color32 Color;
            public bool Stroke;
            public Color32 StrokeColor;
        }

        static readonly Color32 StrokeBrown = new Color32(62, 34, 10, 255);

        /// <summary>
        /// Light text on pictures gets a thick stroke in a dark shade of what is under it (dark orange on the yellow
        /// button, dark green on the green one, …) and a drop shadow, like the chunky captions of casual games; labels
        /// on a ribbon, in a goal pill and on the dark moves counter turn light.
        /// </summary>
        public static TextLook TextFor(UiNode n)
        {
            var t = n.Text;
            var look = new TextLook { Color = UiView.ToColor(t.Color), Stroke = t.Outline, StrokeColor = StrokeBrown };
            if (!Enabled)
                return look;
            if (IsRibbonTitle(n))
            {
                look.Color = new Color32(255, 255, 255, 255);
                look.Stroke = true;
                look.StrokeColor = new Color32(16, 58, 110, 255);
                return look;
            }
            if (IsGoalCount(n))
            {
                look.Color = new Color32(255, 255, 255, 255);
                look.Stroke = false;
                return look;
            }
            if (IsMoves(n.Parent) && Get("PanelDark") != null)
            {
                bool value = n.Id.EndsWith(".value");
                look.Color = value ? new Color32(255, 255, 255, 255) : new Color32(255, 222, 160, 255);
                look.Stroke = value;
                return look;
            }
            if (look.Stroke)
            {
                for (var p = n.Parent; p != null; p = p.Parent)
                {
                    if (p.Shape == LShape.None)
                        continue;
                    if (p.Kind == NodeKind.Button)
                    {
                        switch (p.Color)
                        {
                            case Tokens.Yellow: look.StrokeColor = new Color32(150, 78, 0, 255); break;
                            case Tokens.Green: look.StrokeColor = new Color32(28, 88, 16, 255); break;
                            case Tokens.Blue: look.StrokeColor = new Color32(16, 58, 110, 255); break;
                            case Tokens.Red: look.StrokeColor = new Color32(110, 20, 8, 255); break;
                        }
                    }
                    break;
                }
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

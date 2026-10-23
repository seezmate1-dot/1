using System;

namespace SunnyValley.Layout
{
    /// <summary>
    /// The building blocks every screen is made of: labels, buttons with a label and an icon, round icon buttons,
    /// side buttons with a caption, currency plates, progress bars, badges, panels. Each returns a node with its
    /// children placed inside it; the caller only positions the block. Sizes and colours come from Tokens.
    /// </summary>
    public static class Kit
    {
        public static UiNode Group(string id) => new UiNode(id, NodeKind.Group);

        public static UiNode Label(string id, string text, float size, float min, uint color = Tokens.TextDark,
                                   int lines = 1, TextAlign align = TextAlign.Center, bool outline = false)
        {
            return new UiNode(id, NodeKind.Label)
            {
                Text = new TextSpec
                {
                    Value = text, Size = size, MinSize = min, MaxLines = lines, Align = align, Color = color, Outline = outline,
                },
            };
        }

        public static UiNode Panel(string id, uint color, float radius = Tokens.RadiusLarge, float depth = 0, uint depthColor = Tokens.WoodDark)
        {
            return new UiNode(id, NodeKind.Panel)
            {
                Shape = radius > 0 ? Shape.RoundRect : Shape.None, Radius = radius, Color = color, Depth = depth, DepthColor = depthColor,
            };
        }

        public static UiNode Icon(string id, string icon) => new UiNode(id, NodeKind.Icon) { Icon = icon };

        /// <summary>A round coloured disc with an icon on it.</summary>
        public static UiNode Disc(string id, string icon, uint color)
        {
            var disc = new UiNode(id, NodeKind.Panel) { Shape = Shape.Circle, Color = color, Depth = 6, DepthColor = Tokens.Shadow };
            disc.Add(Icon(id + ".icon", icon)).Stretch(18, 18, 18, 18);
            return disc;
        }

        /// <summary>
        /// A big rounded button with a label and, optionally, an icon before it. The button's own rectangle is what
        /// a finger hits; the label fills what the icon leaves.
        /// </summary>
        public static UiNode Button(string id, string text, uint color, uint shade, float height, string icon = null,
                                    float fontSize = Tokens.FontButton, float fontMin = Tokens.FontButtonMin, uint textColor = Tokens.TextLight)
        {
            var b = new UiNode(id, NodeKind.Button)
            {
                Shape = Shape.RoundRect, Radius = Math.Min(Tokens.RadiusLarge, height / 2), Color = color, Depth = 12, DepthColor = shade,
            };
            float pad = Math.Max(24, height * 0.18f);
            float left = pad;
            if (icon != null)
            {
                float s = height * 0.56f;
                b.Add(Icon(id + ".icon", icon)).LeftMiddle(pad, 4, s, s);
                left = pad + s + 12;
            }
            b.Add(Label(id + ".label", text, fontSize, fontMin, textColor, 1, TextAlign.Center, outline: true))
                .Stretch(left, pad, 10, 18);
            return b;
        }

        /// <summary>An icon-only round button (settings, pause, close).</summary>
        public static UiNode RoundButton(string id, string icon, uint color, uint shade)
        {
            var b = new UiNode(id, NodeKind.Button) { Shape = Shape.Circle, Color = color, Depth = 10, DepthColor = shade };
            b.Add(Icon(id + ".icon", icon)).Stretch(26, 26, 22, 30);
            return b;
        }

        /// <summary>A side button of the farm screen: a round icon with a caption under it, both inside the tap area.</summary>
        public static UiNode SideButton(string id, string icon, uint color, string caption)
        {
            var b = new UiNode(id, NodeKind.Button);
            float w = Tokens.SideButtonWidth, icon0 = Tokens.SideIcon;
            var disc = b.Add(Disc(id + ".disc", icon, color));
            disc.TopCenter(0, 0, icon0, icon0);
            b.Add(Label(id + ".label", caption, Tokens.FontCaption, Tokens.FontCaptionMin, Tokens.TextLight, outline: true))
                .StretchBottom(0, 0, 0, Tokens.SideButtonHeight - icon0 - 4);
            b.Add(Badge(id + ".badge", "")).TopRight((w - icon0) / 2 - 6, 0, Tokens.BadgeSize, Tokens.BadgeSize).Flags |= NodeFlags.Hidden;
            return b;
        }

        /// <summary>A red counter dot on the corner of a button.</summary>
        public static UiNode Badge(string id, string text)
        {
            var badge = new UiNode(id, NodeKind.Panel) { Shape = Shape.Circle, Color = Tokens.Red };
            badge.Add(Label(id + ".label", text, Tokens.FontBadge, Tokens.FontBadgeMin, Tokens.TextLight)).Stretch(4, 4, 4, 6);
            return badge;
        }

        /// <summary>
        /// A currency plate: a dark pill with the currency's round icon on the left and the amount on the right.
        /// With iconText, the number sits on the icon (lives: «5» on the heart) and the label shows a timer.
        /// </summary>
        public static UiNode Plate(string id, string icon, string text, string iconText = null)
        {
            // the tap area is taller than the pill a finger sees: 44 pt even on a dense 720p phone
            var plate = new UiNode(id, NodeKind.Plate);
            float h = Tokens.PlateHeight, s = h - 16;
            var pill = plate.Add(new UiNode(id + ".bg", NodeKind.Panel) { Shape = Shape.RoundRect, Radius = h / 2, Color = Tokens.Plate });
            pill.StretchMiddle(0, 0, h);
            var ic = pill.Add(Icon(id + ".icon", icon));
            ic.LeftMiddle(6, 0, s, s);
            if (iconText != null)
                ic.Add(Label(id + ".count", iconText, Tokens.FontValue, Tokens.FontValueMin, Tokens.TextLight, outline: true)).Stretch(6, 6, 10, 14);
            pill.Add(Label(id + ".value", text, Tokens.FontValue, Tokens.FontValueMin, Tokens.TextLight, 1, TextAlign.Center))
                .Stretch(s + 12, 14, 8, 10);
            return plate;
        }

        /// <summary>A progress bar: a dark track with a rounded fill (Value 0..1) and an optional label over it.</summary>
        public static UiNode Bar(string id, float value, string text = null)
        {
            var bar = new UiNode(id, NodeKind.Bar)
            {
                Shape = Shape.RoundRect, Radius = Tokens.ProgressHeight / 2, Color = Tokens.BarTrack, Value = value,
                DepthColor = Tokens.Green,
            };
            if (text != null)
                bar.Add(Label(id + ".label", text, Tokens.FontCaption, Tokens.FontCaptionMin, Tokens.TextLight, outline: true)).Stretch(16, 16, 4, 6);
            return bar;
        }

        /// <summary>An on/off switch: the tap area is big, the track and knob inside it smaller.</summary>
        public static UiNode Toggle(string id, bool on)
        {
            var t = new UiNode(id, NodeKind.Toggle) { Value = on ? 1 : 0 };
            var track = t.Add(new UiNode(id + ".track", NodeKind.Panel)
            {
                Shape = Shape.RoundRect, Radius = 44, Color = on ? Tokens.Green : Tokens.TrackOff, Depth = 6, DepthColor = Tokens.Shadow,
            });
            track.Center(0, 0, 168, 88);
            track.Add(new UiNode(id + ".knob", NodeKind.Panel) { Shape = Shape.Circle, Color = Tokens.White })
                .At(on ? 1 : 0, 0.5f, 8, 0, 72, 72);
            return t;
        }

        /// <summary>A popup's frame: a wooden panel with a title on top and, optionally, a close button in the corner.</summary>
        public static UiNode PopupPanel(string id, string title, bool closable)
        {
            var panel = Panel(id, Tokens.Wood, Tokens.RadiusLarge, 14, Tokens.WoodDark);
            float side = closable ? Tokens.CloseButton + Tokens.Gap : Tokens.Pad;
            panel.Add(Label(id + ".title", title, Tokens.FontTitle, Tokens.FontTitleMin, Tokens.TextDark))
                .StretchTop(side, side, Tokens.Pad - 8, Tokens.CloseButton);
            if (closable)
                panel.Add(RoundButton(id + ".close", Icons.Close, Tokens.Red, Tokens.RedShade))
                    .TopRight(Tokens.Gap, Tokens.Gap, Tokens.CloseButton, Tokens.CloseButton);
            return panel;
        }

        /// <summary>The space a popup's title row takes from the top of the panel.</summary>
        public const float PopupHeader = Tokens.Pad - 8 + Tokens.CloseButton;
    }

    /// <summary>Names of the icons; the Unity side draws placeholders for them until the art lands.</summary>
    public static class Icons
    {
        public const string Coin = "coin";
        public const string Star = "star";
        public const string Crystal = "crystal";
        public const string Heart = "heart";
        public const string Tasks = "tasks";
        public const string Events = "events";
        public const string Orders = "orders";
        public const string Shop = "shop";
        public const string Settings = "settings";
        public const string Pause = "pause";
        public const string Close = "close";
        public const string Shovel = "shovel";
        public const string Rake = "rake";
        public const string Can = "can";
        public const string Shuffle = "shuffle";
        public const string Pitchfork = "pitchfork";
        public const string Bomb = "bomb";
        public const string Rainbow = "rainbow";
        public const string Sun = "sun";
        public const string Lock = "lock";
        public const string Check = "check";

        // the farm economy (chapter 2)
        public const string Fields = "fields";
        public const string Animals = "animals";
        public const string Workshop = "workshop";
        public const string Barn = "barn";
        public const string Town = "town";
        public const string Truck = "truck";
        public const string Xp = "xp";
        public const string People = "people";
        public const string Lot = "lot";
        public const string Beauty = "beauty";
        public const string Soil = "soil";
        public const string Plus = "plus";
        public const string Clock = "clock";

        /// <summary>A good of the barn by its id: the board's own pieces keep their icons, the rest are «item.&lt;id&gt;».</summary>
        public static string Item(string id) => Array.IndexOf(Pieces, id) >= 0 ? id : "item." + id;

        /// <summary>A tool (hammer, nails, paint, saw, axe).</summary>
        public static string Tool(string id) => "tool." + id;

        /// <summary>The six pieces of the board (GDD §4.1), also goal icons.</summary>
        public static readonly string[] Pieces = { "tomato", "corn", "apple", "carrot", "eggplant", "blueberry" };
    }

    /// <summary>How numbers and times read on the plates.</summary>
    public static class Format
    {
        /// <summary>12 345 under a hundred thousand; then 123К, 1,2М: never more than six characters.</summary>
        public static string Amount(long v)
        {
            if (v < 0)
                v = 0;
            if (v < 100_000)
                return Group(v);
            if (v < 1_000_000)
                return (v / 1000) + "К";
            if (v < 10_000_000)
                return (v / 100_000 / 10.0).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',') + "М";
            return Math.Min(v / 1_000_000, 999) + "М";
        }

        /// <summary>Thousands separated by a thin non-breaking space: 12 345.</summary>
        public static string Group(long v)
        {
            string s = v.ToString(System.Globalization.CultureInfo.InvariantCulture);
            for (int i = s.Length - 3; i > 0; i -= 3)
                s = s.Insert(i, " ");
            return s;
        }

        /// <summary>29:59, or 1:05:00 for an hour and more.</summary>
        public static string Timer(long seconds)
        {
            if (seconds < 0)
                seconds = 0;
            long h = seconds / 3600, m = seconds / 60 % 60, s = seconds % 60;
            return h > 0 ? $"{h}:{m:00}:{s:00}" : $"{m}:{s:00}";
        }
    }
}

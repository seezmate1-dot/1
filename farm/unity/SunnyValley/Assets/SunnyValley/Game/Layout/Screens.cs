using System;
using System.Collections.Generic;

namespace SunnyValley.Layout
{
    /// <summary>
    /// The layouts of the full screens: loading, the farm and the level. Rules: the top row hangs from the top of the
    /// safe area, the bottom row stands on its bottom, the middle takes what is left; nothing interactive leaves the
    /// safe area; sizes come from Tokens and shrink only where a rule says so (plates share the width, the board
    /// takes the largest whole cell that fits).
    /// </summary>
    public static class Screens
    {
        // ------------------------------------------------------------------ loading

        public static UiLayout Loading(CanvasFit fit, float progress = 0.35f, string status = "Загрузка…")
        {
            var L = UiLayout.Begin("loading", fit);
            L.Root.Add(Kit.Panel("loading.bg", Tokens.Sky, 0)).Stretch().Flags |= NodeFlags.FullBleed;
            var hills = L.Root.Add(Kit.Panel("loading.hills", Tokens.Grass, 0));
            hills.StretchBottom(0, 0, 0, fit.Height * 0.32f);
            hills.Flags |= NodeFlags.FullBleed;

            float W = L.W, H = L.H;
            float sun = Math.Min(320, W * 0.3f);
            float top = H * 0.12f;
            L.Safe.Add(Kit.Icon("loading.sun", Icons.Sun)).TopCenter(0, top, sun, sun);
            float logoH = 2 * FontMetrics.LineHeight(Tokens.FontLogo) + 8;
            L.Safe.Add(Kit.Label("loading.logo", "Солнечная долина", Tokens.FontLogo, Tokens.FontLogoMin, Tokens.TextLight, 2,
                    outline: true))
                .TopCenter(0, top + sun + Tokens.GapBig, Math.Min(W - 128, 960), logoH);

            float barW = Math.Min(W - 192, 820);
            float barY = H * 0.12f;
            L.Safe.Add(Kit.Bar("loading.bar", progress, Percent(progress))).BottomCenter(0, barY, barW, Tokens.ProgressHeight);
            L.Safe.Add(Kit.Label("loading.status", status, Tokens.FontBody, Tokens.FontBodyMin, Tokens.TextLight, outline: true))
                .BottomCenter(0, barY + Tokens.ProgressHeight + Tokens.Gap, W - 128, 64);
            return L.Finish();
        }

        public static string Percent(float p) => (int)Math.Round(Math.Max(0, Math.Min(1, p)) * 100) + "%";

        // ------------------------------------------------------------------ a toast

        /// <summary>A short message in a dark pill under the top row, up to two lines; it takes no taps.</summary>
        public static UiLayout Toast(CanvasFit fit, string text)
        {
            var L = UiLayout.Begin("toast", fit);
            float w = Math.Min(L.W - 2 * Tokens.Margin, 860);
            float h = 2 * FontMetrics.LineHeight(Tokens.FontBody) + 40;
            var pill = L.Safe.Add(Kit.Panel("toast.pill", 0x2B1D10E0, Tokens.RadiusLarge));
            pill.TopCenter(0, Tokens.Margin + Tokens.PlateTouch + Tokens.Gap, w, h);
            pill.Add(Kit.Label("toast.label", text, Tokens.FontBody, Tokens.FontBodyMin, Tokens.TextLight, 2)).Stretch(32, 32, 16, 16);
            return L.Finish();
        }

        // ------------------------------------------------------------------ the farm

        public static UiLayout Farm(CanvasFit fit, FarmHud hud)
        {
            var L = UiLayout.Begin("farm", fit);
            L.Root.Add(new UiNode("farm.world", NodeKind.World) { Color = Tokens.Grass, Flags = NodeFlags.FullBleed }).Stretch();

            float W = L.W, M = Tokens.Margin;

            // top: four currency plates sharing the width, centred when they reach their largest size (tablets)
            var top = L.Safe.Add(Kit.Group("farm.top")).StretchTop(M, M, M, Tokens.PlateTouch);
            float rowW = W - 2 * M;
            float pw = Math.Min((rowW - 3 * Tokens.Gap) / 4, Tokens.PlateMaxWidth);
            float x0 = (rowW - (4 * pw + 3 * Tokens.Gap)) / 2;
            bool full = hud.Lives >= hud.MaxLives && !hud.InfiniteLives;
            var plates = new[]
            {
                Kit.Plate("farm.coins", Icons.Coin, Format.Amount(hud.Coins)),
                Kit.Plate("farm.stars", Icons.Star, Format.Amount(hud.Stars)),
                Kit.Plate("farm.crystals", Icons.Crystal, Format.Amount(hud.Crystals)),
                Kit.Plate("farm.lives", Icons.Heart, full ? "Полные" : Format.Timer(hud.NextLifeSeconds), hud.Lives.ToString()),
            };
            for (int i = 0; i < plates.Length; i++)
                top.Add(plates[i]).TopLeft(x0 + i * (pw + Tokens.Gap), 0, pw, Tokens.PlateTouch);

            // the side columns under the plates: tasks and events on the left; settings, shop and orders on the right
            float colTop = M + Tokens.PlateTouch + Tokens.Gap;
            float step = Tokens.SideButtonHeight + Tokens.Gap;
            SideButton(L.Safe, "farm.tasks", Icons.Tasks, Tokens.Green, "Задания", hud.TasksBadge).TopLeft(M, colTop, Tokens.SideButtonWidth, Tokens.SideButtonHeight);
            SideButton(L.Safe, "farm.events", Icons.Events, Tokens.Purple, "События", hud.EventsBadge).TopLeft(M, colTop + step, Tokens.SideButtonWidth, Tokens.SideButtonHeight);

            float inset = (Tokens.SideButtonWidth - Tokens.RoundButton) / 2;
            L.Safe.Add(Kit.RoundButton("farm.settings", Icons.Settings, Tokens.Blue, Tokens.BlueShade))
                .TopRight(M + inset, colTop, Tokens.RoundButton, Tokens.RoundButton);
            float rightTop = colTop + Tokens.RoundButton + Tokens.Gap;
            SideButton(L.Safe, "farm.shop", Icons.Shop, Tokens.Orange, "Магазин", hud.ShopBadge).TopRight(M, rightTop, Tokens.SideButtonWidth, Tokens.SideButtonHeight);
            SideButton(L.Safe, "farm.orders", Icons.Orders, Tokens.Soil, "Заказы", hud.OrdersBadge).TopRight(M, rightTop + step, Tokens.SideButtonWidth, Tokens.SideButtonHeight);

            // bottom: the big yellow «Играть N»
            float playW = Math.Min(W - 2 * M, Math.Max(Tokens.PlayMinWidth, Math.Min(Tokens.PlayMaxWidth, W * 0.64f)));
            L.Safe.Add(Kit.Button("farm.play", "Играть " + hud.Level, Tokens.Yellow, Tokens.YellowShade, Tokens.PlayHeight,
                    fontSize: Tokens.FontPlay, fontMin: Tokens.FontPlayMin))
                .BottomCenter(0, M + Tokens.Gap, playW, Tokens.PlayHeight);

            // over it: the zone being restored with its progress (a tap shows it on the farm)
            if (hud.ZoneName != null)
            {
                float zw = Math.Min(W - 2 * M, ZoneStripWidth);
                var zone = L.Safe.Add(new UiNode("farm.zone", NodeKind.Button)
                {
                    Shape = Shape.RoundRect, Radius = Tokens.RadiusMedium, Color = Tokens.Plate,
                });
                zone.BottomCenter(0, M + Tokens.Gap + Tokens.PlayHeight + Tokens.GapBig, zw, ZoneStripHeight);
                zone.Add(Kit.Label("farm.zone.name", hud.ZoneName, Tokens.FontCaption, Tokens.FontCaptionMin, Tokens.TextLight, outline: true))
                    .StretchTop(24, 24, 8, 52);
                float value = hud.ZoneTotal > 0 ? (float)hud.ZoneDone / hud.ZoneTotal : 1;
                zone.Add(Kit.Bar("farm.zone.bar", value, ZoneProgress(hud.ZoneDone, hud.ZoneTotal)))
                    .StretchBottom(24, 24, 16, Tokens.ProgressHeight);
            }
            return L.Finish();
        }

        public const float ZoneStripWidth = 640;
        public const float ZoneStripHeight = 140;

        public static string ZoneProgress(int done, int total) => done + " из " + total;

        static UiNode SideButton(UiNode parent, string id, string icon, uint color, string caption, int badge)
        {
            var b = parent.Add(Kit.SideButton(id, icon, color, caption));
            SetBadge(b.Find(id + ".badge"), badge);
            return b;
        }

        /// <summary>Shows a badge with a number, or hides it for 0.</summary>
        public static void SetBadge(UiNode badge, int count)
        {
            if (badge == null)
                return;
            if (count > 0)
                badge.Flags &= ~NodeFlags.Hidden;
            else
                badge.Flags |= NodeFlags.Hidden;
            badge.Children[0].Text.Value = count > 99 ? "99" : count.ToString();
        }

        // ------------------------------------------------------------------ the level

        public static readonly string[] BoosterIcons = { Icons.Shovel, Icons.Rake, Icons.Can, Icons.Shuffle };
        public static readonly string[] BoosterNames = { "Лопата", "Грабли", "Лейка", "Перемешать" };
        public static readonly uint[] BoosterColors = { Tokens.Soil, Tokens.Green, Tokens.Blue, Tokens.Purple };

        public static UiLayout Level(CanvasFit fit, LevelHud hud)
        {
            var L = UiLayout.Begin("level", fit);
            L.Root.Add(Kit.Panel("level.bg", Tokens.GrassDark, 0)).Stretch().Flags |= NodeFlags.FullBleed;

            float W = L.W, H = L.H, M = Tokens.Margin;
            float topH = Tokens.LevelTopHeight;

            // top: pause, the goals panel, the moves counter
            var top = L.Safe.Add(Kit.Group("level.top")).StretchTop(M, M, M, topH);
            float rowW = W - 2 * M;
            top.Add(Kit.RoundButton("level.pause", Icons.Pause, Tokens.Blue, Tokens.BlueShade))
                .LeftMiddle(0, 0, Tokens.RoundButton, Tokens.RoundButton);
            float movesW = 220;
            var moves = top.Add(Kit.Panel("level.moves", Tokens.Wood, Tokens.RadiusLarge, 10, Tokens.WoodDark));
            moves.TopRight(0, 0, movesW, topH);
            moves.Add(Kit.Label("level.moves.caption", "Ходы", Tokens.FontCaption, Tokens.FontCaptionMin, Tokens.TextMuted)).StretchTop(12, 12, 14, 50);
            moves.Add(Kit.Label("level.moves.value", hud.Moves.ToString(), Tokens.FontBig, Tokens.FontBigMin, Tokens.TextDark)).Stretch(12, 12, 62, 18);

            float goalsW = rowW - Tokens.RoundButton - movesW - 2 * Tokens.Gap;
            var goals = top.Add(Kit.Panel("level.goals", Tokens.Wood, Tokens.RadiusLarge, 10, Tokens.WoodDark));
            goals.TopLeft(Tokens.RoundButton + Tokens.Gap, 0, goalsW, topH);
            goals.Add(Kit.Label("level.goals.caption", "Цели", Tokens.FontCaption, Tokens.FontCaptionMin, Tokens.TextMuted)).StretchTop(12, 12, 14, 50);
            GoalSlots(goals.Add(Kit.Group("level.goals.slots")).Stretch(12, 12, 62, 18), "level.goal", hud.Goals);

            // bottom: in-level boosters, evenly spread, centred
            var boosters = L.Safe.Add(Kit.Group("level.boosters")).StretchBottom(M, M, M, Tokens.BoosterHeight);
            int n = BoosterIcons.Length;
            float bw = Tokens.BoosterWidth;
            float gap = Math.Min(Tokens.BoosterMaxGap, (rowW - n * bw) / (n + 1));
            float bx = (rowW - (n * bw + (n - 1) * gap)) / 2;
            for (int i = 0; i < n; i++)
            {
                int count = hud.Boosters != null && i < hud.Boosters.Length ? hud.Boosters[i] : 0;
                boosters.Add(Booster("level.booster" + i, BoosterIcons[i], BoosterColors[i], BoosterNames[i], count))
                    .TopLeft(bx + i * (bw + gap), 0, bw, Tokens.BoosterHeight);
            }

            // middle: the board, the largest whole cell that fits between the two rows and the side margins
            float areaTop = M + topH + Tokens.GapBig;
            float areaBottom = M + Tokens.BoosterHeight + Tokens.GapBig;
            float areaH = H - areaTop - areaBottom;
            float areaW = W - 2 * Tokens.BoardMargin;
            int cols = Math.Max(1, hud.Cols), rows = Math.Max(1, hud.Rows);
            float cell = (float)Math.Floor(Math.Min(Math.Min(areaW / cols, areaH / rows), Tokens.MaxCell));
            float bwid = cell * cols, bhei = cell * rows;
            float dy = areaBottom + areaH / 2 - H / 2;
            L.Safe.Add(new UiNode("level.board", NodeKind.Board)
            {
                Cols = cols, Rows = rows, Cell = cell, Shape = Shape.RoundRect, Radius = Tokens.RadiusMedium, Color = 0x2E5A1E80,
            }).Center(0, dy, bwid, bhei);
            return L.Finish();
        }

        /// <summary>A booster button: round icon, how many are left on a badge, the name under it.</summary>
        public static UiNode Booster(string id, string icon, uint color, string caption, int count)
        {
            var b = new UiNode(id, NodeKind.Button);
            float s = Tokens.SideIcon;
            b.Add(Kit.Disc(id + ".disc", icon, color)).TopCenter(0, 4, s, s);
            b.Add(Kit.Label(id + ".label", caption, Tokens.FontCaption, Tokens.FontCaptionMin, Tokens.TextLight, outline: true))
                .StretchBottom(0, 0, 0, Tokens.BoosterHeight - s - 8);
            var badge = b.Add(count > 0 ? Kit.Badge(id + ".badge", count > 99 ? "99" : count.ToString()) : PlusBadge(id + ".badge"));
            badge.TopRight(0, 0, Tokens.BadgeSize, Tokens.BadgeSize);
            return b;
        }

        /// <summary>A green «+» instead of a counter: none left, tap to buy.</summary>
        static UiNode PlusBadge(string id)
        {
            var badge = new UiNode(id, NodeKind.Panel) { Shape = Shape.Circle, Color = Tokens.Green };
            badge.Add(Kit.Label(id + ".label", "+", Tokens.FontBadge, Tokens.FontBadgeMin, Tokens.TextLight)).Stretch(4, 4, 4, 6);
            return badge;
        }

        /// <summary>Goal slots: each an icon with the count under it, sharing the row with proportional anchors.</summary>
        public static void GoalSlots(UiNode row, string id, List<Goal> goals)
        {
            int n = Math.Max(1, goals?.Count ?? 0);
            for (int i = 0; i < n; i++)
            {
                var goal = goals != null && i < goals.Count ? goals[i] : new Goal(Icons.Pieces[0], 0);
                var slot = row.Add(Kit.Group(id + i));
                Slice(slot, i, n, 8);
                slot.Add(Kit.Icon(id + i + ".icon", goal.Icon)).TopCenter(0, 0, 66, 66);
                slot.Add(Kit.Label(id + i + ".count", Math.Max(0, goal.Count).ToString(), Tokens.FontCaption, Tokens.FontCaptionMin,
                    Tokens.TextDark)).StretchBottom(0, 0, 0, 50);
            }
        }

        /// <summary>The i-th of n equal columns of the parent, by anchors (it follows the parent when it resizes).</summary>
        public static UiNode Slice(UiNode node, int i, int n, float gap)
        {
            node.AnchorMin = new V2((float)i / n, 0);
            node.AnchorMax = new V2((float)(i + 1) / n, 1);
            node.OffsetMin = new V2(gap / 2, 0);
            node.OffsetMax = new V2(-gap / 2, 0);
            return node;
        }
    }
}

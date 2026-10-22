using System;
using System.Collections.Generic;

namespace SunnyValley.Layout
{
    /// <summary>
    /// The popups: a dark veil over the whole screen and a wooden panel centred in the safe area, as wide as the
    /// screen allows up to PopupMaxWidth. The content is stacked top to bottom; the panel is as tall as its content.
    /// </summary>
    public static class Popups
    {
        public static readonly string[] PreBoosterIcons = { Icons.Pitchfork, Icons.Bomb, Icons.Rainbow };
        public static readonly string[] PreBoosterNames = { "Вилы", "Бомба", "Радуга" };
        public static readonly uint[] PreBoosterColors = { Tokens.Orange, Tokens.Red, Tokens.Purple };

        /// <summary>A popup being stacked: the panel, its width and the running height from its top edge.</summary>
        sealed class Stack
        {
            public UiNode Panel;
            public float W;
            public float Y;

            /// <summary>Adds a block across the panel inside the padding (or narrower, centred) and moves down.</summary>
            public UiNode Add(UiNode node, float height, float width = -1, float gapAfter = Tokens.Gap)
            {
                float w = width < 0 ? W - 2 * Tokens.Pad : Math.Min(width, W - 2 * Tokens.Pad);
                Panel.Add(node).TopCenter(0, Y, w, height);
                Y += height + gapAfter;
                return node;
            }
        }

        static UiLayout Begin(string name, CanvasFit fit, string title, bool closable, out Stack stack)
        {
            var L = UiLayout.Begin(name, fit);
            L.Root.Add(new UiNode(name + ".dim", NodeKind.Dim) { Color = Tokens.Dim, Flags = NodeFlags.FullBleed }).Stretch();
            float w = Math.Min(L.W - 2 * Tokens.PopupMargin, Tokens.PopupMaxWidth);
            var panel = L.Safe.Add(Kit.PopupPanel(name + ".panel", title, closable));
            stack = new Stack { Panel = panel, W = w, Y = Kit.PopupHeader + Tokens.Gap };
            return L;
        }

        static UiLayout End(UiLayout L, Stack stack)
        {
            float h = stack.Y - Tokens.Gap + Tokens.Pad;
            stack.Panel.Center(0, 0, stack.W, h);
            return L.Finish();
        }

        /// <summary>A recessed well with a caption and goal slots.</summary>
        static UiNode GoalsWell(string id, string caption, List<Goal> goals)
        {
            var well = Kit.Panel(id, Tokens.WoodInset, Tokens.RadiusMedium);
            well.Add(Kit.Label(id + ".caption", caption, Tokens.FontCaption, Tokens.FontCaptionMin, Tokens.TextMuted)).StretchTop(16, 16, 12, 50);
            Screens.GoalSlots(well.Add(Kit.Group(id + ".slots")).Stretch(16, 16, 64, 12), id + ".goal", goals);
            return well;
        }

        // ------------------------------------------------------------------ before a level

        public static UiLayout LevelStart(CanvasFit fit, LevelStartInfo info)
        {
            var L = Begin("start", fit, "Уровень " + info.Level, true, out var s);
            s.Add(GoalsWell("start.goals", "Цели", info.Goals), 200);
            s.Add(Kit.Label("start.caption", "Взять с собой", Tokens.FontBody, Tokens.FontBodyMin, Tokens.TextDark), 64, -1, 8);

            var row = s.Add(Kit.Group("start.boosters"), Tokens.BoosterHeight, -1, Tokens.GapBig);
            for (int i = 0; i < PreBoosterIcons.Length; i++)
            {
                var slot = Screens.Slice(row.Add(Kit.Group("start.slot" + i)), i, PreBoosterIcons.Length, 0);
                int count = info.Boosters != null && i < info.Boosters.Length ? info.Boosters[i] : 0;
                var b = slot.Add(Screens.Booster("start.booster" + i, PreBoosterIcons[i], PreBoosterColors[i], PreBoosterNames[i], count));
                b.TopCenter(0, 0, Tokens.BoosterWidth, Tokens.BoosterHeight);
                bool picked = info.Picked != null && i < info.Picked.Length && info.Picked[i];
                // a tick on a picked booster: a green disc over the icon's lower left
                var tick = b.Add(new UiNode("start.booster" + i + ".picked", NodeKind.Panel) { Shape = Shape.Circle, Color = Tokens.Green });
                tick.TopLeft(0, 84, Tokens.BadgeSize, Tokens.BadgeSize);
                tick.Add(Kit.Icon("start.booster" + i + ".picked.icon", Icons.Check)).Stretch(10, 10, 10, 10);
                if (!picked)
                    tick.Flags |= NodeFlags.Hidden;
            }

            s.Add(Kit.Button("start.play", "Играть", Tokens.Yellow, Tokens.YellowShade, Tokens.PlayHeight,
                fontSize: Tokens.FontPlay, fontMin: Tokens.FontPlayMin), Tokens.PlayHeight, 620);
            return End(L, s);
        }

        // ------------------------------------------------------------------ won

        public static UiLayout Win(CanvasFit fit, WinInfo info)
        {
            var L = Begin("win", fit, "Уровень пройден!", false, out var s);
            var stars = s.Add(Kit.Group("win.stars"), 210);
            for (int i = 0; i < 3; i++)
            {
                bool middle = i == 1;
                float size = middle ? 200 : 160;
                var star = stars.Add(Kit.Icon("win.star" + i, i < info.Stars ? Icons.Star : Icons.Star + ".empty"));
                star.TopCenter((i - 1) * 210, middle ? 0 : 40, size, size);
            }

            var well = s.Add(Kit.Panel("win.rewards", Tokens.WoodInset, Tokens.RadiusMedium), 190);
            well.Add(Kit.Label("win.rewards.caption", "Награда", Tokens.FontCaption, Tokens.FontCaptionMin, Tokens.TextMuted)).StretchTop(16, 16, 12, 50);
            var items = well.Add(Kit.Group("win.rewards.items")).Stretch(16, 16, 64, 12);
            var rewards = new List<(string icon, string text)> { (Icons.Coin, "+" + Format.Amount(info.Coins)), (Icons.Star, "+" + info.FarmStars) };
            for (int i = 0; i < rewards.Count; i++)
            {
                var item = Screens.Slice(items.Add(Kit.Group("win.reward" + i)), i, rewards.Count, 16);
                item.Add(Kit.Icon("win.reward" + i + ".icon", rewards[i].icon)).LeftMiddle(8, 0, 90, 90);
                item.Add(Kit.Label("win.reward" + i + ".value", rewards[i].text, Tokens.FontValue, Tokens.FontValueMin, Tokens.TextDark, 1, TextAlign.Left))
                    .Stretch(110, 8, 8, 8);
            }

            s.Add(Kit.Button("win.next", "Дальше", Tokens.Green, Tokens.GreenShade, Tokens.ButtonHeight), Tokens.ButtonHeight, 560);
            return End(L, s);
        }

        // ------------------------------------------------------------------ lost

        public static UiLayout Lose(CanvasFit fit, LoseInfo info)
        {
            var L = Begin("lose", fit, "Ходы закончились", false, out var s);
            s.Add(GoalsWell("lose.left", "Осталось собрать", info.Left), 200, -1, Tokens.GapBig);
            s.Add(Kit.Button("lose.more", "+5 ходов за " + Format.Group(info.Price), info.CanAfford ? Tokens.Green : Tokens.TrackOff,
                info.CanAfford ? Tokens.GreenShade : Tokens.WoodDark, Tokens.ButtonHeight, Icons.Coin), Tokens.ButtonHeight, 720, Tokens.GapBig);
            s.Add(Kit.Button("lose.give_up", "Сдаться", Tokens.Red, Tokens.RedShade, Tokens.SmallButtonHeight), Tokens.SmallButtonHeight, 460);
            return End(L, s);
        }

        // ------------------------------------------------------------------ settings

        public static readonly string[] SettingNames = { "Музыка", "Звуки", "Вибрация" };

        public static UiLayout Settings(CanvasFit fit, SettingsInfo info)
        {
            var L = Begin("settings", fit, "Настройки", true, out var s);
            bool[] on = { info.Music, info.Sounds, info.Vibration };
            for (int i = 0; i < SettingNames.Length; i++)
            {
                var row = s.Add(Kit.Panel("settings.row" + i, Tokens.WoodInset, Tokens.RadiusMedium), Tokens.RowHeight - 12);
                row.Add(Kit.Label("settings.row" + i + ".label", SettingNames[i], Tokens.FontBody, Tokens.FontBodyMin, Tokens.TextDark, 1, TextAlign.Left))
                    .Stretch(32, Tokens.ToggleWidth + 32, 12, 12);
                row.Add(Kit.Toggle("settings.toggle" + i, on[i])).RightMiddle(16, 0, Tokens.ToggleWidth, Tokens.ToggleHeight);
            }
            s.Y += Tokens.Gap;
            s.Add(Kit.Button("settings.support", "Поддержка", Tokens.Blue, Tokens.BlueShade, Tokens.SmallButtonHeight), Tokens.SmallButtonHeight, 560);
            if (info.InLevel)
                s.Add(Kit.Button("settings.quit", "Выйти из уровня", Tokens.Red, Tokens.RedShade, Tokens.SmallButtonHeight), Tokens.SmallButtonHeight, 560);
            s.Add(Kit.Label("settings.version", "Солнечная долина " + info.Version, Tokens.FontCaption, Tokens.FontCaptionMin, Tokens.TextMuted), 50);
            return End(L, s);
        }

        // ------------------------------------------------------------------ tasks: a scrolling list

        public static UiLayout Tasks(CanvasFit fit, IList<TaskItem> tasks)
        {
            var L = Begin("tasks", fit, "Задания", true, out var s);
            int n = tasks?.Count ?? 0;
            float rowStep = Tokens.RowHeight + Tokens.Gap;
            float contentH = Math.Max(0, n * rowStep - Tokens.Gap);
            // the list takes what the screen leaves, at least two rows, at most all of them
            float room = L.H - 2 * Tokens.PopupMargin - s.Y - Tokens.Pad;
            float viewH = Math.Max(2 * Tokens.RowHeight, Math.Min(contentH, room));
            var scroll = s.Add(new UiNode("tasks.list", NodeKind.Scroll), viewH);
            var content = scroll.Add(Kit.Group("tasks.content")).StretchTop(0, 0, 0, contentH);
            for (int i = 0; i < n; i++)
            {
                var row = content.Add(Kit.Panel("tasks.row" + i, Tokens.WoodInset, Tokens.RadiusMedium));
                row.StretchTop(0, 0, i * rowStep, Tokens.RowHeight);
                row.Add(Kit.Label("tasks.row" + i + ".title", tasks[i].Title, Tokens.FontBody, Tokens.FontBodyMin, Tokens.TextDark, 2, TextAlign.Left))
                    .Stretch(28, 220 + 32, 10, 10);
                row.Add(Kit.Button("tasks.row" + i + ".do", tasks[i].Stars.ToString(), Tokens.Green, Tokens.GreenShade, Tokens.SmallButtonHeight, Icons.Star))
                    .RightMiddle(16, 0, 210, Tokens.SmallButtonHeight);
            }
            return End(L, s);
        }

        /// <summary>The first tasks of chapter 1 (GDD §5), until Core's meta provides them.</summary>
        public static List<TaskItem> DemoTasks() => new List<TaskItem>
        {
            new TaskItem("Расчистить двор от бурьяна", 1),
            new TaskItem("Починить крыльцо", 1),
            new TaskItem("Поставить новый забор вдоль дороги к дому", 2),
            new TaskItem("Вставить окна в доме", 2),
            new TaskItem("Покрасить амбар", 3),
            new TaskItem("Посадить цветы у калитки", 1),
            new TaskItem("Вычистить пруд", 2),
            new TaskItem("Починить курятник", 3),
        };
    }
}

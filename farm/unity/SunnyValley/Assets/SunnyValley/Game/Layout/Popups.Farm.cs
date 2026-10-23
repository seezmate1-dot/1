using System;
using System.Collections.Generic;

namespace SunnyValley.Layout
{
    /// <summary>
    /// The windows of chapter 1 on the farm (GDD 5): the look picker (three cards), the dialogue of a replica or a
    /// story scene (portrait, name, text, a tap anywhere goes on), the celebrations of a finished zone and chapter and
    /// «Пока вас не было».
    /// </summary>
    public static partial class Popups
    {
        // ------------------------------------------------------------------ the look picker

        public const float CardGap = 16;

        /// <summary>Three cards side by side, each a picture and the look's name; the picked one is yellow with a tick.</summary>
        public static UiLayout Picker(CanvasFit fit, PickerInfo info)
        {
            var L = Begin("picker", fit, info.Title, true, out var s);
            s.Add(Kit.Label("picker.caption", info.Caption ?? "", Tokens.FontBody, Tokens.FontBodyMin, Tokens.TextDark, 2),
                2 * FontMetrics.LineHeight(Tokens.FontBody) + 8);
            int n = Math.Max(1, info.Cards?.Count ?? 0);
            float inner = s.W - 2 * Tokens.Pad;
            float cardW = (inner - (n - 1) * CardGap) / n;
            float captionH = 2 * FontMetrics.LineHeight(Tokens.FontCaption) + 8;
            float cardH = cardW - 24 + captionH + 28;
            var row = s.Add(Kit.Group("picker.cards"), cardH, -1, Tokens.GapBig);
            for (int i = 0; i < n; i++)
            {
                var card = info.Cards != null && i < info.Cards.Count ? info.Cards[i] : new LookCard(Icons.Star, "");
                string id = "picker.card" + i;
                bool picked = i == info.Selected;
                var b = row.Add(new UiNode(id, NodeKind.Button)
                {
                    Shape = Shape.RoundRect, Radius = Tokens.RadiusMedium, Color = picked ? CardPicked : CardIdle, Depth = 8,
                    DepthColor = Tokens.WoodDark,
                });
                b.TopLeft(i * (cardW + CardGap), 0, cardW, cardH);
                b.Add(Kit.Icon(id + ".icon", card.Icon)).TopCenter(0, 12, cardW - 24, cardW - 24);
                b.Add(Kit.Label(id + ".name", card.Name ?? "", Tokens.FontCaption, Tokens.FontCaptionMin, Tokens.TextDark, 2))
                    .StretchBottom(10, 10, 14, captionH);
                var tick = b.Add(new UiNode(id + ".tick", NodeKind.Panel) { Shape = Shape.Circle, Color = Tokens.Green });
                tick.TopRight(8, 8, Tokens.BadgeSize, Tokens.BadgeSize);
                tick.Add(Kit.Icon(id + ".tick.icon", Icons.Check)).Stretch(10, 10, 10, 10);
                if (!picked)
                    tick.Flags |= NodeFlags.Hidden;
            }
            string text = info.Button ?? "Выбрать";
            s.Add(Kit.Button("picker.ok", info.Stars > 0 ? text + " " + info.Stars : text, Tokens.Green, Tokens.GreenShade, Tokens.ButtonHeight,
                info.Stars > 0 ? Icons.Star : null), Tokens.ButtonHeight, 620);
            return End(L, s);
        }

        public const uint CardIdle = Tokens.WoodInset;
        public const uint CardPicked = 0xFFE27AFF;

        // ------------------------------------------------------------------ dialogue

        public const float PortraitSize = 420;
        public const int DialogueLines = 4;

        /// <summary>
        /// A replica: a wooden panel at the bottom with the speaker's name on a ribbon and up to four lines of text,
        /// the portrait rising from behind the panel on the left (or the right), «нажмите, чтобы продолжить» in the
        /// corner. The whole screen under the top row is one tap area («dialogue.next»); a scene can be skipped.
        /// </summary>
        public static UiLayout Dialogue(CanvasFit fit, DialogueInfo info)
        {
            var L = UiLayout.Begin("dialogue", fit);
            L.Root.Add(new UiNode("dialogue.dim", NodeKind.Dim) { Color = 0x00000050, Flags = NodeFlags.FullBleed }).Stretch();
            float M = Tokens.Margin, W = L.W;
            float top = M + Tokens.SmallButtonHeight + Tokens.Gap;
            if (info.CanSkip)
            {
                L.Safe.Add(Kit.Button("dialogue.skip", "Пропустить", Tokens.Blue, Tokens.BlueShade, Tokens.SmallButtonHeight))
                    .TopRight(M, M, 400, Tokens.SmallButtonHeight);
            }
            if (info.SceneTitle != null)
            {
                L.Safe.Add(Kit.Label("dialogue.title", info.SceneTitle, Tokens.FontTitle, Tokens.FontCaption, Tokens.TextLight, 2, TextAlign.Left, true))
                    .TopLeft(M, M, W - 2 * M - (info.CanSkip ? 400 + Tokens.Gap : 0), Tokens.SmallButtonHeight);
            }
            L.Safe.Add(new UiNode("dialogue.next", NodeKind.Button)).Stretch(0, 0, top, 0);

            float nameH = 84, hintH = 48;
            float textH = DialogueLines * FontMetrics.LineHeight(Tokens.FontBody) + 8;
            float panelH = Tokens.Pad / 2 + nameH + 8 + textH + hintH + 16;
            float panelW = Math.Min(W - 2 * M, 1240);
            float size = Math.Min(PortraitSize, L.H - top - panelH - M - 40);
            var portrait = L.Safe.Add(Kit.Icon("dialogue.portrait", info.Portrait));
            float py = M + panelH - 40;
            if (info.Right)
                portrait.At(1, 0, (W - panelW) / 2 + 24, py, size, size);
            else
                portrait.At(0, 0, (W - panelW) / 2 + 24, py, size, size);

            var panel = L.Safe.Add(Kit.Panel("dialogue.panel", Tokens.Wood, Tokens.RadiusLarge, 14, Tokens.WoodDark));
            panel.BottomCenter(0, M + 14, panelW, panelH);
            float nameW = Math.Min(560, panelW - 2 * Tokens.Pad);
            var ribbon = panel.Add(Kit.Panel("dialogue.name", Tokens.Orange, Tokens.RadiusMedium, 6, Tokens.RedShade));
            if (info.Right)
                ribbon.TopRight(Tokens.Pad, Tokens.Pad / 2, nameW, nameH);
            else
                ribbon.TopLeft(Tokens.Pad, Tokens.Pad / 2, nameW, nameH);
            ribbon.Add(Kit.Label("dialogue.name.label", info.Name ?? "", Tokens.FontBody, Tokens.FontBodyMin, Tokens.TextLight, 1, TextAlign.Center, true))
                .Stretch(20, 20, 8, 10);
            panel.Add(Kit.Label("dialogue.text", info.Text ?? "", Tokens.FontBody, Tokens.FontBodyMin, Tokens.TextDark, DialogueLines, TextAlign.Left))
                .Stretch(Tokens.Pad, Tokens.Pad, Tokens.Pad / 2 + nameH + 8, hintH + 16);
            panel.Add(Kit.Label("dialogue.hint", "Нажмите, чтобы продолжить", Tokens.FontCaption, Tokens.FontCaptionMin, Tokens.TextMuted, 1, TextAlign.Right))
                .StretchBottom(Tokens.Pad, Tokens.Pad, 12, hintH);
            return L.Finish();
        }

        // ------------------------------------------------------------------ celebrations

        /// <summary>A zone or the whole chapter done: the big picture on sun rays, a line, the rewards, «Дальше».</summary>
        public static UiLayout Celebrate(CanvasFit fit, CelebrateInfo info)
        {
            var L = Begin("celebrate", fit, info.Title, false, out var s);
            var pic = s.Add(Kit.Group("celebrate.picture"), 300);
            pic.Add(Kit.Icon("celebrate.rays", Icons.Sun)).TopCenter(0, 0, 300, 300);
            pic.Add(Kit.Icon("celebrate.icon", info.Icon)).TopCenter(0, 40, 220, 220);
            if (!string.IsNullOrEmpty(info.Text))
                s.Add(Kit.Label("celebrate.text", info.Text, Tokens.FontBody, Tokens.FontBodyMin, Tokens.TextDark, 3),
                    3 * FontMetrics.LineHeight(Tokens.FontBody) + 8);
            var rewards = info.Rewards ?? new List<(string icon, string text)>();
            if (rewards.Count > 0)
                RewardsWell(s, "celebrate.rewards", "Награда", rewards);
            s.Add(Kit.Button("celebrate.next", info.Button ?? "Дальше", Tokens.Green, Tokens.GreenShade, Tokens.ButtonHeight), Tokens.ButtonHeight, 560);
            return End(L, s);
        }

        /// <summary>A well with up to three rewards a row (icon and amount), as many rows as needed (at most two).</summary>
        static void RewardsWell(Stack s, string id, string caption, List<(string icon, string text)> rewards)
        {
            int n = Math.Min(6, rewards.Count);
            int perRow = Math.Min(3, n);
            int rows = (n + perRow - 1) / perRow;
            float rowH = 110;
            var well = s.Add(Kit.Panel(id, Tokens.WoodInset, Tokens.RadiusMedium), 64 + rows * rowH + 12);
            well.Add(Kit.Label(id + ".caption", caption, Tokens.FontCaption, Tokens.FontCaptionMin, Tokens.TextMuted)).StretchTop(16, 16, 12, 50);
            for (int r = 0; r < rows; r++)
            {
                var line = well.Add(Kit.Group(id + ".row" + r)).StretchTop(16, 16, 64 + r * rowH, rowH);
                int inRow = Math.Min(perRow, n - r * perRow);
                for (int k = 0; k < inRow; k++)
                {
                    int i = r * perRow + k;
                    var item = Screens.Slice(line.Add(Kit.Group(id + ".item" + i)), k, inRow, 12);
                    item.Add(Kit.Icon(id + ".item" + i + ".icon", rewards[i].icon)).LeftMiddle(4, 0, 84, 84);
                    item.Add(Kit.Label(id + ".item" + i + ".value", rewards[i].text, Tokens.FontValue, Tokens.FontValueMin, Tokens.TextDark, 1,
                        TextAlign.Left)).Stretch(96, 4, 8, 8);
                }
            }
        }

        // ------------------------------------------------------------------ while you were away

        public const int AwayMaxLines = 6;

        public static UiLayout Away(CanvasFit fit, AwayInfo info)
        {
            var L = Begin("away", fit, "Пока вас не было", false, out var s);
            s.Add(Kit.Label("away.time", info.Away ?? "", Tokens.FontBody, Tokens.FontBodyMin, Tokens.TextMuted), 64);
            var lines = new List<string>(info.Lines ?? new List<string>());
            if (lines.Count > AwayMaxLines)
            {
                int more = lines.Count - (AwayMaxLines - 1);
                lines = lines.GetRange(0, AwayMaxLines - 1);
                lines.Add("…и ещё " + more);
            }
            float lineH = FontMetrics.LineHeight(Tokens.FontBody) + 12;
            var well = s.Add(Kit.Panel("away.list", Tokens.WoodInset, Tokens.RadiusMedium), Math.Max(1, lines.Count) * lineH + 32, -1, Tokens.GapBig);
            for (int i = 0; i < lines.Count; i++)
            {
                // a line may shrink to the caption's minimum: «Сарай инструментов сделал: Радуга-подсолнух ×1»
                well.Add(Kit.Label("away.line" + i, "• " + lines[i], Tokens.FontBody, Tokens.FontCaptionMin, Tokens.TextDark, 1, TextAlign.Left))
                    .StretchTop(28, 28, 16 + i * lineH, lineH);
            }
            if (info.CollectAll)
            {
                // the farm is open and something is ripe: collect it all right here
                var row = s.Add(Kit.Group("away.buttons"), Tokens.ButtonHeight);
                Screens.Slice(row.Add(Kit.Button("away.collect", "Собрать всё", Tokens.Yellow, Tokens.YellowShade, Tokens.ButtonHeight)), 0, 2, Tokens.Gap);
                Screens.Slice(row.Add(Kit.Button("away.ok", "Отлично!", Tokens.Green, Tokens.GreenShade, Tokens.ButtonHeight)), 1, 2, Tokens.Gap);
            }
            else
            {
                s.Add(Kit.Button("away.ok", "Отлично!", Tokens.Green, Tokens.GreenShade, Tokens.ButtonHeight), Tokens.ButtonHeight, 560);
            }
            return End(L, s);
        }
    }
}

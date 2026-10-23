using System;
using System.Collections.Generic;

namespace SunnyValley.Layout
{
    /// <summary>
    /// The windows of the farm economy (chapter 2, GDD 6), all one shape so they read alike: the title, a header well
    /// (the barn's capacity, the town's residents, the truck), a workshop's queue of slots, then a scrolling list of
    /// rows (a picture, a title, a line, a timer bar or chips, buttons on the right) or the order board's grid of
    /// cards, and up to two buttons at the bottom. Ids: «&lt;id&gt;.row3.do», «&lt;id&gt;.row3.alt», «&lt;id&gt;.card3»,
    /// «&lt;id&gt;.slot1», «&lt;id&gt;.head.do», «&lt;id&gt;.foot0».
    /// </summary>
    public static partial class Popups
    {
        public const float RowIcon = 96;
        public const float RowPad = 12;
        public const float RowButtonWidth = 220;
        public const float SectionHeight = 64;
        public const float HeaderHeight = 150;
        public const float SlotGap = 10;
        public const float SlotMax = 160;
        public const float SlotHeight = 176;
        public const float CardChip = 60;
        public const int CardColumns = 3;
        public const int MaxCardChips = 3;
        public const int MaxRowChips = 3;
        public const uint RowDim = 0xDCCDB0FF;
        public const uint MissingText = 0xC0392BFF;

        public static (uint face, uint shade) ToneColors(Tone t)
        {
            switch (t)
            {
                case Tone.Yellow: return (Tokens.Yellow, Tokens.YellowShade);
                case Tone.Blue: return (Tokens.Blue, Tokens.BlueShade);
                case Tone.Red: return (Tokens.Red, Tokens.RedShade);
                case Tone.Grey: return (Tokens.TrackOff, Tokens.WoodDark);
                default: return (Tokens.Green, Tokens.GreenShade);
            }
        }

        public const float SmallIcon = 60;
        public const float SmallPad = 18;

        /// <summary>
        /// A button of a row or a header: smaller type, icon and padding than the big popup buttons (it shares the
        /// row), so a price with its coin («12 500») fits a 220-unit button.
        /// </summary>
        static UiNode SmallButton(string id, RowButton rb, float height = Tokens.SmallButtonHeight)
        {
            var (face, shade) = ToneColors(rb.Tone);
            var b = new UiNode(id, NodeKind.Button)
            {
                Shape = Shape.RoundRect, Radius = Math.Min(Tokens.RadiusLarge, height / 2), Color = face, Depth = 10, DepthColor = shade,
            };
            float left = SmallPad;
            if (rb.Icon != null)
            {
                b.Add(Kit.Icon(id + ".icon", rb.Icon)).LeftMiddle(SmallPad, 4, SmallIcon, SmallIcon);
                left = SmallPad + SmallIcon + 8;
            }
            b.Add(Kit.Label(id + ".label", rb.Text ?? "", Tokens.FontValue, Tokens.FontValueMin, Tokens.TextLight, 1, TextAlign.Center, outline: true))
                .Stretch(left, SmallPad, 10, 18);
            return b;
        }

        static UiNode RoundIconButton(string id, RowButton b)
        {
            var (face, shade) = ToneColors(b.Tone);
            return Kit.RoundButton(id, b.Icon ?? Icons.Close, face, shade);
        }

        public const float CardPay = 48;
        public static float CardHeight => 10 + 48 + 8 + MaxCardChips * CardChip + 8 + 2 * CardPay + 8 + 12;

        public static float RowHeightOf(EconomyRow r) => r.Kind == RowKind.Section ? SectionHeight : Tokens.RowHeight;

        // ------------------------------------------------------------------ the window

        public static UiLayout Economy(CanvasFit fit, EconomyInfo info)
        {
            string id = info.Id;
            var L = Begin(id, fit, info.Title, true, out var s);
            float inner = s.W - 2 * Tokens.Pad;

            if (info.HeaderText != null)
                Header(s, id + ".head", info);
            if (info.Queue != null && info.Queue.Count > 0)
                Queue(s, id, info.Queue, inner);

            int footers = Math.Min(2, info.Footer?.Count ?? 0);
            float footerH = footers > 0 ? Tokens.ButtonHeight + Tokens.Gap : 0;
            var rows = info.Rows ?? new List<EconomyRow>();
            if (rows.Count == 0)
            {
                s.Add(Kit.Label(id + ".empty", info.Empty ?? "", Tokens.FontBody, Tokens.FontBodyMin, Tokens.TextMuted, 2), 2 * Tokens.RowHeight);
            }
            else
            {
                // the list takes what the screen leaves (at least one card row or two rows), at most all of it
                float contentH = info.Cards ? CardsHeight(rows.Count) : ListHeight(rows);
                float room = L.H - 2 * Tokens.PopupMargin - s.Y - Tokens.Pad - footerH;
                float min = info.Cards ? CardHeight : 2 * Tokens.RowHeight;
                float viewH = Math.Max(Math.Min(min, contentH), Math.Min(contentH, room));
                var scroll = s.Add(new UiNode(id + ".list", NodeKind.Scroll), viewH);
                var content = scroll.Add(Kit.Group(id + ".content")).StretchTop(0, 0, 0, contentH);
                if (info.Cards)
                    Cards(content, id, rows, inner);
                else
                    List(content, id, rows);
            }

            if (footers > 0)
            {
                var foot = s.Add(Kit.Group(id + ".foot"), Tokens.ButtonHeight, footers == 1 ? 620 : -1);
                for (int i = 0; i < footers; i++)
                {
                    var b = info.Footer[i];
                    var (face, shade) = ToneColors(b.Tone);
                    Screens.Slice(foot.Add(Kit.Button(id + ".foot" + i, b.Text ?? "", face, shade, Tokens.ButtonHeight, b.Icon)), i, footers,
                        footers > 1 ? Tokens.Gap : 0);
                }
            }
            return End(L, s);
        }

        static float ListHeight(List<EconomyRow> rows)
        {
            float h = 0;
            foreach (var r in rows)
                h += RowHeightOf(r) + Tokens.Gap;
            return Math.Max(0, h - Tokens.Gap);
        }

        static float CardsHeight(int n)
        {
            int lines = (n + CardColumns - 1) / CardColumns;
            return Math.Max(0, lines * (CardHeight + CardGap) - CardGap);
        }

        /// <summary>The header well: a line on top, a bar or chips under it, a button on the right.</summary>
        static void Header(Stack s, string id, EconomyInfo info)
        {
            bool second = info.HeaderBar >= 0 || info.HeaderChips?.Count > 0;
            float h = second || info.HeaderButton != null ? HeaderHeight : 84;
            var well = s.Add(Kit.Panel(id, Tokens.WoodInset, Tokens.RadiusMedium), h);
            float right = info.HeaderButton != null ? 16 + 280 + 16 : 28;
            well.Add(Kit.Label(id + ".text", info.HeaderText, Tokens.FontBody, Tokens.FontBodyMin, Tokens.TextDark, second ? 1 : 2, TextAlign.Left))
                .Stretch(28, right, 12, second ? h - 12 - 60 : 12);
            if (info.HeaderBar >= 0)
            {
                well.Add(Kit.Bar(id + ".bar", Math.Min(1, info.HeaderBar), info.HeaderBarText ?? ""))
                    .StretchBottom(28, right, 16, Tokens.ProgressHeight);
            }
            else if (info.HeaderChips?.Count > 0)
            {
                Chips(well.Add(Kit.Group(id + ".chips")).StretchBottom(28, right, 12, 60), id + ".chip", info.HeaderChips, MaxRowChips);
            }
            if (info.HeaderButton != null)
                well.Add(SmallButton(id + ".do", info.HeaderButton)).RightMiddle(16, 0, 280, Tokens.SmallButtonHeight);
        }

        /// <summary>A workshop's queue: equal square-ish slots in one row, each a picture and a timer or «Готово».</summary>
        static void Queue(Stack s, string id, List<QueueSlot> slots, float inner)
        {
            int n = slots.Count;
            float w = Math.Min(SlotMax, (inner - (n - 1) * SlotGap) / n);
            float x0 = (inner - (n * w + (n - 1) * SlotGap)) / 2;
            var row = s.Add(Kit.Group(id + ".queue"), SlotHeight, -1, Tokens.GapBig);
            for (int i = 0; i < n; i++)
            {
                var q = slots[i];
                string sid = id + ".slot" + i;
                uint color = q.State == SlotState.Ready ? Tokens.Green : q.State == SlotState.Running ? CardPicked
                    : q.State == SlotState.Buy ? Tokens.Blue : RowDim;
                var b = row.Add(new UiNode(sid, NodeKind.Button)
                {
                    Shape = Shape.RoundRect, Radius = Tokens.RadiusSmall, Color = color, Depth = 8, DepthColor = Tokens.WoodDark,
                });
                b.TopLeft(x0 + i * (w + SlotGap), 0, w, SlotHeight);
                string icon = q.Icon ?? (q.State == SlotState.Buy ? Icons.Plus : null);
                if (icon != null)
                    b.Add(Kit.Icon(sid + ".icon", icon)).TopCenter(0, 14, Math.Min(96, w - 24), Math.Min(96, w - 24));
                bool light = q.State == SlotState.Ready || q.State == SlotState.Buy;
                b.Add(Kit.Label(sid + ".label", q.Text ?? "", Tokens.FontCaption, Tokens.FontCaptionMin, light ? Tokens.TextLight : Tokens.TextDark, 1,
                    TextAlign.Center, light)).StretchBottom(6, 6, 12, 50);
            }
        }

        static void List(UiNode content, string id, List<EconomyRow> rows)
        {
            float y = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                string rid = id + ".row" + i;
                float h = RowHeightOf(r);
                if (r.Kind == RowKind.Section)
                {
                    content.Add(Kit.Label(rid, r.Title ?? "", Tokens.FontBody, Tokens.FontBodyMin, Tokens.TextMuted, 1, TextAlign.Left))
                        .StretchTop(12, 12, y, h);
                }
                else
                {
                    var row = content.Add(Kit.Panel(rid, r.Dim ? RowDim : Tokens.WoodInset, Tokens.RadiusMedium));
                    row.StretchTop(0, 0, y, h);
                    Row(row, rid, r);
                }
                y += h + Tokens.Gap;
            }
        }

        /// <summary>One row: the picture, the title over a line, a bar or chips; the main button and the round one on the right.</summary>
        static void Row(UiNode row, string id, EconomyRow r)
        {
            float left = RowPad + RowIcon + RowPad;
            float right = RowPad;
            if (r.Button != null)
            {
                row.Add(SmallButton(id + ".do", r.Button)).RightMiddle(right, 0, RowButtonWidth, Tokens.SmallButtonHeight);
                right += RowButtonWidth + RowPad;
            }
            if (r.Alt != null)
            {
                row.Add(RoundIconButton(id + ".alt", r.Alt)).RightMiddle(right, 0, Tokens.SmallButtonHeight, Tokens.SmallButtonHeight);
                right += Tokens.SmallButtonHeight + RowPad;
            }
            right += 4;
            if (r.Icon != null)
                row.Add(Kit.Icon(id + ".icon", r.Icon)).LeftMiddle(RowPad, 0, RowIcon, RowIcon);

            bool chips = r.Chips != null && r.Chips.Count > 0;
            bool bar = r.Progress >= 0;
            bool sub = !chips && !bar && !string.IsNullOrEmpty(r.Sub);
            bool second = chips || bar || sub;
            // titles may shrink to the caption's minimum: «Вишнёвое варенье · 1 ч 30 мин» on a 720p phone
            row.Add(Kit.Label(id + ".title", r.Title ?? "", Tokens.FontBody, Tokens.FontCaptionMin, Tokens.TextDark, second ? 1 : 2, TextAlign.Left))
                .Stretch(left, right, 12, second ? 80 : 12);
            if (bar)
            {
                row.Add(Kit.Bar(id + ".bar", Math.Min(1, r.Progress), r.ProgressText ?? "")).StretchBottom(left, right, 18, 52);
            }
            else if (chips)
            {
                Chips(row.Add(Kit.Group(id + ".chips")).StretchBottom(left, right, 14, 60), id + ".chip", r.Chips, MaxRowChips);
            }
            else if (sub)
            {
                row.Add(Kit.Label(id + ".sub", r.Sub, Tokens.FontCaption, Tokens.FontCaptionMin, Tokens.TextMuted, 1, TextAlign.Left))
                    .StretchBottom(left, right, 16, 52);
            }
        }

        /// <summary>Chips sharing a strip in equal columns: an icon and its amount (red when missing).</summary>
        static void Chips(UiNode strip, string id, List<Chip> chips, int max)
        {
            int n = Math.Min(max, chips.Count);
            for (int i = 0; i < n; i++)
            {
                var c = chips[i];
                var cell = Screens.Slice(strip.Add(Kit.Group(id + i)), i, n, 8);
                cell.Add(Kit.Icon(id + i + ".icon", c.Icon)).LeftMiddle(0, 0, 44, 44);
                cell.Add(Kit.Label(id + i + ".value", c.Text ?? "", Tokens.FontCaption, Tokens.FontCaptionMin, c.Missing ? MissingText : Tokens.TextDark, 1,
                    TextAlign.Left)).Stretch(48, 0, 4, 4);
            }
        }

        /// <summary>The order board: cards three a row; a card shows who asks, the goods (have/need) and what it pays.</summary>
        static void Cards(UiNode content, string id, List<EconomyRow> rows, float inner)
        {
            float w = (inner - (CardColumns - 1) * CardGap) / CardColumns, h = CardHeight;
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                string cid = id + ".card" + i;
                int col = i % CardColumns, line = i / CardColumns;
                var card = content.Add(new UiNode(cid, NodeKind.Button)
                {
                    Shape = Shape.RoundRect, Radius = Tokens.RadiusMedium, Color = r.Picked ? CardPicked : r.Dim ? RowDim : CardIdle, Depth = 8,
                    DepthColor = Tokens.WoodDark,
                });
                card.TopLeft(col * (w + CardGap), line * (h + CardGap), w, h);
                card.Add(Kit.Label(cid + ".caption", r.Caption ?? "", Tokens.FontCaption, Tokens.FontCaptionMin, Tokens.TextMuted)).StretchTop(12, 12, 10, 48);
                int n = Math.Min(MaxCardChips, r.Chips?.Count ?? 0);
                if (n == 0)
                {
                    // a card waiting for its next order: the picture and the time left
                    if (r.Icon != null)
                        card.Add(Kit.Icon(cid + ".icon", r.Icon)).TopCenter(0, 66, 100, 100);
                    card.Add(Kit.Label(cid + ".title", r.Title ?? "", Tokens.FontCaption, Tokens.FontCaptionMin, Tokens.TextDark)).StretchTop(12, 12, 174, 50);
                    card.Add(Kit.Label(cid + ".sub", r.Sub ?? "", Tokens.FontValue, Tokens.FontValueMin, Tokens.TextDark)).StretchBottom(12, 12, 24, 60);
                    continue;
                }
                for (int k = 0; k < n; k++)
                {
                    var c = r.Chips[k];
                    string kid = cid + ".chip" + k;
                    var line0 = card.Add(Kit.Group(kid)).StretchTop(12, 12, 66 + k * CardChip, CardChip);
                    line0.Add(Kit.Icon(kid + ".icon", c.Icon)).LeftMiddle(0, 0, 54, 54);
                    line0.Add(Kit.Label(kid + ".value", c.Text ?? "", Tokens.FontValue, Tokens.FontValueMin, c.Missing ? MissingText : Tokens.TextDark, 1,
                        TextAlign.Left)).Stretch(62, 0, 4, 4);
                }
                int m = Math.Min(2, r.Rewards?.Count ?? 0);
                if (m > 0)
                {
                    // what it pays, a line each: coins, then XP
                    var pay = card.Add(Kit.Panel(cid + ".pay", Tokens.WoodInset, Tokens.RadiusSmall)).StretchBottom(8, 8, 12, 2 * CardPay + 8);
                    for (int k = 0; k < m; k++)
                    {
                        var c = r.Rewards[k];
                        string kid = cid + ".pay" + k;
                        var line1 = pay.Add(Kit.Group(kid)).StretchTop(8, 8, 4 + k * CardPay, CardPay);
                        line1.Add(Kit.Icon(kid + ".icon", c.Icon)).LeftMiddle(0, 0, 42, 42);
                        line1.Add(Kit.Label(kid + ".value", c.Text ?? "", Tokens.FontCaption, Tokens.FontCaptionMin, Tokens.TextDark, 1, TextAlign.Left))
                            .Stretch(50, 0, 2, 2);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ a question

        /// <summary>Before crystals are spent or a detour is taken: a picture, a line, the button that does it.</summary>
        public static UiLayout Confirm(CanvasFit fit, ConfirmInfo info)
        {
            var L = Begin("confirm", fit, info.Title ?? "", true, out var s);
            var pic = s.Add(Kit.Group("confirm.picture"), 200);
            pic.Add(Kit.Icon("confirm.icon", info.Icon ?? Icons.Crystal)).TopCenter(0, 0, 200, 200);
            s.Add(Kit.Label("confirm.text", info.Text ?? "", Tokens.FontBody, Tokens.FontBodyMin, Tokens.TextDark, 3), 3 * FontMetrics.LineHeight(Tokens.FontBody) + 8,
                -1, Tokens.GapBig);
            var b = info.Button ?? new RowButton("Да");
            var (face, shade) = ToneColors(b.Tone);
            s.Add(Kit.Button("confirm.ok", b.Text ?? "", face, shade, Tokens.ButtonHeight, b.Icon), Tokens.ButtonHeight, 620);
            return End(L, s);
        }
    }
}

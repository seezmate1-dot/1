using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core;
using LastHearth.Core.Combat;
using UnityEngine;

namespace LastHearth.Game
{
    /// <summary>
    /// The "Арена" and "Набеги" windows, laid out like Hustle Castle's arena: a header with the league badge, the
    /// rating and its way to the next league, the tickets and the week's end; three opponent cards with their squads,
    /// their might against ours and what a win or a loss brings; tabs for the defense, the Glory shop, the journal
    /// (with revenge) and the leagues; a result panel after a fight. The raids window shares the cards and adds the
    /// fortress's own defense: traps of the forge and heroes on each line, the walls and the watch fires.
    /// Everything is drawn into rectangles (no GUILayout text), so the window keeps its shape on any phone.
    /// </summary>
    public sealed partial class GameRunner
    {
        static readonly string[] ArenaTabs = { "Соперники", "Оборона", "Лавка славы", "Журнал", "Лиги" };
        static readonly string[] RaidTabs = { "Крепости", "Оборона крепости", "Журнал" };
        static readonly string[] TrapShort = { "Нет", "Капкан", "Угли", "Решётка" };

        static readonly Color CreamText = new Color(1f, 0.94f, 0.8f, 1f);
        static readonly Color GoldText = new Color(1f, 0.84f, 0.42f, 1f);
        static readonly Color DimText = new Color(0.85f, 0.78f, 0.66f, 1f);
        static readonly Color GoodText = new Color(0.55f, 0.95f, 0.45f, 1f);
        static readonly Color EvenText = new Color(1f, 0.85f, 0.35f, 1f);
        static readonly Color BadText = new Color(1f, 0.48f, 0.4f, 1f);

        int arenaTab;
        int raidTab;
        int raidLine;

        /// <summary>What the last arena fight or raid brought: the result panel shows it until "Продолжить".</summary>
        sealed class PvpResult
        {
            public bool Won, Raid, Revenge;
            public string Opponent = "";
            public int Stars, RatingBefore, RatingAfter;
            public long Glory, Essence, Materials;
        }

        PvpResult pvpResult;

        readonly Dictionary<int, GUIStyle> arenaStyles = new Dictionary<int, GUIStyle>();
        GUIStyle hitStyle;

        // ------------------------------------------------------------------ drawing helpers

        GUIStyle TextStyle(int size, TextAnchor anchor, bool wrap)
        {
            int key = size * 100 + (int)anchor * 2 + (wrap ? 1 : 0);
            if (!arenaStyles.TryGetValue(key, out var s))
            {
                s = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = anchor, wordWrap = wrap, richText = false, fontStyle = FontStyle.Bold };
                s.normal.textColor = Color.white;
                s.padding = new RectOffset(0, 0, 0, 0);
                arenaStyles[key] = s;
            }
            return s;
        }

        /// <summary>Outlined text in a colour, like the HUD's plates.</summary>
        void Txt(Rect r, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter, bool wrap = false)
        {
            var old = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, color.a * old.a);
            UiSkin.Outlined(r, text, TextStyle(size, anchor, wrap), size >= 16 ? 1.5f : 1.1f);
            GUI.color = old;
        }

        /// <summary>A tap over a drawn rectangle (the label only names it for the smoke test, it is not drawn).</summary>
        bool Hit(Rect r, string label)
        {
            if (hitStyle == null)
            {
                hitStyle = new GUIStyle { fontSize = 1 };
                var clear = new Color(0, 0, 0, 0);
                hitStyle.normal.textColor = clear;
                hitStyle.hover.textColor = clear;
                hitStyle.active.textColor = clear;
                hitStyle.focused.textColor = clear;
            }
            if (!GUI.Button(r, label, hitStyle))
                return false;
            sounds?.Play("Click", 0.4f);
            return true;
        }

        /// <summary>A wooden plank button; "on" is the gold-trimmed one; an inactive one is greyed and ignores taps.</summary>
        bool Plank(Rect r, string label, bool on = false, bool active = true, int size = 14, string key = null)
        {
            var b = active ? Pressable("pvp" + (key ?? label), r) : r;
            var old = GUI.color;
            if (!active)
                GUI.color = new Color(0.55f, 0.55f, 0.55f, old.a);
            ArtLibrary.DrawSliced(b, on ? "UI/ButtonOn" : "UI/Button", 20);
            Txt(b, label, size, on ? GoldText : CreamText);
            GUI.color = old;
            return active && Hit(r, label);
        }

        static void Card(Rect r) => ArtLibrary.DrawSliced(r, "UI/Pill", 30);

        static void Paper(Rect r) => ArtLibrary.DrawSliced(r, "UI/Panel", 40, 12);

        static void Bar(Rect r, float share, Color fill)
        {
            var old = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.55f * old.a);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            if (share > 0)
            {
                GUI.color = new Color(fill.r, fill.g, fill.b, old.a);
                GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Min(1, share), r.height), Texture2D.whiteTexture);
            }
            GUI.color = old;
        }

        static Color LeagueColor(int league)
        {
            switch (league)
            {
                case 0: return new Color(0.66f, 0.64f, 0.62f, 1);   // Пепел
                case 1: return new Color(0.88f, 0.56f, 0.3f, 1);    // Бронза
                case 2: return new Color(0.8f, 0.86f, 0.95f, 1);    // Серебро
                case 3: return new Color(1f, 0.82f, 0.3f, 1);       // Золото
                case 4: return new Color(1f, 0.45f, 0.22f, 1);      // Пламя
                default: return new Color(0.78f, 0.5f, 1f, 1);      // Легенда Очага
            }
        }

        /// <summary>The league's badge: a plaque in the league's colour with the laurel of Glory on it.</summary>
        static void LeagueBadge(Rect r, int league)
        {
            var old = GUI.color;
            GUI.color = LeagueColor(league);
            ArtLibrary.DrawSliced(r, "UI/Panel", 40, r.width * 0.24f);
            GUI.color = old;
            float pad = r.width * 0.1f;
            ArtLibrary.Draw(new Rect(r.x + pad, r.y + pad, r.width - 2 * pad, r.height - 2 * pad), "Icons/Glory");
        }

        /// <summary>A hero's face in a dark frame with his level in the corner.</summary>
        void Face(Rect r, HeroClass c, int level, bool levelShown = true)
        {
            ArtLibrary.DrawSliced(r, "UI/Pill", 30, Mathf.Min(8, r.width * 0.2f));
            float pad = Mathf.Max(2, r.width * 0.06f);
            ArtLibrary.Draw(new Rect(r.x + pad, r.y + pad, r.width - 2 * pad, r.height - 2 * pad), "Portraits/" + ArtLibrary.HeroSheet(c));
            if (levelShown)
                Txt(new Rect(r.x, r.yMax - 15, r.width - 3, 14), level.ToString(), 11, CreamText, TextAnchor.MiddleCenter);
        }

        /// <summary>An icon with an amount after it, inside a rectangle (centred).</summary>
        void IconAmount(Rect r, string icon, string text, Color color, int size = 13)
        {
            float icon_ = r.height;
            float textW = Mathf.Min(r.width - icon_, 8 + text.Length * size * 0.62f);
            float x = r.x + (r.width - icon_ - textW) / 2;
            ArtLibrary.Draw(new Rect(x, r.y, icon_, icon_), icon);
            Txt(new Rect(x + icon_ + 2, r.y, textW, r.height), text, size, color, TextAnchor.MiddleLeft);
        }

        Rect Row(float height) => GUILayoutUtility.GetRect(viewWidth, height, GUILayout.Width(viewWidth), GUILayout.Height(height));

        static Rect Inset(Rect r, float x, float y) => new Rect(r.x + x, r.y + y, r.width - 2 * x, r.height - 2 * y);

        static string Signed(long v) => v > 0 ? "+" + v : v < 0 ? "−" + (-v) : "0";

        // ------------------------------------------------------------------ the arena

        void DrawArena(long now)
        {
            var p = session.Balance.Activities.Pvp;
            if (!session.ArenaOpen)
            {
                DrawClosed("Арена очагов", string.Format("Откроется на {0}-м уровне очага (сейчас {1}). Здесь ваш отряд бьётся с обороной " +
                    "других очагов: рейтинг, лиги, недельные награды и Лавка славы.", p.ArenaUnlock, session.State.HearthLevel));
                return;
            }
            DrawArenaHeader(now);
            GUILayout.Space(6);
            if (pvpResult != null && !pvpResult.Raid)
            {
                DrawPvpResult();
                return;
            }
            arenaTab = Tabs(ArenaTabs, arenaTab);
            GUILayout.Space(8);
            switch (arenaTab)
            {
                case 0:
                    DrawOpponents(now);
                    break;
                case 1:
                    DrawArenaDefense();
                    break;
                case 2:
                    DrawGloryShop(now);
                    break;
                case 3:
                    DrawJournal(now, false);
                    break;
                default:
                    DrawLeagues(now);
                    break;
            }
        }

        void DrawClosed(string title, string text)
        {
            var r = Row(150);
            var box = new Rect(r.x + r.width * 0.15f, r.y + 10, r.width * 0.7f, 130);
            Card(box);
            Txt(new Rect(box.x, box.y + 12, box.width, 26), title, 18, GoldText);
            Txt(Inset(new Rect(box.x, box.y + 40, box.width, 84), 24, 0), text, 13, CreamText, TextAnchor.MiddleCenter, true);
        }

        int Tabs(string[] names, int selected)
        {
            var r = Row(34);
            float gap = 6, w = (r.width - gap * (names.Length - 1)) / names.Length;
            for (int i = 0; i < names.Length; i++)
            {
                if (Plank(new Rect(r.x + i * (w + gap), r.y, w, r.height), names[i], i == selected, true, 14, "tab" + names[i]) && i != selected)
                {
                    selected = i;
                    windowScroll = Vector2.zero;
                }
            }
            return selected;
        }

        void DrawArenaHeader(long now)
        {
            var st = session.State;
            var p = session.Balance.Activities.Pvp;
            var r = Row(64);
            int rating = st.ArenaRating == 0 ? p.StartRating : st.ArenaRating;
            int league = session.LeagueIndex(rating);

            // the league: its badge, its name, the rating and the way to the next league
            LeagueBadge(new Rect(r.x, r.y, 64, 64), league);
            float lw = Mathf.Min(230, r.width * 0.3f);
            var info = new Rect(r.x + 70, r.y, lw, 64);
            Txt(new Rect(info.x, info.y + 2, info.width, 22), "Лига «" + p.Leagues[league].Name + "»", 16, LeagueColor(league), TextAnchor.MiddleLeft);
            Txt(new Rect(info.x, info.y + 22, info.width, 18), "Рейтинг " + rating, 13, CreamText, TextAnchor.MiddleLeft);
            var next = session.NextLeague(rating);
            if (next != null)
            {
                int from = p.Leagues[league].Rating;
                Bar(new Rect(info.x, info.y + 44, info.width - 4, 7), (float)(rating - from) / Math.Max(1, next.Rating - from), LeagueColor(league + 1));
                Txt(new Rect(info.x, info.y + 50, info.width, 14), string.Format("до «{0}»: {1}", next.Name, next.Rating - rating), 11, DimText, TextAnchor.MiddleLeft);
            }
            else
            {
                Txt(new Rect(info.x, info.y + 44, info.width, 16), "высшая лига", 11, GoldText, TextAnchor.MiddleLeft);
            }

            // tickets, the week, Glory and wins: three plates
            float x = info.xMax + 6, w = (r.xMax - x - 12) / 3;
            int tickets = session.ArenaTickets(now);
            var plate = new Rect(x, r.y + 4, w, 56);
            Card(plate);
            ArtLibrary.Draw(new Rect(plate.x + 6, plate.y + 10, 36, 36), "Buttons/Fights");
            long price = session.ArenaTicketPrice(now);
            float plus = price > 0 ? 30 : 0;
            Txt(new Rect(plate.x + 44, plate.y + 6, plate.width - 50 - plus, 24), string.Format("Билеты {0}/{1}", tickets, p.Tickets), 14,
                tickets > 0 ? CreamText : BadText, TextAnchor.MiddleLeft);
            Txt(new Rect(plate.x + 44, plate.y + 30, plate.width - 50 - plus, 18), st.ArenaTicketAt > 0 ? "+1 через " + Duration(st.ArenaTicketAt - now) : "запас полон",
                11, DimText, TextAnchor.MiddleLeft);
            if (price > 0)
            {
                var add = new Rect(plate.xMax - 34, plate.y + 13, 30, 30);
                ArtLibrary.Draw(Pressable("pvpticket", add), "UI/Plus");
                if (Hit(add, "Билет +"))
                {
                    var fail = session.BuyArenaTicket(now);
                    Report(fail, "Coins");
                    if (fail == Fail.None)
                        message = "Билет куплен за " + price + " искр";
                }
            }

            plate.x += w + 6;
            Card(plate);
            Txt(new Rect(plate.x + 8, plate.y + 6, plate.width - 16, 24), "Неделя: " + Duration(session.ArenaWeekEnds(now) - now), 14, CreamText, TextAnchor.MiddleLeft);
            Txt(new Rect(plate.x + 8, plate.y + 30, plate.width - 16, 18), st.ArenaWeekFights > 0 ? "боёв на неделе: " + st.ArenaWeekFights : "нужен бой для награды",
                11, st.ArenaWeekFights > 0 ? DimText : EvenText, TextAnchor.MiddleLeft);

            plate.x += w + 6;
            Card(plate);
            ArtLibrary.Draw(new Rect(plate.x + 6, plate.y + 10, 36, 36), "Icons/Glory");
            Txt(new Rect(plate.x + 44, plate.y + 6, plate.width - 50, 24), "Слава " + ShortNum(st.Wallet[ResourceType.Glory]), 14, GoldText, TextAnchor.MiddleLeft);
            Txt(new Rect(plate.x + 44, plate.y + 30, plate.width - 50, 18), "побед: " + st.ArenaWins, 11, DimText, TextAnchor.MiddleLeft);
        }

        // ------------------------------------------------------------------ opponents

        void DrawOpponents(long now)
        {
            var list = session.ArenaOpponents(now);
            int tickets = session.ArenaTickets(now);
            double ours = session.SquadPower();
            var p = session.Balance.Activities.Pvp;
            var r = Row(250);
            float gap = 10, w = (r.width - gap * 2) / 3;
            for (int i = 0; i < list.Count && i < 3; i++)
            {
                var o = list[i];
                var c = new Rect(r.x + i * (w + gap), r.y, w, r.height);
                float y = OpponentCard(c, o, ours);
                // what a win or a loss brings
                var reward = new Rect(c.x + 10, y, c.width - 20, 46);
                Paper(reward);
                float half = reward.width / 2;
                Txt(new Rect(reward.x, reward.y + 3, half, 16), "Победа", 11, GoodText);
                Txt(new Rect(reward.x + half, reward.y + 3, half, 16), "Поражение", 11, BadText);
                IconAmount(new Rect(reward.x + 4, reward.y + 21, half - 8, 20), "Icons/Glory",
                    Signed(session.ArenaRatingChange(o, true)) + " · " + p.WinGlory, CreamText, 12);
                IconAmount(new Rect(reward.x + half + 4, reward.y + 21, half - 8, 20), "Icons/Glory",
                    Signed(session.ArenaRatingChange(o, false)) + " · " + p.LossGlory, CreamText, 12);
                var go = new Rect(c.x + 14, c.yMax - 48, c.width - 28, 38);
                if (Plank(go, "В бой", tickets > 0, true, 17, "fight" + i))
                    StartPvp(session.StartArena(i, now, out var started), started, "Арена против " + o.Name);
            }

            GUILayout.Space(8);
            var row = Row(38);
            float bw = Mathf.Min(270, (row.width - 12) / 3);
            long wait = session.State.ArenaListAt - now;
            if (Plank(new Rect(row.x, row.y, bw, 36), wait > 0 ? "Обновить через " + Duration(wait) : "Обновить соперников", false, wait <= 0, 14, "reroll"))
                Report(session.RerollArena(now));
            long price = session.ArenaTicketPrice(now);
            if (Plank(new Rect(row.x + bw + 6, row.y, bw, 36), price > 0 ? "Билет за " + price + " искр" : "Билеты на сегодня куплены", false, price > 0, 14, "buy"))
            {
                var fail = session.BuyArenaTicket(now);
                Report(fail, "Coins");
            }
            Txt(new Rect(row.x + 2 * bw + 18, row.y, row.width - 2 * bw - 18, 36), "Бой стоит билет · своя мощь " + ShortNum(ours), 12, DimText,
                TextAnchor.MiddleLeft, true);
        }

        /// <summary>The top of an opponent's card (name, hearth and rating, the squad, the might); returns where it ends.</summary>
        float OpponentCard(Rect c, PvpOpponent o, double ours)
        {
            Card(c);
            float y = c.y + 8;
            Txt(new Rect(c.x + 8, y, c.width - 16, 22), o.Name, 17, GoldText);
            y += 22;
            Txt(new Rect(c.x + 8, y, c.width - 16, 18), string.Format("очаг {0} · рейтинг {1}", o.HearthLevel, o.Rating), 12, DimText);
            y += 22;
            var squad = session.OpponentSquad(o);
            float size = Mathf.Min(40, (c.width - 20) / squad.Count - 4);
            float x = c.x + (c.width - squad.Count * (size + 4) + 4) / 2;
            for (int k = 0; k < squad.Count; k++)
                Face(new Rect(x + k * (size + 4), y, size, size), squad[k].Class, squad[k].Level, size >= 30);
            y += size + 6;
            double theirs = ours * o.Power;
            var color = o.Power < 0.95 ? GoodText : o.Power <= 1.05 ? EvenText : BadText;
            string verdict = o.Power < 0.95 ? "слабее вас" : o.Power <= 1.05 ? "равный" : "сильнее вас";
            Txt(new Rect(c.x + 8, y, c.width - 16, 20), "Мощь " + ShortNum(theirs) + " · " + verdict, 13, color);
            y += 20;
            Txt(new Rect(c.x + 8, y, c.width - 16, 16), "ваша мощь " + ShortNum(ours), 11, DimText);
            return y + 20;
        }

        /// <summary>Starts an arena fight, a raid or a revenge; when it ends, the result panel shows what it brought.</summary>
        void StartPvp(Fail fail, Battle started, string title)
        {
            Report(fail);
            if (fail != Fail.None)
                return;
            var pb = session.State.PvpBattle;
            int before = session.State.ArenaRating;
            long essence = session.State.Wallet[ResourceType.Essence];
            BeginBattle(started, done =>
            {
                session.FinishPvp(done, Now(), out var r);
                pvpResult = new PvpResult
                {
                    Won = r.Won, Raid = pb.Raid, Revenge = pb.Revenge, Opponent = pb.Opponent.Name, Stars = r.Stars, RatingBefore = before,
                    RatingAfter = session.State.ArenaRating, Glory = r.Got[ResourceType.Glory], Materials = r.Materials,
                    Essence = session.State.Wallet[ResourceType.Essence] - essence,
                };
                if (pb.Raid)
                    return string.Format("{0} · добыча {1} материалов{2}", StarText(r.Stars), r.Materials,
                        r.Got[ResourceType.Glory] > 0 ? " · трофеи +" + r.Got[ResourceType.Glory] + " славы" : "");
                return string.Format("{0} · рейтинг {1:+0;-0} · +{2} славы", done.Won ? "Победа" : "Поражение", r.RatingChange, r.Got[ResourceType.Glory]);
            }, title);
        }

        void DrawPvpResult()
        {
            var res = pvpResult;
            var p = session.Balance.Activities.Pvp;
            var r = Row(290);
            var box = new Rect(r.x + Mathf.Max(0, (r.width - 520) / 2), r.y + 4, Mathf.Min(520, r.width), 280);
            Card(box);
            Txt(new Rect(box.x, box.y + 10, box.width, 40), res.Raid ? (res.Stars == 3 ? "КРЕПОСТЬ ВЗЯТА" : res.Stars > 0 ? "НАБЕГ" : "НАБЕГ ОТБИТ")
                : res.Won ? "ПОБЕДА" : "ПОРАЖЕНИЕ", 28, res.Won || res.Stars > 0 ? GoldText : BadText);
            Txt(new Rect(box.x, box.y + 50, box.width, 20), (res.Revenge ? "Месть: " : res.Raid ? "Крепость " : "против ") + res.Opponent, 14, CreamText);
            float y = box.y + 74;
            if (res.Raid || res.Won)
            {
                Txt(new Rect(box.x, y, box.width, 30), StarText(res.Stars), 24, GoldText);
                y += 32;
            }
            if (!res.Raid)
            {
                int change = res.RatingAfter - res.RatingBefore;
                Txt(new Rect(box.x, y, box.width, 24), string.Format("Рейтинг {0} → {1} ({2})", res.RatingBefore, res.RatingAfter, Signed(change)), 17,
                    change >= 0 ? GoodText : BadText);
                y += 26;
                int was = session.LeagueIndex(res.RatingBefore), now = session.LeagueIndex(res.RatingAfter);
                if (was != now)
                {
                    Txt(new Rect(box.x, y, box.width, 20), (now > was ? "Новая лига: «" : "Лига снизилась: «") + p.Leagues[now].Name + "»", 14, LeagueColor(now));
                    y += 22;
                }
            }
            // the rewards: one plate per resource
            var got = new List<(string icon, string text)>();
            if (res.Glory > 0)
                got.Add(("Icons/Glory", "+" + res.Glory));
            if (res.Essence > 0)
                got.Add(("Icons/Essence", "+" + ShortNum(res.Essence)));
            if (res.Materials > 0)
                got.Add(("Icons/Iron", "+" + ShortNum(res.Materials)));
            float pw = 120, px = box.center.x - (got.Count * (pw + 8) - 8) / 2;
            foreach (var (icon, text) in got)
            {
                var plate = new Rect(px, y + 4, pw, 34);
                Paper(plate);
                IconAmount(Inset(plate, 6, 4), icon, text, CreamText, 15);
                px += pw + 8;
            }
            if (Plank(new Rect(box.center.x - 110, box.yMax - 50, 220, 40), "Продолжить", true, true, 16, "result"))
                pvpResult = null;
        }

        // ------------------------------------------------------------------ the arena defense

        void DrawArenaDefense()
        {
            var st = session.State;
            var heroes = st.Dwellers.Where(d => d.IsHero).OrderByDescending(session.HeroPower).ToList();
            var chosen = st.ArenaDefense.Select(session.FindDweller).Where(d => d != null && d.IsHero).ToList();
            var r = Row(36);
            Txt(r, "Оборона — до 5 героев; первый стоит впереди. На неё нападают дважды в день: удержали +4 рейтинга, пала −8. " +
                "Ресурсы не теряются никогда.", 12, DimText, TextAnchor.MiddleLeft, true);

            // the five places of the defense in their order: a tap takes the hero off
            var slots = Row(128);
            float gap = 8, sw = Mathf.Min(150, (slots.width - 4 * gap - 190) / 5);
            for (int i = 0; i < 5; i++)
            {
                var s = new Rect(slots.x + i * (sw + gap), slots.y, sw, 124);
                Card(s);
                Txt(new Rect(s.x + 8, s.y + 4, 24, 20), (i + 1).ToString(), 14, GoldText);
                if (i < chosen.Count)
                {
                    var d = chosen[i];
                    float face = Mathf.Min(64, sw - 20);
                    Face(new Rect(s.center.x - face / 2, s.y + 8, face, face), d.HeroClass, d.HeroLevel);
                    Txt(new Rect(s.x + 4, s.y + face + 12, s.width - 8, 18), d.Name, 12, CreamText);
                    Txt(new Rect(s.x + 4, s.y + face + 30, s.width - 8, 16), "мощь " + ShortNum(session.HeroPower(d)), 11, DimText);
                    if (Hit(s, "Снять из обороны " + d.Name))
                        Report(session.SetArenaDefense(chosen.Where(h => h != d).Select(h => h.Id).ToList()));
                }
                else
                {
                    Txt(Inset(s, 10, 30), chosen.Count == 0 && i == 0 ? "пусто: защищают сильнейшие" : "свободно", 12, DimText, TextAnchor.MiddleCenter, true);
                }
            }
            var side = new Rect(slots.x + 5 * (sw + gap), slots.y, slots.width - 5 * (sw + gap), 124);
            double power = session.DefensePower();
            Txt(new Rect(side.x, side.y, side.width, 20), "Мощь обороны " + ShortNum(power), 14, GoldText);
            Txt(new Rect(side.x, side.y + 20, side.width, 18), string.Format("держит {0:0}% нападений", session.ArenaDefenseHold() * 100), 12,
                chosen.Count > 0 ? GoodText : DimText);
            if (Plank(new Rect(side.x, side.y + 44, side.width, 36), "Сильнейшие", false, true, 14, "defbest"))
                Report(session.SetArenaDefense(heroes.Take(5).Select(d => d.Id).ToList()));
            if (Plank(new Rect(side.x, side.y + 86, side.width, 36), "Очистить", false, chosen.Count > 0, 14, "defclear"))
                Report(session.SetArenaDefense(new int[0]));

            GUILayout.Space(8);
            var title = Row(22);
            Txt(title, heroes.Count == 0 ? "Героев пока нет: сделайте жителя героем в окне «Жители»" : "Герои — нажмите, чтобы поставить в оборону или снять",
                13, CreamText, TextAnchor.MiddleLeft);
            HeroGrid(heroes, d => chosen.Contains(d) ? (chosen.IndexOf(d) + 1).ToString() : null, d =>
            {
                var ids = chosen.Select(h => h.Id).ToList();
                if (ids.Contains(d.Id))
                    ids.Remove(d.Id);
                else if (ids.Count < 5)
                    ids.Add(d.Id);
                else
                {
                    message = "В обороне уже пятеро: снимите кого-нибудь";
                    return;
                }
                Report(session.SetArenaDefense(ids));
            }, "Оборона: ");
        }

        /// <summary>Small cards of heroes in rows; mark gives the badge of a chosen one (null: not chosen).</summary>
        void HeroGrid(List<Dweller> heroes, Func<Dweller, string> mark, Action<Dweller> tap, string label)
        {
            int per = Math.Max(2, (int)((viewWidth + 8) / 178));
            float gap = 8, w = (viewWidth - gap * (per - 1)) / per;
            for (int i = 0; i < heroes.Count; i += per)
            {
                var row = Row(70);
                for (int k = 0; k < per && i + k < heroes.Count; k++)
                {
                    var d = heroes[i + k];
                    var c = new Rect(row.x + k * (w + gap), row.y, w, 64);
                    string badge = mark(d);
                    if (badge != null)
                        ArtLibrary.DrawSliced(c, "UI/ButtonOn", 20);
                    else
                        Card(c);
                    Face(new Rect(c.x + 6, c.y + 6, 52, 52), d.HeroClass, d.HeroLevel, false);
                    Txt(new Rect(c.x + 62, c.y + 6, c.width - 66, 18), d.Name, 13, badge != null ? GoldText : CreamText, TextAnchor.MiddleLeft);
                    Txt(new Rect(c.x + 62, c.y + 24, c.width - 66, 16), ClassName(d.HeroClass) + ", ур. " + d.HeroLevel, 11, DimText, TextAnchor.MiddleLeft);
                    Txt(new Rect(c.x + 62, c.y + 40, c.width - 66, 16), "мощь " + ShortNum(session.HeroPower(d)), 11, DimText, TextAnchor.MiddleLeft);
                    if (badge != null)
                        Txt(new Rect(c.xMax - 26, c.y + 4, 22, 20), badge, 14, GoldText);
                    if (Hit(c, label + d.Name))
                        tap(d);
                }
            }
        }

        // ------------------------------------------------------------------ the Glory shop

        void DrawGloryShop(long now)
        {
            var p = session.Balance.Activities.Pvp;
            long glory = session.State.Wallet[ResourceType.Glory];
            var r = Row(28);
            IconAmount(new Rect(r.x, r.y, 150, 26), "Icons/Glory", "Слава " + ShortNum(glory), GoldText, 15);
            Txt(new Rect(r.x + 160, r.y, r.width - 160, 26), "Лимиты обновятся через " + Duration(session.GloryShopResets(now) - now) +
                " · Слава: 10 за победу, 3 за поражение, трофеи набегов и итоги недели", 12, DimText, TextAnchor.MiddleLeft, true);
            GUILayout.Space(6);
            var row = Row(220);
            int n = p.GloryShop.Count;
            float gap = 10, w = Mathf.Min(260, (row.width - gap * (n - 1)) / n);
            float x0 = row.x + (row.width - n * (w + gap) + gap) / 2;
            for (int g = 0; g < n; g++)
            {
                var goods = p.GloryShop[g];
                int left = session.GloryLeft(g, now);
                var c = new Rect(x0 + g * (w + gap), row.y, w, 216);
                Card(c);
                var icon = new Rect(c.center.x - 36, c.y + 10, 72, 72);
                switch (goods.Goods)
                {
                    case "Essence":
                        ArtLibrary.Draw(icon, "Icons/Essence");
                        break;
                    case "Hourglass":
                        ArtLibrary.Draw(icon, "UI/Round");
                        Txt(icon, (goods.Amount / 60).ToString("0.#") + " ч", 20, CreamText);
                        break;
                    default:
                        Face(icon, HeroClass.None, 0, false);
                        ArtLibrary.Draw(Inset(icon, 5, 5), "Portraits/" + ArtLibrary.DwellerSheet(3));
                        break;
                }
                Txt(new Rect(c.x + 6, c.y + 86, c.width - 12, 20), GloryName(goods.Goods), 14, GoldText);
                Txt(new Rect(c.x + 10, c.y + 106, c.width - 20, 34), GloryText(goods), 11, DimText, TextAnchor.MiddleCenter, true);
                Txt(new Rect(c.x + 6, c.y + 140, c.width - 12, 18), string.Format("осталось {0} из {1} на неделе", left, goods.PerWeek), 12,
                    left > 0 ? CreamText : BadText);
                bool can = left > 0 && glory >= goods.Price;
                var buy = new Rect(c.x + 14, c.yMax - 48, c.width - 28, 38);
                if (Plank(buy, left > 0 ? "Купить · " + goods.Price : "Распродано", can, can, 15, "glory" + g))
                {
                    var fail = session.BuyGlory(g, now);
                    Report(fail, "Coins");
                    if (fail == Fail.None)
                        message = "Куплено: " + GloryName(goods.Goods);
                }
                if (left > 0)
                    ArtLibrary.Draw(new Rect(buy.xMax - 34, buy.y + 5, 28, 28), "Icons/Glory");
            }
        }

        static string GloryName(string goods)
        {
            switch (goods)
            {
                case "Essence": return "Эссенция";
                case "Hourglass": return "Песочные часы";
                default: return "Печать редкого жителя";
            }
        }

        string GloryText(GloryGoodsData g)
        {
            switch (g.Goods)
            {
                case "Essence":
                    long amount = (long)Math.Round(session.Balance.Activities.EssenceByLevel[session.State.HearthLevel - 1] * g.Amount);
                    return "+" + ShortNum(amount) + " эссенции на уровни героев";
                case "Hourglass":
                    return "ускоряют любую стройку на " + (g.Amount / 60).ToString("0.#") + " ч";
                default:
                    return "житель с +3 ко всем качествам; нужно место в спальнях";
            }
        }

        // ------------------------------------------------------------------ the journal

        void DrawJournal(long now, bool raids)
        {
            var journal = session.State.PvpJournal;
            var shown = Enumerable.Range(0, journal.Count).Reverse().Where(i => journal[i].Raid == raids || !raids).ToList();
            if (shown.Count == 0)
            {
                Txt(Row(40), raids ? "Набегов пока не было" : "Боёв пока не было: нападите на соперника или подождите нападения на оборону", 13, DimText);
                return;
            }
            foreach (int i in shown)
            {
                var e = journal[i];
                var r = Row(58);
                var c = new Rect(r.x, r.y, r.width, 52);
                Card(c);
                string icon = e.Raid ? (e.Attacked ? "Buttons/Fortress" : "Buttons/Routes") : e.Attacked ? "Decor/shield" : "Buttons/Fights";
                ArtLibrary.Draw(new Rect(c.x + 10, c.y + 6, 40, 40), icon);
                string what = e.Attacked
                    ? (e.Raid ? e.Opponent.Name + " совершил набег на крепость" : e.Opponent.Name + " напал на вашу оборону")
                    : (e.Revenge ? "Месть: " : e.Raid ? "Набег на крепость " : "Вы напали на ") + e.Opponent.Name;
                float right = Mathf.Min(300, c.width * 0.42f);
                Txt(new Rect(c.x + 58, c.y + 6, c.width - 58 - right, 22), what, 13, CreamText, TextAnchor.MiddleLeft);
                Txt(new Rect(c.x + 58, c.y + 28, c.width - 58 - right, 18), Duration(Math.Max(0, now - e.Time)) + " назад · очаг " + e.Opponent.HearthLevel +
                    (e.Raid ? "" : " · рейтинг " + e.Opponent.Rating), 11, DimText, TextAnchor.MiddleLeft);
                string verdict = e.Attacked ? (e.Raid ? (e.Won ? "Отбились" : "Взяли крепость") : e.Won ? "Отбились" : "Оборона пала") : e.Won ? "Победа" : "Поражение";
                var res = new Rect(c.xMax - right, c.y, right - 130, 52);
                Txt(new Rect(res.x, res.y + 6, res.width, 20), verdict, 14, e.Won ? GoodText : BadText);
                string detail = (e.Raid ? StarText(e.Stars) : "рейтинг " + Signed(e.RatingChange)) + (e.Glory > 0 ? " · +" + e.Glory + " славы" : "");
                Txt(new Rect(res.x, res.y + 28, res.width, 18), detail, 11, DimText);
                var btn = new Rect(c.xMax - 124, c.y + 8, 114, 36);
                if (session.CanAvenge(e, now))
                {
                    if (Plank(btn, e.Raid ? "Месть" : "Месть · билет", true, true, 13, "revenge" + i))
                        StartPvp(session.StartRevenge(i, now, out var started), started, "Месть: " + e.Opponent.Name);
                }
                else if (e.Attacked && e.Avenged)
                {
                    Txt(btn, "отомщено", 12, DimText);
                }
            }
        }

        // ------------------------------------------------------------------ the leagues

        void DrawLeagues(long now)
        {
            var p = session.Balance.Activities.Pvp;
            int rating = session.State.ArenaRating == 0 ? p.StartRating : session.State.ArenaRating;
            int current = session.LeagueIndex(rating);
            long essenceDay = session.Balance.Activities.EssenceByLevel[session.State.HearthLevel - 1];
            float[] cols = { 0.34f, 0.18f, 0.16f, 0.16f, 0.16f };
            string[] heads = { "Лига", "Рейтинг от", "Искры", "Слава", "Эссенция" };
            var head = Row(24);
            float x = head.x;
            for (int k = 0; k < cols.Length; k++)
            {
                Txt(new Rect(x, head.y, head.width * cols[k], 22), heads[k], 12, DimText);
                x += head.width * cols[k];
            }
            for (int i = 0; i < p.Leagues.Count; i++)
            {
                var l = p.Leagues[i];
                var r = Row(40);
                var c = new Rect(r.x, r.y, r.width, 36);
                if (i == current)
                    ArtLibrary.DrawSliced(c, "UI/ButtonOn", 20);
                else
                    Card(c);
                LeagueBadge(new Rect(c.x + 8, c.y + 3, 30, 30), i);
                var color = i == current ? GoldText : CreamText;
                Txt(new Rect(c.x + 44, c.y, c.width * cols[0] - 44, 36), l.Name + (i == current ? " · вы здесь" : ""), 14, i == current ? GoldText : LeagueColor(i),
                    TextAnchor.MiddleLeft);
                x = c.x + c.width * cols[0];
                string[] cells = { l.Rating.ToString(), l.Sparks.ToString(), l.Glory.ToString(), ShortNum(Math.Round(essenceDay * l.EssenceDays)) };
                string[] icons = { null, "Icons/Sparks", "Icons/Glory", "Icons/Essence" };
                for (int k = 0; k < cells.Length; k++)
                {
                    var cell = new Rect(x, c.y + 7, c.width * cols[k + 1], 22);
                    if (icons[k] == null)
                        Txt(cell, cells[k], 14, color);
                    else
                        IconAmount(cell, icons[k], cells[k], color, 14);
                    x += c.width * cols[k + 1];
                }
            }
            GUILayout.Space(4);
            var foot = Row(40);
            Txt(foot, string.Format("Награда лиги приходит в воскресенье в 22:00 (через {0}) тем, кто дрался на неделе: боёв сейчас {1}. " +
                "Сезон — 6 недель, затем рейтинг сжимается к {2} наполовину.", Duration(session.ArenaWeekEnds(now) - now), session.State.ArenaWeekFights,
                p.StartRating), 12, DimText, TextAnchor.MiddleCenter, true);
        }

        // ------------------------------------------------------------------ raids

        void DrawRaids(long now)
        {
            var p = session.Balance.Activities.Pvp;
            if (!session.RaidsOpen)
            {
                DrawClosed("Набеги", string.Format("Откроются на {0}-м уровне очага (сейчас {1}). Отряд штурмует чужую крепость этаж за этажом; " +
                    "никто ничего не теряет — добычу выдаёт игра.", p.RaidUnlock, session.State.HearthLevel));
                return;
            }
            DrawRaidHeader(now);
            GUILayout.Space(6);
            if (pvpResult != null && pvpResult.Raid)
            {
                DrawPvpResult();
                return;
            }
            raidTab = Tabs(RaidTabs, raidTab);
            GUILayout.Space(8);
            switch (raidTab)
            {
                case 0:
                    DrawRaidTargets(now);
                    break;
                case 1:
                    DrawRaidDefense(now);
                    break;
                default:
                    DrawJournal(now, true);
                    break;
            }
        }

        void DrawRaidHeader(long now)
        {
            var st = session.State;
            var p = session.Balance.Activities.Pvp;
            var r = Row(56);
            float w = (r.width - 12) / 3;
            var plate = new Rect(r.x, r.y, w, 56);
            Card(plate);
            ArtLibrary.Draw(new Rect(plate.x + 6, plate.y + 10, 36, 36), "Buttons/Fights");
            int flags = session.RaidFlags(now);
            Txt(new Rect(plate.x + 44, plate.y + 6, plate.width - 50, 24), string.Format("Флаги {0}/{1}", flags, p.Flags), 14, flags > 0 ? CreamText : BadText,
                TextAnchor.MiddleLeft);
            Txt(new Rect(plate.x + 44, plate.y + 30, plate.width - 50, 18), st.RaidFlagAt > 0 ? "+1 через " + Duration(st.RaidFlagAt - now) : "запас полон", 11,
                DimText, TextAnchor.MiddleLeft);
            plate.x += w + 6;
            Card(plate);
            Txt(new Rect(plate.x + 8, plate.y + 6, plate.width - 16, 24), "Звёзды: этаж за этажом", 14, CreamText, TextAnchor.MiddleLeft);
            Txt(new Rect(plate.x + 8, plate.y + 30, plate.width - 16, 18), string.Join(" · ", GameSession.RaidStarNames), 11, DimText, TextAnchor.MiddleLeft);
            plate.x += w + 6;
            Card(plate);
            ArtLibrary.Draw(new Rect(plate.x + 6, plate.y + 10, 36, 36), "Icons/Glory");
            Txt(new Rect(plate.x + 44, plate.y + 6, plate.width - 50, 24), "Слава " + ShortNum(st.Wallet[ResourceType.Glory]), 14, GoldText, TextAnchor.MiddleLeft);
            Txt(new Rect(plate.x + 44, plate.y + 30, plate.width - 50, 18), "крепостей взято: " + st.RaidWins, 11, DimText, TextAnchor.MiddleLeft);
        }

        void DrawRaidTargets(long now)
        {
            var p = session.Balance.Activities.Pvp;
            var list = session.RaidTargets(now);
            int flags = session.RaidFlags(now);
            double ours = session.SquadPower();
            long loot = (long)Math.Round(session.IncomePerHour() * p.LootHours);
            if (list.Count == 0)
            {
                Txt(Row(40), "Все крепости взяты — новые появятся после обновления", 13, DimText);
            }
            else
            {
                var r = Row(250);
                float gap = 10, w = (r.width - gap * 2) / 3;
                for (int i = 0; i < list.Count; i++)
                {
                    var o = list[i];
                    var c = new Rect(r.x + i * (w + gap), r.y, w, r.height);
                    float y = OpponentCard(c, o, ours);
                    var reward = new Rect(c.x + 10, y, c.width - 20, 46);
                    Paper(reward);
                    Txt(new Rect(reward.x, reward.y + 3, reward.width, 16), "Добыча за 3 звезды", 11, GoodText);
                    float half = reward.width / 2;
                    IconAmount(new Rect(reward.x + 4, reward.y + 21, half - 8, 20), "Icons/Iron", "до " + ShortNum(loot), CreamText, 12);
                    IconAmount(new Rect(reward.x + half + 4, reward.y + 21, half - 8, 20), "Icons/Glory", "+" + p.TrophyGlory, CreamText, 12);
                    if (Plank(new Rect(c.x + 14, c.yMax - 48, c.width - 28, 38), "Набег", flags > 0, true, 17, "raid" + i))
                        StartPvp(session.StartRaid(i, false, now, out var started), started, "Набег: " + o.Name);
                }
            }
            var revenge = session.State.Revenge;
            for (int i = 0; i < revenge.Count; i++)
            {
                var o = revenge[i];
                GUILayout.Space(6);
                var row = Row(48);
                Card(row);
                ArtLibrary.Draw(new Rect(row.x + 10, row.y + 6, 36, 36), "Buttons/Fortress");
                Txt(new Rect(row.x + 54, row.y + 4, row.width - 220, 22), o.Name + " напал на вашу крепость", 13, CreamText, TextAnchor.MiddleLeft);
                Txt(new Rect(row.x + 54, row.y + 26, row.width - 220, 18), "месть бесплатна, добыча ×2 · ещё " + Duration(o.Until - now), 11, DimText, TextAnchor.MiddleLeft);
                if (Plank(new Rect(row.xMax - 150, row.y + 6, 140, 36), "МЕСТЬ", true, true, 15, "raidrevenge" + i))
                    StartPvp(session.StartRaid(i, true, now, out var started), started, "Месть: " + o.Name);
            }
        }

        // ------------------------------------------------------------------ the raid defense of the fortress

        void DrawRaidDefense(long now)
        {
            var st = session.State;
            var defense = session.RaidDefense;
            raidLine = Mathf.Clamp(raidLine, 0, GameSession.RaidLines - 1);
            Txt(Row(36), "Набег идёт сверху вниз и останавливается на линии, которая устояла. Ловушки куёт кузня, на линию встают до двух героев " +
                "(всего пятеро). За каждую неприступную линию — " + GameSession.RaidDenyGlory + " славы.", 12, DimText, TextAnchor.MiddleLeft, true);
            for (int line = 0; line < GameSession.RaidLines; line++)
            {
                var l = defense[line];
                var r = Row(96);
                var c = new Rect(r.x, r.y, r.width, 90);
                if (line == raidLine)
                    ArtLibrary.DrawSliced(c, "UI/ButtonOn", 20);
                else
                    Card(c);
                // the line: its name, what a ghost takes there, how often it holds
                float nameW = Mathf.Min(190, c.width * 0.24f);
                Txt(new Rect(c.x + 12, c.y + 8, nameW, 22), (line + 1) + ". " + GameSession.RaidLineNames[line], 15, line == raidLine ? GoldText : CreamText,
                    TextAnchor.MiddleLeft);
                Txt(new Rect(c.x + 12, c.y + 30, nameW, 18), "звезда: " + GameSession.RaidStarNames[line], 11, DimText, TextAnchor.MiddleLeft);
                double hold = session.RaidHold(line);
                Txt(new Rect(c.x + 12, c.y + 50, nameW, 20), string.Format("держит {0:0}%", hold * 100), 13, hold >= 0.5 ? GoodText : hold >= 0.3 ? EvenText : BadText,
                    TextAnchor.MiddleLeft);
                if (Hit(new Rect(c.x, c.y, nameW + 12, c.height), "Линия " + (line + 1)))
                    raidLine = line;

                // the trap of the line
                float tx = c.x + nameW + 18, tw = Mathf.Min(90, (c.width - nameW - 18 - 160) / 4 - 4);
                Txt(new Rect(tx, c.y + 6, tw * 4, 18), "Ловушка кузни", 11, DimText, TextAnchor.MiddleLeft);
                for (int k = 0; k < TrapShort.Length; k++)
                {
                    var trap = (TrapKind)k;
                    bool open = session.TrapOpen(trap);
                    var b = new Rect(tx + k * (tw + 4), c.y + 26, tw, 34);
                    string label = open ? TrapShort[k] : "с " + GameSession.TrapUnlock[k];
                    if (Plank(b, label, l.Trap == trap, open, 12, "trap" + line + k) && l.Trap != trap)
                        Report(session.SetRaidTrap(line, trap));
                }
                Txt(new Rect(tx, c.y + 62, tw * 4, 18), l.Trap == TrapKind.None ? (session.ForgeLevel() == 0 ? "нужна кузня" : "ловушки нет")
                    : string.Format("{0}: +{1:0}% к обороне", GameSession.TrapNames[(int)l.Trap], session.TrapHold(l.Trap) * 100), 11, DimText, TextAnchor.MiddleLeft);

                // the heroes on guard: a tap takes one off, an empty place chooses the line
                float fx = c.xMax - 2 * 74 - 6;
                for (int k = 0; k < GameSession.RaidLineDefenders; k++)
                {
                    var slot = new Rect(fx + k * 74, c.y + 8, 68, 74);
                    var d = k < l.Defenders.Count ? session.FindDweller(l.Defenders[k]) : null;
                    if (d != null && d.IsHero)
                    {
                        Face(new Rect(slot.x + 4, slot.y, 60, 60), d.HeroClass, d.HeroLevel);
                        Txt(new Rect(slot.x - 4, slot.y + 60, slot.width + 8, 14), d.Name, 10, CreamText);
                        if (Hit(slot, "Снять с линии " + d.Name))
                            Report(session.SetRaidDefenders(line, l.Defenders.Where(id => id != d.Id).ToList()));
                    }
                    else
                    {
                        Paper(new Rect(slot.x + 4, slot.y, 60, 60));
                        Txt(new Rect(slot.x + 4, slot.y, 60, 60), "+", 26, CreamText);
                        if (Hit(slot, "Защитник линии " + (line + 1)))
                            raidLine = line;
                    }
                }
            }

            GUILayout.Space(4);
            var heroes = st.Dwellers.Where(d => d.IsHero).OrderByDescending(session.HeroPower).ToList();
            Txt(Row(22), "Герои — нажмите, чтобы поставить на линию " + (raidLine + 1) + " («" + GameSession.RaidLineNames[raidLine] + "») или снять", 13,
                CreamText, TextAnchor.MiddleLeft);
            HeroGrid(heroes, d =>
            {
                int at = defense.FindIndex(l => l.Defenders.Contains(d.Id));
                return at < 0 ? null : (at + 1).ToString();
            }, d =>
            {
                var ids = defense[raidLine].Defenders.ToList();
                if (ids.Contains(d.Id))
                    ids.Remove(d.Id);
                else if (ids.Count < GameSession.RaidLineDefenders)
                    ids.Add(d.Id);
                else
                    ids[ids.Count - 1] = d.Id;
                var fail = session.SetRaidDefenders(raidLine, ids);
                Report(fail);
                if (fail == Fail.LimitReached)
                    message = "В обороне крепости не больше пяти героев";
            }, "Крепость: ");

            DrawFortifications(now);
        }

        void DrawFortifications(long now)
        {
            GUILayout.Space(6);
            if (session.State.HearthLevel < GameSession.FortificationUnlock)
            {
                Txt(Row(26), "Укрепления (стены, сторожевые огни) откроются на " + GameSession.FortificationUnlock + "-м уровне очага", 12, DimText);
                return;
            }
            var siege = session.ActiveRun("Siege", now);
            var r = Row(96);
            float gap = 10, w = (r.width - gap) / 2;
            string[] icons = { "Buttons/Fortress", "Icons/Ember" };
            string[] effects = { "+3% к обороне каждой линии за уровень", "+6% к обороне очага за уровень" };
            for (int k = 0; k < GameSession.FortificationNames.Length; k++)
            {
                var c = new Rect(r.x + k * (w + gap), r.y, w, 90);
                Card(c);
                int level = session.FortificationLevel(k);
                ArtLibrary.Draw(new Rect(c.x + 10, c.y + 13, 64, 64), icons[k]);
                Txt(new Rect(c.x + 82, c.y + 8, c.width - 90, 22), string.Format("{0} · {1}/{2}", GameSession.FortificationNames[k], level, GameSession.FortificationMax),
                    15, GoldText, TextAnchor.MiddleLeft);
                Txt(new Rect(c.x + 82, c.y + 30, c.width - 90, 16), effects[k], 11, DimText, TextAnchor.MiddleLeft);
                bool more = level < GameSession.FortificationMax;
                float bw = siege != null ? (c.width - 96) / 2 : c.width - 92;
                long glory = session.FortificationCost(k, false);
                if (Plank(new Rect(c.x + 82, c.y + 48, bw, 34), more ? "Слава " + glory : "Предел", false,
                        more && session.State.Wallet[ResourceType.Glory] >= glory, 13, "fortg" + k))
                    Report(session.UpgradeFortification(k, false, now));
                if (siege != null)
                {
                    long stone = session.FortificationCost(k, true);
                    if (Plank(new Rect(c.x + 86 + bw, c.y + 48, bw, 34), more ? "Камень " + stone + " (" + siege.Stone + ")" : "Предел", false,
                            more && siege.Stone >= stone, 13, "forts" + k))
                        Report(session.UpgradeFortification(k, true, now));
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core;
using LastHearth.Core.Combat;
using UnityEngine;

namespace LastHearth.Game
{
    /// <summary>
    /// The "События" window, like Hustle Castle's: a tab per event (running, ended with a reward or a
    /// shop still open, announced for tomorrow), a banner with the event's picture, its countdown and
    /// «Играть», the track of ten milestones with their rewards, the event's own board (the Reaper, the
    /// doors of the Portal, the map of the treasure, the caravan's rates, the outposts, the lanterns) and
    /// its shop. Everything is drawn into rectangles of the window's layout, the labels centred and outlined.
    /// </summary>
    public sealed partial class GameRunner
    {
        int lanternSeed;
        float lanternClock;
        readonly List<(double time, int spot)> lanternTaps = new List<(double time, int spot)>();
        int fairSell, fairBuy = 2;
        string eventTab = "";
        bool eventShop;
        int siegeStand = -1;
        GUIStyle evTitle, evText, evLeft, evSmall, evSmallLeft, evNote, evHit;

        static readonly string[] FairGoodsNames = { "уголь", "еда", "железо", "серебро" };
        static readonly string[] FairGoodsIcons = { "Icons/Coal", "Icons/Food", "Icons/Iron", "Icons/Silver" };
        static readonly string[] TournamentRules = { "Сталь: ближний бой +30%, магия −50%", "Холод: лечение −50%", "Рыцари Двора: все в броне" };
        static readonly Color EvCream = new Color(1f, 0.94f, 0.8f, 1f);
        static readonly Color EvGold = new Color(1f, 0.84f, 0.42f, 1f);

        // ------------------------------------------------------------------ texts of the events

        static string EventShort(string id)
        {
            switch (id)
            {
                case "LongNight": return "Долгая ночь";
                case "Portal": return "Портал";
                case "Tournament": return "Турнир";
                case "Treasure": return "Кладоискатели";
                case "Fair": return "Ярмарка";
                case "Siege": return "Осада";
                default: return "Праздник";
            }
        }

        static string EventTagline(string id)
        {
            switch (id)
            {
                case "LongNight": return "Еженедельный босс: вся группа бьёт Ночного Жнеца";
                case "Portal": return "Башня-рогалик: 30 этажей, двери, дары Угля";
                case "Tournament": return "Правило дня: в зачёт идёт лучший бой дня";
                case "Treasure": return "Карта в тумане: найдите казну Стражей";
                case "Fair": return "Бартер каравана: курсы меняются каждые 4 часа";
                case "Siege": return "Пять застав, штурмы в 12–14 и 20–22";
                default: return "Фонари на стенах и праздничные задания";
            }
        }

        /// <summary>The picture of an event: painted art only (the stubs of the smoke test throw on a missing one).</summary>
        string EventArt(string id, EventRun run, long start)
        {
            switch (id)
            {
                case "LongNight": return "Portraits/" + ArtLibrary.CreatureSheet(run != null ? session.NightFamily(run) : session.NightFamilyAt(start));
                case "Portal": return ArtLibrary.Data.Scene ? "Scene/Portal" : "Buttons/Events";
                case "Tournament": return "Buttons/Events";
                case "Treasure": return "Buttons/Rewards";
                case "Fair": return ArtLibrary.Data.Scene ? "Scene/Merchant" : "Icons/Silver";
                case "Siege": return "Decor/shield";
                default: return "Decor/pumpkin";
            }
        }

        /// <summary>A red dot on the "События" button: an event runs or a reward of an ended one waits.</summary>
        bool EventsWait(long now) => session.ActiveRuns(now).Any() || session.EndedRuns().Any(r => !r.Claimed && r.Points > 0);

        // ------------------------------------------------------------------ drawing helpers

        void EventStyles()
        {
            if (evTitle != null)
                return;
            evTitle = Style(20, TextAnchor.MiddleLeft, false);
            evText = Style(13, TextAnchor.MiddleCenter, false);
            evLeft = Style(13, TextAnchor.MiddleLeft, false);
            evSmall = Style(11, TextAnchor.MiddleCenter, false);
            evSmallLeft = Style(11, TextAnchor.MiddleLeft, false);
            evNote = Style(12, TextAnchor.UpperLeft, true);
            evHit = new GUIStyle(GUIStyle.none);
            foreach (var st in new[] { evHit.normal, evHit.hover, evHit.active, evHit.focused })
                st.textColor = new Color(0, 0, 0, 0); // the label is drawn outlined; the hit area stays silent
        }

        static GUIStyle Style(int size, TextAnchor anchor, bool wrap)
        {
            var s = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = FontStyle.Bold, alignment = anchor, wordWrap = wrap, richText = false };
            s.normal.textColor = EvCream;
            return s;
        }

        /// <summary>A plank button with its label outlined in the middle; true when tapped.</summary>
        bool EvButton(Rect r, string label, bool primary = false, bool enabled = true)
        {
            var b = Pressable("ev" + label, r);
            var old = GUI.color;
            if (!enabled)
                GUI.color = new Color(0.6f, 0.6f, 0.6f, old.a * 0.8f);
            ArtLibrary.DrawSliced(b, primary ? "UI/ButtonOn" : "UI/Button", 16);
            UiSkin.Outlined(b, label, r.height < 26 ? evSmall : evText);
            GUI.color = old;
            if (!enabled || !GUI.Button(r, label, evHit))
                return false;
            sounds?.Play("Click", 0.4f);
            return true;
        }

        static void EvPlate(Rect r) => ArtLibrary.DrawSliced(r, "UI/Pill", 30);

        static void EvFill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = new Color(c.r, c.g, c.b, c.a * old.a);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        /// <summary>A bar on a plate: warm gold by default, a light top and a deeper bottom like the loading bar.</summary>
        static void EvBar(Rect r, double share, Color fill)
        {
            ArtLibrary.DrawSliced(r, "UI/Pill", 30);
            var inner = new Rect(r.x + 4, r.y + 4, r.width - 8, r.height - 8);
            float w = inner.width * Mathf.Clamp((float)share, 0, 1);
            if (w < 1)
                return;
            EvFill(new Rect(inner.x, inner.y, w, inner.height), fill);
            EvFill(new Rect(inner.x, inner.y, w, inner.height * 0.38f), new Color(1, 1, 1, 0.25f));
            EvFill(new Rect(inner.x, inner.y + inner.height * 0.72f, w, inner.height * 0.28f), new Color(0, 0, 0, 0.18f));
        }

        static readonly Color BarGold = new Color(0.96f, 0.68f, 0.22f, 1);
        static readonly Color BarRed = new Color(0.82f, 0.22f, 0.18f, 1);
        static readonly Color BarGreen = new Color(0.45f, 0.78f, 0.3f, 1);

        /// <summary>A picture kept in its proportions, standing in the middle of the rectangle.</summary>
        static void EvPicture(Rect r, string path)
        {
            var t = ArtLibrary.Tex(path);
            if (t == null)
                return;
            float aspect = t.height > 0 ? (float)t.width / t.height : 1;
            float w = Mathf.Min(r.width, r.height * aspect), h = w / aspect;
            GUI.DrawTexture(new Rect(r.x + (r.width - w) / 2, r.y + (r.height - h) / 2, w, h), t);
        }

        /// <summary>Icon and amount pairs in a row, centred in the rectangle (the rewards of a milestone).</summary>
        void EvRewards(Rect r, List<(string icon, string text)> items)
        {
            const float icon = 20, gap = 14;
            float total = items.Sum(x => (x.icon == null ? 0 : icon + 3) + x.text.Length * 6.6f + gap) - gap;
            float x0 = r.x + Mathf.Max(0, (r.width - total) / 2);
            foreach (var (path, text) in items)
            {
                if (path != null)
                {
                    ArtLibrary.Draw(new Rect(x0, r.center.y - icon / 2, icon, icon), path);
                    x0 += icon + 3;
                }
                float w = text.Length * 6.6f;
                UiSkin.Outlined(new Rect(x0, r.y, w + 4, r.height), text, evSmallLeft);
                x0 += w + gap;
            }
        }

        // ------------------------------------------------------------------ the window

        sealed class EventTab
        {
            public string Key = "";
            public EventData Data;
            public EventRun Run;
            public long Start;
        }

        List<EventTab> EventTabs(long now)
        {
            var tabs = new List<EventTab>();
            foreach (var r in session.ActiveRuns(now).OrderBy(r => r.End))
                tabs.Add(new EventTab { Key = r.Id + ":" + r.Start, Data = session.EventInfo(r.Id), Run = r, Start = r.Start });
            foreach (var r in session.EndedRuns().OrderBy(r => r.End))
                tabs.Add(new EventTab { Key = r.Id + ":" + r.Start, Data = session.EventInfo(r.Id), Run = r, Start = r.Start });
            foreach (var (e, start) in session.Upcoming(now))
                tabs.Add(new EventTab { Key = "soon:" + e.Id, Data = e, Start = start });
            return tabs;
        }

        void DrawEvents(long now)
        {
            EventStyles();
            float W = viewWidth;
            var tabs = EventTabs(now);
            if (tabs.Count == 0)
            {
                DrawNoEvents(now, W);
                return;
            }
            if (!tabs.Any(t => t.Key == eventTab))
            {
                eventTab = tabs[0].Key;
                eventShop = false;
            }
            DrawEventTabs(tabs, now, W);
            var tab = tabs.First(t => t.Key == eventTab);
            if (tab.Run == null)
            {
                DrawAnnouncement(tab.Data, tab.Start, now, W);
                return;
            }
            var run = tab.Run;
            DrawEventBanner(run, now, W);
            DrawMilestones(run, W);
            if (eventShop || run.Closed)
            {
                DrawEventShop(run, now, W);
                return;
            }
            switch (run.Id)
            {
                case "LongNight": DrawLongNight(run, now, W); break;
                case "Tournament": DrawTournament(run, now, W); break;
                case "Portal": DrawPortal(run, now, W); break;
                case "Treasure": DrawTreasure(run, now, W); break;
                case "Fair": DrawFair(run, now, W); break;
                case "Siege": DrawSiege(run, now, W); break;
                case "Festival": DrawFestival(run, now, W); break;
            }
            if (session.State.Trophies.Count > 0)
            {
                var t = GUILayoutUtility.GetRect(W, 22);
                UiSkin.Outlined(t, "Трофеи крепости: " + string.Join(" · ", session.State.Trophies), evSmall);
            }
        }

        void DrawEventTabs(List<EventTab> tabs, long now, float W)
        {
            var row = GUILayoutUtility.GetRect(W, 50);
            const float gap = 6;
            float tw = Mathf.Min(176, (W - gap * (tabs.Count - 1)) / tabs.Count);
            for (int i = 0; i < tabs.Count; i++)
            {
                var tab = tabs[i];
                var r = new Rect(row.x + i * (tw + gap), row.y + 2, tw, 44);
                bool chosen = tab.Key == eventTab;
                var b = Pressable("evtab" + tab.Key, r);
                var old = GUI.color;
                if (tab.Run == null)
                    GUI.color = new Color(0.75f, 0.75f, 0.85f, old.a); // announced: a cooler, quieter plank
                ArtLibrary.DrawSliced(b, chosen ? "UI/ButtonOn" : "UI/Button", 16);
                GUI.color = old;
                EvPicture(new Rect(b.x + 6, b.y + 4, 36, 36), EventArt(tab.Data.Id, tab.Run, tab.Start));
                string when = tab.Run == null ? "через " + Duration(tab.Start - now)
                    : tab.Run.Closed ? (tab.Run.Claimed ? "лавка ещё " + Duration(tab.Run.End + 86400 - now) : "итоги")
                    : Duration(tab.Run.End - now);
                UiSkin.Outlined(new Rect(b.x + 44, b.y + 4, b.width - 48, 20), EventShort(tab.Data.Id), evSmall);
                UiSkin.Outlined(new Rect(b.x + 44, b.y + 22, b.width - 48, 18), when, evSmall);
                if (tab.Run != null && tab.Run.Closed && !tab.Run.Claimed && tab.Run.Points > 0)
                    ArtLibrary.Draw(new Rect(b.xMax - 16, b.y - 6, 20, 20), "UI/Dot");
                if (GUI.Button(r, "Вкладка " + EventShort(tab.Data.Id), evHit) && !chosen)
                {
                    sounds?.Play("Page", 0.3f);
                    eventTab = tab.Key;
                    eventShop = false;
                }
            }
            GUILayoutUtility.GetRect(W, 4);
        }

        /// <summary>The banner: picture, name, what to do now, points and place, the countdown, «Играть» or «Забрать».</summary>
        void DrawEventBanner(EventRun run, long now, float W)
        {
            var r = GUILayoutUtility.GetRect(W, 108);
            var e = session.EventInfo(run.Id);
            EvPlate(r);
            EvPicture(new Rect(r.x + 10, r.y + 8, 92, 92), EventArt(run.Id, run, run.Start));
            float x = r.x + 112, right = 196, tw = r.width - 112 - right - 8;
            var old = GUI.color;
            GUI.color = new Color(EvGold.r, EvGold.g, EvGold.b, old.a);
            UiSkin.Outlined(new Rect(x, r.y + 6, tw, 28), e.Name, evTitle, 2f);
            GUI.color = old;
            UiSkin.Outlined(new Rect(x, r.y + 34, tw, 18), run.Personal ? "Личный заход на сутки: награды за один день" : EventTagline(run.Id), evSmallLeft);
            string place = run.Closed ? "место " + run.Rank : "место " + session.EventRank(run);
            if (run.Id == "Tournament")
                place += " · " + GameSession.TournamentTitle(run.Closed ? run.Rank : session.EventRank(run));
            UiSkin.Outlined(new Rect(x, r.y + 54, tw, 18), string.Format("Очки {0:0} из {1:0} · {2} из {3} · {4}: {5}", run.Points, session.RunTarget(run), place,
                session.GroupSize(run.Id), e.Currency, session.Purse(run)), evSmallLeft);
            GUI.color = new Color(EvGold.r, EvGold.g, EvGold.b, old.a);
            UiSkin.Outlined(new Rect(x, r.y + 74, tw, 24), (run.Closed ? "Итог: " : "Задание: ") + EventTask(run, now), evLeft);
            GUI.color = old;

            // the countdown and the buttons on the right
            var timer = new Rect(r.xMax - right, r.y + 8, right - 10, 28);
            ArtLibrary.DrawSliced(timer, "UI/Pill", 30);
            string left = run.Closed ? (session.ShopOpen(run, now) ? "Лавка ещё " + Duration(run.End + 86400 - now) : "Закончилось")
                : "Осталось " + Duration(run.End - now);
            UiSkin.Outlined(timer, left, evText);
            var play = new Rect(r.xMax - right, r.y + 40, right - 10, 32);
            var shop = new Rect(r.xMax - right, r.y + 74, right - 10, 26);
            if (run.Closed)
            {
                if (!run.Claimed && EvButton(play, run.Points > 0 ? "Забрать награду" : "Закрыть итоги", true))
                {
                    var before = session.State.Wallet[ResourceType.Sparks];
                    Report(session.ClaimEventReward(run), "Coins");
                    message = string.Format("{0}: место {1}, +{2} искр", e.Name, run.Rank, session.State.Wallet[ResourceType.Sparks] - before);
                }
                else if (run.Claimed)
                    UiSkin.Outlined(play, "Награда получена", evText);
                return;
            }
            var main = EventMainAction(run, now);
            if (EvButton(play, "Играть", true, main.enabled))
            {
                eventShop = false;
                main.act();
            }
            if (EvButton(shop, eventShop ? "Назад к заданию" : "Лавка · " + session.Purse(run)))
                eventShop = !eventShop;
        }

        string EventTask(EventRun run, long now)
        {
            if (run.Closed)
                return string.Format("{0:0} очков, место {1} из {2}", run.Points, run.Rank, session.GroupSize(run.Id));
            switch (run.Id)
            {
                case "LongNight":
                    return string.Format("бейте Жнеца, атак осталось {0}", session.NightAttacksLeft(run, now));
                case "Tournament":
                    return string.Format("улучшите лучший бой дня ({0:0}), попыток {1}", run.DayBest, session.TournamentAttemptsLeft(run, now));
                case "Portal":
                    if (run.BlessingOffer.Count > 0)
                        return "выберите дар Угля";
                    if (!run.RunAlive)
                        return run.FreeRunDay == session.LocalDay(now) ? "отряд отдыхает до завтра" : "войдите в Портал с этажа " + (run.Checkpoint + 1);
                    return run.Floor >= session.PortalFloorsOpen(run, now) ? "новые этажи откроются завтра" : "пройдите этаж " + (run.Floor + 1);
                case "Treasure":
                    return string.Format("ищите казну: факелов {0}, обломков карты {1} из 5", session.TorchesLeft(run, now), run.Pieces);
                case "Fair":
                    return string.Format("сдайте заказ или торгуйте: сделок {0} из 20", run.Trades);
                case "Siege":
                    long w = session.SiegeNextWindow(run, now);
                    if (w < 0)
                        return "последний штурм позади";
                    return session.SiegeWindow(run, now) == w ? "ШТУРМ! Защищайте заставы" : "готовьте гарнизоны: штурм через " + Duration(session.SiegeWindowStart(w) - now);
                default:
                    return string.Format("зажгите фонари, игр осталось {0} из 3", LanternGamesLeft(run, now));
            }
        }

        /// <summary>What «Играть» does: the main move of the event right now.</summary>
        (bool enabled, Action act) EventMainAction(EventRun run, long now)
        {
            switch (run.Id)
            {
                case "LongNight":
                    return (session.NightAttacksLeft(run, now) > 0, () => EventFight("LongNight", now, -1, "Ночной Жнец"));
                case "Tournament":
                    return (session.TournamentAttemptsLeft(run, now) > 0, () => EventFight("Tournament", now, -1, "Турнир Стражей"));
                case "Portal":
                    bool can = run.BlessingOffer.Count == 0 && (run.RunAlive || run.FreeRunDay != session.LocalDay(now)) &&
                               (run.RunAlive ? run.Floor : run.Checkpoint) < session.PortalFloorsOpen(run, now);
                    return (can, () => EventFight("Portal", now, -1, "Портал, этаж " + (run.Floor + 1)));
                case "Treasure":
                    return (session.TorchesLeft(run, now) > 0, () => TreasureStep(run, now));
                case "Fair":
                    int order = run.Orders.FindIndex(CanDeliver);
                    return (order >= 0 || run.Trades < 20, () =>
                    {
                        if (order >= 0)
                            Report(session.FairDeliver(order, now), "Coins");
                        else
                            FairDeal(now);
                    });
                case "Siege":
                    return (session.SiegeWindow(run, now) >= 0 && !run.WindowsDone.Contains(session.SiegeWindow(run, now)), () => SiegeLive(run, now));
                default:
                    return (LanternGamesLeft(run, now) > 0, () => StartLanternGame(now));
            }
        }

        void EventFight(string id, long now, int cell = -1, string title = "")
        {
            var fail = session.StartEventBattle(id, now, out var started, cell);
            Report(fail);
            if (fail != Fail.None)
                return;
            BeginBattle(started, done =>
            {
                session.FinishEventBattle(done, Now(), out string result);
                return result;
            }, title == "" ? session.EventInfo(id).Name : title);
        }

        /// <summary>The track of the ten milestones: the bar of points, a badge for each, the rewards of the next one.</summary>
        void DrawMilestones(EventRun run, float W)
        {
            var r = GUILayoutUtility.GetRect(W, 66);
            var e = session.EventInfo(run.Id);
            var track = new Rect(r.x + 18, r.y + 12, r.width - 36, 16);
            double share = run.Points / session.RunTarget(run);
            EvBar(track, share, BarGold);
            for (int i = 1; i <= 10; i++)
            {
                float cx = track.x + track.width * i / 10f;
                var badge = new Rect(cx - 14, track.center.y - 14, 28, 28);
                bool reached = run.Thresholds >= i;
                var old = GUI.color;
                if (reached)
                    ArtLibrary.Draw(new Rect(cx - 20, track.center.y - 20, 40, 40), "UI/Glint");
                else
                    GUI.color = new Color(0.55f, 0.55f, 0.6f, old.a * 0.9f);
                ArtLibrary.Draw(badge, i == 5 || i == 10 ? "Icons/Sparks" : "UI/Badge");
                GUI.color = old;
                if (i != 5 && i != 10)
                    UiSkin.Outlined(badge, i.ToString(), evSmall);
            }
            var line = new Rect(r.x, r.y + 38, r.width, 24);
            if (run.Thresholds >= 10)
            {
                UiSkin.Outlined(line, "Все 10 порогов взяты! Место в группе добавит ещё 4–20% награды", evSmall);
                return;
            }
            var part = session.BudgetPart(run, 0.08);
            var items = new List<(string, string)> { (null, string.Format("Порог {0}:", run.Thresholds + 1)) };
            if (part.sparks > 0)
                items.Add(("Icons/Sparks", "+" + part.sparks));
            if (part.essence > 0)
                items.Add(("Icons/Essence", "+" + ShortNum(part.essence)));
            if (part.materials > 0)
                items.Add(("Icons/Silver", "+" + ShortNum(part.materials)));
            if (part.hourglass > 0)
                items.Add((null, string.Format("+{0} мин часов", Math.Max(1, (part.hourglass + 30) / 60))));
            items.Add((null, e.Currency + " +" + session.CurrencyPerThreshold(run)));
            double need = session.RunTarget(run) / 10 * (run.Thresholds + 1) - run.Points;
            items.Add((null, string.Format("(ещё {0:0} очков)", Math.Max(1, Math.Ceiling(need)))));
            EvRewards(line, items);
        }

        // ------------------------------------------------------------------ announcements and an empty window

        void DrawAnnouncement(EventData e, long start, long now, float W)
        {
            var r = GUILayoutUtility.GetRect(W, 108);
            EvPlate(r);
            EvPicture(new Rect(r.x + 10, r.y + 8, 92, 92), EventArt(e.Id, null, start));
            float x = r.x + 112, tw = r.width - 112 - 16;
            UiSkin.Outlined(new Rect(x, r.y + 6, tw, 28), "Скоро: " + e.Name, evTitle, 2f);
            UiSkin.Outlined(new Rect(x, r.y + 34, tw, 18), EventTagline(e.Id), evSmallLeft);
            UiSkin.Outlined(new Rect(x, r.y + 54, tw, 18), string.Format("Начнётся через {0}, продлится {1} дн. Награды: до {2} искр", Duration(start - now), e.Days, e.Sparks), evSmallLeft);
            var old = GUI.color;
            GUI.color = new Color(EvGold.r, EvGold.g, EvGold.b, old.a);
            UiSkin.Outlined(new Rect(x, r.y + 74, tw, 24), "Подготовка: " + Preparation(e.Id, start), evLeft);
            GUI.color = old;
            // what its shop will sell, and the calendar of the others
            var shop = session.EventShop(e.Id);
            var row = GUILayoutUtility.GetRect(W, 64);
            UiSkin.Outlined(new Rect(row.x + 14, row.y + 4, 220, 56), "В лавке «" + e.Currency + "»:", evLeft);
            for (int i = 0; i < shop.Count; i++)
            {
                var card = new Rect(row.x + 240 + i * 250, row.y + 4, 240, 56);
                EvPlate(card);
                EvPicture(new Rect(card.x + 8, card.y + 4, 48, 48), GoodsArt(shop[i].Goods));
                UiSkin.Outlined(new Rect(card.x + 60, card.y + 4, card.width - 64, 48), GoodsName(shop[i]), evSmallLeft);
            }
            DrawSchedule(now, W);
        }

        /// <summary>The calendar: when each event starts next, or at what hearth level it opens.</summary>
        void DrawSchedule(long now, float W)
        {
            var head = GUILayoutUtility.GetRect(W, 24);
            UiSkin.Outlined(head, "Календарь событий (28 дней с понедельника, Праздник — раз в квартал)", evSmall);
            var events = session.Balance.Activities.Events.OrderBy(e => e.Unlock > session.State.HearthLevel).ThenBy(e => session.NextStart(e, now)).ToList();
            for (int i = 0; i < events.Count; i += 2)
            {
                var line = GUILayoutUtility.GetRect(W, 26);
                for (int k = 0; k < 2 && i + k < events.Count; k++)
                {
                    var e = events[i + k];
                    float x = line.x + k * W / 2;
                    EvPicture(new Rect(x + 24, line.y + 2, 22, 22), EventArt(e.Id, null, session.NextStart(e, now)));
                    var run = session.ActiveRun(e.Id, now);
                    string when = session.State.HearthLevel < e.Unlock ? "откроется на " + e.Unlock + "-м уровне очага"
                        : run != null ? "идёт, осталось " + Duration(run.End - now) : "через " + Duration(session.NextStart(e, now) - now);
                    UiSkin.Outlined(new Rect(x + 52, line.y, W / 2 - 60, 26), e.Name + " — " + when, evSmallLeft);
                }
            }
        }

        string Preparation(string id, long start)
        {
            switch (id)
            {
                case "LongNight":
                    var f = combat.Family(session.NightFamilyAt(start));
                    return string.Format("слабость Жнеца — {0}: возьмите {1}", GameSession.WeaknessNames[(int)session.NightFamilyAt(start)], ClassName(f.Weak));
                case "Tournament": return "день 1 — Сталь: мечники, секачи и Страж";
                case "Portal": return "прокачайте отряд: здоровье не восстанавливается между этажами";
                case "Treasure": return "Стрелок с Ловкостью 30+ замечает ловушки";
                case "Fair": return "запаситесь углём, едой и железом для заказов";
                case "Siege": return "сильные жители пойдут в гарнизоны застав";
                default: return "выполняйте поручения дня: каждое даёт очки праздника";
            }
        }

        void DrawNoEvents(long now, float W)
        {
            var r = GUILayoutUtility.GetRect(W, 108);
            EvPlate(r);
            EvPicture(new Rect(r.x + 10, r.y + 8, 92, 92), "Buttons/Events");
            UiSkin.Outlined(new Rect(r.x + 112, r.y + 8, r.width - 128, 30), "Сейчас событий нет", evTitle, 2f);
            UiSkin.Outlined(new Rect(r.x + 112, r.y + 40, r.width - 128, 60),
                "Календарь на 28 дней начинается в понедельник. Новичок получает личный однодневный заход в каждое событие, как только оно откроется.", evNote);
            DrawSchedule(now, W);
        }

        // ------------------------------------------------------------------ the shop

        void DrawEventShop(EventRun run, long now, float W)
        {
            var e = session.EventInfo(run.Id);
            var head = GUILayoutUtility.GetRect(W, 24);
            UiSkin.Outlined(head, string.Format("Лавка: {0} {1}. Открыта до конца события и ещё сутки, остаток станет серебром 1 к {2}",
                session.Purse(run), e.Currency, session.Balance.Activities.EventCurrency.Silver), evSmall);
            var row = GUILayoutUtility.GetRect(W, 104);
            var shop = session.EventShop(run.Id);
            const float gap = 10;
            float cw = Mathf.Min(220, (W - gap * (shop.Count - 1)) / Math.Max(1, shop.Count));
            float x0 = row.x + (W - (cw * shop.Count + gap * (shop.Count - 1))) / 2;
            for (int i = 0; i < shop.Count; i++)
            {
                var g = shop[i];
                var card = new Rect(x0 + i * (cw + gap), row.y + 2, cw, 100);
                EvPlate(card);
                EvPicture(new Rect(card.x + 8, card.y + 8, 56, 56), GoodsArt(g.Goods));
                UiSkin.Outlined(new Rect(card.x + 68, card.y + 8, cw - 74, 22), GoodsName(g), evSmallLeft);
                int bought = session.BoughtOf(run, g.Goods);
                UiSkin.Outlined(new Rect(card.x + 68, card.y + 30, cw - 74, 20), string.Format("куплено {0} из {1}", bought, g.Limit), evSmallLeft);
                bool can = bought < g.Limit && session.Purse(run) >= g.Price && session.ShopOpen(run, now);
                if (EvButton(new Rect(card.x + 8, card.y + 68, cw - 16, 26), "Купить за " + g.Price, false, can))
                    Report(session.BuyEventGoods(run, i, now), "Coins");
            }
        }

        static string GoodsArt(string goods)
        {
            if (goods.StartsWith("Decor:"))
                return "Decor/" + goods.Substring(6);
            return goods == "Recruit" ? "Portraits/Dweller3" : "Icons/" + goods;
        }

        string GoodsName(EventGoodsData g)
        {
            if (g.Goods.StartsWith("Decor:"))
                return session.Decor(g.Goods.Substring(6))?.Name ?? "Украшение";
            switch (g.Goods)
            {
                case "Cores": return "Сердцевины ×" + g.Amount;
                case "Ember": return "Жар ×" + g.Amount;
                case "Blueprints": return "Чертежи ×" + g.Amount;
                default: return "Редкий житель";
            }
        }

        // ------------------------------------------------------------------ Long Night

        void DrawLongNight(EventRun run, long now, float W)
        {
            var r = GUILayoutUtility.GetRect(W, 112);
            EvPlate(r);
            var family = session.NightFamily(run);
            var f = combat.Family(family);
            EvPicture(new Rect(r.x + 10, r.y + 10, 90, 90), "Portraits/" + ArtLibrary.CreatureSheet(family));
            float x = r.x + 110, tw = r.width - 110 - 210;
            UiSkin.Outlined(new Rect(x, r.y + 8, tw, 20), string.Format("Ночной Жнец ({0}) · слабость: {1} — {2} ×{3:0.0}", f.Name,
                GameSession.WeaknessNames[(int)family], ClassName(f.Weak), f.WeakMult), evSmallLeft);
            int phase = session.NightPhase(run, now);
            string[] phases = { "", "Фаза 1: обычные удары", "Фаза 2: Жнец зовёт тварей — нужен урон по всем", "Затмение: гасит свет — жмите «Стену» или «Тёплые руки»" };
            UiSkin.Outlined(new Rect(x, r.y + 28, tw, 20), phases[phase], evSmallLeft);
            double left = session.NightPoolLeft(run, now);
            var bar = new Rect(x, r.y + 50, tw, 20);
            EvBar(bar, left, BarRed);
            UiSkin.Outlined(bar, string.Format("Запас Жнеца у группы: {0:0}%", left * 100), evSmall);
            UiSkin.Outlined(new Rect(x, r.y + 76, tw, 24), run.DawnAt > 0
                ? "Рассвет наступил: сундук " + new string('★', run.DawnTier) + " (+" + 10 * run.DawnTier + " Осколков)"
                : "Когда запас кончится, вся группа получит сундук «Рассвет»: чем раньше, тем лучше", evSmallLeft);
            var bx = r.xMax - 200;
            int attacks = session.NightAttacksLeft(run, now);
            if (EvButton(new Rect(bx, r.y + 14, 186, 36), "Атаковать (осталось " + attacks + ")", true, attacks > 0))
                EventFight("LongNight", now, -1, "Ночной Жнец");
            if (EvButton(new Rect(bx, r.y + 58, 186, 30), "Ещё атака · 30 искр", false, run.Extra < 1))
                Report(session.BuyEventExtra("LongNight", now), "Coins");
            UiSkin.Outlined(new Rect(bx, r.y + 88, 186, 20), "+1 атака за победу в Наплыве", evSmall);
        }

        // ------------------------------------------------------------------ Tournament

        void DrawTournament(EventRun run, long now, float W)
        {
            var r = GUILayoutUtility.GetRect(W, 112);
            EvPlate(r);
            int rule = session.TournamentRule(run, now);
            float x = r.x + 14, tw = r.width - 14 - 210;
            UiSkin.Outlined(new Rect(x, r.y + 8, tw, 20), "Правило дня " + (rule + 1) + ": " + TournamentRules[rule], evLeft);
            UiSkin.Outlined(new Rect(x, r.y + 30, tw, 20), string.Format("Лучший бой сегодня {0:0} · дни: {1} · ранг: {2}", run.DayBest,
                run.DayBests.Count == 0 ? "—" : string.Join(" + ", run.DayBests.Select(b => b.ToString("0"))),
                GameSession.TournamentTitle(session.EventRank(run))), evSmallLeft);
            int day = Math.Max(0, Math.Min(2, (int)((now - run.Start) / 86400)));
            var best = session.TournamentBest(run, day);
            UiSkin.Outlined(new Rect(x, r.y + 52, tw, 18), string.Format("Лучший отряд группы сегодня: {0}, {1:0} очков", best.name, best.score), evSmallLeft);
            for (int i = 0; i < best.squad.Length; i++)
            {
                var face = new Rect(x + i * 42, r.y + 72, 36, 36);
                ArtLibrary.Draw(face, "UI/Badge");
                EvPicture(new Rect(face.x + 3, face.y + 3, 30, 30), "Portraits/" + ArtLibrary.HeroSheet(best.squad[i]));
            }
            UiSkin.Outlined(new Rect(x + 5 * 42 + 6, r.y + 72, tw - 5 * 42, 36), string.Join(", ", best.squad.Select(ClassName).Distinct()), evSmallLeft);
            var bx = r.xMax - 200;
            int left = session.TournamentAttemptsLeft(run, now);
            if (EvButton(new Rect(bx, r.y + 14, 186, 36), "Бой турнира (" + left + ")", true, left > 0))
                EventFight("Tournament", now, -1, "Турнир Стражей");
            if (EvButton(new Rect(bx, r.y + 58, 186, 30), "Попытка · 20 искр", false, run.Extra < 2))
                Report(session.BuyEventExtra("Tournament", now), "Coins");
        }

        // ------------------------------------------------------------------ Portal

        static string DoorArt(GameSession.PortalDoor d, MonsterFamily family)
        {
            switch (d)
            {
                case GameSession.PortalDoor.Rest: return "Icons/Food";
                case GameSession.PortalDoor.Merchant: return "Icons/Ember";
                case GameSession.PortalDoor.Stash: return "Buttons/Rewards";
                default: return "Portraits/" + ArtLibrary.CreatureSheet(family);
            }
        }

        static string DoorHint(GameSession.PortalDoor d, bool hostess)
        {
            switch (d)
            {
                case GameSession.PortalDoor.Fight: return "2 волны · +10 очков";
                case GameSession.PortalDoor.Elite: return "сложнее · +15 очков, 3 Пепла";
                case GameSession.PortalDoor.Rest: return "лечит отряд на 30%";
                case GameSession.PortalDoor.Merchant: return "дары за Пепел";
                case GameSession.PortalDoor.Stash: return "сундук или ловушка";
                default: return hostess ? "Хозяйка Портала!" : "страж этажа · привал";
            }
        }

        void DrawPortal(EventRun run, long now, float W)
        {
            var r = GUILayoutUtility.GetRect(W, 132);
            EvPlate(r);
            int open = session.PortalFloorsOpen(run, now);
            float x = r.x + 14;
            UiSkin.Outlined(new Rect(x, r.y + 6, W * 0.62f, 20), string.Format("Этаж {0} из {1}{2} · привал на {3} · Пепел {4}", Math.Min(run.Floor + 1, 999),
                GameSession.PortalFloors, open == int.MaxValue ? " · Глубина открыта" : " · сегодня до " + open, run.Checkpoint + 1, run.Currency), evLeft);
            double hp = run.HpShares.Count == 0 ? 1 : run.HpShares.Average();
            var bar = new Rect(r.xMax - W * 0.36f, r.y + 8, W * 0.36f - 14, 18);
            EvBar(bar, hp, BarGreen);
            UiSkin.Outlined(bar, string.Format("Здоровье отряда {0:0}%", hp * 100), evSmall);
            UiSkin.Outlined(new Rect(x, r.y + 26, r.width - 28, 18), "Дары: " + (run.Blessings.Count == 0 ? "пока нет (дар каждые 3 этажа)" :
                string.Join(", ", run.Blessings.GroupBy(b => b).Select(g => GameSession.BlessingNames[g.Key].Split(':')[0] + (g.Count() > 1 ? " ×" + g.Count() : "")))), evSmallLeft);
            var area = new Rect(r.x + 10, r.y + 48, r.width - 20, 78);
            if (run.BlessingOffer.Count > 0)
            {
                bool paid = run.OfferPrice > 0;
                float cw = (area.width - (paid ? 130 : 0) - 16) / run.BlessingOffer.Count;
                for (int i = 0; i < run.BlessingOffer.Count; i++)
                {
                    var parts = GameSession.BlessingNames[run.BlessingOffer[i]].Split(':');
                    var card = new Rect(area.x + i * (cw + 8), area.y, cw, area.height);
                    if (EvButton(new Rect(card.x, card.y, card.width, 40), parts[0] + (paid ? " · " + run.OfferPrice + " Пепла" : ""), !paid, !paid || run.Currency >= run.OfferPrice))
                        Report(session.ChooseBlessing(i, now), "Done");
                    UiSkin.Outlined(new Rect(card.x, card.y + 42, card.width, 30), parts.Length > 1 ? parts[1].Trim() : "", evSmall);
                }
                if (paid && EvButton(new Rect(area.xMax - 120, area.y + 6, 120, 30), "Уйти"))
                    Report(session.SkipBlessing(now));
                return;
            }
            if (!run.RunAlive)
            {
                bool free = run.FreeRunDay != session.LocalDay(now);
                if (EvButton(new Rect(area.x, area.y + 6, 300, 40), "Войти в Портал (1 заход в день)", true, free && run.Checkpoint < open))
                    EventFight("Portal", now, -1, "Портал, этаж " + (run.Checkpoint + 1));
                if (run.Revives < 2 && run.Floor > 0 && EvButton(new Rect(area.x + 312, area.y + 6, 240, 40), "Воскресить · " + (run.Revives == 0 ? 50 : 100) + " искр"))
                    Report(session.PortalRevive(now), "Done");
                UiSkin.Outlined(new Rect(area.x, area.y + 50, area.width, 24), "Новая вылазка — с последнего 5-го этажа без даров; павшие герои отдыхают 4 часа", evSmallLeft);
                return;
            }
            if (run.Floor >= open)
            {
                UiSkin.Outlined(area, "Этажи до " + open + " пройдены. Следующие десять откроются завтра", evText);
                return;
            }
            var doors = session.PortalDoors(run);
            var family = (MonsterFamily)((run.Seed + run.Floor) % 4);
            bool hostess = run.Floor + 1 == GameSession.PortalFloors;
            float dw = Mathf.Min(260, (area.width - 8 * (doors.Count - 1)) / doors.Count);
            for (int i = 0; i < doors.Count; i++)
            {
                var d = doors[i];
                var card = new Rect(area.x + i * (dw + 8), area.y, dw, area.height);
                var b = Pressable("door" + i, card);
                ArtLibrary.DrawSliced(b, d == GameSession.PortalDoor.Fight || d == GameSession.PortalDoor.Rest ? "UI/Button" : "UI/ButtonOn", 16);
                var old = GUI.color;
                if (d == GameSession.PortalDoor.Elite || d == GameSession.PortalDoor.Guardian)
                    GUI.color = new Color(1f, 0.75f, 0.7f, old.a);
                EvPicture(new Rect(b.x + 8, b.y + 8, 60, 60), DoorArt(d, family));
                GUI.color = old;
                string name = hostess && d == GameSession.PortalDoor.Guardian ? "Хозяйка Портала" : GameSession.DoorNames[(int)d];
                UiSkin.Outlined(new Rect(b.x + 72, b.y + 10, b.width - 78, 24), name, evText);
                UiSkin.Outlined(new Rect(b.x + 72, b.y + 36, b.width - 78, 30), DoorHint(d, hostess), evSmall);
                if (GUI.Button(card, "Дверь " + name, evHit))
                {
                    var fail = session.PortalEnter(i, now, out string result);
                    Report(fail);
                    if (fail != Fail.None)
                        continue;
                    if (d == GameSession.PortalDoor.Fight || d == GameSession.PortalDoor.Elite || d == GameSession.PortalDoor.Guardian)
                        EventFight("Portal", now, -1, name + ", этаж " + (run.Floor + 1));
                    else
                        message = result;
                }
            }
        }

        // ------------------------------------------------------------------ Treasure hunters

        static string CellArt(GameSession.TreasureCell c)
        {
            switch (c)
            {
                case GameSession.TreasureCell.Resources: return "Icons/Silver";
                case GameSession.TreasureCell.Refugee: return "Portraits/Dweller0";
                case GameSession.TreasureCell.Ambush: return "Portraits/CreatureFiend";
                case GameSession.TreasureCell.Piece: return "Icons/Blueprints";
                case GameSession.TreasureCell.Spring: return "Icons/Essence";
                case GameSession.TreasureCell.Beacon: return "Icons/Ember";
                case GameSession.TreasureCell.Trap: return "UI/Dot";
                default: return "Buttons/Rewards";
            }
        }

        static string CellName(GameSession.TreasureCell c)
        {
            switch (c)
            {
                case GameSession.TreasureCell.Resources: return "ресурсы";
                case GameSession.TreasureCell.Refugee: return "беженец";
                case GameSession.TreasureCell.Ambush: return "засада";
                case GameSession.TreasureCell.Piece: return "обломок карты";
                case GameSession.TreasureCell.Spring: return "родник: +1 факел";
                case GameSession.TreasureCell.Beacon: return "маяк";
                case GameSession.TreasureCell.Trap: return "ловушка";
                default: return "КАЗНА!";
            }
        }

        void OpenTreasureCell(EventRun run, int cell, long now)
        {
            var fail = session.OpenCell(cell, now, out var content);
            Report(fail);
            if (fail != Fail.None)
                return;
            message = "Под клеткой: " + CellName(content);
            if (content == GameSession.TreasureCell.Ambush)
                EventFight("Treasure", now, cell, "Засада");
        }

        /// <summary>«Играть» of the treasure hunt: the torch goes to the openable cell closest to where the treasury may lie.</summary>
        void TreasureStep(EventRun run, long now)
        {
            var targets = session.TreasureCandidates(run);
            var open = Enumerable.Range(0, GameSession.HexCells.Count).Where(c => session.CellOpenable(run, c)).ToList();
            if (open.Count == 0)
                return;
            OpenTreasureCell(run, open.OrderBy(c => targets.Count == 0 ? 0 : targets.Min(t => GameSession.HexDistance(c, t))).First(), now);
        }

        void DrawTreasure(EventRun run, long now, float W)
        {
            var r = GUILayoutUtility.GetRect(W, 174);
            EvPlate(r);
            var targets = session.TreasureCandidates(run);
            const float dx = 22, dy = 18.5f, size = 20;
            var center = new Vector2(r.x + 14 + 4.5f * dx + 2, r.y + 87);
            var cells = GameSession.HexCells;
            for (int i = 0; i < cells.Count; i++)
            {
                var (q, rr) = cells[i];
                var c = new Rect(center.x + (q + rr / 2f) * dx - size / 2, center.y + rr * dy - size / 2, size, size);
                bool opened = run.Opened.Contains(i);
                bool openable = !opened && session.CellOpenable(run, i);
                var old = GUI.color;
                if (opened)
                {
                    ArtLibrary.Draw(c, "UI/Badge");
                    EvPicture(new Rect(c.x + 3, c.y + 3, size - 6, size - 6), CellArt(session.CellContent(run, i)));
                }
                else
                {
                    GUI.color = openable ? old : new Color(0.35f, 0.35f, 0.42f, old.a * 0.85f); // fog
                    ArtLibrary.Draw(c, "UI/Badge");
                    GUI.color = old;
                    if (openable && targets.Count > 0)
                        UiSkin.Outlined(c, targets.Min(t => GameSession.HexDistance(i, t)).ToString(), evSmall);
                }
                if (openable && GUI.Button(c, "Клетка " + i, evHit))
                    OpenTreasureCell(run, i, now);
            }
            float x = r.x + 14 + 9 * dx + 20, tw = r.xMax - x - 14;
            int torches = session.TorchesLeft(run, now);
            UiSkin.Outlined(new Rect(x, r.y + 8, tw, 22), string.Format("Карта {0} · факелов {1}{2}", run.MapNo + 1, torches,
                torches < 6 && run.TorchAt > now ? " (+1 через " + Duration(run.TorchAt - now) + ")" : ""), evLeft);
            UiSkin.Outlined(new Rect(x, r.y + 30, tw, 20), string.Format("Обломков карты {0} из 5 · казна может быть в {1} клетках · найдено казны: {2}",
                run.Pieces, targets.Count, run.Treasures), evSmallLeft);
            UiSkin.Outlined(new Rect(x, r.y + 50, tw, 18), "Число на клетке — шагов до ближайшего места казны", evSmallLeft);
            UiSkin.Outlined(new Rect(x, r.y + 68, tw, 18), "Обломок вычёркивает треть мест, родник даёт факел, маяк — соседей" +
                (session.TrapSpotter() ? "; Стрелок видит ловушки" : ""), evSmallLeft);
            float bw = Mathf.Min(250, (tw - 10) / 2);
            if (EvButton(new Rect(x, r.y + 96, bw, 36), "Открыть ближайшую", true, torches > 0))
                TreasureStep(run, now);
            if (EvButton(new Rect(x + bw + 10, r.y + 96, bw, 36), "Факел · 20 искр (" + (3 - (run.Day == session.LocalDay(now) ? run.Extra : 0)) + " в день)"))
                Report(session.BuyEventExtra("Treasure", now), "Coins");
            if (!string.IsNullOrEmpty(run.LastResult))
                UiSkin.Outlined(new Rect(x, r.y + 140, tw, 22), run.LastResult, evSmallLeft);
        }

        // ------------------------------------------------------------------ Fair

        bool CanDeliver(long[] order) =>
            session.State.Wallet[GameSession.FairGoods[order[0]]] >= order[1] && session.State.Wallet[GameSession.FairGoods[order[2]]] >= order[3];

        void FairDeal(long now)
        {
            long have = session.State.Wallet[GameSession.FairGoods[fairSell]];
            var fail = session.FairTrade(fairSell, fairBuy, Math.Max(1, have / 4), now, out long got);
            Report(fail, "Coins");
            if (fail == Fail.None)
                message = "Получено " + ShortNum(got) + " " + FairGoodsNames[fairBuy];
        }

        void DrawFair(EventRun run, long now, float W)
        {
            var r = GUILayoutUtility.GetRect(W, 172);
            EvPlate(r);
            var rates = session.FairRates(run, now);
            var next = session.FairHinted(run, now);
            float x = r.x + 14;
            UiSkin.Outlined(new Rect(x, r.y + 6, 120, 22), "Курсы сейчас:", evLeft);
            float gw = (r.width - 28 - 120 - 200) / 4;
            for (int i = 0; i < 4; i++)
            {
                var g = new Rect(x + 120 + i * gw, r.y + 4, gw - 6, 26);
                ArtLibrary.Draw(new Rect(g.x, g.y, 26, 26), FairGoodsIcons[i]);
                var old = GUI.color;
                GUI.color = rates[i] > 1.2 ? new Color(0.6f, 1f, 0.5f, old.a) : rates[i] < 0.75 ? new Color(1f, 0.55f, 0.5f, old.a) : old;
                UiSkin.Outlined(new Rect(g.x + 28, g.y, g.width - 28, 26), "×" + rates[i].ToString("0.00") + (next != null ? " → " + next[i].ToString("0.00") : ""), evSmallLeft);
                GUI.color = old;
            }
            if (next == null && EvButton(new Rect(r.xMax - 206, r.y + 4, 192, 26), "Подсказка курса · 1 жетон"))
                Report(session.FairHint(now, out _));
            // the deal: what to sell, what to buy
            UiSkin.Outlined(new Rect(x, r.y + 36, 80, 30), "Продать:", evSmallLeft);
            UiSkin.Outlined(new Rect(x + 250, r.y + 36, 80, 30), "Купить:", evSmallLeft);
            for (int i = 0; i < 4; i++)
            {
                var sell = new Rect(x + 70 + i * 44, r.y + 34, 38, 34);
                var buy = new Rect(x + 316 + i * 44, r.y + 34, 38, 34);
                ArtLibrary.DrawSliced(sell, fairSell == i ? "UI/ButtonOn" : "UI/Button", 16);
                ArtLibrary.DrawSliced(buy, fairBuy == i ? "UI/ButtonOn" : "UI/Button", 16);
                ArtLibrary.Draw(new Rect(sell.x + 6, sell.y + 4, 26, 26), FairGoodsIcons[i]);
                ArtLibrary.Draw(new Rect(buy.x + 6, buy.y + 4, 26, 26), FairGoodsIcons[i]);
                if (GUI.Button(sell, "Продать " + FairGoodsNames[i], evHit) && fairBuy != i)
                    fairSell = i;
                if (GUI.Button(buy, "Купить " + FairGoodsNames[i], evHit) && fairSell != i)
                    fairBuy = i;
            }
            long have = session.State.Wallet[GameSession.FairGoods[fairSell]];
            if (EvButton(new Rect(r.xMax - 206, r.y + 36, 192, 32), "Сделка: четверть (" + run.Trades + "/20)", true, run.Trades < 20 && have > 0))
                FairDeal(now);
            // the caravan's orders
            UiSkin.Outlined(new Rect(x, r.y + 74, r.width - 28, 18), "Заказы каравана (+6 жетонов; новые — с новыми курсами):", evSmallLeft);
            float ow = (r.width - 28 - 16) / 3;
            for (int o = 0; o < 3; o++)
            {
                var card = new Rect(x + o * (ow + 8), r.y + 94, ow, 70);
                if (o >= run.Orders.Count)
                {
                    var old = GUI.color;
                    GUI.color = new Color(1, 1, 1, old.a * 0.4f);
                    ArtLibrary.DrawSliced(card, "UI/Button", 16);
                    GUI.color = old;
                    UiSkin.Outlined(card, "новый заказ через " + Duration(4 * 3600 - (now + session.State.UtcOffsetSeconds) % (4 * 3600)), evSmall);
                    continue;
                }
                var order = run.Orders[o];
                bool ready = CanDeliver(order);
                var b = Pressable("order" + o, card);
                ArtLibrary.DrawSliced(b, ready ? "UI/ButtonOn" : "UI/Button", 16);
                ArtLibrary.Draw(new Rect(b.x + 8, b.y + 8, 24, 24), FairGoodsIcons[order[0]]);
                UiSkin.Outlined(new Rect(b.x + 34, b.y + 8, b.width - 40, 24), ShortNum(order[1]) + " " + FairGoodsNames[order[0]], evSmallLeft);
                ArtLibrary.Draw(new Rect(b.x + 8, b.y + 36, 24, 24), FairGoodsIcons[order[2]]);
                UiSkin.Outlined(new Rect(b.x + 34, b.y + 36, b.width - 40, 24), ShortNum(order[3]) + " " + FairGoodsNames[order[2]] + (ready ? " · сдать!" : ""), evSmallLeft);
                if (GUI.Button(card, "Заказ " + o, evHit))
                    Report(session.FairDeliver(o, now), "Coins");
            }
        }

        // ------------------------------------------------------------------ Siege

        void SiegeLive(EventRun run, long now)
        {
            var fail = session.StartSiegeDefense(now, out var started, siegeStand);
            Report(fail);
            if (fail == Fail.None)
            {
                BeginSurge(started, done =>
                {
                    session.FinishSiegeDefense(done, Now());
                    return "Штурм: " + StarText(done.Stars);
                });
            }
        }

        void DrawSiege(EventRun run, long now, float W)
        {
            long window = session.SiegeNextWindow(run, now);
            bool live = window >= 0 && session.SiegeWindow(run, now) == window;
            bool final = window >= 0 && session.SiegeFinal(run, window);
            var head = GUILayoutUtility.GetRect(W, 50);
            EvPlate(head);
            string scout;
            List<int> targets = window >= 0 && session.SiegeScouted(run, window, now) ? session.SiegeTargets(run, window) : new List<int>();
            if (window < 0)
                scout = "Штурмов больше не будет: держите заставы до конца события";
            else if (targets.Count == 0)
                scout = "Разведчики вернутся за час до штурма (" + Duration(session.SiegeWindowStart(window) - 3600 - now) + ")";
            else
                scout = string.Format("{0}{1}: идут {2} — {3}", live ? "ШТУРМ ИДЁТ" : "Разведка", final ? " · Рыцарь-командор" : "",
                    GameSession.SiegeEnemyNames[session.SiegeEnemy(run, window)], final ? "на все заставы" : string.Join(" и ", targets.Select(t => GameSession.OutpostNames[t])));
            UiSkin.Outlined(new Rect(head.x + 14, head.y + 4, head.width - 250, 22), scout, evLeft);
            UiSkin.Outlined(new Rect(head.x + 14, head.y + 26, head.width - 250, 20), string.Format("Камень застав {0} · ремонт стены 20 искр ({1} из 3) · отбитые волны дают камень",
                run.Stone, run.Repairs), evSmallLeft);
            if (EvButton(new Rect(head.xMax - 230, head.y + 8, 216, 34), "Защищать вживую ×1,5", true, live && !run.WindowsDone.Contains(window)))
                SiegeLive(run, now);

            var row = GUILayoutUtility.GetRect(W, 138);
            float cw = (W - 4 * 8) / 5;
            int enemy = window >= 0 ? session.SiegeEnemy(run, window) : 3;
            for (int o = 0; o < run.Outposts.Count; o++)
            {
                var post = run.Outposts[o];
                var card = new Rect(row.x + o * (cw + 8), row.y + 2, cw, 134);
                var old = GUI.color;
                if (post.Fallen)
                    GUI.color = new Color(1f, 0.6f, 0.55f, old.a);
                else if (targets.Contains(o))
                    GUI.color = new Color(1f, 0.9f, 0.6f, old.a);
                EvPlate(card);
                GUI.color = old;
                UiSkin.Outlined(new Rect(card.x, card.y + 4, cw, 20), GameSession.OutpostNames[o], evText);
                UiSkin.Outlined(new Rect(card.x, card.y + 22, cw, 18), post.Fallen ? "ПАЛА: −5% выработки" : targets.Contains(o) ? "под ударом" : "держится", evSmall);
                UiSkin.Outlined(new Rect(card.x, card.y + 40, cw, 18), string.Format("гарнизон {0}/3 · сила {1:0}", post.Garrison.Count, session.GarrisonPower(post, enemy)), evSmall);
                UiSkin.Outlined(new Rect(card.x, card.y + 56, cw, 18), "ловушки " + post.Traps + "/3" + (final && siegeStand == o ? " · отряд здесь" : ""), evSmall);
                int index = o;
                var b1 = new Rect(card.x + 6, card.y + 76, cw - 12, 24);
                var b2 = new Rect(card.x + 6, card.y + 104, cw - 12, 24);
                if (post.Fallen)
                {
                    if (EvButton(b1, "Отбить", true))
                        EventFight("Siege", now, index, "Отбить " + GameSession.OutpostNames[index]);
                    if (EvButton(b2, "Починить · 20 искр", false, run.Repairs < 3))
                        Report(session.SiegeRepair(index, now), "Build");
                    continue;
                }
                if (EvButton(b1, "Гарнизон: лучшие", false))
                {
                    var used = run.Outposts.Where((x, i) => i != index).SelectMany(x => x.Garrison).ToList();
                    var guards = session.State.Dwellers.Where(d => !d.IsHero && d.RouteId == 0 && !used.Contains(d.Id))
                        .OrderByDescending(d => session.GarrisonPower(new Outpost { Garrison = new List<int> { d.Id } }, enemy)).Take(3).Select(d => d.Id).ToList();
                    Report(session.SetGarrison(index, guards, now));
                }
                if (final)
                {
                    if (EvButton(b2, siegeStand == index ? "Отряд здесь" : "Поставить отряд", siegeStand == index))
                        siegeStand = index;
                }
                else if (EvButton(b2, post.Traps < 3 ? "Ловушка · " + 4 * (post.Traps + 1) + " камня" : "Ловушки готовы", false, post.Traps < 3 && run.Stone >= 4 * (post.Traps + 1)))
                    Report(session.UpgradeTraps(index, now), "Build");
            }
        }

        // ------------------------------------------------------------------ Festival

        int LanternGamesLeft(EventRun run, long now) => Math.Max(0, 3 - (run.Day == session.LocalDay(now) ? run.Attempts : 0));

        void StartLanternGame(long now)
        {
            var fail = session.StartLanterns(now, out int seed);
            Report(fail);
            if (fail == Fail.None)
            {
                lanternSeed = seed;
                lanternClock = 0;
                lanternTaps.Clear();
            }
        }

        void DrawFestival(EventRun run, long now, float W)
        {
            var r = GUILayoutUtility.GetRect(W, 112);
            EvPlate(r);
            ArtLibrary.Draw(new Rect(r.x + 6, r.y + 6, 100, 100), "UI/Glint");
            EvPicture(new Rect(r.x + 26, r.y + 26, 60, 60), "Icons/Ember");
            float x = r.x + 112, tw = r.width - 112 - 210;
            int played = run.Day == session.LocalDay(now) ? run.Attempts : 0;
            UiSkin.Outlined(new Rect(x, r.y + 8, tw, 22), string.Format("Фонари: лучший результат дня {0:0} · игр {1} из 3", run.DayBest, played), evLeft);
            UiSkin.Outlined(new Rect(x, r.y + 30, tw, 20), "Зажигайте фонари на стене, пока их не задул ветер. Поручения дня дают +100", evSmallLeft);
            double goal = session.FestivalServerGoal(run, now);
            var bar = new Rect(x, r.y + 56, tw, 22);
            EvBar(bar, goal, BarGold);
            UiSkin.Outlined(bar, string.Format("Сервер: {0:0.0} млн из 10 млн фонарей", Math.Min(goal, 9.99) * 10), evSmall);
            UiSkin.Outlined(new Rect(x, r.y + 82, tw, 20), goal >= 1 ? "Цель сервера взята: бонус-сундук в итогах!" : "Если сервер зажжёт 10 млн, все получат бонус-сундук", evSmallLeft);
            if (EvButton(new Rect(r.xMax - 200, r.y + 30, 186, 40), "Играть в Фонари", true, played < 3))
                StartLanternGame(now);
        }

        // ------------------------------------------------------------------ lanterns

        void UpdateLanterns()
        {
            if (lanternSeed == 0)
                return;
            lanternClock += Time.unscaledDeltaTime;
            if (lanternClock < 62f)
                return;
            session.FinishLanterns(lanternSeed, lanternTaps, Now(), out int score);
            message = "Фонари: " + score + " очков";
            lanternSeed = 0;
        }

        /// <summary>The lantern mini-game: a wall of nine lanterns; a lit one glows for a second, a tap keeps it burning.</summary>
        void DrawLanterns()
        {
            EventStyles();
            float W = viewWidth;
            var lanterns = GameSession.Lanterns(lanternSeed);
            var head = GUILayoutUtility.GetRect(W, 40);
            EvPlate(head);
            UiSkin.Outlined(head, string.Format("Праздник очага · {0:0} с из 60 · зажжено {1}", Math.Min(60, lanternClock),
                GameSession.LanternScore(lanternSeed, lanternTaps) / 10), evText);
            var wall = GUILayoutUtility.GetRect(W, 330);
            float size = Mathf.Min(100, (wall.height - 20) / 3), gap = 10;
            float x0 = wall.x + (W - (size * 3 + gap * 2)) / 2;
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    int spot = row * 3 + col;
                    bool lit = lanterns.Any(l => l.spot == spot && Math.Abs(l.time - lanternClock) <= 1.0);
                    var cell = new Rect(x0 + col * (size + gap), wall.y + 6 + row * (size + gap), size, size);
                    ArtLibrary.DrawSliced(cell, lit ? "UI/ButtonOn" : "UI/Button", 16);
                    var old = GUI.color;
                    if (lit)
                        ArtLibrary.Draw(new Rect(cell.x - 10, cell.y - 10, size + 20, size + 20), "UI/Glint");
                    else
                        GUI.color = new Color(0.35f, 0.35f, 0.45f, old.a);
                    EvPicture(new Rect(cell.x + 10, cell.y + 8, size - 20, size - 20), "Decor/pumpkin");
                    GUI.color = old;
                    if (GUI.Button(cell, lit ? "ФОНАРЬ!" : "·", evHit))
                        lanternTaps.Add((lanternClock, spot));
                }
            }
        }
    }
}

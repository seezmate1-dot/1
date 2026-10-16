using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core;
using UnityEngine;

namespace LastHearth.Game
{
    /// <summary>
    /// Retention without pressure (GDD 9.3, 12): the window «Пока вас не было» shown once after a long
    /// absence, the tabs «Календарь» and «Сезон» of the «Задания» window (reward cells with icons, the
    /// day of today lit, a claim button), and the local notifications planned when the game goes to
    /// the background (NotificationPlanner; NoNotifications until the mobile notifications package is added).
    /// </summary>
    public sealed partial class GameRunner
    {
        static readonly string[] QuestTabs = { "Поручения", "Календарь", "Сезон" };

        OfflineSummary away;
        int questTab;
        readonly INotificationSink notifications = new NoNotifications();
        GUIStyle cellText;

        void CellStyles()
        {
            if (cellText != null)
                return;
            cellText = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
        }

        /// <summary>After the start (or a return to the game): the summary if the absence was long, else the calendar's gift.</summary>
        void ShowAway(OfflineSummary summary)
        {
            away = summary;
            if (battle != null || surge != null || window != Win.None)
                return;
            if (summary != null && summary.Worth)
                Open(Win.Away);
            else
                OfferCalendar();
        }

        /// <summary>A gift of the calendar waits: its tab opens by itself (not on the very first day: the tutorial goes first).</summary>
        void OfferCalendar()
        {
            if (session.CalendarPending <= 0 || session.State.LoginCount <= 1)
                return;
            Open(Win.Quests);
            questTab = 1;
        }

        void OnReturn()
        {
            notifications.CancelAll();
            if (session != null)
                ShowAway(session.ReturnAfterAbsence(Now()));
        }

        /// <summary>The game goes to the background: at most three useful notifications a day.</summary>
        void PlanNotifications()
        {
            if (session == null)
                return;
            notifications.CancelAll();
            foreach (var n in NotificationPlanner.Plan(session, Now()))
                notifications.Schedule(n);
        }

        // ------------------------------------------------------------------ «Задания»: tasks, calendar, season

        void DrawQuests(long now)
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < QuestTabs.Length; i++)
            {
                int waiting = i == 1 ? session.CalendarPending : i == 2 ? session.SeasonGiftsReady : 0;
                if (GUILayout.Toggle(questTab == i, QuestTabs[i] + (waiting > 0 ? " (" + waiting + ")" : ""), "Button") && questTab != i)
                {
                    questTab = i;
                    windowScroll = Vector2.zero;
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
            switch (questTab)
            {
                case 1:
                    DrawCalendar(now);
                    break;
                case 2:
                    DrawSeason(now);
                    break;
                default:
                    DrawDaily(now);
                    break;
            }
        }

        string GiftIcon(GiftData g)
        {
            switch (g.Kind)
            {
                case GiftKind.Sparks: return "Icons/Sparks";
                case GiftKind.Essence: return "Icons/Essence";
                case GiftKind.Treats: return "Icons/Food"; // the pets' button wears it too
                case GiftKind.Decor: return "Decor/" + g.Decor;
                default: return null; // the hourglass: its minutes on a round plate
            }
        }

        string GiftShort(GiftData g)
        {
            long n = session.GiftAmount(g);
            switch (g.Kind)
            {
                case GiftKind.Hourglass: return "часы";
                case GiftKind.Decor: return "украшение";
                case GiftKind.Treats: return "×" + n + " лакомств";
                default: return "×" + ShortNum(n);
            }
        }

        /// <summary>One reward cell: a plank (gold-trimmed for today), the icon, its caption on top and the amount below.</summary>
        bool GiftCell(Rect r, GiftData g, string caption, bool lit, bool taken, bool locked)
        {
            CellStyles();
            var old = GUI.color;
            ArtLibrary.DrawSliced(r, lit ? "UI/ButtonOn" : "UI/Panel", lit ? 16 : 24);
            if (taken || locked)
                GUI.color = new Color(old.r, old.g, old.b, old.a * (taken ? 0.4f : 0.7f));
            float icon = Mathf.Min(r.width * 0.62f, r.height * 0.5f);
            var at = new Rect(r.center.x - icon / 2, r.y + r.height * 0.24f, icon, icon);
            string path = GiftIcon(g);
            if (path != null && ArtLibrary.Tex(path) != null)
            {
                ArtLibrary.Draw(at, path);
            }
            else
            {
                ArtLibrary.Draw(at, "UI/Round");
                UiSkin.Outlined(new Rect(at.x, at.y - 2, at.width, at.height * 0.7f), session.GiftAmount(g).ToString(), cellText);
                UiSkin.Outlined(new Rect(at.x, at.y + at.height * 0.35f, at.width, at.height * 0.5f), "мин", cellText);
            }
            UiSkin.Outlined(new Rect(r.x, r.y + 3, r.width, 16), caption, cellText);
            GUI.color = old;
            UiSkin.Outlined(new Rect(r.x + 2, r.yMax - 20, r.width - 4, 16), taken ? "получено" : GiftShort(g), cellText);
            return lit && GUI.Button(r, "", GUIStyle.none);
        }

        /// <summary>Cells in rows of <paramref name="perRow"/>, as wide as the window allows.</summary>
        void GiftGrid(int count, int perRow, Func<int, Rect, bool> cell)
        {
            float gap = 6;
            float w = Mathf.Max(40, (viewWidth - gap * (perRow - 1)) / perRow);
            float h = Mathf.Clamp(w * 0.85f, 70, 100);
            for (int row = 0; row * perRow < count; row++)
            {
                var line = GUILayoutUtility.GetRect(viewWidth, h + gap);
                for (int k = 0; k < perRow && row * perRow + k < count; k++)
                    cell(row * perRow + k, new Rect(line.x + k * (w + gap), line.y, w, h));
            }
        }

        void ClaimCalendar(long now)
        {
            var gift = session.Calendar[session.CalendarIndex];
            string text = session.GiftText(gift);
            CollectingAll(() =>
            {
                var fail = session.ClaimCalendar(now);
                Report(fail, "Coins");
                if (fail == Fail.None)
                    message = "Награда календаря: " + text;
            });
        }

        void DrawCalendar(long now)
        {
            var cal = session.Calendar;
            if (cal.Count == 0)
                return;
            int pending = session.CalendarPending, next = session.CalendarIndex;
            GUILayout.Label(string.Format("<b>Календарь входа</b> · круг {0} · день {1} из {2}\n<size=12><color=#d8c8a8>Награда за каждый день в игре. " +
                "Дни не обязаны идти подряд: пропуск ничего не отнимает, награда ждёт.</color></size>",
                session.CalendarRound, Math.Min(cal.Count, next + (pending > 0 ? 1 : 0)), cal.Count), Rich());
            GiftGrid(cal.Count, 7, (i, r) =>
            {
                bool taken = i < next;
                bool lit = i == next && pending > 0;
                if (GiftCell(r, cal[i], "День " + (i + 1), lit, taken, !taken && !lit))
                    ClaimCalendar(now);
                return true;
            });
            GUILayout.Space(4);
            GUI.enabled = pending > 0;
            string label = pending > 0
                ? "Забрать награду дня: " + session.GiftText(cal[next]) + (pending > 1 ? " (ждут ещё " + (pending - 1) + ")" : "")
                : "Следующая награда — в следующий день в игре";
            if (GUILayout.Button(label, GUILayout.Height(40)))
                ClaimCalendar(now);
            GUI.enabled = true;
        }

        void DrawSeason(long now)
        {
            var x = session.SeasonInfo;
            if (x.Track.Count == 0)
                return;
            int step = session.SeasonStep;
            long left = session.SeasonEnds(now) - now;
            GUILayout.Label(string.Format("<b>Сезон {0}</b> · шаг {1} из {2} · до конца {3}\n<size=12><color=#d8c8a8>Очки сезона дают " +
                "поручения ({4} за каждое), награда дня ({5}), календарь ({6}), Наплыв ({7}), Охота ({8}) и пороги событий ({9}). " +
                "Невзятые награды выдадутся в конце сезона.</color></size>", session.SeasonNumber, step, x.Track.Count, Duration(Math.Max(0, left)),
                x.TaskXp, x.DailyXp, x.CalendarXp, x.SurgeXp, x.HuntXp, x.EventXp), Rich());
            // the bar of points to the next step
            var bar = GUILayoutUtility.GetRect(viewWidth, 24);
            ArtLibrary.DrawSliced(bar, "UI/Pill", 30);
            float share = step >= x.Track.Count ? 1 : session.SeasonStepXp / (float)Math.Max(1, x.StepXp);
            var old = GUI.color;
            GUI.color = new Color(1f, 0.78f, 0.32f, 0.9f);
            var fill = new Rect(bar.x + 6, bar.y + 7, (bar.width - 12) * share, bar.height - 14);
            if (fill.width > 0.5f)
                GUI.DrawTexture(fill, Texture2D.whiteTexture);
            GUI.color = old;
            HudStyles();
            UiSkin.Outlined(bar, step >= x.Track.Count ? "Дорожка пройдена" : string.Format("{0} / {1} очков до шага {2}",
                session.SeasonStepXp, x.StepXp, step + 1), buttonLabel);
            GUILayout.Space(6);
            GiftGrid(x.Track.Count, 6, (i, r) =>
            {
                bool taken = session.SeasonClaimed(i);
                bool lit = session.SeasonGiftReady(i);
                if (GiftCell(r, x.Track[i], "Шаг " + (i + 1), lit, taken, i >= step))
                {
                    string text = session.GiftText(x.Track[i]);
                    CollectingAll(() =>
                    {
                        var fail = session.ClaimSeason(i, now);
                        Report(fail, "Coins");
                        if (fail == Fail.None)
                            message = "Награда сезона: " + text;
                    });
                }
                return true;
            });
            GUILayout.Space(4);
            int ready = session.SeasonGiftsReady;
            GUI.enabled = ready > 0;
            if (GUILayout.Button(ready > 0 ? "Забрать награды сезона (" + ready + ")" : "Награды ждут на следующих шагах", GUILayout.Height(40)))
            {
                CollectingAll(() =>
                {
                    int n = session.ClaimSeasonAll(now);
                    message = "Наград сезона получено: " + n;
                    sounds?.Play("Coins");
                });
            }
            GUI.enabled = true;
            if (!session.SeasonPremium)
                GUILayout.Label("<size=12><color=#d8c8a8>Только бесплатная дорожка: всё — за обычную игру.</color></size>", Rich());
        }

        // ------------------------------------------------------------------ «Пока вас не было»

        void DrawAway(long now)
        {
            var s = away;
            if (s == null || s.Empty)
            {
                GUILayout.Label("Пока вас не было, в крепости всё было спокойно.");
                if (GUILayout.Button("Продолжить", GUILayout.Height(40)))
                    CloseWindow();
                return;
            }
            GUILayout.Label(string.Format("<b>Вас не было {0}</b>", Duration(s.Seconds)), Rich());
            AwayIcons("Выработка в залах", s.Produced);
            if (!s.Earned.IsEmpty())
                AwayIcons("Пришло само", s.Earned);
            if (s.CoalBurnt > 0 || s.FoodEaten > 0)
                GUILayout.Label(string.Format("<size=12><color=#d8c8a8>Очаг сжёг {0} угля, жители съели {1} еды.</color></size>",
                    ShortNum(s.CoalBurnt), ShortNum(s.FoodEaten)), Rich());
            if (!s.Lost.IsEmpty())
                GUILayout.Label("<color=#f9a>Не поместилось в залы: " + CostText(s.Lost) + "</color>", Rich());
            AwayLines("Стройка", s.Builds);
            AwayLines("Бои у крепости", s.Fights);
            AwayLines("Родились", s.Births);
            AwayLines("Вылазки", s.Routes);
            AwayLines("Начались события", s.EventsStarted);
            AwayLines("Закончились события", s.EventsEnded);
            AwayLines("Ещё", s.Other);
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (session.State.Rooms.Any(r => r.Stock >= 1) && GUILayout.Button("Собрать выработку", GUILayout.Height(40)))
            {
                CollectingAll(session.CollectAll);
                sounds?.Play("Coins");
            }
            if (GUILayout.Button("Продолжить", GUILayout.Height(40)))
            {
                CloseWindow();
                OfferCalendar();
            }
            GUILayout.EndHorizontal();
        }

        /// <summary>Resources as icon cells with the amount under each.</summary>
        void AwayIcons(string title, ResourceBag bag)
        {
            var types = Enumerable.Range(0, ResourceBag.Count).Select(i => (ResourceType)i).Where(t => bag[t] > 0).ToList();
            if (types.Count == 0)
                return;
            CellStyles();
            GUILayout.Label("<b>" + title + "</b>", Rich());
            float size = 64, gap = 8;
            int perRow = Math.Max(1, (int)((viewWidth + gap) / (size + gap)));
            for (int row = 0; row * perRow < types.Count; row++)
            {
                var line = GUILayoutUtility.GetRect(viewWidth, size + 20);
                for (int k = 0; k < perRow && row * perRow + k < types.Count; k++)
                {
                    var t = types[row * perRow + k];
                    var r = new Rect(line.x + k * (size + gap), line.y, size, size + 16);
                    ArtLibrary.DrawSliced(r, "UI/Panel", 24);
                    ArtLibrary.Draw(new Rect(r.x + 10, r.y + 4, size - 20, size - 20), "Icons/" + t);
                    UiSkin.Outlined(new Rect(r.x, r.yMax - 22, r.width, 18), "+" + ShortNum(bag[t]), cellText);
                }
            }
        }

        void AwayLines(string title, List<string> lines)
        {
            if (lines.Count == 0)
                return;
            GUILayout.Label("<b>" + title + "</b>", Rich());
            foreach (string line in lines.Take(8))
                GUILayout.Label("  · " + line);
            if (lines.Count > 8)
                GUILayout.Label("  · и ещё " + (lines.Count - 8));
        }
    }
}

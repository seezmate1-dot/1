using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core;
using UnityEngine;

namespace LastHearth.Game
{
    /// <summary>
    /// The «Вылазки» window (ACTIVITIES.md 4): the map of each route with its points and lines, where the team
    /// is now, the forks still to choose, the fight by hand, the cards at home; the journal of notes about the
    /// world; and the hall specialization picker of «Развитие» (MODES.md 6). The developer's page «Вылазки».
    /// </summary>
    public sealed partial class GameRunner
    {
        readonly List<int> routeTeam = new List<int>();
        readonly RoutePath[] routeForks = { RoutePath.Safe, RoutePath.Long };
        int routesTab;
        RoomType specHall = RoomType.CoalMine;

        static readonly Color SafeColor = new Color(0.55f, 0.85f, 0.55f, 1);
        static readonly Color RiskyColor = new Color(1f, 0.5f, 0.35f, 1);
        static readonly Color LongColor = new Color(0.55f, 0.75f, 1f, 1);

        static string PathName(RoutePath p) => p == RoutePath.Safe ? "Надёжный" : p == RoutePath.Risky ? "Рискованный" : "Долгий";

        static string PointName(RoutePoint t)
        {
            switch (t)
            {
                case RoutePoint.Fight: return "схватка";
                case RoutePoint.Resource: return "добыча";
                case RoutePoint.Event: return "встреча";
                case RoutePoint.Rest: return "отдых";
                case RoutePoint.Danger: return "опасность";
                default: return "ворота";
            }
        }

        /// <summary>A letter inside the point's circle.</summary>
        static string PointGlyph(RoutePoint t)
        {
            switch (t)
            {
                case RoutePoint.Fight: return "Б";
                case RoutePoint.Resource: return "Д";
                case RoutePoint.Event: return "В";
                case RoutePoint.Rest: return "О";
                case RoutePoint.Danger: return "!";
                default: return "";
            }
        }

        static Color PointColor(RoutePoint t)
        {
            switch (t)
            {
                case RoutePoint.Fight: return new Color(0.9f, 0.3f, 0.25f, 1);
                case RoutePoint.Resource: return new Color(0.85f, 0.7f, 0.3f, 1);
                case RoutePoint.Event: return new Color(0.5f, 0.75f, 0.95f, 1);
                case RoutePoint.Rest: return new Color(0.45f, 0.8f, 0.5f, 1);
                case RoutePoint.Danger: return new Color(0.75f, 0.45f, 0.9f, 1);
                default: return Color.white;
            }
        }

        // ------------------------------------------------------------------ the window

        void DrawRoutes(long now)
        {
            var r = session.Balance.Activities.Routes;
            GUILayout.Label("<b>Вылазки-маршруты</b> · отряд из 1–3 жителей уходит в туман за Чертежами", Rich());
            if (session.State.HearthLevel < r.Unlock)
            {
                GUILayout.Label("Откроются на " + r.Unlock + "-м уровне очага");
                return;
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(routesTab == 0, "Маршруты", "Button") && routesTab != 0)
                routesTab = 0;
            if (GUILayout.Toggle(routesTab == 1, string.Format("Записки ({0}/{1})", session.State.RouteNotes.Count, GameSession.RouteNotes.Length), "Button") && routesTab != 1)
                routesTab = 1;
            GUILayout.EndHorizontal();
            if (routesTab == 1)
            {
                DrawNotes();
                return;
            }

            foreach (var e in session.State.Expeditions.ToList())
                DrawExpedition(e, now);
            DrawFragments(now);
            DrawPlanner(now);
        }

        void DrawExpedition(Expedition e, long now)
        {
            string team = string.Join(", ", e.Team.Select(id => session.FindDweller(id)?.Name ?? "?"));
            var map = session.RouteMapOf(e);
            var walk = session.RouteWalk(e);
            if (!e.Back)
            {
                GUILayout.Label(string.Format("<b>В пути:</b> {0} · точка {1} из {2} · вернутся через {3}", team,
                    Math.Min(e.Passed + 1, walk.Count), walk.Count, Duration(Math.Max(0, e.End - now))), Rich());
                DrawRouteMap(map, walk, e.Forks, e, now);
                // the forks the team has not reached can still change
                for (int fork = 0; fork < 2; fork++)
                {
                    int first = walk.IndexOf(map.Branch(fork, e.Forks[fork]).First());
                    if (e.Passed >= first)
                        continue;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(string.Format("Развилка {0}:", fork + 1), GUILayout.Width(110));
                    foreach (RoutePath p in Enum.GetValues(typeof(RoutePath)))
                    {
                        if (GUILayout.Toggle(e.Forks[fork] == p, PathName(p), "Button") && e.Forks[fork] != p)
                            Report(session.ChooseFork(e.Id, fork, p, now));
                    }
                    GUILayout.EndHorizontal();
                }
                var fight = session.NextSkirmish(e);
                if (fight != null && GUILayout.Button(string.Format("Схватка вручную: «{0}» ({1} с)", fight.Name, session.Balance.Activities.Routes.SkirmishSeconds)))
                    RouteSkirmish(e, now);
            }
            else
            {
                GUILayout.Label(string.Format("<b>Вернулись:</b> {0} · {1}", team, e.Report), Rich());
                DrawRouteMap(map, walk, e.Forks, e, now);
            }
            foreach (string line in e.Log.Skip(Math.Max(0, e.Log.Count - 8)))
                GUILayout.Label("<size=12><color=#d8c8a8>· " + line + "</color></size>", Rich());
            if (!e.Back)
                return;
            GUILayout.BeginHorizontal();
            if (e.Card >= 0)
            {
                var card = GameSession.RouteCards[e.Card];
                GUILayout.Label(card.Title + (e.MoreCards.Count > 0 ? " (ещё карточек: " + e.MoreCards.Count + ")" : "") + ":", GUILayout.Width(300));
                if (GUILayout.Button(card.OptionA))
                    Report(session.ResolveRouteCard(e.Id, 1, now));
                if (GUILayout.Button(card.OptionB))
                    Report(session.ResolveRouteCard(e.Id, 2, now));
            }
            else if (GUILayout.Button("Принять"))
            {
                Report(session.ResolveRouteCard(e.Id, 1, now));
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
        }

        void RouteSkirmish(Expedition e, long now)
        {
            var fail = session.StartRouteSkirmish(e.Id, now, out var started);
            Report(fail);
            if (fail != Fail.None)
                return;
            int id = e.Id;
            BeginBattle(started, done =>
            {
                session.FinishRouteSkirmish(id, done, out string result);
                return result;
            }, "Схватка в пути");
        }

        void DrawFragments(long now)
        {
            int pieces = session.State.MapFragments;
            if (pieces <= 0)
                return;
            GUILayout.BeginHorizontal();
            GUILayout.Label(string.Format("Обломков карт: {0} · Кладоискатели вычёркивают ими треть мест клада (до {1} на карту)",
                pieces, session.Balance.Activities.Routes.FragmentsPerMap));
            GUI.enabled = session.CanUseMapFragment(now);
            if (GUILayout.Button("Отдать Кладоискателям", GUILayout.Width(240)))
                Report(session.UseMapFragment(now));
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        void DrawPlanner(long now)
        {
            GUILayout.Label(string.Format("<b>Новая вылазка</b> · сегодня осталось: {0}. Выберите отряд и путь на развилках:", session.RoutesLeft(now)), Rich());
            var free = session.State.Dwellers.Where(d => d.RouteId == 0 && d.InjuredUntil <= now).ToList();
            routeTeam.RemoveAll(id => free.All(d => d.Id != id));
            GUILayout.BeginHorizontal();
            int shown = 0;
            foreach (var d in free)
            {
                bool on = routeTeam.Contains(d.Id);
                string label = string.Format("{0}{1} [Л{2} С{3} У{4} Ст{5} Д{6}]", d.Name, d.IsHero ? " (герой)" : "", d.StatOf(Stat.Agility), d.StatOf(Stat.Strength),
                    d.StatOf(Stat.Intellect), d.StatOf(Stat.Endurance), d.StatOf(Stat.Spirit));
                if (GUILayout.Toggle(on, label, "Button") != on)
                {
                    if (on)
                        routeTeam.Remove(d.Id);
                    else if (routeTeam.Count < 3)
                        routeTeam.Add(d.Id);
                }
                if (++shown % 3 == 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
            }
            GUILayout.EndHorizontal();

            var map = session.NextRouteMap();
            DrawRouteMap(map, map.Walk(routeForks[0], routeForks[1]), routeForks, null, now);
            for (int fork = 0; fork < 2; fork++)
            {
                var key = map.Nodes.First(n => n.Fork == fork && n.Key);
                int have = routeTeam.Sum(id => session.FindDweller(id).StatOf(key.Stat));
                GUILayout.BeginHorizontal();
                GUILayout.Label(string.Format("Развилка {0}:", fork + 1), GUILayout.Width(110));
                foreach (RoutePath path in Enum.GetValues(typeof(RoutePath)))
                {
                    string text = path == RoutePath.Safe ? "Надёжный путь (+1 встреча)" :
                        path == RoutePath.Long ? "Долгий путь (+3 часа, +добыча)" :
                        string.Format("Рискованный: {0} {1}/{2}", StatName(key.Stat), have, key.Need);
                    if (GUILayout.Toggle(routeForks[fork] == path, text, "Button") && routeForks[fork] != path)
                        routeForks[fork] = path;
                }
                GUILayout.EndHorizontal();
            }
            GUI.enabled = routeTeam.Count > 0;
            if (GUILayout.Button("Отправить отряд", GUILayout.Height(36)))
            {
                var fail = session.StartRoute(routeTeam, routeForks[0], routeForks[1], now, out var route);
                Report(fail);
                if (fail == Fail.None)
                {
                    message = "Отряд ушёл, вернётся через " + Duration(route.End - now);
                    routeTeam.Clear();
                }
            }
            GUI.enabled = true;
        }

        // ------------------------------------------------------------------ the map

        /// <summary>
        /// The points and lines of a route: the chosen way bright, the other branches faint, the passed points
        /// filled, a dot where the team is now. Without an expedition it is the planner's preview.
        /// </summary>
        void DrawRouteMap(RouteMap map, List<int> walk, RoutePath[] forks, Expedition e, long now)
        {
            var area = Row(150);
            ArtLibrary.DrawSliced(area, "UI/Panel", 40, 16);
            float left = area.x + 34, width = area.width - 68, mid = area.y + 64, spread = 40;
            Vector2 Pos(RouteNode n) => new Vector2(left + n.X * width, mid + n.Y * spread);
            var on = new HashSet<int>(walk) { GameSession.RouteGateId };
            int passed = e == null ? 0 : e.Passed;

            // the lines first, the points over them
            foreach (var (from, to) in map.Edges)
            {
                bool lit = on.Contains(from) && on.Contains(to);
                var a = map.Node(from);
                var b = map.Node(to);
                int ib = walk.IndexOf(to);
                bool walked = e != null && ib >= 0 && ib < passed;
                Color c = walked ? GoldText : lit ? CreamText : new Color(1, 1, 1, 0.22f);
                DottedLine(Pos(a), Pos(b), c, lit ? 3 : 2);
            }
            foreach (var n in map.Nodes)
            {
                var p = Pos(n);
                if (n.Type == RoutePoint.Gate)
                {
                    ArtLibrary.Draw(new Rect(p.x - 18, p.y - 18, 36, 36), "Buttons/Fortress");
                    continue;
                }
                int index = walk.IndexOf(n.Id);
                bool inWalk = index >= 0, done = e != null && inWalk && index < passed;
                float size = inWalk ? 26 : 20;
                var old = GUI.color;
                var tint = PointColor(n.Type);
                GUI.color = new Color(tint.r, tint.g, tint.b, inWalk ? (done ? 1f : 0.85f) : 0.3f);
                ArtLibrary.Draw(new Rect(p.x - size / 2, p.y - size / 2, size, size), "UI/Round");
                GUI.color = old;
                if (done)
                    Outline(new Rect(p.x - size / 2 - 2, p.y - size / 2 - 2, size + 4, size + 4));
                Txt(new Rect(p.x - 15, p.y - 11, 30, 22), PointGlyph(n.Type), 13, inWalk ? CreamText : DimText);
            }
            // where the team is now: between the last passed point and the next one, by the time
            if (e != null)
            {
                var times = session.RouteTimes(e);
                Vector2 at;
                if (e.Back || passed >= walk.Count)
                    at = Pos(map.Node(GameSession.RouteGateId));
                else
                {
                    var prev = passed > 0 ? map.Node(walk[passed - 1]) : map.Node(GameSession.RouteGateId);
                    long t0 = passed > 0 ? times[passed - 1] : e.Start, t1 = times[passed];
                    float k = t1 > t0 ? Mathf.Clamp((float)(now - t0) / (t1 - t0), 0f, 1f) : 1;
                    at = Lerp(Pos(prev), Pos(map.Node(walk[passed])), k);
                }
                ArtLibrary.Draw(new Rect(at.x - 9, at.y - 26, 18, 18), "UI/Dot");
            }
            // the legend and the three ways
            Txt(new Rect(area.x, area.yMax - 34, area.width, 16),
                "Б — схватка (Сила) · Д — добыча · В — встреча (Дух) · О — отдых (Стойкость) · ! — опасность (Ум, Ловкость)", 11, DimText);
            Txt(new Rect(area.x, area.y + 6, area.width, 16), string.Format("Развилки: {0} и {1} · точек {2}",
                PathName(forks[0]).ToLower(), PathName(forks[1]).ToLower(), walk.Count), 12, CreamText);
            Txt(new Rect(area.x + 20, mid - spread - 9, 120, 16), "надёжно", 10, SafeColor, TextAnchor.MiddleLeft);
            Txt(new Rect(area.x + 20, mid + spread - 7, 120, 16), "долго", 10, LongColor, TextAnchor.MiddleLeft);
        }

        static Vector2 Lerp(Vector2 a, Vector2 b, float t) => new Vector2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);

        /// <summary>A line of small squares (the IMGUI has no lines).</summary>
        static void DottedLine(Vector2 a, Vector2 b, Color c, float thick)
        {
            var old = GUI.color;
            GUI.color = c;
            float len = new Vector2(b.x - a.x, b.y - a.y).magnitude;
            int steps = Math.Max(1, (int)(len / 7));
            for (int i = 0; i <= steps; i++)
            {
                var p = Lerp(a, b, i / (float)steps);
                GUI.DrawTexture(new Rect(p.x - thick / 2, p.y - thick / 2, thick, thick), Texture2D.whiteTexture);
            }
            GUI.color = old;
        }

        // ------------------------------------------------------------------ notes

        void DrawNotes()
        {
            var found = session.State.RouteNotes;
            if (found.Count == 0)
            {
                GUILayout.Label("Записок пока нет. Их находят в руинах Стражей на пути вылазок.");
                return;
            }
            foreach (int id in found)
            {
                var (title, text) = GameSession.RouteNotes[id];
                GUILayout.Label(string.Format("<b>{0}. {1}</b>\n<size=13><color=#e8dcc0>{2}</color></size>", id + 1, title, text), Rich());
            }
            int left = GameSession.RouteNotes.Length - found.Count;
            if (left > 0)
                GUILayout.Label(string.Format("<size=12><color=#d8c8a8>Ещё не найдено: {0}</color></size>", left), Rich());
        }

        // ------------------------------------------------------------------ specializations

        void DrawSpecs()
        {
            GUILayout.Label(string.Format("<b>Специализации залов</b> (за Чертежи из вылазок) · чертежей {0}",
                session.State.Wallet[ResourceType.Blueprints]), Rich());
            var built = GameSession.SpecRooms.Where(t => session.MaxRoomLevel(t) > 0).ToList();
            if (built.Count == 0)
            {
                GUILayout.Label("Постройте залы: у каждого свои специализации с 5-го уровня");
                return;
            }
            if (!built.Contains(specHall))
                specHall = built[0];
            GUILayout.BeginHorizontal();
            int shown = 0;
            foreach (var type in built)
            {
                int picked = Enumerable.Range(0, GameSession.SpecTierCount).Count(t => session.SpecChosen(type, t) != 0);
                if (GUILayout.Toggle(specHall == type, string.Format("{0} {1}/5", session.Balance.Room(type).Name, picked), "Button") && specHall != type)
                    specHall = type;
                if (++shown % 5 == 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
            }
            GUILayout.EndHorizontal();

            int level = session.MaxRoomLevel(specHall);
            var sp = session.Balance.Activities.Specializations;
            for (int tier = 0; tier < GameSession.SpecTierCount; tier++)
            {
                int chosen = session.SpecChosen(specHall, tier), need = session.SpecTierLevel(tier);
                GUILayout.BeginHorizontal();
                GUILayout.Label(string.Format("Ступень {0}\nур. {1} · {2} чер.", tier + 1, need, session.SpecCost(tier)), GUILayout.Width(130));
                for (int option = 1; option <= 2; option++)
                {
                    var opt = GameSession.SpecOptionOf(specHall, tier, option);
                    bool mine = chosen == option;
                    GUI.enabled = mine || (chosen == 0 && level >= need);
                    string text = string.Format("{0}{1}\n{2}", opt.Name, mine ? " " + new string('I', Math.Max(1, session.SpecRankOf(specHall, tier))) : "", opt.Text);
                    if (GUILayout.Toggle(mine, text, "Button") != mine && !mine)
                        Report(session.BuySpec(specHall, tier, option));
                    GUI.enabled = true;
                }
                if (chosen != 0 && session.State.HearthLevel >= sp.RanksFromHearth && session.SpecRankOf(specHall, tier) < sp.Ranks)
                {
                    int rank = session.SpecRankOf(specHall, tier);
                    if (GUILayout.Button(string.Format("Ранг {0}: {1} чер.", rank + 1, session.SpecCost(tier) * (rank + 1)), GUILayout.Width(150)))
                        Report(session.RankUpSpec(specHall, tier));
                }
                GUILayout.EndHorizontal();
            }
            if (level < sp.Tiers[GameSession.SpecTierCount - 1][0])
                GUILayout.Label(string.Format("<size=12><color=#d8c8a8>{0} ур. {1}: ступени открываются на 5, 10, 15, 20 и 25-м уровне зала. Ранги мастерства — с {2}-го уровня очага.</color></size>",
                    session.Balance.Room(specHall).Name, level, sp.RanksFromHearth), Rich());
        }

        // ------------------------------------------------------------------ the developer's page

        void AdminRoutes(AdminList list, long now)
        {
            var st = session.State;
            list.Row(string.Format("Вылазки · в пути {0}, вернулись {1}, сегодня осталось {2}", session.ActiveRoutes,
                st.Expeditions.Count(e => e.Back), session.RoutesLeft(now)));
            list.Add("Вернуть все отряды", _ => "Вернулись: " + session.AdminFinishRoutes(now));
            list.Add("Новые вылазки на сегодня", _ => "Можно начать: " + session.AdminRefillRoutes(now));
            list.Row(string.Format("Записки о мире · {0} из {1}", st.RouteNotes.Count, GameSession.RouteNotes.Length));
            list.Add("+1 записка", _ => "Записок: " + session.AdminGiveNotes(1));
            list.Add("Все записки", _ => "Записок: " + session.AdminGiveNotes(GameSession.RouteNotes.Length));
            list.Add("Стереть записки", _ =>
            {
                session.AdminClearNotes();
                return "Журнал записок пуст";
            }, true);
            list.Row(string.Format("Обломки карт · {0}", st.MapFragments));
            list.Add("+1 обломок", _ => "Обломков: " + session.AdminGiveFragments(1));
            list.Add("+5 обломков", _ => "Обломков: " + session.AdminGiveFragments(5));
            list.Add("Отдать Кладоискателям", _ =>
            {
                var fail = session.UseMapFragment(now);
                return fail == Fail.None ? "Обломок отдан: мест клада меньше" : fail == Fail.NotRunning ? "Кладоискатели сейчас не идут" : "Нельзя: " + fail;
            });
            int picked = GameSession.SpecRooms.Sum(t => Enumerable.Range(0, GameSession.SpecTierCount).Count(k => session.SpecChosen(t, k) != 0));
            list.Row(string.Format("Специализации · выбрано {0} из {1} · чертежей {2}", picked, GameSession.SpecRooms.Length * GameSession.SpecTierCount,
                st.Wallet[ResourceType.Blueprints]));
            list.Add("Сбросить все (вернуть чертежи)", _ => "Возвращено чертежей: " + session.AdminResetAllSpecs(), true);
            foreach (var type in GameSession.SpecRooms)
            {
                var chosen = Enumerable.Range(0, GameSession.SpecTierCount).Where(k => session.SpecChosen(type, k) != 0).ToList();
                if (chosen.Count == 0)
                    continue;
                list.Row(session.Balance.Room(type).Name);
                foreach (int tier in chosen)
                {
                    var opt = session.SpecChosenOption(type, tier);
                    var t = type;
                    int k = tier;
                    list.Add(string.Format("Сбросить {0}: {1}", tier + 1, opt.Name), _ => string.Format("«{0}» сброшено, возвращено чертежей: {1}", opt.Name, session.AdminResetSpec(t, k)));
                }
            }
        }
    }
}

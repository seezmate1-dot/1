using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core;
using UnityEngine;

namespace LastHearth.Game
{
    /// <summary>The "Развитие", "Вылазки" and "Задания" tabs.</summary>
    public sealed partial class GameRunner
    {
        readonly List<int> routeTeam = new List<int>();
        readonly RoutePath[] routeForks = { RoutePath.Safe, RoutePath.Long };

        static readonly RoomType[] SpecRooms =
        {
            RoomType.CoalMine, RoomType.Kitchen, RoomType.Mine, RoomType.TradingPost, RoomType.Greenhouse,
            RoomType.Barracks, RoomType.Warehouse,
        };

        // ------------------------------------------------------------------ Heart of the Hearth

        void DrawHeart()
        {
            var h = session.Balance.Activities.Heart;
            var (cost, gate) = session.NextHeartNode();
            GUILayout.Label(string.Format("<b>Сердце очага</b> (за Жар из Наплыва) · узлов {0}/{1} · следующий: {2}",
                session.HeartTotal, h.NodeCost.Length, cost == 0 ? "всё изучено" : cost + " жара" + (gate > session.State.HearthLevel ? ", нужен очаг " + gate : "")), Rich());
            GUILayout.BeginHorizontal();
            string[] names = { "Тепло: +{0:0}% тепла этажей", "Хозяйство: +{0:0}% выработки, −угля", "Оборона: +{0:0}% защиты в Наплыве" };
            double[] values = { h.Warmth, h.Output, h.Defense };
            foreach (HeartBranch branch in Enum.GetValues(typeof(HeartBranch)))
            {
                int i = (int)branch;
                string label = string.Format(names[i], values[i] * 100 * session.State.Heart[i]) + string.Format("\n{0}/{1}", session.State.Heart[i], h.PerBranch);
                if (GUILayout.Button(label, GUILayout.Height(44)))
                    Report(session.BuyHeart(branch));
            }
            GUILayout.EndHorizontal();
        }

        // ------------------------------------------------------------------ specializations

        void DrawSpecs()
        {
            GUILayout.Label(string.Format("<b>Специализации залов</b> (за Чертежи из вылазок) · чертежей {0}",
                session.State.Wallet[ResourceType.Blueprints]), Rich());
            foreach (var type in SpecRooms)
            {
                int level = session.MaxRoomLevel(type);
                if (level <= 0)
                    continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label(string.Format("{0} ур.{1}", session.Balance.Room(type).Name, level), GUILayout.Width(170));
                for (int tier = 0; tier < GameSession.SpecTiers.Length; tier++)
                {
                    int chosen = session.SpecChosen(type, tier);
                    if (chosen != 0)
                    {
                        var opt = GameSession.SpecTiers[tier][chosen - 1];
                        int rank = session.SpecRankOf(type, tier);
                        if (GUILayout.Button(string.Format("{0} {1}\n{2}", opt.Name, new string('I', rank), opt.Text), GUILayout.Width(170)))
                            Report(session.RankUpSpec(type, tier));
                        continue;
                    }
                    if (level < session.SpecTierLevel(tier))
                    {
                        GUILayout.Label(string.Format("с ур.{0}", session.SpecTierLevel(tier)), GUILayout.Width(170));
                        continue;
                    }
                    GUILayout.BeginVertical(GUILayout.Width(170));
                    for (int option = 1; option <= 2; option++)
                    {
                        var opt = GameSession.SpecTiers[tier][option - 1];
                        if (GUILayout.Button(string.Format("{0} ({1} чер.)\n{2}", opt.Name, session.SpecCost(tier), opt.Text)))
                            Report(session.BuySpec(type, tier, option));
                    }
                    GUILayout.EndVertical();
                }
                GUILayout.EndHorizontal();
            }
        }

        // ------------------------------------------------------------------ routes

        void DrawRoutes(long now)
        {
            var r = session.Balance.Activities.Routes;
            GUILayout.Label("<b>Вылазки-маршруты</b> · отряд из 1–3 жителей уходит в туман за Чертежами", Rich());
            if (session.State.HearthLevel < r.Unlock)
            {
                GUILayout.Label("Откроются на " + r.Unlock + "-м уровне очага");
                return;
            }
            foreach (var e in session.State.Expeditions.ToList())
            {
                string team = string.Join(", ", e.Team.Select(id => session.FindDweller(id)?.Name ?? "?"));
                if (!e.Back)
                {
                    GUILayout.Label(string.Format("В пути: {0} · вернутся через {1}", team, Duration(e.End - now)));
                    continue;
                }
                GUILayout.Label(string.Format("Вернулись: {0} · {1}", team, e.Report));
                GUILayout.BeginHorizontal();
                if (e.Card >= 0)
                {
                    var card = GameSession.RouteCards[e.Card];
                    GUILayout.Label(card.Title + ":", GUILayout.Width(300));
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
            }

            GUILayout.Label(string.Format("Новых вылазок сегодня: {0}. Выберите отряд:", session.RoutesLeft(now)));
            var free = session.State.Dwellers.Where(d => d.RouteId == 0 && d.InjuredUntil <= now).ToList();
            routeTeam.RemoveAll(id => free.All(d => d.Id != id));
            GUILayout.BeginHorizontal();
            int shown = 0;
            foreach (var d in free)
            {
                bool on = routeTeam.Contains(d.Id);
                string label = string.Format("{0}{1} [Л{2} С{3} У{4}]", d.Name, d.IsHero ? " (герой)" : "", d.StatOf(Stat.Agility), d.StatOf(Stat.Strength), d.StatOf(Stat.Intellect));
                if (GUILayout.Toggle(on, label, "Button") != on)
                {
                    if (on)
                        routeTeam.Remove(d.Id);
                    else if (routeTeam.Count < 3)
                        routeTeam.Add(d.Id);
                }
                if (++shown % 4 == 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
            }
            GUILayout.EndHorizontal();

            for (int fork = 0; fork < 2; fork++)
            {
                var (stat, need) = session.ForkCheck(session.State.NextBattleSeed, fork, session.State.HearthLevel);
                int have = routeTeam.Sum(id => session.FindDweller(id).StatOf(stat));
                GUILayout.BeginHorizontal();
                GUILayout.Label(string.Format("Развилка {0}:", fork + 1), GUILayout.Width(100));
                foreach (RoutePath path in Enum.GetValues(typeof(RoutePath)))
                {
                    string text = path == RoutePath.Safe ? "Надёжный путь" :
                        path == RoutePath.Long ? "Долгий путь (+время, +добыча)" :
                        string.Format("Рискованный: {0} {1}/{2}", StatName(stat), have, need);
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

        static string StatName(Stat s)
        {
            switch (s)
            {
                case Stat.Strength: return "Сила";
                case Stat.Agility: return "Ловкость";
                case Stat.Intellect: return "Ум";
                case Stat.Endurance: return "Стойкость";
                default: return "Дух";
            }
        }

        // ------------------------------------------------------------------ daily tasks (the shops: GameRunner.Kennel.cs)

        void DrawDaily(long now)
        {
            var d = session.Balance.Activities.Daily;
            var st = session.State;
            GUILayout.Label(string.Format("<b>Поручения дня</b> · выполнено {0}/{1} нужных · каждое +{2} очков сезона\n" +
                "<size=12><color=#d8c8a8>Пропущенный день ничего не отнимает.</color></size>",
                session.TasksDone(now), d.TasksNeeded, session.SeasonInfo.TaskXp), Rich());
            foreach (var t in session.TodayTasks(now))
            {
                int have = st.DailyProgress[(int)t], need = session.TaskTimes(t);
                GUILayout.Label(string.Format("  {0} {1} ({2}/{3})", have >= need ? "✓" : "·", session.TaskText(t), Math.Min(have, need), need));
            }
            GUI.enabled = !st.DailyClaimed;
            if (GUILayout.Button(st.DailyClaimed ? "Награда дня получена" : string.Format("Забрать: {0} искр и {1} мин песочных часов", d.TaskSparks, d.TaskHourglassMinutes)))
                Report(session.ClaimDaily(now));
            GUI.enabled = true;
            if (st.Achievements.Count > 0)
                GUILayout.Label("Достижений: " + st.Achievements.Count);
        }

        static string GoodsName(ShopItem it)
        {
            switch (it.Goods)
            {
                case ShopGoods.Essence: return "Эссенция " + it.Amount;
                case ShopGoods.Coal: return "Уголь " + it.Amount;
                case ShopGoods.Food: return "Еда " + it.Amount;
                case ShopGoods.Hourglass: return "Песочные часы " + Duration(it.Amount);
                case ShopGoods.RecruitSeal: return "Печать найма";
                default: return "Сделка дня: часы " + Duration(it.Amount);
            }
        }
    }
}

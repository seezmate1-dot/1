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

        // specializations and routes: GameRunner.Routes.cs

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

using System;
using System.Linq;
using LastHearth.Core;
using LastHearth.Core.Combat;
using LastHearth.Core.Surge;
using UnityEngine;

namespace LastHearth.Game
{
    /// <summary>The "Бои" and "Наплыв" tabs: the campaign and the live evening defense (the arena and raids: GameRunner.Arena.cs).</summary>
    public sealed partial class GameRunner
    {
        SurgeDefense surge;
        Func<SurgeDefense, string> surgeFinish;
        string surgeResult;
        float surgeClock;
        int surgeHero = -1;

        // ------------------------------------------------------------------ campaign

        void DrawCampaign(long now)
        {
            var c = session.Balance.Campaign;
            int next = session.MissionsCleared;
            GUILayout.Label(string.Format("<b>Кампания</b> · пройдено {0}/{1} · повторов сегодня {2}/{3}", next, c.Missions.Count,
                session.RepeatsLeft(now), c.RepeatsPerDay), Rich());
            if (next < c.Missions.Count)
            {
                var m = session.Mission(next);
                var act = c.Acts[Math.Max(0, Math.Min(c.Acts.Count - 1, m.Act - 1))];
                if (TutorButton("campaignNext", string.Format("Бой {0}: акт «{1}», {2}, {3} ранга {4} · +{5} эсс., +{6} мат.{7}", next + 1, act.Name,
                        KindName(m.Kind), combat.Family(m.Family).Name, m.Rank, m.Essence, m.Materials, m.Sparks > 0 ? ", +" + m.Sparks + " искр" : "")))
                    StartMissionBattle(next, now);
            }
            if (next == 0)
                return;
            GUILayout.BeginHorizontal();
            int last = next - 1;
            var stars = session.State.CampaignStars;
            for (int i = Math.Max(0, next - 5); i < next; i++)
            {
                if (GUILayout.Button(string.Format("{0} {1}", i + 1, StarText(stars[i])), GUILayout.Width(110)))
                    StartMissionBattle(i, now);
            }
            if (GUILayout.Button("Повторить бой " + (last + 1) + " мгновенно"))
            {
                var fail = session.RepeatMission(last, now, out var reward);
                Report(fail);
                if (fail == Fail.None)
                    message = "Повтор: " + CostText(reward.Got);
            }
            GUILayout.EndHorizontal();
        }

        void StartMissionBattle(int index, long now)
        {
            var fail = session.StartMission(index, now, out var started);
            Report(fail);
            if (fail != Fail.None)
                return;
            BeginBattle(started, done =>
            {
                session.FinishMission(done, out var reward);
                string text = (done.Won ? "Победа " : "Поражение ") + StarText(reward.Stars);
                if (reward.FirstClear)
                    text += " · первая победа: " + CostText(reward.Got);
                if (reward.NewHero != null)
                    text += " · к отряду примкнул " + reward.NewHero.Name;
                return text;
            }, "Бой " + (index + 1));
        }

        static string KindName(MissionKind k)
        {
            switch (k)
            {
                case MissionKind.Elite: return "элита";
                case MissionKind.Boss: return "БОСС";
                default: return "обычный";
            }
        }

        // ------------------------------------------------------------------ the evening Surge

        void DrawSurgeTab(long now)
        {
            var sd = session.Balance.Activities.Surge;
            GUILayout.Label("<b>Наплыв</b> — каждый вечер с " + sd.StartHour + ":00 твари лезут из холодных залов к очагу", Rich());
            if (session.State.HearthLevel < sd.Unlock)
            {
                GUILayout.Label("Начнётся на " + sd.Unlock + "-м уровне очага");
                return;
            }
            GUILayout.Label("Сегодня: " + (session.State.SurgeDay == session.LocalDay(now) ? "уже отбит" :
                session.SurgeOpen(now) ? "<color=#f84>идёт — защищайте!</color>" : "ещё не начался (без вас отобьётся в полночь на 70%)"), Rich());
            if (session.SurgeOpen(now) && GUILayout.Button("Защищать вживую", GUILayout.Height(40)))
            {
                var fail = session.StartSurge(now, out var started);
                Report(fail);
                if (fail == Fail.None)
                {
                    BeginSurge(started, done =>
                    {
                        session.FinishSurge(done, Now(), out var r);
                        return string.Format("{0} · +{1} жара · +{2} эссенции{3}", StarText(r.Stars), r.Ember, r.Essence,
                            r.Frost ? " · <color=#6cf>очаг погас: залы промёрзли до утра</color>" : "");
                    });
                }
            }
        }

        void BeginSurge(SurgeDefense started, Func<SurgeDefense, string> finish)
        {
            surge = started;
            surgeFinish = finish;
            surgeResult = null;
            surgeClock = 0;
            surgeHero = -1;
        }

        void UpdateSurge()
        {
            if (surge == null)
                return;
            if (!surge.Over)
            {
                surgeClock += Time.unscaledDeltaTime;
                while (surgeClock >= (float)combat.TickSeconds && !surge.Over)
                {
                    surgeClock -= (float)combat.TickSeconds;
                    surge.Step();
                }
            }
            if (surge.Over && surgeResult == null)
                surgeResult = surgeFinish(surge);
        }

        void DrawSurge()
        {
            var c = surge.Numbers;
            GUILayout.Label(string.Format("<b>Наплыв</b> · {0:0} с из {1:0} · тварей {2}/{3} · убито {4} · прорвалось {5} → {6}",
                surge.Tick * combat.TickSeconds, c.Ticks * combat.TickSeconds, surge.Creatures.Count, surge.TotalCreatures,
                surge.Killed, surge.Breaches, StarText(surge.Stars)), Rich());
            // the real fortress with the creatures on it (the Siege has no rooms: only the list below)
            if (surge.Setup.HearthFloor == session.Balance.Grid.HearthFloor)
                DrawCutaway(Now(), surge);
            var floors = surge.Setup.SpawnFloors.Concat(surge.Defenders.Select(d => d.Floor)).Concat(new[] { surge.HearthFloor })
                .Distinct().OrderByDescending(f => f).ToList();
            int top = floors.Max(), bottom = floors.Min();
            for (int f = top; f >= bottom; f--)
            {
                GUILayout.BeginHorizontal();
                string name = f == surge.HearthFloor ? "<b>ОЧАГ</b>" : "этаж " + (f + 1);
                string creatures = string.Join(" ", surge.Creatures.Where(x => x.Floor == f).Select(x => "тварь " + (int)(100 * x.Hp / x.MaxHp) + "%"));
                string defenders = string.Join(", ", surge.Defenders.Where(d => d.Floor == f).Select(d => (d.Hero ? "<b>" + d.Name + "</b>" : d.Name)));
                GUILayout.Label(string.Format("{0}{1} | {2} | {3}", name, surge.HatchClosed(f) ? " [люк закрыт]" : "", creatures, defenders), Rich(), GUILayout.Width(700));
                GUI.enabled = !surge.Over;
                if (surgeHero >= 0 && GUILayout.Button("Сюда", GUILayout.Width(70)))
                {
                    surge.Command(SurgeCommand.Move(surgeHero, f));
                    surgeHero = -1;
                }
                if (f != surge.HearthFloor && surge.HatchesLeft > 0 && GUILayout.Button("Люк", GUILayout.Width(60)))
                    surge.Command(SurgeCommand.Hatch(f));
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            GUI.enabled = !surge.Over;
            foreach (var h in surge.Defenders.Where(d => d.Hero))
            {
                if (GUILayout.Toggle(surgeHero == h.Id, h.Name, "Button") && surgeHero != h.Id)
                    surgeHero = h.Id;
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUI.enabled = !surge.Over && surge.Tick >= surge.FlashReadyAt;
            if (GUILayout.Button(string.Format("Вспышка очага ({0} угля){1}", c.FlashCoal,
                    surge.Tick < surge.FlashReadyAt ? string.Format(" через {0:0} с", (surge.FlashReadyAt - surge.Tick) * combat.TickSeconds) : ""), GUILayout.Height(40)))
                surge.Command(SurgeCommand.Flash());
            GUI.enabled = !surge.Over;
            if (GUILayout.Button("Отдать оборону автомату", GUILayout.Height(40)))
                surge.Brain = SurgeDefense.Careful();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Label("Люков осталось: " + surge.HatchesLeft + " · выберите героя, затем «Сюда» на этаже");
            foreach (string line in surge.Log.Skip(Math.Max(0, surge.Log.Count - 5)))
                GUILayout.Label("   " + line);
            if (surgeResult != null)
            {
                GUILayout.Label("<size=22><b>" + surgeResult + "</b></size>", Rich());
                if (GUILayout.Button("Закрыть", GUILayout.Height(40)))
                {
                    message = surgeResult;
                    surge = null;
                }
            }
        }
    }
}

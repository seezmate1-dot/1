using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core;
using UnityEngine;

namespace LastHearth.Game
{
    /// <summary>The "События" tab: the seven live events and the lantern mini-game of the Festival.</summary>
    public sealed partial class GameRunner
    {
        int lanternSeed;
        float lanternClock;
        readonly List<(double time, int spot)> lanternTaps = new List<(double time, int spot)>();
        int fairSell, fairBuy = 2;

        static readonly string[] FairGoodsNames = { "уголь", "еда", "железо", "серебро" };
        static readonly string[] TournamentRules = { "Сталь: ближний бой +30%, магия −50%", "Холод: лечение −50%", "Рыцари Двора" };

        void DrawEvents(long now)
        {
            var runs = session.ActiveRuns(now);
            if (runs.Count == 0)
            {
                GUILayout.Label("Сейчас событий нет. Календарь на 28 дней начинается в понедельник; новичок получает личный однодневный заход в каждое событие.");
            }
            foreach (var run in runs)
            {
                var e = session.EventInfo(run.Id);
                double target = GameSession.EventTarget[run.Id] * (run.Personal ? 1.0 / Math.Max(1, e.Days) : 1);
                GUILayout.Space(6);
                GUILayout.Label(string.Format("<b>{0}</b>{1} · очков {2:0}/{3:0} · пороги {4}/10 · место {5} из 50 · осталось {6}",
                    e.Name, run.Personal ? " (личный заход)" : "", run.Points, target, run.Thresholds, session.EventRank(run), Duration(run.End - now)), Rich());
                switch (run.Id)
                {
                    case "LongNight": DrawLongNight(run, now); break;
                    case "Tournament": DrawTournament(run, now); break;
                    case "Portal": DrawPortal(run, now); break;
                    case "Treasure": DrawTreasure(run, now); break;
                    case "Fair": DrawFair(run, now); break;
                    case "Siege": DrawSiege(run, now); break;
                    case "Festival": DrawFestival(run, now); break;
                }
                if (!string.IsNullOrEmpty(run.LastResult))
                    GUILayout.Label("   " + run.LastResult);
            }
            var closed = session.State.Events.Where(r => r.Closed).ToList();
            if (closed.Count > 0)
            {
                GUILayout.Space(6);
                GUILayout.Label("<b>Итоги прошедших</b>", Rich());
                foreach (var r in closed)
                    GUILayout.Label("   " + r.LastResult);
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

        void DrawLongNight(EventRun run, long now)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(string.Format("Атаковать Ночного Жнеца (атак {0}; +1 после Наплыва)", session.NightAttacksLeft(run, now))))
                EventFight("LongNight", now, -1, "Ночной Жнец");
            if (GUILayout.Button("Ещё атака за 30 искр"))
                Report(session.BuyEventExtra("LongNight", now));
            GUILayout.EndHorizontal();
        }

        void DrawTournament(EventRun run, long now)
        {
            GUILayout.Label("Правило дня: " + TournamentRules[session.TournamentRule(run, now)] + string.Format(" · лучший результат дня {0:0}", run.DayBest));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(string.Format("Бой турнира (попыток {0})", session.TournamentAttemptsLeft(run, now))))
                EventFight("Tournament", now);
            if (GUILayout.Button("Попытка за 20 искр"))
                Report(session.BuyEventExtra("Tournament", now));
            GUILayout.EndHorizontal();
        }

        void DrawPortal(EventRun run, long now)
        {
            int open = session.PortalFloorsOpen(run, now);
            GUILayout.Label(string.Format("Этаж {0} · привал на {1} · {2}{3} · благословения: {4}", run.Floor + 1, run.Checkpoint + 1,
                run.RunAlive ? "отряд в пути" : "отряд у входа", open == int.MaxValue ? "" : " · открыто этажей " + open,
                run.Blessings.Count == 0 ? "нет" : string.Join(", ", run.Blessings.Select(b => GameSession.BlessingNames[b].Split(':')[0]))));
            if (run.BlessingOffer.Count > 0)
            {
                GUILayout.BeginHorizontal();
                for (int i = 0; i < run.BlessingOffer.Count; i++)
                {
                    if (GUILayout.Button(GameSession.BlessingNames[run.BlessingOffer[i]]))
                        Report(session.ChooseBlessing(i, now));
                }
                GUILayout.EndHorizontal();
                return;
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(run.RunAlive ? "Следующий этаж" : "Войти в Портал (1 заход в день)"))
                EventFight("Portal", now, -1, "Портал, этаж " + (run.Floor + 1));
            if (!run.RunAlive && run.Revives < 2 && run.FreeRunDay == session.LocalDay(now) &&
                GUILayout.Button("Воскресить за " + (run.Revives == 0 ? 50 : 100) + " искр"))
                Report(session.PortalRevive(now));
            GUILayout.EndHorizontal();
        }

        void DrawTreasure(EventRun run, long now)
        {
            GUILayout.Label(string.Format("Карта {0} · факелов {1} · частей карты {2} · сокровищ {3} · ближе к сокровищу клетки «теплее»",
                run.MapNo + 1, session.TorchesLeft(run, now), run.Pieces, run.Treasures));
            var targets = session.TreasureCandidates(run);
            var cells = GameSession.HexCells;
            foreach (var row in cells.Select((c, i) => (c, i)).GroupBy(x => x.c.r).OrderBy(g => g.Key))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(Math.Abs(row.Key) * 22);
                foreach (var (c, i) in row.OrderBy(x => x.c.q))
                {
                    string label;
                    bool opened = run.Opened.Contains(i);
                    if (opened)
                        label = CellName(session.CellContent(run, i));
                    else if (targets.Count > 0)
                        label = targets.Min(t => GameSession.HexDistance(i, t)).ToString();
                    else
                        label = "?";
                    GUI.enabled = !opened && session.CellOpenable(run, i);
                    if (GUILayout.Button(label, GUILayout.Width(44), GUILayout.Height(30)))
                    {
                        var fail = session.OpenCell(i, now, out var content);
                        Report(fail);
                        if (fail == Fail.None)
                        {
                            message = "Под клеткой: " + CellName(content);
                            if (content == GameSession.TreasureCell.Ambush)
                                EventFight("Treasure", now, i, "Засада");
                        }
                    }
                    GUI.enabled = true;
                }
                GUILayout.EndHorizontal();
            }
        }

        static string CellName(GameSession.TreasureCell c)
        {
            switch (c)
            {
                case GameSession.TreasureCell.Resources: return "мат";
                case GameSession.TreasureCell.Refugee: return "чел";
                case GameSession.TreasureCell.Ambush: return "зас";
                case GameSession.TreasureCell.Piece: return "кус";
                case GameSession.TreasureCell.Spring: return "род";
                case GameSession.TreasureCell.Beacon: return "мая";
                case GameSession.TreasureCell.Trap: return "лов";
                default: return "КЛАД";
            }
        }

        void DrawFair(EventRun run, long now)
        {
            var rates = session.FairRates(run, now);
            GUILayout.Label(string.Format("Курсы (меняются каждые 4 часа): {0} · сделок {1}/20",
                string.Join(", ", Enumerable.Range(0, 4).Select(i => FairGoodsNames[i] + " ×" + rates[i].ToString("0.00"))), run.Trades));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Продать:", GUILayout.Width(70));
            for (int i = 0; i < 4; i++)
            {
                if (GUILayout.Toggle(fairSell == i, FairGoodsNames[i], "Button") && fairSell != i)
                    fairSell = i;
            }
            GUILayout.Label("Купить:", GUILayout.Width(60));
            for (int i = 0; i < 4; i++)
            {
                if (GUILayout.Toggle(fairBuy == i, FairGoodsNames[i], "Button") && fairBuy != i)
                    fairBuy = i;
            }
            long have = session.State.Wallet[GameSession.FairGoods[fairSell]];
            if (GUILayout.Button("Сделка: четверть запаса (" + have / 4 + ")"))
            {
                var fail = session.FairTrade(fairSell, fairBuy, Math.Max(1, have / 4), now, out long got);
                Report(fail);
                if (fail == Fail.None)
                    message = "Получено " + got + " " + FairGoodsNames[fairBuy];
            }
            GUILayout.EndHorizontal();
            for (int o = 0; o < run.Orders.Count; o++)
            {
                var order = run.Orders[o];
                if (GUILayout.Button(string.Format("Заказ: {0} {1} и {2} {3} → +6 жетонов", order[1], FairGoodsNames[order[0]], order[3], FairGoodsNames[order[2]])))
                    Report(session.FairDeliver(o, now));
            }
        }

        void DrawSiege(EventRun run, long now)
        {
            long window = session.SiegeWindow(run, now);
            GUILayout.Label(string.Format("Камень {0} · штурмы в 12–14 и 20–22 · {1}", run.Stone,
                window >= 0 ? "<color=#f84>ИДЁТ ШТУРМ: " + string.Join(", ", session.SiegeTargets(run, window).Select(t => GameSession.OutpostNames[t])) + "</color>" : "сейчас тихо"), Rich());
            for (int o = 0; o < run.Outposts.Count; o++)
            {
                var post = run.Outposts[o];
                GUILayout.BeginHorizontal();
                string garrison = post.Garrison.Count == 0 ? "пусто" : string.Join(", ", post.Garrison.Select(id => session.FindDweller(id)?.Name ?? "?"));
                GUILayout.Label(string.Format("{0}: гарнизон {1} · ловушки {2}/3{3}", GameSession.OutpostNames[o], garrison, post.Traps,
                    post.Fallen ? " · <color=#f55>ПАЛА (−5% выработки)</color>" : ""), Rich(), GUILayout.Width(520));
                int index = o;
                if (GUILayout.Button("Гарнизон: 2 сильнейших свободных", GUILayout.Width(250)))
                {
                    var used = run.Outposts.SelectMany(x => x.Garrison).ToList();
                    var guards = session.State.Dwellers.Where(d => !d.IsHero && d.RouteId == 0 && !used.Contains(d.Id))
                        .OrderByDescending(d => d.StatOf(Stat.Strength) + d.StatOf(Stat.Endurance)).Take(2).Select(d => d.Id).ToList();
                    Report(session.SetGarrison(index, guards, now));
                }
                if (post.Traps < 3 && GUILayout.Button("Ловушки (" + 4 * (post.Traps + 1) + " камня)", GUILayout.Width(150)))
                    Report(session.UpgradeTraps(index, now));
                if (post.Fallen && GUILayout.Button("Отбить", GUILayout.Width(90)))
                    EventFight("Siege", now, index, "Отбить " + GameSession.OutpostNames[index]);
                GUILayout.EndHorizontal();
            }
            if (window >= 0 && GUILayout.Button("Защищать штурм вживую (×1,5 очков)", GUILayout.Height(36)))
            {
                var fail = session.StartSiegeDefense(now, out var started);
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
        }

        void DrawFestival(EventRun run, long now)
        {
            GUILayout.Label(string.Format("Зажги фонари: 30 фонарей за минуту, лучший результат дня {0:0} · поручения дня дают +100", run.DayBest));
            if (GUILayout.Button("Играть (3 раза в день)"))
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

        void DrawLanterns()
        {
            var lanterns = GameSession.Lanterns(lanternSeed);
            GUILayout.Label(string.Format("<b>Праздник очага</b> · {0:0} с из 60 · зажжено {1}", lanternClock,
                GameSession.LanternScore(lanternSeed, lanternTaps) / 10), Rich());
            for (int row = 0; row < 3; row++)
            {
                GUILayout.BeginHorizontal();
                for (int col = 0; col < 3; col++)
                {
                    int spot = row * 3 + col;
                    bool lit = lanterns.Any(l => l.spot == spot && Math.Abs(l.time - lanternClock) <= 1.0);
                    if (GUILayout.Button(lit ? "ФОНАРЬ!" : "·", GUILayout.Width(140), GUILayout.Height(90)))
                        lanternTaps.Add((lanternClock, spot));
                }
                GUILayout.EndHorizontal();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using SunnyValley.Core.Match3;

namespace SunnyValley.Tools
{
    /// <summary>Level tools: build (generate + tune), sim (win rates), report (docs/LEVELS.md), bench.</summary>
    public static class Program
    {
        static string Root
        {
            get
            {
                var d = new DirectoryInfo(AppContext.BaseDirectory);
                while (d != null && !Directory.Exists(Path.Combine(d.FullName, "farm", "unity"))) d = d.Parent;
                if (d == null) throw new InvalidOperationException("run from inside the repository");
                return d.FullName;
            }
        }

        static string LevelsDir => Path.Combine(Root, "farm", "unity", "SunnyValley", "Assets", "SunnyValley", "Resources", "Levels");
        static string TuningFile => Path.Combine(Root, "farm", "match3-tools", "tuning.json");

        static int Arg(string[] args, string name, int def)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? int.Parse(args[i + 1]) : def;
        }

        static string ArgS(string[] args, string name, string def)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : def;
        }

        public static int Main(string[] args)
        {
            string cmd = args.Length > 0 ? args[0] : "help";
            int from = Arg(args, "--from", 1), to = Arg(args, "--to", 60);
            switch (cmd)
            {
                case "build": Build(from, to, Arg(args, "--games", 160)); return 0;
                case "sim": Sim(from, to, Arg(args, "--games", 200), ArgS(args, "--bot", "average")); return 0;
                case "report": Report(Arg(args, "--games", 200), Arg(args, "--good", 40)); return 0;
                case "bench": Bench(from, to, Arg(args, "--games", 20), ArgS(args, "--bot", "average"), Arg(args, "--moves", 25)); return 0;
                default:
                    Console.WriteLine("usage: build|sim|report|bench [--from N] [--to N] [--games N] [--bot average|good]");
                    return 1;
            }
        }

        public static Func<IMatch3Bot> BotFactory(string name) =>
            name == "good" ? (Func<IMatch3Bot>)(() => new GoodBot()) : () => new AverageBot();

        static void Bench(int from, int to, int games, string bot, int moves)
        {
            foreach (var d in LevelDesigns.All().Where(x => x.Id >= from && x.Id <= to))
            {
                var level = d.ToLevel(moves);
                var errs = LevelValidator.Validate(level);
                if (errs.Count > 0) { Console.WriteLine("level " + d.Id + " INVALID: " + string.Join("; ", errs)); continue; }
                var sw = Stopwatch.StartNew();
                var r = LevelSimulator.Run(level, BotFactory(bot), games, 1);
                Console.WriteLine(r + $"  [{sw.ElapsedMilliseconds} ms, {sw.ElapsedMilliseconds / (double)games:0} ms/game]");
            }
        }

        // ------------------------------------------------------------------ build: generate + tune

        public sealed class Tuning
        {
            public int Id;
            public int Moves;
            public double Target;
            public bool Hard;
            public double AvgWinRate;
            public Dictionary<int, double> Curve = new Dictionary<int, double>();
        }

        static Dictionary<int, Tuning> LoadTuning() =>
            File.Exists(TuningFile) ? JsonConvert.DeserializeObject<List<Tuning>>(File.ReadAllText(TuningFile)).ToDictionary(t => t.Id) : new Dictionary<int, Tuning>();

        static void SaveTuning(Dictionary<int, Tuning> t) =>
            File.WriteAllText(TuningFile, JsonConvert.SerializeObject(t.Values.OrderBy(x => x.Id).ToList(), Formatting.Indented));

        static void Build(int from, int to, int games)
        {
            Directory.CreateDirectory(LevelsDir);
            var tuning = LoadTuning();
            foreach (var d in LevelDesigns.All().Where(x => x.Id >= from && x.Id <= to))
            {
                var sw = Stopwatch.StartNew();
                var errs = LevelValidator.Validate(d.ToLevel(25));
                if (errs.Count > 0) { Console.WriteLine("level " + d.Id + " INVALID: " + string.Join("; ", errs)); continue; }
                var t = new Tuning { Id = d.Id, Target = d.Target, Hard = d.Hard };
                double Rate(int m)
                {
                    if (t.Curve.TryGetValue(m, out var r)) return r;
                    r = LevelSimulator.Run(d.ToLevel(m), BotFactory("average"), games, 1).WinRate;
                    t.Curve[m] = r;
                    return r;
                }
                // Smallest move count whose win rate reaches the target (win rate grows with moves).
                int lo = d.MinMoves, hi = d.MaxMoves;
                if (Rate(hi) < d.Target) lo = hi;
                else if (Rate(lo) >= d.Target) hi = lo;
                while (lo < hi)
                {
                    int mid = (lo + hi) / 2;
                    if (Rate(mid) >= d.Target) hi = mid; else lo = mid + 1;
                }
                t.Moves = hi;
                t.AvgWinRate = Rate(hi);
                tuning[d.Id] = t;
                var level = d.ToLevel(t.Moves);
                File.WriteAllText(Path.Combine(LevelsDir, $"level_{d.Id:000}.json"), LevelLoader.ToJson(level));
                string flag = t.AvgWinRate < d.Target ? "  << below target even at max moves" :
                    (t.Moves == d.MinMoves && t.AvgWinRate > d.Target + 0.06 ? "  << easy even at min moves" : "");
                Console.WriteLine($"level {d.Id,2} {d.Name,-24} moves {t.Moves,2}  avg {t.AvgWinRate * 100,5:0.0}%  target {d.Target * 100:0}%  [{sw.ElapsedMilliseconds / 1000.0:0.0}s]{flag}");
                SaveTuning(tuning);
            }
        }

        // ------------------------------------------------------------------ sim / report

        static List<LevelData> LoadLevels(int from, int to)
        {
            var list = new List<LevelData>();
            for (int id = from; id <= to; id++)
            {
                var path = Path.Combine(LevelsDir, $"level_{id:000}.json");
                if (File.Exists(path)) list.Add(LevelLoader.FromJson(File.ReadAllText(path)));
            }
            return list;
        }

        static void Sim(int from, int to, int games, string bot)
        {
            foreach (var level in LoadLevels(from, to))
            {
                var sw = Stopwatch.StartNew();
                var r = LevelSimulator.Run(level, BotFactory(bot), games, 1);
                Console.WriteLine(r + $"  [{sw.ElapsedMilliseconds / 1000.0:0.0}s]");
            }
        }

        static void Report(int games, int goodGames)
        {
            var levels = LoadLevels(1, 60);
            var designs = LevelDesigns.All().ToDictionary(d => d.Id);
            var sb = new StringBuilder();
            sb.AppendLine("# «Солнечная долина» — уровни 1–60");
            sb.AppendLine();
            sb.AppendLine("Сгенерировано `dotnet run -c Release --project farm/match3-tools -- report`. Уровни лежат в");
            sb.AppendLine("`farm/unity/SunnyValley/Assets/SunnyValley/Resources/Levels/level_NNN.json`; формы, препятствия и цели");
            sb.AppendLine("задаются в `farm/match3-tools/LevelDesigns.cs`, число ходов подбирает `build` по боту «средний игрок».");
            sb.AppendLine();
            sb.AppendLine($"Доля побед: «средний игрок» — {games} попыток на уровень, «хороший игрок» (Монте-Карло) — {goodGames} попыток.");
            sb.AppendLine("Кривая: 1–20 ≥ 90% у среднего; дальше волны — каждый 10-й уровень трудный (35–50%), следующий за ним лёгкий.");
            sb.AppendLine();
            sb.AppendLine("| # | Название | Новое | Цвета | Ходы | Цели | Средний | Хороший | Ходов в запасе (средний) | Цель кривой |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
            foreach (var level in levels)
            {
                var sw = Stopwatch.StartNew();
                var avg = LevelSimulator.Run(level, BotFactory("average"), games, 1);
                var good = LevelSimulator.Run(level, BotFactory("good"), goodGames, 100001);
                var d = designs[level.Id];
                string goals = string.Join(", ", level.Goals.Select(g => g.Type + (g.Color != null ? " " + g.Color : "") + (g.Count > 0 ? " " + g.Count : " (все)")));
                string target = d.Hard ? "трудный 35–50%" : $"≥ {d.Target * 100:0}%";
                sb.AppendLine($"| {level.Id} | {level.Name} | {level.Intro ?? ""} | {level.Colors} | {level.Moves} | {goals} | {avg.WinRate * 100:0}% | {good.WinRate * 100:0}% | {avg.AvgMovesLeft:0.0} | {target} |");
                Console.WriteLine($"level {level.Id,2}: avg {avg.WinRate * 100,5:0.0}%  good {good.WinRate * 100,5:0.0}%  [{sw.ElapsedMilliseconds / 1000.0:0.0}s]");
            }
            File.WriteAllText(Path.Combine(Root, "farm", "docs", "LEVELS.md.table"), sb.ToString());
        }
    }
}

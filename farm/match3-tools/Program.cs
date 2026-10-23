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
            int from = Arg(args, "--from", 1), to = Arg(args, "--to", LevelDesigns.All().Max(d => d.Id));
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
            public double Scale = 1;
            public Dictionary<int, double> Curve = new Dictionary<int, double>();
        }

        static Dictionary<int, Tuning> LoadTuning() =>
            File.Exists(TuningFile) ? JsonConvert.DeserializeObject<List<Tuning>>(File.ReadAllText(TuningFile)).ToDictionary(t => t.Id) : new Dictionary<int, Tuning>();

        static void SaveTuning(Dictionary<int, Tuning> t) =>
            File.WriteAllText(TuningFile, JsonConvert.SerializeObject(t.Values.OrderBy(x => x.Id).ToList(), Formatting.Indented));

        static Tuning Tune(Design d, int games)
        {
            var t = new Tuning { Id = d.Id, Target = d.Target, Hard = d.Hard, Scale = d.Scale };
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
            // A hard level aims at a band (35–50%): one move less may land closer to the middle of it.
            if (d.Hard && hi > d.MinMoves && Rate(hi) > 0.50 && Rate(hi - 1) >= 0.35) hi--;
            t.Moves = hi;
            t.AvgWinRate = Rate(hi);
            return t;
        }

        static void Build(int from, int to, int games)
        {
            Directory.CreateDirectory(LevelsDir);
            var tuning = LoadTuning();
            foreach (var d in LevelDesigns.All().Where(x => x.Id >= from && x.Id <= to))
            {
                var sw = Stopwatch.StartNew();
                var errs = LevelValidator.Validate(d.ToLevel(25));
                if (errs.Count > 0) { Console.WriteLine("level " + d.Id + " INVALID: " + string.Join("; ", errs)); continue; }
                Tuning t = null;
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    t = Tune(d, games);
                    bool tooEasy = t.Moves == d.MinMoves && t.AvgWinRate > d.Target + (d.Hard ? 0.08 : 0.04);
                    bool tooHard = t.AvgWinRate < d.Target;
                    if (d.Id <= 20 || d.Target >= 0.88) tooEasy = false; // first levels and "easy after hard" may be as easy as they like
                    if (!d.Scalable || (!tooEasy && !tooHard)) break;
                    if (tooEasy && d.Scale >= 3.4) break;
                    d.Scale *= tooEasy ? 1.35 : 0.75;
                }
                tuning[d.Id] = t;
                var level = d.ToLevel(t.Moves);
                File.WriteAllText(Path.Combine(LevelsDir, $"level_{d.Id:000}.json"), LevelLoader.ToJson(level));
                string flag = t.AvgWinRate < d.Target ? "  << below target even at max moves" :
                    (t.Moves == d.MinMoves && t.AvgWinRate > d.Target + 0.06 ? "  << easy even at min moves" : "") +
                    (d.Hard && t.AvgWinRate > 0.5 ? "  << hard level above 50%" : "");
                Console.WriteLine($"level {d.Id,2} {d.Name,-24} moves {t.Moves,2}  avg {t.AvgWinRate * 100,5:0.0}%  target {d.Target * 100:0}%  scale {d.Scale:0.00}  [{sw.ElapsedMilliseconds / 1000.0:0.0}s]{flag}");
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
            var designs = LevelDesigns.All().ToDictionary(d => d.Id);
            var levels = LoadLevels(1, designs.Keys.Max());
            var sb = new StringBuilder();
            sb.AppendLine($"Доля побед: «средний игрок» — {games} попыток на уровень (сиды 1…{games}), «хороший игрок» (Монте-Карло) — {goodGames} попыток.");
            sb.AppendLine();
            sb.AppendLine("| # | Название | Новое | Цвета | Ходы | Цели | Средний | Хороший | Запас ходов¹ | Цель кривой |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
            var avgRates = new Dictionary<int, double>();
            var goodRates = new Dictionary<int, double>();
            foreach (var level in levels)
            {
                var sw = Stopwatch.StartNew();
                var avg = LevelSimulator.Run(level, BotFactory("average"), games, 1);
                var good = LevelSimulator.Run(level, BotFactory("good"), goodGames, 100001);
                avgRates[level.Id] = avg.WinRate;
                goodRates[level.Id] = good.WinRate;
                var d = designs[level.Id];
                string goals = string.Join(", ", level.Goals.Select(g => GoalRu(g)));
                string target = d.Hard ? "трудный 35–50%" : d.Target >= 0.88 && level.Id > 20 ? "лёгкий после трудного" : $"≥ {d.Target * 100:0}%";
                sb.AppendLine($"| {level.Id} | {level.Name} | {IntroRu(level.Intro)} | {level.Colors} | {level.Moves} | {goals} | {avg.WinRate * 100:0}% | {good.WinRate * 100:0}% | {avg.AvgMovesLeft:0.0} | {target} |");
                Console.WriteLine($"level {level.Id,2}: avg {avg.WinRate * 100,5:0.0}%  good {good.WinRate * 100,5:0.0}%  [{sw.ElapsedMilliseconds / 1000.0:0.0}s]");
            }
            sb.AppendLine();
            sb.AppendLine("¹ среднее число оставшихся ходов в выигранных попытках «среднего игрока».");
            sb.AppendLine();
            var early = avgRates.Where(kv => kv.Key <= 20).Select(kv => kv.Value).DefaultIfEmpty(0);
            var hard = avgRates.Where(kv => kv.Key > 20 && kv.Key % 10 == 0).ToList();
            sb.AppendLine("Итог:");
            sb.AppendLine($"- уровни 1–20: у среднего от {early.Min() * 100:0}% до {early.Max() * 100:0}% (цель ≥ 90%);");
            sb.AppendLine("- трудные: " + string.Join(", ", hard.Select(kv => $"{kv.Key} — {kv.Value * 100:0}%")) + " (цель 35–50%);");
            var after = avgRates.Where(kv => kv.Key > 20 && kv.Key % 10 == 1).ToList();
            sb.AppendLine("- лёгкие после трудных: " + string.Join(", ", after.Select(kv => $"{kv.Key} — {kv.Value * 100:0}%")) + ";");
            // The middle of a wave: its levels 2…9 (not the easy one after a hard level, not the hard one).
            var waves = avgRates.Where(kv => kv.Key > 20 && kv.Key % 10 >= 2).GroupBy(kv => (kv.Key - 1) / 10).OrderBy(g => g.Key).ToList();
            sb.AppendLine("- середина волны (уровни …2–…9, среднее у среднего игрока): " +
                string.Join(", ", waves.Select(g => $"{g.Key * 10 + 1}–{g.Key * 10 + 10} — {g.Average(kv => kv.Value) * 100:0}%")) + ";");
            int lastId = avgRates.Keys.DefaultIfEmpty(0).Max();
            var lastWave = waves.LastOrDefault();
            if (lastWave != null)
                sb.AppendLine($"- к уровню {lastId} середина волны — {lastWave.Average(kv => kv.Value) * 100:0}% (цель кривой ~55% к 200);");
            sb.AppendLine($"- «хороший игрок» выигрывает каждый уровень не реже чем в {goodRates.Values.DefaultIfEmpty(0).Min() * 100:0}% попыток.");

            string path = Path.Combine(Root, "farm", "docs", "LEVELS.md");
            string doc = File.Exists(path) ? File.ReadAllText(path) : "<!-- levels:begin -->\n<!-- levels:end -->\n";
            int i0 = doc.IndexOf("<!-- levels:begin -->"), i1 = doc.IndexOf("<!-- levels:end -->");
            if (i0 < 0 || i1 < 0) throw new InvalidOperationException("LEVELS.md needs the levels:begin/end markers");
            doc = doc.Substring(0, i0) + "<!-- levels:begin -->\n" + sb + doc.Substring(i1);
            File.WriteAllText(path, doc);
            Console.WriteLine("written " + path);
        }

        static string GoalRu(GoalData g)
        {
            string n;
            switch (g.Type)
            {
                case "collect": n = "собрать " + ColorRu(g.Color); break;
                case "grass": n = "трава"; break;
                case "crate": n = "ящики"; break;
                case "ice": n = "лёд"; break;
                case "stone": n = "камни"; break;
                case "honey": n = "мёд"; break;
                case "weed": n = "сорняк"; break;
                case "chain": n = "цепи"; break;
                case "chick": n = "цыплята"; break;
                case "crop": n = "урожай грядок"; break;
                case "basket": n = "корзины"; break;
                default: n = g.Type; break;
            }
            return n + (g.Count > 0 ? " " + g.Count : " (все)");
        }

        static string ColorRu(string c)
        {
            switch (c)
            {
                case "tomato": return "помидоры";
                case "corn": return "кукурузу";
                case "apple": return "яблоки";
                case "carrot": return "морковь";
                case "eggplant": return "баклажаны";
                case "blueberry": return "чернику";
                default: return c;
            }
        }

        static string IntroRu(string intro)
        {
            switch (intro)
            {
                case null: return "";
                case "grass": return "трава";
                case "lineH": return "вилы (4 в ряд)";
                case "bee": return "пчела (квадрат)";
                case "crate": return "ящики";
                case "bomb": return "бочка-бомба (L/T)";
                case "rainbow": return "радуга (5 в ряд)";
                case "ice": return "лёд";
                case "chick": return "цыплята";
                case "chain": return "цепи";
                case "bed": return "грядки";
                case "honey": return "мёд";
                case "basket": return "корзины";
                case "stone": return "камни";
                case "wall": return "стенки";
                case "weed": return "сорняк";
                case "portal": return "порталы";
                case "colors6": return "6 культур";
                case "portal3": return "три яруса порталов";
                case "honeyGrass": return "мёд на траве";
                case "weedGrass": return "сорняк на траве";
                case "basketsRow": return "ряд корзин";
                case "chickExit": return "гнёзда цыплят";
                case "bed3": return "тугие грядки (3 удара)";
                case "stoneGrass": return "камни на траве";
                case "chainedSpecials": return "спецфишки в цепях";
                case "mixed4": return "четыре цели";
                case "spawners": return "мало генераторов";
                case "portalWalls": return "порталы и стенки";
                case "honeyWeed": return "мёд и сорняк";
                case "islands": return "острова";
                case "iceSpecial": return "спецфишки во льду";
                case "stoneMaze": return "каменный лабиринт";
                case "honeyBaskets": return "корзины в мёду";
                case "chickBeds": return "цыплята и грядки";
                default: return intro;
            }
        }
    }
}

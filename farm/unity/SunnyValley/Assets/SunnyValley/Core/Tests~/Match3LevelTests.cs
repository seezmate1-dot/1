using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SunnyValley.Core.Match3;
using static SunnyValley.Core.Tests.M3;

namespace SunnyValley.Core.Tests
{
    public class Match3LevelTests
    {
        // ------------------------------------------------------------ shipped levels

        static string LevelsDir()
        {
            var d = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (d != null)
            {
                var p = Path.Combine(d.FullName, "farm", "unity", "SunnyValley", "Assets", "SunnyValley", "Resources", "Levels");
                if (Directory.Exists(p)) return p;
                var unityRel = Path.Combine(d.FullName, "Assets", "SunnyValley", "Resources", "Levels");
                if (Directory.Exists(unityRel)) return unityRel;
                d = d.Parent;
            }
            Assert.Fail("Resources/Levels not found");
            return null;
        }

        static LevelData Load(int id) => LevelLoader.FromJson(File.ReadAllText(Path.Combine(LevelsDir(), $"level_{id:000}.json")));

        /// <summary>How many levels ship (level_001 … level_200); past the last one the game repeats 21… (LevelLibrary).</summary>
        const int LevelCount = 200;

        static IEnumerable<int> AllIds() => Enumerable.Range(1, LevelCount);

        [Test]
        public void TwoHundredLevelsShip_WithoutGaps()
        {
            for (int id = 1; id <= LevelCount; id++)
                Assert.IsTrue(File.Exists(Path.Combine(LevelsDir(), $"level_{id:000}.json")), "level " + id);
            Assert.IsFalse(File.Exists(Path.Combine(LevelsDir(), $"level_{LevelCount + 1:000}.json")), "update LevelCount");
        }

        [TestCaseSource(nameof(AllIds))]
        public void ShippedLevel_Validates(int id)
        {
            var level = Load(id);
            Assert.AreEqual(id, level.Id);
            CollectionAssert.IsEmpty(LevelValidator.Validate(level));
            Assert.That(level.Moves, Is.InRange(15, 40));
            if (id > 60) Assert.That(level.Moves, Is.InRange(20, 35), "levels after 60 give 20–35 moves");
            Assert.IsFalse(string.IsNullOrEmpty(level.Name));
            // Round trip keeps it the same.
            Assert.AreEqual(LevelLoader.ToJson(level), LevelLoader.ToJson(LevelLoader.FromJson(LevelLoader.ToJson(level))));
        }

        [TestCaseSource(nameof(AllIds))]
        public void ShippedLevel_GoodBotWinsMostAttempts(int id)
        {
            var r = LevelSimulator.Run(Load(id), () => new GoodBot(), 8, 500);
            Assert.GreaterOrEqual(r.WinRate, 0.6, r.ToString());
        }

        [Test]
        public void FirstTwentyLevels_AverageBotWinsAtLeast85Percent()
        {
            for (int id = 1; id <= 20; id++)
            {
                var r = LevelSimulator.Run(Load(id), () => new AverageBot(), 40, 7000);
                Assert.GreaterOrEqual(r.WinRate, 0.85, r.ToString());
            }
        }

        [Test]
        public void HardLevels_AreHardForTheAverageBot_EasyAfterHard()
        {
            for (int id = 30; id <= LevelCount; id += 10)
            {
                var r = LevelSimulator.Run(Load(id), () => new AverageBot(), 60, 9000);
                Assert.That(r.WinRate, Is.InRange(0.25, 0.62), r.ToString());
                var next = id < LevelCount ? LevelSimulator.Run(Load(id + 1), () => new AverageBot(), 40, 9000) : null;
                if (next != null) Assert.GreaterOrEqual(next.WinRate, 0.8, next.ToString());
            }
        }

        static double WaveMiddle(int wave) =>
            Enumerable.Range(wave * 10 + 2, 8).Average(id => LevelSimulator.Run(Load(id), () => new AverageBot(), 40, 11000).WinRate);

        [Test]
        public void Curve_MiddleOfTheWaveDriftsDownToAbout55Percent()
        {
            double early = WaveMiddle(2), late = WaveMiddle(19);
            Assert.That(late, Is.InRange(0.45, 0.65), "191–199");
            Assert.Greater(early, late + 0.08, "21–29 vs 191–199");
        }

        [Test]
        public void Levels_NoTwoInARowWithTheSameBoardAndGoals()
        {
            string Key(LevelData l) => LevelLoader.ToJson(new LevelData
            {
                Grid = l.Grid, Layers = l.Layers, Pieces = l.Pieces, Specials = l.Specials, Baskets = l.Baskets, Walls = l.Walls,
                Portals = l.Portals, Spawners = l.Spawners, ChickExits = l.ChickExits, Chicks = l.Chicks, Colors = l.Colors,
                Goals = l.Goals.Select(g => new GoalData { Type = g.Type, Color = g.Color }).ToList(),
            });
            var prev = Key(Load(1));
            for (int id = 2; id <= LevelCount; id++)
            {
                var key = Key(Load(id));
                Assert.AreNotEqual(prev, key, "levels " + (id - 1) + " and " + id);
                prev = key;
            }
        }

        [Test]
        public void Levels_NamesAreUnique_IntrosComeOnce_MostLateLevelsHaveSixCrops()
        {
            var levels = AllIds().Select(Load).ToList();
            CollectionAssert.AllItemsAreUnique(levels.Select(l => l.Name));
            CollectionAssert.AllItemsAreUnique(levels.Where(l => l.Intro != null).Select(l => l.Intro));
            var late = levels.Where(l => l.Id > 60).ToList();
            Assert.GreaterOrEqual(late.Count(l => l.Colors == 6), late.Count * 2 / 3, "6 crops on most levels after 60");
            // A new idea every 5–10 levels until 150: no long stretch without one.
            var intros = levels.Where(l => l.Intro != null && l.Id > 60).Select(l => l.Id).ToList();
            Assert.GreaterOrEqual(intros.Count, 15);
            for (int i = 1; i < intros.Count; i++) Assert.LessOrEqual(intros[i] - intros[i - 1], 10, "levels " + intros[i - 1] + "…" + intros[i]);
        }

        [Test]
        public void GoodBot_BeatsAverageBot()
        {
            var level = Load(30);
            var avg = LevelSimulator.Run(level, () => new AverageBot(), 40, 1);
            var good = LevelSimulator.Run(level, () => new GoodBot(), 40, 1);
            Assert.Greater(good.WinRate, avg.WinRate + 0.2, avg + " / " + good);
        }

        [Test]
        public void Simulator_IsDeterministic()
        {
            var level = Load(25);
            var a = LevelSimulator.Run(level, () => new AverageBot(), 12, 3);
            var b = LevelSimulator.Run(level, () => new AverageBot(), 12, 3, threads: 1);
            Assert.AreEqual(a.Wins, b.Wins);
            Assert.AreEqual(a.AvgMovesLeft, b.AvgMovesLeft);
        }

        [TestCaseSource(nameof(AllIds))]
        public void ShippedLevel_StepLogReproducesTheBoard(int id)
        {
            var g = Match3Game.Create(Load(id), id);
            var mirror = new StepLogMirror(g);
            var bot = new AverageBot();
            var rng = new Rng((ulong)id);
            for (int k = 0; k < 12 && g.Status == GameStatus.Playing; k++)
            {
                var m = bot.Choose(g, rng);
                var r = g.Swap(m.A, m.B);
                AssertAccepted(r);
                mirror.ApplyAll(r.Events);
                CollectionAssert.IsEmpty(mirror.Diff(g), "level " + id + " move " + k);
            }
        }

        // ------------------------------------------------------------ validator

        static LevelData Ok() => new LevelData
        {
            Id = 1, Moves = 20, Colors = 5, Seed = 1,
            Grid = new[] { "######", "######", "######", "######", "######", "######" },
            Goals = new List<GoalData> { Goal("collect", 10, "tomato") },
        };

        static void Bad(LevelData level, string expectedFragment)
        {
            var errors = LevelValidator.Validate(level);
            Assert.IsTrue(errors.Any(e => e.Contains(expectedFragment)), "expected '" + expectedFragment + "' in: " + string.Join(" | ", errors));
        }

        [Test]
        public void Validator_AcceptsAGoodLevel() => CollectionAssert.IsEmpty(LevelValidator.Validate(Ok()));

        [Test]
        public void Validator_Rejects_TooBigOrRagged()
        {
            var l = Ok(); l.Grid = Enumerable.Repeat("##########", 10).ToArray(); Bad(l, "grid must be");
            l = Ok(); l.Grid[2] = "#####"; Bad(l, "different length");
            l = Ok(); l.Grid[2] = "##x###"; Bad(l, "unknown char");
        }

        [Test]
        public void Validator_Rejects_BadNumbers()
        {
            var l = Ok(); l.Moves = 0; Bad(l, "moves");
            l = Ok(); l.Colors = 7; Bad(l, "colors");
            l = Ok(); l.Stars = new[] { 5, 3 }; Bad(l, "stars");
        }

        [Test]
        public void Validator_Rejects_LayerProblems()
        {
            var l = Ok(); l.Grid[0] = ".#####"; l.Layers = Layers(("crate", One(6, 6, 0, 0, '1'))); Bad(l, "on a hole");
            l = Ok(); l.Layers = Layers(("crate", One(6, 6, 1, 1, '4'))); Bad(l, "at most 3");
            l = Ok(); l.Layers = Layers(("crate", One(6, 6, 1, 1, '1')), ("stone", One(6, 6, 1, 1, '1'))); Bad(l, "two blockers");
            l = Ok(); l.Layers = Layers(("crate", One(6, 6, 1, 1, '1')), ("ice", One(6, 6, 1, 1, '1'))); Bad(l, "cannot have an overlay");
            l = Ok(); l.Layers = Layers(("bed", One(6, 6, 1, 1, '2')), ("grass", One(6, 6, 1, 1, '1'))); Bad(l, "no grass under beds");
            l = Ok(); l.Layers = Layers(("lava", One(6, 6, 1, 1, '1'))); Bad(l, "unknown layer");
        }

        [Test]
        public void Validator_Rejects_GoalsThatCannotBeMet()
        {
            var l = Ok(); l.Goals = new List<GoalData> { Goal("crate") }; Bad(l, "no crate on the board");
            l = Ok(); l.Goals = new List<GoalData> { Goal("collect", 10, "blueberry") }; Bad(l, "colour not in play");
            l = Ok(); l.Goals = new List<GoalData> { Goal("chick", 2) }; Bad(l, "no chicks");
            l = Ok(); l.Goals = new List<GoalData> { Goal("crop", 2) }; Bad(l, "no beds");
            l = Ok(); l.Goals = new List<GoalData> { Goal("rocks") }; Bad(l, "Unknown goal");
            l = Ok(); l.Layers = Layers(("crate", One(6, 6, 1, 1, '1'))); l.Goals = new List<GoalData> { Goal("crate", 3) }; Bad(l, "wants 3");
        }

        [Test]
        public void Validator_Rejects_BadPortalsAndUnrefillableCells()
        {
            var l = Ok(); l.Portals = new List<PortalData> { new PortalData { From = new[] { 0, 2 }, To = new[] { 3, 0 } } }; Bad(l, "must have no cell below");
            l = Ok(); l.Spawners = new List<int[]> { new[] { 0, 0 } }; Bad(l, "can never be refilled");
            l = Ok(); l.Walls = new List<WallData> { new WallData { X = 5, Y = 0, Side = "right" } }; Bad(l, "wall");
        }

        [Test]
        public void Validator_Rejects_BadPieces()
        {
            var l = Ok(); l.Pieces = new[] { "Z.....", "......", "......", "......", "......", "......" }; Bad(l, "unknown char");
            l = Ok(); l.Layers = Layers(("crate", One(6, 6, 0, 0, '1'))); l.Goals = new List<GoalData> { Goal("crate") };
            l.Pieces = new[] { "r.....", "......", "......", "......", "......", "......" }; Bad(l, "under a blocker");
            l = Ok(); l.Pieces = new[] { "rrr...", "......", "......", "......", "......", "......" };
            // A preset match is reshuffled away at start, so it is no error; the board must still be playable.
            CollectionAssert.IsEmpty(LevelValidator.Validate(l));
        }

        [Test]
        public void Loader_ReadsTheDocumentedFormat()
        {
            string json = @"{
              ""id"": 5, ""name"": ""Тест"", ""moves"": 20, ""colors"": 4, ""seed"": 3,
              ""grid"": [""#####"", ""#####"", ""#####"", ""#####"", ""#####""],
              ""layers"": { ""grass"": [""....."", ""....."", "".11.."", ""....."", "".....""] },
              ""baskets"": [{ ""x"": 0, ""y"": 0, ""color"": ""corn"", ""count"": 3 }],
              ""goals"": [{ ""type"": ""grass"" }, { ""type"": ""basket"" }],
              ""stars"": [3, 6]
            }";
            var level = LevelLoader.FromJson(json);
            CollectionAssert.IsEmpty(LevelValidator.Validate(level));
            var g = Match3Game.Create(level);
            Assert.AreEqual(2, g.Goals[0].Target);
            Assert.AreEqual(1, g.Goals[1].Target);
            Assert.AreEqual(ObstacleKind.Basket, g.Board.GetBlocker(P(0, 0), out var left));
            Assert.AreEqual(3, left);
            Assert.AreEqual(3, g.StarThreshold2);
        }

        // ------------------------------------------------------------ meta bridge and hints

        [Test]
        public void MetaBridge_ReportsAWin()
        {
            var level = Level(Bg(6, 6, (0, 3, 'r'), (1, 3, 'r'), (2, 2, 'r'), (1, 4, '.')), layers: Layers(("bed", One(6, 6, 1, 4, '1'))),
                goals: new List<GoalData> { Goal("crop", 1) });
            level.BedCrop = "wheat";
            var g = Game(level);
            g.Swap(P(2, 2), P(2, 3));
            var r = Match3MetaBridge.ToLevelResult(g);
            Assert.IsTrue(r.Won);
            Assert.AreEqual(1, r.Level);
            Assert.AreEqual(g.MovesLeft, r.MovesLeft);
            Assert.AreEqual(g.Stars, r.Stars);
            Assert.AreEqual(g.MovesLeft * Match3MetaBridge.CoinsPerMoveLeft, r.CoinsEarned);
            Assert.GreaterOrEqual(r.Crops["wheat"], 1);
        }

        [Test]
        public void MetaBridge_ReportsALoss()
        {
            var g = Game(Level(Bg(6, 6, (0, 3, 'r'), (1, 3, 'r'), (2, 2, 'r')), moves: 1));
            g.Swap(P(2, 2), P(2, 3));
            var r = Match3MetaBridge.ToLevelResult(g);
            Assert.IsFalse(r.Won);
            Assert.AreEqual(0, r.Stars);
            Assert.AreEqual(0, r.CoinsEarned);
        }

        [Test]
        public void Hint_IsAValidMove_AndPrefersTheGoal()
        {
            var g = Match3Game.Create(Load(7), 1);
            var h = Hints.Find(g);
            Assert.IsTrue(h.HasValue);
            Assert.IsTrue(g.IsValidSwap(h.Value.A, h.Value.B));
        }
    }
}

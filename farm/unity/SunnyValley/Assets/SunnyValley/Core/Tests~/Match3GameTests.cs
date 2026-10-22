using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SunnyValley.Core.Match3;
using static SunnyValley.Core.Tests.M3;

namespace SunnyValley.Core.Tests
{
    public class Match3GameTests
    {
        static readonly (int, int, char)[] Std = { (0, 3, 'r'), (1, 3, 'r'), (2, 2, 'r') };

        // ------------------------------------------------------------ goals, win, lose, stars

        [Test]
        public void CollectGoal_Reached_Wins_WithStarsAndAGameEndEvent()
        {
            var level = Level(Bg(6, 6, Std), moves: 20, goals: new List<GoalData> { Goal("collect", 3, "tomato") });
            level.Stars = new[] { 5, 15 };
            var g = Game(level);
            var r = g.Swap(P(2, 2), P(2, 3));
            Assert.AreEqual(GameStatus.Won, g.Status);
            Assert.AreEqual(3, g.Stars, "19 moves left ≥ 15");
            var end = r.OfType<GameEndEvent>().Single();
            Assert.AreEqual(GameStatus.Won, end.Status);
            Assert.AreEqual(3, end.Stars);
            Assert.AreEqual(3, r.OfType<GoalProgressEvent>().Count(e => e.Goal == GoalType.Collect));
            Assert.AreEqual(RejectReason.GameOver, g.Swap(P(0, 0), P(1, 0)).OfType<SwapRejectedEvent>().Single().Reason);
        }

        [Test]
        public void Stars_ByMovesLeft()
        {
            var level = Level(Bg(6, 6, Std), moves: 20);
            level.Stars = new[] { 3, 8 };
            var g = Game(level);
            Assert.AreEqual(1, g.StarsFor(0));
            Assert.AreEqual(1, g.StarsFor(2));
            Assert.AreEqual(2, g.StarsFor(3));
            Assert.AreEqual(2, g.StarsFor(7));
            Assert.AreEqual(3, g.StarsFor(8));
        }

        [Test]
        public void DefaultStarThresholds_Are10And25PercentOfMoves()
        {
            var g = Game(Level(Bg(6, 6, Std), moves: 20));
            Assert.AreEqual(2, g.StarThreshold2);
            Assert.AreEqual(5, g.StarThreshold3);
        }

        [Test]
        public void OutOfMoves_Loses_ThenPlusFiveMovesContinues()
        {
            var g = Game(Level(Bg(6, 6, Std), moves: 1));
            var r = g.Swap(P(2, 2), P(2, 3));
            Assert.AreEqual(GameStatus.Lost, g.Status);
            Assert.AreEqual(GameStatus.Lost, r.OfType<GameEndEvent>().Single().Status);
            Assert.IsFalse(g.UseBooster(BoosterKind.Shovel, P(0, 0)).Accepted, "no boosters after a loss");
            var c = g.ContinueWithExtraMoves();
            Assert.IsTrue(c.Accepted);
            Assert.AreEqual(GameStatus.Playing, g.Status);
            Assert.AreEqual(5, g.MovesLeft);
            Assert.AreEqual(1, g.Continues);
            Assert.AreEqual(5, c.OfType<MovesChangedEvent>().Single().MovesLeft);
            Assert.IsFalse(g.ContinueWithExtraMoves().Accepted, "only after a loss");
        }

        [Test]
        public void MixedGoals_AllMustBeDone()
        {
            var level = Level(Bg(6, 6, Std.Concat(new[] { (1, 4, '.') }).ToArray()), layers: Layers(("crate", One(6, 6, 1, 4, '1'))),
                goals: new List<GoalData> { Goal("crate"), Goal("collect", 50, "corn") });
            var g = Game(level);
            g.Swap(P(2, 2), P(2, 3));
            Assert.IsTrue(g.Goals[0].Done);
            Assert.AreEqual(GameStatus.Playing, g.Status);
        }

        // ------------------------------------------------------------ determinism

        static string PlayLog(long seed, int moves)
        {
            var g = Match3Game.Create(Fixtures.Rich(), seed);
            var rnd = new System.Random(1234);
            var sb = new System.Text.StringBuilder();
            for (int k = 0; k < moves && g.Status == GameStatus.Playing; k++)
            {
                var list = g.GetValidMoves();
                var m = list[rnd.Next(list.Count)];
                sb.Append(g.Swap(m.A, m.B).ToJson()).Append('\n');
            }
            sb.Append(g.Board.Dump());
            return sb.ToString();
        }

        [Test]
        public void SameSeedAndMoves_GiveTheSameLogAndBoard()
        {
            Assert.AreEqual(PlayLog(5, 20), PlayLog(5, 20));
        }

        [Test]
        public void DifferentSeeds_GiveDifferentGames()
        {
            Assert.AreNotEqual(PlayLog(5, 5), PlayLog(6, 5));
        }

        [Test]
        public void Clone_PlaysExactlyLikeTheOriginal()
        {
            var a = Match3Game.Create(Fixtures.Rich(), 3);
            var rnd = new System.Random(3);
            for (int k = 0; k < 5; k++) { var l = a.GetValidMoves(); var m = l[rnd.Next(l.Count)]; a.Swap(m.A, m.B); }
            var b = a.Clone(withLog: true);
            for (int k = 0; k < 10 && a.Status == GameStatus.Playing; k++)
            {
                var l = a.GetValidMoves();
                var m = l[rnd.Next(l.Count)];
                Assert.AreEqual(a.Swap(m.A, m.B).ToJson(), b.Swap(m.A, m.B).ToJson());
            }
            Assert.AreEqual(a.Board.Dump(), b.Board.Dump());
        }

        [Test]
        public void LoggingOff_SameOutcome()
        {
            var a = Match3Game.Create(Fixtures.Rich(), 8);
            var b = Match3Game.Create(Fixtures.Rich(), 8);
            b.LogEnabled = false;
            var rnd = new System.Random(8);
            for (int k = 0; k < 15 && a.Status == GameStatus.Playing; k++)
            {
                var l = a.GetValidMoves();
                var m = l[rnd.Next(l.Count)];
                a.Swap(m.A, m.B);
                Assert.IsEmpty(b.Swap(m.A, m.B).Events);
            }
            Assert.AreEqual(a.Board.Dump(), b.Board.Dump());
            Assert.AreEqual(a.Rng.State, b.Rng.State);
        }

        // ------------------------------------------------------------ the step log reproduces the board

        [Test]
        public void StepLog_ReplayedOnAMirror_ReproducesTheBoardExactly([Range(1, 25)] int seed)
        {
            var g = Match3Game.Create(Fixtures.Rich(), seed);
            var mirror = new StepLogMirror(g);
            var rnd = new System.Random(seed);
            for (int k = 0; k < 40 && g.Status == GameStatus.Playing; k++)
            {
                MoveResult r;
                int roll = rnd.Next(20);
                if (roll == 0) r = g.UseBooster(BoosterKind.Shovel, new Pos(rnd.Next(9), rnd.Next(9)));
                else if (roll == 1) r = g.UseBooster(BoosterKind.Rake, new Pos(0, rnd.Next(9)));
                else if (roll == 2) r = g.UseBooster(BoosterKind.WateringCan, new Pos(rnd.Next(9), 0));
                else if (roll == 3) r = g.UseBooster(BoosterKind.Shuffle, Pos.None);
                else
                {
                    var l = g.GetValidMoves();
                    var m = l[rnd.Next(l.Count)];
                    r = g.Swap(m.A, m.B);
                    AssertAccepted(r);
                }
                mirror.ApplyAll(r.Events);
                var diff = mirror.Diff(g);
                CollectionAssert.IsEmpty(diff, "after action " + k + ":\n" + string.Join("\n", diff.Take(10)));
            }
        }

        [Test]
        public void StepLog_SerializesToJsonWithTypeNames()
        {
            var g = Game(Level(Bg(6, 6, Std)));
            var json = g.Swap(P(2, 2), P(2, 3)).ToJson();
            StringAssert.Contains("\"Type\":\"Swap\"", json);
            StringAssert.Contains("\"Type\":\"Match\"", json);
            StringAssert.Contains("\"Type\":\"Fall\"", json);
            StringAssert.Contains("\"Type\":\"Spawn\"", json);
        }

        [Test]
        public void StepLog_OrderWithinAMove()
        {
            var g = Game(Level(Bg(6, 6, Std)));
            var ev = g.Swap(P(2, 2), P(2, 3)).Events;
            int Idx(StepType t) => ev.FindIndex(e => e.Type == t);
            Assert.Less(Idx(StepType.MovesChanged), Idx(StepType.Swap));
            Assert.Less(Idx(StepType.Swap), Idx(StepType.Match));
            Assert.Less(Idx(StepType.Match), Idx(StepType.PieceCleared));
            Assert.Less(Idx(StepType.PieceCleared), Idx(StepType.Spawn));
            Assert.Less(Idx(StepType.Spawn), Idx(StepType.Fall));
            var match = ev.OfType<MatchEvent>().First();
            Assert.IsTrue(ev.OfType<PieceClearedEvent>().Where(c => c.Wave == 0).All(c => c.CauseId == match.GroupId));
        }

        // ------------------------------------------------------------ reshuffle

        [Test]
        public void DeadStartBoard_IsReshuffled_KeepingObstaclesAndColours()
        {
            // The o/p/b background has no move at all; crates stay put.
            var level = Level(Bg(6, 6, (1, 4, '.')), layers: Layers(("crate", One(6, 6, 1, 4, '2'))), goals: new List<GoalData> { Goal("crate") });
            var g = Game(level);
            Assert.AreEqual(1, g.Reshuffles);
            Assert.IsTrue(g.HasValidMove());
            Assert.AreEqual(0, g.FindMatches().Count);
            Assert.AreEqual(ObstacleKind.Crate, g.Board.GetBlocker(P(1, 4), out var layers));
            Assert.AreEqual(2, layers);
            var colours = g.Board.Pieces.Where(p => p.IsTile).GroupBy(p => p.Color).ToDictionary(x => x.Key, x => x.Count());
            Assert.AreEqual(3, colours.Count, "still only o/p/b — a permutation, not new colours");
        }

        [Test]
        public void ShuffleBooster_MovesPiecesOnly_SpendsNoMove()
        {
            var level = Level(Bg(6, 6, Std.Concat(new[] { (1, 4, '.') }).ToArray()), layers: Layers(("crate", One(6, 6, 1, 4, '2'))), goals: new List<GoalData> { Goal("crate") });
            var g = Game(level);
            var before = g.Board.Pieces.Where(p => p.IsTile).Select(p => p.Id).OrderBy(x => x).ToList();
            var r = g.UseBooster(BoosterKind.Shuffle, Pos.None);
            Assert.IsTrue(r.Accepted);
            var sh = r.OfType<ShuffleEvent>().Single();
            Assert.IsTrue(sh.ByBooster);
            Assert.IsTrue(sh.Moves.Any(m => m.From != m.To));
            Assert.AreEqual(20, g.MovesLeft);
            CollectionAssert.AreEqual(before, g.Board.Pieces.Where(p => p.IsTile).Select(p => p.Id).OrderBy(x => x).ToList());
            Assert.AreEqual(ObstacleKind.Crate, g.Board.GetBlocker(P(1, 4), out _));
            Assert.IsTrue(g.HasValidMove());
        }

        [Test]
        public void NoMovesAfterAMove_TriggersAReshuffleEvent()
        {
            // Play many random games on a 4-colour small board; whenever a reshuffle happens it is logged and leaves a move.
            int seen = 0;
            for (int seed = 1; seed <= 60 && seen == 0; seed++)
            {
                var level = new LevelData
                {
                    Id = 1, Moves = 30, Colors = 4, Seed = seed, Grid = new[] { "#####", "#####", "#####", "#####" },
                    Goals = new List<GoalData> { Goal("collect", 999, "tomato") },
                };
                var g = Match3Game.Create(level, seed);
                var rnd = new System.Random(seed);
                while (g.Status == GameStatus.Playing)
                {
                    var l = g.GetValidMoves();
                    Assert.IsNotEmpty(l);
                    var m = l[rnd.Next(l.Count)];
                    var r = g.Swap(m.A, m.B);
                    var sh = r.OfType<ShuffleEvent>().FirstOrDefault();
                    if (sh != null) { seen++; Assert.IsFalse(sh.ByBooster); }
                }
            }
            Assert.Greater(seen, 0, "a reshuffle should happen on tiny boards");
        }

        // ------------------------------------------------------------ boosters

        [Test]
        public void Shovel_HitsOneCell_EvenAStone_NoMoveSpent()
        {
            var level = Level(Bg(6, 6, Std.Concat(new[] { (4, 4, '.') }).ToArray()), layers: Layers(("stone", One(6, 6, 4, 4, '1'))), goals: new List<GoalData> { Goal("stone") });
            var g = Game(level);
            var r = g.UseBooster(BoosterKind.Shovel, P(4, 4));
            Assert.IsTrue(r.Accepted);
            Assert.AreEqual(HitSource.Booster, r.OfType<ObstacleHitEvent>().Single().Source);
            Assert.AreEqual(GameStatus.Won, g.Status);
            Assert.AreEqual(20, g.MovesLeft);
        }

        [Test]
        public void Rake_ClearsARow_WateringCan_AColumn()
        {
            var g = Game(Level(Bg(6, 6, Std)));
            var r = g.UseBooster(BoosterKind.Rake, P(0, 5));
            var b = r.OfType<BoosterEvent>().Single();
            CollectionAssert.AreEquivalent(Rect(0, 5, 5, 5), b.Cells);
            Assert.AreEqual(6, r.OfType<PieceClearedEvent>().Count(e => e.CauseId == b.ActivationId));
            var r2 = g.UseBooster(BoosterKind.WateringCan, P(4, 0));
            CollectionAssert.AreEquivalent(Rect(4, 0, 4, 5), r2.OfType<BoosterEvent>().Single().Cells);
            Assert.AreEqual(20, g.MovesLeft);
        }

        [Test]
        public void ShovelOnASpecial_FiresIt()
        {
            var g = Game(Level(Bg(9, 9)).With((4, 4, "lineH", "tomato")));
            var r = g.UseBooster(BoosterKind.Shovel, P(4, 4));
            Assert.AreEqual(SpecialKind.LineH, r.OfType<ActivationEvent>().First().Special);
        }

        [Test]
        public void PreLevelBoosters_ArePlacedOnTheBoard()
        {
            var level = Level(Bg(9, 9, (0, 3, 'r'), (1, 3, 'r'), (2, 2, 'r')));
            var g = Match3Game.Create(level, 1, new[] { BoosterKind.PreLine, BoosterKind.PreBomb, BoosterKind.PreRainbow });
            Assert.AreEqual(3, g.InitialEvents.Count);
            var specials = g.Board.Pieces.Where(p => p.IsSpecial).Select(p => p.Special).ToList();
            Assert.AreEqual(1, specials.Count(s => s == SpecialKind.LineH || s == SpecialKind.LineV));
            Assert.AreEqual(1, specials.Count(s => s == SpecialKind.Bomb));
            Assert.AreEqual(1, specials.Count(s => s == SpecialKind.Rainbow));
        }
    }
}

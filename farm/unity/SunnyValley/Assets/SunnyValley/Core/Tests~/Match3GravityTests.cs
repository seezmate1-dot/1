using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SunnyValley.Core.Match3;
using static SunnyValley.Core.Tests.M3;

namespace SunnyValley.Core.Tests
{
    public class Match3GravityTests
    {
        static readonly (int, int, char)[] Std = { (0, 3, 'r'), (1, 3, 'r'), (2, 2, 'r') };

        static MoveResult StdMove(LevelData level, out Match3Game g)
        {
            g = Game(level);
            var r = g.Swap(P(2, 2), P(2, 3));
            AssertAccepted(r);
            return r;
        }

        [Test]
        public void PiecesFallStraightDown_AndSpawnersRefillTheTop()
        {
            var level = Level(Bg(6, 6, Std));
            var g0 = Game(level);
            int above = g0.Board.GetPiece(P(0, 2)).Id;
            var r = StdMove(level, out _);
            var fall = r.OfType<FallEvent>().First(f => f.PieceId == above);
            Assert.AreEqual(P(0, 2), fall.From);
            Assert.AreEqual(P(0, 3), fall.To);
            Assert.IsTrue(fall.Segments.All(s => s.Kind == FallKind.Down));
            var spawns = r.OfType<SpawnEvent>().Where(s => s.Wave == 0).ToList();
            Assert.AreEqual(3, spawns.Count);
            CollectionAssert.AreEquivalent(new[] { P(0, 0), P(1, 0), P(2, 0) }, spawns.Select(s => s.Cell));
        }

        [Test]
        public void ColumnMovesTogether_OneCellPerTick()
        {
            var r = StdMove(Level(Bg(6, 6, Std)), out _);
            // Column 0: (0,0),(0,1),(0,2) all move down by one in tick 0.
            var col0 = r.OfType<FallEvent>().Where(f => f.Wave == 0 && f.From.X == 0).ToList();
            Assert.AreEqual(3, col0.Count);
            Assert.IsTrue(col0.All(f => f.Segments.Count == 1 && f.Segments[0].Tick == 0));
        }

        [Test]
        public void Crate_BlocksTheFall_CellBelowIsFilledDiagonally()
        {
            var level = Level(Bg(6, 6, Std.Concat(new[] { (1, 1, '.') }).ToArray()), layers: Layers(("crate", One(6, 6, 1, 1, '3'))),
                goals: new List<GoalData> { Goal("crate") });
            var r = StdMove(level, out var g);
            Assert.IsTrue(r.OfType<FallEvent>().Any(f => f.Wave == 0 && f.Segments.Any(s => s.Kind == FallKind.Diagonal && s.To == P(1, 2))));
            for (int y = 2; y < 6; y++) Assert.IsFalse(g.Board.GetPiece(P(1, y)).IsEmpty);
        }

        [Test]
        public void Hole_CellUnderItIsFilledDiagonally()
        {
            var board = Bg(6, 6, Std);
            board[1] = board[1].Substring(0, 1) + " " + board[1].Substring(2);
            var r = StdMove(Level(board), out var g);
            Assert.IsTrue(r.OfType<FallEvent>().Any(f => f.Segments.Any(s => s.Kind == FallKind.Diagonal && s.To == P(1, 2))));
            Assert.IsFalse(g.Board.GetPiece(P(1, 2)).IsEmpty);
            Assert.IsFalse(g.Topo.IsCell(P(1, 1)));
        }

        [Test]
        public void Wall_StopsTheFall()
        {
            var level = Level(Bg(6, 6, Std));
            level.Walls = new List<WallData> { new WallData { X = 1, Y = 2, Side = "down" } };
            var g0 = Game(level);
            int held = g0.Board.GetPiece(P(1, 2)).Id;
            var r = StdMove(level, out var g);
            Assert.IsFalse(r.OfType<FallEvent>().Any(f => f.PieceId == held));
            Assert.IsTrue(r.OfType<FallEvent>().Any(f => f.Segments.Any(s => s.Kind == FallKind.Diagonal && s.To == P(1, 3))));
        }

        [Test]
        public void Wall_BlocksSwaps()
        {
            var level = Level(Bg(6, 6, Std));
            level.Walls = new List<WallData> { new WallData { X = 2, Y = 2, Side = "down" } };
            var g = Game(level);
            Assert.AreEqual(RejectReason.Wall, g.Swap(P(2, 2), P(2, 3)).OfType<SwapRejectedEvent>().Single().Reason);
        }

        static LevelData PortalLevel()
        {
            // Top-left 3×3 block feeds the bottom-right 3×3 block through three portals.
            var board = new[]
            {
                "opb   ",
                "pbo   ",
                "bop   ",
                "   orp",
                "   ryb",
                "   rbo",
            };
            var level = Level(board);
            level.Portals = new List<PortalData>
            {
                new PortalData { From = new[] { 0, 2 }, To = new[] { 3, 3 } },
                new PortalData { From = new[] { 1, 2 }, To = new[] { 4, 3 } },
                new PortalData { From = new[] { 2, 2 }, To = new[] { 5, 3 } },
            };
            return level;
        }

        [Test]
        public void Portal_LevelIsValid_AndPortalExitsAreNotSpawners()
        {
            var level = PortalLevel();
            CollectionAssert.IsEmpty(LevelValidator.Validate(level));
            var t = LevelLoader.BuildTopology(level);
            Assert.IsFalse(t.Spawner[t.Index(3, 3)]);
            Assert.IsTrue(t.Spawner[t.Index(0, 0)]);
            Assert.AreEqual(t.Index(3, 3), t.Below[t.Index(0, 2)]);
        }

        [Test]
        public void Portal_PiecesFallThroughIt()
        {
            // r at (3,4),(3,5) and r at (4,3) swapped to (3,3): column 3 clears, refilled through the portal from (0,2).
            var level = PortalLevel();
            var g = Game(level);
            int top = g.Board.GetPiece(P(0, 2)).Id;
            var r = g.Swap(P(4, 3), P(3, 3));
            AssertAccepted(r);
            var fall = r.OfType<FallEvent>().First(f => f.PieceId == top);
            Assert.AreEqual(FallKind.Portal, fall.Segments[0].Kind);
            Assert.AreEqual(P(3, 3), fall.Segments[0].To);
            for (int i = 0; i < g.Topo.Count; i++)
                if (g.Topo.Exists[i]) Assert.IsFalse(g.Board.Pieces[i].IsEmpty);
        }

        [Test]
        public void Portal_Loop_IsInvalid()
        {
            var level = PortalLevel();
            level.Portals.Add(new PortalData { From = new[] { 5, 5 }, To = new[] { 2, 0 } });
            Assert.IsNotEmpty(LevelValidator.Validate(level));
        }

        [Test]
        public void BoardIsFullAfterEveryMove_WithHolesCratesAndWalls([Range(1, 10)] int seed)
        {
            // (weed and honey left out: when they spread they legitimately wall cells off until cleared)
            var level = Fixtures.Rich();
            level.Layers.Remove("weed");
            level.Layers.Remove("honey");
            var g = Match3Game.Create(level, seed);
            var rnd = new System.Random(seed);
            for (int k = 0; k < 15 && g.Status == GameStatus.Playing; k++)
            {
                var moves = g.GetValidMoves();
                var m = moves[rnd.Next(moves.Count)];
                AssertAccepted(g.Swap(m.A, m.B));
                for (int i = 0; i < g.Topo.Count; i++)
                    if (g.Board.IsOpen(i)) Assert.IsFalse(g.Board.Pieces[i].IsEmpty, "empty " + g.Topo.PosOf(i) + "\n" + g.Board.Dump());
                Assert.AreEqual(0, g.FindMatches().Count, "board must be still after a move");
            }
        }

        [Test]
        public void CustomSpawnersAndChickExits()
        {
            // A hole at (3,1); an extra spawner right under it at (3,2). Chicks leave at (4,5) only.
            var board = Bg(6, 6, Std.Concat(new[] { (4, 2, 'C') }).ToArray());
            board[1] = board[1].Substring(0, 3) + " " + board[1].Substring(4);
            var level = Level(board, goals: new List<GoalData> { Goal("chick", 1) });
            level.Spawners = new List<int[]> { new[] { 0, 0 } };
            Assert.IsTrue(LevelValidator.Validate(level).Any(e => e.Contains("can never be refilled")), "top cells without a spawner");
            level.Spawners = Enumerable.Range(0, 6).Select(x => new[] { x, 0 }).Append(new[] { 3, 2 }).ToList();
            level.ChickExits = new List<int[]> { new[] { 4, 5 } };
            CollectionAssert.IsEmpty(LevelValidator.Validate(level));
            var t = LevelLoader.BuildTopology(level);
            Assert.IsTrue(t.Spawner[t.Index(3, 2)]);
            Assert.IsTrue(t.ChickExit[t.Index(4, 5)]);
            Assert.IsFalse(t.ChickExit[t.Index(0, 5)]);
            var r = StdMove(level, out var g);
            Assert.IsTrue(r.OfType<SpawnEvent>().All(e => t.Spawner[t.Index(e.Cell)]));
            for (int i = 0; i < g.Topo.Count; i++) if (g.Board.IsOpen(i)) Assert.IsFalse(g.Board.Pieces[i].IsEmpty);
        }

        [Test]
        public void PosSerializesAsXY()
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(P(3, 4));
            Assert.AreEqual("{\"X\":3,\"Y\":4}", json);
            Assert.AreEqual(P(3, 4), Newtonsoft.Json.JsonConvert.DeserializeObject<Pos>(json));
        }
    }
}

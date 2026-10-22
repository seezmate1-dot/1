using System.Linq;
using NUnit.Framework;
using SunnyValley.Core.Match3;
using static SunnyValley.Core.Tests.M3;

namespace SunnyValley.Core.Tests
{
    public class Match3MatchingTests
    {
        static MoveResult Play(string[] board, Pos a, Pos b, out Match3Game g)
        {
            g = Game(Level(board));
            var r = g.Swap(a, b);
            AssertAccepted(r);
            return r;
        }

        static void AssertGroup(MoveResult r, Pos cell, MatchShape shape, SpecialKind creates, int cells, Pos? specialAt = null)
        {
            var m = GroupAt(r, cell.X, cell.Y);
            Assert.IsNotNull(m, "no match at " + cell);
            Assert.AreEqual(shape, m.Shape);
            Assert.AreEqual(creates, m.Creates);
            Assert.AreEqual(cells, m.Cells.Count);
            Assert.AreEqual(TileColor.Tomato, m.Color);
            if (specialAt != null)
            {
                Assert.AreEqual(specialAt.Value, m.SpecialCell);
                var created = r.OfType<SpecialCreatedEvent>().Single(e => e.CauseId == m.GroupId);
                Assert.AreEqual(creates, created.Special);
                Assert.AreEqual(specialAt.Value, created.Cell);
            }
        }

        [Test]
        public void HorizontalThree_ClearsThree_NoSpecial()
        {
            var r = Play(Bg(6, 6, (0, 3, 'r'), (1, 3, 'r'), (2, 2, 'r')), P(2, 2), P(2, 3), out _);
            AssertGroup(r, P(0, 3), MatchShape.Three, SpecialKind.None, 3);
            Assert.IsFalse(r.OfType<SpecialCreatedEvent>().Any(e => e.Wave == 0));
        }

        [Test]
        public void VerticalThree()
        {
            var r = Play(Bg(6, 6, (2, 0, 'r'), (2, 1, 'r'), (1, 2, 'r')), P(1, 2), P(2, 2), out _);
            AssertGroup(r, P(2, 0), MatchShape.Three, SpecialKind.None, 3);
        }

        [Test]
        public void HorizontalFour_MakesHorizontalPitchforkWhereThePieceWasMoved()
        {
            var r = Play(Bg(6, 6, (0, 3, 'r'), (1, 3, 'r'), (3, 3, 'r'), (2, 2, 'r')), P(2, 2), P(2, 3), out _);
            AssertGroup(r, P(0, 3), MatchShape.Four, SpecialKind.LineH, 4, P(2, 3));
        }

        [Test]
        public void VerticalFour_MakesVerticalPitchfork()
        {
            var r = Play(Bg(6, 6, (2, 0, 'r'), (2, 1, 'r'), (2, 3, 'r'), (1, 2, 'r')), P(1, 2), P(2, 2), out _);
            AssertGroup(r, P(2, 0), MatchShape.Four, SpecialKind.LineV, 4, P(2, 2));
        }

        [Test]
        public void Five_MakesRainbow()
        {
            var r = Play(Bg(6, 6, (0, 3, 'r'), (1, 3, 'r'), (3, 3, 'r'), (4, 3, 'r'), (2, 2, 'r')), P(2, 2), P(2, 3), out var g);
            AssertGroup(r, P(0, 3), MatchShape.Five, SpecialKind.Rainbow, 5, P(2, 3));
            var created = r.OfType<SpecialCreatedEvent>().First();
            Assert.AreEqual(TileColor.Tomato, created.Color, "event keeps the colour the rainbow was made of");
        }

        [Test]
        public void LShape_MakesBombAtTheCorner()
        {
            var r = Play(Bg(6, 7, (0, 3, 'r'), (1, 3, 'r'), (2, 4, 'r'), (2, 5, 'r'), (2, 2, 'r')), P(2, 2), P(2, 3), out _);
            AssertGroup(r, P(0, 3), MatchShape.LT, SpecialKind.Bomb, 5, P(2, 3));
        }

        [Test]
        public void TShape_MakesBomb()
        {
            var r = Play(Bg(6, 7, (0, 3, 'r'), (2, 3, 'r'), (1, 4, 'r'), (1, 5, 'r'), (1, 2, 'r')), P(1, 2), P(1, 3), out _);
            AssertGroup(r, P(0, 3), MatchShape.LT, SpecialKind.Bomb, 5, P(1, 3));
        }

        [Test]
        public void Square_MakesBee()
        {
            var r = Play(Bg(6, 6, (0, 3, 'r'), (1, 3, 'r'), (0, 4, 'r'), (2, 4, 'r')), P(2, 4), P(1, 4), out _);
            AssertGroup(r, P(0, 3), MatchShape.Square, SpecialKind.Bee, 4, P(1, 4));
        }

        [Test]
        public void SquarePlusLine_IsStillABee_AndClearsAllFive()
        {
            var r = Play(Bg(6, 6, (0, 3, 'r'), (2, 3, 'r'), (0, 4, 'r'), (1, 4, 'r'), (1, 2, 'r')), P(1, 2), P(1, 3), out _);
            AssertGroup(r, P(0, 3), MatchShape.Square, SpecialKind.Bee, 5, P(1, 3));
        }

        [Test]
        public void Priority_LTBeatsFour()
        {
            var r = Play(Bg(6, 7, (0, 3, 'r'), (2, 3, 'r'), (3, 3, 'r'), (1, 4, 'r'), (1, 5, 'r'), (1, 2, 'r')), P(1, 2), P(1, 3), out _);
            AssertGroup(r, P(0, 3), MatchShape.LT, SpecialKind.Bomb, 6, P(1, 3));
        }

        [Test]
        public void Priority_FiveBeatsLT()
        {
            var r = Play(Bg(6, 7, (0, 3, 'r'), (1, 3, 'r'), (3, 3, 'r'), (4, 3, 'r'), (2, 4, 'r'), (2, 5, 'r'), (2, 2, 'r')), P(2, 2), P(2, 3), out _);
            AssertGroup(r, P(0, 3), MatchShape.Five, SpecialKind.Rainbow, 7, P(2, 3));
        }

        [Test]
        public void Priority_FourBeatsSquare()
        {
            // r r r r on row 3 after the swap, with (0,4),(1,4) under it forming squares too.
            var r = Play(Bg(6, 6, (0, 3, 'r'), (1, 3, 'r'), (3, 3, 'r'), (0, 4, 'r'), (1, 4, 'r'), (2, 2, 'r')), P(2, 2), P(2, 3), out _);
            AssertGroup(r, P(0, 3), MatchShape.Four, SpecialKind.LineH, 6, P(2, 3));
        }

        [Test]
        public void SwapCreatingMatchesForBothPieces()
        {
            // Column 1: r at (1,0),(1,1), y at (1,2); column 2: y at (2,0),(2,1), r at (2,2). Swapping (1,2)<->(2,2)
            // completes both columns.
            var board = Bg(6, 6, (1, 0, 'r'), (1, 1, 'r'), (1, 2, 'y'), (2, 0, 'y'), (2, 1, 'y'), (2, 2, 'r'));
            var r = Play(board, P(1, 2), P(2, 2), out _);
            var a = GroupAt(r, 1, 0);
            var b = GroupAt(r, 2, 0);
            Assert.IsNotNull(a);
            Assert.IsNotNull(b);
            Assert.AreEqual(TileColor.Tomato, a.Color);
            Assert.AreEqual(TileColor.Corn, b.Color);
        }

        [Test]
        public void InvalidSwap_RevertsAndSpendsNoMove()
        {
            var g = Game(Level(Bg(6, 6, (0, 3, 'r'))));
            string before = g.Board.Dump();
            var r = g.Swap(P(0, 0), P(1, 0));
            Assert.IsFalse(r.Accepted);
            Assert.AreEqual(20, g.MovesLeft);
            Assert.AreEqual(before, g.Board.Dump());
            var ev = r.OfType<SwapRejectedEvent>().Single();
            Assert.AreEqual(RejectReason.NoMatch, ev.Reason);
        }

        [Test]
        public void NonAdjacentAndHoleSwapsAreRejected()
        {
            var board = Bg(6, 6, (0, 3, 'r'));
            board[0] = " " + board[0].Substring(1);
            var g = Game(Level(board));
            Assert.AreEqual(RejectReason.NotAdjacent, g.Swap(P(1, 1), P(2, 2)).OfType<SwapRejectedEvent>().Single().Reason);
            Assert.AreEqual(RejectReason.NotAdjacent, g.Swap(P(1, 0), P(0, 0)).OfType<SwapRejectedEvent>().Single().Reason);
            Assert.AreEqual(20, g.MovesLeft);
        }

        [Test]
        public void ValidMoveSpendsAMove()
        {
            Play(Bg(6, 6, (0, 3, 'r'), (1, 3, 'r'), (2, 2, 'r')), P(2, 2), P(2, 3), out var g);
            Assert.AreEqual(19, g.MovesLeft);
            Assert.AreEqual(1, g.MovesUsed);
        }

        [Test]
        public void ValidMovesListMatchesIsValidSwap()
        {
            var g = Game(Level(Bg(6, 6, (0, 3, 'r'), (1, 3, 'r'), (2, 2, 'r'))));
            var moves = g.GetValidMoves();
            Assert.IsTrue(moves.Any(m => m.A == P(2, 2) && m.B == P(2, 3)));
            foreach (var m in moves) Assert.IsTrue(g.IsValidSwap(m.A, m.B), m.ToString());
            Assert.IsFalse(g.IsValidSwap(P(5, 5), P(4, 5)) && !moves.Any(m => (m.A == P(4, 5) && m.B == P(5, 5))));
        }

        [Test]
        public void RandomStartBoards_HaveNoMatchAndAMove([Range(1, 40)] int seed)
        {
            var level = new LevelData
            {
                Id = 1, Moves = 20, Colors = 3 + seed % 4, Seed = seed,
                Grid = new[] { "#########", "#########", "#########", "###...###", "#########", "#########", "#########", "#########", "#########" },
                Goals = new System.Collections.Generic.List<GoalData> { Goal("collect", 10, "tomato") },
            };
            var g = Match3Game.Create(level, seed);
            Assert.AreEqual(0, g.FindMatches().Count, g.Board.Dump());
            Assert.IsTrue(g.HasValidMove());
            for (int i = 0; i < g.Topo.Count; i++)
                if (g.Topo.Exists[i]) Assert.IsFalse(g.Board.Pieces[i].IsEmpty);
        }
    }
}

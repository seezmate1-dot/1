using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SunnyValley.Core.Match3;
using static SunnyValley.Core.Tests.M3;

namespace SunnyValley.Core.Tests
{
    public class Match3SpecialTests
    {
        static MoveResult SwapOn(LevelData level, Pos a, Pos b, out Match3Game g)
        {
            g = Game(level);
            var r = g.Swap(a, b);
            AssertAccepted(r);
            return r;
        }

        static HashSet<Pos> Cells(ActivationEvent e) => new HashSet<Pos>(e.Cells);

        static ActivationEvent FirstActivation(MoveResult r) => r.OfType<ActivationEvent>().First();

        // ------------------------------------------------------------ single specials, activated by swapping

        [Test]
        public void Pitchfork_Horizontal_ClearsItsRow_WhenSwapped()
        {
            var r = SwapOn(Level(Bg(9, 9)).With((4, 4, "lineH", "tomato")), P(4, 4), P(5, 4), out _);
            var a = FirstActivation(r);
            Assert.AreEqual(SpecialKind.LineH, a.Special);
            Assert.AreEqual(P(5, 4), a.Cell, "fires where it was moved to");
            CollectionAssert.AreEquivalent(Rect(0, 4, 8, 4), Cells(a));
            Assert.IsTrue(r.OfType<PieceClearedEvent>().Any(e => e.CauseId == a.ActivationId && e.Cell == P(0, 4)));
            Assert.IsTrue(r.OfType<PieceClearedEvent>().Any(e => e.CauseId == a.ActivationId && e.Cell == P(8, 4)));
        }

        [Test]
        public void Pitchfork_Vertical_ClearsItsColumn()
        {
            var r = SwapOn(Level(Bg(9, 9)).With((4, 4, "lineV", "tomato")), P(4, 4), P(4, 5), out _);
            var a = FirstActivation(r);
            Assert.AreEqual(SpecialKind.LineV, a.Special);
            CollectionAssert.AreEquivalent(Rect(4, 0, 4, 8), Cells(a));
        }

        [Test]
        public void Bomb_Blasts3x3()
        {
            var r = SwapOn(Level(Bg(9, 9)).With((4, 4, "bomb", "tomato")), P(4, 4), P(5, 4), out _);
            var a = FirstActivation(r);
            Assert.AreEqual(SpecialKind.Bomb, a.Special);
            CollectionAssert.AreEquivalent(Rect(4, 3, 6, 5), Cells(a));
        }

        [Test]
        public void Bee_HitsItsCross_ThenFliesToTheGoalObstacle()
        {
            var level = Level(Bg(9, 9, (8, 8, '.')), goals: new List<GoalData> { Goal("crate") },
                layers: Layers(("crate", new[] { ".........", ".........", ".........", ".........", ".........", ".........", ".........", ".........", "........1" })))
                .With((1, 1, "bee", "tomato"));
            var r = SwapOn(level, P(1, 1), P(2, 1), out var g);
            var a = FirstActivation(r);
            Assert.AreEqual(SpecialKind.Bee, a.Special);
            Assert.IsTrue(Cells(a).IsSupersetOf(new[] { P(2, 1), P(1, 1), P(3, 1), P(2, 0), P(2, 2) }));
            var flight = r.OfType<BeeFlightEvent>().Single();
            Assert.AreEqual(P(8, 8), flight.To);
            var hit = r.OfType<ObstacleHitEvent>().Single(e => e.Obstacle == ObstacleKind.Crate);
            Assert.IsTrue(hit.Removed);
            Assert.AreEqual(HitSource.Bee, hit.Source);
            Assert.AreEqual(GameStatus.Won, g.Status);
        }

        [Test]
        public void Bee_WithCollectGoal_TargetsAGoalColourTile()
        {
            var level = Level(Bg(9, 9, (8, 8, 'y')), goals: new List<GoalData> { Goal("collect", 5, "corn") }).With((1, 1, "bee", "tomato"));
            var r = SwapOn(level, P(1, 1), P(2, 1), out _);
            Assert.AreEqual(P(8, 8), r.OfType<BeeFlightEvent>().Single().To);
        }

        [Test]
        public void Rainbow_SwappedWithTile_RemovesThatColour()
        {
            var level = Level(Bg(9, 9, (0, 0, 'r'), (8, 8, 'r'), (5, 4, 'r'), (0, 8, 'y'))).With((4, 4, "rainbow", null));
            var r = SwapOn(level, P(4, 4), P(5, 4), out _);
            var combo = r.OfType<ComboEvent>().Single();
            Assert.AreEqual(ComboKind.RainbowColor, combo.Combo);
            var a = FirstActivation(r);
            Assert.AreEqual(TileColor.Tomato, a.TargetColor);
            CollectionAssert.AreEquivalent(new[] { P(0, 0), P(8, 8), P(4, 4) }, a.Cells, "the tomato swapped in sits at (4,4) now");
        }

        [Test]
        public void SpecialInAMatch_Fires()
        {
            var level = Level(Bg(9, 9, (0, 3, 'r'), (2, 2, 'r'))).With((1, 3, "lineV", "tomato"));
            var r = SwapOn(level, P(2, 2), P(2, 3), out _);
            var a = r.OfType<ActivationEvent>().Single(e => e.Special == SpecialKind.LineV);
            CollectionAssert.AreEquivalent(Rect(1, 0, 1, 8), Cells(a));
            Assert.AreEqual(GroupAt(r, 0, 3).GroupId, r.OfType<PieceClearedEvent>().Single(e => e.PieceId == a.PieceId).CauseId);
        }

        [Test]
        public void ChainReaction_PitchforkSetsOffBomb_NextGeneration()
        {
            var level = Level(Bg(9, 9)).With((4, 4, "lineH", "tomato"), (0, 4, "bomb", "corn"));
            var r = SwapOn(level, P(4, 4), P(5, 4), out _);
            var line = r.OfType<ActivationEvent>().First(e => e.Special == SpecialKind.LineH);
            var bomb = r.OfType<ActivationEvent>().First(e => e.Special == SpecialKind.Bomb);
            Assert.AreEqual(0, line.Generation);
            Assert.AreEqual(1, bomb.Generation);
            Assert.AreEqual(line.ActivationId, bomb.CauseId);
            CollectionAssert.AreEquivalent(Rect(0, 3, 1, 5), Cells(bomb));
        }

        [Test]
        public void RainbowHitByBlast_RemovesTheMostCommonColour()
        {
            var level = Level(Bg(9, 9)).With((4, 4, "bomb", "tomato"), (6, 4, "rainbow", null));
            var r = SwapOn(level, P(4, 4), P(5, 4), out _);
            var rb = r.OfType<ActivationEvent>().Single(e => e.Special == SpecialKind.Rainbow);
            Assert.AreNotEqual(TileColor.None, rb.TargetColor);
            Assert.AreEqual(1, rb.Generation);
            Assert.Greater(rb.Cells.Count, 10);
        }

        [Test]
        public void NewSpecial_IsImmuneInTheWaveItIsMade()
        {
            // r, bomb(r), _, r on row 3; an r drops into (2,3): a 4-line with the bomb in it. The bomb's blast covers
            // (2,3) where the new pitchfork appears; the pitchfork must survive.
            var level = Level(Bg(9, 9, (0, 3, 'r'), (3, 3, 'r'), (2, 2, 'r'))).With((1, 3, "bomb", "tomato"));
            var r = SwapOn(level, P(2, 2), P(2, 3), out _);
            var made = r.OfType<SpecialCreatedEvent>().First(e => e.Wave == 0);
            Assert.AreEqual(SpecialKind.LineH, made.Special);
            Assert.IsTrue(r.OfType<ActivationEvent>().Any(e => e.Special == SpecialKind.Bomb && e.Wave == 0));
            Assert.IsFalse(r.OfType<PieceClearedEvent>().Any(e => e.PieceId == made.PieceId && e.Wave == 0));
        }

        [Test]
        public void SpecialSwappedWithChick_Fires()
        {
            var level = Level(Bg(9, 9, (5, 4, 'C')), goals: new List<GoalData> { Goal("chick", 1) }).With((4, 4, "lineV", "tomato"));
            var r = SwapOn(level, P(4, 4), P(5, 4), out _);
            Assert.AreEqual(SpecialKind.LineV, FirstActivation(r).Special);
        }

        [Test]
        public void RainbowSwappedWithChick_IsRejected()
        {
            var level = Level(Bg(9, 9, (5, 4, 'C')), goals: new List<GoalData> { Goal("chick", 1) }).With((4, 4, "rainbow", null));
            var g = Game(level);
            Assert.IsFalse(g.Swap(P(4, 4), P(5, 4)).Accepted);
        }

        // ------------------------------------------------------------ combos

        static ActivationEvent Combo(string k1, string k2, ComboKind expected, out MoveResult r, out Match3Game g, string c1 = "tomato", string c2 = "corn",
            params (int x, int y, char c)[] extra)
        {
            var level = Level(Bg(9, 9, extra)).With((4, 4, k1, c1), (5, 4, k2, c2));
            r = SwapOn(level, P(4, 4), P(5, 4), out g);
            var combo = r.OfType<ComboEvent>().Single();
            Assert.AreEqual(expected, combo.Combo);
            Assert.AreEqual(P(5, 4), combo.Center);
            return r.OfType<ActivationEvent>().First(e => e.ActivationId == combo.ActivationId);
        }

        [Test]
        public void Combo_LineLine_IsACross()
        {
            var a = Combo("lineH", "lineV", ComboKind.LineLine, out _, out _);
            var expect = Rect(0, 4, 8, 4);
            expect.UnionWith(Rect(5, 0, 5, 8));
            CollectionAssert.AreEquivalent(expect, Cells(a));
        }

        [Test]
        public void Combo_LineBomb_IsThreeRowsAndThreeColumns()
        {
            var a = Combo("lineV", "bomb", ComboKind.LineBomb, out _, out _);
            var expect = Rect(0, 3, 8, 5);
            expect.UnionWith(Rect(4, 0, 6, 8));
            CollectionAssert.AreEquivalent(expect, Cells(a));
        }

        [Test]
        public void Combo_BombBomb_Is5x5()
        {
            var a = Combo("bomb", "bomb", ComboKind.BombBomb, out _, out _);
            CollectionAssert.AreEquivalent(Rect(3, 2, 7, 6), Cells(a));
        }

        [Test]
        public void Combo_BeeBee_SendsThreeBees()
        {
            Combo("bee", "bee", ComboKind.BeeBee, out var r, out _);
            var flights = r.OfType<BeeFlightEvent>().ToList();
            Assert.AreEqual(3, flights.Count);
            Assert.AreEqual(3, flights.Select(f => f.To).Distinct().Count(), "three different targets");
        }

        [Test]
        public void Combo_BeeLine_BeeCarriesThePitchfork()
        {
            var a = Combo("bee", "lineH", ComboKind.BeeLine, out var r, out _);
            var flight = r.OfType<BeeFlightEvent>().Single();
            Assert.AreEqual(SpecialKind.LineH, flight.Carried);
            var fired = r.OfType<ActivationEvent>().Single(e => e.CauseId == a.ActivationId && e.Special == SpecialKind.LineH);
            Assert.AreEqual(flight.To, fired.Cell);
            CollectionAssert.AreEquivalent(Rect(0, flight.To.Y, 8, flight.To.Y), Cells(fired));
        }

        [Test]
        public void Combo_BeeBomb_BeeCarriesTheBomb()
        {
            var a = Combo("bomb", "bee", ComboKind.BeeBomb, out var r, out _);
            var flight = r.OfType<BeeFlightEvent>().Single();
            Assert.AreEqual(SpecialKind.Bomb, flight.Carried);
            var fired = r.OfType<ActivationEvent>().Single(e => e.CauseId == a.ActivationId && e.Special == SpecialKind.Bomb);
            Assert.AreEqual(flight.To, fired.Cell);
        }

        [Test]
        public void Combo_RainbowLine_TurnsTheColourIntoPitchforksThatFire()
        {
            var a = Combo("rainbow", "lineH", ComboKind.RainbowLine, out var r, out _, null, "corn", (0, 0, 'y'), (8, 8, 'y'), (0, 8, 'y'));
            Assert.AreEqual(TileColor.Corn, a.TargetColor);
            var transforms = r.OfType<TransformEvent>().ToList();
            Assert.AreEqual(3, transforms.Count);
            Assert.IsTrue(transforms.All(t => t.Special == SpecialKind.LineH || t.Special == SpecialKind.LineV));
            foreach (var t in transforms)
                Assert.IsTrue(r.OfType<ActivationEvent>().Any(e => e.PieceId == t.PieceId), "converted piece fires");
        }

        [Test]
        public void Combo_RainbowBomb_TurnsTheColourIntoBombs()
        {
            Combo("bomb", "rainbow", ComboKind.RainbowBomb, out var r, out _, "corn", null, (0, 0, 'y'), (8, 8, 'y'));
            var transforms = r.OfType<TransformEvent>().ToList();
            Assert.AreEqual(2, transforms.Count);
            Assert.IsTrue(transforms.All(t => t.Special == SpecialKind.Bomb));
            Assert.AreEqual(3, r.OfType<ActivationEvent>().Count(e => e.Special == SpecialKind.Bomb && e.Combo == ComboKind.None && e.Wave == 0), "two converted + the swapped one");
        }

        [Test]
        public void Combo_RainbowBee_TurnsTheColourIntoBees()
        {
            Combo("rainbow", "bee", ComboKind.RainbowBee, out var r, out _, null, "corn", (0, 0, 'y'), (8, 8, 'y'));
            Assert.AreEqual(2, r.OfType<TransformEvent>().Count(t => t.Special == SpecialKind.Bee));
            Assert.GreaterOrEqual(r.OfType<BeeFlightEvent>().Count(e => e.Wave == 0), 3);
        }

        [Test]
        public void Combo_RainbowRainbow_HitsEveryCell()
        {
            var a = Combo("rainbow", "rainbow", ComboKind.RainbowRainbow, out var r, out _, null, null);
            Assert.AreEqual(81, a.Cells.Count);
            int clearedWave0 = r.OfType<PieceClearedEvent>().Count(e => e.Wave == 0);
            Assert.AreEqual(81, clearedWave0, "two rainbows + 79 tiles");
        }
    }
}

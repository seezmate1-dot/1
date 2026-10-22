using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SunnyValley.Core.Match3;
using static SunnyValley.Core.Tests.M3;

namespace SunnyValley.Core.Tests
{
    /// <summary>
    /// The standard move in these tests: r at (0,3),(1,3) and an r at (2,2) swapped down into (2,3) — a match
    /// on (0..2, 3). (1,4) is side-adjacent to it, (1,3) is in it, (4,4) is far from it.
    /// </summary>
    public class Match3ObstacleTests
    {
        static readonly (int, int, char)[] Std = { (0, 3, 'r'), (1, 3, 'r'), (2, 2, 'r') };

        static string[] Board(params (int x, int y, char c)[] more) => Bg(6, 6, Std.Concat(more).ToArray());

        static MoveResult StdMove(LevelData level, out Match3Game g)
        {
            g = Game(level);
            var r = g.Swap(P(2, 2), P(2, 3));
            AssertAccepted(r);
            return r;
        }

        static List<ObstacleHitEvent> Hits(MoveResult r, int x, int y, int wave = 0) =>
            r.OfType<ObstacleHitEvent>().Where(e => e.Cell == P(x, y) && e.Wave == wave).ToList();

        // ------------------------------------------------------------ grass

        [Test]
        public void Grass_LosesALayerWhenTheTileOnItIsMatched()
        {
            var level = Level(Board(), layers: Layers(("grass", One(6, 6, 0, 3, '2'))), goals: new List<GoalData> { Goal("grass") });
            var r = StdMove(level, out var g);
            var h = Hits(r, 0, 3).Single();
            Assert.AreEqual(ObstacleKind.Grass, h.Obstacle);
            Assert.AreEqual(2, h.LayersBefore);
            Assert.AreEqual(1, h.LayersAfter);
            Assert.IsFalse(h.Removed);
            Assert.AreEqual(0, g.Goals[0].Current);
        }

        [Test]
        public void Grass_LastLayerCountsForTheGoal_AndAdjacentMatchesDoNothing()
        {
            var grass = One(6, 6, 0, 3, '1');
            grass[4] = ".1....";
            var level = Level(Board(), layers: Layers(("grass", grass)), goals: new List<GoalData> { Goal("grass") });
            var r = StdMove(level, out var g);
            Assert.IsTrue(Hits(r, 0, 3).Single().Removed);
            Assert.IsEmpty(Hits(r, 1, 4), "grass is only hit through its own cell");
            Assert.AreEqual(2, g.Goals[0].Target);
            Assert.GreaterOrEqual(g.Goals[0].Current, 1);
        }

        [Test]
        public void Grass_IsHitBySpecialsPassingOverIt()
        {
            var level = Level(Bg(9, 9), layers: Layers(("grass", One(9, 9, 0, 4, '1'))), goals: new List<GoalData> { Goal("grass") })
                .With((4, 4, "lineH", "tomato"));
            var g = Game(level);
            var r = g.Swap(P(4, 4), P(5, 4));
            var h = Hits(r, 0, 4).Single();
            Assert.AreEqual(HitSource.Line, h.Source);
            Assert.AreEqual(GameStatus.Won, g.Status);
        }

        // ------------------------------------------------------------ crates

        [Test]
        public void Crate_AdjacentMatchTakesOneLayer()
        {
            var level = Level(Board((1, 4, '.')), layers: Layers(("crate", One(6, 6, 1, 4, '3'))), goals: new List<GoalData> { Goal("crate") });
            var r = StdMove(level, out var g);
            var h = Hits(r, 1, 4).Single();
            Assert.AreEqual(ObstacleKind.Crate, h.Obstacle);
            Assert.AreEqual(HitSource.Adjacent, h.Source);
            Assert.AreEqual(2, h.LayersAfter);
            Assert.AreEqual(ObstacleKind.Crate, g.Board.GetBlocker(P(1, 4), out var layers));
            Assert.LessOrEqual(layers, 2);
        }

        [Test]
        public void Crate_OneHitPerMatchGroup_EvenIfSeveralMatchedTilesTouchIt()
        {
            // Crate at (1,2) touches (1,3) only; crate at (1,4) touches (1,3) only — each takes exactly one hit.
            var crate = One(6, 6, 1, 4, '3');
            var level = Level(Board((1, 4, '.')), layers: Layers(("crate", crate)), goals: new List<GoalData> { Goal("crate") });
            var r = StdMove(level, out _);
            Assert.AreEqual(1, Hits(r, 1, 4).Count);
        }

        [Test]
        public void Crate_LastLayerRemoved_CountsForGoal()
        {
            var level = Level(Board((1, 4, '.')), layers: Layers(("crate", One(6, 6, 1, 4, '1'))), goals: new List<GoalData> { Goal("crate") });
            var r = StdMove(level, out var g);
            Assert.IsTrue(Hits(r, 1, 4).Single().Removed);
            Assert.AreEqual(GameStatus.Won, g.Status);
            Assert.IsTrue(r.OfType<GoalProgressEvent>().Any(e => e.Goal == GoalType.Crate && e.Cell == P(1, 4)));
        }

        // ------------------------------------------------------------ stones

        [Test]
        public void Stone_IgnoresAdjacentMatches()
        {
            var level = Level(Board((1, 4, '.')), layers: Layers(("stone", One(6, 6, 1, 4, '1'))), goals: new List<GoalData> { Goal("stone") });
            var r = StdMove(level, out var g);
            Assert.IsEmpty(Hits(r, 1, 4));
            Assert.AreEqual(ObstacleKind.Stone, g.Board.GetBlocker(P(1, 4), out _));
        }

        [Test]
        public void Stone_BrokenByAPitchfork()
        {
            var level = Level(Bg(9, 9, (1, 4, '.')), layers: Layers(("stone", One(9, 9, 1, 4, '2'))), goals: new List<GoalData> { Goal("stone") })
                .With((4, 4, "lineH", "tomato"));
            var g = Game(level);
            var r = g.Swap(P(4, 4), P(5, 4));
            var h = Hits(r, 1, 4).Single();
            Assert.AreEqual(HitSource.Line, h.Source);
            Assert.AreEqual(1, h.LayersAfter);
        }

        [Test]
        public void Stone_BrokenByABomb()
        {
            var level = Level(Bg(9, 9, (6, 5, '.')), layers: Layers(("stone", One(9, 9, 6, 5, '1'))), goals: new List<GoalData> { Goal("stone") })
                .With((4, 4, "bomb", "tomato"));
            var g = Game(level);
            var r = g.Swap(P(4, 4), P(5, 4));
            Assert.AreEqual(HitSource.Blast, Hits(r, 6, 5).Single().Source);
            Assert.AreEqual(GameStatus.Won, g.Status);
        }

        // ------------------------------------------------------------ ice

        [Test]
        public void Ice_FrozenTileNeitherMatchesNorSwaps()
        {
            // (a corn line elsewhere keeps the board alive, so the start is not reshuffled)
            var level = Level(Board((3, 5, 'y'), (4, 5, 'y'), (5, 4, 'y')), layers: Layers(("ice", One(6, 6, 1, 3, '1'))), goals: new List<GoalData> { Goal("ice") });
            var g = Game(level);
            Assert.AreEqual(0, g.Reshuffles);
            Assert.IsFalse(g.IsValidSwap(P(2, 2), P(2, 3)), "the frozen r does not count for a line");
            Assert.AreEqual(RejectReason.NotSwappable, g.Swap(P(1, 3), P(1, 4)).OfType<SwapRejectedEvent>().Single().Reason);
        }

        [Test]
        public void Ice_BrokenByAdjacentMatch_TileStays()
        {
            var level = Level(Board(), layers: Layers(("ice", One(6, 6, 1, 4, '2'))), goals: new List<GoalData> { Goal("ice") });
            var g = Game(level);
            int id = g.Board.GetPiece(P(1, 4)).Id;
            var r = g.Swap(P(2, 2), P(2, 3));
            var h = Hits(r, 1, 4).Single();
            Assert.AreEqual(ObstacleKind.Ice, h.Obstacle);
            Assert.AreEqual(1, h.LayersAfter);
            Assert.IsFalse(r.OfType<PieceClearedEvent>().Any(e => e.PieceId == id && e.Wave == 0));
        }

        [Test]
        public void Ice_FrozenTileDoesNotFall_TheCellUnderItIsFilledDiagonally()
        {
            // Ice at (1,1): (1,3) is matched, (1,2) falls into it, the frozen tile stays and (1,2) gets a piece
            // sliding in diagonally from (0,1) or (2,1).
            var level = Level(Board(), layers: Layers(("ice", One(6, 6, 1, 1, '1'))), goals: new List<GoalData> { Goal("ice") });
            var g = Game(level);
            int id = g.Board.GetPiece(P(1, 1)).Id;
            var r = g.Swap(P(2, 2), P(2, 3));
            Assert.IsFalse(r.OfType<FallEvent>().Any(f => f.PieceId == id && f.Wave == 0));
            Assert.IsTrue(r.OfType<FallEvent>().Any(f => f.Wave == 0 && f.Segments.Any(s => s.Kind == FallKind.Diagonal && s.To == P(1, 2))));
            Assert.IsFalse(g.Board.GetPiece(P(1, 2)).IsEmpty);
        }

        // ------------------------------------------------------------ chains

        [Test]
        public void Chain_ChainedTileMatchesInPlace_ChainBreaks_TileStays()
        {
            var level = Level(Board(), layers: Layers(("chain", One(6, 6, 1, 3, '1'))), goals: new List<GoalData> { Goal("chain") });
            var g = Game(level);
            int id = g.Board.GetPiece(P(1, 3)).Id;
            Assert.IsFalse(g.IsValidSwap(P(1, 3), P(1, 4)), "chained pieces cannot be swapped");
            var r = g.Swap(P(2, 2), P(2, 3));
            AssertAccepted(r);
            var h = Hits(r, 1, 3).Single();
            Assert.AreEqual(ObstacleKind.Chain, h.Obstacle);
            Assert.AreEqual(HitSource.Match, h.Source);
            Assert.IsTrue(h.Removed);
            Assert.IsFalse(r.OfType<PieceClearedEvent>().Any(e => e.PieceId == id && e.Wave == 0));
            Assert.AreEqual(GameStatus.Won, g.Status);
        }

        [Test]
        public void Chain_IgnoresAdjacentMatches()
        {
            var level = Level(Board(), layers: Layers(("chain", One(6, 6, 1, 4, '1'))), goals: new List<GoalData> { Goal("chain") });
            var r = StdMove(level, out _);
            Assert.IsEmpty(Hits(r, 1, 4));
        }

        // ------------------------------------------------------------ honey

        [Test]
        public void Honey_SpreadsWhenNotHitDuringTheMove()
        {
            var level = Level(Board(), layers: Layers(("honey", One(6, 6, 5, 0, '1'))), goals: new List<GoalData> { Goal("honey") });
            var r = StdMove(level, out var g);
            var s = r.OfType<SpreadEvent>().Single();
            Assert.AreEqual(ObstacleKind.Honey, s.Obstacle);
            Assert.AreEqual(P(5, 0), s.From);
            Assert.IsTrue(s.To.IsAdjacent(P(5, 0)));
            Assert.AreEqual(ObstacleKind.Honey, g.Board.GetOverlay(s.To, out _));
            Assert.AreEqual(-1, s.ConsumedPieceId, "the tile stays under the honey");
            Assert.AreEqual(2, g.Goals[0].Target, "an 'all honey' goal grows with it");
        }

        [Test]
        public void Honey_HitNearby_DoesNotSpread()
        {
            var level = Level(Board(), layers: Layers(("honey", One(6, 6, 1, 4, '1'))), goals: new List<GoalData> { Goal("honey"), Goal("collect", 99, "corn") });
            var r = StdMove(level, out _);
            Assert.IsTrue(Hits(r, 1, 4).Single().Removed);
            Assert.IsEmpty(r.OfType<SpreadEvent>());
        }

        // ------------------------------------------------------------ weed

        [Test]
        public void Weed_GrowsIntoANeighbourWhenUntouched()
        {
            var level = Level(Board((5, 0, '.')), layers: Layers(("weed", One(6, 6, 5, 0, '1'))), goals: new List<GoalData> { Goal("weed") });
            var r = StdMove(level, out var g);
            var s = r.OfType<SpreadEvent>().Single();
            Assert.AreEqual(ObstacleKind.Weed, s.Obstacle);
            Assert.Greater(s.ConsumedPieceId, 0, "weed eats the tile");
            Assert.AreEqual(ObstacleKind.Weed, g.Board.GetBlocker(s.To, out _));
            Assert.IsTrue(g.Board.GetPiece(s.To).IsEmpty);
        }

        [Test]
        public void Weed_DestroyedByAdjacentMatch_DoesNotGrow()
        {
            var level = Level(Board((1, 4, '.')), layers: Layers(("weed", One(6, 6, 1, 4, '1'))), goals: new List<GoalData> { Goal("weed") });
            var r = StdMove(level, out var g);
            Assert.IsTrue(Hits(r, 1, 4).Single().Removed);
            Assert.IsEmpty(r.OfType<SpreadEvent>());
            Assert.AreEqual(GameStatus.Won, g.Status);
        }

        // ------------------------------------------------------------ garden beds

        [Test]
        public void Bed_GrowsFromAdjacentMatches_AndGivesACrop()
        {
            var level = Level(Board((1, 4, '.')), layers: Layers(("bed", One(6, 6, 1, 4, '1'))), goals: new List<GoalData> { Goal("crop", 1) });
            var r = StdMove(level, out var g);
            var h = Hits(r, 1, 4).Single();
            Assert.AreEqual(ObstacleKind.Bed, h.Obstacle);
            Assert.IsTrue(h.Harvested);
            Assert.AreEqual(GameStatus.Won, g.Status);
            Assert.AreEqual(ObstacleKind.Bed, g.Board.GetBlocker(P(1, 4), out _), "beds stay and grow again");
        }

        [Test]
        public void Bed_NeedsSeveralHits()
        {
            var level = Level(Board((1, 4, '.')), layers: Layers(("bed", One(6, 6, 1, 4, '3'))), goals: new List<GoalData> { Goal("crop", 2) });
            var r = StdMove(level, out var g);
            var h = Hits(r, 1, 4).Single();
            Assert.AreEqual(1, h.LayersAfter);
            Assert.IsFalse(h.Harvested);
        }

        // ------------------------------------------------------------ baskets

        [Test]
        public void Basket_TakesAdjacentClearedTilesOfItsColourOnly()
        {
            var level = Level(Board((1, 4, '.')), goals: new List<GoalData> { Goal("basket") });
            level.Baskets = new List<BasketData> { new BasketData { X = 1, Y = 5, Color = "tomato", Count = 5 } };
            var b = Board((1, 5, '.'));
            var r = StdMove(Level(b, goals: level.Goals).WithBaskets(level.Baskets), out var g);
            Assert.IsEmpty(r.OfType<PieceClearedEvent>().Where(e => e.IntoBasket == P(1, 5)), "two rows away: nothing");
            level = Level(Board((1, 4, '.')), goals: new List<GoalData> { Goal("basket") });
            level.Baskets = new List<BasketData> { new BasketData { X = 1, Y = 4, Color = "tomato", Count = 5 } };
            r = StdMove(level, out g);
            var fed = r.OfType<PieceClearedEvent>().Where(e => e.IntoBasket == P(1, 4) && e.Wave == 0).ToList();
            CollectionAssert.AreEquivalent(new[] { P(0, 3), P(1, 3), P(2, 3) }, fed.Select(e => e.Cell), "the 8 cells around the basket feed it");
            Assert.AreEqual(2, Hits(r, 1, 4).Last().LayersAfter);
        }

        [Test]
        public void Basket_WrongColourIsIgnored_FullBasketIsRemoved()
        {
            var level = Level(Board((1, 4, '.'), (1, 2, '.')), goals: new List<GoalData> { Goal("basket") });
            level.Baskets = new List<BasketData>
            {
                new BasketData { X = 1, Y = 4, Color = "tomato", Count = 1 },
                new BasketData { X = 1, Y = 2, Color = "corn", Count = 1 },
            };
            var r = StdMove(level, out var g);
            Assert.IsTrue(Hits(r, 1, 4).Single().Removed);
            Assert.IsEmpty(Hits(r, 1, 2));
            Assert.AreEqual(1, g.Goals[0].Current);
            Assert.AreEqual(2, g.Goals[0].Target);
        }

        // ------------------------------------------------------------ chicks

        [Test]
        public void Chick_FallsToTheBottomAndIsRescued()
        {
            // Match on the bottom row under the chick.
            var level = Level(Bg(6, 6, (0, 5, 'r'), (1, 5, 'r'), (2, 4, 'r'), (1, 4, 'C')), goals: new List<GoalData> { Goal("chick", 1) });
            var g = Game(level);
            int chick = g.Board.GetPiece(P(1, 4)).Id;
            var r = g.Swap(P(2, 4), P(2, 5));
            AssertAccepted(r);
            var fall = r.OfType<FallEvent>().Single(f => f.PieceId == chick);
            Assert.AreEqual(P(1, 5), fall.To);
            Assert.AreEqual(chick, r.OfType<ChickRescuedEvent>().Single().PieceId);
            Assert.AreEqual(GameStatus.Won, g.Status);
        }

        [Test]
        public void Chick_IsImmuneToBlasts()
        {
            var level = Level(Bg(9, 9, (1, 4, 'C')), goals: new List<GoalData> { Goal("chick", 1) }).With((4, 4, "lineH", "tomato"));
            var g = Game(level);
            int chick = g.Board.GetPiece(P(1, 4)).Id;
            var r = g.Swap(P(4, 4), P(5, 4));
            Assert.IsFalse(r.OfType<PieceClearedEvent>().Any(e => e.PieceId == chick));
        }

        [Test]
        public void Chick_CanBeSwappedIntoPlace()
        {
            // Swapping the chick at (2,3) with the r at (2,2) puts the r into the line.
            var level = Level(Bg(6, 6, (0, 3, 'r'), (1, 3, 'r'), (2, 2, 'r'), (2, 3, 'C')), goals: new List<GoalData> { Goal("chick", 1) });
            var g = Game(level);
            AssertAccepted(g.Swap(P(2, 3), P(2, 2)));
        }

        [Test]
        public void Chicks_SpawnFromSpawnersUpToTheLimit()
        {
            var level = Level(Bg(6, 6, Std), goals: new List<GoalData> { Goal("chick", 2) });
            level.Chicks = new ChickRules { Spawn = 2, MaxOnBoard = 1, Gap = 0 };
            var g = Game(level);
            var r = g.Swap(P(2, 2), P(2, 3));
            Assert.AreEqual(1, r.OfType<SpawnEvent>().Count(s => s.Kind == PieceKind.Chick), "one per move");
            Assert.AreEqual(1, g.CountChicks());
            Assert.AreEqual(1, g.ChicksStillToSpawn);
        }
    }
}

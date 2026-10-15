using LastHearth.Core;
using NUnit.Framework;

namespace LastHearth.Core.Tests
{
    public class BalanceTests
    {
        [Test]
        public void FirstSevenHearthTimersAreHandMadeLikeWhiteoutSurvival()
        {
            var b = TestData.Balance();
            long[] expected = { 10, 60, 180, 300, 600, 1200, 2700 };
            for (int level = 1; level <= expected.Length; level++)
                Assert.AreEqual(expected[level - 1], b.Level(level).HearthSeconds, "hearth level " + level);
        }

        [Test]
        public void PreviousWarehouseIsTooSmallForTheHearth()
        {
            var b = TestData.Balance();
            for (int level = b.Rules.StorageGateFrom; level < b.MaxHearthLevel; level++)
            {
                long price = b.Level(level).HearthJobCost;
                Assert.Greater(price, b.StorageAt(level - 1), "level " + level);
                Assert.LessOrEqual(price, b.StorageAt(level), "level " + level);
            }
        }

        [Test]
        public void PricesNeverJumpMoreThanThreeTimes()
        {
            var b = TestData.Balance();
            for (int level = 5; level < b.MaxHearthLevel - 1; level++)
            {
                double step = (double)b.Level(level + 1).HearthJobCost / b.Level(level).HearthJobCost;
                Assert.LessOrEqual(step, 3.1, "level " + level);
            }
        }

        [Test]
        public void TimersNeverExceedThreeDaysForTheHearthAndOneDayForARoom()
        {
            var b = TestData.Balance();
            foreach (var row in b.HearthLevels)
            {
                Assert.LessOrEqual(row.HearthSeconds, 72 * 3600, "level " + row.Level);
                Assert.LessOrEqual(row.RoomSeconds, 24 * 3600, "level " + row.Level);
            }
        }

        [Test]
        public void EveryKeyRoomExistsBeforeItIsNeeded()
        {
            var b = TestData.Balance();
            foreach (var row in b.HearthLevels)
                Assert.LessOrEqual(b.Room(row.KeyRoom).Unlock, row.Level, "level " + row.Level + " needs " + row.KeyRoom);
        }
    }

    public class FormulaTests
    {
        [Test]
        public void ProductionFollowsTheGddExample()
        {
            // GDD 6: a worker with stat 10 gives x1.4 of the base, with stat 35 gives x2.4.
            var b = TestData.Balance();
            double basis = b.ProductionBase(ResourceType.Coal, 5);
            Assert.AreEqual(basis * 1.4, Formulas.WorkerOutputPerHour(b, ResourceType.Coal, 5, 10), 1e-9);
            Assert.AreEqual(basis * 2.4, Formulas.WorkerOutputPerHour(b, ResourceType.Coal, 5, 35), 1e-9);
        }

        [Test]
        public void WarmthOfTheHearthAndTheFloors()
        {
            var b = TestData.Balance();
            Assert.AreEqual(100, Formulas.HearthWarmth(b, 1, HearthMode.Steady), 1e-9);
            Assert.AreEqual(1115, Formulas.HearthWarmth(b, 30, HearthMode.Steady), 1e-9);
            Assert.AreEqual(130, Formulas.HearthWarmth(b, 1, HearthMode.Blazing), 1e-9);
            Assert.AreEqual(64, Formulas.FloorWarmth(b, 1, HearthMode.Steady, b.Grid.HearthFloor + 2), 1e-9);
        }

        [Test]
        public void WarmthIsTerritory()
        {
            var b = TestData.Balance();
            int h = b.Grid.HearthFloor;
            Assert.IsTrue(Formulas.IsFloorHabitable(b, 1, HearthMode.Steady, h + 2, 1));
            Assert.IsFalse(Formulas.IsFloorHabitable(b, 1, HearthMode.Steady, h + 3, 1));
            Assert.IsTrue(Formulas.IsFloorHabitable(b, 1, HearthMode.Blazing, h + 3, 1), "blazing reaches one floor further");
            Assert.IsTrue(Formulas.IsFloorHabitable(b, 10, HearthMode.Steady, h + 3, 1), "a higher hearth opens floors");
        }

        [Test]
        public void SpeedupPricesMatchTheEconomy()
        {
            var b = TestData.Balance();
            Assert.AreEqual(0, Formulas.SpeedupCost(b, 5 * 60), "last five minutes are free");
            Assert.AreEqual(20, Formulas.SpeedupCost(b, 3600));
            Assert.AreEqual(260, Formulas.SpeedupCost(b, 24 * 3600));
            Assert.AreEqual(1000, Formulas.SpeedupCost(b, 7 * 24 * 3600));
        }

        [Test]
        public void HeroCapAndCost()
        {
            var b = TestData.Balance();
            Assert.AreEqual(25, Formulas.HeroLevelCap(b, 10));
            Assert.AreEqual(44, Formulas.HeroLevelCost(b, 1));
        }
    }
}

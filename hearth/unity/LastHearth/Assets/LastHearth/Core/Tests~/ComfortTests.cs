using System.Linq;
using LastHearth.Core;
using NUnit.Framework;

namespace LastHearth.Core.Tests
{
    /// <summary>MODES.md 4.4: a new name for the fortress and a room moved, 50 sparks each (the first name free).</summary>
    public class ComfortTests
    {
        const long T0 = TestData.Start;

        static int H => TestData.Balance().Grid.HearthFloor;

        static void Dig(GameSession s, int floor, int cell) => s.State.Cleared.Add(GameSession.CellKey(floor, cell));

        [Test]
        public void TheFirstNameIsFreeTheNextCostsFiftySparks()
        {
            var s = TestData.NewSession();
            Assert.AreEqual(GameSession.DefaultFortressName, s.FortressName);
            Assert.AreEqual(50, s.Balance.Activities.Shop.Convenience);
            Assert.AreEqual(0, s.RenamePrice);
            Assert.AreEqual(Fail.None, s.RenameFortress("  Север   и   пепел ", T0));
            Assert.AreEqual("Север и пепел", s.FortressName);
            Assert.AreEqual(50, s.RenamePrice);

            Assert.AreEqual(Fail.NotEnough, s.RenameFortress("Угольный двор", T0));
            Assert.AreEqual("Север и пепел", s.FortressName);
            s.State.Wallet[ResourceType.Sparks] = 70;
            Assert.AreEqual(Fail.AlreadyQueued, s.RenameFortress("Север и пепел", T0), "the same name costs nothing and changes nothing");
            Assert.AreEqual(Fail.None, s.RenameFortress("Угольный двор", T0));
            Assert.AreEqual(20, s.State.Wallet[ResourceType.Sparks]);
            Assert.AreEqual("Угольный двор", SaveSystem.FromJson(SaveSystem.ToJson(s.State)).FortressName);
        }

        [Test]
        public void ANameIsCleanedAndChecked()
        {
            Assert.IsNull(GameSession.CleanName(null));
            Assert.IsNull(GameSession.CleanName(" я "), "too short");
            Assert.IsNull(GameSession.CleanName(new string('ж', GameSession.FortressNameMax + 1)), "too long");
            Assert.AreEqual("Очаг", GameSession.CleanName("<Очаг>"), "no tags");
            Assert.AreEqual("Ок но", GameSession.CleanName("Ок\n\tно"));
            var s = TestData.NewSession();
            Assert.AreEqual(Fail.RulesNotMet, s.RenameFortress("   ", T0));
            Assert.AreEqual(0, s.State.Renames);
        }

        [Test]
        public void AMovedRoomKeepsItsWorkersStockLevelAndDecor()
        {
            var s = TestData.NewSession();
            var st = s.State;
            var kitchen = st.Rooms.First(r => r.Type == RoomType.Kitchen);
            var workers = kitchen.Workers.ToList();
            kitchen.Stock = 12.5;
            st.RoomDecor[kitchen.Id] = "banner";
            int oldFloor = kitchen.Floor, oldCell = kitchen.Cell;
            Dig(s, H + 1, 2);
            st.Wallet[ResourceType.Sparks] = 60;

            Assert.AreEqual(Fail.None, s.MoveRoom(kitchen.Id, H + 1, 2, T0));
            Assert.AreEqual(10, st.Wallet[ResourceType.Sparks]);
            Assert.AreEqual(H + 1, kitchen.Floor);
            Assert.AreEqual(2, kitchen.Cell);
            Assert.AreSame(kitchen, s.RoomAt(H + 1, 2));
            CollectionAssert.AreEqual(workers, kitchen.Workers);
            Assert.IsTrue(workers.All(id => s.FindDweller(id).RoomId == kitchen.Id));
            Assert.AreEqual(12.5, kitchen.Stock);
            Assert.AreEqual(1, kitchen.Level);
            Assert.AreEqual("banner", st.RoomDecor[kitchen.Id]);
            Assert.AreEqual(CellState.Free, s.CellStateAt(oldFloor, oldCell), "the cell it left stays dug out");
            Assert.Greater(s.RoomOutputPerHour(kitchen), 0, "it works in its new place");
        }

        [Test]
        public void ARoomMovesOnlyToAFreeWarmCellAndNotOntoTheHearth()
        {
            var s = TestData.NewSession();
            var st = s.State;
            var mine = st.Rooms.First(r => r.Type == RoomType.CoalMine);
            var g = s.Balance.Grid;
            st.Wallet[ResourceType.Sparks] = 500;
            Assert.AreEqual(Fail.CellTaken, s.CanMoveRoom(mine.Id, g.HearthFloor, g.HearthCells[0]), "the Hearth's cells");
            var bedrooms = st.Rooms.First(r => r.Type == RoomType.Bedrooms);
            Assert.AreEqual(Fail.CellTaken, s.CanMoveRoom(mine.Id, bedrooms.Floor, bedrooms.Cell), "another room");
            Assert.AreEqual(Fail.CellTaken, s.CanMoveRoom(mine.Id, H + 1, 2), "rubble");
            Assert.AreEqual(Fail.CellTaken, s.CanMoveRoom(mine.Id, mine.Floor, mine.Cell), "where it stands");
            Assert.AreEqual(Fail.NotFound, s.CanMoveRoom(999, H + 1, 2));
            // a far floor is too cold for a level 1 room
            Dig(s, g.Floors - 1, 1);
            Assert.AreEqual(Fail.TooCold, s.CanMoveRoom(mine.Id, g.Floors - 1, 1));
            Dig(s, H + 1, 2);
            st.Wallet[ResourceType.Sparks] = 49;
            Assert.AreEqual(Fail.NotEnough, s.MoveRoom(mine.Id, H + 1, 2, T0));
            Assert.AreEqual(H - 1, mine.Floor);
            st.SurgeLive = true;
            st.Wallet[ResourceType.Sparks] = 50;
            Assert.AreEqual(Fail.NotToday, s.CanMoveRoom(mine.Id, H + 1, 2), "not while the Surge is fought");
        }

        [Test]
        public void AMergedRoomNeedsAllItsCellsAndMayShiftOverItsOwn()
        {
            var s = TestData.NewSession();
            var st = s.State;
            st.Wallet[ResourceType.Sparks] = 500;
            for (int c = 0; c < s.Balance.Grid.Cells; c++)
                Dig(s, H + 1, c);
            var a = new Room { Id = st.NextRoomId++, Type = RoomType.Bedrooms, Level = 1, Floor = H + 1, Cell = 1 };
            var b = new Room { Id = st.NextRoomId++, Type = RoomType.Bedrooms, Level = 1, Floor = H + 1, Cell = 2 };
            st.Rooms.Add(a);
            st.Rooms.Add(b);
            Assert.AreEqual(Fail.None, s.MergeRooms(a.Id, b.Id, T0));
            Assert.AreEqual(2, a.Width);
            // cells 3-4 are free: it may move one step right over its own cell 2
            Assert.AreEqual(Fail.None, s.MoveRoom(a.Id, H + 1, 2, T0));
            Assert.AreEqual(2, a.Cell);
            Assert.AreEqual(CellState.Free, s.CellStateAt(H + 1, 1));
            Assert.AreSame(a, s.RoomAt(H + 1, 3));
            // a wide room does not fit where only one cell is free
            Assert.AreEqual(Fail.CellTaken, s.CanMoveRoom(a.Id, H + 1, s.Balance.Grid.Cells - 1), "its second cell is outside");
        }
    }
}

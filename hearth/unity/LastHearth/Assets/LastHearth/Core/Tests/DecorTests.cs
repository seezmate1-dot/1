using LastHearth.Core;
using NUnit.Framework;

namespace LastHearth.Core.Tests
{
    public class DecorTests
    {
        static GameSession Session()
        {
            var s = TestData.NewSession();
            s.State.HearthLevel = 10;
            return s;
        }

        [Test]
        public void PlainDecorCostsHoursOfIncomeAndPremiumCostsSparks()
        {
            var s = Session();
            long t = TestData.Start;
            var banner = s.Decor("banner_red");
            Assert.AreEqual(2 * s.IncomePerHour(), s.DecorPrice(banner));
            s.State.Wallet[ResourceType.Silver] = s.DecorPrice(banner);
            Assert.AreEqual(Fail.None, s.BuyDecor("banner_red", t));
            Assert.AreEqual(0, s.State.Wallet[ResourceType.Silver]);
            s.State.Wallet[ResourceType.Sparks] = 100;
            Assert.AreEqual(Fail.NotEnough, s.BuyDecor("throne", t));
            Assert.AreEqual(Fail.None, s.BuyDecor("chest_gold", t));
            Assert.AreEqual(0, s.State.Wallet[ResourceType.Sparks]);
            Assert.AreEqual(1, s.DecorOwned("chest_gold"));
        }

        [Test]
        public void ADecorationStandsInOneRoomAtATimeAndCanBeMoved()
        {
            var s = Session();
            s.State.DecorOwned["plant"] = 1;
            int a = s.State.Rooms[0].Id, b = s.State.Rooms[1].Id;
            Assert.AreEqual(Fail.None, s.PlaceDecor(a, "plant"));
            Assert.AreEqual(Fail.NotEnough, s.PlaceDecor(b, "plant"), "only one owned");
            s.RemoveDecor(a);
            Assert.AreEqual(Fail.None, s.PlaceDecor(b, "plant"));
            Assert.IsNull(s.RoomDecorId(a));
            Assert.AreEqual("plant", s.RoomDecorId(b));
            Assert.AreEqual(Fail.NotEnough, s.PlaceDecor(a, "throne"), "not owned");
        }

        [Test]
        public void DecorationsAreOnlyCosmetic()
        {
            var s = Session();
            var room = s.State.Rooms.Find(r => s.Balance.Room(r.Type).Produces != null && r.Workers.Count > 0);
            double before = s.RoomOutputPerHour(room);
            s.State.DecorOwned["crystal"] = 1;
            s.PlaceDecor(room.Id, "crystal");
            Assert.AreEqual(before, s.RoomOutputPerHour(room), "no bonus for money");
        }

        [Test]
        public void DecorationsSurviveASave()
        {
            var s = Session();
            s.State.DecorOwned["rug"] = 2;
            s.PlaceDecor(s.State.Rooms[0].Id, "rug");
            var copy = SaveSystem.FromJson(SaveSystem.ToJson(s.State));
            Assert.AreEqual(2, copy.DecorOwned["rug"]);
            Assert.AreEqual("rug", copy.RoomDecor[s.State.Rooms[0].Id]);
        }
    }
}

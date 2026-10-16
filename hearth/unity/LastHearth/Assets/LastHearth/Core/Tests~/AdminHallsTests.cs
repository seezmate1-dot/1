using System;
using System.Linq;
using LastHearth.Core;
using NUnit.Framework;

namespace LastHearth.Core.Tests
{
    /// <summary>The developer's menu, page «Залы» (GameSession.AdminHalls.cs).</summary>
    public class AdminHallsTests
    {
        const long T0 = TestData.Start;

        static int H => TestData.Balance().Grid.HearthFloor;

        static GameSession Session()
        {
            var s = TestData.NewSession();
            s.Combat = TestData.Combat();
            s.State.HearthLevel = 20;
            foreach (ResourceType r in Enum.GetValues(typeof(ResourceType)))
                s.State.Wallet[r] = 10_000_000;
            return s;
        }

        static Room Put(GameSession s, RoomType type, int level, int cell)
        {
            var room = new Room { Id = s.State.NextRoomId++, Type = type, Level = level, Floor = H + 1, Cell = cell };
            s.State.Rooms.Add(room);
            return room;
        }

        [Test]
        public void ANewBoardOfRecruitsNow()
        {
            var s = Session();
            Assert.AreEqual(0, s.AdminRefreshBoard(T0).Count, "no tavern, no board");
            Put(s, RoomType.Tavern, 5, 0);
            var first = s.TavernBoard(T0);
            Assert.AreEqual(Fail.None, s.Hire(0, T0, out _));
            var fresh = s.AdminRefreshBoard(T0 + 60);
            Assert.AreEqual(s.BoardSize(), fresh.Count);
            Assert.IsTrue(fresh.All(r => !r.Hired), "everyone on the new board can be hired");
            Assert.AreNotEqual(string.Join(",", first.Select(r => r.Name + r.Stats.Sum())), string.Join(",", fresh.Select(r => r.Name + r.Stats.Sum())));
            Assert.AreSame(fresh, s.TavernBoard(T0 + 120), "it stays until the next board time");
        }

        [Test]
        public void TheResearchEndsNowAndEverythingCanBeResearched()
        {
            var s = Session();
            Put(s, RoomType.Library, 1, 0);
            Assert.IsNull(s.AdminFinishResearch(T0));
            Assert.AreEqual(Fail.None, s.StartResearch("Crafts1", T0));
            Assert.AreEqual("Crafts1", s.AdminFinishResearch(T0 + 1).Id);
            Assert.IsTrue(s.Researched("Crafts1"));
            Assert.IsNull(s.CurrentResearch);
            Assert.AreEqual(Fail.None, s.StartResearch("War1", T0 + 1));
            Assert.AreEqual(59, s.AdminResearchAll(T0 + 2));
            Assert.IsNull(s.CurrentResearch, "the research in progress is among them");
            Assert.AreEqual(60, s.ResearchCount);
            Assert.Greater(s.ResearchValue(ResearchEffect.Attack), 0.1);
        }

        [Test]
        public void TrainingEndsNowAndEveryoneHeals()
        {
            var s = Session();
            Put(s, RoomType.TrainingHall, 10, 0);
            var a = s.AddDweller("Олег", new[] { 10, 10, 10, 10, 10 });
            var b = s.AddDweller("Рада", new[] { 10, 10, 10, 10, 10 });
            Assert.AreEqual(Fail.None, s.StartTraining(a.Id, Stat.Strength, T0));
            Assert.AreEqual(Fail.None, s.StartTraining(b.Id, Stat.Spirit, T0));
            Assert.AreEqual(2, s.AdminFinishTraining(T0 + 10));
            Assert.AreEqual(11, a.StatOf(Stat.Strength));
            Assert.AreEqual(11, b.StatOf(Stat.Spirit));
            Assert.AreEqual(0, s.Trainings.Count());

            a.InjuredUntil = T0 + 6 * 3600;
            s.State.Halls.SickUntil[b.Id] = T0 + 12 * 3600;
            Assert.AreEqual(2, s.AdminHealAll(T0 + 20));
            Assert.AreEqual(0, s.Patients(T0 + 20).Count);
        }

        [Test]
        public void AllRecordsOpenAndTheGiftComesAgain()
        {
            var s = Session();
            Put(s, RoomType.Archive, 1, 0);
            Assert.AreEqual(Fail.None, s.StartReading(s.Records[0].Id, T0));
            Assert.AreEqual(s.Records.Count, s.AdminUnlockRecords(T0 + 1));
            Assert.IsTrue(s.Records.All(r => s.RecordRead(r.Id)));
            Assert.IsNull(s.CurrentReading);

            Assert.AreEqual(Fail.Locked, s.AdminChapelGift(T0, out _, out _), "no chapel");
            Put(s, RoomType.Chapel, 10, 1);
            Assert.AreEqual(Fail.None, s.ClaimGift(T0, out _, out _));
            Assert.AreEqual(Fail.NotToday, s.ClaimGift(T0, out _, out _));
            Assert.AreEqual(Fail.None, s.AdminChapelGift(T0, out var gift, out long amount), "the menu gives it again");
            Assert.IsNotNull(gift);
            Assert.Greater(amount, 0);
            Assert.IsFalse(s.GiftReady(T0), "and today's is taken");
        }

        [Test]
        public void AHeroIsPutAtARankOfAscension()
        {
            var s = Session();
            var hero = s.State.Dwellers.First(d => d.IsHero);
            Assert.AreEqual(3, s.AdminSetAscension(hero.Id, 3));
            Assert.AreEqual(3, s.AscensionRank(hero.Id));
            Assert.AreEqual(1 + 3 * s.Balance.Halls.Chapel.Bonus, s.AscensionMult(hero.Id), 1e-9);
            Assert.AreEqual(5, s.AdminSetAscension(hero.Id, 99), "clamped to the top rank");
            Assert.AreEqual(0, s.AdminSetAscension(hero.Id, -4));
            Assert.AreEqual(0, s.AscensionRank(hero.Id));
            var dweller = s.State.Dwellers.First(d => !d.IsHero);
            Assert.AreEqual(-1, s.AdminSetAscension(dweller.Id, 2));
        }
    }
}

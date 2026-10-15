using System.Linq;
using LastHearth.Core;
using LastHearth.Core.Combat;
using NUnit.Framework;

namespace LastHearth.Core.Tests
{
    public class HuntTests
    {
        const long T0 = TestData.Start;

        /// <summary>A fortress at hearth 8 with five heroes of level 16.</summary>
        static GameSession Hunter()
        {
            var s = TestData.NewSession();
            s.Combat = TestData.Combat();
            s.State.HearthLevel = 8;
            var classes = new[] { HeroClass.Guardian, HeroClass.Swordsman, HeroClass.Cleaver, HeroClass.Archer, HeroClass.CoalPriest };
            s.State.Dwellers.Clear();
            foreach (var c in classes)
            {
                var d = s.AddDweller(c.ToString(), new[] { 21, 21, 21, 21, 21 });
                d.HeroClass = c;
                d.HeroLevel = 16;
                d.SkillLevel = 3;
            }
            return s;
        }

        [Test]
        public void ThreeTracksAndOneBackEveryEightHours()
        {
            var s = Hunter();
            var family = s.HuntDens(T0)[0];
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(Fail.None, s.StartHunt(family, 1, T0, out var battle));
                s.FinishHunt(battle, out _);
            }
            Assert.AreEqual(Fail.NoTracks, s.StartHunt(family, 1, T0, out _));
            Assert.AreEqual(0, s.HuntTracks(T0 + 8 * 3600 - 1));
            Assert.AreEqual(1, s.HuntTracks(T0 + 8 * 3600));
            Assert.AreEqual(3, s.HuntTracks(T0 + 48 * 3600), "no more than three");
        }

        [Test]
        public void ThreeDensADayThatRotate()
        {
            var s = Hunter();
            var today = s.HuntDens(T0);
            var tomorrow = s.HuntDens(T0 + 86400);
            Assert.AreEqual(3, today.Count);
            CollectionAssert.AreNotEquivalent(today, tomorrow);
            var resting = System.Enum.GetValues(typeof(MonsterFamily)).Cast<MonsterFamily>().Except(today).Single();
            Assert.AreEqual(Fail.NotToday, s.StartHunt(resting, 1, T0, out _));
        }

        [Test]
        public void RankCannotExceedTheHearth()
        {
            var s = Hunter();
            Assert.AreEqual(Fail.RankTooHigh, s.StartHunt(s.HuntDens(T0)[0], 9, T0, out _));
        }

        [Test]
        public void StarsPayCoresAndAHuntPaysOnce()
        {
            var s = Hunter();
            Assert.AreEqual(Fail.None, s.StartHunt(s.HuntDens(T0)[0], 8, T0, out var battle));
            battle.Brain = Brains.Careful();
            Assert.AreEqual(Fail.None, s.FinishHunt(battle, out var reward));
            Assert.Greater(reward.Stars, 0);
            long full = s.Balance.Activities.Hunt.CoresByLevel[7];
            Assert.AreEqual((long)System.Math.Round(full * s.Combat.StarReward[reward.Stars]), reward.Cores);
            Assert.AreEqual(reward.Cores, s.State.Wallet[ResourceType.Cores]);
            Assert.AreEqual(Fail.NoHunt, s.FinishHunt(battle, out _), "the same battle cannot pay twice");
        }

        [Test]
        public void AHuntLeftWhenTheGameClosedIsFoughtByAutoBattle()
        {
            var s = Hunter();
            s.StartHunt(s.HuntDens(T0)[0], 5, T0, out _);
            var saved = SaveSystem.FromJson(SaveSystem.ToJson(s.State));
            var reopened = new GameSession(s.Balance, saved) { Combat = s.Combat };
            var reward = reopened.ResolvePendingHunt();
            Assert.IsNotNull(reward);
            Assert.IsNull(reopened.State.Hunt);
            Assert.AreEqual(reward.Cores, reopened.State.Wallet[ResourceType.Cores]);
        }

        [Test]
        public void SkillsCostCoresAndCannotOutgrowTheHero()
        {
            var s = Hunter();
            var hero = s.State.Dwellers[0];
            hero.SkillLevel = 1;
            hero.HeroLevel = 11; // cap = 1 + 11 / 6 = 2
            s.State.Wallet[ResourceType.Cores] = 1000;
            Assert.AreEqual(Fail.None, s.UpgradeSkill(hero.Id));
            Assert.AreEqual(1000 - s.Balance.Activities.Skills.Cost[0], s.State.Wallet[ResourceType.Cores]);
            Assert.AreEqual(Fail.SkillCap, s.UpgradeSkill(hero.Id));
        }

        [Test]
        public void RefugeesComeWithTheHearthSoTheFirstHuntHasASquad()
        {
            var s = TestData.NewSession();
            FirstSessionBot.Play(s, T0, T0 + 90 * 60, 10);
            Assert.GreaterOrEqual(s.State.HearthLevel, 6);
            Assert.GreaterOrEqual(s.State.Dwellers.Count, 5 + 3, "enough people to make five heroes and still work the rooms");
        }
    }
}

using System;
using System.Linq;
using LastHearth.Core;
using LastHearth.Core.Combat;
using NUnit.Framework;

namespace LastHearth.Core.Tests
{
    public class CampaignTests
    {
        const long T0 = TestData.Start;

        /// <summary>A fortress with the squad a free player has at this hearth level (heroes at 2L).</summary>
        static GameSession Fortress(int hearth)
        {
            var s = TestData.NewSession();
            s.Combat = TestData.Combat();
            s.State.HearthLevel = hearth;
            s.State.Dwellers.Clear();
            var classes = new[] { HeroClass.Guardian, HeroClass.Swordsman, HeroClass.Cleaver, HeroClass.Archer, HeroClass.CoalPriest,
                                  HeroClass.AshMage, HeroClass.Swordsman, HeroClass.Archer, HeroClass.AshMage };
            int size = CombatCalibrationTests.SquadSize(hearth);
            for (int i = 0; i < size; i++)
            {
                int level = i < 5 ? 2 * hearth : Math.Max(1, 2 * hearth - 3);
                int main = 5 + 2 * hearth;
                var d = s.AddDweller(classes[i].ToString(), new[] { main, main, main, main, main });
                d.HeroClass = classes[i];
                d.HeroLevel = level;
                d.SkillLevel = Math.Min(10, 1 + level / 6);
            }
            return s;
        }

        [Test]
        public void TheDataHasTenActsOf19()
        {
            var c = TestData.Balance().Campaign;
            Assert.AreEqual(10, c.Acts.Count);
            Assert.AreEqual(190, c.Missions.Count);
            Assert.AreEqual(10, c.Missions.Count(m => m.Kind == MissionKind.Boss));
            for (int i = 1; i < c.Missions.Count; i++)
                Assert.GreaterOrEqual(c.Missions[i].Rank, c.Missions[i - 1].Rank, "ranks never go down");
        }

        [Test]
        public void EveryMissionIsWinnableForTheFreePlayerAtItsDay()
        {
            var b = TestData.Balance();
            int careful = 0, auto = 0, total = 0;
            for (int i = 0; i < b.Campaign.Missions.Count; i++)
            {
                var m = b.Campaign.Missions[i];
                var s = Fortress(m.Rank);
                for (int seed = 1; seed <= 3; seed++)
                {
                    var c = new Battle(s.Combat, s.MissionSetup(i, seed)) { Brain = Brains.Careful() };
                    c.RunToEnd();
                    var a = new Battle(s.Combat, s.MissionSetup(i, seed)) { Brain = Brains.AutoBattle() };
                    a.RunToEnd();
                    careful += c.Won ? 1 : 0;
                    auto += a.Won ? 1 : 0;
                    total++;
                    Assert.IsTrue(c.Won, "mission " + (i + 1) + " " + m.Kind + " rank " + m.Rank + " seed " + seed);
                }
            }
            TestContext.WriteLine("careful wins " + careful + "/" + total + ", auto wins " + auto + "/" + total);
            Assert.GreaterOrEqual((double)auto / total, 0.6, "auto-battle clears most of the campaign at the right time");
        }

        [Test]
        public void FirstClearPaysOnceAndThreeStarsOpenTheChest()
        {
            var s = Fortress(TestData.Balance().Campaign.Missions[0].Rank);
            Assert.AreEqual(Fail.None, s.StartMission(0, T0, out var battle));
            battle.Brain = Brains.Careful();
            s.FinishMission(battle, out var reward);
            Assert.IsTrue(reward.FirstClear);
            Assert.AreEqual(1, s.MissionsCleared);
            var m = s.Mission(0);
            Assert.AreEqual(m.Essence, reward.Got[ResourceType.Essence]);
            Assert.AreEqual(Fail.Locked, s.StartMission(2, T0, out _), "missions go in order");
            Assert.AreEqual(Fail.None, s.StartMission(0, T0, out var again));
            again.Brain = Brains.Careful();
            s.FinishMission(again, out var second);
            Assert.IsFalse(second.FirstClear);
            Assert.AreEqual(0, second.Got[ResourceType.Essence], "the first-clear reward is paid once");
        }

        [Test]
        public void ActBossBringsAStoryHero()
        {
            var s = Fortress(TestData.Balance().Campaign.Missions[18].Rank);
            for (int i = 0; i < 18; i++)
                s.State.CampaignStars.Add(3);
            int heroes = s.State.Dwellers.Count(d => d.IsHero);
            Assert.AreEqual(Fail.None, s.StartMission(18, T0, out var battle));
            battle.Brain = Brains.Careful();
            s.FinishMission(battle, out var reward);
            Assert.IsNotNull(reward.NewHero);
            Assert.AreEqual(heroes + 1, s.State.Dwellers.Count(d => d.IsHero));
            Assert.AreEqual(s.Mission(18).Sparks + (reward.Stars == 3 ? s.Balance.Campaign.ChestSparks : 0), reward.Got[ResourceType.Sparks]);
        }

        [Test]
        public void TenRepeatsADayForHalfTheReward()
        {
            var s = Fortress(TestData.Balance().Campaign.Missions[0].Rank);
            s.State.CampaignStars.Add(3);
            for (int i = 0; i < 10; i++)
                Assert.AreEqual(Fail.None, s.RepeatMission(0, T0, out _));
            Assert.AreEqual(Fail.NoTracks, s.RepeatMission(0, T0, out _));
            Assert.AreEqual(Fail.None, s.RepeatMission(0, T0 + 86400, out var next), "a new day brings new repeats");
            Assert.AreEqual((long)Math.Round(s.Mission(0).Essence * s.Balance.Campaign.RepeatEssenceShare), next.Got[ResourceType.Essence]);
        }

        [Test]
        public void AnUnfinishedMissionIsFoughtOutOnTheNextStart()
        {
            var s = Fortress(TestData.Balance().Campaign.Missions[0].Rank);
            s.StartMission(0, T0, out _);
            var reopened = new GameSession(s.Balance, SaveSystem.FromJson(SaveSystem.ToJson(s.State))) { Combat = s.Combat };
            var reward = reopened.ResolvePendingMission();
            Assert.IsNotNull(reward);
            Assert.AreEqual(-1, reopened.State.PendingMission);
        }
    }
}

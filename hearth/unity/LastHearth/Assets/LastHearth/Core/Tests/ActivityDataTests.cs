using System.Linq;
using LastHearth.Core;
using NUnit.Framework;

namespace LastHearth.Core.Tests
{
    /// <summary>The numbers of docs/ACTIVITIES.md reach the game through balance.json.</summary>
    public class ActivityDataTests
    {
        [Test]
        public void EachActivityHasItsOwnResourceTables()
        {
            var a = TestData.Balance().Activities;
            Assert.AreEqual(2, a.Surge.Unlock);
            Assert.AreEqual(6, a.Hunt.Unlock);
            Assert.AreEqual(9, a.Routes.Unlock);
            Assert.AreEqual(30, a.Surge.EmberByLevel.Length);
            Assert.AreEqual(50, a.Surge.EmberByLevel[9], "Жар = 20 + 3 x hearth level");
            Assert.AreEqual(7, a.Hunt.CoresByLevel[9], "Сердцевины = 2 + hearth / 2");
        }

        [Test]
        public void HeartOfTheHearthHasThirtyTalentsInThreeTiers()
        {
            var h = TestData.Balance().Activities.Heart;
            Assert.AreEqual(30, h.NodeCost.Length);
            Assert.AreEqual(30327, h.NodeCost.Sum());
            Assert.AreEqual(new[] { 2, 12, 20 }, h.NodeGate.Distinct().ToArray());
        }

        [Test]
        public void SkillsAndSpecializations()
        {
            var a = TestData.Balance().Activities;
            Assert.AreEqual(737, a.Skills.Cost.Sum(), "one hero's skill from 1 to 10");
            Assert.AreEqual(new[] { 5, 10, 15, 20, 25 }, a.Specializations.Tiers.Select(t => t[0]).ToArray());
            Assert.AreEqual(3, a.Specializations.Ranks);
        }

        [Test]
        public void OldSavesWithFewerResourcesStillLoad()
        {
            var s = TestData.NewSession();
            string json = SaveSystem.ToJson(s.State);
            var state = SaveSystem.FromJson(json);
            state.Wallet.Amounts = new long[6]; // a save from before Ember, Cores and Blueprints
            state.Wallet[ResourceType.Cores] += 5;
            Assert.AreEqual(5, state.Wallet[ResourceType.Cores]);
            Assert.AreEqual(ResourceBag.Count, state.Wallet.Amounts.Length);
        }
    }
}

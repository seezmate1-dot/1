using System;
using System.Linq;
using LastHearth.Core.Combat;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace LastHearth.Core.Tests
{
    /// <summary>GDD 8.1-8.3, 13.1: pets, companions of heroes, the shops, and old saves.</summary>
    public class KennelTests
    {
        const long T0 = TestData.Start;

        /// <summary>A fortress at hearth 8 with five heroes of level 16.</summary>
        static GameSession Fortress()
        {
            var s = TestData.NewSession();
            s.Combat = TestData.Combat();
            s.State.HearthLevel = 8;
            s.Advance(T0); // the login calendar and the starter rewards come first: the wallets below are exact
            s.ClaimRewards();
            var classes = new[] { HeroClass.Guardian, HeroClass.Swordsman, HeroClass.Cleaver, HeroClass.Archer, HeroClass.CoalPriest };
            foreach (var d in s.State.Dwellers.Where(d => d.RoomId < 0).ToList())
                s.State.Dwellers.Remove(d);
            foreach (var c in classes)
            {
                var d = s.AddDweller(c.ToString(), new[] { 21, 21, 21, 21, 21 });
                d.HeroClass = c;
                d.HeroLevel = 16;
                d.SkillLevel = 3;
            }
            return s;
        }

        // ------------------------------------------------------------------ pets

        [Test]
        public void TheFirstWonHuntBringsTheWolfAndItGoesToWork()
        {
            var s = Fortress();
            Assert.IsFalse(s.HasPet("Wolf"));
            s.State.HuntWins = 1;
            CollectionAssert.Contains(s.CheckKennel(), "Волк");
            Assert.AreEqual(1, s.PetLevel("Wolf"));
            Assert.AreEqual("Wolf", s.ActivePet().Id, "the first pet becomes active at once");
            Assert.IsEmpty(s.CheckKennel().Where(n => n == "Волк"), "a pet is given once");
        }

        [Test]
        public void HuntsAndDailyTasksGiveTreats()
        {
            var s = Fortress();
            var family = s.HuntDens(T0)[0];
            Assert.AreEqual(Fail.None, s.StartHunt(family, 1, T0, out var battle));
            battle.Brain = Brains.Careful();
            s.FinishHunt(battle, out _);
            Assert.IsTrue(battle.Won);
            Assert.AreEqual(s.Balance.Pets.TreatsPerHuntWin, s.Treats);
            Assert.IsTrue(s.HasPet("Wolf"), "the first won hunt brings the wolf");
        }

        [Test]
        public void FeedingRaisesTheLevelAndSpendsTreats()
        {
            var s = Fortress();
            s.State.HuntWins = 1;
            s.CheckKennel();
            long cost = s.PetFeedCost("Wolf");
            Assert.AreEqual(s.Balance.Pets.LevelCost[0], cost);
            Assert.AreEqual(Fail.NotEnough, s.FeedPet("Wolf"));
            s.GrantTreats(cost + 3);
            Assert.AreEqual(Fail.None, s.FeedPet("Wolf"));
            Assert.AreEqual(2, s.PetLevel("Wolf"));
            Assert.AreEqual(3, s.Treats);
            Assert.AreEqual(Fail.NotFound, s.FeedPet("Raven"), "a pet the player does not own");

            s.GrantTreats(100000);
            int guard = 0;
            while (s.FeedPet("Wolf") == Fail.None && guard++ < 50)
            {
            }
            Assert.AreEqual(Fail.AboveHearth, s.FeedPet("Wolf"), "no higher than the hearth");
            Assert.AreEqual(8, s.PetLevel("Wolf"));
            s.State.HearthLevel = 20;
            while (s.FeedPet("Wolf") == Fail.None && guard++ < 50)
            {
            }
            Assert.AreEqual(s.Balance.Pets.MaxLevel, s.PetLevel("Wolf"));
            Assert.AreEqual(Fail.LimitReached, s.FeedPet("Wolf"));
            Assert.Greater(s.Treats, 0, "treats never go below zero");
        }

        [Test]
        public void TheActivePetHelpsEveryBattleOfTheSquad()
        {
            var s = Fortress();
            var bare = new Battle(s.Combat, s.MissionSetup(0, 1));
            s.State.HuntWins = 1;
            s.CheckKennel();
            var setup = s.MissionSetup(0, 1);
            Assert.AreEqual(1 + s.PetValue(s.PetKind("Wolf"), 1), setup.PetAttack, 1e-9);
            var helped = new Battle(s.Combat, setup);
            Assert.Greater(helped.Heroes.Sum(h => h.Attack), bare.Heroes.Sum(h => h.Attack) * 1.02);

            // only one pet works at a time: the bear cub replaces the wolf
            s.State.Wallet[ResourceType.Sparks] = 1000;
            Assert.AreEqual(Fail.None, s.BuyOffer("pet:BearCub", T0));
            Assert.AreEqual(Fail.None, s.SetActivePet("BearCub"));
            var bear = s.MissionSetup(0, 1);
            Assert.AreEqual(1, bear.PetAttack);
            Assert.Greater(bear.PetHp, 1);
        }

        [Test]
        public void TheRavenStrikesInBattle()
        {
            var setup = CombatCalibrationTests.Squad(6, 12, MonsterFamily.Possessed, 3);
            var plain = new Battle(TestData.Combat(), setup);
            for (int i = 0; i < 300; i++)
                plain.Step();
            setup = CombatCalibrationTests.Squad(6, 12, MonsterFamily.Possessed, 3);
            setup.PetStrike = 1;
            setup.PetEvery = 50;
            var raven = new Battle(TestData.Combat(), setup);
            for (int i = 0; i < 300; i++)
                raven.Step();
            Assert.Greater(raven.DamageDealt, plain.DamageDealt * 1.03);
            Assert.Less(raven.DamageDealt, plain.DamageDealt * 1.25, "a pet helps, it does not win the fight");
        }

        [Test]
        public void FortressPetsChangeTheEconomy()
        {
            var s = Fortress();
            double burn = s.CoalBurnPerHour();
            var producer = s.State.Rooms.First(r => s.Balance.Room(r.Type).Produces == ResourceType.Coal ||
                                                    s.Balance.Room(r.Type).Produces == ResourceType.Food);
            if (producer.Workers.Count == 0)
                s.Assign(s.State.Dwellers.First(d => !d.IsHero).Id, producer.Id);
            double output = s.RoomOutputPerHour(producer);
            Assert.Greater(output, 0);

            s.State.Wallet[ResourceType.Sparks] = 400;
            Assert.AreEqual(Fail.None, s.BuyOffer("pet:MistFox", T0));
            Assert.AreEqual(0, s.State.Wallet[ResourceType.Sparks]);
            Assert.AreEqual("MistFox", s.ActivePet().Id, "the first pet goes to work");
            Assert.AreEqual(output * (1 + s.PetValue(s.PetKind("MistFox"), 1)), s.RoomOutputPerHour(producer), 1e-6);

            s.State.Wallet[ResourceType.Silver] = 1_000_000;
            Assert.AreEqual(Fail.None, s.BuyOffer("pet:CoalCat", T0));
            Assert.AreEqual(Fail.None, s.SetActivePet("CoalCat"));
            Assert.AreEqual(burn * (1 - s.PetValue(s.PetKind("CoalCat"), 1)), s.CoalBurnPerHour(), 1e-6);
            Assert.AreEqual(output, s.RoomOutputPerHour(producer), 1e-6, "the fox rests: one pet at a time");
        }

        // ------------------------------------------------------------------ companions

        [Test]
        public void HeroesTakeCompanionsAndBattlesUseThem()
        {
            var s = Fortress();
            s.CheckKennel();
            Assert.IsTrue(s.CompanionUnlocked("Squire"), "the squire walks with the first hero");
            Assert.IsFalse(s.CompanionUnlocked("Wolfhound"));
            var guardian = s.Squad().First(d => d.HeroClass == HeroClass.Guardian);
            var worker = s.State.Dwellers.First(d => !d.IsHero);
            Assert.AreEqual(Fail.NotAHero, s.SetCompanion(worker.Id, "Squire"));
            Assert.AreEqual(Fail.Locked, s.SetCompanion(guardian.Id, "Wolfhound"));
            Assert.AreEqual(Fail.None, s.SetCompanion(guardian.Id, "Squire"));

            var setup = s.MissionSetup(0, 1);
            var hero = setup.Heroes.First(h => h.Id == guardian.Id);
            Assert.IsNotNull(hero.Companion);
            Assert.AreEqual(CompanionSkill.Guard, hero.Companion.Skill);
            Assert.IsTrue(setup.Heroes.Where(h => h.Id != guardian.Id).All(h => h.Companion == null));

            s.State.HuntWins = 3;
            s.CheckKennel();
            Assert.AreEqual(Fail.None, s.SetCompanion(guardian.Id, "Wolfhound"));
            Assert.AreEqual(CompanionSkill.Bite, s.MissionSetup(0, 1).Heroes.First(h => h.Id == guardian.Id).Companion.Skill);
            Assert.AreEqual(Fail.None, s.SetCompanion(guardian.Id, ""));
            Assert.IsNull(s.CompanionOf(guardian.Id));
        }

        [Test]
        public void TheBondGrowsWithTreatsUpToTheHeroLevel()
        {
            var s = Fortress();
            s.CheckKennel();
            var hero = s.Squad()[0];
            Assert.AreEqual(Fail.NotFound, s.RaiseBond(hero.Id), "no companion yet");
            s.SetCompanion(hero.Id, "Squire");
            Assert.AreEqual(Fail.NotEnough, s.RaiseBond(hero.Id));
            s.GrantTreats(100000);
            int guard = 0;
            while (s.RaiseBond(hero.Id) == Fail.None && guard++ < 50)
            {
            }
            Assert.AreEqual(1 + 16 / s.Balance.Companions.HeroLevelsPerLevel, s.BondLevel(hero.Id));
            Assert.AreEqual(Fail.HeroCap, s.RaiseBond(hero.Id));
            hero.HeroLevel = 60;
            while (s.RaiseBond(hero.Id) == Fail.None && guard++ < 50)
            {
            }
            Assert.AreEqual(s.Balance.Companions.MaxLevel, s.BondLevel(hero.Id));
            Assert.AreEqual(Fail.LimitReached, s.RaiseBond(hero.Id));
            long spent = 100000 - s.Treats;
            Assert.AreEqual(s.Balance.Companions.LevelCost.Sum(), spent);

            // the bond belongs to the hero: it stays when the companion changes
            s.State.HuntWins = 3;
            s.CheckKennel();
            s.SetCompanion(hero.Id, "Wolfhound");
            Assert.AreEqual(s.Balance.Companions.MaxLevel, s.BondLevel(hero.Id));
        }

        static BattleSetup WithCompanions(BattleSetup setup, CompanionSkill skill, int every, double power)
        {
            foreach (var h in setup.Heroes)
                h.Companion = new CompanionSetup { Name = skill.ToString(), Skill = skill, Every = every, Power = power };
            return setup;
        }

        static Battle Run(BattleSetup setup, int ticks)
        {
            var b = new Battle(TestData.Combat(), setup);
            for (int i = 0; i < ticks && !b.Over; i++)
                b.Step();
            return b;
        }

        [Test]
        public void CompanionSkillsWorkInBattle()
        {
            var c = TestData.Combat();
            var plain = Run(CombatCalibrationTests.Squad(8, 16, MonsterFamily.Knight, 5), 400);
            var bite = Run(WithCompanions(CombatCalibrationTests.Squad(8, 16, MonsterFamily.Knight, 5), CompanionSkill.Bite, 50, 0.4), 400);
            Assert.Greater(bite.DamageDealt, plain.DamageDealt * 1.04, "the wolfhounds bite");
            Assert.Less(bite.DamageDealt, plain.DamageDealt * 1.3);

            double Lost(Battle b) => b.Heroes.Sum(h => h.MaxHp - Math.Max(0, h.Hp));
            var guard = Run(WithCompanions(CombatCalibrationTests.Squad(8, 16, MonsterFamily.Knight, 5), CompanionSkill.Guard, 0, 0.1), 400);
            Assert.Less(Lost(guard), Lost(plain), "the squires take part of the blows");

            var heal = Run(WithCompanions(CombatCalibrationTests.Squad(8, 16, MonsterFamily.Knight, 5), CompanionSkill.Heal, 60, 0.05), 400);
            Assert.Less(Lost(heal), Lost(plain), "the fireflies mend");

            var falcon = Run(WithCompanions(CombatCalibrationTests.Squad(8, 16, MonsterFamily.Knight, 5), CompanionSkill.Stagger, 50, 5), 400);
            Assert.Greater(falcon.DamageDealt, plain.DamageDealt, "the falcons bring the stuns sooner");
            Assert.IsNotNull(c);
        }

        /// <summary>The best kennel a squad can have (the pet up to the hearth, the bonds at 1 + hero level / 4)
        /// is a help, not a replacement for growth.</summary>
        [Test]
        public void AFullKennelDoesNotReplaceHeroLevels()
        {
            var b = TestData.Balance();
            var wolf = b.Pets.Kinds.First(k => k.Id == "Wolf");
            var hound = b.Companions.Kinds.First(k => k.Id == "Wolfhound");
            var squire = b.Companions.Kinds.First(k => k.Id == "Squire");
            BattleSetup Full(int rank, int level, MonsterFamily f, int seed)
            {
                var setup = CombatCalibrationTests.Squad(rank, level, f, seed);
                int pet = Math.Min(b.Pets.MaxLevel, rank);
                setup.PetAttack = 1 + wolf.Value + wolf.PerLevel * (pet - 1);
                foreach (var h in setup.Heroes)
                {
                    var kind = h.Class == HeroClass.Guardian ? squire : hound;
                    int bond = Math.Min(b.Companions.MaxLevel, 1 + h.Level / b.Companions.HeroLevelsPerLevel);
                    h.Companion = new CompanionSetup { Skill = kind.Skill, Every = kind.Every, Power = kind.Power + kind.PerLevel * (bond - 1) };
                }
                return setup;
            }
            foreach (MonsterFamily f in Enum.GetValues(typeof(MonsterFamily)))
            {
                foreach (int r in new[] { 10, 20, 30 })
                {
                    int weakWins = 0, fullWins = 0;
                    for (int seed = 1; seed <= 8; seed++)
                    {
                        var weak = new Battle(TestData.Combat(), Full(r, r, f, seed)) { Brain = Brains.Careful() };
                        weak.RunToEnd();
                        weakWins += weak.Won ? 1 : 0;
                        var full = new Battle(TestData.Combat(), Full(r, 2 * r, f, seed)) { Brain = Brains.Careful() };
                        full.RunToEnd();
                        fullWins += full.Won ? 1 : 0;
                    }
                    TestContext.WriteLine(f + " rank " + r + ": half-level squad with a full kennel " + weakWins + "/8, full squad " + fullWins + "/8");
                    Assert.LessOrEqual(weakWins, 3, f + " rank " + r + ": a half-level squad still loses with a full kennel");
                    Assert.GreaterOrEqual(fullWins, 7, f + " rank " + r);
                }
            }
        }

        [Test]
        public void TheCampaignCountsTheFreePlayersKennel()
        {
            var b = TestData.Balance();
            Assert.AreEqual(b.MaxHearthLevel, b.Companions.CampaignHp.Length);
            for (int i = 0; i < b.Companions.CampaignHp.Length; i++)
            {
                Assert.That(b.Companions.CampaignHp[i], Is.InRange(1.0, 1.3), "level " + (i + 1));
                Assert.That(b.Companions.CampaignAttack[i], Is.InRange(1.0, 1.15), "level " + (i + 1));
                if (i > 0)
                    Assert.GreaterOrEqual(b.Companions.CampaignHp[i], b.Companions.CampaignHp[i - 1]);
            }
        }

        // ------------------------------------------------------------------ shops

        [Test]
        public void TheMerchantSellsTreatsOnceADay()
        {
            var s = Fortress();
            var offer = s.MerchantOffers(T0).First(o => o.Id == "m:Treats");
            Assert.AreEqual(s.IncomePerHour(), offer.Price, "an hour of the fortress income");
            s.State.Wallet[ResourceType.Silver] = offer.Price - 1;
            Assert.AreEqual(Fail.NotEnough, s.BuyOffer("m:Treats", T0));
            Assert.AreEqual(offer.Price - 1, s.State.Wallet[ResourceType.Silver], "nothing is taken");
            Assert.AreEqual(0, s.Treats);

            s.State.Wallet[ResourceType.Silver] = offer.Price * 3;
            Assert.AreEqual(Fail.None, s.BuyOffer("m:Treats", T0));
            Assert.AreEqual(offer.Price * 2, s.State.Wallet[ResourceType.Silver]);
            Assert.AreEqual(s.Balance.Market.MerchantTreats, s.Treats);
            Assert.AreEqual(Fail.LimitReached, s.BuyOffer("m:Treats", T0 + 60));
            Assert.AreEqual(Fail.None, s.BuyOffer("m:Treats", T0 + 86400), "a new day, a new bag");
        }

        [Test]
        public void ShopsAreClosedWithoutTheTradingPost()
        {
            var s = TestData.NewSession();
            Assert.IsEmpty(s.MerchantOffers(T0));
            Assert.IsEmpty(s.DailyDeals(T0));
            Assert.AreEqual(Fail.Locked, s.BuyOffer("m:Treats", T0));
            Assert.AreEqual(Fail.Locked, s.Exchange(ResourceType.Iron, ResourceType.Silver, 10, T0, out _));
            Assert.IsNotEmpty(s.PremiumOffers(T0), "the Sparks shop is open from the start");
        }

        [Test]
        public void SparksShopHasFixedPricesAndLimits()
        {
            var s = Fortress();
            var m = s.Balance.Market;
            s.State.Wallet[ResourceType.Sparks] = 10000;
            long hourglass = s.State.HourglassSeconds;
            for (int i = 0; i < m.PremiumHourglass[2]; i++)
                Assert.AreEqual(Fail.None, s.BuyOffer("p:Hourglass", T0));
            Assert.AreEqual(Fail.LimitReached, s.BuyOffer("p:Hourglass", T0));
            Assert.AreEqual(hourglass + m.PremiumHourglass[2] * m.PremiumHourglass[0] * 60, s.State.HourglassSeconds);
            Assert.AreEqual(10000 - m.PremiumHourglass[2] * m.PremiumHourglass[1], s.State.Wallet[ResourceType.Sparks]);
            Assert.AreEqual(m.PremiumHourglass[1], s.Balance.SpeedupPoints[1][1] * m.PremiumHourglass[0] / s.Balance.SpeedupPoints[1][0],
                "an hourglass hour costs what a speed-up hour costs");

            // pets and companions: once, and never a lottery
            Assert.AreEqual(Fail.None, s.BuyOffer("comp:Firefly", T0));
            Assert.IsTrue(s.CompanionUnlocked("Firefly"));
            Assert.AreEqual(Fail.LimitReached, s.BuyOffer("comp:Firefly", T0));
            Assert.AreEqual(Fail.None, s.BuyOffer("pet:BearCub", T0));
            Assert.AreEqual(Fail.LimitReached, s.BuyOffer("pet:BearCub", T0 + 7 * 86400));

            // the veteran: the stats are shown before buying; one a week; needs a free place
            var (name, stats) = s.VeteranPreview(T0);
            Assert.AreEqual(Fail.None, s.BuyOffer("p:Veteran", T0));
            var vet = s.State.Dwellers.Last();
            Assert.AreEqual(name, vet.Name);
            CollectionAssert.AreEqual(stats, vet.Stats);
            Assert.AreEqual(Fail.LimitReached, s.BuyOffer("p:Veteran", T0));
            while (s.State.Dwellers.Count < Formulas.DwellerCap(s.Balance, s.State.HearthLevel))
                s.AddDweller("x", new[] { 1, 1, 1, 1, 1 });
            Assert.AreEqual(Fail.LimitReached, s.BuyOffer("p:Veteran", T0 + 7 * 86400), "no place in the fortress");

            Assert.AreEqual(Fail.NotFound, s.BuyOffer("p:Lottery", T0));
            Assert.GreaterOrEqual(s.State.Wallet[ResourceType.Sparks], 0);
        }

        [Test]
        public void ThreeDealsADayEachOnce()
        {
            var s = Fortress();
            var today = s.DailyDeals(T0);
            Assert.AreEqual(s.Balance.Market.DealsShown, today.Count);
            Assert.AreEqual(today.Count, today.Select(o => o.Id).Distinct().Count());
            s.State.Wallet[ResourceType.Silver] = 1_000_000;
            foreach (var deal in today)
            {
                Assert.AreEqual(Fail.None, s.BuyOffer(deal.Id, T0));
                Assert.AreEqual(Fail.LimitReached, s.BuyOffer(deal.Id, T0));
            }
            Assert.IsTrue(s.DailyDeals(T0).All(o => o.Left == 0));
            bool changes = Enumerable.Range(1, 6).Any(d => !s.DailyDeals(T0 + d * 86400L).Select(o => o.Id).SequenceEqual(today.Select(o => o.Id)));
            Assert.IsTrue(changes, "the deals change from day to day");
        }

        [Test]
        public void TheExchangeTakesAFeeAndHasADailyLimit()
        {
            var s = Fortress();
            s.State.Wallet[ResourceType.Iron] = 100000;
            s.State.Wallet[ResourceType.Silver] = 0;
            s.State.Rewards[ResourceType.Silver] = 0;
            long left = s.ExchangeLeft(T0);
            Assert.AreEqual((long)(s.Balance.Market.ExchangeHours * s.IncomePerHour()), left);
            long amount = 500;
            Assert.AreEqual(Fail.None, s.Exchange(ResourceType.Iron, ResourceType.Silver, amount, T0, out long got));
            Assert.AreEqual((long)Math.Floor(amount * 1.2 * s.Balance.Market.ExchangeFee), got);
            Assert.Less(got, amount * 1.2 * 0.92, "worse than the Ash Fair");
            Assert.AreEqual(100000 - amount, s.State.Wallet[ResourceType.Iron]);
            Assert.AreEqual(got, s.State.Wallet[ResourceType.Silver] + s.State.Rewards[ResourceType.Silver]);
            Assert.AreEqual(Fail.LimitReached, s.Exchange(ResourceType.Iron, ResourceType.Silver, 99000, T0, out _));
            Assert.AreEqual(Fail.NotEnough, s.Exchange(ResourceType.Coal, ResourceType.Food, s.State.Wallet[ResourceType.Coal] + 1, T0, out _));
            Assert.AreEqual(Fail.NotFound, s.Exchange(ResourceType.Essence, ResourceType.Silver, 1, T0, out _), "essence is not traded");
            Assert.AreEqual(left, s.ExchangeLeft(T0 + 86400), "the limit comes back every day");
        }

        // ------------------------------------------------------------------ saves

        // ------------------------------------------------------------------ the hero screen

        [Test]
        public void TheHeroScreenShowsWhatTheBattleUses()
        {
            var s = Fortress();
            var hero = s.Squad()[0];
            var plain = s.HeroPreview(hero.Id);
            var inBattle = new Battle(s.Combat, s.MissionSetup(0, 1)).Hero(hero.Id);
            Assert.AreEqual(inBattle.Attack, plain.Attack, 1e-6, "the card shows the battle's numbers");
            Assert.AreEqual(inBattle.MaxHp, plain.MaxHp, 1e-6);
            Assert.IsNull(s.HeroPreview(s.State.Dwellers.First(d => !d.IsHero).Id), "a dweller has no hero card");
            Assert.AreEqual(Math.Pow(s.Combat.GearGrowth, 7), s.GearMult(), 1e-9, "the forge's gear follows the hearth (8)");

            s.State.HuntWins = 3;
            s.CheckKennel(); // the wolf and the wolfhound
            s.SetCompanion(hero.Id, "Wolfhound");
            var helped = s.HeroPreview(hero.Id);
            Assert.AreEqual(plain.Attack * (1 + s.PetValue(s.PetKind("Wolf"), 1)), helped.Attack, 1e-6, "the active pet");
            Assert.AreEqual("Волкодав", helped.Companion.Name);
            Assert.AreEqual(s.HeroPreview(hero.Id).Attack, new Battle(s.Combat, s.MissionSetup(0, 1)).Hero(hero.Id).Attack, 1e-6);
        }

        [Test]
        public void ThePlayerBotRaisesItsKennel()
        {
            var s = TestData.NewSession();
            s.Combat = TestData.Combat();
            var bot = new PlayerBot(s);
            bot.PlayDays(TestData.Start, 14);
            Assert.IsTrue(s.HasPet("Wolf") && s.HasPet("Raven"), "the hunts bring the wolf and the raven in two weeks");
            Assert.GreaterOrEqual(bot.PetFeeds, 3, "the battle pet is fed first");
            Assert.Greater(bot.BondRaises, 5);
            Assert.IsTrue(s.Squad().All(h => s.CompanionOf(h.Id) != null), "every hero of the squad walks with a companion");
        }

        [Test]
        public void PetsCompanionsAndShopsSurviveASave()
        {
            var s = Fortress();
            s.State.HuntWins = 3;
            s.CheckKennel();
            s.GrantTreats(500);
            s.FeedPet("Wolf");
            var hero = s.Squad()[0];
            s.SetCompanion(hero.Id, "Wolfhound");
            s.RaiseBond(hero.Id);
            s.State.Wallet[ResourceType.Silver] = 1_000_000;
            s.BuyOffer("m:Treats", T0);

            var loaded = new GameSession(s.Balance, SaveSystem.FromJson(SaveSystem.ToJson(s.State)));
            Assert.AreEqual(2, loaded.PetLevel("Wolf"));
            Assert.AreEqual("Wolf", loaded.ActivePet().Id);
            Assert.AreEqual(s.Treats, loaded.Treats);
            Assert.AreEqual("Wolfhound", loaded.CompanionOf(hero.Id).Kind);
            Assert.AreEqual(2, loaded.BondLevel(hero.Id));
            Assert.AreEqual(Fail.LimitReached, loaded.BuyOffer("m:Treats", T0), "today's purchases are remembered");
        }

        [Test]
        public void ASaveFromBeforePetsLoads()
        {
            var s = Fortress();
            var json = JObject.Parse(JsonConvert.SerializeObject(s.State));
            json.Remove("Pets");
            json.Remove("Companions");
            json["Market"] = null; // a broken or partial save
            var old = JsonConvert.DeserializeObject<GameState>(json.ToString());
            Assert.IsNotNull(old.Pets);
            Assert.IsNotNull(old.Companions);

            var loaded = new GameSession(s.Balance, SaveSystem.FromJson(SaveSystem.ToJson(old)));
            loaded.Combat = TestData.Combat();
            Assert.AreEqual(0, loaded.Treats);
            Assert.IsNull(loaded.ActivePet());
            Assert.IsNotEmpty(loaded.MerchantOffers(T0));
            var battle = new Battle(loaded.Combat, loaded.MissionSetup(0, 1)) { Brain = Brains.AutoBattle() };
            battle.RunToEnd();
            Assert.IsTrue(loaded.CompanionUnlocked("Squire"), "the old fortress gets what its play has earned");
        }
    }
}

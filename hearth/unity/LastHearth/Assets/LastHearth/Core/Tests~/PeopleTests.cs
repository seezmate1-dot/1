using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core;
using LastHearth.Core.Combat;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace LastHearth.Core.Tests
{
    /// <summary>GDD 7-8: traits, levels of the dwellers and the spirit of the fortress (GameSession.People.cs).</summary>
    public class PeopleTests
    {
        const long T0 = TestData.Start;
        const long Hour = 3600;

        static PeopleData P => TestData.Balance().People;
        static MoraleData M => P.Morale;

        static int H => TestData.Balance().Grid.HearthFloor;

        static GameSession Session()
        {
            var s = TestData.NewSession();
            s.Combat = TestData.Combat();
            s.Advance(T0);
            return s;
        }

        static Room Put(GameSession s, RoomType type, int level, int cell, int floor = -1)
        {
            var room = new Room { Id = s.State.NextRoomId++, Type = type, Level = level, Floor = floor < 0 ? H + 1 : floor, Cell = cell };
            s.State.Rooms.Add(room);
            return room;
        }

        static Dweller Plain(GameSession s, string name, int stat = 10, Trait trait = Trait.Hardy)
        {
            var d = s.AddDweller(name, new[] { stat, stat, stat, stat, stat });
            d.Traits = new List<Trait> { trait };
            return d;
        }

        static double Factor(GameSession s, string start) =>
            s.MoraleFactors(s.State.Time).Where(f => f.Name.StartsWith(start)).Sum(f => f.Value);

        static void Hours(GameSession s, double hours)
        {
            long until = s.State.Time + (long)(hours * Hour);
            for (long t = s.State.Time + 600; t <= until; t += 600)
                s.Advance(t);
            s.Advance(until);
        }

        // ================================================================== data

        [Test]
        public void TheDataHasTraitsRaritiesAndBands()
        {
            Assert.AreEqual(Enum.GetValues(typeof(Trait)).Length, P.Traits.Count, "every trait has its numbers");
            Assert.Greater(P.Traits.Count(t => t.Good), 2);
            Assert.Greater(P.Traits.Count(t => !t.Good), 2);
            Assert.AreEqual(60, P.MaxLevel);
            var rarities = TestData.Balance().Halls.Tavern.Rarities;
            for (int k = 1; k < rarities.Count; k++)
            {
                Assert.Greater(rarities[k].Growth, rarities[k - 1].Growth, "a rarer dweller grows faster");
                Assert.GreaterOrEqual(rarities[k].GoodTrait, rarities[k - 1].GoodTrait);
            }
            Assert.AreEqual(1.0, rarities.Last().GoodTrait, "a legendary one is never bad");
            Assert.AreEqual(-0.15, M.Bands.First().Output, 1e-9, "GDD 7: −15% at the bottom");
            Assert.AreEqual(0.15, M.Bands.Last().Output, 1e-9, "+15% at the top");
            Assert.AreEqual(10, M.Modes.Find(x => x.Mode == HearthMode.Blazing).Value, "GDD 7: the blazing hearth +10");
        }

        // ================================================================== traits

        [Test]
        public void EveryNewDwellerHasATraitAndLevelOne()
        {
            var s = Session();
            foreach (var d in s.State.Dwellers)
            {
                Assert.AreEqual(1, d.Traits.Count, d.Name);
                Assert.GreaterOrEqual(d.Level, 1);
            }
            var x = s.AddDweller("Новичок", new[] { 5, 5, 5, 5, 5 });
            Assert.AreEqual(1, x.Traits.Count);
            Assert.AreEqual(1, x.Level);
            Assert.AreEqual(0, x.Xp);
        }

        [Test]
        public void TraitsAreSpreadAndTheRarityDecidesHowGoodTheyAre()
        {
            var s = Session();
            var count = new Dictionary<Trait, int>();
            int good = 0, n = 900;
            for (int i = 0; i < n; i++)
            {
                var d = s.AddDweller("Беженец", new[] { 5, 5, 5, 5, 5 });
                var t = d.Traits[0];
                count[t] = count.TryGetValue(t, out int c) ? c + 1 : 1;
                if (s.TraitInfo(t).Good)
                    good++;
            }
            Assert.AreEqual(P.Traits.Count, count.Count, "every trait happens");
            foreach (var kv in count)
                Assert.Less(kv.Value, 2.2 * n / P.Traits.Count, kv.Key + " is not much more common than the others");
            Assert.AreEqual(0.5, (double)good / n, 0.06, "a common dweller: good or bad half and half");

            // hired in the tavern as legendary: always a good trait
            Put(s, RoomType.Tavern, 20, 0);
            s.State.HearthLevel = 30;
            for (int i = 0; i < 40; i++)
            {
                var board = s.AdminRefreshBoard(T0);
                board[0].Rarity = Rarity.Legendary;
                s.State.Wallet.Add(s.HireCost(board[0]));
                if (s.PopulationFull())
                    s.State.Dwellers.RemoveAll(d => !d.IsHero && d.Name == "Беженец");
                Assert.AreEqual(Fail.None, s.Hire(0, T0, out var hired));
                Assert.AreEqual(Rarity.Legendary, s.RarityOf(hired.Id));
                Assert.IsTrue(s.TraitInfo(hired.Traits[0]).Good, "a legendary recruit: " + hired.Traits[0]);
            }
        }

        [Test]
        public void WorkTraitsChangeTheOutput()
        {
            var s = Session();
            var mine = s.State.Rooms.First(r => r.Type == RoomType.CoalMine);
            var miner = s.FindDweller(mine.Workers[0]);
            miner.Traits = new List<Trait> { Trait.Hardy };
            double plain = s.RoomOutputPerHour(mine);
            miner.Traits = new List<Trait> { Trait.Hardworking };
            Assert.AreEqual(plain * 1.1, s.RoomOutputPerHour(mine), 1e-9, "«Трудяга» +10%");
            miner.Traits = new List<Trait> { Trait.Lazy };
            Assert.AreEqual(plain * 0.9, s.RoomOutputPerHour(mine), 1e-9, "«Лентяй» −10%");
            miner.Traits = new List<Trait> { Trait.Coward };
            Assert.AreEqual(plain * 1.1, s.RoomOutputPerHour(mine), 1e-9, "«Трус»: +10% in the rear");
            miner.Traits = new List<Trait> { Trait.Hardworking, Trait.Coward };
            Assert.AreEqual(plain * 1.2, s.RoomOutputPerHour(mine), 1e-9, "two traits add up");
        }

        [Test]
        public void AHardyWorkerKeepsWorkingInTheCold()
        {
            var s = Session();
            var mine = s.State.Rooms.First(r => r.Type == RoomType.CoalMine);
            var miner = s.FindDweller(mine.Workers[0]);
            miner.Traits = new List<Trait> { Trait.Lazy };
            double lazyWarm = s.RoomOutputPerHour(mine);
            miner.Traits = new List<Trait> { Trait.Hardworking };
            double warm = s.RoomOutputPerHour(mine);
            s.State.OutOfCoal = true;
            Assert.AreEqual(warm * s.Balance.Warmth.FrozenOutput, s.RoomOutputPerHour(mine), 1e-9, "frozen: half");
            miner.Traits = new List<Trait> { Trait.Hardy };
            Assert.AreEqual(warm / 1.1 * s.TraitInfo(Trait.Hardy).Cold, s.RoomOutputPerHour(mine), 1e-9, "«Закалённый»: 80%");
            miner.Traits = new List<Trait> { Trait.Lazy };
            Assert.AreEqual(lazyWarm * s.Balance.Warmth.FrozenOutput, s.RoomOutputPerHour(mine), 1e-9);
        }

        [Test]
        public void BraveAndCowardlyHeroesInBattle()
        {
            var s = Session();
            var hero = s.Squad().First();
            hero.Traits = new List<Trait> { Trait.Hardy };
            var plain = s.HallsHeroMult(hero.Id);
            var before = s.MissionSetup(0, 1).Heroes.First(h => h.Id == hero.Id);
            hero.Traits = new List<Trait> { Trait.Brave };
            double brave = 1 + s.TraitInfo(Trait.Brave).Battle;
            Assert.Greater(brave, 1);
            Assert.AreEqual(plain.attack * brave, s.HallsHeroMult(hero.Id).attack, 1e-9, "«Храбрец»: attack");
            Assert.AreEqual(plain.hp * brave, s.HallsHeroMult(hero.Id).hp, 1e-9, "and health");
            var inBattle = s.MissionSetup(0, 1).Heroes.First(h => h.Id == hero.Id);
            Assert.AreEqual(before.AttackMult * brave, inBattle.AttackMult, 1e-9, "the battle sees it");
            Assert.AreEqual(before.HpMult * brave, inBattle.HpMult, 1e-9);
            hero.Traits = new List<Trait> { Trait.Coward };
            Assert.AreEqual(plain.attack * (1 + s.TraitInfo(Trait.Coward).Battle), s.HallsHeroMult(hero.Id).attack, 1e-9, "«Трус»: weaker in battle");
        }

        [Test]
        public void ASicklyDwellerCatchesTheIllnessTwiceAsOften()
        {
            var s = Session();
            var d = Plain(s, "Хилый", 10, Trait.Sickly);
            Assert.AreEqual(2, s.IllnessMult(d.Id), 1e-9);
            d.Traits = new List<Trait> { Trait.Hardy };
            Assert.AreEqual(1, s.IllnessMult(d.Id), 1e-9);
            // over many returns from the fog the sickly fall ill about twice as often
            int sickly = 0, hardy = 0;
            for (int seed = 1; seed <= 400; seed++)
            {
                foreach (var trait in new[] { Trait.Sickly, Trait.Hardy })
                {
                    var t = TestData.NewSession();
                    var x = t.AddDweller("Ходок", new[] { 9, 9, 9, 9, 9 });
                    x.Traits = new List<Trait> { trait };
                    var e = new Expedition { Id = t.State.NextExpeditionId++, Team = new List<int> { x.Id }, Start = T0, End = T0 + 60, Seed = seed, HearthLevel = 1 };
                    x.RouteId = e.Id;
                    t.State.Expeditions.Add(e);
                    t.Advance(T0 + 60);
                    if (t.IsSick(x.Id, T0 + 60))
                    {
                        if (trait == Trait.Sickly)
                            sickly++;
                        else
                            hardy++;
                    }
                }
            }
            Assert.Greater(hardy, 20);
            Assert.AreEqual(2.0, (double)sickly / hardy, 0.6);
        }

        // ================================================================== levels and experience

        [Test]
        public void AnHourOfWorkIsOnePointOfExperience()
        {
            var s = Session();
            var mine = s.State.Rooms.First(r => r.Type == RoomType.CoalMine);
            var miner = s.FindDweller(mine.Workers[0]);
            miner.Traits = new List<Trait> { Trait.Hardy };
            var idle = Plain(s, "Без дела");
            Assert.AreEqual(P.XpWork, s.XpPerHour(miner), 1e-9);
            Assert.AreEqual(0, s.XpPerHour(idle), "no work, no experience");
            s.Advance(T0 + Hour);
            Assert.AreEqual(P.XpWork, miner.Xp + (miner.Level > 1 ? s.XpToNext(1) : 0), 1e-6);
            Assert.AreEqual(0, idle.Xp);
            miner.Traits = new List<Trait> { Trait.Keeper };
            Assert.AreEqual(P.XpWork * 1.25, s.XpPerHour(miner), 1e-9, "«Помнит Стражей» +25%");
        }

        [Test]
        public void TrainingCountsDoubleAndTheSickLearnAtHalf()
        {
            var s = Session();
            s.State.HearthLevel = 10;
            foreach (ResourceType r in Enum.GetValues(typeof(ResourceType)))
                s.State.Wallet[r] = 1_000_000;
            var hall = Put(s, RoomType.TrainingHall, 5, 0);
            var d = Plain(s, "Ученик");
            Assert.AreEqual(Fail.None, s.StartTraining(d.Id, Stat.Strength, T0));
            Assert.AreEqual(hall.Id, d.RoomId);
            Assert.AreEqual(P.XpWork * P.XpTraining, s.XpPerHour(d), 1e-9);
            var mine = s.State.Rooms.First(r => r.Type == RoomType.CoalMine);
            var miner = s.FindDweller(mine.Workers[0]);
            miner.Traits = new List<Trait> { Trait.Hardy };
            s.State.Halls.SickUntil[miner.Id] = T0 + 10 * Hour;
            Assert.AreEqual(P.XpWork * P.XpSick, s.XpPerHour(miner), 1e-9);
            miner.RouteId = 99;
            Assert.AreEqual(0, s.XpPerHour(miner), "away on a route");
        }

        [Test]
        public void ExperienceDoesNotDependOnHowOftenTheGameTicks()
        {
            var a = Session();
            var b = Session();
            for (long t = 60; t <= 30 * Hour; t += 60)
                a.Advance(T0 + t);
            b.Advance(T0 + 30 * Hour);
            for (int i = 0; i < a.State.Dwellers.Count; i++)
            {
                Assert.AreEqual(a.State.Dwellers[i].Level, b.State.Dwellers[i].Level);
                Assert.AreEqual(a.State.Dwellers[i].Xp, b.State.Dwellers[i].Xp, 1e-6);
            }
            Assert.AreEqual(a.Morale, b.Morale, 1e-6, "and neither does the spirit");
        }

        [Test]
        public void LevelsNeedMoreAndMoreExperienceUpToSixty()
        {
            var s = Session();
            for (int L = 2; L < 60; L++)
                Assert.Greater(s.XpToNext(L), s.XpToNext(L - 1));
            Assert.AreEqual(0, s.XpToNext(60), "the top");
            double month = 0;
            int level = 1;
            while (month + s.XpToNext(level) <= 30 * 24 && level < 60)
                month += s.XpToNext(level++);
            Assert.That(level, Is.InRange(15, 25), "a month of steady work");
            var d = Plain(s, "Ветеран");
            Assert.AreEqual(60, s.AdminAddLevels(d.Id, 100));
            Assert.AreEqual(0, s.AdminGiveXp(d.Id, 1000), "no level above 60");
            Assert.AreEqual(0, d.Xp);
        }

        [Test]
        public void ALevelAddsStatPointsByTheRarity()
        {
            var s = Session();
            s.State.HearthLevel = 30;
            var mine = s.State.Rooms.First(r => r.Type == RoomType.CoalMine);
            foreach (int id in mine.Workers.ToList())
                s.Unassign(id);
            var common = Plain(s, "Обычный", 10);
            var legend = Plain(s, "Легенда", 10);
            s.State.Halls.Rarity[legend.Id] = Rarity.Legendary;
            s.Assign(common.Id, mine.Id);
            s.Assign(legend.Id, mine.Id);
            s.AdminAddLevels(common.Id, 59);
            s.AdminAddLevels(legend.Id, 59);
            int commonGrowth = (int)Math.Floor(60 * s.RarityInfo(Rarity.Common).Growth + 1e-9);
            Assert.AreEqual(10 + commonGrowth, common.StatOf(Stat.Endurance), "the coal mine's stat grows");
            Assert.AreEqual(10, common.StatOf(Stat.Strength), "the others stay");
            int legendGrowth = (int)Math.Floor(60 * s.RarityInfo(Rarity.Legendary).Growth + 1e-9) - (int)Math.Floor(s.RarityInfo(Rarity.Legendary).Growth + 1e-9);
            Assert.AreEqual(10 + legendGrowth, legend.StatOf(Stat.Endurance), "a legendary one: most levels bring a point");
            Assert.Greater(legend.StatOf(Stat.Endurance), common.StatOf(Stat.Endurance));
        }

        [Test]
        public void LevelThirtyBringsASecondGoodTrait()
        {
            var s = Session();
            var d = Plain(s, "Опытный", 10, Trait.Gloomy);
            s.AdminAddLevels(d.Id, 28);
            Assert.AreEqual(29, d.Level);
            Assert.AreEqual(1, d.Traits.Count);
            s.AdminAddLevels(d.Id, 1);
            Assert.AreEqual(2, d.Traits.Count);
            Assert.IsTrue(s.TraitInfo(d.Traits[1]).Good, d.Traits[1].ToString());
            Assert.AreNotEqual(d.Traits[0], d.Traits[1]);
            s.AdminAddLevels(d.Id, 30);
            Assert.AreEqual(2, d.Traits.Count, "only one more");
        }

        [Test]
        public void SecondTraitsAreSpreadToo()
        {
            var s = Session();
            var count = new Dictionary<Trait, int>();
            int n = 300;
            for (int i = 0; i < n; i++)
            {
                var d = s.AddDweller("Ветеран", new[] { 5, 5, 5, 5, 5 });
                s.AdminAddLevels(d.Id, 29);
                var t = d.Traits[1];
                count[t] = count.TryGetValue(t, out int c) ? c + 1 : 1;
            }
            int good = P.Traits.Count(t => t.Good);
            Assert.AreEqual(good, count.Count, "every good trait happens");
            foreach (var kv in count)
                Assert.Less(kv.Value, 2.0 * n / good, kv.Key + " is not much more common than the others");
        }

        [Test]
        public void HeroesLevelUpButTheirStrengthIsTheHeroLevel()
        {
            var s = Session();
            var hero = s.Squad().First();
            var stats = (int[])hero.Stats.Clone();
            s.AdminAddLevels(hero.Id, 40);
            Assert.AreEqual(41, hero.Level);
            CollectionAssert.AreEqual(stats, hero.Stats, "no stat points for heroes");
            Assert.AreEqual(2, hero.Traits.Count, "the second trait comes all the same");
        }

        [Test]
        public void BattlesTeachTheSquad()
        {
            var s = Session();
            var hero = s.Squad().First();
            hero.Traits = new List<Trait> { Trait.Hardy };
            hero.Level = 5;
            hero.Xp = 0;
            // a new fortress is below the first battle's power lock (MODES.md, 1); the lock is not what this test checks
            Assert.AreEqual(Fail.TooWeak, s.StartMission(0, T0, out _));
            s.AdminSetPowerGate(false);
            Assert.AreEqual(Fail.None, s.StartMission(0, T0, out var battle));
            battle.Brain = Brains.Careful();
            s.FinishMission(battle, out var reward);
            Assert.AreEqual(battle.Won ? P.XpWin : P.XpLoss, hero.Xp, 1e-9);
            double after = hero.Xp;
            if (reward.FirstClear)
            {
                Assert.AreEqual(Fail.None, s.RepeatMission(0, T0 + 60, out _));
                Assert.AreEqual(after + P.XpRepeat, hero.Xp, 1e-9, "an automatic repeat teaches a little");
            }
        }

        // ================================================================== the spirit of the fortress

        [Test]
        public void ANewFortressStartsCalmAndFed()
        {
            var s = TestData.NewSession();
            Assert.AreEqual(M.Base + M.Fed, s.Morale, 1e-9);
            Assert.AreEqual(0, s.MoraleBand().Output, "calm: the output as it is");
            Assert.AreEqual(1, s.MoraleOutputMult());
        }

        [Test]
        public void HungerAndColdLowerTheSpirit()
        {
            var s = Session();
            Assert.AreEqual(M.Fed, Factor(s, "Сыты"));
            foreach (var room in s.State.Rooms.Where(r => s.Balance.Room(r.Type).Produces == ResourceType.Food))
            {
                room.Stock = 0;
                foreach (int id in room.Workers.ToList())
                    s.Unassign(id);
            }
            s.State.Wallet[ResourceType.Food] = 0;
            Assert.AreEqual(M.Hunger, Factor(s, "Голод"), "GDD 5: hunger lowers the spirit");
            Assert.AreEqual(0, Factor(s, "Сыты"));
            s.State.Rooms.First(r => r.Type == RoomType.Kitchen).Stock = 50;
            Assert.AreEqual(0, Factor(s, "Голод"), "they eat what waits in the kitchen");
            s.State.OutOfCoal = true;
            Assert.AreEqual(M.NoCoal, Factor(s, "Очаг без угля"));
            Assert.AreEqual(M.Frozen, Factor(s, "Мёрзнут"), 1e-9, "every worker is in a frozen room");
            Assert.Less(s.MoraleTarget(s.State.Time), M.Base);
        }

        [Test]
        public void TheBlazingHearthLiftsTheSpiritAndHealsFaster()
        {
            var s = Session();
            double steady = s.MoraleTarget(s.State.Time), heal = s.HealSpeed();
            s.SetMode(HearthMode.Blazing);
            Assert.AreEqual(steady + 10, s.MoraleTarget(s.State.Time), 1e-9, "GDD 7: +10");
            Assert.Greater(s.HealSpeed(), heal, "and healing is faster");
            s.SetMode(HearthMode.Smoldering);
            Assert.Less(s.MoraleTarget(s.State.Time), steady);
        }

        [Test]
        public void DecorationsTheChapelAndBedsCount()
        {
            var s = Session();
            s.State.HearthLevel = 20;
            int cell = 0;
            foreach (var room in s.State.Rooms.ToList())
                s.State.RoomDecor[room.Id] = "candles";
            Assert.AreEqual(M.Decor * s.State.Rooms.Count, Factor(s, "Украшенные"), 1e-9);
            for (int i = 0; i < 12; i++)
                s.State.RoomDecor[Put(s, RoomType.Warehouse, 1, cell++ % 6, H + 2 + i / 6).Id] = "rug";
            Assert.AreEqual(M.DecorMax, Factor(s, "Украшенные"), 1e-9, "at most");
            var chapel = Put(s, RoomType.Chapel, 12, 0, H - 2);
            Assert.AreEqual(M.ChapelBase + 12 / M.ChapelLevels, Factor(s, "Часовня"), 1e-9);
            chapel.Level = 30;
            Assert.AreEqual(M.ChapelBase + 30 / M.ChapelLevels, Factor(s, "Часовня"), 1e-9);

            // beds: one cell of the living quarters of level 1 holds 6
            int beds = s.Beds();
            Assert.AreEqual(M.BedsBase, beds);
            while (s.State.Dwellers.Count < beds)
                Plain(s, "Жилец");
            Assert.AreEqual(0, Factor(s, "Тесно"));
            Plain(s, "Лишний");
            Plain(s, "Лишняя");
            Assert.AreEqual(2 * M.Crowd, Factor(s, "Тесно"), 1e-9);
            s.State.Rooms.First(r => r.Type == RoomType.Bedrooms).Level = 2;
            Assert.AreEqual(M.BedsBase + M.BedsPerLevel, s.Beds());
            Assert.AreEqual(0, Factor(s, "Тесно"), "a bigger room gives beds");
            for (int i = 0; i < 20; i++)
                Plain(s, "Толпа");
            Assert.AreEqual(M.CrowdMax, Factor(s, "Тесно"), 1e-9, "at most");
        }

        [Test]
        public void TheSickAndTheInjuredWeighOnTheSpirit()
        {
            var s = Session();
            var a = s.State.Dwellers[1];
            var b = s.State.Dwellers[2];
            a.InjuredUntil = T0 + 5 * Hour;
            Assert.AreEqual(M.Patient, Factor(s, "Ранены"), 1e-9);
            s.State.Halls.SickUntil[b.Id] = T0 + 5 * Hour;
            Assert.AreEqual(2 * M.Patient, Factor(s, "Ранены"), 1e-9);
            for (int i = 0; i < 10; i++)
                Plain(s, "Раненый").InjuredUntil = T0 + Hour;
            Assert.AreEqual(M.PatientMax, Factor(s, "Ранены"), 1e-9, "at most");
        }

        [Test]
        public void CheerfulAndGloomyPeople()
        {
            var s = Session();
            foreach (var d in s.State.Dwellers)
                d.Traits = new List<Trait> { Trait.Hardy };
            Assert.AreEqual(0, Factor(s, "Весельчаки") + Factor(s, "Нытики"));
            s.State.Dwellers[0].Traits = new List<Trait> { Trait.Cheerful };
            Assert.AreEqual(s.TraitInfo(Trait.Cheerful).Morale, Factor(s, "Весельчаки"), 1e-9);
            s.State.Dwellers[1].Traits = new List<Trait> { Trait.Gloomy };
            Assert.AreEqual(0, Factor(s, "Весельчаки") + Factor(s, "Нытики"), 1e-9, "one cancels the other");
            for (int i = 0; i < 10; i++)
                Plain(s, "Весёлый", 10, Trait.Cheerful);
            Assert.AreEqual(P.TraitMoraleCap, Factor(s, "Весельчаки"), 1e-9, "at most");
        }

        [Test]
        public void NewsLiftsOrLowersTheSpiritForSomeHours()
        {
            var s = Session();
            double plain = s.MoraleTarget(s.State.Time);
            s.AdminNews(MoraleNews.HearthUp, T0);
            var info = s.NewsInfo(MoraleNews.HearthUp);
            Assert.AreEqual(plain + info.Value, s.MoraleTarget(s.State.Time), 1e-9);
            s.AdminNews(MoraleNews.HearthUp, T0 + Hour);
            Assert.AreEqual(plain + info.Value, s.MoraleTarget(s.State.Time), 1e-9, "the same news does not add up");
            s.AdminNews(MoraleNews.SurgeLost, T0 + Hour);
            Assert.AreEqual(plain + info.Value + s.NewsInfo(MoraleNews.SurgeLost).Value, s.MoraleTarget(s.State.Time), 1e-9);
            s.Advance(T0 + Hour + (long)(info.Hours * Hour));
            Assert.AreEqual(0, s.State.People.News.Count, "the news is old");
            Assert.AreEqual(plain, s.MoraleTarget(s.State.Time), 1e-9);
        }

        [Test]
        public void TheSpiritFollowsItsTarget()
        {
            var s = Session();
            double target = s.MoraleTarget(T0);
            s.AdminSetMorale(target - 30, T0);
            s.Advance(T0 + (long)(M.Hours * Hour));
            Assert.AreEqual(target - 30 * Math.Exp(-1), s.Morale, 1e-6, "63% of the way in 3 hours");
            s.Advance(T0 + 48 * Hour);
            Assert.AreEqual(s.MoraleTarget(s.State.Time), s.Morale, 0.5);
        }

        [Test]
        public void TheBandOfTheSpiritChangesTheOutputUpToFifteenPercent()
        {
            var s = Session();
            var mine = s.State.Rooms.First(r => r.Type == RoomType.CoalMine);
            double calm = s.RoomOutputPerHour(mine);
            foreach (var (morale, mult) in new[] { (5.0, 0.85), (30.0, 0.92), (60.0, 1.0), (85.0, 1.08), (95.0, 1.15) })
            {
                s.AdminSetMorale(morale, T0);
                Assert.AreEqual(mult, s.MoraleOutputMult(), 1e-9, "spirit " + morale);
                Assert.AreEqual(calm * mult, s.RoomOutputPerHour(mine), 1e-9, "spirit " + morale);
            }
        }

        // ================================================================== desertion

        static GameSession Starving()
        {
            var s = Session();
            var kitchen = s.State.Rooms.First(r => r.Type == RoomType.Kitchen);
            foreach (int id in kitchen.Workers.ToList())
                s.Unassign(id);
            kitchen.Stock = 0;
            s.State.Wallet[ResourceType.Food] = 0;
            s.State.FoodDebt = 0;
            return s;
        }

        [Test]
        public void HungerLeadsToDespairAWarningAndThenADeserter()
        {
            var s = Starving();
            Assert.Less(s.MoraleTarget(T0), M.DesertBelow, "hunger alone brings despair");
            var gone = new List<Dweller>();
            s.DwellerDeserted += d => gone.Add(d);
            int before = s.State.Dwellers.Count;
            while (!s.InDespair)
                s.Advance(s.State.Time + 600);
            long despair = s.State.Time;
            Assert.Less(despair - T0, 10 * Hour, "a few hours of hunger");
            Assert.AreEqual(despair + (long)(M.DesertHours * Hour), s.DesertionAt(), "the warning tells when");
            var next = s.NextDeserter();
            Assert.IsFalse(next.IsHero);
            Assert.AreEqual(s.State.Dwellers.Where(d => !d.IsHero).Min(d => d.Level), next.Level, "a newcomer leaves first");
            s.Advance(despair + (long)(M.DesertHours * Hour) - 60);
            Assert.AreEqual(before, s.State.Dwellers.Count, "not before the warning runs out");
            s.Advance(despair + (long)(M.DesertHours * Hour));
            Assert.AreEqual(before - 1, s.State.Dwellers.Count);
            Assert.AreEqual(next.Id, gone.Single().Id);
            Assert.AreEqual(1, s.Deserters.Count);
            Assert.AreEqual(1, s.State.People.DesertedTotal);
            Assert.IsTrue(s.State.People.News.Any(n => n.Id == MoraleNews.Desertion));
            Assert.IsTrue(s.State.Rooms.All(r => !r.Workers.Contains(next.Id)), "out of every room");
            // heroes never desert: at most the others leave, one by one
            s.Advance(s.State.Time + 5 * 24 * Hour);
            Assert.IsTrue(s.State.Dwellers.All(d => d.IsHero));
            Assert.IsTrue(s.State.Dwellers.Count > 0);
        }

        [Test]
        public void ACalmSpiritKeepsEveryone()
        {
            var s = Session();
            s.State.Wallet[ResourceType.Coal] = 100_000;
            s.State.Wallet[ResourceType.Food] = 100_000;
            int before = s.State.Dwellers.Count;
            Hours(s, 72);
            Assert.AreEqual(before, s.State.Dwellers.Count, "nobody left");
            Assert.AreEqual(0, s.DesertionAt());
            Assert.IsFalse(s.InDespair);
        }

        [Test]
        public void APlayerBackFromAbsenceIsWarnedBeforeAnyoneLeaves()
        {
            // three days away: the coal and the food run out, but nobody leaves while the game is closed
            var s = Session();
            int before = s.State.Dwellers.Count;
            var summary = s.ReturnAfterAbsence(T0 + 3 * 24 * Hour);
            Assert.IsTrue(s.InDespair, string.Join("; ", s.MoraleFactors(s.State.Time).Select(f => f.Name)));
            Assert.AreEqual(before, s.State.Dwellers.Count);
            Assert.AreEqual(s.State.Time + (long)(M.DesertHours * Hour), s.DesertionAt(), "the hours of the warning start now");
            Assert.IsNotNull(summary);
        }

        [Test]
        public void ADeserterCanBeCalledBackWhenTheSpiritIsCalm()
        {
            var s = Starving();
            s.AdminSetMorale(5, T0);
            var gone = s.AdminDesertionCheck(T0);
            Assert.IsNotNull(gone);
            int level = gone.Level;
            var traits = gone.Traits.ToList();
            Assert.AreEqual(Fail.RulesNotMet, s.Recall(0, T0, out _), "not while the fortress despairs");
            s.State.Wallet[ResourceType.Food] = 10_000;
            s.AdminSetMorale(70, T0 + 60);
            var cost = s.RecallCost(s.Deserters[0]);
            s.State.Wallet[ResourceType.Silver] = 0;
            if (cost[ResourceType.Silver] > 0)
                Assert.AreEqual(Fail.NotEnough, s.Recall(0, T0 + 60, out _));
            s.State.Wallet.Add(cost);
            Assert.AreEqual(Fail.None, s.Recall(0, T0 + 60, out var back));
            Assert.AreEqual(gone.Id, back.Id);
            Assert.AreEqual(level, back.Level, "with the level");
            CollectionAssert.AreEqual(traits, back.Traits, "and the traits");
            Assert.AreEqual(-1, back.RoomId);
            Assert.AreEqual(0, s.Deserters.Count);
            Assert.AreEqual(1, s.State.People.Recalled);
        }

        [Test]
        public void DesertersWaitInTheFogForThreeDays()
        {
            var s = Starving();
            s.AdminSetMorale(5, T0);
            Assert.IsNotNull(s.AdminDesertionCheck(T0));
            s.State.Wallet[ResourceType.Food] = 1_000_000;
            s.Advance(T0 + (long)(M.DeserterDays * 24 * Hour) - 60);
            Assert.AreEqual(1, s.Deserters.Count);
            s.Advance(T0 + (long)(M.DeserterDays * 24 * Hour));
            Assert.AreEqual(0, s.Deserters.Count, "gone for good");
        }

        // ================================================================== saves

        [Test]
        public void ASaveKeepsLevelsTraitsTheSpiritAndTheDeserters()
        {
            var s = Starving();
            s.AdminSetMorale(5, T0);
            s.AdminDesertionCheck(T0);
            var d = s.State.Dwellers.First();
            s.AdminAddLevels(d.Id, 12);
            s.AdminGiveXp(d.Id, 3);
            var copy = new GameSession(s.Balance, SaveSystem.FromJson(SaveSystem.ToJson(s.State)));
            var c = copy.FindDweller(d.Id);
            Assert.AreEqual(d.Level, c.Level);
            Assert.AreEqual(d.Xp, c.Xp, 1e-9);
            CollectionAssert.AreEqual(d.Traits, c.Traits);
            Assert.AreEqual(s.Morale, copy.Morale, 1e-9);
            Assert.AreEqual(1, copy.Deserters.Count);
            Assert.AreEqual(s.Deserters[0].Dweller.Name, copy.Deserters[0].Dweller.Name);
        }

        [Test]
        public void AnOldSaveLoadsWithLevelsTraitsAndACalmSpirit()
        {
            var s = Session();
            s.Advance(T0 + 5 * Hour);
            var json = JObject.Parse(JsonConvert.SerializeObject(s.State));
            json.Remove("People");
            foreach (var d in (JArray)json["Dwellers"])
            {
                ((JObject)d).Remove("Level");
                ((JObject)d).Remove("Xp");
                ((JObject)d).Remove("Traits");
            }
            var state = JsonConvert.DeserializeObject<GameState>(json.ToString());
            var old = new GameSession(s.Balance, state);
            foreach (var d in old.State.Dwellers)
            {
                Assert.AreEqual(1, d.Level, d.Name);
                Assert.AreEqual(1, d.Traits.Count, d.Name);
                Assert.AreEqual(s.FindDweller(d.Id).Traits[0], d.Traits[0], "the same trait as a new dweller of that id would get");
            }
            Assert.AreEqual(M.Base + M.Fed, old.Morale, 1e-9);
            old.Advance(T0 + 6 * Hour);
            Assert.AreEqual(0, old.State.People.DesertedTotal);
            Assert.Greater(old.State.Dwellers.Sum(d => d.Xp + d.Level), old.State.Dwellers.Count, "work counts from the load on");
        }

        // ================================================================== the developer's menu, page «Жители»

        [Test]
        public void AdminSetsTheSpiritWithinItsBounds()
        {
            var s = Session();
            Assert.AreEqual(100, s.AdminSetMorale(150, T0));
            Assert.AreEqual(0, s.AdminSetMorale(-5, T0));
            Assert.IsTrue(s.InDespair);
            Assert.Greater(s.DesertionAt(), 0, "the warning starts");
            Assert.AreEqual(55, s.AdminSetMorale(55, T0));
            Assert.AreEqual(0, s.DesertionAt());
        }

        [Test]
        public void AdminGivesExperienceAndLevels()
        {
            var s = Session();
            var d = Plain(s, "Ученик");
            var levels = new List<int>();
            s.DwellerLevelUp += x => levels.Add(x.Level);
            Assert.AreEqual(1, s.AdminGiveXp(d.Id, s.XpToNext(1)));
            Assert.AreEqual(2, d.Level);
            Assert.AreEqual(12, s.AdminAddLevels(d.Id, 10));
            CollectionAssert.AreEqual(Enumerable.Range(2, 11).ToList(), levels, "every level is told");
            Assert.AreEqual(0, s.AdminGiveXp(-5, 100), "nobody");
        }

        [Test]
        public void AdminAddsAndRemovesTraits()
        {
            var s = Session();
            var d = Plain(s, "Новичок", 10, Trait.Lazy);
            Assert.IsTrue(s.AdminAddTrait(d.Id, Trait.Brave));
            Assert.IsFalse(s.AdminAddTrait(d.Id, Trait.Brave), "no twice");
            Assert.IsTrue(s.AdminAddTrait(d.Id, Trait.Keeper));
            Assert.IsFalse(s.AdminAddTrait(d.Id, Trait.Cheerful), "three at most");
            CollectionAssert.AreEqual(new[] { Trait.Lazy, Trait.Brave, Trait.Keeper }, d.Traits);
            Assert.IsTrue(s.AdminRemoveTrait(d.Id, Trait.Lazy));
            Assert.IsTrue(s.AdminRemoveTrait(d.Id, Trait.Brave));
            Assert.IsFalse(s.AdminRemoveTrait(d.Id, Trait.Keeper), "one stays");
            Assert.AreEqual(Trait.Hardworking, s.AdminNextTrait(s.TraitList.Last().Id), "the picker goes round");
        }

        [Test]
        public void AdminDesertionCheckOnlyInDespair()
        {
            var s = Session();
            Assert.IsNull(s.AdminDesertionCheck(T0), "a calm fortress keeps everyone");
            s.AdminSetMorale(3, T0);
            var gone = s.AdminDesertionCheck(T0);
            Assert.IsNotNull(gone);
            Assert.IsFalse(gone.IsHero);
            Assert.IsNull(s.FindDweller(gone.Id));
            Assert.AreEqual(1, s.AdminRecallAll(T0));
            Assert.IsNotNull(s.FindDweller(gone.Id), "back for free");
            Assert.AreEqual(0, s.Deserters.Count);
        }

        // ================================================================== the player bot

        [Test]
        public void TheBotKeepsItsFortressInGoodSpirit()
        {
            var s = TestData.NewSession();
            s.Combat = TestData.Combat();
            var bot = new PlayerBot(s);
            var spirit = new List<double>();
            bot.PlayDays(TestData.Start, 20, day => spirit.Add(s.Morale));
            TestContext.WriteLine("spirit by day: " + string.Join(" ", spirit.Select(x => x.ToString("0"))) + " | lowest " + bot.LowestSpirit.ToString("0") +
                                  " | hungry turns " + bot.HungryTurns + " | blazing turns " + bot.BlazingTurns + " | smoulder " + bot.SmolderTurns + " | lowest at " + bot.LowestWhy);
            Assert.AreEqual(0, s.State.People.DesertedTotal, "nobody deserts a cared-for fortress");
            Assert.IsTrue(spirit.All(x => x >= 40), "calm at the end of every day");
            Assert.Greater(bot.LowestSpirit, M.DesertBelow, "never in despair");
            Assert.Greater(s.State.Dwellers.Max(d => d.Level), 10, "the people grow with their work");
            Assert.IsTrue(s.State.Dwellers.All(d => d.Traits.Count >= 1));
        }
    }
}

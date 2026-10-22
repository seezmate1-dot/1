using System.Collections.Generic;
using NUnit.Framework;
using SunnyValley.Core.Meta;
using static SunnyValley.Core.Tests.MetaTestKit;

namespace SunnyValley.Core.Tests
{
    public class MetaMatch3Tests
    {
        static LevelOutcome Play(MetaGame g, long now, bool won, int coins = 0, Dictionary<string, int> crops = null)
        {
            int level = g.NextLevel;
            Assert.That(g.StartLevel(now, level).Ok, Is.True);
            return g.FinishLevel(now, new LevelResult { Level = level, Won = won, Stars = won ? 2 : 0, CoinsEarned = coins, MovesLeft = 3, Crops = crops ?? new Dictionary<string, int>() });
        }

        [Test]
        public void NewGameStartsWithFullLivesAndStartingPurse()
        {
            var g = NewGame();
            Assert.That(g.Lives(T0), Is.EqualTo(5));
            Assert.That(g.Coins, Is.EqualTo(Data.Balance.Start.Coins));
            Assert.That(g.Crystals, Is.EqualTo(Data.Balance.Start.Crystals));
            Assert.That(g.BoosterCount(Boosters.Bomb), Is.EqualTo(1));
            Assert.That(g.NextLevel, Is.EqualTo(1));
            Assert.That(g.Level, Is.EqualTo(1));
            Assert.That(g.FarmOpen, Is.False);
        }

        [Test]
        public void WinPaysOneStarAndCoinsAndAdvances()
        {
            var g = NewGame();
            long coins = g.Coins;
            var o = Play(g, T0, true, coins: 30);
            Assert.That(o.Ok && o.Won, Is.True);
            Assert.That(o.StarsGained, Is.EqualTo(1));
            Assert.That(g.Stars, Is.EqualTo(1), "1 star per won level whatever the rating");
            Assert.That(g.Coins, Is.EqualTo(coins + Data.Balance.Match3.WinCoins + 30));
            Assert.That(g.NextLevel, Is.EqualTo(2));
            Assert.That(g.Lives(T0), Is.EqualTo(5), "a win costs no life");
        }

        [Test]
        public void LossTakesALifeAndLivesComeBackEvery30Minutes()
        {
            var g = NewGame();
            Play(g, T0, false);
            Play(g, T0, false);
            Assert.That(g.Lives(T0), Is.EqualTo(3));
            Assert.That(g.NextLevel, Is.EqualTo(1), "a lost level is replayed");
            Assert.That(g.NextLifeIn(T0), Is.EqualTo(30 * Minute));
            Assert.That(g.Lives(T0 + 29 * Minute), Is.EqualTo(3));
            Assert.That(g.Lives(T0 + 30 * Minute), Is.EqualTo(4));
            Assert.That(g.LivesFullIn(T0 + 30 * Minute), Is.EqualTo(30 * Minute));
            Assert.That(g.Lives(T0 + 10 * Hour), Is.EqualTo(5), "never above the maximum");
        }

        [Test]
        public void NoLivesNoPlayUntilOneIsBack()
        {
            var g = NewGame();
            for (int i = 0; i < 5; i++) Play(g, T0, false);
            Assert.That(g.Lives(T0), Is.EqualTo(0));
            Assert.That(g.StartLevel(T0, 1).Error, Is.EqualTo(MetaError.NoLives));
            Assert.That(g.StartLevel(T0 + 30 * Minute, 1).Ok, Is.True);
        }

        [Test]
        public void InfiniteLivesLetYouLoseForFree()
        {
            var g = NewGame();
            g.Grant(T0, new Reward { InfiniteLivesMinutes = 60 });
            for (int i = 0; i < 8; i++) Play(g, T0 + i * Minute, false);
            Assert.That(g.Lives(T0 + 10 * Minute), Is.EqualTo(5));
            Assert.That(g.InfiniteLivesLeft(T0), Is.EqualTo(60 * Minute));
            g.Grant(T0, new Reward { InfiniteLivesMinutes = 30 });
            Assert.That(g.InfiniteLivesLeft(T0), Is.EqualTo(90 * Minute), "timers stack");
            Play(g, T0 + 91 * Minute, false);
            Assert.That(g.Lives(T0 + 91 * Minute), Is.EqualTo(4));
        }

        [Test]
        public void RefillLivesForCrystals()
        {
            var g = NewGame();
            Assert.That(g.RefillLives(T0).Error, Is.EqualTo(MetaError.AlreadyDone));
            Play(g, T0, false);
            int crystals = g.Crystals;
            Assert.That(g.RefillLives(T0).Ok, Is.True);
            Assert.That(g.Lives(T0), Is.EqualTo(5));
            Assert.That(g.Crystals, Is.EqualTo(crystals - Data.Balance.Lives.RefillCrystals));
        }

        [Test]
        public void ExtraMovesLadder900_1900_2900()
        {
            var g = NewGame();
            g.State.Wallet.Coins = 10000;
            Assert.That(g.BuyExtraMoves(T0).Error, Is.EqualTo(MetaError.NoLevelInProgress));
            g.StartLevel(T0, 1);
            Assert.That(g.ExtraMovesPrice, Is.EqualTo(900));
            Assert.That(g.BuyExtraMoves(T0).Ok, Is.True);
            Assert.That(g.ExtraMovesPrice, Is.EqualTo(1900));
            g.BuyExtraMoves(T0);
            Assert.That(g.ExtraMovesPrice, Is.EqualTo(2900));
            g.BuyExtraMoves(T0);
            Assert.That(g.ExtraMovesPrice, Is.EqualTo(2900), "the last price repeats");
            Assert.That(g.Coins, Is.EqualTo(10000 - 900 - 1900 - 2900));
            Assert.That(g.ExtraMovesCount, Is.EqualTo(5));
            g.FinishLevel(T0, new LevelResult { Level = 1, Won = true, Stars = 1 });
            g.StartLevel(T0, 2);
            Assert.That(g.ExtraMovesPrice, Is.EqualTo(900), "the ladder restarts each attempt");
            g.State.Wallet.Coins = 100;
            Assert.That(g.BuyExtraMoves(T0).Error, Is.EqualTo(MetaError.NotEnoughCoins));
        }

        [Test]
        public void PreLevelBoostersAreTakenOnStart()
        {
            var g = NewGame();
            Assert.That(g.BoosterCount(Boosters.Bomb), Is.EqualTo(1));
            Assert.That(g.StartLevel(T0, 1, new[] { Boosters.Bomb, Boosters.Bomb }).Error, Is.EqualTo(MetaError.NoBooster));
            Assert.That(g.StartLevel(T0, 1, new[] { Boosters.Shovel }).Error, Is.EqualTo(MetaError.NoBooster), "Shovel is an in-level booster");
            Assert.That(g.ActiveLevel, Is.EqualTo(0), "a refused start changes nothing");
            Assert.That(g.StartLevel(T0, 1, new[] { Boosters.Bomb, Boosters.Line }).Ok, Is.True);
            Assert.That(g.BoosterCount(Boosters.Bomb), Is.EqualTo(0));
            Assert.That(g.BoosterCount(Boosters.Line), Is.EqualTo(0));
        }

        [Test]
        public void InLevelBoostersAndShop()
        {
            var g = NewGame();
            Assert.That(g.UseBooster(T0, Boosters.Shovel).Error, Is.EqualTo(MetaError.NoLevelInProgress));
            g.StartLevel(T0, 1);
            Assert.That(g.UseBooster(T0, Boosters.Shovel).Ok, Is.True);
            Assert.That(g.UseBooster(T0, Boosters.Rake).Error, Is.EqualTo(MetaError.NoBooster));
            Assert.That(g.UseBooster(T0, Boosters.Bomb).Error, Is.EqualTo(MetaError.NoBooster), "pre-level boosters are not used mid-level");
            g.State.Wallet.Coins = 1000;
            Assert.That(g.BuyBooster(T0, Boosters.Rake).Ok, Is.True);
            Assert.That(g.Coins, Is.EqualTo(1000 - Data.BoosterDefs[Boosters.Rake].Coins));
            Assert.That(g.UseBooster(T0, Boosters.Rake).Ok, Is.True);
            Assert.That(g.BuyBooster(T0, "Laser").Error, Is.EqualTo(MetaError.UnknownId));
        }

        [Test]
        public void WrongLevelAndDoubleStartAreRefused()
        {
            var g = NewGame();
            Assert.That(g.StartLevel(T0, 5).Error, Is.EqualTo(MetaError.WrongLevel));
            Assert.That(g.FinishLevel(T0, new LevelResult { Level = 1, Won = true }).Error, Is.EqualTo(MetaError.NoLevelInProgress));
            g.StartLevel(T0, 1);
            Assert.That(g.StartLevel(T0, 1).Error, Is.EqualTo(MetaError.LevelInProgress));
            Assert.That(g.FinishLevel(T0, new LevelResult { Level = 2, Won = true }).Error, Is.EqualTo(MetaError.WrongLevel));
            var o = g.AbandonLevel(T0);
            Assert.That(o.Ok && !o.Won, Is.True);
            Assert.That(g.Lives(T0), Is.EqualTo(4), "leaving a level counts as a loss");
        }

        [Test]
        public void MilestonesAndBoosterGifts()
        {
            var g = NewGame();
            var cfg = Data.Balance.Match3;
            int crystals = g.Crystals;
            int boostersBefore = 0;
            foreach (var b in Boosters.All) boostersBefore += g.BoosterCount(b);
            for (int i = 0; i < cfg.MilestoneEvery; i++) Play(g, T0, true);
            Assert.That(g.Crystals, Is.EqualTo(crystals + cfg.MilestoneReward.Crystals));
            Assert.That(g.InfiniteLives(T0), Is.EqualTo(cfg.MilestoneReward.InfiniteLivesMinutes > 0));
            int boostersAfter = 0;
            foreach (var b in Boosters.All) boostersAfter += g.BoosterCount(b);
            Assert.That(boostersAfter - boostersBefore, Is.EqualTo(cfg.MilestoneEvery / cfg.BoosterEvery));
            var events = g.TakeEvents();
            Assert.That(events.Exists(e => e.Kind == MetaEventKind.BoosterFound));
        }

        [Test]
        public void BoardCropsAreSoldBeforeTheFarmAndStoredAfter()
        {
            var g = NewGame();
            long coins = g.Coins;
            Play(g, T0, true, 0, new Dictionary<string, int> { ["tomato"] = 4 });
            Assert.That(g.Coins, Is.EqualTo(coins + Data.Balance.Match3.WinCoins + 4 * Data.Items["tomato"].Sell));
            Assert.That(g.BarnCount("tomato"), Is.EqualTo(0));

            var f = FarmGame();
            int level = f.NextLevel;
            f.StartLevel(T0, level);
            var o = f.FinishLevel(T0, new LevelResult { Level = level, Won = true, Crops = new Dictionary<string, int> { ["tomato"] = 4, ["corn"] = 2, ["nonsense"] = 9 } });
            Assert.That(f.BarnCount("tomato"), Is.EqualTo(4));
            Assert.That(o.Reward.Items["corn"], Is.EqualTo(2));
        }

        [Test]
        public void BoardCropsOverflowingTheBarnAreSold()
        {
            var f = FarmGame();
            Give(f, "wheat", f.BarnFree - 1);
            long coins = f.Coins;
            int level = f.NextLevel;
            f.StartLevel(T0, level);
            var o = f.FinishLevel(T0, new LevelResult { Level = level, Won = true, Crops = new Dictionary<string, int> { ["tomato"] = 3 } });
            Assert.That(f.BarnFree, Is.EqualTo(0));
            Assert.That(o.SoldOverflowCoins, Is.EqualTo(2 * Data.Items["tomato"].Sell));
            Assert.That(f.Coins, Is.GreaterThanOrEqualTo(coins + o.SoldOverflowCoins));
        }
    }
}

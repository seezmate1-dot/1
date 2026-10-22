using System;
using System.Collections.Generic;

namespace SunnyValley.Core.Meta
{
    /// <summary>What the match-3 core reports when a level ends (the meta turns it into rewards).</summary>
    public sealed class LevelResult
    {
        public int Level;
        public bool Won;
        public int MovesLeft;
        /// <summary>Rating 1–3 (for the level map); the meta always pays 1 star per won level (GDD 3).</summary>
        public int Stars;
        /// <summary>Coins the board paid (leftover moves etc.).</summary>
        public int CoinsEarned;
        /// <summary>Crops collected from the board beds: item id → count (tomato, corn, apple…).</summary>
        public Dictionary<string, int> Crops = new Dictionary<string, int>();
    }

    public sealed partial class MetaGame
    {
        // ------------------------------------------------------------------ lives (GDD 4.6)

        void RefreshLives(long now)
        {
            var l = State.Lives;
            int max = B.Lives.Max;
            if (l.Count >= max)
            {
                l.RegenFrom = now;
                return;
            }
            long per = Math.Max(1, B.Lives.RegenSeconds);
            if (now <= l.RegenFrom) return;
            long n = (now - l.RegenFrom) / per;
            if (n <= 0) return;
            if (l.Count + n >= max)
            {
                l.Count = max;
                l.RegenFrom = now;
            }
            else
            {
                l.Count += (int)n;
                l.RegenFrom += n * per;
            }
        }

        public int Lives(long now)
        {
            RefreshLives(now);
            return State.Lives.Count;
        }

        public bool InfiniteLives(long now) => State.Lives.InfiniteUntil > now;
        public long InfiniteLivesLeft(long now) => Left(now, State.Lives.InfiniteUntil);
        public bool CanPlay(long now) => InfiniteLives(now) || Lives(now) > 0;

        /// <summary>Seconds until the next life (0 when full).</summary>
        public long NextLifeIn(long now)
        {
            RefreshLives(now);
            if (State.Lives.Count >= B.Lives.Max) return 0;
            return Left(now, State.Lives.RegenFrom + B.Lives.RegenSeconds);
        }

        /// <summary>Seconds until all lives are back (0 when full).</summary>
        public long LivesFullIn(long now)
        {
            RefreshLives(now);
            int missing = B.Lives.Max - State.Lives.Count;
            if (missing <= 0) return 0;
            return Left(now, State.Lives.RegenFrom + (long)missing * B.Lives.RegenSeconds);
        }

        public ActionResult RefillLives(long now)
        {
            if (Lives(now) >= B.Lives.Max) return ActionResult.Fail(MetaError.AlreadyDone);
            if (!SpendCrystals(B.Lives.RefillCrystals)) return ActionResult.Fail(MetaError.NotEnoughCrystals);
            State.Lives.Count = B.Lives.Max;
            State.Lives.RegenFrom = now;
            return ActionResult.Success(new Reward { Lives = B.Lives.Max });
        }

        void LoseLife(long now)
        {
            if (InfiniteLives(now)) return;
            RefreshLives(now);
            if (State.Lives.Count <= 0) return;
            if (State.Lives.Count >= B.Lives.Max) State.Lives.RegenFrom = now;
            State.Lives.Count--;
        }

        // ------------------------------------------------------------------ the match-3 bridge

        public int NextLevel => State.Match3.NextLevel;
        public int ActiveLevel => State.Match3.Active;

        /// <summary>
        /// Starts the next level: needs a life (or infinite lives). The chosen pre-level boosters
        /// (Line / Bomb / Rainbow) are taken from the inventory; the board places them.
        /// </summary>
        public ActionResult StartLevel(long now, int level, IList<string> preBoosters = null)
        {
            var m = State.Match3;
            if (m.Active != 0) return ActionResult.Fail(MetaError.LevelInProgress);
            if (level != m.NextLevel) return ActionResult.Fail(MetaError.WrongLevel);
            if (!CanPlay(now)) return ActionResult.Fail(MetaError.NoLives);
            var need = new SortedDictionary<string, int>();
            if (preBoosters != null)
                foreach (var b in preBoosters)
                {
                    if (!Boosters.IsPreLevel(b)) return ActionResult.Fail(MetaError.NoBooster);
                    Dict.Add(need, b, 1);
                }
            foreach (var kv in need)
                if (BoosterCount(kv.Key) < kv.Value) return ActionResult.Fail(MetaError.NoBooster);
            foreach (var kv in need)
            {
                Dict.Add(State.Boosters, kv.Key, -kv.Value);
                Progress(now, DailyKinds.UseBoosters, kv.Value);
            }
            m.Active = level;
            m.ExtraMovesBought = 0;
            Stat("levels_started");
            return ActionResult.Success();
        }

        /// <summary>Price of the next +5 moves in this attempt: 900, 1900, 2900, 2900… (GDD 4.7).</summary>
        public int ExtraMovesPrice
        {
            get
            {
                var p = B.Match3.ExtraMovesPrices;
                return p[Math.Min(p.Count - 1, State.Match3.ExtraMovesBought)];
            }
        }

        public int ExtraMovesCount => B.Match3.ExtraMoves;

        /// <summary>Buys +5 moves for the level in progress. Never forced: the UI offers it, the player decides.</summary>
        public ActionResult BuyExtraMoves(long now)
        {
            if (State.Match3.Active == 0) return ActionResult.Fail(MetaError.NoLevelInProgress);
            if (!SpendCoins(ExtraMovesPrice)) return ActionResult.Fail(MetaError.NotEnoughCoins);
            State.Match3.ExtraMovesBought++;
            Stat("extra_moves_bought");
            return ActionResult.Success();
        }

        /// <summary>Uses one in-level booster (Shovel / Rake / WateringCan / Shuffle) in the level in progress.</summary>
        public ActionResult UseBooster(long now, string booster)
        {
            if (State.Match3.Active == 0) return ActionResult.Fail(MetaError.NoLevelInProgress);
            if (!Boosters.IsInLevel(booster) || BoosterCount(booster) <= 0) return ActionResult.Fail(MetaError.NoBooster);
            Dict.Add(State.Boosters, booster, -1);
            Progress(now, DailyKinds.UseBoosters, 1);
            Stat("boosters_used");
            return ActionResult.Success();
        }

        public ActionResult BuyBooster(long now, string booster, int count = 1)
        {
            if (count <= 0 || !Data.BoosterDefs.TryGetValue(booster, out var def)) return ActionResult.Fail(MetaError.UnknownId);
            if (!SpendCoins((long)def.Coins * count)) return ActionResult.Fail(MetaError.NotEnoughCoins);
            Dict.Add(State.Boosters, booster, count);
            var r = new Reward();
            r.Boosters[booster] = count;
            return ActionResult.Success(r);
        }

        /// <summary>
        /// Applies the result of the level in progress. A win pays 1 star, the coins, sometimes a booster
        /// or a tool, and every N-th level a milestone gift; the board's crops go to the barn. A loss takes a life.
        /// </summary>
        public LevelOutcome FinishLevel(long now, LevelResult result)
        {
            var m = State.Match3;
            if (m.Active == 0) return new LevelOutcome { Error = MetaError.NoLevelInProgress };
            if (result == null || result.Level != m.Active) return new LevelOutcome { Error = MetaError.WrongLevel };
            var o = new LevelOutcome { Won = result.Won };
            var cfg = B.Match3;
            m.Active = 0;
            m.ExtraMovesBought = 0;
            Progress(now, DailyKinds.PlayLevels, 1);
            if (!result.Won)
            {
                m.Losses++;
                Stat("levels_lost");
                LoseLife(now);
                o.LivesLeft = Lives(now);
                return o;
            }

            m.Wins++;
            m.NextLevel = Math.Max(m.NextLevel, result.Level + 1);
            Stat("levels_won");
            var r = new Reward { Stars = 1, Coins = cfg.WinCoins + Math.Max(0, result.CoinsEarned) };
            if (cfg.MilestoneEvery > 0 && result.Level % cfg.MilestoneEvery == 0)
                r.Add(cfg.MilestoneReward);
            if (cfg.BoosterEvery > 0 && m.Wins % cfg.BoosterEvery == 0 && cfg.BoosterRotation.Count > 0)
            {
                string b = cfg.BoosterRotation[m.BoosterRotationIndex % cfg.BoosterRotation.Count];
                m.BoosterRotationIndex++;
                Dict.Add(r.Boosters, b, 1);
                Emit(MetaEventKind.BoosterFound, b, 1, null, "Подарок за уровни: " + Data.NameOf(b));
            }
            Grant(now, r);
            if (FarmOpen)
                MaybeDropTool(cfg.ToolPercent, r);
            if (result.Crops != null)
            {
                foreach (var kv in result.Crops)
                {
                    if (kv.Value <= 0 || !Data.Items.ContainsKey(kv.Key)) continue;
                    if (FarmOpen)
                    {
                        o.SoldOverflowCoins += StoreOrSell(kv.Key, kv.Value);
                        Dict.Add(r.Items, kv.Key, kv.Value);
                    }
                    else
                    {
                        // No barn yet: the neighbours buy the board's harvest.
                        int c = kv.Value * Data.Items[kv.Key].Sell;
                        State.Wallet.Coins += c;
                        r.Coins += c;
                    }
                }
            }
            Progress(now, DailyKinds.WinLevels, 1);
            o.StarsGained = 1;
            o.Reward = r;
            o.LivesLeft = Lives(now);
            return o;
        }

        /// <summary>The player left the level: counts as a loss (takes a life).</summary>
        public LevelOutcome AbandonLevel(long now) =>
            FinishLevel(now, new LevelResult { Level = State.Match3.Active, Won = false });
    }
}

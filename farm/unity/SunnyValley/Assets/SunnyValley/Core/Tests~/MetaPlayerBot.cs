using System;
using System.Collections.Generic;
using System.Linq;
using SunnyValley.Core.Meta;

namespace SunnyValley.Core.Tests
{
    /// <summary>
    /// A simulated free player who follows the economy model's habits (balance.json "pacing"): a few
    /// sessions a day, a handful of match-3 attempts with a given win rate, stars spent at once, and two
    /// farm passes per session (before and after the levels): collect, fulfil orders, produce what the
    /// orders need, buy what opens, sell only when the barn is nearly full. Plays the real rules.
    /// </summary>
    public sealed class MetaPlayerBot
    {
        public readonly MetaGame Game;
        readonly MetaData data;
        readonly PacingDef pacing;
        readonly double winRate;
        ulong rng;

        /// <summary>Install time: 08:00 of the first day (local = UTC in the bot).</summary>
        public readonly long Start;

        // ----- what the run measured
        public double? ChapterDoneDay;
        public readonly SortedDictionary<int, double> LevelDay = new SortedDictionary<int, double>();
        public int Attempts, Wins, Sessions;
        public int BarnBlockedPasses, MaxBarnBlockedStreak;
        int barnBlockedStreak;
        public int OrdersDone, TrucksSent, Sold;
        public long CoinsMin = long.MaxValue;
        public readonly List<string> Log = new List<string>();
        /// <summary>XP at the end of each day (for "no dead ends").</summary>
        public readonly List<long> DailyXp = new List<long>();
        public readonly List<int> DailyStarsSpent = new List<int>();

        public MetaPlayerBot(MetaData data, ulong seed, double? winRate = null)
        {
            this.data = data;
            pacing = data.Balance.Pacing;
            this.winRate = winRate ?? pacing.WinRate;
            rng = seed * 7919 + 17;
            Start = MetaTestKit.T0;
            Game = MetaGame.NewGame(data, Start, seed);
            LevelDay[1] = 0;
        }

        double Day(long now) => (now - Start) / 86400.0;
        long DayStart(int d) => Start - 8 * 3600 + d * 86400L;

        public void Run(int days)
        {
            for (int d = 0; d < days; d++)
            {
                var hours = pacing.SessionHours;
                for (int s = 0; s < hours.Count; s++)
                {
                    long now = DayStart(d) + (long)Math.Round(hours[s] * 3600);
                    if (now < Start) continue;
                    long next = s + 1 < hours.Count ? DayStart(d) + (long)Math.Round(hours[s + 1] * 3600)
                                                    : DayStart(d + 1) + (long)Math.Round(hours[0] * 3600);
                    Session(now, next);
                }
                DailyXp.Add(Game.Xp);
                DailyStarsSpent.Add(Game.State.Chapter.StarsSpent);
            }
        }

        void Session(long now, long nextSession)
        {
            Sessions++;
            var g = Game;
            g.Resume(now);
            g.ClaimLoginGift(now);
            long levelsLength = (long)(pacing.AttemptsPerSession * pacing.LevelMinutes * 60);
            FarmPass(now, now + levelsLength);
            now = PlayLevels(now);
            SpendStars(now);
            FarmPass(now, nextSession);
            ClaimDaily(now);
            g.Touch(now);
            CoinsMin = Math.Min(CoinsMin, g.Coins);
            foreach (var e in g.TakeEvents())
                if (e.Kind == MetaEventKind.LevelUp && !LevelDay.ContainsKey(e.Value))
                    LevelDay[e.Value] = Day(now);
        }

        long PlayLevels(long now)
        {
            var g = Game;
            for (int a = 0; a < pacing.AttemptsPerSession; a++)
            {
                if (!g.CanPlay(now)) break;
                int level = g.NextLevel;
                var pre = new List<string>();
                // Uses a pre-level booster on every 4th attempt if it has one (generous early boosters, GDD 2.4).
                if (a == 3)
                    foreach (var b in Boosters.PreLevel)
                        if (g.BoosterCount(b) > 0) { pre.Add(b); break; }
                if (!g.StartLevel(now, level, pre).Ok) break;
                Attempts++;
                bool won = Roll(1000) < winRate * 1000;
                var result = new LevelResult { Level = level, Won = won, Stars = won ? 1 + Roll(3) : 0, MovesLeft = won ? Roll(8) : 0 };
                if (won)
                {
                    Wins++;
                    result.CoinsEarned = 10 + Roll(31);
                    if (g.FarmOpen && Roll(4) == 0) result.Crops["tomato"] = 1 + Roll(3);
                }
                g.FinishLevel(now, result);
                now += (long)(pacing.LevelMinutes * 60);
            }
            return now;
        }

        void SpendStars(long now)
        {
            var g = Game;
            bool any = true;
            while (any && !g.ChapterFinished)
            {
                any = false;
                foreach (var t in g.AvailableTasks().OrderBy(t => t.Stars))
                {
                    if (t.Stars > g.Stars) continue;
                    string look = t.Choices.Count > 0 ? t.Choices[Roll(t.Choices.Count)].Id : null;
                    var r = g.CompleteTask(now, t.Id, look);
                    if (!r.Ok) continue;
                    any = true;
                    if (r.ChapterCompleted)
                    {
                        ChapterDoneDay = Day(now);
                        Log.Add($"day {Day(now):F2}: chapter 1 done, {g.Coins} coins");
                    }
                    break;
                }
            }
            // After the chapter, stars buy the special decorations.
            if (g.ChapterFinished)
                foreach (var d in data.Balance.Town.Decorations.Where(d => d.Stars > 0))
                    if (g.CanBuild(d.Id) == MetaError.None) g.Build(now, d.Id);
        }

        void ClaimDaily(long now)
        {
            var g = Game;
            var tasks = g.DailyTasks(now);
            for (int i = 0; i < tasks.Count; i++)
                if (!tasks[i].Claimed && g.DailyTaskDone(tasks[i])) g.ClaimDailyTask(now, i);
            if (g.DailyChestReady(now)) g.ClaimDailyChest(now);
        }

        // ================================================================== the farm

        void FarmPass(long now, long nextPass)
        {
            var g = Game;
            if (!g.FarmOpen) return;
            for (int round = 0; round < 2; round++)
            {
                g.CollectAll(now);
                FulfilOrders(now);
                LoadTruck(now);
            }
            Buy(now);
            ManageBarn(now);
            g.CollectAll(now);
            Produce(now, nextPass);
            // Blocked harvest: something ready that could not go into the barn.
            bool blocked = g.Plots.Any(p => p.Crop != null && p.ReadyAt <= now) ||
                           g.State.Farm.Workshops.Any(w => w.Jobs.Count > 0 && w.Jobs[0].EndAt <= now && !Boosters.IsBooster(data.Recipes[w.Jobs[0].Recipe].Output));
            if (blocked)
            {
                BarnBlockedPasses++;
                barnBlockedStreak++;
                MaxBarnBlockedStreak = Math.Max(MaxBarnBlockedStreak, barnBlockedStreak);
            }
            else barnBlockedStreak = 0;
        }

        void FulfilOrders(long now)
        {
            var g = Game;
            bool done = true;
            while (done)
            {
                done = false;
                var board = g.Board(now);
                int best = -1;
                for (int i = 0; i < board.Count; i++)
                    if (board[i].Order != null && g.CanFulfil(now, i) && (best < 0 || board[i].Order.Xp > board[best].Order.Xp))
                        best = i;
                if (best >= 0 && g.Fulfil(now, best).Ok)
                {
                    OrdersDone++;
                    done = true;
                }
            }
            // An order nobody can serve for a day goes away (rare: match-3 goods sold meanwhile).
            var b2 = g.Board(now);
            for (int i = 0; i < b2.Count; i++)
                if (b2[i].Order != null && now - b2[i].Order.CreatedAt > 86400 && !g.CanFulfil(now, i))
                    g.Discard(now, i);
        }

        void LoadTruck(long now)
        {
            var g = Game;
            if (!g.TruckUnlocked) return;
            var t = g.Truck(now);
            if (t.AwayUntil > now) return;
            for (int i = 0; i < t.Crates.Count; i++)
                if (!t.Crates[i].Loaded && g.BarnCount(t.Crates[i].Item) >= t.Crates[i].Count)
                    g.LoadCrate(now, i);
            if (t.Crates.Count > 0 && t.Crates.All(c => c.Loaded) && g.SendTruck(now).Ok) TrucksSent++;
        }

        void Buy(long now)
        {
            var g = Game;
            var b = data.Balance;
            long Reserve() => 150 + 30 * g.Level;
            bool Can(long price) => g.Coins - price >= Reserve();
            // Workshops and animal houses first: they open new goods.
            foreach (var w in b.Workshops)
                if (!g.HasWorkshop(w.Id) && w.Level <= g.Level && Can(w.Coins)) g.BuildWorkshop(now, w.Id);
            foreach (var h in b.AnimalHouses)
                if (!g.HasAnimalHouse(h.Id) && h.Level <= g.Level && Can(h.Coins)) g.BuildAnimalHouse(now, h.Id);
            // Town: services make room, houses bring workers.
            foreach (var s in b.Town.Services)
                if (Can(s.Coins) && g.CanBuild(s.Id) == MetaError.None) g.Build(now, s.Id);
            foreach (var h in b.Town.Houses.AsEnumerable().Reverse())
                while (Can(h.Coins) && g.CanBuild(h.Id) == MetaError.None) g.Build(now, h.Id);
            // Production capacity.
            while (g.NextPlotOffer != null && g.NextPlotOffer.Level <= g.Level && Can(g.NextPlotOffer.Coins)) g.BuyPlot(now);
            foreach (var a in b.Animals)
                while (a.Level <= g.Level && g.HasAnimalHouse(a.House) && g.NextAnimalPrice(a.Id) >= 0 && Can(g.NextAnimalPrice(a.Id)))
                    if (!g.BuyAnimal(now, a.Id).Ok) break;
            foreach (var t in b.Trees)
                while (t.Level <= g.Level && g.NextTreePrice(t.Id) >= 0 && Can(g.NextTreePrice(t.Id)))
                    if (!g.BuyTree(now, t.Id).Ok) break;
            // Tools.
            while (g.NextBarnUpgrade != null && g.UpgradeBarn(now).Ok) { }
            if (g.NextExpansion != null && g.LotsUsed >= g.Lots - 1 && Can(g.NextExpansion.Coins)) g.Expand(now);
            // Spare coins: decorations (beauty and XP).
            foreach (var d in b.Town.Decorations)
                if (d.Coins > 0 && g.Coins > 4 * Reserve() + 3 * d.Coins && g.CanBuild(d.Id) == MetaError.None && g.Built(d.Id) < 3)
                    g.Build(now, d.Id);
        }

        void ManageBarn(long now)
        {
            var g = Game;
            if (g.BarnFree > g.BarnCapacity / 8) return;
            var demand = Demand(now);
            // Sell what nothing asks for, the most abundant first, down to 3/4 of the barn.
            foreach (var kv in g.State.Barn.OrderByDescending(kv => kv.Value - demand.GetValueOrDefault(kv.Key)).ToList())
            {
                if (g.BarnUsed <= g.BarnCapacity * 3 / 4) break;
                int spare = kv.Value - demand.GetValueOrDefault(kv.Key);
                int sell = Math.Min(spare, g.BarnUsed - g.BarnCapacity * 3 / 4);
                if (sell > 0 && g.Sell(now, kv.Key, sell).Ok) Sold += sell;
            }
            // Still full (everything is wanted): sell the cheapest goods anyway — the barn must never block for good.
            while (g.BarnUsed > g.BarnCapacity * 7 / 8)
            {
                var cheapest = g.State.Barn.OrderBy(kv => data.Items[kv.Key].Sell).First();
                int n = Math.Max(1, Math.Min(cheapest.Value, g.BarnUsed - g.BarnCapacity * 3 / 4));
                if (!g.Sell(now, cheapest.Key, n).Ok) break;
                Sold += n;
            }
        }

        /// <summary>Goods the orders and the truck want, with the inputs of what has to be made.</summary>
        Dictionary<string, int> Demand(long now)
        {
            var g = Game;
            var want = new Dictionary<string, int>();
            foreach (var c in g.State.Orders.Board)
                if (c.Order != null)
                    foreach (var kv in c.Order.Items) want[kv.Key] = want.GetValueOrDefault(kv.Key) + kv.Value;
            var t = g.State.Orders.Truck;
            if (g.TruckUnlocked && t.AwayUntil <= now)
                foreach (var c in t.Crates)
                    if (!c.Loaded) want[c.Item] = want.GetValueOrDefault(c.Item) + c.Count;
            return want;
        }

        /// <summary>Units of the item on their way: growing, in a queue, inside a fed animal.</summary>
        Dictionary<string, int> Pipeline()
        {
            var g = Game;
            var p = new Dictionary<string, int>();
            foreach (var plot in g.Plots)
                if (plot.Crop != null) p[plot.Crop] = p.GetValueOrDefault(plot.Crop) + data.Crops[plot.Crop].Yield;
            foreach (var w in g.State.Farm.Workshops)
                foreach (var j in w.Jobs)
                {
                    var rc = data.Recipes[j.Recipe];
                    p[rc.Output] = p.GetValueOrDefault(rc.Output) + rc.Count;
                }
            foreach (var a in g.State.Farm.Animals)
                if (a.Fed) p[data.Animals[a.Type].Product] = p.GetValueOrDefault(data.Animals[a.Type].Product) + 1;
            foreach (var tr in g.State.Farm.Trees)
            {
                var def = data.Trees[tr.Type];
                p[def.Item] = p.GetValueOrDefault(def.Item) + def.Yield;
            }
            return p;
        }

        void Produce(long now, long nextPass)
        {
            var g = Game;
            // Missing = wanted - in the barn - on the way, expanded down the production chains.
            var missing = new Dictionary<string, int>();
            var stock = new Dictionary<string, int>(g.State.Barn);
            foreach (var kv in Pipeline()) stock[kv.Key] = stock.GetValueOrDefault(kv.Key) + kv.Value;
            var queue = new Queue<KeyValuePair<string, int>>(Demand(now));
            int guard = 0;
            while (queue.Count > 0 && guard++ < 500)
            {
                var (item, n) = (queue.Peek().Key, queue.Dequeue().Value);
                int have = stock.GetValueOrDefault(item);
                int use = Math.Min(have, n);
                stock[item] = have - use;
                int lack = n - use;
                if (lack <= 0) continue;
                missing[item] = missing.GetValueOrDefault(item) + lack;
                if (!data.Producer.TryGetValue(item, out var prod)) continue;
                if (prod is RecipeDef r)
                {
                    int batches = (lack + r.Count - 1) / r.Count;
                    foreach (var kv in r.Inputs) queue.Enqueue(new KeyValuePair<string, int>(kv.Key, kv.Value * batches));
                }
                else if (prod is AnimalDef a)
                    queue.Enqueue(new KeyValuePair<string, int>(a.Feed, lack));
            }

            // Animals: feed every hungry one (their goods are always wanted sooner or later), make feed if short.
            foreach (var a in g.State.Farm.Animals)
                if (!a.Fed) g.Feed(now, a.Uid);
            foreach (var at in data.Balance.Animals)
            {
                int hungry = g.State.Farm.Animals.Count(x => x.Type == at.Id);
                if (hungry > 0 && g.BarnCount(at.Feed) < hungry && data.Producer.TryGetValue(at.Feed, out var fp) && fp is RecipeDef)
                    missing[at.Feed] = Math.Max(missing.GetValueOrDefault(at.Feed), hungry);
            }

            // Workshops: queue what is missing and can be made now; deepest needs (inputs) were added last.
            foreach (var w in g.State.Farm.Workshops)
            {
                var recipes = g.AvailableRecipes(w.Id);
                bool added = true;
                while (added && w.Jobs.Count < w.Slots)
                {
                    added = false;
                    foreach (var rc in recipes.OrderByDescending(rc => missing.GetValueOrDefault(rc.Output)))
                    {
                        if (missing.GetValueOrDefault(rc.Output) <= 0 || !g.CanAfford(rc)) continue;
                        if (g.Enqueue(now, w.Id, rc.Id).Ok)
                        {
                            missing[rc.Output] -= rc.Count;
                            added = true;
                            break;
                        }
                    }
                }
                // Boosters from the tool shed when it is idle and the inputs are spare.
                if (w.Id == "toolshed" && w.Jobs.Count == 0)
                    foreach (var rc in recipes)
                        if (rc.Inputs.All(kv => g.BarnCount(kv.Key) >= kv.Value * 2) && g.Enqueue(now, w.Id, rc.Id).Ok) break;
            }

            // Fields: first the missing crops, then a stock crop that is ready by the next pass.
            long gap = nextPass - now;
            var crops = g.AvailableCrops();
            for (int i = 0; i < g.Plots.Count; i++)
            {
                if (g.Plots[i].Crop != null) continue;
                var need = crops.Where(c => missing.GetValueOrDefault(c.Item) > 0).OrderBy(c => c.GrowSeconds).FirstOrDefault();
                CropDef pick = need;
                if (pick == null)
                {
                    if (g.BarnFree < g.BarnCapacity / 4) break;
                    // The most valuable crop that ripens before the next pass (or the quickest one).
                    pick = crops.Where(c => c.GrowSeconds <= Math.Max(gap, 120)).OrderByDescending(c => data.Items[c.Item].OrderCoins).FirstOrDefault()
                           ?? crops.OrderBy(c => c.GrowSeconds).First();
                    if (g.BarnCount(pick.Item) > g.BarnCapacity / 6) continue;
                }
                bool fromBarn = g.BarnCount(pick.Item) > missing.GetValueOrDefault(pick.Item) + 4;
                if (g.Plant(now, i, pick.Item, fromBarn).Ok && need != null)
                    missing[pick.Item] -= data.Crops[pick.Item].Yield;
            }
        }

        int Roll(int max) => MetaRng.Range(ref rng, max);

        public string Report()
        {
            var g = Game;
            var lines = new List<string>
            {
                $"chapter 1 done on day {ChapterDoneDay:F2}; sessions {Sessions}, attempts {Attempts}, wins {Wins}",
                $"farm level {g.Level}, xp {g.Xp}, coins {g.Coins} (min {CoinsMin}), crystals {g.Crystals}, stars {g.Stars}",
                $"orders {OrdersDone}, trucks {TrucksSent}, sold {Sold}, barn {g.BarnUsed}/{g.BarnCapacity} (upgrades {g.State.BarnUpgrades}), " +
                $"blocked passes {BarnBlockedPasses} (max streak {MaxBarnBlockedStreak})",
                $"plots {g.Plots.Count}, workshops {g.State.Farm.Workshops.Count}, animals {g.State.Farm.Animals.Count}, trees {g.State.Farm.Trees.Count}, " +
                $"population {g.Population}/{g.PopulationCap}, lots {g.LotsUsed}/{g.Lots}, expansions {g.State.Farm.Expansions}",
                "levels: " + string.Join(", ", LevelDay.Select(kv => $"{kv.Key}@{kv.Value:F1}")),
            };
            lines.AddRange(Log);
            return string.Join("\n", lines);
        }
    }
}

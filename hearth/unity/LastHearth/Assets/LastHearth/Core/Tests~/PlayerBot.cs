using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core;
using LastHearth.Core.Combat;
using LastHearth.Core.Surge;
using NUnit.Framework;

namespace LastHearth.Core.Tests
{
    /// <summary>
    /// A free player who uses every system of the core, on the session plan of the economy model
    /// (pacing_sim: a long first day, then 4 sessions of 12 minutes at 8:00, 12:30, 17:00, 21:30).
    /// </summary>
    public sealed class PlayerBot
    {
        static readonly HeroClass[] HeroOrder =
        {
            HeroClass.Guardian, HeroClass.Swordsman, HeroClass.Cleaver, HeroClass.Archer, HeroClass.CoalPriest,
            HeroClass.AshMage, HeroClass.Swordsman, HeroClass.Archer, HeroClass.AshMage,
        };

        static readonly RoomType[] BuildOrder =
        {
            RoomType.Barracks, RoomType.Warehouse, RoomType.Mine, RoomType.TradingPost, RoomType.Forge, RoomType.Infirmary,
            RoomType.Library, RoomType.TrainingHall, RoomType.Greenhouse, RoomType.Watchtower, RoomType.Tavern,
            RoomType.Kitchen, RoomType.CoalMine, RoomType.Mine, RoomType.TradingPost, RoomType.Chapel, RoomType.Archive,
        };

        static readonly RoomType[] Producers =
        {
            RoomType.CoalMine, RoomType.Kitchen, RoomType.Mine, RoomType.TradingPost, RoomType.Greenhouse,
        };

        public readonly GameSession S;
        public int Hunts, HuntWins, Missions, Surges, SurgeStars, Routes, ArenaFights, ArenaWins, Raids;

        public PlayerBot(GameSession s) => S = s;

        public static readonly (double start, double hours)[] FirstDay = { (8, 1.5), (12.5, 0.5), (17, 0.5), (20, 1) };
        public static readonly (double start, double hours)[] OtherDays = { (8, 0.2), (12.5, 0.2), (17, 0.2), (21.5, 0.2) };

        /// <summary>Plays from day 0 (T0 = 08:00 of day 0) through the given number of days.</summary>
        public void PlayDays(long t0, int days, Action<int> endOfDay = null)
        {
            long midnight = t0 - 8 * 3600;
            for (int day = 0; day < days; day++)
            {
                foreach (var (start, hours) in day == 0 ? FirstDay : OtherDays)
                {
                    long from = midnight + day * 86400L + (long)(start * 3600);
                    long to = from + (long)(hours * 3600);
                    long step = day == 0 && start == 8 ? 10 : 60;
                    for (long now = from; now <= to; now += step)
                        Turn(now);
                }
                endOfDay?.Invoke(day + 1);
            }
        }

        public void Turn(long now)
        {
            S.Advance(now);
            S.ClaimRewards();
            S.CollectAll();
            S.ClaimDaily(now);
            while (S.CalendarPending > 0 && S.ClaimCalendar(now) == Fail.None)
            {
            }
            S.ClaimSeasonAll(now);
            foreach (var e in S.State.Expeditions.Where(x => x.Back).ToList())
                S.ResolveRouteCard(e.Id, 2, now);
            foreach (var job in S.State.Queue.AllJobs().ToList())
                S.FreeFinish(job.Id, now);
            UseHourglass(now);

            Build(now);
            if (S.UpgradeHearth(now) != Fail.None)
            {
                foreach (var room in S.State.Rooms.OrderBy(r => r.Level).ThenBy(r => KeyFirst(r)).ToList())
                {
                    if (S.UpgradeRoom(room.Id, now) == Fail.QueueFull)
                        break;
                }
            }
            Merge(now);
            Breed(now);
            Staff();
            Heroes();
            Hunt(now);
            Campaign(now);
            Surge(now);
            SendRoutes(now);
            Heart();
            Specs();
            Events(now);
            Pvp(now);
            Shop(now);
            Kennel(now);
            Halls(now);
        }

        // ------------------------------------------------------------------ pets and companions (GDD 8.1-8.2)

        public int PetFeeds, BondRaises;

        static string CompanionFor(HeroClass c)
        {
            switch (c)
            {
                case HeroClass.Guardian: return "Squire";
                case HeroClass.Cleaver: return "Falcon";
                case HeroClass.CoalPriest: return "Firefly";
                default: return "Wolfhound";
            }
        }

        void Kennel(long now)
        {
            S.CheckKennel();
            // silver goods only from a surplus, like the essence of Brik's shop
            foreach (var offer in S.MerchantOffers(now).Concat(S.DailyDeals(now)))
            {
                bool surplus = S.State.Wallet[ResourceType.Silver] >= S.Capacity(ResourceType.Silver) * 0.8;
                if (surplus && offer.Left > 0 && offer.PayWith == ResourceType.Silver && !offer.Id.StartsWith("d:Essence"))
                    S.BuyOffer(offer.Id, now);
            }
            // the battle pets first: the wolf, then the raven, then the bear cub
            foreach (string pet in new[] { "Wolf", "Raven", "BearCub" })
            {
                if (S.HasPet(pet))
                {
                    S.SetActivePet(pet);
                    break;
                }
            }
            foreach (var hero in S.Squad())
            {
                var have = S.CompanionOf(hero.Id);
                string want = CompanionFor(hero.HeroClass);
                if (!S.CompanionUnlocked(want))
                    want = "Squire";
                if (have == null || have.Kind != want)
                    S.SetCompanion(hero.Id, want);
            }
            // treats: the active pet to level 5, then the bonds of the squad, the weakest first
            for (int guard = 0; guard < 20; guard++)
            {
                var pet = S.ActivePet();
                if (pet != null && S.PetLevel(pet.Id) < 5 && S.FeedPet(pet.Id) == Fail.None)
                {
                    PetFeeds++;
                    continue;
                }
                var weakest = S.Squad().Where(h => S.CompanionOf(h.Id) != null && S.BondLevel(h.Id) < S.BondCap(h))
                    .OrderBy(h => S.BondLevel(h.Id)).FirstOrDefault();
                if (weakest != null && S.RaiseBond(weakest.Id) == Fail.None)
                {
                    BondRaises++;
                    continue;
                }
                if (pet != null && S.FeedPet(pet.Id) == Fail.None)
                {
                    PetFeeds++;
                    continue;
                }
                break;
            }
        }

        // ------------------------------------------------------------------ the work of the halls (GDD 6, GameSession.Rooms.cs)

        public int Hires, Dismissals, Researches, Trainings, Ascensions, Gifts, RecordsRead;

        static readonly RoomType[] Halls_ = { RoomType.Infirmary, RoomType.Library, RoomType.Tavern, RoomType.Watchtower, RoomType.Chapel, RoomType.Archive };

        /// <summary>Research order: the economy and war first, the household and the lore of the fog after.</summary>
        static readonly string[] BranchOrder = { "Crafts", "War", "Household", "Lore" };

        /// <summary>
        /// Silver to spare for the halls: the hearth is being built (its price is paid) and the store is this full,
        /// or the store is nearly full anyway. Saving for the hearth comes first (MODES.md, 5).
        /// </summary>
        bool SilverSurplus(double share)
        {
            double fill = (double)S.State.Wallet[ResourceType.Silver] / Math.Max(1, S.Capacity(ResourceType.Silver));
            return fill >= 0.95 || fill >= share && S.State.Queue.AllJobs().Any(j => j.IsHearth);
        }

        void Halls(long now)
        {
            Tavern(now);
            // idle people (not heroes) staff the halls without output, one per hall, by the hall's stat
            foreach (var type in Halls_)
            {
                var room = S.BestRoom(type);
                if (room == null || room.Workers.Count > 0)
                    continue;
                var stat = S.Balance.Room(type).Stat.Value;
                var idle = S.State.Dwellers.Where(d => d.RoomId < 0 && d.RouteId == 0 && !d.IsHero)
                    .OrderByDescending(d => d.StatOf(stat)).FirstOrDefault();
                if (idle != null)
                    S.Assign(idle.Id, room.Id);
            }
            // research from a surplus of materials, the cheapest step first
            if (S.CurrentResearch == null && SilverSurplus(0.6))
            {
                var next = S.ResearchFront().Where(n => S.CanResearch(n) == Fail.None)
                    .OrderBy(n => n.Cost).ThenBy(n => Array.IndexOf(BranchOrder, n.Branch)).FirstOrDefault();
                if (next != null && S.StartResearch(next.Id, now) == Fail.None)
                    Researches++;
            }
            // the training hall: people without a job train their best stat for the rooms (the squad's power stays
            // on the campaign's curve); those already in the hall go on first
            var hall = S.TrainingHall();
            if (hall != null)
            {
                foreach (var d in S.State.Dwellers.Where(x => !x.IsHero && x.RouteId == 0 && S.TrainingOf(x.Id) == null && S.PairingOf(x.Id) == null &&
                                                             (x.RoomId < 0 || x.RoomId == hall.Id))
                             .OrderBy(x => x.RoomId == hall.Id ? 0 : 1).ThenByDescending(x => x.Stats.Max()).ToList())
                {
                    if (!SilverSurplus(0.5))
                        break;
                    var stat = (Stat)Array.IndexOf(d.Stats, d.Stats.Max());
                    var fail = S.StartTraining(d.Id, stat, now);
                    if (fail == Fail.None)
                        Trainings++;
                    else if (fail == Fail.RoomFull)
                        break;
                    else if (fail == Fail.LimitReached && d.RoomId == hall.Id)
                        S.Unassign(d.Id); // trained to the cap: back to work
                }
            }
            // the chapel: the gift of the day, and ascension of heroes at their level cap (spare essence)
            if (S.ClaimGift(now, out _, out _) == Fail.None)
                Gifts++;
            int cap = Formulas.HeroLevelCap(S.Balance, S.State.HearthLevel);
            foreach (var hero in S.Squad().Where(h => h.HeroLevel >= cap).ToList())
            {
                if (S.Ascend(hero.Id, now) == Fail.None)
                    Ascensions++;
            }
            // the archive: the first record found and not read
            if (S.ArchiveOpen() && S.CurrentReading == null)
            {
                var record = S.Records.FirstOrDefault(r => S.RecordFound(r) && !S.RecordRead(r.Id));
                if (record != null && S.StartReading(record.Id, now) == Fail.None)
                    RecordsRead++;
            }
        }

        /// <summary>Hires the best recruits while there is room; when full, a rare one replaces the weakest idle dweller.</summary>
        void Tavern(long now)
        {
            if (!S.TavernOpen())
                return;
            for (int guard = 0; guard < 6; guard++)
            {
                var board = S.TavernBoard(now);
                int best = Enumerable.Range(0, board.Count).Where(i => !board[i].Hired && S.State.Wallet.Covers(S.HireCost(board[i])))
                    .OrderByDescending(i => board[i].Rarity).ThenByDescending(i => board[i].Stats.Sum()).DefaultIfEmpty(-1).First();
                if (best < 0)
                    return;
                if (S.PopulationFull())
                {
                    if (board[best].Rarity < Rarity.Rare)
                        return;
                    var weakest = S.State.Dwellers.Where(d => S.CanDismiss(d) == Fail.None && S.RarityOf(d.Id) == Rarity.Common)
                        .OrderBy(d => d.Stats.Sum()).FirstOrDefault();
                    if (weakest == null || weakest.Stats.Sum() >= board[best].Stats.Sum() || S.Dismiss(weakest.Id, now) != Fail.None)
                        return;
                    Dismissals++;
                }
                if (S.Hire(best, now, out _) != Fail.None)
                    return;
                Hires++;
            }
        }

        // ------------------------------------------------------------------ shops and Sparks

        const long Reserve = 300;

        /// <summary>The 4th builder comes first: no Sparks on anything else until it is bought.</summary>
        bool SavingForBuilder => !S.State.FourthBuilder;

        void Shop(long now)
        {
            S.BuyFourthBuilder();
            var items = S.BrikShop(now);
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it.Sold || it.PayWith != ResourceType.Silver)
                    continue;
                bool surplus = S.State.Wallet[ResourceType.Silver] >= S.Capacity(ResourceType.Silver) * 0.8;
                if (surplus && (it.Goods == ShopGoods.Essence || it.Goods == ShopGoods.Hourglass))
                    S.BuyShopItem(i, now);
            }
            if (SavingForBuilder)
                return; // saving for the 4th builder first (ECONOMY.md, 8)
            long spare = S.State.Wallet[ResourceType.Sparks] - Reserve;
            var hearthJob = S.State.Queue.AllJobs().FirstOrDefault(j => j.IsHearth && j.IsRunning(now));
            if (hearthJob != null && spare >= Formulas.SpeedupCost(S.Balance, hearthJob.Remaining(now)))
            {
                S.SpeedUpWithSparks(hearthJob.Id, now);
                return;
            }
            while (S.State.Wallet[ResourceType.Sparks] - Reserve >= S.Balance.Activities.Shop.PackPrice &&
                   S.State.Wallet[ResourceType.Silver] < S.Capacity(ResourceType.Silver) * 0.7 &&
                   S.BuyPack(now) == Fail.None)
            {
            }
        }

        // ------------------------------------------------------------------ arena and raids

        void Pvp(long now)
        {
            if (S.ArenaOpen)
            {
                while (S.ArenaTickets(now) > 0)
                {
                    // the middle defense: a fair fight for a fair rating
                    if (S.StartArena(1, now, out var battle) != Fail.None)
                        break;
                    battle.Brain = Brains.Careful();
                    S.FinishPvp(battle, now, out var reward);
                    ArenaFights++;
                    if (reward.Won)
                        ArenaWins++;
                }
                for (int g = 0; g < S.Balance.Activities.Pvp.GloryShop.Count; g++)
                {
                    while (S.BuyGlory(g, now) == Fail.None)
                    {
                    }
                }
            }
            if (!S.RaidsOpen)
                return;
            while (S.State.Revenge.Count > 0 && S.StartRaid(0, true, now, out var revenge) == Fail.None)
            {
                revenge.Brain = Brains.Careful();
                S.FinishPvp(revenge, now, out _);
                Raids++;
            }
            while (S.RaidFlags(now) > 0)
            {
                if (S.StartRaid(0, false, now, out var raid) != Fail.None)
                    break;
                raid.Brain = Brains.Careful();
                S.FinishPvp(raid, now, out _);
                Raids++;
            }
        }

        // ------------------------------------------------------------------ live events

        public int EventBattles;

        void Fight(string id, long now, int cell = -1)
        {
            if (S.StartEventBattle(id, now, out var battle, cell) != Fail.None)
                return;
            battle.Brain = Brains.Careful();
            S.FinishEventBattle(battle, now, out _);
            EventBattles++;
        }

        void Events(long now)
        {
            // ended events: take the reward of the place, spend the currency in the shop while it is open
            foreach (var run in S.EndedRuns())
                S.ClaimEventReward(run);
            foreach (var run in S.State.Events.Where(r => S.ShopOpen(r, now)).ToList())
            {
                for (int i = 0; i < S.EventShop(run.Id).Count; i++)
                    S.BuyEventGoods(run, i, now);
            }
            foreach (var run in S.ActiveRuns(now))
            {
                switch (run.Id)
                {
                    case "LongNight":
                        if (S.State.Wallet[ResourceType.Sparks] > Reserve + 30 && !SavingForBuilder)
                            S.BuyEventExtra("LongNight", now);
                        while (S.NightAttacksLeft(run, now) > 0 && S.State.EventBattle == null)
                            Fight("LongNight", now);
                        break;
                    case "Tournament":
                        while (S.TournamentAttemptsLeft(run, now) > 0 && S.State.EventBattle == null)
                            Fight("Tournament", now);
                        break;
                    case "Portal":
                        for (int i = 0; i < 4; i++)
                        {
                            if (run.BlessingOffer.Count > 0 && S.ChooseBlessing(0, now) != Fail.None)
                                S.SkipBlessing(now);
                            if (!run.RunAlive && run.FreeRunDay == S.LocalDay(now) && run.Revives == 0 &&
                                S.State.Wallet[ResourceType.Sparks] > Reserve + 50 && !SavingForBuilder)
                                S.PortalRevive(now);
                            int floor = run.Floor;
                            // a hurt squad rests when a door leads to a rest; otherwise it fights
                            var doors = S.PortalDoors(run);
                            int rest = doors.IndexOf(GameSession.PortalDoor.Rest);
                            if (run.RunAlive && rest >= 0 && run.HpShares.Count > 0 && run.HpShares.Average() < 0.6)
                                S.PortalEnter(rest, now, out _);
                            else
                                Fight("Portal", now);
                            if (run.Floor == floor)
                                break;
                        }
                        break;
                    case "Treasure":
                        for (int i = 0; i < 8 && S.TorchesLeft(run, now) > 0; i++)
                        {
                            var targets = S.TreasureCandidates(run);
                            var open = Enumerable.Range(0, GameSession.HexCells.Count).Where(c => S.CellOpenable(run, c)).ToList();
                            if (open.Count == 0 || targets.Count == 0)
                                break;
                            int cell = open.OrderBy(c => targets.Min(t => GameSession.HexDistance(c, t))).First();
                            if (S.OpenCell(cell, now, out var content) != Fail.None)
                                break;
                            if (content == GameSession.TreasureCell.Ambush)
                                Fight("Treasure", now, cell);
                        }
                        break;
                    case "Fair":
                        Fair(run, now);
                        break;
                    case "Siege":
                        if (run.Outposts.All(o => o.Garrison.Count == 0))
                        {
                            var guards = S.State.Dwellers.Where(d => !d.IsHero && d.RouteId == 0)
                                .OrderByDescending(d => d.StatOf(Stat.Strength) + d.StatOf(Stat.Endurance)).Take(10).ToList();
                            for (int o = 0; o < 5 && guards.Count > 0; o++)
                            {
                                var g = guards.Take(2).Select(d => d.Id).ToList();
                                guards = guards.Skip(2).ToList();
                                S.SetGarrison(o, g, now);
                            }
                        }
                        // stones go to the traps, but the Жар of the shop (50 stones) comes first
                        for (int o = 0; o < 5 && (run.Bought.ContainsKey("Ember") || run.Stone >= 50 + 4 * (run.Outposts[o].Traps + 1)); o++)
                            S.UpgradeTraps(o, now);
                        if (S.SiegeWindow(run, now) >= 0 && S.StartSiegeDefense(now, out var defense) == Fail.None)
                        {
                            defense.Brain = SurgeDefense.Careful();
                            S.FinishSiegeDefense(defense, now);
                        }
                        for (int o = 0; o < 5; o++)
                        {
                            if (run.Outposts[o].Fallen)
                                Fight("Siege", now, o);
                        }
                        break;
                    case "Festival":
                        while (S.StartLanterns(now, out int seed) == Fail.None)
                        {
                            var taps = GameSession.Lanterns(seed).Where((l, i) => i % 10 < 7).ToList();
                            S.FinishLanterns(seed, taps, now, out _);
                        }
                        break;
                }
            }
        }

        double Fill(int good)
        {
            var type = GameSession.FairGoods[good];
            long keep = type == ResourceType.Coal ? (long)(S.CoalBurnPerHour() * 24) : 0;
            long cap = Math.Max(1, S.Capacity(type));
            return (double)(S.State.Wallet[type] - keep) / cap;
        }

        /// <summary>Ash Fair: covers orders by trading the fullest good, then sells surplus for the emptiest.</summary>
        void Fair(EventRun run, long now)
        {
            var rates = S.FairRates(run, now);
            for (int o = run.Orders.Count - 1; o >= 0; o--)
            {
                var order = run.Orders[o];
                for (int k = 0; k < 2; k++)
                {
                    int need = (int)order[k * 2];
                    long missing = order[k * 2 + 1] - S.State.Wallet[GameSession.FairGoods[need]];
                    if (missing <= 0)
                        continue;
                    int other = (int)order[(1 - k) * 2];
                    int sell = Enumerable.Range(0, 4).Where(g => g != need && g != other).OrderByDescending(Fill).First();
                    long amount = (long)Math.Ceiling(missing * GameSession.FairValue[need] * rates[need] /
                                                     (GameSession.FairValue[sell] * rates[sell] * 0.92)) + 1;
                    if (Fill(sell) * S.Capacity(GameSession.FairGoods[sell]) >= amount)
                        S.FairTrade(sell, need, amount, now, out _);
                }
                S.FairDeliver(o, now);
            }
            for (int i = 0; i < 4; i++)
            {
                var order = Enumerable.Range(0, 4).OrderByDescending(Fill).ToList();
                int sell = order[0], buy = order[3];
                if (Fill(sell) < 0.5)
                    break;
                long amount = (long)(Fill(sell) * S.Capacity(GameSession.FairGoods[sell]) * 0.3);
                if (S.FairTrade(sell, buy, Math.Max(1, amount), now, out _) != Fail.None)
                    break;
            }
        }

        int KeyFirst(Room r)
        {
            int level = Math.Min(S.State.HearthLevel, S.Balance.HearthLevels.Count);
            return r.Type == S.Balance.Level(level).KeyRoom || r.Type == RoomType.Warehouse ? 0 : 1;
        }

        void UseHourglass(long now)
        {
            if (S.State.HourglassSeconds <= 0)
                return;
            var running = S.State.Queue.AllJobs().Where(j => j.IsRunning(now)).OrderByDescending(j => j.IsHearth).ThenByDescending(j => j.Remaining(now)).FirstOrDefault();
            if (running != null && running.Remaining(now) > S.Balance.Build.FreeFinishSeconds)
                S.UseHourglass(running.Id, now);
        }

        void Build(long now)
        {
            var counts = new Dictionary<RoomType, int>();
            foreach (var type in BuildOrder)
            {
                counts[type] = counts.TryGetValue(type, out int c) ? c + 1 : 1;
                if (S.CountCells(type) >= counts[type] || S.State.HearthLevel < S.Balance.Room(type).Unlock)
                    continue;
                var cell = NextTo(type) ?? FreeCell(S);
                if (cell == null)
                {
                    ClearNearest(S, now);
                    return;
                }
                if (S.BuildRoom(type, cell.Value.floor, cell.Value.cell, now, out _) == Fail.QueueFull)
                    return;
            }
        }

        /// <summary>A dug-out cell beside a room of this type, where the new one can merge with it later.</summary>
        (int floor, int cell)? NextTo(RoomType type)
        {
            foreach (var room in S.State.Rooms.Where(r => r.Type == type && r.Width < S.Balance.Merge.MaxWidth))
            {
                foreach (int cell in new[] { room.Cell - 1, room.Cell + room.Width })
                {
                    if (S.CellFree(room.Floor, cell) && Warm(S, room.Floor))
                        return (room.Floor, cell);
                }
            }
            return null;
        }

        /// <summary>Same rooms side by side become one hall as soon as their levels match.</summary>
        void Merge(long now)
        {
            foreach (var room in S.State.Rooms.ToList())
            {
                var other = S.MergeCandidate(room);
                if (other != null && S.State.Rooms.Contains(room))
                    S.MergeRooms(room.Id, other.Id, now);
            }
        }

        /// <summary>
        /// Couples in the living quarters fill the fortress up to the dweller cap: idle people first,
        /// then the weakest workers; after a birth the parents go back to work.
        /// </summary>
        void Breed(long now)
        {
            // a child that cannot be born (the fortress filled up) does not keep its parents from work
            foreach (var p in S.State.Pairings.Where(p => p.End <= now).ToList())
            {
                S.Unassign(p.ManId);
                S.Unassign(p.WomanId);
            }
            S.Advance(now);
            foreach (var room in S.State.Rooms.Where(r => r.Type == RoomType.Bedrooms && r.Level > 0).ToList())
            {
                if (!S.PairingsIn(room.Id).Any())
                {
                    foreach (int id in room.Workers.ToList())
                        S.Unassign(id);
                }
                while (S.PairingsIn(room.Id).Count() < room.Width &&
                       S.State.Dwellers.Count + S.State.Pairings.Count < Formulas.DwellerCap(S.Balance, S.State.HearthLevel))
                {
                    var man = Parent(Sex.Male);
                    var woman = Parent(Sex.Female);
                    if (man == null || woman == null || S.StartPairing(room.Id, man.Id, woman.Id, now) != Fail.None)
                        break;
                }
            }
        }

        Dweller Parent(Sex sex) => S.State.Dwellers
            .Where(d => d.Sex == sex && !d.IsHero && d.RouteId == 0 && S.PairingOf(d.Id) == null)
            .OrderBy(d => d.RoomId < 0 ? 0 : 1).ThenBy(d => d.Stats.Sum()).FirstOrDefault();

        /// <summary>Floors in the order a player fills them: the hearth's own, then one up and one down, and so on.</summary>
        static IEnumerable<(int floor, int cell)> CellsByFloor(GameSession s)
        {
            var g = s.Balance.Grid;
            for (int d = 0; d < g.Floors; d++)
            {
                foreach (int floor in new[] { g.HearthFloor + d, g.HearthFloor - d })
                {
                    for (int cell = 0; cell < g.Cells; cell++)
                        yield return (floor, cell);
                }
            }
        }

        static bool Warm(GameSession s, int floor) =>
            Formulas.IsFloorHabitable(s.Balance, s.State.HearthLevel, s.State.Mode, floor, s.State.HearthLevel, s.WarmthMult());

        /// <summary>The first dug-out cell warm enough for a room of the hearth's level.</summary>
        public static (int floor, int cell)? FreeCell(GameSession s)
        {
            foreach (var (floor, cell) in CellsByFloor(s))
            {
                if (s.CellFree(floor, cell) && Warm(s, floor))
                    return (floor, cell);
            }
            return null;
        }

        /// <summary>
        /// Clears the next obstacle on a warm floor, the floors nearest to the hearth first (rooms close
        /// together are easier to hold in the Surge), unless one is being cleared already.
        /// </summary>
        public static void ClearNearest(GameSession s, long now)
        {
            if (s.State.Queue.AllJobs().Any(j => j.IsClearing))
                return;
            var next = CellsByFloor(s).Where(c => s.CellStateAt(c.floor, c.cell) == CellState.Obstacle && Warm(s, c.floor))
                .OrderBy(c => Math.Abs(c.floor - s.Balance.Grid.HearthFloor)).ThenBy(c => s.HearthDistance(c.floor, c.cell)).Select(c => ((int floor, int cell)?)c).FirstOrDefault();
            if (next.HasValue)
                s.ClearObstacle(next.Value.floor, next.Value.cell, now);
        }

        void Staff()
        {
            var rooms = S.State.Rooms.Where(r => r.Level > 0 && Producers.Contains(r.Type))
                .OrderBy(r => Array.IndexOf(Producers, r.Type)).ToList();
            foreach (var d in S.State.Dwellers.Where(x => x.RoomId < 0 && x.RouteId == 0).ToList())
            {
                var stat = (Func<Room, int>)(r => d.StatOf(S.Balance.Room(r.Type).Stat.Value));
                var room = rooms.Where(r => S.Assign(d.Id, r.Id) == Fail.None).FirstOrDefault();
                if (room == null)
                    break;
            }
        }

        void Heroes()
        {
            var heroes = S.State.Dwellers.Where(d => d.IsHero).ToList();
            if (S.MaxRoomLevel(RoomType.Barracks) >= 1)
            {
                foreach (var d in S.State.Dwellers.Where(x => !x.IsHero && x.RouteId == 0)
                             .OrderByDescending(x => x.Stats.Sum()).ToList())
                {
                    if (heroes.Count >= S.SquadSize())
                        break;
                    S.Promote(d.Id, HeroOrder[heroes.Count % HeroOrder.Length]);
                    heroes.Add(d);
                }
            }
            // essence: the weakest of the squad first, up to the cap
            while (true)
            {
                var weakest = S.Squad().OrderBy(d => d.HeroLevel).FirstOrDefault();
                if (weakest == null || S.LevelUpHero(weakest.Id) != Fail.None)
                    break;
            }
            while (true)
            {
                var skill = S.Squad().Where(d => d.SkillLevel < S.SkillCap(d)).OrderBy(d => d.SkillLevel).FirstOrDefault();
                if (skill == null || S.UpgradeSkill(skill.Id) != Fail.None)
                    break;
            }
        }

        void Hunt(long now)
        {
            if (S.State.HearthLevel < S.Balance.Activities.Hunt.Unlock)
                return;
            while (S.HuntTracks(now) > 0)
            {
                var family = S.HuntDens(now)[Hunts % 3];
                int rank = Math.Max(1, S.State.HearthLevel - 1);
                if (S.StartHunt(family, rank, now, out var battle) != Fail.None)
                    return;
                battle.Brain = Brains.Careful();
                S.FinishHunt(battle, out var reward);
                Hunts++;
                if (reward.Stars > 0)
                    HuntWins++;
            }
        }

        void Campaign(long now)
        {
            for (int tries = 0; tries < 12 && S.MissionsCleared < S.Balance.Campaign.Missions.Count; tries++)
            {
                if (S.StartMission(S.MissionsCleared, now, out var battle) != Fail.None)
                    break;
                battle.Brain = Brains.Careful();
                S.FinishMission(battle, out var reward);
                if (!reward.FirstClear)
                    break; // too strong for now: come back later
                Missions++;
            }
            while (S.MissionsCleared > 0 && S.RepeatsLeft(now) > 0)
                S.RepeatMission(S.MissionsCleared - 1, now, out _);
        }

        void Surge(long now)
        {
            if (!S.SurgeOpen(now) || S.StartSurge(now, out var defense) != Fail.None)
                return;
            defense.Brain = SurgeDefense.Careful();
            S.FinishSurge(defense, now, out var reward);
            Surges++;
            SurgeStars += reward.Stars;
        }

        void SendRoutes(long now)
        {
            if (S.State.HearthLevel < S.Balance.Activities.Routes.Unlock)
                return;
            while (S.RoutesLeft(now) > 0)
            {
                // the ones not producing anything walk: idle people first, then heroes not on duty
                var team = S.State.Dwellers.Where(d => d.RouteId == 0 && d.InjuredUntil <= now && !d.IsHero && d.RoomId < 0)
                    .Take(3).Select(d => d.Id).ToList();
                if (team.Count == 0)
                {
                    team = S.State.Dwellers.Where(d => d.RouteId == 0 && d.InjuredUntil <= now && !d.IsHero)
                        .OrderBy(d => d.Stats.Sum()).Take(2).Select(d => d.Id).ToList();
                }
                if (team.Count == 0)
                    return;
                var (stat, need) = S.ForkCheck(S.State.NextBattleSeed, 0, S.State.HearthLevel);
                int have = team.Sum(id => S.FindDweller(id).StatOf(stat));
                var first = have >= need ? RoutePath.Risky : RoutePath.Safe;
                if (S.StartRoute(team, first, RoutePath.Long, now, out _) != Fail.None)
                    return;
                Routes++;
            }
        }

        void Heart()
        {
            var order = new[] { HeartBranch.Economy, HeartBranch.Warmth, HeartBranch.Defense };
            for (int i = 0; i < 30; i++)
            {
                var branch = order.OrderBy(b => S.State.Heart[(int)b]).ThenBy(b => Array.IndexOf(order, b)).First();
                if (S.BuyHeart(branch) != Fail.None)
                    return;
            }
        }

        void Specs()
        {
            foreach (var type in Producers.Concat(new[] { RoomType.Barracks, RoomType.Warehouse }))
            {
                for (int tier = 0; tier < GameSession.SpecTiers.Length; tier++)
                {
                    if (S.SpecChosen(type, tier) != 0)
                        continue;
                    var fail = S.BuySpec(type, tier, Producers.Contains(type) ? 1 : 2);
                    if (fail == Fail.NotEnough)
                        return;
                }
            }
        }

        public string Summary(int day)
        {
            var squad = S.Squad();
            return string.Format(
                "day {0,2}: hearth {1,2} | heroes {2} lvl {3}-{4} skill {5} | missions {6,3} | hunts {7}/{8} | surges {9} ({10:0.0}*) | routes {11} | " +
                "heart {12} specs {13} | sparks {14} ember {15} cores {16} bp {17} | dwellers {18} rooms {19} | arena {20}/{21} rating {22} raids {23} glory {24}",
                day, S.State.HearthLevel, squad.Count, squad.Count == 0 ? 0 : squad.Min(d => d.HeroLevel),
                squad.Count == 0 ? 0 : squad.Max(d => d.HeroLevel), squad.Count == 0 ? 0 : squad.Min(d => d.SkillLevel),
                S.MissionsCleared, HuntWins, Hunts, Surges, Surges == 0 ? 0 : (double)SurgeStars / Surges, Routes,
                S.HeartTotal, S.State.SpecChoice.Values.Sum(v => v.Count(x => x != 0)),
                S.State.Wallet[ResourceType.Sparks], S.State.Wallet[ResourceType.Ember], S.State.Wallet[ResourceType.Cores],
                S.State.Wallet[ResourceType.Blueprints], S.State.Dwellers.Count, S.State.Rooms.Count,
                ArenaWins, ArenaFights, S.State.ArenaRating, Raids, S.State.Wallet[ResourceType.Glory]) +
                string.Format(" | halls: hired {0} (let go {1}), research {2}, trained {3}, ascended {4}, gifts {5}, records {6}",
                    Hires, Dismissals, S.ResearchCount, S.PointsTrained, Ascensions, Gifts, S.State.Halls.Read.Count);
        }
    }

    public class FullGameTests
    {
        /// <summary>The economy model (pacing_sim): hearth level by the end of the day.</summary>
        static readonly Dictionary<int, int> Model = new Dictionary<int, int>
        {
            { 1, 9 }, { 2, 10 }, { 5, 13 }, { 7, 14 }, { 10, 15 }, { 14, 16 }, { 18, 17 }, { 25, 18 },
        };

        [Test]
        public void ThirtyDaysFollowTheEconomyModel()
        {
            var s = TestData.NewSession();
            s.Combat = TestData.Combat();
            var bot = new PlayerBot(s);
            var hearth = new Dictionary<int, int>();
            int builderDay = 0;
            bot.PlayDays(TestData.Start, 30, day =>
            {
                hearth[day] = s.State.HearthLevel;
                if (builderDay == 0 && s.State.FourthBuilder)
                    builderDay = day;
                TestContext.WriteLine(bot.Summary(day) + " | events " + string.Join(",", s.ActiveRuns(s.State.Time).Select(r => r.Id + ":" + r.Points.ToString("0") + "/" + r.Thresholds)) + " | 4th " + s.State.FourthBuilder);
            });
            var results = s.State.Events.Where(r => r.Closed).ToList();
            foreach (var r in results)
                TestContext.WriteLine(r.LastResult + " thresholds " + r.Thresholds + (r.Personal ? " (personal)" : ""));
            foreach (var kv in Model)
            {
                TestContext.WriteLine("day " + kv.Key + ": bot " + hearth[kv.Key] + ", model " + kv.Value);
                Assert.That(hearth[kv.Key], Is.InRange(kv.Value - 1, kv.Value + 1), "day " + kv.Key);
            }
            Assert.That(builderDay, Is.InRange(10, 30), "the 4th builder is a goal of the first month");
            Assert.That(bot.Missions, Is.InRange(70, 130));
            // PvP from day ~13: the careful player settles in Silver or Gold, raids every day from hearth 17
            Assert.That(s.State.ArenaRating, Is.InRange(1250, 1600));
            Assert.Greater(bot.Raids, 30);
            // a regular player finishes most events, but not every event gives the first place
            Assert.Greater(results.Count(r => r.Thresholds == 10), results.Count / 2);
            Assert.Less(results.Count(r => r.LastResult.Contains("место 1 из")), results.Count);
        }

        [Test]
        public void TwoWeeksWithEverySystem()
        {
            var s = TestData.NewSession();
            s.Combat = TestData.Combat();
            var bot = new PlayerBot(s);
            var hearth = new Dictionary<int, int>();
            bot.PlayDays(TestData.Start, 14, day =>
            {
                hearth[day] = s.State.HearthLevel;
                TestContext.WriteLine(bot.Summary(day));
            });
            TestContext.WriteLine("economy model: hearth 9 by the first evening, 10 on day 2, 13 on day 5, 14 on day 7, 15 on day 10, 16 on day 14");
            Assert.GreaterOrEqual(hearth[1], 8);
            Assert.GreaterOrEqual(hearth[7], 12);
            Assert.GreaterOrEqual(hearth[14], 14);
            Assert.Greater(bot.HuntWins, 0);
            Assert.Greater(bot.Surges, 0);
            Assert.Greater(bot.Routes, 0);
        }
    }
}

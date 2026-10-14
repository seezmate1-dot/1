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
        public int Hunts, HuntWins, Missions, Surges, SurgeStars, Routes;

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
            Staff();
            Heroes();
            Hunt(now);
            Campaign(now);
            Surge(now);
            SendRoutes(now);
            Heart();
            Specs();
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
                if (S.CountRooms(type) >= counts[type] || S.State.HearthLevel < S.Balance.Room(type).Unlock)
                    continue;
                var cell = FreeCell();
                if (cell == null || S.BuildRoom(type, cell.Value.floor, cell.Value.cell, now, out _) == Fail.QueueFull)
                    return;
            }
        }

        (int floor, int cell)? FreeCell()
        {
            var g = S.Balance.Grid;
            for (int d = 0; d < g.Floors; d++)
            {
                foreach (int floor in new[] { g.HearthFloor + d, g.HearthFloor - d })
                {
                    for (int cell = 0; cell < g.Cells; cell++)
                    {
                        if (S.CellFree(floor, cell) &&
                            Formulas.IsFloorHabitable(S.Balance, S.State.HearthLevel, S.State.Mode, floor, S.State.HearthLevel, S.WarmthMult()))
                            return (floor, cell);
                    }
                }
            }
            return null;
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
                "heart {12} specs {13} | sparks {14} ember {15} cores {16} bp {17} | dwellers {18} rooms {19}",
                day, S.State.HearthLevel, squad.Count, squad.Count == 0 ? 0 : squad.Min(d => d.HeroLevel),
                squad.Count == 0 ? 0 : squad.Max(d => d.HeroLevel), squad.Count == 0 ? 0 : squad.Min(d => d.SkillLevel),
                S.MissionsCleared, HuntWins, Hunts, Surges, Surges == 0 ? 0 : (double)SurgeStars / Surges, Routes,
                S.HeartTotal, S.State.SpecChoice.Values.Sum(v => v.Count(x => x != 0)),
                S.State.Wallet[ResourceType.Sparks], S.State.Wallet[ResourceType.Ember], S.State.Wallet[ResourceType.Cores],
                S.State.Wallet[ResourceType.Blueprints], S.State.Dwellers.Count, S.State.Rooms.Count);
        }
    }

    public class FullGameTests
    {
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

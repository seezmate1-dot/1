using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core.Combat;
using LastHearth.Core.Surge;

namespace LastHearth.Core
{
    public sealed class SurgeReward
    {
        public bool Live;
        public int Stars;
        public long Ember;
        public long Essence;
        public long HourglassSeconds;
        public bool Frost;
    }

    /// <summary>ACTIVITIES.md, 2: the evening Surge, played live or settled automatically.</summary>
    public sealed partial class GameSession
    {
        public event Action<SurgeReward> SurgeSettled;

        SurgeBalance SurgeNumbers => Combat.Surge;

        /// <summary>Live defense is open from 19:00 to midnight local time, once a day.</summary>
        public bool SurgeOpen(long now)
        {
            var s = Balance.Activities.Surge;
            return State.HearthLevel >= s.Unlock && !State.SurgeLive && State.SurgeDay < LocalDay(now) &&
                   LocalSecondOfDay(now) >= s.StartHour * 3600L;
        }

        /// <summary>The defense built from the real fortress: cold rooms spawn creatures, workers fight on their floors.</summary>
        public SurgeSetup BuildSurgeSetup()
        {
            var c = SurgeNumbers;
            int h = Balance.Grid.HearthFloor;
            int L = State.HearthLevel;
            var setup = new SurgeSetup
            {
                HearthFloor = h,
                HearthLevel = L,
                Blazing = State.Mode == HearthMode.Blazing,
                Coal = State.Wallet[ResourceType.Coal],
                DefenseBonus = DefenseMult() - 1,
            };
            var floors = State.Rooms.Where(r => r.Level > 0).Select(r => r.Floor).Distinct().ToList();
            var cold = State.Rooms.Where(r => r.Level > 0 && !IsRoomWarm(r)).Select(r => r.Floor).Distinct()
                .OrderByDescending(f => Math.Abs(f - h)).ToList();
            setup.SpawnFloors.AddRange(cold);
            int up = floors.Where(f => f > h).DefaultIfEmpty(h + 1).Max();
            int down = floors.Where(f => f < h).DefaultIfEmpty(h - 1).Min();
            foreach (int f in new[] { up, down })
            {
                if (!setup.SpawnFloors.Contains(f) && setup.SpawnFloors.Count < 2)
                    setup.SpawnFloors.Add(f);
            }
            double gear = Math.Pow(c.GearGrowth, L - 1);
            foreach (var room in State.Rooms)
            {
                double spec = 1 + SpecValue(room, SpecEffect.Defense);
                foreach (int id in room.Workers)
                {
                    var d = FindDweller(id);
                    if (d == null || d.IsHero)
                        continue;
                    double stat = (d.StatOf(Stat.Strength) + d.StatOf(Stat.Endurance)) / 2.0;
                    setup.Defenders.Add(new SurgeDefender { Id = d.Id, Name = d.Name, Dps = c.DwellerDps * stat * gear * spec, Floor = room.Floor });
                }
            }
            foreach (var d in Squad())
            {
                setup.Defenders.Add(new SurgeDefender
                {
                    Id = d.Id,
                    Name = d.Name,
                    Hero = true,
                    Floor = h,
                    Dps = c.HeroDps * Math.Pow(c.HeroLevelGrowth, d.HeroLevel - 1) * gear,
                });
            }
            return setup;
        }

        public Fail StartSurge(long now, out SurgeDefense defense)
        {
            defense = null;
            Advance(now);
            if (!SurgeOpen(now))
                return Fail.Locked;
            State.SurgeLive = true;
            defense = new SurgeDefense(SurgeNumbers, BuildSurgeSetup());
            return Fail.None;
        }

        public Fail FinishSurge(SurgeDefense defense, long now, out SurgeReward reward)
        {
            reward = null;
            if (!State.SurgeLive)
                return Fail.NoHunt;
            if (!defense.Over)
                defense.RunToEnd();
            State.SurgeLive = false;
            State.Wallet[ResourceType.Coal] = Math.Max(0, State.Wallet[ResourceType.Coal] - defense.CoalSpent);
            Count(DailyTask.Surge);
            reward = PaySurge(defense.Stars, true, LocalDay(now) - (LocalSecondOfDay(now) < Balance.Activities.Surge.StartHour * 3600L ? 1 : 0));
            return Fail.None;
        }

        /// <summary>A live Surge left unfinished (the game closed) is settled as an automatic one.</summary>
        public SurgeReward ResolvePendingSurge(long now)
        {
            if (!State.SurgeLive)
                return null;
            State.SurgeLive = false;
            var d = new SurgeDefense(SurgeNumbers, BuildSurgeSetup()) { Brain = SurgeDefense.Passive() };
            d.RunToEnd();
            return PaySurge(d.Stars, false, Math.Max(State.SurgeDay + 1, LocalDay(now) - 1));
        }

        SurgeReward PaySurge(int stars, bool live, long day)
        {
            var s = Balance.Activities.Surge;
            int L = State.HearthLevel;
            double share = live ? Math.Max(Combat.StarReward[stars], s.AutoShare) : s.AutoShare;
            var reward = new SurgeReward
            {
                Live = live,
                Stars = stars,
                Ember = (long)Math.Round(s.EmberByLevel[L - 1] * share),
                Essence = (long)Math.Round(Balance.Activities.EssenceByLevel[L - 1] * s.EssenceShare),
                HourglassSeconds = s.HourglassMinutes * 60,
            };
            State.Wallet[ResourceType.Ember] += reward.Ember;
            State.Wallet[ResourceType.Essence] += reward.Essence;
            State.HourglassSeconds += reward.HourglassSeconds;
            if (stars == 0)
            {
                // the far floors freeze until 08:00 of the next morning
                reward.Frost = true;
                State.FrostUntil = (day + 1) * 86400 + 8 * 3600 - State.UtcOffsetSeconds;
            }
            State.SurgeDay = Math.Max(State.SurgeDay, day);
            AddNews(stars > 0 ? MoraleNews.SurgeWon : MoraleNews.SurgeLost); // the spirit of the fortress (GameSession.People.cs)
            CountWarmWeek(stars);
            if (stars > 0)
                AddSeasonXp(live ? SeasonInfo.SurgeXp : SeasonInfo.SurgeAutoXp);
            SurgeSettled?.Invoke(reward);
            return reward;
        }

        // ------------------------------------------------------------------ «Тёплая неделя» (EVENTS.md, 11)

        /// <summary>Surges in a row without a defeat; a missed evening is not a defeat.</summary>
        public int SurgeStreak => Retention.SurgeStreak;

        public bool WarmWeekOn(long now) => now < Retention.WarmUntil;

        public long WarmWeekUntil => Retention.WarmUntil;

        /// <summary>+5% output while the warm week lasts (part of OutputMult).</summary>
        public double WarmWeekMult() => State.Time < Retention.WarmUntil ? 1 + Balance.Activities.Surge.WarmWeek.Output : 1;

        /// <summary>Every 7th Surge in a row without a defeat starts (or lengthens) a warm day of +5% output.</summary>
        void CountWarmWeek(int stars)
        {
            var w = Balance.Activities.Surge.WarmWeek;
            var r = Retention;
            if (stars == 0)
            {
                r.SurgeStreak = 0;
                return;
            }
            r.SurgeStreak++;
            if (w.Surges > 0 && r.SurgeStreak % w.Surges == 0)
                r.WarmUntil = Math.Max(r.WarmUntil, State.Time) + w.Hours * 3600L;
        }

        /// <summary>Evenings that passed without the player are defended automatically (70%, at most 3).</summary>
        void SettleMissedSurges(long now)
        {
            if (Combat == null || State.HearthLevel < Balance.Activities.Surge.Unlock || State.SurgeLive)
                return;
            if (State.SurgeDay < 0)
                State.SurgeDay = LocalDay(State.Time) - 1;
            long lastClosed = LocalDay(now) - 1; // yesterday's window closed at midnight
            for (long day = Math.Max(State.SurgeDay + 1, lastClosed - 2); day <= lastClosed; day++)
            {
                var d = new SurgeDefense(SurgeNumbers, BuildSurgeSetup()) { Brain = SurgeDefense.Passive() };
                d.RunToEnd();
                PaySurge(d.Stars, false, day);
            }
        }
    }
}

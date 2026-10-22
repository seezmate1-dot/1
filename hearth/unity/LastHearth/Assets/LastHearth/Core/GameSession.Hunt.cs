using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core.Combat;

namespace LastHearth.Core
{
    public sealed class HuntReward
    {
        public int Stars;
        public long Cores;
        /// <summary>An item found (GDD 8), or null.</summary>
        public GearItem Item;
        /// <summary>The monster of the week was beaten (double Cores).</summary>
        public bool Weekly;
    }

    /// <summary>ACTIVITIES.md, 3: the Hunt — tactical boss fights for Cores, and hero skills.</summary>
    public sealed partial class GameSession
    {
        static readonly string[] RefugeeNames =
        {
            "Олег", "Рада", "Борис", "Ясна", "Кир", "Злата", "Ефим", "Лада", "Савва", "Нея", "Гордей", "Мила",
            "Тихон", "Веда", "Остап", "Устя", "Прохор", "Варя", "Демид", "Агна",
        };

        /// <summary>Numbers of the battle; the game sets it after loading Resources/combat.json.</summary>
        public CombatBalance Combat { get; set; }

        public int SquadSize() => 5 + Balance.SquadSlots.Count(gate => State.HearthLevel >= gate);

        /// <summary>
        /// Until the tavern exists, refugees come out of the fog as the hearth grows: two per level
        /// while there is room (GDD 8). Stats are fixed by order, so the game stays deterministic.
        /// </summary>
        void RefugeesArrive(int count = 2)
        {
            for (int i = 0; i < count; i++)
            {
                if (PopulationFull())
                    return;
                int n = State.NextDwellerId;
                int lead = (n * 3) % 5;
                var stats = new int[5];
                for (int k = 0; k < 5; k++)
                    stats[k] = 3 + (n + k * 7) % 5 + 2 * State.HearthLevel / 3;
                stats[lead] += 4;
                AddDweller(RefugeeNames[(n - 1) % RefugeeNames.Length], stats);
            }
        }

        // ------------------------------------------------------------------ tracks and dens

        long TrackSeconds => Balance.Activities.Hunt.TrackHours * 3600L;

        /// <summary>Tracks left: one comes back every 8 hours, up to three.</summary>
        public int HuntTracks(long now)
        {
            int max = Balance.Activities.Hunt.TracksPerDay;
            while (State.HuntTracks < max && State.HuntTrackAt > 0 && now >= State.HuntTrackAt)
            {
                State.HuntTracks++;
                State.HuntTrackAt = State.HuntTracks < max ? State.HuntTrackAt + TrackSeconds : 0;
            }
            return State.HuntTracks;
        }

        /// <summary>Three dens a day out of the four monster families; they rotate with the date.</summary>
        public List<MonsterFamily> HuntDens(long now)
        {
            int resting = (int)((now / 86400) % 4);
            var dens = new List<MonsterFamily>();
            foreach (MonsterFamily f in Enum.GetValues(typeof(MonsterFamily)))
            {
                if ((int)f != resting)
                    dens.Add(f);
            }
            return dens;
        }

        /// <summary>The squad: heroes by level, as many as the barracks allow.</summary>
        public List<Dweller> Squad()
        {
            return State.Dwellers.Where(d => d.IsHero).OrderByDescending(d => d.HeroLevel).ThenBy(d => d.Id)
                .Take(SquadSize()).ToList();
        }

        /// <summary>The week of the calendar (from the Monday epoch of the events) and its day, 0 = Monday.</summary>
        public long WeekKey(long now) => (long)Math.Floor((LocalDay(now) - MondayEpochDay) / 7.0);

        public int Weekday(long now) => (int)(((LocalDay(now) - MondayEpochDay) % 7 + 7) % 7);

        /// <summary>ACTIVITIES.md, 3: on Sunday the monster of the week waits on the Hunt until it is beaten:
        /// stronger, and its first win of the week pays double Cores. The family changes every week.</summary>
        public MonsterFamily? WeeklyMonster(long now)
        {
            var w = Balance.Activities.Hunt.Weekly;
            if (State.HearthLevel < Balance.Activities.Hunt.Unlock || Weekday(now) != w.Weekday || Retention.WeeklyWonWeek == WeekKey(now))
                return null;
            int families = Enum.GetValues(typeof(MonsterFamily)).Length;
            return (MonsterFamily)(int)(((WeekKey(now) % families) + families) % families);
        }

        public Fail StartHunt(MonsterFamily family, int rank, long now, out Battle battle, bool weekly = false)
        {
            battle = null;
            Advance(now);
            var hunt = Balance.Activities.Hunt;
            if (State.HearthLevel < hunt.Unlock)
                return Fail.Locked;
            if (State.Hunt != null || State.PendingMission >= 0)
                return Fail.HuntInProgress;
            if (weekly ? WeeklyMonster(now) != family : !HuntDens(now).Contains(family))
                return Fail.NotToday;
            if (rank < 1 || rank > State.HearthLevel)
                return Fail.RankTooHigh;
            if (Squad().Count == 0)
                return Fail.NoHeroes;
            if (HuntTracks(now) <= 0)
                return Fail.NoTracks;
            if (State.HuntTracks == hunt.TracksPerDay)
                State.HuntTrackAt = now + TrackSeconds;
            State.HuntTracks--;
            State.Hunt = new PendingHunt { Family = family, Rank = rank, Seed = State.NextBattleSeed++, Weekly = weekly };
            battle = MakeBattle(State.Hunt);
            return Fail.None;
        }

        Battle MakeBattle(PendingHunt hunt)
        {
            var setup = SquadSetup(hunt.Seed);
            setup.Family = hunt.Family;
            setup.Rank = hunt.Rank;
            if (hunt.Weekly)
            {
                var w = Balance.Activities.Hunt.Weekly;
                setup.Waves.Add(new WaveSpec { Family = hunt.Family, Rank = hunt.Rank, HpMult = w.HpMult, AttackMult = w.AttackMult });
            }
            return new Battle(Combat, setup);
        }

        /// <summary>Pays the hunt: Cores by stars (half for one star, three quarters for two, all for three).</summary>
        public Fail FinishHunt(Battle battle, out HuntReward reward)
        {
            reward = null;
            if (State.Hunt == null)
                return Fail.NoHunt;
            if (!battle.Over)
                battle.RunToEnd();
            var hunt = State.Hunt;
            State.Hunt = null;
            FightXp(battle); // the heroes learn (GameSession.People.cs)
            int stars = battle.Stars;
            bool weekly = hunt.Weekly && battle.Won;
            double mult = weekly ? Balance.Activities.Hunt.Weekly.CoreMult : 1;
            mult *= 1 + SpecSum(SpecEffect.HuntCores); // the library's and the archive's bestiaries (GameSession.Specs.cs)
            long cores = (long)Math.Round(Balance.Activities.Hunt.CoresByLevel[hunt.Rank - 1] * Combat.StarReward[stars] * mult);
            State.Wallet[ResourceType.Cores] += cores;
            reward = new HuntReward { Stars = stars, Cores = cores, Weekly = weekly };
            if (weekly)
                Retention.WeeklyWonWeek = WeekKey(State.Time);
            if (battle.Won)
            {
                AddSeasonXp(SeasonInfo.HuntXp);
                Count(DailyTask.Hunt);
                State.HuntWins++;
                GrantTreats(Balance.Pets.TreatsPerHuntWin); // pet treats (GDD 8.1)
                reward.Item = Drop("hunt", hunt.Rank, hunt.Seed, "Охота, ранг " + hunt.Rank); // an item of the rank
                CheckAchievements();
            }
            return Fail.None;
        }

        /// <summary>A hunt left unfinished (the game was closed) is fought out by auto-battle.</summary>
        /// <summary>A campaign battle left unfinished: fought out by auto-battle on the next start.</summary>
        public MissionReward ResolvePendingMission()
        {
            if (State.PendingMission < 0)
                return null;
            var battle = new Battle(Combat, MissionSetup(State.PendingMission, State.PendingMissionSeed)) { Brain = Brains.AutoBattle() };
            FinishMission(battle, out var reward);
            return reward;
        }

        public HuntReward ResolvePendingHunt()
        {
            if (State.Hunt == null)
                return null;
            var battle = MakeBattle(State.Hunt);
            battle.Brain = Brains.AutoBattle();
            FinishHunt(battle, out var reward);
            return reward;
        }

        // ------------------------------------------------------------------ skills

        public int SkillCap(Dweller d) =>
            Math.Min(Balance.Activities.Skills.Max, 1 + d.HeroLevel / Balance.Activities.Skills.HeroLevelsPerSkill);

        public long SkillCost(Dweller d) => Balance.Activities.Skills.Cost[d.SkillLevel - 1];

        public Fail UpgradeSkill(int dwellerId)
        {
            var d = FindDweller(dwellerId);
            if (d == null)
                return Fail.NotFound;
            if (!d.IsHero)
                return Fail.NotAHero;
            if (d.SkillLevel >= SkillCap(d))
                return Fail.SkillCap;
            long cost = SkillCost(d);
            if (State.Wallet[ResourceType.Cores] < cost)
                return Fail.NotEnough;
            State.Wallet[ResourceType.Cores] -= cost;
            d.SkillLevel++;
            Count(DailyTask.HeroGrowth);
            return Fail.None;
        }
    }
}

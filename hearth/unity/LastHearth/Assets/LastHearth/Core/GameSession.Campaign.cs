using System;
using LastHearth.Core.Combat;

namespace LastHearth.Core
{
    public sealed class MissionReward
    {
        public int Stars;
        public bool FirstClear;
        public ResourceBag Got = new ResourceBag();
        public Dweller NewHero;
    }

    /// <summary>MODES.md, 1: the campaign on the battle engine of the Hunt.</summary>
    public sealed partial class GameSession
    {
        /// <summary>Local day number: days, repeats, tasks and the Surge follow the player's clock.</summary>
        public long LocalDay(long now) => (now + State.UtcOffsetSeconds) / 86400;

        /// <summary>Seconds since local midnight.</summary>
        public long LocalSecondOfDay(long now) => (now + State.UtcOffsetSeconds) % 86400;

        public int MissionsCleared => State.CampaignStars.Count;

        public MissionData Mission(int index) => Balance.Campaign.Missions[index];

        public int RepeatsLeft(long now)
        {
            if (State.RepeatsDay != LocalDay(now))
            {
                State.RepeatsDay = LocalDay(now);
                State.RepeatsUsed = 0;
            }
            return Balance.Campaign.RepeatsPerDay - State.RepeatsUsed;
        }

        public BattleSetup MissionSetup(int index, int seed)
        {
            var m = Mission(index);
            var kind = Combat.MissionKinds[m.Kind.ToString()];
            var setup = SquadSetup(seed);
            for (int w = 0; w < kind.Waves; w++)
            {
                setup.Waves.Add(new WaveSpec
                {
                    Family = m.Family,
                    Rank = m.Rank,
                    HpMult = kind.HpMult * KennelExpected(Balance.Companions.CampaignHp, m.Rank),
                    AttackMult = kind.AttackMult * KennelExpected(Balance.Companions.CampaignAttack, m.Rank),
                    Telegraphs = kind.Telegraphs,
                });
            }
            return setup;
        }

        /// <summary>Starts the next mission, or replays a cleared one for more stars.</summary>
        public Fail StartMission(int index, long now, out Battle battle)
        {
            battle = null;
            Advance(now);
            if (index < 0 || index >= Balance.Campaign.Missions.Count || index > MissionsCleared)
                return Fail.Locked;
            if (State.PendingMission >= 0 || State.Hunt != null)
                return Fail.HuntInProgress;
            if (Squad().Count == 0)
                return Fail.NoHeroes;
            State.PendingMission = index;
            State.PendingMissionSeed = State.NextBattleSeed++;
            battle = new Battle(Combat, MissionSetup(index, State.PendingMissionSeed));
            return Fail.None;
        }

        public Fail FinishMission(Battle battle, out MissionReward reward)
        {
            reward = null;
            if (State.PendingMission < 0)
                return Fail.NoHunt;
            if (!battle.Over)
                battle.RunToEnd();
            int index = State.PendingMission;
            State.PendingMission = -1;
            reward = new MissionReward { Stars = battle.Stars };
            if (!battle.Won)
                return Fail.None;
            var m = Mission(index);
            Tutor(TutorialEvent.MissionWon);
            if (index == MissionsCleared)
            {
                reward.FirstClear = true;
                State.CampaignStars.Add(0);
                reward.Got[ResourceType.Essence] += m.Essence;
                reward.Got.Add(ResourceBag.Materials(m.Materials, Balance.SilverShare));
                reward.Got[ResourceType.Sparks] += m.Sparks;
                if (m.Kind == MissionKind.Boss)
                    reward.NewHero = StoryHero(m.Act);
            }
            Count(DailyTask.Campaign);
            if (reward.FirstClear && m.Kind == MissionKind.Boss)
                CheckAchievements();
            if (battle.Stars == 3 && State.CampaignStars[index] < 3)
                reward.Got[ResourceType.Sparks] += Balance.Campaign.ChestSparks;
            State.CampaignStars[index] = Math.Max(State.CampaignStars[index], battle.Stars);
            Pay(reward.Got);
            return Fail.None;
        }

        /// <summary>A cleared mission again, as an instant auto-battle (10 a day): a little essence and half an hour of materials.</summary>
        public Fail RepeatMission(int index, long now, out MissionReward reward)
        {
            reward = null;
            Advance(now);
            if (index < 0 || index >= MissionsCleared)
                return Fail.Locked;
            if (RepeatsLeft(now) <= 0)
                return Fail.NoTracks;
            State.RepeatsUsed++;
            var battle = new Battle(Combat, MissionSetup(index, State.NextBattleSeed++)) { Brain = Brains.AutoBattle() };
            battle.RunToEnd();
            reward = new MissionReward { Stars = battle.Stars };
            if (!battle.Won)
                return Fail.None;
            var m = Mission(index);
            var c = Balance.Campaign;
            reward.Got[ResourceType.Essence] += (long)Math.Round(m.Essence * c.RepeatEssenceShare);
            reward.Got.Add(ResourceBag.Materials((long)Math.Round(m.Materials * c.RepeatMaterialHours / c.FirstClearHours), Balance.SilverShare));
            Pay(reward.Got);
            return Fail.None;
        }

        /// <summary>Materials go through the rewards inbox (they respect the warehouse), the rest straight in.</summary>
        void Pay(ResourceBag got)
        {
            for (int i = 0; i < ResourceBag.Count; i++)
            {
                var type = (ResourceType)i;
                if (type == ResourceType.Silver || type == ResourceType.Iron)
                    State.Rewards[type] += got[type];
                else
                    State.Wallet[type] += got[type];
            }
            ClaimRewards();
        }

        /// <summary>The act boss brings a hero from the story (MODES.md, 1).</summary>
        Dweller StoryHero(int act)
        {
            var classes = new[] { HeroClass.Archer, HeroClass.CoalPriest, HeroClass.AshMage, HeroClass.Cleaver, HeroClass.Swordsman };
            string[] names = { "Вереск", "Отец Ильм", "Зола", "Брант", "Ратша", "Ива", "Северин", "Огняна", "Тур", "Елань" };
            int lvl = Math.Max(1, 2 * State.HearthLevel - 4);
            int stat = 5 + 2 * State.HearthLevel;
            var d = AddDweller(names[(act - 1) % names.Length], new[] { stat, stat, stat, stat, stat });
            d.HeroClass = classes[(act - 1) % classes.Length];
            d.HeroLevel = lvl;
            return d;
        }

        /// <summary>The squad of the fortress as battle heroes.</summary>
        BattleSetup SquadSetup(int seed)
        {
            var setup = new BattleSetup { GearLevel = State.HearthLevel, Seed = seed };
            foreach (var d in Squad())
            {
                setup.Heroes.Add(new HeroSetup
                {
                    Id = d.Id,
                    Name = d.Name,
                    Class = d.HeroClass,
                    Level = d.HeroLevel,
                    SkillLevel = d.SkillLevel,
                    Stats = (int[])d.Stats.Clone(),
                });
            }
            ApplyKennel(setup); // the active pet and the heroes' companions (GameSession.Companions.cs)
            ApplyHalls(setup); // research and ascension (GameSession.Rooms.cs)
            return setup;
        }
    }
}

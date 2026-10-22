using System;
using System.Collections.Generic;
using LastHearth.Core.Combat;

namespace LastHearth.Core
{
    public sealed class MissionReward
    {
        public int Stars;
        public bool FirstClear;
        public ResourceBag Got = new ResourceBag();
        public Dweller NewHero;
        /// <summary>Items found (GDD 8): a first clear, the chest of three stars, a repeat.</summary>
        public List<GearItem> Items = new List<GearItem>();
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
                    // the free player's kennel and finds are part of the recommended strength
                    HpMult = kind.HpMult * KennelExpected(Balance.Companions.CampaignHp, m.Rank) * GearEdge(m.Rank),
                    AttackMult = kind.AttackMult * KennelExpected(Balance.Companions.CampaignAttack, m.Rank) * GearEdge(m.Rank),
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
            int seed = State.PendingMissionSeed;
            State.PendingMission = -1;
            reward = new MissionReward { Stars = battle.Stars };
            FightXp(battle); // the heroes learn (GameSession.People.cs)
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
                string kind = m.Kind == MissionKind.Boss ? "boss" : m.Kind == MissionKind.Elite ? "elite" : "mission";
                AddFind(reward.Items, Drop(kind, m.Rank, seed, "Поход: бой " + (index + 1)));
            }
            Count(DailyTask.Campaign);
            if (reward.FirstClear && m.Kind == MissionKind.Boss)
                CheckAchievements();
            if (battle.Stars == 3 && State.CampaignStars[index] < 3)
            {
                // MODES.md, 1: the chest of three stars — Sparks and a piece of gear
                reward.Got[ResourceType.Sparks] += Balance.Campaign.ChestSparks;
                AddFind(reward.Items, Drop("chest", m.Rank, seed * 3 + 1, "Сундук трёх звёзд"));
            }
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
            int seed = State.NextBattleSeed++;
            var battle = new Battle(Combat, MissionSetup(index, seed)) { Brain = Brains.AutoBattle() };
            battle.RunToEnd();
            reward = new MissionReward { Stars = battle.Stars };
            FightXp(battle, true);
            if (!battle.Won)
                return Fail.None;
            var m = Mission(index);
            var c = Balance.Campaign;
            reward.Got[ResourceType.Essence] += (long)Math.Round(m.Essence * c.RepeatEssenceShare);
            reward.Got.Add(ResourceBag.Materials((long)Math.Round(m.Materials * c.RepeatMaterialHours / c.FirstClearHours), Balance.SilverShare));
            Pay(reward.Got);
            AddFind(reward.Items, Drop("repeat", m.Rank, seed, "Повтор боя " + (index + 1))); // "for collecting gear" (MODES.md, 1)
            return Fail.None;
        }

        static void AddFind(List<GearItem> list, GearItem item)
        {
            if (item != null)
                list.Add(item);
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
            return d; // the forge's issued kit of the hearth level fills the slots
        }

        /// <summary>The squad of the fortress as battle heroes.</summary>
        BattleSetup SquadSetup(int seed)
        {
            var setup = new BattleSetup { Seed = seed };
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
                    Gear = HeroGear(d), // the items, the sets and the forge's issued pieces (GameSession.Gear.cs)
                });
            }
            ApplyKennel(setup); // the active pet and the heroes' companions (GameSession.Companions.cs)
            ApplyHalls(setup); // research and ascension (GameSession.Rooms.cs)
            return setup;
        }
    }
}

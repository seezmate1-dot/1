using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core.Combat;

namespace LastHearth.Core
{
    /// <summary>
    /// GDD 8.1: pets. The player owns several; ONE is active and helps the whole fortress and the
    /// squad. Pets are found by play (the Hunt, the login calendar, the campaign, events, the merchant);
    /// two can be bought sooner for Sparks at a fixed price. Levels 1-10 are raised with treats.
    /// </summary>
    public sealed partial class GameSession
    {
        PetState Pets => State.Pets ?? (State.Pets = new PetState());

        public long Treats => Pets.Treats;

        public void GrantTreats(long amount) => Pets.Treats += Math.Max(0, amount);

        public PetKindData PetKind(string id) => Balance.Pets.Kinds.Find(k => k.Id == id);

        /// <summary>Level of an owned pet; 0 = not owned.</summary>
        public int PetLevel(string id) => id != null && Pets.Levels.TryGetValue(id, out int level) ? level : 0;

        public bool HasPet(string id) => PetLevel(id) > 0;

        public PetKindData ActivePet() => HasPet(Pets.Active) ? PetKind(Pets.Active) : null;

        public double PetValue(PetKindData kind, int level) => kind.Value + kind.PerLevel * (Math.Max(1, level) - 1);

        /// <summary>The bonus of the active pet when it has this effect, else 0.</summary>
        public double ActivePetValue(PetEffect effect)
        {
            var pet = ActivePet();
            return pet != null && pet.Effect == effect ? PetValue(pet, PetLevel(pet.Id)) : 0;
        }

        /// <summary>Output multiplier of the active pet for a produced resource (Owl: materials, Fox: coal and food).</summary>
        public double PetOutputMult(ResourceType resource)
        {
            switch (resource)
            {
                case ResourceType.Iron:
                case ResourceType.Silver:
                    return 1 + ActivePetValue(PetEffect.Materials);
                case ResourceType.Coal:
                case ResourceType.Food:
                    return 1 + ActivePetValue(PetEffect.Supply);
                default:
                    return 1;
            }
        }

        /// <summary>How far the player is on the way to a pet or a companion found by play.</summary>
        public int UnlockProgress(UnlockSource source)
        {
            switch (source)
            {
                case UnlockSource.Start: return 1;
                case UnlockSource.HuntWins: return State.HuntWins;
                case UnlockSource.LoginDays: return State.LoginCount;
                case UnlockSource.Missions: return State.CampaignStars.Count;
                case UnlockSource.EventThresholds: return State.Events.Count == 0 ? 0 : State.Events.Max(r => r.Thresholds);
                default: return 0; // the shop: bought, not earned
            }
        }

        bool Earned(UnlockSource source, int condition) => source != UnlockSource.Shop && UnlockProgress(source) >= condition;

        /// <summary>Gives the pets and companions whose conditions are met; returns their names (for a message).</summary>
        public List<string> CheckKennel()
        {
            var found = new List<string>();
            foreach (var kind in Balance.Pets.Kinds)
            {
                if (!HasPet(kind.Id) && Earned(kind.Source, kind.Condition))
                {
                    AddPet(kind.Id);
                    found.Add(kind.Name);
                }
            }
            foreach (var kind in Balance.Companions.Kinds)
            {
                if (!CompanionUnlocked(kind.Id) && Earned(kind.Source, kind.Condition))
                {
                    Companions.Unlocked.Add(kind.Id);
                    found.Add(kind.Name);
                }
            }
            return found;
        }

        void AddPet(string id)
        {
            Pets.Levels[id] = 1;
            if (!HasPet(Pets.Active))
                Pets.Active = id; // the first pet goes to work at once
        }

        public Fail SetActivePet(string id)
        {
            if (!HasPet(id))
                return Fail.NotFound;
            Pets.Active = id;
            return Fail.None;
        }

        /// <summary>Treats for the next level, or -1 at the top.</summary>
        public long PetFeedCost(string id)
        {
            int level = PetLevel(id);
            var costs = Balance.Pets.LevelCost;
            return level < 1 || level >= Balance.Pets.MaxLevel || level > costs.Length ? -1 : costs[level - 1];
        }

        /// <summary>A pet grows no higher than the hearth (it matters only in the first hours).</summary>
        public Fail FeedPet(string id)
        {
            if (!HasPet(id))
                return Fail.NotFound;
            long cost = PetFeedCost(id);
            if (cost < 0)
                return Fail.LimitReached;
            if (PetLevel(id) >= State.HearthLevel)
                return Fail.AboveHearth;
            if (Pets.Treats < cost)
                return Fail.NotEnough;
            Pets.Treats -= cost;
            Pets.Levels[id] = PetLevel(id) + 1;
            return Fail.None;
        }

        /// <summary>Puts the active pet into a battle: attack, health or the raven's strike.</summary>
        void ApplyPet(BattleSetup setup)
        {
            var pet = ActivePet();
            if (pet == null)
                return;
            double value = PetValue(pet, PetLevel(pet.Id));
            setup.PetName = pet.Name;
            switch (pet.Effect)
            {
                case PetEffect.Attack:
                    setup.PetAttack = 1 + value;
                    break;
                case PetEffect.Health:
                    setup.PetHp = 1 + value;
                    break;
                case PetEffect.Strike:
                    setup.PetStrike = value;
                    setup.PetEvery = Balance.Pets.StrikeEvery;
                    break;
            }
        }

        /// <summary>The text of a pet's bonus at a level, for the windows.</summary>
        public string PetBonusText(PetKindData kind, int level)
        {
            double v = PetValue(kind, level);
            switch (kind.Effect)
            {
                case PetEffect.Attack: return string.Format("атака отряда +{0:0}%", v * 100);
                case PetEffect.Health: return string.Format("здоровье отряда +{0:0}%", v * 100);
                case PetEffect.Strike: return string.Format("в бою бьёт раз в {0:0} с: {1:0}% средней атаки отряда", Balance.Pets.StrikeEvery / 10.0, v * 100);
                case PetEffect.CoalBurn: return string.Format("расход угля −{0:0}%", v * 100);
                case PetEffect.Materials: return string.Format("выработка железа и серебра +{0:0}%", v * 100);
                default: return string.Format("выработка угля и еды +{0:0}%", v * 100);
            }
        }
    }
}

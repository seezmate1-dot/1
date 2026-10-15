using System;
using System.Collections.Generic;

namespace LastHearth.Core
{
    // GDD 8.1-8.3 and 13.1: pets, companions of heroes and the shops. Numbers come from
    // hearth/tools/economy.py (balance.json: "pets", "companions", "market"); the saved part is
    // PetState, CompanionState and MarketState in GameState.

    /// <summary>What the active pet does.</summary>
    public enum PetEffect
    {
        /// <summary>Squad attack ×(1 + value).</summary>
        Attack,
        /// <summary>Squad health ×(1 + value).</summary>
        Health,
        /// <summary>A strike every few seconds of battle for value × the squad's average attack.</summary>
        Strike,
        /// <summary>Coal burn ×(1 − value).</summary>
        CoalBurn,
        /// <summary>Iron and silver output ×(1 + value).</summary>
        Materials,
        /// <summary>Coal and food output ×(1 + value).</summary>
        Supply,
    }

    /// <summary>How a pet or a companion is found by play (Sparks or silver may bring it sooner).</summary>
    public enum UnlockSource
    {
        Start,
        HuntWins,
        LoginDays,
        Missions,
        EventThresholds,
        Shop,
    }

    public sealed class PetKindData
    {
        public string Id = "";
        public string Name = "";
        public PetEffect Effect;
        public double Value;
        public double PerLevel;
        public string Text = "";
        public UnlockSource Source;
        public int Condition;
        /// <summary>Price at the merchant in hours of the fortress income (silver); 0 = not sold.</summary>
        public double SilverHours;
        /// <summary>Price in Sparks; 0 = not sold.</summary>
        public long Sparks;
    }

    public sealed class PetsData
    {
        public int MaxLevel = 10;
        /// <summary>Treats for level L -> L+1 (index L-1).</summary>
        public long[] LevelCost = new long[0];
        public int StrikeEvery = 50;
        public long TreatsPerHuntWin;
        public long TreatsDaily;
        public List<PetKindData> Kinds = new List<PetKindData>();
    }

    public sealed class CompanionKindData
    {
        public string Id = "";
        public string Name = "";
        public Combat.CompanionSkill Skill;
        /// <summary>Battle ticks (0.1 s) between two acts of the companion; 0 = always on (Guard).</summary>
        public int Every;
        public double Power;
        public double PerLevel;
        public string Text = "";
        public UnlockSource Source;
        public int Condition;
        public double SilverHours;
        public long Sparks;
    }

    public sealed class CompanionsData
    {
        public int MaxLevel = 10;
        public long[] LevelCost = new long[0];
        /// <summary>The bond level is at most 1 + hero level / this.</summary>
        public int HeroLevelsPerLevel = 4;
        /// <summary>Campaign enemies' health and attack by rank (hearth level): the free player's pet and
        /// companions are part of the recommended strength (economy.py, kennel_factors).</summary>
        public double[] CampaignHp = new double[0];
        public double[] CampaignAttack = new double[0];
        public List<CompanionKindData> Kinds = new List<CompanionKindData>();
    }

    public sealed class DealData
    {
        public string Id = "";
        public string Goods = "";
        public double Amount;
        /// <summary>Price in hours of the fortress income, paid in silver.</summary>
        public double Hours;
    }

    public sealed class MarketData
    {
        public long MerchantTreats;
        public double ExchangeFee = 0.8;
        public double ExchangeHours = 4;
        /// <summary>Treats, Sparks, per week.</summary>
        public long[] PremiumTreats = new long[0];
        /// <summary>Minutes, Sparks, per day.</summary>
        public long[] PremiumHourglass = new long[0];
        /// <summary>Sparks, per week.</summary>
        public long[] PremiumDweller = new long[0];
        public int PremiumDwellerBonus;
        public List<DealData> Deals = new List<DealData>();
        public int DealsShown = 3;
    }

    // ------------------------------------------------------------------ saved state

    [Serializable]
    public sealed class PetState
    {
        public long Treats;
        /// <summary>Owned pets and their levels.</summary>
        public Dictionary<string, int> Levels = new Dictionary<string, int>();
        /// <summary>The one pet that helps the fortress and the squad ("" = none).</summary>
        public string Active = "";
    }

    [Serializable]
    public sealed class HeroCompanion
    {
        public string Kind = "";
        /// <summary>Bond level 1-10: belongs to the hero, kept when the kind changes.</summary>
        public int Level = 1;
    }

    [Serializable]
    public sealed class CompanionState
    {
        public List<string> Unlocked = new List<string>();
        /// <summary>Hero (dweller id) -> the companion that walks with them.</summary>
        public Dictionary<int, HeroCompanion> Heroes = new Dictionary<int, HeroCompanion>();
    }

    [Serializable]
    public sealed class MarketState
    {
        public long Day = -1;
        public long Week = -1;
        /// <summary>Bought today / this week, by offer id (limits).</summary>
        public Dictionary<string, int> Today = new Dictionary<string, int>();
        public Dictionary<string, int> ThisWeek = new Dictionary<string, int>();
        /// <summary>Value exchanged at the trading post today (in silver).</summary>
        public long Exchanged;
    }

    /// <summary>One line of a shop: what it gives, the price, how many are left.</summary>
    public sealed class Offer
    {
        public string Id = "";
        public string Name = "";
        public string Text = "";
        public ResourceType PayWith;
        public long Price;
        /// <summary>How many can be bought now (0: sold out or not available).</summary>
        public int Left;
        public string Limit = "";
    }
}

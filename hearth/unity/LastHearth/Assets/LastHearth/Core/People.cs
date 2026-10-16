using System;
using System.Collections.Generic;

namespace LastHearth.Core
{
    // GDD 7-8: the dwellers' traits and levels and the spirit of the fortress («Дух крепости»). The rules live in
    // GameSession.People.cs; the numbers come from hearth/tools/economy.py (balance.json: "people"; docs/ECONOMY.md,
    // section 17); the saved part is PeopleState in GameState and Dweller.Level / Xp / Traits. Rarity is the tavern's
    // (Halls.cs): it decides how often a level-up adds a stat point and how likely the trait is a good one.

    /// <summary>GDD 8: a trait of character. Good ones first, then the bad ones (the order of balance.json).</summary>
    public enum Trait
    {
        Hardworking,
        Brave,
        Cheerful,
        Hardy,
        Keeper,
        Lazy,
        Coward,
        Gloomy,
        Sickly,
    }

    /// <summary>News of the fortress that lifts or lowers its spirit for some hours.</summary>
    public enum MoraleNews
    {
        SurgeWon,
        SurgeLost,
        Birth,
        HearthUp,
        Desertion,
    }

    // ------------------------------------------------------------------ balance

    public sealed class TraitData
    {
        public Trait Id;
        public string Name = "";
        public bool Good;
        /// <summary>Output of the dweller's work ×(1 + Work).</summary>
        public double Work;
        /// <summary>Attack and health of the hero in battle ×(1 + Battle).</summary>
        public double Battle;
        /// <summary>Added to the spirit of the fortress (with the others, at most PeopleData.TraitMoraleCap).</summary>
        public double Morale;
        /// <summary>Experience ×(1 + Xp).</summary>
        public double Xp;
        /// <summary>Output in a frozen room instead of Warmth.FrozenOutput (0: as everyone).</summary>
        public double Cold;
        /// <summary>Chance of the illness from the fog ×this.</summary>
        public double Illness = 1;
        public string Text = "";
    }

    public sealed class MoraleModeData
    {
        public HearthMode Mode;
        public double Value;
    }

    public sealed class MoraleNewsData
    {
        public MoraleNews Id;
        public string Name = "";
        public double Value;
        public double Hours;
    }

    /// <summary>A band of the spirit: from this value on, the rooms produce ×(1 + Output).</summary>
    public sealed class MoraleBandData
    {
        public double From;
        public string Name = "";
        public double Output;
    }

    public sealed class MoraleData
    {
        public double Base = 50;
        /// <summary>The spirit moves towards its target: 63% of the way in this many hours.</summary>
        public double Hours = 3;
        public double FoodHours = 12;
        public double Fed;
        public double Hunger;
        public double NoCoal;
        public double Frozen;
        public List<MoraleModeData> Modes = new List<MoraleModeData>();
        public double Decor;
        public double DecorMax;
        public double ChapelBase;
        public int ChapelLevels = 6;
        public int BedsBase = 6;
        public int BedsPerLevel = 2;
        public double Crowd;
        public double CrowdMax;
        public double Patient;
        public double PatientMax;
        public List<MoraleNewsData> News = new List<MoraleNewsData>();
        public List<MoraleBandData> Bands = new List<MoraleBandData>();
        public double DesertBelow = 20;
        public double DesertHours = 8;
        public int DesertersKept = 3;
        public double DeserterDays = 3;
        public double RecallMorale = 40;
    }

    public sealed class PeopleData
    {
        public int MaxLevel = 60;
        public double XpBase = 2;
        public double XpGrowth = 1.25;
        public double XpWork = 1;
        public double XpTraining = 2;
        public double XpSick = 0.5;
        public double XpWin = 2;
        public double XpLoss = 1;
        public double XpRepeat = 1;
        public int SecondTraitLevel = 30;
        public List<TraitData> Traits = new List<TraitData>();
        public double TraitMoraleCap = 9;
        public MoraleData Morale = new MoraleData();
    }

    // ------------------------------------------------------------------ saved state

    /// <summary>A piece of news that changes the spirit until a moment.</summary>
    [Serializable]
    public sealed class MoraleBoost
    {
        public MoraleNews Id;
        public long Until;
    }

    /// <summary>A dweller who left into the fog: they wait there for a few days and can be called back.</summary>
    [Serializable]
    public sealed class Deserter
    {
        public Dweller Dweller = new Dweller();
        public Rarity Rarity;
        public long Left;
        public long Until;
    }

    [Serializable]
    public sealed class PeopleState
    {
        /// <summary>The spirit of the fortress 0-100; below 0: not counted yet (a new game or an old save takes its target).</summary>
        public double Morale = -1;
        public long MoraleAt;
        /// <summary>Since when the spirit is in despair (0: it is not).</summary>
        public long LowSince;
        public List<MoraleBoost> News = new List<MoraleBoost>();
        public List<Deserter> Deserters = new List<Deserter>();
        public int DesertedTotal;
        public int Recalled;
        /// <summary>Up to when the experience of work has been counted.</summary>
        public long XpAt;
    }

    /// <summary>One line of the spirit's breakdown: what and how much.</summary>
    public sealed class MoraleFactor
    {
        public string Name = "";
        public double Value;
    }
}

using System;
using System.Collections.Generic;

namespace LastHearth.Core
{
    // GDD 6: the work of the rooms that produce nothing: the tavern, the infirmary, the library, the
    // training hall, the watchtower, the chapel and the archive (GameSession.Rooms.cs). Numbers come from
    // hearth/tools/economy.py (balance.json: "halls"; docs/ECONOMY.md, section 16); the saved part is
    // HallsState in GameState. The forge is the equipment's.

    /// <summary>GDD 8: how rare a dweller from the tavern is; it adds to the stats and to the training cap.</summary>
    public enum Rarity
    {
        Common,
        Seasoned,
        Rare,
        Legendary,
    }

    /// <summary>What a research of the library gives.</summary>
    public enum ResearchEffect
    {
        /// <summary>Coal and food output ×(1 + value).</summary>
        Supply,
        /// <summary>Iron and silver output ×(1 + value).</summary>
        Materials,
        /// <summary>Squad attack ×(1 + value).</summary>
        Attack,
        /// <summary>Squad health ×(1 + value).</summary>
        Health,
        /// <summary>Coal burn ×(1 − value).</summary>
        CoalBurn,
        /// <summary>The dwellers eat ×(1 − value).</summary>
        Food,
        /// <summary>Healing in the infirmary +value to the speed.</summary>
        Healing,
        /// <summary>Training time ÷(1 + value).</summary>
        Training,
        /// <summary>Research time ÷(1 + value).</summary>
        ResearchSpeed,
        /// <summary>+value to the luck of the tavern.</summary>
        RecruitLuck,
        /// <summary>Surge defenders +value.</summary>
        SurgeDefense,
        /// <summary>Reading records in the archive +value to the speed.</summary>
        Decipher,
    }

    /// <summary>What the chapel gives as the gift of the day.</summary>
    public enum GiftGoods
    {
        Essence,
        Treats,
        Hourglass,
    }

    /// <summary>What the watchtower shows from a level.</summary>
    public enum ScoutInfo
    {
        Surge,
        Dens,
        Spawn,
        Route,
        Forecast,
    }

    /// <summary>What finds a record of the archive.</summary>
    public enum RecordKind
    {
        Hearth,
        Missions,
        Hunts,
        Routes,
        Research,
        Ascension,
    }

    // ------------------------------------------------------------------ balance

    public sealed class HallsData
    {
        public TavernData Tavern = new TavernData();
        public InfirmaryData Infirmary = new InfirmaryData();
        public LibraryData Library = new LibraryData();
        public TrainingData Training = new TrainingData();
        public WatchtowerData Watchtower = new WatchtowerData();
        public ChapelData Chapel = new ChapelData();
        public ArchiveData Archive = new ArchiveData();
    }

    public sealed class RarityData
    {
        public Rarity Id;
        public string Name = "";
        /// <summary>Added to every stat of the recruit.</summary>
        public int StatBonus;
        /// <summary>Added to the training cap of the dweller.</summary>
        public int CapBonus;
        /// <summary>Weight in % without luck.</summary>
        public double Weight;
        /// <summary>Silver price in hours of the material income.</summary>
        public double HireHours;
    }

    public sealed class TavernData
    {
        public int Board = 3;
        public int[] BoardExtraAt = new int[0];
        public int BoardHours = 6;
        public List<RarityData> Rarities = new List<RarityData>();
        /// <summary>Weight moved from the common recruits to each rarity by one point of luck.</summary>
        public double[] LuckShift = new double[0];
        public double MinCommon;
        public double LuckPerLevel;
        public double LuckPerSpirit;
        /// <summary>Food price: hours of the recruit's food × (1 + rarity).</summary>
        public double FoodHours;
    }

    public sealed class InfirmaryData
    {
        public double InjuryHours = 6;
        public double IllnessHours;
        public double IllnessChance;
        public double SickOutput = 1;
        public double PerLevel;
        public double IntellectDivisor = 50;
        public double Blazing = 1;
    }

    public sealed class ResearchBranchData
    {
        public string Id = "";
        public string Name = "";
    }

    public sealed class ResearchNodeData
    {
        public string Id = "";
        public string Branch = "";
        public int Tier;
        public string Name = "";
        public ResearchEffect Effect;
        public double Value;
        /// <summary>Library level the step needs.</summary>
        public int Gate;
        public long Seconds;
        /// <summary>Materials (silver and iron by the silver share).</summary>
        public long Cost;
    }

    public sealed class LibraryData
    {
        public double IntellectDivisor = 100;
        public List<ResearchBranchData> Branches = new List<ResearchBranchData>();
        public List<ResearchNodeData> Nodes = new List<ResearchNodeData>();
    }

    public sealed class TrainingData
    {
        public int CapBase;
        public int CapPerLevel;
        public double HoursBase;
        public double HoursPerPoint;
        public double LevelFactor = 1;
        public double SilverHours;
        public double SilverStatDivisor = 50;
    }

    public sealed class ScoutLevelData
    {
        public ScoutInfo What;
        public int Level;
    }

    public sealed class WatchtowerData
    {
        public List<ScoutLevelData> Scout = new List<ScoutLevelData>();
        public double DefensePerLevel;
        public double AgilityDivisor = 400;
    }

    public sealed class AscensionData
    {
        public int HeroLevel;
        public int Chapel;
        public double EssenceDays;
    }

    public sealed class ChapelGift
    {
        public GiftGoods Goods;
        public double Amount;
    }

    public sealed class ChapelData
    {
        public List<AscensionData> Ranks = new List<AscensionData>();
        public double Bonus;
        public List<ChapelGift> Gifts = new List<ChapelGift>();
    }

    public sealed class RecordData
    {
        public string Id = "";
        public string Title = "";
        public RecordKind Kind;
        public int Need;
        public string Text = "";
    }

    public sealed class ArchiveData
    {
        public double HoursBase;
        public double HoursPerRecord;
        public double PerLevel;
        public double IntellectDivisor = 100;
        public double EssenceDays;
        public List<RecordData> Records = new List<RecordData>();
    }

    // ------------------------------------------------------------------ saved state

    /// <summary>A refugee on the tavern's board.</summary>
    [Serializable]
    public sealed class Recruit
    {
        public string Name = "";
        public Sex Sex;
        public int[] Stats = new int[5];
        public Rarity Rarity;
        public bool Hired;
        /// <summary>A refugee a new hearth level brought: free to take in, like before the tavern.</summary>
        public bool Free;
    }

    [Serializable]
    public sealed class TavernState
    {
        /// <summary>When the board on show was put up (0: never).</summary>
        public long BoardAt;
        public List<Recruit> Board = new List<Recruit>();
        public int Hired;
        public int Dismissed;
    }

    /// <summary>A dweller training a stat in the training hall.</summary>
    [Serializable]
    public sealed class TrainingSlot
    {
        public int DwellerId;
        public Stat Stat;
        public long Start;
        public long End;
    }

    [Serializable]
    public sealed class HallsState
    {
        /// <summary>Seed of the next tavern board or illness roll.</summary>
        public int Seed = 1;
        public TavernState Tavern = new TavernState();
        /// <summary>Rarity of dwellers hired in the tavern (others are common).</summary>
        public Dictionary<int, Rarity> Rarity = new Dictionary<int, Rarity>();

        // Infirmary
        /// <summary>Until when the illness from the fog lasts, by dweller.</summary>
        public Dictionary<int, long> SickUntil = new Dictionary<int, long>();
        /// <summary>The end of each injury as the infirmary last saw it (a new injury has another end).</summary>
        public Dictionary<int, long> KnownInjury = new Dictionary<int, long>();
        /// <summary>Up to when healing has been counted.</summary>
        public long HealedAt;
        /// <summary>Expeditions already checked for the illness.</summary>
        public List<int> RoutesChecked = new List<int>();

        // Library
        public List<string> Researched = new List<string>();
        public string Research = "";
        public long ResearchStart;
        public long ResearchEnd;

        // Training hall
        public List<TrainingSlot> Training = new List<TrainingSlot>();
        public int PointsTrained;

        // Chapel
        public Dictionary<int, int> Ascension = new Dictionary<int, int>();
        public long GiftDay = -1;

        // Archive
        public List<string> Read = new List<string>();
        public string Reading = "";
        public long ReadingEnd;
    }
}

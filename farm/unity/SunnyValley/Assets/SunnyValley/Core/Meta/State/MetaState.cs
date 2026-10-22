using System.Collections.Generic;

namespace SunnyValley.Core.Meta
{
    /// <summary>
    /// The whole saved meta progress. Plain data: every timer is an absolute time (seconds, the same
    /// clock the caller passes as <c>now</c>), so offline progress is just "is the time past?".
    /// </summary>
    public sealed class MetaState
    {
        public int Version = SaveSystem.CurrentVersion;
        public long CreatedAt;
        /// <summary>Last time the player was in the game (for «пока вас не было»).</summary>
        public long LastSeenAt;
        /// <summary>Local time offset of the device, minutes (day boundaries for daily tasks).</summary>
        public int UtcOffsetMinutes;
        public ulong Rng;

        public Wallet Wallet = new Wallet();
        public SortedDictionary<string, int> Tools = new SortedDictionary<string, int>();
        public SortedDictionary<string, int> Boosters = new SortedDictionary<string, int>();
        /// <summary>Goods in the barn.</summary>
        public SortedDictionary<string, int> Barn = new SortedDictionary<string, int>();
        public int BarnUpgrades;

        public LivesState Lives = new LivesState();
        public Match3State Match3 = new Match3State();
        public ChapterState Chapter = new ChapterState();
        public FarmState Farm = new FarmState();
        public OrdersState Orders = new OrdersState();
        public TownState Town = new TownState();
        public DailyState Daily = new DailyState();
        /// <summary>Lifetime counters (stats screen, bot checks).</summary>
        public SortedDictionary<string, long> Stats = new SortedDictionary<string, long>();
        /// <summary>Where the UI placed things on the farm map: key → [x, y]. The core only stores it.</summary>
        public SortedDictionary<string, int[]> Layout = new SortedDictionary<string, int[]>();
    }

    public sealed class Wallet
    {
        public long Coins;
        public int Stars;
        public int Crystals;
        public long Xp;
        public int Level = 1;
    }

    public sealed class LivesState
    {
        public int Count;
        /// <summary>When the regeneration of the next life started (meaningful while Count &lt; max).</summary>
        public long RegenFrom;
        public long InfiniteUntil;
    }

    public sealed class Match3State
    {
        /// <summary>The level the "Играть N" button starts.</summary>
        public int NextLevel = 1;
        /// <summary>The level being played now (0 = none).</summary>
        public int Active;
        /// <summary>How many times +5 moves were bought in the current attempt.</summary>
        public int ExtraMovesBought;
        public int Wins;
        public int Losses;
        /// <summary>Wins counted for the booster gift rotation.</summary>
        public int BoosterRotationIndex;
    }

    public sealed class ChapterState
    {
        public List<string> DoneTasks = new List<string>();
        /// <summary>Task id → chosen look id.</summary>
        public SortedDictionary<string, string> Choices = new SortedDictionary<string, string>();
        public List<string> CompletedZones = new List<string>();
        public List<string> SeenScenes = new List<string>();
        public bool Finished;
        public long FinishedAt;
        public int StarsSpent;
    }

    public sealed class FarmState
    {
        public bool Open;
        public long OpenedAt;
        public List<PlotState> Plots = new List<PlotState>();
        public List<TreeState> Trees = new List<TreeState>();
        public List<string> AnimalHouses = new List<string>();
        public List<AnimalState> Animals = new List<AnimalState>();
        public List<WorkshopState> Workshops = new List<WorkshopState>();
        public int Expansions;
        /// <summary>Next instance number for trees and animals (stable ids for the UI).</summary>
        public int NextUid = 1;
    }

    public sealed class PlotState
    {
        /// <summary>Crop item id, null when empty.</summary>
        public string Crop;
        public long PlantedAt;
        public long ReadyAt;
    }

    public sealed class TreeState
    {
        public int Uid;
        public string Type;
        public long ReadyAt;
    }

    public sealed class AnimalState
    {
        public int Uid;
        public string Type;
        public bool Fed;
        public long ReadyAt;
    }

    public sealed class WorkshopState
    {
        public string Id;
        public int Slots;
        /// <summary>In order; job i starts when job i-1 ends. Finished jobs stay until collected.</summary>
        public List<JobState> Jobs = new List<JobState>();
    }

    public sealed class JobState
    {
        public string Recipe;
        public long StartAt;
        public long EndAt;
    }

    public sealed class OrdersState
    {
        public List<OrderCard> Board = new List<OrderCard>();
        public TruckState Truck = new TruckState();
        public int Fulfilled;
    }

    public sealed class OrderCard
    {
        /// <summary>When the card shows an order (a new one is generated at that moment).</summary>
        public long AvailableAt;
        /// <summary>Null while the card waits for <see cref="AvailableAt"/>.</summary>
        public Order Order;
    }

    public sealed class Order
    {
        public int Uid;
        /// <summary>Character who asks (flavour for the card).</summary>
        public string From;
        public SortedDictionary<string, int> Items = new SortedDictionary<string, int>();
        public int Coins;
        public int Xp;
        public SortedDictionary<string, int> Tools = new SortedDictionary<string, int>();
        public long CreatedAt;
    }

    public sealed class TruckState
    {
        /// <summary>Crates to fill: item → units, and whether each crate is already loaded.</summary>
        public List<TruckCrate> Crates = new List<TruckCrate>();
        /// <summary>The truck is away until this time (0 = at the farm).</summary>
        public long AwayUntil;
        public int Coins;
        public int Xp;
        public SortedDictionary<string, int> Tools = new SortedDictionary<string, int>();
        public int Trips;
    }

    public sealed class TruckCrate
    {
        public string Item;
        public int Count;
        public bool Loaded;
    }

    public sealed class TownState
    {
        /// <summary>Building id → how many are built.</summary>
        public SortedDictionary<string, int> Buildings = new SortedDictionary<string, int>();
    }

    public sealed class DailyState
    {
        /// <summary>Local day number the tasks belong to.</summary>
        public long Day = -1;
        public List<DailyTaskState> Tasks = new List<DailyTaskState>();
        public bool ChestClaimed;
        /// <summary>Login gifts taken so far (index into the 7-day cycle = count % 7).</summary>
        public int LoginGifts;
        public long LastLoginGiftDay = -1;
    }

    public sealed class DailyTaskState
    {
        public string Id;
        public int Progress;
        public bool Claimed;
    }
}

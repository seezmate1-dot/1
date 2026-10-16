using System;
using System.Collections.Generic;

namespace LastHearth.Core
{
    /// <summary>Everything that is saved. Plain data: the rules live in GameSession.</summary>
    /// <summary>A hunt that has started; if the game closes mid-fight it is finished by auto-battle.</summary>
    [Serializable]
    public sealed class PendingHunt
    {
        public Combat.MonsterFamily Family;
        public int Rank;
        public int Seed;
        /// <summary>The monster of the week (Sunday): stronger, pays more Cores.</summary>
        public bool Weekly;
    }

    public enum RoutePath
    {
        Safe,
        Risky,
        Long,
    }

    [Serializable]
    public sealed class Expedition
    {
        public int Id;
        public List<int> Team = new List<int>();
        public RoutePath[] Forks = new RoutePath[2];
        public long Start;
        public long End;
        public int Seed;
        public int HearthLevel;
        /// <summary>Set when the team is back: what it brought and the decision card waiting for the player.</summary>
        public bool Back;
        public string Report = "";
        public int Card = -1;
    }

    public enum ShopGoods
    {
        Essence,
        Coal,
        Food,
        Hourglass,
        RecruitSeal,
        HourglassDeal,
    }

    /// <summary>An arena defense or a fortress of another hearth (a ghost until the server exists).</summary>
    [Serializable]
    public sealed class PvpOpponent
    {
        public string Name = "";
        public int HearthLevel;
        public int Rating;
        /// <summary>Strength against the player's squad: 1 = equal.</summary>
        public double Power = 1;
        public int Seed;
        /// <summary>Revenge only: the free raid lasts until then.</summary>
        public long Until;
    }

    /// <summary>A started arena fight or raid (finished by auto-battle if the game closes).</summary>
    [Serializable]
    public sealed class PvpBattle
    {
        public bool Raid;
        public bool Revenge;
        public PvpOpponent Opponent = new PvpOpponent();
        public int Seed;
    }

    [Serializable]
    public sealed class ShopItem
    {
        public ShopGoods Goods;
        public long Amount;
        public ResourceType PayWith;
        public long Price;
        public bool Sold;
    }

    /// <summary>A battle of an event that has started (finished by auto-battle if the game closes).</summary>
    [Serializable]
    public sealed class EventBattle
    {
        public string EventId = "";
        public int Seed;
        public int Cell = -1;
    }

    [Serializable]
    public sealed class Outpost
    {
        public List<int> Garrison = new List<int>();
        public int Traps;
        public bool Fallen;
    }

    /// <summary>One occurrence of a live event and everything its mechanics keep.</summary>
    [Serializable]
    public sealed class EventRun
    {
        public string Id = "";
        public long Start;
        public long End;
        public bool Personal;
        public bool Closed;
        public double Points;
        public int Thresholds;
        public int Seed;
        public string LastResult = "";

        // the event currency, its shop and the end of the event
        public long Currency;
        public Dictionary<string, int> Bought = new Dictionary<string, int>();
        /// <summary>Sparks this run has paid (thresholds, the place, chests).</summary>
        public long SparksWon;
        public int Rank;
        /// <summary>The reward of the place is taken (by the player, or by itself a day after the end).</summary>
        public bool Claimed;
        /// <summary>A day after the end: the currency left turned into silver, the shop closed.</summary>
        public bool Burned;

        // per local day
        public long Day = -1;
        public int Attempts;
        public int Extra;
        public double DayBest;
        public List<double> DayBests = new List<double>();

        // Portal
        public int Floor;
        public int Checkpoint;
        public bool RunAlive;
        public int Revives;
        public long FreeRunDay = -1;
        public List<int> HpIds = new List<int>();
        public List<double> HpShares = new List<double>();
        public List<int> Blessings = new List<int>();
        public List<int> BlessingOffer = new List<int>();
        /// <summary>The door chosen on this floor (GameSession.PortalDoor), -1: none yet.</summary>
        public int Door = -1;
        /// <summary>Ash a blessing of the merchant's offer costs; 0: a free offer (every third floor).</summary>
        public long OfferPrice;

        // Long Night
        /// <summary>When the group exhausted the Reaper's pool (the «Рассвет» chest), 0: not yet.</summary>
        public long DawnAt;
        public int DawnTier;

        // Treasure
        public List<int> Opened = new List<int>();
        public int Torches;
        public long TorchAt;
        public int MapNo;
        public int Pieces;
        public int Treasures;

        // Fair
        public long RateKey = -1;
        public double[] Rates = new double[4];
        public List<long[]> Orders = new List<long[]>();
        public int Trades;
        public bool Wanderer;

        // Siege
        public List<Outpost> Outposts = new List<Outpost>();
        public long Stone;
        public List<long> WindowsDone = new List<long>();
        public long LiveWindow = -1;
        public int Repairs;
        /// <summary>The final window: the outpost where the squad stands (-1: the weakest one).</summary>
        public int Stand = -1;
        /// <summary>Fair: the rates of the next 4 hours are revealed for this key.</summary>
        public long HintKey = -1;
    }

    [Serializable]
    public sealed class GameState
    {
        /// <summary>2: obstacles, merged rooms and births. Older saves are upgraded on load (GameSession).</summary>
        public int SaveVersion = 2;

        /// <summary>Seconds (Unix time) up to which the fortress has been simulated.</summary>
        public long Time;

        /// <summary>Offset of the player's local time from UTC: days, the evening Surge and tasks follow it.</summary>
        public int UtcOffsetSeconds;

        public int HearthLevel = 1;
        public HearthMode Mode = HearthMode.Steady;
        public bool FourthBuilder;

        /// <summary>True while the hearth has no coal: every room works as frozen.</summary>
        public bool OutOfCoal;

        /// <summary>Coal and food already used up but not yet taken from the wallet (fractions).</summary>
        public double CoalDebt;
        public double FoodDebt;

        public ResourceBag Wallet = new ResourceBag();

        /// <summary>Quest and event rewards waiting to be claimed into the warehouse.</summary>
        public ResourceBag Rewards = new ResourceBag();

        /// <summary>Free hourglass time the player owns (GDD 9, ECONOMY.md section 9).</summary>
        public long HourglassSeconds;

        public List<Room> Rooms = new List<Room>();
        public List<Dweller> Dwellers = new List<Dweller>();
        public BuildQueue Queue = new BuildQueue();
        public int NextRoomId = 1;
        public int NextDwellerId = 1;

        /// <summary>Cells whose obstacle is cleared (GameSession.CellKey); built cells count as cleared anyway.</summary>
        public List<int> Cleared = new List<int>();
        /// <summary>Couples in the living quarters waiting for a child.</summary>
        public List<Pairing> Pairings = new List<Pairing>();

        // Hunt (ACTIVITIES.md, 3)
        public int HuntTracks = 3;
        /// <summary>When the next track appears (0 = tracks are full).</summary>
        public long HuntTrackAt;
        public PendingHunt Hunt;
        public int NextBattleSeed = 1;

        // Surge and the Heart of the Hearth (ACTIVITIES.md, 2)
        /// <summary>Last local day whose Surge is settled (live or automatic).</summary>
        public long SurgeDay = -1;
        public bool SurgeLive;
        /// <summary>After a lost Surge the far floors stay frozen until the morning.</summary>
        public long FrostUntil;
        /// <summary>Talents bought in the three branches: warmth, economy, defense.</summary>
        public int[] Heart = new int[3];

        /// <summary>Room specializations by room type: chosen option (0 none, 1 or 2) and rank of each tier.</summary>
        public Dictionary<RoomType, int[]> SpecChoice = new Dictionary<RoomType, int[]>();
        public Dictionary<RoomType, int[]> SpecRank = new Dictionary<RoomType, int[]>();

        // Expedition routes (ACTIVITIES.md, 4)
        public List<Expedition> Expeditions = new List<Expedition>();
        public int NextExpeditionId = 1;
        public long RoutesDay;
        public int RoutesStarted;

        // Daily tasks, login calendar, achievements (MODES.md, 7)
        public long DailyDay = -1;
        public List<int> DailyTasks = new List<int>();
        public int[] DailyProgress = new int[8];
        public bool DailyClaimed;
        public long LastLoginDay = -1;
        public int LoginCount;
        public int HuntWins;
        public List<string> Achievements = new List<string>();
        /// <summary>The calendar's claims, the season, the warm week, the monster of the week, notifications (Retention.cs).</summary>
        public RetentionState Retention = new RetentionState();

        // Shops (MODES.md, 4)
        public long PacksWeek = -1;
        public int PacksBought;
        public long ShopKey = -1;
        public int ShopRefreshes;
        public long ShopRefreshDay = -1;
        public List<ShopItem> Shop = new List<ShopItem>();

        // Decorations (cosmetic): how many of each the player owns, and which one stands in a room
        public Dictionary<string, int> DecorOwned = new Dictionary<string, int>();
        public Dictionary<int, string> RoomDecor = new Dictionary<int, string>();

        // Arena and raids (MODES.md, 2-3)
        public int ArenaRating;
        public int ArenaTickets = -1;
        public long ArenaTicketAt;
        public long ArenaBuyDay = -1;
        public int ArenaBought;
        public long ArenaWeek = -1;
        public int ArenaWeekFights;
        public int ArenaWins;
        public long ArenaListAt;
        public List<PvpOpponent> ArenaList = new List<PvpOpponent>();
        public List<int> ArenaDefense = new List<int>();
        public long DefenseDay = -1;
        public int RaidFlags = -1;
        public long RaidFlagAt;
        public List<PvpOpponent> RaidList = new List<PvpOpponent>();
        public List<PvpOpponent> Revenge = new List<PvpOpponent>();
        public long GhostRaidDay = -1;
        public int RaidWins;
        public PvpBattle PvpBattle;
        public List<string> PvpLog = new List<string>();
        public long GloryWeek = -1;
        public int[] GloryBought = new int[3];
        /// <summary>The arena and raid journal: our fights and the attacks on us, the newest last (GameSession.Pvp.cs).</summary>
        public List<PvpRecord> PvpJournal = new List<PvpRecord>();
        /// <summary>The fortress's raid defense: three lines (upper halls, barracks, Hearth), filled lazily.</summary>
        public List<RaidLine> RaidDefense = new List<RaidLine>();
        /// <summary>Fortification levels: walls, watch fires (MODES.md, 5).</summary>
        public int[] Fortifications = new int[2];

        // Live events (EVENTS.md)
        public List<EventRun> Events = new List<EventRun>();
        public List<string> EventsSeen = new List<string>();
        /// <summary>Titles and banners of first places in events (cosmetic).</summary>
        public List<string> Trophies = new List<string>();
        public EventBattle EventBattle;

        // Campaign (MODES.md, 1)
        /// <summary>Best stars of every mission; its length is the number of missions cleared.</summary>
        public List<int> CampaignStars = new List<int>();
        public int PendingMission = -1;
        public int PendingMissionSeed;
        public int RepeatsUsed;
        public long RepeatsDay;

        // Pets, companions of heroes, the merchant's and the Sparks shops (GDD 8.1-8.3, 13.1; Kennel.cs)
        public PetState Pets = new PetState();
        public CompanionState Companions = new CompanionState();
        public MarketState Market = new MarketState();

        // The work of the halls: tavern, infirmary, library, training hall, chapel, archive (GDD 6; Halls.cs)
        public HallsState Halls = new HallsState();

        // The first session (MODES.md, 10; Tutorial.cs): a save without it loads with the tutorial done
        public TutorialState Tutorial = new TutorialState();

        /// <summary>The name the player gave the fortress ("" until then: «Последний очаг»), and how many times it was renamed.</summary>
        public string FortressName = "";
        public int Renames;
    }
}

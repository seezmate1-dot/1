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

    [Serializable]
    public sealed class GameState
    {
        public int SaveVersion = 1;

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

        // Campaign (MODES.md, 1)
        /// <summary>Best stars of every mission; its length is the number of missions cleared.</summary>
        public List<int> CampaignStars = new List<int>();
        public int PendingMission = -1;
        public int PendingMissionSeed;
        public int RepeatsUsed;
        public long RepeatsDay;
    }
}

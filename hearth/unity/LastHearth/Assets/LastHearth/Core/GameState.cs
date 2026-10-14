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

    [Serializable]
    public sealed class GameState
    {
        public int SaveVersion = 1;

        /// <summary>Seconds (Unix time) up to which the fortress has been simulated.</summary>
        public long Time;

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
    }
}

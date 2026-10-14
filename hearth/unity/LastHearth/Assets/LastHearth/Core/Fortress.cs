using System;
using System.Collections.Generic;

namespace LastHearth.Core
{
    /// <summary>GDD 6: room types of the fortress.</summary>
    public enum RoomType
    {
        Hearth,
        Bedrooms,
        Kitchen,
        CoalMine,
        Barracks,
        Warehouse,
        Mine,
        Tavern,
        TradingPost,
        Forge,
        Infirmary,
        Library,
        TrainingHall,
        Greenhouse,
        Watchtower,
        Chapel,
        Archive,
    }

    /// <summary>GDD 8: five stats of a dweller.</summary>
    public enum Stat
    {
        Strength,
        Agility,
        Intellect,
        Endurance,
        Spirit,
    }

    /// <summary>GDD 7: how hard the hearth burns.</summary>
    public enum HearthMode
    {
        Smoldering,
        Steady,
        Blazing,
    }

    public enum HeroClass
    {
        None,
        Guardian,
        Swordsman,
        Cleaver,
        Archer,
        CoalPriest,
        AshMage,
    }

    [Serializable]
    public sealed class Room
    {
        public int Id;
        public RoomType Type;
        public int Level = 1;
        public int Floor;
        public int Cell;
        public List<int> Workers = new List<int>();

        /// <summary>Output waiting in the room; at most OfflineCapHours of production.</summary>
        public double Stock;
    }

    [Serializable]
    public sealed class Dweller
    {
        public int Id;
        public string Name = "";
        public int[] Stats = new int[5];
        public int RoomId = -1;
        public HeroClass HeroClass = HeroClass.None;
        public int HeroLevel;

        [Newtonsoft.Json.JsonIgnore]
        public bool IsHero => HeroClass != HeroClass.None;

        public int StatOf(Stat stat) => Stats[(int)stat];
    }
}

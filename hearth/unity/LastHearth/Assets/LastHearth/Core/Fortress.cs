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

    /// <summary>GDD 8: a dweller's sex; Unknown only in saves made before births existed.</summary>
    public enum Sex
    {
        Unknown,
        Male,
        Female,
    }

    [Serializable]
    public sealed class Room
    {
        public int Id;
        public RoomType Type;
        public int Level = 1;
        public int Floor;
        /// <summary>The leftmost cell of the room.</summary>
        public int Cell;
        /// <summary>Cells the room spans to the right of Cell: merged rooms are 2 or 3 wide (GDD 6).</summary>
        public int Width = 1;
        public List<int> Workers = new List<int>();

        /// <summary>Output waiting in the room; at most OfflineCapHours of production.</summary>
        public double Stock;

        public bool Covers(int floor, int cell) => Floor == floor && cell >= Cell && cell < Cell + Width;
    }

    /// <summary>A man and a woman in the living quarters waiting for a child (GDD 8).</summary>
    [Serializable]
    public sealed class Pairing
    {
        public int RoomId;
        public int ManId;
        public int WomanId;
        public long Start;
        /// <summary>When the child is born (later, if the fortress is full then).</summary>
        public long End;
        public int Seed;
    }

    [Serializable]
    public sealed class Dweller
    {
        public int Id;
        public string Name = "";
        public Sex Sex;
        public int[] Stats = new int[5];
        public int RoomId = -1;
        public HeroClass HeroClass = HeroClass.None;
        public int HeroLevel;
        /// <summary>Level of the hero's special skill, 1-10, raised with Cores (ACTIVITIES.md, 3).</summary>
        public int SkillLevel = 1;
        /// <summary>Id of the expedition the dweller walks with, or 0.</summary>
        public int RouteId;
        /// <summary>Hurt on a route: no new route until then (the infirmary heals).</summary>
        public long InjuredUntil;

        [Newtonsoft.Json.JsonIgnore]
        public bool IsHero => HeroClass != HeroClass.None;

        public int StatOf(Stat stat) => Stats[(int)stat];
    }
}

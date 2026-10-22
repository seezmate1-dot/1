using System.Collections.Generic;
using SunnyValley.Core.Match3;

namespace SunnyValley.Core.Tests
{
    internal static class Fixtures
    {
        /// <summary>A 9×9 level with every board feature at once: holes, a portal, walls, all obstacles, chicks, specials.</summary>
        public static LevelData Rich(int moves = 40, List<GoalData> goals = null) => new LevelData
        {
            Id = 999,
            Moves = moves,
            Colors = 5,
            Seed = 99,
            Grid = new[]
            {
                "#########",
                "#########",
                "##.###.##",
                "#########",
                "####.####",
                "#########",
                "#########",
                "#########",
                "#########",
            },
            Layers = new Dictionary<string, string[]>
            {
                ["grass"] = new[] { ".........", ".........", ".........", ".........", ".........", ".........", "11.......", "1122..221", "222222222" },
                ["crate"] = new[] { ".........", ".........", ".........", ".........", "2.......1", ".........", ".........", ".........", "........." },
                ["stone"] = new[] { ".........", ".........", ".........", ".........", ".........", ".........", "...1.....", ".........", "........." },
                ["ice"] = new[] { ".........", ".........", ".........", ".........", ".........", ".........", ".........", ".....1...", "........." },
                ["chain"] = new[] { ".........", ".........", ".........", ".........", ".........", ".........", ".........", "......2..", "........." },
                ["honey"] = new[] { ".........", ".........", ".........", ".........", ".........", ".........", ".........", ".........", "..1......" },
                ["weed"] = new[] { ".........", ".........", ".........", ".........", ".........", ".........", ".......1.", ".........", "........." },
                ["bed"] = new[] { ".........", ".........", ".........", ".........", "..2......", ".........", ".........", ".........", "........." },
            },
            Baskets = new List<BasketData> { new BasketData { X = 5, Y = 1, Color = "tomato", Count = 5 } },
            Walls = new List<WallData> { new WallData { X = 1, Y = 6, Side = "down" }, new WallData { X = 7, Y = 1, Side = "right" } },
            Portals = new List<PortalData> { new PortalData { From = new[] { 4, 3 }, To = new[] { 4, 5 } } },
            Chicks = new ChickRules { Spawn = 3, MaxOnBoard = 2, Gap = 1 },
            Specials = new List<SpecialData>
            {
                new SpecialData { X = 3, Y = 3, Kind = "bomb", Color = "corn" },
                new SpecialData { X = 4, Y = 2, Kind = "lineH", Color = "apple" },
                new SpecialData { X = 5, Y = 3, Kind = "bee", Color = "tomato" },
                new SpecialData { X = 0, Y = 0, Kind = "rainbow" },
                new SpecialData { X = 1, Y = 0, Kind = "lineV", Color = "carrot" },
            },
            Goals = goals ?? new List<GoalData> { new GoalData { Type = "collect", Color = "tomato", Count = 999 } },
        };
    }
}

using System.Collections.Generic;
using Newtonsoft.Json;

namespace SunnyValley.Core.Match3
{
    /// <summary>
    /// A level as stored in Resources/Levels/level_NNN.json. Grids are arrays of strings, one string per row,
    /// top row first, one character per column. See farm/docs/LEVELS.md for the full format.
    /// </summary>
    public sealed class LevelData
    {
        [JsonProperty("id")] public int Id;
        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)] public string Name;
        /// <summary>Mechanic this level introduces (shown as a hint card): "crate", "ice", "bee"…</summary>
        [JsonProperty("intro", NullValueHandling = NullValueHandling.Ignore)] public string Intro;
        [JsonProperty("moves")] public int Moves;
        /// <summary>How many crops are in play: the first N of tomato, corn, apple, carrot, eggplant, blueberry.</summary>
        [JsonProperty("colors")] public int Colors;
        /// <summary>Seed of the board and of everything random in it (spawns, bees, reshuffles).</summary>
        [JsonProperty("seed")] public long Seed;

        /// <summary>Cell mask: '#' playable cell, '.' hole.</summary>
        [JsonProperty("grid")] public string[] Grid;

        /// <summary>
        /// Obstacle grids by name: grass (1–2), crate (1–3), ice (1–2), chain (1–2), honey (1), stone (1–2),
        /// weed (1), bed (1–3 = hits per crop). Digit = layers, '.' or '0' = none.
        /// </summary>
        [JsonProperty("layers", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, string[]> Layers;

        /// <summary>
        /// Optional preset pieces: '.' random, r y g o p b a crop, C chick, H/V pitchfork, E bee, X bomb, W rainbow
        /// (specials get a random colour). Cells under crates/stones/weed/beds/baskets must be '.'.
        /// </summary>
        [JsonProperty("pieces", NullValueHandling = NullValueHandling.Ignore)] public string[] Pieces;

        [JsonProperty("baskets", NullValueHandling = NullValueHandling.Ignore)] public List<BasketData> Baskets;
        [JsonProperty("walls", NullValueHandling = NullValueHandling.Ignore)] public List<WallData> Walls;
        /// <summary>Cells that create new pieces. Default: the top cell of every column.</summary>
        [JsonProperty("spawners", NullValueHandling = NullValueHandling.Ignore)] public List<int[]> Spawners;
        [JsonProperty("portals", NullValueHandling = NullValueHandling.Ignore)] public List<PortalData> Portals;
        /// <summary>Cells where chicks are rescued. Default: the bottom cell of every column.</summary>
        [JsonProperty("chickExits", NullValueHandling = NullValueHandling.Ignore)] public List<int[]> ChickExits;
        [JsonProperty("chicks", NullValueHandling = NullValueHandling.Ignore)] public ChickRules Chicks;

        [JsonProperty("goals")] public List<GoalData> Goals;
        /// <summary>Moves left needed for 2 and 3 stars. Default: 10% and 25% of the moves.</summary>
        [JsonProperty("stars", NullValueHandling = NullValueHandling.Ignore)] public int[] Stars;

        [JsonIgnore] public int Width => Grid == null || Grid.Length == 0 ? 0 : Grid[0].Length;
        [JsonIgnore] public int Height => Grid == null ? 0 : Grid.Length;
    }

    public sealed class BasketData
    {
        [JsonProperty("x")] public int X;
        [JsonProperty("y")] public int Y;
        [JsonProperty("color")] public string Color;
        [JsonProperty("count")] public int Count;
    }

    public sealed class WallData
    {
        [JsonProperty("x")] public int X;
        [JsonProperty("y")] public int Y;
        /// <summary>"down" (between (x,y) and (x,y+1)) or "right" (between (x,y) and (x+1,y)).</summary>
        [JsonProperty("side")] public string Side;
    }

    public sealed class PortalData
    {
        /// <summary>Entrance: a cell with nothing below it; pieces leave the board here...</summary>
        [JsonProperty("from")] public int[] From;
        /// <summary>...and come in here, falling on from this cell.</summary>
        [JsonProperty("to")] public int[] To;
    }

    public sealed class ChickRules
    {
        /// <summary>Chicks that will come out of the spawners in total (on top of the ones placed in "pieces").</summary>
        [JsonProperty("spawn")] public int Spawn;
        /// <summary>Never more than this many chicks on the board at once.</summary>
        [JsonProperty("maxOnBoard")] public int MaxOnBoard = 2;
        /// <summary>At least this many moves between two spawned chicks.</summary>
        [JsonProperty("gap")] public int Gap = 2;
    }

    public sealed class GoalData
    {
        /// <summary>collect, grass, crate, ice, stone, honey, weed, chain, chick, crop, basket.</summary>
        [JsonProperty("type")] public string Type;
        [JsonProperty("color", NullValueHandling = NullValueHandling.Ignore)] public string Color;
        /// <summary>Target. 0 or absent for obstacle goals = all of them on the starting board.</summary>
        [JsonProperty("count")] public int Count;
    }
}

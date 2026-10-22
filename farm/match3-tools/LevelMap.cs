using System;
using System.Collections.Generic;
using System.Linq;
using SunnyValley.Core.Match3;

namespace SunnyValley.Tools
{
    /// <summary>
    /// One design of a level, written compactly as a single character map; turned into <see cref="LevelData"/>.
    /// Map legend (one char per cell):
    ///   '#' plain cell      ' ' hole
    ///   'g' grass 1         'G' grass 2
    ///   '1' '2' '3' crate with 1/2/3 layers      'q' crate 1 on grass 1   'Q' crate 2 on grass 1
    ///   'i' ice 1   'I' ice 2   'j' ice 1 on grass 1
    ///   'l' chain 1 'L' chain 2 'n' chain 1 on grass 1
    ///   'h' honey   's' stone 1 'S' stone 2   'w' weed
    ///   'b' bed (2 hits per crop)   'B' bed (3 hits per crop)
    ///   'k' chick (on a plain cell)  'u' basket (colours/counts from <see cref="Baskets"/> in reading order)
    /// </summary>
    public sealed class Design
    {
        public int Id;
        public string Name;
        public string Intro;
        public int Colors = 5;
        public string[] Map;
        public List<GoalData> Goals = new List<GoalData>();
        public List<(string color, int count)> Baskets = new List<(string, int)>();
        public List<PortalData> Portals;
        public List<WallData> Walls;
        public List<int[]> Spawners;
        public ChickRules Chicks;
        public List<SpecialData> Specials;
        /// <summary>Target win rate of the average bot.</summary>
        public double Target;
        /// <summary>Hard levels aim at a band (35–50%) instead of "at least".</summary>
        public bool Hard;
        public int MinMoves = 15, MaxMoves = 40;
        public long Seed;

        public Design Collect(string color, int n) { Goals.Add(new GoalData { Type = "collect", Color = color, Count = n }); return this; }
        public Design Goal(string type, int n = 0) { Goals.Add(new GoalData { Type = type, Count = n }); return this; }

        public LevelData ToLevel(int moves)
        {
            int h = Map.Length, w = Map.Max(r => r.Length);
            var rows = Map.Select(r => r.PadRight(w)).ToArray();
            var grid = new string[h];
            var layers = new Dictionary<string, char[][]>();
            char[][] L(string name)
            {
                if (!layers.TryGetValue(name, out var g))
                {
                    g = Enumerable.Range(0, h).Select(_ => Enumerable.Repeat('.', w).ToArray()).ToArray();
                    layers[name] = g;
                }
                return g;
            }
            var pieces = Enumerable.Range(0, h).Select(_ => Enumerable.Repeat('.', w).ToArray()).ToArray();
            bool anyPiece = false;
            var baskets = new List<BasketData>();
            int basketIdx = 0;
            for (int y = 0; y < h; y++)
            {
                var gr = new char[w];
                for (int x = 0; x < w; x++)
                {
                    char c = rows[y][x];
                    gr[x] = c == ' ' ? '.' : '#';
                    switch (c)
                    {
                        case '#': case ' ': break;
                        case 'g': L("grass")[y][x] = '1'; break;
                        case 'G': L("grass")[y][x] = '2'; break;
                        case '1': case '2': case '3': L("crate")[y][x] = c; break;
                        case 'q': L("crate")[y][x] = '1'; L("grass")[y][x] = '1'; break;
                        case 'Q': L("crate")[y][x] = '2'; L("grass")[y][x] = '1'; break;
                        case 'i': L("ice")[y][x] = '1'; break;
                        case 'I': L("ice")[y][x] = '2'; break;
                        case 'j': L("ice")[y][x] = '1'; L("grass")[y][x] = '1'; break;
                        case 'l': L("chain")[y][x] = '1'; break;
                        case 'L': L("chain")[y][x] = '2'; break;
                        case 'n': L("chain")[y][x] = '1'; L("grass")[y][x] = '1'; break;
                        case 'h': L("honey")[y][x] = '1'; break;
                        case 's': L("stone")[y][x] = '1'; break;
                        case 'S': L("stone")[y][x] = '2'; break;
                        case 'w': L("weed")[y][x] = '1'; break;
                        case 'b': L("bed")[y][x] = '2'; break;
                        case 'B': L("bed")[y][x] = '3'; break;
                        case 'k': pieces[y][x] = 'C'; anyPiece = true; break;
                        case 'u':
                        {
                            var (color, count) = Baskets[basketIdx++];
                            baskets.Add(new BasketData { X = x, Y = y, Color = color, Count = count });
                            break;
                        }
                        default: throw new FormatException("level " + Id + ": unknown map char '" + c + "'");
                    }
                }
                grid[y] = new string(gr);
            }
            return new LevelData
            {
                Id = Id,
                Name = Name,
                Intro = Intro,
                Moves = moves,
                Colors = Colors,
                Seed = Seed != 0 ? Seed : 1000 + Id * 37,
                Grid = grid,
                Layers = layers.Count == 0 ? null : layers.ToDictionary(kv => kv.Key, kv => kv.Value.Select(r => new string(r)).ToArray()),
                Pieces = anyPiece ? pieces.Select(r => new string(r)).ToArray() : null,
                Baskets = baskets.Count == 0 ? null : baskets,
                Walls = Walls,
                Portals = Portals,
                Spawners = Spawners,
                Chicks = Chicks,
                Specials = Specials,
                Goals = Goals,
            };
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SunnyValley.Core.Match3;

namespace SunnyValley.Core.Tests
{
    /// <summary>Builds tiny levels from text for the match-3 tests.</summary>
    internal static class M3
    {
        public static Pos P(int x, int y) => new Pos(x, y);

        /// <summary>
        /// A level whose whole board is given by <paramref name="pieces"/> (r y g o p b colours, C chick,
        /// H V E X W specials, '.' random or under a blocker, ' ' hole). Default goal: collect 999 tomatoes.
        /// </summary>
        public static LevelData Level(string[] pieces, int colors = 6, int moves = 20, Dictionary<string, string[]> layers = null,
            List<GoalData> goals = null, long seed = 7)
        {
            var grid = pieces.Select(r => new string(r.Select(c => c == ' ' ? '.' : '#').ToArray())).ToArray();
            var pc = pieces.Select(r => r.Replace(' ', '.')).ToArray();
            return new LevelData
            {
                Id = 1,
                Moves = moves,
                Colors = colors,
                Seed = seed,
                Grid = grid,
                Pieces = pc,
                Layers = layers,
                Goals = goals ?? new List<GoalData> { new GoalData { Type = "collect", Color = "tomato", Count = 999 } },
            };
        }

        /// <summary>
        /// Background of o/p/b in a diagonal pattern (no line, no square anywhere) with some cells overridden:
        /// each override is (x, y, char).
        /// </summary>
        public static string[] Bg(int w, int h, params (int x, int y, char c)[] overrides)
        {
            var rows = new char[h][];
            for (int y = 0; y < h; y++)
            {
                rows[y] = new char[w];
                for (int x = 0; x < w; x++) rows[y][x] = "opb"[(x + y) % 3];
            }
            foreach (var (x, y, c) in overrides) rows[y][x] = c;
            return rows.Select(r => new string(r)).ToArray();
        }

        public static LevelData With(this LevelData level, params (int x, int y, string kind, string color)[] specials)
        {
            level.Specials ??= new List<SpecialData>();
            foreach (var (x, y, kind, color) in specials) level.Specials.Add(new SpecialData { X = x, Y = y, Kind = kind, Color = color });
            return level;
        }

        public static System.Collections.Generic.HashSet<Pos> Rect(int x0, int y0, int x1, int y1)
        {
            var set = new HashSet<Pos>();
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++) set.Add(new Pos(x, y));
            return set;
        }

        public static MatchEvent GroupAt(MoveResult r, int x, int y, int wave = 0) =>
            r.OfType<MatchEvent>().FirstOrDefault(m => m.Wave == wave && m.Cells.Contains(new Pos(x, y)));

        public static Match3Game Game(LevelData level, long seed = 7)
        {
            var g = Match3Game.Create(level, seed);
            return g;
        }

        public static GoalData Goal(string type, int count = 0, string color = null) => new GoalData { Type = type, Count = count, Color = color };

        public static Dictionary<string, string[]> Layers(params (string name, string[] rows)[] layers) =>
            layers.ToDictionary(l => l.name, l => l.rows);

        public static Piece At(Match3Game g, int x, int y) => g.Board.GetPiece(new Pos(x, y));

        public static void AssertAccepted(MoveResult r) => Assert.IsTrue(r.Accepted, "move was rejected: " + r.ToJson());
    }
}

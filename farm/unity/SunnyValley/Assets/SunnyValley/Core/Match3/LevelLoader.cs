using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SunnyValley.Core.Match3
{
    /// <summary>Reads level JSON and turns it into a board (shape + obstacles + preset pieces).</summary>
    public static class LevelLoader
    {
        public static readonly string[] LayerNames = { "grass", "crate", "ice", "chain", "honey", "stone", "weed", "bed" };

        public static LevelData FromJson(string json)
        {
            var level = JsonConvert.DeserializeObject<LevelData>(json);
            if (level == null) throw new FormatException("Empty level JSON");
            return level;
        }

        public static string ToJson(LevelData level) => JsonConvert.SerializeObject(level, Formatting.Indented);

        public static ObstacleKind LayerKind(string name)
        {
            switch (name)
            {
                case "grass": return ObstacleKind.Grass;
                case "crate": return ObstacleKind.Crate;
                case "ice": return ObstacleKind.Ice;
                case "chain": return ObstacleKind.Chain;
                case "honey": return ObstacleKind.Honey;
                case "stone": return ObstacleKind.Stone;
                case "weed": return ObstacleKind.Weed;
                case "bed": return ObstacleKind.Bed;
                default: return ObstacleKind.None;
            }
        }

        /// <summary>Highest layer count each obstacle may start with.</summary>
        public static int MaxLayers(ObstacleKind k)
        {
            switch (k)
            {
                case ObstacleKind.Grass: return 2;
                case ObstacleKind.Crate: return 3;
                case ObstacleKind.Ice: return 2;
                case ObstacleKind.Chain: return 2;
                case ObstacleKind.Honey: return 1;
                case ObstacleKind.Stone: return 2;
                case ObstacleKind.Weed: return 1;
                case ObstacleKind.Bed: return 3;
                default: return 0;
            }
        }

        public static bool IsBlocker(ObstacleKind k) =>
            k == ObstacleKind.Crate || k == ObstacleKind.Stone || k == ObstacleKind.Weed || k == ObstacleKind.Bed || k == ObstacleKind.Basket;

        public static bool IsOverlay(ObstacleKind k) => k == ObstacleKind.Ice || k == ObstacleKind.Chain || k == ObstacleKind.Honey;

        public static int LayerAt(string[] rows, int x, int y)
        {
            if (rows == null || y >= rows.Length || x >= rows[y].Length) return 0;
            char c = rows[y][x];
            return c >= '1' && c <= '9' ? c - '0' : 0;
        }

        public static char PieceAt(LevelData level, int x, int y)
        {
            var rows = level.Pieces;
            if (rows == null || y >= rows.Length || x >= rows[y].Length) return '.';
            return rows[y][x];
        }

        static int Idx(BoardTopology t, int[] xy) => t.Index(xy[0], xy[1]);

        /// <summary>Builds the static shape. Assumes the level passed <see cref="LevelValidator"/>'s structural checks.</summary>
        public static BoardTopology BuildTopology(LevelData level)
        {
            int w = level.Width, h = level.Height;
            var t = new BoardTopology(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    t.Exists[t.Index(x, y)] = level.Grid[y][x] != '.' && level.Grid[y][x] != ' ';
            if (level.Walls != null)
                foreach (var wall in level.Walls)
                {
                    int i = t.Index(wall.X, wall.Y);
                    if (wall.Side == "down") t.WallDown[i] = true; else t.WallRight[i] = true;
                }
            var portals = new List<(int, int)>();
            if (level.Portals != null)
                foreach (var p in level.Portals) portals.Add((Idx(t, p.From), Idx(t, p.To)));
            t.Build(portals);

            if (level.Spawners != null && level.Spawners.Count > 0)
            {
                foreach (var s in level.Spawners) t.Spawner[Idx(t, s)] = true;
            }
            else
            {
                for (int x = 0; x < w; x++)
                    for (int y = 0; y < h; y++)
                    {
                        int i = t.Index(x, y);
                        if (!t.Exists[i]) continue;
                        if (!t.PortalExit[i]) t.Spawner[i] = true;
                        break;
                    }
            }

            if (level.ChickExits != null && level.ChickExits.Count > 0)
            {
                foreach (var s in level.ChickExits) t.ChickExit[Idx(t, s)] = true;
            }
            else
            {
                for (int x = 0; x < w; x++)
                    for (int y = h - 1; y >= 0; y--)
                    {
                        int i = t.Index(x, y);
                        if (!t.Exists[i]) continue;
                        if (!t.PortalEntrance[i]) t.ChickExit[i] = true;
                        break;
                    }
            }
            return t;
        }

        /// <summary>Puts obstacles and baskets on a fresh board. Pieces are placed by the game (it needs the RNG).</summary>
        public static Board BuildBoard(LevelData level, BoardTopology topo)
        {
            var b = new Board(topo);
            if (level.Layers != null)
                foreach (var kv in level.Layers)
                {
                    var kind = LayerKind(kv.Key);
                    for (int y = 0; y < topo.Height; y++)
                        for (int x = 0; x < topo.Width; x++)
                        {
                            int n = LayerAt(kv.Value, x, y);
                            if (n == 0) continue;
                            int i = topo.Index(x, y);
                            if (kind == ObstacleKind.Grass) b.Grass[i] = n;
                            else if (IsOverlay(kind)) { b.Overlay[i] = kind; b.OverlayLayers[i] = n; }
                            else if (kind == ObstacleKind.Bed) { b.Blocker[i] = kind; b.BedNeed[i] = n; b.BlockerLayers[i] = 0; }
                            else { b.Blocker[i] = kind; b.BlockerLayers[i] = n; }
                        }
                }
            if (level.Baskets != null)
                foreach (var bk in level.Baskets)
                {
                    int i = topo.Index(bk.X, bk.Y);
                    b.Blocker[i] = ObstacleKind.Basket;
                    b.BlockerLayers[i] = bk.Count;
                    b.BasketColor[i] = TileColors.Parse(bk.Color);
                }
            return b;
        }

        public static GoalType ParseGoalType(string s)
        {
            switch (s)
            {
                case "collect": return GoalType.Collect;
                case "grass": return GoalType.Grass;
                case "crate": return GoalType.Crate;
                case "ice": return GoalType.Ice;
                case "stone": return GoalType.Stone;
                case "honey": return GoalType.Honey;
                case "weed": return GoalType.Weed;
                case "chain": return GoalType.Chain;
                case "chick": return GoalType.Chick;
                case "crop": return GoalType.Crop;
                case "basket": return GoalType.Basket;
                default: throw new FormatException("Unknown goal type '" + s + "'");
            }
        }

        public static string GoalName(GoalType g) => g.ToString().ToLowerInvariant();

        /// <summary>Obstacle a goal type counts, or None for collect/chick/crop.</summary>
        public static ObstacleKind GoalObstacle(GoalType g)
        {
            switch (g)
            {
                case GoalType.Grass: return ObstacleKind.Grass;
                case GoalType.Crate: return ObstacleKind.Crate;
                case GoalType.Ice: return ObstacleKind.Ice;
                case GoalType.Stone: return ObstacleKind.Stone;
                case GoalType.Honey: return ObstacleKind.Honey;
                case GoalType.Weed: return ObstacleKind.Weed;
                case GoalType.Chain: return ObstacleKind.Chain;
                case GoalType.Basket: return ObstacleKind.Basket;
                default: return ObstacleKind.None;
            }
        }

        /// <summary>How many of an obstacle are on the starting board (cells).</summary>
        public static int CountObstacle(Board b, ObstacleKind k)
        {
            int n = 0;
            for (int i = 0; i < b.Topo.Count; i++)
            {
                if (!b.Topo.Exists[i]) continue;
                if (k == ObstacleKind.Grass) { if (b.Grass[i] > 0) n++; }
                else if (IsOverlay(k)) { if (b.Overlay[i] == k) n++; }
                else if (b.Blocker[i] == k) n++;
            }
            return n;
        }

        /// <summary>Chicks placed in the layout plus chicks that will spawn.</summary>
        public static int TotalChicks(LevelData level)
        {
            int n = level.Chicks?.Spawn ?? 0;
            if (level.Pieces != null)
                foreach (var row in level.Pieces)
                    foreach (var c in row) if (c == 'C') n++;
            return n;
        }
    }
}

using System;
using System.Collections.Generic;

namespace SunnyValley.Core.Match3
{
    /// <summary>Checks a level before it ships. Empty list = valid.</summary>
    public static class LevelValidator
    {
        public const int MaxSize = 9;

        /// <summary>Full check: structure, refill reachability, goals achievable, and a playable start (≥1 move, no match).</summary>
        public static List<string> Validate(LevelData level)
        {
            var errors = ValidateStructure(level);
            if (errors.Count > 0) return errors;
            var topo = LevelLoader.BuildTopology(level);
            if (topo.HasFallCycle()) { errors.Add("portals form a loop"); return errors; }
            CheckRefill(topo, errors);
            CheckGoals(level, topo, errors);
            if (errors.Count > 0) return errors;
            try
            {
                var g = Match3Game.Create(level);
                if (g.FindMatches().Count > 0) errors.Add("starting board has a match (check preset pieces)");
                if (!g.HasValidMove()) errors.Add("starting board has no move");
            }
            catch (Exception e)
            {
                errors.Add("cannot start: " + e.Message);
            }
            return errors;
        }

        /// <summary>Everything that can be checked from the JSON alone.</summary>
        public static List<string> ValidateStructure(LevelData level)
        {
            var errors = new List<string>();
            if (level == null) { errors.Add("level is null"); return errors; }
            if (level.Grid == null || level.Grid.Length == 0) { errors.Add("grid is empty"); return errors; }
            int h = level.Grid.Length, w = level.Grid[0]?.Length ?? 0;
            if (w < 3 || w > MaxSize || h < 3 || h > MaxSize) errors.Add("grid must be 3..9 × 3..9, is " + w + "×" + h);
            int cells = 0;
            for (int y = 0; y < h; y++)
            {
                var row = level.Grid[y];
                if (row == null || row.Length != w) { errors.Add("grid row " + y + " has a different length"); return errors; }
                foreach (var c in row)
                {
                    if (c == '#') cells++;
                    else if (c != '.' && c != ' ') errors.Add("grid row " + y + ": unknown char '" + c + "'");
                }
            }
            if (cells < 9) errors.Add("fewer than 9 playable cells");
            if (level.Moves < 1 || level.Moves > 99) errors.Add("moves must be 1..99");
            if (level.Colors < 3 || level.Colors > TileColors.Max) errors.Add("colors must be 3..6");
            if (errors.Count > 0) return errors;

            bool Cell(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && level.Grid[y][x] == '#';
            bool CellXY(int[] xy) => xy != null && xy.Length == 2 && Cell(xy[0], xy[1]);

            var overlay = new ObstacleKind[w * h];
            var blocker = new ObstacleKind[w * h];
            var grass = new bool[w * h];
            if (level.Layers != null)
                foreach (var kv in level.Layers)
                {
                    var kind = LevelLoader.LayerKind(kv.Key);
                    if (kind == ObstacleKind.None) { errors.Add("unknown layer '" + kv.Key + "'"); continue; }
                    var rows = kv.Value;
                    if (rows == null || rows.Length != h) { errors.Add("layer " + kv.Key + " must have " + h + " rows"); continue; }
                    for (int y = 0; y < h; y++)
                    {
                        if (rows[y] == null || rows[y].Length != w) { errors.Add("layer " + kv.Key + " row " + y + " must have " + w + " chars"); continue; }
                        for (int x = 0; x < w; x++)
                        {
                            char c = rows[y][x];
                            if (c == '.' || c == '0' || c == ' ') continue;
                            if (c < '1' || c > '9') { errors.Add("layer " + kv.Key + " (" + x + "," + y + "): bad char '" + c + "'"); continue; }
                            int n = c - '0';
                            if (n > LevelLoader.MaxLayers(kind)) errors.Add("layer " + kv.Key + " (" + x + "," + y + "): at most " + LevelLoader.MaxLayers(kind) + " layers");
                            if (!Cell(x, y)) { errors.Add("layer " + kv.Key + " (" + x + "," + y + ") is on a hole"); continue; }
                            int i = y * w + x;
                            if (kind == ObstacleKind.Grass) grass[i] = true;
                            else if (LevelLoader.IsOverlay(kind))
                            {
                                if (overlay[i] != ObstacleKind.None) errors.Add("(" + x + "," + y + "): two overlays");
                                overlay[i] = kind;
                            }
                            else
                            {
                                if (blocker[i] != ObstacleKind.None) errors.Add("(" + x + "," + y + "): two blockers");
                                blocker[i] = kind;
                            }
                        }
                    }
                }
            if (level.Baskets != null)
                foreach (var b in level.Baskets)
                {
                    if (!Cell(b.X, b.Y)) { errors.Add("basket (" + b.X + "," + b.Y + ") is not on a cell"); continue; }
                    int i = b.Y * w + b.X;
                    if (blocker[i] != ObstacleKind.None) errors.Add("basket (" + b.X + "," + b.Y + ") shares its cell");
                    blocker[i] = ObstacleKind.Basket;
                    if (b.Count < 1) errors.Add("basket (" + b.X + "," + b.Y + ") count must be ≥ 1");
                    try
                    {
                        var col = TileColors.Parse(b.Color);
                        if (col == TileColor.None || (int)col >= level.Colors) errors.Add("basket (" + b.X + "," + b.Y + ") colour not in play");
                    }
                    catch (FormatException e) { errors.Add(e.Message); }
                }
            for (int i = 0; i < w * h; i++)
            {
                if (blocker[i] != ObstacleKind.None && overlay[i] != ObstacleKind.None)
                    errors.Add("(" + i % w + "," + i / w + "): a blocker cannot have an overlay");
                if (grass[i] && (blocker[i] == ObstacleKind.Bed || blocker[i] == ObstacleKind.Basket))
                    errors.Add("(" + i % w + "," + i / w + "): no grass under beds or baskets");
            }
            if (level.Pieces != null)
            {
                if (level.Pieces.Length != h) errors.Add("pieces must have " + h + " rows");
                else
                    for (int y = 0; y < h; y++)
                    {
                        if (level.Pieces[y] == null || level.Pieces[y].Length != w) { errors.Add("pieces row " + y + " must have " + w + " chars"); continue; }
                        for (int x = 0; x < w; x++)
                        {
                            char c = level.Pieces[y][x];
                            if (c == '.' || c == ' ') continue;
                            int li = TileColors.Letters.IndexOf(c);
                            if (li < 0 && "CHVEXW".IndexOf(c) < 0) { errors.Add("pieces (" + x + "," + y + "): unknown char '" + c + "'"); continue; }
                            if (li >= level.Colors) errors.Add("pieces (" + x + "," + y + "): colour not in play");
                            if (!Cell(x, y)) { errors.Add("pieces (" + x + "," + y + ") is on a hole"); continue; }
                            int i = y * w + x;
                            if (blocker[i] != ObstacleKind.None) errors.Add("pieces (" + x + "," + y + ") is under a blocker");
                            if (c == 'C' && overlay[i] != ObstacleKind.None) errors.Add("pieces (" + x + "," + y + "): a chick cannot start under an overlay");
                        }
                    }
            }
            if (level.Specials != null)
                foreach (var sd in level.Specials)
                {
                    if (!Cell(sd.X, sd.Y)) { errors.Add("special (" + sd.X + "," + sd.Y + ") is not on a cell"); continue; }
                    if (blocker[sd.Y * w + sd.X] != ObstacleKind.None) errors.Add("special (" + sd.X + "," + sd.Y + ") is under a blocker");
                    try
                    {
                        var k = SpecialData.ParseKind(sd.Kind);
                        if (k != SpecialKind.Rainbow && sd.Color != null)
                        {
                            var c = TileColors.Parse(sd.Color);
                            if (c == TileColor.None || (int)c >= level.Colors) errors.Add("special (" + sd.X + "," + sd.Y + ") colour not in play");
                        }
                    }
                    catch (FormatException e) { errors.Add(e.Message); }
                }
            if (level.Walls != null)
                foreach (var wl in level.Walls)
                {
                    bool ok = wl.Side == "down" ? Cell(wl.X, wl.Y) && wl.Y + 1 < h : wl.Side == "right" ? Cell(wl.X, wl.Y) && wl.X + 1 < w : false;
                    if (!ok) errors.Add("wall (" + wl.X + "," + wl.Y + "," + wl.Side + ") is invalid");
                }
            if (level.Spawners != null)
                foreach (var s in level.Spawners) if (!CellXY(s)) errors.Add("spawner is not on a cell");
            if (level.ChickExits != null)
                foreach (var s in level.ChickExits) if (!CellXY(s)) errors.Add("chick exit is not on a cell");
            if (level.Portals != null)
            {
                var froms = new HashSet<int>();
                var tos = new HashSet<int>();
                foreach (var p in level.Portals)
                {
                    if (!CellXY(p.From) || !CellXY(p.To)) { errors.Add("portal ends must be cells"); continue; }
                    int fx = p.From[0], fy = p.From[1], tx = p.To[0], ty = p.To[1];
                    bool wallBelow = level.Walls != null && level.Walls.Exists(x => x.X == fx && x.Y == fy && x.Side == "down");
                    if (Cell(fx, fy + 1) && !wallBelow) errors.Add("portal entrance (" + fx + "," + fy + ") must have no cell below it");
                    bool wallAbove = level.Walls != null && level.Walls.Exists(x => x.X == tx && x.Y == ty - 1 && x.Side == "down");
                    if (Cell(tx, ty - 1) && !wallAbove) errors.Add("portal exit (" + tx + "," + ty + ") must have no cell above it");
                    if (!froms.Add(fy * w + fx)) errors.Add("two portals share an entrance");
                    if (!tos.Add(ty * w + tx)) errors.Add("two portals share an exit");
                }
            }
            if (level.Chicks != null && (level.Chicks.Spawn < 0 || level.Chicks.MaxOnBoard < 1 || level.Chicks.Gap < 0))
                errors.Add("chicks: spawn ≥ 0, maxOnBoard ≥ 1, gap ≥ 0");
            if (level.Goals == null || level.Goals.Count == 0 || level.Goals.Count > 4) errors.Add("1..4 goals required");
            else
                foreach (var gd in level.Goals)
                {
                    try
                    {
                        var t = LevelLoader.ParseGoalType(gd.Type);
                        if (t == GoalType.Collect)
                        {
                            var c = TileColors.Parse(gd.Color);
                            if (c == TileColor.None || (int)c >= level.Colors) errors.Add("collect goal colour not in play");
                            if (gd.Count < 1) errors.Add("collect goal needs a count");
                        }
                        if ((t == GoalType.Crop) && gd.Count < 1) errors.Add("crop goal needs a count");
                        if (gd.Count < 0) errors.Add("goal count must be ≥ 0");
                    }
                    catch (FormatException e) { errors.Add(e.Message); }
                }
            if (level.Stars != null && (level.Stars.Length != 2 || level.Stars[0] < 1 || level.Stars[1] <= level.Stars[0] || level.Stars[1] >= level.Moves))
                errors.Add("stars must be [movesLeftFor2, movesLeftFor3] with 1 ≤ a < b < moves");
            return errors;
        }

        /// <summary>Every cell must be refillable once emptied: fed from a spawner straight or by a diagonal slide.</summary>
        static void CheckRefill(BoardTopology t, List<string> errors)
        {
            int n = t.Count, w = t.Width;
            var fed = new bool[n];
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (int i in t.BottomUpOrder)
                {
                    if (!t.Exists[i] || fed[i]) continue;
                    bool f = t.Spawner[i] || (t.Above[i] >= 0 && fed[t.Above[i]]);
                    int x = i % w, y = i / w;
                    if (!f && y > 0)
                        for (int dx = -1; dx <= 1; dx += 2)
                            if (t.IsCell(x + dx, y - 1) && fed[(y - 1) * w + x + dx]) f = true;
                    if (f) { fed[i] = true; changed = true; }
                }
            }
            for (int i = 0; i < n; i++)
                if (t.Exists[i] && !fed[i]) { errors.Add("cell (" + i % w + "," + i / w + ") can never be refilled"); return; }
        }

        static void CheckGoals(LevelData level, BoardTopology topo, List<string> errors)
        {
            var b = LevelLoader.BuildBoard(level, topo);
            foreach (var gd in level.Goals)
            {
                var t = LevelLoader.ParseGoalType(gd.Type);
                var ob = LevelLoader.GoalObstacle(t);
                if (ob != ObstacleKind.None)
                {
                    int have = LevelLoader.CountObstacle(b, ob);
                    bool grows = ob == ObstacleKind.Honey || ob == ObstacleKind.Weed;
                    if (have == 0) errors.Add(gd.Type + " goal but no " + gd.Type + " on the board");
                    else if (!grows && gd.Count > have) errors.Add(gd.Type + " goal wants " + gd.Count + " but there are " + have);
                }
                if (t == GoalType.Chick)
                {
                    int total = LevelLoader.TotalChicks(level);
                    if (total == 0) errors.Add("chick goal but no chicks");
                    else if (gd.Count > total) errors.Add("chick goal wants " + gd.Count + " but only " + total + " chicks");
                    bool exit = false;
                    for (int i = 0; i < topo.Count; i++) if (topo.ChickExit[i]) exit = true;
                    if (!exit) errors.Add("chick goal but no chick exit");
                }
                if (t == GoalType.Crop && LevelLoader.CountObstacle(b, ObstacleKind.Bed) == 0) errors.Add("crop goal but no beds");
            }
        }
    }
}

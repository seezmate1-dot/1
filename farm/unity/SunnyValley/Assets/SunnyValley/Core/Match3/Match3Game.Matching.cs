using System;
using System.Collections.Generic;

namespace SunnyValley.Core.Match3
{
    /// <summary>One connected group of same-coloured matched cells, with the special it makes.</summary>
    public sealed class MatchGroup
    {
        public TileColor Color;
        public List<int> Cells = new List<int>();
        public MatchShape Shape;
        public SpecialKind Creates;
        /// <summary>Cell index where the special appears, -1 if none.</summary>
        public int SpecialCell = -1;
        public int LongestH, LongestV;
    }

    public sealed partial class Match3Game
    {
        sealed class Scratch
        {
            public TileColor[] Color;
            public byte[] RunH, RunV;
            public bool[] Square, Marked, Seen;
            public int[] Stack;
            public bool[] Landed;
            public bool[] Immune;
            public int[] Stamp;

            public Scratch(int n)
            {
                Color = new TileColor[n];
                RunH = new byte[n];
                RunV = new byte[n];
                Square = new bool[n];
                Marked = new bool[n];
                Seen = new bool[n];
                Stack = new int[n];
                Landed = new bool[n];
                Immune = new bool[n];
                Stamp = new int[n];
            }
        }

        Scratch _scratch;
        Scratch S => _scratch ??= new Scratch(Topo.Count);

        /// <summary>True if the piece in cell i is part of a line of 3+ or a 2×2 square right now.</summary>
        public bool MatchesAt(int i)
        {
            var c = Board.MatchColor(i);
            if (c == TileColor.None) return false;
            int w = Topo.Width, x = i % w, y = i / w;
            int l = 0, r = 0, u = 0, d = 0;
            while (x - l - 1 >= 0 && Same(i - l - 1, c)) l++;
            while (x + r + 1 < w && Same(i + r + 1, c)) r++;
            if (l + r + 1 >= 3) return true;
            while (y - u - 1 >= 0 && Same(i - (u + 1) * w, c)) u++;
            while (y + d + 1 < Topo.Height && Same(i + (d + 1) * w, c)) d++;
            if (u + d + 1 >= 3) return true;
            // 2×2 squares containing i
            for (int dy = -1; dy <= 0; dy++)
                for (int dx = -1; dx <= 0; dx++)
                {
                    int sx = x + dx, sy = y + dy;
                    if (sx < 0 || sy < 0 || sx + 1 >= w || sy + 1 >= Topo.Height) continue;
                    int a = sy * w + sx;
                    if (Same(a, c) && Same(a + 1, c) && Same(a + w, c) && Same(a + w + 1, c)) return true;
                }
            return false;
        }

        bool Same(int j, TileColor c) => Topo.Exists[j] && Board.MatchColor(j) == c;

        /// <summary>
        /// Finds all matches on the board. Lines of 3+ (horizontal or vertical) and 2×2 squares are found,
        /// then same-coloured matched cells that touch are joined into one group. The group's special:
        /// a line of 5+ → rainbow; else a horizontal and a vertical line crossing (L/T) → bomb; else a line of 4
        /// → pitchfork along that line; else a 2×2 square → bee; else none.
        /// </summary>
        public List<MatchGroup> FindMatches(int swapA = -1, int swapB = -1)
        {
            var s = S;
            int n = Topo.Count, w = Topo.Width, h = Topo.Height;
            for (int i = 0; i < n; i++)
            {
                s.Color[i] = Topo.Exists[i] ? Board.MatchColor(i) : TileColor.None;
                s.RunH[i] = 0; s.RunV[i] = 0; s.Square[i] = false; s.Marked[i] = false; s.Seen[i] = false;
            }
            bool any = false;
            for (int y = 0; y < h; y++)
            {
                int x = 0;
                while (x < w)
                {
                    int i = y * w + x;
                    var c = s.Color[i];
                    int len = 1;
                    if (c != TileColor.None)
                        while (x + len < w && s.Color[i + len] == c) len++;
                    if (c != TileColor.None && len >= 3)
                    {
                        any = true;
                        for (int k = 0; k < len; k++) { s.RunH[i + k] = (byte)len; s.Marked[i + k] = true; }
                    }
                    x += len;
                }
            }
            for (int x = 0; x < w; x++)
            {
                int y = 0;
                while (y < h)
                {
                    int i = y * w + x;
                    var c = s.Color[i];
                    int len = 1;
                    if (c != TileColor.None)
                        while (y + len < h && s.Color[i + len * w] == c) len++;
                    if (c != TileColor.None && len >= 3)
                    {
                        any = true;
                        for (int k = 0; k < len; k++) { s.RunV[i + k * w] = (byte)len; s.Marked[i + k * w] = true; }
                    }
                    y += len;
                }
            }
            for (int y = 0; y + 1 < h; y++)
                for (int x = 0; x + 1 < w; x++)
                {
                    int a = y * w + x;
                    var c = s.Color[a];
                    if (c == TileColor.None) continue;
                    if (s.Color[a + 1] == c && s.Color[a + w] == c && s.Color[a + w + 1] == c)
                    {
                        any = true;
                        s.Square[a] = s.Square[a + 1] = s.Square[a + w] = s.Square[a + w + 1] = true;
                        s.Marked[a] = s.Marked[a + 1] = s.Marked[a + w] = s.Marked[a + w + 1] = true;
                    }
                }
            var groups = new List<MatchGroup>();
            if (!any) return groups;

            for (int start = 0; start < n; start++)
            {
                if (!s.Marked[start] || s.Seen[start]) continue;
                var g = new MatchGroup { Color = s.Color[start] };
                int sp = 0;
                s.Stack[sp++] = start;
                s.Seen[start] = true;
                while (sp > 0)
                {
                    int i = s.Stack[--sp];
                    g.Cells.Add(i);
                    int x = i % w, y = i / w;
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = x + Dx[d], ny = y + Dy[d];
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int j = ny * w + nx;
                        if (s.Marked[j] && !s.Seen[j] && s.Color[j] == g.Color)
                        {
                            s.Seen[j] = true;
                            s.Stack[sp++] = j;
                        }
                    }
                }
                g.Cells.Sort();
                Classify(g, swapA, swapB);
                groups.Add(g);
            }
            return groups;
        }

        void Classify(MatchGroup g, int swapA, int swapB)
        {
            var s = S;
            bool cross = false, square = false;
            foreach (var i in g.Cells)
            {
                g.LongestH = Math.Max(g.LongestH, s.RunH[i]);
                g.LongestV = Math.Max(g.LongestV, s.RunV[i]);
                if (s.RunH[i] > 0 && s.RunV[i] > 0) cross = true;
                if (s.Square[i]) square = true;
            }
            int longest = Math.Max(g.LongestH, g.LongestV);
            if (longest >= 5) { g.Shape = MatchShape.Five; g.Creates = SpecialKind.Rainbow; }
            else if (cross) { g.Shape = MatchShape.LT; g.Creates = SpecialKind.Bomb; }
            else if (longest == 4) { g.Shape = MatchShape.Four; g.Creates = g.LongestH == 4 ? SpecialKind.LineH : SpecialKind.LineV; }
            else if (square) { g.Shape = MatchShape.Square; g.Creates = SpecialKind.Bee; }
            else { g.Shape = MatchShape.Three; g.Creates = SpecialKind.None; }
            if (g.Creates != SpecialKind.None) g.SpecialCell = PickSpecialCell(g, swapA, swapB, cross);
            if (g.SpecialCell < 0) g.Creates = SpecialKind.None;
        }

        /// <summary>
        /// Where the new special appears: the cell the player moved a piece into (B, then A) if it is in the
        /// group; otherwise for a bomb the crossing cell, for lines/rainbow a cell of the long line, for a bee a
        /// square cell — preferring a cell whose piece just fell in, then the one nearest the middle.
        /// A chained cell keeps its piece, so it can never host the special.
        /// </summary>
        int PickSpecialCell(MatchGroup g, int swapA, int swapB, bool cross)
        {
            var s = S;
            bool Free(int i) => Board.Overlay[i] == ObstacleKind.None;
            if (swapB >= 0 && g.Cells.Contains(swapB) && Free(swapB)) return swapB;
            if (swapA >= 0 && g.Cells.Contains(swapA) && Free(swapA)) return swapA;
            var subset = new List<int>();
            foreach (var i in g.Cells)
            {
                bool inSubset;
                switch (g.Creates)
                {
                    case SpecialKind.Bomb: inSubset = s.RunH[i] > 0 && s.RunV[i] > 0; break;
                    case SpecialKind.Rainbow: inSubset = s.RunH[i] >= 5 || s.RunV[i] >= 5; break;
                    case SpecialKind.LineH: inSubset = s.RunH[i] == 4; break;
                    case SpecialKind.LineV: inSubset = s.RunV[i] == 4; break;
                    case SpecialKind.Bee: inSubset = s.Square[i]; break;
                    default: inSubset = false; break;
                }
                if (inSubset && Free(i)) subset.Add(i);
            }
            if (subset.Count == 0)
                foreach (var i in g.Cells) if (Free(i)) subset.Add(i);
            if (subset.Count == 0) return -1;
            int w = Topo.Width;
            double cx = 0, cy = 0;
            foreach (var i in subset) { cx += i % w; cy += i / w; }
            cx /= subset.Count; cy /= subset.Count;
            int best = -1;
            double bestScore = double.MaxValue;
            foreach (var i in subset)
            {
                double d = Math.Abs(i % w - cx) + Math.Abs(i / w - cy);
                double score = (s.Landed[i] ? 0 : 1000) + d * 10 + (i / w == (int)Math.Round(cy) ? 0 : 0.5);
                // Ties: lower row (bigger y) first, then left.
                score -= (i / w) * 0.01;
                score += (i % w) * 0.0001;
                if (score < bestScore) { bestScore = score; best = i; }
            }
            return best;
        }

        /// <summary>
        /// Clears matched cells (Match hits), damages neighbours (Adjacent hits, once per group), then puts the
        /// new specials down (immune for the rest of this wave). Specials that were matched are queued to fire.
        /// </summary>
        void ApplyMatches(List<MatchGroup> groups, int swapA = -1, int swapB = -1)
        {
            if (groups.Count == 0) return;
            var ids = new int[groups.Count];
            for (int k = 0; k < groups.Count; k++)
            {
                var g = groups[k];
                ids[k] = NextId();
                if (_log != null)
                {
                    var me = new MatchEvent { GroupId = ids[k], Color = g.Color, Shape = g.Shape, Creates = g.Creates };
                    foreach (var i in g.Cells) me.Cells.Add(Topo.PosOf(i));
                    if (g.SpecialCell >= 0) me.SpecialCell = Topo.PosOf(g.SpecialCell);
                    AddEvent(me);
                }
            }
            var s = S;
            var adj = new HashSet<int>();
            for (int k = 0; k < groups.Count; k++)
            {
                var g = groups[k];
                foreach (var i in g.Cells) Hit(i, HitSource.Match, ids[k], 0);
                adj.Clear();
                int w = Topo.Width;
                foreach (var i in g.Cells)
                {
                    int x = i % w, y = i / w;
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = x + Dx[d], ny = y + Dy[d];
                        if (!Topo.IsCell(nx, ny)) continue;
                        int j = ny * w + nx;
                        if (!g.Cells.Contains(j)) adj.Add(j);
                    }
                }
                var sorted = new List<int>(adj);
                sorted.Sort();
                foreach (var j in sorted) Hit(j, HitSource.Adjacent, ids[k], 0);
            }
            for (int k = 0; k < groups.Count; k++)
            {
                var g = groups[k];
                if (g.Creates == SpecialKind.None) continue;
                int c = g.SpecialCell;
                if (!Board.Pieces[c].IsEmpty || Board.Blocker[c] != ObstacleKind.None || Board.Overlay[c] != ObstacleKind.None) continue;
                var p = new Piece
                {
                    Id = _nextPieceId++,
                    Kind = PieceKind.Tile,
                    Color = g.Creates == SpecialKind.Rainbow ? TileColor.None : g.Color,
                    Special = g.Creates,
                };
                Board.Pieces[c] = p;
                s.Immune[c] = true;
                if (_log != null)
                {
                    var se = new SpecialCreatedEvent { PieceId = p.Id, Cell = Topo.PosOf(c), Special = p.Special, Color = g.Color, CauseId = ids[k] };
                    foreach (var i in g.Cells) se.From.Add(Topo.PosOf(i));
                    AddEvent(se);
                }
            }
        }
    }
}

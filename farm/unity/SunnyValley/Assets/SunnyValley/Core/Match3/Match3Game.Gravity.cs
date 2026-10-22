using System;
using System.Collections.Generic;

namespace SunnyValley.Core.Match3
{
    public sealed partial class Match3Game
    {
        Dictionary<int, FallEvent> _falls;
        int _tick;

        /// <summary>
        /// Refill in ticks; in one tick every piece moves at most one cell:
        ///  1. straight falls (down, or through a portal) — whole columns move together;
        ///  2. every empty spawner gets a new piece;
        ///  3. only if nothing moved straight: diagonal slides into empty cells that nothing can reach from
        ///     above (under a crate, a hole, a stuck piece), taking a piece from the upper-left, else upper-right.
        /// When nothing moves, chicks standing on exit cells are rescued and refilling continues.
        /// </summary>
        void Gravity()
        {
            var s = S;
            Array.Clear(s.Landed, 0, s.Landed.Length);
            for (int i = 0; i < s.Stamp.Length; i++) s.Stamp[i] = -1;
            _falls = _log != null ? new Dictionary<int, FallEvent>() : null;
            var order = new List<FallEvent>();
            _tick = 0;
            int n = Topo.Count, w = Topo.Width;
            for (int guard = 0; guard < 10000; guard++)
            {
                bool moved = false;
                // 1. straight falls
                foreach (int i in Topo.BottomUpOrder)
                {
                    if (!Topo.Exists[i] || s.Stamp[i] == _tick || !Board.IsMovable(i)) continue;
                    int j = Topo.Below[i];
                    if (j < 0 || !Board.IsOpen(j) || !Board.Pieces[j].IsEmpty) continue;
                    MovePiece(i, j, Topo.PortalEntrance[i] ? FallKind.Portal : FallKind.Down, order);
                    moved = true;
                }
                // 2. spawns
                for (int i = 0; i < n; i++)
                {
                    if (!Topo.Spawner[i] || !Board.IsOpen(i) || !Board.Pieces[i].IsEmpty || s.Stamp[i] == _tick) continue;
                    var p = NewSpawnPiece();
                    Board.Pieces[i] = p;
                    s.Stamp[i] = _tick;
                    s.Landed[i] = true;
                    if (_log != null) AddEvent(new SpawnEvent { PieceId = p.Id, Cell = Topo.PosOf(i), Tick = _tick, Kind = p.Kind, Color = p.Color });
                    moved = true;
                }
                // 3. diagonals
                if (!moved)
                {
                    foreach (int e in Topo.BottomUpOrder)
                    {
                        if (!Board.IsOpen(e) || !Board.Pieces[e].IsEmpty || s.Stamp[e] == _tick) continue;
                        if (CanFillFromAbove(e)) continue;
                        int ex = e % w, ey = e / w;
                        if (ey == 0) continue;
                        for (int side = 0; side < 2; side++)
                        {
                            int sx = side == 0 ? ex - 1 : ex + 1, sy = ey - 1;
                            if (!Topo.IsCell(sx, sy)) continue;
                            int src = sy * w + sx;
                            if (s.Stamp[src] == _tick || !Board.IsMovable(src)) continue;
                            // Would it rather fall straight? Then it is not ours to take.
                            int below = Topo.Below[src];
                            if (below >= 0 && Board.IsOpen(below) && Board.Pieces[below].IsEmpty) continue;
                            // A slide goes round the corner either over the cell above the target or under the
                            // source; walls on both routes block it.
                            int up = sy * w + ex;
                            int minX = Math.Min(sx, ex);
                            bool overBlocked = Topo.WallRight[sy * w + minX] || Topo.WallDown[up];
                            bool underBlocked = Topo.WallDown[src] || Topo.WallRight[ey * w + minX];
                            if (overBlocked && underBlocked) continue;
                            MovePiece(src, e, FallKind.Diagonal, order);
                            moved = true;
                            break;
                        }
                    }
                }
                if (!moved)
                {
                    bool rescued = false;
                    for (int i = 0; i < n; i++)
                    {
                        if (!Topo.ChickExit[i] || !Board.Pieces[i].IsChick || Board.Overlay[i] != ObstacleKind.None) continue;
                        var p = Board.Pieces[i];
                        Board.Pieces[i] = default;
                        if (_log != null) AddEvent(new ChickRescuedEvent { PieceId = p.Id, Cell = Topo.PosOf(i) });
                        Progress(GoalType.Chick, TileColor.None, i);
                        rescued = true;
                    }
                    if (!rescued) break;
                }
                _tick++;
            }
            if (_log != null)
                foreach (var f in order) AddEvent(f);
            _falls = null;
        }

        void MovePiece(int from, int to, FallKind kind, List<FallEvent> order)
        {
            var p = Board.Pieces[from];
            Board.Pieces[to] = p;
            Board.Pieces[from] = default;
            S.Stamp[to] = _tick;
            S.Landed[to] = true;
            S.Landed[from] = false;
            if (_falls == null) return;
            if (!_falls.TryGetValue(p.Id, out var f))
            {
                f = new FallEvent { PieceId = p.Id };
                _falls[p.Id] = f;
                order.Add(f);
            }
            f.Segments.Add(new FallSegment { From = Topo.PosOf(from), To = Topo.PosOf(to), Tick = _tick, Kind = kind });
        }

        /// <summary>True if a straight fall (or a spawn) will eventually fill this empty cell.</summary>
        bool CanFillFromAbove(int e)
        {
            if (Topo.Spawner[e]) return true;
            int c = Topo.Above[e];
            int guard = 0;
            while (c >= 0 && guard++ < Topo.Count)
            {
                if (Board.Blocker[c] != ObstacleKind.None) return false;
                if (!Board.Pieces[c].IsEmpty) return Board.IsMovable(c);
                if (Topo.Spawner[c]) return true;
                c = Topo.Above[c];
            }
            return false;
        }

        Piece NewSpawnPiece()
        {
            if (_chicksToSpawn > 0 && MovesUsed - _lastChickSpawnMove >= _chickGap && MovesUsed > 0 && CountChicks() < _chickMaxOnBoard)
            {
                _chicksToSpawn--;
                _lastChickSpawnMove = MovesUsed;
                return new Piece { Id = _nextPieceId++, Kind = PieceKind.Chick, Color = TileColor.None };
            }
            return new Piece { Id = _nextPieceId++, Kind = PieceKind.Tile, Color = (TileColor)Rng.Next(Colors) };
        }

        public int CountChicks()
        {
            int c = 0;
            for (int i = 0; i < Topo.Count; i++) if (Board.Pieces[i].IsChick) c++;
            return c;
        }

        // ================================================================== initial board

        void FillInitial()
        {
            int n = Topo.Count, w = Topo.Width;
            var preset = new bool[n];
            for (int i = 0; i < n; i++)
            {
                if (!Topo.Exists[i] || !Board.IsOpen(i)) continue;
                char c = LevelLoader.PieceAt(Level, i % w, i / w);
                if (c == '.' || c == ' ') continue;
                preset[i] = true;
                var p = new Piece { Id = _nextPieceId++, Kind = PieceKind.Tile, Color = TileColor.None };
                int li = TileColors.Letters.IndexOf(c);
                if (li >= 0) p.Color = (TileColor)li;
                else if (c == 'C') p.Kind = PieceKind.Chick;
                else
                {
                    p.Special = c switch
                    {
                        'H' => SpecialKind.LineH, 'V' => SpecialKind.LineV, 'E' => SpecialKind.Bee, 'X' => SpecialKind.Bomb, 'W' => SpecialKind.Rainbow,
                        _ => SpecialKind.None,
                    };
                    if (p.Special != SpecialKind.Rainbow) p.Color = (TileColor)Rng.Next(Colors);
                }
                Board.Pieces[i] = p;
            }
            if (Level.Specials != null)
                foreach (var sd in Level.Specials)
                {
                    int i = Topo.Index(sd.X, sd.Y);
                    var kind = SpecialData.ParseKind(sd.Kind);
                    preset[i] = true;
                    Board.Pieces[i] = new Piece
                    {
                        Id = Board.Pieces[i].Id != 0 ? Board.Pieces[i].Id : _nextPieceId++,
                        Kind = PieceKind.Tile,
                        Special = kind,
                        Color = kind == SpecialKind.Rainbow ? TileColor.None : sd.Color != null ? TileColors.Parse(sd.Color) : (TileColor)Rng.Next(Colors),
                    };
                }
            for (int attempt = 0; attempt < 200; attempt++)
            {
                for (int i = 0; i < n; i++)
                {
                    if (!Topo.Exists[i] || !Board.IsOpen(i) || preset[i]) continue;
                    Board.Pieces[i] = new Piece { Id = Board.Pieces[i].Id != 0 ? Board.Pieces[i].Id : _nextPieceId++, Kind = PieceKind.Tile, Color = TileColor.None };
                }
                for (int i = 0; i < n; i++)
                {
                    if (!Topo.Exists[i] || !Board.IsOpen(i) || preset[i]) continue;
                    PickColorWithoutMatch(i);
                }
                if (FindMatches().Count == 0 && HasValidMove()) return;
            }
        }

        /// <summary>Colours cell i with a random colour that makes no line or square (if one exists).</summary>
        void PickColorWithoutMatch(int i)
        {
            int start = Rng.Next(Colors);
            for (int k = 0; k < Colors; k++)
            {
                var c = (TileColor)((start + k) % Colors);
                var p = Board.Pieces[i];
                p.Color = c;
                Board.Pieces[i] = p;
                if (!MatchesAt(i)) return;
            }
        }

        // ================================================================== reshuffle

        /// <summary>
        /// Moves the free plain tiles around (obstacles, specials, chicks and pieces under ice/chain/honey stay)
        /// so that there is no match and at least one move. If permutations do not work, recolours them.
        /// </summary>
        bool Reshuffle(bool byBooster)
        {
            var cells = new List<int>();
            for (int i = 0; i < Topo.Count; i++)
                if (Topo.Exists[i] && Board.IsSwappable(i) && Board.Pieces[i].IsPlainTile) cells.Add(i);
            if (cells.Count < 2) return false;
            var original = new Piece[cells.Count];
            for (int k = 0; k < cells.Count; k++) original[k] = Board.Pieces[cells[k]];
            var perm = new List<int>();
            for (int k = 0; k < cells.Count; k++) perm.Add(k);
            bool ok = false;
            bool recolored = false;
            for (int attempt = 0; attempt < 100 && !ok; attempt++)
            {
                Rng.Shuffle(perm);
                for (int k = 0; k < cells.Count; k++) Board.Pieces[cells[k]] = original[perm[k]];
                ok = FindMatches().Count == 0 && HasValidMove();
            }
            for (int attempt = 0; attempt < 100 && !ok; attempt++)
            {
                recolored = true;
                foreach (var c in cells) PickColorWithoutMatch(c);
                ok = FindMatches().Count == 0 && HasValidMove();
            }
            if (!ok)
            {
                for (int k = 0; k < cells.Count; k++) Board.Pieces[cells[k]] = original[k];
                return false;
            }
            if (!byBooster) Reshuffles++;
            if (_log != null)
            {
                var ev = new ShuffleEvent { ByBooster = byBooster };
                var where = new Dictionary<int, int>();
                for (int k = 0; k < cells.Count; k++) where[original[k].Id] = cells[k];
                foreach (var c in cells)
                {
                    var p = Board.Pieces[c];
                    ev.Moves.Add(new ShuffleMove { PieceId = p.Id, From = Topo.PosOf(where[p.Id]), To = Topo.PosOf(c), Color = p.Color });
                }
                AddEvent(ev);
            }
            _ = recolored;
            return true;
        }

        // ================================================================== pre-level boosters

        void PlacePreBoosters(IList<BoosterKind> boosters)
        {
            var log = new List<StepEvent>();
            foreach (var b in boosters)
            {
                var candidates = new List<int>();
                for (int i = 0; i < Topo.Count; i++)
                    if (Topo.Exists[i] && Board.IsSwappable(i) && Board.Pieces[i].IsPlainTile) candidates.Add(i);
                if (candidates.Count == 0) break;
                int c = candidates[Rng.Next(candidates.Count)];
                var p = Board.Pieces[c];
                switch (b)
                {
                    case BoosterKind.PreLine: p.Special = Rng.Next(2) == 0 ? SpecialKind.LineH : SpecialKind.LineV; break;
                    case BoosterKind.PreBomb: p.Special = SpecialKind.Bomb; break;
                    case BoosterKind.PreRainbow: p.Special = SpecialKind.Rainbow; p.Color = TileColor.None; break;
                    default: continue;
                }
                Board.Pieces[c] = p;
                log.Add(new TransformEvent { PieceId = p.Id, Cell = Topo.PosOf(c), Special = p.Special, Color = p.Color });
            }
            InitialEvents = log;
        }
    }
}

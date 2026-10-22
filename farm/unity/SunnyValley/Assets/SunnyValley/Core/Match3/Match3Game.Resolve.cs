using System;
using System.Collections.Generic;

namespace SunnyValley.Core.Match3
{
    public sealed partial class Match3Game
    {
        /// <summary>A queued special firing.</summary>
        sealed class Activation
        {
            public int Id;
            public int CauseId;
            public int Generation;
            public int PieceId;
            public SpecialKind Kind;
            public TileColor Color;
            public int Cell;
            /// <summary>Blast radius for bombs (1 = 3×3, 2 = 5×5).</summary>
            public int Radius = 1;
        }

        Queue<Activation> _acts;
        readonly HashSet<int> _beeTargets = new HashSet<int>();

        void ClearWaveState()
        {
            var s = S;
            Array.Clear(s.Immune, 0, s.Immune.Length);
            _beeTargets.Clear();
        }

        // ================================================================== hits: what damages what

        /// <summary>
        /// One hit on one cell. The top layer takes it and stops it:
        ///   overlay (ice/honey: Adjacent or any special/booster hit; chain: Match or any special/booster hit — the
        ///   piece under an overlay is never removed by the hit that damages the overlay);
        ///   blocker (crate/weed: Adjacent or special; stone: special only; bed: Adjacent or special grows it;
        ///   basket: ignores hits, it is fed by adjacent cleared tiles of its colour);
        ///   otherwise the piece (Match or special; chicks are immune; specials fire) and the grass under it
        ///   (Match or special hit on the cell, even if it is empty, one layer per hit).
        /// </summary>
        void Hit(int i, HitSource src, int causeId, int generation)
        {
            if (!Topo.Exists[i]) return;
            var ov = Board.Overlay[i];
            if (ov != ObstacleKind.None)
            {
                bool hurts = ov == ObstacleKind.Chain ? src != HitSource.Adjacent : src != HitSource.Match;
                if (hurts) DamageOverlay(i, src, causeId);
                return;
            }
            var bl = Board.Blocker[i];
            if (bl != ObstacleKind.None)
            {
                switch (bl)
                {
                    case ObstacleKind.Crate:
                    case ObstacleKind.Weed:
                        if (src != HitSource.Match) DamageBlocker(i, src, causeId);
                        break;
                    case ObstacleKind.Stone:
                        if (src != HitSource.Match && src != HitSource.Adjacent) DamageBlocker(i, src, causeId);
                        break;
                    case ObstacleKind.Bed:
                        if (src != HitSource.Match) GrowBed(i, src, causeId);
                        break;
                }
                return;
            }
            if (src == HitSource.Adjacent) return;
            var p = Board.Pieces[i];
            if (p.IsTile && !S.Immune[i]) ClearPiece(i, src, causeId, generation);
            if (Board.Grass[i] > 0) DamageGrass(i, src, causeId);
        }

        void ClearPiece(int i, HitSource src, int causeId, int generation)
        {
            var p = Board.Pieces[i];
            Board.Pieces[i] = default;
            int basket = p.Color != TileColor.None ? FeedBasket(i, p.Color) : -1;
            if (_log != null)
                AddEvent(new PieceClearedEvent
                {
                    PieceId = p.Id, Cell = Topo.PosOf(i), Color = p.Color, Special = p.Special, Source = src, CauseId = causeId,
                    IntoBasket = basket >= 0 ? Topo.PosOf(basket) : Pos.None,
                });
            if (p.Color != TileColor.None) Progress(GoalType.Collect, p.Color, i);
            if (basket >= 0) BasketTook(basket, src, causeId);
            if (p.Special != SpecialKind.None)
                Enqueue(new Activation
                {
                    CauseId = causeId, Generation = generation + 1, PieceId = p.Id, Kind = p.Special, Color = p.Color, Cell = i,
                });
        }

        /// <summary>A side-adjacent basket of this colour that still wants tiles (up, down, left, right), or -1.</summary>
        int FeedBasket(int i, TileColor color)
        {
            int w = Topo.Width, x = i % w, y = i / w;
            for (int d = 0; d < 4; d++)
            {
                int nx = x + Dx[d], ny = y + Dy[d];
                if (!Topo.IsCell(nx, ny)) continue;
                int j = ny * w + nx;
                if (Board.Blocker[j] == ObstacleKind.Basket && Board.BasketColor[j] == color && Board.BlockerLayers[j] > 0) return j;
            }
            return -1;
        }

        void BasketTook(int j, HitSource src, int causeId)
        {
            int before = Board.BlockerLayers[j];
            Board.BlockerLayers[j] = before - 1;
            bool removed = before - 1 <= 0;
            if (_log != null)
                AddEvent(new ObstacleHitEvent { Cell = Topo.PosOf(j), Obstacle = ObstacleKind.Basket, LayersBefore = before, LayersAfter = before - 1, Removed = removed, Source = src, CauseId = causeId });
            if (removed)
            {
                Board.Blocker[j] = ObstacleKind.None;
                Board.BasketColor[j] = TileColor.None;
                Board.BlockerLayers[j] = 0;
                Progress(GoalType.Basket, TileColor.None, j);
            }
        }

        void DamageOverlay(int i, HitSource src, int causeId)
        {
            var kind = Board.Overlay[i];
            int before = Board.OverlayLayers[i];
            Board.OverlayLayers[i] = before - 1;
            bool removed = before - 1 <= 0;
            if (removed) { Board.Overlay[i] = ObstacleKind.None; Board.OverlayLayers[i] = 0; }
            if (kind == ObstacleKind.Honey) _honeyHit = true;
            if (_log != null)
                AddEvent(new ObstacleHitEvent { Cell = Topo.PosOf(i), Obstacle = kind, LayersBefore = before, LayersAfter = before - 1, Removed = removed, Source = src, CauseId = causeId });
            if (removed) Progress(GoalFor(kind), TileColor.None, i);
        }

        void DamageBlocker(int i, HitSource src, int causeId)
        {
            var kind = Board.Blocker[i];
            int before = Board.BlockerLayers[i];
            Board.BlockerLayers[i] = before - 1;
            bool removed = before - 1 <= 0;
            if (removed) { Board.Blocker[i] = ObstacleKind.None; Board.BlockerLayers[i] = 0; }
            if (kind == ObstacleKind.Weed) _weedHit = true;
            if (_log != null)
                AddEvent(new ObstacleHitEvent { Cell = Topo.PosOf(i), Obstacle = kind, LayersBefore = before, LayersAfter = before - 1, Removed = removed, Source = src, CauseId = causeId });
            if (removed) Progress(GoalFor(kind), TileColor.None, i);
        }

        void GrowBed(int i, HitSource src, int causeId)
        {
            int before = Board.BlockerLayers[i];
            int after = before + 1;
            bool harvest = after >= Board.BedNeed[i];
            Board.BlockerLayers[i] = harvest ? 0 : after;
            if (_log != null)
                AddEvent(new ObstacleHitEvent { Cell = Topo.PosOf(i), Obstacle = ObstacleKind.Bed, LayersBefore = before, LayersAfter = harvest ? 0 : after, Harvested = harvest, Source = src, CauseId = causeId });
            if (harvest) Progress(GoalType.Crop, TileColor.None, i);
        }

        void DamageGrass(int i, HitSource src, int causeId)
        {
            int before = Board.Grass[i];
            Board.Grass[i] = before - 1;
            bool removed = before - 1 <= 0;
            if (_log != null)
                AddEvent(new ObstacleHitEvent { Cell = Topo.PosOf(i), Obstacle = ObstacleKind.Grass, LayersBefore = before, LayersAfter = before - 1, Removed = removed, Source = src, CauseId = causeId });
            if (removed) Progress(GoalType.Grass, TileColor.None, i);
        }

        static GoalType GoalFor(ObstacleKind k)
        {
            switch (k)
            {
                case ObstacleKind.Ice: return GoalType.Ice;
                case ObstacleKind.Chain: return GoalType.Chain;
                case ObstacleKind.Honey: return GoalType.Honey;
                case ObstacleKind.Crate: return GoalType.Crate;
                case ObstacleKind.Stone: return GoalType.Stone;
                case ObstacleKind.Weed: return GoalType.Weed;
                case ObstacleKind.Grass: return GoalType.Grass;
                case ObstacleKind.Basket: return GoalType.Basket;
                default: return GoalType.Collect;
            }
        }

        /// <summary>Total of all goal counters (bots use it as a progress measure).</summary>
        public int GoalProgressTotal { get; private set; }

        void Progress(GoalType type, TileColor color, int cell)
        {
            for (int k = 0; k < Goals.Count; k++)
            {
                var g = Goals[k];
                if (g.Type != type || g.Done) continue;
                if (type == GoalType.Collect && g.Color != color) continue;
                g.Current++;
                GoalProgressTotal++;
                if (_log != null)
                    AddEvent(new GoalProgressEvent { GoalIndex = k, Goal = type, Delta = 1, Current = g.Current, Target = g.Target, Cell = Topo.PosOf(cell) });
                return;
            }
        }

        // ================================================================== activations

        void Enqueue(Activation a)
        {
            _acts ??= new Queue<Activation>();
            a.Id = NextId();
            _acts.Enqueue(a);
        }

        /// <summary>If the special that was swapped is still where it landed, it fires (swap activation).</summary>
        void TriggerIfStill(int cell, int pieceId)
        {
            var p = Board.Pieces[cell];
            if (p.Id != pieceId || !p.IsSpecial) return;
            S.Immune[cell] = false;
            ClearPiece(cell, HitSource.Match, -1, -1);
        }

        void ProcessActivations()
        {
            if (_acts == null) return;
            int guard = 0;
            while (_acts.Count > 0 && guard++ < 10000)
            {
                var a = _acts.Dequeue();
                Execute(a);
            }
        }

        void Execute(Activation a)
        {
            var cells = new List<int>();
            HitSource src;
            switch (a.Kind)
            {
                case SpecialKind.LineH: src = HitSource.Line; AddRow(cells, a.Cell / Topo.Width, a.Cell % Topo.Width); break;
                case SpecialKind.LineV: src = HitSource.Line; AddColumn(cells, a.Cell % Topo.Width, a.Cell / Topo.Width); break;
                case SpecialKind.Bomb: src = HitSource.Blast; AddSquare(cells, a.Cell, a.Radius); break;
                case SpecialKind.Bee: ExecuteBee(a, 1, SpecialKind.None); return;
                case SpecialKind.Rainbow:
                {
                    src = HitSource.Rainbow;
                    var color = MostCommonColor();
                    var ev = LogActivation(a, cells, color);
                    if (color != TileColor.None) AddColor(cells, color, a.Cell);
                    if (ev != null) foreach (var c in cells) ev.Cells.Add(Topo.PosOf(c));
                    foreach (var c in cells) Hit(c, src, a.Id, a.Generation);
                    return;
                }
                default: return;
            }
            var e = LogActivation(a, cells, TileColor.None);
            if (e != null) foreach (var c in cells) e.Cells.Add(Topo.PosOf(c));
            foreach (var c in cells) Hit(c, src, a.Id, a.Generation);
        }

        ActivationEvent LogActivation(Activation a, List<int> cells, TileColor target, ComboKind combo = ComboKind.None)
        {
            if (_log == null) return null;
            var e = new ActivationEvent
            {
                ActivationId = a.Id, CauseId = a.CauseId, Generation = Math.Max(0, a.Generation), PieceId = a.PieceId, Special = a.Kind,
                Color = a.Color, Cell = Topo.PosOf(a.Cell), TargetColor = target, Combo = combo,
            };
            AddEvent(e);
            return e;
        }

        /// <summary>Bee: hits its own cell and the 4 around it, then <paramref name="bees"/> bees fly to targets.</summary>
        void ExecuteBee(Activation a, int bees, SpecialKind carried, ComboKind combo = ComboKind.None)
        {
            var cross = new List<int>();
            AddCross(cross, a.Cell);
            var ev = LogActivation(a, cross, TileColor.None, combo);
            if (ev != null) foreach (var c in cross) ev.Cells.Add(Topo.PosOf(c));
            foreach (var c in cross) Hit(c, HitSource.Bee, a.Id, a.Generation);
            for (int k = 0; k < bees; k++)
            {
                int t = PickBeeTarget(cross);
                if (t < 0) break;
                _beeTargets.Add(t);
                if (_log != null) AddEvent(new BeeFlightEvent { ActivationId = a.Id, CauseId = a.Id, From = Topo.PosOf(a.Cell), To = Topo.PosOf(t), Carried = carried });
                if (ev != null) ev.Cells.Add(Topo.PosOf(t));
                if (carried == SpecialKind.None)
                {
                    Hit(t, HitSource.Bee, a.Id, a.Generation);
                }
                else
                {
                    // The carried special fires at the target (its blast covers the target cell).
                    Enqueue(new Activation { CauseId = a.Id, Generation = a.Generation + 1, PieceId = -1, Kind = carried, Color = a.Color, Cell = t });
                }
            }
        }

        // ------------------------------------------------------------------ bee targeting

        /// <summary>
        /// The most useful cell for a bee: what the goals need most (last layers first), then any obstacle,
        /// then a plain tile. Ties are broken by the level RNG (deterministic). Never a chick or a cell another
        /// bee of this wave already targets, never the bee's own cross.
        /// </summary>
        int PickBeeTarget(List<int> exclude)
        {
            int best = int.MinValue;
            var bestCells = new List<int>();
            for (int i = 0; i < Topo.Count; i++)
            {
                if (!Topo.Exists[i] || _beeTargets.Contains(i) || exclude.Contains(i)) continue;
                int sc = BeeScore(i);
                if (sc < 0) continue;
                if (sc > best) { best = sc; bestCells.Clear(); bestCells.Add(i); }
                else if (sc == best) bestCells.Add(i);
            }
            if (bestCells.Count == 0) return -1;
            return bestCells[Rng.Next(bestCells.Count)];
        }

        /// <summary>Usefulness of hitting cell i right now (higher is better, negative = never).</summary>
        public int BeeScore(int i)
        {
            var p = Board.Pieces[i];
            var ov = Board.Overlay[i];
            var bl = Board.Blocker[i];
            if (p.IsChick && ov == ObstacleKind.None) return -1;
            int score = 0;
            foreach (var g in Goals)
            {
                if (g.Done) continue;
                switch (g.Type)
                {
                    case GoalType.Collect:
                        if (p.IsTile && p.Color == g.Color && ov == ObstacleKind.None && bl == ObstacleKind.None) score += 40;
                        break;
                    case GoalType.Grass:
                        if (Board.Grass[i] > 0 && ov == ObstacleKind.None && bl == ObstacleKind.None) score += 60 + (Board.Grass[i] == 1 ? 15 : 0);
                        break;
                    case GoalType.Crop:
                        if (bl == ObstacleKind.Bed) score += 50 + 10 * Board.BlockerLayers[i];
                        break;
                    case GoalType.Chick:
                    {
                        // Clearing the cell under a chick lets it fall.
                        int up = Topo.Above[i];
                        if (up >= 0 && Board.Pieces[up].IsChick && (p.IsTile || bl != ObstacleKind.None)) score += 45;
                        break;
                    }
                    default:
                    {
                        var k = LevelLoader.GoalObstacle(g.Type);
                        if (k == ObstacleKind.None || k == ObstacleKind.Basket) break;
                        if (ov == k) score += 60 + (Board.OverlayLayers[i] == 1 ? 20 : 0);
                        if (bl == k) score += 60 + (Board.BlockerLayers[i] == 1 ? 20 : 0);
                        break;
                    }
                }
            }
            if (score > 0) return score;
            if (ov != ObstacleKind.None || (bl != ObstacleKind.None && bl != ObstacleKind.Basket)) return 5;
            if (p.IsTile) return p.IsSpecial ? 0 : 1;
            return 0;
        }

        TileColor MostCommonColor()
        {
            var counts = new int[TileColors.Max];
            for (int i = 0; i < Topo.Count; i++)
            {
                var p = Board.Pieces[i];
                if (Topo.Exists[i] && p.IsTile && p.Color != TileColor.None) counts[(int)p.Color]++;
            }
            int best = -1;
            for (int c = 0; c < counts.Length; c++) if (counts[c] > 0 && (best < 0 || counts[c] > counts[best])) best = c;
            return best < 0 ? TileColor.None : (TileColor)best;
        }

        // ------------------------------------------------------------------ areas (ordered nearest first)

        void AddRow(List<int> cells, int y, int fromX)
        {
            int w = Topo.Width;
            for (int d = 0; d < w; d++)
            {
                int x1 = fromX - d, x2 = fromX + d;
                if (x1 >= 0 && Topo.Exists[y * w + x1]) cells.Add(y * w + x1);
                if (d > 0 && x2 < w && Topo.Exists[y * w + x2]) cells.Add(y * w + x2);
            }
        }

        void AddColumn(List<int> cells, int x, int fromY)
        {
            int w = Topo.Width, h = Topo.Height;
            for (int d = 0; d < h; d++)
            {
                int y1 = fromY - d, y2 = fromY + d;
                if (y1 >= 0 && Topo.Exists[y1 * w + x]) cells.Add(y1 * w + x);
                if (d > 0 && y2 < h && Topo.Exists[y2 * w + x]) cells.Add(y2 * w + x);
            }
        }

        void AddSquare(List<int> cells, int center, int radius)
        {
            int w = Topo.Width, cx = center % w, cy = center / w;
            for (int r = 0; r <= radius; r++)
                for (int y = cy - r; y <= cy + r; y++)
                    for (int x = cx - r; x <= cx + r; x++)
                    {
                        if (Math.Max(Math.Abs(x - cx), Math.Abs(y - cy)) != r) continue;
                        if (Topo.IsCell(x, y)) cells.Add(y * w + x);
                    }
        }

        void AddCross(List<int> cells, int center)
        {
            int w = Topo.Width, cx = center % w, cy = center / w;
            if (Topo.Exists[center]) cells.Add(center);
            for (int d = 0; d < 4; d++)
                if (Topo.IsCell(cx + Dx[d], cy + Dy[d])) cells.Add((cy + Dy[d]) * w + cx + Dx[d]);
        }

        void AddColor(List<int> cells, TileColor color, int from)
        {
            int w = Topo.Width, fx = from % w, fy = from / w;
            var list = new List<int>();
            for (int i = 0; i < Topo.Count; i++)
            {
                var p = Board.Pieces[i];
                if (Topo.Exists[i] && p.IsTile && p.Color == color) list.Add(i);
            }
            list.Sort((a, b) =>
            {
                int da = Math.Abs(a % w - fx) + Math.Abs(a / w - fy), db = Math.Abs(b % w - fx) + Math.Abs(b / w - fy);
                return da != db ? da.CompareTo(db) : a.CompareTo(b);
            });
            cells.AddRange(list);
        }

        void AddAll(List<int> cells, int from)
        {
            int w = Topo.Width, fx = from % w, fy = from / w;
            var list = new List<int>();
            for (int i = 0; i < Topo.Count; i++) if (Topo.Exists[i]) list.Add(i);
            list.Sort((a, b) =>
            {
                int da = Math.Max(Math.Abs(a % w - fx), Math.Abs(a / w - fy)), db = Math.Max(Math.Abs(b % w - fx), Math.Abs(b / w - fy));
                return da != db ? da.CompareTo(db) : a.CompareTo(b);
            });
            cells.AddRange(list);
        }

        // ================================================================== combos

        static ComboKind ComboOf(Piece a, Piece b)
        {
            bool ra = a.Special == SpecialKind.Rainbow, rb = b.Special == SpecialKind.Rainbow;
            if (ra && rb) return ComboKind.RainbowRainbow;
            if (ra || rb)
            {
                var other = ra ? b : a;
                switch (other.Special)
                {
                    case SpecialKind.LineH:
                    case SpecialKind.LineV: return ComboKind.RainbowLine;
                    case SpecialKind.Bomb: return ComboKind.RainbowBomb;
                    case SpecialKind.Bee: return ComboKind.RainbowBee;
                    default: return ComboKind.RainbowColor;
                }
            }
            if (!a.IsSpecial || !b.IsSpecial) return ComboKind.None;
            bool la = a.Special == SpecialKind.LineH || a.Special == SpecialKind.LineV;
            bool lb = b.Special == SpecialKind.LineH || b.Special == SpecialKind.LineV;
            bool ba = a.Special == SpecialKind.Bomb, bb = b.Special == SpecialKind.Bomb;
            bool ea = a.Special == SpecialKind.Bee, eb = b.Special == SpecialKind.Bee;
            if (la && lb) return ComboKind.LineLine;
            if ((la && bb) || (ba && lb)) return ComboKind.LineBomb;
            if (ba && bb) return ComboKind.BombBomb;
            if (ea && eb) return ComboKind.BeeBee;
            if ((ea && lb) || (la && eb)) return ComboKind.BeeLine;
            if ((ea && bb) || (ba && eb)) return ComboKind.BeeBomb;
            return ComboKind.None;
        }

        /// <summary>
        /// Two specials swapped together (or a rainbow with anything). <paramref name="pa"/> was dragged from
        /// <paramref name="ia"/> and now sits at <paramref name="ib"/> — the combo centre.
        /// </summary>
        void RunCombo(ComboKind combo, int ia, int ib, Piece pa, Piece pb)
        {
            int center = ib;
            int id = NextId();
            if (_log != null) AddEvent(new ComboEvent { Combo = combo, A = Topo.PosOf(ia), B = Topo.PosOf(ib), Center = Topo.PosOf(center), ActivationId = id });

            // Both pieces leave the board (they are the combo), except the plain tile of RainbowColor.
            void Consume(int cell, Piece p)
            {
                Board.Pieces[cell] = default;
                if (_log != null)
                    AddEvent(new PieceClearedEvent { PieceId = p.Id, Cell = Topo.PosOf(cell), Color = p.Color, Special = p.Special, Source = HitSource.Match, CauseId = id });
                if (p.Color != TileColor.None) Progress(GoalType.Collect, p.Color, cell);
            }

            var act = new Activation { Id = id, CauseId = -1, Generation = 0, PieceId = pa.Id, Kind = pa.Special, Color = pa.Color, Cell = center };
            var cells = new List<int>();
            switch (combo)
            {
                case ComboKind.RainbowRainbow:
                {
                    Consume(ib, pa);
                    Consume(ia, pb);
                    AddAll(cells, center);
                    LogCombo(act, cells, TileColor.None, combo);
                    foreach (var c in cells) Hit(c, HitSource.Rainbow, id, 0);
                    break;
                }
                case ComboKind.RainbowColor:
                {
                    bool aIsRainbow = pa.Special == SpecialKind.Rainbow;
                    int rainbowCell = aIsRainbow ? ib : ia;
                    var tile = aIsRainbow ? pb : pa;
                    Consume(rainbowCell, aIsRainbow ? pa : pb);
                    act.Cell = rainbowCell;
                    AddColor(cells, tile.Color, rainbowCell);
                    LogCombo(act, cells, tile.Color, combo);
                    foreach (var c in cells) Hit(c, HitSource.Rainbow, id, 0);
                    break;
                }
                case ComboKind.RainbowLine:
                case ComboKind.RainbowBomb:
                case ComboKind.RainbowBee:
                {
                    bool aIsRainbow = pa.Special == SpecialKind.Rainbow;
                    var special = aIsRainbow ? pb : pa;
                    int specialCell = aIsRainbow ? ia : ib;
                    Consume(aIsRainbow ? ib : ia, aIsRainbow ? pa : pb);
                    // Every free plain tile of the special's colour becomes that special...
                    var converted = new List<int>();
                    for (int i = 0; i < Topo.Count; i++)
                    {
                        var p = Board.Pieces[i];
                        if (!Topo.Exists[i] || !p.IsPlainTile || p.Color != special.Color || Board.Overlay[i] != ObstacleKind.None) continue;
                        converted.Add(i);
                    }
                    cells.AddRange(converted);
                    LogCombo(act, cells, special.Color, combo);
                    foreach (var i in converted)
                    {
                        var kind = special.Special;
                        if (kind == SpecialKind.LineH || kind == SpecialKind.LineV) kind = Rng.Next(2) == 0 ? SpecialKind.LineH : SpecialKind.LineV;
                        var p = Board.Pieces[i];
                        p.Special = kind;
                        Board.Pieces[i] = p;
                        if (_log != null) AddEvent(new TransformEvent { PieceId = p.Id, Cell = Topo.PosOf(i), Special = kind, Color = p.Color, CauseId = id });
                    }
                    // ...and they all fire, the swapped special first.
                    Hit(specialCell, HitSource.Rainbow, id, 0);
                    foreach (var i in converted) Hit(i, HitSource.Rainbow, id, 0);
                    break;
                }
                case ComboKind.LineLine:
                {
                    Consume(ib, pa);
                    Consume(ia, pb);
                    AddRow(cells, center / Topo.Width, center % Topo.Width);
                    var col = new List<int>();
                    AddColumn(col, center % Topo.Width, center / Topo.Width);
                    foreach (var c in col) if (c != center) cells.Add(c);
                    LogCombo(act, cells, TileColor.None, combo);
                    foreach (var c in cells) Hit(c, HitSource.Line, id, 0);
                    break;
                }
                case ComboKind.LineBomb:
                {
                    Consume(ib, pa);
                    Consume(ia, pb);
                    var set = new HashSet<int>();
                    int w = Topo.Width, cx = center % w, cy = center / w;
                    var tmp = new List<int>();
                    for (int d = -1; d <= 1; d++)
                    {
                        if (cy + d >= 0 && cy + d < Topo.Height) AddRow(tmp, cy + d, cx);
                        if (cx + d >= 0 && cx + d < w) AddColumn(tmp, cx + d, cy);
                    }
                    foreach (var c in tmp) if (set.Add(c)) cells.Add(c);
                    LogCombo(act, cells, TileColor.None, combo);
                    foreach (var c in cells) Hit(c, HitSource.Blast, id, 0);
                    break;
                }
                case ComboKind.BombBomb:
                {
                    Consume(ib, pa);
                    Consume(ia, pb);
                    AddSquare(cells, center, 2);
                    LogCombo(act, cells, TileColor.None, combo);
                    foreach (var c in cells) Hit(c, HitSource.Blast, id, 0);
                    break;
                }
                case ComboKind.BeeBee:
                case ComboKind.BeeLine:
                case ComboKind.BeeBomb:
                {
                    Consume(ib, pa);
                    Consume(ia, pb);
                    var other = pa.Special == SpecialKind.Bee ? pb : pa;
                    act.Kind = SpecialKind.Bee;
                    act.Color = (pa.Special == SpecialKind.Bee ? pa : pb).Color;
                    if (combo == ComboKind.BeeBee) ExecuteBee(act, 3, SpecialKind.None, combo);
                    else ExecuteBee(act, 1, other.Special, combo);
                    break;
                }
            }
            ProcessActivations();
        }

        void LogCombo(Activation a, List<int> cells, TileColor target, ComboKind combo)
        {
            var e = LogActivation(a, cells, target, combo);
            if (e != null) foreach (var c in cells) e.Cells.Add(Topo.PosOf(c));
        }

        // ================================================================== cascade

        /// <summary>Refill, then keep resolving new matches (each one wave) until the board is still.</summary>
        void Cascade()
        {
            Gravity();
            for (int guard = 0; guard < 200; guard++)
            {
                _wave++;
                ClearWaveState();
                var groups = FindMatches();
                if (groups.Count == 0) { _wave--; break; }
                ApplyMatches(groups);
                ProcessActivations();
                Gravity();
            }
        }
    }
}

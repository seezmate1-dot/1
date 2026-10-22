using System;
using System.Collections.Generic;

namespace SunnyValley.Core.Match3
{
    /// <summary>
    /// Reference player of the step log: starts from a copy of the board and applies events one by one, the way
    /// the Unity view does with sprites. Tests use it to prove that the log alone reproduces the engine's board;
    /// the view can use it to keep its model in sync (and as a cross-check in debug builds).
    /// Throws <see cref="InvalidOperationException"/> if an event does not fit the mirrored board.
    /// </summary>
    public sealed class StepLogMirror
    {
        readonly BoardTopology _topo;
        public readonly Piece[] Pieces;
        public readonly int[] Grass;
        public readonly ObstacleKind[] Overlay;
        public readonly int[] OverlayLayers;
        public readonly ObstacleKind[] Blocker;
        public readonly int[] BlockerLayers;
        public readonly int[] GoalCurrent;

        readonly List<StepEvent> _refill = new List<StepEvent>();

        public StepLogMirror(Match3Game game)
        {
            var b = game.Board;
            _topo = b.Topo;
            Pieces = (Piece[])b.Pieces.Clone();
            Grass = (int[])b.Grass.Clone();
            Overlay = (ObstacleKind[])b.Overlay.Clone();
            OverlayLayers = (int[])b.OverlayLayers.Clone();
            Blocker = (ObstacleKind[])b.Blocker.Clone();
            BlockerLayers = (int[])b.BlockerLayers.Clone();
            GoalCurrent = new int[game.Goals.Count];
            for (int k = 0; k < GoalCurrent.Length; k++) GoalCurrent[k] = game.Goals[k].Current;
        }

        int I(Pos p) => _topo.Index(p);

        public void ApplyAll(IEnumerable<StepEvent> events)
        {
            foreach (var e in events) Apply(e);
            Flush();
        }

        public void Apply(StepEvent e)
        {
            bool refillEvent = e is FallEvent || e is SpawnEvent || e is ChickRescuedEvent;
            if (!refillEvent) Flush();
            switch (e)
            {
                case SwapEvent s:
                {
                    int a = I(s.A), b = I(s.B);
                    Expect(Pieces[a].Id == s.PieceA && Pieces[b].Id == s.PieceB, "swap ids", e);
                    (Pieces[a], Pieces[b]) = (Pieces[b], Pieces[a]);
                    break;
                }
                case PieceClearedEvent c:
                {
                    int i = I(c.Cell);
                    Expect(Pieces[i].Id == c.PieceId, "cleared piece not at " + c.Cell, e);
                    Pieces[i] = default;
                    break;
                }
                case SpecialCreatedEvent sc:
                {
                    int i = I(sc.Cell);
                    Expect(Pieces[i].IsEmpty, "special created on an occupied cell", e);
                    Pieces[i] = new Piece
                    {
                        Id = sc.PieceId, Kind = PieceKind.Tile, Special = sc.Special,
                        Color = sc.Special == SpecialKind.Rainbow ? TileColor.None : sc.Color,
                    };
                    break;
                }
                case TransformEvent t:
                {
                    int i = I(t.Cell);
                    Expect(Pieces[i].Id == t.PieceId, "transform id", e);
                    var p = Pieces[i];
                    p.Special = t.Special;
                    p.Color = t.Color;
                    Pieces[i] = p;
                    break;
                }
                case ObstacleHitEvent h:
                {
                    int i = I(h.Cell);
                    switch (h.Obstacle)
                    {
                        case ObstacleKind.Grass:
                            Expect(Grass[i] == h.LayersBefore, "grass layers", e);
                            Grass[i] = h.LayersAfter;
                            break;
                        case ObstacleKind.Ice:
                        case ObstacleKind.Chain:
                        case ObstacleKind.Honey:
                            Expect(Overlay[i] == h.Obstacle && OverlayLayers[i] == h.LayersBefore, "overlay", e);
                            OverlayLayers[i] = h.Removed ? 0 : h.LayersAfter;
                            if (h.Removed) Overlay[i] = ObstacleKind.None;
                            break;
                        default:
                            Expect(Blocker[i] == h.Obstacle && BlockerLayers[i] == h.LayersBefore, "blocker", e);
                            BlockerLayers[i] = h.Removed ? 0 : h.LayersAfter;
                            if (h.Removed) Blocker[i] = ObstacleKind.None;
                            break;
                    }
                    break;
                }
                case SpreadEvent sp:
                {
                    int t = I(sp.To);
                    if (sp.Obstacle == ObstacleKind.Honey)
                    {
                        Overlay[t] = ObstacleKind.Honey;
                        OverlayLayers[t] = 1;
                    }
                    else
                    {
                        Expect(Pieces[t].Id == sp.ConsumedPieceId, "weed consumed id", e);
                        Pieces[t] = default;
                        Blocker[t] = ObstacleKind.Weed;
                        BlockerLayers[t] = 1;
                    }
                    break;
                }
                case ShuffleEvent sh:
                {
                    var byId = new Dictionary<int, Piece>();
                    foreach (var m in sh.Moves)
                    {
                        int f = I(m.From);
                        Expect(Pieces[f].Id == m.PieceId, "shuffle from", e);
                        byId[m.PieceId] = Pieces[f];
                    }
                    foreach (var m in sh.Moves) Pieces[I(m.From)] = default;
                    foreach (var m in sh.Moves)
                    {
                        var p = byId[m.PieceId];
                        p.Color = m.Color;
                        Pieces[I(m.To)] = p;
                    }
                    break;
                }
                case GoalProgressEvent gp:
                    GoalCurrent[gp.GoalIndex] = gp.Current;
                    break;
                case FallEvent _:
                case SpawnEvent _:
                case ChickRescuedEvent _:
                    _refill.Add(e);
                    break;
            }
        }

        /// <summary>Plays the buffered refill tick by tick: moves, then that tick's spawns, then rescues.</summary>
        void Flush()
        {
            if (_refill.Count == 0) return;
            var moves = new List<FallSegment>();
            var moveIds = new List<int>();
            int maxTick = 0;
            foreach (var e in _refill)
            {
                if (e is FallEvent f)
                    foreach (var s in f.Segments) { moves.Add(s); moveIds.Add(f.PieceId); maxTick = Math.Max(maxTick, s.Tick); }
                else if (e is SpawnEvent sp) maxTick = Math.Max(maxTick, sp.Tick);
                else if (e is ChickRescuedEvent cr) maxTick = Math.Max(maxTick, cr.Tick);
            }
            for (int t = 0; t <= maxTick; t++)
            {
                var pending = new List<int>();
                for (int k = 0; k < moves.Count; k++) if (moves[k].Tick == t) pending.Add(k);
                int guard = 0;
                while (pending.Count > 0)
                {
                    if (guard++ > 1000) throw new InvalidOperationException("refill moves at tick " + t + " cannot be ordered");
                    for (int n = pending.Count - 1; n >= 0; n--)
                    {
                        var s = moves[pending[n]];
                        int from = I(s.From), to = I(s.To);
                        if (!Pieces[to].IsEmpty) continue;
                        if (Pieces[from].Id != moveIds[pending[n]])
                            throw new InvalidOperationException("fall: piece " + moveIds[pending[n]] + " is not at " + s.From + " at tick " + t);
                        Pieces[to] = Pieces[from];
                        Pieces[from] = default;
                        pending.RemoveAt(n);
                    }
                }
                foreach (var e in _refill)
                    if (e is SpawnEvent sp && sp.Tick == t)
                    {
                        int i = I(sp.Cell);
                        if (!Pieces[i].IsEmpty) throw new InvalidOperationException("spawn on occupied " + sp.Cell);
                        Pieces[i] = new Piece { Id = sp.PieceId, Kind = sp.Kind, Color = sp.Color };
                    }
                foreach (var e in _refill)
                    if (e is ChickRescuedEvent cr && cr.Tick == t)
                    {
                        int i = I(cr.Cell);
                        if (Pieces[i].Id != cr.PieceId) throw new InvalidOperationException("rescued chick not at " + cr.Cell);
                        Pieces[i] = default;
                    }
            }
            _refill.Clear();
        }

        static void Expect(bool ok, string what, StepEvent e)
        {
            if (!ok) throw new InvalidOperationException("step log mismatch (" + what + ") at " + e.Type + " wave " + e.Wave);
        }

        /// <summary>Differences between the mirrored board and the engine's board (empty = identical).</summary>
        public List<string> Diff(Match3Game game)
        {
            Flush();
            var d = new List<string>();
            var b = game.Board;
            for (int i = 0; i < _topo.Count; i++)
            {
                if (!_topo.Exists[i]) continue;
                var p = _topo.PosOf(i);
                if (!Pieces[i].Equals(b.Pieces[i])) d.Add(p + " piece " + Pieces[i] + " vs " + b.Pieces[i]);
                if (Grass[i] != b.Grass[i]) d.Add(p + " grass " + Grass[i] + " vs " + b.Grass[i]);
                if (Overlay[i] != b.Overlay[i] || OverlayLayers[i] != b.OverlayLayers[i]) d.Add(p + " overlay " + Overlay[i] + OverlayLayers[i] + " vs " + b.Overlay[i] + b.OverlayLayers[i]);
                if (Blocker[i] != b.Blocker[i] || BlockerLayers[i] != b.BlockerLayers[i]) d.Add(p + " blocker " + Blocker[i] + BlockerLayers[i] + " vs " + b.Blocker[i] + b.BlockerLayers[i]);
            }
            for (int k = 0; k < GoalCurrent.Length; k++)
                if (GoalCurrent[k] != game.Goals[k].Current) d.Add("goal " + k + " " + GoalCurrent[k] + " vs " + game.Goals[k].Current);
            return d;
        }
    }
}

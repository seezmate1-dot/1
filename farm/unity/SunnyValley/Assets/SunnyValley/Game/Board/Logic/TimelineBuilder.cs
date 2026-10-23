using System;
using System.Collections.Generic;
using SunnyValley.Core.Match3;

namespace SunnyValley.Game.Match3View
{
    /// <summary>
    /// Turns a step log (Match3Game's MoveResult.Events, docs/LEVELS.md §5) into a timeline of cues, strictly in the
    /// engine's order: waves one after another; inside a wave the swap, the matches (pops, merges into new specials),
    /// the activations — each hit lands when the effect reaches its cell (a pitchfork's streak runs outwards, a
    /// bomb's shockwave rings out, a bee lands, a rainbow's beam arrives), chained specials fire when they are hit —
    /// then the refill tick by tick with accelerating ticks and a landing bounce; after the last wave: spreads,
    /// reshuffles, the end. Pure C#: the view only plays the cues, and the tests check the timing rules.
    /// One builder can be reused (its scratch collections are kept between builds).
    /// </summary>
    public sealed class TimelineBuilder
    {
        enum Area : byte { Instant, Group, Line, Blast, Bee, Rainbow, RainbowConvert, Nova, Shovel, Rake, Can }

        struct Cause
        {
            public Area Area;
            public float Start;
            public Pos Origin;
            public List<Pos> Cells;
            /// <summary>RainbowConvert: when the converted specials begin to fire.</summary>
            public float FireStart;
            public float Step;
        }

        readonly Dictionary<int, Cause> causes = new Dictionary<int, Cause>();
        readonly Dictionary<int, float> specialFire = new Dictionary<int, float>();
        readonly Dictionary<int, SpecialCreatedEvent> createdBy = new Dictionary<int, SpecialCreatedEvent>();
        readonly Dictionary<long, float> beeLanding = new Dictionary<long, float>();
        readonly List<StepEvent> refill = new List<StepEvent>();
        readonly List<GoalProgressEvent> chickGoals = new List<GoalProgressEvent>();
        readonly Dictionary<int, float> lastMoveEnd = new Dictionary<int, float>();

        Timeline tl;
        float cursor;      // where the next sequential thing starts
        float matchTime;   // when this wave's matches pop
        float lastHit;     // the last hit scheduled (goal ghosts start there)
        float hitEnd;      // last hit of the wave (refill waits for it)
        float busy;        // things that must finish before pieces fall (merges, new specials, swaps)
        int wave;
        int transforms;    // pre-level boosters placed one after another
        SwapEvent lastSwap;

        /// <summary>Builds the timeline of one action's events into <paramref name="into"/> (cleared first).</summary>
        public Timeline Build(IReadOnlyList<StepEvent> events, Timeline into = null)
        {
            tl = into ?? new Timeline();
            tl.Clear();
            causes.Clear();
            specialFire.Clear();
            createdBy.Clear();
            beeLanding.Clear();
            refill.Clear();
            chickGoals.Clear();
            lastMoveEnd.Clear();
            cursor = matchTime = lastHit = hitEnd = busy = 0;
            wave = 0;
            transforms = 0;
            lastSwap = null;

            for (int i = 0; i < events.Count; i++)
                if (events[i] is SpecialCreatedEvent sc && sc.CauseId >= 0)
                    createdBy[sc.CauseId] = sc;

            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                bool refillEvent = e is FallEvent || e is SpawnEvent || e is ChickRescuedEvent;
                if (refillEvent)
                {
                    refill.Add(e);
                    continue;
                }
                if (refill.Count > 0) FlushRefill();
                if (e.Wave > wave) StartWave(e.Wave);
                Handle(e);
            }
            if (refill.Count > 0) FlushRefill();
            if (transforms > 0) cursor = Math.Max(cursor, transforms * Timings.PreBoosterStep + Timings.Appear);
            tl.BoardTime = Math.Max(cursor, Math.Max(busy, hitEnd));
            tl.Finish();
            return tl;
        }

        void StartWave(int w)
        {
            wave = w;
            cursor = Math.Max(cursor, Math.Max(busy, hitEnd)) + Timings.WaveGap;
            matchTime = cursor;
            hitEnd = busy = cursor;
        }

        Cue NewCue(CueKind kind, float time, float duration, int pieceId = -1)
        {
            return new Cue { Kind = kind, Time = time, Duration = duration, PieceId = pieceId, From = Pos.None, To = Pos.None, Wave = wave };
        }

        void Add(Cue c)
        {
            if (c.IsMove && c.PieceId > 0)
            {
                // a piece never runs two moves at once: a later move waits for the earlier one
                if (lastMoveEnd.TryGetValue(c.PieceId, out float end) && c.Time < end - 1e-4f)
                {
                    c.Time = end;
                    tl.Delayed++;
                }
                lastMoveEnd[c.PieceId] = c.End;
            }
            tl.Add(ref c);
        }

        void Handle(StepEvent e)
        {
            switch (e)
            {
                case MovesChangedEvent mc:
                {
                    var c = NewCue(CueKind.Moves, cursor, 0);
                    c.Value = mc.MovesLeft;
                    c.Event = e;
                    Add(c);
                    break;
                }
                case SwapEvent s:
                {
                    lastSwap = s;
                    var a = NewCue(CueKind.SwapMove, cursor, Timings.Swap, s.PieceA);
                    a.From = s.A;
                    a.To = s.B;
                    Add(a);
                    var b = NewCue(CueKind.SwapMove, cursor, Timings.Swap, s.PieceB);
                    b.From = s.B;
                    b.To = s.A;
                    Add(b);
                    cursor += Timings.Swap;
                    matchTime = cursor;
                    busy = Math.Max(busy, cursor);
                    break;
                }
                case SwapRejectedEvent r:
                {
                    var c = NewCue(CueKind.Bounce, cursor, Timings.Bounce);
                    c.From = r.A;
                    c.To = r.B;
                    c.Value = (int)r.Reason;
                    c.Event = e;
                    Add(c);
                    cursor += Timings.Bounce;
                    break;
                }
                case ComboEvent cb when cb.Combo >= ComboKind.RainbowColor:
                    // the rainbow fires where it is; the partner stays (a tile is beamed, a special is copied)
                    causes[cb.ActivationId] = new Cause { Area = Area.Instant, Start = cursor, Origin = cb.Center };
                    break;
                case ComboEvent cb:
                {
                    // after the swap the dragged piece sits at the centre, the other one at A: it slides in
                    var g = NewCue(CueKind.Gather, cursor, Timings.Gather, lastSwap != null ? lastSwap.PieceB : -1);
                    g.From = cb.A;
                    g.To = cb.Center;
                    g.Value = (int)cb.Combo;
                    g.Event = e;
                    Add(g);
                    cursor += Timings.Gather;
                    busy = Math.Max(busy, cursor);
                    causes[cb.ActivationId] = new Cause { Area = Area.Instant, Start = cursor, Origin = cb.Center };
                    break;
                }
                case MatchEvent m:
                    causes[m.GroupId] = new Cause { Area = Area.Group, Start = matchTime, Origin = m.SpecialCell };
                    break;
                case PieceClearedEvent pc:
                    Cleared(pc);
                    break;
                case ObstacleHitEvent oh:
                {
                    float t;
                    if (oh.Obstacle == ObstacleKind.Basket) t = lastHit + Timings.BasketFly;
                    else
                    {
                        t = HitTime(oh.CauseId, oh.Cell);
                        if (oh.Source == HitSource.Adjacent) t += Timings.AdjacentDelay;
                    }
                    var c = NewCue(CueKind.ObstacleHit, t, Timings.Hit);
                    c.To = oh.Cell;
                    c.Value = oh.LayersAfter;
                    c.Event = e;
                    Add(c);
                    Hit(t);
                    break;
                }
                case GoalProgressEvent gp:
                {
                    if (gp.Goal == GoalType.Chick && gp.Delta > 0)
                    {
                        chickGoals.Add(gp); // the chick leaves during the refill
                        break;
                    }
                    var c = NewCue(CueKind.Goal, gp.Delta > 0 ? lastHit : cursor, Timings.GoalFly);
                    c.From = gp.Cell;
                    c.Value = gp.GoalIndex;
                    c.Event = e;
                    Add(c);
                    break;
                }
                case SpecialCreatedEvent sc:
                {
                    float t = (causes.TryGetValue(sc.CauseId, out var cause) ? cause.Start : matchTime) + Timings.Merge;
                    var c = NewCue(CueKind.SpecialAppear, t, Timings.Appear, sc.PieceId);
                    c.To = sc.Cell;
                    c.Value = (int)sc.Special;
                    c.Event = e;
                    Add(c);
                    busy = Math.Max(busy, t + 0.06f);
                    break;
                }
                case ActivationEvent a:
                    Activation(a);
                    break;
                case BeeFlightEvent bf:
                {
                    float start = causes.TryGetValue(bf.ActivationId, out var cause) ? cause.Start : cursor;
                    float depart = start + Timings.BeeTakeoff;
                    float flight = Timings.BeeFlight(bf.From, bf.To);
                    var c = NewCue(CueKind.BeeFlight, depart, flight);
                    c.From = bf.From;
                    c.To = bf.To;
                    c.Value = (int)bf.Carried;
                    c.Event = e;
                    Add(c);
                    beeLanding[Key(bf.ActivationId, bf.To)] = depart + flight;
                    break;
                }
                case TransformEvent tr:
                {
                    float t;
                    if (tr.CauseId >= 0 && causes.TryGetValue(tr.CauseId, out var cause))
                        t = cause.Start + Timings.RainbowCharge + IndexIn(cause.Cells, tr.Cell) * cause.Step + Timings.Beam;
                    else
                        t = cursor + transforms++ * Timings.PreBoosterStep;
                    var c = NewCue(CueKind.Transform, t, Timings.Appear, tr.PieceId);
                    c.To = tr.Cell;
                    c.Value = (int)tr.Special;
                    c.Event = e;
                    Add(c);
                    busy = Math.Max(busy, t);
                    break;
                }
                case BoosterEvent be:
                {
                    var c = NewCue(CueKind.Booster, cursor, be.Booster == BoosterKind.Shuffle ? 0.2f : Timings.ShovelLead);
                    c.To = be.Target;
                    c.Value = (int)be.Booster;
                    c.Event = e;
                    Add(c);
                    if (be.Booster != BoosterKind.Shuffle)
                    {
                        var area = be.Booster == BoosterKind.Shovel ? Area.Shovel : be.Booster == BoosterKind.Rake ? Area.Rake : Area.Can;
                        causes[be.ActivationId] = new Cause { Area = area, Start = cursor, Origin = be.Target, Cells = be.Cells };
                        if (be.Booster != BoosterKind.Shovel)
                        {
                            var s = NewCue(CueKind.LineStreak, cursor + Timings.SweepLead, 0.4f);
                            s.From = be.Booster == BoosterKind.Rake ? new Pos(0, be.Target.Y) : new Pos(be.Target.X, 0);
                            s.Value = be.Booster == BoosterKind.Rake ? 0 : 1;
                            s.Event = e;
                            Add(s);
                        }
                    }
                    break;
                }
                case SpreadEvent sp:
                {
                    float t = Math.Max(cursor, Math.Max(busy, hitEnd));
                    var c = NewCue(CueKind.Spread, t, Timings.Spread);
                    c.From = sp.From;
                    c.To = sp.To;
                    c.PieceId = sp.ConsumedPieceId;
                    c.Value = (int)sp.Obstacle;
                    c.Event = e;
                    Add(c);
                    cursor = t + Timings.Spread;
                    break;
                }
                case ShuffleEvent sh:
                {
                    float t = Math.Max(cursor, Math.Max(busy, hitEnd));
                    foreach (var m in sh.Moves)
                    {
                        var c = NewCue(CueKind.Shuffle, t, Timings.Shuffle, m.PieceId);
                        c.From = m.From;
                        c.To = m.To;
                        c.Value = (int)m.Color;
                        c.Event = e;
                        Add(c);
                    }
                    cursor = t + Timings.Shuffle;
                    break;
                }
                case GameEndEvent ge:
                {
                    float t = Math.Max(cursor, Math.Max(busy, hitEnd));
                    var c = NewCue(CueKind.End, t, 0);
                    c.Value = (int)ge.Status;
                    c.Event = e;
                    Add(c);
                    break;
                }
            }
        }

        void Hit(float t)
        {
            lastHit = t;
            if (t > hitEnd) hitEnd = t;
        }

        void Cleared(PieceClearedEvent pc)
        {
            float t = HitTime(pc.CauseId, pc.Cell);
            Cue c;
            if (pc.Special != SpecialKind.None)
            {
                c = NewCue(CueKind.Fire, t, Timings.Fire, pc.PieceId);
                specialFire[pc.PieceId] = t;
                busy = Math.Max(busy, t + Timings.Fire);
            }
            else if (!pc.IntoBasket.IsNone)
            {
                c = NewCue(CueKind.IntoBasket, t, Timings.BasketFly, pc.PieceId);
                c.To = pc.IntoBasket;
            }
            else if (createdBy.TryGetValue(pc.CauseId, out var sc) && Contains(sc.From, pc.Cell))
            {
                c = NewCue(CueKind.Merge, t, Timings.Merge, pc.PieceId);
                c.To = sc.Cell;
                busy = Math.Max(busy, t + Timings.Merge);
            }
            else
            {
                c = NewCue(CueKind.Pop, t, Timings.Pop, pc.PieceId);
            }
            c.From = pc.Cell;
            c.Value = (int)pc.Color;
            c.Event = pc;
            Add(c);
            Hit(t);
        }

        void Activation(ActivationEvent a)
        {
            float start;
            if (causes.TryGetValue(a.ActivationId, out var known)) start = known.Start;
            else if (a.PieceId > 0 && specialFire.TryGetValue(a.PieceId, out float fired)) start = fired;
            else start = HitTime(a.CauseId, a.Cell);

            var cause = new Cause { Start = start, Origin = a.Cell, Cells = a.Cells, Area = Area.Instant };
            switch (a.Combo)
            {
                case ComboKind.LineLine:
                    cause.Area = Area.Line;
                    Streak(start, a.Cell, 0, a);
                    Streak(start, a.Cell, 1, a);
                    Shake(start, 1);
                    break;
                case ComboKind.LineBomb:
                    cause.Area = Area.Line;
                    for (int d = -1; d <= 1; d++)
                    {
                        Streak(start, new Pos(a.Cell.X, a.Cell.Y + d), 0, a);
                        Streak(start, new Pos(a.Cell.X + d, a.Cell.Y), 1, a);
                    }
                    Shake(start, 2);
                    break;
                case ComboKind.BombBomb:
                    cause.Area = Area.Blast;
                    Blast(start, a.Cell, 2, a);
                    Shake(start + Timings.BombFuse, 3);
                    break;
                case ComboKind.RainbowColor:
                    cause.Area = Area.Rainbow;
                    Rainbow(ref cause, a);
                    break;
                case ComboKind.RainbowLine:
                case ComboKind.RainbowBomb:
                case ComboKind.RainbowBee:
                    cause.Area = Area.RainbowConvert;
                    Rainbow(ref cause, a);
                    cause.FireStart = start + Timings.RainbowCharge + a.Cells.Count * cause.Step + Timings.Beam + Timings.ConvertPause;
                    Shake(cause.FireStart, 2);
                    break;
                case ComboKind.RainbowRainbow:
                {
                    cause.Area = Area.Nova;
                    var c = NewCue(CueKind.RainbowCharge, start, Timings.NovaLead, -1);
                    c.From = a.Cell;
                    c.Value = -1;
                    c.Event = a;
                    Add(c);
                    Blast(start + Timings.NovaLead - Timings.BombFuse, a.Cell, 9, a);
                    Shake(start + Timings.NovaLead, 3);
                    break;
                }
                case ComboKind.BeeBee:
                case ComboKind.BeeLine:
                case ComboKind.BeeBomb:
                    cause.Area = Area.Bee;
                    break;
                default:
                    switch (a.Special)
                    {
                        case SpecialKind.LineH:
                        case SpecialKind.LineV:
                            cause.Area = Area.Line;
                            Streak(start, a.Cell, a.Special == SpecialKind.LineH ? 0 : 1, a);
                            break;
                        case SpecialKind.Bomb:
                            cause.Area = Area.Blast;
                            Blast(start, a.Cell, 1, a);
                            Shake(start + Timings.BombFuse, 1);
                            break;
                        case SpecialKind.Bee:
                            cause.Area = Area.Bee;
                            break;
                        case SpecialKind.Rainbow:
                            cause.Area = Area.Rainbow;
                            Rainbow(ref cause, a);
                            break;
                    }
                    break;
            }
            causes[a.ActivationId] = cause;
        }

        void Streak(float start, Pos from, int vertical, ActivationEvent a)
        {
            var c = NewCue(CueKind.LineStreak, start, 0.4f);
            c.From = from;
            c.Value = vertical;
            c.Event = a;
            Add(c);
        }

        void Blast(float start, Pos at, int radius, ActivationEvent a)
        {
            var c = NewCue(CueKind.Blast, start + Timings.BombFuse, 0.4f + 0.05f * Math.Min(radius, 4));
            c.From = at;
            c.Value = radius;
            c.Event = a;
            Add(c);
        }

        void Shake(float t, int strength)
        {
            var c = NewCue(CueKind.Shake, t, 0.12f + 0.06f * strength);
            c.Value = strength;
            Add(c);
        }

        void Rainbow(ref Cause cause, ActivationEvent a)
        {
            int n = Math.Max(1, a.Cells.Count);
            cause.Step = Math.Min(Timings.RainbowStep, Timings.RainbowSpan / n);
            var charge = NewCue(CueKind.RainbowCharge, cause.Start, Timings.RainbowCharge);
            charge.From = a.Cell;
            charge.Value = (int)a.TargetColor;
            charge.Event = a;
            Add(charge);
            for (int k = 0; k < a.Cells.Count; k++)
            {
                var b = NewCue(CueKind.Beam, cause.Start + Timings.RainbowCharge + k * cause.Step, Timings.Beam + 0.1f);
                b.From = a.Cell;
                b.To = a.Cells[k];
                b.Event = a;
                Add(b);
            }
        }

        /// <summary>When the effect of <paramref name="causeId"/> reaches <paramref name="cell"/>.</summary>
        float HitTime(int causeId, Pos cell)
        {
            if (causeId < 0 || !causes.TryGetValue(causeId, out var c)) return Math.Max(cursor, matchTime);
            switch (c.Area)
            {
                case Area.Group:
                    return c.Start;
                case Area.Line:
                    return c.Start + Timings.LineLead + Manhattan(c.Origin, cell) * Timings.LineStep;
                case Area.Blast:
                    return c.Start + Timings.BombFuse + Chebyshev(c.Origin, cell) * Timings.BombStep;
                case Area.Bee:
                    return beeLanding.TryGetValue(Key(causeId, cell), out float land) ? land : c.Start + Timings.BeeCross;
                case Area.Rainbow:
                    return c.Start + Timings.RainbowCharge + IndexIn(c.Cells, cell) * c.Step + Timings.Beam;
                case Area.RainbowConvert:
                {
                    int k = IndexIn(c.Cells, cell);
                    return k < 0 ? c.FireStart - Timings.ConvertFireStep * 0.5f : c.FireStart + k * Timings.ConvertFireStep;
                }
                case Area.Nova:
                    return c.Start + Timings.NovaLead + Chebyshev(c.Origin, cell) * Timings.NovaStep;
                case Area.Shovel:
                    return c.Start + Timings.ShovelLead;
                case Area.Rake:
                    return c.Start + Timings.SweepLead + cell.X * Timings.SweepStep;
                case Area.Can:
                    return c.Start + Timings.SweepLead + cell.Y * Timings.SweepStep;
                default:
                    return c.Start;
            }
        }

        /// <summary>The refill of this wave: by ticks that get shorter (the pieces accelerate), a bounce on landing.</summary>
        void FlushRefill()
        {
            float start = Math.Max(cursor, Math.Max(busy, hitEnd > 0 ? hitEnd + Timings.RefillDelay : 0));
            int maxTick = 0;
            foreach (var e in refill)
            {
                if (e is FallEvent f)
                    foreach (var s in f.Segments) maxTick = Math.Max(maxTick, s.Tick);
                else if (e is SpawnEvent sp) maxTick = Math.Max(maxTick, sp.Tick);
                else if (e is ChickRescuedEvent cr) maxTick = Math.Max(maxTick, cr.Tick);
            }
            var tickStart = new float[maxTick + 2];
            tickStart[0] = start;
            for (int k = 0; k <= maxTick; k++) tickStart[k + 1] = tickStart[k] + Timings.TickDuration(k);

            float end = start;
            foreach (var e in refill)
            {
                switch (e)
                {
                    case SpawnEvent sp:
                    {
                        var c = NewCue(CueKind.Spawn, tickStart[sp.Tick], Timings.TickDuration(sp.Tick), sp.PieceId);
                        c.To = sp.Cell;
                        c.Value = (int)sp.Color;
                        c.Event = e;
                        Add(c);
                        end = Math.Max(end, c.End);
                        break;
                    }
                    case FallEvent f:
                        foreach (var s in f.Segments)
                        {
                            var c = NewCue(CueKind.Fall, tickStart[s.Tick], Timings.TickDuration(s.Tick), f.PieceId);
                            c.From = s.From;
                            c.To = s.To;
                            c.Value = (int)s.Kind;
                            c.Event = e;
                            Add(c);
                            end = Math.Max(end, c.End);
                        }
                        break;
                    case ChickRescuedEvent cr:
                    {
                        var c = NewCue(CueKind.ChickExit, tickStart[cr.Tick], Timings.ChickExit, cr.PieceId);
                        c.From = cr.Cell;
                        c.Event = e;
                        Add(c);
                        for (int k = 0; k < chickGoals.Count; k++)
                        {
                            if (chickGoals[k].Cell != cr.Cell) continue;
                            var g = NewCue(CueKind.Goal, c.Time + 0.1f, Timings.GoalFly);
                            g.From = cr.Cell;
                            g.Value = chickGoals[k].GoalIndex;
                            g.Event = chickGoals[k];
                            Add(g);
                            chickGoals.RemoveAt(k);
                            break;
                        }
                        end = Math.Max(end, tickStart[cr.Tick + 1]);
                        break;
                    }
                }
            }
            // a bounce for every piece that moved, when its last move is over
            foreach (var e in refill)
            {
                int id = e is FallEvent f ? f.PieceId : e is SpawnEvent sp ? sp.PieceId : -1;
                if (id <= 0 || !lastMoveEnd.TryGetValue(id, out float landed)) continue;
                if (e is SpawnEvent && HasFall(id)) continue; // bounces after its fall
                var c = NewCue(CueKind.Land, landed, Timings.Land, id);
                c.Event = e;
                Add(c);
            }
            // goals of chicks rescued in an earlier refill bookkeeping (should not happen): show them now
            foreach (var g in chickGoals)
            {
                var c = NewCue(CueKind.Goal, end, Timings.GoalFly);
                c.From = g.Cell;
                c.Value = g.GoalIndex;
                c.Event = g;
                Add(c);
            }
            chickGoals.Clear();
            refill.Clear();
            cursor = end;
            busy = Math.Max(busy, end);
            hitEnd = Math.Max(hitEnd, end);
        }

        bool HasFall(int pieceId)
        {
            foreach (var e in refill)
                if (e is FallEvent f && f.PieceId == pieceId) return true;
            return false;
        }

        static long Key(int id, Pos p) => ((long)id << 16) | (long)((p.Y & 0xFF) << 8) | (long)(p.X & 0xFF);

        static int Manhattan(Pos a, Pos b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

        static int Chebyshev(Pos a, Pos b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

        static int IndexIn(List<Pos> cells, Pos p)
        {
            if (cells == null) return 0;
            for (int i = 0; i < cells.Count; i++)
                if (cells[i] == p) return i;
            return -1;
        }

        static bool Contains(List<Pos> cells, Pos p) => IndexIn(cells, p) >= 0;
    }
}

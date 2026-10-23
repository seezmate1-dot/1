using System;
using System.Collections.Generic;
using SunnyValley.Core.Match3;
using SunnyValley.Game.UI;
using SunnyValley.Layout;
using UnityEngine;

namespace SunnyValley.Game.Match3View
{
    /// <summary>
    /// Playing timelines: each cue starts its animation at exactly its time on the board clock (a cue that is due
    /// in the middle of a frame starts already advanced, so a slow frame never drifts the waves apart).
    /// </summary>
    public sealed partial class BoardView
    {
        readonly TimelineBuilder builder = new TimelineBuilder();
        readonly Timeline moveTimeline = new Timeline();
        Timeline timeline;
        int cueIndex;
        float playStart;
        bool playing, resyncAfter;
        Action done;

        static readonly Color Gold = new Color(1f, 0.86f, 0.35f);

        /// <summary>Animates one action's step log, then calls <paramref name="onDone"/> when the pieces are still.</summary>
        public float Play(IReadOnlyList<StepEvent> events, Action onDone)
        {
            builder.Build(events, moveTimeline);
            return PlayTimeline(moveTimeline, onDone, true);
        }

        /// <summary>
        /// Plays a timeline (a move's, or the bonus moves'). <paramref name="resync"/>: check against the game when it
        /// ends (off for the bonus moves, which are decoration on top of the finished game).
        /// </summary>
        public float PlayTimeline(Timeline tl, Action onDone, bool resync)
        {
            StopPlaying();
            Poke();
            Select(Pos.None);
            timeline = tl;
            cueIndex = 0;
            playStart = now;
            playing = true;
            resyncAfter = resync;
            done = onDone;
            RunCues();
            return tl.BoardTime;
        }

        /// <summary>Drops whatever is playing (a rebuild redraws from the game anyway).</summary>
        void StopPlaying()
        {
            playing = false;
            timeline = null;
            done = null;
        }

        void RunCues()
        {
            var cues = timeline.Cues;
            float t = now - playStart;
            while (cueIndex < cues.Count && cues[cueIndex].Time <= t)
            {
                var c = cues[cueIndex++];
                try
                {
                    Execute(c, playStart + c.Time);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
            if (cueIndex >= cues.Count && t >= timeline.BoardTime)
            {
                playing = false;
                if (resyncAfter)
                    Resync();
                var cb = done;
                done = null;
                timeline = null;
                cb?.Invoke();
            }
        }

        Color ColorOf(int value) => value < 0 || value >= TileColors.Max ? Gold : BoardSprites.CropColors[value];

        void Execute(Cue c, float t)
        {
            float d = c.Duration;
            switch (c.Kind)
            {
                case CueKind.Moves:
                    MovesShown?.Invoke(c.Value);
                    break;

                case CueKind.SwapMove:
                {
                    var p = Piece(c.PieceId);
                    if (p == null)
                        break;
                    p.MoveTo(At(c.To), t, d, Easing.Kind.InOutQuad);
                    p.Cell = c.To;
                    break;
                }

                case CueKind.Bounce:
                {
                    var a = PieceAt(c.From);
                    var b = PieceAt(c.To);
                    var reason = (RejectReason)c.Value;
                    if (reason == RejectReason.NoMatch && a != null && b != null)
                    {
                        a.MoveTo(At(c.To), t, d, Easing.Kind.Linear, PathKind.PingPong);
                        b.MoveTo(At(c.From), t, d, Easing.Kind.Linear, PathKind.PingPong);
                    }
                    else if (a != null)
                    {
                        a.MoveTo(At(c.From) + (At(c.To) - At(c.From)) * 0.18f, t, d * 0.7f, Easing.Kind.Linear, PathKind.PingPong);
                    }
                    break;
                }

                case CueKind.Gather:
                {
                    var p = Piece(c.PieceId);
                    if (p != null)
                    {
                        p.MoveTo(At(c.To), t, d, Easing.Kind.InQuad);
                        p.Cell = c.To;
                    }
                    fx.Flash(At(c.To), new Color(1, 1, 0.85f, 0.9f), cell * 2.2f, t + d * 0.6f, 0.3f);
                    break;
                }

                case CueKind.Pop:
                {
                    var p = Piece(c.PieceId);
                    if (p == null)
                        break;
                    var at = p.Rt.anchoredPosition;
                    p.Effect(ScaleFx.Pop, t, d);
                    Retire(p, t + d);
                    var col = ColorOf(c.Value);
                    fx.Flash(at, new Color(col.r, col.g, col.b, 0.55f), cell * 1.3f, t, d * 0.9f);
                    fx.Burst(at, col, cell, t + d * 0.2f, 7);
                    break;
                }

                case CueKind.Merge:
                case CueKind.IntoBasket:
                {
                    var p = Piece(c.PieceId);
                    if (p == null)
                        break;
                    bool basket = c.Kind == CueKind.IntoBasket;
                    p.MoveTo(At(c.To), t, d, Easing.Kind.InQuad, basket ? PathKind.Arc : PathKind.Line, basket ? 0.7f : 0);
                    p.Effect(ScaleFx.Shrink, t, d);
                    Retire(p, t + d);
                    if (!basket)
                        fx.Burst(At(c.From), Color.Lerp(ColorOf(c.Value), Color.white, 0.4f), cell, t, 3, 0.5f);
                    break;
                }

                case CueKind.Fire:
                {
                    var p = Piece(c.PieceId);
                    var at = p != null ? p.Rt.anchoredPosition : At(c.From);
                    if (p != null)
                    {
                        p.Effect(ScaleFx.Fire, t, d);
                        Retire(p, t + d);
                    }
                    var col = ColorOf(c.Value);
                    fx.Flash(at, new Color(1, 1, 0.9f, 1), cell * 2.4f, t, 0.3f);
                    fx.RingOut(at, new Color(col.r, col.g, col.b, 0.9f), cell * 0.5f, cell * 2.2f, t, 0.35f);
                    fx.Burst(at, col, cell, t, 10, 1.3f);
                    break;
                }

                case CueKind.SpecialAppear:
                {
                    var e = c.Event as SpecialCreatedEvent;
                    if (e == null)
                        break;
                    var color = e.Special == SpecialKind.Rainbow ? TileColor.None : e.Color;
                    var old = PieceAt(c.To);
                    if (old != null && old.Id != c.PieceId)
                        Retire(old, t);
                    var p = NewPiece(c.PieceId, PieceKind.Tile, color, e.Special, c.To);
                    p.Rt.localScale = Vector3.zero;
                    p.Effect(ScaleFx.Appear, t, d);
                    fx.RingOut(At(c.To), new Color(1, 1, 0.8f, 0.9f), cell * 0.3f, cell * 2f, t, 0.4f);
                    fx.Burst(At(c.To), Gold, cell, t, 8, 0.9f);
                    break;
                }

                case CueKind.Transform:
                {
                    var p = Piece(c.PieceId) ?? PieceAt(c.To);
                    if (p == null)
                        break;
                    var special = (SpecialKind)c.Value;
                    var color = p.Color;
                    if (c.Event is TransformEvent te)
                    {
                        special = te.Special;
                        color = te.Color;
                    }
                    p.SetLook(PieceKind.Tile, special == SpecialKind.Rainbow ? TileColor.None : color, special);
                    p.Effect(ScaleFx.Punch, t, d);
                    fx.Flash(At(c.To), new Color(1, 1, 0.85f, 0.9f), cell * 1.8f, t, 0.3f);
                    fx.Burst(At(c.To), Gold, cell, t, 6, 0.8f);
                    break;
                }

                case CueKind.ObstacleHit:
                {
                    if (!(c.Event is ObstacleHitEvent h))
                        break;
                    var cv = CellAt(h.Cell);
                    cv?.Apply(h, t);
                    var col = CellVisual.Debris(h.Obstacle);
                    int n = h.Removed || h.Harvested ? 10 : 6;
                    fx.Burst(At(h.Cell), col, cell, t, n, 1.1f, h.Obstacle == ObstacleKind.Crate || h.Obstacle == ObstacleKind.Stone ? BoardSprites.Dot : null);
                    if (h.Removed || h.Harvested)
                        fx.RingOut(At(h.Cell), new Color(col.r, col.g, col.b, 0.8f), cell * 0.4f, cell * 1.8f, t, 0.35f);
                    break;
                }

                case CueKind.Goal:
                    Ghost(c, t, d);
                    break;

                case CueKind.LineStreak:
                    Streak(c, t);
                    break;

                case CueKind.Blast:
                {
                    var at = At(c.From);
                    float size = cell * (2 * Mathf.Min(c.Value, 5) + 1);
                    fx.Flash(at, new Color(1f, 0.75f, 0.3f, 0.95f), size * 1.1f, t, 0.35f);
                    fx.RingOut(at, new Color(1f, 0.9f, 0.6f, 1f), cell * 0.6f, size * 1.25f, t, d);
                    fx.Burst(at, new Color(1f, 0.55f, 0.15f), cell, t, 14, 1.5f + 0.3f * c.Value);
                    break;
                }

                case CueKind.BeeFlight:
                {
                    var from = At(c.From);
                    var to = At(c.To);
                    float arc = cell * 1.6f;
                    var bee = fx.Fly(BoardSprites.Bee, from, to, cell * 0.95f, arc, t, d, Easing.Kind.InOutQuad);
                    bee.S0 = 1.1f;
                    bee.S1 = 0.85f;
                    bee.Spin = 0;
                    if (c.Value != 0)
                    {
                        var carried = fx.Fly(BoardSprites.Special((SpecialKind)c.Value), from + new Vector2(0, -cell * 0.25f), to, cell * 0.55f, arc, t, d, Easing.Kind.InOutQuad);
                        carried.Spin = 0;
                    }
                    fx.Burst(to, new Color(1f, 0.85f, 0.2f), cell, t + d, 6, 0.8f);
                    break;
                }

                case CueKind.RainbowCharge:
                {
                    var at = At(c.From);
                    var col = c.Value >= 0 ? ColorOf(c.Value) : Gold;
                    var glow = fx.Flash(at, new Color(col.r, col.g, col.b, 0.9f), cell * 2.6f, t, d + 0.15f);
                    glow.S0 = 0.3f;
                    glow.S1 = 1.1f;
                    glow.Spin = 180;
                    fx.RingOut(at, new Color(1, 1, 1, 0.9f), cell * 2.4f, cell * 0.6f, t, d);
                    break;
                }

                case CueKind.Beam:
                {
                    var col = c.Event is ActivationEvent a && a.TargetColor != TileColor.None ? BoardSprites.ColorOf(a.TargetColor) : Gold;
                    fx.StreakLine(At(c.From), At(c.To), Color.Lerp(col, Color.white, 0.45f), cell * 0.32f, t, d);
                    break;
                }

                case CueKind.Shake:
                    shakeT0 = t;
                    shakeDur = d;
                    shakeAmp = cell * (c.Value >= 3 ? 0.14f : c.Value == 2 ? 0.09f : 0.05f);
                    break;

                case CueKind.Booster:
                    BoosterFx(c, t, d);
                    break;

                case CueKind.Fall:
                {
                    var p = Piece(c.PieceId);
                    if (p == null)
                        break;
                    p.MoveTo(At(c.To), t, d, Easing.Kind.Linear, c.Value == (int)FallKind.Portal ? PathKind.Portal : PathKind.Line);
                    p.Cell = c.To;
                    break;
                }

                case CueKind.Spawn:
                {
                    if (!(c.Event is SpawnEvent s))
                        break;
                    var p = NewPiece(c.PieceId, s.Kind, s.Color, SpecialKind.None, c.To);
                    p.Rt.anchoredPosition = At(c.To) + new Vector2(0, cell);
                    p.MoveTo(At(c.To), t, d, Easing.Kind.Linear);
                    if (c.To.Y > 0 && game != null && game.Topo.IsCell(c.To.X, c.To.Y - 1))
                        p.FadeIn(t, d);
                    break;
                }

                case CueKind.Land:
                    Piece(c.PieceId)?.Effect(ScaleFx.Land, t, d);
                    break;

                case CueKind.ChickExit:
                {
                    var p = Piece(c.PieceId);
                    if (p == null)
                        break;
                    p.MoveTo(At(c.From) + new Vector2(0, -cell * 0.9f), t, d, Easing.Kind.InQuad, PathKind.Arc, 0.9f);
                    p.Effect(ScaleFx.Hop, t, d);
                    Retire(p, t + d);
                    fx.Burst(At(c.From), new Color(1f, 0.9f, 0.4f), cell, t, 8, 0.9f, BoardSprites.Spark);
                    break;
                }

                case CueKind.Spread:
                {
                    if (!(c.Event is SpreadEvent s))
                        break;
                    var cv = CellAt(s.To);
                    if (s.Obstacle == ObstacleKind.Honey)
                    {
                        cv?.SetOverlay(ObstacleKind.Honey, 1);
                    }
                    else
                    {
                        var p = Piece(s.ConsumedPieceId);
                        if (p != null)
                        {
                            p.Effect(ScaleFx.Shrink, t, d * 0.6f);
                            Retire(p, t + d * 0.6f);
                        }
                        cv?.SetBlocker(ObstacleKind.Weed, 1);
                    }
                    cv?.Punch(t);
                    fx.Burst(At(s.To), CellVisual.Debris(s.Obstacle), cell, t, 6, 0.6f);
                    break;
                }

                case CueKind.Shuffle:
                {
                    var p = Piece(c.PieceId);
                    if (p == null)
                        break;
                    var color = (TileColor)c.Value;
                    if (p.Kind == PieceKind.Tile && p.Special == SpecialKind.None && color != p.Color && color != TileColor.None)
                        p.SetLook(p.Kind, color, p.Special);
                    p.MoveTo(At(c.To), t, d, Easing.Kind.InOutQuad, PathKind.Arc, 1.1f);
                    p.Cell = c.To;
                    break;
                }

                case CueKind.BonusSpark:
                {
                    if (fly == null || MovesTarget == null)
                        break;
                    var from = ToFly(MovesTarget());
                    var to = ToFly(WorldAt(c.To));
                    var s = fly.Fly(BoardSprites.Spark, from, to, cell * 0.9f, cell * 2f, t, d, Easing.Kind.InOutQuad);
                    s.Color = new Color(1f, 0.92f, 0.5f);
                    s.Spin = 360;
                    var trail = fly.Fly(BoardSprites.Glow, from, to, cell * 0.8f, cell * 2f, t, d, Easing.Kind.InOutQuad);
                    trail.Color = new Color(1f, 0.8f, 0.3f, 0.7f);
                    trail.A0 = trail.A1 = 0.7f;
                    break;
                }
            }
        }

        CellVisual CellAt(Pos p) => game != null && game.Topo.IsCell(p) ? cells[game.Topo.Index(p)] : null;

        /// <summary>The pitchfork heads fly out both ways along the line, each dragging a streak.</summary>
        void Streak(Cue c, float t)
        {
            bool vertical = c.Value == 1;
            var at = At(c.From);
            var col = Gold;
            if (c.Event is ActivationEvent a && a.Color != TileColor.None)
                col = Color.Lerp(BoardSprites.ColorOf(a.Color), Color.white, 0.35f);
            float width = cell * 0.62f;
            for (int dir = -1; dir <= 1; dir += 2)
            {
                Vector2 end;
                int cellsToEdge;
                if (vertical)
                {
                    end = new Vector2(at.x, dir < 0 ? -cell : (rows + 1) * cell);
                    cellsToEdge = dir < 0 ? rows - c.From.Y : c.From.Y + 1;
                }
                else
                {
                    end = new Vector2(dir < 0 ? -cell : (cols + 1) * cell, at.y);
                    cellsToEdge = dir < 0 ? c.From.X + 1 : cols - c.From.X;
                }
                float dur = Timings.LineLead + (cellsToEdge + 1) * Timings.LineStep;
                fx.StreakLine(at, end, new Color(col.r, col.g, col.b, 0.95f), width, t, dur + 0.18f);
                var head = fx.Fly(BoardSprites.Pitchfork, at, end, cell * 0.85f, 0, t, dur, Easing.Kind.Linear);
                head.Orient = true;
                head.OrientOffset = -90;
                head.A0 = head.A1 = 1;
            }
            fx.Flash(at, new Color(1, 1, 0.85f, 1), cell * 1.8f, t, 0.25f);
        }

        void BoosterFx(Cue c, float t, float d)
        {
            var kind = (BoosterKind)c.Value;
            switch (kind)
            {
                case BoosterKind.Shovel:
                {
                    var to = At(c.To);
                    var s = fx.Fly(ShapeSprites.Icon(Icons.Shovel), to + new Vector2(cell * 0.4f, cell * 1.6f), to + new Vector2(cell * 0.2f, cell * 0.3f), cell * 1.4f, 0, t, d, Easing.Kind.InQuad);
                    s.Rot = 25;
                    s.A1 = 0.6f;
                    fx.Burst(to, new Color(0.55f, 0.36f, 0.2f), cell, t + d, 10, 1.1f, BoardSprites.Dot);
                    shakeT0 = t + d;
                    shakeDur = 0.15f;
                    shakeAmp = cell * 0.05f;
                    break;
                }
                case BoosterKind.Rake:
                {
                    float y = At(c.To).y;
                    var r = fx.Fly(ShapeSprites.Icon(Icons.Rake), new Vector2(-cell, y), new Vector2((cols + 1) * cell, y), cell * 1.3f, 0, t + Timings.SweepLead - 0.06f,
                        cols * Timings.SweepStep + 0.15f, Easing.Kind.Linear);
                    r.Rot = -30;
                    break;
                }
                case BoosterKind.WateringCan:
                {
                    float x = At(c.To).x;
                    var w = fx.Fly(ShapeSprites.Icon(Icons.Can), new Vector2(x + cell * 0.6f, (rows + 1) * cell), new Vector2(x + cell * 0.6f, -cell), cell * 1.3f, 0,
                        t + Timings.SweepLead - 0.06f, rows * Timings.SweepStep + 0.15f, Easing.Kind.Linear);
                    w.Rot = 20;
                    for (int i = 0; i < rows; i++)
                        fx.Burst(new Vector2(x, (rows - i - 0.5f) * cell), new Color(0.45f, 0.75f, 1f), cell, t + Timings.SweepLead + i * Timings.SweepStep, 3, 0.5f, BoardSprites.Dot);
                    break;
                }
                case BoosterKind.Shuffle:
                {
                    var mid = new Vector2(cols * cell / 2, rows * cell / 2);
                    var s = fx.Flash(mid, new Color(0.8f, 0.6f, 1f, 0.6f), Mathf.Max(cols, rows) * cell, t, 0.5f);
                    s.Spin = 90;
                    break;
                }
            }
        }

        /// <summary>A goal ghost: the thing the goal counts flies from its cell to the goal's icon.</summary>
        void Ghost(Cue c, float t, float d)
        {
            if (!(c.Event is GoalProgressEvent g))
                return;
            if (g.Delta <= 0 || fly == null || GoalTarget == null)
            {
                GoalReached?.Invoke(g);
                return;
            }
            string icon = GoalIcons.Ghost(g.Goal, game != null && g.GoalIndex < game.Goals.Count ? game.Goals[g.GoalIndex].Color : TileColor.None,
                game?.Level.BedCrop);
            var from = ToFly(WorldAt(g.Cell));
            var to = ToFly(GoalTarget(g.GoalIndex));
            var f = fly.Fly(ShapeSprites.Icon(icon), from, to, cell * 0.85f, cell * 1.4f, t, d, Easing.Kind.InOutQuad);
            f.S0 = 1.15f;
            f.S1 = 0.7f;
            f.Payload = g;
            f.Tag = 1;
        }

        void OnFlyEnded(FxItem f)
        {
            if (f.Payload is GoalProgressEvent g)
                GoalReached?.Invoke(g);
        }

        // ------------------------------------------------------------------ the level's start

        /// <summary>The pieces drop in row by row from above the board. Returns how long it takes.</summary>
        public float Intro()
        {
            float last = 0;
            foreach (var p in live)
            {
                if (!p.Live)
                    continue;
                var home = At(p.Cell);
                float delay = 0.08f + (rows - 1 - p.Cell.Y) * 0.045f + p.Cell.X * 0.012f;
                float dur = 0.32f + 0.03f * (rows - p.Cell.Y);
                p.Rt.anchoredPosition = home + new Vector2(0, (rows + 1) * cell);
                p.MoveTo(home, now + delay, dur, Easing.Kind.InQuad);
                p.Effect(ScaleFx.Land, now + delay + dur, Timings.Land);
                last = Mathf.Max(last, delay + dur + Timings.Land);
            }
            return last;
        }
    }
}

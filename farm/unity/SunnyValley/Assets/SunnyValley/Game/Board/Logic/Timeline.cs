using System;
using System.Collections.Generic;
using SunnyValley.Core.Match3;

namespace SunnyValley.Game.Match3View
{
    /// <summary>What the board view does at a moment of a timeline. Each cue names a piece and/or cells.</summary>
    public enum CueKind : byte
    {
        /// <summary>Moves counter shows <see cref="Cue.Value"/>.</summary>
        Moves,
        /// <summary>Piece slides From → To (the player's swap).</summary>
        SwapMove,
        /// <summary>Swap without effect: the pieces at From and To go towards each other and back (Value = RejectReason).</summary>
        Bounce,
        /// <summary>Combo: the piece slides From → To (the combo centre) and the two melt together.</summary>
        Gather,
        /// <summary>Piece is matched or blasted: squash, pop, sparkles in its colour.</summary>
        Pop,
        /// <summary>Piece flies From → To into the special being made there.</summary>
        Merge,
        /// <summary>Piece flies From → the basket at To.</summary>
        IntoBasket,
        /// <summary>A special piece fires: flash, the piece is gone (its effect is a separate cue).</summary>
        Fire,
        /// <summary>A new special piece appears at To (Value = SpecialKind).</summary>
        SpecialAppear,
        /// <summary>Piece turns into a special in place (Value = SpecialKind).</summary>
        Transform,
        /// <summary>Obstacle at To takes a hit (Event: ObstacleHitEvent): its layers change, debris flies.</summary>
        ObstacleHit,
        /// <summary>A goal counter moves (Event: GoalProgressEvent): a ghost flies from the cell to the goal icon.</summary>
        Goal,
        /// <summary>Pitchfork streak from From along a row (Value 0) or a column (Value 1).</summary>
        LineStreak,
        /// <summary>Bomb blast at From with radius Value: shockwave ring.</summary>
        Blast,
        /// <summary>A bee flies From → To on an arc (Value = carried SpecialKind).</summary>
        BeeFlight,
        /// <summary>The rainbow at From gathers light (Value = TileColor removed, -1 none).</summary>
        RainbowCharge,
        /// <summary>A light beam From → To.</summary>
        Beam,
        /// <summary>The board shakes a little (Value = strength 1..3).</summary>
        Shake,
        /// <summary>An in-level booster strikes (Event: BoosterEvent).</summary>
        Booster,
        /// <summary>One cell of falling: From → To (Value = FallKind).</summary>
        Fall,
        /// <summary>A new piece drops into its spawner To from above (Event: SpawnEvent).</summary>
        Spawn,
        /// <summary>The piece has landed: a small squash and bounce.</summary>
        Land,
        /// <summary>A chick at From hops off the board.</summary>
        ChickExit,
        /// <summary>Honey or weed grows From → To (Event: SpreadEvent).</summary>
        Spread,
        /// <summary>Reshuffle: the piece travels From → To on an arc (Value = colour after it).</summary>
        Shuffle,
        /// <summary>A spark flies from the moves counter to the cell To (bonus moves at the end).</summary>
        BonusSpark,
        /// <summary>The level is over (Value = GameStatus).</summary>
        End,
    }

    /// <summary>One timed action of a timeline.</summary>
    public struct Cue
    {
        public float Time;
        public float Duration;
        public CueKind Kind;
        public int PieceId;
        public Pos From;
        public Pos To;
        public int Value;
        public int Wave;
        public StepEvent Event;
        /// <summary>Insertion order: ties in time keep the engine's order.</summary>
        public int Seq;

        public float End => Time + Duration;

        /// <summary>The cue moves its piece (a piece never runs two of these at once).</summary>
        public bool IsMove => Kind == CueKind.SwapMove || Kind == CueKind.Gather || Kind == CueKind.Merge ||
                              Kind == CueKind.IntoBasket || Kind == CueKind.Fall || Kind == CueKind.Spawn ||
                              Kind == CueKind.Shuffle || Kind == CueKind.ChickExit;

        /// <summary>Decoration only: the board may carry on while it plays (goal ghosts, shakes, streaks).</summary>
        public bool IsCosmetic => Kind == CueKind.Goal || Kind == CueKind.Shake || Kind == CueKind.LineStreak ||
                                  Kind == CueKind.Blast || Kind == CueKind.Beam || Kind == CueKind.RainbowCharge ||
                                  Kind == CueKind.Land || Kind == CueKind.Moves || Kind == CueKind.BonusSpark;

        public override string ToString() =>
            Time.ToString("0.000") + "+" + Duration.ToString("0.000") + " " + Kind + (PieceId > 0 ? " #" + PieceId : "") +
            (From.IsNone ? "" : " " + From) + (To.IsNone ? "" : "->" + To) + (Value != 0 ? " v" + Value : "");
    }

    /// <summary>A step log turned into timed cues, sorted by time.</summary>
    public sealed class Timeline
    {
        public readonly List<Cue> Cues = new List<Cue>();
        /// <summary>Everything is over (goal ghosts landed too).</summary>
        public float Duration;
        /// <summary>The pieces are still: the player may move again.</summary>
        public float BoardTime;
        /// <summary>Moves the builder had to postpone because the piece was still busy (0 for a consistent log).</summary>
        public int Delayed;

        public void Clear()
        {
            Cues.Clear();
            Delayed = 0;
            Duration = 0;
            BoardTime = 0;
        }

        internal void Add(ref Cue c)
        {
            c.Seq = Cues.Count;
            Cues.Add(c);
        }

        static readonly Comparison<Cue> ByTime = (a, b) => a.Time != b.Time ? a.Time.CompareTo(b.Time) : a.Seq.CompareTo(b.Seq);

        internal void Finish()
        {
            Cues.Sort(ByTime);
            Duration = 0;
            float board = BoardTime;
            foreach (var c in Cues)
            {
                if (c.End > Duration) Duration = c.End;
                if (!c.IsCosmetic && c.End > board) board = c.End;
            }
            BoardTime = board;
            if (BoardTime > Duration) Duration = BoardTime;
        }
    }

    /// <summary>
    /// The pace of the board, in seconds. Snappy like the genre: a plain move resolves in under a second, a move with
    /// a couple of cascades in about 1.5 s.
    /// </summary>
    public static class Timings
    {
        public const float Swap = 0.16f;
        public const float Bounce = 0.3f;
        public const float Gather = 0.15f;
        public const float Pop = 0.22f;
        public const float Merge = 0.15f;
        public const float Appear = 0.24f;
        public const float BasketFly = 0.28f;
        public const float Fire = 0.12f;
        public const float Hit = 0.25f;
        public const float AdjacentDelay = 0.04f;
        public const float LineLead = 0.05f;
        public const float LineStep = 0.032f;
        public const float BombFuse = 0.1f;
        public const float BombStep = 0.05f;
        public const float BeeCross = 0.06f;
        public const float BeeTakeoff = 0.1f;
        public const float BeeFlightBase = 0.34f;
        public const float BeeFlightPerCell = 0.02f;
        public const float RainbowCharge = 0.22f;
        public const float RainbowStep = 0.03f;
        public const float RainbowSpan = 0.55f;
        public const float Beam = 0.12f;
        public const float ConvertPause = 0.2f;
        public const float ConvertFireStep = 0.045f;
        public const float NovaLead = 0.25f;
        public const float NovaStep = 0.05f;
        public const float ShovelLead = 0.28f;
        public const float SweepLead = 0.14f;
        public const float SweepStep = 0.035f;
        public const float PreBoosterStep = 0.16f;
        /// <summary>Pieces start to fall this long after the last hit (the pops are still fading).</summary>
        public const float RefillDelay = 0.1f;
        /// <summary>The first tick of a refill; later ticks get shorter (the pieces accelerate) down to TickMin.</summary>
        public const float Tick0 = 0.085f;
        public const float TickDecay = 0.86f;
        public const float TickMin = 0.045f;
        public const float Land = 0.16f;
        public const float WaveGap = 0.03f;
        public const float ChickExit = 0.4f;
        public const float Spread = 0.35f;
        public const float Shuffle = 0.55f;
        public const float GoalFly = 0.55f;
        public const float BonusSpacing = 0.11f;
        public const float BonusSpark = 0.3f;
        public const float BonusFireSpacing = 0.14f;

        public static float TickDuration(int tick)
        {
            float d = Tick0;
            for (int i = 0; i < tick && d > TickMin; i++) d *= TickDecay;
            return d < TickMin ? TickMin : d;
        }

        public static float BeeFlight(Pos from, Pos to)
        {
            float dx = to.X - from.X, dy = to.Y - from.Y;
            return BeeFlightBase + BeeFlightPerCell * (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}

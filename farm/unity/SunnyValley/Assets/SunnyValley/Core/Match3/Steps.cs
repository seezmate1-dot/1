using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace SunnyValley.Core.Match3
{
    /// <summary>
    /// The step log: everything a move (or booster, or reshuffle) did, in the exact order the engine did it.
    /// The view plays it back event by event.
    ///
    /// Timing model:
    ///   * <see cref="StepEvent.Wave"/> — cascade wave. Wave 0 is the player's swap/booster and what it set off;
    ///     every refill that produces new matches starts the next wave. Waves play strictly one after another.
    ///   * Within a wave the order is: Swap/Combo → Match (all groups) → the match's clears and obstacle hits →
    ///     SpecialCreated → Activations in firing order (generation = chain depth), each followed by its own
    ///     clears/hits → refill (Spawn/Fall/ChickRescued). Hits point at their cause via CauseId.
    ///   * <see cref="StepEvent.CauseId"/> links a hit to the match group (<see cref="MatchEvent.GroupId"/>) or the
    ///     activation (<see cref="ActivationEvent.ActivationId"/>) that caused it, so a rocket's hits can be
    ///     animated as the rocket passes. Ids are unique within one <see cref="MoveResult"/>.
    ///   * A refill is a run of Spawn, Fall and ChickRescued events (in that order in the list). Play it by tick:
    ///     falls are per piece, as one-cell segments with a tick number (all pieces move one cell per tick);
    ///     a spawn appears at its spawner at its tick (after that tick's moves); a rescue happens at its tick.
    ///     <see cref="StepLogMirror"/> is a reference implementation of playing the log on a board.
    /// </summary>
    public enum StepType
    {
        Swap,
        SwapRejected,
        Combo,
        Match,
        Activation,
        BeeFlight,
        Transform,
        PieceCleared,
        ObstacleHit,
        SpecialCreated,
        Fall,
        Spawn,
        ChickRescued,
        Spread,
        GoalProgress,
        Shuffle,
        Booster,
        MovesChanged,
        GameEnd,
    }

    public abstract class StepEvent
    {
        [JsonProperty(Order = -10)]
        [JsonConverter(typeof(StringEnumConverter))]
        public abstract StepType Type { get; }

        /// <summary>Cascade wave (0 = the player's action).</summary>
        [JsonProperty(Order = -9)] public int Wave;
        /// <summary>Id of the match group or activation that caused this, -1 for none.</summary>
        [JsonProperty(Order = -8)] public int CauseId = -1;
    }

    /// <summary>The player swapped A (dragged) into B. Pieces have exchanged cells.</summary>
    public sealed class SwapEvent : StepEvent
    {
        public override StepType Type => StepType.Swap;
        public Pos A, B;
        public int PieceA, PieceB;
    }

    /// <summary>Swap had no effect: animate there and back. No move was spent.</summary>
    public sealed class SwapRejectedEvent : StepEvent
    {
        public override StepType Type => StepType.SwapRejected;
        public Pos A, B;
        [JsonConverter(typeof(StringEnumConverter))] public RejectReason Reason;
    }

    public enum RejectReason { NotAdjacent, NotSwappable, Wall, NoMatch, GameOver }

    /// <summary>Two specials (or a rainbow and anything) were swapped together at <see cref="Center"/>.</summary>
    public sealed class ComboEvent : StepEvent
    {
        public override StepType Type => StepType.Combo;
        [JsonConverter(typeof(StringEnumConverter))] public ComboKind Combo;
        public Pos A, B, Center;
        public int ActivationId;
    }

    /// <summary>One connected match. All <see cref="Cells"/> get a Match hit; neighbours get an Adjacent hit.</summary>
    public sealed class MatchEvent : StepEvent
    {
        public override StepType Type => StepType.Match;
        public int GroupId;
        [JsonConverter(typeof(StringEnumConverter))] public TileColor Color;
        [JsonConverter(typeof(StringEnumConverter))] public MatchShape Shape;
        public List<Pos> Cells = new List<Pos>();
        /// <summary>Special made by this match (None for a plain 3) and the cell it appears in.</summary>
        [JsonConverter(typeof(StringEnumConverter))] public SpecialKind Creates;
        public Pos SpecialCell = Pos.None;
    }

    /// <summary>
    /// A special fires. <see cref="Cells"/> are all cells it hits, in the order a wave front reaches them
    /// (nearest first). Chained activations have a higher <see cref="Generation"/> and the parent as cause.
    /// </summary>
    public sealed class ActivationEvent : StepEvent
    {
        public override StepType Type => StepType.Activation;
        public int ActivationId;
        public int Generation;
        public int PieceId;
        [JsonConverter(typeof(StringEnumConverter))] public SpecialKind Special;
        [JsonConverter(typeof(StringEnumConverter))] public TileColor Color;
        public Pos Cell;
        /// <summary>For rainbows: the colour removed. For combos: what the combo does.</summary>
        [JsonConverter(typeof(StringEnumConverter))] public TileColor TargetColor = TileColor.None;
        [JsonConverter(typeof(StringEnumConverter))] public ComboKind Combo;
        public List<Pos> Cells = new List<Pos>();
    }

    /// <summary>A bee flies from its cell to its target (optionally carrying another special that fires there).</summary>
    public sealed class BeeFlightEvent : StepEvent
    {
        public override StepType Type => StepType.BeeFlight;
        public int ActivationId;
        public Pos From, To;
        [JsonConverter(typeof(StringEnumConverter))] public SpecialKind Carried;
    }

    /// <summary>A tile turned into a special in place (rainbow+special combo, pre-level boosters).</summary>
    public sealed class TransformEvent : StepEvent
    {
        public override StepType Type => StepType.Transform;
        public int PieceId;
        public Pos Cell;
        [JsonConverter(typeof(StringEnumConverter))] public SpecialKind Special;
        [JsonConverter(typeof(StringEnumConverter))] public TileColor Color;
    }

    /// <summary>A piece left the board (matched, blasted, eaten by a basket's goal, …).</summary>
    public sealed class PieceClearedEvent : StepEvent
    {
        public override StepType Type => StepType.PieceCleared;
        public int PieceId;
        public Pos Cell;
        [JsonConverter(typeof(StringEnumConverter))] public TileColor Color;
        [JsonConverter(typeof(StringEnumConverter))] public SpecialKind Special;
        [JsonConverter(typeof(StringEnumConverter))] public HitSource Source;
        /// <summary>Basket that took this tile (adjacent, same colour), or None.</summary>
        public Pos IntoBasket = Pos.None;
    }

    /// <summary>
    /// An obstacle took a hit. Layers: crates/stone/ice/chain/honey/weed/grass = layers left;
    /// bed = growth (0 again after a harvest, <see cref="Harvested"/> true); basket = tiles still wanted.
    /// </summary>
    public sealed class ObstacleHitEvent : StepEvent
    {
        public override StepType Type => StepType.ObstacleHit;
        public Pos Cell;
        [JsonConverter(typeof(StringEnumConverter))] public ObstacleKind Obstacle;
        public int LayersBefore, LayersAfter;
        public bool Removed;
        public bool Harvested;
        [JsonConverter(typeof(StringEnumConverter))] public HitSource Source;
    }

    /// <summary>A match produced a special piece (the matched tiles fly into <see cref="Cell"/>).</summary>
    public sealed class SpecialCreatedEvent : StepEvent
    {
        public override StepType Type => StepType.SpecialCreated;
        public int PieceId;
        public Pos Cell;
        [JsonConverter(typeof(StringEnumConverter))] public SpecialKind Special;
        [JsonConverter(typeof(StringEnumConverter))] public TileColor Color;
        public List<Pos> From = new List<Pos>();
    }

    public enum FallKind { Down, Diagonal, Portal }

    public struct FallSegment
    {
        public Pos From, To;
        public int Tick;
        [JsonConverter(typeof(StringEnumConverter))] public FallKind Kind;
    }

    /// <summary>All movement of one piece during one refill, as one-cell segments with ticks.</summary>
    public sealed class FallEvent : StepEvent
    {
        public override StepType Type => StepType.Fall;
        public int PieceId;
        public List<FallSegment> Segments = new List<FallSegment>();
        [JsonIgnore] public Pos From => Segments.Count == 0 ? Pos.None : Segments[0].From;
        [JsonIgnore] public Pos To => Segments.Count == 0 ? Pos.None : Segments[Segments.Count - 1].To;
    }

    /// <summary>A new piece appears at a spawner at <see cref="Tick"/> (falling in from above the cell).</summary>
    public sealed class SpawnEvent : StepEvent
    {
        public override StepType Type => StepType.Spawn;
        public int PieceId;
        public Pos Cell;
        public int Tick;
        [JsonConverter(typeof(StringEnumConverter))] public PieceKind Kind;
        [JsonConverter(typeof(StringEnumConverter))] public TileColor Color;
    }

    public sealed class ChickRescuedEvent : StepEvent
    {
        public override StepType Type => StepType.ChickRescued;
        public int PieceId;
        public Pos Cell;
        /// <summary>Tick of the refill at which the chick leaves (after it has landed on the exit).</summary>
        public int Tick;
    }

    /// <summary>Honey covered a neighbour's tile, or weed grew into a neighbour (the tile there is gone).</summary>
    public sealed class SpreadEvent : StepEvent
    {
        public override StepType Type => StepType.Spread;
        [JsonConverter(typeof(StringEnumConverter))] public ObstacleKind Obstacle;
        public Pos From, To;
        /// <summary>Weed: the tile it replaced (-1 for honey: the tile stays under it).</summary>
        public int ConsumedPieceId = -1;
    }

    /// <summary>A goal counter changed. <see cref="Cell"/> is where the thing came from (fly-to-goal animation).</summary>
    public sealed class GoalProgressEvent : StepEvent
    {
        public override StepType Type => StepType.GoalProgress;
        public int GoalIndex;
        [JsonConverter(typeof(StringEnumConverter))] public GoalType Goal;
        public int Delta, Current, Target;
        public Pos Cell;
    }

    public struct ShuffleMove
    {
        public int PieceId;
        public Pos From, To;
        /// <summary>Colour after the shuffle (differs from before only when the engine had to recolour).</summary>
        [JsonConverter(typeof(StringEnumConverter))] public TileColor Color;
    }

    /// <summary>Pieces changed places (no moves left on the board, or the Shuffle booster).</summary>
    public sealed class ShuffleEvent : StepEvent
    {
        public override StepType Type => StepType.Shuffle;
        public bool ByBooster;
        public List<ShuffleMove> Moves = new List<ShuffleMove>();
    }

    public sealed class BoosterEvent : StepEvent
    {
        public override StepType Type => StepType.Booster;
        [JsonConverter(typeof(StringEnumConverter))] public BoosterKind Booster;
        public Pos Target;
        public int ActivationId;
        public List<Pos> Cells = new List<Pos>();
    }

    public sealed class MovesChangedEvent : StepEvent
    {
        public override StepType Type => StepType.MovesChanged;
        public int MovesLeft;
        public int Delta;
    }

    public sealed class GameEndEvent : StepEvent
    {
        public override StepType Type => StepType.GameEnd;
        [JsonConverter(typeof(StringEnumConverter))] public GameStatus Status;
        public int Stars;
        public int MovesLeft;
    }

    /// <summary>What one player action did.</summary>
    public sealed class MoveResult
    {
        /// <summary>False for a rejected swap or an unusable booster: nothing changed, no move spent.</summary>
        public bool Accepted;
        public List<StepEvent> Events = new List<StepEvent>();
        public GameStatus Status;
        public int MovesLeft;
        /// <summary>Number of cascade waves (1 = no cascade after the action itself).</summary>
        public int Waves;

        public IEnumerable<T> OfType<T>() where T : StepEvent
        {
            foreach (var e in Events) if (e is T t) yield return t;
        }

        public string ToJson(bool indented = false) =>
            JsonConvert.SerializeObject(Events, indented ? Formatting.Indented : Formatting.None);
    }
}

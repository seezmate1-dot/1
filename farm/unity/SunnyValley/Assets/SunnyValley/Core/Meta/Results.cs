using System.Collections.Generic;

namespace SunnyValley.Core.Meta
{
    /// <summary>Why an action was refused. <see cref="Texts.Error"/> gives the Russian text for the UI.</summary>
    public enum MetaError
    {
        None,
        UnknownId,
        Locked,
        NotEnoughCoins,
        NotEnoughStars,
        NotEnoughCrystals,
        NotEnoughItems,
        NotEnoughTools,
        NoBooster,
        NoLives,
        BarnFull,
        AlreadyDone,
        PrerequisitesMissing,
        ZoneLocked,
        BadChoice,
        FarmClosed,
        Busy,
        NotReady,
        QueueFull,
        LimitReached,
        NotEnoughPopulation,
        NoLevelInProgress,
        LevelInProgress,
        WrongLevel,
        NothingToCollect,
        AlreadyClaimed,
        NeedsFeed,
        NotEnoughSpace,
        Empty,
    }

    /// <summary>Outcome of a player action: success or an error, plus what was granted.</summary>
    public class ActionResult
    {
        public MetaError Error;
        public bool Ok => Error == MetaError.None;
        public string ErrorText => Texts.Error(Error);
        /// <summary>What the player received (empty when nothing).</summary>
        public Reward Reward = new Reward();

        public static ActionResult Fail(MetaError e) => new ActionResult { Error = e };
        public static ActionResult Success(Reward r = null) => new ActionResult { Reward = r ?? new Reward() };
        public override string ToString() => Ok ? "OK " + Reward.Describe() : Error.ToString();
    }

    /// <summary>Result of finishing a match-3 level.</summary>
    public sealed class LevelOutcome : ActionResult
    {
        public bool Won;
        public int StarsGained;
        /// <summary>Lives left after the result (a loss takes one unless infinite lives are on).</summary>
        public int LivesLeft;
        /// <summary>Crops from the board that did not fit into the barn and were sold.</summary>
        public int SoldOverflowCoins;
    }

    /// <summary>Result of spending stars on a restoration task.</summary>
    public sealed class TaskOutcome : ActionResult
    {
        public TaskDef Task;
        /// <summary>The replica(s) to show after the build animation.</summary>
        public List<DialogueLine> Lines = new List<DialogueLine>();
        /// <summary>A multi-line scene to play after the task, or null.</summary>
        public SceneDef Scene;
        public ZoneDef ZoneCompleted;
        public bool ChapterCompleted;
    }

    /// <summary>Things that happened that the UI may want to announce (drained with <see cref="MetaGame.TakeEvents"/>).</summary>
    public enum MetaEventKind
    {
        LevelUp,
        ZoneCompleted,
        ChapterCompleted,
        FarmOpened,
        ToolFound,
        BoosterFound,
        OrderAppeared,
        TruckLeft,
        TruckReturned,
        DailyTaskDone,
        NewDay,
    }

    public sealed class MetaEvent
    {
        public MetaEventKind Kind;
        /// <summary>Level number, zone id, item id… depending on the kind.</summary>
        public string Id;
        public int Value;
        public Reward Reward;
        /// <summary>Russian line for a toast.</summary>
        public string Text;
        public override string ToString() => Kind + " " + Id + " " + Value + " " + Text;
    }
}

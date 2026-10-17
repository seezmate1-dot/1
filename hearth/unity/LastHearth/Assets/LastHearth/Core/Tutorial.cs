using System;

namespace LastHearth.Core
{
    /// <summary>The steps of the first session (MODES.md, section 10), in order.</summary>
    public enum TutorialStep
    {
        /// <summary>Mara meets the refugees inside the gates (a tap on «Далее»).</summary>
        Welcome,
        /// <summary>Collect what the coal mine dug up.</summary>
        Collect,
        /// <summary>Upgrade the Hearth: warmth is territory.</summary>
        Hearth,
        /// <summary>The refugees came with the new level: put one to work.</summary>
        Assign,
        /// <summary>Clear the rubble next to the rooms.</summary>
        Clear,
        /// <summary>Build a room (the barracks) on the dug-out cell.</summary>
        Build,
        /// <summary>Upgrade a room: the builders and their queue.</summary>
        Upgrade,
        /// <summary>Raise a hero's level with essence.</summary>
        HeroLevel,
        /// <summary>Train a dweller into a hero (the barracks).</summary>
        Promote,
        /// <summary>The first battle of the campaign (it opens at the hearth level of its first mission).</summary>
        Campaign,
        /// <summary>Mara tells about the evening Surge (a tap on «Вперёд!»).</summary>
        Surge,
        Done,
    }

    /// <summary>What happened in the game: the steps wait for these.</summary>
    public enum TutorialEvent
    {
        /// <summary>The player tapped «Далее» on a step that only talks.</summary>
        Acknowledged,
        Collected,
        HearthUpgradeStarted,
        DwellerAssigned,
        ObstacleClearing,
        RoomBuilt,
        RoomUpgradeStarted,
        MissionWon,
        HeroLevelUp,
        HeroPromoted,
    }

    /// <summary>The tutorial's save: where it is, what the player has already done.</summary>
    [Serializable]
    public sealed class TutorialState
    {
        /// <summary>The default is a finished tutorial: a save made before the tutorial existed loads without it.</summary>
        public TutorialStep Step = TutorialStep.Done;
        /// <summary>Events seen since the tutorial started (bits of TutorialEvent): a step already done is not asked for again.</summary>
        public int Seen;
        public bool Skipped;
        /// <summary>When the tutorial started and when it ended (0: not yet).</summary>
        public long StartedAt;
        public long FinishedAt;

        public bool Active => Step != TutorialStep.Done;
    }

    /// <summary>
    /// The scripted first twenty minutes (MODES.md, 10): one thing at a time, each step waits for the
    /// real action in the game (a room built, a battle won) and never blocks anything — the player may
    /// do the steps in any order, skip the tutorial, and it ends by itself once the hearth outgrows it.
    /// Pure state: the screen (GameRunner.Tutorial.cs) only shows the step and points at its button.
    /// </summary>
    public static class Tutorial
    {
        /// <summary>The tutorial ends by itself at this hearth level: the first session reaches level 5, the
        /// campaign opens at 7, and by level 9 (the first evening, MODES.md 9) the player knows the fortress.</summary>
        public const int EndsAtHearth = 9;

        public sealed class StepInfo
        {
            public TutorialStep Step;
            /// <summary>What finishes the step.</summary>
            public TutorialEvent Event;
            /// <summary>What Mara says: shorter than two lines (MODES.md, 10).</summary>
            public string Text = "";
        }

        public static readonly StepInfo[] Steps =
        {
            Info(TutorialStep.Welcome, TutorialEvent.Acknowledged, "Мы успели! Туман остался за воротами. Я Мара, страж очага. Обживёмся?"),
            Info(TutorialStep.Collect, TutorialEvent.Collected, "Шахта уже накопала уголь. Нажмите на облачко над залом или «Собрать»."),
            Info(TutorialStep.Hearth, TutorialEvent.HearthUpgradeStarted, "Тепло — это жизнь. Нажмите на очаг и улучшите его."),
            Info(TutorialStep.Assign, TutorialEvent.DwellerAssigned, "К теплу пришли беженцы. Откройте кухню или шахту и поставьте их на работу."),
            Info(TutorialStep.Clear, TutorialEvent.ObstacleClearing, "Рядом завал. Нажмите «Расчистить»: строитель освободит место."),
            Info(TutorialStep.Build, TutorialEvent.RoomBuilt, "Место свободно. Нажмите на клетку с плюсом и постройте казарму."),
            Info(TutorialStep.Upgrade, TutorialEvent.RoomUpgradeStarted, "Улучшите любой зал. Последние 5 минут стройки — бесплатно."),
            Info(TutorialStep.HeroLevel, TutorialEvent.HeroLevelUp, "Эссенция делает героев сильнее. Откройте «Герои» и поднимите уровень."),
            Info(TutorialStep.Promote, TutorialEvent.HeroPromoted, "Героями становятся жители. В окне «Жители» нажмите «Сделать героем»."),
            Info(TutorialStep.Campaign, TutorialEvent.MissionWon, "Очаг открыл поход! Откройте карту и выиграйте первый бой."),
            Info(TutorialStep.Surge, TutorialEvent.Acknowledged, "В 19:00 из тумана лезут твари — это Наплыв. Возвращайтесь вечером!"),
        };

        static StepInfo Info(TutorialStep step, TutorialEvent ev, string text) => new StepInfo { Step = step, Event = ev, Text = text };

        public static StepInfo Of(TutorialStep step) => Array.Find(Steps, s => s.Step == step);

        /// <summary>Steps that only talk: a tap on the bubble's button moves them on.</summary>
        public static bool Talks(TutorialStep step) => Of(step)?.Event == TutorialEvent.Acknowledged;

        /// <summary>Starts the tutorial from its first step (a new game, or «начать заново» in the admin menu).</summary>
        public static void Start(TutorialState t, long now)
        {
            t.Step = TutorialStep.Welcome;
            t.Seen = 0;
            t.Skipped = false;
            t.StartedAt = now;
            t.FinishedAt = 0;
        }

        /// <summary>
        /// Something happened in the game. Remembered while the tutorial runs, so a step the player
        /// already did is passed at once; returns true when the step changed.
        /// </summary>
        public static bool Notify(TutorialState t, TutorialEvent e, long now)
        {
            if (!t.Active)
                return false;
            if (e == TutorialEvent.Acknowledged)
            {
                if (!Talks(t.Step))
                    return false;
                return Advance(t, now);
            }
            t.Seen |= 1 << (int)e;
            if (Of(t.Step).Event != e)
                return false;
            return Advance(t, now);
        }

        /// <summary>To the next step, past the ones whose event was already seen.</summary>
        static bool Advance(TutorialState t, long now)
        {
            var step = t.Step + 1;
            while (step < TutorialStep.Done && !Talks(step) && (t.Seen & (1 << (int)Of(step).Event)) != 0)
                step++;
            t.Step = step;
            if (step == TutorialStep.Done)
                t.FinishedAt = now;
            return true;
        }

        /// <summary>«Пропустить обучение»: everything opens as without it.</summary>
        public static void Skip(TutorialState t, long now)
        {
            if (!t.Active)
                return;
            t.Skipped = true;
            Finish(t, now);
        }

        public static void Finish(TutorialState t, long now)
        {
            if (!t.Active)
                return;
            t.Step = TutorialStep.Done;
            t.FinishedAt = now;
        }

        /// <summary>The admin menu: to any step (the events seen are kept, the step itself waits for its action).</summary>
        public static void JumpTo(TutorialState t, TutorialStep step, long now)
        {
            if (step >= TutorialStep.Done)
            {
                Finish(t, now);
                return;
            }
            if (!t.Active)
            {
                t.StartedAt = now;
                t.FinishedAt = 0;
                t.Skipped = false;
            }
            t.Step = step;
            t.Seen &= ~(1 << (int)Of(step).Event);
        }

        /// <summary>Called as time passes: a fortress past the first session needs no tutorial.</summary>
        public static void Settle(TutorialState t, int hearthLevel, long now)
        {
            if (t.Active && hearthLevel >= EndsAtHearth)
                Finish(t, now);
        }

        /// <summary>How far: the step's number from 1 and the number of steps.</summary>
        public static (int index, int count) Progress(TutorialState t) => (Math.Min((int)t.Step, Steps.Length) + 1, Steps.Length);
    }

    public sealed partial class GameSession
    {
        /// <summary>Raised when the tutorial moves to another step (or ends).</summary>
        public event Action<TutorialStep> TutorialChanged;

        public TutorialState TutorialState => State.Tutorial ?? (State.Tutorial = new TutorialState());

        /// <summary>A real game event for the tutorial (one line in the command that did it).</summary>
        void Tutor(TutorialEvent e)
        {
            if (Tutorial.Notify(TutorialState, e, State.Time))
                TutorialChanged?.Invoke(TutorialState.Step);
        }

        /// <summary>The player tapped «Далее» on a step that only talks.</summary>
        public void TutorialNext(long now)
        {
            if (Tutorial.Notify(TutorialState, TutorialEvent.Acknowledged, now))
                TutorialChanged?.Invoke(TutorialState.Step);
        }

        public void SkipTutorial(long now)
        {
            bool was = TutorialState.Active;
            Tutorial.Skip(TutorialState, now);
            if (was)
                TutorialChanged?.Invoke(TutorialStep.Done);
        }

        public void RestartTutorial(long now)
        {
            Tutorial.Start(TutorialState, now);
            TutorialChanged?.Invoke(TutorialState.Step);
        }

        public void JumpTutorial(TutorialStep step, long now)
        {
            Tutorial.JumpTo(TutorialState, step, now);
            TutorialChanged?.Invoke(TutorialState.Step);
        }

        /// <summary>
        /// The current step cannot be done yet (no idle dweller, no free cell, the campaign still closed,
        /// heroes at their cap): the guide keeps quiet until it can, nothing waits for it.
        /// </summary>
        public bool TutorialWaits()
        {
            var st = State;
            switch (TutorialState.Step)
            {
                case TutorialStep.Assign:
                    return !st.Dwellers.Exists(d => d.RoomId < 0 && d.RouteId == 0 && !d.IsHero);
                case TutorialStep.Clear:
                    return !AnyCell(CellState.Obstacle);
                case TutorialStep.Build:
                    return !AnyCell(CellState.Free);
                case TutorialStep.Upgrade:
                    return !st.Rooms.Exists(r => r.Level >= 1 && r.Level < st.HearthLevel && !st.Queue.IsQueued(r.Id));
                case TutorialStep.HeroLevel:
                    return !st.Dwellers.Exists(d => d.IsHero && d.HeroLevel < Formulas.HeroLevelCap(Balance, st.HearthLevel));
                case TutorialStep.Promote:
                    return MaxRoomLevel(RoomType.Barracks) < 1 || !st.Dwellers.Exists(d => !d.IsHero && d.RouteId == 0);
                case TutorialStep.Campaign:
                    return Squad().Count == 0 || st.PendingMission >= 0 || MissionsCleared >= Balance.Campaign.Missions.Count ||
                           Mission(MissionsCleared).Rank > st.HearthLevel;
                default:
                    return !TutorialState.Active;
            }
        }

        bool AnyCell(CellState state)
        {
            var g = Balance.Grid;
            for (int f = 0; f < g.Floors; f++)
            {
                for (int c = 0; c < g.Cells; c++)
                {
                    if (CellStateAt(f, c) == state)
                        return true;
                }
            }
            return false;
        }

        void SettleTutorial()
        {
            bool was = TutorialState.Active;
            Tutorial.Settle(TutorialState, State.HearthLevel, State.Time);
            if (was && !TutorialState.Active)
                TutorialChanged?.Invoke(TutorialStep.Done);
        }
    }
}

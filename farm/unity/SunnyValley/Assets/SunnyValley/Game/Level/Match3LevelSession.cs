using System;
using System.Collections.Generic;
using SunnyValley.Core.Match3;
using SunnyValley.Core.Meta;
using SunnyValley.Game.Match3View;
using SunnyValley.Layout;
using UnityEngine;

namespace SunnyValley.Game.Level
{
    /// <summary>
    /// A real level: Core/Match3's Match3Game over Resources/Levels/level_NNN.json, with the pre-level boosters the
    /// player picked, the in-level boosters, «+5 ходов» and the result for the meta (Match3MetaBridge).
    /// </summary>
    public sealed class Match3LevelSession : ILevelSession, ILevelResultSource
    {
        readonly List<Goal> goals = new List<Goal>();

        public Match3Game Game { get; }
        public int Level { get; }
        public string Name => Game.Level.Name ?? "";
        public int Cols => Game.Topo.Width;
        public int Rows => Game.Topo.Height;
        public int Moves => Game.MovesLeft;
        public IReadOnlyList<StepEvent> InitialEvents => Game.InitialEvents;

        public IReadOnlyList<Goal> Goals
        {
            get
            {
                for (int i = 0; i < Game.Goals.Count; i++)
                    goals[i].Count = Game.Goals[i].Remaining;
                return goals;
            }
        }

        public bool Won => Game.Status == GameStatus.Won;
        public bool Lost => Game.Status == GameStatus.Lost;
        public int StarsEarned => Won ? Game.Stars : 0;
        public LevelResult Result => Match3MetaBridge.ToLevelResult(Game);

        /// <summary>For GameApp.ResultOf → MetaGame.FinishLevel: the board's own report (moves left, stars, crops).</summary>
        public LevelResult ToLevelResult() => Result;

        /// <summary>
        /// Starts level <paramref name="level"/> (any number: after the last designed level they repeat).
        /// <paramref name="preBoosters"/> are flags for Вилы, Бомба, Радуга (the popup's order); <paramref name="seed"/>
        /// drives this attempt's spawns (null = a fresh one each time).
        /// </summary>
        public Match3LevelSession(int level, bool[] preBoosters = null, long? seed = null)
            : this(LevelLibrary.Load(level), level, ToBoosters(preBoosters), seed)
        {
        }

        public Match3LevelSession(LevelData data, int level, IList<BoosterKind> preBoosters, long? seed)
        {
            BoardSprites.Warm(); // goal icons of obstacles (the painted grass among them) before any popup shows them
            Level = level;
            Game = Match3Game.Create(data, seed ?? DateTime.UtcNow.Ticks ^ level * 7919L, preBoosters);
            foreach (var g in Game.Goals)
                goals.Add(new Goal(GoalIcons.For(g), g.Remaining));
        }

        static List<BoosterKind> ToBoosters(bool[] picked)
        {
            var list = new List<BoosterKind>();
            if (picked == null)
                return list;
            for (int i = 0; i < picked.Length && i < 3; i++)
            {
                if (picked[i])
                    list.Add((BoosterKind)((int)BoosterKind.PreLine + i));
            }
            return list;
        }

        /// <summary>HUD order 0..3 → Лопата, Грабли, Лейка, Перемешать.</summary>
        public static BoosterKind InLevelBooster(int hudIndex) => (BoosterKind)((int)BoosterKind.Shovel + Mathf.Clamp(hudIndex, 0, 3));

        public MoveResult Swap(Pos a, Pos b) => Game.Swap(a, b);

        public MoveResult UseBooster(int booster, Pos target) => Game.UseBooster(InLevelBooster(booster), target);

        public MoveResult AddMoves(int n) => Game.ContinueWithExtraMoves(n);
    }

    /// <summary>The designed levels in Resources/Levels (1–200); only beyond the last one do the later levels (21…) come round again.</summary>
    public static class LevelLibrary
    {
        /// <summary>The first level that is repeated after the last designed one (the tutorials are not).</summary>
        public const int RepeatFrom = 21;

        static int count = -1;

        /// <summary>How many levels there are (level_001 … level_NNN without gaps).</summary>
        public static int Count
        {
            get
            {
                if (count < 0)
                {
                    count = 0;
                    while (Resources.Load<TextAsset>(Path(count + 1)) != null)
                        count++;
                }
                return count;
            }
        }

        static string Path(int n) => "Levels/level_" + n.ToString("000");

        /// <summary>Which designed level plays as level <paramref name="level"/>.</summary>
        public static int Source(int level, int designed)
        {
            if (designed <= 0)
                return 1;
            if (level <= designed)
                return Math.Max(1, level);
            int from = Math.Min(RepeatFrom, designed);
            return from + (level - designed - 1) % (designed - from + 1);
        }

        public static LevelData Load(int level)
        {
            int source = Source(level, Count);
            var text = Resources.Load<TextAsset>(Path(source));
            if (text == null)
                throw new InvalidOperationException("Нет уровней в Resources/Levels");
            var data = LevelLoader.FromJson(text.text);
            data.Id = level;
            return data;
        }
    }
}

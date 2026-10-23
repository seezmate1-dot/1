using System.Collections.Generic;
using SunnyValley.Core.Match3;
using SunnyValley.Core.Meta;
using SunnyValley.Layout;

namespace SunnyValley.Game.Level
{
    /// <summary>
    /// What the level screen needs from a level being played: the match-3 game itself (the board view draws its
    /// board and animates the step log of every action), the numbers for the HUD and the popups, and the result
    /// for the meta. <see cref="Match3LevelSession"/> plays a real level from Resources/Levels.
    /// </summary>
    public interface ILevelSession
    {
        int Level { get; }
        string Name { get; }
        /// <summary>Board size (the layout fits the board rectangle to it).</summary>
        int Cols { get; }
        int Rows { get; }
        /// <summary>Moves left.</summary>
        int Moves { get; }
        /// <summary>Goals as the HUD and the popups show them: icon and how many are still needed.</summary>
        IReadOnlyList<Goal> Goals { get; }

        /// <summary>The engine: board, goals, valid moves, hints.</summary>
        Match3Game Game { get; }

        /// <summary>Pre-level boosters placed at the start (their step log, played when the board appears).</summary>
        IReadOnlyList<StepEvent> InitialEvents { get; }

        /// <summary>The player drags the piece at a onto its neighbour b.</summary>
        MoveResult Swap(Pos a, Pos b);

        /// <summary>An in-level booster in HUD order: 0 Лопата (cell), 1 Грабли (row), 2 Лейка (column), 3 Перемешать.</summary>
        MoveResult UseBooster(int booster, Pos target);

        /// <summary>«+5 ходов» after running out (may reshuffle: animate the result).</summary>
        MoveResult AddMoves(int n);

        bool Won { get; }
        bool Lost { get; }
        int StarsEarned { get; }

        /// <summary>The finished attempt for the meta (Match3MetaBridge).</summary>
        LevelResult Result { get; }
    }
}

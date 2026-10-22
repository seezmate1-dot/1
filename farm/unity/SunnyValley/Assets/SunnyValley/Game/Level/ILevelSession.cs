using System.Collections.Generic;
using SunnyValley.Layout;

namespace SunnyValley.Game.Level
{
    /// <summary>
    /// What the level screen needs from a level being played. The hook for Core: when Core/Match3 lands, an adapter
    /// over its board implements this (swaps instead of taps, cascades, specials) and the screen keeps working.
    /// Until then DemoLevelSession plays a simple «tap a group of two or more» placeholder.
    /// </summary>
    public interface ILevelSession
    {
        int Level { get; }
        int Cols { get; }
        int Rows { get; }
        int Moves { get; }
        IReadOnlyList<Goal> Goals { get; }

        /// <summary>The piece in a cell: 0..5 (Icons.Pieces), or -1 for none. Row 0 is the bottom.</summary>
        int PieceAt(int col, int row);

        /// <summary>A tap on a cell. Returns the cells that changed (to animate), empty when nothing happened.</summary>
        IReadOnlyList<(int col, int row)> Tap(int col, int row);

        /// <summary>An in-level booster (0 Лопата, 1 Грабли, 2 Лейка, 3 Перемешать) aimed at a cell.</summary>
        IReadOnlyList<(int col, int row)> UseBooster(int booster, int col, int row);

        void AddMoves(int n);

        bool Won { get; }
        bool Lost { get; }
        int StarsEarned { get; }
    }
}

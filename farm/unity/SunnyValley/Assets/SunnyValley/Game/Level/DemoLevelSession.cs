using System;
using System.Collections.Generic;
using SunnyValley.Layout;

namespace SunnyValley.Game.Level
{
    /// <summary>
    /// A stand-in level until Core/Match3 is in: tapping a group of two or more touching pieces of one kind clears it,
    /// the pieces above fall and new ones drop in; every tap is a move; goals count the cleared pieces of their kind.
    /// Deterministic by seed, like Core will be.
    /// </summary>
    public sealed class DemoLevelSession : ILevelSession
    {
        readonly int[,] board;
        readonly Random rng;
        readonly int kinds;
        readonly List<Goal> goals;
        readonly int startMoves;

        public int Level { get; }
        public int Cols { get; }
        public int Rows { get; }
        public int Moves { get; private set; }
        public IReadOnlyList<Goal> Goals => goals;

        public DemoLevelSession(int level, int cols = 9, int rows = 9)
        {
            Level = level;
            Cols = cols;
            Rows = rows;
            rng = new Random(level * 7919);
            kinds = level < 5 ? 5 : 6;
            startMoves = Moves = 22 + level % 5 * 2;
            goals = new List<Goal> { new Goal(Icons.Pieces[level % kinds], 14 + level % 7 * 2) };
            if (level >= 3)
                goals.Add(new Goal(Icons.Pieces[(level + 2) % kinds], 10 + level % 5 * 2));
            board = new int[cols, rows];
            for (int c = 0; c < cols; c++)
            for (int r = 0; r < rows; r++)
                board[c, r] = rng.Next(kinds);
        }

        public int PieceAt(int col, int row) => col >= 0 && row >= 0 && col < Cols && row < Rows ? board[col, row] : -1;

        public bool Won => goals.TrueForAll(g => g.Count <= 0);
        public bool Lost => !Won && Moves <= 0;

        /// <summary>Three stars for finishing with a third of the moves left, two with any, one at the last move.</summary>
        public int StarsEarned => !Won ? 0 : Moves * 3 >= startMoves ? 3 : Moves > 0 ? 2 : 1;

        public void AddMoves(int n) => Moves += n;

        public IReadOnlyList<(int col, int row)> Tap(int col, int row)
        {
            if (Won || Lost || PieceAt(col, row) < 0)
                return Array.Empty<(int, int)>();
            var group = Group(col, row);
            if (group.Count < 2)
                return Array.Empty<(int, int)>();
            Moves--;
            return Clear(group);
        }

        public IReadOnlyList<(int col, int row)> UseBooster(int booster, int col, int row)
        {
            var cells = new List<(int, int)>();
            switch (booster)
            {
                case 0: // Лопата: one cell
                    cells.Add((col, row));
                    break;
                case 1: // Грабли: the row
                    for (int c = 0; c < Cols; c++)
                        cells.Add((c, row));
                    break;
                case 2: // Лейка: the column
                    for (int r = 0; r < Rows; r++)
                        cells.Add((col, r));
                    break;
                default: // Перемешать
                    for (int c = 0; c < Cols; c++)
                    for (int r = 0; r < Rows; r++)
                    {
                        int c2 = rng.Next(Cols), r2 = rng.Next(Rows);
                        (board[c, r], board[c2, r2]) = (board[c2, r2], board[c, r]);
                        cells.Add((c, r));
                    }
                    return cells;
            }
            return Clear(cells);
        }

        List<(int, int)> Group(int col, int row)
        {
            int kind = board[col, row];
            var seen = new bool[Cols, Rows];
            var stack = new Stack<(int, int)>();
            var group = new List<(int, int)>();
            stack.Push((col, row));
            seen[col, row] = true;
            while (stack.Count > 0)
            {
                var (c, r) = stack.Pop();
                group.Add((c, r));
                foreach (var (dc, dr) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nc = c + dc, nr = r + dr;
                    if (PieceAt(nc, nr) == kind && !seen[nc, nr])
                    {
                        seen[nc, nr] = true;
                        stack.Push((nc, nr));
                    }
                }
            }
            return group;
        }

        /// <summary>Clears the cells, counts goals, lets the columns fall and fills the top. Returns every changed cell.</summary>
        List<(int col, int row)> Clear(List<(int, int)> cells)
        {
            var changed = new List<(int, int)>();
            var columns = new HashSet<int>();
            foreach (var (c, r) in cells)
            {
                if (PieceAt(c, r) < 0)
                    continue;
                string icon = Icons.Pieces[board[c, r]];
                foreach (var g in goals)
                {
                    if (g.Icon == icon && g.Count > 0)
                        g.Count--;
                }
                board[c, r] = -1;
                columns.Add(c);
            }
            foreach (int c in columns)
            {
                int write = 0;
                for (int r = 0; r < Rows; r++)
                {
                    if (board[c, r] >= 0)
                        board[c, write++] = board[c, r];
                }
                for (int r = write; r < Rows; r++)
                    board[c, r] = rng.Next(kinds);
                for (int r = 0; r < Rows; r++)
                    changed.Add((c, r));
            }
            return changed;
        }
    }
}

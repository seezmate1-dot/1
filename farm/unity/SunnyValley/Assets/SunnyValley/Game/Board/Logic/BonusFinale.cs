using System;
using System.Collections.Generic;
using SunnyValley.Core.Match3;

namespace SunnyValley.Game.Match3View
{
    /// <summary>
    /// «Бонус-ходы» after a win, as in Royal Match: every move left becomes a spark that flies from the moves counter
    /// to a tile and turns it into a pitchfork; then they fire one after another, each streak popping the tiles in
    /// its line. Decoration only — the engine has already finished the level (the coins for the moves left come
    /// from Match3MetaBridge) — so it plays on a copy of the board's pieces and never changes the game.
    /// </summary>
    public static class BonusFinale
    {
        /// <summary>At most this many sparks (more moves left still count, the counter runs down faster).</summary>
        public const int MaxSparks = 15;

        public static Timeline Build(Board board, int movesLeft, ulong seed, Timeline into = null)
        {
            var tl = into ?? new Timeline();
            tl.Clear();
            if (movesLeft <= 0) return tl;
            var topo = board.Topo;
            var rng = new Rng(seed);

            var candidates = new List<int>();
            for (int i = 0; i < topo.Count; i++)
                if (topo.Exists[i] && board.Pieces[i].IsPlainTile && board.Overlay[i] == ObstacleKind.None)
                    candidates.Add(i);
            rng.Shuffle(candidates);
            int n = Math.Min(Math.Min(movesLeft, MaxSparks), candidates.Count);

            var picked = new List<int>(n);
            var kinds = new List<SpecialKind>(n);
            for (int k = 0; k < n; k++)
            {
                picked.Add(candidates[k]);
                kinds.Add(rng.Next(2) == 0 ? SpecialKind.LineH : SpecialKind.LineV);
            }
            // fire in reading order of the sparks' landing, so the streaks sweep the board
            var gone = new bool[topo.Count];
            var isBonus = new bool[topo.Count];
            foreach (int i in picked) isBonus[i] = true;

            int seq = 0;
            for (int k = 0; k < n; k++)
            {
                float t = k * Timings.BonusSpacing;
                int left = k == n - 1 ? 0 : movesLeft - (int)Math.Round((k + 1) * (double)movesLeft / n);
                Add(tl, ref seq, new Cue { Kind = CueKind.Moves, Time = t, Value = Math.Max(0, left), From = Pos.None, To = Pos.None, PieceId = -1 });
                var cell = topo.PosOf(picked[k]);
                Add(tl, ref seq, new Cue { Kind = CueKind.BonusSpark, Time = t, Duration = Timings.BonusSpark, From = Pos.None, To = cell, PieceId = -1 });
                Add(tl, ref seq, new Cue
                {
                    Kind = CueKind.Transform, Time = t + Timings.BonusSpark, Duration = Timings.Appear, To = cell, From = Pos.None,
                    PieceId = board.Pieces[picked[k]].Id, Value = (int)kinds[k],
                });
            }
            float fire0 = (n - 1) * Timings.BonusSpacing + Timings.BonusSpark + Timings.Appear + 0.15f;
            for (int k = 0; k < n; k++)
            {
                float t = fire0 + k * Timings.BonusFireSpacing;
                int i = picked[k];
                var cell = topo.PosOf(i);
                gone[i] = true;
                Add(tl, ref seq, new Cue { Kind = CueKind.Fire, Time = t, Duration = Timings.Fire, From = cell, To = Pos.None, PieceId = board.Pieces[i].Id, Value = (int)board.Pieces[i].Color });
                int vertical = kinds[k] == SpecialKind.LineV ? 1 : 0;
                Add(tl, ref seq, new Cue { Kind = CueKind.LineStreak, Time = t, Duration = 0.4f, From = cell, To = Pos.None, PieceId = -1, Value = vertical });
                int len = vertical == 1 ? topo.Height : topo.Width;
                for (int d = 0; d < len; d++)
                {
                    int x = vertical == 1 ? cell.X : d, y = vertical == 1 ? d : cell.Y;
                    if (!topo.IsCell(x, y)) continue;
                    int j = topo.Index(x, y);
                    if (gone[j] || isBonus[j] || board.Pieces[j].IsEmpty || !board.Pieces[j].IsTile || board.Overlay[j] != ObstacleKind.None) continue;
                    gone[j] = true;
                    var at = new Pos(x, y);
                    float hit = t + Timings.LineLead + (Math.Abs(x - cell.X) + Math.Abs(y - cell.Y)) * Timings.LineStep;
                    Add(tl, ref seq, new Cue { Kind = CueKind.Pop, Time = hit, Duration = Timings.Pop, From = at, To = Pos.None, PieceId = board.Pieces[j].Id, Value = (int)board.Pieces[j].Color });
                }
            }
            tl.Finish();
            return tl;
        }

        static void Add(Timeline tl, ref int seq, Cue c)
        {
            tl.Add(ref c);
            seq++;
        }
    }
}

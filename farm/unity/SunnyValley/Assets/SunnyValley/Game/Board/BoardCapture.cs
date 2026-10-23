using System.Collections.Generic;
using SunnyValley.Core.Match3;
using SunnyValley.Game.Level;
using SunnyValley.Game.UI;
using SunnyValley.Layout;
using UnityEngine;

namespace SunnyValley.Game.Match3View
{
    /// <summary>
    /// The level screen with a real level in the middle of a game, for the editor's screenshots (Editor/Screenshots,
    /// CI): the hint bot plays some moves, then one more move is caught mid-animation (pops, sparkles, streaks,
    /// pieces falling). No Update runs in the editor: the board clock is advanced by hand.
    /// </summary>
    public static class BoardCapture
    {
        /// <summary>Builds the level screen under <paramref name="parent"/>: level <paramref name="level"/> after
        /// <paramref name="moves"/> moves, the next move frozen <paramref name="into"/> seconds in.</summary>
        public static UiView LevelMidGame(RectTransform parent, CanvasFit fit, int level, int moves, float into, long seed = 20261022)
        {
            BoardSprites.Warm();
            var session = new Match3LevelSession(level, null, seed);
            var g = session.Game;
            for (int i = 0; i < moves && g.Status == GameStatus.Playing; i++)
            {
                var hint = Hints.Find(g, (ulong)(i + 1));
                if (hint == null)
                    break;
                g.Swap(hint.Value.A, hint.Value.B);
            }
            var goals = new List<Goal>(session.Goals);
            var hud = new LevelHud
            {
                Level = level, Moves = session.Moves, Cols = session.Cols, Rows = session.Rows,
                Goals = goals.ConvertAll(x => new Goal(x.Icon, x.Count)), Boosters = new[] { 3, 2, 2, 1 },
            };
            var view = UiView.Build(Screens.Level(fit, hud), parent);
            var board = view.Rect("level.board").GetComponent<BoardView>();
            board.SetFlyLayer(view.Root);
            board.GoalTarget = i => view.Rect("level.goal" + i + ".icon") is RectTransform r ? r.position : board.transform.position;
            board.MovesTarget = () => view.Rect("level.moves.value").position;
            board.HintDelay = float.MaxValue;
            board.Show(g);
            if (into > 0 && g.Status == GameStatus.Playing)
            {
                var hint = Hints.Find(g, 99);
                if (hint != null)
                {
                    var r = g.Swap(hint.Value.A, hint.Value.B);
                    board.Play(r.Events, null);
                    const float step = 1f / 60;
                    for (float t = 0; t < into; t += step)
                        board.Advance(step);
                }
            }
            return view;
        }

        /// <summary>Puts a real level's pieces on a layout's board (the catalog's level screen and its popups).</summary>
        public static void Fill(UiView view, int level = 1)
        {
            var board = view.Rect("level.board")?.GetComponent<BoardView>();
            if (board == null)
                return;
            BoardSprites.Warm();
            board.HintDelay = float.MaxValue;
            board.Show(new Match3LevelSession(level, null, 1).Game);
        }
    }
}

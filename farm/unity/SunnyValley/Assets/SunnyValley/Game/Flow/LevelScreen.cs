using System.Collections.Generic;
using System.Linq;
using SunnyValley.Game.Level;
using SunnyValley.Game.UI;
using SunnyValley.Layout;
using UnityEngine;

namespace SunnyValley.Game.Flow
{
    /// <summary>
    /// The level: goals and moves on top, the board in the middle (as wide as the screen allows), in-level boosters
    /// at the bottom. Plays an ILevelSession: today the demo, later Core/Match3 through an adapter.
    /// A booster is armed by a tap (it lifts) and spent on the next tap on the board; «Перемешать» works at once.
    /// </summary>
    public sealed class LevelScreen : GameScreen
    {
        public readonly ILevelSession Session;
        BoardView board;
        int armed = -1;
        bool finished;
        float endDelay = -1;

        public LevelScreen(GameApp app, ILevelSession session) : base(app) => Session = session;

        LevelHud Hud() => new LevelHud
        {
            Level = Session.Level, Moves = Session.Moves, Cols = Session.Cols, Rows = Session.Rows,
            Goals = Session.Goals.Select(g => new Goal(g.Icon, g.Count)).ToList(), Boosters = App.State.boosters,
        };

        protected override UiLayout Layout(CanvasFit fit) => Screens.Level(fit, Hud());

        protected override void Bind()
        {
            board = View.Rect("level.board").GetComponent<BoardView>();
            board.Show(Session.PieceAt);
            board.Tapped += OnBoardTap;
            View.OnClick("level.pause", () => new SettingsPopup(App, true).Open());
            for (int i = 0; i < Screens.BoosterIcons.Length; i++)
            {
                int b = i;
                View.OnClick("level.booster" + i, () => OnBooster(b));
            }
            armed = -1;
        }

        void OnBooster(int b)
        {
            if (finished)
                return;
            if (App.State.boosters[b] <= 0)
            {
                Ui.Toast($"«{Screens.BoosterNames[b]}» закончились — их дают задания и события");
                return;
            }
            if (b == 3)
            {
                Spend(b);
                board.Refresh(Session.UseBooster(b, 0, 0));
                return;
            }
            Arm(armed == b ? -1 : b);
            if (armed >= 0)
                Ui.Toast(b == 0 ? "Выберите клетку" : b == 1 ? "Выберите строку" : "Выберите столбец");
        }

        void Arm(int b)
        {
            for (int i = 0; i < Screens.BoosterIcons.Length; i++)
            {
                var disc = View.Rect("level.booster" + i + ".disc");
                if (disc != null)
                    Tweener.Run(disc, "lift", 0.18f, Easing.Kind.OutBack,
                        k => disc.localScale = Vector3.one * (i == b ? Easing.Lerp(1, 1.15f, k) : 1));
            }
            armed = b;
        }

        void Spend(int b)
        {
            App.State.boosters[b]--;
            App.State.Save();
            View.SetBadge("level.booster" + b + ".badge", App.State.boosters[b]);
        }

        void OnBoardTap(int col, int row)
        {
            if (finished)
                return;
            IReadOnlyList<(int, int)> changed;
            if (armed >= 0)
            {
                int b = armed;
                Arm(-1);
                Spend(b);
                changed = Session.UseBooster(b, col, row);
            }
            else
            {
                changed = Session.Tap(col, row);
            }
            if (changed.Count == 0)
                return;
            board.Refresh(changed);
            UpdateHud();
            if (Session.Won || Session.Lost)
            {
                finished = true;
                endDelay = 0.6f; // let the last pieces land
            }
        }

        void UpdateHud()
        {
            View.SetText("level.moves.value", Session.Moves.ToString());
            var moves = View.Rect("level.moves.value");
            if (moves != null)
                Tweener.Punch(moves, 0.15f, 0.25f);
            for (int i = 0; i < Session.Goals.Count; i++)
            {
                string id = "level.goal" + i + ".count";
                string value = System.Math.Max(0, Session.Goals[i].Count).ToString();
                if (View.Node(id)?.Text?.Value != value)
                {
                    View.SetText(id, value);
                    var icon = View.Rect("level.goal" + i + ".icon");
                    if (icon != null)
                        Tweener.Punch(icon, 0.3f, 0.3f);
                }
            }
        }

        public override void Tick(float dt)
        {
            if (endDelay < 0)
                return;
            endDelay -= dt;
            if (endDelay >= 0)
                return;
            endDelay = -1;
            if (Session.Won)
                App.LevelWon(this);
            else
                new LosePopup(App, this).Open();
        }

        /// <summary>After «+5 ходов»: back to playing.</summary>
        public void Continue()
        {
            Session.AddMoves(5);
            finished = false;
            UpdateHud();
        }
    }
}

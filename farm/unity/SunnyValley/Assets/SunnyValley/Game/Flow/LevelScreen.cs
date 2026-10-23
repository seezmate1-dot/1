using System.Collections.Generic;
using SunnyValley.Core.Match3;
using SunnyValley.Game.Level;
using SunnyValley.Game.Match3View;
using SunnyValley.Game.UI;
using SunnyValley.Layout;
using UnityEngine;
using UnityEngine.UI;

namespace SunnyValley.Game.Flow
{
    /// <summary>
    /// The level: goals and moves on top, the board in the middle, in-level boosters at the bottom. Plays an
    /// <see cref="ILevelSession"/> (Core/Match3): a swipe or two taps swap, the board animates the step log, the HUD
    /// counts as the goal ghosts arrive. A booster is armed by a tap (it lifts, the board lights the cell / row /
    /// column under the finger) and spent where the finger is released; «Перемешать» works at once. A win turns the
    /// moves left into pitchforks that fire («бонус-ходы»), then the win popup; out of moves — the +5 popup.
    /// </summary>
    public sealed class LevelScreen : GameScreen
    {
        enum Phase { Intro, Idle, Playing, Bonus, Ending, Done }

        public readonly ILevelSession Session;
        BoardView board;
        int armed = -1;
        Phase phase = Phase.Intro;
        bool built;
        float waitEnd = -1;
        float clock;
        int shownMoves;
        int[] shownLeft;
        readonly List<Image> checks = new List<Image>();
        Text movesText;
        RectTransform movesRt;
        Color movesColor;
        readonly ulong hintSeed;

        public LevelScreen(GameApp app, ILevelSession session) : base(app)
        {
            Session = session;
            BoardSprites.Warm();
            shownMoves = session.Moves;
            var goals = session.Goals;
            shownLeft = new int[goals.Count];
            for (int i = 0; i < goals.Count; i++)
                shownLeft[i] = goals[i].Count;
            hintSeed = (ulong)(session.Level * 31 + 7);
        }

        LevelHud Hud()
        {
            var goals = new List<Goal>();
            var g = Session.Goals;
            for (int i = 0; i < g.Count; i++)
                goals.Add(new Goal(g[i].Icon, shownLeft[i]));
            var boosters = new int[Screens.BoosterIcons.Length];
            for (int i = 0; i < boosters.Length; i++)
                boosters[i] = BoosterCount(i);
            return new LevelHud { Level = Session.Level, Moves = shownMoves, Cols = Session.Cols, Rows = Session.Rows, Goals = goals, Boosters = boosters };
        }

        protected override UiLayout Layout(CanvasFit fit) => Screens.Level(fit, Hud());

        // ------------------------------------------------------------------ booster stock (the meta's, through the app)

        int BoosterCount(int b) => App.State.boosters[b];

        void SpendBooster(int b)
        {
            App.State.boosters[b]--;
            App.State.Save();
            View.SetBadge("level.booster" + b + ".badge", App.State.boosters[b]);
        }

        // ------------------------------------------------------------------ binding

        protected override void Bind()
        {
            board = View.Rect("level.board").GetComponent<BoardView>();
            board.SetFlyLayer(View.Root);
            board.GoalTarget = i => View.Rect("level.goal" + i + ".icon") is RectTransform r ? r.position : board.transform.position;
            board.MovesTarget = () => movesRt != null ? movesRt.position : board.transform.position;
            board.HintProvider = () => Hints.Find(Session.Game, hintSeed);
            board.SwapGesture += OnSwap;
            board.TargetChosen += OnTarget;
            board.MovesShown += ShowMoves;
            board.GoalReached += OnGoal;

            View.OnClick("level.pause", () => new SettingsPopup(App, true).Open());
            for (int i = 0; i < Screens.BoosterIcons.Length; i++)
            {
                int b = i;
                View.OnClick("level.booster" + i, () => OnBooster(b));
            }
            movesRt = View.Rect("level.moves.value");
            movesText = movesRt != null ? movesRt.GetComponent<Text>() : null;
            if (movesText != null)
                movesColor = movesText.color;
            MakeChecks();

            armed = -1;
            if (!built)
            {
                // first time: the pieces drop in, then the pre-level boosters appear on the board
                built = true;
                board.Show(Session.Game, Session.InitialEvents);
                board.InputEnabled = false;
                float intro = board.Intro();
                phase = Phase.Intro;
                waitEnd = clock + intro;
                return;
            }
            // a rebuild (screen size or safe area changed): draw the game as it is and carry on from there
            board.Show(Session.Game);
            SyncHud();
            switch (phase)
            {
                case Phase.Intro:
                case Phase.Playing:
                    AfterMove();
                    break;
                case Phase.Bonus:
                    Finish();
                    break;
                case Phase.Idle:
                    board.InputEnabled = true;
                    break;
                default:
                    board.InputEnabled = false;
                    break;
            }
        }

        /// <summary>A tick under each goal's counter, shown when the goal is done.</summary>
        void MakeChecks()
        {
            checks.Clear();
            for (int i = 0; i < shownLeft.Length; i++)
            {
                var count = View.Rect("level.goal" + i + ".count");
                if (count == null)
                {
                    checks.Add(null);
                    continue;
                }
                var go = new GameObject("check", typeof(RectTransform));
                go.layer = count.gameObject.layer;
                var rt = (RectTransform)go.transform;
                rt.SetParent(count, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                float h = Mathf.Max(30, count.rect.height * 1.1f);
                rt.sizeDelta = new Vector2(h, h);
                var img = go.AddComponent<Image>();
                img.sprite = ShapeSprites.Icon(Icons.Check);
                img.preserveAspect = true;
                img.raycastTarget = false;
                checks.Add(img);
                ShowGoal(i, false);
            }
        }

        void ShowGoal(int i, bool animate)
        {
            bool done = shownLeft[i] <= 0;
            View.SetText("level.goal" + i + ".count", done ? "" : shownLeft[i].ToString());
            var check = i < checks.Count ? checks[i] : null;
            if (check != null)
            {
                bool was = check.enabled;
                check.enabled = done;
                if (done && !was && animate)
                    Tweener.Scale(check.rectTransform, 0.2f, 1, 0.35f, Easing.Kind.OutBack);
            }
            if (animate)
            {
                var icon = View.Rect("level.goal" + i + ".icon");
                if (icon != null)
                    Tweener.Punch(icon, 0.28f, 0.28f);
            }
        }

        void SyncHud()
        {
            shownMoves = Session.Moves;
            ShowMoves(shownMoves);
            var goals = Session.Goals;
            for (int i = 0; i < shownLeft.Length && i < goals.Count; i++)
            {
                shownLeft[i] = goals[i].Count;
                ShowGoal(i, false);
            }
        }

        void ShowMoves(int moves)
        {
            bool changed = moves != shownMoves;
            shownMoves = moves;
            View.SetText("level.moves.value", Mathf.Max(0, moves).ToString());
            if (changed && movesRt != null)
                Tweener.Punch(movesRt, 0.22f, 0.25f);
        }

        void OnGoal(GoalProgressEvent g)
        {
            if (g.GoalIndex < 0 || g.GoalIndex >= shownLeft.Length)
                return;
            int left = Mathf.Max(0, g.Target - g.Current);
            if (left == shownLeft[g.GoalIndex])
                return;
            shownLeft[g.GoalIndex] = left;
            ShowGoal(g.GoalIndex, true);
        }

        // ------------------------------------------------------------------ the player's actions

        void OnSwap(Pos a, Pos b)
        {
            if (phase != Phase.Idle)
                return;
            var r = Session.Swap(a, b);
            Play(r);
        }

        void Play(MoveResult r)
        {
            phase = Phase.Playing;
            board.InputEnabled = false;
            board.Play(r.Events, AfterMove);
        }

        void OnBooster(int b)
        {
            if (phase != Phase.Idle)
                return;
            if (BoosterCount(b) <= 0)
            {
                Ui.Toast($"«{Screens.BoosterNames[b]}» закончились — их дают задания и события");
                return;
            }
            if (b == 3)
            {
                Arm(-1);
                var r = Session.UseBooster(b, new Pos(0, 0));
                if (!r.Accepted)
                {
                    Ui.Toast("Сейчас перемешать нельзя");
                    return;
                }
                SpendBooster(b);
                Play(r);
                return;
            }
            Arm(armed == b ? -1 : b);
            if (armed >= 0)
                Ui.Toast(b == 0 ? "Выберите клетку" : b == 1 ? "Выберите строку" : "Выберите столбец");
        }

        void OnTarget(Pos at)
        {
            if (phase != Phase.Idle || armed < 0)
                return;
            int b = armed;
            var r = Session.UseBooster(b, at);
            if (!r.Accepted)
            {
                Ui.Toast("Сюда нельзя — выберите другую клетку");
                return;
            }
            Arm(-1);
            SpendBooster(b);
            Play(r);
        }

        void Arm(int b)
        {
            for (int i = 0; i < Screens.BoosterIcons.Length; i++)
            {
                var disc = View.Rect("level.booster" + i + ".disc");
                if (disc != null)
                {
                    bool up = i == b;
                    Tweener.Run(disc, "lift", 0.18f, Easing.Kind.OutBack, k => disc.localScale = Vector3.one * (up ? Easing.Lerp(1, 1.15f, k) : 1));
                }
            }
            armed = b;
            board.SetTargetMode(b >= 0 && b < 3 ? b : -1);
        }

        // ------------------------------------------------------------------ after a move

        void AfterMove()
        {
            if (Session.Won)
            {
                phase = Phase.Bonus;
                board.InputEnabled = false;
                waitEnd = -1;
                return; // Tick starts the bonus moves when the goal ghosts have landed
            }
            if (Session.Lost)
            {
                phase = Phase.Ending;
                board.InputEnabled = false;
                return; // Tick opens the popup when the board has settled
            }
            phase = Phase.Idle;
            board.InputEnabled = true;
        }

        void Finish()
        {
            phase = Phase.Done;
            board.InputEnabled = false;
            App.LevelWon(this);
        }

        public override void Tick(float dt)
        {
            clock += dt;
            if (movesRt != null && movesText != null)
            {
                bool low = shownMoves <= 5 && shownMoves > 0 && phase != Phase.Bonus && phase != Phase.Done;
                if (low)
                {
                    float s = 1 + 0.07f * Mathf.Abs(Mathf.Sin(clock * 4.5f));
                    movesRt.localScale = new Vector3(s, s, 1);
                    movesText.color = Color.Lerp(movesColor, new Color(0.9f, 0.2f, 0.12f), 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(clock * 4.5f)));
                }
                else if (movesText.color != movesColor)
                {
                    movesText.color = movesColor;
                }
            }

            switch (phase)
            {
                case Phase.Intro:
                    if (clock < waitEnd)
                        break;
                    if (Session.InitialEvents != null && Session.InitialEvents.Count > 0)
                    {
                        phase = Phase.Playing;
                        board.Play(Session.InitialEvents, AfterMove);
                    }
                    else
                    {
                        AfterMove();
                    }
                    break;

                case Phase.Bonus:
                    if (waitEnd < 0)
                    {
                        if (!board.Settled)
                            break;
                        // «бонус-ходы»: every move left becomes a pitchfork that fires
                        var finale = BonusFinale.Build(Session.Game.Board, Session.Moves, hintSeed);
                        waitEnd = float.MaxValue;
                        if (finale.Cues.Count == 0)
                        {
                            waitEnd = clock + 0.35f;
                            break;
                        }
                        board.PlayTimeline(finale, () => waitEnd = clock + 0.45f, false);
                        break;
                    }
                    if (clock >= waitEnd)
                        Finish();
                    break;

                case Phase.Ending:
                    if (!board.Settled)
                        break;
                    phase = Phase.Done;
                    new LosePopup(App, this).Open();
                    break;
            }
        }

        /// <summary>After «+5 ходов»: back to playing (the engine may reshuffle — that animates too).</summary>
        public void Continue()
        {
            var r = Session.AddMoves(5);
            if (!r.Accepted)
            {
                AfterMove();
                return;
            }
            Play(r);
        }
    }
}

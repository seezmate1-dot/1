using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SunnyValley.Core.Match3;
using SunnyValley.Core.Meta;
using SunnyValley.Game.Flow;
using SunnyValley.Game.Level;
using SunnyValley.Game.UI;
using SunnyValley.Layout;
using UnityEngine;

namespace SunnyValley.Game
{
    /// <summary>
    /// A level session that can report its result to the meta exactly (the Core/Match3 board through
    /// Match3MetaBridge.ToLevelResult). Sessions without it are summed up from ILevelSession's numbers.
    /// </summary>
    public interface ILevelResultSource
    {
        LevelResult ToLevelResult();
    }

    /// <summary>
    /// Starts by itself after any scene loads (the build has one empty scene) and runs the shell:
    /// loading → farm → «Играть N» → the popup before the level → the level → won / lost → back to the farm.
    /// Holds the canvas (UiRoot), the player's state (the real MetaGame, saved in persistentDataPath) and the current
    /// screen; rebuilds the screen and its popups when the screen size or safe area changes. Every rule goes through
    /// MetaGame: StartLevel / FinishLevel around a level, lives, boosters, +5 moves, «Пока вас не было» on start and
    /// on coming back to the app.
    /// </summary>
    public sealed class GameApp : MonoBehaviour
    {
        public UiRoot Ui { get; private set; }
        public GameState State { get; private set; }
        public readonly List<GamePopup> Popups = new List<GamePopup>();
        public GameScreen Current { get; private set; }

        /// <summary>Makes the session for a level (the goals for the popup before it): a real Core/Match3 level.</summary>
        public static Func<int, ILevelSession> SessionFactory = level => new Match3LevelSession(level);

        readonly Queue<string> toasts = new Queue<string>();
        float toastClock;
        AwaySummary pendingAway;
        bool started;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<GameApp>() == null)
                new GameObject("SunnyValley").AddComponent<GameApp>();
        }

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
            Input.multiTouchEnabled = true;
            State = GameState.Load();
            Ui = UiRoot.Create();
            Ui.Changed += Rebuild;
        }

        void Start() => StartCoroutine(Load());

        IEnumerator Load()
        {
            var loading = new LoadingScreen(this);
            Switch(loading);
            yield return null;
            // paint every icon now, not in the middle of the first level
            var icons = ShapeSprites.KnownIcons().ToList();
            for (int i = 0; i < icons.Count; i++)
            {
                ShapeSprites.Icon(icons[i]);
                loading.SetProgress(0.1f + 0.6f * (i + 1) / icons.Count, "Готовим ферму…");
                if (i % 4 == 3)
                    yield return null;
            }
            loading.SetProgress(0.8f, "Расчищаем двор…");
            yield return null;
            var layout = FarmArt.Layout; // parsed before the farm is built
            loading.SetProgress(1, "Солнце встаёт…");
            yield return new WaitForSecondsRealtime(0.35f);

            long now = GameState.Now;
            // the app was closed in the middle of a level: it counts as a loss, like leaving it
            if (State.Meta.ActiveLevel != 0)
            {
                State.Meta.AbandonLevel(now);
                toasts.Enqueue("Прошлый уровень прерван — жизнь потрачена");
            }
            pendingAway = State.Meta.Resume(now);
            State.Save();
            started = true;
            Ui.FadeThrough(() => Switch(new FarmScreen(this)));
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Welcome();
        }

        /// <summary>On the farm after a start or a return: «Пока вас не было», then any story scene waiting.</summary>
        IEnumerator Welcome()
        {
            if (!(Current is FarmScreen farm))
                yield break;
            var away = pendingAway;
            pendingAway = null;
            if (away != null && !away.IsEmpty)
            {
                bool closed = false;
                new AwayPopup(this, away, () => closed = true).Open();
                yield return new WaitUntil(() => closed);
                farm.Refresh();
            }
            if (Current == farm)
                yield return farm.PlayPendingScenes();
            AnnounceEvents();
        }

        void Switch(GameScreen next)
        {
            Ui.CloseAllPopups();
            Popups.Clear();
            Current?.Hide();
            Current = next;
            Current.Show();
        }

        void Update()
        {
            Current?.Tick(Time.unscaledDeltaTime);
            foreach (var p in Popups.ToArray())
                p.Tick(Time.unscaledDeltaTime);
            // queued toasts, one every 2.6 s
            toastClock -= Time.unscaledDeltaTime;
            if (toasts.Count > 0 && toastClock <= 0)
            {
                Ui.Toast(toasts.Dequeue());
                toastClock = 2.6f;
            }
            // Android «back»: close the top popup, else leave a level for the farm
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Popups.Count > 0)
                {
                    var top = Popups[Popups.Count - 1];
                    if (!(top is DialoguePopup) && !(top is CelebratePopup))
                        top.Close();
                }
                else if (Current is LevelScreen)
                {
                    new SettingsPopup(this, true).Open();
                }
            }
        }

        void Rebuild()
        {
            Current?.Rebuild();
            foreach (var p in Popups.ToList())
                p.Rebuild();
        }

        void OnApplicationPause(bool paused)
        {
            if (State == null || !started)
                return;
            long now = GameState.Now;
            if (paused)
            {
                State.Meta.Touch(now);
                State.Save();
                return;
            }
            var away = State.Meta.Resume(now);
            State.Save();
            if (away.IsEmpty)
                return;
            pendingAway = away;
            if (Current is FarmScreen && Popups.Count == 0)
                StartCoroutine(Welcome());
        }

        void OnApplicationQuit()
        {
            if (State == null)
                return;
            State.Meta.Touch(GameState.Now);
            State.Save();
        }

        /// <summary>Toasts for what the meta announced (level ups, found boosters…); zones and the chapter have their own windows.</summary>
        public void AnnounceEvents()
        {
            foreach (var e in State.Meta.TakeEvents())
            {
                if (e.Kind == MetaEventKind.ZoneCompleted || e.Kind == MetaEventKind.ChapterCompleted || e.Kind == MetaEventKind.NewDay)
                    continue;
                if (!string.IsNullOrEmpty(e.Text))
                    toasts.Enqueue(e.Text);
            }
        }

        // ------------------------------------------------------------------ the flow

        public void OpenLevelStart()
        {
            long now = GameState.Now;
            if (!State.Meta.CanPlay(now))
            {
                Ui.Toast($"Нет жизней. Следующая через {Format.Timer(State.Meta.NextLifeIn(now))}");
                return;
            }
            int level = State.Meta.NextLevel;
            var session = SessionFactory(level);
            new LevelStartPopup(this, new LevelStartInfo
            {
                Level = level,
                Goals = session.Goals.Select(g => new Goal(g.Icon, g.Count)).ToList(),
                Boosters = State.PreBoosters(),
                Picked = new bool[Boosters.PreLevel.Length],
            }).Open();
        }

        public void StartLevel(LevelStartInfo info)
        {
            long now = GameState.Now;
            var picked = new List<string>();
            for (int i = 0; i < info.Picked.Length && i < Boosters.PreLevel.Length; i++)
            {
                if (info.Picked[i])
                    picked.Add(Boosters.PreLevel[i]);
            }
            var r = State.Meta.StartLevel(now, info.Level, picked);
            if (!r.Ok)
            {
                Ui.Toast(r.Error == MetaError.NoLives
                    ? $"Нет жизней. Следующая через {Format.Timer(State.Meta.NextLifeIn(now))}"
                    : r.ErrorText);
                return;
            }
            State.Save();
            // the picked pre-level boosters go onto the board at the start (GDD 4.5)
            var session = new Match3LevelSession(info.Level, info.Picked);
            Ui.FadeThrough(() => Switch(new LevelScreen(this, session)));
        }

        /// <summary>The meta's view of a finished attempt: the board's own report if it has one.</summary>
        static LevelResult ResultOf(ILevelSession s, bool won)
        {
            if (s is ILevelResultSource src)
            {
                var r = src.ToLevelResult();
                r.Won = won;
                return r;
            }
            return new LevelResult
            {
                Level = s.Level, Won = won, MovesLeft = Math.Max(0, s.Moves), Stars = won ? s.StarsEarned : 0,
                CoinsEarned = won ? Math.Max(0, s.Moves) * Match3MetaBridge.CoinsPerMoveLeft : 0,
            };
        }

        public void LevelWon(LevelScreen level)
        {
            var o = State.Meta.FinishLevel(GameState.Now, ResultOf(level.Session, true));
            if (!o.Ok)
                Debug.LogWarning("FinishLevel: " + o.Error);
            State.Save();
            var info = new WinInfo
            {
                Level = level.Session.Level, Stars = Math.Max(1, level.Session.StarsEarned), Coins = o.Reward.Coins,
                FarmStars = o.StarsGained,
            };
            new WinPopup(this, info).Open();
        }

        /// <summary>Out of moves and gave up, or left the level from the pause: a loss (a life).</summary>
        public void LevelLost()
        {
            if (Current is LevelScreen level && State.Meta.ActiveLevel != 0)
                State.Meta.FinishLevel(GameState.Now, ResultOf(level.Session, false));
            else if (State.Meta.ActiveLevel != 0)
                State.Meta.AbandonLevel(GameState.Now);
            State.Save();
            BackToFarm();
        }

        public void BackToFarm() => Ui.FadeThrough(() =>
        {
            Switch(new FarmScreen(this));
            AnnounceEvents();
        });
    }
}

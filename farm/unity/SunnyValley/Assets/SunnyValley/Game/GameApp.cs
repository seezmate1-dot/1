using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SunnyValley.Game.Flow;
using SunnyValley.Game.Level;
using SunnyValley.Game.UI;
using SunnyValley.Layout;
using UnityEngine;

namespace SunnyValley.Game
{
    /// <summary>
    /// Starts by itself after any scene loads (the build has one empty scene) and runs the shell:
    /// loading → farm → «Играть N» → the popup before the level → the level → won / lost → back to the farm.
    /// Holds the canvas (UiRoot), the player's state and the current screen; rebuilds the screen and its popups
    /// when the screen size or safe area changes.
    /// </summary>
    public sealed class GameApp : MonoBehaviour
    {
        public UiRoot Ui { get; private set; }
        public GameState State { get; private set; }
        public readonly List<GamePopup> Popups = new List<GamePopup>();
        public GameScreen Current { get; private set; }

        /// <summary>«+5 ходов» bought in this level (the price grows: 900, 1900, 2900).</summary>
        public int MovesBought;

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
                loading.SetProgress(0.1f + 0.8f * (i + 1) / icons.Count, "Готовим ферму…");
                if (i % 4 == 3)
                    yield return null;
            }
            loading.SetProgress(1, "Солнце встаёт…");
            yield return new WaitForSecondsRealtime(0.35f);
            Ui.FadeThrough(() => Switch(new FarmScreen(this)));
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
            // Android «back»: close the top popup, else leave a level for the farm
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Popups.Count > 0)
                    Popups[Popups.Count - 1].Close();
                else if (Current is LevelScreen)
                    new SettingsPopup(this, true).Open();
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
            if (paused)
                State?.Save();
        }

        void OnApplicationQuit() => State?.Save();

        // ------------------------------------------------------------------ the flow

        public void OpenLevelStart()
        {
            var session = new DemoLevelSession(State.level);
            new LevelStartPopup(this, new LevelStartInfo
            {
                Level = State.level,
                Goals = session.Goals.Select(g => new Goal(g.Icon, g.Count)).ToList(),
                Boosters = State.preBoosters.ToArray(),
                Picked = new bool[3],
            }).Open();
        }

        public void StartLevel(LevelStartInfo info)
        {
            State.UpdateLives(GameState.Now);
            if (State.lives <= 0)
            {
                Ui.Toast($"Нет жизней. Следующая через {Format.Timer(State.NextLifeIn(GameState.Now))}");
                return;
            }
            for (int i = 0; i < info.Picked.Length; i++)
            {
                if (info.Picked[i])
                    State.preBoosters[i] = Mathf.Max(0, State.preBoosters[i] - 1);
            }
            State.Save();
            MovesBought = 0;
            // pre-level boosters go onto the board when Core's board can take them (GDD §4.5)
            var session = new DemoLevelSession(State.level);
            Ui.FadeThrough(() => Switch(new LevelScreen(this, session)));
        }

        public void LevelWon(LevelScreen level)
        {
            int coins = 50 + level.Session.Moves * 10;
            var info = new WinInfo { Level = level.Session.Level, Stars = level.Session.StarsEarned, Coins = coins, FarmStars = 1 };
            State.coins += coins;
            State.stars += 1; // every level won is a star for the farm (GDD §3)
            State.level++;
            State.Save();
            new WinPopup(this, info).Open();
        }

        public void LevelLost()
        {
            State.LoseLife(GameState.Now);
            State.Save();
            BackToFarm();
        }

        public void BackToFarm() => Ui.FadeThrough(() => Switch(new FarmScreen(this)));
    }
}

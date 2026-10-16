using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LastHearth.Core;
using LastHearth.Core.Combat;
using LastHearth.Core.Tests;
using LastHearth.Game;
using UnityEngine;

/// <summary>
/// Smoke test of the Unity debug screen: a fresh game and a fortress played by the bot for 20 days
/// are clicked through every tab at random for thousands of frames. Fails on any exception or on
/// unbalanced Begin/End layout groups (which would break Unity's IMGUI). A last run turns on the
/// places on the surface (Art.json "scene") with placeholders for the pictures not painted yet;
/// --scene turns them on for every run and the preview.
/// </summary>
public static class UiSmoke
{
    static readonly BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    /// <summary>How many windows GameRunner has (its Win enum, Win.None included).</summary>
    static readonly int WindowCount = Enum.GetValues(typeof(GameRunner).GetField("window", Any).FieldType).Length;

    /// <summary>The pictures of the places on the surface (GameRunner.Scene.cs), Resources/Art/Scene.</summary>
    static readonly string[] ScenePlaces = { "Pier", "Board", "Merchant", "Pen", "Arena", "Portal" };

    /// <summary>Turns the places on the surface on; the pictures not painted yet load as placeholders.</summary>
    static void SceneOn()
    {
        foreach (string place in ScenePlaces)
            UnityEngine.Resources.Placeholders.Add("Art/Scene/" + place);
        ArtLibrary.Data.Scene = true;
    }

    public static int Main(string[] args)
    {
        string resources = FindResources();
        UnityEngine.Resources.Folder = resources;
        // --scene: every run (and the preview) with the places on the surface, on placeholders until they are painted
        bool sceneMode = args.Contains("--scene");
        args = args.Where(a => a != "--scene").ToArray();
        int frames = args.Length > 0 ? int.Parse(args[0]) : 3 * WindowCount * 150; // three rounds of every window, 150 frames each
        var clicks = new Dictionary<string, int>();
        try
        {
            var balance = Balance.FromJson(File.ReadAllText(Path.Combine(resources, "balance.json")));
            var combat = CombatBalance.FromJson(File.ReadAllText(Path.Combine(resources, "combat.json")));
            // while Art.json has no "scene", the runs below must load no Scene/* picture: the stubs throw on missing art
            bool scenePainted = ArtLibrary.Data.Scene;
            if (sceneMode)
                SceneOn();

            Run("новая игра", null, frames, 1, clicks);

            var played = NewGame.Create(balance, TestData.Start);
            played.Combat = combat;
            new PlayerBot(played).PlayDays(TestData.Start, 20);
            Run("крепость бота, 20 дней", played, frames, 2, clicks);
            if (args.Length > 1)
                Preview(played, args[1], args.Length > 2 ? int.Parse(args[2]) : 0, args.Skip(3).ToArray());

            var late = NewGame.Create(balance, TestData.Start);
            late.Combat = combat;
            new PlayerBot(late).PlayDays(TestData.Start, 35);
            // back after five hours away: «Пока вас не было» opens first
            Run("крепость бота, 35 дней, 5 часов без игрока", late, frames, 3, clicks, 5 * 3600);

            // the places on the surface instead of the side buttons (Art.json "scene"), on placeholders until painted
            var surface = NewGame.Create(balance, TestData.Start);
            surface.Combat = combat;
            new PlayerBot(surface).PlayDays(TestData.Start, 20);
            SceneOn();
            Run("крепость бота, 20 дней, места на поверхности", surface, frames / 3, 4, clicks);
            ArtLibrary.Data.Scene = scenePainted || sceneMode;
            var loaded = (Dictionary<string, Texture2D>)typeof(ArtLibrary).GetField("textures", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var unseen = ScenePlaces.Where(p => !loaded.ContainsKey("Scene/" + p)).ToList();
            if (unseen.Count > 0)
                throw new InvalidOperationException("places on the surface never drawn: " + string.Join(", ", unseen));
        }
        catch (Exception e)
        {
            Console.WriteLine("UI SMOKE FAILED: " + e);
            return 1;
        }
        Console.WriteLine("Нажато разных кнопок: " + clicks.Count + ", всего нажатий: " + clicks.Values.Sum());
        foreach (var kv in clicks.OrderByDescending(kv => kv.Value).Take(25))
            Console.WriteLine("  " + kv.Value + " × " + kv.Key.Replace("\n", " / "));
        // every system must have been reached through its buttons
        string[] must = { "[ячейка]", "Бой", "Атаковать", "Бой турнира", "Войти в Портал", "Сделка", "Гарнизон", "Играть", "[ячейка]Обновить соперников",
                          "Защищать вживую", "Отправить отряд", "Набор материалов", "Забрать:", "Вспышка очага", "ФОНАРЬ!", "Тепло:",
                          "Кормить:", "Сделать активным", "Купить:", "Обменять", "Узы +", "Следующий ▶", "Календарь", "Сезон", "Продолжить" };
        var missing = must.Where(m => !clicks.Keys.Any(k => k.StartsWith(m))).ToList();
        Console.WriteLine("Не нажимались: " + (missing.Count == 0 ? "—" : string.Join(", ", missing)));
        Console.WriteLine("Картинок загружено: " + UnityEngine.Resources.TexturesLoaded + " (заглушек мест на поверхности: " +
                          UnityEngine.Resources.PlaceholdersLoaded + "), нарисовано: " + GUI.Textures);
        Console.WriteLine("Звуков сыграно: " + AudioSource.Played);
        Console.WriteLine("Выходов кнопкой «Назад»: " + Application.Quits + ", подсветка экрана: " + Screen.sleepTimeout);
        Console.WriteLine("UI smoke passed");
        return 0;
    }

    static void Run(string name, GameSession session, int frames, int seed, Dictionary<string, int> clicks, long away = 0)
    {
        Application.persistentDataPath = Path.Combine(Path.GetTempPath(), "lasthearth-ui-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Application.persistentDataPath);
        var runner = new GameRunner();
        Call(runner, "Awake");
        if (session != null)
        {
            Set(runner, "skip", session.State.Time + away - DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            Call(runner, "StartSession", session);
            if (away > 0 && (int)Get(runner, "window") != WindowCount - 1)
                throw new InvalidOperationException(name + ": the summary «Пока вас не было» did not open");
        }
        var rng = new Random(seed);
        GUILayout.Click = label =>
        {
            if (label.Contains("Новая игра") || rng.NextDouble() > 0.04)
                return false;
            string key = new string(label.TakeWhile(c => !char.IsDigit(c) && c != '\n' && c != '(').ToArray()).Trim();
            clicks[key] = clicks.TryGetValue(key, out int n) ? n + 1 : 1;
            return true;
        };
        int battles = 0, surges = 0;
        for (int frame = 0; frame < frames; frame++)
        {
            // open every window in turn, and the bare fortress between them
            SetEnum(runner, "window", (frame / 150) % WindowCount); // every window: Win.None .. Win.Away
            Set(runner, "onMap", (frame / 75) % 3 == 1); // the map of the islands, under the windows too
            if (frame % 500 == 499)
                Set(runner, "skip", (long)Get(runner, "skip") + 3 * 3600); // walk through the day: Surge, siege windows, new tasks
            Time.unscaledDeltaTime = 1.0f;
            Time.realtimeSinceStartup += 1.0f;
            Time.time += 0.5f;
            // a finger drags the screen now and then, and the Android back button is pressed
            int phase = frame % 40;
            Input.Touches = phase == 0 ? new[] { new Touch { phase = TouchPhase.Began } }
                : phase <= 3 ? new[] { new Touch { phase = TouchPhase.Moved, deltaPosition = new Vector2(0, phase == 3 ? -90 : 60) } }
                : phase == 4 ? new[] { new Touch { phase = TouchPhase.Ended } }
                : new Touch[0];
            Input.Escape = frame % 211 == 210;
            GUILayout.Depth = 0;
            Call(runner, "OnGUI");
            if (GUILayout.Depth != 0)
                throw new InvalidOperationException(name + ": layout groups not balanced (" + GUILayout.Depth + ") in window " + Get(runner, "window"));
            Call(runner, "Update");
            if (Get(runner, "battle") != null)
                battles++;
            if (Get(runner, "surge") != null)
                surges++;
        }
        Call(runner, "OnApplicationQuit");
        var s = (GameSession)Get(runner, "session");
        Console.WriteLine(string.Format("{0}: {1} кадров, кадров в бою {2}, в Наплыве {3}, очаг {4}, сообщение «{5}»", name, frames, battles, surges,
            s.State.HearthLevel, Get(runner, "message")));
    }

    /// <summary>Writes what the main screen draws (the fortress and its HUD), for hearth/ui-smoke/preview.py.</summary>
    /// <summary>Extra arguments "field=number" set the runner's int fields first (a tab of the window: arenaTab=2).</summary>
    static void Preview(GameSession session, string path, int window, string[] fields)
    {
        var runner = new GameRunner();
        Call(runner, "Awake");
        Set(runner, "skip", session.State.Time - DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        Call(runner, "StartSession", session);
        foreach (string f in fields.Where(f => f.Contains('=')))
            Set(runner, f.Split('=')[0], int.Parse(f.Split('=')[1]));
        GUILayout.Click = _ => false;
        // window -1: the map of the islands; -2: the loading screen; -3: the effects (collect all, «Готово», a level up) in flight
        Set(runner, "onMap", window == -1);
        SetEnum(runner, "window", Math.Max(0, window));
        if (window == 2)
            Set(runner, "selectedRoom", session.State.Rooms[0].Id);
        Time.unscaledDeltaTime = 0.5f;
        for (int i = 0; i < (window == -2 ? 3 : 12); i++)
            Call(runner, "Update"); // the dwellers start walking
        if (window == -3)
        {
            Call(runner, "OnGUI"); // where the rooms and the plates are
            Call(runner, "CollectingAll", (Action)session.CollectAll, null);
            ((Dictionary<int, float>)Get(runner, "roomDoneAt"))[session.State.Rooms[0].Id] = Time.realtimeSinceStartup;
            Set(runner, "levelUpAt", Time.realtimeSinceStartup);
            Time.realtimeSinceStartup += 0.5f;
        }
        else if (window > 0)
        {
            Call(runner, "OnGUI"); // the window starts popping open
            Time.realtimeSinceStartup += 1f; // and is fully open
        }
        GUI.Record = new List<string>();
        Call(runner, "OnGUI");
        File.WriteAllLines(path, GUI.Record);
        GUI.Record = null;
    }


    static object Get(object o, string field) => o.GetType().GetField(field, Any).GetValue(o);

    static void Set(object o, string field, object value) => o.GetType().GetField(field, Any).SetValue(o, value);

    static void SetEnum(object o, string field, int value)
    {
        var f = o.GetType().GetField(field, Any);
        f.SetValue(o, Enum.ToObject(f.FieldType, value));
    }

    static void Call(object o, string method, params object[] args)
    {
        try
        {
            o.GetType().GetMethod(method, Any).Invoke(o, args);
        }
        catch (TargetInvocationException e)
        {
            throw new Exception(method + " threw", e.InnerException);
        }
    }

    static string FindResources()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, "hearth", "unity", "LastHearth", "Assets", "LastHearth", "Resources");
            if (Directory.Exists(candidate))
                return candidate;
            candidate = Path.Combine(dir.FullName, "unity", "LastHearth", "Assets", "LastHearth", "Resources");
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Resources folder of the Unity project");
    }
}

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
/// unbalanced Begin/End layout groups (which would break Unity's IMGUI).
/// </summary>
public static class UiSmoke
{
    static readonly BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    public static int Main(string[] args)
    {
        string resources = FindResources();
        UnityEngine.Resources.Folder = resources;
        int frames = args.Length > 0 ? int.Parse(args[0]) : 6000;
        var clicks = new Dictionary<string, int>();
        try
        {
            var balance = Balance.FromJson(File.ReadAllText(Path.Combine(resources, "balance.json")));
            var combat = CombatBalance.FromJson(File.ReadAllText(Path.Combine(resources, "combat.json")));

            Run("новая игра", null, frames, 1, clicks);

            var played = NewGame.Create(balance, TestData.Start);
            played.Combat = combat;
            new PlayerBot(played).PlayDays(TestData.Start, 20);
            Run("крепость бота, 20 дней", played, frames, 2, clicks);
            if (args.Length > 1)
                Preview(played, args[1], args.Length > 2 ? int.Parse(args[2]) : 0);

            var late = NewGame.Create(balance, TestData.Start);
            late.Combat = combat;
            new PlayerBot(late).PlayDays(TestData.Start, 35);
            Run("крепость бота, 35 дней", late, frames, 3, clicks);
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
        string[] must = { "[ячейка]", "Бой", "Атаковать", "Бой турнира", "Войти в Портал", "Сделка", "Гарнизон", "Играть", "Другие соперники",
                          "Защищать вживую", "Отправить отряд", "Набор материалов", "Забрать:", "Вспышка очага", "ФОНАРЬ!", "Тепло:" };
        var missing = must.Where(m => !clicks.Keys.Any(k => k.StartsWith(m))).ToList();
        Console.WriteLine("Не нажимались: " + (missing.Count == 0 ? "—" : string.Join(", ", missing)));
        Console.WriteLine("Картинок загружено: " + UnityEngine.Resources.TexturesLoaded + ", нарисовано: " + GUI.Textures);
        Console.WriteLine("Звуков сыграно: " + AudioSource.Played);
        Console.WriteLine("Выходов кнопкой «Назад»: " + Application.Quits + ", подсветка экрана: " + Screen.sleepTimeout);
        Console.WriteLine("UI smoke passed");
        return 0;
    }

    static void Run(string name, GameSession session, int frames, int seed, Dictionary<string, int> clicks)
    {
        Application.persistentDataPath = Path.Combine(Path.GetTempPath(), "lasthearth-ui-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Application.persistentDataPath);
        var runner = new GameRunner();
        Call(runner, "Awake");
        if (session != null)
        {
            Set(runner, "skip", session.State.Time - DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            Call(runner, "StartSession", session);
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
            SetEnum(runner, "window", (frame / 150) % 13);
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
    static void Preview(GameSession session, string path, int window)
    {
        var runner = new GameRunner();
        Call(runner, "Awake");
        Set(runner, "skip", session.State.Time - DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        Call(runner, "StartSession", session);
        GUILayout.Click = _ => false;
        SetEnum(runner, "window", window);
        if (window == 2)
            Set(runner, "selectedRoom", session.State.Rooms[0].Id);
        Time.unscaledDeltaTime = 0.5f;
        for (int i = 0; i < 12; i++)
            Call(runner, "Update"); // the dwellers start walking
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

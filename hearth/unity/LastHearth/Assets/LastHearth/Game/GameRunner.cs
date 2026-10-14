using System;
using System.IO;
using System.Linq;
using LastHearth.Core;
using UnityEngine;

namespace LastHearth.Game
{
    /// <summary>
    /// Starts the game in any scene and shows a debug screen of the core (no art yet, milestone M1).
    /// Press Play in an empty scene: the fortress lives in real time, saves itself and catches up
    /// offline time on the next start. "+1 час" skips time to test long timers.
    /// </summary>
    public sealed class GameRunner : MonoBehaviour
    {
        const string SkipKey = "lasthearth.debugSkipSeconds";

        GameSession session;
        string savePath;
        float tick;
        long skip;
        Vector2 scroll;
        string message = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<GameRunner>() == null)
                new GameObject("LastHearth").AddComponent<GameRunner>();
        }

        long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds() + skip;

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            skip = long.Parse(PlayerPrefs.GetString(SkipKey, "0"));
            var balance = Core.Balance.FromJson(UnityEngine.Resources.Load<TextAsset>("balance").text);
            savePath = Path.Combine(Application.persistentDataPath, "save.json");
            GameState state = File.Exists(savePath) ? SaveSystem.FromJson(File.ReadAllText(savePath)) : null;
            session = state != null ? new GameSession(balance, state) : NewGame.Create(balance, Now());
            session.JobCompleted += job => message = "Готово: " + JobName(job) + " → ур. " + job.ToLevel;
            session.Advance(Now());
        }

        void Update()
        {
            tick += Time.unscaledDeltaTime;
            if (tick >= 1f)
            {
                tick = 0;
                session.Advance(Now());
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (paused)
                Save();
        }

        void OnApplicationQuit() => Save();

        void Save()
        {
            File.WriteAllText(savePath, SaveSystem.ToJson(session.State));
            PlayerPrefs.SetString(SkipKey, skip.ToString());
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------ debug screen

        void OnGUI()
        {
            float scale = Mathf.Max(1f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            var area = new Rect(8, 8, Screen.width / scale - 16, Screen.height / scale - 16);
            GUILayout.BeginArea(area);
            scroll = GUILayout.BeginScrollView(scroll);
            long now = Now();
            var b = session.Balance;
            var st = session.State;

            GUILayout.Label("<b>ПОСЛЕДНИЙ ОЧАГ — отладка ядра</b>   " + message, Rich());
            GUILayout.Label(string.Format("Очаг {0} · режим {1} · тепло {2:0} · строителей {3}{4}",
                st.HearthLevel, ModeName(st.Mode), Formulas.HearthWarmth(b, st.HearthLevel, st.Mode),
                session.BuilderCount(), st.OutOfCoal ? " · <color=#6cf>НЕТ УГЛЯ — всё промерзает</color>" : ""), Rich());
            GUILayout.Label(string.Format("Уголь {0} (−{1:0.0}/ч) · Еда {2} · Серебро {3}/{4} · Железо {5}/{6} · Эссенция {7} · Искры {8} · Часы {9}",
                st.Wallet[ResourceType.Coal], Formulas.CoalBurnPerHour(b, st.HearthLevel, st.Mode), st.Wallet[ResourceType.Food],
                st.Wallet[ResourceType.Silver], session.Capacity(ResourceType.Silver),
                st.Wallet[ResourceType.Iron], session.Capacity(ResourceType.Iron),
                st.Wallet[ResourceType.Essence], st.Wallet[ResourceType.Sparks], Duration(st.HourglassSeconds)));

            GUILayout.BeginHorizontal();
            if (!st.Rewards.IsEmpty() && GUILayout.Button("Забрать награды (" + CostText(st.Rewards) + ")"))
                session.ClaimRewards();
            if (GUILayout.Button("Собрать всё"))
                session.CollectAll();
            foreach (HearthMode mode in Enum.GetValues(typeof(HearthMode)))
            {
                if (GUILayout.Toggle(st.Mode == mode, ModeName(mode), "Button") && st.Mode != mode)
                    session.SetMode(mode);
            }
            if (GUILayout.Button("+1 час"))
            {
                skip += 3600;
                session.Advance(Now());
            }
            if (GUILayout.Button("Новая игра"))
            {
                skip = 0;
                session = NewGame.Create(b, Now());
                message = "";
            }
            GUILayout.EndHorizontal();

            DrawHearth(now);
            DrawQueue(now);
            DrawRooms(now);
            DrawBuild(now);
            DrawDwellers();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawHearth(long now)
        {
            var b = session.Balance;
            int level = session.State.HearthLevel;
            GUILayout.Space(8);
            if (level >= b.MaxHearthLevel)
            {
                GUILayout.Label("<b>Очаг на максимуме</b>", Rich());
                return;
            }
            var row = b.Level(level);
            var unmet = session.HearthRequirements(now);
            string title = string.Format("Улучшить очаг до {0}: {1}, {2}", level + 1, Duration(row.HearthSeconds), CostText(session.HearthJobCost(level)));
            GUI.enabled = unmet.Count == 0;
            if (GUILayout.Button(title))
                Report(session.UpgradeHearth(now));
            GUI.enabled = true;
            foreach (var r in unmet)
                GUILayout.Label("  ✗ " + RequirementText(r));
        }

        void DrawQueue(long now)
        {
            var q = session.State.Queue;
            GUILayout.Label("<b>Стройка</b>", Rich());
            for (int i = 0; i < q.Builders.Count; i++)
            {
                var jobs = q.Builders[i].Jobs;
                GUILayout.Label("Строитель " + (i + 1) + (jobs.Count == 0 ? ": свободен" : ""));
                foreach (var job in jobs.ToList())
                {
                    GUILayout.BeginHorizontal();
                    long left = job.Remaining(now);
                    GUILayout.Label("   " + JobName(job) + " → " + job.ToLevel + (job.IsRunning(now) ? "   осталось " + Duration(left) : "   ждёт"));
                    if (job.IsRunning(now))
                    {
                        if (left <= session.Balance.Build.FreeFinishSeconds && GUILayout.Button("Бесплатно", GUILayout.Width(110)))
                            Report(session.FreeFinish(job.Id, now));
                        else if (session.State.HourglassSeconds > 0 && GUILayout.Button("Часы", GUILayout.Width(70)))
                            Report(session.UseHourglass(job.Id, now));
                        else if (left > session.Balance.Build.FreeFinishSeconds &&
                                 GUILayout.Button("Искры " + Formulas.SpeedupCost(session.Balance, left), GUILayout.Width(110)))
                            Report(session.SpeedUpWithSparks(job.Id, now));
                    }
                    GUILayout.EndHorizontal();
                }
            }
        }

        void DrawRooms(long now)
        {
            GUILayout.Label("<b>Залы</b>", Rich());
            foreach (var room in session.State.Rooms.OrderByDescending(r => r.Floor).ThenBy(r => r.Cell).ToList())
            {
                var data = session.Balance.Room(room.Type);
                GUILayout.BeginHorizontal();
                string output = data.Produces == null ? "" :
                    string.Format(" · {0:0.0}/ч · запас {1:0}", session.RoomOutputPerHour(room), room.Stock);
                string cold = session.IsRoomWarm(room) ? "" : " · <color=#6cf>промёрз</color>";
                GUILayout.Label(string.Format("{0} ур.{1} (этаж {2}, работников {3}){4}{5}",
                    data.Name, room.Level, room.Floor + 1, room.Workers.Count, output, cold), Rich());
                if (data.Produces != null && GUILayout.Button("Собрать", GUILayout.Width(90)))
                    session.Collect(room.Id);
                if (room.Level < session.State.HearthLevel && !session.State.Queue.IsQueued(room.Id) &&
                    GUILayout.Button("Улучшить: " + CostText(session.RoomJobCost(room.Level + 1)), GUILayout.Width(260)))
                    Report(session.UpgradeRoom(room.Id, now));
                GUILayout.EndHorizontal();
            }
        }

        void DrawBuild(long now)
        {
            GUILayout.Label("<b>Построить</b>", Rich());
            GUILayout.BeginHorizontal();
            int shown = 0;
            foreach (var data in session.Balance.Rooms)
            {
                if (data.Type == RoomType.Hearth || session.State.HearthLevel < data.Unlock ||
                    session.CountRooms(data.Type) >= data.MaxCount)
                    continue;
                if (GUILayout.Button(data.Name))
                {
                    var cell = FreeCell();
                    if (cell == null)
                        message = "Нет тёплого места: поднимите очаг или включите «Пылающий»";
                    else
                        Report(session.BuildRoom(data.Type, cell.Value.floor, cell.Value.cell, now, out _));
                }
                if (++shown % 4 == 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
            }
            GUILayout.EndHorizontal();
        }

        void DrawDwellers()
        {
            GUILayout.Label("<b>Жители</b>", Rich());
            var producers = session.State.Rooms.Where(r => session.Balance.Room(r.Type).Stat != null).ToList();
            foreach (var d in session.State.Dwellers.ToList())
            {
                GUILayout.BeginHorizontal();
                var room = session.FindRoom(d.RoomId);
                string where = room == null ? "без работы" : session.Balance.Room(room.Type).Name;
                string hero = d.IsHero ? string.Format(" · герой ур.{0}", d.HeroLevel) : "";
                GUILayout.Label(string.Format("{0} [С{1} Л{2} У{3} Ст{4} Д{5}] · {6}{7}",
                    d.Name, d.Stats[0], d.Stats[1], d.Stats[2], d.Stats[3], d.Stats[4], where, hero));
                if (producers.Count > 0 && GUILayout.Button("Перевести", GUILayout.Width(110)))
                {
                    int index = room == null ? 0 : (producers.IndexOf(room) + 1) % producers.Count;
                    Report(session.Assign(d.Id, producers[index].Id));
                }
                if (!d.IsHero && GUILayout.Button("Сделать героем", GUILayout.Width(150)))
                    Report(session.Promote(d.Id, HeroClass.Swordsman));
                if (d.IsHero && GUILayout.Button("Ур.+1 (" + Formulas.HeroLevelCost(session.Balance, d.HeroLevel) + " эсс.)", GUILayout.Width(170)))
                    Report(session.LevelUpHero(d.Id));
                GUILayout.EndHorizontal();
            }
        }

        // ------------------------------------------------------------------ helpers

        (int floor, int cell)? FreeCell()
        {
            var g = session.Balance.Grid;
            for (int d = 0; d < g.Floors; d++)
            {
                foreach (int floor in new[] { g.HearthFloor + d, g.HearthFloor - d })
                {
                    for (int cell = 0; cell < g.Cells; cell++)
                    {
                        if (session.CellFree(floor, cell) &&
                            Formulas.IsFloorHabitable(session.Balance, session.State.HearthLevel, session.State.Mode, floor, 1))
                            return (floor, cell);
                    }
                }
            }
            return null;
        }

        void Report(Fail fail)
        {
            message = fail == Fail.None ? "" : FailText(fail);
        }

        string JobName(BuildJob job)
        {
            if (job.IsHearth)
                return "Очаг";
            var room = session.FindRoom(job.Target);
            return room == null ? "?" : session.Balance.Room(room.Type).Name;
        }

        string RequirementText(Requirement r)
        {
            switch (r.Kind)
            {
                case RequirementKind.AlreadyQueued: return "очаг уже строится";
                case RequirementKind.Warehouse: return string.Format("Склад {0}/{1} → улучшить склад", r.Have, r.Need);
                case RequirementKind.KeyRoom: return string.Format("{0} {1}/{2} → улучшить", session.Balance.Room(r.Room).Name, r.Have, r.Need);
                case RequirementKind.Heroes: return string.Format("Герои нужного уровня: {0}/{1} → Часовня и эссенция", r.Have, r.Need);
                case RequirementKind.Materials: return string.Format("Материалы {0}/{1}", r.Have, r.Need);
                case RequirementKind.QueueFull: return "все строители заняты";
                default: return r.ToString();
            }
        }

        static string FailText(Fail fail)
        {
            switch (fail)
            {
                case Fail.NotEnough: return "Не хватает ресурсов";
                case Fail.QueueFull: return "Все строители заняты (до 3 работ на строителя)";
                case Fail.AboveHearth: return "Зал не может быть выше очага";
                case Fail.TooCold: return "Там слишком холодно";
                case Fail.RoomFull: return "В зале нет мест";
                case Fail.NoBarracks: return "Нужна казарма";
                case Fail.HeroCap: return "Предел уровня героя: поднимите очаг";
                case Fail.RulesNotMet: return "Не выполнены условия очага";
                default: return fail.ToString();
            }
        }

        static string CostText(ResourceBag bag)
        {
            string[] names = { "уголь", "еда", "железо", "серебро", "эссенция", "искры" };
            var parts = new System.Collections.Generic.List<string>();
            for (int i = 0; i < ResourceBag.Count; i++)
            {
                if (bag.Amounts[i] != 0)
                    parts.Add(names[i] + " " + bag.Amounts[i]);
            }
            return string.Join(", ", parts);
        }

        static string ModeName(HearthMode mode)
        {
            switch (mode)
            {
                case HearthMode.Smoldering: return "Тлеющий";
                case HearthMode.Blazing: return "Пылающий";
                default: return "Ровный";
            }
        }

        static string Duration(long seconds)
        {
            var t = TimeSpan.FromSeconds(seconds);
            if (t.TotalDays >= 1)
                return string.Format("{0}д {1}ч", (int)t.TotalDays, t.Hours);
            if (t.TotalHours >= 1)
                return string.Format("{0}ч {1}м", (int)t.TotalHours, t.Minutes);
            if (t.TotalMinutes >= 1)
                return string.Format("{0}м {1}с", (int)t.TotalMinutes, t.Seconds);
            return t.Seconds + "с";
        }

        static GUIStyle Rich() => new GUIStyle(GUI.skin.label) { richText = true };
    }
}

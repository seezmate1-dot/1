using System;
using System.IO;
using System.Linq;
using LastHearth.Core;
using LastHearth.Core.Combat;
using UnityEngine;

namespace LastHearth.Game
{
    /// <summary>
    /// Starts the game in any scene and shows a debug screen of the core (no art yet, milestone M1).
    /// Press Play in an empty scene: the fortress lives in real time, saves itself and catches up
    /// offline time on the next start. "+1 час" skips time to test long timers. Every system of the
    /// core has a tab: fortress, fights (campaign, Hunt, arena, raids), the live Surge, growth (Heart,
    /// specializations), routes, tasks and shops, live events.
    /// </summary>
    public sealed partial class GameRunner : MonoBehaviour
    {
        const string SkipKey = "lasthearth.debugSkipSeconds";

        GameSession session;
        string savePath;
        float tick;
        long skip;
        Vector2 scroll;
        string message = "";

        CombatBalance combat;
        Battle battle;
        Func<Battle, string> battleFinish;
        string battleTitle = "";
        bool autoBattle;
        float battleClock;
        int huntRank = 1;
        string battleResult;
        int promoteFor = -1;
        int tab;

        static readonly string[] Tabs = { "Крепость", "Бои", "Наплыв", "Развитие", "Вылазки", "Задания и лавки", "События" };

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
            combat = CombatBalance.FromJson(UnityEngine.Resources.Load<TextAsset>("combat").text);
            StartSession(state != null ? new GameSession(balance, state) : NewGame.Create(balance, Now()));
        }

        void StartSession(GameSession s)
        {
            session = s;
            session.Combat = combat;
            session.State.UtcOffsetSeconds = (int)TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalSeconds;
            session.JobCompleted += job => message = "Готово: " + JobName(job) + " → ур. " + job.ToLevel;
            session.SurgeSettled += r => message = r.Live ? "" : string.Format("Наплыв отбит без вас: {0}, +{1} жара", StarText(r.Stars), r.Ember);
            session.Advance(Now());
            // fights left open when the game closed are finished by the auto-battle
            var notes = new System.Collections.Generic.List<string>();
            var hunt = session.ResolvePendingHunt();
            if (hunt != null)
                notes.Add(string.Format("охота доиграна автобоем: {0}, +{1} сердцевин", StarText(hunt.Stars), hunt.Cores));
            var mission = session.ResolvePendingMission();
            if (mission != null)
                notes.Add("бой кампании доигран автобоем: " + StarText(mission.Stars));
            var surge = session.ResolvePendingSurge(Now());
            if (surge != null)
                notes.Add("Наплыв доигран без вас: " + StarText(surge.Stars));
            var ev = session.ResolvePendingEventBattle(Now());
            if (ev != null)
                notes.Add("бой события доигран: " + ev);
            var pvp = session.ResolvePendingPvp(Now());
            if (pvp != null)
                notes.Add(pvp);
            if (notes.Count > 0)
                message = "Незаконченное: " + string.Join("; ", notes);
        }

        void BeginBattle(Battle started, Func<Battle, string> finish, string title = "")
        {
            battle = started;
            battleTitle = title;
            battleFinish = finish;
            battleResult = null;
            battleClock = 0;
            battle.Brain = autoBattle ? Brains.AutoBattle() : null;
        }

        void Update()
        {
            if (battle != null && !battle.Over)
            {
                battleClock += Time.unscaledDeltaTime;
                while (battleClock >= (float)combat.TickSeconds && !battle.Over)
                {
                    battleClock -= (float)combat.TickSeconds;
                    battle.Step();
                }
            }
            if (battle != null && battle.Over && battleResult == null)
                battleResult = battleFinish(battle);
            UpdateSurge();
            UpdateLanterns();
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
            if (battle != null)
            {
                DrawBattle();
                GUILayout.EndArea();
                return;
            }
            if (surge != null)
            {
                DrawSurge();
                GUILayout.EndArea();
                return;
            }
            if (lanternSeed != 0)
            {
                DrawLanterns();
                GUILayout.EndArea();
                return;
            }
            long now = Now();
            var b = session.Balance;
            var st = session.State;

            GUILayout.Label("<b>ПОСЛЕДНИЙ ОЧАГ — отладка ядра</b>   " + message, Rich());
            GUILayout.Label(string.Format("Очаг {0} · режим {1} · тепло {2:0} · строителей {3}{4}",
                st.HearthLevel, ModeName(st.Mode), Formulas.HearthWarmth(b, st.HearthLevel, st.Mode),
                session.BuilderCount(), st.OutOfCoal ? " · <color=#6cf>НЕТ УГЛЯ — всё промерзает</color>" : ""), Rich());
            GUILayout.Label(string.Format("Уголь {0} (−{1:0.0}/ч) · Еда {2} · Серебро {3}/{4} · Железо {5}/{6} · Эссенция {7} · Искры {8} · Часы {9}",
                st.Wallet[ResourceType.Coal], session.CoalBurnPerHour(), st.Wallet[ResourceType.Food],
                st.Wallet[ResourceType.Silver], session.Capacity(ResourceType.Silver),
                st.Wallet[ResourceType.Iron], session.Capacity(ResourceType.Iron),
                st.Wallet[ResourceType.Essence], st.Wallet[ResourceType.Sparks], Duration(st.HourglassSeconds)));
            GUILayout.Label(string.Format("Жар {0} · Сердцевины {1} · Чертежи {2} · Слава {3}",
                st.Wallet[ResourceType.Ember], st.Wallet[ResourceType.Cores], st.Wallet[ResourceType.Blueprints], st.Wallet[ResourceType.Glory]));

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
            if (GUILayout.Button("+8 часов"))
            {
                skip += 8 * 3600;
                session.Advance(Now());
            }
            if (GUILayout.Button("Новая игра"))
            {
                skip = 0;
                StartSession(NewGame.Create(b, Now()));
                message = "";
            }
            GUILayout.EndHorizontal();

            tab = GUILayout.Toolbar(tab, Tabs, GUILayout.Height(32));
            scroll = GUILayout.BeginScrollView(scroll);
            switch (tab)
            {
                case 0:
                    DrawHearth(now);
                    DrawQueue(now);
                    DrawRooms(now);
                    DrawBuild(now);
                    DrawDwellers();
                    break;
                case 1:
                    DrawCampaign(now);
                    DrawHunt(now);
                    DrawArena(now);
                    DrawRaids(now);
                    break;
                case 2:
                    DrawSurgeTab(now);
                    break;
                case 3:
                    DrawHeart();
                    DrawSpecs();
                    break;
                case 4:
                    DrawRoutes(now);
                    break;
                case 5:
                    DrawDaily(now);
                    DrawShops(now);
                    break;
                default:
                    DrawEvents(now);
                    break;
            }
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
                string hero = d.IsHero ? string.Format(" · {0} ур.{1}, умение {2}/{3}", ClassName(d.HeroClass), d.HeroLevel, d.SkillLevel, session.SkillCap(d)) : "";
                GUILayout.Label(string.Format("{0} [С{1} Л{2} У{3} Ст{4} Д{5}] · {6}{7}",
                    d.Name, d.Stats[0], d.Stats[1], d.Stats[2], d.Stats[3], d.Stats[4], where, hero));
                if (producers.Count > 0 && GUILayout.Button("Перевести", GUILayout.Width(110)))
                {
                    int index = room == null ? 0 : (producers.IndexOf(room) + 1) % producers.Count;
                    Report(session.Assign(d.Id, producers[index].Id));
                }
                if (!d.IsHero && GUILayout.Button(promoteFor == d.Id ? "Отмена" : "Сделать героем", GUILayout.Width(150)))
                    promoteFor = promoteFor == d.Id ? -1 : d.Id;
                if (d.IsHero && GUILayout.Button("Ур.+1 (" + Formulas.HeroLevelCost(session.Balance, d.HeroLevel) + " эсс.)", GUILayout.Width(170)))
                    Report(session.LevelUpHero(d.Id));
                if (d.IsHero && d.SkillLevel < session.Balance.Activities.Skills.Max &&
                    GUILayout.Button("Умение +1 (" + session.SkillCost(d) + " серд.)", GUILayout.Width(190)))
                    Report(session.UpgradeSkill(d.Id));
                GUILayout.EndHorizontal();
                if (promoteFor == d.Id)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("   Класс:", GUILayout.Width(70));
                    foreach (HeroClass c in Enum.GetValues(typeof(HeroClass)))
                    {
                        if (c != HeroClass.None && GUILayout.Button(ClassName(c)))
                        {
                            Report(session.Promote(d.Id, c));
                            promoteFor = -1;
                        }
                    }
                    GUILayout.EndHorizontal();
                }
            }
        }

        // ------------------------------------------------------------------ hunt

        void DrawHunt(long now)
        {
            var hunt = session.Balance.Activities.Hunt;
            GUILayout.Label("<b>Охота на чудовищ</b>", Rich());
            if (session.State.HearthLevel < hunt.Unlock)
            {
                GUILayout.Label("Откроется на " + hunt.Unlock + "-м уровне очага");
                return;
            }
            int tracks = session.HuntTracks(now);
            string next = session.State.HuntTrackAt > 0 ? " · следующий через " + Duration(session.State.HuntTrackAt - now) : "";
            GUILayout.BeginHorizontal();
            GUILayout.Label(string.Format("Следы {0}/{1}{2} · отряд {3}/{4} героев · ранг чудовища:", tracks, hunt.TracksPerDay, next,
                session.Squad().Count, session.SquadSize()));
            huntRank = Mathf.Clamp(huntRank, 1, session.State.HearthLevel);
            if (GUILayout.Button("−", GUILayout.Width(30)))
                huntRank = Math.Max(1, huntRank - 1);
            GUILayout.Label(huntRank.ToString(), GUILayout.Width(30));
            if (GUILayout.Button("+", GUILayout.Width(30)))
                huntRank = Math.Min(session.State.HearthLevel, huntRank + 1);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            foreach (var family in session.HuntDens(now))
            {
                var f = combat.Family(family);
                if (GUILayout.Button(string.Format("{0}\nслабость: {1} · +{2} серд.", f.Name, ClassName(f.Weak), hunt.CoresByLevel[huntRank - 1])))
                {
                    var fail = session.StartHunt(family, huntRank, now, out var started);
                    Report(fail);
                    if (fail == Fail.None)
                    {
                        BeginBattle(started, done =>
                        {
                            session.FinishHunt(done, out var reward);
                            return string.Format("{0} {1} · +{2} сердцевин", done.Won ? "Победа" : "Поражение", StarText(reward.Stars), reward.Cores);
                        });
                    }
                }
            }
            GUILayout.EndHorizontal();
        }

        void DrawBattle()
        {
            var f = battle.Family;
            GUILayout.Label(string.Format("<b>{0}{1}</b> · волна {2} из {3} · {4:0.0} с из {5:0}", battleTitle == "" ? "" : battleTitle + ": ", f.Name,
                battle.Wave + 1, battle.WaveCount, battle.Tick * combat.TickSeconds, combat.MaxTicks * combat.TickSeconds), Rich());
            GUILayout.Label(string.Format("Здоровье {0:0} / {1:0} {2}", Math.Max(0, battle.BossHp), battle.BossMaxHp, Bar(battle.BossHp / battle.BossMaxHp)));
            GUILayout.Label(string.Format("Оглушение {0:0}/100 {1}{2}{3}{4}", battle.Stagger, Bar(battle.Stagger / 100),
                battle.BossStunned ? " · <color=#fd5>ОГЛУШЁН</color>" : "", battle.Burning ? " · <color=#f84>горит</color>" : "",
                battle.WallUp ? " · <color=#8cf>Стена</color>" : ""), Rich());
            if (battle.Pending != null)
            {
                double left = (battle.Pending.ImpactTick - battle.Tick) * combat.TickSeconds;
                GUILayout.Label(string.Format("<size=20><color=#f55>!!! {0} через {1:0.0} с — {2}</color></size>",
                    MechanicName(battle.Pending.Mechanic), left, MechanicAnswer(battle.Pending.Mechanic)), Rich());
            }
            else
            {
                GUILayout.Label(" ");
            }

            foreach (var h in battle.Heroes)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(string.Format("{0} ({1}) {2:0}/{3:0} {4}", h.Name, ClassName(h.Class), Math.Max(0, h.Hp), h.MaxHp,
                    h.Alive ? Bar(h.Hp / h.MaxHp) : "пал"), GUILayout.Width(420));
                GUI.enabled = h.Ready && !battle.Over && !autoBattle;
                string label = SkillName(h.Class) + (h.Ready ? "" : string.Format(" ({0:0}%)", 100.0 * h.Charge / h.ChargeMax));
                if (GUILayout.Button(label, GUILayout.Width(220)))
                    battle.Command(BattleCommand.Skill(h.Id));
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            GUI.enabled = !battle.Over && !autoBattle && battle.Tick >= battle.RetreatReadyAt;
            if (GUILayout.Button("Отход передней линии", GUILayout.Height(40)))
                battle.Command(BattleCommand.Retreat());
            GUI.enabled = !battle.Over;
            bool auto = GUILayout.Toggle(autoBattle, "Автобой", "Button", GUILayout.Height(40));
            if (auto != autoBattle)
            {
                autoBattle = auto;
                battle.Brain = autoBattle ? Brains.AutoBattle() : null;
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            foreach (string line in battle.Log.Skip(Math.Max(0, battle.Log.Count - 6)))
                GUILayout.Label("   " + line);

            if (battleResult != null)
            {
                GUILayout.Label("<size=22><b>" + battleResult + "</b></size>", Rich());
                if (GUILayout.Button("Закрыть", GUILayout.Height(40)))
                {
                    message = battleResult;
                    battle = null;
                }
            }
        }

        static string Bar(double share)
        {
            int n = (int)Math.Round(Math.Max(0, Math.Min(1, share)) * 20);
            return "[" + new string('█', n) + new string('·', 20 - n) + "]";
        }

        static string StarText(int stars) => new string('★', stars) + new string('☆', 3 - stars);

        static string ClassName(HeroClass c)
        {
            switch (c)
            {
                case HeroClass.Guardian: return "Страж";
                case HeroClass.Swordsman: return "Мечник";
                case HeroClass.Cleaver: return "Секач";
                case HeroClass.Archer: return "Стрелок";
                case HeroClass.CoalPriest: return "Угольный жрец";
                case HeroClass.AshMage: return "Пепельный маг";
                default: return "житель";
            }
        }

        static string SkillName(HeroClass c)
        {
            switch (c)
            {
                case HeroClass.Guardian: return "Стена";
                case HeroClass.Swordsman: return "Ответный выпад";
                case HeroClass.Cleaver: return "Сокрушение";
                case HeroClass.Archer: return "Залп";
                case HeroClass.CoalPriest: return "Тёплые руки";
                case HeroClass.AshMage: return "Огненный снаряд";
                default: return "—";
            }
        }

        static string MechanicName(Mechanic m)
        {
            switch (m)
            {
                case Mechanic.Smash: return "Удар по передней линии";
                case Mechanic.Wave: return "Волна по всему отряду";
                default: return "Захват героя";
            }
        }

        static string MechanicAnswer(Mechanic m)
        {
            switch (m)
            {
                case Mechanic.Smash: return "ОТХОД или Стена";
                case Mechanic.Wave: return "СТЕНА или сбить Сокрушением";
                default: return "СТЕНА или сбить Сокрушением";
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
                case Fail.RulesNotMet: return "Не выполнены условия";
                case Fail.Locked: return "Ещё закрыто";
                case Fail.NoTracks: return "Попытки на сегодня кончились";
                case Fail.LimitReached: return "Лимит исчерпан";
                case Fail.HuntInProgress: return "Сначала закончите начатый бой";
                case Fail.NoHeroes: return "Нет героев: нужна казарма и герои";
                case Fail.NotToday: return "Пока нельзя: подождите";
                default: return fail.ToString();
            }
        }

        static string CostText(ResourceBag bag)
        {
            string[] names = { "уголь", "еда", "железо", "серебро", "эссенция", "искры", "жар", "сердцевины", "чертежи", "слава" };
            var parts = new System.Collections.Generic.List<string>();
            for (int i = 0; i < ResourceBag.Count; i++)
            {
                long amount = bag[(ResourceType)i];
                if (amount != 0)
                    parts.Add(names[i] + " " + amount);
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

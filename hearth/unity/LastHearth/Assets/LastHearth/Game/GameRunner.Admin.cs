using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LastHearth.Core;
using UnityEngine;

namespace LastHearth.Game
{
    /// <summary>
    /// The developer's menu, to test any part of the game on the phone. It is hidden: five quick taps on
    /// the settings gear, or a long press on the hearth's level badge, turn on the developer mode
    /// (remembered in PlayerPrefs) and open it; then the settings show it too. Its pages are lists of
    /// buttons; every button calls a core shortcut (GameSession.Admin.cs) and tells what happened.
    /// A new page is one line in AdminPages and a method that fills an AdminList.
    /// </summary>
    public sealed partial class GameRunner
    {
        const string DevModeKey = "lasthearth.devMode";

        /// <summary>One button: its text and what it does; returns the message shown after it.</summary>
        sealed class AdminAction
        {
            public string Title = "";
            public Func<long, string> Run;
            /// <summary>Asks "Точно?" first (resets and loads).</summary>
            public bool Confirm;
        }

        /// <summary>A line of the page: a centred caption over its buttons.</summary>
        sealed class AdminRow
        {
            public string Caption = "";
            public readonly List<AdminAction> Actions = new List<AdminAction>();
        }

        /// <summary>What a page fills: rows of buttons, built anew every frame (the captions show the live numbers).</summary>
        sealed class AdminList
        {
            public readonly List<AdminRow> Rows = new List<AdminRow>();
            /// <summary>A long text after the rows, in a field to select and copy (the save).</summary>
            public string Text;

            public AdminList Row(string caption = "")
            {
                Rows.Add(new AdminRow { Caption = caption });
                return this;
            }

            public AdminList Add(string title, Func<long, string> run, bool confirm = false)
            {
                if (Rows.Count == 0)
                    Row();
                Rows[Rows.Count - 1].Actions.Add(new AdminAction { Title = title, Run = run, Confirm = confirm });
                return this;
            }
        }

        /// <summary>The tabs of the menu, in order. Another system adds its page here (and its method in its own file).</summary>
        (string title, Action<AdminList, long> fill)[] AdminPages() => new (string, Action<AdminList, long>)[]
        {
            ("Ресурсы", AdminResources),
            ("Очаг", AdminFortress),
            ("Время", AdminTime),
            ("События", AdminEvents),
            ("Бои", AdminFights),
            ("Кампания", AdminCampaign), // GameRunner.Campaign.cs
            ("Герои", AdminHeroes),
            ("Залы", AdminHalls), // GameRunner.Rooms.cs
            ("Вылазки", AdminRoutes), // GameRunner.Routes.cs
            ("Жители", AdminPeople), // GameRunner.People.cs
            ("Предметы", AdminItems), // GameRunner.Gear.cs
            ("Сохранение", AdminSave),
            ("Арт", AdminArt),
            ("Обучение", AdminTutorial), // GameRunner.Tutorial.cs
        };

        static readonly string[] AdminResourceNames = { "Уголь", "Еда", "Железо", "Серебро", "Эссенция", "Искры", "Жар", "Сердцевины", "Чертежи", "Слава" };

        bool? devMode;
        int adminPage;
        string adminConfirm;
        string adminToast = "";
        float adminToastAt = -99;
        bool adminShowSave;
        int gearTaps;
        float gearTapAt = -99;
        float badgePressAt = -1;
        GUIStyle adminCaption;
        (string name, bool on)[] artFlagsAtStart;

        bool DevMode => devMode ?? (devMode = PlayerPrefs.GetInt(DevModeKey, 0) == 1).Value;

        void SetDevMode(bool on)
        {
            devMode = on;
            PlayerPrefs.SetInt(DevModeKey, on ? 1 : 0);
            PlayerPrefs.Save();
        }

        void OpenAdmin()
        {
            GUIUtility.hotControl = 0; // the finger that opened it presses nothing under it
            if (!DevMode)
            {
                SetDevMode(true);
                AdminToast("Режим разработчика включён");
            }
            Open(Win.Admin);
        }

        void AdminToast(string text)
        {
            adminToast = text ?? "";
            adminToastAt = Time.realtimeSinceStartup;
            message = adminToast; // the toast over the fortress too, once the window is closed
        }

        /// <summary>The hidden ways in, watched by the top bar: five quick taps on the gear, a long press on the badge.</summary>
        void AdminGestures(Rect badge, Rect gear)
        {
            var e = Event.current;
            float t = Time.realtimeSinceStartup;
            if (e.type == EventType.MouseDown && gear.Contains(e.mousePosition) && (window == Win.None || window == Win.Settings))
            {
                // the first tap opens the settings as usual; the gear still counts the taps over them
                gearTaps = t - gearTapAt > 0.8f ? 1 : gearTaps + 1;
                gearTapAt = t;
                if (gearTaps >= 5)
                {
                    gearTaps = 0;
                    e.Use();
                    OpenAdmin();
                    return;
                }
            }
            if (e.type == EventType.MouseDown && badge.Contains(e.mousePosition) && window == Win.None)
                badgePressAt = t;
            else if (e.type == EventType.MouseUp)
                badgePressAt = -1;
            if (badgePressAt >= 0 && e.type == EventType.Repaint)
            {
                if (!Input.GetMouseButton(0) || !badge.Contains(e.mousePosition) || window != Win.None)
                    badgePressAt = -1; // let go, or the finger slid off
                else if (t - badgePressAt >= 1.2f)
                {
                    badgePressAt = -1;
                    OpenAdmin();
                }
            }
        }

        /// <summary>The settings in developer mode: the way back to the menu and the switch.</summary>
        void DrawDevSettings()
        {
            if (!DevMode)
                return;
            GUILayout.Space(10);
            GUILayout.Label("<b>Режим разработчика</b> (тестовая сборка)", Rich());
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Админ-меню"))
                Open(Win.Admin);
            if (GUILayout.Button("Режим разработчика: вкл"))
            {
                SetDevMode(false);
                message = "Режим разработчика выключен";
            }
            GUILayout.EndHorizontal();
        }

        // ------------------------------------------------------------------ the window

        void DrawAdmin(long now)
        {
            if (adminCaption == null)
                adminCaption = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true, wordWrap = true, fontStyle = FontStyle.Bold };
            var pages = AdminPages();
            adminPage = Mathf.Clamp(adminPage, 0, pages.Length - 1);
            // the tabs, in as many lines as the width needs
            int perLine = Math.Max(1, Math.Min(pages.Length, (int)(viewWidth / 104)));
            for (int i = 0; i < pages.Length; i += perLine)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Math.Min(pages.Length, i + perLine); j++)
                {
                    if (GUILayout.Toggle(adminPage == j, pages[j].title, "Button", GUILayout.Height(34)) && adminPage != j)
                    {
                        adminPage = j;
                        adminConfirm = null;
                        windowScroll = Vector2.zero;
                    }
                }
                GUILayout.EndHorizontal();
            }
            DrawAdminToast();
            var list = new AdminList();
            pages[adminPage].fill(list, now);
            foreach (var row in list.Rows)
            {
                GUILayout.Space(4);
                if (row.Caption.Length > 0)
                    GUILayout.Label(row.Caption, adminCaption);
                DrawAdminButtons(row, now);
            }
            if (list.Text != null)
                GUILayout.TextArea(list.Text, GUILayout.Width(viewWidth));
        }

        /// <summary>What the last button did, on a plate under the tabs; it fades after a few seconds.</summary>
        void DrawAdminToast()
        {
            var r = GUILayoutUtility.GetRect(viewWidth, 32);
            float age = Time.realtimeSinceStartup - adminToastAt;
            if (adminToast.Length == 0 || age > 5f)
                return;
            var old = GUI.color;
            GUI.color = new Color(1, 1, 1, Mathf.Clamp(5f - age, 0, 1));
            var plate = new Rect(r.x + 20, r.y + 2, r.width - 40, r.height - 4);
            ArtLibrary.DrawSliced(plate, "UI/Pill", 30);
            HudStyles();
            UiSkin.Outlined(plate, StripColor(adminToast), buttonLabel);
            GUI.color = old;
        }

        void DrawAdminButtons(AdminRow row, long now)
        {
            int count = row.Actions.Count;
            if (count == 0)
                return;
            int perLine = Math.Max(1, Math.Min(count, (int)(viewWidth / 132)));
            float width = (viewWidth - 8 * (perLine - 1)) / perLine;
            for (int i = 0; i < count; i += perLine)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Math.Min(count, i + perLine); j++)
                {
                    var a = row.Actions[j];
                    string key = row.Caption + "|" + a.Title;
                    bool asking = a.Confirm && adminConfirm == key;
                    if (!GUILayout.Button(asking ? "Точно? " + a.Title : a.Title, GUILayout.Width(width), GUILayout.Height(36)))
                        continue;
                    if (a.Confirm && !asking)
                    {
                        adminConfirm = key;
                        AdminToast("Нажмите ещё раз, чтобы подтвердить: " + a.Title);
                        continue;
                    }
                    adminConfirm = null;
                    sounds?.Play("Click", 0.4f);
                    AdminToast(a.Run(now));
                    if (session != null)
                        Save();
                }
                GUILayout.EndHorizontal();
            }
        }

        static string AdminResName(ResourceType t) => AdminResourceNames[(int)t];


        string LocalClock(long now) => DateTimeOffset.FromUnixTimeSeconds(now + session.State.UtcOffsetSeconds).ToString("dd.MM HH:mm");

        // ------------------------------------------------------------------ pages

        void AdminResources(AdminList list, long now)
        {
            var st = session.State;
            for (int i = 0; i < ResourceBag.Count; i++)
            {
                var t = (ResourceType)i;
                long cap = session.Capacity(t);
                list.Row(string.Format("{0}: {1}{2}", AdminResName(t), ShortNum(st.Wallet[t]), cap == long.MaxValue ? "" : " из " + ShortNum(cap)));
                foreach (long amount in new long[] { 100, 1000 })
                    list.Add("+" + amount, _ => string.Format("{0} {1} → {2}", AdminResName(t), Signed(amount), session.AdminAddResource(t, amount)));
                list.Add("+макс", _ => string.Format("{0}: {1}", AdminResName(t), session.AdminMaxResource(t)));
                list.Add("Обнулить", _ =>
                {
                    session.AdminZeroResource(t);
                    return AdminResName(t) + ": 0";
                });
            }
            list.Row(string.Format("Склад и прочее · песочные часы {0} · ждут получения: {1}", Duration(st.HourglassSeconds),
                st.Rewards.IsEmpty() ? "ничего" : CostText(st.Rewards)));
            list.Add("Заполнить склад", _ =>
            {
                session.AdminFillStorage();
                return "Склад полон: серебро, железо, уголь и еда";
            });
            list.Add("+1 ч песочных часов", _ => "Песочные часы: " + Duration(session.AdminAddHourglass(3600)));
            list.Add("Обнулить всё", _ =>
            {
                session.AdminZeroAll();
                return "Все ресурсы, награды и часы обнулены";
            }, true);
        }

        void AdminFortress(AdminList list, long now)
        {
            var st = session.State;
            int max = session.Balance.MaxHearthLevel;
            list.Row(string.Format("Очаг: уровень {0} из {1} · строителей {2}", st.HearthLevel, max, session.BuilderCount()));
            foreach (var (title, level) in new[] { ("−1", st.HearthLevel - 1), ("+1", st.HearthLevel + 1), ("+5", st.HearthLevel + 5), ("1", 1), ("10", 10), ("20", 20), ("Макс.", max) })
            {
                list.Add("Очаг " + title, t =>
                {
                    int from = session.State.HearthLevel;
                    int to = session.AdminSetHearthLevel(level, t);
                    return string.Format("Очаг: {0} → {1} · жителей {2} · строителей {3}", from, to, session.State.Dwellers.Count, session.BuilderCount());
                });
            }
            int jobs = st.Queue.AllJobs().Count();
            list.Row(string.Format("Стройка: работ в очереди {0} · пар в покоях {1} · отрядов в пути {2}", jobs, st.Pairings.Count, st.Expeditions.Count(e => !e.Back)));
            list.Add("Завершить все стройки", t => "Завершено работ: " + session.AdminFinishAllJobs(t));
            list.Add("Роды, вылазки, раны — сейчас", t => "Завершено таймеров: " + session.AdminFinishOtherTimers(t));
            list.Row("Гора");
            list.Add("Убрать все завалы", t => "Расчищено клеток: " + session.AdminClearAllObstacles(t));
            list.Add("Открыть все секции", t => "Открыто клеток: " + session.AdminUnlockAllSections(t));
            list.Row(string.Format("Жители: {0} из {1}", st.Dwellers.Count, Formulas.DwellerCap(session.Balance, st.HearthLevel)));
            foreach (int n in new[] { 1, 5 })
                list.Add("+" + n + (n == 1 ? " житель" : " жителей"), _ => "Пришли: " + string.Join(", ", session.AdminAddDwellers(n).Select(d => d.Name)));
        }

        void AdminTime(AdminList list, long now)
        {
            list.Row(string.Format("Часы игры: {0} · сдвиг от настоящих {1} · прожито всё по часу: стройка, выработка, Наплывы, дни, недели, события",
                LocalClock(now), Duration(Math.Max(0, skip))));
            foreach (var (title, seconds) in new[] { ("+1 час", 3600L), ("+8 часов", 8 * 3600L), ("+1 день", 86400L), ("+7 дней", 7 * 86400L) })
            {
                list.Add(title, t =>
                {
                    long to = session.AdminSkipTime(seconds, t);
                    skip += to - t; // the runner's clock follows the world
                    KennelNews();
                    return string.Format("{0}: сейчас {1}", title, LocalClock(to));
                });
            }
        }

        void AdminEvents(AdminList list, long now)
        {
            var st = session.State;
            list.Row(string.Format("Наплыв: {0} · задания дня: {1}/{2}", session.State.SurgeLive ? "идёт" : st.SurgeDay == session.LocalDay(now) ? "сегодня отбит" : "сегодня ещё не было",
                session.TasksDone(now), session.TodayTasks(now).Count));
            list.Add("Наплыв сейчас", t =>
            {
                var fail = session.AdminStartSurge(t, out var started);
                if (fail != Fail.None)
                    return "Наплыв: " + FailText(fail);
                CloseWindow();
                BeginSurge(started, done =>
                {
                    session.FinishSurge(done, Now(), out var r);
                    return string.Format("{0} · +{1} жара · +{2} эссенции", StarText(r.Stars), r.Ember, r.Essence);
                });
                return "Наплыв начался";
            });
            list.Add("Задания дня заново", t => "Новые задания: " + string.Join(", ", session.AdminResetDaily(t).Select(session.TaskText)));
            list.Add("Выполнить задания дня", t =>
            {
                session.AdminCompleteDaily(t);
                return "Задания выполнены: сундук ждёт в «Заданиях»";
            });
            foreach (var e in session.AdminEvents())
            {
                var run = session.ActiveRun(e.Id, now);
                string status = run == null
                    ? (session.OnCalendar(e, session.LocalDay(now)) ? "по календарю сегодня, но не идёт" : "не идёт")
                    : string.Format("идёт{0}: {1:0}/{2:0} очков, порогов {3}/10, ещё {4}", run.Personal ? " (личный день)" : "", run.Points,
                        session.AdminEventTarget(run), run.Thresholds, Duration(run.End - now));
                string locked = st.HearthLevel < e.Unlock ? " · по правилам с " + e.Unlock + "-го ур. очага" : "";
                list.Row(string.Format("<b>{0}</b> ({1}, {2} дн.): {3}{4}", e.Name, e.Id, e.Days, status, locked));
                string id = e.Id, name = e.Name;
                list.Add("Начать", t => name + ": " + (session.AdminStartEvent(id, t) == Fail.None ? "началось" : "уже идёт"));
                list.Add("Последний этап", t => name + ": " + (session.AdminEventLastStage(id, t) == Fail.None
                    ? string.Format("последний этап, порогов {0}/10", session.ActiveRun(id, t).Thresholds) : "сначала начните"));
                list.Add("Награды", t =>
                {
                    bool running = session.ActiveRun(id, t) != null;
                    int paid = session.AdminGrantEventRewards(id, t);
                    return name + ": " + (!running ? "сначала начните" : paid == 0 ? "все награды уже получены" : "выдано наград порогов: " + paid);
                });
                list.Add("Завершить", t =>
                {
                    if (session.AdminEndEvent(id, t) != Fail.None)
                        return name + ": не идёт";
                    return session.State.Events.LastOrDefault(r => r.Id == id && r.Closed)?.LastResult ?? name + ": завершено";
                });
            }
        }

        void AdminFights(AdminList list, long now)
        {
            var st = session.State;
            int missions = session.Balance.Campaign.Missions.Count;
            list.Row(string.Format("Поход: пройдено {0} из {1}", session.MissionsCleared, missions));
            list.Add("Победа в следующей", t =>
            {
                var fail = session.AdminWinNextMission(t, out var reward);
                if (fail != Fail.None)
                    return "Поход: " + FailText(fail);
                return string.Format("Миссия {0} пройдена ★★★ · {1}{2}", session.MissionsCleared, CostText(reward.Got),
                    reward.NewHero != null ? " · новый герой: " + reward.NewHero.Name : "");
            });
            list.Add("+10 миссий", _ => "Пройдено миссий: " + session.AdminSetCampaignProgress(session.MissionsCleared + 10));
            list.Add("Весь поход", _ => "Пройдено миссий: " + session.AdminSetCampaignProgress(missions));
            list.Add("Поход с начала", _ => "Пройдено миссий: " + session.AdminSetCampaignProgress(0), true);
            var pvp = session.Balance.Activities.Pvp;
            list.Row(string.Format("Арена{0}: рейтинг {1}, лига «{2}» · билетов {3}/{4} · флагов набега {5}/{6}", session.ArenaOpen ? "" : " (по правилам с " + pvp.ArenaUnlock + "-го ур.)",
                st.ArenaRating == 0 ? pvp.StartRating : st.ArenaRating, session.League.Name, session.ArenaTickets(now), pvp.Tickets, session.RaidFlags(now), pvp.Flags));
            list.Add("Билеты и флаги", t => "Билетов арены: " + session.AdminRefillArena(t) + ", флагов: " + session.RaidFlags(t));
            list.Add("Новые соперники", t =>
            {
                session.AdminRerollOpponents(t);
                return "Соперники: " + string.Join(", ", session.ArenaOpponents(t).Select(o => o.Name + " " + o.Rating));
            });
            list.Add("Рейтинг −100", _ => "Рейтинг: " + session.AdminSetArenaRating((st.ArenaRating == 0 ? pvp.StartRating : st.ArenaRating) - 100));
            list.Add("Рейтинг +100", _ => "Рейтинг: " + session.AdminSetArenaRating((st.ArenaRating == 0 ? pvp.StartRating : st.ArenaRating) + 100));
            list.Row("Лига");
            for (int i = 0; i < pvp.Leagues.Count; i++)
            {
                int league = i;
                list.Add(pvp.Leagues[i].Name, _ => string.Format("Лига «{0}», рейтинг {1}", pvp.Leagues[session.AdminSetLeague(league)].Name, session.State.ArenaRating));
            }
        }

        void AdminHeroes(AdminList list, long now)
        {
            var st = session.State;
            var heroes = st.Dwellers.Where(d => d.IsHero).ToList();
            list.Row(string.Format("Новый герой · героев {0}, предел уровня {1}", heroes.Count, Formulas.HeroLevelCap(session.Balance, st.HearthLevel)));
            foreach (HeroClass c in Enum.GetValues(typeof(HeroClass)))
            {
                if (c == HeroClass.None)
                    continue;
                list.Add(ClassName(c), _ =>
                {
                    var d = session.AdminAddHero(c);
                    return string.Format("Новый герой: {0}, {1}", d.Name, ClassName(c));
                });
            }
            list.Row("Уровни героев");
            list.Add("Все герои на максимум", _ => "Героев на пределе: " + session.AdminMaxAllHeroes());
            foreach (var d in heroes)
            {
                int id = d.Id;
                list.Add(string.Format("{0} ({1}) ур. {2}", d.Name, ClassName(d.HeroClass), d.HeroLevel), _ =>
                {
                    var h = session.FindDweller(id);
                    return string.Format("{0}: уровень {1}, умение {2}", h.Name, session.AdminMaxHero(id), h.SkillLevel);
                });
            }
            int pets = session.Balance.Pets.Kinds.Count(k => session.HasPet(k.Id));
            int companions = session.Balance.Companions.Kinds.Count(k => session.CompanionUnlocked(k.Id));
            list.Row(string.Format("Питомцы {0}/{1} · компаньоны {2}/{3} · лакомства {4}", pets, session.Balance.Pets.Kinds.Count, companions,
                session.Balance.Companions.Kinds.Count, session.Treats));
            list.Add("Открыть всех", _ =>
            {
                var found = session.AdminUnlockKennel();
                return found.Count == 0 ? "Все питомцы и компаньоны уже есть" : "Открыты: " + string.Join(", ", found);
            });
            list.Add("Питомцы на макс.", _ => "Питомцев на высшем уровне: " + session.AdminMaxPets());
            foreach (long n in new long[] { 100, 1000 })
                list.Add("+" + n + " лакомств", _ => "Лакомства: " + session.AdminAddTreats(n));
        }

        void AdminSave(AdminList list, long now)
        {
            string json = session.AdminExportSave();
            list.Row(string.Format("Сохранение: {0} КБ · {1}", json.Length / 1024, File.Exists(savePath) ? "файл " + savePath : "файла ещё нет"));
            list.Add("Копировать в буфер", _ =>
            {
                GUIUtility.systemCopyBuffer = session.AdminExportSave();
                return "Сохранение скопировано: " + GUIUtility.systemCopyBuffer.Length + " символов";
            });
            list.Add(adminShowSave ? "Скрыть текст" : "Показать текст", _ =>
            {
                adminShowSave = !adminShowSave;
                return adminShowSave ? "Текст сохранения — внизу страницы" : "Текст скрыт";
            });
            list.Add("Сохранить сейчас", _ => "Сохранено");
            list.Row("Загрузка (несохранённое пропадёт)");
            list.Add("Перезагрузить с диска", _ =>
            {
                var saved = File.Exists(savePath) ? SaveSystem.FromJson(File.ReadAllText(savePath)) : null;
                if (saved == null)
                    return "Файла сохранения нет или он испорчен";
                AdminLoad(saved);
                return "Сохранение перечитано с диска";
            }, true);
            list.Add("Загрузить из буфера", _ =>
            {
                var saved = SaveSystem.FromJson(GUIUtility.systemCopyBuffer ?? "");
                if (saved == null)
                    return "В буфере нет целого сохранения";
                AdminLoad(saved);
                return "Сохранение загружено из буфера";
            }, true);
            list.Row("Сброс");
            list.Add("Новая игра (сброс)", _ =>
            {
                skip = 0;
                StartSession(NewGame.Create(session.Balance, Now()));
                CloseWindow();
                return "Новая игра";
            }, true);
            list.Add("Выключить режим разработчика", _ =>
            {
                SetDevMode(false);
                CloseWindow();
                return "Режим разработчика выключен";
            });
            if (adminShowSave)
            {
                list.Row("Текст сохранения (выделите и скопируйте)");
                list.Text = json;
            }
        }

        void AdminLoad(GameState saved)
        {
            // the clock must not run behind the save: the offset covers the difference
            long real = Now() - skip;
            skip = Math.Max(0, saved.Time - real);
            StartSession(new GameSession(session.Balance, saved));
            CloseWindow();
        }

        void AdminArt(AdminList list, long now)
        {
            var d = ArtLibrary.Data;
            var flags = new (string name, Func<bool> get, Action<bool> set)[]
            {
                ("Framed", () => d.Framed, v => d.Framed = v),
                ("Passages", () => d.Passages, v => d.Passages = v),
                ("Stairs", () => d.Stairs, v => d.Stairs = v),
                ("Scene", () => d.Scene, v => d.Scene = v),
                ("RoomTiers", () => d.RoomTiers, v => d.RoomTiers = v),
                ("Rubble", () => d.Rubble, v => d.Rubble = v),
                ("Scaffold", () => d.Scaffold, v => d.Scaffold = v),
                ("Splash", () => d.Splash, v => d.Splash = v),
            };
            if (artFlagsAtStart == null)
                artFlagsAtStart = flags.Select(f => (f.name, f.get())).ToArray();
            list.Row("Флаги Art.json — до перезапуска игры, чтобы сравнить виды");
            foreach (var f in flags)
            {
                var flag = f;
                list.Add(flag.name + (flag.get() ? ": вкл" : ": выкл"), _ =>
                {
                    flag.set(!flag.get());
                    framed = false; // the fortress is framed anew: the stair tower changes its width
                    return flag.name + (flag.get() ? " включён" : " выключен");
                });
            }
            list.Row();
            list.Add("Как в Art.json", _ =>
            {
                foreach (var f in flags)
                    f.set(artFlagsAtStart.First(a => a.name == f.name).on);
                framed = false;
                return "Флаги — как в Art.json";
            });
        }
    }
}

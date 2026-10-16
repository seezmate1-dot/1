using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LastHearth.Core;
using LastHearth.Core.Combat;
using UnityEngine;

namespace LastHearth.Game
{
    /// <summary>
    /// Starts the game in any scene. The fortress fills the screen and lives in real time, saves itself
    /// and catches up offline time on the next start; every part of the game opens in a window over it
    /// (GameRunner.Hud.cs): fights (campaign, Hunt, arena, raids), the Surge, dwellers, growth, routes,
    /// tasks, the shop, live events, settings, and a hidden developer's menu to test it all (GameRunner.Admin.cs).
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

        // phone: scale by screen density, scroll with a finger, big buttons
        Vector2 fightScroll;
        float uiScale = 1;
        bool dragging;
        float dragDistance;
        int releaseFrames;
        bool styled;
        float saveTimer;
        string shownMessage = "";
        AudioDirector sounds;
        // what the fighters on the battle stage are doing (screen only)
        readonly Dictionary<int, float> heroAttackAt = new Dictionary<int, float>();
        readonly Dictionary<int, float> heroHitAt = new Dictionary<int, float>();
        readonly Dictionary<int, int> heroLastNext = new Dictionary<int, int>();
        readonly Dictionary<int, double> heroLastHp = new Dictionary<int, double>();
        float enemyAttackAt = -9, enemyHitAt = -9, battleOverAt = -1;
        double lastBossHp;
        float messageTime;

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
            ApplyFrameRate(); // 60 FPS, 30 with «Экономия батареи» (GameRunner.Comfort.cs)
            sounds = new AudioDirector(gameObject);
            skip = long.Parse(PlayerPrefs.GetString(SkipKey, "0"));
            var balance = Core.Balance.FromJson(UnityEngine.Resources.Load<TextAsset>("balance").text);
            savePath = Path.Combine(Application.persistentDataPath, "save.json");
            combat = CombatBalance.FromJson(UnityEngine.Resources.Load<TextAsset>("combat").text);
            BeginLoading(balance); // the save is read behind the loading screen (LoadingScreen.cs)
        }

        void StartSession(GameSession s)
        {
            session = s;
            session.Combat = combat;
            TutorHook(s); // the first session's guide (GameRunner.Tutorial.cs)
            session.State.UtcOffsetSeconds = (int)TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalSeconds;
            session.JobCompleted += job =>
            {
                message = job.IsClearing ? "Завал расчищен: здесь можно строить"
                    : job.IsHearth && session.SectionOpensAt(job.ToLevel) ? "Очаг " + job.ToLevel + ": открылась новая часть горы"
                    : "Готово: " + JobName(job) + " → ур. " + job.ToLevel;
                sounds?.Play("Done");
            };
            session.ChildBorn += child =>
            {
                message = "В жилых покоях родился ребёнок: " + child.Name;
                sounds?.Play("Done");
            };
            session.SurgeSettled += r => message = r.Live ? "" : string.Format("Наплыв отбит без вас: {0}, +{1} жара", StarText(r.Stars), r.Ember);
            // the time away is caught up and written down for «Пока вас не было» (GameRunner.Retention.cs)
            var summary = session.ReturnAfterAbsence(Now());
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
            ShowAway(summary);
        }

        void BeginBattle(Battle started, Func<Battle, string> finish, string title = "")
        {
            battle = started;
            battleTitle = title;
            battleFinish = finish;
            battleResult = null;
            battleClock = 0;
            battle.Brain = autoBattle ? Brains.AutoBattle() : null;
            heroAttackAt.Clear();
            heroHitAt.Clear();
            heroLastNext.Clear();
            heroLastHp.Clear();
            enemyAttackAt = enemyHitAt = -9;
            battleOverAt = -1;
            lastBossHp = started.BossHp;
            sounds?.Play("Blade");
        }

        void Update()
        {
            if (LoadingTick())
                return;
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
            {
                battleResult = battleFinish(battle);
                battleOverAt = Time.time;
                sounds?.Play(battle.Won ? "Victory" : "Defeat", 0.7f);
            }
            UpdateWalkers(Time.unscaledDeltaTime);
            sounds?.Tick(battle != null || surge != null, window == Win.None);
            UpdateSurge();
            UpdateLanterns();
            UpdateTouchScroll();
            if (Input.GetKeyDown(KeyCode.Escape))
                Back();
            bool live = battle != null || surge != null || lanternSeed != 0;
            int sleep = live ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
            if (Screen.sleepTimeout != sleep)
                Screen.sleepTimeout = sleep;
            tick += Time.unscaledDeltaTime;
            if (tick >= 1f)
            {
                tick = 0;
                session.Advance(Now());
                KennelNews();
            }
            saveTimer += Time.unscaledDeltaTime;
            if (saveTimer >= 30f)
            {
                saveTimer = 0;
                Save(); // a phone may kill the game at any moment
            }
        }

        ref Vector2 ActiveScroll()
        {
            if (battle != null || surge != null || lanternSeed != 0)
                return ref fightScroll;
            if (window != Win.None)
                return ref windowScroll;
            if (onMap)
                return ref mapScroll;
            return ref scroll;
        }

        /// <summary>IMGUI scroll views do not follow a finger: drag them by hand and swallow the tap that ends a drag.</summary>
        void UpdateTouchScroll()
        {
            if (releaseFrames > 0 && --releaseFrames == 0)
                dragging = false;
            if (Input.touchCount != 1 || lanternSeed != 0)
                return;
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
            {
                dragDistance = 0;
                dragging = false;
            }
            else if (t.phase == TouchPhase.Moved)
            {
                dragDistance += t.deltaPosition.magnitude;
                if (dragDistance > 20 * uiScale)
                    dragging = true;
                if (dragging)
                {
                    ref Vector2 sv = ref ActiveScroll();
                    sv.y = Mathf.Max(0, sv.y + t.deltaPosition.y / uiScale);
                    sv.x = Mathf.Max(0, sv.x - t.deltaPosition.x / uiScale);
                }
            }
            else if ((t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) && dragging)
            {
                releaseFrames = 2;
            }
        }

        /// <summary>The Android back button: close a finished fight, go back to the fortress, then leave.</summary>
        void Back()
        {
            if (battle != null)
            {
                if (battleResult != null)
                {
                    message = battleResult;
                    battle = null;
                }
                return;
            }
            if (surge != null)
            {
                if (surgeResult != null)
                {
                    message = surgeResult;
                    surge = null;
                }
                return;
            }
            if (lanternSeed != 0)
                return;
            if (movingRoom >= 0)
            {
                CancelMove();
                return;
            }
            if (window != Win.None)
            {
                CloseWindow();
                return;
            }
            if (onMap)
            {
                onMap = false;
                return;
            }
            Save();
            Application.Quit();
        }

        /// <summary>Screen density decides the size: a finger needs about 7 mm per button.</summary>
        static float UiScale()
        {
            float byHeight = Screen.height / 900f;
            float byDensity = Screen.dpi > 0 ? Screen.dpi / 190f : 0;
            return Mathf.Max(1f, Mathf.Max(byHeight, byDensity));
        }

        static void Style()
        {
            GUI.skin.label.fontSize = 14;
            GUI.skin.label.wordWrap = true;
            GUI.skin.button.fontSize = 14;
            GUI.skin.button.padding = new RectOffset(10, 10, 9, 9);
            GUI.skin.verticalScrollbar.fixedWidth = 14;
            GUI.skin.verticalScrollbarThumb.fixedWidth = 14;
            UiSkin.Apply();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                PlanNotifications(); // at most three a day (GameRunner.Retention.cs)
                Save();
            }
            else
            {
                OnReturn(); // back after a while: «Пока вас не было»
            }
        }

        void OnApplicationQuit()
        {
            PlanNotifications();
            Save();
        }

        void Save()
        {
            if (session == null)
                return; // still loading
            File.WriteAllText(savePath, SaveSystem.ToJson(session.State));
            PlayerPrefs.SetString(SkipKey, skip.ToString());
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------ debug screen

        void OnGUI()
        {
            if (!styled)
            {
                Style();
                styled = true;
            }
            if (dragging && (Event.current.type == EventType.MouseUp || Event.current.type == EventType.MouseDrag))
            {
                GUIUtility.hotControl = 0;
                Event.current.Use(); // the finger scrolled: this is not a tap
            }
            uiScale = UiScale();
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1));
            if (DrawLoading())
                return;
            TutorInput(); // taps on the tutorial's bubble are its own (GameRunner.Tutorial.cs)
            MoveInput(); // and the «Отмена» of a room being moved (GameRunner.Comfort.cs)
            UiSkin.DrawBackground(new Rect(0, 0, Screen.width / uiScale, Screen.height / uiScale));
            // the safe area keeps buttons away from the camera cut-out and rounded corners
            var safe = Screen.safeArea;
            float top = Screen.height - safe.yMax;
            var area = new Rect(safe.x / uiScale + 6, top / uiScale + 4, safe.width / uiScale - 12, safe.height / uiScale - 8);
            if (battle != null || surge != null || lanternSeed != 0)
            {
                GUILayout.BeginArea(area);
                viewWidth = area.width - 24; // minus the scroll bar
                fightScroll = GUILayout.BeginScrollView(fightScroll);
                if (battle != null)
                    DrawBattle();
                else if (surge != null)
                    DrawSurge();
                else
                    DrawLanterns();
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }
            long now = Now();
            if (onMap)
                DrawWorldMap(area, now);
            else
                DrawMainScreen(area, now);
            DrawMoveBar(area);
            AnimWindow(area, now); // DrawWindow with its pop (GameRunner.Anim.cs)
            DrawTutorial(area); // Mara, the dimming and the arrow of the tutorial (GameRunner.Tutorial.cs)
            DrawFx();
        }

        static string ShortNum(double v)
        {
            if (v >= 1000000)
                return (v / 1000000).ToString("0.#") + "M";
            if (v >= 10000)
                return (v / 1000).ToString("0") + "K";
            return v.ToString("0");
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
            if (TutorButton("hearthUpgrade", title))
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
                    GUILayout.Label("   " + JobName(job) + (job.IsClearing ? "" : " → " + job.ToLevel) +
                                    (job.IsRunning(now) ? "   осталось " + Duration(left) : "   ждёт"));
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
                GUILayout.Label(string.Format("{0} ур.{1}{2} (этаж {3}, работников {4}){5}{6}",
                    data.Name, room.Level, room.Width > 1 ? " ×" + room.Width : "", room.Floor + 1, room.Workers.Count, output, cold), Rich());
                if (data.Produces != null && GUILayout.Button("Собрать", GUILayout.Width(90)))
                    Collecting(() => session.Collect(room.Id));
                if (room.Level < session.State.HearthLevel && !session.State.Queue.IsQueued(room.Id) &&
                    GUILayout.Button("Улучшить: " + CostText(session.RoomUpgradeCost(room)), GUILayout.Width(260)))
                    Report(session.UpgradeRoom(room.Id, now));
                GUILayout.EndHorizontal();
            }
        }

        void DrawDwellers()
        {
            var producers = session.State.Rooms.Where(r => session.Balance.Room(r.Type).Stat != null).ToList();
            foreach (var d in session.State.Dwellers.ToList())
            {
                GUILayout.BeginHorizontal();
                var face = GUILayoutUtility.GetRect(64, 64, GUILayout.Width(64));
                ArtLibrary.Draw(face, "Portraits/" + (d.IsHero ? ArtLibrary.HeroSheet(d.HeroClass) : ArtLibrary.DwellerSheet(d.Id)));
                var room = session.FindRoom(d.RoomId);
                string where = room == null ? "<color=#f9a>без работы</color>" : session.Balance.Room(room.Type).Name;
                string role = d.IsHero ? string.Format("{0}, уровень {1}, умение {2} из {3}", ClassName(d.HeroClass), d.HeroLevel, d.SkillLevel, session.SkillCap(d))
                    : where;
                var stats = string.Join("  ", Enum.GetValues(typeof(Stat)).Cast<Stat>().Select(st => StatName(st) + " " + d.StatOf(st)));
                GUILayout.Label(string.Format("<b>{0}</b> ({1}) · {2}\n<size=12><color=#d8c8a8>{3}</color></size>", d.Name,
                    d.Sex == Sex.Female ? "жен." : "муж.", role, stats), Rich());
                if (producers.Count > 0 && GUILayout.Button("Перевести", GUILayout.Width(110)))
                {
                    int index = room == null ? 0 : (producers.IndexOf(room) + 1) % producers.Count;
                    Report(session.Assign(d.Id, producers[index].Id));
                }
                if (!d.IsHero && GUILayout.Button(promoteFor == d.Id ? "Отмена" : "Сделать героем", GUILayout.Width(150)))
                    promoteFor = promoteFor == d.Id ? -1 : d.Id;
                if (d.IsHero && GUILayout.Button("Карточка", GUILayout.Width(110)))
                {
                    heroCard = d.Id;
                    Open(Win.Hero);
                }
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
            // Sunday: the monster of the week (ACTIVITIES.md, 3)
            if (session.WeeklyMonster(now) is MonsterFamily weekly)
            {
                var f = combat.Family(weekly);
                var w = hunt.Weekly;
                if (GUILayout.Button(string.Format("Чудовище недели: {0}\nсильнее, ×{1:0.#} сердцевин за первую победу", f.Name, w.CoreMult), GUILayout.Height(48)))
                {
                    var fail = session.StartHunt(weekly, huntRank, now, out var started, weekly: true);
                    Report(fail);
                    if (fail == Fail.None)
                    {
                        BeginBattle(started, done =>
                        {
                            session.FinishHunt(done, out var reward);
                            return string.Format("{0} {1} · +{2} сердцевин{3}", done.Won ? "Победа" : "Поражение", StarText(reward.Stars), reward.Cores,
                                reward.Weekly ? " (чудовище недели)" : "");
                        }, "Чудовище недели");
                    }
                }
            }
        }

        /// <summary>The fight on a stage: heroes on the left, the creature on the right, all animated.</summary>
        void DrawBattleStage()
        {
            float w = viewWidth;
            var stage = GUILayoutUtility.GetRect(w, w * 0.34f);
            // a clearing at the edge of the forest (hearth/tools/art/world.py); the path is 0.88 of it high
            ArtLibrary.Draw(new Rect(stage.x, stage.y - w * 0.12f, w, w * 0.5f), ArtLibrary.Tex("Battlefield") != null ? "Battlefield" : "Mountain");
            float now = Time.time;
            float ground = stage.yMax - stage.height * 0.06f;
            float heroH = stage.height * 0.42f;
            // detect what happened since the last frame: who struck, who was hurt
            foreach (var h in battle.Heroes)
            {
                if (heroLastNext.TryGetValue(h.Id, out int next) && h.NextAttack != next)
                    heroAttackAt[h.Id] = now;
                heroLastNext[h.Id] = h.NextAttack;
                if (heroLastHp.TryGetValue(h.Id, out double hp) && h.Hp < hp - 0.5)
                {
                    heroHitAt[h.Id] = now;
                    enemyAttackAt = now;
                }
                heroLastHp[h.Id] = h.Hp;
            }
            if (battle.BossHp < lastBossHp - 0.5)
                enemyHitAt = now;
            lastBossHp = battle.BossHp;
            bool over = battle.Over;
            for (int i = 0; i < battle.Heroes.Count; i++)
            {
                var h = battle.Heroes[i];
                int rank = (int)h.Line; // the front line stands closest to the enemy
                float x = stage.x + w * (0.36f - rank * 0.11f) - (i % 2) * w * 0.035f;
                float y = ground - (i % 2) * stage.height * 0.06f;
                string row;
                float t;
                if (!h.Alive)
                {
                    row = "die";
                    t = now - (heroHitAt.TryGetValue(h.Id, out var hd) ? hd : now - 2);
                    ArtLibrary.DrawCharacter(ArtLibrary.HeroSheet(h.Class), row, t, new Vector2(x, y), heroH, false, 9f, true);
                    continue;
                }
                if (over && battle.Won)
                {
                    row = "cheer";
                    t = now - battleOverAt;
                }
                else if (heroHitAt.TryGetValue(h.Id, out var hit) && now - hit < 0.35f)
                {
                    row = "hit";
                    t = now - hit;
                }
                else if (heroAttackAt.TryGetValue(h.Id, out var at) && now - at < 0.8f)
                {
                    row = "attack";
                    t = now - at;
                }
                else
                {
                    row = "idle";
                    t = now + i;
                }
                ArtLibrary.DrawCharacter(ArtLibrary.HeroSheet(h.Class), row, t, new Vector2(x, y), heroH, false, 10f);
            }
            float enemyH = stage.height * (battle.WaveCount == 1 ? 0.78f : 0.6f);
            var feet = new Vector2(stage.x + w * 0.74f, ground);
            string sheet = ArtLibrary.CreatureSheet(battle.Family.Family);
            if (over && battle.Won)
                ArtLibrary.DrawCharacter(sheet, "die", now - battleOverAt, feet, enemyH, true, 9f, true);
            else if (now - enemyHitAt < 0.3f)
                ArtLibrary.DrawCharacter(sheet, "hit", now - enemyHitAt, feet, enemyH, true, 12f);
            else if (now - enemyAttackAt < 0.8f)
                ArtLibrary.DrawCharacter(sheet, "attack", now - enemyAttackAt, feet, enemyH, true, 10f);
            else
                ArtLibrary.DrawCharacter(sheet, "walk", now * 0.6f, feet, enemyH, true, 6f);
            if (battle.Pending != null && !over)
                GUI.Label(new Rect(feet.x - 20, feet.y - enemyH * 1.25f, 60, 50), "<size=40><color=#f33><b>!</b></color></size>", Rich());
            if (battle.BossHp < lastBossHp + 1 && enemyHitAt == now)
                sounds?.Play("Hit", 0.35f);
            if (enemyAttackAt == now)
                sounds?.Play("Clang", 0.3f);
        }

        void DrawBattle()
        {
            DrawBattleStage();
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

        void Report(Fail fail, string sound = "Click")
        {
            message = fail == Fail.None ? "" : FailText(fail);
            if (fail == Fail.None && sound != null)
                sounds?.Play(sound, 0.5f);
        }

        string JobName(BuildJob job)
        {
            if (job.IsHearth)
                return "Очаг";
            if (job.IsClearing)
                return "Расчистка завала (этаж " + (job.Floor + 1) + ")";
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
                case Fail.CellTaken: return "Клетка занята или ещё завалена";
                case Fail.AlreadyQueued: return "Уже в работе";
                case Fail.NotMergeable: return "Объединяются только соседние залы одного типа и уровня";
                case Fail.NotAPair: return "Нужны мужчина и женщина, не в походе";
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

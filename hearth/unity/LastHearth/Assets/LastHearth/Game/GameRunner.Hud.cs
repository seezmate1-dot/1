using System;
using System.Linq;
using LastHearth.Core;
using UnityEngine;

namespace LastHearth.Game
{
    /// <summary>
    /// The main screen, laid out like Hustle Castle: the fortress fills the screen and scrolls under a
    /// finger; small resource plates run along the top; round icon buttons sit along the bottom and the
    /// right edge, with a red dot where something waits. Every part of the game opens in its own window
    /// over the fortress (a title, a close button, its content scrolling inside).
    /// </summary>
    public sealed partial class GameRunner
    {
        enum Win { None, Hearth, Room, Build, Fights, Surge, Dwellers, Growth, Routes, Quests, Shop, Events, Settings }

        Win window;
        Vector2 windowScroll;
        bool blockCutaway;           // a window is open, or the finger is on the interface: the rooms ignore taps
        bool framed;                 // the first frame scrolls to the rooms
        Rect[] hudRects = new Rect[0];

        static readonly (Win win, string title)[] Titles =
        {
            (Win.Hearth, "Очаг Стражей"), (Win.Room, ""), (Win.Build, "Стройка"), (Win.Fights, "Бои"), (Win.Surge, "Наплыв"),
            (Win.Dwellers, "Жители"), (Win.Growth, "Развитие"), (Win.Routes, "Вылазки"), (Win.Quests, "Задания"),
            (Win.Shop, "Лавка"), (Win.Events, "События"), (Win.Settings, "Настройки"),
        };

        GUIStyle hudText, buttonLabel, titleText;

        void HudStyles()
        {
            if (hudText != null)
                return;
            hudText = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            buttonLabel = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            titleText = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
        }

        void Open(Win w)
        {
            sounds?.Play("Page", 0.35f);
            window = w;
            windowScroll = Vector2.zero;
            decorPicker = false;
        }

        void CloseWindow()
        {
            window = Win.None;
            selectedRoom = -1;
            selectedFloor = -1;
            decorPicker = false;
        }

        /// <summary>A tap on the art of the fortress (rooms, cells, clouds), unless a window or the HUD is over it.</summary>
        bool Tap(Rect r) => !blockCutaway && GUI.Button(r, "", GUIStyle.none);

        // ------------------------------------------------------------------ the main screen

        void DrawMainScreen(Rect area, long now)
        {
            HudStyles();
            const float top = 50, bottom = 92, right = 74;
            // the HUD's places: taps there do not reach the rooms under it
            float sideButtons = (session.State.Rewards.IsEmpty() ? 3 : 4) * 76;
            hudRects = new[]
            {
                new Rect(area.x, area.y, area.width, top),
                new Rect(area.x, area.yMax - bottom, area.width, bottom),
                new Rect(area.xMax - right, area.y + top, right, sideButtons),
            };
            var mouse = Event.current.mousePosition;
            blockCutaway = window != Win.None || hudRects.Any(r => r.Contains(mouse));

            GUILayout.BeginArea(area);
            viewWidth = area.width;
            if (!framed)
            {
                // open on the Hearth's floor, in the middle of what the HUD leaves visible
                framed = true;
                var g = session.Balance.Grid;
                float cellW = Math.Max(60, (viewWidth - 8) / g.Cells), cellH = cellW * 0.5f;
                float hearthTop = (top - 6) + cellW * g.Cells * 0.5f * 0.8f + (ShownFloors().top - g.HearthFloor) * cellH;
                scroll.y = Math.Max(0, hearthTop + cellH / 2 - (top + (area.height - top - bottom) / 2));
            }
            scroll = GUILayout.BeginScrollView(scroll, GUIStyle.none, GUIStyle.none);
            GUILayout.Space(top - 6);
            DrawCutaway(now);
            GUILayout.Space(bottom + 6);
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            DrawTopBar(new Rect(area.x, area.y, area.width, top));
            DrawSideButtons(new Rect(area.xMax - right, area.y + top + 4, right, area.height - top - bottom - 8), now);
            DrawBottomButtons(new Rect(area.x, area.yMax - bottom, area.width - right, bottom), now);
            DrawToast(area);
        }

        void DrawTopBar(Rect bar)
        {
            var st = session.State;
            float x = bar.x;
            // the Hearth: its level on the big badge, a tap opens its window (upgrade, mode, builders)
            var badge = new Rect(x, bar.y + 2, 46, 46);
            ArtLibrary.Draw(badge, "UI/Badge");
            GUI.Label(new Rect(badge.x, badge.y - 1, badge.width - 3, badge.height - 3), st.HearthLevel.ToString(), titleText);
            if (window == Win.None && GUI.Button(badge, "", GUIStyle.none))
                Open(Win.Hearth);
            x += 52;
            var plates = new (ResourceType type, string text, float fill)[]
            {
                (ResourceType.Coal, ShortNum(st.Wallet[ResourceType.Coal]), -1),
                (ResourceType.Food, ShortNum(st.Wallet[ResourceType.Food]), -1),
                (ResourceType.Iron, ShortNum(st.Wallet[ResourceType.Iron]), Fill(ResourceType.Iron)),
                (ResourceType.Silver, ShortNum(st.Wallet[ResourceType.Silver]), Fill(ResourceType.Silver)),
                (ResourceType.Essence, ShortNum(st.Wallet[ResourceType.Essence]), -1),
            };
            float w = Mathf.Min(118, (bar.width - 52 - 46 - 100) / plates.Length - 4);
            foreach (var (type, text, fill) in plates)
            {
                Plate(new Rect(x, bar.y + 9, w, 30), type, text, fill, type == ResourceType.Coal && st.OutOfCoal);
                x += w + 4;
            }
            // Sparks (the premium currency) on the right, before the settings
            Plate(new Rect(bar.xMax - 46 - 104, bar.y + 9, 100, 30), ResourceType.Sparks, ShortNum(st.Wallet[ResourceType.Sparks]), -1, false);
            var gear = new Rect(bar.xMax - 44, bar.y + 3, 42, 42);
            ArtLibrary.Draw(gear, "UI/Gear");
            if (window == Win.None && GUI.Button(gear, "", GUIStyle.none))
                Open(Win.Settings);
        }

        float Fill(ResourceType t)
        {
            double cap = session.Capacity(t);
            return cap > 0 ? (float)Math.Min(1, session.State.Wallet[t] / cap) : -1;
        }

        /// <summary>One resource: a dark plate, its icon on the left, the amount, a thin bar of the storage.</summary>
        void Plate(Rect r, ResourceType type, string text, float fill, bool alarm)
        {
            ArtLibrary.DrawSliced(r, "UI/Pill", 18);
            ArtLibrary.Draw(new Rect(r.x - 4, r.y - 5, 38, 38), "Icons/" + type);
            var old = GUI.color;
            if (alarm)
                GUI.color = new Color(1f, 0.45f, 0.4f, 1);
            GUI.Label(new Rect(r.x + 34, r.y, r.width - 36, r.height), text, hudText);
            GUI.color = old;
            if (fill >= 0)
            {
                var track = new Rect(r.x + 34, r.yMax - 7, r.width - 44, 3);
                GUI.color = new Color(0, 0, 0, 0.5f);
                GUI.DrawTexture(track, Texture2D.whiteTexture);
                GUI.color = fill >= 1 ? new Color(1f, 0.5f, 0.3f, 1) : new Color(0.55f, 0.9f, 0.45f, 1);
                GUI.DrawTexture(new Rect(track.x, track.y, track.width * fill, track.height), Texture2D.whiteTexture);
                GUI.color = old;
            }
        }

        /// <summary>A round icon button with its name under it and a red dot when something waits there.</summary>
        bool RoundButton(Rect r, string icon, string label, bool dot)
        {
            float pulse = dot ? 1 + 0.04f * Mathf.Sin(Time.time * 5) : 1;
            var b = new Rect(r.center.x - r.width * pulse / 2, r.y + (r.width - r.width * pulse) / 2, r.width * pulse, r.width * pulse);
            ArtLibrary.Draw(b, "UI/Round");
            float pad = b.width * 0.14f;
            ArtLibrary.Draw(new Rect(b.x + pad, b.y + pad * 0.8f, b.width - 2 * pad, b.width - 2 * pad), icon);
            if (dot)
                ArtLibrary.Draw(new Rect(b.xMax - b.width * 0.3f, b.y, b.width * 0.3f, b.width * 0.3f), "UI/Dot");
            if (label.Length > 0)
            {
                var old = GUI.color;
                GUI.color = Color.black;
                GUI.Label(new Rect(r.x - 10 + 1, b.yMax - 6 + 1, r.width + 20, 16), label, buttonLabel);
                GUI.color = old;
                GUI.Label(new Rect(r.x - 10, b.yMax - 6, r.width + 20, 16), label, buttonLabel);
            }
            if (window != Win.None || !GUI.Button(b, "", GUIStyle.none))
                return false;
            sounds?.Play("Click", 0.4f);
            return true;
        }

        void DrawBottomButtons(Rect bar, long now)
        {
            // the buttons stand on their own wooden panel along the bottom, not on the rooms
            ArtLibrary.Draw(new Rect(bar.x - 8, bar.y + 22, Screen.width / uiScale + 16, bar.height), "UI/Bar");
            var st = session.State;
            var b = session.Balance;
            float size = Mathf.Min(64, bar.height - 22);
            var buttons = new (string icon, string label, Win win, bool dot)[]
            {
                ("Buttons/Fights", "Бои", Win.Fights, session.Squad().Count > 0 && st.PendingMission < 0 &&
                    session.MissionsCleared < b.Campaign.Missions.Count && session.Mission(session.MissionsCleared).Rank <= st.HearthLevel),
                ("Portraits/CreatureFiend", "Наплыв", Win.Surge, session.SurgeOpen(now)),
                ("Portraits/" + ArtLibrary.DwellerSheet(0), "Жители", Win.Dwellers,
                    st.Dwellers.Any(d => d.RoomId < 0 && d.RouteId == 0 && !d.IsHero)),
                ("Buttons/Growth", "Развитие", Win.Growth, false),
                ("Buttons/Routes", "Вылазки", Win.Routes, false),
            };
            // "collect all" first, the windows after it, all centred on the panel
            float x = bar.x + Math.Max(6, (bar.width - (buttons.Length + 1) * (size + 22) - 4) / 2);
            if (RoundButton(new Rect(x, bar.y + 4, size, size), "Buttons/Collect", "Собрать", st.Rooms.Any(r => r.Stock >= 1)))
            {
                session.CollectAll();
                sounds?.Play("Coins");
            }
            x += size + 22;
            foreach (var (icon, label, win, dot) in buttons)
            {
                if (RoundButton(new Rect(x, bar.y + 4, size, size), icon, label, dot))
                    Open(win);
                x += size + 22;
            }
        }

        void DrawSideButtons(Rect col, long now)
        {
            var st = session.State;
            float size = Mathf.Min(56, col.width - 14);
            float y = col.y;
            float x = col.center.x - size / 2;
            if (!st.Rewards.IsEmpty())
            {
                if (RoundButton(new Rect(x, y, size, size), "Buttons/Rewards", "Награды", true))
                {
                    message = "Получено: " + CostText(st.Rewards);
                    session.ClaimRewards();
                    sounds?.Play("Coins");
                }
                y += size + 20;
            }
            if (RoundButton(new Rect(x, y, size, size), "Buttons/Quests", "Задания",
                    session.TasksDone(now) >= session.Balance.Activities.Daily.TasksNeeded && !st.DailyClaimed))
                Open(Win.Quests);
            y += size + 20;
            if (RoundButton(new Rect(x, y, size, size), "Buttons/Shop", "Лавка", false))
                Open(Win.Shop);
            y += size + 20;
            if (RoundButton(new Rect(x, y, size, size), "Buttons/Events", "События", session.ActiveRuns(now).Any()))
                Open(Win.Events);
        }

        /// <summary>A short message over the fortress, fading after a few seconds.</summary>
        void DrawToast(Rect area)
        {
            if (message != shownMessage)
            {
                shownMessage = message;
                messageTime = Time.realtimeSinceStartup;
            }
            float age = Time.realtimeSinceStartup - messageTime;
            if (message == "" || age > 4f)
                return;
            var old = GUI.color;
            GUI.color = new Color(1, 1, 1, Mathf.Clamp(4f - age, 0, 1));
            var r = new Rect(area.center.x - 230, area.y + 56, 460, 30);
            ArtLibrary.DrawSliced(r, "UI/Pill", 18);
            GUI.Label(r, StripColor(message), buttonLabel);
            GUI.color = old;
        }

        // ------------------------------------------------------------------ windows

        void DrawWindow(Rect area, long now)
        {
            // the fortress dims under the window
            var old = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width / uiScale, Screen.height / uiScale), Texture2D.whiteTexture);
            GUI.color = old;
            var w = new Rect(area.x + area.width * 0.04f, area.y + 6, area.width * 0.92f, area.height - 10);
            ArtLibrary.DrawSliced(w, "UI/Window", 40);
            string title = Titles.First(t => t.win == window).title;
            if (window == Win.Room && session.FindRoom(selectedRoom) is Room room)
                title = session.Balance.Room(room.Type).Name;
            var plank = new Rect(w.center.x - 160, w.y - 2, 320, 40);
            ArtLibrary.DrawSliced(plank, "UI/ButtonOn", 16);
            GUI.Label(plank, title, titleText);
            var close = new Rect(w.xMax - 46, w.y - 8, 50, 50);
            ArtLibrary.Draw(close, "UI/Close");
            if (GUI.Button(close, "", GUIStyle.none))
            {
                sounds?.Play("Click", 0.4f);
                CloseWindow();
                return;
            }
            var inside = new Rect(w.x + 30, w.y + 44, w.width - 60, w.height - 72);
            GUILayout.BeginArea(inside);
            viewWidth = inside.width - 24;
            windowScroll = GUILayout.BeginScrollView(windowScroll);
            DrawWindowContent(now);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawWindowContent(long now)
        {
            var st = session.State;
            switch (window)
            {
                case Win.Hearth:
                    GUILayout.Label(string.Format("Режим очага: <b>{0}</b>. Тлеющий бережёт уголь, Пылающий греет дальние этажи.", ModeName(st.Mode)), Rich());
                    GUILayout.BeginHorizontal();
                    foreach (HearthMode mode in Enum.GetValues(typeof(HearthMode)))
                    {
                        if (GUILayout.Toggle(st.Mode == mode, ModeName(mode), "Button") && st.Mode != mode)
                            session.SetMode(mode);
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.Label(string.Format("Уголь: {0} · горит {1:0} в час{2}", ShortNum(st.Wallet[ResourceType.Coal]), session.CoalBurnPerHour(),
                        st.OutOfCoal ? " · <color=#f86>уголь кончился, залы промерзают</color>" : ""), Rich());
                    DrawHearth(now);
                    GUILayout.Space(8);
                    DrawQueue(now);
                    break;
                case Win.Room:
                    var room = session.FindRoom(selectedRoom);
                    if (room == null)
                        CloseWindow();
                    else
                        DrawRoomPanel(room, now);
                    break;
                case Win.Build:
                    if (selectedFloor >= 0 && session.CellFree(selectedFloor, selectedCell))
                        DrawBuildPanel(now);
                    else
                        CloseWindow();
                    break;
                case Win.Fights:
                    DrawCampaign(now);
                    DrawHunt(now);
                    DrawArena(now);
                    DrawRaids(now);
                    break;
                case Win.Surge:
                    DrawSurgeTab(now);
                    break;
                case Win.Dwellers:
                    DrawDwellers();
                    break;
                case Win.Growth:
                    DrawHeart();
                    DrawSpecs();
                    break;
                case Win.Routes:
                    DrawRoutes(now);
                    break;
                case Win.Quests:
                    DrawDaily(now);
                    break;
                case Win.Shop:
                    DrawShops(now);
                    break;
                case Win.Events:
                    DrawEvents(now);
                    break;
                case Win.Settings:
                    DrawSettings(now);
                    break;
            }
        }

        void DrawSettings(long now)
        {
            GUILayout.BeginHorizontal();
            if (sounds != null && GUILayout.Button(sounds.MusicOn ? "Музыка: вкл" : "Музыка: выкл"))
                sounds.ToggleMusic();
            if (sounds != null && GUILayout.Button(sounds.SoundOn ? "Звуки: вкл" : "Звуки: выкл"))
                sounds.ToggleSound();
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
            GUILayout.Label("<b>Для проверки</b> (тестовая сборка)", Rich());
            GUILayout.BeginHorizontal();
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
                StartSession(NewGame.Create(session.Balance, Now()));
                message = "";
                CloseWindow();
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
            DrawRooms(now);
        }
    }
}

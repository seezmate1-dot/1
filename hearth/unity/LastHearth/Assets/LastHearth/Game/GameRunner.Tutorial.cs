using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core;
using UnityEngine;

namespace LastHearth.Game
{
    /// <summary>
    /// The screen of the tutorial (Core/Tutorial.cs, MODES.md 10): Mara speaks from a bubble, a gold
    /// frame pulses round the button or the room cell to tap, an arrow bobs over it, and the rest of the
    /// screen dims. Nothing is blocked: every tap still reaches the game, the step moves on when the game
    /// says the thing was done, and «Пропустить обучение» ends it at once.
    ///
    /// The draw code tells where its buttons are with one-liners: TutorTarget (a rectangle drawn by
    /// hand: a round button, a cell), TutorButton (a GUILayout button in a window), TutorRoom (a room
    /// cell). They store screen positions, like the effects (GameRunner.Anim.cs), so scroll views and the
    /// window's pop do not matter; DrawTutorial, last in OnGUI, draws the layer and forgets them.
    /// </summary>
    public sealed partial class GameRunner
    {
        readonly Dictionary<string, Rect> tutorTargets = new Dictionary<string, Rect>();
        readonly Dictionary<int, Rect> tutorRooms = new Dictionary<int, Rect>();
        // the bubble and its buttons as drawn last time (GUI units of the screen): taps there are the tutorial's
        bool tutorShown;
        Rect tutorBubble, tutorNextButton, tutorSkipButton;
        float tutorStepAt = -9;
        GUIStyle tutorText, tutorName, tutorSmall;

        /// <summary>The tutorial runs and the guide has something to say now.</summary>
        bool TutorOn => session != null && session.TutorialState.Active;

        void TutorHook(GameSession s)
        {
            s.TutorialChanged += step =>
            {
                tutorStepAt = Tween.Now;
                if (step == TutorialStep.Done)
                {
                    if (!s.TutorialState.Skipped)
                        message = "Обучение пройдено: крепость в ваших руках";
                }
                sounds?.Play("Done", 0.5f);
            };
        }

        /// <summary>A place to point at, drawn by hand (GUI units of the current group).</summary>
        void TutorTarget(string id, Rect r)
        {
            if (TutorOn && !tutorTargets.ContainsKey(id))
                tutorTargets[id] = ToScreen(r);
        }

        /// <summary>A room cell of the cutaway.</summary>
        void TutorRoom(int roomId, Rect r)
        {
            if (TutorOn)
                tutorRooms[roomId] = ToScreen(r);
        }

        /// <summary>GUILayout.Button that the tutorial can point at (id null: never).</summary>
        bool TutorButton(string id, string text, params GUILayoutOption[] options)
        {
            bool pressed = GUILayout.Button(text, options);
            if (id != null && TutorOn)
                TutorTarget(id, GUILayoutUtility.GetLastRect());
            return pressed;
        }

        static string TutorStepName(TutorialStep step)
        {
            switch (step)
            {
                case TutorialStep.Welcome: return "Встреча";
                case TutorialStep.Collect: return "Сбор";
                case TutorialStep.Hearth: return "Очаг";
                case TutorialStep.Assign: return "Работа";
                case TutorialStep.Clear: return "Завал";
                case TutorialStep.Build: return "Стройка";
                case TutorialStep.Upgrade: return "Улучшение";
                case TutorialStep.HeroLevel: return "Уровень героя";
                case TutorialStep.Promote: return "Новый герой";
                case TutorialStep.Campaign: return "Поход";
                case TutorialStep.Surge: return "Наплыв";
                default: return "Готово";
            }
        }

        // ------------------------------------------------------------------ where to point

        /// <summary>Whether the guide speaks over this screen: the fortress, the map, or the window of the step.</summary>
        bool TutorScreen(TutorialStep step)
        {
            if (window == Win.None)
                return true;
            switch (step)
            {
                case TutorialStep.Hearth: return window == Win.Hearth;
                case TutorialStep.Assign:
                case TutorialStep.Upgrade:
                case TutorialStep.Collect: return window == Win.Room;
                case TutorialStep.Clear:
                case TutorialStep.Build: return window == Win.Build;
                case TutorialStep.HeroLevel: return window == Win.Hero;
                case TutorialStep.Promote: return window == Win.Dwellers;
                case TutorialStep.Campaign: return window == Win.Campaign;
                default: return false;
            }
        }

        /// <summary>The rectangle (screen space) to point at for this step, if it is on the screen.</summary>
        Rect? TutorFocus(TutorialStep step)
        {
            var st = session.State;
            Rect? Target(string id) => tutorTargets.TryGetValue(id, out var r) ? r : (Rect?)null;
            Rect? Room(Func<Room, bool> pick)
            {
                foreach (var room in st.Rooms)
                {
                    if (pick(room) && tutorRooms.TryGetValue(room.Id, out var r))
                        return r;
                }
                return null;
            }
            bool fortress = window == Win.None && !onMap;
            switch (step)
            {
                case TutorialStep.Collect:
                    if (!fortress)
                        return null;
                    return Room(r => r.Stock >= 1 && session.Balance.Room(r.Type).Produces != null) ?? Target("collect");
                case TutorialStep.Hearth:
                    return window == Win.Hearth ? Target("hearthUpgrade") : fortress ? Target("hearth") : null;
                case TutorialStep.Assign:
                    if (window == Win.Room)
                        return Target("roomAssign");
                    return fortress ? Room(r => session.Balance.Room(r.Type).Produces != null && r.Workers.Count < session.WorkerPlaces(r)) : null;
                case TutorialStep.Clear:
                    return window == Win.Build ? Target("clearObstacle") : fortress ? Target("obstacle") : null;
                case TutorialStep.Build:
                    return window == Win.Build ? Target("buildBarracks") ?? Target("buildAny") : fortress ? Target("free") : null;
                case TutorialStep.Upgrade:
                    if (window == Win.Room)
                        return Target("roomUpgrade");
                    return fortress ? Room(r => r.Level >= 1 && r.Level < st.HearthLevel && !st.Queue.IsQueued(r.Id)) : null;
                case TutorialStep.HeroLevel:
                    return window == Win.None ? Target("Hero") : null;
                case TutorialStep.Promote:
                    return window == Win.None ? Target("Dwellers") : null;
                case TutorialStep.Campaign:
                    if (window == Win.Campaign)
                        return Target("campaignNext");
                    if (window != Win.None)
                        return null;
                    return onMap ? Target("act") : Target("map");
                case TutorialStep.Surge:
                    return window == Win.None ? Target("Surge") : null;
                default:
                    return null;
            }
        }

        // ------------------------------------------------------------------ taps

        /// <summary>
        /// Early in OnGUI: a tap on the bubble belongs to the tutorial. IMGUI hands a tap to the first
        /// control drawn under it, and the bubble is drawn last, so its taps are taken here and used up.
        /// </summary>
        void TutorInput()
        {
            var e = Event.current;
            if (!tutorShown || (e.type != EventType.MouseDown && e.type != EventType.MouseUp) || !tutorBubble.Contains(e.mousePosition))
                return;
            if (e.type == EventType.MouseUp)
            {
                if (tutorNextButton.Contains(e.mousePosition))
                    TutorNextTap();
                else if (tutorSkipButton.Contains(e.mousePosition))
                    TutorSkipTap();
            }
            e.Use();
        }

        void TutorNextTap()
        {
            sounds?.Play("Click", 0.4f);
            session.TutorialNext(Now());
        }

        void TutorSkipTap()
        {
            sounds?.Play("Click", 0.4f);
            session.SkipTutorial(Now());
            message = "Обучение пропущено";
        }

        // ------------------------------------------------------------------ the layer

        /// <summary>Last in OnGUI over the fortress and its windows: the dimming, the frame, the arrow and Mara.</summary>
        void DrawTutorial(Rect area)
        {
            tutorShown = false;
            try
            {
                if (!TutorOn || session.TutorialWaits() || window == Win.Admin || window == Win.Away)
                    return;
                var step = session.TutorialState.Step;
                var info = Tutorial.Of(step);
                if (info == null || !TutorScreen(step))
                    return;
                TutorStyles();
                var old = GUI.color;
                float appear = Tween.OutCubic(Tween.Progress(tutorStepAt, 0.35f));
                var focus = TutorFocus(step) is Rect s ? FromScreen(s) : (Rect?)null;
                var screen = new Rect(0, 0, Screen.width / uiScale, Screen.height / uiScale);
                if (focus is Rect f)
                {
                    var hole = Inflate(f, 6);
                    // the rest of the screen dims around the place to tap
                    GUI.color = new Color(0, 0, 0, 0.5f * appear * old.a);
                    Fill(new Rect(screen.x, screen.y, screen.width, Math.Max(0, hole.y - screen.y)));
                    Fill(new Rect(screen.x, hole.yMax, screen.width, Math.Max(0, screen.yMax - hole.yMax)));
                    Fill(new Rect(screen.x, hole.y, Math.Max(0, hole.x - screen.x), hole.height));
                    Fill(new Rect(hole.xMax, hole.y, Math.Max(0, screen.xMax - hole.xMax), hole.height));
                    GUI.color = old;
                    DrawRing(hole);
                    DrawArrow(hole, screen);
                }
                else if (Tutorial.Talks(step) && window == Win.None)
                {
                    // Mara speaks to the whole fortress
                    GUI.color = new Color(0, 0, 0, 0.35f * appear * old.a);
                    Fill(screen);
                    GUI.color = old;
                }
                DrawBubble(area, step, info, focus, appear);
                GUI.color = old;
            }
            finally
            {
                tutorTargets.Clear();
                tutorRooms.Clear();
            }
        }

        void TutorStyles()
        {
            if (tutorText != null)
                return;
            tutorText = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true, richText = false, alignment = TextAnchor.UpperLeft };
            tutorName = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            tutorSmall = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
        }

        static Rect Inflate(Rect r, float by) => new Rect(r.x - by, r.y - by, r.width + 2 * by, r.height + 2 * by);

        static void Fill(Rect r)
        {
            if (r.width > 0.5f && r.height > 0.5f)
                GUI.DrawTexture(r, Texture2D.whiteTexture);
        }

        /// <summary>A gold frame that breathes round the place to tap, with a soft glow outside it.</summary>
        void DrawRing(Rect hole)
        {
            var old = GUI.color;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Tween.Now * 5);
            var glow = Inflate(hole, 4 + 5 * pulse);
            GUI.color = new Color(1f, 0.8f, 0.3f, (0.25f + 0.25f * pulse) * old.a);
            Frame(glow, 6);
            GUI.color = new Color(1f, 0.88f, 0.4f, old.a);
            Frame(hole, 3);
            GUI.color = old;
        }

        static void Frame(Rect r, float w)
        {
            Fill(new Rect(r.x, r.y, r.width, w));
            Fill(new Rect(r.x, r.yMax - w, r.width, w));
            Fill(new Rect(r.x, r.y + w, w, r.height - 2 * w));
            Fill(new Rect(r.xMax - w, r.y + w, w, r.height - 2 * w));
        }

        /// <summary>A gold arrow bobbing over the place (under it when the place is at the top of the screen).</summary>
        void DrawArrow(Rect hole, Rect screen)
        {
            var old = GUI.color;
            bool below = hole.y < screen.y + 110;
            float bob = 6 * Mathf.Sin(Tween.Now * 6);
            float cx = Mathf.Clamp(hole.center.x, screen.x + 30, screen.xMax - 30);
            const float size = 34, rows = 12;
            // the tip is next to the place, the arrow widens away from it; a shaft behind it
            float tip = below ? hole.yMax + 8 + bob : hole.y - 8 - bob;
            float dir = below ? 1 : -1;
            for (int pass = 0; pass < 2; pass++)
            {
                GUI.color = pass == 0 ? new Color(0.25f, 0.12f, 0.04f, 0.9f * old.a) : new Color(1f, 0.85f, 0.35f, old.a);
                float grow = pass == 0 ? 3 : 0;
                for (int i = 0; i < rows; i++)
                {
                    float w = (size + grow * 2) * (i + 1) / rows;
                    float h = size * 0.75f / rows + 1;
                    float y = tip + dir * (i * size * 0.75f / rows - grow) - (below ? 0 : h);
                    Fill(new Rect(cx - w / 2, y, w, h));
                }
                float shaftW = size * 0.38f + grow * 2;
                float shaftY = tip + dir * size * 0.75f;
                Fill(new Rect(cx - shaftW / 2, below ? shaftY : shaftY - 22 - grow, shaftW, 22 + grow));
            }
            GUI.color = old;
        }

        /// <summary>Mara's bubble: her portrait, her line, «Далее» on a talking step and «Пропустить обучение».</summary>
        void DrawBubble(Rect area, TutorialStep step, Tutorial.StepInfo info, Rect? focus, float appear)
        {
            var old = GUI.color;
            float w = Mathf.Min(720, area.width - 40), h = 112;
            float x = area.center.x - w / 2;
            // under the place to tap, or over it: never on it
            float low = area.yMax - HudBottom - h - 8, high = area.y + HudTop + 30;
            float y = low;
            if (focus is Rect f && (f.yMax > low - 40 && f.y < low + h + 40))
                y = high;
            if (window != Win.None && focus is Rect g && g.center.y > area.center.y)
                y = high;
            y += (1 - appear) * 30;
            GUI.color = new Color(old.r, old.g, old.b, old.a * appear);
            var bubble = new Rect(x, y, w, h);
            ArtLibrary.DrawSliced(bubble, "UI/Window", 60);
            // Mara in a round frame like the buttons', leaning out of the bubble's left edge
            var face = new Rect(bubble.x - 22, bubble.y - 18, 116, 116);
            ArtLibrary.Draw(face, "UI/Round");
            float pad = face.width * 0.14f;
            ArtLibrary.Draw(new Rect(face.x + pad, face.y + pad * 0.8f, face.width - 2 * pad, face.width - 2 * pad),
                "Portraits/" + ArtLibrary.HeroSheet(HeroClass.Guardian));
            var plate = new Rect(face.x + 12, face.yMax - 14, 92, 24);
            ArtLibrary.DrawSliced(plate, "UI/Pill", 30);
            UiSkin.Outlined(plate, "Мара", tutorName);
            var (index, count) = Tutorial.Progress(session.TutorialState);
            var text = new Rect(bubble.x + 110, bubble.y + 16, bubble.width - 136, 50);
            UiSkin.Outlined(text, info.Text, tutorText, 1f);
            UiSkin.Outlined(new Rect(bubble.xMax - 70, bubble.y + 2, 60, 18), index + "/" + count, tutorSmall, 1f);
            // the buttons along the bottom of the bubble
            var skip = new Rect(bubble.x + 110, bubble.yMax - 40, 210, 28);
            ArtLibrary.DrawSliced(skip, "UI/Pill", 30);
            UiSkin.Outlined(skip, "Пропустить обучение", tutorSmall, 1f);
            var next = new Rect(bubble.xMax - 160, bubble.yMax - 44, 140, 34);
            if (Tutorial.Talks(step))
            {
                float pulse = 1 + 0.03f * Mathf.Sin(Tween.Now * 5);
                var drawn = new Rect(next.center.x - next.width * pulse / 2, next.center.y - next.height * pulse / 2, next.width * pulse, next.height * pulse);
                ArtLibrary.DrawSliced(drawn, "UI/ButtonOn", 16);
                UiSkin.Outlined(drawn, step == TutorialStep.Surge ? "Вперёд!" : "Далее", tutorName);
            }
            else
            {
                next = new Rect(0, 0, 0, 0);
            }
            GUI.color = old;
            tutorBubble = bubble;
            tutorNextButton = next;
            tutorSkipButton = skip;
            tutorShown = true;
            // in Unity TutorInput has already taken these taps; the buttons here answer where nothing came first
            if (next.width > 0 && GUI.Button(next, "Далее", GUIStyle.none))
                TutorNextTap();
            else if (GUI.Button(skip, "Пропустить обучение", GUIStyle.none))
                TutorSkipTap();
        }

        // ------------------------------------------------------------------ the admin page

        void AdminTutorial(AdminList list, long now)
        {
            var t = session.TutorialState;
            string state = !t.Active ? (t.Skipped ? "пропущено" : "пройдено") :
                string.Format("шаг {0}/{1}: {2}{3}", Tutorial.Progress(t).index, Tutorial.Steps.Length, TutorStepName(t.Step),
                    session.TutorialWaits() ? " (ждёт, пока шаг станет возможен)" : "");
            list.Row("Обучение: " + state);
            list.Add("Начать заново", n =>
            {
                session.RestartTutorial(n);
                return "Обучение начато заново";
            });
            list.Add("Завершить", n =>
            {
                session.JumpTutorial(TutorialStep.Done, n);
                return "Обучение завершено";
            });
            list.Row("Перейти к шагу");
            foreach (var info in Tutorial.Steps)
            {
                var step = info.Step;
                list.Add(TutorStepName(step), n =>
                {
                    session.JumpTutorial(step, n);
                    return "Обучение: шаг «" + TutorStepName(step) + "»";
                });
            }
        }
    }
}

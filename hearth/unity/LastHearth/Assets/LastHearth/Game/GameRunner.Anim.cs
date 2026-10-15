using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core;
using UnityEngine;

namespace LastHearth.Game
{
    /// <summary>
    /// The life of the interface, like Hustle Castle's: round buttons sink under the finger and pop
    /// back, windows open with a springy pop and close with a fade, collected resources fly as icons
    /// from the room to their plate on the top bar (and its number counts up), "+N" floats over the
    /// room, a finished building job makes the room glow with a «Готово» sparkle, the Hearth's badge
    /// bounces on a level up. All time based (Tween.cs); the layer is drawn last in OnGUI (DrawFx).
    ///
    /// Hooks in the other files are one-liners: Pressable (RoundButton, the window's close button),
    /// Collecting / CollectingAll (around session.Collect / CollectAll / ClaimRewards), PlateText
    /// (Plate), HearthBadgeFx (the top bar), RoomFx (each room cell), AnimWindow and DimScreen.
    /// Positions travel between the scrolled fortress and the top bar in screen space
    /// (GUIUtility.GUIToScreenPoint and back), so the scroll views do not matter.
    /// </summary>
    public sealed partial class GameRunner
    {
        const int HearthFx = -1000;

        sealed class Flyer
        {
            public ResourceType Type;
            public Vector2 From;     // screen space
            public Vector2 Burst;    // GUI units: where it pops out to first
            public float Start, Duration, Side;
            public long Share;
            public bool ToPlate, Arrived;
        }

        sealed class Floater
        {
            public string Text = "";
            public ResourceType? Icon;
            public Vector2 At;       // screen space
            public float Start, Duration = 1.3f, Rise = 46;
            public Color Color = Color.white;
            public int Size = 20;
        }

        readonly List<Flyer> flyers = new List<Flyer>();
        readonly List<Floater> floaters = new List<Floater>();
        readonly Dictionary<int, (Rect rect, float seen)> roomOnScreen = new Dictionary<int, (Rect, float)>();
        readonly Dictionary<ResourceType, Rect> plateOnScreen = new Dictionary<ResourceType, Rect>();
        readonly long[] inFlight = new long[ResourceBag.Count];
        readonly double[] plateShown = new double[ResourceBag.Count];
        readonly float[] plateBumpAt = Enumerable.Repeat(-9f, ResourceBag.Count).ToArray();
        readonly Dictionary<int, float> roomDoneAt = new Dictionary<int, float>();
        readonly Dictionary<string, float> releasedAt = new Dictionary<string, float>();
        readonly System.Random fxRng = new System.Random(11);
        GameSession fxSession;
        int fxHearthLevel;
        float levelUpAt = -9, fxLastTick = -1;
        Rect hearthBadgeOnScreen;
        Vector2 fxScreenCenter;
        string pressedKey;
        float pressedAt;
        Win fxWindow;
        float windowOpenAt = -9, windowCloseAt = -9;
        Win closingWindow;
        int closingRoom = -1, closingFloor = -1, closingCell = -1;
        bool closingDecor;
        Matrix4x4? dimMatrix;
        GUIStyle fxNumber, fxDone;

        static Vector2 ToScreen(Vector2 gui) => GUIUtility.GUIToScreenPoint(gui);

        static Rect ToScreen(Rect r)
        {
            var a = ToScreen(new Vector2(r.x, r.y));
            var b = ToScreen(new Vector2(r.xMax, r.yMax));
            return new Rect(a.x, a.y, b.x - a.x, b.y - a.y);
        }

        static Vector2 FromScreen(Vector2 s) => GUIUtility.ScreenToGUIPoint(s);

        static Rect FromScreen(Rect r)
        {
            var a = FromScreen(new Vector2(r.x, r.y));
            var b = FromScreen(new Vector2(r.xMax, r.yMax));
            return new Rect(a.x, a.y, Mathf.Max(1, b.x - a.x), Mathf.Max(1, b.y - a.y));
        }

        // ------------------------------------------------------------------ buttons

        /// <summary>Where to draw a button that sinks while pressed and pops back when let go (the hit
        /// area stays the given rectangle).</summary>
        Rect Pressable(string key, Rect b)
        {
            var e = Event.current;
            float now = Tween.Now;
            if (e.type == EventType.MouseDown && b.Contains(e.mousePosition))
            {
                pressedKey = key;
                pressedAt = now;
            }
            bool held = Input.GetMouseButton(0) || Input.touchCount > 0;
            if (pressedKey == key && (e.type == EventType.MouseUp || (!held && now - pressedAt > 0.05f) || now - pressedAt > 5))
            {
                pressedKey = null;
                releasedAt[key] = now;
            }
            float s = 1;
            if (pressedKey == key)
                s = Tween.Lerp(1, 0.86f, Tween.OutCubic(Tween.Progress(pressedAt, 0.08f, now)));
            else if (releasedAt.TryGetValue(key, out float at))
            {
                float k = Tween.Progress(at, 0.32f, now);
                if (k >= 1)
                    releasedAt.Remove(key);
                else
                    s = Tween.Lerp(0.86f, 1, Tween.OutBack(k, 3.2f));
            }
            return s == 1 ? b : Tween.Scale(b, s);
        }

        // ------------------------------------------------------------------ windows

        /// <summary>Draws the open window with its pop (scale and fade in), and the closed one fading out.</summary>
        void AnimWindow(Rect area, long now)
        {
            float t = Tween.Now;
            if (window != fxWindow)
            {
                if (window == Win.None)
                {
                    closingWindow = fxWindow;
                    windowCloseAt = t;
                }
                else if (fxWindow == Win.None)
                {
                    windowOpenAt = t;
                }
                fxWindow = window;
            }
            var m = GUI.matrix;
            var c = GUI.color;
            bool enabled = GUI.enabled;
            dimMatrix = m;
            try
            {
                if (window != Win.None)
                {
                    closingRoom = selectedRoom;
                    closingFloor = selectedFloor;
                    closingCell = selectedCell;
                    closingDecor = decorPicker;
                    float k = Tween.Progress(windowOpenAt, 0.3f, t);
                    if (k < 1)
                    {
                        float s = Tween.Lerp(0.8f, 1, Tween.OutBack(k));
                        GUIUtility.ScaleAroundPivot(new Vector2(s, s), area.center);
                        GUI.color = Tween.Fade(c, Tween.OutCubic(Tween.Clamp01(k * 1.8f)));
                    }
                    DrawWindow(area, now);
                    return;
                }
                float f = Tween.Progress(windowCloseAt, 0.2f, t);
                if (closingWindow == Win.None || f >= 1)
                    return;
                // the window that was just closed, once more, shrinking and fading; nothing in it answers taps
                float sc = Tween.Lerp(1, 0.9f, Tween.InCubic(f));
                GUIUtility.ScaleAroundPivot(new Vector2(sc, sc), area.center);
                GUI.color = Tween.Fade(c, 1 - Tween.OutQuad(f));
                GUI.enabled = false;
                int room = selectedRoom, floor = selectedFloor, cell = selectedCell;
                bool decor = decorPicker;
                var scrollWas = windowScroll;
                window = closingWindow;
                selectedRoom = closingRoom;
                selectedFloor = closingFloor;
                selectedCell = closingCell;
                decorPicker = closingDecor;
                try
                {
                    DrawWindow(area, now);
                }
                finally
                {
                    window = Win.None;
                    selectedRoom = room;
                    selectedFloor = floor;
                    selectedCell = cell;
                    decorPicker = decor;
                    windowScroll = scrollWas;
                }
            }
            finally
            {
                GUI.matrix = m;
                GUI.color = c;
                GUI.enabled = enabled;
                dimMatrix = null;
            }
        }

        /// <summary>The fortress dims under a window: the whole screen, whatever the window's pop does.</summary>
        void DimScreen()
        {
            var m = GUI.matrix;
            var old = GUI.color;
            if (dimMatrix.HasValue)
                GUI.matrix = dimMatrix.Value;
            GUI.color = new Color(0, 0, 0, 0.55f * old.a);
            GUI.DrawTexture(new Rect(0, 0, Screen.width / uiScale, Screen.height / uiScale), Texture2D.whiteTexture);
            GUI.color = old;
            GUI.matrix = m;
        }

        // ------------------------------------------------------------------ collecting

        /// <summary>Runs a collect and sends what it brought flying to the top bar.</summary>
        T Collecting<T>(Func<T> collect, Rect? from = null)
        {
            var st = session.State;
            var before = new long[ResourceBag.Count];
            for (int i = 0; i < before.Length; i++)
                before[i] = st.Wallet[(ResourceType)i];
            var stock = st.Rooms.ToDictionary(r => r.Id, r => r.Stock);
            Vector2? source = from.HasValue ? ToScreen(from.Value.center) : (Vector2?)null;
            T result = collect();
            var gained = new long[ResourceBag.Count];
            for (int i = 0; i < gained.Length; i++)
                gained[i] = st.Wallet[(ResourceType)i] - before[i];
            float now = Tween.Now;
            foreach (var room in st.Rooms)
            {
                var made = session.Balance.Room(room.Type).Produces;
                if (made == null || !stock.TryGetValue(room.Id, out double was))
                    continue;
                int k = (int)made.Value;
                long taken = Math.Min((long)Math.Round(was - room.Stock), gained[k]);
                if (taken <= 0)
                    continue;
                gained[k] -= taken;
                // from the room when it was on the screen a moment ago, else from the button
                bool seen = roomOnScreen.TryGetValue(room.Id, out var at) && now - at.seen < 0.5f;
                SpawnCollect(made.Value, taken, seen ? at.rect.center : source ?? fxScreenCenter);
            }
            for (int i = 0; i < gained.Length; i++)
            {
                if (gained[i] > 0)
                    SpawnCollect((ResourceType)i, gained[i], source ?? fxScreenCenter);
            }
            return result;
        }

        void CollectingAll(Action collect, Rect? from = null) => Collecting(() =>
        {
            collect();
            return 0;
        }, from);

        void SpawnCollect(ResourceType type, long amount, Vector2 at)
        {
            float now = Tween.Now;
            int n = amount >= 50 ? 6 : amount >= 10 ? 5 : amount >= 3 ? 4 : (int)Math.Max(1, amount);
            bool toPlate = plateOnScreen.ContainsKey(type);
            if (toPlate)
                inFlight[(int)type] += amount;
            long left = amount;
            for (int i = 0; i < n; i++)
            {
                long share = i == n - 1 ? left : amount / n;
                left -= share;
                float a = (float)(fxRng.NextDouble() * Math.PI * 2), d = 26 + (float)fxRng.NextDouble() * 34;
                flyers.Add(new Flyer
                {
                    Type = type, From = at, Burst = new Vector2(Mathf.Cos(a) * d, Mathf.Sin(a) * d * 0.7f - 12),
                    Start = now + i * 0.055f, Duration = 0.8f + (float)fxRng.NextDouble() * 0.2f,
                    Side = (float)fxRng.NextDouble() * 2 - 1, Share = share, ToPlate = toPlate,
                });
            }
            floaters.Add(new Floater { Text = "+" + ShortNum(amount), Icon = type, At = at, Start = now, Color = ResourceTint(type), Size = 22 });
        }

        static Color ResourceTint(ResourceType t)
        {
            switch (t)
            {
                case ResourceType.Coal: return new Color(0.88f, 0.9f, 0.96f, 1);
                case ResourceType.Food: return new Color(0.7f, 1f, 0.5f, 1);
                case ResourceType.Iron: return new Color(0.75f, 0.88f, 1f, 1);
                case ResourceType.Essence: return new Color(0.85f, 0.65f, 1f, 1);
                case ResourceType.Sparks: return new Color(1f, 0.65f, 0.9f, 1);
                case ResourceType.Ember: return new Color(1f, 0.65f, 0.35f, 1);
                default: return new Color(1f, 0.9f, 0.55f, 1);
            }
        }

        /// <summary>A plate's number: it counts up as the icons land, and remembers where the plate is.</summary>
        string PlateText(ResourceType type, Rect r, string text)
        {
            plateOnScreen[type] = ToScreen(r);
            int i = (int)type;
            double wallet = session.State.Wallet[type];
            if (inFlight[i] == 0 && Math.Abs(plateShown[i] - wallet) < 0.5)
                return text;
            return ShortNum(Math.Floor(plateShown[i]));
        }

        // ------------------------------------------------------------------ rooms and the Hearth

        /// <summary>Called by each room cell: remembers where it is and draws its «Готово» glow.</summary>
        void RoomFx(Rect r, int id)
        {
            float now = Tween.Now;
            roomOnScreen[id] = (ToScreen(r), now);
            if (!roomDoneAt.TryGetValue(id, out float at))
                return;
            float k = (now - at) / 1.9f;
            if (k >= 1 || k < 0)
            {
                if (k >= 1)
                    roomDoneAt.Remove(id);
                return;
            }
            var old = GUI.color;
            // a soft warm light that swells and fades twice
            float glow = 0.34f * Tween.Bump(Tween.Clamp01(k * 2.4f)) + 0.16f * Tween.Bump(Tween.Clamp01((k - 0.38f) * 2.6f));
            GUI.color = new Color(1f, 0.9f, 0.6f, glow * old.a);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            // sparkles twinkling over the room
            for (int i = 0; i < 9; i++)
            {
                float h1 = FxHash(id * 31 + i * 7 + 1), h2 = FxHash(id * 17 + i * 13 + 2);
                float life = Tween.Bump(Tween.Clamp01((k - i * 0.06f) / 0.45f));
                Twinkle(new Vector2(r.x + r.width * (0.1f + 0.8f * h1), r.y + r.height * (0.12f + 0.66f * h2)),
                    r.height * (0.07f + 0.05f * h2) * life, old.a * life);
            }
            // «Готово» pops up in the middle and fades away
            FxStyles();
            fxDone.fontSize = (int)Mathf.Clamp(r.height * 0.17f, 14, 40);
            float pop = Tween.OutBack(Tween.Clamp01(k / 0.22f), 2.4f);
            float a = k < 0.7f ? 1 : 1 - (k - 0.7f) / 0.3f;
            var m = GUI.matrix;
            var lr = new Rect(r.x, r.y + r.height * 0.3f - k * r.height * 0.08f, r.width, fxDone.fontSize * 1.6f);
            GUIUtility.ScaleAroundPivot(new Vector2(Mathf.Max(0.05f, pop), Mathf.Max(0.05f, pop)), lr.center);
            GUI.color = Tween.Fade(old, a);
            UiSkin.Outlined(lr, "Готово!", fxDone, 2f);
            GUI.matrix = m;
            GUI.color = old;
        }

        void OnJobDoneFx(BuildJob job) => roomDoneAt[job.IsHearth ? HearthFx : job.Target] = Tween.Now;

        /// <summary>The Hearth's badge on the top bar: bounces after a level up.</summary>
        Rect HearthBadgeFx(Rect badge)
        {
            hearthBadgeOnScreen = ToScreen(badge);
            float k = Tween.Progress(levelUpAt, 1.3f);
            if (k >= 1)
                return badge;
            float s = 1 + 0.5f * (1 - k) * Mathf.Abs(Mathf.Sin(k * Mathf.PI * 3.5f));
            return Tween.Scale(badge, s);
        }

        // ------------------------------------------------------------------ the layer drawn last

        void FxStyles()
        {
            if (fxNumber != null)
                return;
            fxNumber = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            fxDone = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            fxDone.normal.textColor = new Color(1f, 0.86f, 0.4f, 1);
        }

        /// <summary>Keeps the effects in step with the game: a new session, a level up, the plates' count.</summary>
        void FxTick()
        {
            float now = Tween.Now;
            float dt = fxLastTick < 0 ? 0 : Mathf.Clamp(now - fxLastTick, 0, 0.5f);
            fxLastTick = now;
            if (session == null)
                return;
            var st = session.State;
            if (fxSession != session)
            {
                fxSession = session;
                session.JobCompleted += OnJobDoneFx;
                fxHearthLevel = st.HearthLevel;
                flyers.Clear();
                floaters.Clear();
                roomDoneAt.Clear();
                Array.Clear(inFlight, 0, inFlight.Length);
                for (int i = 0; i < plateShown.Length; i++)
                    plateShown[i] = st.Wallet[(ResourceType)i];
            }
            if (st.HearthLevel > fxHearthLevel)
            {
                levelUpAt = now;
                floaters.Add(new Floater
                {
                    Text = "Очаг: уровень " + st.HearthLevel + "!", At = new Vector2(hearthBadgeOnScreen.xMax, hearthBadgeOnScreen.yMax),
                    Start = now + 0.15f, Duration = 2.2f, Rise = 18, Color = new Color(1f, 0.86f, 0.4f, 1), Size = 20,
                });
            }
            fxHearthLevel = st.HearthLevel;
            for (int i = 0; i < plateShown.Length; i++)
            {
                double target = st.Wallet[(ResourceType)i] - Math.Max(0, inFlight[i]);
                if (target <= plateShown[i])
                    plateShown[i] = target; // spending shows at once
                else
                    plateShown[i] = Math.Min(target, plateShown[i] + (target - plateShown[i]) * Math.Min(1, dt * 9) + dt * 4);
            }
        }

        /// <summary>The animation layer, last in OnGUI: flying icons, floating numbers, the badge's echo, the loading fade.</summary>
        void DrawFx()
        {
            FxTick();
            FxStyles();
            fxScreenCenter = ToScreen(new Vector2(Screen.width / uiScale / 2, Screen.height / uiScale / 2));
            var old = GUI.color;
            var m = GUI.matrix;
            float now = Tween.Now;
            if (session != null)
            {
                DrawBadgeEcho(now);
                DrawFlyers(now);
                DrawPlateBumps(now);
                DrawFloaters(now);
            }
            GUI.color = old;
            GUI.matrix = m;
            DrawLoadingFade();
            GUI.color = old;
        }

        void DrawFlyers(float now)
        {
            for (int i = flyers.Count - 1; i >= 0; i--)
            {
                var f = flyers[i];
                float t = (now - f.Start) / f.Duration;
                if (t < 0)
                    continue;
                if (t >= 1)
                {
                    if (!f.Arrived && f.ToPlate)
                    {
                        inFlight[(int)f.Type] = Math.Max(0, inFlight[(int)f.Type] - f.Share);
                        plateBumpAt[(int)f.Type] = now;
                    }
                    flyers.RemoveAt(i);
                    continue;
                }
                var tex = ArtLibrary.Tex("Icons/" + f.Type);
                if (tex == null)
                    continue;
                var from = FromScreen(f.From);
                var burst = from + f.Burst;
                Vector2 to = burst + new Vector2(0, -150);
                if (f.ToPlate && plateOnScreen.TryGetValue(f.Type, out var plate))
                {
                    var pr = FromScreen(plate);
                    to = new Vector2(pr.x + 15, pr.y + 14); // the plate's icon
                }
                // first a pop out of the room, then an arc up into the plate, faster at the end
                const float pop = 0.22f;
                Vector2 p;
                float size;
                float alpha = 1;
                if (t < pop)
                {
                    float k = Tween.OutCubic(t / pop);
                    p = Tween.Lerp(from, burst, k);
                    size = Tween.Lerp(14, 40, Tween.OutBack(t / pop, 2.5f));
                }
                else
                {
                    float k = (t - pop) / (1 - pop);
                    float e = Tween.InQuad(k) * 0.65f + k * 0.35f;
                    var control = new Vector2(Tween.Lerp(burst.x, to.x, 0.3f) + f.Side * 110, Tween.Lerp(burst.y, to.y, 0.75f));
                    p = Tween.Bezier(burst, control, to, e);
                    size = Tween.Lerp(40, f.ToPlate ? 28 : 34, e);
                    if (!f.ToPlate)
                        alpha = 1 - e;
                }
                GUI.color = new Color(1, 1, 1, alpha);
                GUI.DrawTexture(new Rect(p.x - size / 2, p.y - size / 2, size, size), tex);
            }
        }

        /// <summary>The plate's icon jumps when an icon lands in it.</summary>
        void DrawPlateBumps(float now)
        {
            foreach (var kv in plateOnScreen)
            {
                float k = Tween.Progress(plateBumpAt[(int)kv.Key], 0.28f, now);
                if (k >= 1)
                    continue;
                var tex = ArtLibrary.Tex("Icons/" + kv.Key);
                if (tex == null)
                    continue;
                var r = FromScreen(kv.Value);
                var icon = Tween.Scale(new Rect(r.x - 4, r.y - 5, 38, 38), 1 + 0.32f * Tween.Bump(k));
                GUI.color = new Color(1, 1, 1, 1);
                GUI.DrawTexture(icon, tex);
            }
        }

        void DrawFloaters(float now)
        {
            for (int i = floaters.Count - 1; i >= 0; i--)
            {
                var f = floaters[i];
                float k = (now - f.Start) / f.Duration;
                if (k >= 1)
                {
                    floaters.RemoveAt(i);
                    continue;
                }
                if (k < 0)
                    continue;
                var at = FromScreen(f.At);
                float y = at.y - 20 - f.Rise * Tween.OutCubic(k);
                float a = k < 0.65f ? 1 : 1 - (k - 0.65f) / 0.35f;
                float s = Mathf.Max(0.05f, Tween.Lerp(0.5f, 1, Tween.OutBack(Tween.Clamp01(k / 0.22f), 2.6f)));
                fxNumber.fontSize = f.Size;
                fxNumber.normal.textColor = f.Color;
                float icon = f.Icon.HasValue ? f.Size * 1.5f : 0;
                float w = f.Text.Length * f.Size * 0.62f + icon;
                var r = new Rect(at.x - w / 2, y - f.Size, w, f.Size * 2);
                var m = GUI.matrix;
                GUIUtility.ScaleAroundPivot(new Vector2(s, s), r.center);
                GUI.color = new Color(1, 1, 1, a);
                if (f.Icon.HasValue && ArtLibrary.Tex("Icons/" + f.Icon.Value) is Texture2D tex)
                    GUI.DrawTexture(new Rect(r.x, r.center.y - icon / 2, icon, icon), tex);
                UiSkin.Outlined(new Rect(r.x + icon + 2, r.y, w, r.height), f.Text, fxNumber, 2f);
                GUI.matrix = m;
            }
        }

        /// <summary>Rings of gold going out of the Hearth's badge after a level up.</summary>
        void DrawBadgeEcho(float now)
        {
            var tex = ArtLibrary.Tex("UI/Badge");
            if (tex == null || now - levelUpAt > 1.6f)
                return;
            var badge = FromScreen(hearthBadgeOnScreen);
            for (int ring = 0; ring < 2; ring++)
            {
                float k = Tween.Progress(levelUpAt + ring * 0.35f, 0.9f, now);
                if (k <= 0 || k >= 1)
                    continue;
                GUI.color = new Color(1f, 0.85f, 0.4f, 0.55f * (1 - k));
                GUI.DrawTexture(Tween.Scale(badge, 1 + 1.6f * Tween.OutCubic(k)), tex);
            }
            for (int i = 0; i < 8; i++)
            {
                float k = Tween.Progress(levelUpAt + 0.05f * i, 1.1f, now);
                if (k <= 0 || k >= 1)
                    continue;
                float a = i * Mathf.PI / 4 + 0.3f, d = badge.width * (0.6f + 1.1f * Tween.OutCubic(k));
                Twinkle(new Vector2(badge.center.x + Mathf.Cos(a) * d, badge.center.y + Mathf.Sin(a) * d),
                    badge.width * 0.28f * Tween.Bump(k), 1);
            }
        }

        /// <summary>A four-pointed twinkle of light (two thin crossed bars and a bright core).</summary>
        static void Twinkle(Vector2 c, float size, float alpha)
        {
            if (size < 1 || alpha <= 0.01f)
                return;
            var old = GUI.color;
            var t = Texture2D.whiteTexture;
            float thin = Mathf.Max(1, size * 0.16f);
            GUI.color = new Color(1f, 0.95f, 0.75f, alpha * 0.9f);
            GUI.DrawTexture(new Rect(c.x - size / 2, c.y - thin / 2, size, thin), t);
            GUI.DrawTexture(new Rect(c.x - thin / 2, c.y - size / 2, thin, size), t);
            float core = Mathf.Max(1, size * 0.3f);
            GUI.color = new Color(1, 1, 1, alpha);
            GUI.DrawTexture(new Rect(c.x - core / 2, c.y - core / 2, core, core), t);
            GUI.color = old;
        }

        static float FxHash(int i)
        {
            float v = Mathf.Sin(i * 12.9898f) * 43758.5453f;
            return v - (float)Math.Floor(v);
        }
    }
}

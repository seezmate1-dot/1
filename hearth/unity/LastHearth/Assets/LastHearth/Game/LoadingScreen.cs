using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LastHearth.Core;
using UnityEngine;

namespace LastHearth.Game
{
    /// <summary>
    /// The loading screen, like a mobile game's: the painted key art (Resources/Art/Splash, from
    /// hearth/tools/art/sdpaint.py; a dark gradient over the mountain without it), the title in the
    /// game's font, a gold progress bar on the dark plate and a tip that changes every few seconds.
    /// The work is done in steps, one or a few a frame, so the bar moves; it stays at least
    /// MinSeconds and then fades out over the fortress.
    /// </summary>
    public sealed class LoadingScreen
    {
        public const float MinSeconds = 1.6f, FadeSeconds = 0.55f, HintSeconds = 3.6f;

        static readonly string[] Hints =
        {
            "Очаг греет этажи вокруг себя: чем выше его уровень, тем дальше доходит тепло.",
            "Без угля очаг гаснет, и залы промерзают. Не забывайте про угольную шахту!",
            "Тлеющий очаг бережёт уголь, Пылающий греет даже дальние этажи.",
            "Каждый вечер из тумана приходит Наплыв: держите героев наготове и закрывайте люки.",
            "За отбитый Наплыв дают жар: на него растёт Сердце очага.",
            "Ставьте жителей туда, где пригодится их лучшее умение: работа пойдёт быстрее.",
            "Промёрзший зал не работает: поднимите очаг, включите «Пылающий» или изучите Тепло.",
            "Новые залы открываются с уровнем очага, а к растущему очагу приходят беженцы.",
            "Собирайте запасы вовремя: кладовые залов не бесконечны.",
            "Герои ходят в походы и на охоту, жители держат крепость: нужны и те, и другие.",
        };

        readonly List<(string label, Action run)> steps = new List<(string, Action)>();
        readonly int firstHint;
        int next;
        float shown;
        bool drawn;
        GUIStyle title, small, hint;

        public LoadingScreen(int seed)
        {
            firstHint = Math.Abs(seed) % Hints.Length;
        }

        /// <summary>Seconds the screen has been up (the clock of its animations).</summary>
        public float Clock { get; private set; }

        /// <summary>When the bar was full (Clock), or -1.</summary>
        public float DoneAt { get; private set; } = -1;

        public bool Finished => DoneAt >= 0;

        /// <summary>The fade is over: the screen can be dropped.</summary>
        public bool Gone => Finished && Clock - DoneAt >= FadeSeconds;

        /// <summary>How opaque the screen still is while it fades out.</summary>
        public float Opacity => Finished ? 1 - Tween.InOutCubic(Tween.Clamp01((Clock - DoneAt) / FadeSeconds)) : 1;

        public string Label => next < steps.Count ? steps[next].label : steps.Count > 0 ? steps[steps.Count - 1].label : "";

        public void Add(string label, Action run) => steps.Add((label, run));

        /// <summary>The screen was drawn once: the heavy steps may start (the art shows first).</summary>
        public void MarkDrawn() => drawn = true;

        /// <summary>Runs the steps that are due, paced so the bar fills over at least MinSeconds.</summary>
        public void Tick(float dt)
        {
            // a long first frame (the loading itself) would jump the bar
            Clock += Mathf.Min(Mathf.Max(dt, 0), 0.25f);
            if (!drawn && Clock < 0.3f)
                return;
            while (next < steps.Count && (float)next / steps.Count <= Clock / MinSeconds)
                steps[next++].run();
            float real = steps.Count == 0 ? 1 : (float)next / steps.Count;
            float target = Mathf.Min(real, Clock / MinSeconds);
            shown += (target - shown) * Mathf.Min(1, dt * 8);
            if (target >= 1 && shown > 0.995f)
                shown = 1;
            if (!Finished && next >= steps.Count && shown >= 1)
                DoneAt = Clock;
        }

        // ------------------------------------------------------------------ drawing

        void Styles()
        {
            if (title != null)
                return;
            title = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            title.normal.textColor = new Color(1f, 0.85f, 0.45f, 1);
            small = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            small.normal.textColor = new Color(1f, 0.94f, 0.8f, 1);
            hint = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            hint.normal.textColor = new Color(1f, 0.94f, 0.8f, 1);
        }

        /// <summary>Draws the whole screen into s (GUI units) at the given opacity.</summary>
        public void Draw(Rect s, float alpha)
        {
            Styles();
            var old = GUI.color;
            GUI.color = new Color(0.03f, 0.03f, 0.06f, alpha);
            GUI.DrawTexture(s, Texture2D.whiteTexture);
            var splash = ArtLibrary.Data.Splash ? ArtLibrary.Tex("Splash") : null;
            if (splash != null)
            {
                // the picture covers the screen and drifts slowly closer, like a camera
                GUI.color = new Color(1, 1, 1, alpha);
                Cover(s, splash, 1.06f - 0.06f * Tween.OutCubic(Tween.Clamp01(Clock / 8f)));
            }
            else
            {
                // no painting yet: night sky over the mountain, purple fog below
                Gradient(s, new Color(0.05f, 0.06f, 0.14f, 1), new Color(0.17f, 0.1f, 0.16f, 1), alpha);
                var mountain = ArtLibrary.Tex("Mountain");
                if (mountain != null)
                {
                    float w = s.width * 1.05f, h = w * mountain.height / Mathf.Max(1, mountain.width);
                    GUI.color = new Color(0.5f, 0.5f, 0.66f, alpha);
                    GUI.DrawTexture(new Rect(s.center.x - w / 2, s.yMax - h * 0.92f, w, h), mountain);
                }
                Gradient(new Rect(s.x, s.yMax - s.height * 0.3f, s.width, s.height * 0.3f),
                    new Color(0.32f, 0.18f, 0.42f, 0), new Color(0.32f, 0.18f, 0.42f, 0.7f), alpha);
            }
            // shade the top under the title and the bottom under the bar, so the text reads over any art
            Gradient(new Rect(s.x, s.y, s.width, s.height * 0.38f), new Color(0, 0, 0, 0.5f), new Color(0, 0, 0, 0), alpha);
            Gradient(new Rect(s.x, s.yMax - s.height * 0.42f, s.width, s.height * 0.42f), new Color(0, 0, 0, 0), new Color(0, 0, 0, 0.78f), alpha);
            Snow(s, alpha);
            DrawTitle(s, alpha);
            DrawBar(s, alpha);
            GUI.color = old;
        }

        void DrawTitle(Rect s, float alpha)
        {
            title.fontSize = (int)Mathf.Clamp(s.height * 0.12f, 40, 110);
            float k = Tween.Clamp01(Clock / 0.75f);
            float scale = Tween.Lerp(0.82f, 1f, Tween.OutBack(k));
            var r = new Rect(s.x, s.y + s.height * 0.16f, s.width, title.fontSize * 1.5f);
            var m = GUI.matrix;
            GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), r.center);
            GUI.color = new Color(1, 1, 1, alpha * Tween.OutCubic(Tween.Clamp01(Clock / 0.45f)));
            UiSkin.Outlined(r, "Последний очаг", title, Mathf.Max(2f, title.fontSize / 26f));
            GUI.matrix = m;
        }

        void DrawBar(Rect s, float alpha)
        {
            float w = Mathf.Min(640, s.width * 0.6f), h = 34;
            var bar = new Rect(s.center.x - w / 2, s.yMax - s.height * 0.2f - h / 2, w, h);
            float appear = Tween.OutCubic(Tween.Clamp01((Clock - 0.15f) / 0.4f));
            GUI.color = new Color(1, 1, 1, alpha * appear);
            // what is being done, over the bar
            UiSkin.Outlined(new Rect(bar.x, bar.y - 30, bar.width, 24), Label + "…", small);
            ArtLibrary.DrawSliced(bar, "UI/Pill", 30);
            var inner = new Rect(bar.x + 7, bar.y + 7, bar.width - 14, bar.height - 14);
            float fw = inner.width * Mathf.Clamp(shown, 0, 1);
            if (fw >= 1)
            {
                // warm gold: a light top, a deeper bottom, a shine running along it
                Fill(new Rect(inner.x, inner.y, fw, inner.height), new Color(0.96f, 0.68f, 0.22f, 1), alpha * appear);
                Fill(new Rect(inner.x, inner.y, fw, inner.height * 0.38f), new Color(1f, 0.9f, 0.55f, 1), alpha * appear);
                Fill(new Rect(inner.x, inner.yMax - inner.height * 0.28f, fw, inner.height * 0.28f), new Color(0.78f, 0.42f, 0.12f, 1), alpha * appear);
                float sx = inner.x + ((Clock * 0.7f) % 1.4f - 0.2f) * inner.width;
                float x0 = Mathf.Max(inner.x, sx), x1 = Mathf.Min(inner.x + fw, sx + 36);
                if (x1 - x0 >= 1)
                    Fill(new Rect(x0, inner.y, x1 - x0, inner.height), new Color(1, 1, 0.9f, 0.35f), alpha * appear);
                // an ember rides the end of the bar
                var ember = ArtLibrary.Tex("Icons/Ember");
                if (ember != null)
                {
                    float es = h * 1.25f, bob = Mathf.Sin(Clock * 6) * 2;
                    GUI.color = new Color(1, 1, 1, alpha * appear);
                    GUI.DrawTexture(new Rect(inner.x + fw - es / 2, bar.center.y - es / 2 + bob, es, es), ember);
                }
            }
            GUI.color = new Color(1, 1, 1, alpha * appear);
            UiSkin.Outlined(bar, Mathf.RoundToInt(Mathf.Clamp(shown, 0, 1) * 100) + "%", small);
            // the tip of the moment, crossfading to the next one
            int n = (int)(Clock / HintSeconds);
            float phase = Clock - n * HintSeconds;
            float fade = Mathf.Min(1, Mathf.Min(phase / 0.35f, (HintSeconds - phase) / 0.35f));
            GUI.color = new Color(1, 1, 1, alpha * appear * Mathf.Clamp(fade, 0, 1));
            float hw = Mathf.Min(900, s.width * 0.8f);
            UiSkin.Outlined(new Rect(s.center.x - hw / 2, bar.yMax + 14, hw, 48), Hints[(firstHint + n) % Hints.Length], hint);
        }

        /// <summary>Snowflakes drifting down over the picture.</summary>
        void Snow(Rect s, float alpha)
        {
            for (int i = 0; i < 70; i++)
            {
                float h1 = Hash(i * 3 + 1), h2 = Hash(i * 3 + 2), h3 = Hash(i * 3 + 3);
                float x = Frac(h1 + Clock * 0.012f * (h2 - 0.4f)) * s.width + Mathf.Sin(Clock * (0.6f + h3) + i) * 8;
                float y = Frac(h2 + Clock * (0.025f + 0.05f * h3)) * s.height;
                float size = 1.5f + 2.6f * h3;
                GUI.color = new Color(1, 1, 1, alpha * (0.25f + 0.5f * h1));
                GUI.DrawTexture(new Rect(s.x + x, s.y + y, size, size), Texture2D.whiteTexture);
            }
        }

        static float Hash(int i)
        {
            float v = Mathf.Sin(i * 12.9898f) * 43758.5453f;
            return Frac(v);
        }

        static float Frac(float v) => v - (float)Math.Floor(v);

        static void Fill(Rect r, Color c, float alpha)
        {
            GUI.color = new Color(c.r, c.g, c.b, c.a * alpha);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
        }

        /// <summary>A vertical gradient from top to bottom, in bands.</summary>
        static void Gradient(Rect r, Color top, Color bottom, float alpha, int bands = 24)
        {
            float bh = r.height / bands;
            for (int i = 0; i < bands; i++)
            {
                float t = (i + 0.5f) / bands;
                GUI.color = new Color(Tween.Lerp(top.r, bottom.r, t), Tween.Lerp(top.g, bottom.g, t), Tween.Lerp(top.b, bottom.b, t),
                    Tween.Lerp(top.a, bottom.a, t) * alpha);
                GUI.DrawTexture(new Rect(r.x, r.y + i * bh, r.width, bh), Texture2D.whiteTexture); // edge to edge: an overlap would stripe the see-through bands
            }
        }

        /// <summary>The picture scaled to cover the rectangle, cropped in the middle, zoomed by zoom.</summary>
        static void Cover(Rect r, Texture2D tex, float zoom)
        {
            float ta = tex.width / (float)Mathf.Max(1, tex.height), ra = r.width / Mathf.Max(1, r.height);
            float uw = ta > ra ? ra / ta : 1, uh = ta > ra ? 1 : ta / ra;
            uw /= zoom;
            uh /= zoom;
            GUI.DrawTextureWithTexCoords(r, tex, new Rect((1 - uw) / 2, (1 - uh) / 2, uw, uh));
        }
    }

    /// <summary>The start of the game behind the loading screen: the save, the session, the art.</summary>
    public sealed partial class GameRunner
    {
        LoadingScreen loader;

        /// <summary>Called by Awake: the save is read and the fortress caught up behind the loading screen.</summary>
        void BeginLoading(Core.Balance balance)
        {
            loader = new LoadingScreen(Environment.TickCount);
            GameState saved = null;
            loader.Add("Открываем летопись крепости", () =>
                saved = File.Exists(savePath) ? SaveSystem.FromJson(File.ReadAllText(savePath)) : null);
            loader.Add("Считаем, что случилось без вас", () =>
            {
                if (session == null)
                    StartSession(saved != null ? new GameSession(balance, saved) : NewGame.Create(balance, Now()));
            });
            // the pictures are loaded (and sent to the graphics card) a part a frame
            List<string> art = null;
            const int parts = 6;
            for (int i = 0; i < parts; i++)
            {
                int part = i;
                loader.Add(i < 3 ? "Разжигаем очаг" : "Будим жителей", () =>
                {
                    art = art ?? WarmList();
                    int from = art.Count * part / parts, to = art.Count * (part + 1) / parts;
                    for (int k = from; k < to; k++)
                        ArtLibrary.Tex(art[k]);
                });
            }
        }

        /// <summary>Every picture the fortress shows at once: rooms, the interface, icons, characters.</summary>
        List<string> WarmList()
        {
            var list = new List<string> { "Mountain", "HearthFire", "Rooms/Empty", "Rooms/Rock", "Rooms/Frost" };
            if (session != null)
                list.AddRange(session.Balance.Rooms.Select(r => "Rooms/" + r.Type));
            if (ArtLibrary.Data.Framed)
                list.Add("Rooms/Frame");
            if (ArtLibrary.Data.Passages)
                list.AddRange(new[] { "Rooms/Door", "Rooms/Ladder" });
            if (ArtLibrary.Data.Stairs)
                list.AddRange(new[] { "Rooms/Stairs", "Rooms/Arch" });
            list.AddRange(new[] { "Badge", "Pill", "Bubble", "Plus", "Round", "Dot", "Bar", "Window", "ButtonOn", "Close", "Gear" }.Select(n => "UI/" + n));
            list.AddRange(Enum.GetValues(typeof(ResourceType)).Cast<ResourceType>().Select(t => "Icons/" + t));
            list.AddRange(new[] { "Collect", "Map", "Fortress", "Growth", "Routes", "Rewards", "Quests", "Shop", "Events" }.Select(n => "Buttons/" + n));
            foreach (var sheet in ArtLibrary.Data.Sheets.Keys)
            {
                list.Add("Sheets/" + sheet);
                list.Add("Portraits/" + sheet);
            }
            return list.Distinct().ToList();
        }

        /// <summary>Update's first line: true while loading (the game waits).</summary>
        bool LoadingTick()
        {
            if (loader == null)
                return session == null;
            bool was = loader.Finished;
            loader.Tick(Time.unscaledDeltaTime);
            if (!was && loader.Finished)
                sounds?.Play("Hearth", 0.5f);
            if (loader.Gone)
                loader = null;
            return session == null || (loader != null && !loader.Finished);
        }

        /// <summary>OnGUI's hook: draws the loading screen and returns true while it covers everything.</summary>
        bool DrawLoading()
        {
            if (loader == null)
                return session == null;
            loader.MarkDrawn();
            if (loader.Finished && session != null)
                return false; // the fortress shows; the fade is drawn over it last (DrawFx)
            loader.Draw(new Rect(0, 0, Screen.width / uiScale, Screen.height / uiScale), 1);
            return true;
        }

        /// <summary>The loading screen fading out over the fortress.</summary>
        void DrawLoadingFade()
        {
            if (loader != null && loader.Finished)
                loader.Draw(new Rect(0, 0, Screen.width / uiScale, Screen.height / uiScale), loader.Opacity);
        }
    }
}

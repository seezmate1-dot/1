using System;
using System.Linq;
using UnityEngine;

namespace LastHearth.Game
{
    /// <summary>
    /// The map of the kingdom, like Hustle Castle's: an archipelago that scrolls under a finger. The
    /// fortress has its own island; the campaign's acts run across three islands; the arena, the
    /// hunters' woods, the camp to raid, the crossroads of the routes and the portal of the events
    /// stand on islands of their own. A tap on a place opens its window, a tap on the fortress (or
    /// the "Крепость" button) goes home.
    /// </summary>
    public sealed partial class GameRunner
    {
        bool onMap;
        bool mapFramed;
        Vector2 mapScroll;
        Rect sideOnScreen;

        static readonly (string key, string name, Win win)[] MapPlaces =
        {
            ("Fortress", "Крепость", Win.None), ("Arena", "Арена", Win.Arena), ("Hunt", "Охота", Win.Hunt),
            ("Raids", "Набеги", Win.Raids), ("Routes", "Вылазки", Win.Routes), ("Events", "События", Win.Events),
        };

        void OpenMap(bool open)
        {
            sounds?.Play("Page", 0.35f);
            onMap = open;
        }

        /// <summary>The next mission can be fought: the map's button and the current act wear a red dot.</summary>
        bool MissionWaits()
        {
            var st = session.State;
            var c = session.Balance.Campaign;
            return session.Squad().Count > 0 && st.PendingMission < 0 && session.MissionsCleared < c.Missions.Count &&
                   session.Mission(session.MissionsCleared).Rank <= st.HearthLevel;
        }

        bool PlaceWaits(string key, long now)
        {
            var st = session.State;
            switch (key)
            {
                case "Hunt": return st.HearthLevel >= session.Balance.Activities.Hunt.Unlock && session.HuntTracks(now) > 0;
                case "Events": return session.ActiveRuns(now).Any();
                default: return false;
            }
        }

        void DrawWorldMap(Rect area, long now)
        {
            HudStyles();
            const float top = HudTop, bottom = HudBottom, right = HudRight;
            hudRects = new[]
            {
                new Rect(area.x, area.y, area.width, top),
                new Rect(area.x, area.yMax - bottom, area.width, bottom),
                new Rect(area.xMax - right, area.y + top, right, SideButtons(false) * 76),
            };
            // the islands' names keep out from under the buttons along the right edge (MapPlate)
            sideOnScreen = ToScreen(hudRects[2]);
            var mouse = Event.current.mousePosition;
            blockCutaway = window != Win.None || hudRects.Any(r => r.Contains(mouse));

            var tex = ArtLibrary.Tex("Map");
            var map = ArtLibrary.Data.Map ?? new ArtLibrary.MapInfo();
            // the map is bigger than the screen: a finger moves it, like the fortress
            float w = Mathf.Max(area.width * 1.1f, area.height * 16f / 9f), h = w * 9f / 16f;
            GUILayout.BeginArea(area);
            if (!mapFramed)
            {
                // open on the fortress's island, in the free middle of the screen
                mapFramed = true;
                if (map.Places.TryGetValue("Fortress", out var home))
                {
                    mapScroll.x = Math.Max(0, home.X * w - area.width * 0.35f);
                    mapScroll.y = Math.Max(0, home.Y * h - (top + (area.height - top - bottom) * 0.5f));
                }
            }
            mapScroll = GUILayout.BeginScrollView(mapScroll, GUIStyle.none, GUIStyle.none);
            var r = GUILayoutUtility.GetRect(w, h, GUILayout.Width(w), GUILayout.Height(h));
            if (tex != null)
                GUI.DrawTexture(r, tex);
            DrawSea(r, map);
            DrawShips(r, map);
            DrawActs(r, map, now);
            foreach (var (key, name, win) in MapPlaces)
            {
                if (!map.Places.TryGetValue(key, out var p))
                    continue;
                var at = new Vector2(r.x + p.X * r.width, r.y + p.Y * r.height);
                if (MapMarker(at, name, key == "Fortress" ? 150 : 120, PlaceWaits(key, now)))
                {
                    if (win == Win.None)
                        OpenMap(false);
                    else
                        Open(win);
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            DrawTopBar(new Rect(area.x, area.y, area.width, top));
            DrawSideButtons(new Rect(area.xMax - right, area.y + top + 4, right, area.height - top - bottom - 8), now, false);
            DrawBottomButtons(new Rect(area.x, area.yMax - bottom, area.width - right, bottom), now);
            DrawToast(area);
        }

        /// <summary>The sea lives: sun glints come and go on the water, each in its own rhythm, drifting a little
        /// with the waves (the points are picked on the painted water by hearth/tools/art/integrate.py).</summary>
        void DrawSea(Rect r, ArtLibrary.MapInfo map)
        {
            if (map.Sea == null || map.Sea.Count == 0)
                return;
            var glint = ArtLibrary.Tex("UI/Glint");
            if (glint == null)
                return;
            float t = Time.time, s = r.width * 0.008f;
            var keep = GUI.color;
            for (int i = 0; i < map.Sea.Count; i++)
            {
                var p = map.Sea[i];
                float phase = i * 2.39996f; // the golden angle: no two glints in step
                float a = Mathf.Sin(t * (0.7f + i % 5 * 0.15f) + phase);
                if (a <= 0)
                    continue;
                a *= a * a;
                float drift = Mathf.Sin(t * 0.35f + phase) * s;
                float k = s * (0.7f + 0.8f * a);
                GUI.color = new Color(1, 1, 1, a * 0.8f);
                GUI.DrawTexture(new Rect(r.x + p.X * r.width + drift - k, r.y + p.Y * r.height - k * 0.4f, 2 * k, k * 0.8f), glint);
            }
            GUI.color = keep;
        }

        /// <summary>Little ships sail the lanes between the islands, there and back, slowing where they turn and
        /// bobbing on the waves (the lanes run over open water only: hearth/tools/art/integrate.py).</summary>
        void DrawShips(Rect r, ArtLibrary.MapInfo map)
        {
            if (!ArtLibrary.Data.MapShips || map.Ships == null || map.Ships.Count == 0)
                return;
            var tex = ArtLibrary.Tex("Map/Ship");
            if (tex == null)
                return;
            float t = Time.time, s = r.width * 0.035f;
            for (int i = 0; i < map.Ships.Count; i++)
            {
                var lane = map.Ships[i];
                if (lane == null || lane.Count < 2)
                    continue;
                var a = lane[0];
                var b = lane[lane.Count - 1];
                float period = 80f + i * 23f, u = t / period + i * 0.37f;
                u -= (float)Math.Floor(u);
                bool back = u > 0.5f;
                float k = back ? (1f - u) * 2f : u * 2f;
                k = k * k * (3f - 2f * k); // easing at the ends of the lane, where the ship turns
                float x = r.x + (a.X + (b.X - a.X) * k) * r.width;
                float y = r.y + (a.Y + (b.Y - a.Y) * k) * r.height + Mathf.Sin(t * 1.3f + i) * s * 0.04f;
                var rect = new Rect(x - s / 2, y - s * 0.8f, s, s);
                // the picture sails to the right; going left it is mirrored
                if ((b.X - a.X) * (back ? -1 : 1) < 0)
                    GUI.DrawTextureWithTexCoords(rect, tex, new Rect(1, 0, -1, 1));
                else
                    GUI.DrawTexture(rect, tex);
            }
        }

        /// <summary>A place on the map: its name on a plaque under it (a red dot when something waits); the
        /// building over it and the plaque take the tap.</summary>
        bool MapMarker(Vector2 at, string name, float size, bool dot)
        {
            var plaque = MapPlate(new Rect(at.x - 56, at.y + size * 0.28f, 112, 28));
            ArtLibrary.DrawSliced(plaque, "UI/Pill", 30);
            UiSkin.Outlined(plaque, name, buttonLabel);
            if (dot)
                ArtLibrary.Draw(new Rect(plaque.xMax - 12, plaque.y - 8, 20, 20), "UI/Dot");
            var hit = new Rect(at.x - size / 2, at.y - size * 0.6f, size, size * 0.6f + plaque.yMax - at.y);
            if (!Tap(hit))
                return false;
            sounds?.Play("Click", 0.4f);
            return true;
        }

        /// <summary>A name plate on the map moved to the left, out from under the buttons along the right edge
        /// («Награды», «Задания»...), so neither hides the other; anywhere else it stays under its place.</summary>
        Rect MapPlate(Rect plate)
        {
            var col = FromScreen(sideOnScreen); // the map scrolls: the column in the map's own coordinates
            if (plate.xMax > col.x - 4 && plate.x < col.xMax && plate.yMax > col.y && plate.y < col.yMax)
                plate.x = col.x - 4 - plate.width;
            return plate;
        }

        /// <summary>The ten acts of the campaign: the passed ones carry their number, the current one pulses
        /// with its name and opens the campaign; the Fog still hides the ones after it.</summary>
        void DrawActs(Rect r, ArtLibrary.MapInfo map, long now)
        {
            var c = session.Balance.Campaign;
            int next = session.MissionsCleared;
            int currentAct = next < c.Missions.Count ? session.Mission(next).Act : c.Acts.Count + 1;
            for (int i = 0; i < map.Acts.Count && i < c.Acts.Count; i++)
            {
                var p = map.Acts[i];
                int act = i + 1;
                var at = new Vector2(r.x + p.X * r.width, r.y + p.Y * r.height);
                bool current = act == currentAct, fog = act > currentAct;
                float s = (current ? 44 : 34) * (current ? 1 + 0.06f * Mathf.Sin(Time.time * 4) : 1);
                var b = new Rect(at.x - s / 2, at.y - s / 2, s, s);
                var old = GUI.color;
                if (fog)
                    GUI.color = new Color(0.45f, 0.45f, 0.55f, 0.85f);
                ArtLibrary.Draw(b, "UI/Badge");
                GUI.color = old;
                UiSkin.Outlined(new Rect(b.x, b.y - 1, b.width - 2, b.height - 2), act.ToString(), current ? titleText : buttonLabel);
                if (current)
                {
                    TutorTarget("act", b);
                    // as wide as the name, so it hides as little of the acts next to it as it can
                    float pw = Mathf.Clamp(c.Acts[i].Name.Length * 8.5f + 30, 90, 200);
                    var plaque = MapPlate(new Rect(at.x - pw / 2, b.yMax - 2, pw, 26));
                    ArtLibrary.DrawSliced(plaque, "UI/Pill", 30);
                    UiSkin.Outlined(plaque, c.Acts[i].Name, buttonLabel);
                    if (MissionWaits())
                        ArtLibrary.Draw(new Rect(b.xMax - 14, b.y - 4, 18, 18), "UI/Dot");
                }
                if (!Tap(new Rect(at.x - 30, at.y - 30, 60, 60)))
                    continue;
                sounds?.Play("Click", 0.4f);
                if (fog)
                    message = "Туман ещё скрывает этот край: сначала пройдите акт " + currentAct;
                else
                    Open(Win.Campaign);
            }
        }
    }
}

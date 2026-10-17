using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core;
using LastHearth.Core.Combat;
using LastHearth.Core.Surge;
using UnityEngine;

namespace LastHearth.Game
{
    /// <summary>
    /// The cutaway of the fortress (GDD: 2.5D like Hustle Castle): the snowy peak with the watchtowers
    /// above, floors cut into the rock, rendered rooms with their decorations, the hearth with its
    /// living fire, frost over frozen rooms, dwellers walking and working, the creatures of the Surge.
    /// Tap a room for its actions, tap a dug empty cell to build, tap rubble to clear it; the parts of
    /// the mountain that open at a higher hearth level stand closed. Art: hearth/tools/art.
    /// </summary>
    public sealed partial class GameRunner
    {
        int selectedRoom = -1;
        int selectedFloor = -1, selectedCell = -1;
        float viewWidth = 900;
        bool decorPicker;
        bool pairPicker;
        int pairMan = -1, pairWoman = -1;
        GUIStyle cellStyle;
        Vector2? cutawayBand;        // the main screen: the top and bottom of the fortress the bars leave visible, screen y
        bool cutawayScene;           // the main screen with the places on the surface (GameRunner.Scene.cs)

        /// <summary>Where each dweller is in its room and what it is doing (screen only, not saved).</summary>
        sealed class Walker
        {
            public float X = 0.5f, Target = 0.5f, Timer;
            public int Mode; // 0 idle, 1 walk, 2 work, 3 cheer
            public float Phase;
            public bool Left;
        }

        readonly Dictionary<int, Walker> walkers = new Dictionary<int, Walker>();
        readonly System.Random walkRng = new System.Random(7);

        void CellText(Rect r, string text)
        {
            if (cellStyle == null)
                cellStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true, wordWrap = false };
            var old = GUI.color;
            GUI.color = Color.black;
            GUI.Label(new Rect(r.x + 7, r.y + 6, r.width - 8, r.height), StripColor(text), cellStyle);
            GUI.color = old;
            GUI.Label(new Rect(r.x + 6, r.y + 5, r.width - 8, r.height), text, cellStyle);
        }

        GUIStyle badgeStyle;

        GUIStyle BadgeStyle() => badgeStyle ?? (badgeStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false });

        /// <summary>The level of a room on a round gold badge in its top left corner.</summary>
        void Badge(Rect r, int level)
        {
            float s = Mathf.Max(22, r.height * 0.24f);
            var b = new Rect(r.x + r.width * 0.035f, r.y + r.height * 0.06f, s, s);
            ArtLibrary.Draw(b, "UI/Badge");
            UiSkin.Outlined(new Rect(b.x, b.y - 1, b.width - 2, b.height - 2), level.ToString(), BadgeStyle());
        }

        /// <summary>A short text (a timer) on a dark plate along the bottom of a cell: wide enough for
        /// the text, and inside the cell (the floor below is drawn over anything under it).</summary>
        void Pill(Rect r, string text)
        {
            float h = Mathf.Max(18, r.height * 0.15f);
            float w = Mathf.Clamp(text.Length * 8.5f + 24, r.width * 0.4f, r.width - 8);
            var p = new Rect(r.center.x - w / 2, r.yMax - h - r.height * 0.04f, w, h);
            ArtLibrary.DrawSliced(p, "UI/Pill", 30);
            UiSkin.Outlined(p, text, BadgeStyle());
        }

        /// <summary>The cloud with the resource's icon floating over a room; true when it was tapped.</summary>
        bool Bubble(Rect r, ResourceType resource, int seed)
        {
            float s = Mathf.Max(28, r.height * 0.34f);
            float bob = Mathf.Sin(Time.time * 2.5f + seed) * s * 0.06f;
            var b = new Rect(r.center.x - s / 2, r.y + r.height * 0.14f + bob, s, s);
            ArtLibrary.Draw(b, "UI/Bubble");
            // the disc of the cloud is 0.06..0.94 wide and 0.06..0.86 high: the icon fills its middle
            float icon = s * 0.62f;
            ArtLibrary.Draw(new Rect(b.x + s * 0.5f - icon / 2, b.y + s * 0.46f - icon / 2, icon, icon), "Icons/" + resource);
            return Tap(new Rect(b.x - s * 0.15f, b.y - s * 0.15f, s * 1.3f, s * 1.3f));
        }

        /// <summary>1 234 → 1,2 тыс.: numbers that fit a narrow cell.</summary>
        static string Short(double v)
        {
            if (v >= 1000000)
                return (v / 1000000).ToString("0.#") + " млн";
            if (v >= 1000)
                return (v / 1000).ToString("0.#") + " тыс.";
            return v.ToString("0");
        }

        static string StripColor(string text)
        {
            var sb = new System.Text.StringBuilder();
            bool tag = false;
            foreach (char c in text)
            {
                if (c == '<')
                    tag = true;
                else if (c == '>')
                    tag = false;
                else if (!tag)
                    sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>Floors worth showing: the built and the open ones, and one closed floor above and below.</summary>
        (int top, int bottom) ShownFloors()
        {
            var g = session.Balance.Grid;
            int top = g.HearthFloor, bottom = g.HearthFloor;
            foreach (var r in session.State.Rooms)
            {
                top = Math.Max(top, r.Floor);
                bottom = Math.Min(bottom, r.Floor);
            }
            foreach (var s in g.Sections.Where(s => s.Hearth <= session.State.HearthLevel))
            {
                top = Math.Max(top, s.FloorTo);
                bottom = Math.Min(bottom, s.FloorFrom);
            }
            return (Math.Min(g.Floors - 1, top + 1), Math.Max(0, bottom - 1));
        }

        // ------------------------------------------------------------------ the dwellers move

        void UpdateWalkers(float dt)
        {
            foreach (var d in session.State.Dwellers)
            {
                if (!walkers.TryGetValue(d.Id, out var w))
                {
                    walkers[d.Id] = w = new Walker { X = 0.2f + (float)walkRng.NextDouble() * 0.6f, Phase = (float)walkRng.NextDouble() * 10 };
                    w.Target = w.X;
                }
                w.Phase += dt;
                w.Timer -= dt;
                if (w.Mode == 1)
                {
                    float step = 0.07f * dt;
                    if (Mathf.Abs(w.Target - w.X) <= step)
                    {
                        w.X = w.Target;
                        w.Timer = 0;
                    }
                    else
                    {
                        w.X += Mathf.Sign(w.Target - w.X) * step;
                    }
                }
                if (w.Timer > 0)
                    continue;
                // what next: workers mostly work, the others idle and stroll
                double roll = walkRng.NextDouble();
                bool working = d.RoomId >= 0 && session.FindRoom(d.RoomId) is Room room && session.Balance.Room(room.Type).Produces != null;
                if (session.PairingOf(d.Id) != null)
                    working = false; // a couple waiting for a child stays together
                if (roll < (working ? 0.45 : 0.15))
                {
                    w.Mode = 2;
                    w.Timer = 3 + (float)walkRng.NextDouble() * 4;
                }
                else if (roll < 0.75)
                {
                    w.Mode = 1;
                    w.Target = 0.16f + (float)walkRng.NextDouble() * 0.68f;
                    w.Left = w.Target < w.X;
                    w.Timer = 20;
                }
                else
                {
                    w.Mode = roll < 0.8 && !working ? 3 : 0;
                    w.Timer = 2 + (float)walkRng.NextDouble() * 3;
                }
                w.Phase = 0;
            }
        }

        /// <summary>
        /// Dwellers in a room: its workers (a couple in the living quarters); dwellers without work live in
        /// the first living quarters (children appear there); heroes off duty rest in the barracks (or by the hearth).
        /// </summary>
        IEnumerable<Dweller> PeopleIn(Room room, bool hearth)
        {
            var st = session.State;
            if (room != null && room.Type == RoomType.Bedrooms && st.Rooms.FirstOrDefault(r => r.Type == RoomType.Bedrooms) == room)
            {
                foreach (var d in st.Dwellers.Where(x => !x.IsHero && x.RoomId < 0 && x.RouteId == 0).Take(3 * room.Width))
                    yield return d;
            }
            if (room != null)
            {
                foreach (int id in room.Workers)
                {
                    var d = session.FindDweller(id);
                    if (d != null)
                        yield return d;
                }
            }
            bool barracks = room != null && room.Type == RoomType.Barracks;
            bool hasBarracks = st.Rooms.Any(r => r.Type == RoomType.Barracks);
            if (barracks || (hearth && !hasBarracks))
            {
                foreach (var d in st.Dwellers.Where(x => x.IsHero && x.RoomId < 0 && x.RouteId == 0).Take(5))
                    yield return d;
            }
        }

        void DrawPeople(Rect cell, IEnumerable<Dweller> people)
        {
            // the room art (hearth/tools/art/rooms.py): a 1.8 m person stands 0.40 of the cell high, feet at 0.80
            float h = cell.height * 0.40f;
            float floorY = cell.y + cell.height * 0.80f;
            var list = people.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                var d = list[i];
                if (!walkers.TryGetValue(d.Id, out var w))
                    continue;
                string sheet = d.IsHero ? ArtLibrary.HeroSheet(d.HeroClass) : ArtLibrary.DwellerSheet(d.Id);
                string row = w.Mode == 1 ? "walk" : w.Mode == 2 ? (d.IsHero ? "attack" : "work") : w.Mode == 3 ? "cheer" : "idle";
                // stand a little apart in depth: the later ones a bit higher and smaller
                float depth = (i % 3) * 0.035f;
                var feet = new Vector2(cell.x + cell.width * w.X, floorY - cell.height * depth);
                ArtLibrary.DrawCharacter(sheet, row, w.Phase, feet, h * (1 - depth), w.Mode == 1 && w.Left);
            }
        }

        // ------------------------------------------------------------------ the cutaway

        /// <summary>Draws the cutaway; the creatures of a Surge, if any, climb on top of it.</summary>
        void DrawCutaway(long now, SurgeDefense live = null)
        {
            var b = session.Balance;
            var g = b.Grid;
            var st = session.State;
            var (top, bottom) = ShownFloors();
            bool tower = live == null && ArtLibrary.Data.Stairs;
            float cellW = CellWidth(tower);
            float cellH = cellW * 0.5f; // the room pictures are 2:1
            float towerW = tower ? cellW * TowerCells : 0, fullW = cellW * g.Cells + towerW;
            float mountainH = live == null ? fullW * 0.5f : 0;
            var area = GUILayoutUtility.GetRect(fullW, mountainH * MountainShown + cellH * (top - bottom + 1));
            if (mountainH > 0)
                ArtLibrary.Draw(new Rect(area.x, area.y - mountainH * MountainSky, fullW, mountainH), "Mountain");
            float gridY = area.y + mountainH * MountainShown;
            if (mountainH > 0 && cutawayScene)
                DrawScene(new Rect(area.x, area.y - mountainH * MountainSky, fullW, mountainH), gridY, now);
            // the rooms start right of the stair tower
            float gx = area.x + towerW;
            if (tower)
                DrawStairTower(area.x, towerW, gridY, top, bottom, cellH);
            for (int f = top; f >= bottom; f--)
            {
                float y = gridY + (top - f) * cellH;
                for (int c = 0; c < g.Cells; c++)
                {
                    var r = new Rect(gx + c * cellW, y, cellW, cellH);
                    if (session.IsHearthCell(f, c))
                    {
                        if (c == g.HearthCells[0])
                            DrawHearthCell(new Rect(r.x, r.y, cellW * g.HearthCells.Length, cellH), now);
                        continue;
                    }
                    var room = session.RoomAt(f, c);
                    if (room != null)
                    {
                        // a merged room is one picture over all its cells
                        if (c == room.Cell)
                            DrawRoomCell(new Rect(r.x, r.y, cellW * room.Width, cellH), room, now);
                    }
                    else
                    {
                        DrawEmptyCell(r, f, c, now);
                    }
                }
                if (live == null)
                    DrawLockedLabels(gx, y, f, cellW, cellH);
                if (live != null)
                    DrawCreatures(area, gridY, top, f, cellW, cellH, live);
            }
            if (live == null && (ArtLibrary.Data.Passages || tower))
                DrawPassages(gx, gridY, top, bottom, cellW, cellH, tower);
        }

        /// <summary>The stair tower beside the cutaway is half a cell wide (Art.json "stairs").</summary>
        const float TowerCells = 0.5f;

        /// <summary>The width of a cell of the cutaway: the cells (and the stair tower) fill the view.</summary>
        float CellWidth(bool tower) => Math.Max(60, (viewWidth - 8) / (session.Balance.Grid.Cells + (tower ? TowerCells : 0)));

        /// <summary>A floor the stair tower reaches: some of it is dug or being dug (not a closed section).</summary>
        bool TowerFloor(int floor)
        {
            for (int c = 0; c < session.Balance.Grid.Cells; c++)
            {
                var state = session.CellStateAt(floor, c);
                if (state != CellState.Locked && state != CellState.Outside)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// The stair tower left of the rooms: one shaft cut into the rock through every floor it reaches, a
        /// flight of wooden stairs on each floor (Rooms/Stairs), the rock of the mountain beside closed floors.
        /// </summary>
        void DrawStairTower(float x, float w, float gridY, int top, int bottom, float cellH)
        {
            int f = top;
            while (f >= bottom)
            {
                float y = gridY + (top - f) * cellH;
                if (!TowerFloor(f))
                {
                    ArtLibrary.Draw(new Rect(x, y, w, cellH), "Rooms/Rock");
                    f--;
                    continue;
                }
                // a run of floors the tower reaches: one opening in the rock, a flight on each floor
                int end = f;
                while (end - 1 >= bottom && TowerFloor(end - 1))
                    end--;
                var run = new Rect(x, y, w, (f - end + 1) * cellH);
                var inner = new Rect(run.x + cellH * 0.03f, run.y + cellH * 0.06f, run.width - cellH * 0.06f, run.height - cellH * 0.12f);
                float step = inner.height / (f - end + 1);
                for (int k = 0; k <= f - end; k++)
                    ArtLibrary.Draw(new Rect(inner.x, inner.y + k * step, inner.width, step), "Rooms/Stairs");
                ArtLibrary.DrawSliced(run, "Rooms/Frame", 48, cellH * 0.125f);
                f = end - 1;
            }
        }

        /// <summary>A built room (or the Hearth) stands in this cell: doors and ladders join such cells.</summary>
        bool Built(int floor, int cell)
        {
            var state = session.CellStateAt(floor, cell);
            return state == CellState.Hearth || state == CellState.Room;
        }

        /// <summary>Two cells of one room (the hearth or a merged hall): no door between them.</summary>
        bool SameRoom(int floor, int a, int b)
        {
            if (session.IsHearthCell(floor, a) && session.IsHearthCell(floor, b))
                return true;
            var room = session.RoomAt(floor, a);
            return room != null && room.Covers(floor, b);
        }

        /// <summary>
        /// The rooms join like Hustle Castle's. With the stair tower: an open arch through the rock between
        /// two built neighbours on a floor, and between the tower and the first room; the tower joins the
        /// floors. Without it: a wooden door between neighbours, a ladder through the floor.
        /// </summary>
        void DrawPassages(float x0, float gridY, int top, int bottom, float cellW, float cellH, bool tower)
        {
            var g = session.Balance.Grid;
            for (int f = top; f >= bottom; f--)
            {
                float y = gridY + (top - f) * cellH;
                for (int c = tower ? -1 : 0; c + 1 < g.Cells; c++)
                {
                    bool joined = c < 0 ? Built(f, 0) : !SameRoom(f, c, c + 1) && Built(f, c) && Built(f, c + 1);
                    if (!joined)
                        continue;
                    float bx = x0 + (c + 1) * cellW;
                    if (tower)
                    {
                        // the arch stands on the floor of the rooms, as high as their openings
                        float aw = cellH * 0.3f, ah = cellH * 0.8f;
                        ArtLibrary.Draw(new Rect(bx - aw / 2, y + cellH * 0.94f - ah, aw, ah), "Rooms/Arch");
                    }
                    else
                    {
                        float w = cellH * 0.3f, h = cellH * 0.5f;
                        ArtLibrary.Draw(new Rect(bx - w / 2, y + cellH * 0.9f - h, w, h), "Rooms/Door");
                    }
                }
                if (f == bottom || tower)
                    continue;
                for (int c = 0; c < g.Cells; c++)
                {
                    if (!Built(f, c) || !Built(f - 1, c))
                        continue;
                    // one ladder by the left wall of the room, down through the floor into the room below
                    float w = cellH * 0.17f, lx = x0 + c * cellW + cellH * 0.16f;
                    float from = y + cellH * 0.6f, to = y + cellH + cellH * 0.88f;
                    ArtLibrary.Draw(new Rect(lx, from, w, to - from), "Rooms/Ladder");
                }
            }
        }

        /// <summary>
        /// A room's picture in its cell. Painted rooms are interiors: they sit in an opening cut into the
        /// mountain's rock (Rooms/Frame, nine slices), so the cells join into one cutaway like Hustle
        /// Castle's. Returns where the interior was drawn. Without the frame, the picture fills the cell.
        /// </summary>
        Rect DrawInterior(Rect r, string path)
        {
            if (!ArtLibrary.Data.Framed)
            {
                ArtLibrary.Draw(r, path);
                return r;
            }
            float h = r.height;
            var inner = new Rect(r.x + h * 0.03f, r.y + h * 0.06f, r.width - h * 0.06f, h * 0.88f);
            ArtLibrary.Draw(inner, path);
            ArtLibrary.DrawSliced(r, "Rooms/Frame", 48, h * 0.125f);
            return inner;
        }

        void DrawHearthCell(Rect r, long now)
        {
            var old = GUI.color;
            if (session.State.OutOfCoal)
                GUI.color = new Color(0.55f, 0.6f, 0.75f, 1);
            var inner = DrawInterior(r, "Rooms/Hearth");
            GUI.color = old;
            // the living fire: its frames over the fireplace, dimmer when coal runs out
            var fire = ArtLibrary.Data.Fire;
            var tex = ArtLibrary.Tex("HearthFire");
            if (fire != null && tex != null && fire.Frames > 0)
            {
                int frame = (int)(Time.time * 10) % fire.Frames;
                var fr = new Rect(inner.x + fire.X * inner.width, inner.y + fire.Y * inner.height, fire.W * inner.width, fire.H * inner.height);
                if (session.State.OutOfCoal)
                    fr = new Rect(fr.x + fr.width * 0.2f, fr.y + fr.height * 0.5f, fr.width * 0.6f, fr.height * 0.5f);
                GUI.DrawTextureWithTexCoords(fr, tex, new Rect((float)frame / fire.Frames, 0, 1f / fire.Frames, 1));
            }
            var job = session.State.Queue.AllJobs().FirstOrDefault(j => j.IsHearth);
            DrawScaffold(inner, job, now);
            DrawPeople(r, PeopleIn(null, true));
            Badge(r, session.State.HearthLevel);
            if (job != null)
                Pill(r, job.IsRunning(now) ? Duration(job.Remaining(now)) : "ждёт");
            RoomFx(r, HearthFx);
            TutorTarget("hearth", r);
            if (Tap(r))
            {
                selectedRoom = -1;
                selectedFloor = -1;
                Open(Win.Hearth);
            }
        }

        void DrawRoomCell(Rect r, Room room, long now)
        {
            var data = session.Balance.Room(room.Type);
            var inner = DrawInterior(r, ArtLibrary.RoomArt(room.Type, room.Level));
            var job = session.State.Queue.AllJobs().FirstOrDefault(j => j.Target == room.Id);
            DrawScaffold(inner, job, now);
            string decor = session.RoomDecorId(room.Id);
            if (decor != null && ArtLibrary.Data.Decor.TryGetValue(decor, out var di))
            {
                // decorations stand in the right corner, scaled against the 7.7-unit tall room
                // decorations are rendered in metres; by the back wall of a room a metre is 0.2 of the cell high
                float h = Mathf.Clamp(di.Height * 0.2f, 0.12f, 0.6f) * r.height;
                float w = h * Mathf.Max(0.4f, di.Width / Mathf.Max(0.1f, di.Height));
                ArtLibrary.Draw(new Rect(r.xMax - r.width * 0.12f - w, r.y + r.height * 0.76f - h, w, h), "Decor/" + decor);
            }
            DrawPeople(r, PeopleIn(room, false));
            bool warm = session.IsRoomWarm(room);
            if (!warm)
                ArtLibrary.Draw(r, "Rooms/Frost");
            // no text over the art, like in Hustle Castle: the level on a badge, a cloud with the resource
            // when there is something to collect, a timer while it is being built
            Badge(r, room.Level);
            var child = session.PairingsIn(room.Id).OrderBy(p => p.End).FirstOrDefault();
            if (job != null)
                Pill(r, job.IsRunning(now) ? Duration(job.Remaining(now)) : "в очереди");
            else if (child != null)
                Pill(r, child.End > now ? "дитя " + Duration(child.End - now) : "ждёт места");
            if (selectedRoom == room.Id)
                Outline(r);
            RoomFx(r, room.Id); // the «Готово» glow, where the collected icons fly from
            TutorRoom(room.Id, r);
            if (data.Produces != null && room.Stock >= 1 && Bubble(r, data.Produces.Value, room.Id))
            {
                message = string.Format("Собрано {0}", Collecting(() => session.Collect(room.Id)));
                sounds?.Play("Coins");
                return;
            }
            if (Tap(r))
            {
                selectedRoom = room.Id;
                selectedFloor = -1;
                Open(Win.Room);
            }
        }

        /// <summary>Scaffolding over a room (or the hearth) while a builder works on it, when it is painted.</summary>
        void DrawScaffold(Rect inner, BuildJob job, long now)
        {
            if (job != null && job.IsRunning(now) && ArtLibrary.Data.Scaffold)
                ArtLibrary.Draw(inner, "Rooms/Scaffold");
        }

        /// <summary>The rock of the mountain with rubble on it (an obstacle), when the rubble is painted.</summary>
        void DrawRubble(Rect r)
        {
            ArtLibrary.Draw(r, "Rooms/Rock");
            if (ArtLibrary.Data.Rubble)
                ArtLibrary.Draw(r, "Rooms/Rubble");
        }

        /// <summary>
        /// Where the «Расчистить» sign of an obstacle goes: a little above the middle of the cell, but
        /// inside what the top plates and the bottom buttons leave visible (cutawayBand), so the sign of a
        /// half hidden row climbs out from under the bar; it keeps to its cell (at most half over its top
        /// edge) and waits for the scroll (null) when the bars hide nearly all of the cell.
        /// </summary>
        float? SignY(Rect r, float h)
        {
            float y = r.center.y - r.height * 0.1f;
            float low = r.y - h * 0.5f, high = r.yMax - h - r.height * 0.06f;
            if (!(cutawayBand is Vector2 band))
                return y;
            // the band is in screen points: back into the scrolled cutaway's own coordinates
            float visibleTop = FromScreen(new Vector2(0, band.x)).y + 4, visibleBottom = FromScreen(new Vector2(0, band.y)).y - 4;
            y = Mathf.Max(Mathf.Min(y, visibleBottom - h), visibleTop);
            if (y < low || y > high)
                return null;
            return y;
        }

        /// <summary>An obstacle next to a room, the Hearth or a free cell: the one to clear next.</summary>
        bool ReachableObstacle(int floor, int cell)
        {
            foreach (var (f, c) in new[] { (floor, cell - 1), (floor, cell + 1), (floor - 1, cell), (floor + 1, cell) })
            {
                var g = session.Balance.Grid;
                if (f < 0 || f >= g.Floors || c < 0 || c >= g.Cells)
                    continue;
                var st = session.CellStateAt(f, c);
                if (st != CellState.Locked && st != CellState.Obstacle)
                    return true;
            }
            return false;
        }

        void DrawEmptyCell(Rect r, int floor, int cell, long now)
        {
            var state = session.CellStateAt(floor, cell);
            bool selected = selectedFloor == floor && selectedCell == cell;
            switch (state)
            {
                case CellState.Locked:
                {
                    // the closed part of the mountain: darker rock, its label is drawn over the whole run
                    var old = GUI.color;
                    GUI.color = new Color(0.62f, 0.62f, 0.7f, 1);
                    ArtLibrary.Draw(r, "Rooms/Rock");
                    GUI.color = old;
                    if (Tap(r))
                        message = LockedText(session.SectionLevel(floor, cell));
                    return;
                }
                case CellState.Obstacle:
                {
                    DrawRubble(r);
                    // like Hustle Castle: only the obstacles next to the dug-out part carry a sign, the rest is plain rubble
                    float bw = Mathf.Clamp(10 * 8.5f + 30, r.width * 0.6f, r.width - 8), bh = Mathf.Max(22, r.height * 0.2f);
                    if (ReachableObstacle(floor, cell) && SignY(r, bh) is float sy)
                    {
                        var b = new Rect(r.center.x - bw / 2, sy, bw, bh);
                        ArtLibrary.DrawSliced(b, "UI/Pill", 30);
                        UiSkin.Outlined(b, "Расчистить", BadgeStyle());
                        TutorTarget("obstacle", r);
                    }
                    if (selected)
                        Outline(r);
                    if (Tap(r))
                        SelectCell(floor, cell);
                    return;
                }
                case CellState.Clearing:
                {
                    DrawRubble(r);
                    var job = session.State.Queue.Clearing(floor, cell);
                    Pill(r, job != null && job.IsRunning(now) ? Duration(job.Remaining(now)) : "в очереди");
                    if (Tap(r))
                        message = "Здесь расчищают завал";
                    return;
                }
            }
            // a dug-out empty cell: build here, if it is warm enough
            var b2 = session.Balance;
            bool warm = Formulas.IsFloorHabitable(b2, session.State.HearthLevel, session.State.Mode, floor, 1, session.WarmthMult());
            DrawInterior(r, "Rooms/Empty");
            if (!warm)
            {
                ArtLibrary.Draw(r, "Rooms/Frost");
                if (Tap(r))
                    message = FailText(Fail.TooCold);
                return;
            }
            float s = r.height * 0.36f * (1 + 0.05f * Mathf.Sin(Time.time * 3 + cell));
            ArtLibrary.Draw(new Rect(r.center.x - s / 2, r.y + r.height * 0.42f - s / 2, s, s), "UI/Plus");
            TutorTarget("free", r);
            MoveMark(r, floor, cell); // a room being moved can go here (GameRunner.Comfort.cs)
            if (selected)
                Outline(r);
            if (Tap(r) && !MoveTap(floor, cell))
                SelectCell(floor, cell);
        }

        void SelectCell(int floor, int cell)
        {
            selectedFloor = floor;
            selectedCell = cell;
            selectedRoom = -1;
            Open(Win.Build);
        }

        static string LockedText(int level) =>
            level > 0 ? string.Format("Откроется на {0}-м уровне очага", level) : "Эта часть горы не открывается";

        /// <summary>One label over each run of closed cells of a floor that open at the same hearth level.</summary>
        void DrawLockedLabels(float x0, float y, int floor, float cellW, float cellH)
        {
            int cells = session.Balance.Grid.Cells;
            for (int c = 0; c < cells;)
            {
                if (session.CellStateAt(floor, c) != CellState.Locked)
                {
                    c++;
                    continue;
                }
                int level = session.SectionLevel(floor, c);
                int end = c + 1;
                while (end < cells && session.CellStateAt(floor, end) == CellState.Locked && session.SectionLevel(floor, end) == level)
                    end++;
                float w = (end - c) * cellW;
                string text = end - c >= 3 ? LockedText(level) : level > 0 ? "очаг " + level : "";
                if (text.Length > 0)
                {
                    float pw = Mathf.Min(w - 8, Mathf.Max(70, text.Length * 8.5f));
                    var p = new Rect(x0 + c * cellW + (w - pw) / 2, y + cellH * 0.42f, pw, Mathf.Max(20, cellH * 0.16f));
                    ArtLibrary.DrawSliced(p, "UI/Pill", 30);
                    UiSkin.Outlined(p, text, BadgeStyle());
                }
                c = end;
            }
        }

        void DrawCreatures(Rect area, float gridY, int top, int floor, float cellW, float cellH, SurgeDefense live)
        {
            float y = gridY + (top - floor) * cellH;
            var here = live.Creatures.Where(x => x.Floor == floor).ToList();
            float h = cellH * 0.42f;
            bool towardsRight = floor != live.HearthFloor && live.HearthFloor > floor;
            for (int i = 0; i < here.Count; i++)
            {
                var cr = here[i];
                // creatures climb: the further on their floor, the closer to the hearth side
                float progress = cr.Progress / (float)Math.Max(1, live.Numbers.TicksPerFloor);
                float x = area.x + (0.12f + 0.76f * progress) * cellW * session.Balance.Grid.Cells + i * h * 0.25f;
                var family = (MonsterFamily)(cr.Id % 4);
                var old = GUI.color;
                GUI.color = new Color(1, 1, 1, 0.45f + 0.55f * (float)(cr.Hp / cr.MaxHp));
                ArtLibrary.DrawCharacter(ArtLibrary.CreatureSheet(family), "walk", Time.time + cr.Id, new Vector2(x, y + cellH * 0.80f), h, !towardsRight);
                GUI.color = old;
            }
            if (live.HatchClosed(floor))
                CellText(new Rect(area.x, y + cellH * 0.4f, cellW * 3, 24), "<color=#fd5>люк закрыт</color>");
        }

        void Outline(Rect r)
        {
            var old = GUI.color;
            GUI.color = new Color(1f, 0.85f, 0.3f, 1);
            var t = Texture2D.whiteTexture;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 3), t);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 3, r.width, 3), t);
            GUI.DrawTexture(new Rect(r.x, r.y, 3, r.height), t);
            GUI.DrawTexture(new Rect(r.xMax - 3, r.y, 3, r.height), t);
            GUI.color = old;
        }

        // ------------------------------------------------------------------ panels under the cutaway

        void DrawRoomPanel(Room room, long now)
        {
            var b = session.Balance;
            var data = b.Room(room.Type);
            string output = data.Produces == null ? "" :
                string.Format(" · выработка {0:0.0}/ч · запас {1:0}", session.RoomOutputPerHour(room), room.Stock);
            string size = room.Width > 1 ? string.Format(" · зал на {0} клетки, +{1:0}% выработки", room.Width, (session.MergeBonus(room) - 1) * 100) : "";
            GUILayout.Label(string.Format("<b>{0}</b>, уровень {1}, этаж {2}{3}{4}{5}", data.Name, room.Level, room.Floor + 1, size, output,
                session.IsRoomWarm(room) ? "" : " · <color=#9cf>промёрз: поднимите очаг, включите «Пылающий» или изучите Тепло</color>"), Rich());
            if (room.Type == RoomType.Forge && room.Level > 0 && ForgeTabs())
            {
                DrawForge(now); // the forging tab (GameRunner.Gear.cs)
                return;
            }
            GUILayout.BeginHorizontal();
            if (data.Produces != null && GUILayout.Button("Собрать"))
            {
                message = "Собрано " + Collecting(() => session.Collect(room.Id));
                sounds?.Play("Coins");
            }
            if (room.Level < session.State.HearthLevel && !session.State.Queue.IsQueued(room.Id) &&
                TutorButton("roomUpgrade", "Улучшить до " + (room.Level + 1) + ": " + CostText(session.RoomUpgradeCost(room))))
                Report(session.UpgradeRoom(room.Id, now), "Build");
            var mate = session.MergeCandidate(room);
            if (mate != null && GUILayout.Button("Объединить с соседним залом"))
            {
                int keep = room.Cell < mate.Cell ? room.Id : mate.Id;
                var fail = session.MergeRooms(room.Id, mate.Id, now);
                Report(fail, "Build");
                if (fail == Fail.None)
                {
                    selectedRoom = keep;
                    message = "Залы объединены: больше мест и выработки";
                }
            }
            if (data.Stat != null)
            {
                var idle = session.State.Dwellers.Where(d => d.RoomId < 0 && d.RouteId == 0 && !d.IsHero)
                    .OrderByDescending(d => d.StatOf(data.Stat.Value)).FirstOrDefault();
                if (idle != null && TutorButton("roomAssign", "Поставить: " + idle.Name))
                    Report(session.Assign(idle.Id, room.Id));
            }
            if (GUILayout.Button(decorPicker ? "Скрыть украшения" : "Украсить"))
                decorPicker = !decorPicker;
            DrawMoveButton(room); // to another free cell for sparks (GameRunner.Comfort.cs)
            GUILayout.EndHorizontal();
            if (decorPicker)
                DrawDecorPicker(room, now);
            if (mate == null && room.Width < session.Balance.Merge.MaxWidth)
            {
                // a neighbour of the same type that is not ready yet: tell what merging needs
                var near = new[] { session.RoomAt(room.Floor, room.Cell - 1), session.RoomAt(room.Floor, room.Cell + room.Width) }
                    .FirstOrDefault(x => x != null && x.Type == room.Type);
                if (near != null)
                    GUILayout.Label(near.Level != room.Level ? "Рядом такой же зал: сравняйте уровни, и их можно будет объединить"
                        : room.Width + near.Width > session.Balance.Merge.MaxWidth ? "Зал больше " + session.Balance.Merge.MaxWidth + " клеток не бывает"
                        : "Объединить можно, когда закончится стройка");
            }
            if (room.Type == RoomType.Bedrooms)
                DrawLivingQuarters(room, now);
            DrawHallWork(room, now); // the tavern, infirmary, library, training hall, watchtower, chapel, archive (GameRunner.Rooms.cs)
            foreach (int id in room.Workers.ToList())
            {
                var d = session.FindDweller(id);
                if (d == null)
                    continue;
                GUILayout.BeginHorizontal();
                string stat = data.Stat == null ? "" : string.Format(" · {0} {1}", StatName(data.Stat.Value), d.StatOf(data.Stat.Value));
                GUILayout.Label(d.Name + (d.IsHero ? " (герой)" : "") + stat);
                if (GUILayout.Button("Снять с работы", GUILayout.Width(170)))
                    session.Unassign(id);
                GUILayout.EndHorizontal();
            }
        }

        /// <summary>GDD 8: couples in the living quarters and the choice of a new couple.</summary>
        void DrawLivingQuarters(Room room, long now)
        {
            var st = session.State;
            GUILayout.Label(string.Format("Жителей {0} из {1}. Мужчина и женщина в покоях ждут ребёнка {2}; на каждую клетку покоев одна пара. " +
                "Ребёнок похож на родителей характеристиками.", st.Dwellers.Count, Formulas.DwellerCap(session.Balance, st.HearthLevel),
                Duration(session.BirthSeconds(room))));
            var pairs = session.PairingsIn(room.Id).ToList();
            foreach (var p in pairs)
            {
                string man = session.FindDweller(p.ManId)?.Name ?? "?", woman = session.FindDweller(p.WomanId)?.Name ?? "?";
                GUILayout.Label(p.End > now ? string.Format("{0} и {1}: ребёнок через {2}", man, woman, Duration(p.End - now))
                    : string.Format("{0} и {1}: ребёнку нет места, поднимите очаг", man, woman));
            }
            if (pairs.Count >= room.Width)
                return;
            if (session.PopulationFull())
            {
                GUILayout.Label("В крепости нет места для ребёнка: лимит растёт с уровнем очага");
                return;
            }
            if (GUILayout.Button(pairPicker ? "Отмена" : "Пара"))
            {
                pairPicker = !pairPicker;
                pairMan = pairWoman = -1;
            }
            if (!pairPicker)
                return;
            PickParent("Он:", Sex.Male, ref pairMan);
            PickParent("Она:", Sex.Female, ref pairWoman);
            if (pairMan >= 0 && pairWoman >= 0 && GUILayout.Button("Начать: ребёнок через " + Duration(session.BirthSeconds(room))))
            {
                var fail = session.StartPairing(room.Id, pairMan, pairWoman, now);
                Report(fail, "Done");
                if (fail == Fail.None)
                {
                    pairPicker = false;
                    message = "Пара в жилых покоях: ждите ребёнка";
                }
            }
        }

        void PickParent(string label, Sex sex, ref int chosen)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(50));
            foreach (var d in session.State.Dwellers.Where(x => x.Sex == sex && x.RouteId == 0 && session.PairingOf(x.Id) == null)
                         .OrderBy(x => x.IsHero).ThenByDescending(x => x.Stats.Sum()).Take(6))
            {
                if (GUILayout.Toggle(chosen == d.Id, d.Name + " (" + d.Stats.Sum() + ")", "Button") && chosen != d.Id)
                    chosen = d.Id;
            }
            GUILayout.EndHorizontal();
        }

        /// <summary>Decorations for this room: owned ones go in at once, others are bought first.</summary>
        void DrawDecorPicker(Room room, long now)
        {
            string current = session.RoomDecorId(room.Id);
            GUILayout.Label(current == null ? "В зале нет украшения. Украшения только для красоты: силы за них не дают."
                : "Сейчас стоит: " + session.Decor(current).Name);
            GUILayout.BeginHorizontal();
            int shown = 0;
            foreach (var d in GameSession.DecorCatalog)
            {
                int free = session.DecorOwned(d.Id) - session.DecorPlaced(d.Id);
                string price = free > 0 ? "есть: " + free :
                    d.Currency == ResourceType.Sparks ? session.DecorPrice(d) + " искр" :
                    d.Currency == ResourceType.Glory ? session.DecorPrice(d) + " славы" : Short(session.DecorPrice(d)) + " серебра";
                GUILayout.BeginVertical(GUILayout.Width(120));
                var icon = GUILayoutUtility.GetRect(100, 70);
                ArtLibrary.Draw(new Rect(icon.x + 15, icon.y, 70, 70), "Decor/" + d.Id);
                if (GUILayout.Button((d.Premium ? "★ " : "") + d.Name + "\n" + price))
                {
                    if (free <= 0)
                    {
                        var fail = session.BuyDecor(d.Id, now);
                        Report(fail, "Coins");
                        if (fail != Fail.None)
                        {
                            GUILayout.EndVertical();
                            break;
                        }
                    }
                    Report(session.PlaceDecor(room.Id, d.Id), "Done");
                }
                GUILayout.EndVertical();
                if (++shown % 6 == 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
            }
            GUILayout.EndHorizontal();
            if (current != null && GUILayout.Button("Убрать украшение"))
                session.RemoveDecor(room.Id);
        }

        void DrawBuildPanel(long now)
        {
            GUILayout.Label(string.Format("Построить на этаже {0}:", selectedFloor + 1));
            GUILayout.BeginHorizontal();
            int shown = 0;
            foreach (var data in session.Balance.Rooms)
            {
                if (data.Type == RoomType.Hearth || session.State.HearthLevel < data.Unlock || session.CountCells(data.Type) >= data.MaxCount)
                    continue;
                GUILayout.BeginVertical(GUILayout.Width(150));
                var icon = GUILayoutUtility.GetRect(144, 90);
                ArtLibrary.Draw(new Rect(icon.x, icon.y, 144, 90), "Rooms/" + data.Type);
                if (TutorButton(data.Type == RoomType.Barracks ? "buildBarracks" : "buildAny", data.Name))
                {
                    var fail = session.BuildRoom(data.Type, selectedFloor, selectedCell, now, out var built);
                    Report(fail, "Build");
                    if (fail == Fail.None)
                    {
                        selectedRoom = built.Id;
                        selectedFloor = -1;
                        window = Win.Room;
                    }
                }
                GUILayout.EndVertical();
                if (++shown % 5 == 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
            }
            if (shown == 0)
                GUILayout.Label("Всё доступное уже построено: новые залы открываются с уровнем очага");
            GUILayout.EndHorizontal();
            GUILayout.Label("Зал рядом с таким же залом того же уровня можно объединить: больше мест и выработки.");
        }

        /// <summary>The small window of an obstacle: its price, its time and the button to clear it.</summary>
        void DrawClearPanel(long now)
        {
            int floor = selectedFloor, cell = selectedCell;
            var cost = session.ClearCost(floor, cell);
            long seconds = session.ClearSeconds(floor, cell);
            GUILayout.Label(string.Format("Завал на этаже {0}. Строитель разберёт его за {1}, и здесь можно будет строить. " +
                "Чем дальше от очага, тем дороже.", floor + 1, Duration(seconds)));
            GUILayout.Label("Цена: " + CostText(cost));
            if (TutorButton("clearObstacle", "Расчистить: " + CostText(cost) + ", " + Duration(seconds)))
            {
                var fail = session.ClearObstacle(floor, cell, now);
                Report(fail, "Build");
                if (fail == Fail.None)
                {
                    message = "Расчистка началась";
                    CloseWindow();
                }
            }
        }
    }
}

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
    /// Tap a room for its actions, tap a dug empty cell to build. Art: hearth/tools/art.
    /// </summary>
    public sealed partial class GameRunner
    {
        int selectedRoom = -1;
        int selectedFloor = -1, selectedCell = -1;
        float viewWidth = 900;
        bool listView;
        bool decorPicker;
        GUIStyle cellStyle;

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

        /// <summary>Floors worth showing: the dug ones and one more above and below.</summary>
        (int top, int bottom) ShownFloors()
        {
            var g = session.Balance.Grid;
            int top = g.HearthFloor, bottom = g.HearthFloor;
            foreach (var r in session.State.Rooms)
            {
                top = Math.Max(top, r.Floor);
                bottom = Math.Min(bottom, r.Floor);
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

        /// <summary>Dwellers in a cell: its workers; heroes off duty rest in the barracks (or by the hearth).</summary>
        IEnumerable<Dweller> PeopleIn(Room room, bool hearth)
        {
            var st = session.State;
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
            float cellW = Math.Max(60, (viewWidth - 8) / g.Cells);
            float cellH = cellW * 0.5f; // the room pictures are 2:1
            float mountainH = live == null ? cellW * g.Cells * 0.5f : 0;
            var area = GUILayoutUtility.GetRect(cellW * g.Cells, mountainH * 0.8f + cellH * (top - bottom + 1));
            if (mountainH > 0)
                ArtLibrary.Draw(new Rect(area.x, area.y, cellW * g.Cells, mountainH), "Mountain");
            float gridY = area.y + mountainH * 0.8f;
            for (int f = top; f >= bottom; f--)
            {
                float y = gridY + (top - f) * cellH;
                for (int c = 0; c < g.Cells; c++)
                {
                    var r = new Rect(area.x + c * cellW, y, cellW, cellH);
                    if (f == g.HearthFloor && Array.IndexOf(g.HearthCells, c) >= 0)
                    {
                        if (c == g.HearthCells[0])
                            DrawHearthCell(new Rect(r.x, r.y, cellW * g.HearthCells.Length, cellH), now);
                        continue;
                    }
                    var room = st.Rooms.Find(x => x.Floor == f && x.Cell == c);
                    if (room != null)
                        DrawRoomCell(r, room, now);
                    else
                        DrawEmptyCell(r, f, c);
                }
                if (live != null)
                    DrawCreatures(area, gridY, top, f, cellW, cellH, live);
            }
        }

        void DrawHearthCell(Rect r, long now)
        {
            var old = GUI.color;
            if (session.State.OutOfCoal)
                GUI.color = new Color(0.55f, 0.6f, 0.75f, 1);
            ArtLibrary.Draw(r, "Rooms/Hearth");
            GUI.color = old;
            // the living fire: its frames over the fireplace, dimmer when coal runs out
            var fire = ArtLibrary.Data.Fire;
            var tex = ArtLibrary.Tex("HearthFire");
            if (fire != null && tex != null && fire.Frames > 0)
            {
                int frame = (int)(Time.time * 10) % fire.Frames;
                var fr = new Rect(r.x + fire.X * r.width, r.y + fire.Y * r.height, fire.W * r.width, fire.H * r.height);
                if (session.State.OutOfCoal)
                    fr = new Rect(fr.x + fr.width * 0.2f, fr.y + fr.height * 0.5f, fr.width * 0.6f, fr.height * 0.5f);
                GUI.DrawTextureWithTexCoords(fr, tex, new Rect((float)frame / fire.Frames, 0, 1f / fire.Frames, 1));
            }
            DrawPeople(r, PeopleIn(null, true));
            var job = session.State.Queue.AllJobs().FirstOrDefault(j => j.IsHearth);
            string build = job == null ? "" : job.IsRunning(now) ? "\nстройка " + Duration(job.Remaining(now)) : "\nждёт строителя";
            CellText(r, string.Format("<b>Очаг ур. {0}</b> · {1}{2}", session.State.HearthLevel, ModeName(session.State.Mode), build));
            if (GUI.Button(r, "", GUIStyle.none))
            {
                selectedRoom = -1;
                selectedFloor = -1;
                message = "Очаг: улучшение — кнопка над разрезом; режим — кнопки вверху";
            }
        }

        void DrawRoomCell(Rect r, Room room, long now)
        {
            var data = session.Balance.Room(room.Type);
            ArtLibrary.Draw(r, "Rooms/" + room.Type);
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
            // two short lines: a cell on a phone is narrow
            string status = "";
            if (data.Produces != null && room.Stock >= 1)
                status += "<color=#fd5>+" + Short(room.Stock) + "</color> ";
            if (!warm)
                status += "<color=#9cf>промёрз</color> ";
            var job = session.State.Queue.AllJobs().FirstOrDefault(j => j.Target == room.Id);
            if (job != null)
                status += job.IsRunning(now) ? "стройка " + Duration(job.Remaining(now)) : "в очереди";
            CellText(r, string.Format("<b>{0}</b> {1}\n{2}", data.Name, room.Level, status.Trim()));
            if (selectedRoom == room.Id)
                Outline(r);
            if (GUI.Button(r, "", GUIStyle.none))
            {
                // a tap on a room with output ready collects it at once, like in the genre
                if (data.Produces != null && room.Stock >= 1 && selectedRoom == room.Id)
                {
                    message = string.Format("Собрано {0}", session.Collect(room.Id));
                    sounds?.Play("Coins");
                }
                else
                {
                    sounds?.Play("Click", 0.4f);
                }
                selectedRoom = room.Id;
                selectedFloor = -1;
                decorPicker = false;
            }
        }

        void DrawEmptyCell(Rect r, int floor, int cell)
        {
            var b = session.Balance;
            bool warm = Formulas.IsFloorHabitable(b, session.State.HearthLevel, session.State.Mode, floor, 1, session.WarmthMult());
            if (!warm)
            {
                ArtLibrary.Draw(r, "Rooms/Rock");
                CellText(r, "<color=#9cf>скала\nздесь холодно</color>");
                return;
            }
            // only the cells next to built rooms are dug out; further on stands the rock
            var g = b.Grid;
            bool hearthNear = floor == g.HearthFloor && (Array.IndexOf(g.HearthCells, cell - 1) >= 0 || Array.IndexOf(g.HearthCells, cell + 1) >= 0);
            bool near = hearthNear || session.State.Rooms.Any(x => x.Floor == floor && Math.Abs(x.Cell - cell) == 1)
                        || session.State.Rooms.Any(x => x.Cell == cell && Math.Abs(x.Floor - floor) == 1);
            ArtLibrary.Draw(r, near ? "Rooms/Empty" : "Rooms/Rock");
            CellText(r, near ? "+ построить" : "<color=#aaa>+</color>");
            if (selectedFloor == floor && selectedCell == cell)
                Outline(r);
            if (GUI.Button(r, "", GUIStyle.none))
            {
                sounds?.Play("Click", 0.4f);
                selectedFloor = floor;
                selectedCell = cell;
                selectedRoom = -1;
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

        void DrawSelection(long now)
        {
            var room = selectedRoom >= 0 ? session.FindRoom(selectedRoom) : null;
            if (room != null)
            {
                DrawRoomPanel(room, now);
                return;
            }
            if (selectedFloor >= 0 && session.CellFree(selectedFloor, selectedCell))
                DrawBuildPanel(now);
            else
                GUILayout.Label("Нажмите на зал, чтобы увидеть его дела, или на пустую ячейку, чтобы построить.");
        }

        void DrawRoomPanel(Room room, long now)
        {
            var b = session.Balance;
            var data = b.Room(room.Type);
            string output = data.Produces == null ? "" :
                string.Format(" · выработка {0:0.0}/ч · запас {1:0}", session.RoomOutputPerHour(room), room.Stock);
            GUILayout.Label(string.Format("<b>{0}</b>, уровень {1}, этаж {2}{3}{4}", data.Name, room.Level, room.Floor + 1, output,
                session.IsRoomWarm(room) ? "" : " · <color=#9cf>промёрз: поднимите очаг, включите «Пылающий» или изучите Тепло</color>"), Rich());
            GUILayout.BeginHorizontal();
            if (data.Produces != null && GUILayout.Button("Собрать"))
            {
                message = "Собрано " + session.Collect(room.Id);
                sounds?.Play("Coins");
            }
            if (room.Level < session.State.HearthLevel && !session.State.Queue.IsQueued(room.Id) &&
                GUILayout.Button("Улучшить до " + (room.Level + 1) + ": " + CostText(session.RoomJobCost(room.Level + 1))))
                Report(session.UpgradeRoom(room.Id, now), "Build");
            if (data.Stat != null)
            {
                var idle = session.State.Dwellers.Where(d => d.RoomId < 0 && d.RouteId == 0 && !d.IsHero)
                    .OrderByDescending(d => d.StatOf(data.Stat.Value)).FirstOrDefault();
                if (idle != null && GUILayout.Button("Поставить: " + idle.Name))
                    Report(session.Assign(idle.Id, room.Id));
            }
            if (GUILayout.Button(decorPicker ? "Скрыть украшения" : "Украсить"))
                decorPicker = !decorPicker;
            if (GUILayout.Button("Закрыть", GUILayout.Width(110)))
                selectedRoom = -1;
            GUILayout.EndHorizontal();
            if (decorPicker)
                DrawDecorPicker(room, now);
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
                if (data.Type == RoomType.Hearth || session.State.HearthLevel < data.Unlock || session.CountRooms(data.Type) >= data.MaxCount)
                    continue;
                GUILayout.BeginVertical(GUILayout.Width(150));
                var icon = GUILayoutUtility.GetRect(144, 90);
                ArtLibrary.Draw(new Rect(icon.x, icon.y, 144, 90), "Rooms/" + data.Type);
                if (GUILayout.Button(data.Name))
                {
                    var fail = session.BuildRoom(data.Type, selectedFloor, selectedCell, now, out var built);
                    Report(fail, "Build");
                    if (fail == Fail.None)
                    {
                        selectedRoom = built.Id;
                        selectedFloor = -1;
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
        }

        void DrawFortressTab(long now)
        {
            DrawHearth(now);
            DrawCutaway(now);
            DrawSelection(now);
            DrawQueue(now);
            listView = GUILayout.Toggle(listView, listView ? "Скрыть залы списком" : "Залы списком", "Button");
            if (listView)
            {
                DrawRooms(now);
                DrawBuild(now);
            }
            DrawDwellers();
        }
    }
}

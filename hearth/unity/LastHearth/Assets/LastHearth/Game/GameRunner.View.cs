using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core;
using LastHearth.Core.Surge;
using UnityEngine;

namespace LastHearth.Game
{
    /// <summary>
    /// The cutaway of the fortress (GDD: 2.5D like Hustle Castle): floors cut into the rock, rooms with
    /// their art, the burning hearth, frozen rooms under frost, dwellers at work. Tap a room for its
    /// actions, tap a dug empty cell to build. Art comes from Resources/Art (hearth/tools/room_art.py).
    /// </summary>
    public sealed partial class GameRunner
    {
        readonly Dictionary<string, Texture2D> art = new Dictionary<string, Texture2D>();
        int selectedRoom = -1;
        int selectedFloor = -1, selectedCell = -1;
        float viewWidth = 900;
        bool listView;
        GUIStyle cellStyle;

        Texture2D Art(string name)
        {
            if (!art.TryGetValue(name, out var texture))
                art[name] = texture = UnityEngine.Resources.Load<Texture2D>("Art/" + name);
            return texture;
        }

        void Draw(Rect r, string name)
        {
            var t = Art(name);
            if (t != null)
                GUI.DrawTexture(r, t);
        }

        void CellText(Rect r, string text)
        {
            if (cellStyle == null)
                cellStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true, wordWrap = false };
            var shadow = new Rect(r.x + 5, r.y + 4, r.width - 8, r.height);
            var old = GUI.color;
            GUI.color = Color.black;
            GUI.Label(shadow, StripColor(text), cellStyle);
            GUI.color = old;
            GUI.Label(new Rect(r.x + 4, r.y + 3, r.width - 8, r.height), text, cellStyle);
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

        /// <summary>Draws the cutaway; the creatures of a Surge, if any, climb on top of it.</summary>
        void DrawCutaway(long now, SurgeDefense live = null)
        {
            var b = session.Balance;
            var g = b.Grid;
            var st = session.State;
            var (top, bottom) = ShownFloors();
            float cellW = Math.Max(60, (viewWidth - 8) / g.Cells);
            float cellH = cellW * 0.625f;
            var area = GUILayoutUtility.GetRect(cellW * g.Cells, cellH * (top - bottom + 1));
            for (int f = top; f >= bottom; f--)
            {
                float y = area.y + (top - f) * cellH;
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
                    DrawCreatures(area, top, f, cellW, cellH, live);
            }
        }

        void DrawHearthCell(Rect r, long now)
        {
            // the fire breathes: brightness follows a slow flicker
            float flicker = 0.9f + 0.1f * Mathf.Sin(Time.time * 3.1f) * Mathf.Sin(Time.time * 1.7f);
            var old = GUI.color;
            GUI.color = session.State.OutOfCoal ? new Color(0.55f, 0.6f, 0.75f, 1) : new Color(flicker, flicker, flicker, 1);
            Draw(r, "RoomHearth");
            GUI.color = old;
            var job = session.State.Queue.AllJobs().FirstOrDefault(j => j.IsHearth);
            string build = job == null ? "" : job.IsRunning(now) ? "\nстройка " + Duration(job.Remaining(now)) : "\nждёт строителя";
            CellText(r, string.Format("<b>Очаг ур. {0}</b> · {1}{2}", session.State.HearthLevel, ModeName(session.State.Mode), build));
            if (GUI.Button(r, "", GUIStyle.none))
            {
                tab = 0;
                selectedRoom = -1;
                selectedFloor = -1;
                message = "Очаг: улучшение — кнопка под разрезом; режим — кнопки вверху";
            }
        }

        void DrawRoomCell(Rect r, Room room, long now)
        {
            var data = session.Balance.Room(room.Type);
            Draw(r, "Room" + room.Type);
            bool warm = session.IsRoomWarm(room);
            if (!warm)
                Draw(r, "Frost");
            // dwellers at work stand on the floor of the room
            float w = r.width * 0.09f, h = w * 44f / 24f;
            int i = 0;
            foreach (int id in room.Workers)
            {
                var d = session.FindDweller(id);
                if (d == null)
                    continue;
                var spot = new Rect(r.x + r.width * 0.12f + i * w * 1.6f, r.yMax - h - r.height * 0.06f, w, h);
                Draw(spot, d.IsHero ? "Hero" : "Dweller");
                i++;
            }
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
                    message = string.Format("Собрано {0}", session.Collect(room.Id));
                selectedRoom = room.Id;
                selectedFloor = -1;
            }
        }

        void DrawEmptyCell(Rect r, int floor, int cell)
        {
            var b = session.Balance;
            bool warm = Formulas.IsFloorHabitable(b, session.State.HearthLevel, session.State.Mode, floor, 1, session.WarmthMult());
            if (!warm)
            {
                var old = GUI.color;
                GUI.color = new Color(0.7f, 0.75f, 0.9f, 1);
                Draw(r, "Rock");
                GUI.color = old;
                CellText(r, "<color=#9cf>скала\nздесь холодно</color>");
                return;
            }
            Draw(r, "Empty");
            CellText(r, "+ построить");
            if (selectedFloor == floor && selectedCell == cell)
                Outline(r);
            if (GUI.Button(r, "", GUIStyle.none))
            {
                selectedFloor = floor;
                selectedCell = cell;
                selectedRoom = -1;
            }
        }

        void DrawCreatures(Rect area, int top, int floor, float cellW, float cellH, SurgeDefense live)
        {
            float y = area.y + (top - floor) * cellH;
            var here = live.Creatures.Where(x => x.Floor == floor).ToList();
            float size = cellH * 0.45f;
            for (int i = 0; i < here.Count; i++)
            {
                var cr = here[i];
                // creatures climb: the further on their floor, the closer to the hearth side
                float progress = cr.Progress / (float)Math.Max(1, live.Numbers.TicksPerFloor);
                float x = area.x + (0.2f + 0.6f * progress) * cellW * session.Balance.Grid.Cells - size / 2 + i * size * 0.3f;
                var r = new Rect(x, y + cellH - size - cellH * 0.08f, size, size);
                var old = GUI.color;
                GUI.color = new Color(1, 1, 1, 0.35f + 0.65f * (float)(cr.Hp / cr.MaxHp));
                Draw(r, "Creature");
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
                message = "Собрано " + session.Collect(room.Id);
            if (room.Level < session.State.HearthLevel && !session.State.Queue.IsQueued(room.Id) &&
                GUILayout.Button("Улучшить до " + (room.Level + 1) + ": " + CostText(session.RoomJobCost(room.Level + 1))))
                Report(session.UpgradeRoom(room.Id, now));
            if (data.Stat != null)
            {
                var idle = session.State.Dwellers.Where(d => d.RoomId < 0 && d.RouteId == 0 && !d.IsHero)
                    .OrderByDescending(d => d.StatOf(data.Stat.Value)).FirstOrDefault();
                if (idle != null && GUILayout.Button("Поставить: " + idle.Name))
                    Report(session.Assign(idle.Id, room.Id));
            }
            if (GUILayout.Button("Закрыть", GUILayout.Width(110)))
                selectedRoom = -1;
            GUILayout.EndHorizontal();
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

        void DrawBuildPanel(long now)
        {
            GUILayout.Label(string.Format("Построить на этаже {0}:", selectedFloor + 1));
            GUILayout.BeginHorizontal();
            int shown = 0;
            foreach (var data in session.Balance.Rooms)
            {
                if (data.Type == RoomType.Hearth || session.State.HearthLevel < data.Unlock || session.CountRooms(data.Type) >= data.MaxCount)
                    continue;
                if (GUILayout.Button(data.Name))
                {
                    var fail = session.BuildRoom(data.Type, selectedFloor, selectedCell, now, out var built);
                    Report(fail);
                    if (fail == Fail.None)
                    {
                        selectedRoom = built.Id;
                        selectedFloor = -1;
                    }
                }
                if (++shown % 4 == 0)
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

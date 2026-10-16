using LastHearth.Core;
using UnityEngine;

namespace LastHearth.Game
{
    /// <summary>
    /// Conveniences (GDD 14, MODES.md 4.4): «Экономия батареи» runs the game at 30 frames a second
    /// instead of 60 (remembered on the phone); the fortress gets a name (settings, shown on the top
    /// bar; the first name is free, then 50 sparks); a room moves to another dug-out cell for 50 sparks
    /// («Перенести» in its window, then a tap on a cell marked green).
    /// </summary>
    public sealed partial class GameRunner
    {
        const string BatteryKey = "lasthearth.batterySaver";

        bool? batterySaver;
        string nameDraft;
        int movingRoom = -1;
        Rect moveCancel;
        GUIStyle nameStyle;

        bool BatterySaver => batterySaver ?? (batterySaver = PlayerPrefs.GetInt(BatteryKey, 0) == 1).Value;

        void ApplyFrameRate() => Application.targetFrameRate = BatterySaver ? 30 : 60;

        void SetBatterySaver(bool on)
        {
            batterySaver = on;
            PlayerPrefs.SetInt(BatteryKey, on ? 1 : 0);
            PlayerPrefs.Save();
            ApplyFrameRate();
        }

        void DrawComfortSettings(long now)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(BatterySaver ? "Экономия батареи: вкл (30 кадров)" : "Экономия батареи: выкл (60 кадров)"))
            {
                SetBatterySaver(!BatterySaver);
                message = BatterySaver ? "Экономия батареи: 30 кадров в секунду" : "Плавно: 60 кадров в секунду";
            }
            GUILayout.EndHorizontal();

            // the fortress's name
            GUILayout.Space(6);
            GUILayout.Label(string.Format("<b>Крепость «{0}»</b> · имя от {1} до {2} букв", session.FortressName,
                GameSession.FortressNameMin, GameSession.FortressNameMax), Rich());
            if (nameDraft == null)
                nameDraft = session.FortressName;
            GUILayout.BeginHorizontal();
            nameDraft = GUILayout.TextField(nameDraft, GameSession.FortressNameMax, GUILayout.Width(Mathf.Min(320, viewWidth * 0.5f)));
            long price = session.RenamePrice;
            if (GUILayout.Button(price == 0 ? "Назвать: бесплатно" : "Назвать: " + price + " искр"))
            {
                var fail = session.RenameFortress(nameDraft, now);
                Report(fail, "Done");
                if (fail == Fail.RulesNotMet)
                    message = string.Format("Имя крепости — от {0} до {1} букв", GameSession.FortressNameMin, GameSession.FortressNameMax);
                else if (fail == Fail.AlreadyQueued)
                    message = "Крепость уже так зовётся";
                else if (fail == Fail.None)
                    message = "Теперь крепость зовётся «" + session.FortressName + "»";
                nameDraft = session.FortressName;
            }
            GUILayout.EndHorizontal();
        }

        /// <summary>The name on the top bar, between the resources and the sparks, when there is room for it; a tap opens the settings.</summary>
        void DrawFortressName(Rect r)
        {
            if (r.width < 90)
                return;
            if (nameStyle == null)
                nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            UiSkin.Outlined(r, session.FortressName, nameStyle);
            if (window == Win.None && GUI.Button(r, "", GUIStyle.none))
            {
                sounds?.Play("Click", 0.4f);
                Open(Win.Settings);
            }
        }

        // ------------------------------------------------------------------ moving a room

        /// <summary>«Перенести» in the room's window: the window closes, the free cells it fits glow.</summary>
        void DrawMoveButton(Room room)
        {
            if (!GUILayout.Button("Перенести: " + session.MovePrice + " искр"))
                return;
            if (session.State.Wallet[ResourceType.Sparks] < session.MovePrice)
            {
                Report(Fail.NotEnough);
                return;
            }
            movingRoom = room.Id;
            CloseWindow();
            message = "Выберите свободную клетку для зала «" + session.Balance.Room(room.Type).Name + "»";
        }

        /// <summary>Where the moving room would stand if this free cell were tapped (its left cell), or -1.</summary>
        int MoveCellFor(int floor, int cell)
        {
            var room = session.FindRoom(movingRoom);
            if (room == null)
                return -1;
            // the tapped cell is any of the cells a merged room spans
            for (int left = cell; left > cell - room.Width && left >= 0; left--)
            {
                if (session.CanMoveRoom(room.Id, floor, left) == Fail.None)
                    return left;
            }
            return -1;
        }

        /// <summary>While a room is being moved: a green frame on every free cell it can go to.</summary>
        void MoveMark(Rect r, int floor, int cell)
        {
            if (movingRoom < 0 || MoveCellFor(floor, cell) < 0)
                return;
            var old = GUI.color;
            GUI.color = new Color(0.45f, 1f, 0.5f, 0.6f + 0.3f * Mathf.Sin(Time.time * 5));
            Frame(Inflate(r, -3), 4);
            GUI.color = old;
        }

        /// <summary>A tap on a free cell while a room is being moved: true when the tap was the move's.</summary>
        bool MoveTap(int floor, int cell)
        {
            if (movingRoom < 0)
                return false;
            var room = session.FindRoom(movingRoom);
            int left = MoveCellFor(floor, cell);
            if (room == null || left < 0)
            {
                message = room == null ? "" : "Сюда зал не встанет: нужна свободная тёплая клетка" + (room.Width > 1 ? " на " + room.Width + " места" : "");
                return true;
            }
            var fail = session.MoveRoom(room.Id, floor, left, Now());
            Report(fail, "Build");
            if (fail == Fail.None)
            {
                message = "Зал «" + session.Balance.Room(room.Type).Name + "» перенесён";
                movingRoom = -1;
            }
            return true;
        }

        /// <summary>Early in OnGUI: the «Отмена» plate of a move takes its own taps.</summary>
        void MoveInput()
        {
            var e = Event.current;
            if (movingRoom < 0 || (e.type != EventType.MouseDown && e.type != EventType.MouseUp) || !moveCancel.Contains(e.mousePosition))
                return;
            if (e.type == EventType.MouseUp)
                CancelMove();
            e.Use();
        }

        void CancelMove()
        {
            movingRoom = -1;
            message = "Перенос отменён";
        }

        /// <summary>A plate under the top bar while a room is being moved, with «Отмена».</summary>
        void DrawMoveBar(Rect area)
        {
            moveCancel = new Rect(0, 0, 0, 0);
            if (movingRoom < 0)
                return;
            var room = session.FindRoom(movingRoom);
            if (room == null || window != Win.None || onMap)
            {
                movingRoom = -1; // another window or the map: the move is over
                return;
            }
            HudStyles();
            var plate = new Rect(area.center.x - 250, area.y + HudTop + 6, 500, 34);
            ArtLibrary.DrawSliced(plate, "UI/Pill", 30);
            UiSkin.Outlined(new Rect(plate.x + 14, plate.y, plate.width - 130, plate.height),
                "Перенос: " + session.Balance.Room(room.Type).Name + " · " + session.MovePrice + " искр", buttonLabel);
            moveCancel = new Rect(plate.xMax - 112, plate.y + 3, 104, plate.height - 6);
            ArtLibrary.DrawSliced(moveCancel, "UI/ButtonOn", 16);
            UiSkin.Outlined(moveCancel, "Отмена", buttonLabel);
            if (GUI.Button(moveCancel, "Отмена", GUIStyle.none))
                CancelMove();
        }
    }
}

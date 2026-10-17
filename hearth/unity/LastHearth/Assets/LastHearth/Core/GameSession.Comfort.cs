using System;
using System.Text;

namespace LastHearth.Core
{
    /// <summary>
    /// MODES.md 4.4, «Смена имени, перенос зала — 50 искр, удобство»: the player names the fortress
    /// (the first name is free) and moves a room to another dug-out cell. A moved room keeps its id, so
    /// its level, workers, stock, decoration, couples and the work of its hall (Halls.cs) go with it.
    /// </summary>
    public sealed partial class GameSession
    {
        public const string DefaultFortressName = "Последний очаг";
        public const int FortressNameMin = 2, FortressNameMax = 20;

        /// <summary>The fortress's name as shown: the player's, or «Последний очаг» until it is given.</summary>
        public string FortressName => string.IsNullOrEmpty(State.FortressName) ? DefaultFortressName : State.FortressName;

        /// <summary>Sparks the next rename costs: the first one is free.</summary>
        public long RenamePrice => State.Renames == 0 ? 0 : Balance.Activities.Shop.Convenience;

        /// <summary>Sparks a room's move costs.</summary>
        public long MovePrice => Balance.Activities.Shop.Convenience;

        /// <summary>A name as the game keeps it: trimmed, single spaces, no control characters; null when it is too short or long.</summary>
        public static string CleanName(string raw)
        {
            if (raw == null)
                return null;
            var sb = new StringBuilder();
            bool space = false;
            foreach (char c in raw.Trim())
            {
                if (char.IsWhiteSpace(c))
                {
                    space = true;
                    continue;
                }
                if (char.IsControl(c) || c == '<' || c == '>')
                    continue; // no rich-text tags in a name shown on the screen
                if (space && sb.Length > 0)
                    sb.Append(' ');
                space = false;
                sb.Append(c);
            }
            string name = sb.ToString();
            return name.Length < FortressNameMin || name.Length > FortressNameMax ? null : name;
        }

        public Fail RenameFortress(string raw, long now)
        {
            Advance(now);
            string name = CleanName(raw);
            if (name == null)
                return Fail.RulesNotMet;
            if (name == FortressName)
                return Fail.AlreadyQueued;
            long price = RenamePrice;
            if (State.Wallet[ResourceType.Sparks] < price)
                return Fail.NotEnough;
            State.Wallet[ResourceType.Sparks] -= price;
            State.FortressName = name;
            State.Renames++;
            return Fail.None;
        }

        /// <summary>
        /// Whether the room can stand with its left cell at (floor, cell): every cell it spans is dug out
        /// and empty (or its own), not the Hearth's, and the floor is warm enough for its level.
        /// </summary>
        public Fail CanMoveRoom(int roomId, int floor, int cell)
        {
            var room = FindRoom(roomId);
            if (room == null)
                return Fail.NotFound;
            if (room.Type == RoomType.Hearth)
                return Fail.Locked;
            if (room.Floor == floor && room.Cell == cell)
                return Fail.CellTaken;
            if (State.SurgeLive)
                return Fail.NotToday; // the defenders stand where their rooms are
            for (int c = cell; c < cell + Math.Max(1, room.Width); c++)
            {
                if (room.Covers(floor, c))
                    continue;
                if (CellStateAt(floor, c) != CellState.Free)
                    return Fail.CellTaken;
            }
            if (!Formulas.IsFloorHabitable(Balance, State.HearthLevel, State.Mode, floor, Math.Max(1, room.Level), WarmthMult()))
                return Fail.TooCold;
            if (State.Wallet[ResourceType.Sparks] < MovePrice)
                return Fail.NotEnough;
            return Fail.None;
        }

        public Fail MoveRoom(int roomId, int floor, int cell, long now)
        {
            Advance(now);
            var fail = CanMoveRoom(roomId, floor, cell);
            if (fail != Fail.None)
                return fail;
            var room = FindRoom(roomId);
            State.Wallet[ResourceType.Sparks] -= MovePrice;
            int oldFloor = room.Floor, oldCell = room.Cell;
            room.Floor = floor;
            room.Cell = cell;
            // the cells it leaves stay dug out
            for (int c = oldCell; c < oldCell + Math.Max(1, room.Width); c++)
            {
                if (!room.Covers(oldFloor, c))
                    MarkCleared(oldFloor, c);
            }
            return Fail.None;
        }
    }
}

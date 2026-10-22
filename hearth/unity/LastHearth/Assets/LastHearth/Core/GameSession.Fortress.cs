using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core.Combat;

namespace LastHearth.Core
{
    /// <summary>What stands in a cell of the grid.</summary>
    public enum CellState
    {
        /// <summary>Outside the grid.</summary>
        Outside,
        Hearth,
        Room,
        /// <summary>Its section of the mountain opens at a higher hearth level.</summary>
        Locked,
        /// <summary>Rubble that a builder must clear before anything is built here.</summary>
        Obstacle,
        /// <summary>A builder is clearing it (or will, when its turn in the queue comes).</summary>
        Clearing,
        /// <summary>Dug out and empty: a room can be built here.</summary>
        Free,
    }

    /// <summary>
    /// GDD 6, like Hustle Castle: the mountain opens in sections with the hearth level, obstacles are
    /// cleared by the builders, rooms of one type merge into big halls, and the living quarters raise
    /// children (GDD 8).
    /// </summary>
    public sealed partial class GameSession
    {
        static readonly string[] BoyNames =
        {
            "Ярослав", "Мирон", "Святослав", "Егор", "Лука", "Тимофей", "Всеволод", "Добрыня", "Ратмир", "Елисей",
            "Назар", "Фрол", "Богдан", "Матвей",
        };

        static readonly string[] GirlNames =
        {
            "Милава", "Дарина", "Любава", "Есения", "Василиса", "Забава", "Огняна", "Снежана", "Ведана", "Купава",
            "Отрада", "Голуба", "Ульяна", "Белава",
        };

        /// <summary>A child was born in the living quarters.</summary>
        public event Action<Dweller> ChildBorn;

        /// <summary>The key of a cell in GameState.Cleared.</summary>
        public static int CellKey(int floor, int cell) => floor * 100 + cell;

        /// <summary>Sex by the name (the names of the game are known; refugees alternate), else by the id.</summary>
        public static Sex SexOfName(string name, int id)
        {
            if (Array.IndexOf(BoyNames, name) >= 0 || name == "Тим" || name == "Странник")
                return Sex.Male;
            if (Array.IndexOf(GirlNames, name) >= 0 || name == "Мара" || name == "Ина")
                return Sex.Female;
            int refugee = Array.IndexOf(RefugeeNames, name);
            if (refugee >= 0)
                return refugee % 2 == 0 ? Sex.Male : Sex.Female;
            return id % 2 == 1 ? Sex.Male : Sex.Female;
        }

        /// <summary>Brings a save of an older version up to date: nothing of the player's is lost.</summary>
        void UpgradeSave()
        {
            if (State.SaveVersion < 2)
            {
                // the cells next to the rooms and the hearth were dug out before obstacles existed
                var g = Balance.Grid;
                foreach (var room in State.Rooms)
                {
                    for (int c = room.Cell; c < room.Cell + Math.Max(1, room.Width); c++)
                    {
                        MarkCleared(room.Floor, c - 1);
                        MarkCleared(room.Floor, c + 1);
                        MarkCleared(room.Floor - 1, c);
                        MarkCleared(room.Floor + 1, c);
                    }
                }
                foreach (int c in g.HearthCells)
                {
                    MarkCleared(g.HearthFloor, c - 1);
                    MarkCleared(g.HearthFloor, c + 1);
                }
                State.SaveVersion = 2;
            }
            if (State.SaveVersion < 3)
            {
                // Before items, gear was a number that followed the hearth. Every slot without an item holds the
                // forge's issued piece of the hearth level, so the heroes of an old save keep exactly that power.
                if (State.Gear == null)
                    State.Gear = new GearState();
                State.SaveVersion = 3;
            }
            if (State.Gear == null)
                State.Gear = new GearState();
            foreach (var room in State.Rooms)
                room.Width = Math.Max(1, room.Width);
            foreach (var d in State.Dwellers)
            {
                if (d.Sex == Sex.Unknown)
                    d.Sex = SexOfName(d.Name, d.Id);
            }
            UpgradePeople(); // levels and traits of the dwellers (GameSession.People.cs)
        }

        void MarkCleared(int floor, int cell)
        {
            var state = CellStateAt(floor, cell);
            if (state == CellState.Obstacle || state == CellState.Locked)
                State.Cleared.Add(CellKey(floor, cell));
        }

        // ------------------------------------------------------------------ sections and obstacles

        public bool IsHearthCell(int floor, int cell)
        {
            var g = Balance.Grid;
            return floor == g.HearthFloor && Array.IndexOf(g.HearthCells, cell) >= 0;
        }

        public Room RoomAt(int floor, int cell) => State.Rooms.Find(r => r.Covers(floor, cell));

        /// <summary>The hearth level that opens this cell's section; 0 when no section holds it.</summary>
        public int SectionLevel(int floor, int cell)
        {
            int level = 0;
            foreach (var s in Balance.Grid.Sections)
            {
                if (s.Contains(floor, cell) && (level == 0 || s.Hearth < level))
                    level = s.Hearth;
            }
            return level;
        }

        /// <summary>True when the hearth job to this level opens a new part of the mountain.</summary>
        public bool SectionOpensAt(int hearthLevel) => Balance.Grid.Sections.Any(s => s.Hearth == hearthLevel);

        public CellState CellStateAt(int floor, int cell)
        {
            var g = Balance.Grid;
            if (floor < 0 || floor >= g.Floors || cell < 0 || cell >= g.Cells)
                return CellState.Outside;
            if (IsHearthCell(floor, cell))
                return CellState.Hearth;
            if (RoomAt(floor, cell) != null)
                return CellState.Room;
            if (State.Cleared.Contains(CellKey(floor, cell)))
                return CellState.Free;
            if (State.Queue.Clearing(floor, cell) != null)
                return CellState.Clearing;
            int section = SectionLevel(floor, cell);
            return section == 0 || section > State.HearthLevel ? CellState.Locked : CellState.Obstacle;
        }

        /// <summary>Steps from the hearth: floors, plus cells beyond the hearth's own.</summary>
        public int HearthDistance(int floor, int cell)
        {
            var g = Balance.Grid;
            int dx = Math.Max(0, Math.Max(g.HearthCells.Min() - cell, cell - g.HearthCells.Max()));
            return Math.Abs(floor - g.HearthFloor) + dx;
        }

        double ClearFactor(int floor, int cell) => 1 + Balance.Obstacles.PerDistance * Math.Max(0, HearthDistance(floor, cell) - 1);

        static long ByLevel(long[] table, int level) => table[Math.Max(1, Math.Min(level, table.Length)) - 1];

        /// <summary>Coal and food to clear the obstacle: more at a higher hearth and further from it.</summary>
        public ResourceBag ClearCost(int floor, int cell)
        {
            var o = Balance.Obstacles;
            double k = ClearFactor(floor, cell);
            var bag = new ResourceBag();
            bag[ResourceType.Coal] = (long)Math.Ceiling(ByLevel(o.Coal, State.HearthLevel) * k);
            bag[ResourceType.Food] = (long)Math.Ceiling(ByLevel(o.Food, State.HearthLevel) * k);
            return bag;
        }

        public long ClearSeconds(int floor, int cell) =>
            (long)Math.Round(ByLevel(Balance.Obstacles.Seconds, State.HearthLevel) * ClearFactor(floor, cell));

        /// <summary>A builder clears the obstacle like a construction job; then the cell is free to build on.</summary>
        public Fail ClearObstacle(int floor, int cell, long now)
        {
            Advance(now);
            switch (CellStateAt(floor, cell))
            {
                case CellState.Obstacle:
                    break;
                case CellState.Locked:
                    return Fail.Locked;
                case CellState.Clearing:
                    return Fail.AlreadyQueued;
                default:
                    return Fail.CellTaken;
            }
            int builder = State.Queue.PickBuilder(Balance.Build.QueuePerBuilder, now);
            if (builder < 0)
                return Fail.QueueFull;
            var cost = ClearCost(floor, cell);
            if (!State.Wallet.Covers(cost))
                return Fail.NotEnough;
            State.Wallet.Subtract(cost);
            var job = State.Queue.Enqueue(builder, BuildJob.ClearTarget, 0, ClearSeconds(floor, cell), now);
            job.Floor = floor;
            job.Cell = cell;
            Tutor(TutorialEvent.ObstacleClearing);
            Advance(now);
            return Fail.None;
        }

        // ------------------------------------------------------------------ merged rooms

        /// <summary>Cells of this type already built: the room limit counts cells, so merging gives no extra rooms.</summary>
        public int CountCells(RoomType type) => State.Rooms.Where(r => r.Type == type).Sum(r => r.Width);

        public int WorkerPlaces(Room room) => Balance.Production.WorkersPerRoom * room.Width + (int)SpecValue(room, SpecEffect.WorkerSlot);

        /// <summary>Output multiplier of a merged room: +10% for every cell beyond the first.</summary>
        public double MergeBonus(Room room) => 1 + Balance.Merge.BonusPerCell * (room.Width - 1);

        /// <summary>An upgrade of a merged room costs the room job of every cell.</summary>
        public ResourceBag RoomUpgradeCost(Room room)
        {
            var one = RoomJobCost(room.Level + 1);
            double k = 1 + Balance.Merge.CostPerCell * (room.Width - 1);
            var bag = new ResourceBag();
            for (int i = 0; i < ResourceBag.Count; i++)
                bag[(ResourceType)i] = (long)Math.Ceiling(one[(ResourceType)i] * k);
            return bag;
        }

        /// <summary>Whether two rooms can merge now: same type and level, side by side, not being built.</summary>
        public Fail CanMerge(Room a, Room b)
        {
            if (a == null || b == null || a == b)
                return Fail.NotFound;
            if (a.Type != b.Type || a.Level != b.Level || a.Floor != b.Floor || a.Type == RoomType.Hearth)
                return Fail.NotMergeable;
            if (a.Cell + a.Width != b.Cell && b.Cell + b.Width != a.Cell)
                return Fail.NotMergeable;
            if (a.Width + b.Width > Balance.Merge.MaxWidth)
                return Fail.LimitReached;
            if (a.Level < 1 || State.Queue.IsQueued(a.Id) || State.Queue.IsQueued(b.Id))
                return Fail.AlreadyQueued;
            return Fail.None;
        }

        /// <summary>A neighbour this room can merge with right now, or null.</summary>
        public Room MergeCandidate(Room room)
        {
            if (room == null)
                return null;
            var left = RoomAt(room.Floor, room.Cell - 1);
            if (CanMerge(room, left) == Fail.None)
                return left;
            var right = RoomAt(room.Floor, room.Cell + room.Width);
            return CanMerge(room, right) == Fail.None ? right : null;
        }

        /// <summary>
        /// Merges two neighbouring rooms into one hall (GDD 6): it keeps the left room's id, gets the
        /// workers, the stock and the couples of both, and a decoration if the left one had none.
        /// </summary>
        public Fail MergeRooms(int roomId, int otherId, long now)
        {
            Advance(now);
            var a = FindRoom(roomId);
            var b = FindRoom(otherId);
            var fail = CanMerge(a, b);
            if (fail != Fail.None)
                return fail;
            var keep = a.Cell < b.Cell ? a : b;
            var gone = keep == a ? b : a;
            keep.Width += gone.Width;
            keep.Stock += gone.Stock;
            foreach (int id in gone.Workers)
            {
                keep.Workers.Add(id);
                var d = FindDweller(id);
                if (d != null)
                    d.RoomId = keep.Id;
            }
            foreach (var p in State.Pairings.Where(p => p.RoomId == gone.Id))
                p.RoomId = keep.Id;
            if (State.RoomDecor.TryGetValue(gone.Id, out var decor) && !State.RoomDecor.ContainsKey(keep.Id))
                State.RoomDecor[keep.Id] = decor;
            State.RoomDecor.Remove(gone.Id);
            State.Rooms.Remove(gone);
            return Fail.None;
        }

        // ------------------------------------------------------------------ births in the living quarters

        /// <summary>Time from a pairing to the birth: shorter with the level of the living quarters.</summary>
        public long BirthSeconds(Room room)
        {
            var b = Balance.Births;
            double hours = Math.Max(b.MinHours, b.Hours * Math.Pow(b.LevelFactor, Math.Max(1, room.Level) - 1));
            return (long)Math.Round(hours * 3600);
        }

        /// <summary>No place for another dweller: a child on the way already holds its place under the cap.</summary>
        public bool PopulationFull() => State.Dwellers.Count + State.Pairings.Count >= Formulas.DwellerCap(Balance, State.HearthLevel);

        public IEnumerable<Pairing> PairingsIn(int roomId) => State.Pairings.Where(p => p.RoomId == roomId);

        public Pairing PairingOf(int dwellerId) => State.Pairings.Find(p => p.ManId == dwellerId || p.WomanId == dwellerId);

        /// <summary>
        /// Puts a man and a woman into the living quarters (one couple per cell); after BirthSeconds a
        /// child is born, if the fortress still has room under the dweller cap.
        /// </summary>
        public Fail StartPairing(int roomId, int manId, int womanId, long now)
        {
            Advance(now);
            var room = FindRoom(roomId);
            var man = FindDweller(manId);
            var woman = FindDweller(womanId);
            if (room == null || man == null || woman == null)
                return Fail.NotFound;
            if (room.Type != RoomType.Bedrooms || room.Level < 1)
                return Fail.Locked;
            if (man.Sex != Sex.Male || woman.Sex != Sex.Female || man.RouteId != 0 || woman.RouteId != 0)
                return Fail.NotAPair;
            if (PairingOf(manId) != null || PairingOf(womanId) != null)
                return Fail.AlreadyQueued;
            if (PairingsIn(roomId).Count() >= room.Width)
                return Fail.RoomFull;
            if (PopulationFull())
                return Fail.LimitReached;
            // the living quarters hold only couples: anyone else there goes out
            var couples = PairingsIn(roomId).SelectMany(p => new[] { p.ManId, p.WomanId }).ToList();
            foreach (int id in room.Workers.Where(id => !couples.Contains(id)).ToList())
                Unassign(id);
            Unassign(manId);
            Unassign(womanId);
            foreach (var d in new[] { man, woman })
            {
                room.Workers.Add(d.Id);
                d.RoomId = room.Id;
            }
            State.Pairings.Add(new Pairing
            {
                RoomId = roomId,
                ManId = manId,
                WomanId = womanId,
                Start = now,
                End = now + BirthSeconds(room),
                Seed = (int)((State.NextDwellerId * 7919L + manId * 31L + womanId + now) % 1000003),
            });
            return Fail.None;
        }

        /// <summary>Couples whose time has come have a child; couples that left the room part.</summary>
        void SettleBirths(long now)
        {
            foreach (var p in State.Pairings.ToList())
            {
                var man = FindDweller(p.ManId);
                var woman = FindDweller(p.WomanId);
                if (FindRoom(p.RoomId) == null || man == null || woman == null || man.RoomId != p.RoomId || woman.RoomId != p.RoomId)
                {
                    State.Pairings.Remove(p);
                    continue;
                }
                if (now < p.End || State.Dwellers.Count >= Formulas.DwellerCap(Balance, State.HearthLevel))
                    continue; // not yet, or the fortress is full: the child waits for room
                State.Pairings.Remove(p);
                var child = Born(p, man, woman);
                AddNews(MoraleNews.Birth);
                ChildBorn?.Invoke(child);
            }
        }

        /// <summary>A child takes after its parents: the average of each stat, a little up or down, now and then a gift.</summary>
        Dweller Born(Pairing p, Dweller man, Dweller woman)
        {
            var b = Balance.Births;
            var rng = new Rng(p.Seed);
            var stats = new int[5];
            for (int k = 0; k < 5; k++)
            {
                int average = (int)Math.Round((man.Stats[k] + woman.Stats[k]) / 2.0, MidpointRounding.AwayFromZero);
                int shift = (int)(rng.Next() * (2 * b.StatSpread + 1)) - b.StatSpread;
                stats[k] = Math.Max(1, average + shift);
            }
            if (rng.Next() < b.GiftChance)
                stats[(int)(rng.Next() * 5)] += b.GiftBonus;
            var sex = rng.Next() < 0.5 ? Sex.Male : Sex.Female;
            var names = sex == Sex.Male ? BoyNames : GirlNames;
            return AddDweller(names[(int)(rng.Next() * names.Length)], stats, sex);
        }
    }
}

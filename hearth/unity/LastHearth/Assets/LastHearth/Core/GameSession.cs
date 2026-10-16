using System;
using System.Collections.Generic;

namespace LastHearth.Core
{
    public enum Fail
    {
        None,
        Locked,
        LimitReached,
        CellTaken,
        TooCold,
        NotFound,
        AlreadyQueued,
        AboveHearth,
        QueueFull,
        NotEnough,
        RoomFull,
        NotAHero,
        HeroCap,
        NoBarracks,
        RulesNotMet,
        NotRunning,
        NoTracks,
        NotToday,
        RankTooHigh,
        NoHeroes,
        SkillCap,
        HuntInProgress,
        NoHunt,
        NotMergeable,
        NotAPair,
        /// <summary>The bag of items is full: sell or dismantle something first.</summary>
        InventoryFull,
    }

    /// <summary>
    /// The rules of the fortress. Every command takes the current time explicitly, so the core is
    /// deterministic and can be replayed (offline progress, tests, a server later).
    /// </summary>
    public sealed partial class GameSession
    {
        public readonly Balance Balance;
        public readonly GameState State;

        public event Action<BuildJob> JobCompleted;

        public GameSession(Balance balance, GameState state)
        {
            Balance = balance;
            State = state;
            UpgradeSave();
            State.Queue.EnsureBuilders(BuilderCount());
        }

        // ------------------------------------------------------------------ queries

        public Room FindRoom(int id) => State.Rooms.Find(r => r.Id == id);

        public Dweller FindDweller(int id) => State.Dwellers.Find(d => d.Id == id);

        public int MaxRoomLevel(RoomType type)
        {
            int best = 0;
            foreach (var room in State.Rooms)
            {
                if (room.Type == type && room.Level > best)
                    best = room.Level;
            }
            return best;
        }

        public int CountRooms(RoomType type) => State.Rooms.FindAll(r => r.Type == type).Count;

        public int BuilderCount()
        {
            int level = Math.Min(State.HearthLevel, Balance.HearthLevels.Count);
            return Balance.Level(level).Builders + (State.FourthBuilder ? 1 : 0);
        }

        /// <summary>Silver + iron the warehouse holds (the smallest warehouse before it is built).</summary>
        public long Storage() => Balance.StorageAt(MaxRoomLevel(RoomType.Warehouse));

        public long Capacity(ResourceType type)
        {
            long storage = Storage();
            long silver = (long)Math.Round(storage * Balance.SilverShare);
            if (type == ResourceType.Silver)
                return silver;
            if (type == ResourceType.Iron)
                return storage - silver;
            return long.MaxValue;
        }

        public ResourceBag RoomJobCost(int toLevel) => ResourceBag.Materials(RoomJob(toLevel).cost, Balance.SilverShare);

        public ResourceBag HearthJobCost(int fromLevel) =>
            ResourceBag.Materials(Balance.Level(fromLevel).HearthJobCost, Balance.SilverShare);

        /// <summary>Room job to level T uses the row of hearth level T (rooms rise first, then the hearth).</summary>
        (long cost, long seconds) RoomJob(int toLevel)
        {
            if (toLevel <= Balance.HearthLevels.Count)
            {
                var row = Balance.Level(toLevel);
                return (row.RoomJobCost, row.RoomSeconds);
            }
            var last = Balance.Level(Balance.HearthLevels.Count);
            return ((long)Math.Ceiling(last.RoomJobCost * 1.4), last.RoomSeconds);
        }

        public bool IsRoomWarm(Room room)
        {
            if (Specialized(room, SpecEffect.NoFreeze))
                return true; // «Тёплые стены» keep the room warm even without coal
            if (State.OutOfCoal)
                return false;
            if (State.Time < State.FrostUntil && Math.Abs(room.Floor - Balance.Grid.HearthFloor) > 1)
                return false;
            return Formulas.IsFloorHabitable(Balance, State.HearthLevel, State.Mode, room.Floor, room.Level, WarmthMult());
        }

        public double RoomOutputPerHour(Room room)
        {
            var data = Balance.Room(room.Type);
            if (data.Produces == null || data.Stat == null || room.Level < 1)
                return 0;
            double total = 0;
            foreach (int id in room.Workers)
            {
                var d = FindDweller(id);
                if (d != null)
                    total += Formulas.WorkerOutputPerHour(Balance, data.Produces.Value, room.Level, d.StatOf(data.Stat.Value)) * WorkerHealth(d);
            }
            double warm = IsRoomWarm(room) ? 1.0 : Balance.Warmth.FrozenOutput;
            return total * warm * Balance.Mode(State.Mode).Output * OutputMult() * PetOutputMult(data.Produces.Value) * ResearchOutputMult(data.Produces.Value) * (1 + SpecValue(room, SpecEffect.Output)) * MergeBonus(room) *
                   SiegePenalty(State.Time);
        }

        /// <summary>A dug-out empty cell: its obstacle is cleared and nothing stands there.</summary>
        public bool CellFree(int floor, int cell) => CellStateAt(floor, cell) == CellState.Free;

        // ------------------------------------------------------------------ time

        /// <summary>
        /// Moves the world to <paramref name="now"/>: production fills the rooms (at most 10 hours),
        /// the hearth burns coal, the dwellers eat, finished jobs complete in order.
        /// Works the same for one second of play and for a night offline.
        /// </summary>
        public void Advance(long now)
        {
            while (now > State.Time)
            {
                var next = State.Queue.NextToFinish();
                long until = next != null && next.End <= now ? Math.Max(next.End, State.Time) : now;
                Produce(until - State.Time);
                State.Time = until;
                if (next != null && next.End <= now)
                    Complete(next);
                else
                    break;
            }
            // Jobs that finish exactly now (free finish, speed-ups) complete without time passing.
            var due = State.Queue.NextToFinish();
            while (due != null && due.End <= State.Time)
            {
                Complete(due);
                due = State.Queue.NextToFinish();
            }
            ReturnRoutes(State.Time);
            SettleCraft(State.Time); // the forge (GameSession.Gear.cs)
            UpdateEvents(State.Time);
            SettleSiege(State.Time);
            SettleMissedSurges(State.Time);
            CheckLogin(State.Time);
            SettlePvp(State.Time);
            SettleBirths(State.Time);
            SettleHalls(State.Time); // the tavern, infirmary, library, training hall and archive (GameSession.Rooms.cs)
        }

        void Produce(long seconds)
        {
            if (seconds <= 0)
                return;
            double hours = seconds / 3600.0;
            var wallet = State.Wallet;

            // Upkeep is counted in fractions and paid in whole units, so ticking every second costs
            // exactly the same as one long offline step.
            State.CoalDebt += CoalBurnPerHour() * hours;
            long burn = (long)Math.Floor(State.CoalDebt);
            State.OutOfCoal = wallet[ResourceType.Coal] < burn;
            wallet[ResourceType.Coal] = Math.Max(0, wallet[ResourceType.Coal] - burn);
            State.CoalDebt -= burn;

            State.FoodDebt += State.Dwellers.Count * Balance.FoodPerDwellerHour * FoodEatMult() * hours;
            long eat = (long)Math.Floor(State.FoodDebt);
            wallet[ResourceType.Food] = Math.Max(0, wallet[ResourceType.Food] - eat);
            State.FoodDebt -= eat;

            foreach (var room in State.Rooms)
            {
                double rate = RoomOutputPerHour(room);
                if (rate <= 0)
                    continue;
                room.Stock = Math.Min(room.Stock + rate * hours, rate * (Balance.OfflineCapHours + SpecValue(room, SpecEffect.StockHours)));
            }
        }

        void Complete(BuildJob job)
        {
            State.Queue.Remove(job);
            if (job.IsHearth)
            {
                State.HearthLevel = job.ToLevel;
                State.Queue.EnsureBuilders(BuilderCount());
                if (TavernOpen())
                    RefugeesToTavern(); // with a tavern they wait on its board to be hired (GameSession.Rooms.cs)
                else
                    RefugeesArrive();
                CheckAchievements();
            }
            else if (job.IsClearing)
            {
                State.Cleared.Add(CellKey(job.Floor, job.Cell));
            }
            else
            {
                var room = FindRoom(job.Target);
                if (room != null)
                    room.Level = job.ToLevel;
            }
            JobCompleted?.Invoke(job);
        }

        // ------------------------------------------------------------------ resources

        /// <summary>Adds to the wallet up to the warehouse capacity; returns what was taken.</summary>
        public long Deposit(ResourceType type, long amount)
        {
            if (amount <= 0)
                return 0;
            long room = Capacity(type) - State.Wallet[type];
            long taken = Math.Max(0, Math.Min(amount, room));
            State.Wallet[type] += taken;
            return taken;
        }

        public long Collect(int roomId)
        {
            var room = FindRoom(roomId);
            var data = room == null ? null : Balance.Room(room.Type);
            if (data?.Produces == null)
                return 0;
            long whole = (long)Math.Floor(room.Stock);
            long taken = Deposit(data.Produces.Value, whole);
            room.Stock -= taken;
            if (taken > 0)
                Count(DailyTask.Collect);
            return taken;
        }

        public void CollectAll()
        {
            foreach (var room in State.Rooms)
                Collect(room.Id);
        }

        public void ClaimRewards()
        {
            for (int i = 0; i < ResourceBag.Count; i++)
            {
                var type = (ResourceType)i;
                State.Rewards[type] -= Deposit(type, State.Rewards[type]);
            }
        }

        public void SetMode(HearthMode mode) => State.Mode = mode;

        // ------------------------------------------------------------------ building

        public Fail BuildRoom(RoomType type, int floor, int cell, long now, out Room built)
        {
            built = null;
            Advance(now);
            var data = Balance.Room(type);
            if (type == RoomType.Hearth || State.HearthLevel < data.Unlock)
                return Fail.Locked;
            if (CountCells(type) >= data.MaxCount)
                return Fail.LimitReached;
            if (!CellFree(floor, cell))
                return Fail.CellTaken;
            if (!Formulas.IsFloorHabitable(Balance, State.HearthLevel, State.Mode, floor, 1, WarmthMult()))
                return Fail.TooCold;
            int builder = State.Queue.PickBuilder(Balance.Build.QueuePerBuilder, now);
            if (builder < 0)
                return Fail.QueueFull;
            var cost = RoomJobCost(1);
            if (!State.Wallet.Covers(cost))
                return Fail.NotEnough;
            State.Wallet.Subtract(cost);
            built = new Room { Id = State.NextRoomId++, Type = type, Level = 0, Floor = floor, Cell = cell };
            State.Rooms.Add(built);
            State.Queue.Enqueue(builder, built.Id, 1, RoomJob(1).seconds, now);
            Advance(now);
            return Fail.None;
        }

        public Fail UpgradeRoom(int roomId, long now)
        {
            Advance(now);
            var room = FindRoom(roomId);
            if (room == null)
                return Fail.NotFound;
            if (State.Queue.IsQueued(roomId))
                return Fail.AlreadyQueued;
            int to = room.Level + 1;
            if (to > State.HearthLevel)
                return Fail.AboveHearth;
            int builder = State.Queue.PickBuilder(Balance.Build.QueuePerBuilder, now);
            if (builder < 0)
                return Fail.QueueFull;
            var cost = RoomUpgradeCost(room);
            if (!State.Wallet.Covers(cost))
                return Fail.NotEnough;
            State.Wallet.Subtract(cost);
            State.Queue.Enqueue(builder, roomId, to, RoomJob(to).seconds, now);
            Count(DailyTask.Build);
            Advance(now);
            return Fail.None;
        }

        public List<Requirement> HearthRequirements(long now)
        {
            Advance(now);
            return HearthRules.Unmet(this, now);
        }

        public Fail UpgradeHearth(long now)
        {
            Advance(now);
            if (HearthRules.Unmet(this, now).Count > 0)
                return Fail.RulesNotMet;
            int level = State.HearthLevel;
            int builder = State.Queue.PickBuilder(Balance.Build.QueuePerBuilder, now);
            State.Wallet.Subtract(HearthJobCost(level));
            State.Queue.Enqueue(builder, BuildJob.HearthTarget, level + 1, Balance.Level(level).HearthSeconds, now);
            Advance(now);
            return Fail.None;
        }

        /// <summary>The last 5 minutes of any job are free (one tap).</summary>
        public Fail FreeFinish(int jobId, long now)
        {
            Advance(now);
            var job = State.Queue.Find(jobId);
            if (job == null)
                return Fail.NotFound;
            if (!job.IsRunning(now) || job.Remaining(now) > Balance.Build.FreeFinishSeconds)
                return Fail.NotRunning;
            State.Queue.Shorten(jobId, job.Remaining(now), now);
            Advance(now);
            return Fail.None;
        }

        public Fail UseHourglass(int jobId, long now)
        {
            Advance(now);
            var job = State.Queue.Find(jobId);
            if (job == null)
                return Fail.NotFound;
            if (!job.IsRunning(now))
                return Fail.NotRunning;
            if (State.HourglassSeconds <= 0)
                return Fail.NotEnough;
            State.HourglassSeconds -= State.Queue.Shorten(jobId, State.HourglassSeconds, now);
            Advance(now);
            return Fail.None;
        }

        public Fail SpeedUpWithSparks(int jobId, long now)
        {
            Advance(now);
            var job = State.Queue.Find(jobId);
            if (job == null)
                return Fail.NotFound;
            if (!job.IsRunning(now))
                return Fail.NotRunning;
            long price = Formulas.SpeedupCost(Balance, job.Remaining(now));
            if (State.Wallet[ResourceType.Sparks] < price)
                return Fail.NotEnough;
            State.Wallet[ResourceType.Sparks] -= price;
            State.Queue.Shorten(jobId, job.Remaining(now), now);
            Advance(now);
            return Fail.None;
        }

        // ------------------------------------------------------------------ dwellers and heroes

        /// <summary>A new dweller; without a sex given, it follows the name.</summary>
        public Dweller AddDweller(string name, int[] stats, Sex sex = Sex.Unknown)
        {
            int id = State.NextDwellerId++;
            var d = new Dweller { Id = id, Name = name, Sex = sex == Sex.Unknown ? SexOfName(name, id) : sex, Stats = (int[])stats.Clone() };
            State.Dwellers.Add(d);
            return d;
        }

        public Fail Assign(int dwellerId, int roomId)
        {
            var d = FindDweller(dwellerId);
            var room = FindRoom(roomId);
            if (d == null || room == null)
                return Fail.NotFound;
            if (room.Workers.Count >= WorkerPlaces(room))
                return Fail.RoomFull;
            Unassign(dwellerId);
            room.Workers.Add(dwellerId);
            d.RoomId = roomId;
            return Fail.None;
        }

        public void Unassign(int dwellerId)
        {
            var d = FindDweller(dwellerId);
            if (d == null || d.RoomId < 0)
                return;
            FindRoom(d.RoomId)?.Workers.Remove(dwellerId);
            d.RoomId = -1;
        }

        /// <summary>GDD 8: heroes are dwellers trained in the barracks; no lottery.</summary>
        public Fail Promote(int dwellerId, HeroClass heroClass)
        {
            var d = FindDweller(dwellerId);
            if (d == null)
                return Fail.NotFound;
            if (MaxRoomLevel(RoomType.Barracks) < 1)
                return Fail.NoBarracks;
            d.HeroClass = heroClass;
            if (d.HeroLevel < 1)
                d.HeroLevel = 1;
            FitGear(d); // a new class: the weapon of the old one goes to the bag (the forge's issued piece stands in)
            return Fail.None;
        }

        public Fail LevelUpHero(int dwellerId)
        {
            var d = FindDweller(dwellerId);
            if (d == null)
                return Fail.NotFound;
            if (!d.IsHero)
                return Fail.NotAHero;
            if (d.HeroLevel >= Formulas.HeroLevelCap(Balance, State.HearthLevel))
                return Fail.HeroCap;
            long cost = Formulas.HeroLevelCost(Balance, d.HeroLevel);
            if (State.Wallet[ResourceType.Essence] < cost)
                return Fail.NotEnough;
            State.Wallet[ResourceType.Essence] -= cost;
            d.HeroLevel++;
            Count(DailyTask.HeroGrowth);
            return Fail.None;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core.Combat;
using LastHearth.Core.Surge;

namespace LastHearth.Core
{
    /// <summary>What the watchtower sees now (GDD 6): only the parts its level opens are filled.</summary>
    public sealed class ScoutReport
    {
        public int TowerLevel;
        /// <summary>Tonight's Surge (from level 1): when it opens, its waves and creatures; false when today's is settled.</summary>
        public bool Surge;
        public long SurgeAt;
        public int Waves;
        public int Creatures;
        /// <summary>From level 3: the monster families of tomorrow's dens.</summary>
        public List<MonsterFamily> Dens = new List<MonsterFamily>();
        /// <summary>From level 5: the floors the creatures come out of (the coldest rooms first).</summary>
        public List<int> SpawnFloors = new List<int>();
        /// <summary>From level 8: the stats the two forks of the next route test, and the team sum each needs.</summary>
        public List<(Stat stat, int need)> RouteForks = new List<(Stat stat, int need)>();
        /// <summary>From level 10: stars of the automatic defense if the player is away; -1 when not shown.</summary>
        public int Forecast = -1;
    }

    /// <summary>
    /// GDD 6: the work of the rooms that produce nothing. The tavern hires refugees from a board (rarity
    /// by luck), the infirmary heals injuries and the illness from the fog faster, the library researches a
    /// tree of 60 steps whose bonuses reach production, battle, the hearth and the other halls, the training
    /// hall raises the stats a player picks, the watchtower scouts the evening Surge and the dens, the chapel
    /// ascends heroes and gives a gift of the day, the archive keeps the records of the story. Numbers:
    /// balance.json "halls" (economy.py, ECONOMY.md section 16). The forge is the equipment's.
    /// </summary>
    public sealed partial class GameSession
    {
        HallsState Halls => State.Halls ?? (State.Halls = new HallsState());

        HallsData HallsNumbers => Balance.Halls;

        /// <summary>A research has finished (its node).</summary>
        public event Action<ResearchNodeData> ResearchDone;

        /// <summary>A training point was gained (the dweller and the stat).</summary>
        public event Action<Dweller, Stat> TrainingDone;

        /// <summary>The highest built room of a type, or null.</summary>
        public Room BestRoom(RoomType type) =>
            State.Rooms.Where(r => r.Type == type && r.Level > 0).OrderByDescending(r => r.Level).FirstOrDefault();

        /// <summary>Sum of a stat of the workers of the built rooms of a type (the sick count at half).</summary>
        public double StaffStat(RoomType type, Stat stat)
        {
            double sum = 0;
            foreach (var room in State.Rooms.Where(r => r.Type == type && r.Level > 0))
            {
                foreach (int id in room.Workers)
                {
                    var d = FindDweller(id);
                    if (d != null)
                        sum += d.StatOf(stat) * WorkerHealth(d);
                }
            }
            return sum;
        }

        /// <summary>All materials per hour of a typical fortress at the hearth level: the unit of the halls' prices.</summary>
        long HourOfIncome()
        {
            var table = Balance.Activities.Routes.IncomeByLevel;
            return table.Length == 0 ? 0 : table[Math.Max(1, Math.Min(State.HearthLevel, table.Length)) - 1];
        }

        long EssenceDay()
        {
            var table = Balance.Activities.EssenceByLevel;
            return table.Length == 0 ? 0 : table[Math.Max(1, Math.Min(State.HearthLevel, table.Length)) - 1];
        }

        /// <summary>Called by Advance: illness, healing, research, training and reading move with time.</summary>
        void SettleHalls(long now)
        {
            var newSick = SettleIllness();
            SettleHealing(now, newSick);
            SettleResearch(now);
            SettleTraining(now);
            SettleArchive(now);
        }

        // ------------------------------------------------------------------ tavern

        public bool TavernOpen() => MaxRoomLevel(RoomType.Tavern) >= 1;

        public RarityData RarityInfo(Rarity rarity) =>
            HallsNumbers.Tavern.Rarities.Find(r => r.Id == rarity) ?? new RarityData { Id = rarity, Name = rarity.ToString() };

        /// <summary>Rarity of a dweller (dwellers who did not come from the tavern are common).</summary>
        public Rarity RarityOf(int dwellerId) => Halls.Rarity.TryGetValue(dwellerId, out var r) ? r : Rarity.Common;

        /// <summary>Luck of the tavern: its level, the Spirit of its workers, the research «Тайны тумана».</summary>
        public double RecruitLuck()
        {
            int level = MaxRoomLevel(RoomType.Tavern);
            if (level < 1)
                return 0;
            var t = HallsNumbers.Tavern;
            return t.LuckPerLevel * level + t.LuckPerSpirit * StaffStat(RoomType.Tavern, Stat.Spirit) + ResearchValue(ResearchEffect.RecruitLuck);
        }

        /// <summary>Chances of each rarity on the board (sum 1): luck moves weight from the common recruits.</summary>
        public double[] RarityOdds()
        {
            var t = HallsNumbers.Tavern;
            int n = Enum.GetValues(typeof(Rarity)).Length;
            var w = new double[n];
            double luck = RecruitLuck(), moved = 0;
            for (int k = 0; k < n; k++)
            {
                w[k] = RarityInfo((Rarity)k).Weight;
                if (k > 0)
                {
                    double shift = k < t.LuckShift.Length ? t.LuckShift[k] * luck : 0;
                    w[k] += shift;
                    moved += shift;
                }
            }
            w[0] = Math.Max(t.MinCommon, w[0] - moved);
            double sum = w.Sum();
            return sum <= 0 ? w : w.Select(x => x / sum).ToArray();
        }

        public int BoardSize() => HallsNumbers.Tavern.Board + HallsNumbers.Tavern.BoardExtraAt.Count(lv => MaxRoomLevel(RoomType.Tavern) >= lv);

        long BoardSeconds => Math.Max(1, HallsNumbers.Tavern.BoardHours) * 3600L;

        /// <summary>When the next board goes up (boards change at fixed hours: 0:00, 6:00, 12:00, 18:00 UTC).</summary>
        public long NextBoardAt(long now) => (now / BoardSeconds + 1) * BoardSeconds;

        /// <summary>The recruits on the board now; a new board after every few hours. Empty without a tavern.</summary>
        public List<Recruit> TavernBoard(long now)
        {
            var tavern = Halls.Tavern;
            if (!TavernOpen())
                return new List<Recruit>();
            long slot = now / BoardSeconds * BoardSeconds;
            if (tavern.BoardAt != slot || tavern.Board.Count == 0)
            {
                tavern.BoardAt = slot;
                tavern.Board = NewBoard(Halls.Seed++);
            }
            return tavern.Board;
        }

        /// <summary>
        /// With a tavern the refugees a new hearth level brings (two) do not come in by themselves: they wait on the
        /// board, to be hired or left (GDD 8: people come through the tavern).
        /// </summary>
        void RefugeesToTavern(int count = 2)
        {
            var board = TavernBoard(State.Time);
            board.AddRange(NewBoard(Halls.Seed++, count));
        }

        List<Recruit> NewBoard(int seed, int size = -1)
        {
            var rng = new Rng(seed * 7919 + 17);
            var odds = RarityOdds();
            int L = State.HearthLevel;
            var board = new List<Recruit>();
            for (int i = 0; i < (size < 0 ? BoardSize() : size); i++)
            {
                double roll = rng.Next(), acc = 0;
                var rarity = Rarity.Common;
                for (int k = 0; k < odds.Length; k++)
                {
                    acc += odds[k];
                    if (roll < acc)
                    {
                        rarity = (Rarity)k;
                        break;
                    }
                }
                int bonus = RarityInfo(rarity).StatBonus;
                var stats = new int[5];
                for (int k = 0; k < 5; k++)
                    stats[k] = 3 + (int)(rng.Next() * 5) + 2 * L / 3 + bonus;
                stats[(int)(rng.Next() * 5)] += 4; // everyone is good at something
                var sex = rng.Next() < 0.5 ? Sex.Male : Sex.Female;
                var names = sex == Sex.Male ? BoyNames : GirlNames;
                board.Add(new Recruit { Name = names[(int)(rng.Next() * names.Length)], Sex = sex, Stats = stats, Rarity = rarity });
            }
            return board;
        }

        /// <summary>Silver (hours of income by rarity) and food (a day of the recruit's food × (1 + rarity)).</summary>
        public ResourceBag HireCost(Recruit recruit)
        {
            var bag = new ResourceBag();
            bag[ResourceType.Silver] = (long)Math.Ceiling(RarityInfo(recruit.Rarity).HireHours * HourOfIncome());
            bag[ResourceType.Food] = (long)Math.Ceiling(HallsNumbers.Tavern.FoodHours * Balance.FoodPerDwellerHour * (1 + (int)recruit.Rarity));
            return bag;
        }

        /// <summary>Hires a recruit of the board: a new dweller of the fortress, if the dweller cap allows.</summary>
        public Fail Hire(int index, long now, out Dweller hired)
        {
            hired = null;
            Advance(now);
            if (!TavernOpen())
                return Fail.Locked;
            var board = TavernBoard(now);
            if (index < 0 || index >= board.Count)
                return Fail.NotFound;
            var r = board[index];
            if (r.Hired)
                return Fail.AlreadyQueued;
            if (PopulationFull())
                return Fail.LimitReached;
            var cost = HireCost(r);
            if (!State.Wallet.Covers(cost))
                return Fail.NotEnough;
            State.Wallet.Subtract(cost);
            hired = AddDweller(r.Name, r.Stats, r.Sex);
            if (r.Rarity != Rarity.Common)
                Halls.Rarity[hired.Id] = r.Rarity;
            r.Hired = true;
            Halls.Tavern.Hired++;
            return Fail.None;
        }

        /// <summary>Whether a dweller can be let go: not a hero, not on a route, not a parent-to-be, not in a garrison.</summary>
        public Fail CanDismiss(Dweller d)
        {
            if (d == null)
                return Fail.NotFound;
            if (d.IsHero || d.RouteId != 0 || PairingOf(d.Id) != null)
                return Fail.Locked;
            if (State.Events.Any(run => run.Outposts.Any(o => o.Garrison.Contains(d.Id))))
                return Fail.Locked;
            return Fail.None;
        }

        /// <summary>Lets a dweller go back into the fog, to make room for a better recruit (like Hustle Castle).</summary>
        public Fail Dismiss(int dwellerId, long now)
        {
            Advance(now);
            var d = FindDweller(dwellerId);
            var fail = CanDismiss(d);
            if (fail != Fail.None)
                return fail;
            Unassign(dwellerId);
            Halls.Training.RemoveAll(t => t.DwellerId == dwellerId);
            Halls.Rarity.Remove(dwellerId);
            Halls.SickUntil.Remove(dwellerId);
            Halls.KnownInjury.Remove(dwellerId);
            State.Dwellers.Remove(d);
            Halls.Tavern.Dismissed++;
            return Fail.None;
        }

        // ------------------------------------------------------------------ infirmary

        /// <summary>
        /// How fast injuries and the illness heal: 1 without the infirmary; with it +5% per level, + the staff's
        /// Intellect / 50 and the research of «Быт»; the blazing hearth heals faster still (GDD 7).
        /// </summary>
        public double HealSpeed()
        {
            var m = HallsNumbers.Infirmary;
            double speed = 1;
            int level = MaxRoomLevel(RoomType.Infirmary);
            if (level >= 1)
                speed += m.PerLevel * level + StaffStat(RoomType.Infirmary, Stat.Intellect) / Math.Max(1, m.IntellectDivisor) + ResearchValue(ResearchEffect.Healing);
            if (State.Mode == HearthMode.Blazing)
                speed *= Math.Max(1, m.Blazing);
            return speed;
        }

        /// <summary>Real time left of a timer that heals at the current speed.</summary>
        public long HealingLeft(long until, long now) => until <= now ? 0 : (long)Math.Ceiling((until - now) / HealSpeed());

        public bool IsSick(int dwellerId, long now) => Halls.SickUntil.TryGetValue(dwellerId, out long until) && until > now;

        public long SickUntil(int dwellerId) => Halls.SickUntil.TryGetValue(dwellerId, out long until) ? until : 0;

        /// <summary>A sick worker gives half of the output (GDD 6: «болезни из тумана»).</summary>
        public double WorkerHealth(Dweller d) => d != null && IsSick(d.Id, State.Time) ? HallsNumbers.Infirmary.SickOutput : 1;

        /// <summary>The injured and the sick, the soonest healed first.</summary>
        public List<Dweller> Patients(long now) => State.Dwellers.Where(d => d.InjuredUntil > now || IsSick(d.Id, now))
            .OrderBy(d => Math.Max(d.InjuredUntil, SickUntil(d.Id))).ToList();

        /// <summary>Teams back from a route may bring the illness from the fog (each member by chance).</summary>
        HashSet<int> SettleIllness()
        {
            var h = Halls;
            var m = HallsNumbers.Infirmary;
            var fresh = new HashSet<int>();
            foreach (var e in State.Expeditions.Where(x => x.Back && !h.RoutesChecked.Contains(x.Id)))
            {
                h.RoutesChecked.Add(e.Id);
                var rng = new Rng(e.Seed * 31 + 17);
                foreach (int id in e.Team)
                {
                    if (rng.Next() >= m.IllnessChance || FindDweller(id) == null)
                        continue;
                    long until = e.End + (long)Math.Round(m.IllnessHours * 3600);
                    if (until > SickUntil(id))
                    {
                        h.SickUntil[id] = until;
                        fresh.Add(id);
                    }
                }
            }
            h.RoutesChecked.RemoveAll(id => State.Expeditions.All(x => x.Id != id));
            return fresh;
        }

        /// <summary>
        /// Injuries and illness run on their own timers; the infirmary takes (speed − 1) seconds more off them for
        /// every second that passes, counted from the moment each began.
        /// </summary>
        void SettleHealing(long now, HashSet<int> freshSick)
        {
            var h = Halls;
            var m = HallsNumbers.Infirmary;
            long from = h.HealedAt <= 0 || h.HealedAt > now ? now : h.HealedAt;
            double extra = HealSpeed() - 1;
            long injury = (long)Math.Round(m.InjuryHours * 3600), illness = (long)Math.Round(m.IllnessHours * 3600);
            foreach (var d in State.Dwellers)
            {
                if (d.InjuredUntil > from)
                {
                    bool known = h.KnownInjury.TryGetValue(d.Id, out long seen) && seen == d.InjuredUntil;
                    long start = known ? from : Math.Max(from, d.InjuredUntil - injury);
                    if (extra > 0 && now > start)
                        d.InjuredUntil = Math.Max(now, d.InjuredUntil - (long)Math.Round((now - start) * extra));
                }
                if (d.InjuredUntil > now)
                    h.KnownInjury[d.Id] = d.InjuredUntil;
                else
                    h.KnownInjury.Remove(d.Id);
            }
            foreach (int id in h.SickUntil.Keys.ToList())
            {
                long until = h.SickUntil[id];
                long start = freshSick.Contains(id) ? until - illness : from;
                if (extra > 0 && now > start)
                    until = Math.Max(now, until - (long)Math.Round((now - start) * extra));
                if (until > now && FindDweller(id) != null)
                    h.SickUntil[id] = until;
                else
                    h.SickUntil.Remove(id);
            }
            h.HealedAt = now;
        }

        // ------------------------------------------------------------------ library

        public List<ResearchNodeData> ResearchNodes => HallsNumbers.Library.Nodes;

        public ResearchNodeData ResearchNode(string id) => HallsNumbers.Library.Nodes.Find(n => n.Id == id);

        public bool Researched(string id) => Halls.Researched.Contains(id);

        public int ResearchCount => Halls.Researched.Count;

        /// <summary>The research going on now, or null.</summary>
        public ResearchNodeData CurrentResearch => string.IsNullOrEmpty(Halls.Research) ? null : ResearchNode(Halls.Research);

        public long ResearchEnd => Halls.ResearchEnd;

        /// <summary>The sum of an effect over the finished researches.</summary>
        public double ResearchValue(ResearchEffect effect)
        {
            double sum = 0;
            foreach (string id in Halls.Researched)
            {
                var n = ResearchNode(id);
                if (n != null && n.Effect == effect)
                    sum += n.Value;
            }
            return sum;
        }

        /// <summary>Output of the producing rooms ×this: coal and food by «Ремёсла», iron and silver too.</summary>
        public double ResearchOutputMult(ResourceType resource)
        {
            switch (resource)
            {
                case ResourceType.Coal:
                case ResourceType.Food:
                    return 1 + ResearchValue(ResearchEffect.Supply);
                case ResourceType.Iron:
                case ResourceType.Silver:
                    return 1 + ResearchValue(ResearchEffect.Materials);
                default:
                    return 1;
            }
        }

        /// <summary>The dwellers eat ×this («Быт»).</summary>
        public double FoodEatMult() => Math.Max(0.5, 1 - ResearchValue(ResearchEffect.Food));

        /// <summary>The next step of every branch that is not finished.</summary>
        public List<ResearchNodeData> ResearchFront() =>
            HallsNumbers.Library.Branches.Select(b => ResearchNodes.Where(n => n.Branch == b.Id && !Researched(n.Id)).OrderBy(n => n.Tier).FirstOrDefault())
                .Where(n => n != null).ToList();

        /// <summary>Whether a step can be started: the library's level, the step before it, nothing else going on.</summary>
        public Fail CanResearch(ResearchNodeData node)
        {
            if (node == null)
                return Fail.NotFound;
            if (Researched(node.Id))
                return Fail.AlreadyQueued;
            if (MaxRoomLevel(RoomType.Library) < Math.Max(1, node.Gate))
                return Fail.Locked;
            var before = ResearchNodes.Find(n => n.Branch == node.Branch && n.Tier == node.Tier - 1);
            if (before != null && !Researched(before.Id))
                return Fail.Locked;
            if (CurrentResearch != null)
                return Fail.QueueFull;
            return Fail.None;
        }

        /// <summary>Time of a step: the staff's Intellect / 100 and «Тайны тумана» make it faster.</summary>
        public long ResearchSeconds(ResearchNodeData node)
        {
            double speed = 1 + StaffStat(RoomType.Library, Stat.Intellect) / Math.Max(1, HallsNumbers.Library.IntellectDivisor) +
                           ResearchValue(ResearchEffect.ResearchSpeed);
            return Math.Max(1, (long)Math.Ceiling(node.Seconds / speed));
        }

        public ResourceBag ResearchCost(ResearchNodeData node) => ResourceBag.Materials(node.Cost, Balance.SilverShare);

        public Fail StartResearch(string id, long now)
        {
            Advance(now);
            var node = ResearchNode(id);
            var fail = CanResearch(node);
            if (fail != Fail.None)
                return fail;
            var cost = ResearchCost(node);
            if (!State.Wallet.Covers(cost))
                return Fail.NotEnough;
            State.Wallet.Subtract(cost);
            Halls.Research = node.Id;
            Halls.ResearchStart = now;
            Halls.ResearchEnd = now + ResearchSeconds(node);
            Advance(now);
            return Fail.None;
        }

        /// <summary>Diamonds to finish the research now (the last 5 minutes are free, like building).</summary>
        public long ResearchSpeedupCost(long now) => CurrentResearch == null ? 0 : Formulas.SpeedupCost(Balance, Math.Max(0, Halls.ResearchEnd - now));

        public Fail SpeedUpResearch(long now)
        {
            Advance(now);
            if (CurrentResearch == null)
                return Fail.NotRunning;
            long price = ResearchSpeedupCost(now);
            if (State.Wallet[ResourceType.Sparks] < price)
                return Fail.NotEnough;
            State.Wallet[ResourceType.Sparks] -= price;
            Halls.ResearchEnd = now;
            Advance(now);
            return Fail.None;
        }

        void SettleResearch(long now)
        {
            var node = CurrentResearch;
            if (node == null || Halls.ResearchEnd > now)
                return;
            Halls.Research = "";
            if (!Halls.Researched.Contains(node.Id))
                Halls.Researched.Add(node.Id);
            ResearchDone?.Invoke(node);
        }

        // ------------------------------------------------------------------ training hall

        public Room TrainingHall() => BestRoom(RoomType.TrainingHall);

        /// <summary>A stat trains up to 10 + 3 × hall level plus the rarity's bonus, at most 100.</summary>
        public int TrainingCap(Dweller d)
        {
            var hall = TrainingHall();
            if (hall == null || d == null)
                return 0;
            var t = HallsNumbers.Training;
            return Math.Min(100, t.CapBase + t.CapPerLevel * hall.Level + RarityInfo(RarityOf(d.Id)).CapBonus);
        }

        public long TrainingSeconds(Dweller d, Stat stat)
        {
            var t = HallsNumbers.Training;
            int level = TrainingHall()?.Level ?? 1;
            double hours = (t.HoursBase + t.HoursPerPoint * d.StatOf(stat)) * Math.Pow(t.LevelFactor, level - 1) /
                           (1 + ResearchValue(ResearchEffect.Training));
            return Math.Max(60, (long)Math.Round(hours * 3600));
        }

        public ResourceBag TrainingCost(Dweller d, Stat stat)
        {
            var t = HallsNumbers.Training;
            var bag = new ResourceBag();
            bag[ResourceType.Silver] = (long)Math.Ceiling(t.SilverHours * HourOfIncome() * (1 + d.StatOf(stat) / Math.Max(1, t.SilverStatDivisor)));
            return bag;
        }

        public TrainingSlot TrainingOf(int dwellerId) => Halls.Training.Find(t => t.DwellerId == dwellerId);

        public IEnumerable<TrainingSlot> Trainings => Halls.Training;

        public int PointsTrained => Halls.PointsTrained;

        /// <summary>
        /// A dweller or a hero trains one point of a stat chosen by the player: they take a place in the training
        /// hall (and leave other work) until it is done; the next point can be started at once.
        /// </summary>
        public Fail StartTraining(int dwellerId, Stat stat, long now)
        {
            Advance(now);
            var hall = TrainingHall();
            if (hall == null)
                return Fail.Locked;
            var d = FindDweller(dwellerId);
            if (d == null)
                return Fail.NotFound;
            if (d.RouteId != 0 || PairingOf(d.Id) != null)
                return Fail.Locked;
            if (TrainingOf(dwellerId) != null)
                return Fail.AlreadyQueued;
            if (d.StatOf(stat) >= TrainingCap(d))
                return Fail.LimitReached;
            if (d.RoomId != hall.Id && hall.Workers.Count >= WorkerPlaces(hall))
                return Fail.RoomFull;
            var cost = TrainingCost(d, stat);
            if (!State.Wallet.Covers(cost))
                return Fail.NotEnough;
            if (d.RoomId != hall.Id)
            {
                var fail = Assign(dwellerId, hall.Id);
                if (fail != Fail.None)
                    return fail;
            }
            State.Wallet.Subtract(cost);
            Halls.Training.Add(new TrainingSlot { DwellerId = dwellerId, Stat = stat, Start = now, End = now + TrainingSeconds(d, stat) });
            Advance(now);
            return Fail.None;
        }

        /// <summary>Points that are done; a trainee who left the hall loses the point in progress.</summary>
        void SettleTraining(long now)
        {
            var hall = TrainingHall();
            foreach (var slot in Halls.Training.ToList())
            {
                var d = FindDweller(slot.DwellerId);
                if (d == null || hall == null || d.RoomId != hall.Id)
                {
                    Halls.Training.Remove(slot);
                    continue;
                }
                if (slot.End > now)
                    continue;
                Halls.Training.Remove(slot);
                d.Stats[(int)slot.Stat] = Math.Min(100, d.Stats[(int)slot.Stat] + 1);
                Halls.PointsTrained++;
                TrainingDone?.Invoke(d, slot.Stat);
            }
        }

        // ------------------------------------------------------------------ watchtower

        public int TowerLevel => MaxRoomLevel(RoomType.Watchtower);

        /// <summary>Whether the tower's level shows this.</summary>
        public bool Scouts(ScoutInfo what)
        {
            var s = HallsNumbers.Watchtower.Scout.Find(x => x.What == what);
            return s != null && TowerLevel >= s.Level && TowerLevel >= 1;
        }

        public int ScoutLevel(ScoutInfo what) => HallsNumbers.Watchtower.Scout.Find(x => x.What == what)?.Level ?? 0;

        /// <summary>Sentries on the tower: Surge defenders +0.5% per level + their Agility / 400.</summary>
        public double WatchDefenseBonus()
        {
            int level = TowerLevel;
            if (level < 1)
                return 0;
            var w = HallsNumbers.Watchtower;
            return w.DefensePerLevel * level + StaffStat(RoomType.Watchtower, Stat.Agility) / Math.Max(1, w.AgilityDivisor);
        }

        /// <summary>Defense bonus of the halls in the Surge: the sentries and the research «Тайны тумана».</summary>
        public double HallsDefenseBonus() => WatchDefenseBonus() + ResearchValue(ResearchEffect.SurgeDefense);

        long forecastKey = long.MinValue;
        int forecastStars = -1;

        /// <summary>What the watchtower sees now; empty below its first level.</summary>
        public ScoutReport Scout(long now)
        {
            var report = new ScoutReport { TowerLevel = TowerLevel };
            if (report.TowerLevel < 1)
                return report;
            var surge = Balance.Activities.Surge;
            if (Scouts(ScoutInfo.Surge) && State.HearthLevel >= surge.Unlock && Combat != null)
            {
                long day = LocalDay(now);
                bool settled = State.SurgeDay >= day;
                report.Surge = !settled;
                report.SurgeAt = (settled ? day + 1 : day) * 86400 + surge.StartHour * 3600L - State.UtcOffsetSeconds;
                report.Waves = SurgeNumbers.Waves.Count;
                report.Creatures = SurgeNumbers.Waves.Sum(w => w.Length > 1 ? w[1] : 0);
                if (Scouts(ScoutInfo.Spawn) || Scouts(ScoutInfo.Forecast))
                {
                    var setup = BuildSurgeSetup();
                    if (Scouts(ScoutInfo.Spawn))
                        report.SpawnFloors.AddRange(setup.SpawnFloors);
                    if (Scouts(ScoutInfo.Forecast))
                    {
                        long key = now / 600 * 1000 + State.Rooms.Sum(r => r.Workers.Count + r.Level) + State.Dwellers.Count;
                        if (key != forecastKey)
                        {
                            var defense = new SurgeDefense(SurgeNumbers, setup) { Brain = SurgeDefense.Passive() };
                            defense.RunToEnd();
                            forecastStars = defense.Stars;
                            forecastKey = key;
                        }
                        report.Forecast = forecastStars;
                    }
                }
            }
            if (Scouts(ScoutInfo.Dens) && State.HearthLevel >= Balance.Activities.Hunt.Unlock)
                report.Dens.AddRange(HuntDens(now + 86400));
            if (Scouts(ScoutInfo.Route) && State.HearthLevel >= Balance.Activities.Routes.Unlock)
            {
                for (int f = 0; f < 2; f++)
                    report.RouteForks.Add(ForkCheck(State.NextBattleSeed, f, State.HearthLevel));
            }
            return report;
        }

        // ------------------------------------------------------------------ chapel

        public bool ChapelOpen() => MaxRoomLevel(RoomType.Chapel) >= 1;

        public int AscensionRank(int heroId) => Halls.Ascension.TryGetValue(heroId, out int rank) ? rank : 0;

        /// <summary>The next rank of a hero (its hero level, chapel level and essence), or null at the top.</summary>
        public AscensionData NextAscension(Dweller d)
        {
            var ranks = HallsNumbers.Chapel.Ranks;
            int rank = d == null ? ranks.Count : AscensionRank(d.Id);
            return rank < ranks.Count ? ranks[rank] : null;
        }

        public long AscensionEssence(AscensionData rank) => rank == null ? 0 : (long)Math.Round(EssenceDay() * rank.EssenceDays);

        /// <summary>Attack and health of a hero ×this: +4% per rank of ascension.</summary>
        public double AscensionMult(int heroId) => 1 + HallsNumbers.Chapel.Bonus * AscensionRank(heroId);

        /// <summary>
        /// Ascends a hero in the chapel: the hero must have reached the rank's level, the chapel its level; the
        /// price is essence. The hero level cap stays (the anti-rush rules are untouched).
        /// </summary>
        public Fail Ascend(int heroId, long now)
        {
            Advance(now);
            if (!ChapelOpen())
                return Fail.Locked;
            var d = FindDweller(heroId);
            if (d == null)
                return Fail.NotFound;
            if (!d.IsHero)
                return Fail.NotAHero;
            var next = NextAscension(d);
            if (next == null)
                return Fail.LimitReached;
            if (d.HeroLevel < next.HeroLevel)
                return Fail.HeroCap;
            if (MaxRoomLevel(RoomType.Chapel) < next.Chapel)
                return Fail.Locked;
            long essence = AscensionEssence(next);
            if (State.Wallet[ResourceType.Essence] < essence)
                return Fail.NotEnough;
            State.Wallet[ResourceType.Essence] -= essence;
            Halls.Ascension[heroId] = AscensionRank(heroId) + 1;
            Count(DailyTask.HeroGrowth);
            return Fail.None;
        }

        /// <summary>The gift the chapel gives today (essence, treats and hourglass in turn).</summary>
        public ChapelGift TodayGift(long now)
        {
            var gifts = HallsNumbers.Chapel.Gifts;
            return gifts.Count == 0 ? null : gifts[(int)(LocalDay(now) % gifts.Count)];
        }

        /// <summary>The amount of a gift: essence, treats or hourglass seconds; ×(1 + level / 30) × (1 + Spirit / 100).</summary>
        public long ChapelGiftAmount(ChapelGift gift)
        {
            if (gift == null)
                return 0;
            int level = MaxRoomLevel(RoomType.Chapel);
            double k = (1 + level / 30.0) * (1 + StaffStat(RoomType.Chapel, Stat.Spirit) / 100.0);
            switch (gift.Goods)
            {
                case GiftGoods.Essence: return (long)Math.Round(EssenceDay() * gift.Amount * k);
                case GiftGoods.Treats: return (long)Math.Round(gift.Amount * k);
                default: return (long)Math.Round(gift.Amount * 60 * k);
            }
        }

        public bool GiftReady(long now) => ChapelOpen() && Halls.GiftDay != LocalDay(now);

        /// <summary>Takes the gift of the day (once per local day).</summary>
        public Fail ClaimGift(long now, out ChapelGift gift, out long amount)
        {
            gift = null;
            amount = 0;
            Advance(now);
            if (!ChapelOpen())
                return Fail.Locked;
            if (!GiftReady(now))
                return Fail.NotToday;
            gift = TodayGift(now);
            amount = ChapelGiftAmount(gift);
            switch (gift.Goods)
            {
                case GiftGoods.Essence:
                    State.Wallet[ResourceType.Essence] += amount;
                    break;
                case GiftGoods.Treats:
                    GrantTreats(amount);
                    break;
                default:
                    State.HourglassSeconds += amount;
                    break;
            }
            Halls.GiftDay = LocalDay(now);
            return Fail.None;
        }

        // ------------------------------------------------------------------ battle

        /// <summary>The library («Ратное дело») and the chapel (ascension) make the squad stronger (SquadSetup).</summary>
        void ApplyHalls(BattleSetup setup)
        {
            double attack = 1 + ResearchValue(ResearchEffect.Attack), hp = 1 + ResearchValue(ResearchEffect.Health);
            foreach (var h in setup.Heroes)
            {
                double rank = AscensionMult(h.Id);
                h.AttackMult *= attack * rank;
                h.HpMult *= hp * rank;
            }
        }

        /// <summary>Attack and health of a hero ×these from the halls (for the hero screen).</summary>
        public (double attack, double hp) HallsHeroMult(int heroId) =>
            ((1 + ResearchValue(ResearchEffect.Attack)) * AscensionMult(heroId), (1 + ResearchValue(ResearchEffect.Health)) * AscensionMult(heroId));

        // ------------------------------------------------------------------ archive

        public bool ArchiveOpen() => MaxRoomLevel(RoomType.Archive) >= 1;

        public List<RecordData> Records => HallsNumbers.Archive.Records;

        public RecordData Record(string id) => Records.Find(r => r.Id == id);

        /// <summary>How far the player is on the way to records of this kind.</summary>
        public int RecordProgress(RecordKind kind)
        {
            switch (kind)
            {
                case RecordKind.Hearth: return State.HearthLevel;
                case RecordKind.Missions: return MissionsCleared;
                case RecordKind.Hunts: return State.HuntWins;
                case RecordKind.Routes: return State.NextExpeditionId - 1;
                case RecordKind.Research: return ResearchCount;
                default: return Halls.Ascension.Values.Sum();
            }
        }

        public bool RecordFound(RecordData r) => r != null && RecordProgress(r.Kind) >= r.Need;

        public bool RecordRead(string id) => Halls.Read.Contains(id);

        public RecordData CurrentReading => string.IsNullOrEmpty(Halls.Reading) ? null : Record(Halls.Reading);

        public long ReadingEnd => Halls.ReadingEnd;

        /// <summary>Hours to read a record: 1 + 0.5 × its number, faster with the archive's level and staff.</summary>
        public long ReadSeconds(RecordData r)
        {
            var a = HallsNumbers.Archive;
            double speed = 1 + a.PerLevel * MaxRoomLevel(RoomType.Archive) + StaffStat(RoomType.Archive, Stat.Intellect) / Math.Max(1, a.IntellectDivisor) +
                           ResearchValue(ResearchEffect.Decipher);
            return Math.Max(60, (long)Math.Round((a.HoursBase + a.HoursPerRecord * Records.IndexOf(r)) * 3600 / speed));
        }

        public long RecordEssence() => (long)Math.Round(EssenceDay() * HallsNumbers.Archive.EssenceDays);

        public Fail StartReading(string id, long now)
        {
            Advance(now);
            if (!ArchiveOpen())
                return Fail.Locked;
            var r = Record(id);
            if (r == null)
                return Fail.NotFound;
            if (!RecordFound(r))
                return Fail.Locked;
            if (RecordRead(id))
                return Fail.AlreadyQueued;
            if (CurrentReading != null)
                return Fail.QueueFull;
            Halls.Reading = id;
            Halls.ReadingEnd = now + ReadSeconds(r);
            Advance(now);
            return Fail.None;
        }

        void SettleArchive(long now)
        {
            var r = CurrentReading;
            if (r == null || Halls.ReadingEnd > now)
                return;
            Halls.Reading = "";
            if (!Halls.Read.Contains(r.Id))
            {
                Halls.Read.Add(r.Id);
                State.Wallet[ResourceType.Essence] += RecordEssence();
            }
        }
    }
}

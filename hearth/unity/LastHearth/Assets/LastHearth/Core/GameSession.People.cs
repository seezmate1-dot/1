using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core.Combat;

namespace LastHearth.Core
{
    /// <summary>
    /// GDD 7-8: the depth of the dwellers and the spirit of the fortress. Every dweller has a trait of character
    /// (a second, good one at level 30), a rarity (the tavern's) and a level up to 60: work, training and battles
    /// bring experience, and a level-up may add a point to the stat the dweller works with, more often for a rarer
    /// one (heroes grow by their hero level instead).
    /// The spirit («Дух крепости», 0-100) follows its target: food, warmth and coal, the hearth's mode, decorations,
    /// the chapel, beds in the living quarters, the injured and the sick, cheerful and gloomy people and the news of
    /// the day. Its band makes the rooms produce up to ±15%; in despair the fortress warns, then the newest dweller
    /// deserts (and can be called back later). Numbers: balance.json "people" (economy.py, ECONOMY.md section 17).
    /// </summary>
    public sealed partial class GameSession
    {
        PeopleState People => State.People ?? (State.People = new PeopleState());

        PeopleData PeopleNumbers => Balance.People ?? (Balance.People = new PeopleData());

        MoraleData MoraleNumbers => PeopleNumbers.Morale;

        /// <summary>A dweller reached a new level (the stat point and a second trait are already given).</summary>
        public event Action<Dweller> DwellerLevelUp;

        /// <summary>A dweller left the fortress in despair.</summary>
        public event Action<Dweller> DwellerDeserted;

        /// <summary>Called by Advance: experience of the work, the spirit and desertion move with time.</summary>
        void SettlePeople(long now)
        {
            SettleWorkXp(now);
            SettleMorale(now);
        }

        /// <summary>Old saves: every dweller gets a level and a trait (GameSession.UpgradeSave).</summary>
        void UpgradePeople()
        {
            foreach (var d in State.Dwellers)
                EnsureDweller(d);
            var p = People;
            if (p.Morale < 0)
            {
                // a new fortress or a save from before the spirit starts calm and fed, then follows its target
                p.Morale = MoraleNumbers.Base + MoraleNumbers.Fed;
                p.MoraleAt = State.Time;
            }
            if (p.XpAt <= 0)
                p.XpAt = State.Time;
        }

        void EnsureDweller(Dweller d)
        {
            if (d.Level < 1)
                d.Level = 1;
            if (d.Traits == null)
                d.Traits = new List<Trait>();
            if (d.Traits.Count == 0)
                d.Traits.Add(RollTrait(d.Id, RarityOf(d.Id), d.Traits, null));
        }

        // ------------------------------------------------------------------ traits

        public List<TraitData> TraitList => PeopleNumbers.Traits;

        public TraitData TraitInfo(Trait t) => PeopleNumbers.Traits.Find(x => x.Id == t) ?? new TraitData { Id = t, Name = t.ToString() };

        /// <summary>A trait by the dweller's id: good with the rarity's chance (or as asked), never one the dweller has.</summary>
        Trait RollTrait(int dwellerId, Rarity rarity, List<Trait> has, bool? good)
        {
            var rng = new Rng(Mix(dwellerId * 31 + has.Count));
            double roll = rng.Next();
            bool want = good ?? roll < RarityInfo(rarity).GoodTrait;
            var pool = TraitList.Where(t => t.Good == want && !has.Contains(t.Id)).ToList();
            if (pool.Count == 0)
                pool = TraitList.Where(t => !has.Contains(t.Id)).ToList();
            if (pool.Count == 0)
                return Trait.Hardworking;
            return pool[(int)(rng.Next() * pool.Count) % pool.Count].Id;
        }

        /// <summary>A well-mixed seed of a small number (neighbouring ids give unrelated traits).</summary>
        static int Mix(int x)
        {
            unchecked
            {
                uint h = (uint)x * 0x9E3779B1u;
                h ^= h >> 16;
                h *= 0x85EBCA6Bu;
                h ^= h >> 13;
                h *= 0xC2B2AE35u;
                h ^= h >> 16;
                return (int)(h & 0x7FFFFFFF);
            }
        }

        /// <summary>A new dweller's trait (GameSession.AddDweller) and again once the tavern has told its rarity.</summary>
        void GiveTrait(Dweller d)
        {
            d.Traits = new List<Trait>();
            d.Traits.Add(RollTrait(d.Id, RarityOf(d.Id), d.Traits, null));
        }

        public bool HasTrait(Dweller d, Trait t) => d?.Traits != null && d.Traits.Contains(t);

        double TraitSum(Dweller d, Func<TraitData, double> f)
        {
            if (d?.Traits == null)
                return 0;
            double sum = 0;
            foreach (var t in d.Traits)
                sum += f(TraitInfo(t));
            return sum;
        }

        /// <summary>Output of the dweller's work ×this («Трудяга», «Лентяй», «Трус» in the rear).</summary>
        public double TraitWorkMult(Dweller d) => Math.Max(0.1, 1 + TraitSum(d, t => t.Work));

        /// <summary>Attack and health in battle ×this («Храбрец», «Трус»).</summary>
        public double TraitBattleMult(Dweller d) => Math.Max(0.1, 1 + TraitSum(d, t => t.Battle));

        public double TraitXpMult(Dweller d) => Math.Max(0, 1 + TraitSum(d, t => t.Xp));

        /// <summary>The dweller's output in a frozen room («Закалённый»: 80% instead of 50%).</summary>
        public double ColdOutput(Dweller d)
        {
            double best = Balance.Warmth.FrozenOutput;
            if (d?.Traits != null)
            {
                foreach (var t in d.Traits)
                    best = Math.Max(best, TraitInfo(t).Cold);
            }
            return best;
        }

        /// <summary>The chance of the illness from the fog ×this («Хворый»).</summary>
        public double IllnessMult(int dwellerId)
        {
            var d = FindDweller(dwellerId);
            double k = 1;
            if (d?.Traits != null)
            {
                foreach (var t in d.Traits)
                    k *= TraitInfo(t).Illness <= 0 ? 1 : TraitInfo(t).Illness;
            }
            return k;
        }

        /// <summary>Brave and cowardly heroes in battle (SquadSetup, through ApplyHalls).</summary>
        void ApplyTraits(BattleSetup setup)
        {
            foreach (var h in setup.Heroes)
            {
                double k = TraitBattleMult(FindDweller(h.Id));
                h.AttackMult *= k;
                h.HpMult *= k;
            }
        }

        // ------------------------------------------------------------------ levels and experience

        public int MaxDwellerLevel => Math.Max(1, PeopleNumbers.MaxLevel);

        /// <summary>Experience to go from this level to the next (0 at the top).</summary>
        public double XpToNext(int level) => level >= MaxDwellerLevel ? 0 : PeopleNumbers.XpBase * Math.Pow(Math.Max(1, level), PeopleNumbers.XpGrowth);

        /// <summary>Experience per hour this dweller gets now: work in a room with a stat, training (×2), sick (×0.5).</summary>
        public double XpPerHour(Dweller d)
        {
            if (d == null || d.RouteId != 0)
                return 0;
            var room = FindRoom(d.RoomId);
            if (room == null || room.Level < 1)
                return 0;
            var p = PeopleNumbers;
            double rate;
            if (room.Type == RoomType.TrainingHall)
                rate = TrainingOf(d.Id) != null ? p.XpWork * p.XpTraining : 0;
            else
                rate = Balance.Room(room.Type).Stat != null ? p.XpWork : 0;
            if (IsSick(d.Id, State.Time))
                rate *= p.XpSick;
            return rate * TraitXpMult(d);
        }

        /// <summary>Adds experience (×the dweller's trait) and raises the level; returns the levels gained.</summary>
        public int GainXp(Dweller d, double xp)
        {
            if (d == null || xp <= 0)
                return 0;
            EnsureDweller(d);
            return AddXp(d, xp * TraitXpMult(d));
        }

        int AddXp(Dweller d, double xp)
        {
            if (d.Level >= MaxDwellerLevel)
            {
                d.Xp = 0;
                return 0;
            }
            int gained = 0;
            d.Xp += xp;
            while (d.Level < MaxDwellerLevel && d.Xp >= XpToNext(d.Level))
            {
                d.Xp -= XpToNext(d.Level);
                d.Level++;
                gained++;
                LevelUp(d);
            }
            if (d.Level >= MaxDwellerLevel)
                d.Xp = 0;
            return gained;
        }

        /// <summary>Stat points a level brings by the rarity: the whole part of level × growth goes up by them.</summary>
        public int GrowthPoints(Rarity rarity, int level)
        {
            double g = RarityInfo(rarity).Growth;
            return (int)Math.Floor(level * g + 1e-9) - (int)Math.Floor((level - 1) * g + 1e-9);
        }

        /// <summary>The stat a level-up raises: the stat of the room, the one trained, else the best one.</summary>
        public Stat GrowthStat(Dweller d)
        {
            var room = FindRoom(d.RoomId);
            var stat = room == null ? null : Balance.Room(room.Type).Stat;
            if (stat != null)
                return stat.Value;
            var slot = TrainingOf(d.Id);
            if (slot != null)
                return slot.Stat;
            return (Stat)Array.IndexOf(d.Stats, d.Stats.Max());
        }

        void LevelUp(Dweller d)
        {
            // a hero's strength is the hero level (essence) and gear: experience gives heroes no stat points, so the
            // campaign and the anti-rush rules keep the economy model's pace (ECONOMY.md, sections 6, 11 and 17)
            int points = d.IsHero ? 0 : GrowthPoints(RarityOf(d.Id), d.Level);
            if (points > 0)
            {
                int k = (int)GrowthStat(d);
                d.Stats[k] = Math.Min(100, d.Stats[k] + points);
            }
            if (d.Level >= PeopleNumbers.SecondTraitLevel && d.Traits.Count < 2)
                d.Traits.Add(RollTrait(d.Id, RarityOf(d.Id), d.Traits, true));
            DwellerLevelUp?.Invoke(d);
        }

        /// <summary>Every hour of work and training counts, also offline (the rooms work then too).</summary>
        void SettleWorkXp(long now)
        {
            var p = People;
            long from = p.XpAt <= 0 || p.XpAt > now ? now : p.XpAt;
            p.XpAt = now;
            double hours = (now - from) / 3600.0;
            if (hours <= 0)
                return;
            foreach (var d in State.Dwellers)
            {
                double rate = XpPerHour(d);
                if (rate > 0)
                {
                    EnsureDweller(d);
                    AddXp(d, rate * hours);
                }
            }
        }

        /// <summary>The heroes of a finished battle learn from it: a win, a loss, or an automatic repeat.</summary>
        void FightXp(Battle battle, bool repeat = false)
        {
            if (battle == null)
                return;
            var p = PeopleNumbers;
            double xp = repeat ? p.XpRepeat : battle.Won ? p.XpWin : p.XpLoss;
            foreach (var h in battle.Heroes)
                GainXp(FindDweller(h.Id), xp);
        }

        // ------------------------------------------------------------------ the spirit of the fortress

        /// <summary>The spirit now (0-100).</summary>
        public double Morale => People.Morale < 0 ? MoraleTarget(State.Time) : People.Morale;

        /// <summary>Beds of the living quarters: 6 + 2 × (level − 1) in every cell.</summary>
        public int Beds()
        {
            var m = MoraleNumbers;
            return State.Rooms.Where(r => r.Type == RoomType.Bedrooms && r.Level >= 1)
                .Sum(r => Math.Max(1, r.Width) * (m.BedsBase + m.BedsPerLevel * (r.Level - 1)));
        }

        public MoraleNewsData NewsInfo(MoraleNews id) => MoraleNumbers.News.Find(n => n.Id == id) ?? new MoraleNewsData { Id = id, Name = id.ToString() };

        /// <summary>News of the fortress: lifts or lowers the spirit for some hours (a new one of a kind replaces the old).</summary>
        public void AddNews(MoraleNews id)
        {
            var info = NewsInfo(id);
            People.News.RemoveAll(n => n.Id == id);
            People.News.Add(new MoraleBoost { Id = id, Until = State.Time + (long)Math.Round(info.Hours * 3600) });
        }

        /// <summary>Food the dwellers can reach: the store and what waits in the kitchens and the greenhouse.</summary>
        public double FoodReserve()
        {
            double food = State.Wallet[ResourceType.Food];
            foreach (var room in State.Rooms)
            {
                if (room.Level >= 1 && Balance.Room(room.Type).Produces == ResourceType.Food)
                    food += room.Stock;
            }
            return food;
        }

        /// <summary>What the spirit is made of now: every line with its value (the target is 50 plus their sum).</summary>
        public List<MoraleFactor> MoraleFactors(long now)
        {
            var m = MoraleNumbers;
            var list = new List<MoraleFactor>();
            void Add(string name, double value)
            {
                if (Math.Abs(value) > 1e-9)
                    list.Add(new MoraleFactor { Name = name, Value = value });
            }

            double eat = State.Dwellers.Count * Balance.FoodPerDwellerHour * FoodEatMult();
            double food = FoodReserve();
            if (State.Dwellers.Count > 0 && food < 1)
                Add("Голод: в кладовой нет еды", m.Hunger);
            else if (eat <= 0 || food >= eat * m.FoodHours)
                Add("Сыты: еды больше чем на " + m.FoodHours + " ч", m.Fed);
            if (State.OutOfCoal)
                Add("Очаг без угля", m.NoCoal);
            int workers = 0, cold = 0;
            foreach (var room in State.Rooms)
            {
                if (room.Workers.Count == 0 || room.Level < 1 || room.Type == RoomType.Bedrooms)
                    continue;
                workers += room.Workers.Count;
                if (!IsRoomWarm(room))
                    cold += room.Workers.Count;
            }
            if (workers > 0 && cold > 0)
                Add("Мёрзнут в холодных залах: " + cold + " из " + workers, m.Frozen * cold / workers);
            var mode = m.Modes.Find(x => x.Mode == State.Mode);
            if (mode != null)
                Add(State.Mode == HearthMode.Blazing ? "Очаг пылает" : State.Mode == HearthMode.Smoldering ? "Очаг тлеет: зябко" : "Очаг", mode.Value);
            int decorated = State.RoomDecor.Keys.Count(id => FindRoom(id) != null);
            if (decorated > 0)
                Add("Украшенные залы: " + decorated, Math.Min(m.DecorMax, m.Decor * decorated));
            int chapel = MaxRoomLevel(RoomType.Chapel);
            if (chapel >= 1)
                Add("Часовня Угля", m.ChapelBase + chapel / Math.Max(1, m.ChapelLevels));
            int homeless = State.Dwellers.Count - Beds();
            if (homeless > 0)
                Add("Тесно: без кровати " + homeless, Math.Max(m.CrowdMax, m.Crowd * homeless));
            int patients = State.Dwellers.Count(d => d.InjuredUntil > now || IsSick(d.Id, now));
            if (patients > 0)
                Add("Ранены и больны: " + patients, Math.Max(m.PatientMax, m.Patient * patients));
            double traits = 0;
            foreach (var d in State.Dwellers)
                traits += TraitSum(d, t => t.Morale);
            double cap = PeopleNumbers.TraitMoraleCap;
            Add(traits > 0 ? "Весельчаки" : "Нытики", Math.Max(-cap, Math.Min(cap, traits)));
            foreach (var n in People.News.Where(x => x.Until > now))
                Add(NewsInfo(n.Id).Name, NewsInfo(n.Id).Value);
            return list;
        }

        /// <summary>Where the spirit is heading: 50 plus the factors, within 0-100.</summary>
        public double MoraleTarget(long now) => Math.Max(0, Math.Min(100, MoraleNumbers.Base + MoraleFactors(now).Sum(f => f.Value)));

        /// <summary>The band of the spirit: its name and what it does to the output.</summary>
        public MoraleBandData MoraleBand(double morale)
        {
            MoraleBandData band = null;
            foreach (var b in MoraleNumbers.Bands)
            {
                if (morale >= b.From && (band == null || b.From >= band.From))
                    band = b;
            }
            return band ?? new MoraleBandData { Name = "Спокойствие" };
        }

        public MoraleBandData MoraleBand() => MoraleBand(Morale);

        /// <summary>Output of every room ×this: ±15% at the edges (GDD 7).</summary>
        public double MoraleOutputMult() => 1 + MoraleBand().Output;

        public bool InDespair => Morale < MoraleNumbers.DesertBelow;

        /// <summary>When the next dweller leaves if the spirit stays this low (0: nobody is about to leave).</summary>
        public long DesertionAt() => People.LowSince <= 0 || NextDeserter() == null ? 0 : People.LowSince + (long)Math.Round(MoraleNumbers.DesertHours * 3600);

        /// <summary>Who leaves first: the newcomers (the lowest level, then the newest); never a hero or someone busy.</summary>
        public Dweller NextDeserter() =>
            State.Dwellers.Where(d => !d.IsHero && CanDismiss(d) == Fail.None).OrderBy(d => d.Level).ThenByDescending(d => d.Id).FirstOrDefault();

        public IReadOnlyList<Deserter> Deserters => People.Deserters;

        void SettleMorale(long now)
        {
            var p = People;
            var m = MoraleNumbers;
            p.News.RemoveAll(n => n.Until <= now);
            p.Deserters.RemoveAll(x => x.Until <= now);
            double target = MoraleTarget(now);
            if (p.Morale < 0)
            {
                p.Morale = target; // a new game or an old save starts at its target
                p.MoraleAt = now;
            }
            long from = p.MoraleAt <= 0 || p.MoraleAt > now ? now : p.MoraleAt;
            double hours = (now - from) / 3600.0;
            if (hours > 0)
                p.Morale = target + (p.Morale - target) * Math.Exp(-hours / Math.Max(0.01, m.Hours));
            p.Morale = Math.Max(0, Math.Min(100, p.Morale));
            p.MoraleAt = now;
            CheckDesertion(now, false);
        }

        /// <summary>
        /// In despair the fortress first warns (LowSince is set); after DesertHours in a row the newest dweller leaves,
        /// and the clock starts again. <paramref name="force"/>: the developer's menu does not wait for the clock.
        /// </summary>
        Dweller CheckDesertion(long now, bool force)
        {
            var p = People;
            if (!InDespair)
            {
                p.LowSince = 0;
                return null;
            }
            if (p.LowSince <= 0)
                p.LowSince = now;
            if (!force && now - p.LowSince < MoraleNumbers.DesertHours * 3600)
                return null;
            var gone = Desert(now);
            if (gone != null)
                p.LowSince = now;
            return gone;
        }

        Dweller Desert(long now)
        {
            var d = NextDeserter();
            if (d == null)
                return null;
            var m = MoraleNumbers;
            var rarity = RarityOf(d.Id);
            Unassign(d.Id);
            Halls.Training.RemoveAll(t => t.DwellerId == d.Id);
            Halls.Rarity.Remove(d.Id);
            Halls.SickUntil.Remove(d.Id);
            Halls.KnownInjury.Remove(d.Id);
            State.Dwellers.Remove(d);
            var p = People;
            p.Deserters.Add(new Deserter { Dweller = d, Rarity = rarity, Left = now, Until = now + (long)Math.Round(m.DeserterDays * 86400) });
            while (p.Deserters.Count > Math.Max(0, m.DesertersKept))
                p.Deserters.RemoveAt(0);
            p.DesertedTotal++;
            AddNews(MoraleNews.Desertion);
            DwellerDeserted?.Invoke(d);
            return d;
        }

        /// <summary>The price of calling a deserter back: what hiring one of that rarity in the tavern costs.</summary>
        public ResourceBag RecallCost(Deserter x) => HireCost(new Recruit { Rarity = x?.Rarity ?? Rarity.Common });

        /// <summary>Calls a deserter back from the fog: the spirit must be calm again and the fortress must have room.</summary>
        public Fail Recall(int index, long now, out Dweller back)
        {
            back = null;
            Advance(now);
            var p = People;
            if (index < 0 || index >= p.Deserters.Count)
                return Fail.NotFound;
            if (Morale < MoraleNumbers.RecallMorale)
                return Fail.RulesNotMet;
            if (PopulationFull())
                return Fail.LimitReached;
            var x = p.Deserters[index];
            var cost = RecallCost(x);
            if (!State.Wallet.Covers(cost))
                return Fail.NotEnough;
            State.Wallet.Subtract(cost);
            p.Deserters.RemoveAt(index);
            back = x.Dweller;
            back.RoomId = -1;
            back.RouteId = 0;
            EnsureDweller(back);
            State.Dwellers.Add(back);
            if (x.Rarity != Rarity.Common)
                Halls.Rarity[back.Id] = x.Rarity;
            p.Recalled++;
            return Fail.None;
        }
    }
}

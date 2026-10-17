using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core.Combat;

namespace LastHearth.Core
{
    public sealed class PvpReward
    {
        public bool Won;
        public int Stars;
        public int RatingChange;
        public ResourceBag Got = new ResourceBag();
        /// <summary>Raid loot (silver and iron), already in the warehouse or the rewards.</summary>
        public long Materials;
    }

    /// <summary>A hero of a ghost's squad, as its card shows it.</summary>
    public sealed class PvpHero
    {
        public HeroClass Class;
        public int Level;
    }

    /// <summary>One line of the arena and raid journal: our fights and the attacks on us.</summary>
    [Serializable]
    public sealed class PvpRecord
    {
        public long Time;
        public bool Raid;
        /// <summary>They attacked us (the arena defense or the fortress); false: our own fight.</summary>
        public bool Attacked;
        /// <summary>Our side won: our attack, or our defense held.</summary>
        public bool Won;
        public int RatingChange;
        public long Glory;
        public int Stars;
        public bool Revenge;
        public PvpOpponent Opponent = new PvpOpponent();
        /// <summary>The revenge for this attack was taken (or started).</summary>
        public bool Avenged;
    }

    /// <summary>The forge's traps of the raid defense (MODES.md, 3).</summary>
    public enum TrapKind
    {
        None,
        Snare,
        Embers,
        IceGrate,
    }

    /// <summary>One line of the fortress a raid storms: its trap and up to two heroes on guard.</summary>
    [Serializable]
    public sealed class RaidLine
    {
        public TrapKind Trap;
        public List<int> Defenders = new List<int>();
    }

    /// <summary>
    /// MODES.md, 2-3: the arena (squad against another hearth's defense) and raids (a storm of another
    /// fortress, floor by floor). Until the server exists both are played against ghosts of the same
    /// hearth level; nobody loses resources to PvP.
    /// </summary>
    public sealed partial class GameSession
    {
        static readonly string[] GhostNames =
        {
            "Ворон", "Седой Ульф", "Ольха", "Брам", "Тёмная Иса", "Корвин", "Мирослава", "Хват", "Яр", "Белая Нора",
            "Гест", "Рогволд", "Сиверка", "Тур", "Ингвар", "Дарена", "Кремень", "Лют",
        };

        /// <summary>The floors of a fortress, as the battle engine sees them: traps, defenders, the hearth guard.</summary>
        static readonly MonsterFamily[] RaidFloors = { MonsterFamily.Root, MonsterFamily.Knight, MonsterFamily.Possessed };
        public static readonly string[] RaidStarNames = { "ловушки разрушены", "защитники побеждены", "дошли до очага" };

        PvpData Pvp => Balance.Activities.Pvp;

        public bool ArenaOpen => State.HearthLevel >= Pvp.ArenaUnlock;

        public bool RaidsOpen => State.HearthLevel >= Pvp.RaidUnlock;

        bool BattleBusy => State.EventBattle != null || State.Hunt != null || State.PendingMission >= 0 || State.PvpBattle != null;

        // ------------------------------------------------------------------ tickets and flags

        static int Regen(ref int stock, ref long at, int max, long seconds, long now)
        {
            while (stock < max && at > 0 && now >= at)
            {
                stock++;
                at = stock < max ? at + seconds : 0;
            }
            return stock;
        }

        static void Spend(ref int stock, ref long at, int max, long seconds, long now)
        {
            if (stock >= max)
                at = now + seconds;
            stock--;
        }

        void EnsurePvp()
        {
            if (State.ArenaRating == 0)
                State.ArenaRating = Pvp.StartRating;
            if (State.ArenaTickets < 0)
                State.ArenaTickets = Pvp.Tickets;
            if (State.RaidFlags < 0)
                State.RaidFlags = Pvp.Flags;
        }

        public int ArenaTickets(long now)
        {
            EnsurePvp();
            return Regen(ref State.ArenaTickets, ref State.ArenaTicketAt, Pvp.Tickets, Pvp.TicketHours * 3600L, now);
        }

        public int RaidFlags(long now)
        {
            EnsurePvp();
            return Regen(ref State.RaidFlags, ref State.RaidFlagAt, Pvp.Flags, Pvp.FlagHours * 3600L, now);
        }

        public long ArenaTicketPrice(long now)
        {
            if (State.ArenaBuyDay != LocalDay(now))
            {
                State.ArenaBuyDay = LocalDay(now);
                State.ArenaBought = 0;
            }
            var prices = Balance.Activities.Shop.ArenaTicket;
            return State.ArenaBought < prices.Length ? prices[State.ArenaBought] : -1;
        }

        /// <summary>An extra ticket for 20, 40, then 60 Sparks; three a day.</summary>
        public Fail BuyArenaTicket(long now)
        {
            if (!ArenaOpen)
                return Fail.Locked;
            long price = ArenaTicketPrice(now);
            if (price < 0)
                return Fail.LimitReached;
            if (State.Wallet[ResourceType.Sparks] < price)
                return Fail.NotEnough;
            State.Wallet[ResourceType.Sparks] -= price;
            State.ArenaBought++;
            ArenaTickets(now);
            State.ArenaTickets++;
            return Fail.None;
        }

        // ------------------------------------------------------------------ leagues

        /// <summary>When the current arena week ends (the league pays): the next Sunday, 22:00 local.</summary>
        public long ArenaWeekEnds(long now) => (ArenaWeekKey(now) + 1) * 7 * Day + 3 * Day + 22 * 3600 - State.UtcOffsetSeconds;

        /// <summary>The league above a rating, or null at the top.</summary>
        public LeagueData NextLeague(int rating)
        {
            int i = LeagueIndex(rating) + 1;
            return i < Pvp.Leagues.Count ? Pvp.Leagues[i] : null;
        }

        /// <summary>The arena week ends on Sunday at 22:00 local time.</summary>
        public long ArenaWeekKey(long now)
        {
            long local = now + State.UtcOffsetSeconds - (3 * Day + 22 * 3600);
            return local >= 0 ? local / (7 * Day) : (local - 7 * Day + 1) / (7 * Day);
        }

        public int LeagueIndex(int rating)
        {
            int league = 0;
            for (int i = 0; i < Pvp.Leagues.Count; i++)
            {
                if (rating >= Pvp.Leagues[i].Rating)
                    league = i;
            }
            return league;
        }

        public LeagueData League => Pvp.Leagues[LeagueIndex(State.ArenaRating == 0 ? Pvp.StartRating : State.ArenaRating)];

        /// <summary>Called by Advance: the weekly league reward, ghost attacks on the defense, ghost raids.</summary>
        void SettlePvp(long now)
        {
            if (!ArenaOpen)
                return;
            EnsurePvp();
            long week = ArenaWeekKey(now);
            if (State.ArenaWeek < 0)
                State.ArenaWeek = week;
            if (week != State.ArenaWeek)
            {
                if (State.ArenaWeekFights > 0)
                    PayLeague();
                State.ArenaWeekFights = 0;
                // a season is 6 weeks: then the rating is pulled halfway back to the start
                if (week / 6 != State.ArenaWeek / 6)
                    State.ArenaRating = Pvp.StartRating + (State.ArenaRating - Pvp.StartRating) / 2;
                State.ArenaWeek = week;
            }
            long day = LocalDay(now);
            if (State.DefenseDay < 0)
                State.DefenseDay = day;
            for (long d = Math.Max(State.DefenseDay + 1, day - 2); d <= day; d++)
                DefendArena(d, now);
            State.DefenseDay = day;
            if (RaidsOpen)
            {
                if (State.GhostRaidDay < 0)
                    State.GhostRaidDay = day;
                if (State.GhostRaidDay < day)
                {
                    State.GhostRaidDay = day;
                    GhostRaid(now);
                }
                State.Revenge.RemoveAll(r => r.Until <= now);
            }
        }

        void PayLeague()
        {
            var league = League;
            State.Wallet[ResourceType.Sparks] += league.Sparks;
            State.Wallet[ResourceType.Glory] += league.Glory;
            State.Wallet[ResourceType.Essence] += (long)Math.Round(Balance.Activities.EssenceByLevel[State.HearthLevel - 1] * league.EssenceDays);
            PvpNote(string.Format("Итоги недели: лига «{0}», +{1} искр, +{2} славы", league.Name, league.Sparks, league.Glory));
        }

        /// <summary>Two ghosts attack the defense each day; a lost defense costs half a lost attack, never resources.</summary>
        void DefendArena(long day, long now)
        {
            var rng = new Rng((int)(day % 1000003) * 31 + State.ArenaRating);
            double hold = ArenaDefenseHold();
            for (int i = 0; i < 2; i++)
            {
                bool held = rng.Next() < hold;
                int change = held ? 4 : -8;
                State.ArenaRating = Math.Max(0, State.ArenaRating + change);
                string name = GhostNames[(int)(rng.Next() * GhostNames.Length)];
                PvpNote(string.Format("{0} напал на вашу оборону: {1} ({2:+0;-0})", name, held ? "отбились" : "оборона пала", change));
                // the attacker stays in the journal: a revenge on the arena for a day
                var attacker = Ghost(new Rng((int)(day % 1000003) * 7 + 3 + i), 0.92 + 0.08 * i, true);
                attacker.Name = name;
                long at = Math.Min(now, day * Day - State.UtcOffsetSeconds + (i == 0 ? 10 : 18) * 3600L);
                attacker.Until = at + Pvp.RevengeHours * 3600L;
                Record(new PvpRecord { Time = at, Attacked = true, Won = held, RatingChange = change, Opponent = attacker });
            }
        }

        void GhostRaid(long now)
        {
            var g = Ghost(new Rng(State.NextBattleSeed++), 0.9 + 0.2 * (State.NextBattleSeed % 5) / 4.0);
            g.Until = now + Pvp.RevengeHours * 3600L;
            State.Revenge.Add(g);
            // the ghost storms the lines one by one and stops at the first that holds
            var rng = new Rng(g.Seed + 101);
            int stars = 0;
            while (stars < RaidLines && rng.Next() >= RaidHold(stars))
                stars++;
            long glory = RaidDenyGlory * (RaidLines - stars);
            State.Wallet[ResourceType.Glory] += glory;
            PvpNote(string.Format("{0} совершил набег на крепость: {1} зв.{2}. Вы ничего не потеряли; месть доступна сутки", g.Name, stars,
                glory > 0 ? ", оборона дала " + glory + " славы" : ""));
            Record(new PvpRecord { Time = now, Raid = true, Attacked = true, Won = stars < RaidLines, Stars = stars, Glory = glory, Opponent = g });
        }

        void PvpNote(string text)
        {
            State.PvpLog.Add(text);
            if (State.PvpLog.Count > 30)
                State.PvpLog.RemoveAt(0);
        }

        void Record(PvpRecord r)
        {
            State.PvpJournal.Add(r);
            if (State.PvpJournal.Count > JournalSize)
                State.PvpJournal.RemoveAt(0);
        }

        // ------------------------------------------------------------------ the journal and revenge

        const int JournalSize = 20;

        /// <summary>An attack on us that can still be avenged: on the arena for a ticket, on the fortress for free (raids).</summary>
        public bool CanAvenge(PvpRecord r, long now)
        {
            if (!r.Attacked || r.Avenged || now >= r.Opponent.Until)
                return false;
            return r.Raid ? RaidsOpen && State.Revenge.Any(g => g.Seed == r.Opponent.Seed) : ArenaOpen;
        }

        /// <summary>The revenge for a journal record (an index of State.PvpJournal).</summary>
        public Fail StartRevenge(int record, long now, out Battle battle)
        {
            battle = null;
            Advance(now);
            if (record < 0 || record >= State.PvpJournal.Count)
                return Fail.NotFound;
            var r = State.PvpJournal[record];
            if (!CanAvenge(r, now))
                return Fail.NotFound;
            Fail fail;
            if (r.Raid)
            {
                fail = StartRaid(State.Revenge.FindIndex(g => g.Seed == r.Opponent.Seed), true, now, out battle);
            }
            else
            {
                fail = ArenaReady(now);
                if (fail == Fail.None)
                {
                    Spend(ref State.ArenaTickets, ref State.ArenaTicketAt, Pvp.Tickets, Pvp.TicketHours * 3600L, now);
                    State.PvpBattle = new PvpBattle { Revenge = true, Opponent = r.Opponent, Seed = State.NextBattleSeed++ };
                    battle = new Battle(Combat, PvpSetup(State.PvpBattle));
                }
            }
            if (fail == Fail.None)
                r.Avenged = true;
            return fail;
        }

        // ------------------------------------------------------------------ power, squads, the arena defense

        /// <summary>A hero's might as the cards show it: damage per second and health of the battle preview.</summary>
        public double HeroPower(Dweller d)
        {
            var f = d == null ? null : HeroPreview(d.Id);
            if (f == null)
                return d == null || !d.IsHero ? 0 : 10 * d.HeroLevel;
            double seconds = Math.Max(1, f.Interval) * (Combat?.TickSeconds ?? 0.1);
            return Math.Round(f.Attack / seconds * 4 + f.MaxHp / 2);
        }

        /// <summary>The squad that attacks (the strongest heroes), its might summed.</summary>
        public double SquadPower() => Squad().Sum(HeroPower);

        /// <summary>The arena defense in its order; empty choice: the five strongest.</summary>
        public List<Dweller> ArenaDefenders()
        {
            var chosen = State.ArenaDefense.Select(FindDweller).Where(d => d != null && d.IsHero).ToList();
            return chosen.Count > 0 ? chosen : Squad().Take(5).ToList();
        }

        public double DefensePower() => ArenaDefenders().Sum(HeroPower);

        /// <summary>How often the defense holds against a ghost: 45% without a choice, up to 55% for the five strongest.</summary>
        public double ArenaDefenseHold()
        {
            double best = Squad().Take(5).Sum(HeroPower);
            double share = State.ArenaDefense.Count == 0 || best <= 0 ? 0 : Math.Min(1, DefensePower() / best);
            return 0.45 + 0.10 * share;
        }

        /// <summary>
        /// The heroes of a ghost's defense for its card: as many as a squad of its hearth level holds, at the hero level of
        /// that hearth (2L - 2, ±2), a front of a Guardian or a Swordsman, never more than two of a class. They come from the
        /// opponent's seed, so the card and the fight stay the same until the list changes.
        /// </summary>
        public List<PvpHero> OpponentSquad(PvpOpponent o)
        {
            var rng = new Rng(o.Seed * 7 + 3);
            int size = 5 + Balance.SquadSlots.Count(gate => o.HearthLevel >= gate);
            var classes = Enum.GetValues(typeof(HeroClass)).Cast<HeroClass>().Where(c => c != HeroClass.None).ToList();
            var squad = new List<PvpHero>();
            while (squad.Count < size)
            {
                var c = squad.Count == 0 ? (rng.Next() < 0.6 ? HeroClass.Guardian : HeroClass.Swordsman) : classes[(int)(rng.Next() * classes.Count)];
                if (squad.Count(h => h.Class == c) >= 2)
                    continue;
                int level = Math.Max(1, 2 * o.HearthLevel - 2 + (int)(rng.Next() * 5) - 2);
                squad.Add(new PvpHero { Class = c, Level = level });
            }
            return squad;
        }

        // ------------------------------------------------------------------ opponents

        /// <summary>
        /// Higher in the ladder the defenses are better played: +10% power per 200 rating above 1200.
        /// A careful player settles in Gold, the auto-battle in Bronze, as a real ladder would sort them.
        /// </summary>
        public double RatingPressure(int rating) => Math.Max(0.85, 1 + (rating - 1200) / 2000.0);

        PvpOpponent Ghost(Rng rng, double power, bool arena = false)
        {
            int L = State.HearthLevel;
            int rating = State.ArenaRating == 0 ? Pvp.StartRating : State.ArenaRating;
            if (arena)
                power *= RatingPressure(rating);
            double r = rng.Next();
            int level = Math.Max(1, Math.Min(Balance.MaxHearthLevel, L + (r < 0.25 ? -1 : r > 0.75 ? 1 : 0)));
            return new PvpOpponent
            {
                Name = GhostNames[(int)(rng.Next() * GhostNames.Length)],
                HearthLevel = level,
                Power = Math.Round(power, 3),
                Rating = Math.Max(0, rating + (int)((power / (arena ? RatingPressure(rating) : 1) - 1) * 400) + (int)(rng.Next() * 60) - 30),
                Seed = (int)(rng.Next() * 1000000),
            };
        }

        List<PvpOpponent> ThreeGhosts(bool arena)
        {
            var rng = new Rng(State.NextBattleSeed++);
            return new[] { 0.86, 0.96, 1.06 }.Select(p => Ghost(rng, p + 0.08 * rng.Next(), arena)).ToList();
        }

        /// <summary>Three defenses to choose from: weaker, equal, stronger (power ±10%).</summary>
        public List<PvpOpponent> ArenaOpponents(long now)
        {
            EnsurePvp();
            if (State.ArenaList.Count == 0)
                State.ArenaList = ThreeGhosts(true);
            return State.ArenaList;
        }

        /// <summary>A new list for free once in 10 minutes.</summary>
        public Fail RerollArena(long now)
        {
            if (now < State.ArenaListAt)
                return Fail.NotToday;
            State.ArenaList = ThreeGhosts(true);
            State.ArenaListAt = now + 600;
            return Fail.None;
        }

        public List<PvpOpponent> RaidTargets(long now)
        {
            EnsurePvp();
            if (State.RaidList.Count == 0)
                State.RaidList = ThreeGhosts(false);
            return State.RaidList;
        }

        /// <summary>The arena defense: up to five heroes; empty means the strongest squad.</summary>
        public Fail SetArenaDefense(IList<int> heroes)
        {
            if (heroes.Count > 5 || heroes.Any(id => FindDweller(id) == null || !FindDweller(id).IsHero))
                return Fail.NotAHero;
            State.ArenaDefense = heroes.Distinct().ToList();
            return Fail.None;
        }

        // ------------------------------------------------------------------ the raid defense of the fortress

        /// <summary>The lines a raid storms, top down: a star for each line taken (RaidStarNames).</summary>
        public const int RaidLines = 3;
        public static readonly string[] RaidLineNames = { "Верхние залы", "Казарма", "Очаг" };
        public const int RaidLineDefenders = 2;
        /// <summary>Glory (the raids' trophies) for every line a ghost raid did not take.</summary>
        public const long RaidDenyGlory = 2;

        public static readonly string[] TrapNames = { "Без ловушки", "Капкан", "Горящие угли", "Ледяная решётка" };
        /// <summary>The hearth level that opens each trap of the forge, and how much it adds to its line's hold.</summary>
        public static readonly int[] TrapUnlock = { 0, 17, 20, 23 };
        static readonly double[] TrapHoldBase = { 0, 0.12, 0.15, 0.18 };

        /// <summary>MODES.md, 5: late fortifications paid with the raids' trophies (Glory) or the Siege's outpost stone.</summary>
        public static readonly string[] FortificationNames = { "Стены", "Сторожевые огни" };
        public const int FortificationUnlock = 20;
        public const int FortificationMax = 5;

        public List<RaidLine> RaidDefense
        {
            get
            {
                while (State.RaidDefense.Count < RaidLines)
                    State.RaidDefense.Add(new RaidLine());
                return State.RaidDefense;
            }
        }

        int[] Forts
        {
            get
            {
                if (State.Fortifications == null || State.Fortifications.Length < FortificationNames.Length)
                    Array.Resize(ref State.Fortifications, FortificationNames.Length);
                return State.Fortifications;
            }
        }

        public int FortificationLevel(int kind) => kind >= 0 && kind < FortificationNames.Length ? Forts[kind] : 0;

        /// <summary>Traps come from the forge: its highest level makes them stronger (+5% a level above the first).</summary>
        public int ForgeLevel() => State.Rooms.Where(r => r.Type == RoomType.Forge).Select(r => r.Level).DefaultIfEmpty(0).Max();

        public bool TrapOpen(TrapKind trap) => trap == TrapKind.None || ForgeLevel() > 0 && State.HearthLevel >= TrapUnlock[(int)trap];

        public double TrapHold(TrapKind trap) => trap == TrapKind.None ? 0 : TrapHoldBase[(int)trap] * (1 + 0.05 * Math.Max(0, ForgeLevel() - 1));

        /// <summary>
        /// How likely a line stops a ghost raid: 15%, the trap, 12% for each defender (by his might against an average hero
        /// of the squad, at most 1.5 times), the walls 3% a level on every line, the watch fires 6% a level at the Hearth; at most 85%.
        /// </summary>
        public double RaidHold(int line)
        {
            if (line < 0 || line >= RaidLines)
                return 0;
            var l = RaidDefense[line];
            var squad = Squad();
            double average = squad.Count == 0 ? 1 : Math.Max(1, squad.Average(HeroPower));
            double hold = 0.15 + TrapHold(l.Trap);
            foreach (var d in l.Defenders.Select(FindDweller).Where(d => d != null && d.IsHero))
                hold += 0.12 * Math.Min(1.5, HeroPower(d) / average);
            hold += 0.03 * Forts[0];
            if (line == RaidLines - 1)
                hold += 0.06 * Forts[1];
            hold *= 1 + SpecSum(SpecEffect.RaidDefense); // «Охрана» of the warehouse and the like (GameSession.Specs.cs)
            return Math.Min(0.85, hold);
        }

        public Fail SetRaidTrap(int line, TrapKind trap)
        {
            if (!RaidsOpen)
                return Fail.Locked;
            if (line < 0 || line >= RaidLines)
                return Fail.NotFound;
            if (!TrapOpen(trap))
                return Fail.Locked;
            RaidDefense[line].Trap = trap;
            return Fail.None;
        }

        /// <summary>Up to two heroes guard a line, five in the whole fortress; a hero put on a line leaves the others.</summary>
        public Fail SetRaidDefenders(int line, IList<int> heroes)
        {
            if (!RaidsOpen)
                return Fail.Locked;
            if (line < 0 || line >= RaidLines)
                return Fail.NotFound;
            var ids = heroes.Distinct().ToList();
            if (ids.Any(id => FindDweller(id) == null || !FindDweller(id).IsHero))
                return Fail.NotAHero;
            int elsewhere = RaidDefense.Where((l, i) => i != line).Sum(l => l.Defenders.Count(id => !ids.Contains(id)));
            if (ids.Count > RaidLineDefenders || elsewhere + ids.Count > 5)
                return Fail.LimitReached;
            for (int i = 0; i < RaidLines; i++)
                RaidDefense[i].Defenders.RemoveAll(ids.Contains);
            RaidDefense[line].Defenders = ids;
            return Fail.None;
        }

        /// <summary>The next level of a fortification: 150 Glory a level, or 4 outpost stone a level while the Siege runs.</summary>
        public long FortificationCost(int kind, bool stone) => (stone ? 4 : 150) * (FortificationLevel(kind) + 1);

        public Fail UpgradeFortification(int kind, bool stone, long now)
        {
            Advance(now);
            if (!RaidsOpen || State.HearthLevel < FortificationUnlock)
                return Fail.Locked;
            if (kind < 0 || kind >= FortificationNames.Length)
                return Fail.NotFound;
            if (Forts[kind] >= FortificationMax)
                return Fail.LimitReached;
            long cost = FortificationCost(kind, stone);
            if (stone)
            {
                var run = ActiveRun("Siege", now);
                if (run == null)
                    return Fail.NotRunning;
                if (run.Stone < cost)
                    return Fail.NotEnough;
                run.Stone -= cost;
            }
            else
            {
                if (State.Wallet[ResourceType.Glory] < cost)
                    return Fail.NotEnough;
                State.Wallet[ResourceType.Glory] -= cost;
            }
            Forts[kind]++;
            return Fail.None;
        }

        // ------------------------------------------------------------------ battles

        public BattleSetup PvpSetup(PvpBattle pb)
        {
            var setup = SquadSetup(pb.Seed);
            var kind = Combat.MissionKinds[pb.Raid ? "Raid" : "Arena"];
            var o = pb.Opponent;
            // every defense is different: its lines (families) and whether it is tanky or hard-hitting
            var rng = new Rng(o.Seed + 17);
            for (int w = 0; w < kind.Waves; w++)
            {
                var family = pb.Raid ? RaidFloors[w % RaidFloors.Length] : (MonsterFamily)(int)(rng.Next() * 4);
                double tilt = 0.8 + 0.4 * rng.Next();
                setup.Waves.Add(new WaveSpec
                {
                    Family = family,
                    Rank = o.HearthLevel,
                    HpMult = kind.HpMult * o.Power * tilt,
                    AttackMult = kind.AttackMult * o.Power / tilt,
                    // traps do not telegraph, the defenders and the hearth guard do
                    Telegraphs = kind.Telegraphs && !(pb.Raid && w == 0),
                    Mirror = true,
                });
            }
            return setup;
        }

        Fail ArenaReady(long now)
        {
            if (!ArenaOpen)
                return Fail.Locked;
            if (BattleBusy)
                return Fail.HuntInProgress;
            if (Squad().Count == 0)
                return Fail.NoHeroes;
            return ArenaTickets(now) <= 0 ? Fail.NoTracks : Fail.None;
        }

        public Fail StartArena(int index, long now, out Battle battle)
        {
            battle = null;
            Advance(now);
            var fail = ArenaReady(now);
            if (fail != Fail.None && fail != Fail.NoTracks)
                return fail;
            var list = ArenaOpponents(now);
            if (index < 0 || index >= list.Count)
                return Fail.NotFound;
            if (fail != Fail.None)
                return fail;
            Spend(ref State.ArenaTickets, ref State.ArenaTicketAt, Pvp.Tickets, Pvp.TicketHours * 3600L, now);
            State.PvpBattle = new PvpBattle { Opponent = list[index], Seed = State.NextBattleSeed++ };
            battle = new Battle(Combat, PvpSetup(State.PvpBattle));
            return Fail.None;
        }

        /// <summary>A raid on one of three fortresses for a flag, or a free revenge raid on an attacker.</summary>
        public Fail StartRaid(int index, bool revenge, long now, out Battle battle)
        {
            battle = null;
            Advance(now);
            if (!RaidsOpen)
                return Fail.Locked;
            if (BattleBusy)
                return Fail.HuntInProgress;
            if (Squad().Count == 0)
                return Fail.NoHeroes;
            var list = revenge ? State.Revenge : RaidTargets(now);
            if (index < 0 || index >= list.Count)
                return Fail.NotFound;
            if (!revenge)
            {
                if (RaidFlags(now) <= 0)
                    return Fail.NoTracks;
                Spend(ref State.RaidFlags, ref State.RaidFlagAt, Pvp.Flags, Pvp.FlagHours * 3600L, now);
            }
            State.PvpBattle = new PvpBattle { Raid = true, Revenge = revenge, Opponent = list[index], Seed = State.NextBattleSeed++ };
            if (revenge)
            {
                foreach (var r in State.PvpJournal.Where(r => r.Raid && r.Attacked && r.Opponent.Seed == list[index].Seed))
                    r.Avenged = true;
            }
            list.RemoveAt(index);
            battle = new Battle(Combat, PvpSetup(State.PvpBattle));
            return Fail.None;
        }

        public Fail FinishPvp(Battle battle, long now, out PvpReward reward)
        {
            reward = new PvpReward();
            var pb = State.PvpBattle;
            if (pb == null)
                return Fail.NoHunt;
            if (!battle.Over)
                battle.RunToEnd();
            State.PvpBattle = null;
            reward.Won = battle.Won;
            if (pb.Raid)
                FinishRaid(pb, battle, reward, now);
            else
                FinishArena(pb, battle, reward, now);
            State.Wallet.Add(reward.Got);
            return Fail.None;
        }

        /// <summary>What a win or a loss against this opponent does to the rating: +15..+30, -10..-20 by the rating gap.</summary>
        public int ArenaRatingChange(PvpOpponent o, bool won)
        {
            EnsurePvp();
            int diff = o.Rating - State.ArenaRating;
            return won ? Math.Max(15, Math.Min(30, 22 + diff / 20)) : -Math.Max(10, Math.Min(20, 15 - diff / 40));
        }

        void FinishArena(PvpBattle pb, Battle battle, PvpReward reward, long now)
        {
            reward.Stars = battle.Won ? battle.Stars : 0;
            reward.RatingChange = ArenaRatingChange(pb.Opponent, battle.Won);
            State.ArenaRating = Math.Max(0, State.ArenaRating + reward.RatingChange);
            State.ArenaWeekFights++;
            long essenceDay = Balance.Activities.EssenceByLevel[State.HearthLevel - 1];
            reward.Got[ResourceType.Glory] = battle.Won ? Pvp.WinGlory : Pvp.LossGlory;
            reward.Got[ResourceType.Essence] = (long)Math.Round(essenceDay * Pvp.WinEssence * (battle.Won ? 1 : 1.0 / 3));
            if (battle.Won)
                State.ArenaWins++;
            if (!pb.Revenge)
                State.ArenaList.Clear();
            PvpNote(string.Format("{0} {1}: {2} ({3:+0;-0})", pb.Revenge ? "Месть на арене" : "Арена против", pb.Opponent.Name,
                battle.Won ? "победа" : "поражение", reward.RatingChange));
            Record(new PvpRecord
            {
                Time = now, Won = battle.Won, RatingChange = reward.RatingChange, Glory = reward.Got[ResourceType.Glory],
                Stars = reward.Stars, Revenge = pb.Revenge, Opponent = pb.Opponent,
            });
        }

        void FinishRaid(PvpBattle pb, Battle battle, PvpReward reward, long now)
        {
            // a star per floor taken: traps, defenders, the hearth
            reward.Stars = battle.Won ? 3 : Math.Min(2, battle.Wave);
            double loot = Pvp.LootHours * reward.Stars / 3.0 * (pb.Revenge ? 2 : 1);
            var materials = ResourceBag.Materials((long)Math.Round(IncomePerHour() * loot), Balance.SilverShare);
            State.Rewards.Add(materials);
            ClaimRewards();
            reward.Materials = materials[ResourceType.Silver] + materials[ResourceType.Iron];
            if (reward.Stars == 3)
            {
                reward.Got[ResourceType.Glory] = Pvp.TrophyGlory;
                State.RaidWins++;
            }
            PvpNote(string.Format("{0} на крепость {1}: {2} зв., добыча {3}", pb.Revenge ? "Месть" : "Набег", pb.Opponent.Name,
                reward.Stars, reward.Materials));
            Record(new PvpRecord
            {
                Time = now, Raid = true, Won = battle.Won, Glory = reward.Got[ResourceType.Glory], Stars = reward.Stars,
                Revenge = pb.Revenge, Opponent = pb.Opponent,
            });
        }

        /// <summary>A fight left open when the game closed is finished by auto-battle.</summary>
        public string ResolvePendingPvp(long now)
        {
            if (State.PvpBattle == null)
                return null;
            var battle = new Battle(Combat, PvpSetup(State.PvpBattle)) { Brain = Brains.AutoBattle() };
            FinishPvp(battle, now, out var reward);
            return State.PvpLog.LastOrDefault();
        }

        // ------------------------------------------------------------------ Glory shop

        /// <summary>When the Glory shop's weekly limits come back.</summary>
        public long GloryShopResets(long now) => (LocalDay(now) / 7 + 1) * 7 * Day - State.UtcOffsetSeconds;

        public int GloryLeft(int goods, long now)
        {
            long week = LocalDay(now) / 7;
            if (State.GloryWeek != week)
            {
                State.GloryWeek = week;
                State.GloryBought = new int[Pvp.GloryShop.Count];
            }
            if (State.GloryBought.Length < Pvp.GloryShop.Count)
                Array.Resize(ref State.GloryBought, Pvp.GloryShop.Count);
            return Pvp.GloryShop[goods].PerWeek - State.GloryBought[goods];
        }

        /// <summary>MODES.md, 4.2: essence (a quarter of a day), an hour of hourglass, a rare dweller's seal.</summary>
        public Fail BuyGlory(int goods, long now)
        {
            Advance(now);
            if (goods < 0 || goods >= Pvp.GloryShop.Count)
                return Fail.NotFound;
            var g = Pvp.GloryShop[goods];
            if (GloryLeft(goods, now) <= 0)
                return Fail.LimitReached;
            if (State.Wallet[ResourceType.Glory] < g.Price)
                return Fail.NotEnough;
            if (g.Goods == "Seal" && PopulationFull())
                return Fail.LimitReached;
            GearItem gear = null;
            if (g.Goods == "Gear")
            {
                // MODES.md 4.2: an epic piece for the weakest slot of the squad, shown before buying
                gear = GloryGearPreview(now);
                if (gear == null)
                    return Fail.NoHeroes;
                if (BagFull())
                    return Fail.InventoryFull;
            }
            State.Wallet[ResourceType.Glory] -= g.Price;
            State.GloryBought[goods]++;
            switch (g.Goods)
            {
                case "Essence":
                    State.Wallet[ResourceType.Essence] += (long)Math.Round(Balance.Activities.EssenceByLevel[State.HearthLevel - 1] * g.Amount);
                    break;
                case "Hourglass":
                    State.HourglassSeconds += (long)Math.Round(g.Amount * 60);
                    break;
                case "Gear":
                    Receive(gear, "Лавка славы");
                    break;
                default:
                    RefugeesArrive(1);
                    var d = State.Dwellers.Last();
                    for (int k = 0; k < d.Stats.Length; k++)
                        d.Stats[k] += 3; // a rare dweller: better in everything
                    break;
            }
            return Fail.None;
        }
    }
}

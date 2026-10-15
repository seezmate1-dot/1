using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core.Combat;
using LastHearth.Core.Surge;

namespace LastHearth.Core
{
    /// <summary>EVENTS.md 4-10: the mechanics of the seven live events.</summary>
    public sealed partial class GameSession
    {
        // ================================================================== battles of events

        int EventDayIndex(EventRun run, long now) => (int)((now - run.Start) / Day);

        double BossUnitHp(MonsterFamily family, int rank)
        {
            var b = Combat.Boss;
            return b.Hp * Combat.Family(family).Hp * Math.Pow(b.HpGrowth, rank - 1);
        }

        MonsterFamily WeeklyFamily(long now) => (MonsterFamily)((LocalDay(now) / 7) % 4);

        /// <summary>Attacks of the Long Night today: 3, +1 after today's Surge, +1 bought.</summary>
        public int NightAttacksLeft(EventRun run, long now)
        {
            NewDayFor(run, now);
            int bonus = State.SurgeDay == LocalDay(now) ? 1 : 0;
            return 3 + bonus + run.Extra - run.Attempts;
        }

        public int TournamentAttemptsLeft(EventRun run, long now)
        {
            NewDayFor(run, now);
            return 5 + run.Extra - run.Attempts;
        }

        /// <summary>Tournament rule of the day: 0 Steel (melee +30%, magic -50%), 1 Cold (healing -50%), 2 Knights.</summary>
        public int TournamentRule(EventRun run, long now) => EventDayIndex(run, now) % 3;

        public int PortalFloorsOpen(EventRun run, long now)
        {
            int day = EventDayIndex(run, now);
            return run.Personal || day >= 3 ? int.MaxValue : 10 * (day + 1);
        }

        public Fail BuyEventExtra(string id, long now)
        {
            var run = ActiveRun(id, now);
            if (run == null)
                return Fail.Locked;
            NewDayFor(run, now);
            long price;
            if (id == "LongNight" && run.Extra < 1)
                price = 30;
            else if (id == "Tournament" && run.Extra < 2)
                price = 20;
            else
                return Fail.LimitReached;
            if (State.Wallet[ResourceType.Sparks] < price)
                return Fail.NotEnough;
            State.Wallet[ResourceType.Sparks] -= price;
            run.Extra++;
            return Fail.None;
        }

        public Fail StartEventBattle(string id, long now, out Battle battle, int cell = -1)
        {
            battle = null;
            Advance(now);
            var run = ActiveRun(id, now);
            if (run == null)
                return Fail.Locked;
            if (State.EventBattle != null || State.Hunt != null || State.PendingMission >= 0)
                return Fail.HuntInProgress;
            if (Squad().Count == 0)
                return Fail.NoHeroes;
            NewDayFor(run, now);
            switch (id)
            {
                case "LongNight":
                    if (NightAttacksLeft(run, now) <= 0)
                        return Fail.NoTracks;
                    break;
                case "Tournament":
                    if (TournamentAttemptsLeft(run, now) <= 0)
                        return Fail.NoTracks;
                    break;
                case "Portal":
                    if (run.BlessingOffer.Count > 0)
                        return Fail.RulesNotMet;
                    if (!run.RunAlive)
                    {
                        if (run.FreeRunDay == LocalDay(now))
                            return Fail.NoTracks;
                        run.FreeRunDay = LocalDay(now);
                        run.RunAlive = true;
                        run.Floor = run.Checkpoint;
                        run.Blessings.Clear();
                        run.HpIds.Clear();
                        run.HpShares.Clear();
                        run.Revives = 0;
                    }
                    if (run.Floor >= PortalFloorsOpen(run, now))
                        return Fail.Locked;
                    break;
                case "Siege":
                    if (cell < 0 || cell >= run.Outposts.Count || !run.Outposts[cell].Fallen)
                        return Fail.NotFound;
                    break;
                case "Treasure":
                    if (cell < 0)
                        return Fail.NotFound;
                    break;
                default:
                    return Fail.NotFound;
            }
            State.EventBattle = new EventBattle { EventId = id, Seed = State.NextBattleSeed++, Cell = cell };
            battle = new Battle(Combat, EventBattleSetup(run, State.EventBattle, now));
            return Fail.None;
        }

        BattleSetup EventBattleSetup(EventRun run, EventBattle eb, long now)
        {
            var setup = SquadSetup(eb.Seed);
            int L = State.HearthLevel;
            switch (run.Id)
            {
                case "LongNight":
                    setup.Waves.Add(new WaveSpec { Family = WeeklyFamily(now), Rank = L, HpMult = 40, Telegraphs = true });
                    break;
                case "Tournament":
                    int rule = TournamentRule(run, now);
                    var family = rule == 2 ? MonsterFamily.Knight : WeeklyFamily(now);
                    setup.Waves.Add(new WaveSpec { Family = family, Rank = L, HpMult = 1.3, Telegraphs = true });
                    if (rule == 0)
                    {
                        foreach (var c in new[] { HeroClass.Guardian, HeroClass.Swordsman, HeroClass.Cleaver })
                            setup.ClassAttack[c] = 1.3;
                        foreach (var c in new[] { HeroClass.AshMage, HeroClass.CoalPriest })
                            setup.ClassAttack[c] = 0.5;
                    }
                    else if (rule == 1)
                    {
                        setup.HealMult = 0.5;
                    }
                    break;
                case "Portal":
                    int rank = Math.Max(1, L - 3 + run.Floor / 6);
                    bool boss = (run.Floor + 1) % 5 == 0;
                    var kind = Combat.MissionKinds[boss ? "Boss" : "Normal"];
                    var fam = (MonsterFamily)((run.Seed + run.Floor) % 4);
                    for (int w = 0; w < (boss ? 1 : 2); w++)
                        setup.Waves.Add(new WaveSpec { Family = fam, Rank = rank, HpMult = kind.HpMult, AttackMult = kind.AttackMult, Telegraphs = kind.Telegraphs });
                    foreach (var h in setup.Heroes)
                    {
                        int i = run.HpIds.IndexOf(h.Id);
                        h.HpShare = i >= 0 ? run.HpShares[i] : 1;
                        h.AttackMult = 1 + 0.10 * run.Blessings.Count(b => b == 0);
                        h.HpMult = 1 + 0.15 * run.Blessings.Count(b => b == 1);
                    }
                    break;
                default: // Treasure ambush, Siege retake: a normal fight of two waves
                    var normal = Combat.MissionKinds["Normal"];
                    int r = Math.Max(1, L - (run.Id == "Treasure" ? 1 - 2 * run.MapNo : 0));
                    for (int w = 0; w < 2; w++)
                        setup.Waves.Add(new WaveSpec { Family = (MonsterFamily)(eb.Seed % 4), Rank = r, HpMult = normal.HpMult, AttackMult = normal.AttackMult, Telegraphs = false });
                    break;
            }
            return setup;
        }

        public Fail FinishEventBattle(Battle battle, long now, out string result)
        {
            result = "";
            var eb = State.EventBattle;
            if (eb == null)
                return Fail.NoHunt;
            if (!battle.Over)
                battle.RunToEnd();
            State.EventBattle = null;
            var run = State.Events.FirstOrDefault(r => r.Id == eb.EventId && !r.Closed);
            if (run == null)
                return Fail.None;
            NewDayFor(run, now);
            int L = State.HearthLevel;
            switch (run.Id)
            {
                case "LongNight":
                {
                    run.Attempts++;
                    double pts = battle.DamageDealt / BossUnitHp(WeeklyFamily(now), L) * 100;
                    AddPoints(run, pts);
                    result = string.Format("Урон по Ночному Жнецу: {0:0} очков", pts);
                    break;
                }
                case "Tournament":
                {
                    run.Attempts++;
                    double score = battle.DamageDealt / BossUnitHp(MonsterFamily.Knight, L) * 100 +
                                   (battle.Won ? (Combat.MaxTicks - battle.Tick) / 3.0 : 0) +
                                   30 * battle.Heroes.Count(h => h.Alive);
                    if (score > run.DayBest)
                    {
                        AddPoints(run, score - run.DayBest);
                        run.DayBest = score;
                    }
                    result = string.Format("Результат боя {0:0}, лучший за день {1:0}", score, run.DayBest);
                    break;
                }
                case "Portal":
                    if (battle.Won)
                    {
                        run.Floor++;
                        run.HpIds = battle.Heroes.Select(h => h.Id).ToList();
                        int regen = run.Blessings.Count(b => b == 2);
                        run.HpShares = battle.Heroes.Select(h => h.Alive ? Math.Min(1, h.Hp / h.MaxHp + 0.3 * regen) : 0).ToList();
                        if (run.Floor % 5 == 0)
                            run.Checkpoint = run.Floor;
                        if (run.Floor % 3 == 0)
                        {
                            var rng = new Rng(run.Seed * 97 + run.Floor);
                            run.BlessingOffer = new List<int> { 0, 1, 2 }.OrderBy(_ => rng.Next()).ToList();
                        }
                        AddPoints(run, 10);
                        result = "Этаж " + run.Floor + " пройден";
                    }
                    else
                    {
                        run.RunAlive = false;
                        result = "Отряд пал на этаже " + (run.Floor + 1) + ". Воскресить за Искры или начать с этажа " + run.Checkpoint;
                    }
                    break;
                case "Treasure":
                    if (battle.Won && eb.Cell >= 0)
                    {
                        run.Opened.Add(eb.Cell);
                        AddPoints(run, 3);
                        State.Rewards.Add(ResourceBag.Materials(IncomePerHour(), Balance.SilverShare));
                        ClaimRewards();
                        result = "Засада разбита";
                    }
                    else
                    {
                        result = "Засада устояла: клетка закрыта";
                    }
                    break;
                case "Siege":
                    if (battle.Won && eb.Cell >= 0 && eb.Cell < run.Outposts.Count)
                    {
                        run.Outposts[eb.Cell].Fallen = false;
                        result = "Застава отбита";
                    }
                    break;
            }
            run.LastResult = result;
            return Fail.None;
        }

        /// <summary>A started event battle left unfinished (the game closed): fought out by auto-battle.</summary>
        public string ResolvePendingEventBattle(long now)
        {
            var eb = State.EventBattle;
            if (eb == null)
                return null;
            var run = State.Events.FirstOrDefault(r => r.Id == eb.EventId && !r.Closed);
            if (run == null)
            {
                State.EventBattle = null;
                return null;
            }
            var battle = new Battle(Combat, EventBattleSetup(run, eb, now)) { Brain = Brains.AutoBattle() };
            FinishEventBattle(battle, now, out string result);
            return result;
        }

        public Fail PortalRevive(long now)
        {
            var run = ActiveRun("Portal", now);
            if (run == null || run.RunAlive || run.Revives >= 2)
                return Fail.Locked;
            long price = run.Revives == 0 ? 50 : 100;
            if (State.Wallet[ResourceType.Sparks] < price)
                return Fail.NotEnough;
            State.Wallet[ResourceType.Sparks] -= price;
            run.Revives++;
            run.RunAlive = true;
            run.HpShares = run.HpShares.Select(x => Math.Max(x, 0.5)).ToList();
            return Fail.None;
        }

        public static readonly string[] BlessingNames = { "Горящие клинки: +10% атаки", "Закалка: +15% здоровья", "Тлеющий: +30% здоровья после каждого этажа" };

        public Fail ChooseBlessing(int option, long now)
        {
            var run = ActiveRun("Portal", now);
            if (run == null || option < 0 || option >= run.BlessingOffer.Count)
                return Fail.NotFound;
            run.Blessings.Add(run.BlessingOffer[option]);
            run.BlessingOffer.Clear();
            return Fail.None;
        }

        // ================================================================== Treasure hunters

        public static readonly List<(int q, int r)> HexCells = BuildHex();

        static List<(int q, int r)> BuildHex()
        {
            var cells = new List<(int, int)>();
            for (int q = -4; q <= 4; q++)
            {
                for (int r = -4; r <= 4; r++)
                {
                    if (Math.Abs(q + r) <= 4)
                        cells.Add((q, r));
                }
            }
            return cells;
        }

        public static int HexDistance(int a, int b)
        {
            var (q1, r1) = HexCells[a];
            var (q2, r2) = HexCells[b];
            return (Math.Abs(q1 - q2) + Math.Abs(r1 - r2) + Math.Abs(q1 + r1 - q2 - r2)) / 2;
        }

        public static int HexCenter => HexCells.IndexOf((0, 0));

        public enum TreasureCell
        {
            Resources,
            Refugee,
            Ambush,
            Piece,
            Spring,
            Beacon,
            Trap,
            Treasure,
        }

        /// <summary>What lies under a cell: fixed by the run seed and the map number (same for everybody).</summary>
        public TreasureCell CellContent(EventRun run, int cell)
        {
            if (cell == HexCenter)
                return TreasureCell.Resources;
            var rng = new Rng(run.Seed * 131 + run.MapNo * 7919);
            var far = Enumerable.Range(0, HexCells.Count).Where(i => HexDistance(i, HexCenter) >= 3).ToList();
            int treasure = far[(int)(rng.Next() * far.Count)];
            if (cell == treasure)
                return TreasureCell.Treasure;
            var others = Enumerable.Range(0, HexCells.Count).Where(i => i != treasure && i != HexCenter).OrderBy(_ => rng.Next()).ToList();
            int k = others.IndexOf(cell);
            if (k < 5) return TreasureCell.Piece;
            if (k < 11) return TreasureCell.Spring;
            if (k < 15) return TreasureCell.Beacon;
            if (k < 20) return TreasureCell.Trap;
            if (k < 30) return TreasureCell.Ambush;
            if (k < 33) return TreasureCell.Refugee;
            return TreasureCell.Resources;
        }

        /// <summary>Cells where the treasure can still be: every map piece crosses out a third of them.</summary>
        public List<int> TreasureCandidates(EventRun run)
        {
            var far = Enumerable.Range(0, HexCells.Count).Where(i => HexDistance(i, HexCenter) >= 3 && !run.Opened.Contains(i)).ToList();
            int treasure = far.FirstOrDefault(i => CellContent(run, i) == TreasureCell.Treasure);
            var rest = far.Where(i => i != treasure).OrderBy(i => (i * 37 + run.Seed) % 101).ToList();
            int keep = (int)Math.Round(rest.Count * Math.Pow(2.0 / 3, run.Pieces));
            var list = rest.Take(keep).ToList();
            if (far.Contains(treasure))
                list.Add(treasure);
            return list.OrderBy(i => i).ToList();
        }

        void StartTreasureMap(EventRun run)
        {
            run.Opened = new List<int> { HexCenter };
            run.Pieces = 0;
            if (run.MapNo == 0)
                run.Torches = 6;
        }

        public int TorchesLeft(EventRun run, long now)
        {
            if (run.Day != LocalDay(now))
            {
                bool first = run.Day < 0;
                NewDayFor(run, now);
                if (!first)
                    run.Torches = Math.Min(8, run.Torches + 2); // daily tasks bring two torches
            }
            while (run.Torches < 6 && run.TorchAt > 0 && now >= run.TorchAt)
            {
                run.Torches++;
                run.TorchAt = run.Torches < 6 ? run.TorchAt + 2 * 3600 : 0;
            }
            return run.Torches;
        }

        public bool CellOpenable(EventRun run, int cell) =>
            !run.Opened.Contains(cell) && run.Opened.Any(o => HexDistance(o, cell) == 1);

        /// <summary>Opens a cell next to an opened one for a torch. Ambushes start a battle (StartEventBattle).</summary>
        public Fail OpenCell(int cell, long now, out TreasureCell content)
        {
            content = TreasureCell.Resources;
            Advance(now);
            var run = ActiveRun("Treasure", now);
            if (run == null)
                return Fail.Locked;
            if (cell < 0 || cell >= HexCells.Count || !CellOpenable(run, cell))
                return Fail.NotFound;
            if (TorchesLeft(run, now) <= 0)
                return Fail.NoTracks;
            if (run.Torches == 6)
                run.TorchAt = now + 2 * 3600;
            run.Torches--;
            content = CellContent(run, cell);
            if (content == TreasureCell.Ambush)
                return Fail.None; // the caller fights it with StartEventBattle("Treasure", now, out _, cell)
            Reveal(run, cell, content);
            return Fail.None;
        }

        void Reveal(EventRun run, int cell, TreasureCell content)
        {
            run.Opened.Add(cell);
            AddPoints(run, 1);
            switch (content)
            {
                case TreasureCell.Resources:
                    State.Rewards.Add(ResourceBag.Materials(IncomePerHour() / 4, Balance.SilverShare));
                    ClaimRewards();
                    break;
                case TreasureCell.Refugee:
                    if (!PopulationFull())
                        RefugeesArrive(1);
                    break;
                case TreasureCell.Piece:
                    run.Pieces++;
                    break;
                case TreasureCell.Spring:
                    run.Torches++;
                    break;
                case TreasureCell.Trap:
                    run.Torches = Math.Max(0, run.Torches - 1);
                    break;
                case TreasureCell.Beacon:
                    foreach (int n in Enumerable.Range(0, HexCells.Count).Where(i => HexDistance(i, cell) == 1 && !run.Opened.Contains(i)).ToList())
                    {
                        var c = CellContent(run, n);
                        if (c != TreasureCell.Ambush && c != TreasureCell.Treasure && c != TreasureCell.Trap)
                            Reveal(run, n, c);
                    }
                    break;
                case TreasureCell.Treasure:
                    run.Treasures++;
                    AddPoints(run, 50);
                    State.Wallet[ResourceType.Blueprints] += 5 * (run.MapNo + 1);
                    run.MapNo++;
                    StartTreasureMap(run);
                    run.LastResult = "Казна Стражей найдена! Открыта следующая карта";
                    break;
            }
        }

        // ================================================================== Ash Fair

        public static readonly ResourceType[] FairGoods = { ResourceType.Coal, ResourceType.Food, ResourceType.Iron, ResourceType.Silver };
        public static readonly double[] FairValue = { 0.5, 0.5, 1.2, 1.0 };

        /// <summary>Rates change every 4 hours: one good in short supply (x1.5), one in surplus (x0.6).</summary>
        public double[] FairRates(EventRun run, long now)
        {
            long key = (now + State.UtcOffsetSeconds) / (4 * 3600);
            if (run.RateKey != key)
            {
                run.RateKey = key;
                var rng = new Rng((int)(key % 1000003) + run.Seed);
                for (int i = 0; i < 4; i++)
                    run.Rates[i] = 0.85 + 0.3 * rng.Next();
                int deficit = (int)(rng.Next() * 4);
                int surplus = (deficit + 1 + (int)(rng.Next() * 3)) % 4;
                run.Rates[deficit] *= 1.5;
                run.Rates[surplus] *= 0.6;
                while (run.Orders.Count < 3)
                    run.Orders.Add(NewOrder(rng));
            }
            return run.Rates;
        }

        long[] NewOrder(Rng rng)
        {
            int a = (int)(rng.Next() * 4), b = (a + 1 + (int)(rng.Next() * 3)) % 4;
            long income = IncomePerHour();
            return new[] { a, (long)(income / FairValue[a] * 0.5), b, (long)(income / FairValue[b] * 0.5) };
        }

        /// <summary>Sells one good for another at today's rates (20 deals a day); every deal earns tokens.</summary>
        public Fail FairTrade(int sell, int buy, long amount, long now, out long got)
        {
            got = 0;
            Advance(now);
            var run = ActiveRun("Fair", now);
            if (run == null)
                return Fail.Locked;
            NewDayFor(run, now);
            if (sell == buy || sell < 0 || sell > 3 || buy < 0 || buy > 3 || amount <= 0)
                return Fail.NotFound;
            if (run.Trades >= 20)
                return Fail.NoTracks;
            var rates = FairRates(run, now);
            var from = FairGoods[sell];
            if (State.Wallet[from] < amount)
                return Fail.NotEnough;
            double value = amount * FairValue[sell] * rates[sell];
            got = (long)Math.Floor(value / (FairValue[buy] * rates[buy]) * 0.92);
            State.Wallet[from] -= amount;
            long taken = Deposit(FairGoods[buy], got);
            State.Rewards[FairGoods[buy]] += got - taken;
            run.Trades++;
            // a big deal is worth more, but no more than 3 tokens: the Fair rewards many small trades
            AddPoints(run, Math.Min(3, Math.Max(1, Math.Round(value / (IncomePerHour() * 0.5)))));
            return Fail.None;
        }

        public Fail FairDeliver(int order, long now)
        {
            Advance(now);
            var run = ActiveRun("Fair", now);
            if (run == null)
                return Fail.Locked;
            FairRates(run, now);
            if (order < 0 || order >= run.Orders.Count)
                return Fail.NotFound;
            var o = run.Orders[order];
            var a = FairGoods[o[0]];
            var b = FairGoods[o[2]];
            if (State.Wallet[a] < o[1] || State.Wallet[b] < o[3])
                return Fail.NotEnough;
            State.Wallet[a] -= o[1];
            State.Wallet[b] -= o[3];
            run.Orders.RemoveAt(order); // new orders come with the next rates, every 4 hours
            AddPoints(run, 6);
            return Fail.None;
        }

        // ================================================================== Siege of the outposts

        public static readonly string[] OutpostNames = { "Мельница", "Брод", "Старая шахта", "Сторожевой холм", "Часовня" };

        void StartSiege(EventRun run)
        {
            run.Outposts = Enumerable.Range(0, 5).Select(_ => new Outpost()).ToList();
            run.Stone = 6;
            run.WindowsDone.Clear();
        }

        void EndSiege(EventRun run)
        {
            foreach (var o in run.Outposts)
                o.Fallen = false;
        }

        /// <summary>Output penalty of fallen outposts: -5% each while the Siege lasts.</summary>
        public double SiegePenalty(long now)
        {
            var run = ActiveRun("Siege", now);
            return run == null ? 1 : 1 - 0.05 * run.Outposts.Count(o => o.Fallen);
        }

        public Fail SetGarrison(int outpost, IList<int> dwellers, long now)
        {
            var run = ActiveRun("Siege", now);
            if (run == null || outpost < 0 || outpost >= 5 || dwellers.Count > 3)
                return Fail.NotFound;
            foreach (var o in run.Outposts)
                o.Garrison.RemoveAll(dwellers.Contains);
            run.Outposts[outpost].Garrison = dwellers.ToList();
            foreach (int id in dwellers)
                Unassign(id);
            return Fail.None;
        }

        public Fail UpgradeTraps(int outpost, long now)
        {
            var run = ActiveRun("Siege", now);
            if (run == null || outpost < 0 || outpost >= 5)
                return Fail.NotFound;
            var o = run.Outposts[outpost];
            long cost = 4 * (o.Traps + 1);
            if (o.Traps >= 3)
                return Fail.LimitReached;
            if (run.Stone < cost)
                return Fail.NotEnough;
            run.Stone -= cost;
            o.Traps++;
            return Fail.None;
        }

        /// <summary>The current attack window (12-14 and 20-22 local) as a number, or -1.</summary>
        public long SiegeWindow(EventRun run, long now)
        {
            long sec = LocalSecondOfDay(now);
            bool noon = sec >= 12 * 3600 && sec < 14 * 3600;
            bool evening = sec >= 20 * 3600 && sec < 22 * 3600;
            return noon || evening ? LocalDay(now) * 2 + (evening ? 1 : 0) : -1;
        }

        /// <summary>Outposts attacked in a window: two, announced an hour ahead; the last evening, all five.</summary>
        public List<int> SiegeTargets(EventRun run, long window)
        {
            long lastWindow = (LocalDay(run.End - 1)) * 2 + 1;
            if (window == lastWindow)
                return Enumerable.Range(0, 5).ToList();
            var rng = new Rng((int)(window % 1000003) + run.Seed);
            int a = (int)(rng.Next() * 5);
            int b = (a + 1 + (int)(rng.Next() * 4)) % 5;
            return new List<int> { a, b };
        }

        double GarrisonPower(Outpost o)
        {
            double gear = Math.Pow(Combat.Surge.GearGrowth, State.HearthLevel - 1);
            return o.Garrison.Select(FindDweller).Where(d => d != null).Sum(d => (d.StatOf(Stat.Strength) + d.StatOf(Stat.Endurance)) / 2.0 * gear);
        }

        /// <summary>Live defense in the window: the Surge engine on the attacked outposts, heroes join.</summary>
        public Fail StartSiegeDefense(long now, out SurgeDefense defense)
        {
            defense = null;
            Advance(now);
            var run = ActiveRun("Siege", now);
            if (run == null)
                return Fail.Locked;
            long window = SiegeWindow(run, now);
            if (window < 0 || run.WindowsDone.Contains(window) || run.LiveWindow == window)
                return Fail.NotRunning;
            run.LiveWindow = window;
            var c = Combat.Surge;
            var setup = new SurgeSetup { HearthFloor = 0, HearthLevel = State.HearthLevel, Coal = State.Wallet[ResourceType.Coal] };
            setup.SpawnFloors.Add(2);
            double gear = Math.Pow(c.GearGrowth, State.HearthLevel - 1);
            foreach (int target in SiegeTargets(run, window))
            {
                var o = run.Outposts[target];
                foreach (var d in o.Garrison.Select(FindDweller).Where(x => x != null))
                    setup.Defenders.Add(new SurgeDefender { Id = d.Id, Name = d.Name, Floor = 1, Dps = c.DwellerDps * (d.StatOf(Stat.Strength) + d.StatOf(Stat.Endurance)) / 2.0 * gear * (1 + 0.15 * o.Traps) });
            }
            foreach (var d in Squad())
                setup.Defenders.Add(new SurgeDefender { Id = d.Id, Name = d.Name, Hero = true, Floor = 0, Dps = c.HeroDps * Math.Pow(c.HeroLevelGrowth, d.HeroLevel - 1) * gear });
            defense = new SurgeDefense(c, setup);
            return Fail.None;
        }

        public Fail FinishSiegeDefense(SurgeDefense defense, long now)
        {
            var run = ActiveRun("Siege", now) ?? State.Events.LastOrDefault(r => r.Id == "Siege");
            if (run == null || run.LiveWindow < 0)
                return Fail.NoHunt;
            if (!defense.Over)
                defense.RunToEnd();
            long window = run.LiveWindow;
            run.LiveWindow = -1;
            bool held = defense.Stars >= 2;
            ResolveWindow(run, window, held ? 1.0 : 0.0, 1.5);
            return Fail.None;
        }

        /// <summary>Called by Advance: windows that closed without a live defense are fought automatically.</summary>
        void SettleSiege(long now)
        {
            var run = State.Events.FirstOrDefault(r => r.Id == "Siege" && !r.Closed);
            if (run == null)
                return;
            long first = LocalDay(run.Start) * 2;
            long current = SiegeWindow(run, now);
            long nowKey = LocalDay(now) * 2 + (LocalSecondOfDay(now) >= 22 * 3600 ? 2 : LocalSecondOfDay(now) >= 14 * 3600 ? 1 : 0);
            for (long w = first; w < nowKey; w++)
            {
                if (w == current || run.WindowsDone.Contains(w))
                    continue;
                long windowStart = (w / 2) * Day + (w % 2 == 0 ? 12 : 20) * 3600 - State.UtcOffsetSeconds;
                if (windowStart < run.Start || windowStart >= run.End)
                    continue;
                ResolveWindow(run, w, -1, 1.0);
            }
        }

        void ResolveWindow(EventRun run, long window, double live, double mult)
        {
            if (run.WindowsDone.Contains(window))
                return;
            run.WindowsDone.Add(window);
            var rng = new Rng((int)(window % 1000003) * 3 + run.Seed);
            double attack = 3 * (5 + 2 * State.HearthLevel) * Math.Pow(Combat.Surge.GearGrowth, State.HearthLevel - 1) * 0.9;
            var lines = new List<string>();
            foreach (int t in SiegeTargets(run, window))
            {
                var o = run.Outposts[t];
                bool held = live >= 0 ? live > 0 : GarrisonPower(o) * (1 + 0.15 * o.Traps) >= attack * (0.8 + 0.4 * rng.Next());
                if (held)
                {
                    AddPoints(run, 10 * mult);
                    run.Stone += 2;
                }
                else
                {
                    o.Fallen = true;
                }
                lines.Add(OutpostNames[t] + (held ? " устояла" : " пала"));
            }
            run.LastResult = string.Join(", ", lines);
        }

        // ================================================================== Hearth Festival

        /// <summary>The lantern mini-game: 30 lanterns in 60 seconds; a tap within a second of a lantern lights it.</summary>
        public static List<(double time, int spot)> Lanterns(int seed)
        {
            var rng = new Rng(seed);
            return Enumerable.Range(0, 30).Select(i => (1.5 + i * 1.9 + rng.Next() * 0.8, (int)(rng.Next() * 9))).ToList();
        }

        public static int LanternScore(int seed, IEnumerable<(double time, int spot)> taps)
        {
            var lanterns = Lanterns(seed);
            var lit = new HashSet<int>();
            foreach (var (t, spot) in taps)
            {
                int i = lanterns.FindIndex(l => l.spot == spot && Math.Abs(l.time - t) <= 1.0);
                if (i >= 0)
                    lit.Add(i);
            }
            return lit.Count * 10;
        }

        public Fail StartLanterns(long now, out int seed)
        {
            seed = 0;
            var run = ActiveRun("Festival", now);
            if (run == null)
                return Fail.Locked;
            NewDayFor(run, now);
            if (run.Attempts >= 3)
                return Fail.NoTracks;
            run.Attempts++;
            seed = run.Seed * 1000 + run.Attempts + (int)(LocalDay(now) % 1000) * 7;
            return Fail.None;
        }

        public Fail FinishLanterns(int seed, IEnumerable<(double time, int spot)> taps, long now, out int score)
        {
            score = LanternScore(seed, taps);
            var run = ActiveRun("Festival", now);
            if (run == null)
                return Fail.Locked;
            NewDayFor(run, now);
            if (score > run.DayBest)
            {
                AddPoints(run, score - run.DayBest);
                run.DayBest = score;
            }
            return Fail.None;
        }

        /// <summary>Daily tasks done during the Festival are festival tasks too (+100 points).</summary>
        void FestivalTasksDone(long now) => AddPoints(ActiveRun("Festival", now), 100);
    }
}

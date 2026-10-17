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

        /// <summary>A boss's health at a rank, the unit of the events' points. Like the event enemies, it counts the free
        /// player's items over the forge's issued kit (GearEdge), so finds do not inflate the points.</summary>
        double BossUnitHp(MonsterFamily family, int rank)
        {
            var b = Combat.Boss;
            return b.Hp * Combat.Family(family).Hp * Math.Pow(b.HpGrowth, rank - 1) * GearEdge(rank);
        }

        MonsterFamily WeeklyFamily(long now) => (MonsterFamily)((LocalDay(now) / 7) % 4);

        /// <summary>Share of the run's time that has passed.</summary>
        public double RunProgress(EventRun run, long now) =>
            Math.Max(0, Math.Min(1, (now - run.Start) / (double)Math.Max(1, run.End - run.Start)));

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
            else if (id == "Treasure" && run.Extra < 3)
                price = 20;
            else
                return Fail.LimitReached;
            if (State.Wallet[ResourceType.Sparks] < price)
                return Fail.NotEnough;
            State.Wallet[ResourceType.Sparks] -= price;
            run.Extra++;
            if (id == "Treasure")
                run.Torches++;
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
                    var ready = PortalReady(run, now);
                    if (ready != Fail.None)
                        return ready;
                    var doors = PortalDoors(run);
                    if (run.Door < 0 || !doors.Contains((PortalDoor)run.Door))
                        run.Door = (int)doors[0];
                    if (run.Door != (int)PortalDoor.Fight && run.Door != (int)PortalDoor.Elite && run.Door != (int)PortalDoor.Guardian)
                        return Fail.RulesNotMet;
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
                {
                    int phase = NightPhase(run, now);
                    if (phase == 2) // the Reaper calls up creatures: damage to all of them counts
                        setup.Waves.Add(new WaveSpec { Family = MonsterFamily.Fiend, Rank = L, HpMult = 2, AttackMult = 0.7, Telegraphs = false });
                    // the Eclipse: the light goes out, heavy strikes come harder (the Wall or the Warm hands answer them)
                    setup.Waves.Add(new WaveSpec { Family = NightFamily(run), Rank = L, HpMult = 40, AttackMult = phase == 3 ? 1.3 : 1, Telegraphs = true });
                    break;
                }
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
                {
                    int rank = Math.Max(1, L - 3 + run.Floor / 6);
                    var door = run.Door < 0 ? PortalDoors(run)[0] : (PortalDoor)run.Door;
                    bool boss = door == PortalDoor.Guardian;
                    bool hostess = boss && run.Floor + 1 == PortalFloors;
                    var kind = Combat.MissionKinds[boss ? "Boss" : door == PortalDoor.Elite ? "Elite" : "Normal"];
                    var fam = (MonsterFamily)((run.Seed + run.Floor) % 4);
                    for (int w = 0; w < (door == PortalDoor.Fight ? 2 : 1); w++)
                        setup.Waves.Add(new WaveSpec
                        {
                            Family = fam, Rank = rank, HpMult = kind.HpMult * (hostess ? 1.5 : 1), AttackMult = kind.AttackMult * (hostess ? 1.2 : 1),
                            Telegraphs = kind.Telegraphs,
                        });
                    int fallen = run.HpShares.Count(x => x <= 0);
                    foreach (var h in setup.Heroes)
                    {
                        int i = run.HpIds.IndexOf(h.Id);
                        h.HpShare = i >= 0 ? run.HpShares[i] : 1;
                        var (hallsAttack, hallsHp) = HallsHeroMult(h.Id); // research and ascension (GameSession.Rooms.cs)
                        h.AttackMult = (1 + 0.10 * Blessed(run, 0) + 0.20 * Blessed(run, 3) * fallen + (boss ? 0.25 * Blessed(run, 5) : 0)) * hallsAttack;
                        h.HpMult = (1 + 0.15 * Blessed(run, 1)) * hallsHp;
                    }
                    break;
                }
                default: // Treasure ambush, Siege retake: a normal fight of two waves
                    var normal = Combat.MissionKinds["Normal"];
                    int r = Math.Max(1, L - (run.Id == "Treasure" ? 1 - 2 * run.MapNo : 0));
                    for (int w = 0; w < 2; w++)
                        setup.Waves.Add(new WaveSpec { Family = (MonsterFamily)(eb.Seed % 4), Rank = r, HpMult = normal.HpMult, AttackMult = normal.AttackMult, Telegraphs = false });
                    break;
            }
            foreach (var w in setup.Waves)
            {
                // the free player's finds are part of the strength the events expect, as in the campaign
                w.HpMult *= GearEdge(w.Rank);
                w.AttackMult *= GearEdge(w.Rank);
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
                    // the whole damage counts, but one attack gives the rating no more than 3% of the Reaper's pool
                    double pts = Math.Min(battle.DamageDealt / BossUnitHp(NightFamily(run), L) * 100, NightAttackCap * NightPool(run));
                    AddPoints(run, pts);
                    CheckDawn(run, now);
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
                {
                    var door = run.Door < 0 ? PortalDoor.Fight : (PortalDoor)run.Door;
                    run.HpIds = battle.Heroes.Select(h => h.Id).ToList();
                    if (battle.Won)
                    {
                        int regen = Blessed(run, 2);
                        run.HpShares = battle.Heroes.Select(h => h.Alive ? Math.Min(1, h.Hp / h.MaxHp + 0.3 * regen) : 0).ToList();
                        TireFallen(battle, now);
                        bool hostess = door == PortalDoor.Guardian && run.Floor + 1 == PortalFloors;
                        PortalFloorDone(run, door == PortalDoor.Elite ? 15 : hostess ? 40 : 10, door == PortalDoor.Elite ? 3 : hostess ? 20 : 1);
                        result = hostess ? "Хозяйка Портала повержена! Дальше — Глубина" : "Этаж " + run.Floor + " пройден";
                    }
                    else
                    {
                        run.HpShares = battle.Heroes.Select(_ => 0.0).ToList();
                        TireFallen(battle, now);
                        run.RunAlive = false;
                        run.Door = -1;
                        result = "Отряд пал на этаже " + (run.Floor + 1) + ". Воскресить за Искры или начать с этажа " + (run.Checkpoint + 1);
                    }
                    break;
                }
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

        // ================================================================== Long Night

        /// <summary>The weakness of the Reaper by family: Fiend, Possessed, Knight, Root (EVENTS.md 4).</summary>
        public static readonly string[] WeaknessNames = { "огонь", "кровотечение", "оглушение", "холод" };

        /// <summary>The Reaper's pool for the group: 28 targets; the ghosts and the player wear it down in about two thirds of the night.</summary>
        const double NightPoolTargets = 28;
        const double NightAttackCap = 0.03;

        /// <summary>The Reaper of the week: its family (and so its weakness) turns every Monday and is announced a day ahead.</summary>
        public MonsterFamily NightFamilyAt(long t) => (MonsterFamily)((((LocalDay(t) - MondayEpochDay) / 7) % 4 + 4) % 4);

        public MonsterFamily NightFamily(EventRun run) => NightFamilyAt(run.Start);

        public double NightPool(EventRun run) => RunTarget(run) * NightPoolTargets;

        /// <summary>Damage of the whole group: the ghosts strike evenly through the night, the player as he plays.</summary>
        public double NightGroupDamage(EventRun run, long now) => Ghosts(run).Sum() * RunProgress(run, now) + run.Points;

        /// <summary>Share of the Reaper's pool left (0: the Dawn came).</summary>
        public double NightPoolLeft(EventRun run, long now) => Math.Max(0, 1 - NightGroupDamage(run, now) / NightPool(run));

        /// <summary>1: plain strikes (100-60%), 2: creatures called up (60-30%), 3: the Eclipse (below 30%).</summary>
        public int NightPhase(EventRun run, long now)
        {
            double left = NightPoolLeft(run, now);
            return left > 0.6 ? 1 : left > 0.3 ? 2 : 3;
        }

        /// <summary>Attacks of the Long Night today: 3, +1 after today's Surge, +1 bought.</summary>
        public int NightAttacksLeft(EventRun run, long now)
        {
            NewDayFor(run, now);
            int bonus = State.SurgeDay == LocalDay(now) ? 1 : 0;
            return 3 + bonus + run.Extra - run.Attempts;
        }

        /// <summary>The «Рассвет» chest once the group has exhausted the pool: the earlier, the better (3 tiers).</summary>
        void CheckDawn(EventRun run, long now)
        {
            if (run.Id != "LongNight" || run.DawnAt > 0 || NightGroupDamage(run, now) < NightPool(run))
                return;
            double ghosts = Math.Max(1e-9, Ghosts(run).Sum());
            double p = Math.Max(0, Math.Min(RunProgress(run, now), (NightPool(run) - run.Points) / ghosts));
            run.DawnAt = Math.Max(run.Start + 1, run.Start + (long)(p * (run.End - run.Start)));
            run.DawnTier = p <= 0.6 ? 3 : p <= 0.7 ? 2 : 1;
            Purse(run) += Math.Max(1, (long)Math.Round(10 * run.DawnTier * RunScale(run)));
            run.LastResult = "Рассвет! Группа одолела Ночного Жнеца: сундук «Рассвет» " + new string('★', run.DawnTier);
        }

        /// <summary>Warmth of all rooms x0.85 while the Long Night lasts (EVENTS.md 4).</summary>
        public double NightWarmth() => ActiveRun("LongNight", State.Time) != null ? 0.85 : 1;

        // ================================================================== Tournament

        public int TournamentAttemptsLeft(EventRun run, long now)
        {
            NewDayFor(run, now);
            return 5 + run.Extra - run.Attempts;
        }

        /// <summary>Tournament rule of the day: 0 Steel (melee +30%, magic -50%), 1 Cold (healing -50%), 2 Knights.</summary>
        public int TournamentRule(EventRun run, long now) => EventDayIndex(run, now) % 3;

        public static readonly string[] TournamentTitles = { "Хранитель очага", "Магистр", "Старший Страж", "Страж", "Ученик" };

        /// <summary>The title of a place in the group of 30: 1, 2-5, 6-12, 13-20, the rest (30 to 120 Sparks with the budget).</summary>
        public static string TournamentTitle(int rank) =>
            TournamentTitles[rank == 1 ? 0 : rank <= 5 ? 1 : rank <= 12 ? 2 : rank <= 20 ? 3 : 4];

        static readonly HeroClass[][] RuleSquads =
        {
            new[] { HeroClass.Guardian, HeroClass.Swordsman, HeroClass.Cleaver, HeroClass.Swordsman, HeroClass.CoalPriest },
            new[] { HeroClass.Guardian, HeroClass.AshMage, HeroClass.AshMage, HeroClass.Archer, HeroClass.Cleaver },
            new[] { HeroClass.Guardian, HeroClass.Cleaver, HeroClass.Cleaver, HeroClass.Archer, HeroClass.AshMage },
        };

        /// <summary>EVENTS.md 6: the best squad of the group for a day of the tournament (a ghost of the arena's names).</summary>
        public (string name, HeroClass[] squad, double score) TournamentBest(EventRun run, int day)
        {
            var rng = new Rng(run.Seed * 17 + day);
            var squad = RuleSquads[day % 3].OrderBy(_ => rng.Next()).ToArray();
            double top = Ghosts(run).Max() / Math.Max(1, EventInfo(run.Id).Days) * (0.95 + 0.1 * rng.Next());
            return (GhostNames[(run.Seed + day) % GhostNames.Length], squad, Math.Max(top, day < run.DayBests.Count ? run.DayBests[day] : 0));
        }

        // ================================================================== Portal

        public enum PortalDoor
        {
            Fight,
            Elite,
            Rest,
            Merchant,
            Stash,
            Guardian,
        }

        public static readonly string[] DoorNames = { "Бой", "Элита", "Привал", "Торговец", "Тайник", "Страж этажа" };

        public const int PortalFloors = 30;
        const long MerchantPrice = 8;

        public static readonly string[] BlessingNames =
        {
            "Горящие клинки: +10% атаки", "Закалка: +15% здоровья", "Тлеющий: +30% здоровья после каждого этажа",
            "Братство: +20% атаки за каждого павшего", "Пепельный дар: +2 Пепла за этаж", "Гроза стражей: +25% атаки на стражей этажей",
        };

        int Blessed(EventRun run, int blessing) => run.Blessings.Count(b => b == blessing);

        public int PortalFloorsOpen(EventRun run, long now)
        {
            int day = EventDayIndex(run, now);
            return run.Personal || day >= 3 ? int.MaxValue : 10 * (day + 1);
        }

        /// <summary>The doors of the next floor: a fight and one or two more; every 5th floor only its guardian (the 30th: the Hostess).</summary>
        public List<PortalDoor> PortalDoors(EventRun run)
        {
            if ((run.Floor + 1) % 5 == 0)
                return new List<PortalDoor> { PortalDoor.Guardian };
            var rng = new Rng(run.Seed * 53 + run.Floor * 7 + 1);
            int more = rng.Next() < 0.5 ? 1 : 2;
            var others = new[] { PortalDoor.Elite, PortalDoor.Rest, PortalDoor.Merchant, PortalDoor.Stash }.Select(d => (d, k: rng.Next()))
                .OrderBy(x => x.k).Take(more).Select(x => x.d);
            return new[] { PortalDoor.Fight }.Concat(others).ToList();
        }

        /// <summary>The squad can go on: a blessing chosen, a run alive (or the free run of the day starts), the floor open.</summary>
        Fail PortalReady(EventRun run, long now)
        {
            if (run.BlessingOffer.Count > 0)
                return Fail.RulesNotMet;
            if ((run.RunAlive ? run.Floor : run.Checkpoint) >= PortalFloorsOpen(run, now))
                return Fail.Locked;
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
                run.Door = -1;
            }
            return Fail.None;
        }

        /// <summary>Goes through a door. A fight, an elite or a guardian is fought next with StartEventBattle("Portal");
        /// a rest, the merchant and a stash are settled at once.</summary>
        public Fail PortalEnter(int index, long now, out string result)
        {
            result = "";
            Advance(now);
            var run = ActiveRun("Portal", now);
            if (run == null)
                return Fail.Locked;
            if (State.EventBattle != null)
                return Fail.HuntInProgress;
            var fail = PortalReady(run, now);
            if (fail != Fail.None)
                return fail;
            var doors = PortalDoors(run);
            if (index < 0 || index >= doors.Count)
                return Fail.NotFound;
            var door = doors[index];
            run.Door = (int)door;
            switch (door)
            {
                case PortalDoor.Rest:
                    PortalHp(run);
                    run.HpShares = run.HpShares.Select(x => Math.Min(1, x + 0.3)).ToList();
                    PortalFloorDone(run, 10, 1);
                    result = "Привал: отряд подлечился на 30%";
                    break;
                case PortalDoor.Merchant:
                    PortalFloorDone(run, 10, 1);
                    if (run.BlessingOffer.Count == 0)
                    {
                        run.BlessingOffer = BlessingOffer(run, 11);
                        run.OfferPrice = MerchantPrice;
                    }
                    result = "Торговец предлагает дары за " + MerchantPrice + " Пепла";
                    break;
                case PortalDoor.Stash:
                    PortalHp(run);
                    if (new Rng(run.Seed * 31 + run.Floor).Next() < 0.65)
                    {
                        Purse(run) += 3;
                        State.Rewards.Add(ResourceBag.Materials(IncomePerHour() / 2, Balance.SilverShare));
                        ClaimRewards();
                        result = "Тайник: сундук с материалами и Пеплом";
                    }
                    else
                    {
                        run.HpShares = run.HpShares.Select(x => x > 0 ? Math.Max(0.05, x - 0.2) : 0).ToList();
                        result = "Тайник оказался ловушкой: отряд ранен";
                    }
                    PortalFloorDone(run, 10, 0);
                    break;
                default:
                    return Fail.None; // fight it: StartEventBattle("Portal", now, out battle)
            }
            run.LastResult = result;
            return Fail.None;
        }

        /// <summary>Health of the squad kept between floors, filled in for heroes the run has not seen yet.</summary>
        void PortalHp(EventRun run)
        {
            foreach (var d in Squad())
            {
                if (!run.HpIds.Contains(d.Id))
                {
                    run.HpIds.Add(d.Id);
                    run.HpShares.Add(1);
                }
            }
        }

        void PortalFloorDone(EventRun run, double points, long ash)
        {
            run.Floor++;
            run.Door = -1;
            if (run.Floor % 5 == 0)
                run.Checkpoint = run.Floor;
            if (run.Floor % 3 == 0)
            {
                run.BlessingOffer = BlessingOffer(run, 0);
                run.OfferPrice = 0;
            }
            Purse(run) += ash + 2 * Blessed(run, 4);
            AddPoints(run, points);
        }

        List<int> BlessingOffer(EventRun run, int salt)
        {
            var rng = new Rng(run.Seed * 97 + run.Floor + salt);
            return Enumerable.Range(0, BlessingNames.Length).Select(b => (b, k: rng.Next())).OrderBy(x => x.k).Take(3).Select(x => x.b).ToList();
        }

        /// <summary>Heroes fallen in the Portal do not die: they are tired for 4 hours (no routes).</summary>
        void TireFallen(Battle battle, long now)
        {
            foreach (var h in battle.Heroes.Where(h => !h.Alive))
            {
                var d = FindDweller(h.Id);
                if (d != null)
                    d.InjuredUntil = Math.Max(d.InjuredUntil, now + 4 * 3600);
            }
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

        /// <summary>Takes a blessing of the offer: free every third floor, for Ash at the merchant.</summary>
        public Fail ChooseBlessing(int option, long now)
        {
            var run = ActiveRun("Portal", now);
            if (run == null || option < 0 || option >= run.BlessingOffer.Count)
                return Fail.NotFound;
            if (Purse(run) < run.OfferPrice)
                return Fail.NotEnough;
            Purse(run) -= run.OfferPrice;
            run.Blessings.Add(run.BlessingOffer[option]);
            run.BlessingOffer.Clear();
            run.OfferPrice = 0;
            return Fail.None;
        }

        /// <summary>Leaves the merchant without buying (the free blessing of every third floor must be taken).</summary>
        public Fail SkipBlessing(long now)
        {
            var run = ActiveRun("Portal", now);
            if (run == null || run.BlessingOffer.Count == 0 || run.OfferPrice == 0)
                return Fail.NotFound;
            run.BlessingOffer.Clear();
            run.OfferPrice = 0;
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
            run.RouteFragments = 0; // pieces of maps from the routes (GameSession.Routes.cs)
            if (run.MapNo == 0)
                run.Torches = 6;
        }

        public int TorchesLeft(EventRun run, long now)
        {
            NewDayFor(run, now); // a new day: the daily tasks bring two torches
            while (run.Torches < 6 && run.TorchAt > 0 && now >= run.TorchAt)
            {
                run.Torches++;
                run.TorchAt = run.Torches < 6 ? run.TorchAt + 2 * 3600 : 0;
            }
            return run.Torches;
        }

        public bool CellOpenable(EventRun run, int cell) =>
            !run.Opened.Contains(cell) && run.Opened.Any(o => HexDistance(o, cell) == 1);

        /// <summary>A sharp-eyed Archer (Agility 30+) in the squad spots traps.</summary>
        public bool TrapSpotter() => Squad().Any(d => d.HeroClass == HeroClass.Archer && d.StatOf(Stat.Agility) >= 30);

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
                    if (TrapSpotter())
                        run.LastResult = "Стрелок заметил ловушку: факел цел";
                    else
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
                    // the chest of the treasury: a rare dweller and coins, the second one a decoration of the fortress
                    Purse(run) += Math.Max(1, (long)Math.Round(30 * RunScale(run)));
                    string prize;
                    if (run.MapNo == 0 && !PopulationFull())
                    {
                        RareDweller();
                        prize = "редкий житель";
                    }
                    else if (run.MapNo == 1)
                    {
                        State.DecorOwned["chest_gold"] = DecorOwned("chest_gold") + 1;
                        prize = "украшение «Сундук мастера»";
                    }
                    else
                    {
                        State.Rewards.Add(ResourceBag.Materials(IncomePerHour() * 2, Balance.SilverShare));
                        ClaimRewards();
                        prize = "материалы";
                    }
                    run.MapNo++;
                    StartTreasureMap(run);
                    run.LastResult = "Казна Стражей найдена: " + prize + " и монеты казны! Открыта следующая карта";
                    break;
            }
        }

        // ================================================================== Ash Fair

        public static readonly ResourceType[] FairGoods = { ResourceType.Coal, ResourceType.Food, ResourceType.Iron, ResourceType.Silver };
        public static readonly double[] FairValue = { 0.5, 0.5, 1.2, 1.0 };

        long FairKey(long now) => (now + State.UtcOffsetSeconds) / (4 * 3600);

        /// <summary>The rates of a 4-hour window: one good in short supply (x1.5), one in surplus (x0.6).</summary>
        double[] FairRatesAt(EventRun run, long key)
        {
            var rates = new double[4];
            var rng = new Rng((int)(key % 1000003) + run.Seed);
            for (int i = 0; i < 4; i++)
                rates[i] = 0.85 + 0.3 * rng.Next();
            int deficit = (int)(rng.Next() * 4);
            int surplus = (deficit + 1 + (int)(rng.Next() * 3)) % 4;
            rates[deficit] *= 1.5;
            rates[surplus] *= 0.6;
            return rates;
        }

        /// <summary>Rates change every 4 hours; new orders come with them.</summary>
        public double[] FairRates(EventRun run, long now)
        {
            long key = FairKey(now);
            if (run.RateKey != key)
            {
                run.RateKey = key;
                run.Rates = FairRatesAt(run, key);
                var rng = new Rng((int)(key % 1000003) * 5 + run.Seed);
                while (run.Orders.Count < 3)
                    run.Orders.Add(NewOrder(rng));
            }
            return run.Rates;
        }

        /// <summary>The rates of the next 4 hours, revealed for one Ash token (paid once per window).</summary>
        public Fail FairHint(long now, out double[] next)
        {
            next = null;
            var run = ActiveRun("Fair", now);
            if (run == null)
                return Fail.Locked;
            long key = FairKey(now) + 1;
            if (run.HintKey != key)
            {
                if (Purse(run) < 1)
                    return Fail.NotEnough;
                Purse(run) -= 1;
                run.HintKey = key;
            }
            next = FairRatesAt(run, key);
            return Fail.None;
        }

        /// <summary>The rates of the next 4 hours if the hint is bought; null otherwise.</summary>
        public double[] FairHinted(EventRun run, long now) => run.HintKey == FairKey(now) + 1 ? FairRatesAt(run, run.HintKey) : null;

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

        /// <summary>Who attacks a window: creatures, the possessed, knights of the Court or a mixed host.</summary>
        public static readonly string[] SiegeEnemyNames = { "твари", "одержимые", "рыцари Двора", "смешанный отряд" };

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

        /// <summary>An instant repair of a fallen outpost's wall: 20 Sparks, no more than 3 times an event.</summary>
        public Fail SiegeRepair(int outpost, long now)
        {
            var run = ActiveRun("Siege", now);
            if (run == null || outpost < 0 || outpost >= run.Outposts.Count || !run.Outposts[outpost].Fallen)
                return Fail.NotFound;
            if (run.Repairs >= 3)
                return Fail.LimitReached;
            if (State.Wallet[ResourceType.Sparks] < 20)
                return Fail.NotEnough;
            State.Wallet[ResourceType.Sparks] -= 20;
            run.Repairs++;
            run.Outposts[outpost].Fallen = false;
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

        /// <summary>Start (UTC seconds) of a window.</summary>
        public long SiegeWindowStart(long window) => (window / 2) * Day + (window % 2 == 0 ? 12 : 20) * 3600 - State.UtcOffsetSeconds;

        /// <summary>The window going on now, or the next one of the run; -1 when none is left.</summary>
        public long SiegeNextWindow(EventRun run, long now)
        {
            long current = SiegeWindow(run, now);
            if (current >= 0)
                return current;
            for (long w = LocalDay(now) * 2; w <= LocalDay(run.End) * 2 + 1; w++)
            {
                long start = SiegeWindowStart(w);
                if (start > now && start >= run.Start && start < run.End)
                    return w;
            }
            return -1;
        }

        /// <summary>The scouts report an hour before the window: which outposts and who comes.</summary>
        public bool SiegeScouted(EventRun run, long window, long now) => window >= 0 && now >= SiegeWindowStart(window) - 3600;

        /// <summary>The last window of the event (Sunday 20-22): the Commander of the Court on all five outposts.</summary>
        public bool SiegeFinal(EventRun run, long window) => window == LocalDay(run.End - 1) * 2 + 1;

        /// <summary>Outposts attacked in a window: two, announced an hour ahead; the last evening, all five.</summary>
        public List<int> SiegeTargets(EventRun run, long window)
        {
            if (SiegeFinal(run, window))
                return Enumerable.Range(0, 5).ToList();
            var rng = new Rng((int)(window % 1000003) + run.Seed);
            int a = (int)(rng.Next() * 5);
            int b = (a + 1 + (int)(rng.Next() * 4)) % 5;
            return new List<int> { a, b };
        }

        /// <summary>0 creatures, 1 possessed, 2 knights, 3 mixed.</summary>
        public int SiegeEnemy(EventRun run, long window) => (int)(new Rng((int)(window % 1000003) * 7 + run.Seed).Next() * 4);

        /// <summary>The stat of the garrison that answers the enemy: Agility shoots creatures from the walls,
        /// Endurance holds the possessed, Strength the knights.</summary>
        static double GuardStat(Dweller d, int enemy)
        {
            double wall = (d.StatOf(Stat.Strength) + d.StatOf(Stat.Endurance)) / 2.0;
            double answer = enemy == 0 ? d.StatOf(Stat.Agility) : enemy == 1 ? d.StatOf(Stat.Endurance) : enemy == 2 ? d.StatOf(Stat.Strength) : wall;
            return (wall + answer) / 2;
        }

        public double GarrisonPower(Outpost o, int enemy = 3)
        {
            double gear = Math.Pow(Combat.Surge.GearGrowth, State.HearthLevel - 1);
            return o.Garrison.Select(FindDweller).Where(d => d != null).Sum(d => GuardStat(d, enemy) * gear) * (1 + 0.15 * o.Traps);
        }

        /// <summary>The outpost the Commander goes for in the final window: the weakest one.</summary>
        public int SiegeWeakest(EventRun run, long window)
        {
            int enemy = SiegeEnemy(run, window);
            return Enumerable.Range(0, run.Outposts.Count).OrderBy(i => GarrisonPower(run.Outposts[i], enemy)).ThenBy(i => i).First();
        }

        /// <summary>Live defense in the window: the Surge engine on the attacked outposts, heroes join. In the final
        /// window the squad stands at one outpost (<paramref name="stand"/>, by default where the Commander goes).</summary>
        public Fail StartSiegeDefense(long now, out SurgeDefense defense, int stand = -1)
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
            bool final = SiegeFinal(run, window);
            run.Stand = final ? (stand >= 0 && stand < 5 ? stand : SiegeWeakest(run, window)) : -1;
            var c = Combat.Surge;
            var setup = new SurgeSetup { HearthFloor = 0, HearthLevel = State.HearthLevel, Coal = State.Wallet[ResourceType.Coal] };
            setup.SpawnFloors.Add(2);
            double gear = Math.Pow(c.GearGrowth, State.HearthLevel - 1);
            foreach (int target in final ? new List<int> { run.Stand } : SiegeTargets(run, window))
            {
                var o = run.Outposts[target];
                foreach (var d in o.Garrison.Select(FindDweller).Where(x => x != null))
                    setup.Defenders.Add(new SurgeDefender { Id = d.Id, Name = d.Name, Floor = 1, Dps = c.DwellerDps * (d.StatOf(Stat.Strength) + d.StatOf(Stat.Endurance)) / 2.0 * gear * (1 + 0.15 * o.Traps) });
            }
            foreach (var d in Squad())
                setup.Defenders.Add(new SurgeDefender { Id = d.Id, Name = d.Name, Hero = true, Floor = 0, Dps = c.HeroDps * Math.Pow(c.HeroLevelGrowth, d.HeroLevel - 1) * (1 + HeroGear(d).Attack) });
            defense = new SurgeDefense(c, setup);
            return Fail.None;
        }

        public Fail FinishSiegeDefense(SurgeDefense defense, long now)
        {
            var run = ActiveRun("Siege", now) ?? State.Events.LastOrDefault(r => r.Id == "Siege" && !r.Closed);
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
            if (run != null)
                SettleSiege(run, now);
        }

        void SettleSiege(EventRun run, long now)
        {
            long first = LocalDay(run.Start) * 2;
            long current = SiegeWindow(run, now);
            long nowKey = LocalDay(now) * 2 + (LocalSecondOfDay(now) >= 22 * 3600 ? 2 : LocalSecondOfDay(now) >= 14 * 3600 ? 1 : 0);
            for (long w = first; w < nowKey; w++)
            {
                if (w == current || run.WindowsDone.Contains(w))
                    continue;
                long windowStart = SiegeWindowStart(w);
                if (windowStart < run.Start || windowStart >= run.End)
                    continue;
                if (run.LiveWindow == w)
                    run.LiveWindow = -1; // a live defense left open: the outposts fought alone
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
            int enemy = SiegeEnemy(run, window);
            bool final = SiegeFinal(run, window);
            int commander = final ? SiegeWeakest(run, window) : -1;
            var lines = new List<string>();
            foreach (int t in SiegeTargets(run, window))
            {
                var o = run.Outposts[t];
                double strength = attack * (0.8 + 0.4 * rng.Next()) * (t == commander ? 1.5 : 1);
                bool liveHere = live >= 0 && (!final || t == run.Stand);
                bool held = liveHere ? live > 0 : GarrisonPower(o, enemy) >= strength;
                if (held)
                {
                    AddPoints(run, 10 * (liveHere ? mult : 1.0));
                    run.Stone += 2;
                }
                else
                {
                    o.Fallen = true;
                }
                lines.Add(OutpostNames[t] + (held ? " устояла" : " пала") + (t == commander ? " (командор)" : ""));
            }
            run.LastResult = (final ? "Рыцарь-командор: " : "") + string.Join(", ", lines);
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

        public const long FestivalGoal = 10000000;

        /// <summary>The server's lanterns («Зажечь 10 миллионов фонарей»): ghosts light them through the festival,
        /// the goal is reached near the end. Share of the goal, may pass 1.</summary>
        public double FestivalServerGoal(EventRun run, long now) => RunProgress(run, now) * 1.15 + run.Points / FestivalGoal;
    }
}

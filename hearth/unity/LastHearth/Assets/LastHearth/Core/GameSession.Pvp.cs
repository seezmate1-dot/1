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
                DefendArena(d);
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
        void DefendArena(long day)
        {
            var rng = new Rng((int)(day % 1000003) * 31 + State.ArenaRating);
            for (int i = 0; i < 2; i++)
            {
                bool held = rng.Next() < (State.ArenaDefense.Count > 0 ? 0.55 : 0.45);
                int change = held ? 4 : -8;
                State.ArenaRating = Math.Max(0, State.ArenaRating + change);
                PvpNote(string.Format("{0} напал на вашу оборону: {1} ({2:+0;-0})", GhostNames[(int)(rng.Next() * GhostNames.Length)],
                    held ? "отбились" : "оборона пала", change));
            }
        }

        void GhostRaid(long now)
        {
            var g = Ghost(new Rng(State.NextBattleSeed++), 0.9 + 0.2 * (State.NextBattleSeed % 5) / 4.0);
            g.Until = now + Pvp.RevengeHours * 3600L;
            State.Revenge.Add(g);
            PvpNote(string.Format("{0} совершил набег на крепость. Вы ничего не потеряли; месть доступна сутки", g.Name));
        }

        void PvpNote(string text)
        {
            State.PvpLog.Add(text);
            if (State.PvpLog.Count > 30)
                State.PvpLog.RemoveAt(0);
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

        public Fail StartArena(int index, long now, out Battle battle)
        {
            battle = null;
            Advance(now);
            if (!ArenaOpen)
                return Fail.Locked;
            if (BattleBusy)
                return Fail.HuntInProgress;
            if (Squad().Count == 0)
                return Fail.NoHeroes;
            var list = ArenaOpponents(now);
            if (index < 0 || index >= list.Count)
                return Fail.NotFound;
            if (ArenaTickets(now) <= 0)
                return Fail.NoTracks;
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
                FinishRaid(pb, battle, reward);
            else
                FinishArena(pb, battle, reward);
            State.Wallet.Add(reward.Got);
            return Fail.None;
        }

        void FinishArena(PvpBattle pb, Battle battle, PvpReward reward)
        {
            int diff = pb.Opponent.Rating - State.ArenaRating;
            reward.Stars = battle.Won ? battle.Stars : 0;
            reward.RatingChange = battle.Won
                ? Math.Max(15, Math.Min(30, 22 + diff / 20))
                : -Math.Max(10, Math.Min(20, 15 - diff / 40));
            State.ArenaRating = Math.Max(0, State.ArenaRating + reward.RatingChange);
            State.ArenaWeekFights++;
            long essenceDay = Balance.Activities.EssenceByLevel[State.HearthLevel - 1];
            reward.Got[ResourceType.Glory] = battle.Won ? Pvp.WinGlory : Pvp.LossGlory;
            reward.Got[ResourceType.Essence] = (long)Math.Round(essenceDay * Pvp.WinEssence * (battle.Won ? 1 : 1.0 / 3));
            if (battle.Won)
                State.ArenaWins++;
            State.ArenaList.Clear();
            PvpNote(string.Format("Арена против {0}: {1} ({2:+0;-0})", pb.Opponent.Name, battle.Won ? "победа" : "поражение", reward.RatingChange));
        }

        void FinishRaid(PvpBattle pb, Battle battle, PvpReward reward)
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

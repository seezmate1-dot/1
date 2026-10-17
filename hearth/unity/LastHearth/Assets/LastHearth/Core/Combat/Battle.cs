using System;
using System.Collections.Generic;
using System.Linq;

namespace LastHearth.Core.Combat
{
    /// <summary>Small deterministic generator: the same seed gives the same battle everywhere.</summary>
    public sealed class Rng
    {
        ulong state;

        public Rng(int seed)
        {
            state = (ulong)(seed * 2654435761L) ^ 0x9E3779B97F4A7C15UL;
            if (state == 0)
                state = 1;
        }

        public double Next()
        {
            state ^= state << 13;
            state ^= state >> 7;
            state ^= state << 17;
            return (state >> 11) * (1.0 / (1UL << 53));
        }
    }

    public sealed class HeroSetup
    {
        public int Id;
        public string Name = "";
        public HeroClass Class;
        public int Level = 1;
        public int SkillLevel = 1;
        public int[] Stats = new int[5];
        /// <summary>Health carried over from an earlier battle (Portal floors), share of the maximum.</summary>
        public double HpShare = 1;
        /// <summary>Event blessings and modifiers.</summary>
        public double AttackMult = 1;
        public double HpMult = 1;
        /// <summary>The companion who fights next to the hero (GDD 8.2); null = none.</summary>
        public CompanionSetup Companion;
        /// <summary>What the hero's equipment adds (GameSession.HeroGear); null = nothing at all.</summary>
        public GearBonus Gear;
    }

    /// <summary>
    /// GDD 8: what a hero's equipment adds in battle — the items, their sets and the forge's issued pieces in
    /// the empty slots. Attack and Health are shares of the hero's base (a kit of the hearth level adds about
    /// 1.05^(L-1)); the rest are the extra stats.
    /// </summary>
    public sealed class GearBonus
    {
        public double Attack;
        public double Health;
        public double Crit;
        public double Dodge;
        public double Haste;
        public double Armor;
        public double Healing;
        public double MonsterDamage;
        public double SkillPower;

        public void Add(ItemStat stat, double value)
        {
            switch (stat)
            {
                case ItemStat.Attack: Attack += value; break;
                case ItemStat.Health: Health += value; break;
                case ItemStat.Crit: Crit += value; break;
                case ItemStat.Dodge: Dodge += value; break;
                case ItemStat.Haste: Haste += value; break;
                case ItemStat.Armor: Armor += value; break;
                case ItemStat.Healing: Healing += value; break;
                case ItemStat.MonsterDamage: MonsterDamage += value; break;
                default: SkillPower += value; break;
            }
        }

        public double Get(ItemStat stat)
        {
            switch (stat)
            {
                case ItemStat.Attack: return Attack;
                case ItemStat.Health: return Health;
                case ItemStat.Crit: return Crit;
                case ItemStat.Dodge: return Dodge;
                case ItemStat.Haste: return Haste;
                case ItemStat.Armor: return Armor;
                case ItemStat.Healing: return Healing;
                case ItemStat.MonsterDamage: return MonsterDamage;
                default: return SkillPower;
            }
        }
    }

    /// <summary>GDD 8.2: the simple skill of a hero's companion.</summary>
    public enum CompanionSkill
    {
        /// <summary>Always on: the hero takes x(1 - Power) damage.</summary>
        Guard,
        /// <summary>Every Every ticks: a bite for Power x the hero's attack.</summary>
        Bite,
        /// <summary>Every Every ticks: Power stagger to the boss.</summary>
        Stagger,
        /// <summary>Every Every ticks: heals the hero by Power x health.</summary>
        Heal,
    }

    public sealed class CompanionSetup
    {
        public string Name = "";
        public CompanionSkill Skill;
        public int Every;
        public double Power;
    }

    /// <summary>One enemy of a battle. Campaign battles have several waves; a hunt has one boss.</summary>
    public sealed class WaveSpec
    {
        public MonsterFamily Family;
        public int Rank = 1;
        public double HpMult = 1;
        public double AttackMult = 1;
        /// <summary>Only bosses and elites telegraph heavy strikes.</summary>
        public bool Telegraphs = true;
        /// <summary>PvP: health and attack follow the player's own squad (power ±10%), not the rank.</summary>
        public bool Mirror;
    }

    public sealed class BattleSetup
    {
        public List<HeroSetup> Heroes = new List<HeroSetup>();
        /// <summary>Empty: one boss of Family and Rank (the Hunt).</summary>
        public List<WaveSpec> Waves = new List<WaveSpec>();
        public MonsterFamily Family;
        public int Rank = 1;
        public int Seed = 1;
        /// <summary>Tournament rules of the day: attack of a class, healing of the priest.</summary>
        public Dictionary<HeroClass, double> ClassAttack = new Dictionary<HeroClass, double>();
        public double HealMult = 1;
        /// <summary>GDD 8.1: the active pet of the fortress. Attack and health of the whole squad, or a strike
        /// every PetEvery ticks for PetStrike x the squad's average attack.</summary>
        public string PetName = "";
        public double PetAttack = 1;
        public double PetHp = 1;
        public double PetStrike;
        public int PetEvery;
        /// <summary>MODES.md, 1: what wins the battle (killing every wave, holding the point, bringing the refugees through).</summary>
        public BattleObjective Objective;
        /// <summary>Hold and Escort: the battle is won when this many ticks pass (the point held, the refugees arrived).</summary>
        public int HoldTicks;
        /// <summary>Escort: the refugees' health as a share of the squad's average hero health.</summary>
        public double EscortHp;
        /// <summary>Escort: every this-many blow of the enemy goes at the refugees.</summary>
        public int EscortEvery;
    }

    /// <summary>MODES.md, 1: the kinds of campaign battles by what wins them.</summary>
    public enum BattleObjective
    {
        /// <summary>Kill every wave.</summary>
        Kill,
        /// <summary>«Оборона»: stand HoldTicks with anyone alive (or kill every wave sooner).</summary>
        Hold,
        /// <summary>«Сопровождение»: the refugees walk HoldTicks through the field and must live.</summary>
        Escort,
    }

    public sealed class Fighter
    {
        public int Id;
        public string Name = "";
        public HeroClass Class;
        public Line Line;
        public double MaxHp;
        public double Hp;
        public double Attack;
        public double Armor;
        public double Crit;
        public int Interval;
        public int NextAttack;
        public int Charge;
        public int ChargeMax;
        public double SkillMult;
        public CompanionSetup Companion;
        public int CompanionNext;
        /// <summary>Equipment: chance that a blow misses, healing done and received, damage against monsters,
        /// and the crit and armor the items add (the ghosts of PvP copy them).</summary>
        public double Dodge;
        public double Healing;
        public double MonsterDamage;
        public double GearCrit;
        public double GearArmor;
        public double GearSkill;
        /// <summary>Blows that missed the hero (dodge).</summary>
        public int Dodged;

        public bool Alive => Hp > 0;
        public bool Ready => Alive && Charge >= ChargeMax;
    }

    public enum CommandKind
    {
        Skill,
        Retreat,
    }

    public struct BattleCommand
    {
        public CommandKind Kind;
        public int HeroId;

        public static BattleCommand Skill(int heroId) => new BattleCommand { Kind = CommandKind.Skill, HeroId = heroId };
        public static BattleCommand Retreat() => new BattleCommand { Kind = CommandKind.Retreat };
    }

    public sealed class Telegraph
    {
        public Mechanic Mechanic;
        public int StartTick;
        public int ImpactTick;
        public int TargetId;
    }

    /// <summary>
    /// ACTIVITIES.md, 1: a battle of up to 90 seconds in ticks of 0.1 s. Heroes hit on their own; the
    /// player (or a brain) uses skills and answers the telegraphed heavy strikes of the boss.
    /// </summary>
    public sealed class Battle
    {
        readonly CombatBalance c;
        FamilyData family;
        readonly List<WaveSpec> waves;
        bool telegraphs;
        bool mirror;
        readonly Rng rng;
        readonly Queue<BattleCommand> commands = new Queue<BattleCommand>();
        int bossNext;
        int nextTelegraph;
        int mechanicIndex;
        double burnPerTick;

        public readonly List<Fighter> Heroes = new List<Fighter>();
        public readonly List<string> Log = new List<string>();
        public int Wave;
        public int WaveCount => waves.Count;
        public double BossMaxHp;
        public double BossHp;
        public double BossAttack;
        public double BossArmor;
        public int BossInterval;
        public double Stagger;
        public int StunnedUntil;
        public int BurnUntil;
        public int WallUntil;
        public int RetreatUntil;
        public int RetreatReadyAt;
        public int HasteUntil;
        public Telegraph Pending;
        public int Tick;
        public bool Over;
        public bool Won;
        public int HeavyLanded;
        public int HeavyCountered;
        /// <summary>All damage the squad dealt (event scores).</summary>
        public double DamageDealt;
        double healMult = 1;
        /// <summary>MODES.md, 1: what wins this battle; Hold and Escort end at HoldTicks.</summary>
        public readonly BattleObjective Objective;
        public readonly int HoldTicks;
        /// <summary>Escort: the refugees (they do not fight; the Wall covers them too).</summary>
        public double EscortMaxHp;
        public double EscortHp;
        public bool EscortLost;
        /// <summary>Escort: the enemy's blows that went at the refugees.</summary>
        public int EscortHits;
        readonly int escortEvery;
        int enemyBlows;
        readonly double petStrike;
        readonly int petEvery;
        int petNext;

        /// <summary>Decides commands every tick; null for a battle driven only by Command().</summary>
        public Func<Battle, IEnumerable<BattleCommand>> Brain;

        public CombatBalance Numbers => c;
        public FamilyData Family => family;
        public bool BossStunned => Tick < StunnedUntil;
        public bool Burning => Tick < BurnUntil;
        public bool WallUp => Tick < WallUntil;
        public bool Retreated => Tick < RetreatUntil;
        public double Rand() => rng.Next();
        /// <summary>Hold and Escort: the share of the time already stood (0..1); Kill: 0.</summary>
        public double HoldProgress => HoldTicks <= 0 ? 0 : Math.Min(1, (double)Tick / HoldTicks);

        public int Stars
        {
            get
            {
                if (!Won)
                    return 0;
                return 1 + (HeavyLanded == 0 ? 1 : 0) + (Tick <= c.StarTicks ? 1 : 0);
            }
        }

        public Battle(CombatBalance numbers, BattleSetup setup)
        {
            c = numbers;
            rng = new Rng(setup.Seed);
            waves = setup.Waves.Count > 0
                ? setup.Waves
                : new List<WaveSpec> { new WaveSpec { Family = setup.Family, Rank = setup.Rank } };
            healMult = setup.HealMult;
            foreach (var h in setup.Heroes)
            {
                var f = MakeHero(h);
                if (setup.ClassAttack.TryGetValue(h.Class, out double mult))
                    f.Attack *= mult;
                f.Attack *= setup.PetAttack;
                f.MaxHp *= setup.PetHp;
                f.Hp *= setup.PetHp;
                f.Companion = h.Companion;
                f.CompanionNext = h.Companion != null && h.Companion.Every > 0 ? h.Companion.Every : int.MaxValue;
                Heroes.Add(f);
            }
            petStrike = setup.PetStrike;
            petEvery = setup.PetEvery;
            petNext = petEvery > 0 && petStrike > 0 ? petEvery : int.MaxValue;
            Objective = setup.Objective;
            HoldTicks = Objective == BattleObjective.Kill ? 0 : Math.Max(1, setup.HoldTicks);
            if (Objective == BattleObjective.Escort)
            {
                EscortMaxHp = EscortHp = Math.Max(0.1, setup.EscortHp) * (Heroes.Count == 0 ? 100 : Heroes.Average(h => h.MaxHp));
                escortEvery = Math.Max(1, setup.EscortEvery);
            }
            StartWave(0);
        }

        void StartWave(int index)
        {
            Wave = index;
            var w = waves[index];
            var boss = c.Boss;
            family = c.Family(w.Family);
            telegraphs = w.Telegraphs;
            mirror = w.Mirror;
            if (w.Mirror)
            {
                // health: 100 ticks of the squad's damage; one hit: 5% of the squad's health (at x1).
                // The ghost has pets and companions too: their strikes count in the squad's damage.
                BossMaxHp = BossHp = family.Hp * w.HpMult * 100 * SquadDamagePerTick();
                BossAttack = family.Attack * w.AttackMult * Heroes.Sum(h => h.MaxHp) / 20 * CompanionToughness();
            }
            else
            {
                BossMaxHp = BossHp = boss.Hp * family.Hp * w.HpMult * Math.Pow(boss.HpGrowth, w.Rank - 1);
                BossAttack = boss.Attack * family.Attack * w.AttackMult * Math.Pow(boss.AttackGrowth, w.Rank - 1);
            }
            BossArmor = boss.Armor * family.Armor;
            BossInterval = Math.Max(1, (int)Math.Round(boss.Interval * family.Interval));
            bossNext = Tick + BossInterval;
            nextTelegraph = Tick + boss.FirstTelegraph;
            Stagger = 0;
            StunnedUntil = Tick;
            BurnUntil = Tick;
            Pending = null;
            if (index > 0)
                Log.Add(Tick + ": волна " + (index + 1) + " из " + waves.Count);
        }

        /// <summary>Bites and the pet's strikes count at half in the ghost's health: the basis of the mirror leaves out
        /// the skills, which they do not grow, and they land only while the hero stands (calibrated on the 30-day bot:
        /// the same arena win rate as before the kennel).</summary>
        const double KennelInMirror = 0.5;

        /// <summary>How strongly the ghost of PvP copies the items' extra stats (crit, skill power, dodge, armor, healing):
        /// they also work on the skills and the heavy strikes the mirror leaves out, so they count more than their share of
        /// the basic blows (calibrated on the 30-day bot: the same arena rating with and without items).</summary>
        const double GhostGearWeight = 2.0;

        /// <summary>The squad's damage per tick without skills: basic attacks, bites, the pet's strikes.</summary>
        double SquadDamagePerTick()
        {
            double sum = 0;
            foreach (var h in Heroes)
            {
                // the crit and the skill power of the items count
                sum += h.Attack / h.Interval * (1 + GhostGearWeight * (h.GearCrit * (c.CritMult - 1) + 0.5 * h.GearSkill));
                if (h.Companion != null && h.Companion.Skill == CompanionSkill.Bite && h.Companion.Every > 0)
                    sum += KennelInMirror * h.Attack * h.Companion.Power / h.Companion.Every;
            }
            if (petNext != int.MaxValue && Heroes.Count > 0)
                sum += KennelInMirror * petStrike * Heroes.Average(h => h.Attack) / petEvery;
            return sum;
        }

        /// <summary>How much longer the squad stands thanks to companions (guards take less, healers mend):
        /// a ghost of the same strength hits that much harder.</summary>
        double CompanionToughness()
        {
            if (Heroes.Count == 0)
                return 1;
            double sum = 0;
            foreach (var h in Heroes)
            {
                double t = 1;
                var comp = h.Companion;
                if (comp != null && comp.Skill == CompanionSkill.Guard)
                    t /= 1 - Math.Min(0.5, comp.Power);
                if (comp != null && comp.Skill == CompanionSkill.Heal && comp.Every > 0)
                    t *= 1 + comp.Power * c.MaxTicks / 4.0 / comp.Every; // a quarter of a battle of healing
                if (comp != null && comp.Skill == CompanionSkill.Stagger && comp.Every > 0)
                    t *= 1 + comp.Power / comp.Every * c.MaxTicks / 1000.0; // the stuns it adds
                // the dodge and the armor of the items: the ghost wears the same gear
                t *= 1 + GhostGearWeight * ((100 + h.Armor) / (100 + h.Armor - h.GearArmor) - 1 + h.Dodge + 0.5 * h.Healing);
                sum += t;
            }
            return sum / Heroes.Count;
        }

        Fighter MakeHero(HeroSetup h) => HeroFighter(c, h);

        /// <summary>A hero as the battle makes them (the hero screen shows the same numbers): the level, the main
        /// stat and the equipment (GDD 8: items, sets, the forge's issued pieces).</summary>
        public static Fighter HeroFighter(CombatBalance c, HeroSetup h)
        {
            var cls = c.Class(h.Class);
            var gear = h.Gear ?? new GearBonus();
            double growth = Math.Pow(c.LevelGrowth, h.Level - 1);
            double hp = cls.Hp * growth * (1 + gear.Health) * h.HpMult;
            double main = h.Stats[(int)cls.MainStat];
            double agility = h.Stats[(int)Stat.Agility];
            int interval = Math.Max(1, (int)Math.Round(cls.Interval / (1 + Math.Max(0, gear.Haste))));
            return new Fighter
            {
                Id = h.Id,
                Name = h.Name,
                Class = h.Class,
                Line = cls.Line,
                MaxHp = hp,
                Hp = hp * Math.Max(0, Math.Min(1, h.HpShare)),
                Attack = cls.Attack * growth * (1 + gear.Attack) * (1 + main * c.StatBonus) * h.AttackMult,
                // the main stats of items add health and attack; armor comes only from extra stats and sets,
                // so strikes stay dangerous at every rank
                Armor = cls.Armor + gear.Armor,
                Crit = c.CritBase + Math.Max(0, agility - 10) * c.CritPerAgility + gear.Crit,
                Interval = interval,
                NextAttack = interval,
                ChargeMax = cls.SkillCharge,
                Charge = cls.SkillCharge / 2, // battles start with half a charge, so the first strike can be answered
                SkillMult = (1 + (h.SkillLevel - 1) * c.SkillPerLevel) * (1 + gear.SkillPower),
                Dodge = Math.Min(0.5, Math.Max(0, gear.Dodge)),
                Healing = gear.Healing,
                MonsterDamage = gear.MonsterDamage,
                GearCrit = gear.Crit,
                GearArmor = gear.Armor,
                GearSkill = gear.SkillPower,
            };
        }

        public Fighter Hero(int id) => Heroes.Find(h => h.Id == id);

        public void Command(BattleCommand command) => commands.Enqueue(command);

        public void RunToEnd()
        {
            while (!Over)
                Step();
        }

        // ------------------------------------------------------------------ one tick

        public void Step()
        {
            if (Over)
                return;
            if (Brain != null)
            {
                foreach (var cmd in Brain(this))
                    commands.Enqueue(cmd);
            }
            while (commands.Count > 0)
                Apply(commands.Dequeue());

            Tick++;
            if (Burning)
                HitBoss(burnPerTick, null, false);

            foreach (var h in Heroes)
            {
                if (!h.Alive)
                    continue;
                if (h.Charge < h.ChargeMax)
                    h.Charge++;
                int interval = Tick < HasteUntil ? (int)Math.Round(h.Interval / (1 + c.Skills.Haste)) : h.Interval;
                if (Tick >= h.NextAttack)
                {
                    BasicAttack(h);
                    h.NextAttack = Tick + interval;
                }
                if (Tick >= h.CompanionNext)
                {
                    CompanionActs(h);
                    h.CompanionNext = Tick + h.Companion.Every;
                }
            }
            if (Tick >= petNext && BossHp > 0)
            {
                var alive = Heroes.FindAll(x => x.Alive);
                if (alive.Count > 0)
                    HitBoss(petStrike * alive.Average(x => x.Attack), null, false);
                petNext = Tick + petEvery;
            }

            if (!BossStunned && BossHp > 0)
                BossActs();
            CheckEnd();
        }

        void Apply(BattleCommand cmd)
        {
            if (cmd.Kind == CommandKind.Retreat)
            {
                if (Tick >= RetreatReadyAt)
                {
                    RetreatUntil = Tick + c.RetreatTicks;
                    RetreatReadyAt = Tick + c.RetreatCooldown;
                    Log.Add(Tick + ": отход передней линии");
                }
                return;
            }
            var h = Hero(cmd.HeroId);
            if (h == null || !h.Ready)
                return;
            h.Charge = 0;
            UseSkill(h);
        }

        void UseSkill(Fighter h)
        {
            var s = c.Skills;
            switch (h.Class)
            {
                case HeroClass.Guardian:
                    WallUntil = Tick + s.WallTicks;
                    Log.Add(Tick + ": " + h.Name + " — Стена");
                    break;
                case HeroClass.Swordsman:
                    HitBoss(h.Attack * s.CounterMult * h.SkillMult, h, BossStunned);
                    if (WallUp)
                    {
                        Log.Add(Tick + ": связка Стена + Ответный выпад — контратака отряда");
                        foreach (var other in Heroes)
                        {
                            if (other.Alive && other != h && other.Class != HeroClass.CoalPriest)
                                HitBoss(other.Attack, other, false);
                        }
                    }
                    break;
                case HeroClass.Cleaver:
                    HitBoss(h.Attack * 1.5 * h.SkillMult, h, false);
                    AddStagger(s.CrushStagger * h.SkillMult);
                    break;
                case HeroClass.Archer:
                    double volley = h.Attack * s.VolleyMult * h.SkillMult;
                    if (Burning)
                    {
                        volley *= s.ExplosionMult;
                        BurnUntil = Tick;
                        Log.Add(Tick + ": связка поджог + Залп — взрыв углей");
                    }
                    HitBoss(volley, h, false);
                    break;
                case HeroClass.CoalPriest:
                    foreach (var other in Heroes)
                    {
                        if (other.Alive)
                            Heal(h, other, other.MaxHp * s.HealShare * h.SkillMult * healMult);
                    }
                    HasteUntil = Tick + s.HasteTicks;
                    Log.Add(Tick + ": " + h.Name + " — Тёплые руки");
                    break;
                case HeroClass.AshMage:
                    HitBoss(h.Attack * s.FireballMult * h.SkillMult, h, false);
                    double fire = family.Weak == HeroClass.AshMage ? family.WeakMult : 1;
                    burnPerTick = h.Attack * s.BurnShare * h.SkillMult * fire;
                    BurnUntil = Tick + s.BurnTicks;
                    break;
            }
        }

        /// <summary>GDD 8.2: the companion acts while its hero stands (no luck involved: battles stay replayable).</summary>
        void CompanionActs(Fighter h)
        {
            var comp = h.Companion;
            switch (comp.Skill)
            {
                case CompanionSkill.Bite:
                    if (BossHp > 0)
                        HitBoss(h.Attack * comp.Power, null, false);
                    break;
                case CompanionSkill.Stagger:
                    if (BossHp > 0)
                        AddStagger(comp.Power);
                    break;
                case CompanionSkill.Heal:
                    Heal(null, h, h.MaxHp * comp.Power * healMult);
                    break;
            }
        }

        void BasicAttack(Fighter h)
        {
            if (h.Class == HeroClass.CoalPriest)
            {
                Fighter low = null;
                foreach (var other in Heroes)
                {
                    if (other.Alive && other.Hp < other.MaxHp && (low == null || other.Hp / other.MaxHp < low.Hp / low.MaxHp))
                        low = other;
                }
                if (low != null)
                    Heal(h, low, h.Attack * c.Skills.PriestBasicHeal * healMult);
                return;
            }
            bool crit = rng.Next() < h.Crit;
            HitBoss(h.Attack * (crit ? c.CritMult : 1), h, h.Class == HeroClass.Swordsman && BossStunned);
            if (h.Class == HeroClass.Cleaver)
                AddStagger(c.Boss.StaggerPerHit);
        }

        /// <summary>Healing from a hero (null: a companion) to a hero; the items' «лечение» of both counts.</summary>
        static void Heal(Fighter from, Fighter to, double amount)
        {
            amount *= (1 + (from?.Healing ?? 0)) * (1 + to.Healing);
            to.Hp = Math.Min(to.MaxHp, to.Hp + amount);
        }

        void HitBoss(double damage, Fighter from, bool stunCrit)
        {
            if (from != null && from.Class == family.Weak)
                damage *= family.WeakMult;
            if (from != null && !mirror)
                damage *= 1 + from.MonsterDamage; // «урон по чудовищам»: not against the ghosts of PvP
            if (stunCrit)
                damage *= c.Skills.StunCritMult;
            if (BossStunned)
                damage *= 1 + c.Boss.StunDamageBonus;
            double dealt = damage * 100 / (100 + BossArmor);
            BossHp -= dealt;
            DamageDealt += dealt;
        }

        void AddStagger(double amount)
        {
            if (BossStunned)
                return;
            Stagger += amount * family.StaggerMult;
            if (Stagger < 100)
                return;
            Stagger = 0;
            StunnedUntil = Tick + c.Boss.StunTicks;
            if (Pending != null)
            {
                HeavyCountered++;
                Log.Add(Tick + ": сильный удар сорван оглушением");
                Pending = null;
            }
            else
            {
                Log.Add(Tick + ": босс оглушён");
            }
        }

        // ------------------------------------------------------------------ boss

        void BossActs()
        {
            if (Pending != null && Tick >= Pending.ImpactTick)
                Impact(Pending);
            if (telegraphs && Pending == null && Tick >= nextTelegraph)
            {
                var mechanic = family.Mechanics[mechanicIndex++ % family.Mechanics.Count];
                var target = mechanic == Mechanic.Grab ? BackMost() : null;
                Pending = new Telegraph
                {
                    Mechanic = mechanic,
                    StartTick = Tick,
                    ImpactTick = Tick + c.Boss.Windup,
                    TargetId = target?.Id ?? -1,
                };
                nextTelegraph = Tick + c.Boss.TelegraphEvery;
                Log.Add(Tick + ": босс готовит " + mechanic);
            }
            if (Tick >= bossNext)
            {
                if (EscortHp > 0 && ++enemyBlows % escortEvery == 0)
                {
                    HitEscort(BossAttack);
                }
                else
                {
                    var target = FrontMost();
                    if (target != null)
                        HitHero(target, BossAttack);
                }
                bossNext = Tick + BossInterval;
            }
        }

        void Impact(Telegraph t)
        {
            Pending = null;
            var b = c.Boss;
            bool countered = WallUp;
            switch (t.Mechanic)
            {
                case Mechanic.Smash:
                    if (Retreated)
                    {
                        HeavyCountered++;
                        Log.Add(Tick + ": удар по передней линии мимо — отход");
                        return;
                    }
                    foreach (var h in LineTargets())
                        HitHero(h, BossAttack * b.Smash);
                    break;
                case Mechanic.Wave:
                    foreach (var h in Heroes)
                    {
                        if (h.Alive)
                            HitHero(h, BossAttack * b.Wave);
                    }
                    break;
                case Mechanic.Grab:
                    var target = Hero(t.TargetId);
                    if (target == null || !target.Alive)
                        target = BackMost();
                    if (target != null)
                        HitHero(target, BossAttack * b.Grab);
                    break;
            }
            if (countered)
            {
                HeavyCountered++;
                Log.Add(Tick + ": " + t.Mechanic + " принят на Стену");
            }
            else
            {
                HeavyLanded++;
                Log.Add(Tick + ": " + t.Mechanic + " попал");
            }
        }

        void HitHero(Fighter h, double damage)
        {
            // dodge: the generator is asked only when the hero has it, so battles without it stay the same
            if (h.Dodge > 0 && rng.Next() < h.Dodge)
            {
                h.Dodged++;
                return;
            }
            if (WallUp)
                damage *= 1 - c.Skills.WallReduction;
            if (h.Companion != null && h.Companion.Skill == CompanionSkill.Guard)
                damage *= 1 - Math.Min(0.5, h.Companion.Power);
            h.Hp -= damage * 100 / (100 + h.Armor);
            if (!h.Alive)
                Log.Add(Tick + ": пал " + h.Name);
        }

        /// <summary>Escort: a blow at the refugees; the Wall covers them as well as the squad.</summary>
        void HitEscort(double damage)
        {
            if (WallUp)
                damage *= 1 - c.Skills.WallReduction;
            EscortHits++;
            EscortHp -= damage;
            if (EscortHp <= 0)
            {
                EscortHp = 0;
                EscortLost = true;
                Log.Add(Tick + ": беженцы пали");
            }
        }

        /// <summary>The line the boss hits: front first; during a retreat the middle steps forward.</summary>
        List<Fighter> LineTargets()
        {
            Line[] order = Retreated
                ? new[] { Line.Middle, Line.Front, Line.Back }
                : new[] { Line.Front, Line.Middle, Line.Back };
            foreach (var line in order)
            {
                var list = Heroes.FindAll(h => h.Alive && h.Line == line);
                if (list.Count > 0)
                    return list;
            }
            return new List<Fighter>();
        }

        Fighter FrontMost()
        {
            var line = LineTargets();
            return line.Count > 0 ? line[0] : null;
        }

        Fighter BackMost()
        {
            foreach (var line in new[] { Line.Back, Line.Middle, Line.Front })
            {
                var h = Heroes.Find(x => x.Alive && x.Line == line);
                if (h != null)
                    return h;
            }
            return null;
        }

        void CheckEnd()
        {
            if (BossHp <= 0 && Wave + 1 < waves.Count)
            {
                StartWave(Wave + 1);
            }
            else if (BossHp <= 0)
            {
                Over = true;
                Won = true;
                Log.Add(Tick + ": победа");
            }
            else if (!Heroes.Exists(h => h.Alive))
            {
                Over = true;
                Log.Add(Tick + ": отряд пал");
            }
            else if (EscortLost)
            {
                Over = true;
            }
            else if (HoldTicks > 0 && Tick >= HoldTicks)
            {
                Over = true;
                Won = true;
                Log.Add(Tick + (Objective == BattleObjective.Escort ? ": беженцы дошли — победа" : ": точка удержана — победа"));
            }
            else if (Tick >= c.MaxTicks)
            {
                Over = true;
                Log.Add(Tick + ": время вышло");
            }
        }
    }
}

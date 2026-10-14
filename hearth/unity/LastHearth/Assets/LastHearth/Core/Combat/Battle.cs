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
        /// <summary>Gear quality follows the hearth level (ECONOMY.md, section 6).</summary>
        public int GearLevel = 1;
        public int Seed = 1;
        /// <summary>Tournament rules of the day: attack of a class, healing of the priest.</summary>
        public Dictionary<HeroClass, double> ClassAttack = new Dictionary<HeroClass, double>();
        public double HealMult = 1;
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

        /// <summary>Decides commands every tick; null for a battle driven only by Command().</summary>
        public Func<Battle, IEnumerable<BattleCommand>> Brain;

        public CombatBalance Numbers => c;
        public FamilyData Family => family;
        public bool BossStunned => Tick < StunnedUntil;
        public bool Burning => Tick < BurnUntil;
        public bool WallUp => Tick < WallUntil;
        public bool Retreated => Tick < RetreatUntil;
        public double Rand() => rng.Next();

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
                var f = MakeHero(h, setup.GearLevel);
                if (setup.ClassAttack.TryGetValue(h.Class, out double mult))
                    f.Attack *= mult;
                Heroes.Add(f);
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
            if (w.Mirror)
            {
                // health: 100 ticks of the squad's damage; one hit: 5% of the squad's health (at x1)
                BossMaxHp = BossHp = family.Hp * w.HpMult * 100 * Heroes.Sum(h => h.Attack / h.Interval);
                BossAttack = family.Attack * w.AttackMult * Heroes.Sum(h => h.MaxHp) / 20;
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

        Fighter MakeHero(HeroSetup h, int gearLevel)
        {
            var cls = c.Class(h.Class);
            double growth = Math.Pow(c.LevelGrowth, h.Level - 1) * Math.Pow(c.GearGrowth, gearLevel - 1);
            double main = h.Stats[(int)cls.MainStat];
            double agility = h.Stats[(int)Stat.Agility];
            return new Fighter
            {
                Id = h.Id,
                Name = h.Name,
                Class = h.Class,
                Line = cls.Line,
                MaxHp = cls.Hp * growth * h.HpMult,
                Hp = cls.Hp * growth * h.HpMult * Math.Max(0, Math.Min(1, h.HpShare)),
                Attack = cls.Attack * growth * (1 + main * c.StatBonus) * h.AttackMult,
                Armor = cls.Armor, // gear adds health and attack, not armor: strikes stay dangerous at every rank
                Crit = c.CritBase + Math.Max(0, agility - 10) * c.CritPerAgility,
                Interval = cls.Interval,
                NextAttack = cls.Interval,
                ChargeMax = cls.SkillCharge,
                Charge = cls.SkillCharge / 2, // battles start with half a charge, so the first strike can be answered
                SkillMult = 1 + (h.SkillLevel - 1) * c.SkillPerLevel,
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
                            other.Hp = Math.Min(other.MaxHp, other.Hp + other.MaxHp * s.HealShare * h.SkillMult * healMult);
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
                    low.Hp = Math.Min(low.MaxHp, low.Hp + h.Attack * c.Skills.PriestBasicHeal * healMult);
                return;
            }
            bool crit = rng.Next() < h.Crit;
            HitBoss(h.Attack * (crit ? c.CritMult : 1), h, h.Class == HeroClass.Swordsman && BossStunned);
            if (h.Class == HeroClass.Cleaver)
                AddStagger(c.Boss.StaggerPerHit);
        }

        void HitBoss(double damage, Fighter from, bool stunCrit)
        {
            if (from != null && from.Class == family.Weak)
                damage *= family.WeakMult;
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
                var target = FrontMost();
                if (target != null)
                    HitHero(target, BossAttack);
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
            if (WallUp)
                damage *= 1 - c.Skills.WallReduction;
            h.Hp -= damage * 100 / (100 + h.Armor);
            if (!h.Alive)
                Log.Add(Tick + ": пал " + h.Name);
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
            else if (Tick >= c.MaxTicks)
            {
                Over = true;
                Log.Add(Tick + ": время вышло");
            }
        }
    }
}
